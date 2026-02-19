# Integracja MVP KSeF w WinForms (.NET Framework 4.8)

Ten moduł implementuje procedurę:

1. Autoryzacja KSeF przez token.
2. Pobranie XML faktury po numerze KSeF.
3. Lokalna generacja PDF przez oficjalny generator MF.

## 1. Dodanie projektu do rozwiązania

1. Skopiuj folder `src/KsefIntegration` do repozytorium aplikacji WinForms.
2. Dodaj projekt `KsefIntegration.csproj` do solution w VS2019/VS2022.
3. Dodaj referencję do projektu `KsefIntegration` z projektu WinForms.
4. Przywróć NuGet packages.

## 2. Wymagania środowiskowe

1. Token KSeF operator wpisuje ręcznie w UI.
2. Dostęp do środowiska `TEST` lub `DEMO` (zależnie od etapu).
3. Zainstalowany Node.js na stanowisku.
4. Dostępny lokalnie oficjalny generator PDF MF i ścieżka do jego skryptu `.js`/`.mjs`.
5. Szczegóły instalacji/argumentów: `docs/PDF_GENERATOR_SETUP_PL.md`.

## 3. Przykładowe podpięcie pod przycisk WinForms

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KsefIntegration.Abstractions;
using KsefIntegration.Models;
using KsefIntegration.Services;

public partial class MainForm : Form
{
    private readonly HttpClient _httpClient = new HttpClient();
    private IKsefFacade? _ksefFacade;

    public MainForm()
    {
        InitializeComponent();
    }

    private void BuildKsefFacade()
    {
        var ksefSettings = new KsefSettings
        {
            BaseUrl = "https://api-test.ksef.mf.gov.pl/api/v2",
            Nip = txtNip.Text.Trim(),
            KsefToken = txtKsefToken.Text.Trim(),
            SubjectIdentifierType = "onip",
            RequestTimeoutSeconds = 60,
            AuthStatusPollDelayMs = 1000,
            AuthStatusMaxAttempts = 30,
            InvoiceRetryCount = 4,
            InvoiceRetryDelayMs = 1000,
        };

        var pdfSettings = new PdfGeneratorSettings
        {
            CommandPath = "node",
            ScriptPath = @"C:\tools\ksef-pdf-generator\dist\index.js",
            ArgumentsTemplate = "{script} faktura {input} {output} {extra}",
            TimeoutSeconds = 60,
        };

        _ksefFacade = KsefFacadeFactory.Create(ksefSettings, pdfSettings, _httpClient);
    }

    private async void btnPobierzWizualizacje_Click(object sender, EventArgs e)
    {
        btnPobierzWizualizacje.Enabled = false;
        try
        {
            BuildKsefFacade();

            var ksefNumber = txtKsefNumber.Text.Trim();
            var outputPath = txtOutputPdfPath.Text.Trim();

            var resultPath = await _ksefFacade!.DownloadInvoiceVisualizationAsync(
                ksefNumber,
                outputPath,
                options: new PdfRenderOptions
                {
                    IncludeQrCode = true,
                    IncludeKsefMetadata = true,
                },
                cancellationToken: CancellationToken.None);

            MessageBox.Show($"Wizualizacja zapisana: {resultPath}", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd pobierania wizualizacji: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnPobierzWizualizacje.Enabled = true;
        }
    }
}
```

## 4. Obsługa błędów

- `401` powoduje odświeżenie/reautoryzację i retry.
- `429` oraz kod KSeF `21165` mają retry z backoff.
- W przypadku finalnego błędu rzucany jest wyjątek `KsefApiException` z kodem HTTP i kodem API.

## 5. Uwagi produkcyjne (po MVP)

1. Przenieść token z pola UI do bezpiecznego storage (np. DPAPI).
2. Dodać logi audytowe per operacja.
3. Rozważyć tryb XAdES jako alternatywę.
4. Dodać monitoring i alertowanie błędów integracji.
