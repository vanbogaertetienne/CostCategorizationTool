namespace CostCategorizationTool.Models;

public class Transaction
{
    public string SequenceNumber { get; set; } = "";
    public DateTime ExecutionDate { get; set; }
    public DateTime ValueDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public string Counterpart { get; set; } = "";
    public string CounterpartName { get; set; } = "";
    public string Communication { get; set; } = "";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "";
    public int? CategoryId { get; set; }
    public bool IsExpense => Amount < 0;

    /// <summary>
    /// Concatenation of all free-text fields used for keyword matching and
    /// tokenization.  Different banks put merchant names in different columns
    /// (Details vs Communication vs CounterpartName), so we search across all of them.
    /// </summary>
    public string SearchableText =>
        string.Join(" ", Details, Communication, CounterpartName)
              .Trim();
}
