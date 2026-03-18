using Microsoft.Data.Sqlite;
using CostCategorizationTool.Models;

namespace CostCategorizationTool.Data;

public class AppDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public AppDatabase(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS categories (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS category_rules (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                category_id INTEGER NOT NULL REFERENCES categories(id) ON DELETE CASCADE,
                rule_type   INTEGER NOT NULL,
                pattern     TEXT NOT NULL,
                is_auto     INTEGER NOT NULL DEFAULT 0
            );
        ";
        cmd.ExecuteNonQuery();

        // Migration: add is_auto column to existing databases
        try
        {
            cmd.CommandText = "ALTER TABLE category_rules ADD COLUMN is_auto INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
        catch { /* column already exists */ }

        try
        {
            cmd.CommandText = "ALTER TABLE category_rules ADD COLUMN amount_sign INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
        catch { /* column already exists */ }

        // Seed default categories if none exist
        cmd.CommandText = "SELECT COUNT(*) FROM categories;";
        var count = (long)(cmd.ExecuteScalar() ?? 0L);
        if (count == 0) SeedDefaultCategories();
    }

    private void SeedDefaultCategories()
    {
        var defaults = new[]
        {
            "Food & Groceries", "Transport", "Leisure", "Health",
            "Travel", "Utilities", "Rent", "Shopping", "Car", "Other"
        };
        foreach (var name in defaults)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO categories (name) VALUES (@name);";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Wipes all categories and rules, then restores the default categories.</summary>
    public void ResetDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM category_rules;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DELETE FROM categories;";
        cmd.ExecuteNonQuery();
        SeedDefaultCategories();
    }

    // ── Categories ──────────────────────────────────────────────────────────

    public List<Category> GetCategories()
    {
        var list = new List<Category>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM categories ORDER BY name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new Category { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        return list;
    }

    public void AddCategory(string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO categories (name) VALUES (@name);";
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.ExecuteNonQuery();
    }

    public void UpdateCategory(int id, string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE categories SET name = @name WHERE id = @id;";
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCategory(int id)
    {
        using var cmd = _connection.CreateCommand();
        // CASCADE will delete associated rules
        cmd.CommandText = "DELETE FROM categories WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // ── Rules ────────────────────────────────────────────────────────────────

    public List<CategoryRule> GetRules()
    {
        var list = new List<CategoryRule>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, category_id, rule_type, pattern, is_auto, amount_sign FROM category_rules;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new CategoryRule
            {
                Id         = reader.GetInt32(0),
                CategoryId = reader.GetInt32(1),
                RuleType   = (RuleType)reader.GetInt32(2),
                Pattern    = reader.GetString(3),
                IsAuto     = reader.GetInt32(4) != 0,
                AmountSign = (AmountSign)reader.GetInt32(5)
            });
        return list;
    }

    public List<CategoryRule> GetRulesForCategory(int categoryId)
    {
        var list = new List<CategoryRule>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, category_id, rule_type, pattern, is_auto, amount_sign FROM category_rules WHERE category_id = @cid;";
        cmd.Parameters.AddWithValue("@cid", categoryId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new CategoryRule
            {
                Id         = reader.GetInt32(0),
                CategoryId = reader.GetInt32(1),
                RuleType   = (RuleType)reader.GetInt32(2),
                Pattern    = reader.GetString(3),
                IsAuto     = reader.GetInt32(4) != 0,
                AmountSign = (AmountSign)reader.GetInt32(5)
            });
        return list;
    }

    /// <summary>
    /// Called by the intelligent auto-categorizer when the user assigns a category to a transaction.
    /// For IBAN rules: upserts (creates or updates the category of an existing IBAN rule).
    /// For Details rules: inserts if a rule with the same pattern does not already exist.
    /// </summary>
    public void AddAutoRule(int categoryId, RuleType ruleType, string pattern, AmountSign amountSign = AmountSign.Any)
    {
        pattern = pattern.Trim();
        if (string.IsNullOrEmpty(pattern)) return;

        if (ruleType == RuleType.IBAN)
        {
            // Each IBAN should point to exactly one category; upsert.
            using var checkCmd = _connection.CreateCommand();
            checkCmd.CommandText = "SELECT id FROM category_rules WHERE rule_type = 0 AND LOWER(pattern) = LOWER(@pat) AND amount_sign = @sign LIMIT 1;";
            checkCmd.Parameters.AddWithValue("@pat", pattern);
            checkCmd.Parameters.AddWithValue("@sign", (int)amountSign);
            var existingId = checkCmd.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                using var updCmd = _connection.CreateCommand();
                updCmd.CommandText = "UPDATE category_rules SET category_id = @cid, amount_sign = @sign WHERE id = @id;";
                updCmd.Parameters.AddWithValue("@cid", categoryId);
                updCmd.Parameters.AddWithValue("@sign", (int)amountSign);
                updCmd.Parameters.AddWithValue("@id", (long)existingId);
                updCmd.ExecuteNonQuery();
            }
            else
            {
                using var insCmd = _connection.CreateCommand();
                insCmd.CommandText = "INSERT INTO category_rules (category_id, rule_type, pattern, is_auto, amount_sign) VALUES (@cid, 0, @pat, 1, @sign);";
                insCmd.Parameters.AddWithValue("@cid", categoryId);
                insCmd.Parameters.AddWithValue("@pat", pattern);
                insCmd.Parameters.AddWithValue("@sign", (int)amountSign);
                insCmd.ExecuteNonQuery();
            }
            return;
        }

        // Details rule: only insert if no rule with the same pattern already exists (any category).
        using var dupCheck = _connection.CreateCommand();
        dupCheck.CommandText = "SELECT COUNT(*) FROM category_rules WHERE rule_type = 1 AND LOWER(pattern) = LOWER(@pat) AND amount_sign = @sign;";
        dupCheck.Parameters.AddWithValue("@pat", pattern);
        dupCheck.Parameters.AddWithValue("@sign", (int)amountSign);
        if ((long)(dupCheck.ExecuteScalar() ?? 0L) > 0) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO category_rules (category_id, rule_type, pattern, is_auto, amount_sign) VALUES (@cid, 1, @pat, 1, @sign);";
        cmd.Parameters.AddWithValue("@cid", categoryId);
        cmd.Parameters.AddWithValue("@pat", pattern);
        cmd.Parameters.AddWithValue("@sign", (int)amountSign);
        cmd.ExecuteNonQuery();
    }

    public void AddRule(int categoryId, RuleType ruleType, string pattern, AmountSign amountSign = AmountSign.Any)
    {
        // Skip duplicates
        using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = @"
            SELECT COUNT(*) FROM category_rules
            WHERE category_id = @cid AND rule_type = @rt AND LOWER(pattern) = LOWER(@pat) AND amount_sign = @sign;";
        checkCmd.Parameters.AddWithValue("@cid", categoryId);
        checkCmd.Parameters.AddWithValue("@rt", (int)ruleType);
        checkCmd.Parameters.AddWithValue("@pat", pattern.Trim());
        checkCmd.Parameters.AddWithValue("@sign", (int)amountSign);
        var exists = (long)(checkCmd.ExecuteScalar() ?? 0L);
        if (exists > 0) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO category_rules (category_id, rule_type, pattern, is_auto, amount_sign)
            VALUES (@cid, @rt, @pat, 0, @sign);";
        cmd.Parameters.AddWithValue("@cid", categoryId);
        cmd.Parameters.AddWithValue("@rt", (int)ruleType);
        cmd.Parameters.AddWithValue("@pat", pattern.Trim());
        cmd.Parameters.AddWithValue("@sign", (int)amountSign);
        cmd.ExecuteNonQuery();
    }

    public void DeleteRule(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM category_rules WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
