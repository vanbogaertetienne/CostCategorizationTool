using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CostCategorizationTool.Models;

namespace CostCategorizationTool.Services;

/// <summary>
/// Auto-detecting CSV parser for Belgian bank exports.
/// Handles BNP Paribas Fortis, ING, KBC, Belfius, Argenta, Triodos, Crelan
/// and similar formats in both French and Dutch, with semicolon, comma or tab
/// delimiters, quoted fields, UTF-8 / Windows-1252 / Latin-1 encodings.
/// </summary>
public class CsvParserService
{
    // Maps normalised column header text → Transaction property name.
    // First match per property wins, so more-specific entries must come first.
    private static readonly (string Header, string Field)[] HeaderMap =
    {
        // ── Execution date ────────────────────────────────────────────────────
        ("date d'exécution",            "ExecutionDate"),
        ("uitvoeringsdatum",            "ExecutionDate"),
        ("date de comptabilisation",    "ExecutionDate"),
        ("boekingsdatum",               "ExecutionDate"),
        ("datum",                       "ExecutionDate"),
        ("date",                        "ExecutionDate"),

        // ── Value date ────────────────────────────────────────────────────────
        ("date valeur",                 "ValueDate"),
        ("valutadatum",                 "ValueDate"),

        // ── Amount ────────────────────────────────────────────────────────────
        ("montant",                     "Amount"),
        ("bedrag (eur)",                "Amount"),
        ("bedrag (€)",                  "Amount"),
        ("bedrag eur",                  "Amount"),
        ("bedrag",                      "Amount"),
        ("amount",                      "Amount"),

        // ── Currency ──────────────────────────────────────────────────────────
        ("devise",                      "Currency"),
        ("munt",                        "Currency"),
        ("valuta",                      "Currency"),
        ("currency",                    "Currency"),

        // ── Own account ───────────────────────────────────────────────────────
        ("numéro de compte",            "AccountNumber"),
        ("compte",                      "AccountNumber"),
        ("nom du compte",               "AccountNumber"),   // Belfius own-account name
        ("naam rekening",               "AccountNumber"),   // KBC own-account name
        ("rekening",                    "AccountNumber"),

        // ── Counterpart IBAN / BIC ────────────────────────────────────────────
        // More-specific names first so "nom de la contrepartie" doesn't match here
        ("compte contrepartie",         "Counterpart"),   // Belfius
        ("rekeningnummer tegenpartij",  "Counterpart"),
        ("tegenrekeningnummer",         "Counterpart"),
        ("contrepartie",                "Counterpart"),
        ("tegenpartij",                 "Counterpart"),
        ("rekeningnummer",              "Counterpart"),   // ING: counterpart account
        ("bic",                         "Counterpart"),

        // ── Counterpart name ──────────────────────────────────────────────────
        ("nom contrepartie contient",   "CounterpartName"),  // Belfius
        ("nom de la contrepartie",      "CounterpartName"),
        ("naam van de tegenpartij",     "CounterpartName"),
        ("naam tegenrekening",          "CounterpartName"),
        ("naam",                        "CounterpartName"),

        // ── Communication / payment reference ─────────────────────────────────
        ("communications",              "Communication"),  // Belfius (plural)
        ("communication",               "Communication"),
        ("mededeling",                  "Communication"),
        ("référence de contrepartie",   "Communication"),
        ("reden",                       "Communication"),  // KBC

        // ── Details / transaction description ─────────────────────────────────
        ("omschrijving van de handeling", "Details"),
        ("beschrijving",                "Details"),
        ("omschrijving",                "Details"),
        ("détails",                     "Details"),
        ("details",                     "Details"),
        ("transaction",                 "Details"),
        ("description",                 "Details"),

        // ── Transaction type ──────────────────────────────────────────────────
        ("type de transaction",         "TransactionType"),
        ("transactietype",              "TransactionType"),
        ("rekeningtype",                "TransactionType"),

        // ── Sequence / reference number ───────────────────────────────────────
        ("numéro de séquence",          "SequenceNumber"),
        ("volgnummer",                  "SequenceNumber"),
        ("numéro d'extrait",            "SequenceNumber"),
        ("numéro de transaction",       "SequenceNumber"),
        ("referentie instelling",       "SequenceNumber"),
        ("referentie",                  "SequenceNumber"),

        // ── Status ────────────────────────────────────────────────────────────
        ("statut",                      "Status"),
        ("status",                      "Status"),
    };

    // ── Public API ─────────────────────────────────────────────────────────────

    public List<Transaction> ParseFile(string filePath)
    {
        var lines = ReadLines(filePath);
        if (lines.Length == 0) return new();

        int headerIdx  = FindHeaderRow(lines);
        char delimiter = DetectDelimiter(lines[headerIdx]);
        var  colMap    = BuildColumnMap(SplitLine(lines[headerIdx], delimiter));

        var transactions = new List<Transaction>();
        for (int i = headerIdx + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var tx = TryParseRow(SplitLine(line, delimiter), colMap);
            if (tx != null) transactions.Add(tx);
        }
        return transactions;
    }

    // ── Encoding detection ─────────────────────────────────────────────────────

    private static string[] ReadLines(string filePath)
    {
        var encodings = new Encoding[]
        {
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: false),
            Encoding.UTF8,
            Encoding.GetEncoding(1252),          // Windows-1252 — common in Belgian exports
            Encoding.GetEncoding("iso-8859-1"),
        };

        foreach (var enc in encodings)
        {
            try
            {
                var lines = File.ReadAllLines(filePath, enc);
                // Reject if any replacement characters (bad decode) appear in early lines
                if (!lines.Take(5).Any(l => l.Contains('\uFFFD')))
                    return StripBom(lines);
            }
            catch { }
        }

        return StripBom(File.ReadAllLines(filePath, Encoding.GetEncoding("iso-8859-1")));
    }

    private static string[] StripBom(string[] lines)
    {
        if (lines.Length > 0 && lines[0].StartsWith('\uFEFF'))
            lines[0] = lines[0][1..];
        return lines;
    }

    // ── Header detection ───────────────────────────────────────────────────────

    private static int FindHeaderRow(string[] lines)
    {
        int best = 0, bestScore = -1;
        for (int i = 0; i < Math.Min(20, lines.Length); i++)
        {
            if (!lines[i].Contains(';') && !lines[i].Contains(',') && !lines[i].Contains('\t'))
                continue;
            char delim = DetectDelimiter(lines[i]);
            int score  = SplitLine(lines[i], delim).Count(p => LooksLikeHeader(p));
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    private static bool LooksLikeHeader(string cell) =>
        HeaderMap.Any(e => e.Header.Equals(Normalise(cell), StringComparison.OrdinalIgnoreCase));

    // ── Column mapping ─────────────────────────────────────────────────────────

    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            string norm = Normalise(headers[i]);
            foreach (var (header, field) in HeaderMap)
            {
                if (header.Equals(norm, StringComparison.OrdinalIgnoreCase) &&
                    !map.ContainsKey(field))
                {
                    map[field] = i;
                    break;
                }
            }
        }
        return map;
    }

    // ── Row parsing ────────────────────────────────────────────────────────────

    private static Transaction? TryParseRow(string[] fields, Dictionary<string, int> colMap)
    {
        var date = ParseDate(Get(fields, colMap, "ExecutionDate"));
        if (date == DateTime.MinValue) return null;   // skip summary/empty rows

        var tx = new Transaction
        {
            ExecutionDate   = date,
            ValueDate       = ParseDate(Get(fields, colMap, "ValueDate")),
            Amount          = ParseAmount(Get(fields, colMap, "Amount")),
            Currency        = Fallback(Get(fields, colMap, "Currency"), "EUR"),
            AccountNumber   = Get(fields, colMap, "AccountNumber"),
            TransactionType = Get(fields, colMap, "TransactionType"),
            Counterpart     = Get(fields, colMap, "Counterpart"),
            CounterpartName = Get(fields, colMap, "CounterpartName"),
            Communication   = Get(fields, colMap, "Communication"),
            Details         = Get(fields, colMap, "Details"),
            SequenceNumber  = Get(fields, colMap, "SequenceNumber"),
            Status          = Get(fields, colMap, "Status"),
        };

        // Ensure Details contains something useful for keyword categorisation
        if (string.IsNullOrWhiteSpace(tx.Details))
            tx.Details = Fallback(tx.Communication, tx.CounterpartName);

        return tx;
    }

    // ── Delimiter detection ────────────────────────────────────────────────────

    private static char DetectDelimiter(string line)
    {
        int sc = line.Count(c => c == ';');
        int cc = line.Count(c => c == ',');
        int tc = line.Count(c => c == '\t');
        if (sc >= cc && sc >= tc) return ';';
        if (tc >= cc) return '\t';
        return ',';
    }

    // ── RFC 4180 CSV field splitter ────────────────────────────────────────────

    private static string[] SplitLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb     = new StringBuilder();
        bool inQ   = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQ)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                { sb.Append('"'); i++; }                 // escaped quote ""
                else if (c == '"') inQ = false;
                else sb.Append(c);
            }
            else
            {
                if      (c == '"')       inQ = true;
                else if (c == delimiter) { fields.Add(sb.ToString()); sb.Clear(); }
                else                     sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    // ── Date parsing ───────────────────────────────────────────────────────────

    private static readonly string[] DateFormats =
    {
        "dd/MM/yyyy", "d/M/yyyy",
        "dd-MM-yyyy", "d-M-yyyy",
        "dd.MM.yyyy", "d.M.yyyy",
        "yyyy-MM-dd", "yyyy/MM/dd",
        "dd/MM/yy",   "d/M/yy",
    };

    private static DateTime ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTime.MinValue;
        value = value.Trim();
        if (DateTime.TryParseExact(value, DateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
            return dt;
        return DateTime.MinValue;
    }

    // ── Amount parsing ─────────────────────────────────────────────────────────

    private static decimal ParseAmount(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;

        // Normalise special characters
        value = value
            .Replace('\u2212', '-')   // Unicode minus sign → hyphen-minus
            .Replace('\u00A0', ' ')   // non-breaking space → space
            .Trim();

        // Strip currency symbols and whitespace-as-thousands-separator
        value = Regex.Replace(value, @"[€$£\s]", "");

        if (string.IsNullOrEmpty(value)) return 0m;

        int lastDot   = value.LastIndexOf('.');
        int lastComma = value.LastIndexOf(',');

        string normalised;
        if (lastDot > lastComma)
            // Dot is decimal separator (1,234.56 or 1234.56)
            normalised = value.Replace(",", "");
        else if (lastComma > lastDot)
            // Comma is decimal separator (1.234,56 or 1234,56)
            normalised = value.Replace(".", "").Replace(",", ".");
        else
            normalised = value;   // no ambiguity

        return decimal.TryParse(normalised, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out var result)
            ? result : 0m;
    }

    // ── Utilities ──────────────────────────────────────────────────────────────

    private static string Get(string[] fields, Dictionary<string, int> map, string field) =>
        map.TryGetValue(field, out int idx) && idx < fields.Length
            ? fields[idx].Trim()
            : "";

    private static string Normalise(string s) =>
        s.Trim().Trim('"').Trim().ToLowerInvariant();

    private static string Fallback(string primary, string secondary) =>
        !string.IsNullOrWhiteSpace(primary) ? primary : secondary;
}
