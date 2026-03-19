using CostCategorizationTool.Data;
using CostCategorizationTool.Forms.Steps;
using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms;

public class MainForm : Form
{
    // ── State ─────────────────────────────────────────────────────────────────
    private AppDatabase? _db;
    private string?      _projectPath;
    private readonly AppSettings _settings;

    // ── Controls ──────────────────────────────────────────────────────────────
    private readonly Panel  _headerPanel;
    private readonly Label  _titleLabel;
    private readonly Label  _projectLabel;
    private readonly Panel  _contentPanel;
    private readonly Panel  _toolbarPanel;
    private readonly Button _btnImportCsv;
    private readonly Button _btnViewSummary;

    // ── Panels ────────────────────────────────────────────────────────────────
    private readonly HomePanel _homePanel;
    private TransactionCategorizationStep? _categorizationStep;

    // ── Menu items ────────────────────────────────────────────────────────────
    private readonly ToolStripMenuItem _miCloseProject;
    private readonly ToolStripMenuItem _miImportCsv;
    private readonly ToolStripMenuItem _miExportExcel;
    private readonly ToolStripMenuItem _miRecentProjects;

    public MainForm()
    {
        _settings = AppSettings.Load();

        SuspendLayout();


        Text            = Resources.AppTitle;
        Size            = new Size(1050, 760);
        MinimumSize     = new Size(800, 600);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState     = FormWindowState.Maximized;

        // ── Header ───────────────────────────────────────────────────────────
        _headerPanel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 74,
            BackColor = Color.FromArgb(0, 84, 153)
        };

        _titleLabel = new Label
        {
            Text      = Resources.AppTitle,
            Font      = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize  = true,
            Location  = new Point(16, 10)
        };

        _projectLabel = new Label
        {
            Text      = "",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(180, 220, 255),
            AutoSize  = false,
            Location  = new Point(16, 46),
            Size      = new Size(700, 18)
        };

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_projectLabel);

        // ── Toolbar (only visible when a project is open) ─────────────────────
        _toolbarPanel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 56,
            BackColor = Color.FromArgb(235, 240, 248),
            Visible   = false
        };

        var arrowFont = new Font("Segoe UI", 18f);
        int aw = ArrowW(arrowFont);

        _btnImportCsv = MakeStepButton(Resources.StepImport, false);
        _btnImportCsv.Location = new Point(8, 8);
        _btnImportCsv.Click += OnImportCsv;

        int ax1 = _btnImportCsv.Right + 4;
        var arrow1 = new Label
        {
            Text      = "›",
            Font      = arrowFont,
            ForeColor = Color.FromArgb(150, 160, 180),
            AutoSize  = true,
            Location  = new Point(ax1, 16)
        };

        var btnCategorize = MakeStepButton(Resources.StepCategorize, true);  // always active
        btnCategorize.Location = new Point(ax1 + aw, 8);

        int ax2 = btnCategorize.Right + 4;
        var arrow2 = new Label
        {
            Text      = "›",
            Font      = arrowFont,
            ForeColor = Color.FromArgb(150, 160, 180),
            AutoSize  = true,
            Location  = new Point(ax2, 16)
        };

        _btnViewSummary = MakeStepButton(Resources.StepSummary, false);
        _btnViewSummary.Location = new Point(ax2 + aw, 8);
        _btnViewSummary.Click += OnViewSummary;

        _toolbarPanel.Controls.AddRange(new Control[] { _btnImportCsv, arrow1, btnCategorize, arrow2, _btnViewSummary });

        // ── Content panel ─────────────────────────────────────────────────────
        _contentPanel = new Panel { Dock = DockStyle.Fill };

        _homePanel = new HomePanel(_settings) { Dock = DockStyle.Fill };
        _homePanel.ProjectRequested += OnProjectRequested;

        _contentPanel.Controls.Add(_homePanel);

        // ── Menu ──────────────────────────────────────────────────────────────
        var menuStrip = new MenuStrip { RenderMode = ToolStripRenderMode.System };

        var fileMenu = new ToolStripMenuItem($"&{Resources.File}");

        var miNew  = new ToolStripMenuItem($"&{Resources.NewProject}",  null, (_, _) => _homePanel.OnNewProject());
        var miOpen = new ToolStripMenuItem($"&{Resources.OpenProject}", null, (_, _) => _homePanel.OnOpenProject());

        _miRecentProjects = new ToolStripMenuItem($"{Resources.RecentProjects}") { Enabled = false };
        _miCloseProject   = new ToolStripMenuItem($"&{Resources.CloseProject}",  null, (_, _) => CloseProject()) { Enabled = false };
        _miImportCsv      = new ToolStripMenuItem($"&{Resources.ImportCsv}",    null, (_, _) => OnImportCsv(null, EventArgs.Empty)) { Enabled = false };
        _miExportExcel    = new ToolStripMenuItem($"{Resources.ExportExcel}",    null, (_, _) => OnViewSummary(null, EventArgs.Empty)) { Enabled = false };

        var miExit = new ToolStripMenuItem($"&{Resources.Exit}", null, (_, _) => Close());

        fileMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            miNew, miOpen, _miRecentProjects,
            new ToolStripSeparator(),
            _miCloseProject, _miImportCsv, _miExportExcel,
            new ToolStripSeparator(),
            miExit
        });

        var toolsMenu    = new ToolStripMenuItem($"&{Resources.Tools}");
        var settingsItem = new ToolStripMenuItem($"&{Resources.Settings}", null, OnOpenSettings);
        toolsMenu.DropDownItems.Add(settingsItem);

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(toolsMenu);

        // ── Assemble ──────────────────────────────────────────────────────────
        Controls.Add(_contentPanel);
        Controls.Add(_toolbarPanel);
        Controls.Add(_headerPanel);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        FormClosed += (_, _) => _db?.Dispose();

        ResumeLayout(false);

        RefreshRecentMenu();
    }

    private static Button MakeStepButton(string text, bool active)
    {
        var font = new Font("Segoe UI", 10f, active ? FontStyle.Bold : FontStyle.Regular);
        var btn = new Button
        {
            Text      = text,
            Size      = new Size(TextRenderer.MeasureText(text, font).Width + 28, 40),
            Font      = font,
            FlatStyle = FlatStyle.Flat,
            BackColor = active ? Color.FromArgb(0, 100, 180) : Color.FromArgb(255, 255, 255),
            ForeColor = active ? Color.White : Color.FromArgb(50, 60, 80),
            Cursor    = active ? Cursors.Default : Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = active ? Color.FromArgb(0, 80, 150) : Color.FromArgb(180, 190, 210);
        btn.FlatAppearance.BorderSize  = 1;
        return btn;
    }

    private static int ArrowW(Font font) =>
        TextRenderer.MeasureText("›", font).Width + 4;

    // ── Project lifecycle ────────────────────────────────────────────────────

    private void OnProjectRequested(object? sender, string path)
    {
        OpenProject(path);
    }

    private void OpenProject(string path)
    {
        try
        {
            _db?.Dispose();
            _db          = new AppDatabase(path);
            _projectPath = path;

            _settings.AddRecentProject(path);
            _homePanel.RefreshRecentList();
            RefreshRecentMenu();

            // Build categorization step
            _categorizationStep = new TransactionCategorizationStep(_db) { Dock = DockStyle.Fill };

            _contentPanel.Controls.Clear();
            _contentPanel.Controls.Add(_categorizationStep);

            _toolbarPanel.Visible = true;

            int count = _db.GetTransactionCount();
            _projectLabel.Text = $"{Path.GetFileName(path)}  —  {path}";
            Text = $"{Resources.AppTitle} — {Path.GetFileNameWithoutExtension(path)}";

            _miCloseProject.Enabled = true;
            _miImportCsv.Enabled    = true;
            _miExportExcel.Enabled  = true;

            if (count > 0)
            {
                _categorizationStep.LoadFromDatabase();
            }
            else
            {
                // New project — prompt to import CSV immediately
                var result = MessageBox.Show(
                    Resources.NoTransactionsYet,
                    Resources.NewProjectTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    OnImportCsv(null, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Resources.CouldNotOpen, ex.Message), Resources.ErrorTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CloseProject()
    {
        _db?.Dispose();
        _db          = null;
        _projectPath = null;

        _contentPanel.Controls.Clear();
        _contentPanel.Controls.Add(_homePanel);
        _homePanel.RefreshRecentList();

        _toolbarPanel.Visible = false;
        _projectLabel.Text    = "";
        Text = Resources.AppTitle;

        _miCloseProject.Enabled = false;
        _miImportCsv.Enabled    = false;
        _miExportExcel.Enabled  = false;
        _categorizationStep     = null;
    }

    // ── CSV Import ────────────────────────────────────────────────────────────

    private void OnImportCsv(object? sender, EventArgs e)
    {
        if (_db == null || _categorizationStep == null) return;

        using var dlg = new OpenFileDialog
        {
            Title  = "Select bank CSV export file",
            Filter = Resources.CsvFilter
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var (added, skipped) = _categorizationStep.ImportCsv(dlg.FileName);
            MessageBox.Show(
                string.Format(Resources.ImportCompleteMsg, added, skipped),
                Resources.ImportCompleteTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Resources.ImportFailed, ex.Message), Resources.ImportError,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Summary / Export ──────────────────────────────────────────────────────

    private void OnViewSummary(object? sender, EventArgs e)
    {
        if (_db == null || _categorizationStep == null) return;

        var transactions = _categorizationStep.GetTransactions();
        var categories   = _db.GetCategories();
        var groups       = _categorizationStep.GetGroups();

        // Show the SummaryStep in a dialog
        using var form = new Form
        {
            Text            = Resources.SummaryTitle,
            Size            = new Size(900, 680),
            MinimumSize     = new Size(600, 400),
            StartPosition   = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable
        };
        var step = new SummaryStep { Dock = DockStyle.Fill };
        form.Controls.Add(step);
        form.Load += (_, _) => step.LoadSummary(transactions, categories, groups);
        form.ShowDialog(this);
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        if (_db == null)
        {
            MessageBox.Show(Resources.OpenProjectFirst,
                Resources.NoProjectTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new SettingsDialog(_db, _settings);
        dlg.ShowDialog(this);

        if (dlg.ResetDatabaseExecuted)
        {
            // Reload categories and re-run categorization
            _categorizationStep?.LoadFromDatabase();
        }
        else if (dlg.ResetTransactionsRequested)
        {
            // Transactions were cleared — reload (will show empty)
            _categorizationStep?.LoadFromDatabase();
        }
    }

    // ── Recent projects menu ─────────────────────────────────────────────────

    private void RefreshRecentMenu()
    {
        _miRecentProjects.DropDownItems.Clear();

        if (_settings.RecentProjects.Count == 0)
        {
            _miRecentProjects.Enabled = false;
            return;
        }

        _miRecentProjects.Enabled = true;
        foreach (var path in _settings.RecentProjects)
        {
            var p    = path; // capture for lambda
            var item = new ToolStripMenuItem(path, null, (_, _) =>
            {
                if (!File.Exists(p))
                {
                    MessageBox.Show($"Project file not found:\n{p}", Resources.FileNotFoundTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                OpenProject(p);
            });
            item.Enabled = File.Exists(path);
            _miRecentProjects.DropDownItems.Add(item);
        }
    }
}
