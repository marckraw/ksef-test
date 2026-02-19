using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KsefWinFormsApp
{
    public sealed class LoggingHttpMessageHandler : DelegatingHandler
    {
        private readonly Action<string> _log;

        public LoggingHttpMessageHandler(Action<string> log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var method = request.Method.Method;
            var uri = request.RequestUri != null ? request.RequestUri.ToString() : "<null>";

            _log("HTTP -> " + method + " " + uri);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                _log(
                    "HTTP <- "
                    + (int)response.StatusCode
                    + " "
                    + response.ReasonPhrase
                    + " ("
                    + stopwatch.ElapsedMilliseconds
                    + " ms) "
                    + method
                    + " "
                    + uri);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _log(
                    "HTTP !! "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message
                    + " ("
                    + stopwatch.ElapsedMilliseconds
                    + " ms) "
                    + method
                    + " "
                    + uri);
                throw;
            }
        }
    }
}
