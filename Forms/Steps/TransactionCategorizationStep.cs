using CostCategorizationTool.Data;
using CostCategorizationTool.Forms;
using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms.Steps;

public class TransactionCategorizationStep : UserControl
{
    private readonly AppDatabase  _db;
    private readonly AppSettings  _settings;
    private readonly CategorizationService        _categorizationService = new();
    private readonly IntelliCategorizationService _intelliService        = new();

    private List<Transaction> _transactions = new();
    private List<Category> _categories = new();

    // Sequence numbers of transactions the user has manually categorised this session.
    private readonly HashSet<string> _manuallyCategorized = new();

    private bool _suppressGridEvents;

    // ── Controls ─────────────────────────────────────────────────────────────
    private readonly Button       _btnAutoCateg;
    private readonly Label        _lblProgress;
    private readonly DataGridView _grid;

    // Bottom info panel
    private readonly TextBox _detailsBox;
    private readonly Label   _lblRuleInfo;

    public TransactionCategorizationStep(AppDatabase db, AppSettings settings)
    {
        _db       = db;
        _settings = settings;
        SuspendLayout();

        BackColor = Color.White;

        // ── Top bar ──────────────────────────────────────────────────────────
        _btnAutoCateg = new Button
        {
            Text     = "Auto-Categorize All",
            Size     = new Size(150, 28),
            Location = new Point(8, 8)
        };

        _lblProgress = new Label
        {
            Text      = "0 of 0 transactions categorized",
            AutoSize  = true,
            Location  = new Point(328, 14),
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = Color.DimGray
        };

        var _btnManageCats = new Button
        {
            Text     = "Manage Categories…",
            Size     = new Size(150, 28),
            Location = new Point(168, 8)
        };
        _btnManageCats.Click += OnManageCategories;

        // ── DataGridView ─────────────────────────────────────────────────────
        _grid = new DataGridView
        {
            Location                    = new Point(8, 44),
            Anchor                      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            RowHeadersVisible           = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.None,
            EditMode                    = DataGridViewEditMode.EditOnEnter,
            BorderStyle                 = BorderStyle.Fixed3D,
            BackgroundColor             = SystemColors.Window
        };

        _grid.DataError += (_, e) => e.Cancel = true;
        _grid.CellValueChanged += OnCellValueChanged;
        _grid.SelectionChanged += OnSelectionChanged;
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        // ── Bottom info panel ────────────────────────────────────────────────
        var bottomPanel = new Panel
        {
            Height    = 115,
            Dock      = DockStyle.Bottom,
            BackColor = Color.FromArgb(248, 248, 252),
            Padding   = new Padding(8)
        };

        var lblDetails = new Label
        {
            Text     = "Transaction Details:",
            AutoSize = true,
            Font     = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(8, 6)
        };

        _detailsBox = new TextBox
        {
            Location   = new Point(8, 24),
            Multiline  = true,
            ReadOnly   = true,
            ScrollBars = ScrollBars.Vertical,
            Font       = new Font("Consolas", 8.5f),
            BackColor  = Color.White,
            Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblRuleInfo = new Label
        {
            Text      = "",
            AutoSize  = false,
            Location  = new Point(8, 80),
            Height    = 24,
            Font      = new Font("Segoe UI", 9f, FontStyle.Italic),
            ForeColor = Color.DarkGreen,
            Anchor    = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        bottomPanel.Controls.Add(lblDetails);
        bottomPanel.Controls.Add(_detailsBox);
        bottomPanel.Controls.Add(_lblRuleInfo);

        // ── Add to UserControl ────────────────────────────────────────────────
        Controls.Add(_btnAutoCateg);
        Controls.Add(_btnManageCats);
        Controls.Add(_lblProgress);
        Controls.Add(_grid);
        Controls.Add(bottomPanel);

        _btnAutoCateg.Click += OnAutoCateg;

        Resize += OnResize;
        ResumeLayout(false);
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LayoutControls();
    }

    private void OnResize(object? sender, EventArgs e) => LayoutControls();

    private void LayoutControls()
    {
        const int bottomH = 115;
        const int topH    = 44;
        int gridH = Math.Max(60, ClientSize.Height - topH - bottomH - 4);
        int gridW = ClientSize.Width - 16;

        _grid.Location = new Point(8, topH);
        _grid.Size     = new Size(gridW, gridH);

        var bottomPanel = Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Bottom);
        if (bottomPanel != null)
        {
            _detailsBox.Width = bottomPanel.ClientSize.Width - 16;
            _lblRuleInfo.Width = bottomPanel.ClientSize.Width - 16;
        }

        ResizeGridColumns();
    }

    private void ResizeGridColumns()
    {
        if (_grid.Columns.Count < 5) return;
        int w = _grid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
        _grid.Columns[0].Width = 80;    // Date
        _grid.Columns[1].Width = 90;    // Amount
        _grid.Columns[2].Width = 140;   // Counterpart
        _grid.Columns[4].Width = 170;   // Category
        _grid.Columns[3].Width = Math.Max(60, w - 80 - 90 - 140 - 170 - 4); // Details
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void LoadTransactions(List<Transaction> transactions)
    {
        _transactions = transactions;
        _manuallyCategorized.Clear();
        _categories = _db.GetCategories();

        BuildGrid();
        PopulateGrid();
        PopulateGrid(); // second call ensures ComboBox cells are ready
        UpdateProgress();
        LayoutControls();
    }

    public List<Transaction> GetTransactions() => _transactions;

    // ── Grid construction ────────────────────────────────────────────────────

    private void BuildGrid()
    {
        _grid.Columns.Clear();

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Date", HeaderText = "Date", Width = 80, ReadOnly = true
        });

        var amountCol = new DataGridViewTextBoxColumn
        {
            Name = "Amount", HeaderText = "Amount", Width = 90, ReadOnly = true
        };
        amountCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns.Add(amountCol);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Counterpart", HeaderText = "Counterpart", Width = 140, ReadOnly = true
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Details", HeaderText = "Details", Width = 300, ReadOnly = true
        });

        var catCol = new DataGridViewComboBoxColumn
        {
            Name       = "Category",
            HeaderText = "Category",
            Width      = 170,
            FlatStyle  = FlatStyle.Flat
        };
        catCol.Items.Add(""); // blank = uncategorized
        foreach (var cat in _categories)
            catCol.Items.Add(cat.Name);
        _grid.Columns.Add(catCol);

        ResizeGridColumns();
    }

    private void PopulateGrid()
    {
        _suppressGridEvents = true;
        _grid.Rows.Clear();

        foreach (var tx in _transactions)
        {
            var details = tx.Details.Length > 70 ? tx.Details[..70] + "…" : tx.Details;
            var catName = tx.CategoryId.HasValue
                ? _categories.FirstOrDefault(c => c.Id == tx.CategoryId)?.Name ?? ""
                : "";

            _grid.Rows.Add(
                tx.ExecutionDate.ToString("dd/MM/yyyy"),
                tx.Amount.ToString("N2"),
                string.IsNullOrEmpty(tx.CounterpartName) ? tx.Counterpart : tx.CounterpartName,
                details,
                catName);

            var row = _grid.Rows[_grid.Rows.Count - 1];
            row.Tag = tx;
            row.DefaultCellStyle.BackColor = tx.IsExpense
                ? Color.FromArgb(255, 228, 228)
                : Color.FromArgb(228, 255, 228);
        }

        _suppressGridEvents = false;
    }

    /// <summary>Updates only the Category column cells — avoids full grid rebuild.</summary>
    private void RefreshGridCategories()
    {
        _suppressGridEvents = true;
        for (int i = 0; i < _grid.Rows.Count; i++)
        {
            var tx = _grid.Rows[i].Tag as Transaction;
            if (tx == null) continue;
            var catName = tx.CategoryId.HasValue
                ? _categories.FirstOrDefault(c => c.Id == tx.CategoryId)?.Name ?? ""
                : "";
            _grid.Rows[i].Cells[4].Value = catName;
        }
        _suppressGridEvents = false;
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        int total = _transactions.Count;
        int done  = _transactions.Count(t => t.CategoryId.HasValue);
        _lblProgress.Text      = $"{done} of {total} transactions categorized";
        _lblProgress.ForeColor = done == total && total > 0 ? Color.DarkGreen : Color.DimGray;
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private void OnAutoCateg(object? sender, EventArgs e)
    {
        var rules = _db.GetRules();
        foreach (var tx in _transactions)
            if (!_manuallyCategorized.Contains(tx.SequenceNumber))
                tx.CategoryId = null;

        _categorizationService.AutoCategorize(_transactions, rules);
        RefreshGridCategories();
    }

    private void OnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressGridEvents) return;
        if (e.RowIndex < 0 || e.ColumnIndex != 4) return;

        var row = _grid.Rows[e.RowIndex];
        var tx  = row.Tag as Transaction;
        if (tx == null) return;

        var selectedName = row.Cells[4].Value as string;

        if (string.IsNullOrEmpty(selectedName))
        {
            tx.CategoryId = null;
            _manuallyCategorized.Remove(tx.SequenceNumber);
            _lblRuleInfo.Text      = "";
            _lblRuleInfo.ForeColor = Color.DimGray;
            UpdateProgress();
            return;
        }

        var cat = _categories.FirstOrDefault(c => c.Name == selectedName);
        if (cat == null) return;

        // Save the previous category so we can revert if the user cancels.
        var prevCategoryId = tx.CategoryId;

        // ── Pre-compute candidate Details patterns (shown in the dialog) ────────
        var sameCatTxs = _transactions
            .Where(t => t != tx && t.CategoryId == cat.Id)
            .ToList();
        var suggestedPatterns = _intelliService.SuggestDetailsPatterns(tx, sameCatTxs);

        // ── Always ask the user how to handle the categorisation ──────────────
        var (choice, sign, chosenPattern) = RuleTypeSelectionDialog.Show(
            ParentForm,
            tx,
            cat.Name,
            suggestedPatterns,
            autoExpandDetails: _settings.ConfirmBeforeRuleModification);

        if (choice == RuleTypeSelectionDialog.RuleChoice.Cancelled)
        {
            // Revert the cell to the previous category value.
            _suppressGridEvents = true;
            row.Cells[4].Value = prevCategoryId.HasValue
                ? _categories.FirstOrDefault(c => c.Id == prevCategoryId)?.Name ?? ""
                : "";
            _suppressGridEvents = false;
            _lblRuleInfo.Text      = "Cancelled.";
            _lblRuleInfo.ForeColor = Color.DimGray;
            UpdateProgress();
            return;
        }

        // Commit the category assignment.
        tx.CategoryId = cat.Id;
        _manuallyCategorized.Add(tx.SequenceNumber);

        switch (choice)
        {
            case RuleTypeSelectionDialog.RuleChoice.JustThis:
                _lblRuleInfo.Text      = $"✓  \"{cat.Name}\" — this transaction only (no rule saved)";
                _lblRuleInfo.ForeColor = Color.SteelBlue;
                UpdateProgress();
                return; // No rule to save, no need to re-run auto-categorize.

            case RuleTypeSelectionDialog.RuleChoice.ByDescription:
                _db.AddAutoRule(cat.Id, RuleType.Details, chosenPattern, sign);
                _lblRuleInfo.Text      = $"✓  Rule saved: description contains \"{chosenPattern}\" → {cat.Name}";
                _lblRuleInfo.ForeColor = Color.DarkGreen;
                break;

            case RuleTypeSelectionDialog.RuleChoice.ByIBAN:
                _db.AddAutoRule(cat.Id, RuleType.IBAN, tx.Counterpart, sign);
                _lblRuleInfo.Text      = $"✓  Rule saved: IBAN {tx.Counterpart} → {cat.Name}";
                _lblRuleInfo.ForeColor = Color.DarkGreen;
                break;
        }

        // ── Re-run auto-categorize on non-manual transactions ─────────────────
        var rules = _db.GetRules();
        foreach (var t in _transactions)
            if (!_manuallyCategorized.Contains(t.SequenceNumber))
                t.CategoryId = null;
        _categorizationService.AutoCategorize(_transactions, rules);

        RefreshGridCategories();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0) return;
        var tx = _grid.SelectedRows[0].Tag as Transaction;
        if (tx == null) return;

        _detailsBox.Text = tx.Details;
    }

    private void OnManageCategories(object? sender, EventArgs e)
    {
        using var dlg = new CostCategorizationTool.Forms.CategoryManagementDialog(_db);
        dlg.ShowDialog(ParentForm);
        ReloadCategories();
    }

    private void ReloadCategories()
    {
        _categories = _db.GetCategories();
        if (_grid.Columns.Count >= 5 && _grid.Columns[4] is DataGridViewComboBoxColumn catCol)
        {
            catCol.Items.Clear();
            catCol.Items.Add("");
            foreach (var cat in _categories)
                catCol.Items.Add(cat.Name);
        }
        RefreshGridCategories();
    }
}
