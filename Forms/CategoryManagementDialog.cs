using CostCategorizationTool.Data;
using CostCategorizationTool.Forms.Steps;

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

        Text            = "Manage Categories & Rules";
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

        var btnClose = new Button
        {
            Text         = "Close",
            DialogResult = DialogResult.OK,
            Size         = new Size(90, 32),
            Anchor       = AnchorStyles.Top | AnchorStyles.Right
        };
        footer.Controls.Add(btnClose);
        footer.Resize += (_, _) =>
            btnClose.Location = new Point(footer.ClientSize.Width - 104, 8);

        Controls.Add(step);
        Controls.Add(footer);
        AcceptButton = btnClose;

        ResumeLayout(false);
    }
}
