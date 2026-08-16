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
/// 交易作業核心：單據類別定義、參數、取號、存檔/刪除資料流。
/// 存檔流程（單一 BEGIN IMMEDIATE 交易，失敗全數回滾）：
/// 主檔 → 明細 → 貨品庫存扣減 → 帳款三層（主檔/簡要/明細）→ 異動快照。
/// 修改 = 先回復舊單影響，再以新值重算；刪除 = 回復 + 沖銷 + 刪除。
/// </summary>
public static class TradeService
{
    // ── 單據類別定義（依 CHM 8-2 影響矩陣：— 庫存減、┼ 庫存增、╳ 不動）──
    public sealed class TradeKind
    {
        /// <summary>單據類別（資料庫值）</summary>
        public required string Name { get; init; }
        public required string Display { get; init; }
        /// <summary>交易對象類型：客戶 / 廠商</summary>
        public required string ObjectType { get; init; }
        /// <summary>庫存影響方向：-1 減 / +1 增 / 0 不動</summary>
        public int StockDirection { get; init; }
        /// <summary>帳款影響方向：+1 應收(付)增 / -1 沖減 / 0 不動</summary>
        public int PayDirection { get; init; }
        /// <summary>稅率來源：銷項 / 進項 / 免稅</summary>
        public string TaxSource { get; init; } = "銷項";
    }

    public static readonly TradeKind[] Kinds =
    {
        new() { Name = "出貨", Display = "出貨單", ObjectType = "客戶", StockDirection = -1, PayDirection = 1, TaxSource = "銷項" },
        new() { Name = "出退", Display = "出貨退回單", ObjectType = "客戶", StockDirection = 1, PayDirection = -1, TaxSource = "銷項" },
        new() { Name = "進貨", Display = "進貨單", ObjectType = "廠商", StockDirection = 1, PayDirection = 1, TaxSource = "進項" },
        new() { Name = "進退", Display = "進貨退出單", ObjectType = "廠商", StockDirection = -1, PayDirection = -1, TaxSource = "進項" },

        // 借出借入：不動帳款（押借性質），庫存依方向增減
        new() { Name = "借出", Display = "借出單", ObjectType = "客戶", StockDirection = -1, PayDirection = 0, TaxSource = "銷項" },
        new() { Name = "借出還入", Display = "借出還入單", ObjectType = "客戶", StockDirection = 1, PayDirection = 0, TaxSource = "銷項" },
        new() { Name = "借入", Display = "借入單", ObjectType = "廠商", StockDirection = 1, PayDirection = 0, TaxSource = "銷項" },
        new() { Name = "借入還出", Display = "借入還出單", ObjectType = "廠商", StockDirection = -1, PayDirection = 0, TaxSource = "銷項" },

        // 託售／託工：寄銷與外包加工，不動帳款
        new() { Name = "託售", Display = "託售單據", ObjectType = "客戶", StockDirection = -1, PayDirection = 0, TaxSource = "銷項" },
        new() { Name = "託售回貨", Display = "託售回貨單", ObjectType = "客戶", StockDirection = 1, PayDirection = 0, TaxSource = "銷項" },
        new() { Name = "託工出庫", Display = "託工出庫", ObjectType = "廠商", StockDirection = -1, PayDirection = 0, TaxSource = "銷項" },
        new() { Name = "託工入庫", Display = "託工入庫", ObjectType = "廠商", StockDirection = 1, PayDirection = 0, TaxSource = "銷項" },

        // 調撥：倉庫間轉移（調出倉庫扣減、調入倉庫增加），不動帳款、可不填交易對象
        new() { Name = "調撥", Display = "調撥單", ObjectType = "倉庫", StockDirection = 0, PayDirection = 0, TaxSource = "銷項" },
        // 領料：倉庫領出，不動帳款
        new() { Name = "領料", Display = "領料單", ObjectType = "倉庫", StockDirection = -1, PayDirection = 0, TaxSource = "銷項" },
    };

    public static TradeKind GetKind(string name) => Kinds.FirstOrDefault(k => k.Name == name) ?? Kinds[0];

    /// <summary>交易相關參數（存檔前載入一次，交易內共用）</summary>
    public sealed class TradeParams
    {
        public int 使用多倉管理 = 1;
        public int 使用貨品批號 = 0;
        public int 使用貨品顏色 = 0;
        public int 檢查庫存量 = 0;
        public decimal 銷項稅率 = 5m;
        public decimal 進項稅率 = 5m;
        public string 常用倉庫 = "A";
    }

    public static TradeParams LoadParams()
    {
        var p = new TradeParams();
        var inv = DbManager.QueryTable("SELECT * FROM [庫存參數] WHERE [參數編號] = '0000'");
        if (inv.Rows.Count > 0)
        {
            p.使用多倉管理 = GetInt(inv.Rows[0], "使用多倉管理", 1);
            p.使用貨品批號 = GetInt(inv.Rows[0], "使用貨品批號", 0);
            p.使用貨品顏色 = GetInt(inv.Rows[0], "使用貨品顏色", 0);
            p.檢查庫存量 = GetInt(inv.Rows[0], "檢查庫存量", 0);
        }
        var sys = DbManager.QueryTable("SELECT * FROM [系統參數] WHERE [編號] = '0000'");
        if (sys.Rows.Count > 0)
        {
            p.銷項稅率 = GetDec(sys.Rows[0], "銷項稅率", 5m);
            p.進項稅率 = GetDec(sys.Rows[0], "進項稅率", 5m);
            p.常用倉庫 = GetStr(sys.Rows[0], "常用倉庫", "A");
        }
        return p;
    }

    public static decimal TaxRateFor(TradeKind kind) =>
        kind.TaxSource == "進項" ? LoadParams().進項稅率 : LoadParams().銷項稅率;

    /// <summary>單據層級試算結果（免稅時稅額為 0）</summary>
    public readonly record struct BillTotals(decimal 合計, decimal 稅, decimal 總計);

    // ==================== 存檔 / 刪除請求模型 ====================

    public sealed class DetailRow
    {
        public string 貨品編號 = "";
        public string 倉庫編號 = "";
        /// <summary>調撥時為調入倉庫（雙倉異動）</summary>
        public string 調入倉庫 = "";
        public decimal 數量;
        public string 單位 = "";
        public decimal 單價;
        public decimal 成本;
        public decimal 折扣 = 100m;
        public string 附註說明 = "";
        public bool 贈品;
        public bool 服務項目;
    }

    public sealed class SaveBillRequest
    {
        public string 單據類別 = "出貨";
        /// <summary>null = 新增（自動取號）；非 null = 修改（全單重算）</summary>
        public long? 單據副碼;
        /// <summary>null = 自動產生（YYMMDD+當日流水）</summary>
        public string? 交易單號;
        public DateTime 交易日期 = DateTime.Now;
        public DateTime 帳款日期;
        public string 交易對象 = "";
        public string 倉庫編號 = "";
        public string 員工編號 = "";
        public string 發票號碼 = "";
        public string 備註 = "";
        public List<DetailRow> 明細 = new();
    }

    public sealed record SaveResult(string 交易單號, long 單據副碼);

    // ==================== 存檔（新增 / 修改全單重算） ====================

    public static SaveResult SaveBill(SaveBillRequest req)
    {
        if (req.明細.Count == 0)
            throw new InvalidOperationException("請至少輸入一筆明細。");
        if (req.明細.Any(d => string.IsNullOrWhiteSpace(d.貨品編號) || d.數量 <= 0))
            throw new InvalidOperationException("明細的貨品編號不可空白，且數量必須大於 0。");
        var kind = GetKind(req.單據類別);
        if (kind.Name == "調撥" && req.明細.Any(d => string.IsNullOrWhiteSpace(d.調入倉庫)))
            throw new InvalidOperationException("調撥單的每筆明細都必須填寫調入倉庫。");
        if (string.IsNullOrWhiteSpace(req.交易對象) && kind.Name is not ("調撥" or "領料"))
            throw new InvalidOperationException("請輸入交易對象。");
        if (req.帳款日期 == default)
            req.帳款日期 = req.交易日期;

        var p = LoadParams();
        SaveResult? result = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            long 副碼 = req.單據副碼 ?? NextSeq(conn, "交易主檔", "單據副碼");
            if (req.單據副碼 is not null)
                ReverseEffects(conn, p, 副碼);          // 修改：先回復舊單影響（不刪主檔）
            string 單號 = req.交易單號 ?? NextBillNo(conn, req.單據類別);
            var dup = ExecScalar(conn,
                "SELECT COUNT(*) FROM [交易主檔] WHERE [交易單號] = $n AND [單據副碼] <> $c",
                DbManager.Param("$n", 單號), DbManager.Param("$c", 副碼));
            if (Convert.ToInt64(dup) > 0)
                throw new InvalidOperationException($"交易單號「{單號}」已存在，請改用其他單號。");

            string 課稅類別 = LookupStr(conn, "SELECT [課稅別] FROM [客戶廠商] WHERE [客廠編號] = $o", "$o", req.交易對象);
            string 售價稅別 = LookupStr(conn, "SELECT [售價稅別] FROM [客戶廠商] WHERE [客廠編號] = $o", "$o", req.交易對象);
            bool 免稅 = 課稅類別.Contains("免") || string.IsNullOrWhiteSpace(req.交易對象);
            decimal 稅率 = kind.TaxSource == "進項" ? p.進項稅率 : p.銷項稅率;
            var totals = CalcTotals(req, 稅率, 免稅);

            if (string.IsNullOrWhiteSpace(req.發票號碼) && kind.Name is "出貨" or "進貨")
            {
                var trackSeq = ExecScalar(conn,
                    "SELECT [序號] FROM [發票字軌] WHERE [自動配號] = 1 AND [狀態] = '啟用' " +
                    "ORDER BY [年度] DESC, [月期] DESC, [字軌] DESC LIMIT 1");
                if (trackSeq is not null)
                {
                    req.發票號碼 = InvoiceTrackService.NextInvoiceNoInTransaction(
                        conn, Convert.ToInt64(trackSeq), kind.Name, 單號);
                }
            }

            InsertMaster(conn, req, kind, 副碼, 單號, totals, 課稅類別, 售價稅別);
            InsertDetails(conn, req, 副碼);
            ApplyStockAll(conn, p, kind, req);
            if (kind.PayDirection != 0)
                ApplyAccountAll(conn, req, kind, 副碼, 單號, totals);
            InsertSnapshot(conn, req, 副碼, 單號, totals);
            result = new SaveResult(單號, 副碼);
        });
        if (result is not null)
            AuditService.Log(AuditService.存檔, "交易", result.交易單號, "成功",
                $"{req.單據類別}，明細 {req.明細.Count} 筆");
        return result!;
    }

    /// <summary>刪除交易單據：沖帳檢查 → 回復庫存/沖銷帳款 → 刪快照/明細/主檔。</summary>
    public static void DeleteBill(long 副碼)
    {
        var p = LoadParams();
        string? auditNo = null;
        string auditKind = "";
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            var m = SelectOne(conn, "SELECT [交易單號],[單據類別] FROM [交易主檔] WHERE [單據副碼] = $c",
                DbManager.Param("$c", 副碼));
            if (m is null)
                throw new InvalidOperationException("找不到該交易單據，可能已被刪除。");
            string 單號 = Str(m["交易單號"]);
            string 單別 = Str(m["單據類別"]);
            var paid = ExecScalar(conn,
                "SELECT COUNT(*) FROM [收付明細] WHERE [單據號碼] = $n AND [單別] = $k",
                DbManager.Param("$n", 單號), DbManager.Param("$k", 單別));
            if (Convert.ToInt64(paid) > 0)
                throw new InvalidOperationException($"該單據已被收付款沖帳（{paid} 筆），請先沖銷收付後再刪除。");
            ReverseEffects(conn, p, 副碼);
            Execute(conn, "DELETE FROM [交易主檔] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            auditNo = 單號;
            auditKind = 單別;
        });
        if (auditNo is not null)
            AuditService.Log(AuditService.刪除, "交易", auditNo, "成功", $"單據類別 {auditKind}");
    }

    // ==================== 主檔 / 明細 ====================

    private static void InsertMaster(SqliteConnection conn, SaveBillRequest req, TradeKind kind,
        long 副碼, string 單號, BillTotals totals, string 課稅類別, string 售價稅別)
    {
        decimal 數量合計 = req.明細.Sum(d => d.數量);
        decimal 本張成本 = req.明細.Sum(d => d.成本 * d.數量);
        decimal 未收付 = kind.PayDirection * totals.總計;
        string 調入倉庫 = req.明細.Select(d => d.調入倉庫).FirstOrDefault(w => !string.IsNullOrWhiteSpace(w)) ?? "";

        var vals = new Dictionary<string, object?>
        {
            ["單據類別"] = req.單據類別, ["交易單號"] = 單號, ["單據副碼"] = 副碼,
            ["交易日期"] = req.交易日期.ToString("yyyy-MM-dd HH:mm:ss"),
            ["交易對象"] = Nz(req.交易對象),
            ["倉庫編號"] = Nz(req.倉庫編號), ["員工編號"] = Nz(req.員工編號),
            ["調入倉庫"] = Nz(調入倉庫),
            ["發票號碼"] = req.發票號碼, ["帳款日期"] = req.帳款日期.ToString("yyyy-MM-dd HH:mm:ss"),
            ["備註"] = req.備註, ["課稅類別"] = Nz(課稅類別), ["售價稅別"] = Nz(售價稅別),
            ["計算庫存"] = 1, ["數量合計"] = 數量合計, ["合計金額"] = totals.合計,
            ["營業稅"] = totals.稅, ["總計金額"] = totals.總計, ["加項金額"] = 0m, ["減項金額"] = 0m,
            ["折讓金額"] = 0m, ["已收付金額"] = 0m, ["未收付金額"] = 未收付,
            ["應收付金額"] = 未收付, ["現金收付金額"] = 0m, ["明細總筆數"] = req.明細.Count,
            ["本張成本"] = 本張成本, ["原幣合計金額"] = totals.合計, ["原幣營業稅"] = totals.稅,
            ["原幣總計金額"] = totals.總計, ["製單"] = CurrentUser,
        };
        if (req.單據副碼 is null)
            InsertRow(conn, "交易主檔", vals);
        else
        {
            var sets = string.Join(", ", vals.Keys.Select(k => $"[{k}] = ${k}"));
            var pars = vals.Select(kv => DbManager.Param($"${kv.Key}", kv.Value)).ToList();
            pars.Add(DbManager.Param("$c", 副碼));
            Execute(conn, $"UPDATE [交易主檔] SET {sets} WHERE [單據副碼] = $c", pars.ToArray());
        }
    }

    private static void InsertDetails(SqliteConnection conn, SaveBillRequest req, long 副碼)
    {
        long seq = NextSeq(conn, "交易明細", "建檔序號");
        foreach (var d in req.明細)
        {
            decimal 金額 = CalcDetailAmount(d);
            InsertRow(conn, "交易明細", new Dictionary<string, object?>
            {
                ["單據副碼"] = 副碼, ["建檔序號"] = seq++, ["貨品編號"] = d.貨品編號,
                ["倉庫編號"] = d.倉庫編號, ["調入倉庫"] = Nz(d.調入倉庫),
                ["數量"] = d.數量, ["單位"] = Nz(d.單位),
                ["單價"] = d.單價, ["成本"] = d.成本, ["折扣"] = d.折扣, ["金額"] = 金額,
                ["附註說明"] = Nz(d.附註說明), ["贈品"] = d.贈品 ? 1 : 0,
                ["服務項目"] = d.服務項目 ? 1 : 0, ["計算庫存"] = 1,
                ["異動數量"] = d.數量, ["異動金額"] = 金額,
            });
        }
    }

    // ==================== 貨品庫存（動態 WHERE，依庫存參數開關） ====================

    private static void ApplyStockAll(SqliteConnection conn, TradeParams p, TradeKind kind, SaveBillRequest req)
    {
        foreach (var d in req.明細)
            ApplyStock(conn, p, kind, d);
    }

    private static void ApplyStock(SqliteConnection conn, TradeParams p, TradeKind kind, DetailRow d)
    {
        if (d.贈品 || d.服務項目 || d.數量 == 0)
            return;

        // 調撥：調出倉庫扣減、調入倉庫增加（雙倉異動，不套用 kind.StockDirection）
        if (kind.Name == "調撥")
        {
            if (string.IsNullOrWhiteSpace(d.調入倉庫) || string.IsNullOrWhiteSpace(d.倉庫編號))
                throw new InvalidOperationException($"調撥明細「{d.貨品編號}」必須填寫調出與調入倉庫。");
            ApplyStockOne(conn, p, d.貨品編號, d.倉庫編號, -d.數量, p.檢查庫存量 == 1);
            ApplyStockOne(conn, p, d.貨品編號, d.調入倉庫, +d.數量, false);
            return;
        }

        decimal delta = kind.StockDirection * d.數量;
        ApplyStockOne(conn, p, d.貨品編號, d.倉庫編號, delta, p.檢查庫存量 == 1 && delta < 0);
    }

    /// <summary>對單一倉庫庫存列異動：鎖定有庫存的列（現有數量大於 0 優先），必要時檢查庫存不足。</summary>
    private static void ApplyStockOne(SqliteConnection conn, TradeParams p, string 貨品編號, string 倉庫編號,
        decimal delta, bool 檢查庫存)
    {
        var where = new List<string> { "[貨品編號] = $g" };
        var pars = new List<SqliteParameter> { DbManager.Param("$g", 貨品編號) };
        if (p.使用多倉管理 == 1)
        {
            where.Add("[倉庫編號] = $w");
            pars.Add(DbManager.Param("$w", 倉庫編號));
        }
        string cond = string.Join(" AND ", where);
        var target = ExecScalar(conn,
            $"SELECT [建檔序號] FROM [貨品庫存] WHERE {cond} " +
            "ORDER BY CASE WHEN [現有數量] > 0 THEN 0 ELSE 1 END, [建檔序號] LIMIT 1",
            pars.ToArray());
        if (target is null)
            throw new InvalidOperationException($"貨品「{貨品編號}」在倉庫「{倉庫編號}」沒有庫存列，無法異動庫存。");
        if (檢查庫存)
        {
            var cur = Convert.ToDecimal(ExecScalar(conn,
                $"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [建檔序號] = $i",
                DbManager.Param("$g", 貨品編號), DbManager.Param("$i", target)));
            if (cur + delta < 0)
                throw new InvalidOperationException($"貨品「{貨品編號}」庫存不足（現有 {cur}，需 {-delta}）。");
        }
        Execute(conn,
            "UPDATE [貨品庫存] SET [現有數量] = [現有數量] + $d WHERE [貨品編號] = $g AND [建檔序號] = $i",
            DbManager.Param("$d", delta), DbManager.Param("$g", 貨品編號), DbManager.Param("$i", target));
    }

    /// <summary>回復庫存（刪除/修改時反向加回，鎖定建檔序號最小列）</summary>
    private static void RestoreStock(SqliteConnection conn, TradeParams p, TradeKind kind, DetailRow d)
    {
        if (d.贈品 || d.服務項目 || d.數量 == 0)
            return;

        // 調撥反向：調出倉庫加回、調入倉庫扣回
        if (kind.Name == "調撥")
        {
            if (!string.IsNullOrWhiteSpace(d.倉庫編號))
                ApplyStockOne(conn, p, d.貨品編號, d.倉庫編號, +d.數量, false);
            if (!string.IsNullOrWhiteSpace(d.調入倉庫))
                ApplyStockOne(conn, p, d.貨品編號, d.調入倉庫, -d.數量, false);
            return;
        }

        decimal delta = -kind.StockDirection * d.數量;
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
            return;
        Execute(conn,
            "UPDATE [貨品庫存] SET [現有數量] = [現有數量] + $d WHERE [貨品編號] = $g AND [建檔序號] = $i",
            DbManager.Param("$d", delta), DbManager.Param("$g", d.貨品編號), DbManager.Param("$i", target));
    }

    // ==================== 帳款三層 ====================

    private static void ApplyAccountAll(SqliteConnection conn, SaveBillRequest req, TradeKind kind,
        long 副碼, string 單號, BillTotals totals)
    {
        int dir = kind.PayDirection;
        if (dir == 0)
            return;   // 借出/借入/託售/託工/調撥/領料不動帳款

        // 1. 帳款主檔（每交易對象一筆彙總，僅累加本期欄位，前期欄位不動）
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
                ["本期合計"] = dir * totals.合計, ["營業稅"] = dir * totals.稅,
                ["折讓金額"] = 0m, ["已收付金額"] = 0m, ["現金收付金額"] = 0m,
                ["本期總計"] = dir * (totals.合計 + totals.稅),
            });
        }
        else
        {
            Execute(conn,
                "UPDATE [帳款主檔] SET [本期合計] = [本期合計] + $h, [營業稅] = [營業稅] + $t, " +
                "[本期總計] = [本期總計] + $m WHERE [建檔序號] = $i",
                DbManager.Param("$h", dir * totals.合計), DbManager.Param("$t", dir * totals.稅),
                DbManager.Param("$m", dir * (totals.合計 + totals.稅)), DbManager.Param("$i", accSeq));
        }

        // 2. 帳款簡要（每單據一筆，未收付/應收付 = 方向 × 總計）
        InsertRow(conn, "帳款簡要", new Dictionary<string, object?>
        {
            ["建檔序號"] = NextSeq(conn, "帳款簡要", "建檔序號"),
            ["單據類別"] = req.單據類別, ["交易對象"] = req.交易對象,
            ["員工編號"] = Nz(req.員工編號), ["交易日期"] = req.交易日期.ToString("yyyy-MM-dd HH:mm:ss"),
            ["交易單號"] = 單號, ["發票號碼"] = Nz(req.發票號碼),
            ["合計金額"] = totals.合計, ["營業稅"] = totals.稅, ["總計金額"] = totals.合計 + totals.稅,
            ["折讓金額"] = 0m, ["現金收付金額"] = 0m, ["已收付金額"] = 0m,
            ["未收付金額"] = dir * (totals.合計 + totals.稅),
            ["應收付金額"] = dir * (totals.合計 + totals.稅),
        });

        // 3. 帳款明細（每明細列一筆，供沖帳逐筆對應）
        foreach (var d in req.明細)
        {
            string 品名 = LookupStr(conn, "SELECT [品名] FROM [貨品主檔] WHERE [貨品編號] = $g", "$g", d.貨品編號);
            InsertRow(conn, "帳款明細", new Dictionary<string, object?>
            {
                ["建檔序號"] = NextSeq(conn, "帳款明細", "建檔序號"),
                ["單據類別"] = req.單據類別, ["交易對象"] = req.交易對象,
                ["員工編號"] = Nz(req.員工編號), ["交易日期"] = req.交易日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["交易單號"] = 單號, ["發票號碼"] = Nz(req.發票號碼),
                ["貨品編號"] = d.貨品編號, ["品名"] = Nz(品名),
                ["數量"] = d.數量, ["單位"] = Nz(d.單位), ["單價"] = d.單價,
                ["折扣"] = d.折扣, ["金額"] = CalcDetailAmount(d),
                ["附註說明"] = Nz(d.附註說明), ["贈品"] = d.贈品 ? 1 : 0,
                ["服務項目"] = d.服務項目 ? 1 : 0,
            });
        }
    }

    // ==================== 異動快照（每明細列一筆，供稽核與彙總表重建） ====================

    private static void InsertSnapshot(SqliteConnection conn, SaveBillRequest req, long 副碼, string 單號, BillTotals totals)
    {
        string 公司簡稱 = LookupStr(conn, "SELECT [公司簡稱] FROM [客戶廠商] WHERE [客廠編號] = $o", "$o", req.交易對象);
        long seq = NextSeq(conn, "交易異動", "建檔序號");
        foreach (var d in req.明細)
        {
            decimal 金額 = CalcDetailAmount(d);
            string 品名 = LookupStr(conn, "SELECT [品名] FROM [貨品主檔] WHERE [貨品編號] = $g", "$g", d.貨品編號);
            var snap = new Dictionary<string, object?>
            {
                ["建檔序號"] = seq++, ["單據類別"] = req.單據類別, ["交易單號"] = 單號,
                ["單據副碼"] = 副碼, ["來源副碼"] = 副碼,
                ["交易日期"] = req.交易日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["交易對象"] = req.交易對象, ["公司簡稱"] = Nz(公司簡稱),
                ["倉庫編號"] = Nz(d.倉庫編號), ["調入倉庫"] = Nz(d.調入倉庫), ["員工編號"] = Nz(req.員工編號),
                ["發票號碼"] = Nz(req.發票號碼),
                ["帳款日期"] = req.帳款日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["合計金額"] = totals.合計, ["營業稅"] = totals.稅, ["總計金額"] = totals.合計 + totals.稅,
                ["明細總筆數"] = req.明細.Count,
                ["貨品編號"] = d.貨品編號, ["批號"] = null, ["品名"] = Nz(品名),
                ["數量"] = d.數量, ["單位"] = Nz(d.單位), ["單價"] = d.單價,
                ["成本"] = d.成本, ["折扣"] = d.折扣, ["金額"] = 金額,
                ["附註說明"] = Nz(d.附註說明), ["贈品"] = d.贈品 ? 1 : 0,
                ["服務項目"] = d.服務項目 ? 1 : 0, ["計算庫存"] = 1,
                ["異動數量"] = d.數量, ["異動金額"] = 金額,
            };
            InsertRow(conn, "交易異動", snap);
            var snapDetail = new Dictionary<string, object?>(snap) { ["交易數量"] = d.數量 };
            InsertRow(conn, "異動明細", snapDetail);
        }
    }

    // ==================== 回復（刪除 / 修改共用） ====================

    private static void ReverseEffects(SqliteConnection conn, TradeParams p, long 副碼)
    {
        var m = SelectOne(conn, "SELECT * FROM [交易主檔] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼))
            ?? throw new InvalidOperationException("找不到該交易單據。");
        var kind = GetKind(Str(m["單據類別"]));
        string 單號 = Str(m["交易單號"]);
        string 對象 = Str(m["交易對象"]);

        // 1. 回復貨品庫存（反向加回）
        var details = SelectAll(conn, "SELECT * FROM [交易明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
        foreach (var d in details)
        {
            RestoreStock(conn, p, kind, new DetailRow
            {
                貨品編號 = Str(d["貨品編號"]),
                倉庫編號 = Str(d["倉庫編號"]),
                調入倉庫 = Str(d["調入倉庫"]),
                數量 = GetDec(d, "數量", 0m),
                贈品 = GetInt(d, "贈品", 0) == 1,
                服務項目 = GetInt(d, "服務項目", 0) == 1,
            });
        }

        // 2. 沖銷帳款主檔（依單據方向反向回復：出貨/進貨回復減、出退/進退回復加；不動帳款類別跳過）
        if (kind.PayDirection != 0)
        {
            decimal 合計 = GetDec(m, "合計金額", 0m);
            decimal 稅 = GetDec(m, "營業稅", 0m);
            decimal 總計 = GetDec(m, "總計金額", 0m);
            decimal 回復 = -kind.PayDirection;
            Execute(conn,
                "UPDATE [帳款主檔] SET [本期合計] = [本期合計] + $h, [營業稅] = [營業稅] + $t, " +
                "[本期總計] = [本期總計] + $m WHERE [交易對象] = $o",
                DbManager.Param("$h", 回復 * 合計), DbManager.Param("$t", 回復 * 稅),
                DbManager.Param("$m", 回復 * 總計), DbManager.Param("$o", 對象));

            // 3. 刪帳款簡要 / 明細（該單號）
            Execute(conn, "DELETE FROM [帳款簡要] WHERE [交易單號] = $n AND [單據類別] = $k",
                DbManager.Param("$n", 單號), DbManager.Param("$k", kind.Name));
            Execute(conn, "DELETE FROM [帳款明細] WHERE [交易單號] = $n AND [單據類別] = $k",
                DbManager.Param("$n", 單號), DbManager.Param("$k", kind.Name));
        }

        // 4. 刪異動快照
        Execute(conn, "DELETE FROM [交易異動] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
        Execute(conn, "DELETE FROM [異動明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));

        // 5. 刪交易明細（主檔由呼叫端處理）
        Execute(conn, "DELETE FROM [交易明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
    }

    // ==================== 金額計算 ====================

    public static decimal CalcDetailAmount(DetailRow d) =>
        Math.Round(d.數量 * d.單價 * d.折扣 / 100m, 2, MidpointRounding.AwayFromZero);

    public static BillTotals CalcTotals(SaveBillRequest req, decimal 稅率, bool 免稅)
    {
        decimal 合計 = req.明細.Sum(CalcDetailAmount);
        decimal 稅 = 免稅 ? 0m : Math.Round(合計 * 稅率 / 100m, 0, MidpointRounding.AwayFromZero);
        return new BillTotals(合計, 稅, 合計 + 稅);
    }

    // ==================== 帶入查詢（畫面使用） ====================

    public static Dictionary<string, object?>? LookupCustomerInfo(string 客廠編號, string 客廠類別)
    {
        var row = DbManager.QueryTable(
            "SELECT * FROM [客戶廠商] WHERE [客廠編號] = $o AND [客廠類別] = $t",
            DbManager.Param("$o", 客廠編號), DbManager.Param("$t", 客廠類別));
        if (row.Rows.Count == 0)
            return null;
        var dict = new Dictionary<string, object?>();
        foreach (DataColumn c in row.Columns)
            dict[c.ColumnName] = row.Rows[0][c] is DBNull ? null : row.Rows[0][c];
        return dict;
    }

    public static Dictionary<string, object?>? LookupGoodsInfo(string 貨品編號)
    {
        var row = DbManager.QueryTable(
            "SELECT [貨品編號],[品名],[規格],[基本單位],[標準售價],[最近售價],[售價A],[標準成本],[現行平均成本],[現行成本],[倉庫編號] FROM [貨品主檔] WHERE [貨品編號] = $g",
            DbManager.Param("$g", 貨品編號));
        if (row.Rows.Count == 0)
            return null;
        var dict = new Dictionary<string, object?>();
        foreach (DataColumn c in row.Columns)
            dict[c.ColumnName] = row.Rows[0][c] is DBNull ? null : row.Rows[0][c];
        return dict;
    }

    public static string? LookupStaffName(string 員工編號) =>
        DbManager.QueryScalar("SELECT [員工姓名] FROM [員工資料] WHERE [員工編號] = $e",
            DbManager.Param("$e", 員工編號)) as string;

    public static DataTable LoadCustomerCombo(string 客廠類別) =>
        DbManager.QueryTable(
            "SELECT [客廠編號], [公司簡稱] FROM [客戶廠商] WHERE [客廠類別] = $t ORDER BY [客廠編號]",
            DbManager.Param("$t", 客廠類別));

    public static DataTable LoadStaffCombo() =>
        DbManager.QueryTable("SELECT [員工編號], [員工姓名] FROM [員工資料] ORDER BY [員工編號]");

    public static DataTable LoadWarehouseCombo() =>
        DbManager.QueryTable("SELECT [倉庫編號], [倉庫名稱] FROM [倉庫資料] ORDER BY [倉庫編號]");

    /// <summary>預估下一筆單號（僅供畫面顯示；正式取號以 SaveBill 交易內為準）</summary>
    public static string PreviewBillNo(string kind)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = DbManager.QueryScalar(
            "SELECT MAX([交易單號]) FROM [交易主檔] WHERE [單據類別] = $k AND [交易單號] LIKE $p",
            DbManager.Param("$k", kind), DbManager.Param("$p", today + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= 10 && int.TryParse(max.AsSpan(6, 4), out var last))
            seq = last + 1;
        return today + seq.ToString("0000");
    }

    // ==================== 交易內輔助 ====================

    private static string CurrentUser => Environment.UserName;

    private static long NextSeq(SqliteConnection conn, string table, string column) =>
        Convert.ToInt64(ExecScalar(conn, $"SELECT COALESCE(MAX([{column}]), 0) FROM [{table}]")) + 1;

    /// <summary>產生單號：YYMMDD + 當日 4 位流水（依單據類別，格式由庫存參數「單據編號產生方式」決定）</summary>
    private static string NextBillNo(SqliteConnection conn, string kind)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = ExecScalar(conn,
            "SELECT MAX([交易單號]) FROM [交易主檔] WHERE [單據類別] = $k AND [交易單號] LIKE $p",
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

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    private static object? Nz(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static int GetInt(DataRow row, string col, int def) =>
        row.IsNull(col) || !int.TryParse(row[col].ToString(), out var v) ? def : v;

    private static decimal GetDec(DataRow row, string col, decimal def) =>
        row.IsNull(col) || !decimal.TryParse(row[col].ToString(), out var v) ? def : v;

    private static string GetStr(DataRow row, string col, string def) =>
        row.IsNull(col) ? def : row[col].ToString() ?? def;

    private static int GetInt(Dictionary<string, object?> d, string col, int def) =>
        d.TryGetValue(col, out var v) && v is not null && int.TryParse(v.ToString(), out var i) ? i : def;

    private static decimal GetDec(Dictionary<string, object?> d, string col, decimal def) =>
        d.TryGetValue(col, out var v) && v is not null && decimal.TryParse(v.ToString(), out var m) ? m : def;
}
