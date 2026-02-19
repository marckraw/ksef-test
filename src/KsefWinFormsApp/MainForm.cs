using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using KsefIntegration.Abstractions;
using KsefIntegration.Infrastructure;
using KsefIntegration.Models;
using KsefIntegration.Services;

namespace KsefWinFormsApp
{
    public sealed class MainForm : Form
    {
        private readonly TextBox _txtKsefNumber = new TextBox();
        private readonly TextBox _txtOutputPdfPath = new TextBox();

        private readonly Button _btnBrowseOutput = new Button();
        private readonly Button _btnSettings = new Button();
        private readonly Button _btnDownloadPdf = new Button();

        private readonly Label _lblConfigSummary = new Label();
        private readonly Label _lblStatus = new Label();

        private readonly HttpClient _httpClient;

        private AppSettings _settings;

        public MainForm()
        {
            _httpClient = new HttpClient();
            _settings = AppSettingsStore.Load();

            if (string.IsNullOrWhiteSpace(_settings.DefaultOutputPdfPath))
            {
                _settings.DefaultOutputPdfPath = GetDefaultOutputPath();
            }

            Text = "KSeF Invoice Downloader (MVP)";
            Width = 900;
            Height = 420;
            MinimumSize = new Size(840, 380);
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            ApplySettingsToUi();
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
                RowCount = 8,
                Padding = new Padding(12),
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

            for (var i = 0; i < 7; i++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            }

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

            try
            {
                var facade = BuildFacade();
                var outputPath = _txtOutputPdfPath.Text.Trim();
                var ksefNumber = _txtKsefNumber.Text.Trim();

                var resultPath = await facade.DownloadInvoiceVisualizationAsync(
                    ksefNumber,
                    outputPath,
                    new PdfRenderOptions
                    {
                        IncludeQrCode = _settings.IncludeQrCode,
                        IncludeKsefMetadata = _settings.IncludeKsefMetadata,
                        AdditionalArguments = BuildAdditionalDataArgument(ksefNumber),
                    },
                    CancellationToken.None);

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
                var details = "Kod HTTP: " + (int)ex.StatusCode + "\n"
                    + "Kod API: " + (ex.ApiCode ?? "brak") + "\n"
                    + "Opis: " + ex.Message;
                MessageBox.Show(this, details, "Błąd KSeF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Błąd operacji.";
                MessageBox.Show(this, ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
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

            AppSettingsStore.Save(_settings);
            _txtOutputPdfPath.Text = _settings.DefaultOutputPdfPath;
            _lblConfigSummary.Text = BuildConfigSummary(_settings);
            _lblStatus.Text = "Ustawienia zapisane.";
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(_txtKsefNumber.Text))
            {
                MessageBox.Show(this, "Wpisz numer KSeF faktury.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_txtOutputPdfPath.Text))
            {
                MessageBox.Show(this, "Wskaż ścieżkę docelowego pliku PDF.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
            {
                MessageBox.Show(this, "Uzupełnij KSeF Base URL w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.Nip))
            {
                MessageBox.Show(this, "Uzupełnij NIP w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.KsefToken))
            {
                MessageBox.Show(this, "Uzupełnij token KSeF w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.PdfCommandPath))
            {
                MessageBox.Show(this, "Uzupełnij polecenie PDF (np. node) w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.PdfScriptPath))
            {
                MessageBox.Show(this, "Uzupełnij ścieżkę skryptu PDF (.js/.mjs) w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.PdfArgumentsTemplate))
            {
                MessageBox.Show(this, "Uzupełnij szablon argumentów w ustawieniach.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private IKsefFacade BuildFacade()
        {
            var ksefSettings = new KsefSettings
            {
                BaseUrl = _settings.BaseUrl.Trim(),
                Nip = _settings.Nip.Trim(),
                KsefToken = _settings.KsefToken.Trim(),
                SubjectIdentifierType = "onip",
                RequestTimeoutSeconds = 60,
                AuthStatusPollDelayMs = 1000,
                AuthStatusMaxAttempts = 30,
                InvoiceRetryCount = 4,
                InvoiceRetryDelayMs = 1000,
            };

            var pdfSettings = new PdfGeneratorSettings
            {
                CommandPath = _settings.PdfCommandPath.Trim(),
                ScriptPath = _settings.PdfScriptPath.Trim(),
                ArgumentsTemplate = _settings.PdfArgumentsTemplate.Trim(),
                TimeoutSeconds = 60,
            };

            return KsefFacadeFactory.Create(ksefSettings, pdfSettings, _httpClient);
        }

        private void ToggleBusy(bool busy)
        {
            _btnDownloadPdf.Enabled = !busy;
            _btnBrowseOutput.Enabled = !busy;
            _btnSettings.Enabled = !busy;
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

        private static string BuildConfigSummary(AppSettings settings)
        {
            var nipPart = string.IsNullOrWhiteSpace(settings.Nip) ? "NIP: brak" : "NIP: " + settings.Nip;
            var tokenPart = string.IsNullOrWhiteSpace(settings.KsefToken) ? "token: brak" : "token: ustawiony";
            var scriptPart = string.IsNullOrWhiteSpace(settings.PdfScriptPath) ? "PDF CLI: brak" : "PDF CLI: ustawiony";

            return "Konfiguracja -> " + nipPart + ", " + tokenPart + ", " + scriptPart;
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

        private static string GetDefaultOutputPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Path.Combine(desktop, "ksef-invoice.pdf");
        }
    }
}
