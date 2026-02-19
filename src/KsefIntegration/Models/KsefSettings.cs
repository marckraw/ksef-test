namespace KsefIntegration.Models
{
    public sealed class KsefSettings
    {
        public string BaseUrl { get; set; } = "https://api-test.ksef.mf.gov.pl/api/v2";

        public string KsefToken { get; set; } = string.Empty;

        public string Nip { get; set; } = string.Empty;

        public string SubjectIdentifierType { get; set; } = "onip";

        public int RequestTimeoutSeconds { get; set; } = 60;

        public int AuthStatusPollDelayMs { get; set; } = 1000;

        public int AuthStatusMaxAttempts { get; set; } = 30;

        public int InvoiceRetryCount { get; set; } = 4;

        public int InvoiceRetryDelayMs { get; set; } = 1000;
    }
}
