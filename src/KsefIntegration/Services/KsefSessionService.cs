using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KsefIntegration.Abstractions;
using KsefIntegration.Infrastructure;
using KsefIntegration.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Security;

namespace KsefIntegration.Services
{
    public sealed class KsefSessionService : IKsefSessionService
    {
        private readonly HttpClient _httpClient;
        private readonly KsefSettings _settings;
        private readonly SemaphoreSlim _authLock = new SemaphoreSlim(1, 1);

        private KsefSessionTokens? _tokens;

        public KsefSessionService(HttpClient httpClient, KsefSettings settings)
        {
            KsefArgumentValidator.ValidateSettings(settings);

            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _settings = settings;

            if (_settings.RequestTimeoutSeconds > 0)
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds);
            }
        }

        public void Invalidate()
        {
            _tokens = null;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (HasValidAccessToken())
            {
                return _tokens!.AccessToken;
            }

            await _authLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (HasValidAccessToken())
                {
                    return _tokens!.AccessToken;
                }

                if (HasValidRefreshToken())
                {
                    try
                    {
                        await RefreshAccessTokenAsync(cancellationToken).ConfigureAwait(false);
                        if (HasValidAccessToken())
                        {
                            return _tokens!.AccessToken;
                        }
                    }
                    catch
                    {
                        _tokens = null;
                    }
                }

                await AuthenticateWithKsefTokenAsync(cancellationToken).ConfigureAwait(false);

                if (!HasValidAccessToken())
                {
                    throw new InvalidOperationException("KSeF authentication finished without a valid access token.");
                }

                return _tokens!.AccessToken;
            }
            finally
            {
                _authLock.Release();
            }
        }

        private bool HasValidAccessToken()
        {
            return _tokens != null
                && !string.IsNullOrWhiteSpace(_tokens.AccessToken)
                && _tokens.AccessTokenExpirationDate > DateTimeOffset.UtcNow.AddSeconds(30);
        }

        private bool HasValidRefreshToken()
        {
            return _tokens != null
                && !string.IsNullOrWhiteSpace(_tokens.RefreshToken)
                && _tokens.RefreshTokenExpirationDate > DateTimeOffset.UtcNow.AddSeconds(30);
        }

        private async Task RefreshAccessTokenAsync(CancellationToken cancellationToken)
        {
            var payload = new JObject
            {
                ["refreshToken"] = _tokens!.RefreshToken,
                ["grantType"] = "refresh_token",
            };

            var refreshResponse = await PostJsonAsync("/auth/token/refresh", payload, null, cancellationToken)
                .ConfigureAwait(false);

            _tokens = ParseSessionTokens(refreshResponse);
        }

        private async Task AuthenticateWithKsefTokenAsync(CancellationToken cancellationToken)
        {
            var challengePayload = new JObject
            {
                ["contextIdentifier"] = CreateContextIdentifier(),
            };

            var challengeResponse = await PostJsonAsync("/auth/challenge", challengePayload, null, cancellationToken)
                .ConfigureAwait(false);

            var challenge = GetRequiredString(challengeResponse, "challenge");
            var challengeTimestamp = GetRequiredString(challengeResponse, "timestamp");

            var publicKeyResponse = await GetJsonAsync("/certificates/public-key", null, cancellationToken)
                .ConfigureAwait(false);
            var publicKey = GetRequiredString(publicKeyResponse, "value");

            var encryptedToken = EncryptKsefToken(_settings.KsefToken, challengeTimestamp, publicKey);

            var initPayload = new JObject
            {
                ["contextIdentifier"] = CreateContextIdentifier(),
                ["challenge"] = challenge,
                ["encryptedToken"] = encryptedToken,
            };

            var initResponse = await PostJsonAsync("/auth/ksef-token", initPayload, null, cancellationToken)
                .ConfigureAwait(false);

            var referenceNumber = GetRequiredString(initResponse, "referenceNumber");

            await WaitForAuthCompletionAsync(referenceNumber, cancellationToken).ConfigureAwait(false);

            var redeemHeaders = new Dictionary<string, string>
            {
                ["Reference-Number"] = referenceNumber,
            };

            var redeemResponse = await PostJsonAsync("/auth/token/redeem", new JObject(), redeemHeaders, cancellationToken)
                .ConfigureAwait(false);

            _tokens = ParseSessionTokens(redeemResponse);
        }

        private async Task WaitForAuthCompletionAsync(string referenceNumber, CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= _settings.AuthStatusMaxAttempts; attempt++)
            {
                var statusResponse = await GetJsonAsync($"/auth/{Uri.EscapeDataString(referenceNumber)}", null, cancellationToken)
                    .ConfigureAwait(false);

                var status = statusResponse["status"]?.ToString();
                if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    var reason = statusResponse["details"]?[0]?["description"]?.ToString();
                    throw new InvalidOperationException($"KSeF authentication failed. {reason}".Trim());
                }

                if (attempt == _settings.AuthStatusMaxAttempts)
                {
                    break;
                }

                await Task.Delay(_settings.AuthStatusPollDelayMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out while waiting for KSeF authentication status.");
        }

        private KsefSessionTokens ParseSessionTokens(JObject response)
        {
            var accessToken = GetRequiredString((JObject?)response["accessToken"], "token");
            var accessExpiration = GetRequiredString((JObject?)response["accessToken"], "expirationDate");
            var refreshToken = GetRequiredString((JObject?)response["refreshToken"], "token");
            var refreshExpiration = GetRequiredString((JObject?)response["refreshToken"], "expirationDate");

            return new KsefSessionTokens
            {
                AccessToken = accessToken,
                AccessTokenExpirationDate = ParseDate(accessExpiration),
                RefreshToken = refreshToken,
                RefreshTokenExpirationDate = ParseDate(refreshExpiration),
            };
        }

        private static DateTimeOffset ParseDate(string value)
        {
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Could not parse KSeF date value: '{value}'.");
        }

        private JObject CreateContextIdentifier()
        {
            return new JObject
            {
                ["type"] = _settings.SubjectIdentifierType,
                ["identifier"] = _settings.Nip,
            };
        }

        private async Task<JObject> GetJsonAsync(
            string relativePath,
            IDictionary<string, string>? headers,
            CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(relativePath)))
            {
                ApplyHeaders(request, headers);
                return await SendAndParseAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<JObject> PostJsonAsync(
            string relativePath,
            JObject payload,
            IDictionary<string, string>? headers,
            CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(relativePath)))
            {
                ApplyHeaders(request, headers);
                request.Content = new StringContent(
                    payload.ToString(Formatting.None),
                    Encoding.UTF8,
                    "application/json");

                return await SendAndParseAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<JObject> SendAndParseAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var apiError = ApiErrorInfo.Parse(responseBody);
                    var message = $"KSeF call failed: {(int)response.StatusCode} {response.StatusCode}. {apiError.Description}".Trim();
                    throw new KsefApiException(message, response.StatusCode, apiError.Code, responseBody);
                }

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return new JObject();
                }

                return JObject.Parse(responseBody);
            }
        }

        private static void ApplyHeaders(HttpRequestMessage request, IDictionary<string, string>? headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (var pair in headers)
            {
                request.Headers.Remove(pair.Key);
                request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
            }
        }

        private string BuildUrl(string relativePath)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}",
                _settings.BaseUrl.TrimEnd('/'),
                relativePath.TrimStart('/'));
        }

        private static string GetRequiredString(JObject? source, string propertyName)
        {
            if (source == null)
            {
                throw new InvalidOperationException($"Missing required object for property '{propertyName}'.");
            }

            var value = source[propertyName]?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required property '{propertyName}' in KSeF response.");
            }

            return value;
        }

        private static string EncryptKsefToken(string token, string timestamp, string publicKey)
        {
            var cipherText = BuildTokenCipherText(token, timestamp);
            var keyBytes = Convert.FromBase64String(publicKey);

            var key = PublicKeyFactory.CreateKey(keyBytes);
            var engine = new Pkcs1Encoding(new RsaEngine());
            engine.Init(true, key);

            var input = Encoding.UTF8.GetBytes(cipherText);
            var encrypted = engine.ProcessBlock(input, 0, input.Length);
            var encoded = Convert.ToBase64String(encrypted);

            return SplitIntoLines(encoded, 64);
        }

        private static string BuildTokenCipherText(string token, string timestamp)
        {
            if (DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}|{1}",
                    token,
                    parsed.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}", token, timestamp);
        }

        private static string SplitIntoLines(string value, int lineLength)
        {
            if (lineLength <= 0 || value.Length <= lineLength)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + (value.Length / lineLength));
            for (var i = 0; i < value.Length; i += lineLength)
            {
                var chunkLength = Math.Min(lineLength, value.Length - i);
                builder.Append(value, i, chunkLength);
                if (i + chunkLength < value.Length)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }
    }
}
