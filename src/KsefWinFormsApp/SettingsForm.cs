using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace KsefWinFormsApp
{
    public sealed class SettingsForm : Form
    {
        private readonly TextBox _txtBaseUrl = new TextBox();
        private readonly TextBox _txtNip = new TextBox();
        private readonly TextBox _txtToken = new TextBox();
        private readonly TextBox _txtPdfCommandPath = new TextBox();
        private readonly TextBox _txtPdfScriptPath = new TextBox();
        private readonly TextBox _txtPdfArgumentsTemplate = new TextBox();
        private readonly TextBox _txtDefaultOutputPdfPath = new TextBox();
        private readonly TextBox _txtXmlOutputDirectory = new TextBox();

        private readonly CheckBox _chkSaveInvoiceXml = new CheckBox();
        private readonly CheckBox _chkIncludeQr = new CheckBox();
        private readonly CheckBox _chkIncludeMetadata = new CheckBox();

        private readonly Button _btnBrowseScript = new Button();
        private readonly Button _btnBrowseOutput = new Button();
        private readonly Button _btnBrowseXmlFolder = new Button();
        private readonly Button _btnSave = new Button();
        private readonly Button _btnCancel = new Button();

        public SettingsForm(AppSettings currentSettings)
        {
            if (currentSettings == null)
            {
                throw new ArgumentNullException(nameof(currentSettings));
            }

            Settings = currentSettings.Clone();

            Text = "Ustawienia KSeF";
            Width = 980;
            Height = 660;
            MinimumSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterParent;

            BuildLayout();
            FillFromSettings();
        }

        public AppSettings Settings { get; private set; }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 13,
                Padding = new Padding(12),
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

            for (var i = 0; i < 12; i++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            }

            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            AddField(root, 0, "KSeF Base URL", _txtBaseUrl);
            AddField(root, 1, "NIP", _txtNip);
            AddField(root, 2, "Token KSeF", _txtToken);
            AddField(root, 3, "Polecenie PDF (np. node)", _txtPdfCommandPath);
            AddField(root, 4, "Ścieżka wrappera PDF (.mjs/.js)", _txtPdfScriptPath);
            AddField(root, 5, "Szablon argumentów", _txtPdfArgumentsTemplate);
            AddField(root, 6, "Domyślny plik wyjściowy PDF", _txtDefaultOutputPdfPath);
            AddField(root, 7, "Folder zapisu XML", _txtXmlOutputDirectory);

            _btnBrowseScript.Text = "Wybierz...";
            _btnBrowseScript.Click += BrowseScriptClick;
            root.Controls.Add(_btnBrowseScript, 2, 4);

            _btnBrowseOutput.Text = "Wybierz...";
            _btnBrowseOutput.Click += BrowseOutputClick;
            root.Controls.Add(_btnBrowseOutput, 2, 6);

            _btnBrowseXmlFolder.Text = "Wybierz...";
            _btnBrowseXmlFolder.Click += BrowseXmlFolderClick;
            root.Controls.Add(_btnBrowseXmlFolder, 2, 7);

            _chkSaveInvoiceXml.Text = "Zapisuj pobrane XML faktur";
            _chkSaveInvoiceXml.Checked = false;
            root.Controls.Add(_chkSaveInvoiceXml, 1, 8);

            _chkIncludeQr.Text = "Dołącz kod QR";
            _chkIncludeQr.Checked = true;
            root.Controls.Add(_chkIncludeQr, 1, 9);

            _chkIncludeMetadata.Text = "Dołącz metadane KSeF";
            _chkIncludeMetadata.Checked = true;
            root.Controls.Add(_chkIncludeMetadata, 1, 10);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
            };

            _btnSave.Text = "Zapisz";
            _btnSave.Width = 120;
            _btnSave.Click += SaveClick;

            _btnCancel.Text = "Anuluj";
            _btnCancel.Width = 120;
            _btnCancel.Click += CancelClick;

            buttons.Controls.Add(_btnSave);
            buttons.Controls.Add(_btnCancel);

            root.Controls.Add(buttons, 1, 11);

            _txtToken.UseSystemPasswordChar = true;

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

        private void FillFromSettings()
        {
            _txtBaseUrl.Text = Settings.BaseUrl;
            _txtNip.Text = Settings.Nip;
            _txtToken.Text = Settings.KsefToken;
            _txtPdfCommandPath.Text = Settings.PdfCommandPath;
            _txtPdfScriptPath.Text = Settings.PdfScriptPath;
            _txtPdfArgumentsTemplate.Text = Settings.PdfArgumentsTemplate;
            _txtDefaultOutputPdfPath.Text = Settings.DefaultOutputPdfPath;
            _txtXmlOutputDirectory.Text = Settings.XmlOutputDirectory;
            _chkSaveInvoiceXml.Checked = Settings.SaveInvoiceXml;
            _chkIncludeQr.Checked = Settings.IncludeQrCode;
            _chkIncludeMetadata.Checked = Settings.IncludeKsefMetadata;
        }

        private void SaveClick(object? sender, EventArgs e)
        {
            Settings = new AppSettings
            {
                BaseUrl = _txtBaseUrl.Text.Trim(),
                Nip = _txtNip.Text.Trim(),
                KsefToken = _txtToken.Text.Trim(),
                PdfCommandPath = _txtPdfCommandPath.Text.Trim(),
                PdfScriptPath = _txtPdfScriptPath.Text.Trim(),
                PdfArgumentsTemplate = _txtPdfArgumentsTemplate.Text.Trim(),
                SaveInvoiceXml = _chkSaveInvoiceXml.Checked,
                XmlOutputDirectory = _txtXmlOutputDirectory.Text.Trim(),
                IncludeQrCode = _chkIncludeQr.Checked,
                IncludeKsefMetadata = _chkIncludeMetadata.Checked,
                DefaultOutputPdfPath = _txtDefaultOutputPdfPath.Text.Trim(),
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private void CancelClick(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
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
            var directory = Path.GetDirectoryName(_txtDefaultOutputPdfPath.Text);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = Path.GetFileName(_txtDefaultOutputPdfPath.Text),
                InitialDirectory = directory,
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _txtDefaultOutputPdfPath.Text = dialog.FileName;
            }
        }

        private void BrowseXmlFolderClick(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisu pobranych XML faktur",
                SelectedPath = _txtXmlOutputDirectory.Text.Trim(),
                ShowNewFolderButton = true,
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _txtXmlOutputDirectory.Text = dialog.SelectedPath;
            }
        }
    }
}
