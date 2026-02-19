using System;
using System.Net;

namespace KsefIntegration.Infrastructure
{
    public sealed class KsefApiException : Exception
    {
        public KsefApiException(
            string message,
            HttpStatusCode statusCode,
            string? apiCode = null,
            string? responseBody = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ApiCode = apiCode;
            ResponseBody = responseBody;
        }

        public HttpStatusCode StatusCode { get; }

        public string? ApiCode { get; }

        public string? ResponseBody { get; }
    }
}
