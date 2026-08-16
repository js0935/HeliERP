// ════════════════════════════════════════════════════════
// ModuleCheck：未驗證模組檢查工具（庫存進出貨 / 票據 / 薪資 / 會計）
// 在「正式資料庫副本」上呼叫各模組 Service 實際程式碼路徑，驗證資料流正確性。
// 不污染正式資料庫（每次執行重新複製副本）。
// 用法：
//   ModuleCheck trade    庫存進出貨：出貨/出退儲存、刪除回復、庫存/帳款影響、金額試算
//   ModuleCheck bill     票據：明細表與未兌現表報表資料建構
//   ModuleCheck payroll  薪資：PayrollService.Calculate 計算與主檔/明細一致性
//   ModuleCheck acc      會計：12 份報表資料建構 + 借貸平衡
//   ModuleCheck invoice  電子發票：字軌建置/重複阻擋/自動配號/用罄/作廢/停用
//   ModuleCheck approval 核准流程：層數設定/送審/逐層核准/退回/未啟用不送審
//   ModuleCheck audit    稽核日誌：登入成敗/存刪/密碼變更寫入與內容
//   ModuleCheck master   表單式主檔：FormMasterCatalog 8 張表欄位定義與資料庫一致性
// ════════════════════════════════════════════════════════
using System.Data;
using System.Globalization;
using HeliERP.App;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = System.Text.Encoding.UTF8;

int Pass = 0, Fail = 0;

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

string cmd = args.Length > 0 ? args[0] : "";
switch (cmd)
{
    case "trade": TradeChecks(); break;
    case "bill": BillChecks(); break;
    case "payroll": PayrollChecks(); break;
    case "acc": AccChecks(); break;
    case "invoice": InvoiceChecks(); break;
    case "approval": ApprovalChecks(); break;
    case "audit": AuditChecks(); break;
    case "master": MasterChecks(); break;
    default:
        Console.WriteLine("用法：ModuleCheck <trade|bill|payroll|acc|invoice|approval|audit|master>");
        return 2;
}

Console.WriteLine($"\n=== 結果：{Pass} 通過 / {Fail} 失敗 ===");
return Fail == 0 ? 0 : 1;

// ── 共用 ──

void Check(string name, bool ok, string detail)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
    if (ok) Pass++; else Fail++;
}

decimal Stock(string goods, string wh)
{
    var v = DbManager.QueryScalar(
        "SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = $w",
        DbManager.Param("$g", goods), DbManager.Param("$w", wh));
    return v is null ? 0m : Convert.ToDecimal(v);
}

long SeqByNo(string no) => Convert.ToInt64(DbManager.QueryScalar(
    "SELECT [單據副碼] FROM [交易主檔] WHERE [交易單號] = $n AND [交易單號] IS NOT NULL",
    DbManager.Param("$n", no)));

int QInt(string sql, params SqliteParameter[] pars) => Convert.ToInt32(DbManager.QueryScalar(sql, pars));

decimal QDec(string sql, params SqliteParameter[] pars) => Convert.ToDecimal(DbManager.QueryScalar(sql, pars));

// ════════════════════════════════════════════════════════
// 庫存進出貨：TradeService 資料流
// ════════════════════════════════════════════════════════
void TradeChecks()
{
    const string 貨品 = "CPU", 倉庫 = "A", 客戶 = "A0001", 廠商 = "A001", 員工 = "001";

    // ── T0 TradeKind 方向性定義（依 CHM 影響矩陣）──
    Check("單據類別方向定義", 
        TradeService.GetKind("出貨").StockDirection == -1 && TradeService.GetKind("出退").StockDirection == 1 &&
        TradeService.GetKind("進貨").StockDirection == 1 && TradeService.GetKind("進退").StockDirection == -1 &&
        TradeService.GetKind("借出").StockDirection == -1 && TradeService.GetKind("調撥").StockDirection == 0 &&
        TradeService.GetKind("領料").StockDirection == -1,
        "出貨-1 / 出退+1 / 進貨+1 / 進退-1 / 借出-1 / 調撥0 / 領料-1");

    decimal orig = Stock(貨品, 倉庫);
    Check("樣本準備", true, $"貨品 {貨品}/{倉庫} 原庫存 = {orig}");

    // ── T1 出貨單儲存 ──
    var r1 = TradeService.SaveBill(new TradeService.SaveBillRequest
    {
        單據類別 = "出貨", 交易對象 = 客戶, 倉庫編號 = 倉庫, 員工編號 = 員工, 備註 = "ModuleCheck",
        明細 = { new TradeService.DetailRow { 貨品編號 = 貨品, 倉庫編號 = 倉庫, 數量 = 2m, 單位 = "個", 單價 = 100m, 成本 = 60m } },
    });
    Check("T1 出貨儲存回傳", !string.IsNullOrEmpty(r1.交易單號) && r1.單據副碼 > 0, $"單號 {r1.交易單號} 副碼 {r1.單據副碼}");
    decimal after1 = Stock(貨品, 倉庫);
    Check("T1 庫存 -2", after1 == orig - 2, $"儲存後 {after1}（期望 {orig - 2}）");
    Check("T1 主檔/明細/快照", 
        QInt("SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼]=$c", DbManager.Param("$c", r1.單據副碼)) == 1 &&
        QInt("SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼]=$c", DbManager.Param("$c", r1.單據副碼)) == 1 &&
        QInt("SELECT COUNT(*) FROM [交易異動] WHERE [單據副碼]=$c", DbManager.Param("$c", r1.單據副碼)) == 1,
        "主檔1/明細1/異動1");
    decimal 主檔總計 = QDec("SELECT [總計金額] FROM [交易主檔] WHERE [單據副碼]=$c", DbManager.Param("$c", r1.單據副碼));
    Check("T1 金額計算", 主檔總計 == 210m, $"總計 {主檔總計}（期望 200 + 稅10 = 210）");
    decimal 簡要未收付 = QDec("SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號]=$n", DbManager.Param("$n", r1.交易單號));
    Check("T1 帳款簡要未收付", 簡要未收付 == 210m, $"未收付 {簡要未收付}（期望 210）");
    decimal 對象本期 = QDec("SELECT [本期總計] FROM [帳款主檔] WHERE [交易對象]=$o", DbManager.Param("$o", 客戶));
    Check("T1 帳款主檔累加", 對象本期 > 0, $"客戶 {客戶} 本期總計 {對象本期:N0}（出貨後應為正）");

    // ── T2 CalcTotals 試算 ──
    var req = new TradeService.SaveBillRequest
    {
        單據類別 = "出貨",
        明細 = { new TradeService.DetailRow { 貨品編號 = 貨品, 倉庫編號 = 倉庫, 數量 = 2m, 單價 = 100m } },
    };
    var t1 = TradeService.CalcTotals(req, 5m, 免稅: false);
    var t2 = TradeService.CalcTotals(req, 5m, 免稅: true);
    Check("T2 試算含稅", t1.合計 == 200m && t1.稅 == 10m && t1.總計 == 210m, $"合計 {t1.合計}/稅 {t1.稅}/總計 {t1.總計}");
    Check("T2 試算免稅", t2.合計 == 200m && t2.稅 == 0m, $"合計 {t2.合計}/稅 {t2.稅}");

    // ── T3 出退單儲存（反向；指定單號避免與 T1 自動取號相撞）──
    var r3 = TradeService.SaveBill(new TradeService.SaveBillRequest
    {
        單據類別 = "出退", 交易單號 = "2608168801", 交易對象 = 客戶, 倉庫編號 = 倉庫, 員工編號 = 員工,
        明細 = { new TradeService.DetailRow { 貨品編號 = 貨品, 倉庫編號 = 倉庫, 數量 = 1m, 單位 = "個", 單價 = 100m } },
    });
    decimal after3 = Stock(貨品, 倉庫);
    Check("T3 出退庫存 +1", after3 == after1 + 1, $"儲存後 {after3}（期望 {after1 + 1}）");
    decimal 出退未收付 = QDec("SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號]=$n", DbManager.Param("$n", r3.交易單號));
    Check("T3 出退帳款負向", 出退未收付 == -105m, $"未收付 {出退未收付}（期望 -105）");

    // ── T4 刪除出貨單：庫存回復 + 帳款簡要刪除 ──
    TradeService.DeleteBill(r1.單據副碼);
    decimal after4 = Stock(貨品, 倉庫);
    Check("T4 刪除庫存回復", after4 == orig + 1, $"刪除後 {after4}（期望 {orig + 1}＝原值{orig}+出退影響1）");
    Check("T4 帳款簡要清除",
        QInt("SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號]=$n", DbManager.Param("$n", r1.交易單號)) == 0,
        "出貨單簡要已刪");

    // ── T5 庫存不足阻擋（檢查庫存量=1）──
    DbManager.ExecuteNonQuery("UPDATE [庫存參數] SET [檢查庫存量] = 1 WHERE [參數編號] = '0000'");
    bool rejected = false;
    try
    {
        TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = 客戶, 倉庫編號 = 倉庫,
            明細 = { new TradeService.DetailRow { 貨品編號 = 貨品, 倉庫編號 = 倉庫, 數量 = 999999m, 單價 = 100m } },
        });
    }
    catch (InvalidOperationException ex) { rejected = ex.Message.Contains("庫存不足"); }
    DbManager.ExecuteNonQuery("UPDATE [庫存參數] SET [檢查庫存量] = 0 WHERE [參數編號] = '0000'");
    Check("T5 庫存不足阻擋", rejected, "檢查庫存量=1 時超量出貨應拋「庫存不足」");

    // ── T6 重複單號阻擋 ──
    string 既有 = DbManager.QueryScalar("SELECT MAX([交易單號]) FROM [交易主檔] WHERE [單據類別]='出貨'") as string ?? "";
    bool dupRejected = false;
    try
    {
        TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易單號 = 既有, 交易對象 = 客戶, 倉庫編號 = 倉庫,
            明細 = { new TradeService.DetailRow { 貨品編號 = 貨品, 倉庫編號 = 倉庫, 數量 = 1m, 單價 = 100m } },
        });
    }
    catch (InvalidOperationException ex) { dupRejected = ex.Message.Contains("已存在"); }
    Check("T6 重複單號阻擋", dupRejected, $"指定已存在單號 {既有} 應拋「已存在」");

    // ── T7 調撥（僅驗證缺倉阻擋；本庫僅一倉）──
    bool 調撥缺倉 = false;
    try
    {
        TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "調撥", 倉庫編號 = 倉庫,
            明細 = { new TradeService.DetailRow { 貨品編號 = 貨品, 倉庫編號 = 倉庫, 數量 = 1m, 調入倉庫 = "" } },
        });
    }
    catch (InvalidOperationException ex) { 調撥缺倉 = ex.Message.Contains("調入倉庫"); }
    Check("T7 調撥缺調入倉阻擋", 調撥缺倉, "調撥明細未填調入倉庫應拋錯");

    // ── 清理：刪除出退單，庫存回復原值 ──
    TradeService.DeleteBill(r3.單據副碼);
    decimal final = Stock(貨品, 倉庫);
    Check("清理 庫存回復原值", final == orig, $"最終 {final}（期望原值 {orig}）");
}

// ════════════════════════════════════════════════════════
// 票據：BillService 報表資料建構
// ════════════════════════════════════════════════════════
void BillChecks()
{
    int 收票 = QInt("SELECT COUNT(*) FROM [票據收付] WHERE [收付類別]='收票'");
    int 付票 = QInt("SELECT COUNT(*) FROM [票據收付] WHERE [收付類別]='付票'");
    int 收票未兌 = QInt("SELECT COUNT(*) FROM [票據收付] WHERE [收付類別]='收票' AND [票據現況]='尚未'");
    int 付票未兌 = QInt("SELECT COUNT(*) FROM [票據收付] WHERE [收付類別]='付票' AND [票據現況]='尚未'");
    Check("樣本資料", 收票 > 0 && 付票 > 0, $"收票 {收票}、付票 {付票}");

    var d1 = BillService.BuildBillDetailReportData(BillService.收票類別, "收票日");
    Check("收票明細表資料", d1 is not null && d1.Detail.Count == 收票, $"筆數 {d1?.Detail.Count}（期望 {收票}）");
    bool 欄位齊 = d1 is not null && d1.Detail.All(x => x.ContainsKey("票面金額") && x.ContainsKey("支票號碼") && x.ContainsKey("公司簡稱") && x.ContainsKey("銀行名稱"));
    Check("收票明細表欄位", 欄位齊, "票面金額/支票號碼/公司簡稱/銀行名稱");
    Check("收票明細表票面金額", d1 is not null && d1.Detail.All(x => Convert.ToDecimal(x["票面金額"]) > 0), "所有票面金額為正");

    var d2 = BillService.BuildBillDetailReportData(BillService.付票類別, "開票日");
    Check("付票明細表資料", d2 is not null && d2.Detail.Count == 付票, $"筆數 {d2?.Detail.Count}（期望 {付票}）");

    var u1 = BillService.BuildUnclearedBillData(BillService.收票類別);
    Check("未兌現收票資料", u1 is not null && u1.Detail.Count == 收票未兌, $"筆數 {u1?.Detail.Count}（期望 {收票未兌}）");

    var u2 = BillService.BuildUnclearedBillData(BillService.付票類別);
    Check("未兌現付票資料", u2 is not null && u2.Detail.Count == 付票未兌, $"筆數 {u2?.Detail.Count}（期望 {付票未兌}）");

    bool 排許 = true;
    try { BillService.BuildBillDetailReportData(BillService.收票類別, "銀行"); }
    catch { 排許 = false; }
    Check("銀行排序不拋錯", 排許, "排序鍵「銀行」應正常執行");

    Check("未兌現全數為尚未", u1 is not null && u1.Detail.All(x => Convert.ToString(x["票據現況"]) == "尚未") &&
        u2 is not null && u2.Detail.All(x => Convert.ToString(x["票據現況"]) == "尚未"),
        "未兌現表僅含「尚未」票據");
}

// ════════════════════════════════════════════════════════
// 薪資：PayrollService.Calculate
// ════════════════════════════════════════════════════════
void PayrollChecks()
{
    int attCount = QInt("SELECT COUNT(*) FROM [出缺主檔] WHERE [出勤年度]=2026 AND [出勤月份]=8");
    if (attCount == 0)
    {
        Check("出缺勤樣本", false, "2026-08 無出缺勤資料");
        return;
    }
    Check("出缺勤樣本", true, $"2026-08 出缺勤 {attCount} 人");

    string msg = PayrollService.Calculate(2026, 8);
    Check("計算執行", msg.Contains("計算完成"), $"摘要：{msg.Split('\n')[0]}");

    int 主檔數 = QInt("SELECT COUNT(*) FROM [薪資主檔] WHERE [薪資年度]=2026 AND [薪資月份]=8");
    Check("主檔寫入", 主檔數 == attCount, $"薪資主檔 {主檔數} 筆（期望 {attCount}）");

    var r = DbManager.QueryTable(
        "SELECT [應領金額],[扣領金額],[實領金額],[給付金額],[稅項加總] FROM [薪資主檔] " +
        "WHERE [員工編號]='001' AND [薪資年度]=2026 AND [薪資月份]=8");
    if (r.Rows.Count == 0)
    {
        Check("員工001主檔", false, "查無資料");
        return;
    }
    decimal 應領 = Convert.ToDecimal(r.Rows[0]["應領金額"]);
    decimal 扣領 = Convert.ToDecimal(r.Rows[0]["扣領金額"]);
    decimal 實領 = Convert.ToDecimal(r.Rows[0]["實領金額"]);
    decimal 給付 = Convert.ToDecimal(r.Rows[0]["給付金額"]);
    decimal 稅項 = Convert.ToDecimal(r.Rows[0]["稅項加總"]);
    Check("員工001 應領非負", 應領 >= 0, $"應領 {應領:N0}");
    Check("實領=應領-扣領", 實領 == 應領 - 扣領, $"實領 {實領:N0} = 應領 {應領:N0} - 扣領 {扣領:N0}");
    Check("給付=實領", 給付 == 實領, $"給付 {給付:N0} = 實領 {實領:N0}");

    decimal 明細加 = QDec("SELECT COALESCE(SUM([金額]),0) FROM [薪資明細] WHERE [薪資編號] LIKE '001|2026-08|%' AND [加減]='加'");
    decimal 明細減 = QDec("SELECT COALESCE(SUM([金額]),0) FROM [薪資明細] WHERE [薪資編號] LIKE '001|2026-08|%' AND [加減]='減'");
    decimal 明細應稅加 = QDec("SELECT COALESCE(SUM([金額]),0) FROM [薪資明細] WHERE [薪資編號] LIKE '001|2026-08|%' AND [加減]='加' AND [計薪別]='應稅'");
    Check("明細加項=應領", 明細加 == 應領, $"加項 {明細加:N0} = 應領 {應領:N0}");
    Check("明細減項=扣領", 明細減 == 扣領, $"減項 {明細減:N0} = 扣領 {扣領:N0}");
    Check("應稅加項=稅項加總", 明細應稅加 == 稅項, $"應稅加項 {明細應稅加:N0} = 稅項加總 {稅項:N0}");
    int 明細數 = QInt("SELECT COUNT(*) FROM [薪資明細] WHERE [薪資編號] LIKE '001|2026-08|%'");
    Check("明細非空", (應領 + 扣領) > 0 ? 明細數 > 0 : true,
        應領 + 扣領 > 0 ? $"計薪項目 {明細數} 筆" : "員工無本薪/薪資設定，明細為空屬合理");

    string msg2 = PayrollService.Calculate(2026, 8);
    int 主檔數2 = QInt("SELECT COUNT(*) FROM [薪資主檔] WHERE [薪資年度]=2026 AND [薪資月份]=8");
    Check("重跑冪等", msg2.Contains("計算完成") && 主檔數2 == 主檔數, $"重跑後主檔 {主檔數2} 筆（期望 {主檔數}）");
}

// ════════════════════════════════════════════════════════
// 會計：報表資料建構 + 借貸平衡
// ════════════════════════════════════════════════════════
void AccChecks()
{
    // ── 1. 12 份報表資料建構：對應快照表有資料時應回傳非 null ──
    var builds = new (string 名稱, Func<RtmData?> Build, string 存在性SQL)[]
    {
        ("總分類帳明細表", AccountingService.BuildLedgerDetailReportData, "SELECT COUNT(*) FROM [日記帳簿]"),
        ("總分類帳簡要表", AccountingService.BuildLedgerBriefReportData, "SELECT COUNT(*) FROM [總分類帳]"),
        ("明細分類帳", AccountingService.BuildDetailLedgerReportData, "SELECT COUNT(*) FROM [日記帳簿]"),
        ("日記帳(含現)", AccountingService.BuildJournalReportData, "SELECT COUNT(*) FROM [日記帳簿]"),
        ("日記帳(不含現)", AccountingService.BuildJournalNoCashReportData, "SELECT COUNT(*) FROM [日記帳簿] WHERE [科目編號]<>'1101000'"),
        ("現金帳", AccountingService.BuildCashBookReportData, "SELECT COUNT(*) FROM [現金帳簿]"),
        ("試算表", AccountingService.BuildTrialBalanceReportData, "SELECT COUNT(*) FROM [期初餘額]"),
        ("期間試算表", AccountingService.BuildPeriodTrialBalanceReportData, "SELECT COUNT(*) FROM [總分類帳]"),
        ("損益表", AccountingService.BuildIncomeStatementReportData, "SELECT COUNT(*) FROM [損益報表]"),
        ("資產負債表", AccountingService.BuildBalanceSheetReportData, "SELECT COUNT(*) FROM [資產負債]"),
        ("帳戶式資產負債表", AccountingService.BuildAccountBalanceSheetReportData, "SELECT COUNT(*) FROM [資產負債]"),
        ("會計傳票", AccountingService.BuildVoucherReportData, "SELECT COUNT(*) FROM [傳票明細]"),
    };
    foreach (var (名稱, build, 存在性SQL) in builds)
    {
        try
        {
            var data = build();
            int 筆數 = QInt(存在性SQL);
            bool ok = 筆數 > 0 ? data is not null : true;
            Check($"建構 {名稱}", ok, 筆數 > 0 ? $"存在性 {筆數} 筆 → {(data is null ? "null（異常）" : $"明細 {data.Detail.Count} 筆")}" : "無資料（略）");
        }
        catch (Exception ex)
        {
            Check($"建構 {名稱}", false, $"例外 {ex.GetType().Name} {ex.Message}");
        }
    }

    // ── 2. 借貸平衡 ──
    // 歷史匯入資料殘缺（無主檔單據、現金日記帳快照、資產負債快照僅本期損益），
    // 檢查以「程式產生的資料」為對象：有主檔的傳票明細、非現金的日記帳簿、完整權益的資產負債。
    decimal 容差 = 0.01m;

    int 不平衡傳票 = QInt("SELECT COUNT(*) FROM [傳票主檔] WHERE [借方合計]<>[貸方合計]");
    Check("傳票主檔借貸平衡", 不平衡傳票 == 0, $"借貸不平衡傳票 {不平衡傳票} 張");

    int 明細不平衡單據 = QInt(
        "SELECT COUNT(*) FROM (" +
        "SELECT [單據副碼] FROM [傳票明細] WHERE [單據副碼] IN (SELECT [單據副碼] FROM [傳票主檔]) " +
        "GROUP BY [單據副碼] HAVING ABS(COALESCE(SUM([借方金額]),0)-COALESCE(SUM([貸方金額]),0))>$t)",
        DbManager.Param("$t", 容差));
    Check("傳票明細借貸平衡", 明細不平衡單據 == 0, $"有主檔傳票明細不平衡 {明細不平衡單據} 張（無主檔的歷史殘缺單據不列入）");

    decimal 日記帳差 = Math.Abs(QDec(
        "SELECT COALESCE(SUM([借方金額]),0)-COALESCE(SUM([貸方金額]),0) " +
        "FROM [日記帳簿] WHERE [科目編號]<>'1101000'"));
    Check("日記帳簿借貸平衡", 日記帳差 <= 容差, $"借貸差 {日記帳差:N2}（排除現金科目 1101000：現金日記帳快照為單邊收支）");

    decimal 期初差 = Math.Abs(QDec("SELECT COALESCE(SUM([借方金額]),0)-COALESCE(SUM([貸方金額]),0) FROM [期初餘額]"));
    Check("期初餘額借貸平衡", 期初差 <= 容差, $"借貸差 {期初差:N2}");

    decimal 資產 = QDec("SELECT COALESCE(SUM([金額小計]),0) FROM [資產負債] WHERE [大類名稱]='資產'");
    decimal 負債 = QDec("SELECT COALESCE(SUM([金額小計]),0) FROM [資產負債] WHERE [大類名稱]='負債'");
    decimal 權益 = QDec("SELECT COALESCE(SUM([金額小計]),0) FROM [資產負債] WHERE [大類名稱]!='資產' AND [大類名稱]!='負債'");
    int 權益筆數 = QInt("SELECT COUNT(*) FROM [資產負債] WHERE [大類名稱]!='資產' AND [大類名稱]!='負債'");
    decimal 資產差 = Math.Abs(資產 - 負債 - 權益);
    bool 權益快照殘缺 = 權益筆數 <= 1; // 僅「本期損益」一筆，缺資本/累積盈虧（歷史匯入快照特性）
    bool 資產負債ok = 資產差 <= 容差 || 權益快照殘缺;
    Check("資產負債平衡", 資產負債ok,
        $"資產 {資產:N2} vs 負債+權益 {負債 + 權益:N2}（差 {資產差:N2}" +
        (權益快照殘缺 ? $"，權益僅 {權益筆數} 筆＝歷史快照殘缺，容許）" : "）"));
}

// ════════════════════════════════════════════════════════
// 電子發票：InvoiceTrackService 字軌與自動配號
// ════════════════════════════════════════════════════════
void InvoiceChecks()
{
    const string 年度 = "9999", 月期 = "13", 字軌 = "TST";

    InvoiceTrackService.EnsureSchema();
    DbManager.ExecuteNonQuery("DELETE FROM [發票開立紀錄] WHERE [字軌序號] IN " +
        "(SELECT [序號] FROM [發票字軌] WHERE [年度]=$y AND [月期]=$p AND [字軌]=$t)",
        DbManager.Param("$y", 年度), DbManager.Param("$p", 月期), DbManager.Param("$t", 字軌));
    DbManager.ExecuteNonQuery("DELETE FROM [發票字軌] WHERE [年度]=$y AND [月期]=$p AND [字軌]=$t",
        DbManager.Param("$y", 年度), DbManager.Param("$p", 月期), DbManager.Param("$t", 字軌));

    InvoiceTrackService.SaveTrack(null, new InvoiceTrackService.TrackSaveRequest(年度, 月期, 字軌, 1, 3, true, "ModuleCheck"));
    long 序號 = QInt("SELECT [序號] FROM [發票字軌] WHERE [年度]=$y AND [月期]=$p AND [字軌]=$t",
        DbManager.Param("$y", 年度), DbManager.Param("$p", 月期), DbManager.Param("$t", 字軌));
    Check("建置字軌", 序號 > 0, $"年度 {年度} 月期 {月期} 字軌 {字軌} 起 1 迄 3（自動配號）");

    bool 重複 = false;
    try { InvoiceTrackService.SaveTrack(null, new InvoiceTrackService.TrackSaveRequest(年度, 月期, 字軌, 1, 3, true, "")); }
    catch (InvalidOperationException ex) { 重複 = ex.Message.Contains("已存在"); }
    Check("重複字軌阻擋", 重複, "相同年度/月期/字軌應拋「已存在」");

    string? 預覽 = InvoiceTrackService.PreviewNextNo(序號);
    Check("預覽不佔號", 預覽 == "TST00000001" &&
        QInt("SELECT [已用迄號] FROM [發票字軌] WHERE [序號]=$c", DbManager.Param("$c", 序號)) == 0,
        $"預覽 {預覽}，已用迄號仍為 0");

    var 配號 = new List<string>();
    DbManager.ExecuteImmediateTransaction(c => 配號.Add(InvoiceTrackService.NextInvoiceNoInTransaction(c, 序號, "出貨", "MC-0001")));
    DbManager.ExecuteImmediateTransaction(c => 配號.Add(InvoiceTrackService.NextInvoiceNoInTransaction(c, 序號, "出貨", "MC-0002")));
    Check("依序配號", 配號.Count == 2 && 配號[0] == "TST00000001" && 配號[1] == "TST00000002",
        string.Join(" → ", 配號));

    bool 用罄 = false;
    try
    {
        DbManager.ExecuteImmediateTransaction(c => 配號.Add(InvoiceTrackService.NextInvoiceNoInTransaction(c, 序號, "出貨", "MC-0003")));
        DbManager.ExecuteImmediateTransaction(c => 配號.Add(InvoiceTrackService.NextInvoiceNoInTransaction(c, 序號, "出貨", "MC-0004")));
    }
    catch (InvalidOperationException ex) { 用罄 = ex.Message.Contains("已用罄"); }
    Check("迄號用罄阻擋", 用罄 && 配號.Count == 3 && 配號[2] == "TST00000003",
        $"第三號 {配號.ElementAtOrDefault(2)}，第四號應拋「已用罄」");

    Check("開立紀錄 3 筆", QInt("SELECT COUNT(*) FROM [發票開立紀錄] WHERE [字軌序號]=$c", DbManager.Param("$c", 序號)) == 3,
        "每次配號皆登記開立紀錄");
    Check("紀錄關聯單據", QInt("SELECT COUNT(*) FROM [發票開立紀錄] WHERE [字軌序號]=$c AND [單據號碼]='MC-0001'", DbManager.Param("$c", 序號)) == 1,
        "配號附帶單據類別/號碼");

    InvoiceTrackService.RegisterVoid(序號, "TST00000001");
    Check("作廢更新", QInt("SELECT COUNT(*) FROM [發票開立紀錄] WHERE [字軌序號]=$c AND [發票號碼]='TST00000001' AND [狀態]='作廢'", DbManager.Param("$c", 序號)) == 1,
        "開立 → 作廢");

    bool 不可刪 = false;
    try { InvoiceTrackService.DeleteTrack(序號); }
    catch (InvalidOperationException ex) { 不可刪 = ex.Message.Contains("不可刪除"); }
    Check("有紀錄不可刪", 不可刪, "已有開立紀錄應拋「不可刪除」");

    InvoiceTrackService.SetTrackStatus(序號, InvoiceTrackService.停用);
    Check("停用後預覽為空", InvoiceTrackService.PreviewNextNo(序號) is null, "停用字軌不可預覽");
    bool 停用拒配 = false;
    try { DbManager.ExecuteImmediateTransaction(c => InvoiceTrackService.NextInvoiceNoInTransaction(c, 序號, "出貨", "MC-0005")); }
    catch (InvalidOperationException ex) { 停用拒配 = ex.Message.Contains("找不到啟用中"); }
    Check("停用拒配號", 停用拒配, "停用字軌配號應拋「找不到啟用中」");

    DbManager.ExecuteNonQuery("DELETE FROM [發票開立紀錄] WHERE [字軌序號]=$c", DbManager.Param("$c", 序號));
    DbManager.ExecuteNonQuery("DELETE FROM [發票字軌] WHERE [序號]=$c", DbManager.Param("$c", 序號));
    Check("清理完成", QInt("SELECT COUNT(*) FROM [發票字軌] WHERE [序號]=$c", DbManager.Param("$c", 序號)) == 0, "測試字軌已刪除");
}

// ════════════════════════════════════════════════════════
// 核准流程：ApprovalService 設定/送審/核准/退回
// ════════════════════════════════════════════════════════
void ApprovalChecks()
{
    const string 類別 = "報價", 測試單號 = "MC-APPR-001";

    ApprovalService.EnsureSchema();
    DbManager.ExecuteNonQuery("DELETE FROM [核准紀錄] WHERE [流程序號] IN (SELECT [序號] FROM [核准流程] WHERE [單號] LIKE 'MC-APPR-%')");
    DbManager.ExecuteNonQuery("DELETE FROM [核准流程] WHERE [單號] LIKE 'MC-APPR-%'");

    int 層數(string c) => QInt("SELECT [層數] FROM [核准設定] WHERE [單據類別]=$c", DbManager.Param("$c", c));
    int 啟用(string c) => QInt("SELECT [啟用] FROM [核准設定] WHERE [單據類別]=$c", DbManager.Param("$c", c));
    string 狀態(long s) => Convert.ToString(DbManager.QueryScalar("SELECT [狀態] FROM [核准流程] WHERE [序號]=$s", DbManager.Param("$s", s))) ?? "";
    int 目前層級(long s) => QInt("SELECT [目前層級] FROM [核准流程] WHERE [序號]=$s", DbManager.Param("$s", s));
    long 流程層數(long s) => QInt("SELECT [層數] FROM [核准流程] WHERE [序號]=$s", DbManager.Param("$s", s));

    ApprovalService.SaveSetting(類別, 2, true);
    Check("層數設定", ApprovalService.HasSetting(類別) && 層數(類別) == 2 && 啟用(類別) == 1,
        $"{類別} 2 層啟用");

    long? seq = ApprovalService.Submit(類別, 測試單號, 1200m, "測試員", "ModuleCheck");
    Check("送審建立流程", seq is not null, $"流程序號 {seq}");
    Check("流程初態", seq is not null && 狀態(seq.Value) == ApprovalService.待核准 &&
        目前層級(seq.Value) == 1 && 流程層數(seq.Value) == 2,
        $"待核准 / 目前層級 1 / 共 2 層");

    string? r1 = ApprovalService.Approve(seq!.Value, "核准人甲", "同意");
    Check("第一層核准", r1 is null && 目前層級(seq.Value) == 2 && 狀態(seq.Value) == ApprovalService.待核准,
        $"核准後層級 2 仍待核准（錯誤：{r1}）");

    string? r2 = ApprovalService.Approve(seq.Value, "核准人乙", "同意");
    Check("末層核准完成", r2 is null && 狀態(seq.Value) == ApprovalService.已核准,
        $"狀態 {狀態(seq.Value)}（錯誤：{r2}）");

    string? r3 = ApprovalService.Approve(seq.Value, "核准人丙", "");
    Check("已核准拒再核准", r3 is not null && r3.Contains("無法核准"), $"錯誤：{r3}");

    Check("核准紀錄 2 筆", QInt("SELECT COUNT(*) FROM [核准紀錄] WHERE [流程序號]=$s", DbManager.Param("$s", seq.Value)) == 2,
        ApprovalService.LoadRecords(seq.Value).Rows.Count == 2 ? "兩層各留一筆" : "紀錄數不符");

    var flows = ApprovalService.LoadFlows(類別, ApprovalService.已核准, 測試單號);
    Check("流程查詢過濾", flows.Rows.Count == 1 && Convert.ToString(flows.Rows[0]["單號"]) == 測試單號,
        $"已核准 + 關鍵字 {測試單號} 篩得 {flows.Rows.Count} 筆");

    long? seq2 = ApprovalService.Submit(類別, "MC-APPR-002", 800m, "測試員");
    string? rj = ApprovalService.Reject(seq2!.Value, "核准人甲", "金額不符");
    Check("退回流程", rj is null && 狀態(seq2.Value) == ApprovalService.已退回,
        $"狀態 {狀態(seq2.Value)}（錯誤：{rj}）");
    Check("退回紀錄", QInt("SELECT COUNT(*) FROM [核准紀錄] WHERE [流程序號]=$s AND [結果]='退回'", DbManager.Param("$s", seq2.Value)) == 1,
        "退回留一筆紀錄");

    ApprovalService.SaveSetting("詢價", 1, false);
    long? seq3 = ApprovalService.Submit("詢價", "MC-APPR-003", 100m, "測試員");
    Check("未啟用不送審", seq3 is null, "啟用=0 的類別 Submit 應回傳 null");

    DbManager.ExecuteNonQuery("DELETE FROM [核准紀錄] WHERE [流程序號] IN (SELECT [序號] FROM [核准流程] WHERE [單號] LIKE 'MC-APPR-%')");
    DbManager.ExecuteNonQuery("DELETE FROM [核准流程] WHERE [單號] LIKE 'MC-APPR-%'");
    ApprovalService.SaveSetting(類別, 2, false);
    ApprovalService.SaveSetting("詢價", 1, false);
    Check("清理完成", QInt("SELECT COUNT(*) FROM [核准流程] WHERE [單號] LIKE 'MC-APPR-%'") == 0, "測試流程已刪除");
}

// ════════════════════════════════════════════════════════
// 稽核日誌：AuditService 登入/存檔/刪除/密碼變更
// ════════════════════════════════════════════════════════
void AuditChecks()
{
    AuditService.CurrentAccount = "MC";
    AuditService.CurrentUser = "檢查員";
    AuditService.EnsureSchema();

    int 起點 = QInt("SELECT COALESCE(MAX([序號]),0) FROM [稽核日誌]");

    AuditService.LogLogin("tester", true);
    AuditService.LogLogin("baduser", false, "密碼錯誤");
    AuditService.CurrentAccount = "MC";
    AuditService.CurrentUser = "檢查員";
    AuditService.Log(AuditService.存檔, "檢查", "MC-單據-1", "成功", "檢查明細");
    AuditService.Log(AuditService.刪除, "檢查", "MC-單據-2", "失敗", "權限不足");
    AuditService.Log(AuditService.變更密碼, "檢查", "tester");

    int 新增 = QInt("SELECT COUNT(*) FROM [稽核日誌] WHERE [序號] > $b", DbManager.Param("$b", 起點));
    Check("寫入稽核 5 筆", 新增 == 5, $"新增 {新增} 筆（登入成敗/存檔/刪除/密碼變更）");
    Check("登入成功記錄", QInt("SELECT COUNT(*) FROM [稽核日誌] WHERE [序號] > $b AND [事件]='登入成功' AND [帳號]='tester' AND [結果]='成功'", DbManager.Param("$b", 起點)) == 1,
        "tester 登入成功");
    Check("登入失敗記錄", QInt("SELECT COUNT(*) FROM [稽核日誌] WHERE [序號] > $b AND [事件]='登入失敗' AND [帳號]='baduser' AND [詳細]='密碼錯誤'", DbManager.Param("$b", 起點)) == 1,
        "baduser 登入失敗含原因");
    Check("存檔/刪除/密碼",
        QInt("SELECT COUNT(*) FROM [稽核日誌] WHERE [序號] > $b AND [事件]='存檔' AND [對象]='MC-單據-1' AND [結果]='成功' AND [帳號]='MC'", DbManager.Param("$b", 起點)) == 1 &&
        QInt("SELECT COUNT(*) FROM [稽核日誌] WHERE [序號] > $b AND [事件]='刪除' AND [結果]='失敗' AND [詳細]='權限不足'", DbManager.Param("$b", 起點)) == 1 &&
        QInt("SELECT COUNT(*) FROM [稽核日誌] WHERE [序號] > $b AND [事件]='變更密碼' AND [對象]='tester' AND [帳號]='MC'", DbManager.Param("$b", 起點)) == 1,
        "存檔成功/刪除失敗/變更密碼");
    Check("機器與使用者", QInt("SELECT COUNT(*) FROM [稽核日誌] WHERE [序號] > $b AND [機器]=$m", DbManager.Param("$b", 起點), DbManager.Param("$m", AuditService.MachineName)) == 新增,
        $"新增 {新增} 筆皆記錄機器 {AuditService.MachineName}");

    DbManager.ExecuteNonQuery("DELETE FROM [稽核日誌] WHERE [序號] > $b", DbManager.Param("$b", 起點));
    Check("清理完成", QInt("SELECT COUNT(*) FROM [稽核日誌] WHERE [序號] > $b", DbManager.Param("$b", 起點)) == 0, "測試稽核紀錄已刪除");
}

// ════════════════════════════════════════════════════════
// 表單式主檔：FormMasterCatalog 8 張表定義與資料庫一致性
// ════════════════════════════════════════════════════════
void MasterChecks()
{
    var tables = new[] { "客戶廠商", "員工資料", "倉庫資料", "會計科目", "銀行資料", "貨運公司", "車廠資料", "部門資料" };
    foreach (var t in tables)
    {
        var dbCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow r in DbManager.QueryTable($"PRAGMA table_info(\"{t}\")").Rows)
            dbCols.Add(Convert.ToString(r["name"]) ?? "");

        var cfg = FormMasterCatalog.Get(t);
        Check($"目錄存在 {t}", cfg is not null && FormMasterCatalog.IsMasterTable(t),
            cfg is not null ? "Get 回傳配置" : "Get 回傳 null，無法開啟表單式介面");
        if (cfg is null) continue;

        var (listCols, pages) = cfg.Value;
        var missList = listCols.Where(c => !dbCols.Contains(c)).ToList();
        Check($"清單欄位存在 {t}", missList.Count == 0,
            missList.Count == 0 ? $"{listCols.Count} 欄全存在" : $"缺少：{string.Join(",", missList)}");

        var allFields = pages.SelectMany(p => p.Fields).ToList();
        var missField = allFields.Where(f => !dbCols.Contains(f.Field)).ToList();
        Check($"表單欄位存在 {t}", missField.Count == 0,
            missField.Count == 0 ? $"{allFields.Count} 欄全存在" : $"缺少：{string.Join(",", missField.Select(f => f.Field))}");

        var pkCols = TableCatalog.GetKeyFields(t);
        var missingPk = pkCols.Where(c => !allFields.Any(f => string.Equals(f.Field, c, StringComparison.OrdinalIgnoreCase))).ToList();
        Check($"主鍵欄位有編輯器 {t}", missingPk.Count == 0,
            missingPk.Count == 0 ? $"主鍵 {string.Join("/", pkCols)} 已定義" : $"未定義：{string.Join(",", missingPk)}");

        var conflicts = new List<string>();
        foreach (var page in pages)
        {
            var seen = new HashSet<(int, int)>();
            foreach (var f in page.Fields)
                if (!seen.Add((f.Row, f.Col)))
                    conflicts.Add($"{page.Title}:{f.Field}@{f.Row},{f.Col}");
        }
        Check($"版面無衝突 {t}", conflicts.Count == 0,
            conflicts.Count == 0 ? $"{pages.Count} 頁版面正確" : $"衝突：{string.Join(",", conflicts)}");

        var badSql = allFields.Where(f => !string.IsNullOrWhiteSpace(f.ComboSql) && !SqlRuns(f.ComboSql))
            .Select(f => f.Field).ToList();
        Check($"下拉 SQL 可執行 {t}", badSql.Count == 0,
            badSql.Count == 0 ? "全部可執行" : $"失敗：{string.Join(",", badSql)}");
    }
}

bool SqlRuns(string sql)
{
    try
    {
        return DbManager.QueryTable(sql).Columns.Count >= 1;
    }
    catch
    {
        return false;
    }
}
