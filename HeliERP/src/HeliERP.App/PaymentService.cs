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
/// 收付作業核心：收款（客戶應收）/ 付款（廠商應付）之沖帳、撤銷沖帳。
/// 資料流（單一 BEGIN IMMEDIATE 交易，失敗全數回滾）：
/// 收付主檔 → 收付明細 → 帳款簡要（未收付遞減）→ 帳款主檔（已收付累加）→ 交易主檔（已收付/未收付同步）。
/// 收付明細依歷史慣例以「單據號碼 = 交易單號、單別 = 單據類別」關聯交易單據。
/// </summary>
public static class PaymentService
{
    // ── 收付類別定義 ──
    public sealed class PaymentKind
    {
        /// <summary>收付類別（資料庫值）：收款 / 付款</summary>
        public required string Name { get; init; }
        public required string Display { get; init; }
        /// <summary>沖帳對象類型：客戶 / 廠商</summary>
        public required string ObjectType { get; init; }
    }

    public static readonly PaymentKind[] Kinds =
    {
        new() { Name = "收款", Display = "收款單（客戶應收沖帳）", ObjectType = "客戶" },
        new() { Name = "付款", Display = "付款單（廠商應付沖帳）", ObjectType = "廠商" },
    };

    public static PaymentKind GetKind(string name) => Kinds.FirstOrDefault(k => k.Name == name) ?? Kinds[0];

    // ==================== 請求模型 ====================

    /// <summary>收付明細列：對應一張被沖帳的單據</summary>
    public sealed class PaymentDetailRow
    {
        public string 交易單號 = "";
        public string 單據類別 = "";
        public string 交易日期 = "";
        public decimal 未收付金額;
        public decimal 沖帳金額;
        public decimal 折讓金額;
    }

    public sealed class SavePaymentRequest
    {
        public string 收付類別 = "收款";
        public DateTime 沖帳日期 = DateTime.Now;
        public string 沖帳對象 = "";
        public decimal 現金金額;
        public decimal 票據金額;
        /// <summary>取用預收：以該對象預收餘額沖帳（現金+票據+取用預收 = 沖帳合計）</summary>
        public decimal 取用預收;
        /// <summary>累入預收：純預收單專用（不沖帳，收現金存入預收）</summary>
        public decimal 累入預收;
        public List<PaymentDetailRow> 明細 = new();
    }

    public sealed record SaveResult(string 收付單號, long 單據副碼);

    // ==================== 帶入查詢（畫面使用） ====================

    /// <summary>交易對象下拉（客戶 / 廠商）</summary>
    public static DataTable LoadObjectCombo(string 客廠類別) =>
        DbManager.QueryTable(
            "SELECT [客廠編號], [公司簡稱] FROM [客戶廠商] WHERE [客廠類別] = $t ORDER BY [客廠編號]",
            DbManager.Param("$t", 客廠類別));

    public static string? LookupObjectName(string 客廠編號) =>
        DbManager.QueryScalar("SELECT [公司簡稱] FROM [客戶廠商] WHERE [客廠編號] = $o",
            DbManager.Param("$o", 客廠編號)) as string;

    /// <summary>載入對象的未沖帳單據（未收付金額 &gt; 0 者；出退/進退 為負值之抵銷單不列）</summary>
    public static DataTable LoadOpenBills(string 對象)
    {
        var dt = DbManager.QueryTable(
            "SELECT [交易單號], [單據類別], [交易日期], [總計金額], [已收付金額], [未收付金額] " +
            "FROM [帳款簡要] WHERE [交易對象] = $o AND [未收付金額] > 0 ORDER BY [交易單號]",
            DbManager.Param("$o", 對象));
        dt.Columns.Add("沖帳金額", typeof(decimal));
        dt.Columns.Add("折讓金額", typeof(decimal));
        foreach (DataRow r in dt.Rows)
        {
            var 未收 = r.IsNull("未收付金額") ? 0m : Convert.ToDecimal(r["未收付金額"]);
            r["沖帳金額"] = 未收;   // 預設全額沖帳，使用者可改
            r["折讓金額"] = 0m;
        }
        return dt;
    }

    /// <summary>收付主檔列表（依類別/對象/日期範圍過濾）</summary>
    public static DataTable LoadPayments(string? 類別, string? 對象, DateTime? 起日, DateTime? 迄日)
    {
        var where = new List<string>();
        var pars = new List<SqliteParameter>();
        if (!string.IsNullOrWhiteSpace(類別))
        {
            where.Add("[收付類別] = $k");
            pars.Add(DbManager.Param("$k", 類別));
        }
        if (!string.IsNullOrWhiteSpace(對象))
        {
            where.Add("[沖帳對象] LIKE $o");
            pars.Add(DbManager.Param("$o", "%" + 對象.Trim() + "%"));
        }
        if (起日 is not null)
        {
            where.Add("[沖帳日期] >= $d1");
            pars.Add(DbManager.Param("$d1", 起日.Value.ToString("yyyy-MM-dd 00:00:00")));
        }
        if (迄日 is not null)
        {
            where.Add("[沖帳日期] <= $d2");
            pars.Add(DbManager.Param("$d2", 迄日.Value.ToString("yyyy-MM-dd 23:59:59")));
        }
        string cond = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);
        return DbManager.QueryTable(
            "SELECT P.[單據副碼], P.[收付單號], P.[收付類別], P.[沖帳日期], P.[沖帳對象], " +
            "P.[現金金額], P.[票據金額], P.[沖帳合計], P.[應收餘額], C.[公司簡稱] AS 對象名稱 " +
            "FROM [收付主檔] P LEFT JOIN [客戶廠商] C ON C.[客廠編號] = P.[沖帳對象]" +
            cond + " ORDER BY P.[收付單號] DESC", pars.ToArray());
    }

    /// <summary>單張收付單的明細（導覽顯示用）</summary>
    public static DataTable LoadPaymentDetails(long 收付副碼) =>
        DbManager.QueryTable(
            "SELECT [單據號碼], [單別], [單據日期], [現行餘額], [折讓金額], [沖帳金額] " +
            "FROM [收付明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
            DbManager.Param("$c", 收付副碼));

    /// <summary>預估下一筆收付單號（僅供畫面顯示；正式取號以 SavePayment 交易內為準）</summary>
    public static string PreviewPaymentNo()
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = DbManager.QueryScalar(
            "SELECT MAX([收付單號]) FROM [收付主檔] WHERE [收付單號] LIKE $p",
            DbManager.Param("$p", today + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= 10 && int.TryParse(max.AsSpan(6, 4), out var last))
            seq = last + 1;
        return today + seq.ToString("0000");
    }

    // ==================== 沖帳（新增） ====================

    public static SaveResult SavePayment(SavePaymentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.沖帳對象))
            throw new InvalidOperationException("請選擇沖帳對象。");
        decimal 取用預收 = Math.Max(0m, req.取用預收);
        decimal 累入預收 = Math.Max(0m, req.累入預收);
        if (req.明細.Count == 0)
        {
            if (累入預收 <= 0m)
                throw new InvalidOperationException("未選取待沖帳單據。請勾選單據沖帳，或輸入「累入預收」存入預收。");
            if (Math.Abs(req.現金金額 - 累入預收) > 0.005m || req.票據金額 != 0m || 取用預收 != 0m)
                throw new InvalidOperationException("累入預收單：現金金額須等於累入預收，且不可同時沖帳或取用預收。");
            return SavePrepayment(req, 累入預收);
        }
        foreach (var d in req.明細)
        {
            if (d.沖帳金額 <= 0)
                throw new InvalidOperationException($"單據「{d.交易單號}」的沖帳金額必須大於 0。");
            if (d.折讓金額 < 0)
                throw new InvalidOperationException($"單據「{d.交易單號}」折讓金額不得為負數。");
            if (d.沖帳金額 + d.折讓金額 > d.未收付金額 + 0.005m)
                throw new InvalidOperationException(
                    $"單據「{d.交易單號}」沖帳 {d.沖帳金額:N2} + 折讓 {d.折讓金額:N2} 超過未收付金額 {d.未收付金額:N2}。");
        }
        decimal 沖帳合計 = req.明細.Sum(d => d.沖帳金額);
        decimal 折讓合計 = req.明細.Sum(d => d.折讓金額);
        if (Math.Abs(req.現金金額 + req.票據金額 + 取用預收 - 沖帳合計) > 0.005m)
            throw new InvalidOperationException(
                $"現金 + 票據 + 取用預收（{req.現金金額:N2} + {req.票據金額:N2} + {取用預收:N2}）必須等於沖帳合計 {沖帳合計:N2}。");

        SaveResult? result = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            long 副碼 = NextSeq(conn, "收付主檔", "單據副碼");
            string 收付單號 = NextPaymentNo(conn);
            decimal 原預收 = GetPrepaidBalance(conn, req.沖帳對象);
            if (取用預收 > 原預收 + 0.005m)
                throw new InvalidOperationException($"取用預收 {取用預收:N2} 超過該對象預收餘額 {原預收:N2}。");

            // 1. 應收(付)餘額 = 該對象所有未收付金額（取絕對值合計） - 本次沖帳 - 折讓
            decimal 餘額 = Convert.ToDecimal(ExecScalar(conn,
                "SELECT COALESCE(SUM(ABS([未收付金額])), 0) FROM [帳款簡要] WHERE [交易對象] = $o",
                DbManager.Param("$o", req.沖帳對象))) - 沖帳合計 - 折讓合計;

            // 2. 收付主檔（沖帳合計不含折讓；折讓為放棄之應收）
            InsertRow(conn, "收付主檔", new Dictionary<string, object?>
            {
                ["收付類別"] = req.收付類別, ["收付單號"] = 收付單號, ["單據副碼"] = 副碼,
                ["沖帳日期"] = req.沖帳日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["沖帳對象"] = req.沖帳對象,
                ["現金金額"] = req.現金金額, ["票據金額"] = req.票據金額,
                ["取用預收"] = 取用預收, ["應收餘額"] = 餘額,
                ["預收餘額"] = 原預收 - 取用預收, ["累入預收"] = 0m,
                ["銷貨折讓"] = 折讓合計, ["現金折讓"] = 0m, ["沖帳合計"] = 沖帳合計,
                ["可沖餘額"] = 0m, ["經辦人員"] = CurrentUser,
            });

            // 3. 收付明細（每張單據一筆；單據副碼 = 收付主檔副碼，單據號碼 = 交易單號）
            long seq = NextSeq(conn, "收付明細", "建檔序號");
            foreach (var d in req.明細)
            {
                InsertRow(conn, "收付明細", new Dictionary<string, object?>
                {
                    ["單據副碼"] = 副碼, ["建檔序號"] = seq++,
                    ["單據號碼"] = d.交易單號, ["單別"] = d.單據類別,
                    ["單據日期"] = Nz(d.交易日期),
                    ["現行餘額"] = d.未收付金額, ["折讓金額"] = d.折讓金額, ["沖帳金額"] = d.沖帳金額,
                });
                // 4. 帳款簡要：已收付累加、未收付遞減（沖帳 + 折讓）
                Execute(conn,
                    "UPDATE [帳款簡要] SET [已收付金額] = [已收付金額] + $p, [未收付金額] = [未收付金額] - $p - $d " +
                    "WHERE [交易單號] = $n AND [單據類別] = $k",
                    DbManager.Param("$p", d.沖帳金額), DbManager.Param("$d", d.折讓金額),
                    DbManager.Param("$n", d.交易單號), DbManager.Param("$k", d.單據類別));
                // 5. 交易主檔同步
                Execute(conn,
                    "UPDATE [交易主檔] SET [已收付金額] = [已收付金額] + $p, [未收付金額] = [未收付金額] - $p - $d " +
                    "WHERE [交易單號] = $n AND [單據類別] = $k",
                    DbManager.Param("$p", d.沖帳金額), DbManager.Param("$d", d.折讓金額),
                    DbManager.Param("$n", d.交易單號), DbManager.Param("$k", d.單據類別));
            }

            // 6. 帳款主檔：已收付累加、折讓累加、取用預收遞減
            Execute(conn,
                "UPDATE [帳款主檔] SET [已收付金額] = [已收付金額] + $p, [折讓金額] = [折讓金額] + $d, " +
                "[累計預收貨款] = [累計預收貨款] - $u WHERE [交易對象] = $o",
                DbManager.Param("$p", 沖帳合計), DbManager.Param("$d", 折讓合計),
                DbManager.Param("$u", 取用預收), DbManager.Param("$o", req.沖帳對象));

            result = new SaveResult(收付單號, 副碼);
        });
        if (result is not null)
            AuditService.Log(AuditService.存檔, "收付", result.收付單號, "成功",
                $"{req.收付類別}，現金 {req.現金金額:N0}、票據 {req.票據金額:N0}、沖帳 {沖帳合計:N0}");
        return result!;
    }

    /// <summary>純累入預收單：收現金存入該對象預收，不沖帳</summary>
    private static SaveResult SavePrepayment(SavePaymentRequest req, decimal 累入預收)
    {
        SaveResult? result = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            long 副碼 = NextSeq(conn, "收付主檔", "單據副碼");
            string 收付單號 = NextPaymentNo(conn);
            decimal 原預收 = GetPrepaidBalance(conn, req.沖帳對象);
            decimal 餘額 = Convert.ToDecimal(ExecScalar(conn,
                "SELECT COALESCE(SUM(ABS([未收付金額])), 0) FROM [帳款簡要] WHERE [交易對象] = $o",
                DbManager.Param("$o", req.沖帳對象)));

            InsertRow(conn, "收付主檔", new Dictionary<string, object?>
            {
                ["收付類別"] = req.收付類別, ["收付單號"] = 收付單號, ["單據副碼"] = 副碼,
                ["沖帳日期"] = req.沖帳日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["沖帳對象"] = req.沖帳對象,
                ["現金金額"] = req.現金金額, ["票據金額"] = 0m,
                ["取用預收"] = 0m, ["應收餘額"] = 餘額,
                ["預收餘額"] = 原預收 + 累入預收, ["累入預收"] = 累入預收,
                ["銷貨折讓"] = 0m, ["現金折讓"] = 0m, ["沖帳合計"] = 0m,
                ["可沖餘額"] = 0m, ["經辦人員"] = CurrentUser,
            });

            Execute(conn,
                "UPDATE [帳款主檔] SET [累計預收貨款] = [累計預收貨款] + $p WHERE [交易對象] = $o",
                DbManager.Param("$p", 累入預收), DbManager.Param("$o", req.沖帳對象));

            result = new SaveResult(收付單號, 副碼);
        });
        if (result is not null)
            AuditService.Log(AuditService.存檔, "收付", result.收付單號, "成功",
                $"純累入預收 {累入預收:N0}");
        return result!;
    }

    // ==================== 撤銷沖帳（刪除收付單） ====================

    public static void DeletePayment(long 副碼)
    {
        string? auditNo = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            var m = SelectOne(conn, "SELECT * FROM [收付主檔] WHERE [單據副碼] = $c",
                DbManager.Param("$c", 副碼))
                ?? throw new InvalidOperationException("找不到該收付單，可能已被刪除。");
            decimal 沖帳合計 = GetDec(m, "沖帳合計", 0m);
            decimal 折讓合計 = GetDec(m, "銷貨折讓", 0m);
            decimal 取用預收 = GetDec(m, "取用預收", 0m);
            decimal 累入預收 = GetDec(m, "累入預收", 0m);

            var details = SelectAll(conn, "SELECT * FROM [收付明細] WHERE [單據副碼] = $c",
                DbManager.Param("$c", 副碼));
            foreach (var d in details)
            {
                decimal 金額 = GetDec(d, "沖帳金額", 0m);
                decimal 折讓 = GetDec(d, "折讓金額", 0m);
                string 單號 = Str(d["單據號碼"]);
                string 單別 = Str(d["單別"]);
                Execute(conn,
                    "UPDATE [帳款簡要] SET [已收付金額] = [已收付金額] - $p, [未收付金額] = [未收付金額] + $p + $d " +
                    "WHERE [交易單號] = $n AND [單據類別] = $k",
                    DbManager.Param("$p", 金額), DbManager.Param("$d", 折讓),
                    DbManager.Param("$n", 單號), DbManager.Param("$k", 單別));
                Execute(conn,
                    "UPDATE [交易主檔] SET [已收付金額] = [已收付金額] - $p, [未收付金額] = [未收付金額] + $p + $d " +
                    "WHERE [交易單號] = $n AND [單據類別] = $k",
                    DbManager.Param("$p", 金額), DbManager.Param("$d", 折讓),
                    DbManager.Param("$n", 單號), DbManager.Param("$k", 單別));
            }

            Execute(conn,
                "UPDATE [帳款主檔] SET [已收付金額] = [已收付金額] - $p, [折讓金額] = [折讓金額] - $d, " +
                "[累計預收貨款] = [累計預收貨款] + $u - $l WHERE [交易對象] = $o",
                DbManager.Param("$p", 沖帳合計), DbManager.Param("$d", 折讓合計),
                DbManager.Param("$u", 取用預收), DbManager.Param("$l", 累入預收),
                DbManager.Param("$o", Str(m["沖帳對象"])));

            Execute(conn, "DELETE FROM [收付明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            Execute(conn, "DELETE FROM [收付主檔] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            auditNo = Str(m["收付單號"]);
        });
        if (auditNo is not null)
            AuditService.Log(AuditService.刪除, "收付", auditNo, "成功");
    }

    /// <summary>對象目前預收餘額（帳款主檔累計預收貨款）</summary>
    public static decimal LookupPrepaidBalance(string 對象)
    {
        var v = DbManager.QueryScalar(
            "SELECT COALESCE([累計預收貨款], 0) FROM [帳款主檔] WHERE [交易對象] = $o",
            DbManager.Param("$o", 對象));
        return v is null or DBNull ? 0m : Convert.ToDecimal(v);
    }

    // ==================== 交易內輔助 ====================

    private static string CurrentUser => Environment.UserName;

    private static long NextSeq(SqliteConnection conn, string table, string column) =>
        Convert.ToInt64(ExecScalar(conn, $"SELECT COALESCE(MAX([{column}]), 0) FROM [{table}]")) + 1;

    private static decimal GetPrepaidBalance(SqliteConnection conn, string 對象) =>
        Convert.ToDecimal(ExecScalar(conn,
            "SELECT COALESCE([累計預收貨款], 0) FROM [帳款主檔] WHERE [交易對象] = $o",
            DbManager.Param("$o", 對象)));

    /// <summary>產生收付單號：YYMMDD + 當日 4 位流水（依歷史慣例，全域流水不分類別）</summary>
    private static string NextPaymentNo(SqliteConnection conn)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = ExecScalar(conn,
            "SELECT MAX([收付單號]) FROM [收付主檔] WHERE [收付單號] LIKE $p",
            DbManager.Param("$p", today + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= 10 && int.TryParse(max.AsSpan(6, 4), out var last))
            seq = last + 1;
        if (seq > 9999)
            throw new InvalidOperationException("當日收付單號流水已滿（超過 9999 張），請聯絡系統管理員。");
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

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    private static object? Nz(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static decimal GetDec(Dictionary<string, object?> d, string col, decimal def) =>
        d.TryGetValue(col, out var v) && v is not null && decimal.TryParse(v.ToString(), out var m) ? m : def;
}
