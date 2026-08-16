// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 庫存調整作業核心：盤點盤盈／盤虧、報廢、贈品、損耗等非進出貨之庫存異動。
/// 資料流（單一 BEGIN IMMEDIATE 交易，失敗全數回滾）：
/// 主檔（單據類別 = 庫存調整）→ 明細 → 貨品庫存增減 → 異動快照（稽核）。
/// 調整數量帶方向：正數 = 盤盈（庫存增加）、負數 = 盤虧（庫存減少）。
/// 不產生帳款：主檔帳款欄位一律為 0，亦不寫入帳款三層。
/// </summary>
public static class AdjustmentService
{
    public const string KindName = "庫存調整";

    /// <summary>調整原因選項</summary>
    public static readonly string[] 調整原因 =
        { "盤點盤盈", "盤點盤虧", "報廢", "贈品", "損耗", "其他" };

    public sealed class AdjustmentLine
    {
        public string 貨品編號 = "";
        public string 倉庫編號 = "";
        /// <summary>帶方向：正 = 盤盈（庫存增加）、負 = 盤虧（庫存減少）</summary>
        public decimal 數量;
        public string 單位 = "";
        public string 附註說明 = "";
    }

    public sealed class AdjustmentRequest
    {
        public DateTime 調整日期 = DateTime.Now;
        public string 原因 = "";
        public string 備註 = "";
        public List<AdjustmentLine> 明細 = new();
    }

    // ==================== 存檔 ====================

    public static string SaveAdjustment(AdjustmentRequest req)
    {
        if (req.明細.Count == 0)
            throw new InvalidOperationException("請至少輸入一筆調整明細。");
        if (req.明細.Any(d => string.IsNullOrWhiteSpace(d.貨品編號) || d.數量 == 0))
            throw new InvalidOperationException("明細的貨品編號不可空白，且調整數量不可為 0。");
        if (req.明細.GroupBy(d => d.貨品編號 + "|" + d.倉庫編號).Any(g => g.Count() > 1))
            throw new InvalidOperationException("同一貨品在同一倉庫不可重複列示，請合併調整數量。");

        var p = TradeService.LoadParams();
        string? 單號 = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            long 副碼 = NextSeq(conn, "交易主檔", "單據副碼");
            string no = NextBillNo(conn);
            string 備註 = string.IsNullOrWhiteSpace(req.原因)
                ? req.備註.Trim()
                : (string.IsNullOrWhiteSpace(req.備註) ? req.原因.Trim() : $"{req.原因.Trim()}；{req.備註.Trim()}");

            InsertRow(conn, "交易主檔", new Dictionary<string, object?>
            {
                ["單據類別"] = KindName, ["交易單號"] = no, ["單據副碼"] = 副碼,
                ["交易日期"] = req.調整日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["交易對象"] = null,
                ["計算庫存"] = 1,
                ["數量合計"] = req.明細.Sum(d => d.數量),
                ["合計金額"] = 0m, ["營業稅"] = 0m, ["總計金額"] = 0m,
                ["加項金額"] = 0m, ["減項金額"] = 0m, ["折讓金額"] = 0m,
                ["已收付金額"] = 0m, ["未收付金額"] = 0m, ["應收付金額"] = 0m,
                ["現金收付金額"] = 0m,
                ["明細總筆數"] = req.明細.Count,
                ["本張成本"] = 0m,
                ["原幣合計金額"] = 0m, ["原幣營業稅"] = 0m, ["原幣總計金額"] = 0m,
                ["備註"] = Nz(備註), ["製單"] = CurrentUser,
            });

            long seq = NextSeq(conn, "交易明細", "建檔序號");
            long snapSeq = NextSeq(conn, "交易異動", "建檔序號");
            foreach (var d in req.明細)
            {
                string 品名 = LookupStr(conn,
                    "SELECT [品名] FROM [貨品主檔] WHERE [貨品編號] = $g", "$g", d.貨品編號);
                decimal 成本 = LookupDec(conn,
                    "SELECT COALESCE([現行平均成本], 0) FROM [貨品主檔] WHERE [貨品編號] = $g", "$g", d.貨品編號);

                ApplyStock(conn, p, d);

                InsertRow(conn, "交易明細", new Dictionary<string, object?>
                {
                    ["單據副碼"] = 副碼, ["建檔序號"] = seq++, ["貨品編號"] = d.貨品編號,
                    ["倉庫編號"] = Nz(d.倉庫編號), ["數量"] = d.數量, ["單位"] = Nz(d.單位),
                    ["品名"] = Nz(品名), ["單價"] = 0m, ["成本"] = 成本, ["折扣"] = 100m, ["金額"] = 0m,
                    ["附註說明"] = Nz(d.附註說明), ["贈品"] = 0, ["服務項目"] = 0, ["計算庫存"] = 1,
                    ["異動數量"] = d.數量, ["異動金額"] = 0m,
                });

                var snap = new Dictionary<string, object?>
                {
                    ["建檔序號"] = snapSeq++, ["單據類別"] = KindName, ["交易單號"] = no,
                    ["單據副碼"] = 副碼, ["來源副碼"] = 副碼,
                    ["交易日期"] = req.調整日期.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["交易對象"] = null, ["公司簡稱"] = null,
                    ["倉庫編號"] = Nz(d.倉庫編號), ["員工編號"] = null, ["發票號碼"] = null,
                    ["帳款日期"] = null,
                    ["合計金額"] = 0m, ["營業稅"] = 0m, ["總計金額"] = 0m, ["明細總筆數"] = req.明細.Count,
                    ["貨品編號"] = d.貨品編號, ["批號"] = null, ["品名"] = Nz(品名),
                    ["數量"] = d.數量, ["單位"] = Nz(d.單位), ["單價"] = 0m,
                    ["成本"] = 成本, ["折扣"] = 100m, ["金額"] = 0m,
                    ["附註說明"] = Nz(d.附註說明), ["贈品"] = 0, ["服務項目"] = 0, ["計算庫存"] = 1,
                    ["異動數量"] = d.數量, ["異動金額"] = 0m,
                };
                InsertRow(conn, "交易異動", snap);
                var snapDetail = new Dictionary<string, object?>(snap) { ["交易數量"] = d.數量 };
                InsertRow(conn, "異動明細", snapDetail);
            }
            單號 = no;
        });
        return 單號!;
    }

    // ==================== 刪除 ====================

    public static void DeleteAdjustment(long 副碼)
    {
        var p = TradeService.LoadParams();
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            var m = SelectOne(conn, "SELECT [交易單號],[單據類別] FROM [交易主檔] WHERE [單據副碼] = $c",
                DbManager.Param("$c", 副碼))
                ?? throw new InvalidOperationException("找不到該調整單，可能已被刪除。");
            if (Str(m["單據類別"]) != KindName)
                throw new InvalidOperationException("該單據不是庫存調整單，無法在此刪除。");

            // 1. 回復貨品庫存（反向扣回）
            var details = SelectAll(conn,
                "SELECT [貨品編號],[倉庫編號],[數量] FROM [交易明細] WHERE [單據副碼] = $c",
                DbManager.Param("$c", 副碼));
            foreach (var d in details)
                RestoreStock(conn, p, d);

            // 2. 刪快照 / 明細 / 主檔
            Execute(conn, "DELETE FROM [交易異動] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            Execute(conn, "DELETE FROM [異動明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            Execute(conn, "DELETE FROM [交易明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            Execute(conn, "DELETE FROM [交易主檔] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
        });
    }

    // ==================== 貨品庫存增減 ====================

    private static void ApplyStock(SqliteConnection conn, TradeService.TradeParams p, AdjustmentLine d)
    {
        var where = new List<string> { "[貨品編號] = $g" };
        var pars = new List<SqliteParameter> { DbManager.Param("$g", d.貨品編號) };
        if (p.使用多倉管理 == 1)
        {
            where.Add("[倉庫編號] = $w");
            pars.Add(DbManager.Param("$w", d.倉庫編號));
        }
        string cond = string.Join(" AND ", where);
        var target = ExecScalar(conn,
            $"SELECT [建檔序號] FROM [貨品庫存] WHERE {cond} ORDER BY [建檔序號] LIMIT 1",
            pars.ToArray());
        if (target is null)
            throw new InvalidOperationException($"貨品「{d.貨品編號}」在倉庫「{d.倉庫編號}」沒有庫存列，無法調整庫存。");
        if (p.檢查庫存量 == 1 && d.數量 < 0)
        {
            var cur = Convert.ToDecimal(ExecScalar(conn,
                "SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [建檔序號] = $i",
                DbManager.Param("$g", d.貨品編號), DbManager.Param("$i", target)));
            if (cur + d.數量 < 0)
                throw new InvalidOperationException($"貨品「{d.貨品編號}」庫存不足（現有 {cur}，盤虧 {d.數量}）。");
        }
        Execute(conn,
            "UPDATE [貨品庫存] SET [現有數量] = [現有數量] + $d WHERE [貨品編號] = $g AND [建檔序號] = $i",
            DbManager.Param("$d", d.數量), DbManager.Param("$g", d.貨品編號), DbManager.Param("$i", target));
    }

    /// <summary>刪除調整單時回復庫存（反向扣回）</summary>
    private static void RestoreStock(SqliteConnection conn, TradeService.TradeParams p, Dictionary<string, object?> d)
    {
        string 貨品編號 = Str(d["貨品編號"]);
        string 倉庫編號 = Str(d["倉庫編號"]);
        decimal 調整量 = GetDec(d, "數量", 0m);
        if (調整量 == 0) return;

        var where = new List<string> { "[貨品編號] = $g" };
        var pars = new List<SqliteParameter> { DbManager.Param("$g", 貨品編號) };
        if (p.使用多倉管理 == 1)
        {
            where.Add("[倉庫編號] = $w");
            pars.Add(DbManager.Param("$w", 倉庫編號));
        }
        var target = ExecScalar(conn,
            $"SELECT [建檔序號] FROM [貨品庫存] WHERE {string.Join(" AND ", where)} ORDER BY [建檔序號] LIMIT 1",
            pars.ToArray());
        if (target is null) return;
        Execute(conn,
            "UPDATE [貨品庫存] SET [現有數量] = [現有數量] - $d WHERE [貨品編號] = $g AND [建檔序號] = $i",
            DbManager.Param("$d", 調整量), DbManager.Param("$g", 貨品編號), DbManager.Param("$i", target));
    }

    // ==================== 帶入查詢（畫面使用） ====================

    /// <summary>調整單清單（依單號倒序）</summary>
    public static DataTable LoadAdjustmentList(string? 單號 = null)
    {
        var where = new List<string> { "[單據類別] = $k" };
        var pars = new List<SqliteParameter> { DbManager.Param("$k", KindName) };
        if (!string.IsNullOrWhiteSpace(單號))
        {
            where.Add("[交易單號] LIKE $n");
            pars.Add(DbManager.Param("$n", 單號.Trim() + "%"));
        }
        return DbManager.QueryTable(
            "SELECT [單據副碼], [交易單號], [交易日期], COALESCE([備註],'') AS [備註], " +
            "COALESCE([數量合計],0) AS [數量合計], COALESCE([明細總筆數],0) AS [明細總筆數], " +
            "COALESCE([製單],'') AS [製單] " +
            "FROM [交易主檔] WHERE " + string.Join(" AND ", where) +
            " ORDER BY [交易單號] DESC", pars.ToArray());
    }

    /// <summary>單一調整單明細（檢視用）</summary>
    public static DataTable LoadAdjustmentDetails(long 副碼)
    {
        return DbManager.QueryTable(
            "SELECT d.[貨品編號], COALESCE(p.[品名],'') AS [品名], COALESCE(d.[倉庫編號],'') AS [倉庫編號], " +
            "d.[數量] AS [調整數量], COALESCE(d.[單位],'') AS [單位], COALESCE(d.[附註說明],'') AS [附註說明] " +
            "FROM [交易明細] d " +
            "LEFT JOIN [貨品主檔] p ON p.[貨品編號] = d.[貨品編號] " +
            "WHERE d.[單據副碼] = $c ORDER BY d.[建檔序號]",
            DbManager.Param("$c", 副碼));
    }

    /// <summary>貨品在某倉庫的現有數量／安全存量／基本單位（畫面顯示用）</summary>
    public static Dictionary<string, object?>? LoadStockInfo(string 貨品編號, string 倉庫編號)
    {
        var where = new List<string> { "k.[貨品編號] = $g" };
        var pars = new List<SqliteParameter> { DbManager.Param("$g", 貨品編號) };
        if (!string.IsNullOrWhiteSpace(倉庫編號))
        {
            where.Add("k.[倉庫編號] = $w");
            pars.Add(DbManager.Param("$w", 倉庫編號));
        }
        var row = DbManager.QueryTable(
            "SELECT k.[貨品編號], COALESCE(k.[現有數量],0) AS [現有數量], COALESCE(k.[安全存量],0) AS [安全存量], " +
            "COALESCE(p.[基本單位],'') AS [基本單位] " +
            "FROM [貨品庫存] k LEFT JOIN [貨品主檔] p ON k.[貨品編號] = p.[貨品編號] " +
            "WHERE " + string.Join(" AND ", where) +
            " ORDER BY k.[建檔序號] LIMIT 1", pars.ToArray());
        if (row.Rows.Count == 0)
            return null;
        var dict = new Dictionary<string, object?>();
        foreach (DataColumn c in row.Columns)
            dict[c.ColumnName] = row.Rows[0][c] is DBNull ? null : row.Rows[0][c];
        return dict;
    }

    /// <summary>預估下一筆調整單號（僅供畫面顯示；正式取號以 SaveAdjustment 交易內為準）</summary>
    public static string PreviewAdjustmentNo()
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = DbManager.QueryScalar(
            "SELECT MAX([交易單號]) FROM [交易主檔] WHERE [單據類別] = $k AND [交易單號] LIKE $p",
            DbManager.Param("$k", KindName), DbManager.Param("$p", today + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= 10 && int.TryParse(max.AsSpan(6, 4), out var last))
            seq = last + 1;
        return today + seq.ToString("0000");
    }

    // ==================== 交易內輔助 ====================

    private static string CurrentUser => Environment.UserName;

    private static long NextSeq(SqliteConnection conn, string table, string column) =>
        Convert.ToInt64(ExecScalar(conn, $"SELECT COALESCE(MAX([{column}]), 0) FROM [{table}]")) + 1;

    /// <summary>調整單號：YYMMDD + 當日 4 位流水（與其他單別共用交易主檔流水）</summary>
    private static string NextBillNo(SqliteConnection conn)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = ExecScalar(conn,
            "SELECT MAX([交易單號]) FROM [交易主檔] WHERE [單據類別] = $k AND [交易單號] LIKE $p",
            DbManager.Param("$k", KindName), DbManager.Param("$p", today + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= 10 && int.TryParse(max.AsSpan(6, 4), out var last))
            seq = last + 1;
        if (seq > 9999)
            throw new InvalidOperationException("當日單號流水已滿（超過 9999 張），請聯絡系統管理員。");
        return today + seq.ToString("0000");
    }

    private static void InsertRow(SqliteConnection conn, string table, Dictionary<string, object?> vals)
    {
        var cols = string.Join(", ", vals.Keys.Select(k => $"[{k}]"));
        var marks = string.Join(", ", vals.Keys.Select(k => $"${k}"));
        var pars = vals.Select(kv => DbManager.Param($"${kv.Key}", kv.Value)).ToArray();
        Execute(conn, $"INSERT INTO [{table}] ({cols}) VALUES ({marks})", pars);
    }

    private static void Execute(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        cmd.ExecuteNonQuery();
    }

    private static object? ExecScalar(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    private static Dictionary<string, object?>? SelectOne(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return null;
        var dict = new Dictionary<string, object?>();
        for (int i = 0; i < r.FieldCount; i++)
            dict[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return dict;
    }

    private static List<Dictionary<string, object?>> SelectAll(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        var list = new List<Dictionary<string, object?>>();
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                dict[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(dict);
        }
        return list;
    }

    private static string LookupStr(SqliteConnection conn, string sql, string paramName, string value)
    {
        var v = ExecScalar(conn, sql, DbManager.Param(paramName, value));
        return v is null ? "" : v.ToString() ?? "";
    }

    private static decimal LookupDec(SqliteConnection conn, string sql, string paramName, string value)
    {
        var v = ExecScalar(conn, sql, DbManager.Param(paramName, value));
        return v is null || !decimal.TryParse(v.ToString(), out var m) ? 0m : m;
    }

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    private static object? Nz(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static decimal GetDec(Dictionary<string, object?> d, string col, decimal def) =>
        d.TryGetValue(col, out var v) && v is not null && decimal.TryParse(v.ToString(), out var m) ? m : def;
}
