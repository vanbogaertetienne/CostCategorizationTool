using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms;

/// <summary>
/// Shown when no project is open. Lets the user create or open a .ccp project file,
/// or pick from the recent-projects list.
/// </summary>
public class HomePanel : UserControl
{
    public event EventHandler<string>? ProjectRequested;   // fired with the chosen path

    private readonly AppSettings _settings;
    private readonly ListBox     _recentList;

    public HomePanel(AppSettings settings)
    {
        _settings = settings;
        SuspendLayout();
        BackColor = Color.White;

        // ── Title ────────────────────────────────────────────────────────────
        var lblTitle = new Label
        {
            Text      = Resources.HomeTitle,
            Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 84, 153),
            AutoSize  = true,
            Location  = new Point(40, 40)
        };

        var lblSub = new Label
        {
            Text      = Resources.HomeSubtitle,
            Font      = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(80, 80, 80),
            AutoSize  = true,
            Location  = new Point(40, 80)
        };

        // ── Buttons ──────────────────────────────────────────────────────────
        var btnFont = new Font("Segoe UI", 10f);
        int btnH = 40, btnY = 120;

        var btnNew = new Button
        {
            Text      = Resources.BtnNewProject,
            Size      = new Size(TextRenderer.MeasureText(Resources.BtnNewProject, btnFont).Width + 24, btnH),
            Location  = new Point(40, btnY),
            Font      = btnFont,
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnNew.FlatAppearance.BorderSize = 0;
        btnNew.Click += (s, e) => OnNewProject(s, e);

        var btnOpen = new Button
        {
            Text      = Resources.BtnOpenProject,
            Size      = new Size(TextRenderer.MeasureText(Resources.BtnOpenProject, btnFont).Width + 24, btnH),
            Location  = new Point(40 + btnNew.Width + 10, btnY),
            Font      = btnFont
        };
        btnOpen.Click += (s, e) => OnOpenProject(s, e);

        // ── Recent projects ──────────────────────────────────────────────────
        var lblRecent = new Label
        {
            Text      = Resources.LblRecentProjects,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            AutoSize  = true,
            Location  = new Point(40, 184),
            ForeColor = Color.FromArgb(50, 50, 50)
        };

        _recentList = new ListBox
        {
            Location              = new Point(40, 208),
            Font                  = new Font("Segoe UI", 10f),
            IntegralHeight        = false,
            BorderStyle           = BorderStyle.FixedSingle,
            HorizontalScrollbar   = true,
            Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        _recentList.DoubleClick += OnRecentDoubleClick;

        Controls.AddRange(new Control[] { lblTitle, lblSub, btnNew, btnOpen, lblRecent, _recentList });

        Resize += (_, _) => LayoutControls();
        ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RefreshRecentList();
        LayoutControls();
    }

    private void LayoutControls()
    {
        int w = Math.Max(ClientSize.Width - 80, 200);
        int h = Math.Max(ClientSize.Height - 248, 80);
        _recentList.Size = new Size(w, h);
    }

    public void RefreshRecentList()
    {
        _recentList.Items.Clear();
        foreach (var p in _settings.RecentProjects)
            _recentList.Items.Add(p);

        // Mark missing files
        for (int i = 0; i < _recentList.Items.Count; i++)
        {
            if (_recentList.Items[i] is string path && !File.Exists(path))
                _recentList.Items[i] = path + Resources.NotFound;
        }
    }

    public void OnNewProject(object? sender = null, EventArgs? e = null)
    {
        using var dlg = new SaveFileDialog
        {
            Title      = Resources.CreateProjectTitle,
            Filter     = Resources.ProjectFilter,
            DefaultExt = "ccp",
            FileName   = "MyProject.ccp"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        // Delete existing file so the DB is created fresh
        if (File.Exists(dlg.FileName))
            File.Delete(dlg.FileName);

        ProjectRequested?.Invoke(this, dlg.FileName);
    }

    public void OnOpenProject(object? sender = null, EventArgs? e = null)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = Resources.OpenProjectTitle,
            Filter = Resources.ProjectFilter
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        ProjectRequested?.Invoke(this, dlg.FileName);
    }

    private void OnRecentDoubleClick(object? sender, EventArgs e)
    {
        if (_recentList.SelectedItem is not string item) return;
        // Strip the "[not found]" suffix if present
        var notFound = Resources.NotFound;
        var path = item.EndsWith(notFound) ? item[..^notFound.Length] : item;
        if (!File.Exists(path))
        {
            MessageBox.Show(string.Format(Resources.FileNotFoundMsg, path),
                Resources.FileNotFoundTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ProjectRequested?.Invoke(this, path);
    }
}
