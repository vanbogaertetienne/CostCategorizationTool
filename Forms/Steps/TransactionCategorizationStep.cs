using CostCategorizationTool.Data;
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
    private TransactionGroup? _selectedGroup;

    // Groups grid column indices
    private const int ColCount     = 0;
    private const int ColType      = 1;
    private const int ColPattern   = 2;
    private const int ColDir       = 3;
    private const int ColFrequency = 4;
    private const int ColTotal     = 5;
    private const int ColCategory  = 6;

    // Detail grid column indices
    private const int DColDate        = 0;
    private const int DColAmount      = 1;
    private const int DColCounterpart = 2;
    private const int DColDescription = 3;
    private const int DColCategory    = 4;

    // ── Controls ─────────────────────────────────────────────────────────────
    private readonly Button       _btnAutoCateg;
    private readonly Button       _btnManageCats;
    private readonly Button       _btnSplit;
    private readonly CheckBox     _chkUncategorizedOnly;
    private readonly Label        _lblProgress;
    private readonly DataGridView _groupsGrid;
    private readonly Label        _lblDetailHeader;
    private readonly DataGridView _detailGrid;

    public TransactionCategorizationStep(AppDatabase db)
    {
        _db = db;
        SuspendLayout();
        BackColor = Color.White;


        // ── Top bar ──────────────────────────────────────────────────────────
        var topBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 56,
            BackColor = Color.White
        };

        var btnFont = new Font("Segoe UI", 10f);
        int bx = 8, by = 8, bh = 40;

        _btnAutoCateg = new Button
        {
            Text     = Resources.BtnAutoCateg,
            Size     = new Size(BW(Resources.BtnAutoCateg, btnFont), bh),
            Location = new Point(bx, by),
            Font     = btnFont
        };
        bx += _btnAutoCateg.Width + 8;

        _btnManageCats = new Button
        {
            Text     = Resources.BtnManageCats,
            Size     = new Size(BW(Resources.BtnManageCats, btnFont), bh),
            Location = new Point(bx, by),
            Font     = btnFont
        };
        bx += _btnManageCats.Width + 8;

        _btnSplit = new Button
        {
            Text     = Resources.BtnSplitGroup,
            Size     = new Size(BW(Resources.BtnSplitGroup, btnFont), bh),
            Location = new Point(bx, by),
            Font     = btnFont,
            Enabled  = false
        };

        // Right-anchored container for checkbox + progress label
        var rightBar = new Panel
        {
            Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.White
        };
        // Size/location are set in the Resize handler; set initial values too:
        // (will be corrected on first layout)

        _chkUncategorizedOnly = new CheckBox
        {
            Text     = Resources.ChkUncategorized,
            AutoSize = true,
            Location = new Point(0, 10),
            Font     = new Font("Segoe UI", 10f)
        };

        _lblProgress = new Label
        {
            Text      = "",
            AutoSize  = false,
            Size      = new Size(240, 24),
            Location  = new Point(0, 12),   // X set in layout
            Font      = new Font("Segoe UI", 10f),
            ForeColor = Color.DimGray
        };

        rightBar.Controls.Add(_chkUncategorizedOnly);
        rightBar.Controls.Add(_lblProgress);
        topBar.Controls.Add(rightBar);

        // Layout handler to position rightBar and its children
        void layoutTopBar()
        {
            if (topBar.Width == 0) return;
            int chkW = _chkUncategorizedOnly.PreferredSize.Width + 8;
            int lblW = 220;
            int totalW = chkW + 16 + lblW;
            int rightBarX = Math.Max(_btnSplit.Right + 12, topBar.Width - totalW - 12);
            rightBar.Location = new Point(rightBarX, 0);
            rightBar.Size     = new Size(topBar.Width - rightBarX, topBar.Height);
            _chkUncategorizedOnly.Location = new Point(0, (topBar.Height - _chkUncategorizedOnly.PreferredSize.Height) / 2);
            _lblProgress.Location = new Point(chkW + 12, (topBar.Height - 24) / 2);
            _lblProgress.Width    = lblW;
        }
        topBar.Resize += (_, _) => layoutTopBar();
        Resize         += (_, _) => layoutTopBar();

        topBar.Controls.AddRange(new Control[] { _btnAutoCateg, _btnManageCats, _btnSplit });

        // ── Groups grid ──────────────────────────────────────────────────────
        _groupsGrid = new DataGridView
        {
            Dock                        = DockStyle.Fill,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            RowHeadersVisible           = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 36,
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.None,
            EditMode                    = DataGridViewEditMode.EditOnEnter,
            BorderStyle                 = BorderStyle.None,
            BackgroundColor             = SystemColors.Window,
            MultiSelect                 = true,
            Font                        = new Font("Segoe UI", 10f)
        };
        _groupsGrid.RowTemplate.Height = 40;

        _groupsGrid.DataError += (_, e) => e.Cancel = true;
        _groupsGrid.CellValueChanged += OnGroupCategoryChanged;
        _groupsGrid.SelectionChanged += OnGroupSelectionChanged;
        _groupsGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_suppressEvents) return;
            if (_groupsGrid.IsCurrentCellDirty)
                _groupsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _groupsGrid.SortCompare += (_, e) =>
        {
            if (e.Column.Index == ColCount || e.Column.Index == ColTotal)
            {
                decimal v1 = ParseDecimal(e.CellValue1);
                decimal v2 = ParseDecimal(e.CellValue2);
                e.SortResult = v1.CompareTo(v2);
                e.Handled    = true;
            }
        };

        // ── Context menu ─────────────────────────────────────────────────────
        var ctxMenu   = new ContextMenuStrip();
        var miCombine = new ToolStripMenuItem(Resources.CombineGroups);
        miCombine.Click += OnCombineGroups;
        ctxMenu.Items.Add(miCombine);
        ctxMenu.Opening += (_, e) =>
        {
            int sel = _groupsGrid.SelectedRows.Count;
            miCombine.Enabled = sel >= 2;
            e.Cancel = sel == 0;
        };
        _groupsGrid.ContextMenuStrip = ctxMenu;

        // ── Detail panel ─────────────────────────────────────────────────────
        _lblDetailHeader = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 24,
            Text      = Resources.SelectGroupPrompt,
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
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 36,
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.None,
            EditMode                    = DataGridViewEditMode.EditOnEnter,
            BorderStyle                 = BorderStyle.None,
            BackgroundColor             = SystemColors.Window,
            Font                        = new Font("Segoe UI", 10f)
        };
        _detailGrid.RowTemplate.Height = 40;
        _detailGrid.DataError += (_, e) => e.Cancel = true;
        _detailGrid.CellValueChanged += OnDetailCategoryChanged;
        _detailGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_suppressEvents) return;
            if (_detailGrid.CurrentCell?.OwningColumn?.Index == DColCategory
                && _detailGrid.IsCurrentCellDirty)
                _detailGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _detailGrid.SortCompare += (_, e) =>
        {
            if (e.Column.Index == DColAmount)
            {
                e.SortResult = ParseDecimal(e.CellValue1).CompareTo(ParseDecimal(e.CellValue2));
                e.Handled    = true;
            }
        };

        // ── SplitContainer ───────────────────────────────────────────────────
        var detailPanel = new Panel { Dock = DockStyle.Fill };
        detailPanel.Controls.Add(_detailGrid);
        detailPanel.Controls.Add(_lblDetailHeader);

        var split = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Horizontal,
            SplitterDistance = 340,
            SplitterWidth    = 8,
            Panel1MinSize    = 120,
            Panel2MinSize    = 120
        };
        split.Panel1.Controls.Add(_groupsGrid);
        split.Panel2.Controls.Add(detailPanel);

        _btnAutoCateg.Click                  += OnAutoCateg;
        _btnManageCats.Click                 += OnManageCategories;
        _btnSplit.Click                      += OnSplitGroup;
        _chkUncategorizedOnly.CheckedChanged += (_, _) => PopulateGroupsGrid();

        Controls.Add(split);
        Controls.Add(topBar);

        ResumeLayout(false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads transactions from the database and refreshes the UI.
    /// Does NOT run auto-categorization: categories stored in the database are
    /// shown exactly as-is, and no rule is applied automatically.
    /// Use the "Auto-Categorize from Rules" button for explicit rule application.
    /// </summary>
    public void LoadFromDatabase()
    {
        _transactions = _db.GetTransactions();
        _categories   = _db.GetCategories();
        _groups       = _grouperService.Group(_transactions);
        MapCategoriesToGroups();
        BuildGrids();
        PopulateGroupsGrid();
        UpdateProgress();
    }

    /// <summary>
    /// Parses the given CSV file and imports new transactions into the database.
    /// Returns counts of added and skipped records, then reloads the UI.
    /// </summary>
    public (int added, int skipped) ImportCsv(string csvPath)
    {
        var parser = new CsvParserService();
        var parsed = parser.ParseFile(csvPath);
        var (added, skipped) = _db.ImportTransactions(parsed);
        LoadFromDatabase();
        return (added, skipped);
    }

    /// <summary>Loads an in-memory list (used when carrying over from a CSV load).</summary>
    public void LoadTransactions(List<Transaction> transactions)
    {
        _transactions = transactions;
        _categories   = _db.GetCategories();

        var rules = _db.GetRules();
        _categorizationService.AutoCategorize(_transactions, rules);

        _groups = _grouperService.Group(_transactions);
        MapCategoriesToGroups();

        BuildGrids();
        PopulateGroupsGrid();
        UpdateProgress();
    }

    public List<Transaction>      GetTransactions() => _transactions;
    public List<TransactionGroup> GetGroups()       => _groups;

    // ── Grid construction ────────────────────────────────────────────────────

    private void BuildGrids()
    {

        // Groups grid columns
        _groupsGrid.Columns.Clear();

        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Count", HeaderText = Resources.GColCount, Width = 38, ReadOnly = true });
        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Type", HeaderText = Resources.GColType, Width = 62, ReadOnly = true });

        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Pattern", HeaderText = Resources.GColPattern, Width = 260, ReadOnly = true });

        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Direction", HeaderText = Resources.GColDirection, Width = 80, ReadOnly = true });

        _groupsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Frequency", HeaderText = Resources.GColFrequency, Width = 82, ReadOnly = true });

        var totalCol = new DataGridViewTextBoxColumn
            { Name = "Total", HeaderText = Resources.GColTotal, Width = 90, ReadOnly = true };
        totalCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _groupsGrid.Columns.Add(totalCol);

        var catCol = new DataGridViewComboBoxColumn
        {
            Name = "Category", HeaderText = Resources.GColCategory, Width = 170, FlatStyle = FlatStyle.Flat
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
            { Name = "Date", HeaderText = Resources.DColDate, Width = 100, ReadOnly = true });

        var amtCol = new DataGridViewTextBoxColumn
            { Name = "Amount", HeaderText = Resources.DColAmount, Width = 100, ReadOnly = true };
        amtCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _detailGrid.Columns.Add(amtCol);

        _detailGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Counterpart", HeaderText = Resources.DColCounterpart, Width = 150, ReadOnly = true });

        var descCol = new DataGridViewTextBoxColumn
            { Name = "Description", HeaderText = Resources.DColDescription, ReadOnly = true };
        descCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _detailGrid.Columns.Add(descCol);

        var detailCatCol = new DataGridViewComboBoxColumn
        {
            Name       = "Category",
            HeaderText = Resources.DColCategory,
            Width      = 180,
            FlatStyle  = FlatStyle.Flat,
            ReadOnly   = false
        };
        detailCatCol.Items.Add("");
        foreach (var cat in _categories)
            detailCatCol.Items.Add(cat.Name);
        _detailGrid.Columns.Add(detailCatCol);
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
            string typeLabel = group.RuleType == RuleType.IBAN ? Resources.TypeIban : Resources.TypeKeyword;
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
        FitColumns(_groupsGrid);
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

    private static int BW(string text, Font font) =>
        TextRenderer.MeasureText(text, font).Width + 24;

    private static void FitColumns(DataGridView grid)
    {
        if (!grid.IsHandleCreated || grid.Columns.Count == 0) return;
        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
                grid.AutoResizeColumn(col.Index, DataGridViewAutoSizeColumnMode.AllCells);
        }
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

        // Refresh detail grid override highlights when group categories change
        if (_selectedGroup != null) PopulateDetailGrid(_selectedGroup);
    }

    private void PopulateDetailGrid(TransactionGroup group)
    {
        _suppressEvents = true;
        _detailGrid.Rows.Clear();

        foreach (var tx in group.Transactions.OrderBy(t => t.ExecutionDate))
        {
            string catName = tx.CategoryId.HasValue
                ? _categories.FirstOrDefault(c => c.Id == tx.CategoryId)?.Name ?? ""
                : "";

            _detailGrid.Rows.Add(
                tx.ExecutionDate.ToString("dd/MM/yyyy"),
                tx.Amount.ToString("N2"),
                string.IsNullOrWhiteSpace(tx.CounterpartName) ? tx.Counterpart : tx.CounterpartName,
                tx.Details,
                catName);

            var row = _detailGrid.Rows[_detailGrid.Rows.Count - 1];
            row.Tag = tx;

            // Color base by income/expense
            row.DefaultCellStyle.BackColor = tx.Amount >= 0
                ? Color.FromArgb(240, 255, 240)
                : Color.FromArgb(255, 242, 242);

            // Highlight category cell when this transaction's category differs from the group
            bool overridden = tx.CategoryId != group.CategoryId;
            row.Cells[DColCategory].Style.BackColor = overridden
                ? Color.FromArgb(255, 245, 200)   // amber tint = individual override
                : row.DefaultCellStyle.BackColor;
        }

        _lblDetailHeader.Text      = string.Format(Resources.DetailHeaderFmt, group.DisplayName, group.Count, group.Total);
        _lblDetailHeader.ForeColor = Color.FromArgb(50, 50, 120);
        _lblDetailHeader.Font      = new Font("Segoe UI", 10f, FontStyle.Bold);
        _suppressEvents = false;
        FitColumns(_detailGrid);
    }

    private void UpdateProgress()
    {
        int total = _groups.Count;
        int done  = _groups.Count(g => g.CategoryId.HasValue);
        _lblProgress.Text      = string.Format(Resources.ProgressFmt, done, total);
        _lblProgress.ForeColor = done == total && total > 0 ? Color.DarkGreen : Color.DimGray;
    }

    // ── Category mapping ─────────────────────────────────────────────────────

    /// <summary>
    /// Derives each group's category from the majority of its transactions,
    /// then propagates that category back to every transaction in the group.
    /// This keeps the group and all its transactions in sync in both directions.
    /// </summary>
    /// <summary>
    /// Derives each group's displayed category from the majority of its transactions.
    /// Individual transaction categories are NOT overwritten — this allows per-transaction overrides.
    /// Group-level propagation only happens when the user explicitly assigns via the groups grid.
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
            _db.SaveTransactionCategories(group.Transactions);
            UpdateProgress();
            return;
        }

        var cat = _categories.FirstOrDefault(c => c.Name == selectedName);
        if (cat == null) return;

        // Assign category to every transaction in the group
        group.CategoryId = cat.Id;
        foreach (var tx in group.Transactions) tx.CategoryId = cat.Id;

        // Persist to database
        _db.SaveTransactionCategories(group.Transactions);

        // Save the rule so it is available when the user explicitly clicks
        // "Auto-Categorize from Rules". We do NOT auto-apply it to other groups
        // here — that would silently overwrite the user's deliberate choices.
        _db.AddAutoRule(cat.Id, group.RuleType, group.Pattern, group.DetectedSign);

        // Refresh display only
        RefreshGroupCategoryColumn();
    }

    private void OnGroupSelectionChanged(object? sender, EventArgs e)
    {
        int sel = _groupsGrid.SelectedRows.Count;

        if (sel == 0)
        {
            _selectedGroup    = null;
            _btnSplit.Enabled = false;
            _detailGrid.Rows.Clear();
            _lblDetailHeader.Text      = Resources.SelectGroupPrompt;
            _lblDetailHeader.ForeColor = Color.DimGray;
            _lblDetailHeader.Font      = new Font("Segoe UI", 9f, FontStyle.Italic);
            return;
        }

        // When exactly one row is selected show its transactions; otherwise keep showing
        // the last single-selection so the user can see what they're about to combine.
        if (sel == 1)
        {
            var group = _groupsGrid.SelectedRows[0].Tag as TransactionGroup;
            if (group == null) return;
            _selectedGroup    = group;
            _btnSplit.Enabled = group.Count > 1;
            PopulateDetailGrid(group);
        }
        else
        {
            // Multiple rows selected — disable split, keep detail panel showing last group
            _btnSplit.Enabled = false;
        }
    }

    private void OnDetailCategoryChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressEvents) return;
        if (e.RowIndex < 0 || e.ColumnIndex != DColCategory) return;

        var row = _detailGrid.Rows[e.RowIndex];
        if (row.Tag is not Transaction tx) return;

        var selectedName = row.Cells[DColCategory].Value as string;
        if (string.IsNullOrEmpty(selectedName))
        {
            tx.CategoryId = null;
        }
        else
        {
            var cat = _categories.FirstOrDefault(c => c.Name == selectedName);
            if (cat == null) return;
            tx.CategoryId = cat.Id;
        }

        // Persist the individual change
        _db.SaveTransactionCategories(new[] { tx });

        // Re-derive the group's displayed category from the majority
        if (_selectedGroup != null)
        {
            _selectedGroup.CategoryId = _selectedGroup.Transactions
                .Where(t => t.CategoryId.HasValue)
                .GroupBy(t => t.CategoryId!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            // Refresh group row in the groups grid
            foreach (DataGridViewRow groupRow in _groupsGrid.Rows)
            {
                if (groupRow.Tag == _selectedGroup)
                {
                    _suppressEvents = true;
                    groupRow.Cells[ColCategory].Value = _selectedGroup.CategoryId.HasValue
                        ? _categories.FirstOrDefault(c => c.Id == _selectedGroup.CategoryId)?.Name ?? ""
                        : "";
                    _suppressEvents = false;
                    break;
                }
            }

            // Refresh override highlights in detail grid
            PopulateDetailGrid(_selectedGroup);
        }

        UpdateProgress();
    }

    private void OnSplitGroup(object? sender, EventArgs e)
    {
        if (_groupsGrid.SelectedRows.Count == 0) return;
        var group = _groupsGrid.SelectedRows[0].Tag as TransactionGroup;
        if (group == null || group.Count <= 1) return;

        var subGroups = _grouperService.SplitGroup(group);

        if (subGroups.Count <= 1)
        {
            var groupTokens = string.Join(", ",
                IntelliCategorizationService.TokenizeAndClean(group.Pattern)
                    .Select(t => $"\"{t}\""));

            MessageBox.Show(
                string.Format(Resources.CannotSplitMsg,
                    group.Pattern,
                    groupTokens.Length > 0 ? groupTokens : "(none)"),
                Resources.CannotSplitTitle,
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

    private void OnCombineGroups(object? sender, EventArgs e)
    {
        // Collect selected groups (preserve visual order)
        var selectedGroups = _groupsGrid.Rows
            .Cast<DataGridViewRow>()
            .Where(r => r.Selected && r.Tag is TransactionGroup)
            .Select(r => (TransactionGroup)r.Tag!)
            .ToList();

        if (selectedGroups.Count < 2) return;

        // ── Determine combined pattern (intersection of each group's tokens) ──
        var tokenSets = selectedGroups
            .Select(g => IntelliCategorizationService.TokenizeAndClean(g.Pattern)
                         .ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToList();

        IEnumerable<string> commonTokens = tokenSets[0];
        for (int i = 1; i < tokenSets.Count; i++)
            commonTokens = commonTokens.Intersect(tokenSets[i], StringComparer.OrdinalIgnoreCase);

        var commonList = commonTokens.ToList();
        // Fallback: use the pattern of the group with the most transactions
        string newPattern = commonList.Count > 0
            ? string.Join(" ", commonList)
            : selectedGroups.OrderByDescending(g => g.Count).First().Pattern;

        // ── Merge transactions ────────────────────────────────────────────────
        var allTransactions = selectedGroups.SelectMany(g => g.Transactions).ToList();

        // ── Determine combined category (majority vote) ───────────────────────
        int? combinedCategoryId = allTransactions
            .Where(t => t.CategoryId.HasValue)
            .GroupBy(t => t.CategoryId!.Value)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        foreach (var tx in allTransactions)
            tx.CategoryId = combinedCategoryId;

        // ── Determine sign ────────────────────────────────────────────────────
        bool hasPos = allTransactions.Any(t => t.Amount > 0);
        bool hasNeg = allTransactions.Any(t => t.Amount < 0);
        AmountSign sign = (hasPos && hasNeg) ? AmountSign.Any
                        : hasPos             ? AmountSign.Positive
                                             : AmountSign.Negative;

        // ── Determine rule type ───────────────────────────────────────────────
        RuleType ruleType = selectedGroups.All(g => g.RuleType == RuleType.IBAN)
            ? RuleType.IBAN
            : RuleType.Details;

        var combinedGroup = new TransactionGroup
        {
            RuleType     = ruleType,
            Pattern      = newPattern,
            DisplayName  = newPattern,
            DetectedSign = sign,
            Transactions = allTransactions,
            CategoryId   = combinedCategoryId
        };

        // ── Replace selected groups with the combined group ───────────────────
        // Insert at the position of the topmost (first visible) selected group
        int insertAt = _groups.Count;
        foreach (var g in selectedGroups)
        {
            int i = _groups.IndexOf(g);
            if (i >= 0 && i < insertAt) insertAt = i;
        }
        foreach (var g in selectedGroups)
            _groups.Remove(g);
        _groups.Insert(Math.Min(insertAt, _groups.Count), combinedGroup);

        // ── Persist ───────────────────────────────────────────────────────────
        _db.SaveTransactionCategories(allTransactions);

        _selectedGroup = combinedGroup;
        PopulateGroupsGrid();
        UpdateProgress();

        // Select and scroll to the new combined row
        foreach (DataGridViewRow row in _groupsGrid.Rows)
        {
            if (row.Tag == combinedGroup)
            {
                row.Selected = true;
                _groupsGrid.FirstDisplayedScrollingRowIndex = row.Index;
                break;
            }
        }
    }

    private static decimal ParseDecimal(object? value) =>
        decimal.TryParse(value?.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.CurrentCulture,
            out var d) ? d : 0m;

    private void OnAutoCateg(object? sender, EventArgs e)
    {
        RunAutoCategAndRefresh();
    }

    private void RunAutoCategAndRefresh()
    {
        var rules = _db.GetRules();
        _categorizationService.AutoCategorize(_transactions, rules);
        MapCategoriesToGroups();
        _db.SaveTransactionCategories(_transactions);
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

        if (_detailGrid.Columns.Count > DColCategory &&
            _detailGrid.Columns[DColCategory] is DataGridViewComboBoxColumn detailCatCol)
        {
            detailCatCol.Items.Clear();
            detailCatCol.Items.Add("");
            foreach (var cat in _categories)
                detailCatCol.Items.Add(cat.Name);
        }

        RefreshGroupCategoryColumn();
        if (_selectedGroup != null) PopulateDetailGrid(_selectedGroup);
    }
}
