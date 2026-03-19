using CostCategorizationTool.Data;

namespace CostCategorizationTool.Forms;

public class SettingsDialog : Form
{
    public bool ResetTransactionsRequested { get; private set; }
    public bool ResetDatabaseExecuted      { get; private set; }

    private readonly AppDatabase _db;

    public SettingsDialog(AppDatabase db)
    {
        _db = db;

        SuspendLayout();

        Text            = "Settings";
        ClientSize      = new Size(480, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);

        // ── Section: Maintenance ─────────────────────────────────────────────
        var grpMaintenance = new GroupBox
        {
            Text     = "Maintenance",
            Location = new Point(14, 14),
            Size     = new Size(452, 220),
            Font     = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        var btnResetDb = new Button
        {
            Text      = "Reset Database",
            Size      = new Size(148, 30),
            Location  = new Point(12, 24),
            ForeColor = Color.DarkRed
        };
        btnResetDb.Click += OnResetDatabase;

        var lblResetDb = new Label
        {
            Text      = "Deletes all your categories and rules, and restores the original\n" +
                        "default categories (Food & Groceries, Transport, Leisure, …).\n" +
                        "Your loaded transactions are not affected.",
            AutoSize  = false,
            Size      = new Size(428, 52),
            Location  = new Point(12, 62),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        var separator = new Panel
        {
            Location  = new Point(12, 122),
            Size      = new Size(428, 1),
            BackColor = Color.FromArgb(210, 210, 210)
        };

        var btnResetTx = new Button
        {
            Text      = "Reset Transactions",
            Size      = new Size(148, 30),
            Location  = new Point(12, 130),
            ForeColor = Color.DarkRed
        };
        btnResetTx.Click += OnResetTransactions;

        var lblResetTx = new Label
        {
            Text      = "Clears the transactions currently loaded in this session.\n" +
                        "You will be taken back to the file selection step so you\n" +
                        "can load a different CSV file.",
            AutoSize  = false,
            Size      = new Size(428, 52),
            Location  = new Point(12, 168),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        grpMaintenance.Controls.AddRange(new Control[]
        {
            btnResetDb, lblResetDb, separator, btnResetTx, lblResetTx
        });

        var btnClose = new Button
        {
            Text         = "Close",
            DialogResult = DialogResult.OK,
            Size         = new Size(90, 32),
            Location     = new Point(386, 266)
        };

        Controls.AddRange(new Control[] { grpMaintenance, btnClose });
        AcceptButton = btnClose;
        CancelButton = btnClose;
        ResumeLayout(false);
    }

    private void OnResetDatabase(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "This will permanently delete all your categories and categorisation rules,\n" +
            "and restore the original default categories.\n\n" +
            "This cannot be undone. Are you sure?",
            "Reset Database",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes) return;

        _db.ResetDatabase();
        ResetDatabaseExecuted = true;

        MessageBox.Show(
            "The database has been reset.\n" +
            "Default categories have been restored.\n\n" +
            "You will be returned to the file selection step.",
            "Reset Complete",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnResetTransactions(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "This will clear the transactions you have loaded in this session.\n\n" +
            "You will be returned to the file selection step.\n" +
            "Are you sure?",
            "Reset Transactions",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes) return;

        ResetTransactionsRequested = true;

        MessageBox.Show(
            "Transactions have been cleared.\n" +
            "You will be returned to the file selection step.",
            "Transactions Cleared",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
