using CostCategorizationTool.Data;
using CostCategorizationTool.Models;

namespace CostCategorizationTool.Forms.Steps;

public class CategoryManagementStep : UserControl
{
    private readonly AppDatabase _db;

    private readonly ListBox   _categoryList;
    private readonly ListView  _rulesList;
    private readonly Button    _btnAdd;
    private readonly Button    _btnRename;
    private readonly Button    _btnDelete;
    private readonly Button    _btnAddRule;
    private readonly Button    _btnDeleteRule;

    private List<Category> _categories = new();

    public CategoryManagementStep(AppDatabase db)
    {
        _db = db;
        SuspendLayout();

        BackColor = Color.White;

        // ── Description label ────────────────────────────────────────────────
        var descLabel = new Label
        {
            Text     = "Manage expense categories. You can add, rename, or delete categories.",
            AutoSize = false,
            Font     = new Font("Segoe UI", 10f),
            Location = new Point(16, 16),
            Height   = 24,
            Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // ── Left panel – Category list ────────────────────────────────────────
        var leftPanel = new Panel
        {
            Location = new Point(16, 52),
            Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
        };

        var catLabel = new Label
        {
            Text     = "Categories",
            Font     = new Font("Segoe UI", 10f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        _categoryList = new ListBox
        {
            Location      = new Point(0, 24),
            Size          = new Size(200, 340),
            Font          = new Font("Segoe UI", 10f),
            SelectionMode = SelectionMode.One
        };

        _btnAdd = new Button
        {
            Text     = "Add",
            Size     = new Size(62, 28),
            Location = new Point(0, 372)
        };
        _btnRename = new Button
        {
            Text     = "Rename",
            Size     = new Size(70, 28),
            Location = new Point(66, 372)
        };
        _btnDelete = new Button
        {
            Text     = "Delete",
            Size     = new Size(62, 28),
            Location = new Point(140, 372),
            ForeColor = Color.DarkRed
        };

        leftPanel.Controls.Add(catLabel);
        leftPanel.Controls.Add(_categoryList);
        leftPanel.Controls.Add(_btnAdd);
        leftPanel.Controls.Add(_btnRename);
        leftPanel.Controls.Add(_btnDelete);

        // ── Right panel – Rules list ─────────────────────────────────────────
        var rightPanel = new Panel
        {
            Location = new Point(232, 52),
            Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        var rulesLabel = new Label
        {
            Text     = "Categorization Rules for selected category",
            Font     = new Font("Segoe UI", 10f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        _rulesList = new ListView
        {
            Location      = new Point(0, 24),
            Size          = new Size(500, 340),
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = true,
            Font          = new Font("Segoe UI", 9.5f),
            Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        _rulesList.Columns.Add("Type",      80);
        _rulesList.Columns.Add("Pattern",   300);
        _rulesList.Columns.Add("Direction", 100);

        _btnAddRule = new Button
        {
            Text     = "Add Rule",
            Size     = new Size(90, 28),
            Location = new Point(0, 372)
        };
        _btnDeleteRule = new Button
        {
            Text      = "Delete Rule",
            Size      = new Size(100, 28),
            Location  = new Point(96, 372),
            ForeColor = Color.DarkRed
        };

        rightPanel.Controls.Add(rulesLabel);
        rightPanel.Controls.Add(_rulesList);
        rightPanel.Controls.Add(_btnAddRule);
        rightPanel.Controls.Add(_btnDeleteRule);

        // ── Add to form ──────────────────────────────────────────────────────
        Controls.Add(descLabel);
        Controls.Add(leftPanel);
        Controls.Add(rightPanel);

        // ── Events ───────────────────────────────────────────────────────────
        _btnAdd.Click        += OnAddCategory;
        _btnRename.Click     += OnRenameCategory;
        _btnDelete.Click     += OnDeleteCategory;
        _btnAddRule.Click    += OnAddRule;
        _btnDeleteRule.Click += OnDeleteRule;
        _categoryList.SelectedIndexChanged += OnCategorySelected;

        Resize += OnResize;
        ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RefreshData();
        LayoutControls();
    }

    private void OnResize(object? sender, EventArgs e) => LayoutControls();

    private void LayoutControls()
    {
        int h = ClientSize.Height - 68;
        h = Math.Max(h, 200);

        // Left panel
        var leftPanel  = Controls.OfType<Panel>().ElementAtOrDefault(0);
        var rightPanel = Controls.OfType<Panel>().ElementAtOrDefault(1);

        if (leftPanel != null)
        {
            leftPanel.Location = new Point(16, 52);
            leftPanel.Size     = new Size(210, h);
            _categoryList.Size = new Size(210, h - 56);
            _btnAdd.Location   = new Point(0, h - 48);
            _btnRename.Location= new Point(66, h - 48);
            _btnDelete.Location= new Point(140, h - 48);
        }

        if (rightPanel != null)
        {
            int rw = ClientSize.Width - 244;
            rightPanel.Location = new Point(232, 52);
            rightPanel.Size     = new Size(Math.Max(rw, 200), h);
            _rulesList.Size     = new Size(Math.Max(rw, 200), h - 56);
            _rulesList.Columns[1].Width = Math.Max(100, rw - 80 - 100 - 4);
            _btnAddRule.Location    = new Point(0, h - 48);
            _btnDeleteRule.Location = new Point(96, h - 48);
        }

        // Description label width
        var descLabel = Controls.OfType<Label>().FirstOrDefault();
        if (descLabel != null)
            descLabel.Width = ClientSize.Width - 32;
    }

    public void RefreshData()
    {
        _categories = _db.GetCategories();
        _categoryList.Items.Clear();
        foreach (var cat in _categories)
            _categoryList.Items.Add(cat);

        _rulesList.Items.Clear();
    }

    private Category? SelectedCategory =>
        _categoryList.SelectedItem as Category;

    private void OnCategorySelected(object? sender, EventArgs e)
    {
        _rulesList.Items.Clear();
        if (SelectedCategory == null) return;

        var rules = _db.GetRulesForCategory(SelectedCategory.Id);
        foreach (var rule in rules)
        {
            string typeStr = rule.RuleType == RuleType.IBAN ? "IBAN" : "Details";
            string signStr = rule.AmountSign == AmountSign.Positive ? "Incoming (+)"
                           : rule.AmountSign == AmountSign.Negative ? "Outgoing (−)"
                           : "All";
            var item = new ListViewItem(typeStr);
            item.SubItems.Add(rule.Pattern);
            item.SubItems.Add(signStr);
            item.Tag = rule;
            _rulesList.Items.Add(item);
        }
    }

    private void OnAddCategory(object? sender, EventArgs e)
    {
        var name = ShowInputDialog("New category name:", "Add Category", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        _db.AddCategory(name);
        RefreshData();
    }

    private void OnRenameCategory(object? sender, EventArgs e)
    {
        if (SelectedCategory == null) { MessageBox.Show("Select a category first."); return; }
        var name = ShowInputDialog("New name:", "Rename Category", SelectedCategory.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        _db.UpdateCategory(SelectedCategory.Id, name);
        RefreshData();
    }

    private void OnDeleteCategory(object? sender, EventArgs e)
    {
        if (SelectedCategory == null) { MessageBox.Show("Select a category first."); return; }
        var result = MessageBox.Show(
            $"Delete category \"{SelectedCategory.Name}\" and all its rules?",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _db.DeleteCategory(SelectedCategory.Id);
        RefreshData();
    }

    private void OnAddRule(object? sender, EventArgs e)
    {
        if (SelectedCategory == null)
        {
            MessageBox.Show("Select a category first.", "No Category Selected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new AddRuleDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _db.AddRule(SelectedCategory.Id, dlg.SelectedRuleType, dlg.Pattern, dlg.SelectedAmountSign);
        OnCategorySelected(null, EventArgs.Empty); // Refresh rules list
    }

    private void OnDeleteRule(object? sender, EventArgs e)
    {
        if (_rulesList.SelectedItems.Count == 0) { MessageBox.Show("Select a rule first."); return; }
        var rule = _rulesList.SelectedItems[0].Tag as CategoryRule;
        if (rule == null) return;
        var result = MessageBox.Show($"Delete rule \"{rule.Pattern}\"?",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _db.DeleteRule(rule.Id);
        OnCategorySelected(null, EventArgs.Empty);
    }

    // ── Simple input dialog ──────────────────────────────────────────────────

    private static string ShowInputDialog(string prompt, string title, string defaultValue)
    {
        using var form = new Form
        {
            Text            = title,
            Size            = new Size(380, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            MaximizeBox     = false,
            MinimizeBox     = false
        };
        var lbl = new Label { Text = prompt, AutoSize = true, Location = new Point(12, 12) };
        var txt = new TextBox { Location = new Point(12, 36), Width = 340, Text = defaultValue };
        var ok  = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Location = new Point(194, 72), Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(280, 72), Width = 80 };
        form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : "";
    }
}

// ── Add Rule Dialog ──────────────────────────────────────────────────────────

public class AddRuleDialog : Form
{
    public RuleType SelectedRuleType { get; private set; }
    public string Pattern { get; private set; } = "";

    private readonly RadioButton _rbIban;
    private readonly RadioButton _rbDetails;
    private readonly TextBox _txtPattern;
    private readonly RadioButton _rbSignAny;
    private readonly RadioButton _rbSignPos;
    private readonly RadioButton _rbSignNeg;

    public AmountSign SelectedAmountSign =>
        _rbSignPos.Checked ? AmountSign.Positive :
        _rbSignNeg.Checked ? AmountSign.Negative :
        AmountSign.Any;

    public AddRuleDialog()
    {
        SuspendLayout();
        Text            = "Add Categorization Rule";
        Size            = new Size(420, 270);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;

        var typeLabel = new Label { Text = "Rule type:", AutoSize = true, Location = new Point(12, 14) };
        _rbIban    = new RadioButton { Text = "Match by Counterpart IBAN (exact, case-insensitive)", Location = new Point(12, 36), AutoSize = true, Checked = true };
        _rbDetails = new RadioButton { Text = "Match by keyword in Details (contains, case-insensitive)", Location = new Point(12, 60), AutoSize = true };

        var patternLabel = new Label { Text = "Pattern:", AutoSize = true, Location = new Point(12, 92) };
        _txtPattern = new TextBox { Location = new Point(12, 110), Width = 378, Font = new Font("Consolas", 10f) };

        var grpDir = new GroupBox
        {
            Text = "Transaction direction", Location = new Point(12, 136), Size = new Size(390, 52)
        };
        _rbSignAny = new RadioButton { Text = "All",            Location = new Point(8,  18), AutoSize = true, Checked = true };
        _rbSignPos = new RadioButton { Text = "Incoming (+)",   Location = new Point(70, 18), AutoSize = true };
        _rbSignNeg = new RadioButton { Text = "Outgoing (−)",   Location = new Point(190, 18), AutoSize = true };
        grpDir.Controls.AddRange(new Control[] { _rbSignAny, _rbSignPos, _rbSignNeg });

        var ok     = new Button { Text = "Save Rule", DialogResult = DialogResult.OK,     Location = new Point(218, 198), Width = 90 };
        var cancel = new Button { Text = "Cancel",    DialogResult = DialogResult.Cancel, Location = new Point(314, 198), Width = 80 };

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtPattern.Text))
            {
                MessageBox.Show("Please enter a pattern.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            SelectedRuleType = _rbIban.Checked ? RuleType.IBAN : RuleType.Details;
            Pattern = _txtPattern.Text.Trim();
        };

        Controls.AddRange(new Control[] { typeLabel, _rbIban, _rbDetails, patternLabel, _txtPattern, grpDir, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
        ResumeLayout(false);
    }
}
