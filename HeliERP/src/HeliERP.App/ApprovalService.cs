// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（核准流程）
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 多層核准流程：採購／訂貨／報價／詢價／收款／付款等單據，
/// 可依「核准設定」於存檔後自動送審，由核准人逐層核准或退回。
/// 若該單據類別未啟用核准，則直接視為完成，不影響既有作業流程。
/// </summary>
public static class ApprovalService
{
    public const string 待核准 = "待核准";
    public const string 已核准 = "已核准";
    public const string 已退回 = "已退回";

    public static readonly string[] 預設類別 =
        { "報價", "訂貨", "採購", "詢價", "收款", "付款" };

    public static void EnsureSchema()
    {
        using var conn = DbManager.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE IF NOT EXISTS [核准設定] (" +
            "[單據類別] TEXT PRIMARY KEY, " +
            "[層數] INTEGER NOT NULL DEFAULT 1, " +
            "[啟用] INTEGER NOT NULL DEFAULT 0);" +
            "CREATE TABLE IF NOT EXISTS [核准流程] (" +
            "[序號] INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "[單據類別] TEXT NOT NULL, [單號] TEXT NOT NULL, [金額] REAL NOT NULL DEFAULT 0, " +
            "[申請人] TEXT NOT NULL, [申請時間] TEXT NOT NULL, " +
            "[目前層級] INTEGER NOT NULL DEFAULT 1, [層數] INTEGER NOT NULL DEFAULT 1, " +
            "[狀態] TEXT NOT NULL, [完成時間] TEXT, [備註] TEXT);" +
            "CREATE TABLE IF NOT EXISTS [核准紀錄] (" +
            "[序號] INTEGER PRIMARY KEY AUTOINCREMENT, [流程序號] INTEGER NOT NULL, " +
            "[層級] INTEGER NOT NULL, [核准人] TEXT NOT NULL, [意見] TEXT, " +
            "[時間] TEXT NOT NULL, [結果] TEXT NOT NULL);";
        cmd.ExecuteNonQuery();
    }

    /// <summary>該類別是否已存在設定（否則建立預設 1 層未啟用）</summary>
    public static bool HasSetting(string 類別)
    {
        return DbManager.QueryScalar(
            "SELECT COUNT(*) FROM [核准設定] WHERE [單據類別] = $c",
            DbManager.Param("$c", 類別)) is long n && n > 0;
    }

    public static void SaveSetting(string 類別, int 層數, bool 啟用)
    {
        using var conn = DbManager.OpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var del = DbManager.CreateCommand(conn, "DELETE FROM [核准設定] WHERE [單據類別] = $c", DbManager.Param("$c", 類別)))
            {
                del.Transaction = tx;
                del.ExecuteNonQuery();
            }
            using (var ins = DbManager.CreateCommand(conn,
                "INSERT INTO [核准設定] ([單據類別], [層數], [啟用]) VALUES ($c, $l, $e)",
                DbManager.Param("$c", 類別), DbManager.Param("$l", Math.Clamp(層數, 1, 5)),
                DbManager.Param("$e", 啟用 ? 1 : 0)))
            {
                ins.Transaction = tx;
                ins.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public static DataTable LoadSettings()
    {
        EnsureSchema();
        var dt = DbManager.QueryTable("SELECT [單據類別], [層數], [啟用] FROM [核准設定] ORDER BY [單據類別]");
        if (dt.Rows.Count == 0)
        {
            foreach (var c in 預設類別)
                if (!HasSetting(c))
                    SaveSetting(c, 2, false);
            dt = DbManager.QueryTable("SELECT [單據類別], [層數], [啟用] FROM [核准設定] ORDER BY [單據類別]");
        }
        return dt;
    }

    /// <summary>
    /// 存檔後送審：若該類別未啟用核准，直接回傳 null（不需送審）。
    /// 啟用時建立待核准流程，回傳流程序號。
    /// </summary>
    public static long? Submit(string 類別, string 單號, decimal 金額, string 申請人, string 備註 = "")
    {
        EnsureSchema();
        using var conn = DbManager.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT [層數], [啟用] FROM [核准設定] WHERE [單據類別] = $c";
        cmd.Parameters.Add(DbManager.Param("$c", 類別));
        long? 層數 = null; long 啟用 = 0;
        using (var r = cmd.ExecuteReader())
        {
            if (r.Read())
            {
                層數 = (long)r["層數"];
                啟用 = (long)r["啟用"];
            }
        }
        if (層數 is null || 啟用 == 0) return null;

        using var tx = conn.BeginTransaction();
        try
        {
            using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText =
                "INSERT INTO [核准流程] ([單據類別], [單號], [金額], [申請人], [申請時間], [目前層級], [層數], [狀態], [備註]) " +
                "VALUES ($c, $n, $a, $u, $t, 1, $l, $s, $m); SELECT last_insert_rowid();";
            ins.Parameters.Add(DbManager.Param("$c", 類別));
            ins.Parameters.Add(DbManager.Param("$n", 單號));
            ins.Parameters.Add(DbManager.Param("$a", 金額));
            ins.Parameters.Add(DbManager.Param("$u", 申請人));
            ins.Parameters.Add(DbManager.Param("$t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            ins.Parameters.Add(DbManager.Param("$l", 層數.Value));
            ins.Parameters.Add(DbManager.Param("$s", 待核准));
            ins.Parameters.Add(DbManager.Param("$m", 備註));
            long seq = Convert.ToInt64(ins.ExecuteScalar());
            tx.Commit();
            return seq;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public static DataTable LoadFlows(string? 類別, string? 狀態, string 關鍵字)
    {
        EnsureSchema();
        var sql = "SELECT [序號], [單據類別], [單號], [金額], [申請人], [申請時間], " +
                  "[目前層級], [層數], [狀態], [完成時間], [備註] FROM [核准流程] WHERE 1 = 1";
        var pars = new List<SqliteParameter>();
        if (!string.IsNullOrWhiteSpace(類別) && 類別 != "全部")
        {
            sql += " AND [單據類別] = $c";
            pars.Add(DbManager.Param("$c", 類別));
        }
        if (!string.IsNullOrWhiteSpace(狀態) && 狀態 != "全部")
        {
            sql += " AND [狀態] = $s";
            pars.Add(DbManager.Param("$s", 狀態));
        }
        if (!string.IsNullOrWhiteSpace(關鍵字))
        {
            sql += " AND ([單號] LIKE $k OR [申請人] LIKE $k)";
            pars.Add(DbManager.Param("$k", $"%{關鍵字}%"));
        }
        sql += " ORDER BY [申請時間] DESC";
        return DbManager.QueryTable(sql, pars.ToArray());
    }

    /// <summary>核准下一層；回傳 null 表示成功，否則回傳錯誤訊息。</summary>
    public static string? Approve(long 流程序號, string 核准人, string 意見)
    {
        EnsureSchema();
        using var conn = DbManager.OpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT [目前層級], [層數], [狀態] FROM [核准流程] WHERE [序號] = $s";
                cmd.Parameters.Add(DbManager.Param("$s", 流程序號));
                long 目前層級, 層數; string 狀態;
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return "找不到該核准流程。";
                    目前層級 = (long)r["目前層級"];
                    層數 = (long)r["層數"];
                    狀態 = Convert.ToString(r["狀態"]) ?? "";
                }
                if (狀態 != 待核准) return $"該流程狀態為「{狀態}」，無法核准。";
                LogRecord(tx, 流程序號, 目前層級, 核准人, 意見, "核准");
                if (目前層級 >= 層數)
                {
                    using var up = conn.CreateCommand();
                    up.Transaction = tx;
                    up.CommandText = "UPDATE [核准流程] SET [狀態] = $s, [完成時間] = $t WHERE [序號] = $i";
                    up.Parameters.Add(DbManager.Param("$s", 已核准));
                    up.Parameters.Add(DbManager.Param("$t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                    up.Parameters.Add(DbManager.Param("$i", 流程序號));
                    up.ExecuteNonQuery();
                }
                else
                {
                    using var up = conn.CreateCommand();
                    up.Transaction = tx;
                    up.CommandText = "UPDATE [核准流程] SET [目前層級] = [目前層級] + 1 WHERE [序號] = $i";
                    up.Parameters.Add(DbManager.Param("$i", 流程序號));
                    up.ExecuteNonQuery();
                }
            }
            tx.Commit();
            return null;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return ex.Message;
        }
    }

    public static string? Reject(long 流程序號, string 核准人, string 意見)
    {
        EnsureSchema();
        using var conn = DbManager.OpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT [目前層級], [狀態] FROM [核准流程] WHERE [序號] = $s";
                cmd.Parameters.Add(DbManager.Param("$s", 流程序號));
                long 目前層級; string 狀態;
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return "找不到該核准流程。";
                    目前層級 = (long)r["目前層級"];
                    狀態 = Convert.ToString(r["狀態"]) ?? "";
                }
                if (狀態 != 待核准) return $"該流程狀態為「{狀態}」，無法退回。";
                LogRecord(tx, 流程序號, 目前層級, 核准人, 意見, "退回");
                using (var up = conn.CreateCommand())
                {
                    up.Transaction = tx;
                    up.CommandText = "UPDATE [核准流程] SET [狀態] = $s, [完成時間] = $t WHERE [序號] = $i";
                    up.Parameters.Add(DbManager.Param("$s", 已退回));
                    up.Parameters.Add(DbManager.Param("$t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                    up.Parameters.Add(DbManager.Param("$i", 流程序號));
                    up.ExecuteNonQuery();
                }
            }
            tx.Commit();
            return null;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return ex.Message;
        }
    }

    private static void LogRecord(SqliteTransaction tx, long 流程序號, long 層級, string 核准人, string 意見, string 結果)
    {
        using var cmd = tx.Connection!.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO [核准紀錄] ([流程序號], [層級], [核准人], [意見], [時間], [結果]) " +
            "VALUES ($f, $l, $u, $m, $t, $r)";
        cmd.Parameters.Add(DbManager.Param("$f", 流程序號));
        cmd.Parameters.Add(DbManager.Param("$l", 層級));
        cmd.Parameters.Add(DbManager.Param("$u", 核准人));
        cmd.Parameters.Add(DbManager.Param("$m", string.IsNullOrWhiteSpace(意見) ? null : 意見));
        cmd.Parameters.Add(DbManager.Param("$t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        cmd.Parameters.Add(DbManager.Param("$r", 結果));
        cmd.ExecuteNonQuery();
    }

    public static DataTable LoadRecords(long 流程序號)
    {
        return DbManager.QueryTable(
            "SELECT [層級], [核准人], [意見], [時間], [結果] FROM [核准紀錄] " +
            "WHERE [流程序號] = $f ORDER BY [時間], [層級]",
            DbManager.Param("$f", 流程序號));
    }
}
