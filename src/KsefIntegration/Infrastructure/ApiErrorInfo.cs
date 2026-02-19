using Newtonsoft.Json.Linq;

namespace KsefIntegration.Infrastructure
{
    internal sealed class ApiErrorInfo
    {
        public string? Code { get; set; }

        public string? Description { get; set; }

        public static ApiErrorInfo Parse(string? responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new ApiErrorInfo();
            }

            try
            {
                var json = JObject.Parse(responseBody);
                return new ApiErrorInfo
                {
                    Code = json["exceptionCode"]?.ToString()
                        ?? json["code"]?.ToString(),
                    Description = json["exceptionDescription"]?.ToString()
                        ?? json["message"]?.ToString()
                        ?? json["title"]?.ToString(),
                };
            }
            catch
            {
                return new ApiErrorInfo
                {
                    Description = responseBody,
                };
            }
        }
    }
}
