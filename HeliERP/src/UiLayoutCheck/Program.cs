// ════════════════════════════════════════════════════════
// UiLayoutCheck：主畫面儀表板版面程式化驗證
// 實例化 MainForm，遞迴遍歷控制項樹，驗證：
// 統計卡數字與 DashboardService 一致、卡片無重疊、
// 庫存不足警示卡表格列數正確、按鈕齊全。
// 唯讀連線正式資料庫，不寫入任何資料。
// ════════════════════════════════════════════════════════
using System.Drawing;
using System.Globalization;
using HeliERP.App;
using HeliERP.Data;
using HeliERP.Models;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = System.Text.Encoding.UTF8;

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
    if (ok) pass++; else fail++;
}

var config = DbConfig.Load();
var user = new AppUser { UserId = "U001", DisplayName = "測試使用者", IsAdmin = true };

const string SourceDb = @"D:\HeliAcc\HeliERP.db";
string testDb = Path.Combine(AppContext.BaseDirectory, "HeliERP.db");
if (!File.Exists(SourceDb))
{
    Console.WriteLine($"FAIL 找不到來源資料庫：{SourceDb}");
    return 1;
}
File.Copy(SourceDb, testDb, overwrite: true);
DbManager.DatabasePath = testDb;

using var form = new MainForm(config, user);
form.StartPosition = FormStartPosition.Manual;
form.Location = new Point(-4000, -4000);
form.Size = new Size(1440, 900);
form.CreateControl();
form.Show();
for (int i = 0; i < 8; i++)
{
    Application.DoEvents();
    Thread.Sleep(150);
}

var allLabels = new List<Label>();
var cards = new List<(Panel Card, string Title, string Big, string Sub)>();
var wideCards = new List<(Panel Card, DataGridView? Grid, ModernButton? Btn)>();
var allGrids = new List<DataGridView>();

void Walk(Control parent)
{
    foreach (Control c in parent.Controls)
    {
        if (c is Label l)
            allLabels.Add(l);
        if (c is DataGridView dg)
            allGrids.Add(dg);
        if (c is Panel p)
        {
            if (p.Size == new Size(300, 132))
            {
                string title = "", big = "", sub = "";
                foreach (Control pc in p.Controls)
                {
                    if (pc is Label pl)
                    {
                        if (pl.Font.Size >= 20F) big = pl.Text;
                        else if (pl.Font.Size >= 11F) title = pl.Text;
                        else sub = pl.Text;
                    }
                }
                cards.Add((p, title, big, sub));
            }
            if (p.Size == new Size(640, 190))
            {
                DataGridView? g = null;
                ModernButton? b = null;
                foreach (Control pc in p.Controls)
                {
                    if (pc is DataGridView pg) g = pg;
                    if (pc is ModernButton pb && pb.Text == "開啟庫存管理") b = pb;
                }
                wideCards.Add((p, g, b));
            }
        }
        Walk(c);
    }
}
Walk(form);

// ── 1. 統計卡數字與 DashboardService 一致 ──
var dash = DashboardService.Load();
var stat = cards.ToDictionary(x => x.Title, x => (x.Big, x.Sub));

Check("公司資訊卡存在", cards.Any(x => x.Title == "公司資訊"),
    $"卡片數 {cards.Count}");

bool StatOk(string title, string expectedBig)
{
    return stat.TryGetValue(title, out var v) && v.Big == expectedBig;
}
Check("庫存不足卡數字", StatOk("庫存不足", $"{dash.庫存不足筆數} 項"),
    $"顯示「{dash.庫存不足筆數} 項」");
Check("應收帳款餘額卡", StatOk("應收帳款餘額", dash.應收餘額.ToString("N0")),
    $"顯示「{dash.應收餘額:N0}」");
Check("應付帳款餘額卡", StatOk("應付帳款餘額", dash.應付餘額.ToString("N0")),
    $"顯示「{dash.應付餘額:N0}」");
Check("今日出貨卡", StatOk("今日出貨", dash.今日出貨金額.ToString("N0")),
    $"顯示「{dash.今日出貨金額:N0}」");
Check("本月進貨卡", StatOk("本月進貨", dash.本月進貨金額.ToString("N0")),
    $"顯示「{dash.本月進貨金額:N0}」");

// ── 2. 卡片無重疊（絕對座標）──
bool overlap = false;
string? overlapDetail = null;
for (int i = 0; i < cards.Count; i++)
{
    for (int j = i + 1; j < cards.Count; j++)
    {
        var a = cards[i].Card.RectangleToScreen(cards[i].Card.ClientRectangle);
        var b = cards[j].Card.RectangleToScreen(cards[j].Card.ClientRectangle);
        if (a.IntersectsWith(b))
        {
            overlap = true;
            overlapDetail = $"「{cards[i].Title}」與「{cards[j].Title}」重疊";
        }
    }
}
Check("統計卡無重疊", !overlap, overlapDetail ?? "6 張卡片互不重疊");

// ── 3. 庫存不足警示卡 ──
Check("警示卡存在", wideCards.Count == 1, $"卡片數 {wideCards.Count}");
if (wideCards.Count == 1)
{
    var (_, grid, btn) = wideCards[0];
    int expectedRows = dash.庫存不足清單.Rows.Count;
    Check("警示卡表格列數", grid is not null && grid!.Rows.Count == expectedRows,
        grid is null ? "無表格" : $"列數 {grid.Rows.Count} = 預期 {expectedRows}");
    if (grid is not null)
    {
        var visible = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Select(c => c.Name).ToList();
        Check("警示卡表格欄位", visible.SequenceEqual(new[] { "貨品編號", "品名", "倉庫名稱", "現有數量", "安全存量" }),
            $"可見欄: {string.Join(", ", visible)}");
    }
    Check("開啟庫存管理按鈕", btn is not null, btn is null ? "無按鈕" : "按鈕存在");
}

// ── 4. 快速入口卡與標題 ──
Check("快速入口卡存在", allLabels.Any(x => x.Text == "快速入口"), "標題存在");
Check("庫存不足警示標題", allLabels.Any(x => x.Text == "庫存不足警示"), "標題存在");

Console.WriteLine($"\n=== 結果：{pass} 通過 / {fail} 失敗 ===");
return fail == 0 ? 0 : 1;
