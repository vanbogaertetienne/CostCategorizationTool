using CostCategorizationTool.Data;
using CostCategorizationTool.Models;

namespace CostCategorizationTool.Forms;

public class SettingsDialog : Form
{
    // ── Results that WizardForm checks after the dialog closes ────────────────
    public bool ResetTransactionsRequested { get; private set; }
    public bool ResetDatabaseExecuted      { get; private set; }

    private readonly AppDatabase _db;
    private readonly AppSettings _settings;
    private readonly CheckBox    _chkConfirmRules;

    public SettingsDialog(AppDatabase db, AppSettings settings)
    {
        _db       = db;
        _settings = settings;

        SuspendLayout();

        Text            = "Settings";
        ClientSize      = new Size(480, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);

        int y = 14;

        // ── Section: Categorization ───────────────────────────────────────────
        var grpCategorization = new GroupBox
        {
            Text     = "Categorization",
            Location = new Point(14, y),
            Size     = new Size(452, 100),
            Font     = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _chkConfirmRules = new CheckBox
        {
            Text     = "Ask for confirmation before saving rules",
            AutoSize = true,
            Location = new Point(12, 24),
            Font     = new Font("Segoe UI", 9.5f),
            Checked  = _settings.ConfirmBeforeRuleModification
        };

        var lblChkDesc = new Label
        {
            Text      = "When enabled, the app will show you a plain-English explanation of\n" +
                        "each rule before it is saved, and ask you to confirm.",
            AutoSize  = false,
            Size      = new Size(428, 40),
            Location  = new Point(28, 46),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        grpCategorization.Controls.Add(_chkConfirmRules);
        grpCategorization.Controls.Add(lblChkDesc);
        y += grpCategorization.Height + 14;

        // ── Section: Maintenance ─────────────────────────────────────────────
        var grpMaintenance = new GroupBox
        {
            Text     = "Maintenance",
            Location = new Point(14, y),
            Size     = new Size(452, 220),
            Font     = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        // Reset Database
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

        // Reset Transactions
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

        // ── Close button ─────────────────────────────────────────────────────
        var btnClose = new Button
        {
            Text         = "Close",
            DialogResult = DialogResult.OK,
            Size         = new Size(90, 32),
            Location     = new Point(386, 376)
        };

        Controls.AddRange(new Control[] { grpCategorization, grpMaintenance, btnClose });
        AcceptButton = btnClose;
        CancelButton = btnClose;
        ResumeLayout(false);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Persist settings whenever the dialog closes
        _settings.ConfirmBeforeRuleModification = _chkConfirmRules.Checked;
        _settings.Save();
        base.OnFormClosing(e);
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
