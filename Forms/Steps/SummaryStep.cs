using System.Globalization;
using System.Text;
using CostCategorizationTool.Models;
using CostCategorizationTool.Services;

namespace CostCategorizationTool.Forms.Steps;

public class SummaryStep : UserControl
{
    private List<Transaction>      _transactions = new();
    private List<Category>         _categories   = new();
    private List<TransactionGroup> _groups       = new();

    private readonly ListView _summaryList;
    private readonly Button   _btnExport;
    private readonly Button   _btnExportExcel;
    private readonly Label    _lblTotal;

    public SummaryStep()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode       = AutoScaleMode.Dpi;
        BackColor = Color.White;

        var titleLabel = new Label
        {
            Text     = Resources.SumTitle,
            Font     = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 16)
        };

        _summaryList = new ListView
        {
            Location      = new Point(16, 52),
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = true,
            Font          = new Font("Segoe UI", 10f),
            Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle   = BorderStyle.Fixed3D
        };
        _summaryList.Columns.Add(Resources.SumColCategory,  200);
        _summaryList.Columns.Add(Resources.SumColCount,     130);
        _summaryList.Columns.Add(Resources.SumColTotal,     150);

        _lblTotal = new Label
        {
            Text      = "",
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            AutoSize  = true,
            ForeColor = Color.DarkBlue,
            Anchor    = AnchorStyles.Bottom | AnchorStyles.Left
        };

        var btnFont = new Font("Segoe UI", 10f);
        _btnExport = new Button
        {
            Text   = Resources.BtnExportCsv,
            Size   = new Size(UiScaler.BW(Resources.BtnExportCsv, btnFont), 32),
            Font   = btnFont,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        _btnExportExcel = new Button
        {
            Text      = Resources.BtnExportExcel,
            Size      = new Size(UiScaler.BW(Resources.BtnExportExcel, btnFont), 32),
            Font      = btnFont,
            Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.FromArgb(33, 115, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        Controls.Add(titleLabel);
        Controls.Add(_summaryList);
        Controls.Add(_lblTotal);
        Controls.Add(_btnExport);
        Controls.Add(_btnExportExcel);

        _btnExport.Click      += OnExport;
        _btnExportExcel.Click += OnExportExcel;
        Resize += OnResize;

        ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LayoutControls();
    }

    private void OnResize(object? sender, EventArgs e) => LayoutControls();

    private void LayoutControls()
    {
        int Sc(int v) => (int)(v * (float)DeviceDpi / 96f);
        int m  = Sc(16);
        int w  = ClientSize.Width - 2 * m;
        int h  = ClientSize.Height - Sc(52) - Sc(44);
        _summaryList.Location = new Point(m, Sc(52));
        _summaryList.Size     = new Size(Math.Max(w, 200), Math.Max(h, 60));

        int bottom = _summaryList.Bottom + Sc(8);
        _lblTotal.Location        = new Point(m, bottom);
        _btnExport.Location       = new Point(ClientSize.Width - m - _btnExport.Width, bottom - 2);
        _btnExportExcel.Location  = new Point(_btnExport.Left - Sc(8) - _btnExportExcel.Width, bottom - 2);

        // Resize columns proportionally
        if (_summaryList.Columns.Count >= 3)
        {
            int total = _summaryList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
            _summaryList.Columns[0].Width = (int)(total * 0.45);
            _summaryList.Columns[1].Width = (int)(total * 0.25);
            _summaryList.Columns[2].Width = total - _summaryList.Columns[0].Width - _summaryList.Columns[1].Width;
        }
    }

    public void LoadSummary(List<Transaction> transactions, List<Category> categories,
        List<TransactionGroup>? groups = null)
    {
        _transactions = transactions;
        _categories   = categories;
        _groups       = groups ?? new();

        _summaryList.Items.Clear();

        // Group expenses by category
        var expenses = transactions.Where(t => t.IsExpense).ToList();
        var income   = transactions.Where(t => !t.IsExpense).ToList();

        // Group by category
        var grouped = expenses
            .Where(t => t.CategoryId.HasValue)
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g =>
            {
                var cat = categories.FirstOrDefault(c => c.Id == g.Key);
                return (Name: cat?.Name ?? "Unknown", Count: g.Count(), Total: g.Sum(t => t.Amount));
            })
            .OrderBy(x => x.Total) // most negative (biggest expense) first
            .ToList();

        foreach (var row in grouped)
        {
            var item = new ListViewItem(row.Name);
            item.SubItems.Add(row.Count.ToString());
            item.SubItems.Add(row.Total.ToString("N2", CultureInfo.CurrentCulture));
            _summaryList.Items.Add(item);
        }

        // Uncategorized expenses
        var uncategorized = expenses.Where(t => !t.CategoryId.HasValue).ToList();
        if (uncategorized.Count > 0)
        {
            var item = new ListViewItem(Resources.SumUncategorized) { ForeColor = Color.DarkRed };
            item.SubItems.Add(uncategorized.Count.ToString());
            item.SubItems.Add(uncategorized.Sum(t => t.Amount).ToString("N2", CultureInfo.CurrentCulture));
            _summaryList.Items.Add(item);
        }

        // Income row
        if (income.Count > 0)
        {
            var item = new ListViewItem(Resources.SumIncome) { ForeColor = Color.DarkGreen };
            item.SubItems.Add(income.Count.ToString());
            item.SubItems.Add(income.Sum(t => t.Amount).ToString("N2", CultureInfo.CurrentCulture));
            _summaryList.Items.Add(item);
        }

        // Grand total row for expenses
        decimal grandTotal = expenses.Sum(t => t.Amount);
        var totalItem = new ListViewItem(Resources.SumTotalExp)
        {
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.DarkBlue
        };
        totalItem.SubItems.Add(expenses.Count.ToString());
        totalItem.SubItems.Add(grandTotal.ToString("N2", CultureInfo.CurrentCulture));
        _summaryList.Items.Add(totalItem);

        _lblTotal.Text = string.Format(Resources.SumGrandTotal, grandTotal,
            transactions.FirstOrDefault()?.Currency ?? "EUR");

        LayoutControls();
    }

    private void OnExportExcel(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title      = Resources.ExportExcelTitle,
            Filter     = Resources.ExportExcelFilter,
            FileName   = $"CostCategorization_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExt = "xlsx"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var svc = new ExcelExportService();
            svc.Export(dlg.FileName, _transactions, _categories, _groups);
            MessageBox.Show(string.Format(Resources.ExportCompleteMsg, dlg.FileName), Resources.ExportComplete,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Resources.ExportFailed, ex.Message), Resources.ExportError,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExport(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title            = Resources.ExportTitle,
            Filter           = Resources.ExportFilter,
            FileName         = $"CostSummary_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            DefaultExt       = "csv"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var sb = new StringBuilder();

            // Summary section
            sb.AppendLine("=== SUMMARY ===");
            sb.AppendLine("Category;Transactions;Total Amount");
            foreach (ListViewItem item in _summaryList.Items)
                sb.AppendLine($"{item.Text};{item.SubItems[1].Text};{item.SubItems[2].Text}");

            sb.AppendLine();

            // All transactions
            sb.AppendLine("=== ALL TRANSACTIONS ===");
            sb.AppendLine("Date;Amount;Currency;Counterpart;CounterpartName;Details;Category;Status");
            foreach (var tx in _transactions)
            {
                var catName = tx.CategoryId.HasValue
                    ? _categories.FirstOrDefault(c => c.Id == tx.CategoryId)?.Name ?? "Unknown"
                    : Resources.SumUncategorized;
                sb.AppendLine(
                    $"{tx.ExecutionDate:dd/MM/yyyy};" +
                    $"{tx.Amount.ToString("N2", CultureInfo.CurrentCulture)};" +
                    $"{tx.Currency};" +
                    $"{tx.Counterpart};" +
                    $"{tx.CounterpartName};" +
                    $"\"{tx.Details.Replace("\"", "\"\"")}\";" +
                    $"{catName};" +
                    $"{tx.Status}"
                );
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(string.Format(Resources.ExportCompleteMsg, dlg.FileName), Resources.ExportComplete,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Resources.ExportFailed, ex.Message), Resources.ExportError,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
