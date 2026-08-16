// ════════════════════════════════════════════════════════
// DashboardCheck：主畫面儀表板統計驗證工具
// 在「正式資料庫副本」上呼叫 DashboardService.Load()，
// 驗證統計值與直接查詢一致、無例外、欄位正常。
// 不污染正式資料庫（每次執行重新複製副本）。
// ════════════════════════════════════════════════════════
using System.Globalization;
using HeliERP.App;
using HeliERP.Data;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = System.Text.Encoding.UTF8;

const string SourceDb = @"D:\HeliAcc\HeliERP.db";

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

var d = DashboardService.Load();

// 對照：庫存不足筆數（獨立重算）
var directShort = InventoryService.LoadStock(僅不足: true);
Check("庫存不足筆數一致", d.庫存不足筆數 == directShort.Rows.Count,
    $"儀表板 {d.庫存不足筆數} = 直接查詢 {directShort.Rows.Count}");

// 對照：應收餘額（LoadObjectSummary 加總）
decimal arSum = 0m;
foreach (System.Data.DataRow r in ARService.LoadObjectSummary(ARService.應收類別).Rows)
    arSum += r.IsNull("未收付合計") ? 0m : Convert.ToDecimal(r["未收付合計"]);
Check("應收餘額一致", d.應收餘額 == arSum, $"儀表板 {d.應收餘額:N0} = 重算 {arSum:N0}");

// 對照：應付餘額
decimal apSum = 0m;
foreach (System.Data.DataRow r in ARService.LoadObjectSummary(ARService.應付類別).Rows)
    apSum += r.IsNull("未收付合計") ? 0m : Convert.ToDecimal(r["未收付合計"]);
Check("應付餘額一致", d.應付餘額 == apSum, $"儀表板 {d.應付餘額:N0} = 重算 {apSum:N0}");

// 對照：未收單據筆數（帳款簡要 > 0 且客戶）
int arOpen = Convert.ToInt32(DbManager.QueryScalar(
    "SELECT COUNT(*) FROM [帳款簡要] A JOIN [客戶廠商] C ON A.[交易對象]=C.[客廠編號] " +
    "WHERE C.[客廠類別]='客戶' AND A.[未收付金額] > 0"));
Check("未收單據筆數一致", d.未收單據筆數 == arOpen, $"儀表板 {d.未收單據筆數} = 重算 {arOpen}");

// 對照：今日出貨金額（交易主檔）
string today = DateTime.Now.ToString("yyyy-MM-dd");
decimal ship = Convert.ToDecimal(DbManager.QueryScalar(
    "SELECT COALESCE(SUM([總計金額]),0) FROM [交易主檔] WHERE [單據類別]='出貨' AND [交易日期] LIKE $p",
    DbManager.Param("$p", today + "%")));
Check("今日出貨金額一致", d.今日出貨金額 == ship, $"儀表板 {d.今日出貨金額:N0} = 重算 {ship:N0}");

// 欄位健全：庫存不足清單上限 8 筆且欄位齊全
Check("不足清單上限 8 筆", d.庫存不足清單.Rows.Count <= 8, $"清單 {d.庫存不足清單.Rows.Count} 筆");
bool colsOk = d.庫存不足清單.Columns.Contains("貨品編號") && d.庫存不足清單.Columns.Contains("品名")
    && d.庫存不足清單.Columns.Contains("現有數量") && d.庫存不足清單.Columns.Contains("安全存量");
Check("不足清單欄位齊全", colsOk, $"欄位: {string.Join(", ", d.庫存不足清單.Columns.Cast<System.Data.DataColumn>().Select(x => x.ColumnName))}");

// 全欄位非負（庫存不足筆數/單據筆數為整數）
Check("統計欄位健全",
    d.庫存不足筆數 >= 0 && d.未收單據筆數 >= 0 && d.未付單據筆數 >= 0
    && d.今日出貨筆數 >= 0 && d.本月進貨筆數 >= 0,
    $"不足 {d.庫存不足筆數}、未收 {d.未收單據筆數}、未付 {d.未付單據筆數}、出貨 {d.今日出貨筆數}、進貨 {d.本月進貨筆數}");

Console.WriteLine($"\n=== 結果：{pass} 通過 / {fail} 失敗 ===");
return fail == 0 ? 0 : 1;
