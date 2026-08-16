// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.Data;

/// <summary>資料表欄位結構</summary>
public class ColumnInfo
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public bool NotNull { get; init; }
    public bool IsPrimaryKey { get; init; }
    public string? DefaultValue { get; init; }

    /// <summary>SQLite 型別 → .NET 型別</summary>
    public Type ClrType
    {
        get
        {
            var t = Type.ToUpperInvariant();
            if (t.Contains("INT")) return typeof(long);
            if (t.Contains("REAL") || t.Contains("FLOA") || t.Contains("DOUB"))
                return typeof(double);
            if (t.Contains("BLOB")) return typeof(byte[]);
            if (t.Contains("TEXT") || t.Contains("CHAR") || t.Contains("CLOB"))
                return typeof(string);
            if (t.Contains("DATE") || t.Contains("TIME")) return typeof(string);
            return typeof(string);
        }
    }
}

/// <summary>資料表結構</summary>
public class TableInfo
{
    public string Name { get; init; } = "";
    public List<ColumnInfo> Columns { get; init; } = new();
    public List<string> PrimaryKey { get; init; } = new();
}

/// <summary>
/// 讀取 SQLite 全部資料表結構並快取，供動態產生資料維護畫面使用。
/// </summary>
public static class SchemaReader
{
    private static Dictionary<string, TableInfo>? _cache;
    private static readonly object _lock = new();

    /// <summary>取得全部資料表結構（含快取）</summary>
    public static IReadOnlyDictionary<string, TableInfo> GetTables()
    {
        if (_cache is not null) return _cache;
        lock (_lock)
        {
            if (_cache is not null) return _cache;
            _cache = LoadSchema();
            return _cache;
        }
    }

    /// <summary>取得單一資料表結構（不存在回傳 null）</summary>
    public static TableInfo? GetTable(string tableName) =>
        GetTables().TryGetValue(tableName, out var t) ? t : null;

    /// <summary>取得資料表主鍵欄位（無主鍵回傳空清單）</summary>
    public static IReadOnlyList<string> GetPrimaryKey(string tableName) =>
        GetTable(tableName)?.PrimaryKey ?? new List<string>();

    /// <summary>重新載入（資料庫結構變更後呼叫）</summary>
    public static void Reload()
    {
        lock (_lock)
        {
            _cache = LoadSchema();
        }
    }

    private static Dictionary<string, TableInfo> LoadSchema()
    {
        var result = new Dictionary<string, TableInfo>(StringComparer.OrdinalIgnoreCase);
        using var conn = DbManager.OpenConnection();

        // 取得全部表名（排除 SQLite 系統表）
        var tableNames = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT name FROM sqlite_master
                WHERE type='table' AND name NOT LIKE 'sqlite_%'
                ORDER BY name
                """;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                tableNames.Add(reader.GetString(0));
        }

        foreach (var name in tableNames)
        {
            var table = new TableInfo { Name = name };
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{name}\")";
            using var reader = cmd.ExecuteReader();
            var pkOrder = new List<(int Seq, string Col)>();
            while (reader.Read())
            {
                var col = new ColumnInfo
                {
                    Name = reader.GetString(1),
                    Type = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    NotNull = reader.GetInt32(3) == 1,
                    DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                };
                var pkSeq = reader.GetInt32(5);
                if (pkSeq > 0)
                    pkOrder.Add((pkSeq, col.Name));
                table.Columns.Add(col);
            }
            foreach (var (_, c) in pkOrder.OrderBy(x => x.Seq))
            {
                table.PrimaryKey.Add(c);
                var target = table.Columns.First(x => x.Name == c);
                table.Columns[table.Columns.IndexOf(target)] = new ColumnInfo
                {
                    Name = target.Name,
                    Type = target.Type,
                    NotNull = target.NotNull,
                    IsPrimaryKey = true,
                    DefaultValue = target.DefaultValue,
                };
            }

            // 後備主鍵：表未定義 PRIMARY KEY 時，若第一欄資料唯一，以第一欄作為邏輯主鍵，
            // 讓動態維護表單的 CRUD 定位（KeyOf/WHERE 條件）得以運作。
            if (table.PrimaryKey.Count == 0 && table.Columns.Count > 0)
            {
                var candidate = table.Columns[0].Name;
                using var chkCmd = conn.CreateCommand();
                chkCmd.CommandText =
                    $"SELECT COUNT(*) FROM (SELECT \"{candidate}\" FROM \"{name}\" GROUP BY \"{candidate}\" HAVING COUNT(*) > 1)";
                try
                {
                    if (Convert.ToInt64(chkCmd.ExecuteScalar()) == 0)
                    {
                        table.PrimaryKey.Add(candidate);
                        var target = table.Columns[0];
                        table.Columns[0] = new ColumnInfo
                        {
                            Name = target.Name,
                            Type = target.Type,
                            NotNull = target.NotNull,
                            IsPrimaryKey = true,
                            DefaultValue = target.DefaultValue,
                        };
                    }
                }
                catch (SqliteException)
                {
                    // 查詢失敗（例如無法分組的欄位型別）時放棄後備
                }
            }
            result[name] = table;
        }
        return result;
    }
}
