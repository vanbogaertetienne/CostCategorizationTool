using System.Text.RegularExpressions;
using CostCategorizationTool.Models;

namespace CostCategorizationTool.Services;

/// <summary>
/// Analyses transaction details to suggest the best categorization rule pattern
/// without requiring the user to write any regex or filter manually.
/// </summary>
public class IntelliCategorizationService
{
    // ── Noise words that carry no merchant-identity signal ───────────────────

    private static readonly HashSet<string> NoiseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // French payment vocabulary
        "PAIEMENT", "AVEC", "LA", "LE", "LES", "DE", "DU", "DES", "EN", "ET",
        "UN", "UNE", "PAR", "AU", "AUX", "SUR", "POUR", "DANS",
        "CARTE", "DEBIT", "CREDIT", "NUMERO", "DATE", "VALEUR", "REFERENCE",
        "BANQUE", "VIREMENT", "ORDRE", "COMMUNICATION", "MOTIF", "STATUT",
        "ACCEPTE", "ACCEPTÉ", "REFUS",
        // Card-scheme names
        "BANCONTACT", "MAESTRO", "VISA", "MASTERCARD", "PAYCONIQ",
        // Misc
        "NR", "REF", "BE", "N", "VIA", "SEPA", "MONSIEUR", "MADAME", "MR", "MME",
        "THE", "AND", "FOR",
        // Currency words (carry no merchant-identity signal)
        "EUR", "EUROS", "EURO"
    };

    // ── Noise patterns to strip before tokenisation ──────────────────────────

    private static readonly Regex[] NoisePatterns =
    {
        // Belgian card number mask: "4871 04XX XXXX 3606"
        new Regex(@"\b[0-9X]{4}(\s+[0-9X]{4}){3}\b", RegexOptions.IgnoreCase),
        // Dates dd/MM/yyyy or dd-MM-yyyy
        new Regex(@"\b\d{1,2}[/\-]\d{1,2}[/\-]\d{4}\b"),
        // Times HH:MM
        new Regex(@"\b\d{1,2}:\d{2}\b"),
        // Long reference numbers (10+ digits)
        new Regex(@"\b\d{10,}\b"),
        // Belgian postcodes and isolated 4-digit codes
        new Regex(@"\b\d{4}\b"),
    };

    // ── Minimum common tokens for two transactions to be considered "similar" ─
    private const int MinSimilarTokens = 2;

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Suggests the best (ruleType, pattern) pair for categorising <paramref name="newTx"/>.
    /// Uses other same-category transactions already assigned in this session to detect
    /// emerging patterns automatically.
    /// </summary>
    public (RuleType RuleType, string Pattern) SuggestRule(
        Transaction newTx,
        int categoryId,
        IEnumerable<Transaction> allSameCategoryTxs)
    {
        // IBAN is the most precise identifier – always prefer it.
        if (!string.IsNullOrWhiteSpace(newTx.Counterpart))
            return (RuleType.IBAN, newTx.Counterpart);

        var newTokens = TokenizeAndClean(newTx.SearchableText);
        if (newTokens.Count == 0)
            return (RuleType.Details, newTx.SearchableText);

        // Gather same-category transactions whose Details are "similar" to this one
        // (share at least MinSimilarTokens cleaned tokens).  This prevents unrelated
        // merchants assigned to the same category from polluting each other's pattern.
        var similarTokenSets = allSameCategoryTxs
            .Where(t => t != newTx && !string.IsNullOrWhiteSpace(t.SearchableText))
            .Select(t => TokenizeAndClean(t.SearchableText))
            .Where(tokens => tokens.Intersect(newTokens, StringComparer.OrdinalIgnoreCase).Count() >= MinSimilarTokens)
            .ToList();

        if (similarTokenSets.Count == 0)
        {
            // Only one data point – extract the most distinctive phrase from this transaction.
            return (RuleType.Details, PhraseFromTokens(newTokens));
        }

        // Multiple similar transactions → find the longest word sequence they all share.
        similarTokenSets.Add(newTokens);
        var common = FindCommonWordSequence(similarTokenSets);

        return !string.IsNullOrEmpty(common)
            ? (RuleType.Details, common)
            : (RuleType.Details, PhraseFromTokens(newTokens));
    }

    /// <summary>
    /// Returns a ranked list of Details-based pattern candidates for the user to choose from.
    /// The first item is the best suggestion (LCS across similar transactions if available,
    /// otherwise the longest cleaned phrase from this transaction).
    /// Subsequent items are shorter sub-phrases and individual tokens, giving the user
    /// several specificity levels to pick from.
    /// </summary>
    public List<string> SuggestDetailsPatterns(Transaction tx, IEnumerable<Transaction> allSameCategoryTxs)
    {
        var newTokens = TokenizeAndClean(tx.SearchableText);
        if (newTokens.Count == 0)
            return new List<string> { tx.SearchableText };

        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        void Add(string s)
        {
            s = s.Trim();
            if (s.Length >= 3 && seen.Add(s))
                result.Add(s);
        }

        // 1. LCS with similar transactions — most "verified" pattern, shown first.
        var similarTokenSets = allSameCategoryTxs
            .Where(t => t != tx && !string.IsNullOrWhiteSpace(t.SearchableText))
            .Select(t => TokenizeAndClean(t.SearchableText))
            .Where(tokens => tokens.Intersect(newTokens, StringComparer.OrdinalIgnoreCase).Count() >= MinSimilarTokens)
            .ToList();

        if (similarTokenSets.Count > 0)
        {
            similarTokenSets.Add(newTokens);
            var lcs = FindCommonWordSequence(similarTokenSets);
            if (!string.IsNullOrWhiteSpace(lcs))
                Add(lcs);
        }

        // 2. Subphrases of the current transaction (longest → shortest).
        for (int len = Math.Min(newTokens.Count, 4); len >= 1; len--)
            Add(string.Join(" ", newTokens.Take(len)));

        // 3. Any remaining individual tokens not yet covered.
        foreach (var token in newTokens)
            Add(token);

        return result.Take(6).ToList();
    }

    /// <summary>
    /// Returns only the Details-based pattern, regardless of whether the transaction
    /// has a Counterpart IBAN. Used to pre-populate the dialog shown to the user.
    /// </summary>
    public string SuggestDetailsPattern(Transaction tx, IEnumerable<Transaction> allSameCategoryTxs)
    {
        var patterns = SuggestDetailsPatterns(tx, allSameCategoryTxs);
        return patterns.Count > 0 ? patterns[0] : tx.SearchableText;
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private static string PhraseFromTokens(List<string> tokens)
        => string.Join(" ", tokens.Take(4));

    /// <summary>
    /// Returns the LCS-based common pattern across token sets, or a fallback phrase
    /// if the transaction list is provided and no LCS is found.
    /// </summary>
    public static string FindCommonPattern(List<List<string>> tokenSets, IEnumerable<Transaction>? txFallback = null)
    {
        var nonEmpty = tokenSets.Where(s => s.Count > 0).ToList();
        if (nonEmpty.Count == 0)
        {
            var first = txFallback?.FirstOrDefault();
            return first?.SearchableText ?? "";
        }
        if (nonEmpty.Count == 1)
            return string.Join(" ", nonEmpty[0].Take(4));

        var lcs = FindCommonWordSequence(nonEmpty);
        return !string.IsNullOrWhiteSpace(lcs) ? lcs : string.Join(" ", nonEmpty[0].Take(3));
    }

    private static string FindCommonWordSequence(List<List<string>> tokenSets)
    {
        var common = tokenSets[0];
        for (int i = 1; i < tokenSets.Count && common.Count > 0; i++)
            common = LongestCommonSubarray(common, tokenSets[i]);
        return string.Join(" ", common);
    }

    /// <summary>Longest contiguous common sub-array of words (case-insensitive).</summary>
    private static List<string> LongestCommonSubarray(List<string> a, List<string> b)
    {
        int bestLen = 0, bestStart = 0;

        for (int i = 0; i < a.Count; i++)
        {
            for (int j = 0; j < b.Count; j++)
            {
                int len = 0;
                while (i + len < a.Count && j + len < b.Count &&
                       string.Equals(a[i + len], b[j + len], StringComparison.OrdinalIgnoreCase))
                    len++;

                if (len > bestLen) { bestLen = len; bestStart = i; }
            }
        }

        return bestLen > 0 ? a.GetRange(bestStart, bestLen) : new List<string>();
    }

    public static List<string> TokenizeAndClean(string details)
    {
        var text = details.ToUpperInvariant();

        // Strip noise patterns
        foreach (var rx in NoisePatterns)
            text = rx.Replace(text, " ");

        // Tokenise
        var words = text
            .Split(new[] { ' ', '\t', '\n', '\r', ':', '.', ',', ';', '/', '\\', '(', ')' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2)
            .Where(w => !NoiseWords.Contains(w))
            .Where(w => !w.All(char.IsDigit))
            .Where(w => !w.All(c => c == 'X'))
            .ToList();

        // Remove consecutive duplicates (e.g. "GRIMBERGEN GRIMBERGEN" → "GRIMBERGEN")
        var deduped = new List<string>();
        for (int i = 0; i < words.Count; i++)
            if (i == 0 || !string.Equals(words[i], words[i - 1], StringComparison.OrdinalIgnoreCase))
                deduped.Add(words[i]);

        return deduped;
    }
}
