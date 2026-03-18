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

    /// <summary>
    /// Estimates how often this group recurs based on average gap between consecutive
    /// transaction dates.  Returns "Once" for single transactions.
    /// </summary>
    public string FrequencyLabel
    {
        get
        {
            if (Count <= 1) return "Once";
            var dates = Transactions.Select(t => t.ExecutionDate).OrderBy(d => d).ToList();
            double avg = (dates.Last() - dates.First()).TotalDays / (dates.Count - 1);
            if (avg <  3)  return "Daily";
            if (avg <  9)  return "Weekly";
            if (avg < 19)  return "Bi-weekly";
            if (avg < 45)  return "Monthly";
            if (avg < 105) return "Quarterly";
            return "Irregular";
        }
    }
}
