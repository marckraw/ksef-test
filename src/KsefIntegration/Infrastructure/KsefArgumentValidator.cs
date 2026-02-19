using System;
using KsefIntegration.Models;

namespace KsefIntegration.Infrastructure
{
    internal static class KsefArgumentValidator
    {
        public static void ValidateSettings(KsefSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                throw new ArgumentException("KSeF BaseUrl cannot be empty.", nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(settings.KsefToken))
            {
                throw new ArgumentException("KSeF token cannot be empty.", nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(settings.Nip))
            {
                throw new ArgumentException("NIP cannot be empty.", nameof(settings));
            }
        }

        public static void ValidatePdfSettings(PdfGeneratorSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(settings.CommandPath))
            {
                throw new ArgumentException("PDF generator command path cannot be empty.", nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(settings.ArgumentsTemplate))
            {
                throw new ArgumentException("PDF generator arguments template cannot be empty.", nameof(settings));
            }
        }
    }
}
