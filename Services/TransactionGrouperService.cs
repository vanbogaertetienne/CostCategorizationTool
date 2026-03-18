using CostCategorizationTool.Models;

namespace CostCategorizationTool.Services;

/// <summary>
/// Clusters transactions into recurring groups using IBAN identity (exact match)
/// and description similarity (token-based union-find).  Each group becomes one
/// row the user can assign a single category to.
/// </summary>
public class TransactionGrouperService
{
    private const int MinCommonTokens = 2;

    /// <summary>
    /// Returns groups sorted by transaction count descending (most recurring first).
    /// Groups with mixed signs (e.g. an IBAN used both for salary and for charges)
    /// are automatically split into a positive-only and negative-only subgroup so
    /// the user can assign different categories to each direction.
    /// </summary>
    public List<TransactionGroup> Group(IEnumerable<Transaction> transactions)
    {
        var all    = transactions.ToList();
        var result = new List<TransactionGroup>();

        // ── Step 1: group by counterpart IBAN ────────────────────────────────
        var withIban    = all.Where(t => !string.IsNullOrWhiteSpace(t.Counterpart)).ToList();
        var noIban      = all.Where(t =>  string.IsNullOrWhiteSpace(t.Counterpart)).ToList();

        foreach (var g in withIban.GroupBy(t => t.Counterpart.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            string displayName = BuildIbanDisplayName(g.ToList(), g.Key);
            result.AddRange(SplitBySign(g.ToList(), RuleType.IBAN, g.Key, displayName));
        }

        // ── Step 2: cluster remaining by description tokens ───────────────────
        result.AddRange(ClusterByDescription(noIban));

        // Most recurring first, then by absolute total descending.
        result.Sort((a, b) =>
        {
            int byCount = b.Count.CompareTo(a.Count);
            return byCount != 0 ? byCount : Math.Abs(b.Total).CompareTo(Math.Abs(a.Total));
        });

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildIbanDisplayName(List<Transaction> txs, string iban)
    {
        var name = txs.Select(t => t.CounterpartName)
                      .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return string.IsNullOrWhiteSpace(name) ? iban : $"{name}";
    }

    /// <summary>
    /// If all transactions share the same sign → one group.
    /// If mixed → split into a positive subgroup and a negative subgroup.
    /// </summary>
    private static IEnumerable<TransactionGroup> SplitBySign(
        List<Transaction> txs, RuleType ruleType, string pattern, string displayName)
    {
        bool hasPos = txs.Any(t => t.Amount > 0);
        bool hasNeg = txs.Any(t => t.Amount < 0);

        if (hasPos && hasNeg)
        {
            var pos = txs.Where(t => t.Amount > 0).ToList();
            var neg = txs.Where(t => t.Amount < 0).ToList();
            if (pos.Count > 0)
                yield return Make(ruleType, pattern, displayName, AmountSign.Positive, pos);
            if (neg.Count > 0)
                yield return Make(ruleType, pattern, displayName, AmountSign.Negative, neg);
        }
        else
        {
            yield return Make(ruleType, pattern, displayName,
                hasPos ? AmountSign.Positive : AmountSign.Negative, txs);
        }
    }

    private static TransactionGroup Make(
        RuleType ruleType, string pattern, string displayName,
        AmountSign sign, List<Transaction> txs) => new()
    {
        RuleType     = ruleType,
        Pattern      = pattern,
        DisplayName  = displayName,
        DetectedSign = sign,
        Transactions = txs
    };

    // ── Description clustering ────────────────────────────────────────────────

    private static List<TransactionGroup> ClusterByDescription(List<Transaction> transactions)
    {
        if (transactions.Count == 0) return new();

        var tokenSets = transactions
            .Select(t => IntelliCategorizationService.TokenizeAndClean(t.Details))
            .ToList();

        // Union-find
        var parent = Enumerable.Range(0, transactions.Count).ToArray();
        for (int i = 0; i < transactions.Count; i++)
        {
            if (tokenSets[i].Count == 0) continue;
            for (int j = i + 1; j < transactions.Count; j++)
            {
                if (tokenSets[j].Count == 0) continue;
                int common = tokenSets[i]
                    .Intersect(tokenSets[j], StringComparer.OrdinalIgnoreCase)
                    .Count();
                if (common >= MinCommonTokens)
                    Union(parent, i, j);
            }
        }

        // Collect connected components
        var components = new Dictionary<int, List<int>>();
        for (int i = 0; i < transactions.Count; i++)
        {
            int root = Find(parent, i);
            if (!components.TryGetValue(root, out var list))
                components[root] = list = new();
            list.Add(i);
        }

        var result = new List<TransactionGroup>();
        foreach (var indices in components.Values)
        {
            var txs     = indices.Select(i => transactions[i]).ToList();
            var sets    = indices.Select(i => tokenSets[i]).ToList();
            var pattern = IntelliCategorizationService.FindCommonPattern(sets, txs);
            result.AddRange(SplitBySign(txs, RuleType.Details, pattern, pattern));
        }
        return result;
    }

    private static int  Find(int[] p, int i) => p[i] == i ? i : p[i] = Find(p, p[i]);
    private static void Union(int[] p, int i, int j) => p[Find(p, i)] = Find(p, j);
}
