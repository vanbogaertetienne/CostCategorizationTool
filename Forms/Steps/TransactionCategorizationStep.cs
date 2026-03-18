using CostCategorizationTool.Data;
using CostCategorizationTool.Forms;
using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms.Steps;

public class TransactionCategorizationStep : UserControl
{
    private readonly AppDatabase             _db;
    private readonly CategorizationService   _categorizationService = new();
    private readonly TransactionGrouperService _grouperService       = new();

    private List<Transaction>      _transactions = new();
    private List<Category>         _categories   = new();
    private List<TransactionGroup> _groups       = new();

    private bool _suppressEvents;

    private const int ColCount     = 0;
    private const int ColType      = 1;
    private const int ColPattern   = 2;
    private const int ColDir       = 3;
    private const int ColFrequency = 4;
    private const int ColTotal     = 5;
    private const int ColCategory  = 6;

    // ── Controls ─────────────────────────────────────────────────────────────
    private readonly Button       _btnAutoCateg;
    private readonly Button       _btnManageCats;
    private readonly Button       _btnSplit;
    private readonly CheckBox     _chkUncategorizedOnly;
    private readonly Label        _lblProgress;
    private readonly DataGridView _groupsGrid;
    private readonly Label        _lblDetailHeader;
    private readonly DataGridView _detailGrid;

    public TransactionCategorizationStep(AppDatabase db, AppSettings settings)
    {
        _db = db;
        SuspendLayout();
        BackColor = Color.White;

        // ── Top bar ──────────────────────────────────────────────────────────
        var topBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 44,
            BackColor = Color.White
        };

        _btnAutoCateg = new Button
        {
            Text     = "Auto-Categorize from Rules",
            Size     = new Size(190, 28),
            Location = new Point(8, 8)
        };

        _btnManageCats = new Button
        {
            Text     = "Manage Categories…",
            Size     = new Size(150, 28),
            Location = new Point(206, 8)
        };

        _btnSplit = new Button
        {
            Text     = "Split Group…",
            Size     = new Size(110, 28),
            Location = new Point(364, 8),
            Enabled  = false
        };

        _chkUncategorizedOnly = new CheckBox
        {
            Text      = "Show uncategorized only",
            AutoSize  = true,
            Location  = new Point(484, 12),
            Font      = new Font("Segoe UI", 9.5f)
        };

        _lblProgress = new Label
        {
            Text      = "",
            AutoSize  = true,
            Location  = new Point(668, 14),
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = Color.DimGray
        };

        topBar.Controls.AddRange(new Control[] { _btnAutoCateg, _btnManageCats, _btnSplit, _chkUncategorizedOnly, _lblProgress });

        // ── Groups grid ──────────────────────────────────────────────────────
        _groupsGrid = new DataGridView
        {
            Dock                        = DockStyle.Fill,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            RowHeadersVisible           = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.None,
            EditMode                    = DataGridViewEditMode.EditOnEnter,
            BorderStyle                 = BorderStyle.None,
            BackgroundColor             = SystemColors.Window,
            MultiSelect                 = false
        };

        _groupsGrid.DataError += (_, e) => e.Cancel = true;
        _groupsGrid.CellValueChanged += OnGroupCategoryChanged;
        _groupsGrid.SelectionChanged += OnGroupSelectionChanged;
        _groupsGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_groupsGrid.IsCurrentCellDirty)
                _groupsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        // ── Detail panel ─────────────────────────────────────────────────────
        _lblDetailHeader = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 24,
            Text      = "Select a group above to see its transactions",
            Font      = new Font("Segoe UI", 9f, FontStyle.Italic),
            ForeColor = Color.DimGray,
            Padding   = new Padding(4, 4, 0, 0)
        };

        _detailGrid = new DataGridView
        {
            Dock                        = DockStyle.Fill,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            RowHeadersVisible           = false,
            ReadOnly                    = true,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.None,
            BorderStyle                 = BorderStyle.None,
            BackgroundColor             = SystemColors.Window
        };

        // ── SplitContainer ───────────────────────────────────────────────────
        var detailPanel = new Panel { Dock = DockStyle.Fill };
        detailPanel.Controls.Add(_detailGrid);
        detailPanel.Controls.Add(_lblDetailHeader);

        var split = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Horizontal,
            SplitterDistance = 320,
            Panel1MinSize    = 120,
            Panel2MinSize    = 100
        };
        split.Panel1.Controls.Add(_groupsGrid);
        split.Panel2.Controls.Add(detailPanel);

        _btnAutoCateg.Click             += OnAutoCateg;
        _btnManageCats.Click            += OnManageCategories;
        _btnSplit.Click                 += OnSplitGroup;
        _chkUncategorizedOnly.CheckedChanged += (_, _) => PopulateGroupsGrid();

        Controls.Add(split);
        Controls.Add(topBar);

        ResumeLayout(false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void LoadTransactions(List<Transaction> transactions)
    {
        _transactions = transactions;
        _categories   = _db.GetCategories();

        // Auto-categorize with existing rules first
        var rules = _db.GetRules();
        _categorizationService.AutoCategorize(_transactions, rules);

        // Build groups
        _groups = _grouperService.Group(_transactions);

        // Map auto-categorized results back to groups
        MapCategoriesToGroups();

        BuildGrids();
        PopulateGroupsGrid();
        UpdateProgress();
    }

    public List<Transaction> GetTransactions() => _transactions;

    // ── Grid construction ────────────────────────────────────────────────────

    private void BuildGrids()
    {
        // Groups grid columns
        _groupsGrid.Columns.Clear();

        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Count", HeaderText = "#", Width = 38, ReadOnly = true });
        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Type", HeaderText = "Type", Width = 62, ReadOnly = true });

        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Pattern", HeaderText = "Pattern / Name", Width = 260, ReadOnly = true });

        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Direction", HeaderText = "Direction", Width = 80, ReadOnly = true });

        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Frequency", HeaderText = "Frequency", Width = 82, ReadOnly = true });

        var totalCol = new DataGridViewTextBoxColumn
            { Name = "Total", HeaderText = "Total", Width = 90, ReadOnly = true };
        totalCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _groupsGrid.Columns.Add(totalCol);

        var catCol = new DataGridViewComboBoxColumn
        {
            Name = "Category", HeaderText = "Category", Width = 170, FlatStyle = FlatStyle.Flat
        };
        catCol.Items.Add("");
        foreach (var cat in _categories)
            catCol.Items.Add(cat.Name);
        _groupsGrid.Columns.Add(catCol);

        // Make Pattern column fill remaining space
        _groupsGrid.Columns[ColPattern].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        // Detail grid columns
        _detailGrid.Columns.Clear();

        _detailGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Date", HeaderText = "Date", Width = 90 });

        var amtCol = new DataGridViewTextBoxColumn
            { Name = "Amount", HeaderText = "Amount", Width = 90 };
        amtCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _detailGrid.Columns.Add(amtCol);

        _detailGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Counterpart", HeaderText = "Counterpart", Width = 130 });

        var descCol = new DataGridViewTextBoxColumn
            { Name = "Description", HeaderText = "Description" };
        descCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _detailGrid.Columns.Add(descCol);
    }

    private void PopulateGroupsGrid()
    {
        _suppressEvents = true;
        _groupsGrid.Rows.Clear();

        var visibleGroups = _chkUncategorizedOnly.Checked
            ? _groups.Where(g => !g.CategoryId.HasValue).ToList()
            : _groups;

        foreach (var group in visibleGroups)
        {
            string typeLabel = group.RuleType == RuleType.IBAN ? "IBAN" : "Keyword";
            string catName   = group.CategoryId.HasValue
                ? _categories.FirstOrDefault(c => c.Id == group.CategoryId)?.Name ?? ""
                : "";

            _groupsGrid.Rows.Add(
                group.Count,
                typeLabel,
                group.DisplayName,
                group.DirectionLabel,
                group.FrequencyLabel,
                group.Total.ToString("N2"),
                catName);

            var row = _groupsGrid.Rows[_groupsGrid.Rows.Count - 1];
            row.Tag = group;

            StyleGroupRow(row, group);
        }

        _suppressEvents = false;
    }

    private static void StyleGroupRow(DataGridViewRow row, TransactionGroup group)
    {
        // Recurring groups (>1 tx) get a slightly bolder count cell
        bool recurring = group.Count > 1;
        row.Cells[ColCount].Style.Font      = new Font("Segoe UI", 9f,
            recurring ? FontStyle.Bold : FontStyle.Regular);
        row.Cells[ColCount].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        // Color rows by direction
        Color bg = group.DetectedSign == AmountSign.Positive
            ? Color.FromArgb(235, 255, 235)
            : group.DetectedSign == AmountSign.Negative
                ? Color.FromArgb(255, 238, 238)
                : Color.FromArgb(245, 245, 255);

        // Dim singletons slightly
        if (!recurring)
            bg = Blend(bg, Color.White, 0.4f);

        row.DefaultCellStyle.BackColor = bg;
    }

    private static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

    private void RefreshGroupCategoryColumn()
    {
        _suppressEvents = true;
        foreach (DataGridViewRow row in _groupsGrid.Rows)
        {
            if (row.Tag is not TransactionGroup group) continue;
            row.Cells[ColCategory].Value = group.CategoryId.HasValue
                ? _categories.FirstOrDefault(c => c.Id == group.CategoryId)?.Name ?? ""
                : "";
        }
        _suppressEvents = false;
        UpdateProgress();
    }

    private void PopulateDetailGrid(TransactionGroup group)
    {
        _detailGrid.Rows.Clear();

        foreach (var tx in group.Transactions.OrderBy(t => t.ExecutionDate))
        {
            _detailGrid.Rows.Add(
                tx.ExecutionDate.ToString("dd/MM/yyyy"),
                tx.Amount.ToString("N2"),
                string.IsNullOrWhiteSpace(tx.CounterpartName) ? tx.Counterpart : tx.CounterpartName,
                tx.Details);

            var row = _detailGrid.Rows[_detailGrid.Rows.Count - 1];
            row.Tag = tx;
            row.DefaultCellStyle.BackColor = tx.Amount >= 0
                ? Color.FromArgb(240, 255, 240)
                : Color.FromArgb(255, 242, 242);
        }

        _lblDetailHeader.Text      = $"Transactions in \"{group.DisplayName}\" — {group.Count} transaction(s), total: {group.Total:N2}";
        _lblDetailHeader.ForeColor = Color.FromArgb(50, 50, 120);
        _lblDetailHeader.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

    private void UpdateProgress()
    {
        int total = _groups.Count;
        int done  = _groups.Count(g => g.CategoryId.HasValue);
        _lblProgress.Text      = $"{done} of {total} groups categorized";
        _lblProgress.ForeColor = done == total && total > 0 ? Color.DarkGreen : Color.DimGray;
    }

    // ── Category mapping ─────────────────────────────────────────────────────

    /// <summary>
    /// After auto-categorize runs, maps the most common CategoryId in each group
    /// back to the group itself (so the ComboBox shows the right value).
    /// </summary>
    private void MapCategoriesToGroups()
    {
        foreach (var group in _groups)
        {
            group.CategoryId = group.Transactions
                .Where(t => t.CategoryId.HasValue)
                .GroupBy(t => t.CategoryId!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;
        }
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private void OnGroupCategoryChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressEvents) return;
        if (e.RowIndex < 0 || e.ColumnIndex != ColCategory) return;

        var row   = _groupsGrid.Rows[e.RowIndex];
        var group = row.Tag as TransactionGroup;
        if (group == null) return;

        var selectedName = row.Cells[ColCategory].Value as string;

        if (string.IsNullOrEmpty(selectedName))
        {
            group.CategoryId = null;
            foreach (var tx in group.Transactions) tx.CategoryId = null;
            UpdateProgress();
            return;
        }

        var cat = _categories.FirstOrDefault(c => c.Name == selectedName);
        if (cat == null) return;

        // Assign category to every transaction in the group
        group.CategoryId = cat.Id;
        foreach (var tx in group.Transactions) tx.CategoryId = cat.Id;

        // Save the rule (type and sign are known from the group)
        _db.AddAutoRule(cat.Id, group.RuleType, group.Pattern, group.DetectedSign);

        // Re-run auto-categorize — the new rule may auto-fill other groups too
        RunAutoCategAndRefresh();
    }

    private void OnGroupSelectionChanged(object? sender, EventArgs e)
    {
        if (_groupsGrid.SelectedRows.Count == 0) return;
        var group = _groupsGrid.SelectedRows[0].Tag as TransactionGroup;
        if (group == null) return;

        // Enable Split only for groups with multiple transactions
        _btnSplit.Enabled = group.Count > 1;

        PopulateDetailGrid(group);
    }

    private void OnSplitGroup(object? sender, EventArgs e)
    {
        if (_groupsGrid.SelectedRows.Count == 0) return;
        var group = _groupsGrid.SelectedRows[0].Tag as TransactionGroup;
        if (group == null || group.Count <= 1) return;

        var subGroups = _grouperService.SplitGroup(group);

        if (subGroups.Count <= 1)
        {
            MessageBox.Show(
                "No distinct sub-patterns were found within this group.\n" +
                "All transactions share the same keywords after removing the group pattern.",
                "Cannot Split Further",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Preserve the category already assigned to the original group
        foreach (var sg in subGroups)
            sg.CategoryId = group.CategoryId;

        // Replace the original group with the sub-groups
        int idx = _groups.IndexOf(group);
        _groups.RemoveAt(idx);
        _groups.InsertRange(idx, subGroups);

        PopulateGroupsGrid();
        UpdateProgress();

        // Select the first of the new sub-group rows
        if (_groupsGrid.Rows.Count > idx)
            _groupsGrid.Rows[idx].Selected = true;
    }

    private void OnAutoCateg(object? sender, EventArgs e)
    {
        RunAutoCategAndRefresh();
    }

    private void RunAutoCategAndRefresh()
    {
        var rules = _db.GetRules();
        _categorizationService.AutoCategorize(_transactions, rules);
        MapCategoriesToGroups();
        RefreshGroupCategoryColumn();
    }

    private void OnManageCategories(object? sender, EventArgs e)
    {
        using var dlg = new CategoryManagementDialog(_db);
        dlg.ShowDialog(ParentForm);
        ReloadCategories();
    }

    private void ReloadCategories()
    {
        _categories = _db.GetCategories();

        if (_groupsGrid.Columns.Count > ColCategory &&
            _groupsGrid.Columns[ColCategory] is DataGridViewComboBoxColumn catCol)
        {
            catCol.Items.Clear();
            catCol.Items.Add("");
            foreach (var cat in _categories)
                catCol.Items.Add(cat.Name);
        }

        RefreshGroupCategoryColumn();
    }
}
