namespace CostCategorizationTool.Models;

public class TransactionGroup
{
    public required RuleType          RuleType     { get; init; }
    public required string            Pattern      { get; init; }
    public required string            DisplayName  { get; init; }
    public required AmountSign        DetectedSign { get; init; }
    public required List<Transaction> Transactions { get; init; }

    public int?    CategoryId { get; set; }
    public decimal Total      => Transactions.Sum(t => t.Amount);
    public int     Count      => Transactions.Count;

    public string DirectionLabel =>
        DetectedSign == AmountSign.Positive ? "Incoming" :
        DetectedSign == AmountSign.Negative ? "Outgoing" : "Mixed";
}
