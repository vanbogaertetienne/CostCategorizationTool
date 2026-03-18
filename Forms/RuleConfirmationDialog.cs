using CostCategorizationTool.Models;

namespace CostCategorizationTool.Forms;

/// <summary>
/// Shows a user-friendly confirmation before an auto-generated categorisation rule is saved.
/// Plain-English description is always shown; technical details are hidden by default.
/// </summary>
public class RuleConfirmationDialog : Form
{
    private readonly Button _btnToggleDetails;
    private readonly Panel  _detailsPanel;
    private readonly Button _btnOK;
    private readonly Button _btnCancel;

    private const int BaseHeight    = 210;
    private const int DetailsHeight = 96;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>Shows the dialog and returns true if the user confirmed.</summary>
    public static bool Confirm(IWin32Window? owner, RuleType ruleType, string pattern, string categoryName)
    {
        using var dlg = new RuleConfirmationDialog(ruleType, pattern, categoryName);
        return dlg.ShowDialog(owner) == DialogResult.OK;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    private RuleConfirmationDialog(RuleType ruleType, string pattern, string categoryName)
    {
        SuspendLayout();

        Text            = "Save Categorisation Rule";
        ClientSize      = new Size(460, BaseHeight);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);

        // ── Info icon ─────────────────────────────────────────────────────────
        var lblIcon = new Label
        {
            Text      = "ℹ",
            Font      = new Font("Segoe UI", 22f),
            ForeColor = Color.SteelBlue,
            AutoSize  = true,
            Location  = new Point(14, 14)
        };

        // ── Plain-English description ─────────────────────────────────────────
        string description = ruleType == RuleType.IBAN
            ? $"Future bank transfers to or from account\n\n" +
              $"    {pattern}\n\n" +
              $"will automatically be categorised as  \"{categoryName}\"."
            : $"Future transactions whose description contains\n\n" +
              $"    \"{pattern}\"\n\n" +
              $"will automatically be categorised as  \"{categoryName}\".";

        var lblDesc = new Label
        {
            Text     = description,
            Font     = new Font("Segoe UI", 10f),
            Location = new Point(56, 14),
            Size     = new Size(390, 115),
            AutoSize = false
        };

        // ── Toggle details link ───────────────────────────────────────────────
        _btnToggleDetails = new Button
        {
            Text      = "▶  Show technical details",
            AutoSize  = true,
            Location  = new Point(56, 132),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.SteelBlue,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 9f)
        };
        _btnToggleDetails.FlatAppearance.BorderSize = 0;
        _btnToggleDetails.Click += OnToggleDetails;

        // ── Technical details panel (hidden by default) ───────────────────────
        string ruleTypeText = ruleType == RuleType.IBAN
            ? "Counterpart IBAN  (exact match, case-insensitive)"
            : "Description keyword  (case-insensitive contains)";

        _detailsPanel = new Panel
        {
            Location    = new Point(14, 158),
            Size        = new Size(432, DetailsHeight - 8),
            BackColor   = Color.FromArgb(245, 246, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Visible     = false
        };
        _detailsPanel.Controls.Add(new Label
        {
            Text     = $"Type:       {ruleTypeText}\r\n" +
                       $"Pattern:    {pattern}\r\n" +
                       $"Category:   {categoryName}",
            Font     = new Font("Consolas", 9f),
            Location = new Point(8, 8),
            AutoSize = true
        });

        // ── OK / Cancel buttons ───────────────────────────────────────────────
        _btnOK = new Button
        {
            Text         = "Save Rule",
            DialogResult = DialogResult.OK,
            Size         = new Size(110, 32),
            Location     = new Point(230, BaseHeight - 46)
        };

        _btnCancel = new Button
        {
            Text         = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size         = new Size(90, 32),
            Location     = new Point(354, BaseHeight - 46)
        };

        Controls.AddRange(new Control[]
        {
            lblIcon, lblDesc, _btnToggleDetails, _detailsPanel, _btnOK, _btnCancel
        });

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;
        ResumeLayout(false);
    }

    private void OnToggleDetails(object? sender, EventArgs e)
    {
        bool show  = !_detailsPanel.Visible;
        int  delta = show ? DetailsHeight : -DetailsHeight;

        _detailsPanel.Visible      = show;
        _btnToggleDetails.Text     = show ? "▼  Hide technical details" : "▶  Show technical details";
        _btnOK.Top    += delta;
        _btnCancel.Top += delta;
        Height         += delta;
    }
}
