using CostCategorizationTool.Models;

namespace CostCategorizationTool.Services;

public class CategorizationService
{
    public void AutoCategorize(List<Transaction> transactions, List<CategoryRule> rules)
    {
        // Details rules before IBAN rules; within Details rules, longer patterns first
        // so that "Brico Vilvoorde" is tested before the broader "Brico".
        var orderedRules = rules
            .OrderBy(r => r.RuleType == RuleType.IBAN ? 1 : 0)
            .ThenByDescending(r => r.Pattern.Length)
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
                        MatchesDetails(tx, rule.Pattern),
                    _ => false
                };

                if (matched) { tx.CategoryId = rule.CategoryId; break; }
            }
        }
    }

    /// <summary>
    /// Checks a Details rule pattern against a transaction.
    /// First tries a raw substring match (covers manually-typed rules).
    /// If that fails, also tries against the space-joined cleaned tokens,
    /// because auto-generated patterns are derived from tokenised text and
    /// may not literally appear in the raw description (e.g. "APPLE BILL"
    /// won't be found as-is in "APPLE.COM BILL" without the token check).
    /// </summary>
    private static bool MatchesDetails(Transaction tx, string pattern)
    {
        if (tx.SearchableText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        var cleanedText = string.Join(" ",
            IntelliCategorizationService.TokenizeAndClean(tx.SearchableText));
        return cleanedText.Contains(pattern, StringComparison.OrdinalIgnoreCase);
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
