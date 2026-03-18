using System.Globalization;
using System.Text;
using CostCategorizationTool.Models;

namespace CostCategorizationTool.Services;

public class CsvParserService
{
    public List<Transaction> ParseFile(string filePath)
    {
        // Try UTF-8 first, fall back to Latin1
        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath, Encoding.UTF8);
            // Sanity check: if the header doesn't contain expected French characters, retry with Latin1
            if (lines.Length > 0 && !lines[0].Contains("S") && lines[0].Contains("?"))
                lines = File.ReadAllLines(filePath, Encoding.GetEncoding("iso-8859-1"));
        }
        catch
        {
            lines = File.ReadAllLines(filePath, Encoding.GetEncoding("iso-8859-1"));
        }

        var transactions = new List<Transaction>();
        // Skip header line (index 0)
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var fields = SplitCsvLine(line, ';');
            if (fields.Length < 12) continue;

            var tx = new Transaction
            {
                SequenceNumber  = fields[0].Trim(),
                ExecutionDate   = ParseDate(fields[1].Trim()),
                ValueDate       = ParseDate(fields[2].Trim()),
                Amount          = ParseAmount(fields[3].Trim()),
                Currency        = fields[4].Trim(),
                AccountNumber   = fields[5].Trim(),
                TransactionType = fields[6].Trim(),
                Counterpart     = fields[7].Trim(),
                CounterpartName = fields[8].Trim(),
                Communication   = fields[9].Trim(),
                Details         = fields[10].Trim(),
                Status          = fields[11].Trim()
            };
            transactions.Add(tx);
        }
        return transactions;
    }

    private static string[] SplitCsvLine(string line, char delimiter)
    {
        // Simple split; Belgian bank exports don't typically quote fields
        return line.Split(delimiter);
    }

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.MinValue;
    }

    private static decimal ParseAmount(string value)
    {
        // European format: comma as decimal separator, possible dot as thousands separator
        var normalized = value.Replace(".", "").Replace(",", ".");
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return amount;
        return 0m;
    }
}
