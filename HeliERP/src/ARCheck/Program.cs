using System.Data;
using System.Globalization;
using System.Text;
using HeliERP.App;
using HeliERP.Data;

Console.OutputEncoding = Encoding.UTF8;
Thread.CurrentThread.CurrentCulture = new CultureInfo("zh-TW");

var dbPath = Path.Combine(AppContext.BaseDirectory, "HeliERP.db");
if (!File.Exists(dbPath))
    File.Copy(@"D:\HeliAcc\HeliERP.db", dbPath);
Console.WriteLine($"DB: {dbPath}");
DbManager.DatabasePath = dbPath;

// 固定基準日，測試可重複執行（不依賴 DateTime.Today）
var 基準日 = new DateTime(2026, 8, 12);
var pass = 0;
var fail = 0;

// ── helpers ──
void RunCase(string name, Action body)
{
    try
    {
        body();
        pass++;
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        fail++;
        Console.WriteLine($"FAIL  {name}  {ex.Message}");
    }
}

void Exec(string sql, params Microsoft.Data.Sqlite.SqliteParameter[] ps) => DbManager.ExecuteNonQuery(sql, ps);

long NextSeq(string 表) =>
    Convert.ToInt64(DbManager.QueryScalar($"SELECT IFNULL(MAX([建檔序號]),0)+1 FROM [{表}]"));

decimal Cell(DataTable dt, int r, string 欄) =>
    dt.Rows[r].IsNull(欄) ? 0m : Convert.ToDecimal(dt.Rows[r][欄]);

void 期望(decimal 實際, decimal 期望值, string 說明)
{
    if (實際 != 期望值) throw new Exception($"{說明}：期望 {期望值}，實際 {實際}");
}

// ── fixture ──
void Cleanup()
{
    Exec("DELETE FROM [帳款簡要] WHERE [交易對象] LIKE 'AR-%'");
    Exec("DELETE FROM [帳款主檔] WHERE [交易對象] LIKE 'AR-%'");
    Exec("DELETE FROM [客戶廠商] WHERE [客廠編號] LIKE 'AR-%'");
    Exec("DELETE FROM [帳龄期間] WHERE [類別] = '一般'");
}

void Fixture()
{
    foreach (var (k, no, name) in new[]
    {
        ("客戶", "AR-C001", "測試客戶一"),
        ("客戶", "AR-C002", "測試客戶二"),
        ("客戶", "AR-C003", "測試客戶三"),
        ("廠商", "AR-V001", "測試廠商一"),
    })
        Exec("INSERT INTO [客戶廠商] ([客廠類別],[客廠編號],[公司簡稱]) VALUES ($k,$n,$m)",
            DbManager.Param("$k", k), DbManager.Param("$n", no), DbManager.Param("$m", name));

    void 主檔(string 對象, decimal 前期, decimal 本期總計, decimal 已收, decimal 折讓, decimal 預收) =>
        Exec("INSERT INTO [帳款主檔] ([建檔序號],[交易對象],[前期累計應收帳款],[本期合計],[營業稅]," +
             "[折讓金額],[已收付金額],[現金收付金額],[本期總計],[累計預收貨款]) VALUES ($s,$o,$p,0,0,$z,$y,0,$m,$u)",
             DbManager.Param("$s", NextSeq("帳款主檔")), DbManager.Param("$o", 對象),
             DbManager.Param("$p", 前期), DbManager.Param("$z", 折讓),
             DbManager.Param("$y", 已收), DbManager.Param("$m", 本期總計), DbManager.Param("$u", 預收));

    主檔("AR-C001", 前期: 5000, 本期總計: 6500, 已收: 900, 折讓: 100, 預收: 0);
    主檔("AR-C002", 前期: 0, 本期總計: 4500, 已收: 500, 折讓: 0, 預收: 0);
    主檔("AR-C003", 前期: 0, 本期總計: 600, 已收: 300, 折讓: 0, 預收: 0);
    主檔("AR-V001", 前期: 0, 本期總計: 1500, 已收: 0, 折讓: 0, 預收: 0);

    void 簡要(string 對象, DateTime 日期, string 單號, decimal 總計, decimal 已收, decimal 折讓, decimal 未收) =>
        Exec("INSERT INTO [帳款簡要] ([建檔序號],[單據類別],[交易對象],[交易日期],[交易單號],[發票號碼]," +
             "[合計金額],[營業稅],[總計金額],[折讓金額],[現金收付金額],[已收付金額],[未收付金額],[應收付金額]) " +
             "VALUES ($s,$k,$o,$d,$n,$i,$t,0,$t,$z,0,$y,$u,$u)",
             DbManager.Param("$s", NextSeq("帳款簡要")), DbManager.Param("$k", "出貨"),
             DbManager.Param("$o", 對象), DbManager.Param("$d", 日期.ToString("yyyy-MM-dd HH:mm:ss")),
             DbManager.Param("$n", 單號), DbManager.Param("$i", "AR-INV"),
             DbManager.Param("$t", 總計), DbManager.Param("$z", 折讓),
             DbManager.Param("$y", 已收), DbManager.Param("$u", 未收));

    簡要("AR-C001", 基準日.AddDays(-10), "AR-SO-001", 總計: 1500, 已收: 400, 折讓: 100, 未收: 1000);
    簡要("AR-C001", 基準日.AddDays(-45), "AR-SO-002", 總計: 2500, 已收: 500, 折讓: 0, 未收: 2000);
    簡要("AR-C001", 基準日.AddDays(-200), "AR-SO-003", 總計: 3000, 已收: 0, 折讓: 0, 未收: 3000);
    簡要("AR-C001", 基準日.AddDays(-5), "AR-SO-004", 總計: -500, 已收: 0, 折讓: 0, 未收: -500);
    簡要("AR-C002", 基準日.AddDays(-75), "AR-SO-005", 總計: 4500, 已收: 500, 折讓: 0, 未收: 4000);
    簡要("AR-V001", 基準日.AddDays(-120), "AR-PO-001", 總計: 1500, 已收: 0, 折讓: 0, 未收: 1500);
    簡要("AR-C003", 基準日.AddDays(-29), "AR-SO-006", 總計: 100, 已收: 0, 折讓: 0, 未收: 100);
    簡要("AR-C003", 基準日.AddDays(-30), "AR-SO-007", 總計: 200, 已收: 0, 折讓: 0, 未收: 200);
    簡要("AR-C003", 基準日.AddDays(-60), "AR-SO-008", 總計: 300, 已收: 300, 折讓: 0, 未收: 0);
}

// ── 測試案例 ──
Cleanup();
Fixture();

RunCase("T1 預設期間設定（無資料時 30/60/90/120/150/180）", () =>
{
    var p = ARService.LoadAgePeriods();
    期望(p.第一期間, 30, "第一期間");
    期望(p.第二期間, 60, "第二期間");
    期望(p.第三期間, 90, "第三期間");
    期望(p.第四期間, 120, "第四期間");
    期望(p.第五期間, 150, "第五期間");
    期望(p.第六期間, 180, "第六期間");
});

RunCase("T4 應收總覽未收付合計（前期+本期−已收−折讓）", () =>
{
    var dt = ARService.LoadObjectSummary("客戶");
    var 期望值 = new Dictionary<string, decimal>
    {
        ["AR-C001"] = 10500m, ["AR-C002"] = 4000m, ["AR-C003"] = 300m,
    };
    foreach (DataRow r in dt.Rows)
    {
        var 對象 = r["交易對象"].ToString();
        if (對象?.StartsWith("AR-") != true) continue;  // 副本含主 DB 真實資料，只驗證測試對象
        if (!期望值.ContainsKey(對象)) throw new Exception($"出現未預期對象 {對象}");
        期望(Convert.ToDecimal(r["未收付合計"]), 期望值[對象], $"未收付合計 {對象}");
    }
});

RunCase("T5 應付總覽未收付合計", () =>
{
    var dt = ARService.LoadObjectSummary("廠商");
    DataRow? row = null;
    foreach (DataRow r in dt.Rows)
        if (r["交易對象"].ToString() == "AR-V001") { row = r; break; }
    if (row is null) throw new Exception($"找不到 AR-V001 應付對象（實際 {dt.Rows.Count} 筆，含真實資料）");
    期望(Cell(dt, dt.Rows.IndexOf(row), "未收付合計"), 1500m, "AR-V001 未收付合計");
});

RunCase("T6 帳齡分桶（單對象，含期初/貸項）", () =>
{
    var dt = ARService.AgingAnalysis("AR-C001", ARService.LoadAgePeriods(), 基準日);
    if (dt.Rows.Count != 1) throw new Exception($"AR-C001 應為 1 列，實際 {dt.Rows.Count}");
    期望(Cell(dt, 0, "期初帳款"), 8000m, "期初帳款");     // 前期 5000 + C 單據 3000（200 天 ≥ 180）
    期望(Cell(dt, 0, "第一期間"), 1000m, "第一期間");      // A：10 天
    期望(Cell(dt, 0, "第二期間"), 2000m, "第二期間");      // B：45 天
    期望(Cell(dt, 0, "第三期間"), 0m, "第三期間");
    期望(Cell(dt, 0, "貸項"), -500m, "貸項");              // D：貸項單據
    期望(Cell(dt, 0, "合計"), 10500m, "合計");
});

RunCase("T7 帳齡分析（全部對象彙總）", () =>
{
    var dt = ARService.AgingAnalysis(null, ARService.LoadAgePeriods(), 基準日);
    var 期望值 = new Dictionary<string, decimal>
    {
        ["AR-C001"] = 10500m, ["AR-C002"] = 4000m, ["AR-C003"] = 300m, ["AR-V001"] = 1500m,
    };
    foreach (DataRow r in dt.Rows)
    {
        var 對象 = r["交易對象"].ToString()!;
        if (對象.StartsWith("AR-") != true) continue;  // 副本含主 DB 真實資料，只驗證測試對象
        if (!期望值.ContainsKey(對象)) throw new Exception($"出現未預期對象 {對象}");
        期望(Convert.ToDecimal(r["合計"]), 期望值[對象], $"合計 {對象}");
    }
});

RunCase("T8 帳齡分桶邊界（29 天→第一、30 天→第二）", () =>
{
    var dt = ARService.AgingAnalysis("AR-C003", ARService.LoadAgePeriods(), 基準日);
    if (dt.Rows.Count != 1) throw new Exception($"AR-C003 應為 1 列，實際 {dt.Rows.Count}");
    期望(Cell(dt, 0, "第一期間"), 100m, "第一期間");      // G：29 天
    期望(Cell(dt, 0, "第二期間"), 200m, "第二期間");      // H：30 天（30 < 60）
    期望(Cell(dt, 0, "合計"), 300m, "合計");              // I 已沖完（未收付 0）不計
});

RunCase("T9 未收付明細過濾（未收付=0 不出現）", () =>
{
    var dt1 = ARService.LoadOpenDetails("AR-C001");
    if (dt1.Rows.Count != 4) throw new Exception($"AR-C001 明細應為 4 筆，實際 {dt1.Rows.Count}");
    var dt2 = ARService.LoadOpenDetails("AR-C003");
    if (dt2.Rows.Count != 2) throw new Exception($"AR-C003 明細應為 2 筆（I 已沖完應被濾除），實際 {dt2.Rows.Count}");
    foreach (DataRow r in dt2.Rows)
        if (Convert.ToDecimal(r["未收付金額"]) == 0m)
            throw new Exception("未收付金額 = 0 的單據不應出現");
});

RunCase("T10 基準日參數化（E 單據 75 天前；基準日提前 42 天 → 落第二期間）", () =>
{
    var dt = ARService.AgingAnalysis("AR-C002", ARService.LoadAgePeriods(), new DateTime(2026, 7, 1));
    if (dt.Rows.Count != 1) throw new Exception($"AR-C002 應為 1 列，實際 {dt.Rows.Count}");
    // E 交易日期 = 2026-05-29；2026-07-01 − 2026-05-29 = 33 天 → 第二期間
    期望(Cell(dt, 0, "第二期間"), 4000m, "第二期間");
    期望(Cell(dt, 0, "第三期間"), 0m, "第三期間");
    期望(Cell(dt, 0, "合計"), 4000m, "合計");
});

// 期間設定測試移到最後：儲存 30/45/60/90/120/180 會影響 LoadAgePeriods()，
// 若在前面執行會讓 T6/T7/T8/T10 的分桶落在錯誤期間。
RunCase("T11 期間設定往返（Save 30/45/60/90/120/180 → Load 一致）", () =>
{
    ARService.SaveAgePeriods(new ARService.AgePeriods
    {
        第一期間 = 30, 第二期間 = 45, 第三期間 = 60, 第四期間 = 90, 第五期間 = 120, 第六期間 = 180,
    });
    var p = ARService.LoadAgePeriods();
    期望(p.第一期間, 30, "第一期間");
    期望(p.第二期間, 45, "第二期間");
    期望(p.第三期間, 60, "第三期間");
    期望(p.第六期間, 180, "第六期間");
});

RunCase("T12 期間設定驗證（非遞增應拋例外）", () =>
{
    var threw = false;
    try
    {
        ARService.SaveAgePeriods(new ARService.AgePeriods
        {
            第一期間 = 30, 第二期間 = 60, 第三期間 = 50, 第四期間 = 90, 第五期間 = 120, 第六期間 = 180,
        });
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }
    if (!threw) throw new Exception("非遞增期間應拋 InvalidOperationException");
});

Cleanup();
Console.WriteLine($"\n=== 結果: {pass} PASS / {fail} FAIL ===");
return fail == 0 ? 0 : 1;
