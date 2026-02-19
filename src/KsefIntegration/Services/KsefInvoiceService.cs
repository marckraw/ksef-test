using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KsefIntegration.Abstractions;
using KsefIntegration.Infrastructure;
using KsefIntegration.Models;

namespace KsefIntegration.Services
{
    public sealed class KsefInvoiceService : IKsefInvoiceService
    {
        private readonly HttpClient _httpClient;
        private readonly KsefSettings _settings;
        private readonly IKsefSessionService _sessionService;

        public KsefInvoiceService(HttpClient httpClient, KsefSettings settings, IKsefSessionService sessionService)
        {
            KsefArgumentValidator.ValidateSettings(settings);

            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _settings = settings;
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        }

        public async Task<string> GetInvoiceXmlAsync(string ksefNumber, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ksefNumber))
            {
                throw new ArgumentException("KSeF number cannot be empty.", nameof(ksefNumber));
            }

            var attempts = Math.Max(1, _settings.InvoiceRetryCount + 1);

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                var token = await _sessionService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

                using (var request = new HttpRequestMessage(HttpMethod.Get, BuildInvoiceUrl(ksefNumber)))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Accept.Clear();
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

                    using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                            return Encoding.UTF8.GetString(bytes);
                        }

                        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var error = ApiErrorInfo.Parse(responseBody);

                        if (response.StatusCode == HttpStatusCode.Unauthorized && attempt < attempts)
                        {
                            _sessionService.Invalidate();
                            await KsefDelay.BackoffAsync(attempt, _settings.InvoiceRetryDelayMs, cancellationToken)
                                .ConfigureAwait(false);
                            continue;
                        }

                        if (ShouldRetry(response.StatusCode, error.Code, attempt, attempts))
                        {
                            await KsefDelay.BackoffAsync(attempt, _settings.InvoiceRetryDelayMs, cancellationToken)
                                .ConfigureAwait(false);
                            continue;
                        }

                        var message = string.Format(
                            CultureInfo.InvariantCulture,
                            "KSeF invoice download failed: {0} {1}. {2}",
                            (int)response.StatusCode,
                            response.StatusCode,
                            error.Description);

                        throw new KsefApiException(message.Trim(), response.StatusCode, error.Code, responseBody);
                    }
                }
            }

            throw new TimeoutException("KSeF invoice could not be downloaded within the configured retry window.");
        }

        private static bool ShouldRetry(HttpStatusCode statusCode, string? apiCode, int attempt, int maxAttempts)
        {
            if (attempt >= maxAttempts)
            {
                return false;
            }

            if (statusCode == HttpStatusCode.TooManyRequests)
            {
                return true;
            }

            return string.Equals(apiCode, "21165", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildInvoiceUrl(string ksefNumber)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/invoices/ksef/{1}",
                _settings.BaseUrl.TrimEnd('/'),
                Uri.EscapeDataString(ksefNumber));
        }
    }
}
