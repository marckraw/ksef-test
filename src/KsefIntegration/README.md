# KsefIntegration (MVP)

Reusable C# module for:

1. KSeF token-based authentication (`/auth/challenge`, `/auth/ksef-token`, `/auth/{referenceNumber}`, `/auth/token/redeem`, `/auth/token/refresh`).
2. Invoice XML download by KSeF number (`/invoices/ksef/{ksefNumber}`).
3. Local PDF generation using official MF renderer (invoked as external process).

## Target

- `netstandard2.0` so it can be referenced from WinForms `.NET Framework 4.8` (VS2019/VS2022).

## Quick Integration (WinForms)

```csharp
using System.Net.Http;
using KsefIntegration.Models;
using KsefIntegration.Services;

var ksefSettings = new KsefSettings
{
    BaseUrl = "https://api-test.ksef.mf.gov.pl/api/v2",
    Nip = "1234567890",
    KsefToken = tokenFromTextBox,
    SubjectIdentifierType = "onip",
};

var pdfSettings = new PdfGeneratorSettings
{
    CommandPath = "node",
    ScriptPath = @"C:\tools\ksef-pdf-generator\dist\cli.js",
    ArgumentsTemplate = "{script} --input {input} --output {output} --ksef {ksefNumber}",
};

var httpClient = new HttpClient();
var facade = KsefFacadeFactory.Create(ksefSettings, pdfSettings, httpClient);

var outputPath = await facade.DownloadInvoiceVisualizationAsync(
    ksefNumberTextBox.Text,
    @"C:\Temp\invoice.pdf");
```

## Notes

- In MVP mode token is expected to be entered manually in UI and kept in memory only.
- `ArgumentsTemplate` supports placeholders:
  - `{script}`
  - `{input}`
  - `{output}`
  - `{ksefNumber}`
  - `{includeQrCode}`
  - `{includeKsefMetadata}`
  - `{extra}`
- Ensure local Node runtime and official MF PDF generator are installed on workstation.
