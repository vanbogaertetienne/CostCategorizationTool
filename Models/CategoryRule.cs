namespace CostCategorizationTool.Models;

public enum RuleType   { IBAN = 0, Details = 1 }
public enum AmountSign { Any = 0, Positive = 1, Negative = 2 }

public class CategoryRule
{
    public int        Id         { get; set; }
    public int        CategoryId { get; set; }
    public RuleType   RuleType   { get; set; }
    public string     Pattern    { get; set; } = "";
    public bool       IsAuto     { get; set; }
    public AmountSign AmountSign { get; set; } = AmountSign.Any;
}
