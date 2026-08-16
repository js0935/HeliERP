// ════════════════════════════════════════════════════════
// AdjustmentCheck：庫存調整單端到端驗證工具
// 在「正式資料庫副本」上直接呼叫 AdjustmentService 的實際程式碼路徑，
// 驗證：儲存（庫存增減/主檔/明細/快照）→ 刪除（庫存回復/資料清除）→ 庫存檢查。
// 不污染正式資料庫（每次執行重新複製副本）。
// ════════════════════════════════════════════════════════
using System.Globalization;
using HeliERP.App;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = System.Text.Encoding.UTF8;

const string SourceDb = @"D:\HeliAcc\HeliERP.db";
const string TestGoods = "CPU";
const string TestWarehouse = "A";
const decimal TestQty = 5m;

string testDb = Path.Combine(AppContext.BaseDirectory, "HeliERP.db");
if (!File.Exists(SourceDb))
{
    Console.WriteLine($"FAIL 找不到來源資料庫：{SourceDb}");
    return 1;
}
File.Copy(SourceDb, testDb, overwrite: true);
DbManager.DatabasePath = testDb;
Console.WriteLine($"已建立測試資料庫副本：{testDb}\n");

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
    if (ok) pass++; else fail++;
}

long? savedSeq = null;
try
{
    // ── 準備：取得測試貨品原庫存 ──
    decimal original = Stock(TestGoods, TestWarehouse);
    Check("準備", true, $"貨品 {TestGoods}/{TestWarehouse} 原庫存 = {original}");

    // ── T1 儲存盤盈 ──
    string no = AdjustmentService.SaveAdjustment(new AdjustmentService.AdjustmentRequest
    {
        調整日期 = DateTime.Now,
        原因 = "盤點盤盈",
        備註 = "調整單測試",
        明細 = { new AdjustmentService.AdjustmentLine { 貨品編號 = TestGoods, 倉庫編號 = TestWarehouse, 數量 = TestQty, 單位 = "個" } },
    });
    savedSeq = AdjSeqByNo(no);
    Check("T1 儲存回傳單號", !string.IsNullOrEmpty(no), $"單號 = {no}");

    decimal after1 = Stock(TestGoods, TestWarehouse);
    Check("T1 庫存 +5", after1 == original + TestQty, $"儲存後 = {after1}（期望 {original + TestQty}）");

    var master = QueryInt("SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq));
    var detail = QueryInt("SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq));
    var snap = QueryInt("SELECT COUNT(*) FROM [交易異動] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq));
    var snapDetail = QueryInt("SELECT COUNT(*) FROM [異動明細] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq));
    var amount = QueryDec("SELECT COALESCE([數量合計],0) FROM [交易主檔] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq));
    Check("T1 主檔/明細/快照寫入",
        master == 1 && detail == 1 && snap == 1 && snapDetail == 1 && amount == TestQty,
        $"主檔 {master}、明細 {detail}、交易異動 {snap}、異動明細 {snapDetail}、數量合計 {amount}");

    var list = AdjustmentService.LoadAdjustmentList();
    var found = list.Select().Any(r => Convert.ToString(r["交易單號"]) == no);
    Check("T1 調整單清單可查", found, $"清單筆數 = {list.Rows.Count}");

    // ── T2 刪除（回復庫存 + 清除資料）──
    AdjustmentService.DeleteAdjustment(savedSeq.Value);
    decimal after2 = Stock(TestGoods, TestWarehouse);
    Check("T2 庫存回復", after2 == original, $"刪除後 = {after2}（期望 {original}）");

    var left = QueryInt("SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq));
    var leftDetail = QueryInt("SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq));
    var leftSnap = QueryInt("SELECT COUNT(*) FROM [交易異動] WHERE [單據副碼] = $s", DbManager.Param("$s", savedSeq));
    Check("T2 資料全數清除", left == 0 && leftDetail == 0 && leftSnap == 0,
        $"主檔 {left}、明細 {leftDetail}、快照 {leftSnap}");
    savedSeq = null;

    // ── T3 檢查庫存量開關：盤虧超過現有應被阻擋 ──
    var before4 = QueryInt("SELECT COUNT(*) FROM [交易主檔] WHERE [單據類別] = '庫存調整'");
    DbManager.ExecuteNonQuery("UPDATE [庫存參數] SET [檢查庫存量] = 1 WHERE [參數編號] = '0000'");
    bool rejected = false;
    try
    {
        AdjustmentService.SaveAdjustment(new AdjustmentService.AdjustmentRequest
        {
            調整日期 = DateTime.Now,
            原因 = "盤點盤虧",
            明細 = { new AdjustmentService.AdjustmentLine { 貨品編號 = TestGoods, 倉庫編號 = TestWarehouse, 數量 = -99999m } },
        });
    }
    catch (InvalidOperationException ex)
    {
        rejected = ex.Message.Contains("庫存不足");
    }
    Check("T3 盤虧超量被阻擋", rejected, "檢查庫存量=1 時，盤虧超過現有數量應拋出「庫存不足」");
    DbManager.ExecuteNonQuery("UPDATE [庫存參數] SET [檢查庫存量] = 0 WHERE [參數編號] = '0000'");

    // ── T4 驗證失敗不殘留（T3 拋錯後交易應回滾）──
    var left2 = QueryInt("SELECT COUNT(*) FROM [交易主檔] WHERE [單據類別] = '庫存調整'");
    Check("T4 失敗交易回滾", left2 == before4, $"調整單 {left2} 筆（期望與執行前相同 {before4}）");

    // ── T5 同貨品同倉庫重複列示應被阻擋 ──
    bool dupRejected = false;
    try
    {
        AdjustmentService.SaveAdjustment(new AdjustmentService.AdjustmentRequest
        {
            調整日期 = DateTime.Now,
            原因 = "盤點盤盈",
            明細 =
            {
                new AdjustmentService.AdjustmentLine { 貨品編號 = TestGoods, 倉庫編號 = TestWarehouse, 數量 = 1m },
                new AdjustmentService.AdjustmentLine { 貨品編號 = TestGoods, 倉庫編號 = TestWarehouse, 數量 = 2m },
            },
        });
    }
    catch (InvalidOperationException ex)
    {
        dupRejected = ex.Message.Contains("重複");
    }
    Check("T5 重複明細被阻擋", dupRejected, "同一貨品同一倉庫重複列示應拋錯");
    var left3 = QueryInt("SELECT COUNT(*) FROM [交易主檔] WHERE [單據類別] = '庫存調整'");
    Check("T5 失敗交易回滾", left3 == before4, $"調整單 {left3} 筆（期望與執行前相同 {before4}）");

    // ── T6 異動歷史可見（InventoryService 契約）──
    string no6 = AdjustmentService.SaveAdjustment(new AdjustmentService.AdjustmentRequest
    {
        調整日期 = DateTime.Now,
        原因 = "盤點盤盈",
        明細 = { new AdjustmentService.AdjustmentLine { 貨品編號 = TestGoods, 倉庫編號 = TestWarehouse, 數量 = TestQty } },
    });
    savedSeq = AdjSeqByNo(no6);
    var moves = InventoryService.LoadMovements(TestGoods);
    var moveRow = moves.Select().FirstOrDefault(r => Convert.ToString(r["交易單號"]) == no6);
    Check("T6 異動歷史顯示調整單",
        moveRow is not null && Convert.ToDecimal(moveRow["異動數量"]) == TestQty,
        $"單號 {no6} 異動數量 = {moveRow?["異動數量"]}（期望 {TestQty}）");
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
        try { AdjustmentService.DeleteAdjustment(savedSeq.Value); }
        catch { /* 清理失敗不影響結果 */ }
    }
}

Console.WriteLine($"\n=== 結果：{pass} 通過 / {fail} 失敗 ===");
return fail == 0 ? 0 : 1;

// ── 輔助 ──
decimal Stock(string goods, string wh)
{
    var v = DbManager.QueryScalar(
        "SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = $w",
        DbManager.Param("$g", goods), DbManager.Param("$w", wh));
    return v is null ? 0m : Convert.ToDecimal(v);
}

long AdjSeqByNo(string no) => Convert.ToInt64(DbManager.QueryScalar(
    "SELECT [單據副碼] FROM [交易主檔] WHERE [單據類別] = '庫存調整' AND [交易單號] = $n",
    DbManager.Param("$n", no)));

int QueryInt(string sql, params SqliteParameter[] pars) => Convert.ToInt32(DbManager.QueryScalar(sql, pars));

decimal QueryDec(string sql, params SqliteParameter[] pars) => Convert.ToDecimal(DbManager.QueryScalar(sql, pars));
