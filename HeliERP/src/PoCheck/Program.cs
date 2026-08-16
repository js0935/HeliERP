// ════════════════════════════════════════════════════════
// PoCheck：採購訂貨作業端到端驗證工具
// 在「正式資料庫副本」上直接呼叫 PoOrderService 的實際程式碼路徑，
// 驗證：儲存（主檔/明細/金額試算）→ 修改（明細重寫）→ 刪除（資料清除）。
// 不污染正式資料庫（每次執行重新複製副本）。
// ════════════════════════════════════════════════════════
using System.Globalization;
using HeliERP.App;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = System.Text.Encoding.UTF8;

const string SourceDb = @"D:\HeliAcc\HeliERP.db";
const string TestGoods1 = "B-ET0001";
const string TestGoods2 = "B-HR0001";

string testDb = Path.Combine(AppContext.BaseDirectory, "HeliERP.db");
if (!File.Exists(SourceDb))
{
    Console.WriteLine($"FAIL 找不到來源資料庫：{SourceDb}");
    return 1;
}
File.Copy(SourceDb, testDb, overwrite: true);
DbManager.DatabasePath = testDb;
Console.WriteLine($"已建立測試資料庫副本：{testDb}\n");

// ── 前置：修正採訂主檔主鍵（單據類別 → 單據副碼，dump 併表缺陷）──
var py = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = "python",
    ArgumentList = { "fix_pobill_pk.py", "--db", testDb, "--no-backup" },
    WorkingDirectory = AppContext.BaseDirectory,
    UseShellExecute = false,
});
py!.WaitForExit();
if (py.ExitCode != 0)
{
    Console.WriteLine("FAIL 採訂主檔主鍵修正失敗");
    return 1;
}
Console.WriteLine("採訂主檔主鍵修正完成（單據副碼）\n");

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
    if (ok) pass++; else fail++;
}

long? savedSeq = null;
try
{
    // ── 準備：確認測試貨品存在 ──
    var g1 = TradeService.LookupGoodsInfo(TestGoods1);
    var g2 = TradeService.LookupGoodsInfo(TestGoods2);
    if (g1 is null || g2 is null)
    {
        Console.WriteLine($"FAIL 測試貨品不存在：{TestGoods1}/{TestGoods2}");
        return 1;
    }
    Check("準備", true, $"測試貨品 {TestGoods1}、{TestGoods2} 存在");

    // ── T1 儲存報價單（2 明細）──
    var saved = PoOrderService.SavePoBill(new PoOrderService.PoBillRequest
    {
        單據類別 = "報價",
        交易日期 = DateTime.Now,
        交貨日期 = DateTime.Now.AddDays(7),
        交易對象 = "AR-C001",
        部門編號 = "B",
        員工編號 = "001",
        課稅類別 = "外加",
        備註 = "報價單測試",
        明細 =
        {
            new PoOrderService.PoLine { 貨品編號 = TestGoods1, 倉庫編號 = "A", 數量 = 2m, 單價 = 100m, 折扣 = 100m, 單位 = "個" },
            new PoOrderService.PoLine { 貨品編號 = TestGoods2, 倉庫編號 = "A", 數量 = 3m, 單價 = 200m, 折扣 = 90m, 單位 = "台" },
        },
    });
    string no = saved.交易單號;
    savedSeq = SeqByNo(no);
    Check("T1 儲存回傳單號", !string.IsNullOrEmpty(no) && savedSeq > 0, $"單號 = {no}，副碼 = {savedSeq}");

    var (合計, 稅, 總計, 未交, 筆數) = MasterTotals(savedSeq.Value);
    decimal 期望合計 = 2 * 100 * 100 / 100m + 3 * 200 * 90 / 100m;   // 200 + 540 = 740
    decimal 期望稅 = Math.Round(期望合計 * 5m / 100m, 0, MidpointRounding.AwayFromZero);
    Check("T1 金額試算", 合計 == 期望合計 && 稅 == 期望稅 && 總計 == 期望合計 + 期望稅,
        $"合計 {合計}、稅 {稅}、總計 {總計}（期望 {期望合計}/{期望稅}/{期望合計 + 期望稅}）");
    Check("T1 未交完數/明細筆數", 未交 == 5m && 筆數 == 2, $"未交完數 {未交}、明細筆數 {筆數}");

    var details = PoOrderService.LoadPoDetails(savedSeq.Value);
    Check("T1 明細寫入", details.Rows.Count == 2 &&
        Convert.ToDecimal(details.Rows[0]["金額"]) == 200m &&
        Convert.ToDecimal(details.Rows[1]["金額"]) == 540m,
        $"明細 {details.Rows.Count} 筆，金額 200 / 540");

    var list = PoOrderService.LoadPoList("報價");
    var found = list.Select().Any(r => Convert.ToString(r["交易單號"]) == no && Convert.ToString(r["對象名稱"]).Length > 0);
    Check("T1 單據清單可查", found, $"清單筆數 = {list.Rows.Count}");

    // ── T2 修改：明細重寫，交易數量保留 ──
    var saved2 = PoOrderService.SavePoBill(new PoOrderService.PoBillRequest
    {
        單據類別 = "報價",
        單據副碼 = savedSeq,
        交易日期 = DateTime.Now,
        交貨日期 = DateTime.Now.AddDays(7),
        交易對象 = "AR-C001",
        課稅類別 = "外加",
        備註 = "報價單測試（修改）",
        明細 =
        {
            new PoOrderService.PoLine { 貨品編號 = TestGoods1, 倉庫編號 = "A", 數量 = 5m, 單價 = 100m, 折扣 = 100m, 單位 = "個" },
        },
    });
    string no2 = saved2.交易單號;
    var details2 = PoOrderService.LoadPoDetails(savedSeq.Value);
    decimal 金額2 = Convert.ToDecimal(details2.Rows[0]["金額"]);
    Check("T2 修改明細重寫", no2 == no && details2.Rows.Count == 1 && 金額2 == 500m,
        $"單號保留 {no2 == no}、明細 {details2.Rows.Count} 筆、金額 {金額2}");

    // ── T3 四種單據類別皆可儲存 ──
    bool allKindsOk = true;
    foreach (var k in PoOrderService.Kinds)
    {
        var o = k.ObjectType == "客戶" ? "AR-C001" : "AR-V001";
        var n = PoOrderService.SavePoBill(new PoOrderService.PoBillRequest
        {
            單據類別 = k.Name,
            交易對象 = o,
            課稅類別 = "外加",
            明細 = { new PoOrderService.PoLine { 貨品編號 = TestGoods1, 數量 = 1m, 單價 = 50m, 折扣 = 100m } },
        });
        if (string.IsNullOrEmpty(n.交易單號)) allKindsOk = false;
        try { PoOrderService.DeletePoBill(n.單據副碼); }
        catch { allKindsOk = false; }
    }
    Check("T3 四類單據儲存/刪除", allKindsOk, string.Join("、", PoOrderService.Kinds.Select(k => k.Name)));

    // ── T4 驗證失敗不殘留（空明細、無對象）──
    bool emptyRejected = false, objRejected = false;
    try
    {
        PoOrderService.SavePoBill(new PoOrderService.PoBillRequest { 單據類別 = "報價", 交易對象 = "AR-C001", 課稅類別 = "外加" });
    }
    catch (InvalidOperationException ex) { emptyRejected = ex.Message.Contains("明細"); }
    try
    {
        PoOrderService.SavePoBill(new PoOrderService.PoBillRequest
        {
            單據類別 = "報價", 交易對象 = "",
            明細 = { new PoOrderService.PoLine { 貨品編號 = TestGoods1, 數量 = 1m, 單價 = 10m, 折扣 = 100m } },
        });
    }
    catch (InvalidOperationException ex) { objRejected = ex.Message.Contains("對象"); }
    Check("T4 空明細被阻擋", emptyRejected, "無明細應拋出「請至少輸入一筆明細」");
    Check("T4 無對象被阻擋", objRejected, "無交易對象應拋出「請輸入交易對象」");
    var before = QueryInt("SELECT COUNT(*) FROM [採訂主檔]");
    Check("T4 失敗交易回滾", before == 2, $"採訂主檔殘留 {before} 筆（期望 2：T1+T2）");

    // ── T5 刪除清除資料 ──
    PoOrderService.DeletePoBill(savedSeq.Value);
    var leftMaster = QueryInt("SELECT COUNT(*) FROM [採訂主檔] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq.Value));
    var leftDetail = QueryInt("SELECT COUNT(*) FROM [採訂明細] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq.Value));
    Check("T5 刪除清除資料", leftMaster == 0 && leftDetail == 0, $"主檔 {leftMaster}、明細 {leftDetail}");
    savedSeq = null;

    // ── T6 預估單號格式 ──
    string preview = PoOrderService.PreviewPoNo("報價");
    Check("T6 預估單號格式", preview.Length == 10 && preview.StartsWith(DateTime.Now.ToString("yyMMdd")), $"預估單號 = {preview}");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL  未預期例外：{ex.GetType().Name} {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    fail++;
}
finally
{
    if (savedSeq is not null)
    {
        try { PoOrderService.DeletePoBill(savedSeq.Value); }
        catch { /* 清理失敗不影響結果 */ }
    }
}

Console.WriteLine($"\n=== 結果：{pass} 通過 / {fail} 失敗 ===");
return fail == 0 ? 0 : 1;

// ── 輔助 ──
long SeqByNo(string no) => Convert.ToInt64(DbManager.QueryScalar(
    "SELECT [單據副碼] FROM [採訂主檔] WHERE [交易單號] = $n",
    DbManager.Param("$n", no)));

(decimal 合計, decimal 稅, decimal 總計, decimal 未交, int 筆數) MasterTotals(long seq)
{
    var v = DbManager.QueryTable(
        "SELECT COALESCE([合計金額],0) AS [合計金額], COALESCE([營業稅],0) AS [營業稅], " +
        "COALESCE([總計金額],0) AS [總計金額], COALESCE([未交完數],0) AS [未交完數], " +
        "COALESCE([明細筆數],0) AS [明細筆數] FROM [採訂主檔] WHERE [單據副碼] = $s",
        DbManager.Param("$s", seq));
    var r = v.Rows[0];
    return (Convert.ToDecimal(r["合計金額"]), Convert.ToDecimal(r["營業稅"]),
        Convert.ToDecimal(r["總計金額"]), Convert.ToDecimal(r["未交完數"]), Convert.ToInt32(r["明細筆數"]));
}

int QueryInt(string sql, params SqliteParameter[] pars) => Convert.ToInt32(DbManager.QueryScalar(sql, pars));
