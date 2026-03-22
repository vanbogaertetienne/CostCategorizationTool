using ClosedXML.Excel;
using CostCategorizationTool.Models;
using System.Globalization;

namespace CostCategorizationTool.Services;

public class ExcelExportService
{
    public void Export(
        string filePath,
        List<Transaction>      transactions,
        List<Category>         categories,
        List<TransactionGroup> groups)
    {
        using var wb = new XLWorkbook();

        WriteTransactions(wb, transactions, categories, groups);
        WriteCategories(wb, transactions, categories);
        WriteGroups(wb, groups, categories);
        WritePivot(wb);

        wb.SaveAs(filePath);
    }

    // ── Sheet 1: All transactions ─────────────────────────────────────────────

    private static void WriteTransactions(
        XLWorkbook wb, List<Transaction> transactions, List<Category> categories,
        List<TransactionGroup> groups)
    {
        var ws = wb.AddWorksheet("Transactions");

        // Build transaction → group display name lookup
        var txGroupName = new Dictionary<Transaction, string>(ReferenceEqualityComparer.Instance);
        foreach (var g in groups)
            foreach (var tx in g.Transactions)
                txGroupName[tx] = g.DisplayName;

        // Headers
        string[] headers =
        {
            "Date", "Year", "Quarter", "Month",
            "Amount", "Currency", "AccountNumber",
            "TransactionType", "Counterpart", "CounterpartName",
            "Communication", "Details", "Category", "Group"
        };
        for (int c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        // Data
        for (int r = 0; r < transactions.Count; r++)
        {
            var tx  = transactions[r];
            int row = r + 2;
            var cat = tx.CategoryId.HasValue
                ? categories.FirstOrDefault(c => c.Id == tx.CategoryId)?.Name ?? ""
                : "";
            var grpName = txGroupName.TryGetValue(tx, out var gn) ? gn : "";

            ws.Cell(row, 1).Value  = tx.ExecutionDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value  = tx.ExecutionDate.Year;
            ws.Cell(row, 3).Value  = $"Q{(tx.ExecutionDate.Month - 1) / 3 + 1}";
            ws.Cell(row, 4).Value  = tx.ExecutionDate.ToString("MMM", CultureInfo.InvariantCulture);
            ws.Cell(row, 5).Value  = (double)tx.Amount;
            ws.Cell(row, 6).Value  = tx.Currency;
            ws.Cell(row, 7).Value  = tx.AccountNumber;
            ws.Cell(row, 8).Value  = tx.TransactionType;
            ws.Cell(row, 9).Value  = tx.Counterpart;
            ws.Cell(row, 10).Value = tx.CounterpartName;
            ws.Cell(row, 11).Value = tx.Communication;
            ws.Cell(row, 12).Value = tx.Details;
            ws.Cell(row, 13).Value = cat;
            ws.Cell(row, 14).Value = grpName;
        }

        // Format as Excel table
        if (transactions.Count > 0)
        {
            var tbl = ws.Range(1, 1, transactions.Count + 1, headers.Length)
                        .CreateTable("TransactionData");
            tbl.Theme = XLTableTheme.TableStyleMedium2;
        }

        // Amount column: number format
        ws.Column(5).Style.NumberFormat.Format = "#,##0.00";

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 60);
    }

    // ── Sheet 2: Category summary ─────────────────────────────────────────────

    private static void WriteCategories(
        XLWorkbook wb, List<Transaction> transactions, List<Category> categories)
    {
        var ws = wb.AddWorksheet("Categories");

        ws.Cell(1, 1).Value = "Category";
        ws.Cell(1, 2).Value = "# Transactions";
        ws.Cell(1, 3).Value = "Total Amount";

        var rows = transactions
            .Where(t => t.CategoryId.HasValue)
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g =>
            {
                var cat = categories.FirstOrDefault(c => c.Id == g.Key);
                return (Name: cat?.Name ?? "Unknown", Count: g.Count(), Total: g.Sum(t => (double)t.Amount));
            })
            .OrderBy(x => x.Total)
            .ToList();

        for (int r = 0; r < rows.Count; r++)
        {
            ws.Cell(r + 2, 1).Value = rows[r].Name;
            ws.Cell(r + 2, 2).Value = rows[r].Count;
            ws.Cell(r + 2, 3).Value = rows[r].Total;
        }

        int uncatCount = transactions.Count(t => !t.CategoryId.HasValue && t.Amount < 0);
        if (uncatCount > 0)
        {
            int row = rows.Count + 2;
            ws.Cell(row, 1).Value = "Uncategorized";
            ws.Cell(row, 2).Value = uncatCount;
            ws.Cell(row, 3).Value = (double)transactions.Where(t => !t.CategoryId.HasValue && t.Amount < 0).Sum(t => t.Amount);
            ws.Row(row).Style.Font.FontColor = XLColor.DarkRed;
        }

        int lastRow = rows.Count + (uncatCount > 0 ? 1 : 0) + 1;
        if (lastRow > 1)
        {
            var tbl = ws.Range(1, 1, lastRow, 3).CreateTable("CategorySummary");
            tbl.Theme = XLTableTheme.TableStyleMedium6;
        }

        ws.Column(3).Style.NumberFormat.Format = "#,##0.00";
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 50);
    }

    // ── Sheet 3: Groups overview ──────────────────────────────────────────────

    private static void WriteGroups(
        XLWorkbook wb, List<TransactionGroup> groups, List<Category> categories)
    {
        var ws = wb.AddWorksheet("Groups");

        ws.Cell(1, 1).Value = "#";
        ws.Cell(1, 2).Value = "Type";
        ws.Cell(1, 3).Value = "Pattern / Name";
        ws.Cell(1, 4).Value = "Direction";
        ws.Cell(1, 5).Value = "Frequency";
        ws.Cell(1, 6).Value = "Total";
        ws.Cell(1, 7).Value = "Category";

        for (int r = 0; r < groups.Count; r++)
        {
            var g   = groups[r];
            int row = r + 2;
            var cat = g.CategoryId.HasValue
                ? categories.FirstOrDefault(c => c.Id == g.CategoryId)?.Name ?? ""
                : "";

            ws.Cell(row, 1).Value = g.Count;
            ws.Cell(row, 2).Value = g.RuleType == RuleType.IBAN ? "IBAN" : "Keyword";
            ws.Cell(row, 3).Value = g.DisplayName;
            ws.Cell(row, 4).Value = g.DirectionLabel;
            ws.Cell(row, 5).Value = g.FrequencyLabel;
            ws.Cell(row, 6).Value = (double)g.Total;
            ws.Cell(row, 7).Value = cat;

            // Colour by direction (matches the grid colouring)
            var bg = g.DetectedSign == AmountSign.Positive
                ? XLColor.FromArgb(235, 255, 235)
                : XLColor.FromArgb(255, 238, 238);
            ws.Row(row).Style.Fill.BackgroundColor = bg;
        }

        if (groups.Count > 0)
        {
            var tbl = ws.Range(1, 1, groups.Count + 1, 7).CreateTable("GroupsOverview");
            tbl.Theme = XLTableTheme.TableStyleMedium9;
        }

        ws.Column(6).Style.NumberFormat.Format = "#,##0.00";
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 60);
    }

    // ── Sheet 4: Pivot table ──────────────────────────────────────────────────

    private static void WritePivot(XLWorkbook wb)
    {
        var wsData = wb.Worksheet("Transactions");
        var dataRange = wsData.RangeUsed();
        if (dataRange == null) return;

        var ws = wb.AddWorksheet("Pivot");

        ws.Cell("A1").Value = "Pivot table";
        ws.Cell("A1").Style.Font.Bold      = true;
        ws.Cell("A1").Style.Font.FontSize  = 12;
        ws.Cell("A2").Value = "Source: Transactions sheet — refresh with right-click → Refresh if you updated data.";
        ws.Cell("A2").Style.Font.Italic    = true;
        ws.Cell("A2").Style.Font.FontColor = XLColor.Gray;

        try
        {
            var pt = ws.PivotTables.Add("CategoryOverview", ws.Cell("A4"), dataRange);
            pt.RowLabels.Add("Category");
            pt.ColumnLabels.Add("Year");
            pt.Values.Add("Amount", "Sum of Amount").SetSummaryFormula(XLPivotSummary.Sum);
        }
        catch
        {
            // Fallback: write a note if pivot table creation fails
            ws.Cell("A4").Value = "Pivot table creation not supported in this environment.";
            ws.Cell("A5").Value = "Please create a pivot table manually from the Transactions sheet.";
        }
    }
}
