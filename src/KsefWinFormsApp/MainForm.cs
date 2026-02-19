using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using KsefIntegration.Infrastructure;
using KsefIntegration.Models;
using KsefIntegration.Services;

namespace KsefWinFormsApp
{
    public sealed class MainForm : Form
    {
        private readonly TextBox _txtKsefNumber = new TextBox();
        private readonly TextBox _txtOutputPdfPath = new TextBox();
        private readonly TextBox _txtLog = new TextBox();

        private readonly Button _btnBrowseOutput = new Button();
        private readonly Button _btnSettings = new Button();
        private readonly Button _btnDownloadPdf = new Button();
        private readonly Button _btnClearLog = new Button();

        private readonly Label _lblConfigSummary = new Label();
        private readonly Label _lblStatus = new Label();

        private readonly HttpClient _httpClient;

        private AppSettings _settings;

        public MainForm()
        {
            Text = "KSeF Invoice Downloader (MVP)";
            Width = 980;
            Height = 640;
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();

            _httpClient = CreateHttpClient();
            _settings = AppSettingsStore.Load();

            if (string.IsNullOrWhiteSpace(_settings.DefaultOutputPdfPath))
            {
                _settings.DefaultOutputPdfPath = GetDefaultOutputPath();
            }

            if (_settings.SaveInvoiceXml && string.IsNullOrWhiteSpace(_settings.XmlOutputDirectory))
            {
                _settings.XmlOutputDirectory = GetDefaultXmlDirectory();
            }

            ApplySettingsToUi();
            AppendLog("Aplikacja uruchomiona.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }

            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 9,
                Padding = new Padding(12),
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

            for (var i = 0; i < 7; i++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            }

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            AddField(root, 0, "Numer KSeF faktury", _txtKsefNumber);
            AddField(root, 1, "Wyjściowy plik PDF", _txtOutputPdfPath);

            _btnBrowseOutput.Text = "Wybierz...";
            _btnBrowseOutput.Click += BrowseOutputClick;
            root.Controls.Add(_btnBrowseOutput, 2, 1);

            _btnSettings.Text = "Ustawienia...";
            _btnSettings.Height = 36;
            _btnSettings.Click += OpenSettingsClick;
            root.Controls.Add(_btnSettings, 1, 2);

            _lblConfigSummary.Text = "Konfiguracja: brak";
            _lblConfigSummary.AutoEllipsis = true;
            _lblConfigSummary.Dock = DockStyle.Fill;
            _lblConfigSummary.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(_lblConfigSummary, 1, 3);

            _btnDownloadPdf.Text = "Pobierz fakturę PDF";
            _btnDownloadPdf.Height = 38;
            _btnDownloadPdf.Click += DownloadPdfClick;
            root.Controls.Add(_btnDownloadPdf, 1, 4);

            _lblStatus.Text = "Gotowe.";
            _lblStatus.AutoEllipsis = true;
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(_lblStatus, 1, 5);

            _btnClearLog.Text = "Wyczyść log";
            _btnClearLog.Height = 36;
            _btnClearLog.Click += ClearLogClick;
            root.Controls.Add(_btnClearLog, 1, 6);

            var lblLog = new Label
            {
                Text = "Log operacji (krok po kroku)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            root.Controls.Add(lblLog, 0, 7);
            root.SetColumnSpan(lblLog, 3);

            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = ScrollBars.Both;
            _txtLog.WordWrap = false;
            _txtLog.Dock = DockStyle.Fill;
            _txtLog.Font = new Font("Consolas", 9f);
            root.Controls.Add(_txtLog, 0, 8);
            root.SetColumnSpan(_txtLog, 3);

            Controls.Add(root);
        }

        private static void AddField(TableLayoutPanel root, int row, string label, TextBox textBox)
        {
            var lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            textBox.Dock = DockStyle.Fill;

            root.Controls.Add(lbl, 0, row);
            root.Controls.Add(textBox, 1, row);
        }

        private HttpClient CreateHttpClient()
        {
            var loggingHandler = new LoggingHttpMessageHandler(AppendLog)
            {
                InnerHandler = new HttpClientHandler(),
            };

            return new HttpClient(loggingHandler);
        }

        private void ApplySettingsToUi()
        {
            if (string.IsNullOrWhiteSpace(_txtOutputPdfPath.Text))
            {
                _txtOutputPdfPath.Text = _settings.DefaultOutputPdfPath;
            }

            _lblConfigSummary.Text = BuildConfigSummary(_settings);
        }

        private async void DownloadPdfClick(object? sender, EventArgs e)
        {
            if (!ValidateInputs())
            {
                return;
            }

            ToggleBusy(true);
            AppendLog("========================================");
            AppendLog("Start: Pobierz fakturę PDF");

            try
            {
                var outputPath = _txtOutputPdfPath.Text.Trim();
                var ksefNumber = _txtKsefNumber.Text.Trim();

                AppendLog("Numer KSeF: " + ksefNumber);
                AppendLog("Wyjściowy PDF: " + outputPath);
                AppendLog("KSeF Base URL: " + _settings.BaseUrl);
                AppendLog("NIP: " + _settings.Nip);
                AppendLog("Token KSeF: " + (string.IsNullOrWhiteSpace(_settings.KsefToken) ? "brak" : "ustawiony"));

                var ksefSettings = BuildKsefSettings();
                var pdfSettings = BuildPdfSettings();

                AppendLog("Tworzenie serwisów KSeF...");
                var sessionService = new KsefSessionService(_httpClient, ksefSettings);
                var invoiceService = new KsefInvoiceService(_httpClient, ksefSettings, sessionService);
                var pdfService = new KsefPdfVisualizationService(pdfSettings);

                AppendLog("Krok 1/2: Pobieranie XML z KSeF...");
                var invoiceXml = await invoiceService.GetInvoiceXmlAsync(ksefNumber, CancellationToken.None);
                AppendLog("Krok 1/2 OK: XML pobrany (" + invoiceXml.Length + " znaków).");

                var savedXmlPath = SaveInvoiceXmlIfConfigured(ksefNumber, invoiceXml);
                if (!string.IsNullOrWhiteSpace(savedXmlPath))
                {
                    AppendLog("XML zapisany: " + savedXmlPath);
                }

                AppendLog("Krok 2/2: Generowanie PDF...");
                var resultPath = await pdfService.GeneratePdfAsync(
                    invoiceXml,
                    outputPath,
                    ksefNumber,
                    new PdfRenderOptions
                    {
                        IncludeQrCode = _settings.IncludeQrCode,
                        IncludeKsefMetadata = _settings.IncludeKsefMetadata,
                        AdditionalArguments = BuildAdditionalDataArgument(ksefNumber),
                    },
                    CancellationToken.None);

                AppendLog("Sukces: PDF wygenerowany -> " + resultPath);
                _lblStatus.Text = "Sukces: " + resultPath;
                MessageBox.Show(
                    this,
                    "Pobrano i wygenerowano PDF:\n" + resultPath,
                    "Sukces",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _settings.DefaultOutputPdfPath = outputPath;
                AppSettingsStore.Save(_settings);
            }
            catch (KsefApiException ex)
            {
                _lblStatus.Text = "Błąd API KSeF.";
                AppendLog("Błąd API KSeF: HTTP=" + (int)ex.StatusCode + ", API=" + (ex.ApiCode ?? "brak") + ", Msg=" + ex.Message);
                if (!string.IsNullOrWhiteSpace(ex.ResponseBody))
                {
                    AppendLog("Response body: " + Truncate(ex.ResponseBody, 1000));
                }

                var details = "Kod HTTP: " + (int)ex.StatusCode + "\n"
                    + "Kod API: " + (ex.ApiCode ?? "brak") + "\n"
                    + "Opis: " + ex.Message;
                MessageBox.Show(this, details, "Błąd KSeF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Błąd operacji.";
                AppendLog("Błąd: " + ex.GetType().Name + ": " + ex.Message);
                MessageBox.Show(this, ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                AppendLog("Koniec operacji.");
                ToggleBusy(false);
            }
        }

        private void OpenSettingsClick(object? sender, EventArgs e)
        {
            using var dialog = new SettingsForm(_settings);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _settings = dialog.Settings;
            if (string.IsNullOrWhiteSpace(_settings.DefaultOutputPdfPath))
            {
                _settings.DefaultOutputPdfPath = GetDefaultOutputPath();
            }

            if (_settings.SaveInvoiceXml && string.IsNullOrWhiteSpace(_settings.XmlOutputDirectory))
            {
                _settings.XmlOutputDirectory = GetDefaultXmlDirectory();
            }

            AppSettingsStore.Save(_settings);
            _txtOutputPdfPath.Text = _settings.DefaultOutputPdfPath;
            _lblConfigSummary.Text = BuildConfigSummary(_settings);
            _lblStatus.Text = "Ustawienia zapisane.";
            AppendLog("Ustawienia zapisane.");
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(_txtKsefNumber.Text))
            {
                AppendLog("Walidacja: brak numeru KSeF.");
                MessageBox.Show(this, "Wpisz numer KSeF faktury.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_txtOutputPdfPath.Text))
            {
                AppendLog("Walidacja: brak ścieżki wyjściowej PDF.");
                MessageBox.Show(this, "Wskaż ścieżkę docelowego pliku PDF.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
            {
                AppendLog("Walidacja: brak KSeF Base URL.");
                MessageBox.Show(this, "Uzupełnij KSeF Base URL w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.Nip))
            {
                AppendLog("Walidacja: brak NIP.");
                MessageBox.Show(this, "Uzupełnij NIP w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.KsefToken))
            {
                AppendLog("Walidacja: brak tokenu KSeF.");
                MessageBox.Show(this, "Uzupełnij token KSeF w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (_settings.SaveInvoiceXml && string.IsNullOrWhiteSpace(_settings.XmlOutputDirectory))
            {
                AppendLog("Walidacja: włączono zapis XML, ale brak folderu XML.");
                MessageBox.Show(this, "Uzupełnij folder zapisu XML w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.PdfCommandPath))
            {
                AppendLog("Walidacja: brak polecenia PDF (np. node).");
                MessageBox.Show(this, "Uzupełnij polecenie PDF (np. node) w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.PdfScriptPath))
            {
                AppendLog("Walidacja: brak ścieżki skryptu PDF.");
                MessageBox.Show(this, "Uzupełnij ścieżkę skryptu PDF (.js/.mjs) w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.PdfArgumentsTemplate))
            {
                AppendLog("Walidacja: brak szablonu argumentów PDF.");
                MessageBox.Show(this, "Uzupełnij szablon argumentów w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private KsefSettings BuildKsefSettings()
        {
            return new KsefSettings
            {
                BaseUrl = _settings.BaseUrl.Trim(),
                Nip = _settings.Nip.Trim(),
                KsefToken = _settings.KsefToken.Trim(),
                SubjectIdentifierType = "Nip",
                RequestTimeoutSeconds = 60,
                AuthStatusPollDelayMs = 1000,
                AuthStatusMaxAttempts = 30,
                InvoiceRetryCount = 4,
                InvoiceRetryDelayMs = 1000,
            };
        }

        private PdfGeneratorSettings BuildPdfSettings()
        {
            return new PdfGeneratorSettings
            {
                CommandPath = _settings.PdfCommandPath.Trim(),
                ScriptPath = _settings.PdfScriptPath.Trim(),
                ArgumentsTemplate = _settings.PdfArgumentsTemplate.Trim(),
                TimeoutSeconds = 60,
            };
        }

        private string? SaveInvoiceXmlIfConfigured(string ksefNumber, string invoiceXml)
        {
            if (!_settings.SaveInvoiceXml)
            {
                return null;
            }

            var directory = _settings.XmlOutputDirectory.Trim();
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            Directory.CreateDirectory(directory);

            var safeKsefNumber = MakeSafeFileName(ksefNumber);
            var fileName = safeKsefNumber + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml";
            var fullPath = Path.Combine(directory, fileName);

            File.WriteAllText(fullPath, invoiceXml, Encoding.UTF8);
            return fullPath;
        }

        private void ToggleBusy(bool busy)
        {
            _btnDownloadPdf.Enabled = !busy;
            _btnBrowseOutput.Enabled = !busy;
            _btnSettings.Enabled = !busy;
            _btnClearLog.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (busy)
            {
                _lblStatus.Text = "Przetwarzanie...";
            }
        }

        private void BrowseOutputClick(object? sender, EventArgs e)
        {
            var defaultDirectory = Path.GetDirectoryName(_txtOutputPdfPath.Text);
            if (string.IsNullOrWhiteSpace(defaultDirectory))
            {
                defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = Path.GetFileName(_txtOutputPdfPath.Text),
                InitialDirectory = defaultDirectory,
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _txtOutputPdfPath.Text = dialog.FileName;
            }
        }

        private void ClearLogClick(object? sender, EventArgs e)
        {
            _txtLog.Clear();
            AppendLog("Log wyczyszczony.");
        }

        private void AppendLog(string message)
        {
            if (_txtLog.IsDisposed)
            {
                return;
            }

            if (_txtLog.InvokeRequired)
            {
                _txtLog.BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            var line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + message + Environment.NewLine;
            _txtLog.AppendText(line);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private static string BuildConfigSummary(AppSettings settings)
        {
            var nipPart = string.IsNullOrWhiteSpace(settings.Nip) ? "NIP: brak" : "NIP: " + settings.Nip;
            var tokenPart = string.IsNullOrWhiteSpace(settings.KsefToken) ? "token: brak" : "token: ustawiony";
            var scriptPart = string.IsNullOrWhiteSpace(settings.PdfScriptPath) ? "PDF CLI: brak" : "PDF CLI: ustawiony";
            var xmlPart = settings.SaveInvoiceXml
                ? (string.IsNullOrWhiteSpace(settings.XmlOutputDirectory) ? "XML: folder brak" : "XML: zapis ON")
                : "XML: zapis OFF";

            return "Konfiguracja -> " + nipPart + ", " + tokenPart + ", " + scriptPart + ", " + xmlPart;
        }

        private static string BuildAdditionalDataArgument(string ksefNumber)
        {
            if (string.IsNullOrWhiteSpace(ksefNumber))
            {
                return string.Empty;
            }

            var escapedKsefNumber = ksefNumber
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");

            var json = "{\"nrKSeF\":\"" + escapedKsefNumber + "\"}";
            return "\"" + json.Replace("\"", "\\\"") + "\"";
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "invoice";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                var isInvalid = false;
                foreach (var invalid in invalidChars)
                {
                    if (character == invalid)
                    {
                        isInvalid = true;
                        break;
                    }
                }

                builder.Append(isInvalid ? '_' : character);
            }

            var result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "invoice" : result;
        }

        private static string GetDefaultOutputPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Path.Combine(desktop, "ksef-invoice.pdf");
        }

        private static string GetDefaultXmlDirectory()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents, "KSeF", "XML");
        }
    }
}
