// ════════════════════════════════════════════════════════
// UiShot：UI 截圖工具——將各表單渲染為 PNG，供視覺驗證。
// 用法：UiShot [輸出目錄] [資料庫路徑]
//   - 輸出目錄預設 D:\HeliAcc\shots
//   - 資料庫路徑可選；未指定時使用設定檔，若設定庫不含「權限主檔」
//     自動改用候選資料庫中第一個有效庫（含執行檔上層目錄掃描）。
//   找不到有效庫時中止，絕不連到空庫（避免 SQLite 自動建檔造成
//   「no such table」誤判）。
// 每個表單於獨立 STA 執行緒渲染，逾時 25 秒，個別失敗不影響其他。
// ════════════════════════════════════════════════════════
using System.Drawing.Imaging;
using System.Globalization;
using HeliERP.App;
using HeliERP.Data;
using HeliERP.Models;
using Microsoft.Data.Sqlite;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = System.Text.Encoding.UTF8;

// UI 執行緒例外防護：不跳 JIT 對話框，改記錄並顯示於輸出
object uiLock = new();
Exception? uiError = null;
Application.ThreadException += (s, e) =>
{
    lock (uiLock) { uiError = e.Exception; }
    Console.WriteLine($"UI 執行緒例外: {e.Exception.GetType().Name}: {e.Exception.Message}");
};

string outDir = args.Length > 0 ? args[0] : @"D:\HeliAcc\shots";
Directory.CreateDirectory(outDir);

var config = DbConfig.Load();
if (args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1]))
    config.DatabasePath = args[1];

// 候選庫：設定/歷史 + 執行檔往上層遞迴掃描（找出專案根附近的真實庫）
var candidates = config.FindDatabases().ToList();
var seen = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
{
    foreach (var f in dir.GetFiles("*.db", SearchOption.TopDirectoryOnly))
    {
        if (seen.Add(f.FullName))
            candidates.Add(f.FullName);
    }
    if (dir.Parent is null) break;
}

// 設定庫不含登入表（權限主檔）時，改用候選資料庫中第一個有效庫
if (!DbConfig.HasLoginTable(config.DatabasePath))
{
    var alt = candidates.FirstOrDefault(DbConfig.HasLoginTable);
    if (alt is not null)
    {
        Console.WriteLine($"設定庫不可用（{config.DatabasePath}），改用 {alt}");
        config.DatabasePath = alt;
    }
}

if (!DbConfig.HasLoginTable(config.DatabasePath))
{
    Console.WriteLine("錯誤：找不到可用的資料庫（需包含「權限主檔」表）。");
    Console.WriteLine("請指定資料庫路徑：UiShot <輸出目錄> <資料庫路徑>");
    return 1;
}
var user = new AppUser { UserId = "U001", DisplayName = "測試使用者", IsAdmin = true };

// 與正式程式 Program.cs 一致：設定全域資料庫路徑（表單建構時依賴此值）
DbManager.DatabasePath = config.DatabasePath;

var targets = new List<(string Name, Func<Form> Factory)>
{
    ("01-LoginForm", () => new LoginForm(config)),
    ("02-MainForm", () => new MainForm(config, user)),
    ("03-TableBrowserForm", () => new TableBrowserForm()),
    ("04-ConfigForm", () => new ConfigForm(config)),
    ("05-ProductMaintenanceForm", () => new ProductMaintenanceForm()),
    ("06-RepairModuleForm", () => new RepairModuleForm(user)),
    ("06b-InventoryForm", () => new InventoryForm()),
    ("06c-AdjustmentForm", () => new AdjustmentForm()),
    ("09-TransactionForm", () => new TransactionForm(user)),
    ("10-PaymentForm", () => new PaymentForm()),
    ("11-AccountReceivableForm", () => new AccountReceivableForm()),
    ("11b-SystemSettingsForm", () => new SystemSettingsForm(config, user)),
    ("11c-BillModuleForm", () => new BillModuleForm()),
    ("12-ProductionModuleForm", () => new ProductionModuleForm()),
    ("13-PayrollModuleForm", () => new PayrollModuleForm()),
    ("14-AccountingModuleForm", () => new AccountingModuleForm()),
    ("15-PoOrderForm", () => new PoOrderForm()),
    ("16-ReportMenuForm", () => new ReportMenuForm()),
    ("17-HelpForm", () => new HelpForm()),
};

// 泛型維護：渲染主要主檔表（客戶廠商、倉庫資料、權限主檔）與複合主鍵 Editable 表
var genericTables = new[] { "客戶廠商", "倉庫資料", "權限主檔", "權限明細" };
foreach (var t in genericTables)
    targets.Add(($"07-GenericTableForm-{t}", () => new GenericTableForm(t)));

Console.WriteLine($"輸出目錄: {outDir}\nDB: {config.DatabasePath}\n");
int ok = 0, fail = 0;
foreach (var (name, factory) in targets)
{
    if (RenderOne(name, factory, outDir)) ok++; else fail++;
}
Console.WriteLine($"\n=== 總計: {ok} OK / {fail} FAIL / {ok + fail} 表單 ===");
return 0;

bool RenderOne(string name, Func<Form> factory, string outDir)
{
    Exception? error = null;
    var done = false;
    lock (uiLock) { uiError = null; }
    var t = new Thread(() =>
    {
        try
        {
            using var form = factory();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-4000, -4000);   // 螢幕外渲染
            form.Size = new Size(1440, 900);
            form.CreateControl();
            form.Show();                               // 真正顯示以觸發完整 WM_PAINT（DrawToBitmap 對未顯示視窗只渲染頂部）
            form.PerformLayout();
            for (int i = 0; i < 8; i++)                // 讓 async 資料載入有機會完成
            {
                Application.DoEvents();
                Thread.Sleep(150);
            }
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
            bmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
            Console.WriteLine($"OK   {name}  {form.Width}x{form.Height}");
        }
        catch (Exception ex) { error = ex; }
        finally { done = true; }
    });
    t.SetApartmentState(ApartmentState.STA);
    t.IsBackground = true;
    t.Start();
    if (!t.Join(TimeSpan.FromSeconds(25)))
    {
        Console.WriteLine($"TIMEOUT {name}");
        return false;
    }
    if (error is not null)
    {
        Console.WriteLine($"FAIL  {name}  {error}");
        return false;
    }
    lock (uiLock)
    {
        if (uiError is not null)
        {
            Console.WriteLine($"FAIL  {name}  表單內未處理例外: {uiError.Message}");
            return false;
        }
    }
    return done;
}
