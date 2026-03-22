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

    // ── Filter state ──────────────────────────────────────────────────────────
    private bool _showUncategorizedOnly;
    private bool _showIncomingOnly;
    private bool _showOutgoingOnly;

    // Date filter
    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private string _dateFilterLabel = "";   // set after applying filter

    // Column filters (column index → filter string)
    private readonly Dictionary<int, string> _colFilters      = new();
    // Which column filters are exact-match (picked from list) vs. contains (custom input)
    private readonly HashSet<int>            _colFilterExact  = new();

    // Groups grid column indices
    private const int ColCount     = 0;
    private const int ColType      = 1;
    private const int ColPattern   = 2;
    private const int ColDir       = 3;
    private const int ColFrequency = 4;
    private const int ColTotal     = 5;
    private const int ColCategory  = 6;

    // Text-filterable columns (skip Count/Total which are numeric)
    private static readonly HashSet<int> TextColumns = new() { ColType, ColPattern, ColDir, ColFrequency, ColCategory };

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
    private readonly Label        _lblProgress;
    private readonly DataGridView _groupsGrid;
    private readonly Label        _lblDetailHeader;
    private readonly DataGridView _detailGrid;

    // Date filter bar controls
    private readonly Button _btnDateFilter;
    private readonly Button _btnDateFilterClear;

    public TransactionCategorizationStep(AppDatabase db)
    {
        _db = db;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode       = AutoScaleMode.Dpi;
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

        // Right-anchored container for progress label only
        var rightBar = new Panel
        {
            Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.White
        };

        _lblProgress = new Label
        {
            Text      = "",
            AutoSize  = false,
            Size      = new Size(240, 24),
            Location  = new Point(0, 12),
            Font      = new Font("Segoe UI", 10f),
            ForeColor = Color.DimGray
        };

        rightBar.Controls.Add(_lblProgress);
        topBar.Controls.Add(rightBar);

        void layoutTopBar()
        {
            if (topBar.Width == 0) return;
            int Sc(int v) => (int)(v * (float)DeviceDpi / 96f);
            int lblW      = Sc(240);
            int rightBarX = Math.Max(_btnSplit.Right + Sc(12), topBar.Width - lblW - Sc(12));
            rightBar.Location = new Point(rightBarX, 0);
            rightBar.Size     = new Size(topBar.Width - rightBarX, topBar.Height);
            _lblProgress.Location = new Point(0, (topBar.Height - Sc(24)) / 2);
            _lblProgress.Width    = lblW;
        }
        topBar.Resize += (_, _) => layoutTopBar();
        Resize         += (_, _) => layoutTopBar();

        topBar.Controls.AddRange(new Control[] { _btnAutoCateg, _btnManageCats, _btnSplit });

        // ── Date filter bar ───────────────────────────────────────────────────
        var filterBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 32,
            BackColor = Color.FromArgb(245, 248, 252)
        };

        var lblDate = new Label
        {
            Text      = Resources.DateFilterAll.Split(' ')[0] == "All" ? "Date:" : "Date :",
            AutoSize  = true,
            Location  = new Point(8, 7),
            Font      = new Font("Segoe UI", 9f)
        };
        // Always use the localized label "Date:"
        lblDate.Text = "Date:";

        _btnDateFilter = new Button
        {
            Text      = Resources.DateFilterAll,
            AutoSize  = false,
            Height    = 22,
            Width     = 160,
            Location  = new Point(50, 5),
            Font      = new Font("Segoe UI", 9f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
        _btnDateFilter.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 210);

        _btnDateFilterClear = new Button
        {
            Text      = "✕",
            AutoSize  = false,
            Height    = 22,
            Width     = 26,
            Location  = new Point(216, 5),
            Font      = new Font("Segoe UI", 9f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Visible   = false
        };
        _btnDateFilterClear.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 210);

        filterBar.Controls.AddRange(new Control[] { lblDate, _btnDateFilter, _btnDateFilterClear });

        _btnDateFilter.Click      += OnDateFilterClick;
        _btnDateFilterClear.Click += (_, _) => ClearDateFilter();

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
        _groupsGrid.Sorted += (_, _) => ReapplyGroupColors();

        // Column header right-click for filtering
        _groupsGrid.ColumnHeaderMouseClick += OnColumnHeaderMouseClick;

        // ── Context menu ─────────────────────────────────────────────────────
        var ctxMenu   = new ContextMenuStrip();
        var miCombine = new ToolStripMenuItem(Resources.CombineGroups);
        miCombine.Click += OnCombineGroups;
        ctxMenu.Items.Add(miCombine);
        ctxMenu.Opening += (_, e) =>
        {
            // Suppress the context menu when the click is on the column header row
            // so that OnColumnHeaderMouseClick can handle it instead.
            var hit = _groupsGrid.HitTest(
                _groupsGrid.PointToClient(Cursor.Position).X,
                _groupsGrid.PointToClient(Cursor.Position).Y);
            if (hit.Type == DataGridViewHitTestType.ColumnHeader)
            {
                e.Cancel = true;
                return;
            }

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

        _btnAutoCateg.Click  += OnAutoCateg;
        _btnManageCats.Click += OnManageCategories;
        _btnSplit.Click      += OnSplitGroup;

        Controls.Add(split);
        Controls.Add(filterBar);
        Controls.Add(topBar);

        ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        float sc = DeviceDpi / 96f;
        _groupsGrid.RowTemplate.Height  = (int)(40 * sc);
        _groupsGrid.ColumnHeadersHeight = (int)(36 * sc);
        _detailGrid.RowTemplate.Height  = (int)(40 * sc);
        _detailGrid.ColumnHeadersHeight = (int)(36 * sc);

        // Defer SplitterDistance until after layout so the container has its final Height.
        if (Controls.OfType<SplitContainer>().FirstOrDefault() is { } split)
        {
            int desired = (int)(340 * sc);
            BeginInvoke(() =>
            {
                int max = split.Height - split.Panel2MinSize - split.SplitterWidth;
                if (max > split.Panel1MinSize)
                    split.SplitterDistance = Math.Clamp(desired, split.Panel1MinSize, max);
            });
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void LoadFromDatabase()
    {
        _transactions = _db.GetTransactions();
        _categories   = _db.GetCategories().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        _groups       = _grouperService.Group(_transactions);
        MapCategoriesToGroups();
        BuildGrids();
        PopulateGroupsGrid();
        UpdateProgress();
    }

    public (int added, int skipped) ImportCsv(string csvPath)
    {
        var parser = new CsvParserService();
        var parsed = parser.ParseFile(csvPath);
        var (added, skipped) = _db.ImportTransactions(parsed);
        LoadFromDatabase();
        return (added, skipped);
    }

    public void LoadTransactions(List<Transaction> transactions)
    {
        _transactions = transactions;
        _categories   = _db.GetCategories().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

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

    // ── View filter public API (called from MainForm View menu) ───────────────

    public void SetUncategorizedOnly(bool value)
    {
        _showUncategorizedOnly = value;
        PopulateGroupsGrid();
    }

    public void SetIncomingOnly(bool value)
    {
        _showIncomingOnly = value;
        if (value) _showOutgoingOnly = false;
        PopulateGroupsGrid();
    }

    public void SetOutgoingOnly(bool value)
    {
        _showOutgoingOnly = value;
        if (value) _showIncomingOnly = false;
        PopulateGroupsGrid();
    }

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

        var catCol = new FlatComboBoxColumn
        {
            Name = "Category", HeaderText = Resources.GColCategory, Width = 170, FlatStyle = FlatStyle.Flat
        };
        catCol.Items.Add(Resources.NewCategoryItem);
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

        var detailCatCol = new FlatComboBoxColumn
        {
            Name       = "Category",
            HeaderText = Resources.DColCategory,
            Width      = 180,
            FlatStyle  = FlatStyle.Flat,
            ReadOnly   = false
        };
        detailCatCol.Items.Add(Resources.NewCategoryItem);
        detailCatCol.Items.Add("");
        foreach (var cat in _categories)
            detailCatCol.Items.Add(cat.Name);
        _detailGrid.Columns.Add(detailCatCol);
    }

    private void PopulateGroupsGrid()
    {
        _suppressEvents = true;
        _groupsGrid.Rows.Clear();

        // Step 1: Date filter — regroup from filtered transactions
        List<TransactionGroup> baseGroups;
        if (_dateFrom.HasValue || _dateTo.HasValue)
        {
            var filteredTxs = _transactions.Where(t =>
                (!_dateFrom.HasValue || t.ExecutionDate >= _dateFrom.Value) &&
                (!_dateTo.HasValue   || t.ExecutionDate <= _dateTo.Value))
                .ToList();

            baseGroups = _grouperService.Group(filteredTxs);

            // Re-apply categories to filtered groups by matching Pattern + RuleType.
            // Use assignment-loop instead of ToDictionary to silently handle duplicate keys
            // (same pattern can appear with different DetectedSign in _groups).
            var categoryLookup = new Dictionary<(string, RuleType), int?>();
            foreach (var g in _groups.Where(g => g.CategoryId.HasValue))
                categoryLookup[(g.Pattern, g.RuleType)] = g.CategoryId;
            foreach (var g in baseGroups)
            {
                if (categoryLookup.TryGetValue((g.Pattern, g.RuleType), out var catId))
                    g.CategoryId = catId;
            }
        }
        else
        {
            baseGroups = _groups;
        }

        // Step 2: Incoming / Outgoing filter
        IEnumerable<TransactionGroup> visibleGroups = baseGroups;
        if (_showIncomingOnly)
            visibleGroups = visibleGroups.Where(g => g.DetectedSign == AmountSign.Positive);
        else if (_showOutgoingOnly)
            visibleGroups = visibleGroups.Where(g => g.DetectedSign == AmountSign.Negative);

        // Step 3: Uncategorized only
        if (_showUncategorizedOnly)
            visibleGroups = visibleGroups.Where(g => !g.CategoryId.HasValue);

        // Step 4: Column text filters
        if (_colFilters.Count > 0)
        {
            visibleGroups = visibleGroups.Where(g =>
            {
                foreach (var kv in _colFilters)
                {
                    string cellText = GetGroupColumnText(g, kv.Key);
                    bool match = _colFilterExact.Contains(kv.Key)
                        ? cellText.Equals(kv.Value, StringComparison.OrdinalIgnoreCase)
                        : cellText.Contains(kv.Value, StringComparison.OrdinalIgnoreCase);
                    if (!match) return false;
                }
                return true;
            });
        }

        // Step 5: Display rows
        int rowIndex = 0;
        foreach (var group in visibleGroups)
        {
            string catName = group.CategoryId.HasValue
                ? _categories.FirstOrDefault(c => c.Id == group.CategoryId)?.Name ?? ""
                : "";

            _groupsGrid.Rows.Add(
                group.Count,
                group.RuleType == RuleType.IBAN ? Resources.TypeIban : Resources.TypeKeyword,
                group.DisplayName,
                group.DirectionLabel,
                group.FrequencyLabel,
                group.Total.ToString("N2"),
                catName);

            var row = _groupsGrid.Rows[_groupsGrid.Rows.Count - 1];
            row.Tag = group;

            StyleGroupRow(row, group, rowIndex);
            rowIndex++;
        }

        _suppressEvents = false;
        FitColumns(_groupsGrid);

        // Re-trigger detail view: events were suppressed during row rebuild so the
        // detail grid stays in sync with whatever row is now selected.
        if (_groupsGrid.SelectedRows.Count > 0)
            OnGroupSelectionChanged(null, EventArgs.Empty);
        else if (_groupsGrid.Rows.Count > 0)
            _groupsGrid.Rows[0].Selected = true;   // fires SelectionChanged → OnGroupSelectionChanged
    }

    private string GetGroupColumnText(TransactionGroup g, int colIdx) => colIdx switch
    {
        ColType      => g.RuleType == RuleType.IBAN ? Resources.TypeIban : Resources.TypeKeyword,
        ColPattern   => g.DisplayName,
        ColDir       => g.DirectionLabel,
        ColFrequency => g.FrequencyLabel,
        ColCategory  => g.CategoryId.HasValue
                            ? _categories.FirstOrDefault(c => c.Id == g.CategoryId)?.Name ?? ""
                            : "",
        _            => ""
    };

    private static void StyleGroupRow(DataGridViewRow row, TransactionGroup group, int rowIndex = 0)
    {
        bool recurring = group.Count > 1;
        row.Cells[ColCount].Style.Font      = new Font("Segoe UI", 9f,
            recurring ? FontStyle.Bold : FontStyle.Regular);
        row.Cells[ColCount].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        Color bg = group.DetectedSign == AmountSign.Positive
            ? Color.FromArgb(235, 255, 235)
            : group.DetectedSign == AmountSign.Negative
                ? Color.FromArgb(255, 238, 238)
                : Color.FromArgb(245, 245, 255);

        if (!recurring)
            bg = Blend(bg, Color.White, 0.4f);

        // Alternating row shading (odd rows slightly lighter)
        if (rowIndex % 2 == 1)
            bg = Color.FromArgb(
                Math.Min(255, bg.R + 8),
                Math.Min(255, bg.G + 8),
                Math.Min(255, bg.B + 8));

        row.DefaultCellStyle.BackColor = bg;
    }

    private void ReapplyGroupColors()
    {
        int rowIndex = 0;
        foreach (DataGridViewRow row in _groupsGrid.Rows)
        {
            if (row.Tag is TransactionGroup group)
                StyleGroupRow(row, group, rowIndex);
            rowIndex++;
        }
    }

    private static int BW(string text, Font font) => UiScaler.BW(text, font);

    private static void FitColumns(DataGridView grid)
    {
        if (!grid.IsHandleCreated || grid.Columns.Count == 0) return;
        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) continue;
            grid.AutoResizeColumn(col.Index, DataGridViewAutoSizeColumnMode.AllCells);
            if (col is DataGridViewComboBoxColumn combo && combo.Items.Count > 0)
            {
                int itemW = combo.Items.Cast<object>()
                    .Max(item => TextRenderer.MeasureText(item?.ToString() ?? "", grid.Font).Width);
                int needed = itemW + 36;
                if (col.Width < needed) col.Width = needed;
            }
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

            row.DefaultCellStyle.BackColor = tx.Amount >= 0
                ? Color.FromArgb(240, 255, 240)
                : Color.FromArgb(255, 242, 242);

            bool overridden = tx.CategoryId != group.CategoryId;
            row.Cells[DColCategory].Style.BackColor = overridden
                ? Color.FromArgb(255, 245, 200)
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

    // ── Date Filter ──────────────────────────────────────────────────────────

    private void OnDateFilterClick(object? sender, EventArgs e)
    {
        var menu = new ContextMenuStrip();

        void AddItem(string text, Action action)
        {
            var mi = new ToolStripMenuItem(text);
            mi.Click += (_, _) => action();
            menu.Items.Add(mi);
        }

        AddItem(Resources.DateFilterAll, () => ClearDateFilter());
        AddItem(Resources.DateFilterThisYear, () => ApplyDateFilter(
            new DateTime(DateTime.Today.Year, 1, 1),
            new DateTime(DateTime.Today.Year, 12, 31),
            Resources.DateFilterThisYear));
        AddItem(Resources.DateFilterLastYear, () => ApplyDateFilter(
            new DateTime(DateTime.Today.Year - 1, 1, 1),
            new DateTime(DateTime.Today.Year - 1, 12, 31),
            Resources.DateFilterLastYear));
        AddItem(Resources.DateFilterThisQuarter, () =>
        {
            int q = (DateTime.Today.Month - 1) / 3;
            ApplyDateFilter(
                new DateTime(DateTime.Today.Year, q * 3 + 1, 1),
                new DateTime(DateTime.Today.Year, q * 3 + 1, 1).AddMonths(3).AddDays(-1),
                Resources.DateFilterThisQuarter);
        });
        AddItem(Resources.DateFilterLastQuarter, () =>
        {
            int q = ((DateTime.Today.Month - 1) / 3 + 3) % 4;   // previous quarter
            int year = DateTime.Today.Month <= 3 ? DateTime.Today.Year - 1 : DateTime.Today.Year;
            ApplyDateFilter(
                new DateTime(year, q * 3 + 1, 1),
                new DateTime(year, q * 3 + 1, 1).AddMonths(3).AddDays(-1),
                Resources.DateFilterLastQuarter);
        });
        menu.Items.Add(new ToolStripSeparator());
        AddItem(Resources.DateFilterCustom, () => ShowCustomDateFilter());

        menu.Show(_btnDateFilter, new Point(0, _btnDateFilter.Height));
    }

    private void ApplyDateFilter(DateTime from, DateTime to, string label)
    {
        _dateFrom = from;
        _dateTo   = to;
        _dateFilterLabel = label;
        _btnDateFilter.Text = label;
        _btnDateFilterClear.Visible = true;
        PopulateGroupsGrid();
    }

    private void ClearDateFilter()
    {
        _dateFrom = null;
        _dateTo   = null;
        _dateFilterLabel = "";
        _btnDateFilter.Text = Resources.DateFilterAll;
        _btnDateFilterClear.Visible = false;
        PopulateGroupsGrid();
    }

    private void ShowCustomDateFilter()
    {
        using var dlg = new Form
        {
            Text            = Resources.DateFilterCustom,
            Size            = new Size(320, 170),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            MaximizeBox     = false,
            MinimizeBox     = false
        };

        var lblFrom = new Label { Text = Resources.DateFilterFrom, Location = new Point(12, 16), AutoSize = true };
        var dtpFrom = new DateTimePicker
        {
            Location = new Point(80, 12),
            Size     = new Size(200, 24),
            Format   = DateTimePickerFormat.Short,
            Value    = _dateFrom ?? DateTime.Today.AddYears(-1)
        };

        var lblTo = new Label { Text = Resources.DateFilterTo, Location = new Point(12, 52), AutoSize = true };
        var dtpTo = new DateTimePicker
        {
            Location = new Point(80, 48),
            Size     = new Size(200, 24),
            Format   = DateTimePickerFormat.Short,
            Value    = _dateTo ?? DateTime.Today
        };

        var btnOk = new Button
        {
            Text         = "OK",
            DialogResult = DialogResult.OK,
            Location     = new Point(116, 90),
            Size         = new Size(80, 28)
        };
        var btnCancel = new Button
        {
            Text         = Resources.Cancel,
            DialogResult = DialogResult.Cancel,
            Location     = new Point(204, 90),
            Size         = new Size(80, 28)
        };

        dlg.Controls.AddRange(new Control[] { lblFrom, dtpFrom, lblTo, dtpTo, btnOk, btnCancel });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        if (dlg.ShowDialog(ParentForm) == DialogResult.OK)
        {
            string label = $"{dtpFrom.Value:dd/MM/yyyy} – {dtpTo.Value:dd/MM/yyyy}";
            ApplyDateFilter(dtpFrom.Value.Date, dtpTo.Value.Date, label);
        }
    }

    // ── Column Header Filter ──────────────────────────────────────────────────

    private void OnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        if (e.ColumnIndex < 0) return;

        int colIdx = e.ColumnIndex;
        if (!TextColumns.Contains(colIdx)) return;

        string colHeader = _groupsGrid.Columns[colIdx].HeaderText.TrimStart('▼', ' ');
        bool hasFilter   = _colFilters.ContainsKey(colIdx);

        // Collect distinct values with frequency counts from ALL groups
        var valueCounts = _groups
            .Select(g => GetGroupColumnText(g, colIdx))
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Value: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var menu = new ContextMenuStrip();
        menu.Font = _groupsGrid.Font;

        const int threshold = 10;

        if (valueCounts.Count >= threshold)
        {
            // Custom "contains" filter at the top
            var miCustom = new ToolStripMenuItem(Resources.ColFilterMenuItem);
            miCustom.Click += (_, _) => ShowColumnFilterCustom(colIdx, colHeader);
            menu.Items.Add(miCustom);
            menu.Items.Add(new ToolStripSeparator());
        }

        // Value list: all if < threshold, top 10 otherwise
        var valuesToShow = valueCounts.Count < threshold
            ? valueCounts
            : valueCounts.Take(threshold).ToList();

        string? activeFilter = hasFilter ? _colFilters[colIdx] : null;

        foreach (var (val, cnt) in valuesToShow)
        {
            string label = string.IsNullOrEmpty(val)
                ? $"(empty)  [{cnt}]"
                : $"{val}  [{cnt}]";
            var mi = new ToolStripMenuItem(label);
            // Checkmark if this value is the active filter
            mi.Checked = hasFilter
                && _colFilterExact.Contains(colIdx)
                && string.Equals(activeFilter, val, StringComparison.OrdinalIgnoreCase);
            string captured = val;
            mi.Click += (_, _) => ApplyColumnFilter(colIdx, captured, exact: true);
            menu.Items.Add(mi);
        }

        menu.Items.Add(new ToolStripSeparator());

        var miClear = new ToolStripMenuItem(Resources.ColFilterClear);
        miClear.Enabled = hasFilter;
        miClear.Click += (_, _) => ClearColumnFilter(colIdx);
        menu.Items.Add(miClear);

        var miClearAll = new ToolStripMenuItem(Resources.ColFilterClearAll);
        miClearAll.Enabled = _colFilters.Count > 0;
        miClearAll.Click += (_, _) => ClearAllColumnFilters();
        menu.Items.Add(miClearAll);

        var headerRect = _groupsGrid.GetColumnDisplayRectangle(colIdx, true);
        menu.Show(_groupsGrid, new Point(headerRect.Left, _groupsGrid.ColumnHeadersHeight));
    }

    private void ShowColumnFilterCustom(int colIdx, string colHeader)
    {
        string prompt  = string.Format(Resources.ColFilterPrompt, colHeader);
        string current = (_colFilters.TryGetValue(colIdx, out var existing) &&
                          !_colFilterExact.Contains(colIdx)) ? existing : "";

        string? result = ShowInputDialog(Resources.ColFilterTitle, prompt, current);
        if (result == null) return;

        if (string.IsNullOrWhiteSpace(result))
            ClearColumnFilter(colIdx);
        else
            ApplyColumnFilter(colIdx, result.Trim(), exact: false);
    }

    private void ApplyColumnFilter(int colIdx, string value, bool exact)
    {
        _colFilters[colIdx] = value;
        if (exact) _colFilterExact.Add(colIdx);
        else       _colFilterExact.Remove(colIdx);
        UpdateColumnFilterIndicator(colIdx, true);
        PopulateGroupsGrid();
    }

    private void ClearColumnFilter(int colIdx)
    {
        _colFilters.Remove(colIdx);
        _colFilterExact.Remove(colIdx);
        UpdateColumnFilterIndicator(colIdx, false);
        PopulateGroupsGrid();
    }

    private void ClearAllColumnFilters()
    {
        var keys = _colFilters.Keys.ToList();
        _colFilters.Clear();
        _colFilterExact.Clear();
        foreach (int k in keys)
            UpdateColumnFilterIndicator(k, false);
        PopulateGroupsGrid();
    }

    private void UpdateColumnFilterIndicator(int colIdx, bool active)
    {
        if (colIdx < 0 || colIdx >= _groupsGrid.Columns.Count) return;
        var col = _groupsGrid.Columns[colIdx];
        string baseText = col.HeaderText.TrimStart('▼', ' ');
        col.HeaderText = active ? $"▼ {baseText}" : baseText;
    }

    // ── Category mapping ─────────────────────────────────────────────────────

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

        // Handle "New category..." option
        if (selectedName == Resources.NewCategoryItem)
        {
            string? newName = ShowInputDialog(Resources.NewCatTitle, Resources.NewCatPrompt, "");
            if (!string.IsNullOrWhiteSpace(newName))
            {
                _db.AddCategory(newName.Trim());
                ReloadCategories();
                // Now set the newly created category
                var newCat = _categories.FirstOrDefault(c => c.Name == newName.Trim());
                if (newCat != null)
                {
                    _suppressEvents = true;
                    row.Cells[ColCategory].Value = newCat.Name;
                    _suppressEvents = false;
                    group.CategoryId = newCat.Id;
                    foreach (var tx in group.Transactions) tx.CategoryId = newCat.Id;
                    _db.SaveTransactionCategories(group.Transactions);
                    _db.AddAutoRule(newCat.Id, group.RuleType, group.Pattern, group.DetectedSign);
                    RefreshGroupCategoryColumn();
                }
                else
                {
                    _suppressEvents = true;
                    row.Cells[ColCategory].Value = group.CategoryId.HasValue
                        ? _categories.FirstOrDefault(c => c.Id == group.CategoryId)?.Name ?? ""
                        : "";
                    _suppressEvents = false;
                }
            }
            else
            {
                // User cancelled — restore previous value
                _suppressEvents = true;
                row.Cells[ColCategory].Value = group.CategoryId.HasValue
                    ? _categories.FirstOrDefault(c => c.Id == group.CategoryId)?.Name ?? ""
                    : "";
                _suppressEvents = false;
            }
            return;
        }

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

        group.CategoryId = cat.Id;
        foreach (var tx in group.Transactions) tx.CategoryId = cat.Id;

        _db.SaveTransactionCategories(group.Transactions);
        _db.AddAutoRule(cat.Id, group.RuleType, group.Pattern, group.DetectedSign);

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

        // Handle "New category..." option
        if (selectedName == Resources.NewCategoryItem)
        {
            string? newName = ShowInputDialog(Resources.NewCatTitle, Resources.NewCatPrompt, "");
            if (!string.IsNullOrWhiteSpace(newName))
            {
                _db.AddCategory(newName.Trim());
                ReloadCategories();
                var newCat = _categories.FirstOrDefault(c => c.Name == newName.Trim());
                if (newCat != null)
                {
                    tx.CategoryId = newCat.Id;
                    _suppressEvents = true;
                    row.Cells[DColCategory].Value = newCat.Name;
                    _suppressEvents = false;
                }
                else
                {
                    _suppressEvents = true;
                    row.Cells[DColCategory].Value = tx.CategoryId.HasValue
                        ? _categories.FirstOrDefault(c => c.Id == tx.CategoryId)?.Name ?? ""
                        : "";
                    _suppressEvents = false;
                    return;
                }
            }
            else
            {
                _suppressEvents = true;
                row.Cells[DColCategory].Value = tx.CategoryId.HasValue
                    ? _categories.FirstOrDefault(c => c.Id == tx.CategoryId)?.Name ?? ""
                    : "";
                _suppressEvents = false;
                return;
            }
        }
        else if (string.IsNullOrEmpty(selectedName))
        {
            tx.CategoryId = null;
        }
        else
        {
            var cat = _categories.FirstOrDefault(c => c.Name == selectedName);
            if (cat == null) return;
            tx.CategoryId = cat.Id;
        }

        _db.SaveTransactionCategories(new[] { tx });

        if (_selectedGroup != null)
        {
            _selectedGroup.CategoryId = _selectedGroup.Transactions
                .Where(t => t.CategoryId.HasValue)
                .GroupBy(t => t.CategoryId!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

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

            PopulateDetailGrid(_selectedGroup);
        }

        UpdateProgress();
    }

    private void OnSplitGroup(object? sender, EventArgs e)
    {
        if (_groupsGrid.SelectedRows.Count == 0) return;
        var group = _groupsGrid.SelectedRows[0].Tag as TransactionGroup;
        if (group == null || group.Count <= 1) return;

        // Collect distinct tokens from transaction Details, ordered by frequency
        var candidateTokens = group.Transactions
            .SelectMany(t => IntelliCategorizationService.TokenizeAndClean(t.Details))
            .GroupBy(tok => tok, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();

        using var dlg = new SplitGroupDialog(group.DisplayName, candidateTokens, group.Transactions);
        if (dlg.ShowDialog(ParentForm) != DialogResult.OK) return;

        string token = dlg.SelectedToken;
        if (string.IsNullOrWhiteSpace(token)) return;

        var matching = group.Transactions
            .Where(t => t.Details.Contains(token, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matching.Count == 0)
        {
            MessageBox.Show(string.Format(Resources.SplitNoMatch, token),
                Resources.CannotSplitTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (matching.Count == group.Count)
        {
            MessageBox.Show(string.Format(Resources.SplitAllMatch, token),
                Resources.CannotSplitTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool hasPos  = matching.Any(t => t.Amount > 0);
        bool hasNeg  = matching.Any(t => t.Amount < 0);
        AmountSign sign = hasPos && hasNeg ? AmountSign.Any
                        : hasPos           ? AmountSign.Positive
                                           : AmountSign.Negative;

        var newGroup = new TransactionGroup
        {
            RuleType     = RuleType.Details,
            Pattern      = token,
            DisplayName  = token,
            DetectedSign = sign,
            Transactions = matching
        };

        // Remove matching transactions from old group (Transactions list is mutable)
        foreach (var tx in matching)
            group.Transactions.Remove(tx);

        // Insert new group directly after the old one
        int idx = _groups.IndexOf(group);
        _groups.Insert(idx + 1, newGroup);

        PopulateGroupsGrid();
        UpdateProgress();
    }

    private void OnCombineGroups(object? sender, EventArgs e)
    {
        var selectedGroups = _groupsGrid.Rows
            .Cast<DataGridViewRow>()
            .Where(r => r.Selected && r.Tag is TransactionGroup)
            .Select(r => (TransactionGroup)r.Tag!)
            .ToList();

        if (selectedGroups.Count < 2) return;

        var tokenSets = selectedGroups
            .Select(g => IntelliCategorizationService.TokenizeAndClean(g.Pattern)
                         .ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToList();

        IEnumerable<string> commonTokens = tokenSets[0];
        for (int i = 1; i < tokenSets.Count; i++)
            commonTokens = commonTokens.Intersect(tokenSets[i], StringComparer.OrdinalIgnoreCase);

        var commonList = commonTokens.ToList();
        string newPattern = commonList.Count > 0
            ? string.Join(" ", commonList)
            : selectedGroups.OrderByDescending(g => g.Count).First().Pattern;

        var allTransactions = selectedGroups.SelectMany(g => g.Transactions).ToList();

        int? combinedCategoryId = allTransactions
            .Where(t => t.CategoryId.HasValue)
            .GroupBy(t => t.CategoryId!.Value)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        foreach (var tx in allTransactions)
            tx.CategoryId = combinedCategoryId;

        bool hasPos = allTransactions.Any(t => t.Amount > 0);
        bool hasNeg = allTransactions.Any(t => t.Amount < 0);
        AmountSign sign = (hasPos && hasNeg) ? AmountSign.Any
                        : hasPos             ? AmountSign.Positive
                                             : AmountSign.Negative;

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

        int insertAt = _groups.Count;
        foreach (var g in selectedGroups)
        {
            int i = _groups.IndexOf(g);
            if (i >= 0 && i < insertAt) insertAt = i;
        }
        foreach (var g in selectedGroups)
            _groups.Remove(g);
        _groups.Insert(Math.Min(insertAt, _groups.Count), combinedGroup);

        _db.SaveTransactionCategories(allTransactions);

        _selectedGroup = combinedGroup;
        PopulateGroupsGrid();
        UpdateProgress();

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
        _categories = _db.GetCategories().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (_groupsGrid.Columns.Count > ColCategory &&
            _groupsGrid.Columns[ColCategory] is DataGridViewComboBoxColumn catCol)
        {
            catCol.Items.Clear();
            catCol.Items.Add(Resources.NewCategoryItem);
            catCol.Items.Add("");
            foreach (var cat in _categories)
                catCol.Items.Add(cat.Name);
        }

        if (_detailGrid.Columns.Count > DColCategory &&
            _detailGrid.Columns[DColCategory] is DataGridViewComboBoxColumn detailCatCol)
        {
            detailCatCol.Items.Clear();
            detailCatCol.Items.Add(Resources.NewCategoryItem);
            detailCatCol.Items.Add("");
            foreach (var cat in _categories)
                detailCatCol.Items.Add(cat.Name);
        }

        RefreshGroupCategoryColumn();
        if (_selectedGroup != null) PopulateDetailGrid(_selectedGroup);
    }

    // ── Input dialog helper ───────────────────────────────────────────────────

    private static string? ShowInputDialog(string title, string prompt, string defaultValue)
    {
        using var dlg = new Form
        {
            Text            = title,
            Size            = new Size(380, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            MaximizeBox     = false,
            MinimizeBox     = false
        };

        var lbl = new Label
        {
            Text     = prompt,
            Location = new Point(12, 14),
            Size     = new Size(350, 20),
            AutoSize = false
        };

        var txt = new TextBox
        {
            Text     = defaultValue,
            Location = new Point(12, 38),
            Size     = new Size(348, 24)
        };

        var btnOk = new Button
        {
            Text         = "OK",
            DialogResult = DialogResult.OK,
            Location     = new Point(194, 72),
            Size         = new Size(80, 28)
        };
        var btnCancel = new Button
        {
            Text         = Resources.Cancel,
            DialogResult = DialogResult.Cancel,
            Location     = new Point(282, 72),
            Size         = new Size(80, 28)
        };

        dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        txt.SelectAll();

        return dlg.ShowDialog() == DialogResult.OK ? txt.Text : null;
    }
}

// ── Split Group dialog ────────────────────────────────────────────────────────

public class SplitGroupDialog : Form
{
    public string SelectedToken { get; private set; } = "";

    public SplitGroupDialog(string groupName, List<string> tokens, List<Transaction> transactions)
    {
        SuspendLayout();
        Text            = Resources.SplitGroupTitle;
        Size            = new Size(460, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;

        var lblGroup = new Label
        {
            Text     = $"{Resources.SplitGroupLabel}: {groupName}",
            Font     = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(12, 12),
            Size     = new Size(420, 18),
            AutoSize = false
        };

        var lblInstr = new Label
        {
            Text     = Resources.SplitGroupInstr,
            Location = new Point(12, 36),
            Size     = new Size(420, 18),
            AutoSize = false
        };

        var listBox = new ListBox
        {
            Location      = new Point(12, 58),
            Size          = new Size(420, 160),
            SelectionMode = SelectionMode.One,
            Font          = new Font("Consolas", 9f)
        };
        foreach (var t in tokens)
            listBox.Items.Add(t);

        var lblManual = new Label
        {
            Text     = Resources.SplitGroupManual,
            Location = new Point(12, 228),
            AutoSize = true
        };

        var txtToken = new TextBox
        {
            Location = new Point(12, 248),
            Size     = new Size(420, 24)
        };

        var lblPreview = new Label
        {
            Text      = "",
            Location  = new Point(12, 280),
            Size      = new Size(420, 18),
            ForeColor = Color.DimGray,
            AutoSize  = false
        };

        void UpdatePreview()
        {
            string tok = txtToken.Text.Trim();
            lblPreview.Text = string.IsNullOrEmpty(tok) ? "" :
                string.Format(Resources.SplitGroupPreview,
                    transactions.Count(t => t.Details.Contains(tok, StringComparison.OrdinalIgnoreCase)),
                    transactions.Count);
        }

        listBox.SelectedIndexChanged += (_, _) =>
        {
            if (listBox.SelectedItem is string s) { txtToken.Text = s; txtToken.SelectAll(); }
        };
        txtToken.TextChanged += (_, _) => UpdatePreview();

        var btnOk = new Button
        {
            Text         = "OK",
            DialogResult = DialogResult.OK,
            Location     = new Point(264, 316),
            Size         = new Size(80, 28)
        };
        var btnCancel = new Button
        {
            Text         = Resources.Cancel,
            DialogResult = DialogResult.Cancel,
            Location     = new Point(352, 316),
            Size         = new Size(80, 28)
        };

        btnOk.Click += (_, _) => { SelectedToken = txtToken.Text.Trim(); };

        Controls.AddRange(new Control[] { lblGroup, lblInstr, listBox, lblManual, txtToken, lblPreview, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ResumeLayout(false);
    }
}

// ── Flat combo cell — looks like text, shows dropdown only when editing ────────

/// <summary>
/// DataGridViewComboBoxCell that paints as plain text with a subtle chevron.
/// The full combo editor appears only when the cell enters edit mode.
/// </summary>
internal class FlatComboBoxCell : DataGridViewComboBoxCell
{
    protected override void Paint(
        Graphics graphics, Rectangle clipBounds, Rectangle cellBounds,
        int rowIndex, DataGridViewElementStates elementState,
        object? value, object? formattedValue, string? errorText,
        DataGridViewCellStyle cellStyle,
        DataGridViewAdvancedBorderStyle advancedBorderStyle,
        DataGridViewPaintParts paintParts)
    {
        // Let the base class draw background, selection, and border —
        // but suppress its own content (the chunky combo chrome).
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, elementState,
                   value, formattedValue, errorText, cellStyle, advancedBorderStyle,
                   paintParts & ~DataGridViewPaintParts.ContentForeground);

        bool selected = (elementState & DataGridViewElementStates.Selected) != 0;
        Color fg = selected ? cellStyle.SelectionForeColor : cellStyle.ForeColor;

        // Small chevron on the right
        const int arrowAreaW = 18;
        int ax = cellBounds.Right - arrowAreaW;
        int ay = cellBounds.Y + cellBounds.Height / 2;
        Color arrowColor = Color.FromArgb(selected ? 200 : 140, fg);
        using (var brush = new SolidBrush(arrowColor))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.FillPolygon(brush, new[]
            {
                new Point(ax,      ay - 3),
                new Point(ax + 9,  ay - 3),
                new Point(ax + 4,  ay + 3)
            });
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        }

        // Value text
        string text = formattedValue?.ToString() ?? "";
        var textRect = new Rectangle(
            cellBounds.X + 3, cellBounds.Y,
            cellBounds.Width - arrowAreaW - 4, cellBounds.Height);
        TextRenderer.DrawText(graphics, text, cellStyle.Font, textRect, fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

internal class FlatComboBoxColumn : DataGridViewComboBoxColumn
{
    public FlatComboBoxColumn() { CellTemplate = new FlatComboBoxCell(); }
}
