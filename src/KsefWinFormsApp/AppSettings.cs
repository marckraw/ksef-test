using System;

namespace KsefWinFormsApp
{
    public sealed class AppSettings
    {
        public string BaseUrl { get; set; } = "https://api-test.ksef.mf.gov.pl/api/v2";

        public string Nip { get; set; } = string.Empty;

        public string KsefToken { get; set; } = string.Empty;

        public string PdfCommandPath { get; set; } = "node";

        public string PdfScriptPath { get; set; } = string.Empty;

        public string PdfArgumentsTemplate { get; set; } = "{script} faktura {input} {output} {extra}";

        public bool IncludeQrCode { get; set; } = true;

        public bool IncludeKsefMetadata { get; set; } = true;

        public bool SaveInvoiceXml { get; set; } = false;

        public string XmlOutputDirectory { get; set; } = string.Empty;

        public string DefaultOutputPdfPath { get; set; } = string.Empty;

        public AppSettings Clone()
        {
            return new AppSettings
            {
                BaseUrl = BaseUrl,
                Nip = Nip,
                KsefToken = KsefToken,
                PdfCommandPath = PdfCommandPath,
                PdfScriptPath = PdfScriptPath,
                PdfArgumentsTemplate = PdfArgumentsTemplate,
                IncludeQrCode = IncludeQrCode,
                IncludeKsefMetadata = IncludeKsefMetadata,
                SaveInvoiceXml = SaveInvoiceXml,
                XmlOutputDirectory = XmlOutputDirectory,
                DefaultOutputPdfPath = DefaultOutputPdfPath,
            };
        }
    }
}
