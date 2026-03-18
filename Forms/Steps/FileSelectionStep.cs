using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms.Steps;

public class FileSelectionStep : UserControl
{
    public List<Transaction>? LoadedTransactions { get; private set; }

    private readonly TextBox _filePathBox;
    private readonly Button  _browseButton;
    private readonly Button  _loadButton;
    private readonly DataGridView _previewGrid;
    private readonly Label _statusLabel;
    private readonly CsvParserService _parser = new();

    public FileSelectionStep()
    {
        SuspendLayout();

        Padding = new Padding(16);

        // ── Title ────────────────────────────────────────────────────────────
        var titleLabel = new Label
        {
            Text     = "Select your bank CSV export file:",
            Font     = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 16)
        };

        // ── File selection row ───────────────────────────────────────────────
        var fileRowPanel = new Panel
        {
            Location = new Point(16, 48),
            Height   = 32,
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _filePathBox = new TextBox
        {
            ReadOnly = true,
            Dock     = DockStyle.Fill,
            Font     = new Font("Segoe UI", 10f)
        };

        _browseButton = new Button
        {
            Text   = "Browse...",
            Width  = 90,
            Dock   = DockStyle.Right,
            Height = 32
        };

        fileRowPanel.Controls.Add(_filePathBox);
        fileRowPanel.Controls.Add(_browseButton);

        // ── Load button ──────────────────────────────────────────────────────
        _loadButton = new Button
        {
            Text     = "Load File",
            Size     = new Size(100, 30),
            Location = new Point(16, 88),
            Enabled  = false
        };

        // ── Status label ─────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Text      = "No file loaded.",
            AutoSize  = true,
            Location  = new Point(125, 95),
            ForeColor = Color.DimGray,
            Font      = new Font("Segoe UI", 9.5f)
        };

        // ── Preview DataGridView ─────────────────────────────────────────────
        var previewLabel = new Label
        {
            Text     = "Preview (first 20 transactions):",
            AutoSize = true,
            Location = new Point(16, 128),
            Font     = new Font("Segoe UI", 10f)
        };

        _previewGrid = new DataGridView
        {
            Location          = new Point(16, 150),
            Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly          = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            SelectionMode     = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowHeadersVisible = false,
            BorderStyle       = BorderStyle.Fixed3D,
            BackgroundColor   = SystemColors.Window
        };

        _previewGrid.DataError += (_, e) => e.Cancel = true;

        BuildPreviewColumns();

        // ── Add controls ─────────────────────────────────────────────────────
        Controls.Add(titleLabel);
        Controls.Add(fileRowPanel);
        Controls.Add(_loadButton);
        Controls.Add(_statusLabel);
        Controls.Add(previewLabel);
        Controls.Add(_previewGrid);

        // ── Events ───────────────────────────────────────────────────────────
        _browseButton.Click += OnBrowse;
        _loadButton.Click   += OnLoad;

        Resize += OnResize;
        ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LayoutControls();
    }

    private void OnResize(object? sender, EventArgs e) => LayoutControls();

    private void LayoutControls()
    {
        int left  = 16;
        int right = ClientSize.Width - 16;
        int w     = right - left;

        // File row
        var fileRow = Controls.OfType<Panel>().FirstOrDefault();
        if (fileRow != null)
        {
            fileRow.Location = new Point(left, 48);
            fileRow.Width    = w;
        }

        // Preview grid
        _previewGrid.Location = new Point(left, 150);
        _previewGrid.Size     = new Size(w, Math.Max(100, ClientSize.Height - 170));

        // Resize columns proportionally
        if (_previewGrid.Columns.Count >= 4 && _previewGrid.Width > 0)
        {
            int gridWidth = _previewGrid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
            _previewGrid.Columns[0].Width = 90;    // Date
            _previewGrid.Columns[1].Width = 100;   // Amount
            _previewGrid.Columns[2].Width = 140;   // Counterpart
            _previewGrid.Columns[3].Width = Math.Max(80, gridWidth - 90 - 100 - 140 - 80); // Details
            _previewGrid.Columns[4].Width = 80;    // Status
        }
    }

    private void BuildPreviewColumns()
    {
        _previewGrid.Columns.Clear();
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",        HeaderText = "Date",        Width = 90  });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount",      HeaderText = "Amount",      Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Counterpart", HeaderText = "Counterpart", Width = 140 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Details",     HeaderText = "Details",     Width = 300 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status",      HeaderText = "Status",      Width = 80  });
    }

    public void Reset()
    {
        LoadedTransactions     = null;
        _filePathBox.Text      = "";
        _loadButton.Enabled    = false;
        _statusLabel.Text      = "No file loaded.";
        _statusLabel.ForeColor = Color.DimGray;
        _previewGrid.Rows.Clear();
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select bank CSV file",
            Filter = "CSV files|*.csv|All files|*.*"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _filePathBox.Text  = dlg.FileName;
        _loadButton.Enabled = true;
        LoadFile(dlg.FileName);
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_filePathBox.Text)) return;
        LoadFile(_filePathBox.Text);
    }

    private void LoadFile(string path)
    {
        try
        {
            var transactions = _parser.ParseFile(path);
            LoadedTransactions = transactions;

            int expenses = transactions.Count(t => t.IsExpense);
            int income   = transactions.Count - expenses;
            _statusLabel.Text      = $"{transactions.Count} transactions loaded ({expenses} expenses, {income} income)";
            _statusLabel.ForeColor = Color.DarkGreen;

            PopulatePreview(transactions);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading file:\n{ex.Message}", "Load Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text      = "Error loading file.";
            _statusLabel.ForeColor = Color.Red;
        }
    }

    private void PopulatePreview(List<Transaction> transactions)
    {
        _previewGrid.Rows.Clear();
        var preview = transactions.Take(20).ToList();
        foreach (var tx in preview)
        {
            var details = tx.Details.Length > 60 ? tx.Details[..60] + "…" : tx.Details;
            _previewGrid.Rows.Add(
                tx.ExecutionDate.ToString("dd/MM/yyyy"),
                tx.Amount.ToString("N2"),
                tx.Counterpart,
                details,
                tx.Status
            );
            // Color rows
            var row = _previewGrid.Rows[_previewGrid.Rows.Count - 1];
            row.DefaultCellStyle.BackColor = tx.IsExpense
                ? Color.FromArgb(255, 220, 220)
                : Color.FromArgb(220, 255, 220);
        }
    }
}
