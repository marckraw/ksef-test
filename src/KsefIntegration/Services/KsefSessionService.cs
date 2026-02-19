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
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

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
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + _tokens!.RefreshToken,
            };

            var refreshResponse = await PostJsonAsync("/auth/token/refresh", new JObject(), headers, cancellationToken)
                .ConfigureAwait(false);

            _tokens = ParseRefreshedSessionTokens(refreshResponse);
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
            var challengeTimestampMs = ResolveChallengeTimestampMs(challengeResponse);

            var publicKey = await GetTokenEncryptionPublicMaterialAsync(cancellationToken).ConfigureAwait(false);

            var encryptedToken = EncryptKsefToken(_settings.KsefToken, challengeTimestampMs, publicKey);

            var initPayload = new JObject
            {
                ["contextIdentifier"] = CreateContextIdentifier(),
                ["challenge"] = challenge,
                ["encryptedToken"] = encryptedToken,
            };

            var initResponse = await PostJsonAsync("/auth/ksef-token", initPayload, null, cancellationToken)
                .ConfigureAwait(false);

            var referenceNumber = GetRequiredString(initResponse, "referenceNumber");
            var authenticationToken = GetRequiredString((JObject?)initResponse["authenticationToken"], "token");

            await WaitForAuthCompletionAsync(referenceNumber, authenticationToken, cancellationToken).ConfigureAwait(false);

            var redeemHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + authenticationToken,
            };
            var redeemResponse = await PostJsonAsync("/auth/token/redeem", new JObject(), redeemHeaders, cancellationToken)
                .ConfigureAwait(false);

            _tokens = ParseSessionTokens(redeemResponse);
        }

        private async Task WaitForAuthCompletionAsync(string referenceNumber, string authenticationToken, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + authenticationToken,
            };

            for (var attempt = 1; attempt <= _settings.AuthStatusMaxAttempts; attempt++)
            {
                var statusResponse = await GetJsonAsync($"/auth/{Uri.EscapeDataString(referenceNumber)}", headers, cancellationToken)
                    .ConfigureAwait(false);

                var statusCode = statusResponse["status"]?["code"]?.Value<int?>();
                if (statusCode == 200)
                {
                    return;
                }

                if (statusCode.HasValue && statusCode.Value >= 400)
                {
                    var reason = statusResponse["status"]?["description"]?.ToString()
                        ?? statusResponse["details"]?[0]?["description"]?.ToString();
                    throw new InvalidOperationException($"KSeF authentication failed. {reason}".Trim());
                }

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

        private async Task<string> GetTokenEncryptionPublicMaterialAsync(CancellationToken cancellationToken)
        {
            try
            {
                var payload = await GetAnyJsonAsync("/security/public-key-certificates", null, cancellationToken)
                    .ConfigureAwait(false);
                var material = TryExtractPublicMaterialFromSecurityPayload(payload);
                if (!string.IsNullOrWhiteSpace(material))
                {
                    return material;
                }
            }
            catch (KsefApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Fallback to older endpoint.
            }

            var publicKeyResponse = await GetJsonAsync("/certificates/public-key", null, cancellationToken)
                .ConfigureAwait(false);
            return GetRequiredString(publicKeyResponse, "value");
        }

        private static string? TryExtractPublicMaterialFromSecurityPayload(JToken payload)
        {
            if (payload == null)
            {
                return null;
            }

            if (payload.Type == JTokenType.Object)
            {
                var obj = (JObject)payload;
                var direct = obj["certificate"]?.ToString()
                    ?? obj["publicKey"]?.ToString()
                    ?? obj["value"]?.ToString();
                if (!string.IsNullOrWhiteSpace(direct))
                {
                    return direct;
                }
            }

            JArray? certificates = null;

            if (payload.Type == JTokenType.Array)
            {
                certificates = (JArray)payload;
            }
            else
            {
                certificates = payload["certificates"] as JArray
                    ?? payload["items"] as JArray
                    ?? payload["data"] as JArray;
            }

            if (certificates == null || certificates.Count == 0)
            {
                return null;
            }

            foreach (var certificate in certificates)
            {
                var usageToken = certificate["usage"];
                if (usageToken != null)
                {
                    if (usageToken.Type == JTokenType.Array)
                    {
                        foreach (var usage in usageToken)
                        {
                            if (usage != null && usage.ToString().IndexOf("KsefTokenEncryption", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var matched = certificate["certificate"]?.ToString()
                                    ?? certificate["publicKey"]?.ToString()
                                    ?? certificate["value"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(matched))
                                {
                                    return matched;
                                }
                            }
                        }
                    }
                    else if (usageToken.ToString().IndexOf("KsefTokenEncryption", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var matched = certificate["certificate"]?.ToString()
                            ?? certificate["publicKey"]?.ToString()
                            ?? certificate["value"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(matched))
                        {
                            return matched;
                        }
                    }
                }
            }

            var first = certificates.First;
            return first?["certificate"]?.ToString()
                ?? first?["publicKey"]?.ToString()
                ?? first?["value"]?.ToString();
        }

        private KsefSessionTokens ParseSessionTokens(JObject response)
        {
            var accessToken = GetRequiredString((JObject?)response["accessToken"], "token");
            var accessExpiration = GetRequiredTokenExpiration((JObject?)response["accessToken"]);
            var refreshToken = GetRequiredString((JObject?)response["refreshToken"], "token");
            var refreshExpiration = GetRequiredTokenExpiration((JObject?)response["refreshToken"]);

            return new KsefSessionTokens
            {
                AccessToken = accessToken,
                AccessTokenExpirationDate = ParseDate(accessExpiration),
                RefreshToken = refreshToken,
                RefreshTokenExpirationDate = ParseDate(refreshExpiration),
            };
        }

        private KsefSessionTokens ParseRefreshedSessionTokens(JObject response)
        {
            var accessToken = GetRequiredString((JObject?)response["accessToken"], "token");
            var accessExpiration = GetRequiredTokenExpiration((JObject?)response["accessToken"]);

            var existingRefreshToken = _tokens?.RefreshToken;
            var existingRefreshExpiration = _tokens?.RefreshTokenExpirationDate ?? DateTimeOffset.MinValue;

            var refreshTokenObject = response["refreshToken"] as JObject;
            var refreshToken = refreshTokenObject != null
                ? GetRequiredString(refreshTokenObject, "token")
                : existingRefreshToken ?? string.Empty;
            var refreshExpiration = refreshTokenObject != null
                ? ParseDate(GetRequiredTokenExpiration(refreshTokenObject))
                : existingRefreshExpiration;

            return new KsefSessionTokens
            {
                AccessToken = accessToken,
                AccessTokenExpirationDate = ParseDate(accessExpiration),
                RefreshToken = refreshToken,
                RefreshTokenExpirationDate = refreshExpiration,
            };
        }

        private static string GetRequiredTokenExpiration(JObject? tokenObject)
        {
            var value = tokenObject?["expirationDate"]?.ToString()
                ?? tokenObject?["validUntil"]?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Missing required token expiration in KSeF response.");
            }

            return value;
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
            var type = NormalizeContextIdentifierType(_settings.SubjectIdentifierType);
            return new JObject
            {
                ["type"] = type,
                ["value"] = _settings.Nip,
            };
        }

        private static string NormalizeContextIdentifierType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return "Nip";
            }

            var normalized = type.Trim();
            if (string.Equals(normalized, "onip", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "nip", StringComparison.OrdinalIgnoreCase))
            {
                return "Nip";
            }

            if (string.Equals(normalized, "internalid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "internal-id", StringComparison.OrdinalIgnoreCase))
            {
                return "InternalId";
            }

            if (string.Equals(normalized, "nipvatue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "vatue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "vat-ue", StringComparison.OrdinalIgnoreCase))
            {
                return "NipVatUe";
            }

            if (string.Equals(normalized, "peppolid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "peppol-id", StringComparison.OrdinalIgnoreCase))
            {
                return "PeppolId";
            }

            return normalized;
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

        private async Task<JToken> GetAnyJsonAsync(
            string relativePath,
            IDictionary<string, string>? headers,
            CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(relativePath)))
            {
                ApplyHeaders(request, headers);
                return await SendAndParseAnyAsync(request, cancellationToken).ConfigureAwait(false);
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

        private async Task<JToken> SendAndParseAnyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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

                return JToken.Parse(responseBody);
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

        private static string EncryptKsefToken(string token, string timestamp, string publicMaterial)
        {
            var cipherText = BuildTokenCipherText(token, timestamp);
            var key = ParseRsaPublicKey(publicMaterial);
            var engine = new OaepEncoding(new RsaEngine(), new Sha256Digest(), new Sha256Digest(), null);
            engine.Init(true, key);

            var input = Encoding.UTF8.GetBytes(cipherText);
            var encrypted = engine.ProcessBlock(input, 0, input.Length);
            return Convert.ToBase64String(encrypted);
        }

        private static RsaKeyParameters ParseRsaPublicKey(string publicMaterial)
        {
            var bytes = ParseBase64OrPem(publicMaterial);

            try
            {
                var cert = new X509CertificateParser().ReadCertificate(bytes);
                return (RsaKeyParameters)cert.GetPublicKey();
            }
            catch
            {
                var key = PublicKeyFactory.CreateKey(bytes);
                return (RsaKeyParameters)key;
            }
        }

        private static byte[] ParseBase64OrPem(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Missing public key/certificate material.");
            }

            var trimmed = value.Trim();
            if (trimmed.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase))
            {
                var lines = trimmed.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var builder = new StringBuilder();
                foreach (var line in lines)
                {
                    if (!line.StartsWith("-----", StringComparison.Ordinal))
                    {
                        builder.Append(line.Trim());
                    }
                }

                return Convert.FromBase64String(builder.ToString());
            }

            var compact = trimmed
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace(" ", string.Empty);

            return Convert.FromBase64String(compact);
        }

        private static string BuildTokenCipherText(string token, string timestamp)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}", token, timestamp);
        }

        private static string ResolveChallengeTimestampMs(JObject challengeResponse)
        {
            var timestampMs = challengeResponse["timestampMs"]?.ToString();
            if (!string.IsNullOrWhiteSpace(timestampMs))
            {
                return timestampMs;
            }

            var timestamp = challengeResponse["timestamp"]?.ToString();
            if (!string.IsNullOrWhiteSpace(timestamp)
                && DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            }

            throw new InvalidOperationException("Missing required property 'timestampMs' in KSeF response.");
        }

    }
}
