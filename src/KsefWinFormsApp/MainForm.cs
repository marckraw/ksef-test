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
        private readonly TextBox _txtBaseUrl = new TextBox();
        private readonly TextBox _txtNip = new TextBox();
        private readonly TextBox _txtToken = new TextBox();
        private readonly TextBox _txtKsefNumber = new TextBox();
        private readonly TextBox _txtPdfScriptPath = new TextBox();
        private readonly TextBox _txtPdfArgumentsTemplate = new TextBox();
        private readonly TextBox _txtOutputPdfPath = new TextBox();

        private readonly CheckBox _chkIncludeQr = new CheckBox();
        private readonly CheckBox _chkIncludeMetadata = new CheckBox();

        private readonly Button _btnBrowseScript = new Button();
        private readonly Button _btnBrowseOutput = new Button();
        private readonly Button _btnDownloadPdf = new Button();

        private readonly Label _lblStatus = new Label();

        private readonly HttpClient _httpClient;

        public MainForm()
        {
            _httpClient = new HttpClient();
            Text = "KSeF Invoice Downloader (MVP)";
            Width = 980;
            Height = 640;
            MinimumSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            SetDefaults();
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
                RowCount = 12,
                Padding = new Padding(12),
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

            for (var i = 0; i < 11; i++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            }

            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            AddField(root, 0, "KSeF Base URL", _txtBaseUrl);
            AddField(root, 1, "NIP", _txtNip);
            AddField(root, 2, "Token KSeF", _txtToken);
            AddField(root, 3, "Numer KSeF faktury", _txtKsefNumber);
            AddField(root, 4, "Ścieżka CLI generatora PDF", _txtPdfScriptPath);
            AddField(root, 5, "Szablon argumentów CLI", _txtPdfArgumentsTemplate);
            AddField(root, 6, "Wyjściowy plik PDF", _txtOutputPdfPath);

            _btnBrowseScript.Text = "Wybierz...";
            _btnBrowseScript.Click += BrowseScriptClick;
            root.Controls.Add(_btnBrowseScript, 2, 4);

            _btnBrowseOutput.Text = "Wybierz...";
            _btnBrowseOutput.Click += BrowseOutputClick;
            root.Controls.Add(_btnBrowseOutput, 2, 6);

            _chkIncludeQr.Text = "Dołącz kod QR";
            _chkIncludeQr.Checked = true;
            root.Controls.Add(_chkIncludeQr, 1, 7);

            _chkIncludeMetadata.Text = "Dołącz metadane KSeF";
            _chkIncludeMetadata.Checked = true;
            root.Controls.Add(_chkIncludeMetadata, 1, 8);

            _btnDownloadPdf.Text = "Pobierz fakturę PDF";
            _btnDownloadPdf.Height = 38;
            _btnDownloadPdf.Click += DownloadPdfClick;
            root.Controls.Add(_btnDownloadPdf, 1, 9);

            _lblStatus.Text = "Gotowe.";
            _lblStatus.AutoEllipsis = true;
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(_lblStatus, 1, 10);

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

        private void SetDefaults()
        {
            _txtBaseUrl.Text = "https://api-test.ksef.mf.gov.pl/api/v2";
            _txtToken.UseSystemPasswordChar = true;
            _txtPdfArgumentsTemplate.Text = "{script} --input {input} --output {output} --ksef {ksefNumber} {extra}";
            _txtOutputPdfPath.Text = GetDefaultOutputPath();
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
                        IncludeQrCode = _chkIncludeQr.Checked,
                        IncludeKsefMetadata = _chkIncludeMetadata.Checked,
                    },
                    CancellationToken.None);

                _lblStatus.Text = "Sukces: " + resultPath;
                MessageBox.Show(
                    this,
                    "Pobrano i wygenerowano PDF:\n" + resultPath,
                    "Sukces",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(_txtNip.Text))
            {
                MessageBox.Show(this, "Wpisz NIP.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_txtToken.Text))
            {
                MessageBox.Show(this, "Wpisz token KSeF.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_txtKsefNumber.Text))
            {
                MessageBox.Show(this, "Wpisz numer KSeF faktury.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_txtPdfScriptPath.Text))
            {
                MessageBox.Show(this, "Wskaż ścieżkę do skryptu CLI generatora PDF.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_txtOutputPdfPath.Text))
            {
                MessageBox.Show(this, "Wskaż ścieżkę docelowego pliku PDF.", "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private IKsefFacade BuildFacade()
        {
            var ksefSettings = new KsefSettings
            {
                BaseUrl = _txtBaseUrl.Text.Trim(),
                Nip = _txtNip.Text.Trim(),
                KsefToken = _txtToken.Text.Trim(),
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
                ScriptPath = _txtPdfScriptPath.Text.Trim(),
                ArgumentsTemplate = _txtPdfArgumentsTemplate.Text.Trim(),
                TimeoutSeconds = 60,
            };

            return KsefFacadeFactory.Create(ksefSettings, pdfSettings, _httpClient);
        }

        private void ToggleBusy(bool busy)
        {
            _btnDownloadPdf.Enabled = !busy;
            _btnBrowseScript.Enabled = !busy;
            _btnBrowseOutput.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (busy)
            {
                _lblStatus.Text = "Przetwarzanie...";
            }
        }

        private void BrowseScriptClick(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Node script (*.js)|*.js|Wszystkie pliki (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _txtPdfScriptPath.Text = dialog.FileName;
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

        private static string GetDefaultOutputPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Path.Combine(desktop, "ksef-invoice.pdf");
        }
    }
}
