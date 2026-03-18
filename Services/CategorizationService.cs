using CostCategorizationTool.Models;

namespace CostCategorizationTool.Services;

public class CategorizationService
{
    public void AutoCategorize(List<Transaction> transactions, List<CategoryRule> rules)
    {
        // Details rules are evaluated before IBAN rules so that a specific keyword
        // match always wins over a broad "all payments from this account" IBAN rule.
        var orderedRules = rules
            .OrderBy(r => r.RuleType == RuleType.IBAN ? 1 : 0)
            .ToList();

        foreach (var tx in transactions)
        {
            if (tx.CategoryId.HasValue) continue;

            foreach (var rule in orderedRules)
            {
                // Sign filter
                bool signOk = rule.AmountSign == AmountSign.Any
                    || (rule.AmountSign == AmountSign.Positive && tx.Amount > 0)
                    || (rule.AmountSign == AmountSign.Negative && tx.Amount < 0);
                if (!signOk) continue;

                bool matched = rule.RuleType switch
                {
                    RuleType.IBAN =>
                        !string.IsNullOrEmpty(tx.Counterpart) &&
                        string.Equals(tx.Counterpart, rule.Pattern, StringComparison.OrdinalIgnoreCase),
                    RuleType.Details =>
                        !string.IsNullOrEmpty(rule.Pattern) &&
                        tx.SearchableText.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };

                if (matched) { tx.CategoryId = rule.CategoryId; break; }
            }
        }
    }

    public void ReAutoCategorize(List<Transaction> transactions, List<CategoryRule> rules,
        HashSet<int>? manuallySetSequences = null)
    {
        foreach (var tx in transactions)
        {
            if (manuallySetSequences == null || !manuallySetSequences.Contains(tx.GetHashCode()))
                tx.CategoryId = null;
        }
        AutoCategorize(transactions, rules);
    }
}
