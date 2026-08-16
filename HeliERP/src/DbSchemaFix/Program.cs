using System.Text;
using System.Text.RegularExpressions;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

Console.OutputEncoding = Encoding.UTF8;

var apply = args.Contains("--apply");
var backup = !args.Contains("--no-backup");
var only = ParseOnly(args);
var explicitPk = ParsePk(args);
var verifyPath = ParseVerify(args);
var dbPath = ResolveDbPath(args);
if (!File.Exists(dbPath))
{
    Console.WriteLine($"找不到資料庫：{dbPath}");
    Console.WriteLine("用法：DbSchemaFix [--apply] [--db <資料庫路徑>] [--only <表名>] [--pk \"欄位1,欄位2\"] [--verify <基準庫>]");
    Console.WriteLine("未指定 --db 時依序尋找：HeliERP.config.json → D:\\HeliAcc\\HeliERP.db");
    return 1;
}
DbManager.DatabasePath = dbPath;
Console.WriteLine($"資料庫：{dbPath}");
if (only is not null) Console.WriteLine($"僅處理表：{only}");
if (explicitPk is not null) Console.WriteLine($"指定主鍵：{string.Join(", ", explicitPk)}");

var tables = LoadTables(dbPath);
if (only is not null)
    tables = tables.Where(t => t.Name == only).ToList();
foreach (var t in tables.Where(t => t.PkColumns.Count == 0 && explicitPk is not null))
{
    t.ExplicitPk = explicitPk;
    t.SafeToFix = true;
}
if (verifyPath is not null)
    return Verify(tables, verifyPath);
if (apply)
    return ApplyFix(dbPath, tables, only, backup);

Report(tables);
WriteScript(dbPath, tables, only);
Console.WriteLine("\n=== 產出 DbSchemaFix.sql（重建無主鍵表的腳本，僅安全組） ===");
Console.WriteLine("執行方式：DbSchemaFix --apply（會先備份資料庫再套用）");
return 0;

static List<TableDiag> LoadTables(string dbPath)
{
    var tables = new List<TableDiag>();
    using var conn = DbManager.OpenConnection();
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var r = cmd.ExecuteReader();
        while (r.Read()) tables.Add(new TableDiag { Name = r.GetString(0) });
    }
    foreach (var t in tables)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name=$n";
        cmd.Parameters.Add(DbManager.Param("$n", t.Name));
        t.CreateSql = (cmd.ExecuteScalar() as string) ?? "";
    }
    foreach (var t in tables)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{t.Name}\")";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var col = new ColumnDiag
            {
                Name = r.GetString(1),
                Type = r.IsDBNull(2) ? "" : r.GetString(2),
                PkSeq = r.GetInt32(5),
            };
            t.Columns.Add(col);
            if (col.PkSeq > 0) t.PkColumns.Add(col);
        }
    }
    foreach (var t in tables)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA index_list(\"{t.Name}\")";
        using var r = cmd.ExecuteReader();
        var idxs = new List<(string Name, bool Unique, string Origin)>();
        while (r.Read()) idxs.Add((r.GetString(1), r.GetInt32(2) == 1, r.GetString(3)));
        foreach (var (name, unique, origin) in idxs)
        {
            var idx = new IndexDiag { Name = name, Unique = unique, Origin = origin };
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = $"PRAGMA index_info(\"{name}\")";
            using var r2 = cmd2.ExecuteReader();
            while (r2.Read()) idx.Columns.Add(r2.GetString(2));
            t.Indexes.Add(idx);
        }
    }
    foreach (var t in tables)
    {
        if (t.PkColumns.Count > 0) continue;
        var first = t.Columns.FirstOrDefault();
        if (first is null) continue;
        t.CandidatePk = first.Name;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM (SELECT \"{first.Name}\" FROM \"{t.Name}\" GROUP BY \"{first.Name}\" HAVING COUNT(*) > 1)";
        try
        {
            t.FirstColumnUnique = Convert.ToInt64(cmd.ExecuteScalar()) == 0;
        }
        catch (SqliteException)
        {
            t.FirstColumnUnique = false;
        }
        t.CandidateHasUniqueIndex = t.Indexes.Any(i => i.Unique
            && i.Columns.Count == 1 && i.Columns[0] == first.Name);
        t.SafeToFix = t.FirstColumnUnique || t.CandidateHasUniqueIndex;
    }
    return tables;
}

static void Report(List<TableDiag> tables)
{
    Console.WriteLine("\n=== 主鍵 / 索引診斷 ===\n");
    var missing = tables.Where(t => t.PkColumns.Count == 0).ToList();
    foreach (var t in tables)
    {
        if (t.PkColumns.Count > 0)
        {
            var pk = string.Join(", ", t.PkColumns.OrderBy(c => c.PkSeq).Select(c => c.Name));
            Console.WriteLine($"[OK ] {t.Name}  主鍵: {pk}");
            continue;
        }
        var status = t.SafeToFix ? "可修復" : "需人工評估";
        Console.WriteLine($"[缺PK] {t.Name}  ({status})");
        Console.WriteLine($"        欄位: {string.Join(", ", t.Columns.Select(c => $"{c.Name}({c.Type})"))}");
        if (t.ExplicitPk is not null)
            Console.WriteLine($"        指定主鍵: {string.Join(", ", t.ExplicitPk)}");
        else
        {
            Console.WriteLine($"        第一欄候選: {t.CandidatePk} ({t.Columns.First().Type})");
            Console.WriteLine($"        資料唯一: {(t.FirstColumnUnique ? "是" : "否")}  唯一索引: {(t.CandidateHasUniqueIndex ? "是" : "否")}");
        }
        foreach (var i in t.Indexes.Where(i => !i.Name.StartsWith("sqlite_autoindex_")))
            Console.WriteLine($"        索引 {i.Name} unique={(i.Unique ? 1 : 0)} [{string.Join(", ", i.Columns)}] ({OriginText(i.Origin)})");
    }
    var okCount = tables.Count - missing.Count;
    Console.WriteLine($"\n=== 共 {tables.Count} 張表：有主鍵 {okCount}，缺主鍵 {missing.Count}（安全可修復 {missing.Count(t => t.SafeToFix)}） ===");
}

static void WriteScript(string dbPath, List<TableDiag> tables, string? only)
{
    var sb = new StringBuilder();
    sb.AppendLine("-- 由 DbSchemaFix 產出：為缺少 PRIMARY KEY 的表重建（保留資料與索引）");
    sb.AppendLine($"-- 產生時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    sb.AppendLine("PRAGMA foreign_keys=OFF;");
    sb.AppendLine("BEGIN TRANSACTION;");
    foreach (var t in tables.Where(t => t.PkColumns.Count == 0 && (t.SafeToFix || t.ExplicitPk is not null)))
    {
        var pkCols = t.ExplicitPk ?? new List<string> { t.CandidatePk! };
        sb.AppendLine();
        sb.AppendLine($"-- 表 {t.Name}：原無主鍵，指定主鍵 {string.Join(", ", pkCols)}");
        sb.AppendLine(BuildCreateSql(t.CreateSql, NewName(t.Name), pkCols) + ";");
        var cols = string.Join(", ", t.Columns.Select(c => $"\"{c.Name}\""));
        sb.AppendLine($"INSERT INTO \"{NewName(t.Name)}\" ({cols}) SELECT {cols} FROM \"{t.Name}\";");
        sb.AppendLine($"DROP TABLE \"{t.Name}\";");
        sb.AppendLine($"ALTER TABLE \"{NewName(t.Name)}\" RENAME TO \"{t.Name}\";");
        foreach (var i in t.Indexes.Where(i => i.Origin == "c"))
        {
            var sql = GetIndexSql(t.Name, i.Name);
            sql = Regex.Replace(sql, "(?is)(ON\\s+)(?:\"[^\"]+\"|[^\\s(]+)", m => $"{m.Groups[1].Value}\"{t.Name}\"");
            sb.AppendLine(sql + ";");
        }
    }
    sb.AppendLine("COMMIT;");
    sb.AppendLine("PRAGMA foreign_keys=ON;");
    var scriptPath = ScriptPath(dbPath, only);
    File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(true));
    Console.WriteLine($"\n已寫入: {scriptPath}");
}

static int ApplyFix(string dbPath, List<TableDiag> tables, string? only, bool backup)
{
    var targets = tables.Where(t => t.PkColumns.Count == 0 && (t.SafeToFix || t.ExplicitPk is not null)).ToList();
    if (targets.Count == 0)
    {
        Console.WriteLine("沒有需要修復的表。");
        return 0;
    }
    var scriptPath = ScriptPath(dbPath, only);
    if (!File.Exists(scriptPath))
        WriteScript(dbPath, tables, only);
    if (!File.Exists(scriptPath))
    {
        Console.WriteLine($"找不到腳本 {scriptPath}，中止。");
        return 1;
    }
    var bak = $"{dbPath}.bak-{DateTime.Now:yyyyMMdd-HHmmss}";
    if (backup)
    {
        File.Copy(dbPath, bak);
        Console.WriteLine($"已備份: {bak}");
    }
    else
    {
        Console.WriteLine("略過備份（--no-backup）。");
    }

    var content = File.ReadAllText(scriptPath, Encoding.UTF8).TrimStart('\uFEFF');
    var statements = string.Join('\n', content
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("--")))
        .Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim())
        .Where(s => s.Length > 0
            && !s.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase)
            && !s.StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase)
            && !s.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase));
    using var conn = DbManager.OpenConnection();
    using var tx = conn.BeginTransaction();
    try
    {
        foreach (var stmt in statements)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = stmt;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.Rollback();
        Console.WriteLine($"修復失敗，已復原: {ex.Message}");
        return 1;
    }
    Console.WriteLine($"修復完成，共重建 {targets.Count} 張表。");
    var after = LoadTables(dbPath);
    Report(after);
    return 0;
}

static string BuildCreateSql(string createSql, string newName, IEnumerable<string> pkColumns)
{
    var m = Regex.Match(createSql, @"(?is)^\s*CREATE\s+TABLE\s+(?:""[^""]+""|[^\s(]+)\s*");
    if (!m.Success) return createSql;
    var body = createSql[m.Length..].Trim();
    var last = body.LastIndexOf(')');
    if (last < 0) return createSql;
    var head = body[..last];
    var tail = body[last..];
    var pk = string.Join(", ", pkColumns.Select(c => $"\"{c}\""));
    return $"CREATE TABLE \"{newName}\" {head}, PRIMARY KEY ({pk}){tail}";
}

static string GetIndexSql(string tableName, string indexName)
{
    using var conn = DbManager.OpenConnection();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name=$n AND tbl_name=$t";
    cmd.Parameters.Add(DbManager.Param("$n", indexName));
    cmd.Parameters.Add(DbManager.Param("$t", tableName));
    return (cmd.ExecuteScalar() as string) ?? "";
}

static string NewName(string tableName) => $"__fix_{tableName}";

static string OriginText(string origin) => origin switch
{
    "pk" => "主鍵",
    "u" => "唯一約束",
    "c" => "自建索引",
    _ => origin,
};

static string ResolveDbPath(string[] args)
{
    var idx = Array.IndexOf(args, "--db");
    if (idx >= 0 && idx + 1 < args.Length)
        return args[idx + 1];
    var cfg = DbConfig.Load().DatabasePath;
    if (File.Exists(cfg))
        return cfg;
    return @"D:\HeliAcc\HeliERP.db";
}

static string? ParseOnly(string[] args)
{
    var idx = Array.IndexOf(args, "--only");
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

static List<string>? ParsePk(string[] args)
{
    var idx = Array.IndexOf(args, "--pk");
    if (idx < 0 || idx + 1 >= args.Length) return null;
    var cols = args[idx + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return cols.Length == 0 ? null : cols.ToList();
}

static string? ParseVerify(string[] args)
{
    var idx = Array.IndexOf(args, "--verify");
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

/// <summary>
/// 全庫資料完整性驗證：以基準庫（修復前備份）對比主庫，
/// 檢查筆數一致性、主鍵唯一性/NULL、索引完整性與 SQLite 內建 integrity_check。
/// </summary>
static int Verify(List<TableDiag> tables, string backupPath)
{
    Console.WriteLine("\n=== 全庫資料完整性驗證 ===\n");
    Console.WriteLine($"主庫: {DbManager.DatabasePath}");
    Console.WriteLine($"基準: {backupPath}\n");
    if (!File.Exists(backupPath))
    {
        Console.WriteLine($"找不到基準庫：{backupPath}");
        return 1;
    }

    var failures = 0;
    var warnings = 0;

    using (var conn = DbManager.OpenConnection())
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "PRAGMA integrity_check";
        var r = cmd.ExecuteScalar()?.ToString();
        if (r == "ok")
            Console.WriteLine($"OK   integrity_check: ok");
        else
        {
            Console.WriteLine($"FAIL integrity_check: {r}");
            failures++;
        }
    }

    using var refConn = new SqliteConnection($"Data Source={backupPath}");
    refConn.Open();
    var refNames = new List<string>();
    using (var cmd = refConn.CreateCommand())
    {
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var r = cmd.ExecuteReader();
        while (r.Read()) refNames.Add(r.GetString(0));
    }
    var mainNames = tables.Select(t => t.Name).OrderBy(n => n).ToList();
    foreach (var name in refNames.Except(mainNames, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"FAIL 基準庫有、主庫缺表: {name}");
        failures++;
    }
    foreach (var name in mainNames.Except(refNames, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"WARN 主庫多出表（基準庫無）: {name}");
        warnings++;
    }

    foreach (var t in tables)
    {
        var mainCnt = ScalarLong($"SELECT COUNT(*) FROM \"{t.Name}\"");
        var refCnt = RefScalarLong(refConn, $"SELECT COUNT(*) FROM \"{t.Name}\"");
        if (mainCnt != refCnt)
        {
            Console.WriteLine($"FAIL {t.Name}: 筆數 {mainCnt} ≠ 基準 {refCnt}");
            failures++;
        }

        var pkCols = t.PkColumns.OrderBy(c => c.PkSeq).Select(c => c.Name).ToList();
        if (pkCols.Count == 0)
        {
            Console.WriteLine($"WARN {t.Name}: 無主鍵");
            warnings++;
            continue;
        }
        var nullCnt = 0;
        foreach (var c in pkCols)
            nullCnt += (int)ScalarLong($"SELECT COUNT(*) FROM \"{t.Name}\" WHERE \"{c}\" IS NULL");
        if (nullCnt > 0)
        {
            Console.WriteLine($"FAIL {t.Name}: 主鍵欄位含 {nullCnt} 筆 NULL");
            failures++;
        }
        var pkList = string.Join(", ", pkCols.Select(c => $"\"{c}\""));
        var dupCnt = ScalarLong($"SELECT COUNT(*) FROM (SELECT {pkList} FROM \"{t.Name}\" GROUP BY {pkList} HAVING COUNT(*) > 1)");
        if (dupCnt > 0)
        {
            Console.WriteLine($"FAIL {t.Name}: 主鍵重複 {dupCnt} 組");
            failures++;
        }

        var refIndexes = LoadRefIndexes(refConn, t.Name);
        var mainSig = t.Indexes.Select(i => (string.Join(",", i.Columns), i.Unique)).ToHashSet();
        foreach (var (cols, unique) in refIndexes)
        {
            if (!mainSig.Contains((cols, unique)))
            {
                Console.WriteLine($"FAIL {t.Name}: 基準索引 [{cols}] unique={(unique ? 1 : 0)} 在主庫不存在");
                failures++;
            }
        }
    }

    Console.WriteLine($"\n=== 驗證完成：{tables.Count} 張表，FAIL {failures}，WARN {warnings} ===");
    return failures == 0 ? 0 : 1;
}

static long ScalarLong(string sql)
{
    using var conn = DbManager.OpenConnection();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    return Convert.ToInt64(cmd.ExecuteScalar());
}

static long RefScalarLong(SqliteConnection conn, string sql)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    return Convert.ToInt64(cmd.ExecuteScalar());
}

static List<(string Cols, bool Unique)> LoadRefIndexes(SqliteConnection conn, string tableName)
{
    var result = new List<(string, bool)>();
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"PRAGMA index_list(\"{tableName}\")";
        using var r = cmd.ExecuteReader();
        var idxs = new List<(string Name, bool Unique)>();
        while (r.Read()) idxs.Add((r.GetString(1), r.GetInt32(2) == 1));
        foreach (var (name, unique) in idxs)
        {
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = $"PRAGMA index_info(\"{name}\")";
            using var r2 = cmd2.ExecuteReader();
            var cols = new List<string>();
            while (r2.Read()) cols.Add(r2.GetString(2));
            result.Add((string.Join(",", cols), unique));
        }
    }
    return result;
}

static string ScriptPath(string dbPath, string? only)
{
    var dir = Path.GetDirectoryName(dbPath) ?? ".";
    var name = only is null ? "DbSchemaFix.sql" : $"DbSchemaFix-{SanitizeFileName(only)}.sql";
    return Path.Combine(dir, name);
}

static string SanitizeFileName(string name) =>
    string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

class TableDiag
{
    public string Name { get; init; } = "";
    public string CreateSql { get; set; } = "";
    public List<ColumnDiag> Columns { get; } = new();
    public List<ColumnDiag> PkColumns { get; } = new();
    public List<IndexDiag> Indexes { get; } = new();
    public string? CandidatePk { get; set; }
    public List<string>? ExplicitPk { get; set; }
    public bool FirstColumnUnique { get; set; }
    public bool CandidateHasUniqueIndex { get; set; }
    public bool SafeToFix { get; set; }
}

class ColumnDiag
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public int PkSeq { get; init; }
}

class IndexDiag
{
    public string Name { get; init; } = "";
    public bool Unique { get; init; }
    public string Origin { get; init; } = "";
    public List<string> Columns { get; } = new();
}
