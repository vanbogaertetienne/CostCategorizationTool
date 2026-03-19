using System.Globalization;
using CostCategorizationTool.Data;
using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms;

public class SettingsDialog : Form
{
    public bool ResetTransactionsRequested { get; private set; }
    public bool ResetDatabaseExecuted      { get; private set; }

    private readonly AppDatabase _db;
    private readonly AppSettings _settings;
    private readonly ComboBox    _cmbLanguage;

    public SettingsDialog(AppDatabase db, AppSettings settings)
    {
        _db       = db;
        _settings = settings;

        SuspendLayout();

        var btnF = new Font("Segoe UI", 10f);

        Text            = Resources.SettTitle;
        ClientSize      = new Size(500, 430);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 10f);

        // ── Language section ─────────────────────────────────────────────────
        var grpLanguage = new GroupBox
        {
            Text     = Resources.SettLanguage,
            Location = new Point(14, 14),
            Size     = new Size(472, 90),
            Font     = new Font("Segoe UI", 10f, FontStyle.Bold)
        };

        _cmbLanguage = new ComboBox
        {
            Location      = new Point(12, 28),
            Size          = new Size(200, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("Segoe UI", 10f)
        };
        foreach (var (code, name) in new[] { ("en", "English"), ("fr", "Français") })
            _cmbLanguage.Items.Add(new LanguageItem(code, name));

        // Select current language
        string currentLang = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";
        for (int i = 0; i < _cmbLanguage.Items.Count; i++)
        {
            if (_cmbLanguage.Items[i] is LanguageItem li && li.Code == currentLang)
            { _cmbLanguage.SelectedIndex = i; break; }
        }
        _cmbLanguage.SelectedIndexChanged += OnLanguageChanged;

        var lblLangNote = new Label
        {
            Text      = Resources.SettLangNote,
            AutoSize  = false,
            Size      = new Size(448, 34),
            Location  = new Point(12, 58),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        grpLanguage.Controls.AddRange(new Control[] { _cmbLanguage, lblLangNote });

        // ── Maintenance section ──────────────────────────────────────────────
        var grpMaintenance = new GroupBox
        {
            Text     = Resources.SettMaintenance,
            Location = new Point(14, 116),
            Size     = new Size(472, 252),
            Font     = new Font("Segoe UI", 10f, FontStyle.Bold)
        };

        var btnResetDb = new Button
        {
            Text      = Resources.BtnResetDb,
            Size      = new Size(TextRenderer.MeasureText(Resources.BtnResetDb, btnF).Width + 24, 36),
            Location  = new Point(12, 28),
            ForeColor = Color.DarkRed,
            Font      = btnF
        };
        btnResetDb.Click += OnResetDatabase;

        var lblResetDb = new Label
        {
            Text      = Resources.ResetDbDesc,
            AutoSize  = false,
            Size      = new Size(448, 60),
            Location  = new Point(12, 72),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        var separator = new Panel
        {
            Location  = new Point(12, 140),
            Size      = new Size(448, 1),
            BackColor = Color.FromArgb(210, 210, 210)
        };

        var btnResetTx = new Button
        {
            Text      = Resources.BtnResetTx,
            Size      = new Size(TextRenderer.MeasureText(Resources.BtnResetTx, btnF).Width + 24, 36),
            Location  = new Point(12, 150),
            ForeColor = Color.DarkRed,
            Font      = btnF
        };
        btnResetTx.Click += OnResetTransactions;

        var lblResetTx = new Label
        {
            Text      = Resources.ResetTxDesc,
            AutoSize  = false,
            Size      = new Size(448, 60),
            Location  = new Point(12, 194),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        grpMaintenance.Controls.AddRange(new Control[]
        {
            btnResetDb, lblResetDb, separator, btnResetTx, lblResetTx
        });

        int closeW = TextRenderer.MeasureText(Resources.Close, btnF).Width + 24;
        var btnClose = new Button
        {
            Text         = Resources.Close,
            DialogResult = DialogResult.OK,
            Size         = new Size(closeW, 36),
            Location     = new Point(500 - 14 - closeW, 388),
            Font         = btnF
        };

        Controls.AddRange(new Control[] { grpLanguage, grpMaintenance, btnClose });
        AcceptButton = btnClose;
        CancelButton = btnClose;
        ResumeLayout(false);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_cmbLanguage.SelectedItem is not LanguageItem li) return;
        _settings.Language = li.Code;
        _settings.Save();
    }

    private void OnResetDatabase(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(Resources.ResetDbConfirmMsg, Resources.ResetDbConfirmTitle,
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;
        _db.ResetDatabase();
        ResetDatabaseExecuted = true;
        MessageBox.Show(Resources.ResetDbDoneMsg, Resources.ResetDbDoneTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnResetTransactions(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(Resources.ResetTxConfirmMsg, Resources.ResetTxConfirmTitle,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;
        _db.ClearTransactions();
        ResetTransactionsRequested = true;
        MessageBox.Show(Resources.ResetTxDoneMsg, Resources.ResetTxDoneTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private record LanguageItem(string Code, string Name)
    {
        public override string ToString() => Name;
    }
}
