namespace KsefIntegration.Models
{
    public sealed class PdfRenderOptions
    {
        public bool IncludeQrCode { get; set; } = true;

        public bool IncludeKsefMetadata { get; set; } = true;

        public string AdditionalArguments { get; set; } = string.Empty;
    }
}
