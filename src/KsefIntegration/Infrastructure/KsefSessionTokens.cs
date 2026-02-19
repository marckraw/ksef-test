using System;

namespace KsefIntegration.Infrastructure
{
    internal sealed class KsefSessionTokens
    {
        public string AccessToken { get; set; } = string.Empty;

        public DateTimeOffset AccessTokenExpirationDate { get; set; }

        public string RefreshToken { get; set; } = string.Empty;

        public DateTimeOffset RefreshTokenExpirationDate { get; set; }
    }
}
