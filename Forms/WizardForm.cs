using CostCategorizationTool.Data;
using CostCategorizationTool.Forms.Steps;
using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms;

public class WizardForm : Form
{
    // ── DB / services ────────────────────────────────────────────────────────
    private readonly AppDatabase _db;
    private readonly AppSettings _settings;
    private readonly CategorizationService _categorizationService = new();

    // ── State ────────────────────────────────────────────────────────────────
    private List<Transaction> _transactions = new();
    private int _currentStep;
    private const int TotalSteps = 3;

    // ── Controls ─────────────────────────────────────────────────────────────
    private readonly Panel _headerPanel;
    private readonly Label _titleLabel;
    private readonly Label _stepLabel;
    private readonly Panel _contentPanel;
    private readonly Panel _footerPanel;
    private readonly Button _btnCancel;
    private readonly Button _btnBack;
    private readonly Button _btnNext;

    // ── Steps ────────────────────────────────────────────────────────────────
    private readonly FileSelectionStep _fileStep;
    private readonly TransactionCategorizationStep _categorizationStep;
    private readonly SummaryStep _summaryStep;
    private readonly UserControl[] _steps;

    public WizardForm()
    {
        SuspendLayout();

        // DB path
        var dbDir  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CostCategorizationTool");
        var dbPath = Path.Combine(dbDir, "data.db");
        _db       = new AppDatabase(dbPath);
        _settings = AppSettings.Load();

        // ── Form properties ──────────────────────────────────────────────────
        Text            = "Cost Categorization Tool";
        Size            = new Size(950, 700);
        MinimumSize     = new Size(800, 600);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;

        // ── Header panel ─────────────────────────────────────────────────────
        _headerPanel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 70,
            BackColor = Color.FromArgb(0, 122, 204)
        };

        _titleLabel = new Label
        {
            Text      = "Cost Categorization Tool",
            Font      = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize  = false,
            Location  = new Point(20, 10),
            Size      = new Size(500, 30)
        };

        _stepLabel = new Label
        {
            Text      = "Step 1 of 4",
            Font      = new Font("Segoe UI", 10f),
            ForeColor = Color.LightCyan,
            AutoSize  = false,
            Location  = new Point(20, 42),
            Size      = new Size(300, 20)
        };

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_stepLabel);

        // ── Footer panel ─────────────────────────────────────────────────────
        _footerPanel = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 55,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        _btnCancel = new Button
        {
            Text     = "Cancel",
            Size     = new Size(90, 32),
            Location = new Point(12, 11)
        };

        _btnBack = new Button
        {
            Text     = "< Back",
            Size     = new Size(90, 32),
            Anchor   = AnchorStyles.Top | AnchorStyles.Right
        };

        _btnNext = new Button
        {
            Text     = "Next >",
            Size     = new Size(90, 32),
            Anchor   = AnchorStyles.Top | AnchorStyles.Right
        };

        _footerPanel.Controls.Add(_btnCancel);
        _footerPanel.Controls.Add(_btnBack);
        _footerPanel.Controls.Add(_btnNext);

        _footerPanel.Resize += (_, _) => PositionFooterButtons();
        PositionFooterButtons();

        // ── Content panel ────────────────────────────────────────────────────
        _contentPanel = new Panel { Dock = DockStyle.Fill };

        // ── Create steps ────────────────────────────────────────────────────
        _fileStep           = new FileSelectionStep                        { Dock = DockStyle.Fill };
        _categorizationStep = new TransactionCategorizationStep(_db, _settings) { Dock = DockStyle.Fill };
        _summaryStep        = new SummaryStep                             { Dock = DockStyle.Fill };

        _steps = new UserControl[]
        {
            _fileStep, _categorizationStep, _summaryStep
        };

        // ── Menu strip ───────────────────────────────────────────────────────
        var menuStrip  = new MenuStrip { RenderMode = ToolStripRenderMode.System };
        var toolsMenu  = new ToolStripMenuItem("&Tools");
        var settingsItem = new ToolStripMenuItem("&Settings…", null, OnOpenSettings);
        toolsMenu.DropDownItems.Add(settingsItem);
        menuStrip.Items.Add(toolsMenu);

        // ── Wire events ──────────────────────────────────────────────────────
        _btnCancel.Click += (_, _) => Close();
        _btnBack.Click   += OnBack;
        _btnNext.Click   += OnNext;

        // ── Add controls ─────────────────────────────────────────────────────
        // Order matters for Dock=Top stacking: last added = topmost.
        Controls.Add(_contentPanel);
        Controls.Add(_footerPanel);
        Controls.Add(_headerPanel);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        FormClosed += (_, _) => _db.Dispose();

        ResumeLayout(false);

        ShowStep(0);
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private void PositionFooterButtons()
    {
        int w = _footerPanel.ClientSize.Width;
        _btnNext.Location  = new Point(w - 12 - _btnNext.Width, 11);
        _btnBack.Location  = new Point(w - 12 - _btnNext.Width - 8 - _btnBack.Width, 11);
    }

    private void ShowStep(int stepIndex)
    {
        _currentStep = stepIndex;
        _contentPanel.Controls.Clear();

        var step = _steps[stepIndex];
        _contentPanel.Controls.Add(step);

        // Update header
        string[] stepNames = { "Select File", "Categorize Transactions", "Summary" };
        _stepLabel.Text = $"Step {stepIndex + 1} of {TotalSteps}: {stepNames[stepIndex]}";

        // Update buttons
        _btnBack.Enabled = stepIndex > 0;
        _btnNext.Text    = stepIndex == TotalSteps - 1 ? "Finish" : "Next >";
    }

    private void OnBack(object? sender, EventArgs e)
    {
        if (_currentStep > 0)
            ShowStep(_currentStep - 1);
    }

    private void OnNext(object? sender, EventArgs e)
    {
        if (_currentStep == TotalSteps - 1)
        {
            Close();
            return;
        }

        // Validate current step before advancing
        if (!ValidateStep(_currentStep)) return;

        int next = _currentStep + 1;

        // Prepare next step
        switch (next)
        {
            case 1:
                // File selection → categorization
                _transactions = _fileStep.LoadedTransactions ?? new List<Transaction>();
                foreach (var tx in _transactions) tx.CategoryId = null;
                var rules = _db.GetRules();
                _categorizationService.AutoCategorize(_transactions, rules);
                _categorizationStep.LoadTransactions(_transactions);
                break;

            case 2:
                // Categorization → summary
                _transactions = _categorizationStep.GetTransactions();
                var categories = _db.GetCategories();
                var groups     = _categorizationStep.GetGroups();
                _summaryStep.LoadSummary(_transactions, categories, groups);
                break;
        }

        ShowStep(next);
    }

    private bool ValidateStep(int step)
    {
        switch (step)
        {
            case 0:
                if (_fileStep.LoadedTransactions == null || _fileStep.LoadedTransactions.Count == 0)
                {
                    MessageBox.Show("Please load a CSV file with transactions before proceeding.",
                        "No transactions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                return true;
            default:
                return true;
        }
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_db, _settings);
        dlg.ShowDialog(this);
        // Settings are saved inside the dialog on close.

        if (dlg.ResetTransactionsRequested || dlg.ResetDatabaseExecuted)
        {
            _transactions.Clear();
            _fileStep.Reset();
            ShowStep(0);
        }
    }
}
