// ════════════════════════════════════════════════════════
// PayCheck：收付（沖帳/折讓/預收）端到端驗證工具
// 在「真實資料庫副本」上直接呼叫 PaymentService 實際程式碼路徑，
// 驗證：沖帳+折讓、純累入預收、取用預收、超額拒絕、刪除撤銷、回歸。
// 不污染正式資料庫（副本 + 測試資料前後清理）。
// ════════════════════════════════════════════════════════
using System.Data;
using System.Globalization;
using HeliERP.App;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = System.Text.Encoding.UTF8;

const string SourceDb = @"D:\HeliAcc\HeliERP.db";
const string TestObject = "PAYCHECK-001";

string testDb = Path.Combine(AppContext.BaseDirectory, "HeliERP.db");
if (!File.Exists(testDb) || new FileInfo(testDb).Length == 0)
{
    if (!File.Exists(SourceDb))
    {
        Console.WriteLine($"FAIL 找不到來源資料庫：{SourceDb}");
        return 1;
    }
    File.Copy(SourceDb, testDb, overwrite: true);
    Console.WriteLine($"已建立測試資料庫副本：{testDb}");
}
DbManager.DatabasePath = testDb;

Cleanup();
SetupFixture();

long 副碼P1 = 0, 副碼P2 = 0, 副碼P3 = 0;

int pass = 0, fail = 0;
var results = new List<(string Name, bool Ok, string Detail)>();
results.Add(RunCase("P1 沖帳+折讓", TestP1));
results.Add(RunCase("P2 純累入預收", TestP2));
results.Add(RunCase("P3 取用預收沖帳", TestP3));
results.Add(RunCase("P4 取用預收超額拒絕", TestP4));
results.Add(RunCase("P5 LookupPrepaidBalance", TestP5));
results.Add(RunCase("P6 刪除撤銷（折讓/取用/累入）", TestP6));
results.Add(RunCase("P7 回歸：現金+票據沖帳與撤銷", TestP7));

Cleanup();

foreach (var (name, ok, detail) in results)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
    if (ok) pass++; else fail++;
}
Console.WriteLine($"\n=== 總計: {pass} PASS / {fail} FAIL ===");
return fail == 0 ? 0 : 1;

// ==================== 測試案例 ====================

// P1：SO-001 全額沖 1000；SO-002 沖 1400 + 折讓 600；現金 2400
string TestP1()
{
    var open = PaymentService.LoadOpenBills(TestObject);
    if (open.Rows.Count != 3) throw new Exception($"LoadOpenBills 應 3 列，實際 {open.Rows.Count}");
    if (!open.Columns.Contains("折讓金額")) throw new Exception("LoadOpenBills 缺折讓金額欄");
    var 未收合計 = open.Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["未收付金額"]));
    if (未收合計 != 6000m) throw new Exception($"未收付合計應 6000，實際 {未收合計}");

    var r = PaymentService.SavePayment(new PaymentService.SavePaymentRequest
    {
        收付類別 = "收款",
        沖帳日期 = DateTime.Now,
        沖帳對象 = TestObject,
        現金金額 = 2400m,
        明細 = new List<PaymentService.PaymentDetailRow>
        {
            new() { 交易單號 = "PCHK-SO-001", 單據類別 = "出貨", 交易日期 = "2026-08-01", 未收付金額 = 1000m, 沖帳金額 = 1000m, 折讓金額 = 0m },
            new() { 交易單號 = "PCHK-SO-002", 單據類別 = "出貨", 交易日期 = "2026-08-02", 未收付金額 = 2000m, 沖帳金額 = 1400m, 折讓金額 = 600m },
        },
    });
    副碼P1 = r.單據副碼;

    AssertBill("PCHK-SO-001", 1000m, 0m);
    AssertBill("PCHK-SO-002", 1400m, 0m);
    AssertLedger(2400m, 600m, 0m);
    var 銷貨折讓 = GetDec("SELECT 銷貨折讓 FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", 副碼P1));
    var 沖帳合計 = GetDec("SELECT 沖帳合計 FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", 副碼P1));
    var 現金 = GetDec("SELECT 現金金額 FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", 副碼P1));
    if (銷貨折讓 != 600m || 沖帳合計 != 2400m || 現金 != 2400m)
        throw new Exception($"收付主檔錯誤：折讓 {銷貨折讓}/合計 {沖帳合計}/現金 {現金}");
    var 明細數 = (long)(DbManager.QueryScalar("SELECT COUNT(*) FROM 收付明細 WHERE 單據副碼 = $c", DbManager.Param("$c", 副碼P1)) ?? 0L);
    if (明細數 != 2) throw new Exception($"收付明細應 2 筆，實際 {明細數}");
    return $"沖帳 2400 + 折讓 600（SO-002 折 600）四表同步正確";
}

// P2：純累入預收 5000（無明細，現金 = 累入預收）
string TestP2()
{
    var r = PaymentService.SavePayment(new PaymentService.SavePaymentRequest
    {
        收付類別 = "收款",
        沖帳對象 = TestObject,
        現金金額 = 5000m,
        累入預收 = 5000m,
        明細 = new(),
    });
    副碼P2 = r.單據副碼;

    var 預收 = GetDec("SELECT 累計預收貨款 FROM 帳款主檔 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    if (預收 != 5000m) throw new Exception($"累計預收貨款應 5000，實際 {預收}");
    var 累入 = GetDec("SELECT 累入預收 FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", 副碼P2));
    var 合計 = GetDec("SELECT 沖帳合計 FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", 副碼P2));
    if (累入 != 5000m || 合計 != 0m) throw new Exception($"純預收單錯誤：累入 {累入}/合計 {合計}");
    return $"純累入預收 5000 成功（累計預收貨款 = 5000）";
}

// P3：取用預收 1000 + 現金 2000 沖 SO-003（3000）
string TestP3()
{
    var r = PaymentService.SavePayment(new PaymentService.SavePaymentRequest
    {
        收付類別 = "收款",
        沖帳對象 = TestObject,
        現金金額 = 2000m,
        取用預收 = 1000m,
        明細 = new List<PaymentService.PaymentDetailRow>
        {
            new() { 交易單號 = "PCHK-SO-003", 單據類別 = "出貨", 交易日期 = "2026-08-03", 未收付金額 = 3000m, 沖帳金額 = 3000m },
        },
    });
    副碼P3 = r.單據副碼;

    AssertBill("PCHK-SO-003", 3000m, 0m);
    var 預收 = GetDec("SELECT 累計預收貨款 FROM 帳款主檔 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    if (預收 != 4000m) throw new Exception($"取用預收 1000 後餘額應 4000，實際 {預收}");
    var 取用 = GetDec("SELECT 取用預收 FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", 副碼P3));
    if (取用 != 1000m) throw new Exception($"收付主檔取用預收應 1000，實際 {取用}");
    var 已收付 = GetDec("SELECT 已收付金額 FROM 帳款主檔 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    if (已收付 != 5400m) throw new Exception($"帳款主檔已收付應 5400（2400+3000），實際 {已收付}");
    return $"取用預收 1000 + 現金 2000 沖 SO-003 成功（預收餘額 5000→4000）";
}

// P4：取用預收 5000 超過餘額 4000 → 應拒絕且資料回滾
string TestP4()
{
    bool thrown = false;
    try
    {
        PaymentService.SavePayment(new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款",
            沖帳對象 = TestObject,
            現金金額 = 0m,
            取用預收 = 5000m,
            明細 = new List<PaymentService.PaymentDetailRow>
            {
                new() { 交易單號 = "PCHK-SO-001", 單據類別 = "出貨", 交易日期 = "2026-08-01", 未收付金額 = 1000m, 沖帳金額 = 1000m },
            },
        });
    }
    catch (InvalidOperationException) { thrown = true; }
    if (!thrown) throw new Exception("取用預收超額未被拒絕");
    AssertBill("PCHK-SO-001", 1000m, 0m);
    var 預收 = GetDec("SELECT 累計預收貨款 FROM 帳款主檔 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    if (預收 != 4000m) throw new Exception($"拒絕後預收餘額應仍 4000，實際 {預收}");
    var 收付數 = (long)(DbManager.QueryScalar("SELECT COUNT(*) FROM 收付主檔 WHERE 沖帳對象 = $o", DbManager.Param("$o", TestObject)) ?? 0L);
    if (收付數 != 3) throw new Exception($"拒絕後收付單應仍 3 張，實際 {收付數}");
    return "取用預收 5000 > 餘額 4000 被拒絕，交易回滾資料未變";
}

// P5：公開預收餘額查詢
string TestP5()
{
    var 餘額 = PaymentService.LookupPrepaidBalance(TestObject);
    if (餘額 != 4000m) throw new Exception($"LookupPrepaidBalance 應 4000，實際 {餘額}");
    return $"LookupPrepaidBalance = {餘額:N2} 正確";
}

// P6：依序刪除 P1（折讓）→ P3（取用）→ P2（累入），驗證逐步回復
string TestP6()
{
    PaymentService.DeletePayment(副碼P1);
    AssertBill("PCHK-SO-001", 0m, 1000m);
    AssertBill("PCHK-SO-002", 0m, 2000m);
    AssertLedger(3000m, 0m, 4000m);
    if (收付存在(副碼P1)) throw new Exception("P1 收付主檔未刪除");

    PaymentService.DeletePayment(副碼P3);
    AssertBill("PCHK-SO-003", 0m, 3000m);
    AssertLedger(0m, 0m, 5000m);
    if (收付存在(副碼P3)) throw new Exception("P3 收付主檔未刪除");

    PaymentService.DeletePayment(副碼P2);
    AssertLedger(0m, 0m, 0m);
    if (收付存在(副碼P2)) throw new Exception("P2 收付主檔未刪除");
    return "刪除撤銷正確：折讓回復、取用預收回復、累入預收回復，四表歸零";
}

// P7：回歸——現金 + 票據沖帳（無折讓/預收），再刪除回復
string TestP7()
{
    var r = PaymentService.SavePayment(new PaymentService.SavePaymentRequest
    {
        收付類別 = "收款",
        沖帳對象 = TestObject,
        現金金額 = 2600m,
        票據金額 = 400m,
        明細 = new List<PaymentService.PaymentDetailRow>
        {
            new() { 交易單號 = "PCHK-SO-001", 單據類別 = "出貨", 交易日期 = "2026-08-01", 未收付金額 = 1000m, 沖帳金額 = 1000m },
            new() { 交易單號 = "PCHK-SO-002", 單據類別 = "出貨", 交易日期 = "2026-08-02", 未收付金額 = 2000m, 沖帳金額 = 2000m },
        },
    });
    AssertBill("PCHK-SO-001", 1000m, 0m);
    AssertBill("PCHK-SO-002", 2000m, 0m);
    AssertLedger(3000m, 0m, 0m);
    var 現金 = GetDec("SELECT 現金金額 FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", r.單據副碼));
    var 票據 = GetDec("SELECT 票據金額 FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", r.單據副碼));
    if (現金 != 2600m || 票據 != 400m) throw new Exception($"現金/票據應 2600/400，實際 {現金}/{票據}");

    PaymentService.DeletePayment(r.單據副碼);
    AssertBill("PCHK-SO-001", 0m, 1000m);
    AssertBill("PCHK-SO-002", 0m, 2000m);
    AssertLedger(0m, 0m, 0m);
    return "現金+票據沖帳與刪除撤銷回歸通過";
}

// ==================== 工具函式 ====================

(string Name, bool Ok, string Detail) RunCase(string name, Func<string> test)
{
    try { return (name, true, test()); }
    catch (Exception ex) { return (name, false, $"{ex.GetType().Name}: {ex.Message}"); }
}

void SetupFixture()
{
    DbManager.ExecuteNonQuery("INSERT INTO 客戶廠商 (客廠類別, 客廠編號, 公司簡稱) VALUES ('客戶', $o, '預收測試客戶')",
        DbManager.Param("$o", TestObject));
    DbManager.ExecuteNonQuery("INSERT INTO 帳款主檔 (交易對象, 累計預收貨款, 折讓金額, 已收付金額, 本期總計) VALUES ($o, 0, 0, 0, 0)",
        DbManager.Param("$o", TestObject));
    InsertBill("PCHK-SO-001", "2026-08-01", 1000m);
    InsertBill("PCHK-SO-002", "2026-08-02", 2000m);
    InsertBill("PCHK-SO-003", "2026-08-03", 3000m);
}

void InsertBill(string 單號, string 日期, decimal 金額)
{
    DbManager.ExecuteNonQuery(
        "INSERT INTO 交易主檔 (單據類別, 交易單號, 交易日期, 交易對象, 總計金額, 已收付金額, 未收付金額, 應收付金額) " +
        "VALUES ('出貨', $n, $d, $o, $a, 0, $a, $a)",
        DbManager.Param("$n", 單號), DbManager.Param("$d", 日期), DbManager.Param("$o", TestObject), DbManager.Param("$a", 金額));
    DbManager.ExecuteNonQuery(
        "INSERT INTO 帳款簡要 (單據類別, 交易對象, 交易日期, 交易單號, 總計金額, 已收付金額, 未收付金額, 應收付金額) " +
        "VALUES ('出貨', $o, $d, $n, $a, 0, $a, $a)",
        DbManager.Param("$n", 單號), DbManager.Param("$d", 日期), DbManager.Param("$o", TestObject), DbManager.Param("$a", 金額));
}

void Cleanup()
{
    DbManager.ExecuteNonQuery("DELETE FROM 收付明細 WHERE 單據副碼 IN (SELECT 單據副碼 FROM 收付主檔 WHERE 沖帳對象 = $o)",
        DbManager.Param("$o", TestObject));
    DbManager.ExecuteNonQuery("DELETE FROM 收付主檔 WHERE 沖帳對象 = $o", DbManager.Param("$o", TestObject));
    DbManager.ExecuteNonQuery("DELETE FROM 帳款簡要 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    DbManager.ExecuteNonQuery("DELETE FROM 交易主檔 WHERE 交易單號 LIKE 'PCHK-%'");
    DbManager.ExecuteNonQuery("DELETE FROM 帳款主檔 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    DbManager.ExecuteNonQuery("DELETE FROM 客戶廠商 WHERE 客廠編號 = $o", DbManager.Param("$o", TestObject));
}

decimal GetDec(string sql, params SqliteParameter[] pars) =>
    Convert.ToDecimal(DbManager.QueryScalar(sql, pars) ?? 0m);

bool 收付存在(long 副碼) =>
    (long)(DbManager.QueryScalar("SELECT COUNT(*) FROM 收付主檔 WHERE 單據副碼 = $c", DbManager.Param("$c", 副碼)) ?? 0L) > 0;

void AssertBill(string 單號, decimal 已收付, decimal 未收付)
{
    var a已收 = GetDec("SELECT 已收付金額 FROM 帳款簡要 WHERE 交易單號 = $n", DbManager.Param("$n", 單號));
    var a未收 = GetDec("SELECT 未收付金額 FROM 帳款簡要 WHERE 交易單號 = $n", DbManager.Param("$n", 單號));
    if (a已收 != 已收付 || a未收 != 未收付)
        throw new Exception($"帳款簡要 {單號} 應 {已收付}/{未收付}，實際 {a已收}/{a未收}");
    var t已收 = GetDec("SELECT 已收付金額 FROM 交易主檔 WHERE 交易單號 = $n", DbManager.Param("$n", 單號));
    var t未收 = GetDec("SELECT 未收付金額 FROM 交易主檔 WHERE 交易單號 = $n", DbManager.Param("$n", 單號));
    if (t已收 != 已收付 || t未收 != 未收付)
        throw new Exception($"交易主檔 {單號} 應 {已收付}/{未收付}，實際 {t已收}/{t未收}");
}

void AssertLedger(decimal 已收付, decimal 折讓, decimal 累計預收)
{
    var a = GetDec("SELECT 已收付金額 FROM 帳款主檔 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    var b = GetDec("SELECT 折讓金額 FROM 帳款主檔 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    var c = GetDec("SELECT 累計預收貨款 FROM 帳款主檔 WHERE 交易對象 = $o", DbManager.Param("$o", TestObject));
    if (a != 已收付 || b != 折讓 || c != 累計預收)
        throw new Exception($"帳款主檔應 已收付 {已收付}/折讓 {折讓}/預收 {累計預收}，實際 {a}/{b}/{c}");
}
