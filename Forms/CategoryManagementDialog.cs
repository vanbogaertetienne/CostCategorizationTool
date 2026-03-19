using CostCategorizationTool.Data;
using CostCategorizationTool.Forms.Steps;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms;

/// <summary>
/// Hosts the CategoryManagementStep UserControl inside a standalone dialog,
/// accessible from the "Manage Categories…" button in the categorization step.
/// </summary>
public class CategoryManagementDialog : Form
{
    public CategoryManagementDialog(AppDatabase db)
    {
        SuspendLayout();

        Text            = Resources.CatTitle;
        Size            = new Size(830, 560);
        MinimumSize     = new Size(700, 480);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        Font            = new Font("Segoe UI", 9.5f);

        var step = new CategoryManagementStep(db) { Dock = DockStyle.Fill };

        var footer = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 48,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        var closeFont = new Font("Segoe UI", 9.5f);
        int closeW    = TextRenderer.MeasureText(Resources.Close, closeFont).Width + 24;
        var btnClose  = new Button
        {
            Text         = Resources.Close,
            DialogResult = DialogResult.OK,
            Size         = new Size(closeW, 32),
            Font         = closeFont,
            Anchor       = AnchorStyles.Top | AnchorStyles.Right
        };
        footer.Controls.Add(btnClose);
        footer.Resize += (_, _) =>
            btnClose.Location = new Point(footer.ClientSize.Width - closeW - 14, 8);

        Controls.Add(step);
        Controls.Add(footer);
        AcceptButton = btnClose;

        ResumeLayout(false);
    }
}
