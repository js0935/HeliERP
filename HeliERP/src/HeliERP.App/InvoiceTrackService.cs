// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（電子發票字軌管理）
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 電子發票／統一發票字軌管理：
/// 每期由國稅局配發之字軌（年度、月期、字軌英文、起號、迄號）輸入系統後，
/// 開立發票時依「已用迄號」依序配號（自動配號可於出貨／進貨存檔時觸發），
/// 並登記每張發票的開立單據與作廢狀態，供查核與使用進度統計。
/// 既有自由輸入發票號碼的流程不受影響（僅在字軌設定且啟用自動配號時配號）。
/// </summary>
public static class InvoiceTrackService
{
    public const string 啟用 = "啟用";
    public const string 停用 = "停用";
    public const string 開立 = "開立";
    public const string 作廢 = "作廢";

    public static void EnsureSchema()
    {
        using var conn = DbManager.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE IF NOT EXISTS [發票字軌] (" +
            "[序號] INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "[年度] TEXT NOT NULL, [月期] TEXT NOT NULL, [字軌] TEXT NOT NULL, " +
            "[起號] INTEGER NOT NULL, [迄號] INTEGER NOT NULL, " +
            "[已用迄號] INTEGER NOT NULL DEFAULT 0, " +
            "[自動配號] INTEGER NOT NULL DEFAULT 0, " +
            "[狀態] TEXT NOT NULL DEFAULT '啟用', [備註] TEXT)";
        cmd.ExecuteNonQuery();
        using (var idx = cmd.Connection!.CreateCommand())
        {
            idx.CommandText = "CREATE INDEX IF NOT EXISTS [IX_發票字軌_字軌] ON [發票字軌]([年度],[月期],[字軌])";
            idx.ExecuteNonQuery();
        }
        using (var cmd2 = conn.CreateCommand())
        {
            cmd2.CommandText =
                "CREATE TABLE IF NOT EXISTS [發票開立紀錄] (" +
                "[序號] INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "[字軌序號] INTEGER NOT NULL, [發票號碼] TEXT NOT NULL, " +
                "[單據類別] TEXT, [單據號碼] TEXT, [開立日期] TEXT, " +
                "[狀態] TEXT NOT NULL DEFAULT '開立', [經辦] TEXT, [備註] TEXT)";
            cmd2.ExecuteNonQuery();
            using var idx2 = conn.CreateCommand();
            idx2.CommandText = "CREATE INDEX IF NOT EXISTS [IX_發票開立_號碼] ON [發票開立紀錄]([發票號碼])";
            idx2.ExecuteNonQuery();
        }
    }

    // ── 字軌查詢 ──

    /// <summary>全部字軌（含使用進度）。</summary>
    public static DataTable LoadTracks()
    {
        EnsureSchema();
        return DbManager.QueryTable(
            "SELECT [序號], [年度], [月期], [字軌], [起號], [迄號], [已用迄號], " +
            "CASE WHEN [已用迄號] = 0 THEN 0 ELSE [迄號] - [已用迄號] END AS [剩餘張數], " +
            "[自動配號], [狀態], [備註] FROM [發票字軌] ORDER BY [年度] DESC, [月期], [字軌]");
    }

    /// <summary>開立紀錄（可依字軌序號／狀態過濾）。</summary>
    public static DataTable LoadIssueLog(long? 字軌序號, string? 狀態, string? 發票號碼 = null)
    {
        EnsureSchema();
        var where = new List<string> { "1=1" };
        var pars = new List<SqliteParameter>();
        if (字軌序號 is long seq)
        {
            where.Add("[字軌序號] = $s");
            pars.Add(DbManager.Param("$s", seq));
        }
        if (!string.IsNullOrEmpty(狀態) && 狀態 != "全部")
        {
            where.Add("[狀態] = $st");
            pars.Add(DbManager.Param("$st", 狀態));
        }
        if (!string.IsNullOrEmpty(發票號碼))
        {
            where.Add("[發票號碼] LIKE $n");
            pars.Add(DbManager.Param("$n", "%" + 發票號碼 + "%"));
        }
        return DbManager.QueryTable(
            "SELECT [序號], [發票號碼], [單據類別], [單據號碼], [開立日期], [狀態], [經辦], [備註] " +
            "FROM [發票開立紀錄] WHERE " + string.Join(" AND ", where) +
            " ORDER BY [發票號碼] DESC LIMIT 2000", pars.ToArray());
    }

    // ── 字軌維護 ──

    public sealed record TrackSaveRequest(string 年度, string 月期, string 字軌, long 起號, long 迄號, bool 自動配號, string 備註);

    public static void SaveTrack(long? 序號, TrackSaveRequest req)
    {
        if (req.字軌.Trim().Length == 0) throw new InvalidOperationException("請輸入字軌（英文）。");
        if (req.起號 < 1 || req.迄號 < req.起號) throw new InvalidOperationException("起號必須大於 0 且迄號不得小於起號。");
        EnsureSchema();
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            var dup = ExecScalar(conn,
                "SELECT COUNT(*) FROM [發票字軌] WHERE [年度] = $y AND [月期] = $p AND [字軌] = $t AND [序號] <> $c",
                DbManager.Param("$y", req.年度.Trim()), DbManager.Param("$p", req.月期.Trim()),
                DbManager.Param("$t", req.字軌.Trim().ToUpperInvariant()), DbManager.Param("$c", 序號 ?? 0));
            if (Convert.ToInt64(dup) > 0)
                throw new InvalidOperationException("相同年度／月期／字軌已存在，請勿重複建置。");
            if (序號 is null)
            {
                InsertRow(conn, "發票字軌", new Dictionary<string, object?>
                {
                    ["年度"] = req.年度.Trim(), ["月期"] = req.月期.Trim(),
                    ["字軌"] = req.字軌.Trim().ToUpperInvariant(),
                    ["起號"] = req.起號, ["迄號"] = req.迄號,
                    ["已用迄號"] = 0, ["自動配號"] = req.自動配號 ? 1 : 0,
                    ["狀態"] = 啟用, ["備註"] = Nz(req.備註),
                });
            }
            else
            {
                Execute(conn,
                    "UPDATE [發票字軌] SET [年度]=$y,[月期]=$p,[字軌]=$t,[起號]=$a,[迄號]=$b," +
                    "[自動配號]=$auto,[備註]=$r WHERE [序號]=$c",
                    DbManager.Param("$y", req.年度.Trim()), DbManager.Param("$p", req.月期.Trim()),
                    DbManager.Param("$t", req.字軌.Trim().ToUpperInvariant()), DbManager.Param("$a", req.起號),
                    DbManager.Param("$b", req.迄號), DbManager.Param("$auto", req.自動配號 ? 1 : 0),
                    DbManager.Param("$r", Nz(req.備註)), DbManager.Param("$c", 序號.Value));
            }
        });
    }

    public static void SetTrackStatus(long 序號, string 狀態)
    {
        EnsureSchema();
        DbManager.ExecuteNonQuery("UPDATE [發票字軌] SET [狀態] = $s WHERE [序號] = $c",
            DbManager.Param("$s", 狀態), DbManager.Param("$c", 序號));
    }

    public static void DeleteTrack(long 序號)
    {
        EnsureSchema();
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            var used = ExecScalar(conn, "SELECT COUNT(*) FROM [發票開立紀錄] WHERE [字軌序號] = $c",
                DbManager.Param("$c", 序號));
            if (Convert.ToInt64(used) > 0)
                throw new InvalidOperationException("該字軌已有開立紀錄，不可刪除（可改為停用）。");
            Execute(conn, "DELETE FROM [發票字軌] WHERE [序號] = $c", DbManager.Param("$c", 序號));
        });
    }

    // ── 配號 ──

    /// <summary>依字軌序號取下一可用發票號碼（交易內使用，併發安全：同一寫入鎖）。</summary>
    public static string NextInvoiceNoInTransaction(SqliteConnection conn, long 序號, string 單據類別, string 單據號碼)
    {
        var m = SelectOne(conn,
            "SELECT [字軌], [起號], [迄號], [已用迄號] FROM [發票字軌] WHERE [序號] = $c AND [狀態] = '啟用'",
            DbManager.Param("$c", 序號))
            ?? throw new InvalidOperationException("找不到啟用中的發票字軌。");
        long next = Convert.ToInt64(m["已用迄號"]) == 0
            ? Convert.ToInt64(m["起號"])
            : Convert.ToInt64(m["已用迄號"]) + 1;
        long 迄號 = Convert.ToInt64(m["迄號"]);
        if (next > 迄號)
            throw new InvalidOperationException($"字軌「{m["字軌"]}」已用罄（迄號 {迄號:D8}），請設定新字軌。");
        Execute(conn, "UPDATE [發票字軌] SET [已用迄號] = $n WHERE [序號] = $c",
            DbManager.Param("$n", next), DbManager.Param("$c", 序號));
        string 號碼 = $"{m["字軌"]}{next:D8}";
        InsertRow(conn, "發票開立紀錄", new Dictionary<string, object?>
        {
            ["字軌序號"] = 序號, ["發票號碼"] = 號碼,
            ["單據類別"] = Nz(單據類別), ["單據號碼"] = Nz(單據號碼),
            ["開立日期"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["狀態"] = 開立, ["經辦"] = AuditService.CurrentUser, ["備註"] = null,
        });
        return 號碼;
    }

    /// <summary>預覽下一可用號碼（僅顯示，不佔號）。</summary>
    public static string? PreviewNextNo(long 序號)
    {
        var m = DbManager.QueryTable(
            "SELECT [字軌], [起號], [迄號], [已用迄號] FROM [發票字軌] WHERE [序號] = $c AND [狀態] = '啟用'",
            DbManager.Param("$c", 序號));
        if (m.Rows.Count == 0) return null;
        long next = Convert.ToInt64(m.Rows[0]["已用迄號"]) == 0
            ? Convert.ToInt64(m.Rows[0]["起號"])
            : Convert.ToInt64(m.Rows[0]["已用迄號"]) + 1;
        if (next > Convert.ToInt64(m.Rows[0]["迄號"])) return null;
        return $"{m.Rows[0]["字軌"]}{next:D8}";
    }

    // ── 開立／作廢 ──

    /// <summary>手動登記一筆發票開立（含既存手輸發票號碼之補登記）。</summary>
    public static void RegisterIssue(long 字軌序號, string 發票號碼, string 單據類別, string 單據號碼, string? 備註 = null)
    {
        EnsureSchema();
        DbManager.ExecuteNonQuery(
            "INSERT INTO [發票開立紀錄] ([字軌序號],[發票號碼],[單據類別],[單據號碼],[開立日期],[狀態],[經辦],[備註]) " +
            "VALUES ($t,$n,$k,$d,$date,$st,$u,$r)",
            DbManager.Param("$t", 字軌序號), DbManager.Param("$n", 發票號碼.Trim().ToUpperInvariant()),
            DbManager.Param("$k", Nz(單據類別)), DbManager.Param("$d", Nz(單據號碼)),
            DbManager.Param("$date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            DbManager.Param("$st", 開立), DbManager.Param("$u", AuditService.CurrentUser),
            DbManager.Param("$r", Nz(備註)));
    }

    /// <summary>作廢發票（單據刪除時呼叫）。</summary>
    public static void RegisterVoid(long 字軌序號, string 發票號碼, string? 備註 = null)
    {
        EnsureSchema();
        DbManager.ExecuteNonQuery(
            "UPDATE [發票開立紀錄] SET [狀態] = $st, [備註] = $r WHERE [字軌序號] = $t AND [發票號碼] = $n AND [狀態] = '開立'",
            DbManager.Param("$st", 作廢), DbManager.Param("$r", Nz(備註)),
            DbManager.Param("$t", 字軌序號), DbManager.Param("$n", 發票號碼.Trim().ToUpperInvariant()));
    }

    // ── 交易內輔助 ──

    private static object? ExecScalar(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    private static void Execute(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        cmd.ExecuteNonQuery();
    }

    private static void InsertRow(SqliteConnection conn, string table, Dictionary<string, object?> vals)
    {
        var cols = string.Join(", ", vals.Keys.Select(k => $"[{k}]"));
        var marks = string.Join(", ", vals.Keys.Select(k => $"${k}"));
        var pars = vals.Select(kv => DbManager.Param($"${kv.Key}", kv.Value)).ToArray();
        Execute(conn, $"INSERT INTO [{table}] ({cols}) VALUES ({marks})", pars);
    }

    private static Dictionary<string, object?>? SelectOne(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        var dict = new Dictionary<string, object?>();
        for (int i = 0; i < r.FieldCount; i++)
            dict[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return dict;
    }

    private static string? Nz(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
