// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（新增折讓作業）
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 折讓作業核心：出貨折讓（銷貨折讓）／進貨折讓（進貨折讓）單據存檔、刪除。
/// 折讓不異動庫存（退貨另以「出退／進退」處理），僅沖減應收／應付帳款：
///   出貨折讓 → 應收減少；進貨折讓 → 應付減少。
/// 存檔流程（單一 BEGIN IMMEDIATE 交易，失敗全數回滾）：
///   主檔 → 明細 → 帳款三層沖減（主檔／簡要／明細）。
/// 修改 = 先回復舊單影響，再以新值重算；刪除 = 回復 + 沖銷 + 刪除。
/// 折讓主檔／明細欄位沿用既有 Pili6 結構，折讓明細不足欄位以
/// <see cref="EnsureDiscountSchema"/> 於執行期補欄（單據副碼／建檔序號／貨單編號／發票…）。
/// </summary>
public static class DiscountService
{
    // ── 折讓類別 ──
    public sealed class DiscountKind
    {
        public required string Name { get; init; }
        public required string Display { get; init; }
        public required string ObjectType { get; init; }
        public required string TaxSource { get; init; }
    }

    public static readonly DiscountKind[] Kinds =
    {
        new() { Name = "出貨折讓", Display = "出貨折讓單", ObjectType = "客戶", TaxSource = "銷項" },
        new() { Name = "進貨折讓", Display = "進貨折讓單", ObjectType = "廠商", TaxSource = "進項" },
    };

    public static DiscountKind GetKind(string name) =>
        Kinds.FirstOrDefault(k => k.Name == name) ?? Kinds[0];

    // ── 資料模型 ──
    public sealed class DiscountDetailRow
    {
        public string 貨單編號 = "";
        public string 發票編號 = "";
        public string 發票日期 = "";
        public decimal 單據金額;
        public decimal 單據稅金;
        public decimal 折讓金額;
        public decimal 折扣金額;
        public string 附註 = "";
    }

    public sealed class SaveDiscountRequest
    {
        public string 單據類別 = "出貨折讓";
        /// <summary>null = 新增（自動取號）；非 null = 修改（全單重算）</summary>
        public long? 單據副碼;
        /// <summary>null = 自動產生（YYMMDD+當日流水）</summary>
        public string? 折讓單號;
        public DateTime 折讓日期 = DateTime.Now;
        public DateTime 帳款日期;
        public string 交易對象 = "";
        public string 員工編號 = "";
        public string 備註 = "";
        public List<DiscountDetailRow> 明細 = new();
    }

    public sealed record SaveResult(string 折讓單號, long 單據副碼);

    // ── 執行期表結構補欄（折讓明細沿用舊結構，補上報表所需欄位）──
    private static readonly (string Col, string Decl)[] 折讓明細補欄 =
    {
        ("單據副碼", "INTEGER"),
        ("建檔序號", "INTEGER"),
        ("貨單編號", "TEXT"),
        ("發票編號", "TEXT"),
        ("發票日期", "TEXT"),
        ("單據金額", "REAL"),
        ("單據稅金", "REAL"),
        ("單據折讓", "REAL"),
        ("折扣稅額", "REAL"),
        ("附註", "TEXT"),
    };

    /// <summary>確認折讓明細表具備作業所需欄位（缺欄以 ALTER TABLE 補上；欄位已存在則無操作）。</summary>
    public static void EnsureDiscountSchema()
    {
        using var conn = DbManager.OpenConnection();
        foreach (var (col, decl) in 折讓明細補欄)
        {
            var has = ExecScalar(conn,
                "SELECT COUNT(*) FROM pragma_table_info('折讓明細') WHERE [name] = $c",
                DbManager.Param("$c", col));
            if (Convert.ToInt64(has) == 0)
                Execute(conn, $"ALTER TABLE [折讓明細] ADD COLUMN [{col}] {decl}");
        }
    }

    // ── 存檔（新增 / 修改全單重算）──
    public static SaveResult SaveDiscount(SaveDiscountRequest req)
    {
        if (req.明細.Count == 0)
            throw new InvalidOperationException("請至少輸入一筆明細。");
        if (req.明細.Any(d => d.折讓金額 <= 0))
            throw new InvalidOperationException("明細的折讓金額必須大於 0。");
        if (string.IsNullOrWhiteSpace(req.交易對象))
            throw new InvalidOperationException("請輸入交易對象。");
        if (req.帳款日期 == default)
            req.帳款日期 = req.折讓日期;

        var kind = GetKind(req.單據類別);
        decimal 稅率 = kind.TaxSource == "進項" ? TradeService.TaxRateFor(TradeService.GetKind("進貨")) : TradeService.TaxRateFor(TradeService.GetKind("出貨"));
        var totals = CalcTotals(req, 稅率);

        SaveResult? result = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            EnsureDiscountSchema();
            long 副碼 = req.單據副碼 ?? NextSeq(conn, "折讓主檔", "單據副碼");
            if (req.單據副碼 is not null)
                ReverseDiscountAccount(conn, 副碼);   // 修改：先回復舊單帳款影響
            string 單號 = req.折讓單號 ?? NextBillNo(conn, req.單據類別);
            var dup = ExecScalar(conn,
                "SELECT COUNT(*) FROM [折讓主檔] WHERE [折讓單號] = $n AND [單據副碼] <> $c",
                DbManager.Param("$n", 單號), DbManager.Param("$c", 副碼));
            if (Convert.ToInt64(dup) > 0)
                throw new InvalidOperationException($"折讓單號「{單號}」已存在，請改用其他單號。");

            InsertMaster(conn, req, 副碼, 單號, totals);
            InsertDetails(conn, req, 副碼, 單號, totals, 稅率);
            ApplyDiscountAccount(conn, req, 副碼, 單號, totals);
            result = new SaveResult(單號, 副碼);
        });
        if (result is not null)
            AuditService.Log(AuditService.存檔, "折讓", result.折讓單號, "成功",
                $"{req.單據類別}，明細 {req.明細.Count} 筆");
        return result!;
    }

    /// <summary>刪除折讓單：沖銷帳款 → 刪明細 → 刪主檔。</summary>
    public static void DeleteDiscount(long 副碼)
    {
        string? auditNo = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            var m = SelectOne(conn, "SELECT [折讓單號] FROM [折讓主檔] WHERE [單據副碼] = $c",
                DbManager.Param("$c", 副碼))
                ?? throw new InvalidOperationException("找不到該折讓單，可能已被刪除。");
            string 單號 = Str(m["折讓單號"]);
            var paid = ExecScalar(conn,
                "SELECT COUNT(*) FROM [收付明細] WHERE [單據號碼] = $n",
                DbManager.Param("$n", 單號));
            if (Convert.ToInt64(paid) > 0)
                throw new InvalidOperationException($"該折讓單已被收付款沖帳（{paid} 筆），請先沖銷收付後再刪除。");
            ReverseDiscountAccount(conn, 副碼);
            Execute(conn, "DELETE FROM [折讓明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            Execute(conn, "DELETE FROM [折讓主檔] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            auditNo = 單號;
        });
        if (auditNo is not null)
            AuditService.Log(AuditService.刪除, "折讓", auditNo, "成功");
    }

    // ==================== 主檔 / 明細 ====================

    private static void InsertMaster(SqliteConnection conn, SaveDiscountRequest req,
        long 副碼, string 單號, BillTotals totals)
    {
        string 對象簡稱 = LookupStr(conn, "SELECT [公司簡稱] FROM [客戶廠商] WHERE [客廠編號] = $o", "$o", req.交易對象);
        var vals = new Dictionary<string, object?>
        {
            ["單據類別"] = req.單據類別, ["折讓單號"] = 單號, ["單據副碼"] = 副碼,
            ["折讓日期"] = req.折讓日期.ToString("yyyy-MM-dd HH:mm:ss"),
            ["對象編號"] = Nz(req.交易對象), ["對象簡稱"] = Nz(對象簡稱),
            ["員編編號"] = Nz(req.員工編號), ["備註"] = Nz(req.備註),
            ["淨計金額"] = totals.淨計, ["稅額合計"] = totals.稅,
            ["折讓金額"] = totals.折讓, ["折扣金額"] = totals.折扣,
            ["總計金額"] = totals.總計, ["退稅"] = totals.退稅,
        };
        if (req.單據副碼 is null)
            InsertRow(conn, "折讓主檔", vals);
        else
        {
            var sets = string.Join(", ", vals.Keys.Select(k => $"[{k}] = ${k}"));
            var pars = vals.Select(kv => DbManager.Param($"${kv.Key}", kv.Value)).ToList();
            pars.Add(DbManager.Param("$c", 副碼));
            Execute(conn, $"UPDATE [折讓主檔] SET {sets} WHERE [單據副碼] = $c", pars.ToArray());
        }
    }

    private static void InsertDetails(SqliteConnection conn, SaveDiscountRequest req,
        long 副碼, string 單號, BillTotals totals, decimal 稅率)
    {
        long seq = NextSeq(conn, "折讓明細", "建檔序號");
        foreach (var d in req.明細)
        {
            decimal 折讓稅額 = RoundTax(d.折讓金額, 稅率);
            decimal 折扣稅額 = RoundTax(d.折扣金額, 稅率);
            InsertRow(conn, "折讓明細", new Dictionary<string, object?>
            {
                ["單據類別"] = req.單據類別, ["折讓單號"] = 單號, ["單據副碼"] = 副碼,
                ["建檔序號"] = seq++,
                ["折讓日期"] = req.折讓日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["對象編號"] = Nz(req.交易對象),
                ["折讓金額"] = d.折讓金額, ["折讓稅額"] = 折讓稅額,
                ["折讓貨物"] = Nz(d.貨單編號), ["折扣金額"] = d.折扣金額,
                ["貨單編號"] = Nz(d.貨單編號), ["發票編號"] = Nz(d.發票編號),
                ["發票日期"] = Nz(d.發票日期),
                ["單據金額"] = d.單據金額, ["單據稅金"] = d.單據稅金,
                ["單據折讓"] = d.折讓金額, ["折扣稅額"] = 折扣稅額,
                ["附註"] = Nz(d.附註), ["備註"] = Nz(d.附註),
            });
        }
    }

    // ==================== 帳款三層沖減（折讓一律減少應收／應付） ====================

    private static void ApplyDiscountAccount(SqliteConnection conn, SaveDiscountRequest req,
        long 副碼, string 單號, BillTotals totals)
    {
        const int dir = -1;

        // 1. 帳款主檔（每交易對象一筆彙總，負向累加；無列則建立負值列）
        var accSeq = ExecScalar(conn, "SELECT [建檔序號] FROM [帳款主檔] WHERE [交易對象] = $o",
            DbManager.Param("$o", req.交易對象));
        if (accSeq is null)
        {
            var cust = SelectOne(conn,
                "SELECT [公司全名],[統一編號],[聯絡人一],[聯絡電話一],[傳真號碼] FROM [客戶廠商] WHERE [客廠編號] = $o",
                DbManager.Param("$o", req.交易對象));
            string 員工姓名 = LookupStr(conn, "SELECT [員工姓名] FROM [員工資料] WHERE [員工編號] = $e", "$e", req.員工編號);
            InsertRow(conn, "帳款主檔", new Dictionary<string, object?>
            {
                ["建檔序號"] = NextSeq(conn, "帳款主檔", "建檔序號"),
                ["交易對象"] = req.交易對象,
                ["公司全名"] = cust is null ? null : Str(cust["公司全名"]),
                ["員工編號"] = Nz(req.員工編號), ["員工姓名"] = Nz(員工姓名),
                ["統一編號"] = cust is null ? null : Str(cust["統一編號"]),
                ["聯絡人一"] = cust is null ? null : Str(cust["聯絡人一"]),
                ["聯絡電話一"] = cust is null ? null : Str(cust["聯絡電話一"]),
                ["傳真號碼"] = cust is null ? null : Str(cust["傳真號碼"]),
                ["累計預收貨款"] = 0m, ["前期累計應收帳款"] = 0m,
                ["本期合計"] = dir * totals.淨計, ["營業稅"] = dir * totals.稅,
                ["折讓金額"] = totals.折讓, ["已收付金額"] = 0m, ["現金收付金額"] = 0m,
                ["本期總計"] = dir * totals.總計,
            });
        }
        else
        {
            Execute(conn,
                "UPDATE [帳款主檔] SET [本期合計] = [本期合計] + $h, [營業稅] = [營業稅] + $t, " +
                "[本期總計] = [本期總計] + $m, [折讓金額] = [折讓金額] + $r WHERE [建檔序號] = $i",
                DbManager.Param("$h", dir * totals.淨計), DbManager.Param("$t", dir * totals.稅),
                DbManager.Param("$m", dir * totals.總計), DbManager.Param("$r", totals.折讓),
                DbManager.Param("$i", accSeq));
        }

        // 2. 帳款簡要（每單據一筆，未收付 = 負總計）
        InsertRow(conn, "帳款簡要", new Dictionary<string, object?>
        {
            ["建檔序號"] = NextSeq(conn, "帳款簡要", "建檔序號"),
            ["單據類別"] = req.單據類別, ["交易對象"] = req.交易對象,
            ["員工編號"] = Nz(req.員工編號), ["交易日期"] = req.折讓日期.ToString("yyyy-MM-dd HH:mm:ss"),
            ["交易單號"] = 單號, ["發票號碼"] = "",
            ["合計金額"] = totals.淨計, ["營業稅"] = totals.稅, ["總計金額"] = totals.總計,
            ["折讓金額"] = totals.折讓, ["現金收付金額"] = 0m, ["已收付金額"] = 0m,
            ["未收付金額"] = dir * totals.總計, ["應收付金額"] = dir * totals.總計,
        });

        // 3. 帳款明細（每明細列一筆，供沖帳逐筆對應）
        foreach (var d in req.明細)
        {
            InsertRow(conn, "帳款明細", new Dictionary<string, object?>
            {
                ["建檔序號"] = NextSeq(conn, "帳款明細", "建檔序號"),
                ["單據類別"] = req.單據類別, ["交易對象"] = req.交易對象,
                ["員工編號"] = Nz(req.員工編號), ["交易日期"] = req.折讓日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["交易單號"] = 單號, ["發票號碼"] = Nz(d.發票編號),
                ["貨品編號"] = Nz(d.貨單編號), ["品名"] = Nz(d.附註),
                ["數量"] = 0m, ["單位"] = "", ["單價"] = 0m, ["折扣"] = 100m,
                ["金額"] = d.折讓金額, ["附註說明"] = Nz(d.附註), ["贈品"] = 0, ["服務項目"] = 0,
            });
        }
    }

    /// <summary>回復折讓帳款影響（刪除／修改共用）：沖回帳款主檔並刪除簡要／明細。</summary>
    private static void ReverseDiscountAccount(SqliteConnection conn, long 副碼)
    {
        var m = SelectOne(conn, "SELECT * FROM [折讓主檔] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼))
            ?? throw new InvalidOperationException("找不到該折讓單。");
        string 單號 = Str(m["折讓單號"]);
        string 對象 = Str(m["對象編號"]);
        decimal 淨計 = GetDec(m, "淨計金額", 0m);
        decimal 稅 = GetDec(m, "稅額合計", 0m);
        decimal 總計 = GetDec(m, "總計金額", 0m);
        decimal 折讓 = GetDec(m, "折讓金額", 0m);

        Execute(conn,
            "UPDATE [帳款主檔] SET [本期合計] = [本期合計] + $h, [營業稅] = [營業稅] + $t, " +
            "[本期總計] = [本期總計] + $m, [折讓金額] = [折讓金額] - $r WHERE [交易對象] = $o",
            DbManager.Param("$h", 淨計), DbManager.Param("$t", 稅),
            DbManager.Param("$m", 總計), DbManager.Param("$r", 折讓), DbManager.Param("$o", 對象));

        Execute(conn, "DELETE FROM [帳款簡要] WHERE [交易單號] = $n AND [單據類別] = $k",
            DbManager.Param("$n", 單號), DbManager.Param("$k", Str(m["單據類別"])));
        Execute(conn, "DELETE FROM [帳款明細] WHERE [交易單號] = $n AND [單據類別] = $k",
            DbManager.Param("$n", 單號), DbManager.Param("$k", Str(m["單據類別"])));
    }

    // ==================== 金額計算 ====================

    public readonly record struct BillTotals(decimal 淨計, decimal 折扣, decimal 稅, decimal 總計, decimal 折讓, decimal 退稅);

    /// <summary>折讓明細金額彙總：淨計 = 折讓金額合計；折扣另計；稅 = 折讓+折扣之稅。</summary>
    public static BillTotals CalcTotals(SaveDiscountRequest req, decimal 稅率)
    {
        decimal 淨計 = req.明細.Sum(d => d.折讓金額);
        decimal 折扣 = req.明細.Sum(d => d.折扣金額);
        decimal 折讓稅 = req.明細.Sum(d => RoundTax(d.折讓金額, 稅率));
        decimal 折扣稅 = req.明細.Sum(d => RoundTax(d.折扣金額, 稅率));
        decimal 稅 = 折讓稅 + 折扣稅;
        return new BillTotals(淨計, 折扣, 稅, 淨計 + 折扣 + 稅, 淨計, 稅);
    }

    private static decimal RoundTax(decimal 金額, decimal 稅率) =>
        Math.Round(金額 * 稅率 / 100m, 0, MidpointRounding.AwayFromZero);

    // ==================== 帶入查詢（畫面使用） ====================

    /// <summary>預估下一筆單號（僅供畫面顯示；正式取號以 SaveDiscount 交易內為準）。</summary>
    public static string PreviewBillNo(string kind)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = DbManager.QueryScalar(
            "SELECT MAX([折讓單號]) FROM [折讓主檔] WHERE [單據類別] = $k AND [折讓單號] LIKE $p",
            DbManager.Param("$k", kind), DbManager.Param("$p", today + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= 10 && int.TryParse(max.AsSpan(6, 4), out var last))
            seq = last + 1;
        return today + seq.ToString("0000");
    }

    /// <summary>依原交易單號帶入發票資料（供折讓明細參考）。</summary>
    public static Dictionary<string, object?>? LookupBillForDiscount(string 交易單號)
    {
        var row = DbManager.QueryTable(
            "SELECT [交易單號],[發票號碼],[總計金額],[營業稅],[交易日期] FROM [交易主檔] WHERE [交易單號] = $n LIMIT 1",
            DbManager.Param("$n", 交易單號));
        if (row.Rows.Count == 0)
            return null;
        var dict = new Dictionary<string, object?>();
        foreach (DataColumn c in row.Columns)
            dict[c.ColumnName] = row.Rows[0][c] is DBNull ? null : row.Rows[0][c];
        return dict;
    }

    // ==================== 交易內輔助 ====================

    private static string CurrentUser => Environment.UserName;

    private static long NextSeq(SqliteConnection conn, string table, string column) =>
        Convert.ToInt64(ExecScalar(conn, $"SELECT COALESCE(MAX([{column}]), 0) FROM [{table}]")) + 1;

    private static string NextBillNo(SqliteConnection conn, string kind)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = ExecScalar(conn,
            "SELECT MAX([折讓單號]) FROM [折讓主檔] WHERE [單據類別] = $k AND [折讓單號] LIKE $p",
            DbManager.Param("$k", kind), DbManager.Param("$p", today + "%")) as string;
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

    private static string LookupStr(SqliteConnection conn, string sql, string paramName, string value)
    {
        var v = ExecScalar(conn, sql, DbManager.Param(paramName, value));
        return v is null ? "" : v.ToString() ?? "";
    }

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    private static object? Nz(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static decimal GetDec(Dictionary<string, object?> d, string col, decimal def) =>
        d.TryGetValue(col, out var v) && v is not null && decimal.TryParse(v.ToString(), out var m) ? m : def;
}
