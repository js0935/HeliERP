// ════════════════════════════════════════════════════════
// CrudCheck：CRUD 端到端驗證工具
// 在「真實資料庫副本」上，透過反射直接操作 ProductMaintenanceForm 的
// 實際程式碼路徑（含已修復的 3 個功能 bug），驗證新增/修改/刪除/儲存流程。
// 不污染正式資料庫（使用副本 + 測試前後清理測試資料）。
// ════════════════════════════════════════════════════════
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using HeliERP.App;
using HeliERP.Data;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── 測試資料庫副本（來源：正式 DB；副本：執行目錄）──
const string SourceDb = @"D:\HeliAcc\HeliERP.db";
const string TableName = "貨品主檔";
const string PkColumn = "貨品編號";
const string TestPrefix = "CRUD-%";
// 複合主鍵測試表：權限明細（使用者編號 TEXT + 序號 INTEGER，Editable）
const string CompTable = "權限明細";
const string CompTestUser = "CRUD-COMP-001";

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

// 清理上次殘留的測試資料（含診斷殘留的 NULL 主鍵列）
DbManager.ExecuteNonQuery($"DELETE FROM \"{TableName}\" WHERE \"{PkColumn}\" IS NULL OR \"{PkColumn}\" LIKE '{TestPrefix}'");
DbManager.ExecuteNonQuery($"DELETE FROM \"{CompTable}\" WHERE \"使用者編號\" LIKE '{CompTestUser}%'");
Console.WriteLine($"清理後貨品主檔筆數：{DbManager.QueryScalar($"SELECT COUNT(*) FROM \"{TableName}\"")}");

// ── MessageBox 自動點擊器：測試環境中自動按下對話框按鈕 ──
using var cts = new CancellationTokenSource();
StartMessageBoxCloser(cts.Token);

Console.WriteLine($"=== CrudCheck：CRUD 端到端驗證 ===");
Console.WriteLine($"DB: {testDb}\n");

// ── 診斷：探針 INSERT（定位新增失敗根因）──
DiagnoseInsert();

// ── 全表主鍵盤點（無主鍵的表 = CRUD 定位失效）──
var tables = SchemaReader.GetTables().OrderBy(x => x.Key).ToList();
var noPk = tables.Where(x => x.Value.PrimaryKey.Count == 0).ToList();
Console.WriteLine($"全表盤點：{tables.Count} 張表，其中 {noPk.Count} 張無主鍵：");
foreach (var kv in noPk)
    Console.WriteLine($"  無主鍵  {kv.Key}");
Console.WriteLine();

int pass = 0, fail = 0;
var results = new List<(string Name, bool Ok, string Detail)>();

// ── C1：新增 ──
results.Add(RunCase("C1 新增", () =>
{
    using var form = new ProductMaintenanceForm();
    var dt = F<DataTable>(form, "_dt");
    var cols = F<List<string>>(form, "_columns");
    var pks = F<List<string>>(form, "_pkColumns");
    var nonPk = cols.First(c => !pks.Contains(c));

    var row = dt.NewRow();
    row[PkColumn] = "CRUD-001";
    row[nonPk] = "CRUD 測試品一號";
    dt.Rows.Add(row);
    Invoke(form, "SaveChanges");                       // 彈「儲存完成」→ 自動按確定

    var name = DbManager.QueryScalar(
        $"SELECT \"{nonPk}\" FROM \"{TableName}\" WHERE \"{PkColumn}\" = $k",
        DbManager.Param("$k", "CRUD-001"));
    if (name is not string s || s != "CRUD 測試品一號")
        throw new Exception($"新增未寫入資料庫：取得 {name ?? "null"}");
    return "資料庫已新增 CRUD-001，品名正確";
}));

// ── C2：重複主鍵防護（編輯期放寬後，儲存時由 _existingKeys 攔截重複新增）──
results.Add(RunCase("C2 重複主鍵防護", () =>
{
    using var form = new ProductMaintenanceForm();
    var dt = F<DataTable>(form, "_dt");
    var cols = F<List<string>>(form, "_columns");
    var pks = F<List<string>>(form, "_pkColumns");
    var nonPk = cols.First(c => !pks.Contains(c));

    // 編輯期允許輸入重複主鍵列（PrimaryKey 已放寬以支援多列輸入），
    // 儲存時 SaveChanges 依 _existingKeys 略過重複列、RejectChanges 並提示。
    var row = dt.NewRow();
    row[PkColumn] = "CRUD-001";                        // C1 已存在於資料庫
    row[nonPk] = "不應寫入的品名";
    dt.Rows.Add(row);
    Invoke(form, "SaveChanges");                       // 彈「部分資料未儲存」→ 自動按確定

    if (row.RowState == DataRowState.Added)
        throw new Exception("重複主鍵列未被 SaveChanges 攔截（應 RejectChanges）");

    var cnt = (long)(DbManager.QueryScalar(
        $"SELECT COUNT(*) FROM \"{TableName}\" WHERE \"{PkColumn}\" = $k",
        DbManager.Param("$k", "CRUD-001")) ?? 0L);
    if (cnt != 1) throw new Exception($"應僅 1 筆，實際 {cnt} 筆");
    return $"儲存時攔截重複主鍵（資料庫仍 {cnt} 筆）";
}));

// ── C3：修改非主鍵欄（bug #3：SET 與 WHERE 參數 $p_ 前綴不衝突）──
results.Add(RunCase("C3 修改非主鍵欄", () =>
{
    using var form = new ProductMaintenanceForm();
    var dt = F<DataTable>(form, "_dt");
    var cols = F<List<string>>(form, "_columns");
    var pks = F<List<string>>(form, "_pkColumns");
    var nonPk = cols.First(c => !pks.Contains(c));

    var row = FindRow(dt, PkColumn, "CRUD-001");
    row[nonPk] = "CRUD 測試品一號-改";
    Invoke(form, "SaveChanges");

    var name = DbManager.QueryScalar(
        $"SELECT \"{nonPk}\" FROM \"{TableName}\" WHERE \"{PkColumn}\" = $k",
        DbManager.Param("$k", "CRUD-001"));
    if (name is not string s || s != "CRUD 測試品一號-改")
        throw new Exception($"修改未寫入資料庫：取得 {name ?? "null"}");
    return "資料庫品名已更新為「CRUD 測試品一號-改」";
}));

// ── C4：刪除（bug #1：完整 DeleteRows → Delete() 標記 → SaveChanges 執行 DELETE）──
results.Add(RunCase("C4 刪除", () =>
{
    using var form = new ProductMaintenanceForm();
    form.StartPosition = FormStartPosition.Manual;
    form.Location = new Point(-4000, -4000);
    form.Show();                                       // DataGridView 列綁定需表單顯示才完整
    Application.DoEvents();
    var dt = F<DataTable>(form, "_dt");
    var grid = F<DataGridView>(form, "_grid");

    var row = FindRow(dt, PkColumn, "CRUD-001");
    DataGridViewRow? target = null;
    foreach (DataGridViewRow gRow in grid.Rows)
    {
        if (gRow.DataBoundItem is DataRowView drv && ReferenceEquals(drv.Row, row))
        { target = gRow; break; }
    }
    if (target is null) throw new Exception("前置資料 CRUD-001 不在 DataGridView 中");

    grid.ClearSelection();
    target.Selected = true;
    Invoke(form, "DeleteRows");                        // 彈「刪除確認(是/否)」→ 自動按「是」

    if (row.RowState != DataRowState.Deleted)
        throw new Exception($"Delete() 標記未生效：狀態 {row.RowState}");

    Invoke(form, "SaveChanges");                       // 彈「儲存完成」→ 自動按確定

    var cnt = (long)(DbManager.QueryScalar(
        $"SELECT COUNT(*) FROM \"{TableName}\" WHERE \"{PkColumn}\" = $k",
        DbManager.Param("$k", "CRUD-001")) ?? 0L);
    if (cnt != 0) throw new Exception($"刪除未生效：資料庫仍剩 {cnt} 筆");
    return "資料庫已無 CRUD-001（Delete 標記 → DELETE 執行成功）";
}));

// ── C5：空儲存（無任何修改，應安全 no-op）──
results.Add(RunCase("C5 空儲存", () =>
{
    using var form = new ProductMaintenanceForm();
    var before = (long)(DbManager.QueryScalar($"SELECT COUNT(*) FROM \"{TableName}\"") ?? 0L);
    Invoke(form, "SaveChanges");                       // 應無異常、彈「儲存完成」
    var after = (long)(DbManager.QueryScalar($"SELECT COUNT(*) FROM \"{TableName}\"") ?? 0L);
    if (before != after) throw new Exception($"空儲存不應變動資料：{before} → {after}");
    return $"無修改儲存安全（資料筆數不變：{after}）";
}));

// ── C6：複合主鍵新增（bug：KeyOf 對 INTEGER PK 欄位轉型失敗 → 序號變空字串）──
results.Add(RunCase("C6 複合鍵新增", () =>
{
    using var form = new GenericTableForm(CompTable);
    var dt = F<DataTable>(form, "_dt");
    var cols = F<List<string>>(form, "_columns");
    var pks = F<List<string>>(form, "_pkColumns");
    if (pks.Count != 2) throw new Exception($"應為複合主鍵（2 欄），實際 {pks.Count} 欄：{string.Join(",", pks)}");
    var nonPk = cols.First(c => !pks.Contains(c));

    var row = dt.NewRow();
    row["使用者編號"] = CompTestUser;
    row["序號"] = 1L;
    row[nonPk] = "複合鍵測試一號";
    dt.Rows.Add(row);
    Invoke(form, "SaveChanges");                       // 彈「儲存完成」→ 自動按確定

    var got = DbManager.QueryScalar(
        $"SELECT \"{nonPk}\" FROM \"{CompTable}\" WHERE \"使用者編號\" = $u AND \"序號\" = 1",
        DbManager.Param("$u", CompTestUser));
    if (got is not string s || s != "複合鍵測試一號")
        throw new Exception($"複合鍵新增未寫入：取得 {got ?? "null"}");
    return $"資料庫已新增 {CompTestUser}/1，複合主鍵定位正確";
}));

// ── C7：複合主鍵重複防護（同使用者不同序號應為不同鍵；同鍵應被攔截）──
results.Add(RunCase("C7 複合鍵重複防護", () =>
{
    using var form = new GenericTableForm(CompTable);
    var dt = F<DataTable>(form, "_dt");
    var cols = F<List<string>>(form, "_columns");
    var pks = F<List<string>>(form, "_pkColumns");
    var nonPk = cols.First(c => !pks.Contains(c));

    // 同使用者 + 不同序號（序號 2）：若 KeyOf 把 INTEGER 欄位轉成空字串，會被誤判與序號 1 同鍵
    var row2 = dt.NewRow();
    row2["使用者編號"] = CompTestUser;
    row2["序號"] = 2L;
    row2[nonPk] = "複合鍵測試二號";
    dt.Rows.Add(row2);
    Invoke(form, "SaveChanges");
    var cnt2 = (long)(DbManager.QueryScalar(
        $"SELECT COUNT(*) FROM \"{CompTable}\" WHERE \"使用者編號\" = $u", DbManager.Param("$u", CompTestUser)) ?? 0L);
    if (cnt2 != 2)
        throw new Exception($"不同序號被誤判為同鍵：應 2 筆，實際 {cnt2} 筆");

    // 同使用者 + 同序號（序號 1 重複）：應被 DataTable UniqueConstraint 攔截（複合鍵）
    var rowDup = dt.NewRow();
    rowDup["使用者編號"] = CompTestUser;
    rowDup["序號"] = 1L;
    rowDup[nonPk] = "不應寫入的重複鍵";
    bool blocked = false;
    try { dt.Rows.Add(rowDup); }
    catch (ConstraintException) { blocked = true; }
    if (!blocked) throw new Exception("重複複合鍵未被 UniqueConstraint 攔截");
    var cnt1 = (long)(DbManager.QueryScalar(
        $"SELECT COUNT(*) FROM \"{CompTable}\" WHERE \"使用者編號\" = $u AND \"序號\" = 1", DbManager.Param("$u", CompTestUser)) ?? 0L);
    if (cnt1 != 1)
        throw new Exception($"重複複合鍵未被攔截：序號 1 應僅 1 筆，實際 {cnt1} 筆");
    return $"不同序號可並存（2 筆）、同鍵被 UniqueConstraint 攔截（序號 1 仍 1 筆）";
}));

// ── C8：複合主鍵修改（bug：KeyOf 序號為空 → WHERE 序號 = '' 永不匹配 → 靜默失敗）──
results.Add(RunCase("C8 複合鍵修改", () =>
{
    using var form = new GenericTableForm(CompTable);
    var dt = F<DataTable>(form, "_dt");
    var cols = F<List<string>>(form, "_columns");
    var pks = F<List<string>>(form, "_pkColumns");
    var nonPk = cols.First(c => !pks.Contains(c));

    var row = dt.Rows.Cast<DataRow>()
        .First(r => (r["使用者編號"] as string ?? "") == CompTestUser && Convert.ToInt64(r["序號"]) == 1L);
    row[nonPk] = "複合鍵測試一號-改";
    Invoke(form, "SaveChanges");

    var got = DbManager.QueryScalar(
        $"SELECT \"{nonPk}\" FROM \"{CompTable}\" WHERE \"使用者編號\" = $u AND \"序號\" = 1",
        DbManager.Param("$u", CompTestUser));
    if (got is not string s || s != "複合鍵測試一號-改")
        throw new Exception($"複合鍵修改未生效：取得 {got ?? "null"}（WHERE 序號條件可能為空字串）");
    var cnt = (long)(DbManager.QueryScalar(
        $"SELECT COUNT(*) FROM \"{CompTable}\" WHERE \"使用者編號\" = $u", DbManager.Param("$u", CompTestUser)) ?? 0L);
    if (cnt != 2)
        throw new Exception($"修改誤傷其他列：應 2 筆，實際 {cnt} 筆");
    return "複合鍵 WHERE 定位正確更新（僅更新目標列）";
}));

// ── C9：複合主鍵刪除（bug：KeyOfDeleted 對 boxed long 強制轉 string 拋 InvalidCastException）──
results.Add(RunCase("C9 複合鍵刪除", () =>
{
    using var form = new GenericTableForm(CompTable);
    form.StartPosition = FormStartPosition.Manual;
    form.Location = new Point(-4000, -4000);
    form.Show();
    Application.DoEvents();
    var dt = F<DataTable>(form, "_dt");
    var grid = F<DataGridView>(form, "_grid");

    var row = dt.Rows.Cast<DataRow>()
        .First(r => (r["使用者編號"] as string ?? "") == CompTestUser && Convert.ToInt64(r["序號"]) == 2L);
    DataGridViewRow? target = null;
    foreach (DataGridViewRow gRow in grid.Rows)
    {
        if (gRow.DataBoundItem is DataRowView drv && ReferenceEquals(drv.Row, row))
        { target = gRow; break; }
    }
    if (target is null) throw new Exception("前置資料序號 2 不在 DataGridView 中");

    grid.ClearSelection();
    target.Selected = true;
    Invoke(form, "DeleteRows");                        // 彈「刪除確認」→ 自動按「是」
    Invoke(form, "SaveChanges");

    var cnt = (long)(DbManager.QueryScalar(
        $"SELECT COUNT(*) FROM \"{CompTable}\" WHERE \"使用者編號\" = $u AND \"序號\" = 2", DbManager.Param("$u", CompTestUser)) ?? 0L);
    if (cnt != 0) throw new Exception($"複合鍵刪除未生效：序號 2 仍剩 {cnt} 筆");
    return "複合鍵 DELETE 定位正確（僅刪除目標列）";
}));

// ── 收尾：清理測試資料 + 輸出結果 ──
DbManager.ExecuteNonQuery($"DELETE FROM \"{TableName}\" WHERE \"{PkColumn}\" IS NULL OR \"{PkColumn}\" LIKE '{TestPrefix}'");
DbManager.ExecuteNonQuery($"DELETE FROM \"{CompTable}\" WHERE \"使用者編號\" LIKE '{CompTestUser}%'");
cts.Cancel();

foreach (var (name, ok, detail) in results)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
    if (ok) pass++; else fail++;
}
Console.WriteLine($"\n=== 總計: {pass} PASS / {fail} FAIL ===");
return fail == 0 ? 0 : 1;

// ════════════════════════════════════════════════════════
// 工具函式
// ════════════════════════════════════════════════════════

// 在獨立 STA 執行緒執行測試案例（WinForms 控制項需求），逾時 30 秒
static (string Name, bool Ok, string Detail) RunCase(string name, Func<string> test)
{
    Exception? error = null;
    string detail = "";
    var done = false;
    var t = new Thread(() =>
    {
        try { detail = test(); }
        catch (Exception ex)
        {
            error = ex;
            var st = (ex.StackTrace ?? "").Split('\n').Where(l => l.Trim().Length > 0).Take(3);
            detail = $"{ex.GetType().Name}: {ex.Message} | {string.Join(" | ", st.Select(l => l.Trim()))}";
        }
        finally { done = true; }
    });
    t.SetApartmentState(ApartmentState.STA);
    t.IsBackground = true;
    t.Start();
    if (!t.Join(TimeSpan.FromSeconds(30)))
        return (name, false, "TIMEOUT（30 秒）");
    return (name, error is null && done, detail);
}

// 診斷：印出貨品主檔欄位結構、主鍵定義與資料完整性，並執行「僅主鍵欄」探針 INSERT 以定位新增失敗根因
static void DiagnoseInsert()
{
    try
    {
        var cols = new List<string>();
        var pragmaPks = new List<string>();
        using (var conn = DbManager.OpenConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(\"貨品主檔\")";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                cols.Add($"{r.GetString(1)}({r.GetString(2)}{(r.GetInt32(3) == 1 ? ",NOT NULL" : "")})");
                if (r.GetInt32(5) > 0) pragmaPks.Add($"{r.GetInt32(5)}:{r.GetString(1)}");
            }
        }
        Console.WriteLine($"貨品主檔欄位：{string.Join(" ", cols)}");
        Console.WriteLine($"PRAGMA 主鍵：{(pragmaPks.Count == 0 ? "（無主鍵！）" : string.Join(", ", pragmaPks))}");
        var schemaPk = SchemaReader.GetTable("貨品主檔")?.PrimaryKey;
        Console.WriteLine($"SchemaReader 主鍵：{(schemaPk is null || schemaPk.Count == 0 ? "（空！）" : string.Join(", ", schemaPk))}");
        Console.WriteLine($"貨品編號空白列數：{DbManager.QueryScalar("SELECT COUNT(*) FROM \"貨品主檔\" WHERE \"貨品編號\" IS NULL OR TRIM(\"貨品編號\") = ''")}");
        Console.WriteLine($"貨品編號重複組數：{DbManager.QueryScalar("SELECT COUNT(*) FROM (SELECT \"貨品編號\" FROM \"貨品主檔\" GROUP BY \"貨品編號\" HAVING COUNT(*) > 1)")}");
        using (var conn = DbManager.OpenConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA index_list(\"貨品主檔\")";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                Console.WriteLine($"索引：{r.GetString(1)}  unique={r.GetInt32(2)}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DIAG 讀取欄位失敗：{ex.GetType().Name}: {ex.Message}");
    }

    try
    {
        DbManager.ExecuteTransaction(tx =>
        {
            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO \"貨品主檔\" (\"貨品編號\") VALUES ($p0)";
            cmd.Parameters.Add(DbManager.Param("$p0", "CRUD-DIAG"));
            cmd.ExecuteNonQuery();
        });
        Console.WriteLine("DIAG 探針 INSERT 成功（僅主鍵欄，其餘欄位可空）");
        DbManager.ExecuteNonQuery("DELETE FROM \"貨品主檔\" WHERE \"貨品編號\" = 'CRUD-DIAG'");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DIAG 探針 INSERT 失敗：{ex.GetType().Name}: {ex.Message}");
    }

    try
    {
        var cols = new List<string>();
        using (var conn = DbManager.OpenConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM pragma_table_info('貨品主檔')";
            using var r = cmd.ExecuteReader();
            while (r.Read()) cols.Add(r.GetString(0));
        }
        var names = string.Join(",", cols.Select(c => $"\"{c}\""));
        var pars = string.Join(",", cols.Select(c => $"${c}"));
        DbManager.ExecuteTransaction(tx =>
        {
            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"INSERT INTO \"貨品主檔\" ({names}) VALUES ({pars})";
            cmd.Parameters.Add(DbManager.Param($"${cols[0]}", "CRUD-DIAG"));
            for (int i = 1; i < cols.Count; i++)
                cmd.Parameters.Add(DbManager.Param($"${cols[i]}", null));
            cmd.ExecuteNonQuery();
        });
        Console.WriteLine($"DIAG 完整 INSERT 成功（{cols.Count} 欄位、中文參數名）");
        DbManager.ExecuteNonQuery("DELETE FROM \"貨品主檔\" WHERE \"貨品編號\" = 'CRUD-DIAG'");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DIAG 完整 INSERT 失敗：{ex.GetType().Name}: {ex.Message}");
    }
    Console.WriteLine();
}

// 反射讀取 private 欄位
static T F<T>(object obj, string fieldName) =>
    (T)(obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingFieldException(obj.GetType().Name, fieldName)).GetValue(obj)!;

// 反射呼叫 private 方法
static void Invoke(object obj, string methodName) =>
    (obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingMethodException(obj.GetType().Name, methodName)).Invoke(obj, null);

static DataRow FindRow(DataTable dt, string pkColumn, string key) =>
    dt.Rows.Cast<DataRow>().First(r => ((r[pkColumn] as string) ?? "").Trim() == key);

// MessageBox 自動點擊器：讀取對話框文字（診斷被吞掉的例外訊息）並按下第一個按鈕（是/確定）
static void StartMessageBoxCloser(CancellationToken ct)
{
    string lastText = "";
    var t = new Thread(() =>
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var hwnd = Native.FindWindow("#32770", null);
                if (hwnd != IntPtr.Zero)
                {
                    var sb = new StringBuilder(1024);
                    var staticHwnd = IntPtr.Zero;
                    while ((staticHwnd = Native.FindWindowEx(hwnd, staticHwnd, "Static", null)) != IntPtr.Zero)
                    {
                        sb.Clear();
                        Native.GetWindowText(staticHwnd, sb, 1024);
                        if (sb.Length > 0) break;
                    }
                    var text = sb.ToString().Trim();
                    if (text.Length > 0 && text != lastText)
                    {
                        lastText = text;
                        Console.WriteLine($"[MessageBox] {text}");
                    }
                    var btn = Native.FindWindowEx(hwnd, IntPtr.Zero, "Button", null);
                    if (btn != IntPtr.Zero)
                        Native.SendMessage(btn, Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch { }
            Thread.Sleep(80);
        }
    });
    t.IsBackground = true;
    t.Start();
}

// Win32 使用者介面 API（MessageBox 自動點擊用）
static class Native
{
    public const uint BM_CLICK = 0x00F5;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
