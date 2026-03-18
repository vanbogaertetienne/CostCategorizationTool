using CostCategorizationTool.Models;

namespace CostCategorizationTool.Forms;

public class RuleTypeSelectionDialog : Form
{
    public enum RuleChoice { Cancelled, JustThis, ByDescription, ByIBAN }

    public RuleChoice Choice      { get; private set; } = RuleChoice.Cancelled;
    public AmountSign SelectedSign => _rbSignPos.Checked ? AmountSign.Positive
                                    : _rbSignNeg.Checked ? AmountSign.Negative
                                    : AmountSign.Any;

    public static (RuleChoice Choice, AmountSign Sign) Show(
        IWin32Window? owner,
        Transaction   tx,
        string        categoryName,
        string        detectedPattern,
        bool          autoExpandDetails)
    {
        using var dlg = new RuleTypeSelectionDialog(tx, categoryName, detectedPattern, autoExpandDetails);
        dlg.ShowDialog(owner);
        return (dlg.Choice, dlg.SelectedSign);
    }

    // ── Controls ─────────────────────────────────────────────────────────────

    private readonly RadioButton _rbJustThis;
    private readonly RadioButton _rbByDesc;
    private readonly RadioButton _rbByIBAN;
    private readonly RadioButton _rbSignAny;
    private readonly RadioButton _rbSignPos;
    private readonly RadioButton _rbSignNeg;
    private readonly Label       _lblTechDetails;
    private readonly Panel       _detailsPanel;
    private readonly Button      _btnToggleDetails;
    private readonly Button      _btnOK;
    private readonly Button      _btnCancel;

    private readonly string _detectedPattern;
    private readonly string _iban;
    private readonly string _categoryName;

    private const int BaseClientHeight = 530;
    private const int DetailsHeight    = 90;

    private RuleTypeSelectionDialog(
        Transaction tx,
        string      categoryName,
        string      detectedPattern,
        bool        autoExpandDetails)
    {
        _detectedPattern = detectedPattern;
        _iban            = tx.Counterpart ?? "";
        _categoryName    = categoryName;

        SuspendLayout();

        Text            = "Categorise Transaction";
        ClientSize      = new Size(520, BaseClientHeight);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        Font            = new Font("Segoe UI", 9.5f);

        bool hasIBAN = !string.IsNullOrWhiteSpace(tx.Counterpart);

        // ── Transaction context ───────────────────────────────────────────────
        var lblCatHeader = new Label
        {
            Text     = $"Categorising as:  \"{categoryName}\"",
            Font     = new Font("Segoe UI", 11f, FontStyle.Bold),
            Location = new Point(14, 12),
            Size     = new Size(492, 26),
            AutoSize = false
        };

        var pnlCtx = new Panel
        {
            Location    = new Point(14, 44),
            Size        = new Size(492, 50),
            BackColor   = Color.FromArgb(245, 246, 252),
            BorderStyle = BorderStyle.FixedSingle
        };
        string snippet = tx.Details.Length > 90 ? tx.Details[..90] + "…" : tx.Details;
        pnlCtx.Controls.Add(new Label
        {
            Text = $"Date: {tx.ExecutionDate:dd/MM/yyyy}    Amount: {tx.Amount:N2} {tx.Currency}",
            Font = new Font("Segoe UI", 9f), Location = new Point(8, 5), AutoSize = true
        });
        pnlCtx.Controls.Add(new Label
        {
            Text = snippet, Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(90, 90, 90),
            Location = new Point(8, 26), Size = new Size(476, 18), AutoSize = false
        });

        // ── Rule type ─────────────────────────────────────────────────────────
        var sep0  = HRule(14, 102);
        var lblQ  = Bold("How should the app handle this categorisation?", 14, 110);

        _rbJustThis = Radio("Only this transaction", 14, 138);
        var lbl1 = Desc(
            "Only this specific payment will be categorised. No rule is saved —\n" +
            "other similar payments will not be affected.", 34, 160);

        var sep1 = HRule(14, 202);
        _rbByDesc = Radio("Similar payments — match by description keyword", 14, 210, defaultChecked: true);
        string descText = !string.IsNullOrWhiteSpace(detectedPattern)
            ? $"Payments whose description contains  \"{detectedPattern}\"  will\n" +
              $"be automatically categorised as \"{categoryName}\" in the future."
            : $"Payments with a similar description will be automatically\n" +
              $"categorised as \"{categoryName}\" in the future.";
        var lbl2 = Desc(descText, 34, 232);

        var sep2 = HRule(14, 278);
        _rbByIBAN = Radio("All payments to/from the same account (IBAN)", 14, 286, enabled: hasIBAN);
        string ibanText = hasIBAN
            ? $"Every payment to or from  {tx.Counterpart}  will be categorised\n" +
              $"as \"{categoryName}\". Only choose this if all payments from this\n" +
              $"account always belong to the same category."
            : "Not available — this transaction has no linked account number\n" +
              "(card payments do not carry a counterpart IBAN).";
        var lbl3 = Desc(ibanText, 34, 308, enabled: hasIBAN);

        // ── Transaction direction ─────────────────────────────────────────────
        var sep3 = HRule(14, 362);

        var grpSign = new GroupBox
        {
            Text     = "Transaction direction",
            Location = new Point(14, 372),
            Size     = new Size(492, 54),
            Font     = new Font("Segoe UI", 9f)
        };

        _rbSignAny = new RadioButton
        {
            Text = "All amounts", AutoSize = true, Location = new Point(10, 20),
            Checked = tx.Amount == 0
        };
        _rbSignPos = new RadioButton
        {
            Text = "Incoming only  (+)", AutoSize = true, Location = new Point(140, 20),
            Checked = tx.Amount > 0
        };
        _rbSignNeg = new RadioButton
        {
            Text = "Outgoing only  (−)", AutoSize = true, Location = new Point(300, 20),
            Checked = tx.Amount < 0
        };
        grpSign.Controls.AddRange(new Control[] { _rbSignAny, _rbSignPos, _rbSignNeg });

        // ── Technical details ─────────────────────────────────────────────────
        var sep4 = HRule(14, 434);

        _btnToggleDetails = new Button
        {
            Text      = "▶  Show technical details",
            AutoSize  = true,
            Location  = new Point(14, 442),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.SteelBlue,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 9f)
        };
        _btnToggleDetails.FlatAppearance.BorderSize = 0;
        _btnToggleDetails.Click += OnToggleDetails;

        _detailsPanel = new Panel
        {
            Location    = new Point(14, 470),
            Size        = new Size(492, DetailsHeight - 6),
            BackColor   = Color.FromArgb(245, 246, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Visible     = false
        };
        _lblTechDetails = new Label
        {
            Font = new Font("Consolas", 9f), Location = new Point(8, 6), AutoSize = true
        };
        _detailsPanel.Controls.Add(_lblTechDetails);

        _btnOK = new Button
        {
            Text = "Apply", Size = new Size(100, 32),
            Location = new Point(306, BaseClientHeight - 46)
        };
        _btnCancel = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel,
            Size = new Size(90, 32), Location = new Point(416, BaseClientHeight - 46)
        };

        _btnOK.Click += OnOK;

        // Wire sign changes to tech details update
        _rbJustThis.CheckedChanged += (_, _) => UpdateTechDetails();
        _rbByDesc.CheckedChanged   += (_, _) => UpdateTechDetails();
        _rbByIBAN.CheckedChanged   += (_, _) => UpdateTechDetails();
        _rbSignAny.CheckedChanged  += (_, _) => UpdateTechDetails();
        _rbSignPos.CheckedChanged  += (_, _) => UpdateTechDetails();
        _rbSignNeg.CheckedChanged  += (_, _) => UpdateTechDetails();
        UpdateTechDetails();

        Controls.AddRange(new Control[]
        {
            lblCatHeader, pnlCtx, sep0, lblQ,
            _rbJustThis, lbl1, sep1,
            _rbByDesc,   lbl2, sep2,
            _rbByIBAN,   lbl3, sep3,
            grpSign,     sep4,
            _btnToggleDetails, _detailsPanel,
            _btnOK, _btnCancel
        });

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;

        if (autoExpandDetails)
        {
            _detailsPanel.Visible  = true;
            _btnToggleDetails.Text = "▼  Hide technical details";
            _btnOK.Top            += DetailsHeight;
            _btnCancel.Top        += DetailsHeight;
            ClientSize = new Size(ClientSize.Width, BaseClientHeight + DetailsHeight);
        }

        ResumeLayout(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RadioButton Radio(string text, int x, int y,
        bool defaultChecked = false, bool enabled = true) => new RadioButton
    { Text = text, Font = new Font("Segoe UI", 10f), Location = new Point(x, y),
      AutoSize = true, Checked = defaultChecked, Enabled = enabled };

    private static Label Desc(string text, int x, int y, bool enabled = true) => new Label
    { Text = text, Font = new Font("Segoe UI", 9f),
      ForeColor = enabled ? Color.FromArgb(80, 80, 80) : Color.Silver,
      Location = new Point(x, y), Size = new Size(472, 44), AutoSize = false };

    private static Label Bold(string text, int x, int y) => new Label
    { Text = text, Font = new Font("Segoe UI", 10f, FontStyle.Bold),
      Location = new Point(x, y), AutoSize = true };

    private static Panel HRule(int x, int y) => new Panel
    { Location = new Point(x, y), Size = new Size(492, 1),
      BackColor = Color.FromArgb(210, 210, 210) };

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnOK(object? sender, EventArgs e)
    {
        Choice = _rbJustThis.Checked ? RuleChoice.JustThis
               : _rbByDesc.Checked   ? RuleChoice.ByDescription
                                     : RuleChoice.ByIBAN;
        DialogResult = DialogResult.OK;
    }

    private void OnToggleDetails(object? sender, EventArgs e)
    {
        bool show  = !_detailsPanel.Visible;
        int  delta = show ? DetailsHeight : -DetailsHeight;
        _detailsPanel.Visible  = show;
        _btnToggleDetails.Text = show ? "▼  Hide technical details" : "▶  Show technical details";
        _btnOK.Top    += delta;
        _btnCancel.Top += delta;
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + delta);
    }

    private void UpdateTechDetails()
    {
        string signText = _rbSignPos.Checked ? "Incoming payments only (positive amounts)"
                        : _rbSignNeg.Checked ? "Outgoing payments only (negative amounts)"
                        : "All amounts (positive and negative)";

        if (_rbJustThis.Checked)
        {
            _lblTechDetails.Text =
                "No rule will be saved.\r\n" +
                "Only this transaction is categorised for this session.";
        }
        else if (_rbByDesc.Checked)
        {
            string p = !string.IsNullOrWhiteSpace(_detectedPattern) ? _detectedPattern : "(no pattern detected)";
            _lblTechDetails.Text =
                $"Rule type:  Description keyword (case-insensitive contains)\r\n" +
                $"Pattern:     {p}\r\n" +
                $"Direction:   {signText}\r\n" +
                $"Category:   {_categoryName}";
        }
        else
        {
            _lblTechDetails.Text =
                $"Rule type:  Counterpart IBAN (exact match, case-insensitive)\r\n" +
                $"Pattern:     {_iban}\r\n" +
                $"Direction:   {signText}\r\n" +
                $"Category:   {_categoryName}";
        }
    }
}
