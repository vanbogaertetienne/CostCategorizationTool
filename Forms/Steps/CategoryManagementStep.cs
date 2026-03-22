using CostCategorizationTool.Data;
using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

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
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode       = AutoScaleMode.Dpi;
        BackColor = Color.White;

        // ── Description label ────────────────────────────────────────────────
        var descLabel = new Label
        {
            Text     = Resources.CatDesc,
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
            Text     = Resources.CatListLabel,
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

        var smFont = new Font("Segoe UI", 9f);
        int sbh = 28, sbx = 0, sby = 372;
        _btnAdd = new Button
        {
            Text     = Resources.BtnAdd,
            Size     = new Size(UiScaler.BW(Resources.BtnAdd, smFont, 16), sbh),
            Location = new Point(sbx, sby),
            Font     = smFont
        };
        sbx += _btnAdd.Width + 4;
        _btnRename = new Button
        {
            Text     = Resources.BtnRename,
            Size     = new Size(UiScaler.BW(Resources.BtnRename, smFont, 16), sbh),
            Location = new Point(sbx, sby),
            Font     = smFont
        };
        sbx += _btnRename.Width + 4;
        _btnDelete = new Button
        {
            Text      = Resources.BtnDelete,
            Size      = new Size(UiScaler.BW(Resources.BtnDelete, smFont, 16), sbh),
            Location  = new Point(sbx, sby),
            ForeColor = Color.DarkRed,
            Font      = smFont
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
            Text     = Resources.RulesListLabel,
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
        _rulesList.Columns.Add(Resources.RuleColType,      80);
        _rulesList.Columns.Add(Resources.RuleColPattern,   300);
        _rulesList.Columns.Add(Resources.RuleColDirection, 100);

        int rbx = 0;
        _btnAddRule = new Button
        {
            Text     = Resources.BtnAddRule,
            Size     = new Size(UiScaler.BW(Resources.BtnAddRule, smFont, 16), sbh),
            Location = new Point(rbx, sby),
            Font     = smFont
        };
        rbx += _btnAddRule.Width + 4;
        _btnDeleteRule = new Button
        {
            Text      = Resources.BtnDeleteRule,
            Size      = new Size(UiScaler.BW(Resources.BtnDeleteRule, smFont, 16), sbh),
            Location  = new Point(rbx, sby),
            ForeColor = Color.DarkRed,
            Font      = smFont
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
        int Sc(int v) => (int)(v * (float)DeviceDpi / 96f);
        int gap  = Sc(16);
        int h    = Math.Max(ClientSize.Height - Sc(68), 200);
        int topH = Sc(28);   // height reserved for the section label above each list
        int btnH = Sc(32);   // button height
        int btnGap = Sc(6);  // gap between list bottom and buttons

        // Button Y is anchored to the bottom of the panel
        int lby = h - btnH - btnGap;

        var leftPanel  = Controls.OfType<Panel>().ElementAtOrDefault(0);
        var rightPanel = Controls.OfType<Panel>().ElementAtOrDefault(1);

        if (leftPanel != null)
        {
            int lpw = Math.Max(_btnAdd.Width + _btnRename.Width + _btnDelete.Width + Sc(12), Sc(210));
            leftPanel.Location     = new Point(gap, Sc(52));
            leftPanel.Size         = new Size(lpw, h);
            // List fills the space between the label and the buttons
            _categoryList.Location = new Point(0, topH);
            _categoryList.Size     = new Size(lpw, Math.Max(40, lby - topH - btnGap));
            int lbx = 0;
            _btnAdd.Location    = new Point(lbx, lby); lbx += _btnAdd.Width + Sc(4);
            _btnRename.Location = new Point(lbx, lby); lbx += _btnRename.Width + Sc(4);
            _btnDelete.Location = new Point(lbx, lby);
        }

        if (rightPanel != null)
        {
            int lpw = leftPanel?.Width ?? Sc(210);
            int rpx = gap + lpw + gap;
            int rw  = Math.Max(ClientSize.Width - rpx - gap, 200);
            rightPanel.Location  = new Point(rpx, Sc(52));
            rightPanel.Size      = new Size(rw, h);
            _rulesList.Location  = new Point(0, topH);
            _rulesList.Size      = new Size(rw, Math.Max(40, lby - topH - btnGap));
            _rulesList.Columns[1].Width = Math.Max(100, rw - 80 - 100 - 4);
            int rbx = 0;
            _btnAddRule.Location    = new Point(rbx, lby); rbx += _btnAddRule.Width + Sc(4);
            _btnDeleteRule.Location = new Point(rbx, lby);
        }

        var descLabel = Controls.OfType<Label>().FirstOrDefault();
        if (descLabel != null)
            descLabel.Width = ClientSize.Width - 2 * gap;
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
            string typeStr = rule.RuleType == RuleType.IBAN ? Resources.TypeIban : "Details";
            string signStr = rule.AmountSign == AmountSign.Positive ? Resources.DirIncoming
                           : rule.AmountSign == AmountSign.Negative ? Resources.DirOutgoing
                           : Resources.DirAll;
            var item = new ListViewItem(typeStr);
            item.SubItems.Add(rule.Pattern);
            item.SubItems.Add(signStr);
            item.Tag = rule;
            _rulesList.Items.Add(item);
        }
    }

    private void OnAddCategory(object? sender, EventArgs e)
    {
        var name = ShowInputDialog(Resources.NewCatName, Resources.AddCatTitle, "");
        if (string.IsNullOrWhiteSpace(name)) return;
        _db.AddCategory(name);
        RefreshData();
    }

    private void OnRenameCategory(object? sender, EventArgs e)
    {
        if (SelectedCategory == null) { MessageBox.Show(Resources.SelectCatFirst); return; }
        var name = ShowInputDialog(Resources.NewNameLbl, Resources.RenameCatTitle, SelectedCategory.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        _db.UpdateCategory(SelectedCategory.Id, name);
        RefreshData();
    }

    private void OnDeleteCategory(object? sender, EventArgs e)
    {
        if (SelectedCategory == null) { MessageBox.Show(Resources.SelectCatFirst); return; }
        var result = MessageBox.Show(
            string.Format(Resources.DeleteCatMsg, SelectedCategory.Name),
            Resources.ConfirmDeleteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _db.DeleteCategory(SelectedCategory.Id);
        RefreshData();
    }

    private void OnAddRule(object? sender, EventArgs e)
    {
        if (SelectedCategory == null)
        {
            MessageBox.Show(Resources.SelectCatFirst, Resources.NoCatSelectedTitle,
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
        if (_rulesList.SelectedItems.Count == 0) { MessageBox.Show(Resources.SelectRuleFirst); return; }
        var rule = _rulesList.SelectedItems[0].Tag as CategoryRule;
        if (rule == null) return;
        var result = MessageBox.Show(string.Format(Resources.DeleteRuleMsg, rule.Pattern),
            Resources.ConfirmDeleteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
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
        var df  = SystemFonts.DefaultFont;
        int canW = Math.Max(80, TextRenderer.MeasureText(Resources.Cancel, df).Width + 20);
        int okW  = Math.Max(80, TextRenderer.MeasureText("OK",     df).Width + 20);
        int rx   = txt.Right;
        var cancel = new Button { Text = Resources.Cancel, DialogResult = DialogResult.Cancel, Size = new Size(canW, 28), Location = new Point(rx - canW,          72) };
        var ok     = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Size = new Size(okW,  28), Location = new Point(rx - canW - 8 - okW, 72) };
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
        Text            = Resources.AddRuleTitle;
        Size            = new Size(420, 270);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;

        var typeLabel = new Label { Text = Resources.RuleTypeLbl, AutoSize = true, Location = new Point(12, 14) };
        _rbIban    = new RadioButton { Text = Resources.RbIban,    Location = new Point(12, 36), AutoSize = true, Checked = true };
        _rbDetails = new RadioButton { Text = Resources.RbDetails, Location = new Point(12, 60), AutoSize = true };

        var patternLabel = new Label { Text = Resources.PatternLbl, AutoSize = true, Location = new Point(12, 92) };
        _txtPattern = new TextBox { Location = new Point(12, 110), Width = 378, Font = new Font("Consolas", 10f) };

        var grpDir = new GroupBox
        {
            Text = Resources.DirectionLbl, Location = new Point(12, 136), Size = new Size(390, 52)
        };
        _rbSignAny = new RadioButton { Text = Resources.DirAll,      Location = new Point(8,  18), AutoSize = true, Checked = true };
        _rbSignPos = new RadioButton { Text = Resources.DirIncoming, Location = new Point(70, 18), AutoSize = true };
        _rbSignNeg = new RadioButton { Text = Resources.DirOutgoing, Location = new Point(190, 18), AutoSize = true };
        grpDir.Controls.AddRange(new Control[] { _rbSignAny, _rbSignPos, _rbSignNeg });

        var df2     = SystemFonts.DefaultFont;
        int saveW   = Math.Max(80, TextRenderer.MeasureText(Resources.BtnSaveRule, df2).Width + 20);
        int cancelW = Math.Max(80, TextRenderer.MeasureText(Resources.Cancel,      df2).Width + 20);
        int rx2     = _txtPattern.Right;
        var cancel  = new Button { Text = Resources.Cancel,      DialogResult = DialogResult.Cancel, Size = new Size(cancelW, 28), Location = new Point(rx2 - cancelW,           198) };
        var ok      = new Button { Text = Resources.BtnSaveRule, DialogResult = DialogResult.OK,     Size = new Size(saveW,   28), Location = new Point(rx2 - cancelW - 8 - saveW, 198) };

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtPattern.Text))
            {
                MessageBox.Show(Resources.EnterPattern, Resources.ValidationTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
