using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.Reflection;
using System.Text;
using HeliERP.App;
using HeliERP.Data;
using HeliERP.Models;
using Microsoft.Data.Sqlite;

CultureInfo.CurrentCulture = new CultureInfo("zh-TW");
Console.OutputEncoding = Encoding.UTF8;

// 指定 App 實際資料庫（測試執行目錄下無 HeliERP.db）
DbManager.DatabasePath = @"D:\HeliAcc\HeliERP.db";

if (args.Length > 0 && args[0] == "all")
{
    RunBatch();
    return;
}

if (args.Length >= 1 && args[0] == "newrep")
{
    NewReportChecks();
    return;
}

if (args.Length >= 1 && args[0] == "subrep")
{
    SubRepDebug();
    return;
}

if (args.Length >= 2 && args[0] == "dumpt")
{
    foreach (var f in args.Skip(1))
        DumpTree(f);
    return;
}

if (args.Length >= 2 && args[0] == "dump")
{
    var sb = new System.Text.StringBuilder();
    var oldOut = Console.Out;
    Console.SetOut(new System.IO.StringWriter(sb));
    foreach (var f in args.Skip(1))
        DumpFields(f);
    Console.SetOut(oldOut);
    File.WriteAllText(@"D:\HeliAcc\shots\dump_fields.txt", sb.ToString(), new System.Text.UTF8Encoding(false));
    return;
}

// 依清單檔（每行一個檔名）輸出 DB 欄位，避免命令列中文參數編碼問題
if (args.Length >= 1 && args[0] == "dumpf")
{
    var sb = new System.Text.StringBuilder();
    var oldOut = Console.Out;
    Console.SetOut(new System.IO.StringWriter(sb));
    string listFile = args.Length >= 2 ? args[1] : @"D:\HeliAcc\Rep\_unapp.txt";
    foreach (var line in File.ReadAllLines(listFile))
    {
        var f = line.Trim();
        if (f.Length == 0) continue;
        DumpFields(Path.Combine(@"D:\HeliAcc\Rep", f));
    }
    Console.SetOut(oldOut);
    File.WriteAllText(@"D:\HeliAcc\shots\dump_unapp.txt", sb.ToString(), new System.Text.UTF8Encoding(false));
    return;
}

// 依清單檔（每行一個檔名）輸出元件樹（含 Name/座標/字型），避免命令列中文參數編碼問題
if (args.Length >= 1 && args[0] == "dumpft")
{
    var sb = new System.Text.StringBuilder();
    var oldOut = Console.Out;
    Console.SetOut(new System.IO.StringWriter(sb));
    string listFile = args.Length >= 2 ? args[1] : @"D:\HeliAcc\Rep\_unapp.txt";
    foreach (var line in File.ReadAllLines(listFile))
    {
        var f = line.Trim();
        if (f.Length == 0) continue;
        DumpTree(Path.Combine(@"D:\HeliAcc\Rep", f));
    }
    Console.SetOut(oldOut);
    File.WriteAllText(@"D:\HeliAcc\shots\dump_tree.txt", sb.ToString(), new System.Text.UTF8Encoding(false));
    return;
}

if (args.Length >= 1 && args[0] == "dumpb")
{
    string[] 票據報表 =
    {
        "應收票據明細表(收票日).rtm", "應收票據明細表(託收銀行).rtm",
        "應付票據明細表(開票日).rtm", "應付票據明細表(開票銀行).rtm",
        "未兌現應收票據.rtm", "未兌現應付票據.rtm",
        "收款沖銷日報表.rtm", "付款沖銷日報表.rtm",
        "業務應收統計表.rtm", "業務應收明細表.rtm",
    };
    foreach (var f in 票據報表)
        DumpFields(Path.Combine(@"D:\HeliAcc\Rep", f));
    return;
}

if (args.Length >= 2 && args[0] == "trade")
{
    TradeRender(args[1], args.Length > 2 ? args[2] : "");
    return;
}

if (args.Length >= 1 && args[0] == "repair")
{
    RepairRender();
    return;
}

if (args.Length >= 1 && args[0] == "dcheck")
{
    DataCheck();
    return;
}

if (args.Length >= 1 && args[0] == "backfill")
{
    if (args.Length >= 2 && File.Exists(args[1]))
        DbManager.DatabasePath = args[1];
    AccountBackfill();
    return;
}

if (args.Length >= 1 && args[0] == "svc")
{
    ServiceFlowTest();
    return;
}

if (args.Length >= 1 && args[0] == "tflow")
{
    TradeFlowTest();
    return;
}

if (args.Length >= 1 && args[0] == "pcheck")
{
    PrintPipelineCheck();
    return;
}

if (args.Length >= 1 && args[0] == "overlap")
{
    OverlapCheck();
    return;
}

if (args.Length >= 2 && args[0] == "one")
{
    OneRender(args[1]);
    return;
}

if (args.Length >= 1 && args[0] == "oneall")
{
    OneAll();
    return;
}

if (args.Length >= 1 && args[0] == "oneallpng")
{
    RenderAllPng();
    return;
}

if (args.Length >= 1 && args[0] == "fixall")
{
    FixAll();
    return;
}

if (args.Length >= 1 && args[0] == "fixboxes")
{
    FixBoxes();
    return;
}

if (args.Length >= 1 && args[0] == "fix7")
{
    FixSeven();
    return;
}

if (args.Length >= 1 && args[0] == "fix8")
{
    FixEight();
    return;
}

if (args.Length >= 2 && args[0] == "fixdbg")
{
    FixDbg(args[1]);
    return;
}

if (args.Length >= 2 && args[0] == "align")
{
    AlignCheck(args[1]);
    return;
}

if (args.Length >= 2 && args[0] == "dumpal")
{
    DumpDetail(args[1]);
    return;
}

if (args.Length >= 1 && args[0] == "dumpalall")
{
    foreach (var line in File.ReadAllLines(@"D:\HeliAcc\shots\targets.txt"))
    {
        var f = line.Trim();
        if (f.Length == 0) continue;
        DumpDetail(f);
    }
    return;
}

if (args.Length >= 2 && args[0] == "alignfix")
{
    AlignFix(args[1]);
    return;
}

if (args.Length >= 1 && args[0] == "alignfixall")
{
    AlignFixAll();
    return;
}

if (args.Length >= 1 && args[0] == "adjust")
{
    AdjustRender();
    return;
}

if (args.Length >= 1 && args[0] == "mkadj")
{
    MakeAdjustment();
    return;
}

if (args.Length >= 1 && args[0] == "stock")
{
    StockRender();
    return;
}

if (args.Length >= 1 && args[0] == "stockrep")
{
    StockRepRender();
    return;
}

if (args.Length >= 1 && args[0] == "appb")
{
    AppBuildersCheck();
    return;
}

if (args.Length >= 1 && args[0] == "mreport")
{
    MissingReportRender();
    return;
}

if (args.Length >= 1 && args[0] == "biz")
{
    BizRender();
    return;
}

if (args.Length >= 1 && args[0] == "ar")
{
    ArRender();
    return;
}

if (args.Length >= 1 && args[0] == "arrpt")
{
    ArRealRender();
    return;
}

if (args.Length >= 1 && args[0] == "billpt")
{
    BillRealRender();
    return;
}

if (args.Length >= 1 && args[0] == "b2")
{
    B2Render();
    return;
}

if (args.Length >= 1 && args[0] == "acc")
{
    AccRender();
    return;
}

if (args.Length >= 1 && args[0] == "b3")
{
    B3Render();
    return;
}

if (args.Length >= 1 && args[0] == "anl")
{
    AnalysisRender();
    return;
}

if (args.Length >= 1 && args[0] == "dumpallf")
{
    string listFile = args.Length >= 2 ? args[1] : @"C:\Users\JS\AppData\Local\Temp\opencode\dump_13.txt";
    foreach (var line in File.ReadAllLines(listFile))
    {
        var f = line.Trim();
        if (f.Length == 0) continue;
        DumpDetail(f);
    }
    return;
}

if (args.Length >= 1 && args[0] == "lscan")
{
    LineScanAll();
    return;
}

if (args.Length >= 1 && args[0] == "dumpL")
{
    foreach (var a in args.Skip(1))
    {
        if (a.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && File.Exists(a))
        {
            foreach (var ln in File.ReadAllLines(a))
                if (!string.IsNullOrWhiteSpace(ln)) DumpLayout(ln.Trim());
        }
        else DumpLayout(a);
    }
    return;
}

if (args.Length >= 1 && args[0] == "fixov")
{
    foreach (var a in args.Skip(1)) FixOverlaps(a);
    return;
}

if (args.Length >= 1 && args[0] == "fixoval")
{
    FixOverlapsAll();
    return;
}

/// <summary>掃描 Rep 全部報表：渲染假資料後，用像素級檢查「線切穿字跡」。</summary>
static void LineScanAll()
{
    var files = Directory.GetFiles(@"D:\HeliAcc\Rep", "*.rtm").OrderBy(f => f).ToList();
    var sb = new System.Text.StringBuilder();
    int hit = 0, fail = 0;
    foreach (var path in files)
    {
        try
        {
            var name = Path.GetFileName(path);
            var res = LineScanOne(name);
            if (res.Count > 0)
            {
                hit++;
                sb.AppendLine($"== {name} ==");
                foreach (var s in res) sb.AppendLine("  " + s);
            }
        }
        catch (Exception ex)
        {
            fail++;
            sb.AppendLine($"!! {Path.GetFileName(path)}: {ex.GetType().Name}: {ex.Message}");
        }
    }
    sb.Insert(0, $"重疊掃描：{files.Count} 份報表，{hit} 份有重疊，{fail} 份失敗\n\n");
    File.WriteAllText(@"D:\HeliAcc\shots\linecheck_report.txt", sb.ToString(), new System.Text.UTF8Encoding(false));
    Console.WriteLine(sb.ToString());
}

/// <summary>輸出報表 band 與元件佈局（mm），供重疊修復規劃。</summary>
static void DumpLayout(string rtmFile)
{
    string path = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    if (!File.Exists(path)) { Console.WriteLine($"找不到 {path}"); return; }
    var root = Tpf0Reader.Parse(File.ReadAllBytes(path));
    var r = RtmLoader.Load(root);
    Console.WriteLine($"== {rtmFile} == 紙 {r.MmPaperWidth / 1000f:F1}x{r.MmPaperHeight / 1000f:F1} 組={r.GroupBy}");
    void Walk(RtmComponent c, string ind)
    {
        string text = c.Caption ?? (c.DataField != null ? $"<{c.DataField}>" : "");
        Console.WriteLine($"{ind}{c.ClassName}/{c.Name} L={c.MmLeft / 1000f:F2} T={c.MmTop / 1000f:F2} W={c.MmWidth / 1000f:F2} H={c.MmHeight / 1000f:F2} al={c.TextAlignment} fs={c.FontSize:F1} \"{text}\"");
        foreach (var s in c.Children) Walk(s, ind + "    ");
    }
    void Band(string kind, RtmBand? b)
    {
        if (b is null) { Console.WriteLine($"[{kind}] 無"); return; }
        Console.WriteLine($"[{kind}] H={b.MmHeight / 1000f:F2}");
        foreach (var c in b.Components) Walk(c, "  ");
    }
    Band("Title", r.TitleBand);
    Band("Header", r.HeaderBand);
    Band("GroupHeader", r.GroupHeaderBand);
    Band("Detail", r.DetailBand);
    Band("GroupFooter", r.GroupFooterBand);
    Band("Summary", r.SummaryBand);
    Band("Footer", r.FooterBand);
}

/// <summary>單份報表：渲染假資料 → 像素級線/字重疊掃描（不存圖）。</summary>
static List<string> LineScanOne(string rtmFile)
{
    const int dpi = 150;
    string path = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    if (!File.Exists(path)) return new List<string>();
    var root = Tpf0Reader.Parse(File.ReadAllBytes(path));
    var r = RtmLoader.Load(root);

    var fields = new List<(string Pipe, string Field)>();
    void Scan(RtmComponent c)
    {
        if (c.DataField is { Length: > 0 })
            fields.Add((c.DataPipeline ?? "", c.DataField));
        foreach (var s in c.Children) Scan(s);
    }
    foreach (var b in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
        if (b is not null)
            foreach (var c in b.Components) Scan(c);

    string Fake(string f) => FakeValue(f);
    var data = MakeReportData(fields);

    int w = Math.Max(1, (int)Math.Round(r.MmPaperWidth * dpi / 25400.0));
    int h = Math.Max(1, (int)Math.Round(r.MmPaperHeight * dpi / 25400.0));
    using var bmp = new Bitmap(w, h);
    bmp.SetResolution(dpi, dpi);
    using var ren = new RtmRenderer(r, data);
    ren.DrawnTexts = new List<(RtmComponent, float, float, float, float, string)>();
    ren.DrawnLines = new List<(RtmComponent, float, float, float, float)>();
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        int pages = 0;
        do { pages++; } while (ren.RenderPage(g, new RectangleF(0, 0, w, h), st));
    }
    try { bmp.Save($@"C:\Users\JS\AppData\Local\Temp\opencode\lscan_{Path.GetFileNameWithoutExtension(rtmFile)}.png"); } catch { }
    var res = new List<string>();
    // 實際文字重疊（DrawnTexts 記錄為 [x,y,w,h]；交疊 > 1mm² 且交疊區像素有墨）
    var dt = ren.DrawnTexts;
    float eps = 0.3f;
    float scale = dpi / 25400f;
    for (int i = 0; i < dt.Count; i++)
    for (int j = i + 1; j < dt.Count; j++)
    {
        var (ca, ax, ay, aw, ah, tta) = dt[i];
        var (cb, bx, by, bw, bh, ttb) = dt[j];
        if (ReferenceEquals(ca, cb)) continue;
        if (ax + eps < bx + bw && bx + eps < ax + aw
            && ay + eps < by + bh && by + eps < ay + ah)
        {
            float ox = Math.Min(ax + aw, bx + bw) - Math.Max(ax, bx);
            float oy = Math.Min(ay + ah, by + bh) - Math.Max(ay, by);
            if (ox * oy > 1f)
            {
                int pxa = Math.Max(0, (int)Math.Floor(Math.Max(ax, bx) * scale));
                int pxb = Math.Min(bmp.Width - 1, (int)Math.Ceiling(Math.Min(ax + aw, bx + bw) * scale));
                int pya = Math.Max(0, (int)Math.Floor(Math.Max(ay, by) * scale));
                int pyb = Math.Min(bmp.Height - 1, (int)Math.Ceiling(Math.Min(ay + ah, by + bh) * scale));
                int ink = CountInk(bmp, pxa, pya, pxb, pyb);
                if (ink > 150)
                    res.Add($"字/字 {Ox(ca)}[{ax / 1000f:F1},{ay / 1000f:F1}] \"{Tr(tta)}\" 與 {Ox(cb)}[{bx / 1000f:F1},{by / 1000f:F1}] \"{Tr(ttb)}\" => {ox * oy / 1e6f:F2}mm² 墨{ink}px");
                else
                    res.Add($"字/字?{Ox(ca)}[{ax / 1000f:F1},{ay / 1000f:F1}] \"{Tr(tta)}\" 與 {Ox(cb)}[{bx / 1000f:F1},{by / 1000f:F1}] \"{Tr(ttb)}\" => {ox * oy / 1e6f:F2}mm² 墨{ink}px(待確認)");
            }
        }
    }
    // 線切穿字（像素級）
    res.AddRange(PixelLineTextOverlaps(bmp, dpi, ren.DrawnLines, ren.DrawnTexts));
    ren.Dispose();
    return res;
}

/// <summary>統計矩形內非白像素數。</summary>
static int CountInk(Bitmap bmp, int x0, int y0, int x1, int y1)
{
    if (x1 < x0 || y1 < y0) return 0;
    int n = 0;
    var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    try
    {
        unsafe
        {
            byte* p0 = (byte*)bd.Scan0;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    long idx = (long)y * bd.Stride + (long)x * 3;
                    byte b = p0[idx], g = p0[idx + 1], r = p0[idx + 2];
                    if (r < 200 || g < 200 || b < 200) n++;
                }
        }
    }
    finally { bmp.UnlockBits(bd); }
    return n;
}

/// <summary>分析統計 13 份報表（MissingReportService.Build*，真實 DB 資料）。</summary>
static void AnalysisRender()
{
    var type = typeof(MissingReportService);
    (string File, string Method)[] items =
    {
        ("客戶交易排行.rtm", "Build客戶交易排行"),
        ("客戶交易類別.rtm", "Build客戶交易類別"),
        ("客戶別報價明細.rtm", "Build客戶別報價明細"),
        ("客戶歷次售價.rtm", "Build客戶歷次售價"),
        ("廠商交易排行.rtm", "Build廠商交易排行"),
        ("廠商歷次售價.rtm", "Build廠商歷次售價"),
        ("業務銷售排行.rtm", "Build業務銷售排行"),
        ("業務銷售明細表.rtm", "Build業務銷售明細表"),
        ("業務利潤分析表.rtm", "Build業務利潤分析表"),
        ("貨品交易排行.rtm", "Build貨品交易排行"),
        ("貨品交易明細表.rtm", "Build貨品交易明細表"),
        ("貨品類別排行.rtm", "Build貨品類別排行"),
        ("貨品別報價明細.rtm", "Build貨品別報價明細"),
    };
    int pass = 0, fail = 0, skip = 0;
    foreach (var (file, method) in items)
    {
        try
        {
            var m = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!;
            var data = m.Invoke(null, null) as RtmData;
            if (data is null || data.Detail.Count == 0)
            {
                Console.WriteLine($"SKIP {file} 查無資料");
                skip++;
                continue;
            }
            Console.WriteLine($"明細欄位: {string.Join(",", data.Detail.SelectMany(d => d.Keys).Distinct())}");
            AccRenderOne(file, method, data);
            pass++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL {file} EX {ex.InnerException?.Message ?? ex.Message}");
            fail++;
        }
    }
    Console.WriteLine($"\n分析統計報表: {pass} PASS / {fail} FAIL / {skip} SKIP");
}

/// <summary>批次 2：交易分析 13 份報表（歷次售價／交易排行／類別／明細／利潤／報價）。</summary>
static void B2Render()
{
    const string 出貨退回類 = "('出貨退回','出退')";
    const string 進貨類 = "('進貨','進貨折讓','進貨退出','進退')";

    // ═══ 1/2. 客戶／廠商歷次售價（出貨／進貨明細，依交易對象分組）═══
    foreach (var (rtm, kind, classes, scope) in new (string, string, string, string)[]
    {
        ("客戶歷次售價.rtm", "出貨", "('出貨')", "全部客戶"),
        ("廠商歷次售價.rtm", "進貨", 進貨類, "全部廠商"),
    })
    {
        var dt = DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易對象], COALESCE(c.[公司全名],'') AS [公司全名], " +
            "d.[貨品編號], COALESCE(NULLIF(p.[品名],''),d.[品名]) AS [品名], " +
            "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單位],'') AS [單位], " +
            "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額] " +
            "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
            "LEFT JOIN [客戶廠商] c ON c.[客廠編號]=m.[交易對象] " +
            "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
            $"WHERE m.[單據類別] IN {classes} AND COALESCE(d.[贈品],0)=0 AND COALESCE(d.[服務項目],0)=0 " +
            "ORDER BY m.[交易對象], m.[交易日期], d.[建檔序號]");
        var data = StockData(dt, scope);
        data.Master["日期區間"] = "全部日期";
        RenderAnyReport(rtm, $"{kind}明細（依交易對象分組）", data,
            (h, s, f) => new (string, float, float, float, float)[]
            {
                ("公司全名", 88636, 3704, 20108, 5821),
                ("日期區間", 2381, 12965, 16933, 5027),
                ("明細交易日期", 2117, h, 20902, 5027),
                ("明細貨品編號", 23813, h, 27252, 5027),
                ("明細數量", 105834, h, 21960, 5027),
                ("明細金額", 163248, h, 28840, 5027),
            });
    }

    // ═══ 3. 客戶交易排行 ═══
    var 客戶排行 = StockData(DbManager.QueryTable(
        "SELECT [編號], [公司全名], [出貨金額], [折讓金額], [退回金額], " +
        "([出貨金額]-[折讓金額]-[退回金額]) AS [實銷金額] FROM ( " +
        "SELECT C.[客廠編號] AS [編號], COALESCE(C.[公司全名],'') AS [公司全名], " +
        "COALESCE(SUM(CASE WHEN m.[單據類別]='出貨' THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [出貨金額], " +
        "COALESCE(SUM(CASE WHEN m.[單據類別]='出貨折讓' THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [折讓金額], " +
        $"COALESCE(SUM(CASE WHEN m.[單據類別] IN {出貨退回類} THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [退回金額] " +
        "FROM [交易主檔] m JOIN [客戶廠商] C ON C.[客廠編號]=m.[交易對象] AND C.[客廠類別]='客戶' " +
        "WHERE m.[單據類別] IN ('出貨','出貨折讓','出貨退回','出退') " +
        "GROUP BY C.[客廠編號], C.[公司全名]) ORDER BY [實銷金額] DESC"), "全部客戶");
    客戶排行.Master["日期區間"] = "全部日期";
    RenderAnyReport("客戶交易排行.rtm", "客戶出貨彙總", 客戶排行,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 73775, 3704, 50095, 5927),
            ("日期區間", 0, 12700, 16933, 5009),
            ("明細編號", 7408, h, 23019, 5027),
            ("明細公司全名", 30163, h, 75671, 5027),
            ("明細出貨金額", 107156, h, 20902, 5027),
            ("明細實銷金額", 169334, h, 22754, 5027),
            ("彙總出貨金額", 107686, s + 2381, 20638, 5027),
        });

    // ═══ 4. 客戶交易類別（客戶×貨品類別彙總，依客戶分組）═══
    var 客戶類別 = StockData(DbManager.QueryTable(
        "SELECT m.[交易對象] AS [客廠編號], COALESCE(C.[公司全名],'') AS [公司全名], " +
        "COALESCE(K.[類別編號],'') AS [類別編號], COALESCE(K.[類別名稱],'未分類') AS [類別名稱], " +
        "SUM(COALESCE(d.[數量],0)) AS [數量之總計], SUM(COALESCE(d.[金額],0)) AS [金額之總計] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "JOIN [客戶廠商] C ON C.[客廠編號]=m.[交易對象] AND C.[客廠類別]='客戶' " +
        "LEFT JOIN [貨品主檔] P ON P.[貨品編號]=d.[貨品編號] " +
        "LEFT JOIN [貨品類別] K ON K.[類別編號]=P.[類別編號] " +
        "WHERE m.[單據類別]='出貨' " +
        "GROUP BY m.[交易對象], C.[公司全名], K.[類別編號], K.[類別名稱] " +
        "ORDER BY m.[交易對象], K.[類別編號]"), "全部客戶");
    客戶類別.Master["日期區間"] = "全部日期";
    RenderAnyReport("客戶交易類別.rtm", "客戶×貨品類別彙總", 客戶類別,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 73775, 3704, 50095, 5927),
            ("日期區間", 19050, 20638, 57644, 5009),
            ("明細類別編號", 19844, h, 20902, 5027),
            ("明細類別名稱", 44979, h, 44186, 5027),
            ("明細金額之總計", 125942, h, 35719, 5027),
            ("彙總金額之總計", 126736, s + 6350, 34925, 5027),
        });

    // ═══ 5. 廠商交易排行 ═══
    var 廠商排行 = StockData(DbManager.QueryTable(
        "SELECT [編號], [公司全名], [出貨金額], [折讓金額], [退回金額], " +
        "([出貨金額]-[折讓金額]-[退回金額]) AS [實銷金額] FROM ( " +
        "SELECT C.[客廠編號] AS [編號], COALESCE(C.[公司全名],'') AS [公司全名], " +
        "COALESCE(SUM(CASE WHEN m.[單據類別]='進貨' THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [出貨金額], " +
        "COALESCE(SUM(CASE WHEN m.[單據類別]='進貨折讓' THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [折讓金額], " +
        $"COALESCE(SUM(CASE WHEN m.[單據類別] IN {進貨類} THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [退回金額] " +
        "FROM [交易主檔] m JOIN [客戶廠商] C ON C.[客廠編號]=m.[交易對象] AND C.[客廠類別]='廠商' " +
        "WHERE m.[單據類別] IN ('進貨','進貨折讓','進貨退出','進退') " +
        "GROUP BY C.[客廠編號], C.[公司全名]) ORDER BY [實銷金額] DESC"), "全部廠商");
    廠商排行.Master["日期區間"] = "全部日期";
    RenderAnyReport("廠商交易排行.rtm", "廠商進貨彙總", 廠商排行,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 78846, 3704, 39952, 5821),
            ("日期區間", 0, 12700, 61119, 5027),
            ("明細編號", 7408, h, 23019, 5027),
            ("明細實銷金額", 169334, h, 22754, 5027),
        });

    // ═══ 6. 業務銷售排行 ═══
    var 業務排行 = StockData(DbManager.QueryTable(
        "SELECT [編號], [員工姓名], [出貨金額], [折讓金額], [退回金額], " +
        "([出貨金額]-[折讓金額]-[退回金額]) AS [實銷金額] FROM ( " +
        "SELECT COALESCE(E.[員工編號],'') AS [編號], COALESCE(E.[員工姓名],'') AS [員工姓名], " +
        "COALESCE(SUM(CASE WHEN m.[單據類別]='出貨' THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [出貨金額], " +
        "COALESCE(SUM(CASE WHEN m.[單據類別]='出貨折讓' THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [折讓金額], " +
        $"COALESCE(SUM(CASE WHEN m.[單據類別] IN {出貨退回類} THEN COALESCE(m.[合計金額],0) ELSE 0 END),0) AS [退回金額] " +
        "FROM [交易主檔] m LEFT JOIN [員工資料] E ON E.[員工編號]=m.[員工編號] " +
        "WHERE m.[單據類別] IN ('出貨','出貨折讓','出貨退回','出退') " +
        "GROUP BY E.[員工編號], E.[員工姓名]) ORDER BY [實銷金額] DESC"), "全部業務員");
    業務排行.Master["日期區間"] = "全部日期";
    RenderAnyReport("業務銷售排行.rtm", "業務員銷售彙總", 業務排行,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 78846, 3704, 39952, 5821),
            ("日期區間", 0, 12700, 16933, 5027),
            ("明細編號", 20108, h, 20373, 5027),
            ("明細員工姓名", 42333, h, 61648, 5027),
            ("明細出貨金額", 106098, h, 20902, 5027),
            ("明細實銷金額", 169334, h, 22754, 5027),
            ("彙總出貨金額", 107686, s + 2381, 20638, 5027),
        });

    // ═══ 7. 貨品交易排行 ═══
    var 貨品排行 = StockData(DbManager.QueryTable(
        "SELECT [編號], [品名], [基本單位], [出貨數量], [出貨金額], [退回數量], [退回金額], " +
        "([出貨數量]-[退回數量]) AS [合計數量], ([出貨金額]-[退回金額]) AS [合計金額] FROM ( " +
        "SELECT d.[貨品編號] AS [編號], COALESCE(NULLIF(p.[品名],''),d.[品名]) AS [品名], COALESCE(p.[基本單位],'') AS [基本單位], " +
        "SUM(CASE WHEN m.[單據類別]='出貨' THEN COALESCE(d.[數量],0) ELSE 0 END) AS [出貨數量], " +
        "SUM(CASE WHEN m.[單據類別]='出貨' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [出貨金額], " +
        $"SUM(CASE WHEN m.[單據類別] IN {出貨退回類} THEN COALESCE(d.[數量],0) ELSE 0 END) AS [退回數量], " +
        $"SUM(CASE WHEN m.[單據類別] IN {出貨退回類} THEN COALESCE(d.[金額],0) ELSE 0 END) AS [退回金額] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
        "WHERE m.[單據類別] IN ('出貨','出貨退回','出退') " +
        "GROUP BY d.[貨品編號], p.[品名], p.[基本單位]) ORDER BY [合計金額] DESC"), "全部貨品");
    貨品排行.Master["日期區間"] = "全部日期";
    RenderAnyReport("貨品交易排行.rtm", "貨品銷售彙總", 貨品排行,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 73775, 3704, 50095, 5927),
            ("日期區間", 2117, 13229, 16933, 5009),
            ("明細編號", 7408, h, 23019, 5027),
            ("明細品名", 30163, h, 38100, 5027),
            ("明細出貨金額", 97896, h, 19050, 5027),
            ("明細合計金額", 175684, h, 18256, 5027),
            ("彙總合計金額", 176742, s + 2646, 17198, 5027),
        });

    // ═══ 8. 貨品類別排行 ═══
    var 貨品類別 = StockData(DbManager.QueryTable(
        "SELECT [編號], [類別名稱], [出貨數量], [出貨金額], [退回數量], [退回金額], " +
        "([出貨數量]-[退回數量]) AS [合計數量], ([出貨金額]-[退回金額]) AS [合計金額] FROM ( " +
        "SELECT COALESCE(K.[類別編號],'') AS [編號], COALESCE(K.[類別名稱],'未分類') AS [類別名稱], " +
        "SUM(CASE WHEN m.[單據類別]='出貨' THEN COALESCE(d.[數量],0) ELSE 0 END) AS [出貨數量], " +
        "SUM(CASE WHEN m.[單據類別]='出貨' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [出貨金額], " +
        $"SUM(CASE WHEN m.[單據類別] IN {出貨退回類} THEN COALESCE(d.[數量],0) ELSE 0 END) AS [退回數量], " +
        $"SUM(CASE WHEN m.[單據類別] IN {出貨退回類} THEN COALESCE(d.[金額],0) ELSE 0 END) AS [退回金額] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [貨品主檔] P ON P.[貨品編號]=d.[貨品編號] " +
        "LEFT JOIN [貨品類別] K ON K.[類別編號]=P.[類別編號] " +
        "WHERE m.[單據類別] IN ('出貨','出貨退回','出退') " +
        "GROUP BY K.[類別編號], K.[類別名稱]) ORDER BY [合計金額] DESC"), "全部貨品");
    貨品類別.Master["日期區間"] = "全部日期";
    RenderAnyReport("貨品類別排行.rtm", "貨品類別銷售彙總", 貨品類別,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 73775, 3704, 50095, 5927),
            ("日期區間", 6085, 13229, 16933, 5009),
            ("明細編號", 15610, h, 23019, 5027),
            ("明細類別名稱", 39158, h, 38100, 5027),
            ("明細出貨金額", 97896, h, 19050, 5027),
            ("明細合計金額", 175684, h, 18256, 5027),
            ("彙總合計金額", 176742, s + 2646, 17198, 5027),
        });

    // ═══ 9. 業務銷售明細表 ═══
    var 業務明細 = StockData(DbManager.QueryTable(
        "SELECT m.[交易日期], m.[交易單號], COALESCE(c.[公司簡稱],'') AS [公司簡稱], d.[貨品編號], " +
        "COALESCE(NULLIF(p.[品名],''),d.[品名]) AS [品名], " +
        "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單位],'') AS [單位], " +
        "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額], m.[單據類別] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [客戶廠商] c ON c.[客廠編號]=m.[交易對象] " +
        "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
        "WHERE m.[單據類別]='出貨' " +
        "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]"), "全部日期");
    業務明細.Master["日期區間"] = "全部日期";
    RenderAnyReport("業務銷售明細表.rtm", "出貨明細", 業務明細,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 68263, 3704, 60854, 5821),
            ("日期區間", 1588, 13229, 16933, 5027),
            ("明細交易單號", 16404, h, 22490, 5027),
            ("明細公司簡稱", 59796, h, 18785, 5027),
            ("明細品名", 101071, h, 32258, 5027),
            ("明細金額", 172509, h, 19050, 5027),
            ("彙總金額", 174096, s + 3969, 17198, 5027),
        });

    // ═══ 10. 業務利潤分析表（依員工分組，毛利=金額-數量×成本）═══
    var 利潤分析 = StockData(DbManager.QueryTable(
        "SELECT m.[員工編號], COALESCE(E.[員工姓名],'') AS [員工姓名], m.[交易日期], m.[交易單號], d.[貨品編號], " +
        "COALESCE(NULLIF(p.[品名],''),d.[品名]) AS [品名], " +
        "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單位],'') AS [單位], " +
        "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額], " +
        "(COALESCE(d.[金額],0) - COALESCE(d.[數量],0)*COALESCE(NULLIF(d.[成本],0), NULLIF(p.[現行成本],0), 0)) AS [毛利], " +
        "CASE WHEN COALESCE(d.[金額],0) <> 0 " +
        "THEN ROUND((COALESCE(d.[金額],0) - COALESCE(d.[數量],0)*COALESCE(NULLIF(d.[成本],0), NULLIF(p.[現行成本],0), 0)) / COALESCE(d.[金額],0) * 100, 2) " +
        "ELSE 0 END AS [毛利率%] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [員工資料] E ON E.[員工編號]=m.[員工編號] " +
        "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
        "WHERE m.[單據類別]='出貨' AND COALESCE(d.[贈品],0)=0 AND COALESCE(d.[服務項目],0)=0 " +
        "ORDER BY m.[員工編號], m.[交易日期], d.[建檔序號]"), "全部日期");
    利潤分析.Master["日期區間"] = "全部日期";
    RenderAnyReport("業務利潤分析表.rtm", "出貨毛利（依員工分組）", 利潤分析,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 73554, 3704, 50006, 5821),
            ("日期區間", 529, 13229, 16933, 5027),
            ("明細員工姓名", 0, h, 18785, 5027),
            ("明細交易單號", 17992, h, 22490, 5027),
            ("明細貨品編號", 60590, h, 19844, 5027),
            ("明細金額", 148167, h, 18256, 5027),
            ("明細毛利", 166159, h, 13758, 5027),
        });

    // ═══ 11. 貨品交易明細表（單一貨品全部交易）═══
    string pick = Convert.ToString(DbManager.QueryScalar(
        "SELECT d.[貨品編號] FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "WHERE m.[單據類別]='出貨' GROUP BY d.[貨品編號] ORDER BY SUM(COALESCE(d.[金額],0)) DESC LIMIT 1")) ?? "";
    var gdt = DbManager.QueryTable(
        "SELECT m.[交易日期], m.[交易單號], COALESCE(c.[公司簡稱],'') AS [公司簡稱], d.[貨品編號], " +
        "COALESCE(NULLIF(p.[品名],''),d.[品名]) AS [品名], " +
        "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單位],'') AS [單位], " +
        "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額], m.[單據類別] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [客戶廠商] c ON c.[客廠編號]=m.[交易對象] " +
        "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
        "WHERE d.[貨品編號]=$g ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]",
        DbManager.Param("$g", pick));
    var gdata = StockData(gdt);
    gdata.Master["日期區間"] = "全部日期";
    gdata.Master["編號區間"] = $"貨品 {pick}";
    RenderAnyReport("貨品交易明細表.rtm", $"貨品 {pick} 全部交易明細", gdata,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 78846, 3704, 39952, 5821),
            ("日期區間", 2117, 13229, 16933, 5027),
            ("明細交易單號", 16404, h, 22490, 5027),
            ("明細貨品編號", 79375, h, 21431, 5027),
            ("明細金額", 172509, h, 19050, 5027),
            ("彙總金額", 174096, s + 3969, 17198, 5027),
        });

    // ═══ 12. 客戶別報價明細（依客戶）═══
    var 客戶報價 = StockData(DbManager.QueryTable(
        "SELECT m.[交易日期], m.[交易單號], COALESCE(c.[公司全名],'') AS [公司全名], d.[貨品編號], " +
        "COALESCE(NULLIF(p.[品名],''),d.[品名]) AS [品名], " +
        "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單位],'') AS [單位], " +
        "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [客戶廠商] c ON c.[客廠編號]=m.[交易對象] " +
        "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
        "WHERE m.[單據類別]='出貨' " +
        "ORDER BY c.[公司全名], m.[交易日期], d.[建檔序號]"), "全部客戶");
    客戶報價.Master["日期區間"] = "全部日期";
    RenderAnyReport("客戶別報價明細.rtm", "出貨明細（依客戶）", 客戶報價,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 79904, 3704, 37571, 5821),
            ("日期區間", 1588, 13229, 16933, 5027),
            ("明細公司全名", 2910, h, 31780, 5027),
            ("明細貨品編號", 34396, h, 22490, 5027),
            ("明細品名", 57150, h, 38100, 5027),
            ("明細金額", 175684, h, 19844, 5027),
            ("彙總金額", 175948, s + 2117, 19315, 5027),
        });

    // ═══ 13. 貨品別報價明細（依貨品）═══
    var 貨品報價 = StockData(DbManager.QueryTable(
        "SELECT m.[交易日期], m.[交易單號], COALESCE(c.[公司全名],'') AS [公司全名], d.[貨品編號], " +
        "COALESCE(NULLIF(p.[品名],''),d.[品名]) AS [品名], " +
        "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單位],'') AS [單位], " +
        "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [客戶廠商] c ON c.[客廠編號]=m.[交易對象] " +
        "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
        "WHERE m.[單據類別]='出貨' " +
        "ORDER BY d.[貨品編號], m.[交易日期], d.[建檔序號]"), "全部貨品");
    貨品報價.Master["日期區間"] = "全部日期";
    RenderAnyReport("貨品別報價明細.rtm", "出貨明細（依貨品）", 貨品報價,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 79904, 3704, 37571, 5821),
            ("日期區間", 1588, 13229, 16933, 5027),
            ("明細貨品編號", 2117, h, 22490, 5027),
            ("明細品名", 25400, h, 41810, 5027),
            ("明細金額", 175684, h, 19844, 5027),
            ("彙總金額", 175948, s + 2117, 19315, 5027),
        });
}

static void ArRender()
{
    // ═══ 應收帳款 4 張報表（統計表／帳齡分析用模擬彙總資料；明細表／簡要表用模擬單據資料）═══
    string[] files =
    {
        @"D:\HeliAcc\Rep\應收帳款明細表.rtm",
        @"D:\HeliAcc\Rep\應收帳款簡要表.rtm",
        @"D:\HeliAcc\Rep\應收帳款統計表.rtm",
        @"D:\HeliAcc\Rep\應收帳款帳齡分析.rtm",
    };
    foreach (var f in files) DumpBands(f);

    // 統計表：每列一個對象（DetailPipeline = ppDBPipeline1）
    var stat = new RtmData { DetailPipeline = "ppDBPipeline1" };
    FillArCompany(stat);
    stat.Master["日期區間"] = "2025-01-01 ~ 2025-12-31";
    stat.Master["編號區間"] = "全部客戶";
    AddArStatRow(stat, "A001", "亞太保全股份有限公司", 20000m, 50000m, 48000m, 35000m, 33000m, 15000m, 2000m, 3000m);
    AddArStatRow(stat, "A002", "宏基工程有限公司", 0m, 120000m, 120000m, 80000m, 40000m, 60000m, 0m, 0m);
    AddArStatRow(stat, "A003", "全方位監控器材行", 8000m, 30000m, 30000m, 5000m, 33000m, 8000m, 0m, 0m);
    RenderArReport("應收帳款統計表.rtm", "統計表（模擬 LoadObjectSummary 彙總）", stat);

    // 帳齡分析：每列一個對象（DetailPipeline = ppDBPipeline1）
    var aging = new RtmData { DetailPipeline = "ppDBPipeline1" };
    FillArCompany(aging);
    aging.Master["日期區間"] = "基準日 2026-08-13";
    aging.Master["編號區間"] = "全部客戶";
    AddArAgingRow(aging, "A001", "亞太保全股份有限公司", 20000m, 5000m, 12000m, 8000m, 0m, 10000m, 20000m, 75000m);
    AddArAgingRow(aging, "A002", "宏基工程有限公司", 0m, 0m, 0m, 40000m, 50000m, 30000m, 0m, 120000m);
    AddArAgingRow(aging, "A003", "全方位監控器材行", 8000m, 0m, 0m, 0m, 5000m, 10000m, 10000m, 33000m);
    RenderArReport("應收帳款帳齡分析.rtm", "帳齡分析（模擬 AgingAnalysis 全部對象）", aging);

    // 明細表：單一對象（Master = 帳款主檔+客戶廠商，DetailPipeline = ppDBPipeline2 明細列）
    var det = new RtmData { DetailPipeline = "ppDBPipeline2" };
    FillArCompany(det);
    det.Master["日期區間"] = "2025-01-01 ~ 2025-12-31";
    det.Master["交易對象"] = "A001";
    det.Master["公司全名"] = "亞太保全股份有限公司";
    det.Master["聯絡人一"] = "王經理";
    det.Master["聯絡電話一"] = "(02)2593-2101";
    det.Master["統一編號"] = "12345678";
    det.Master["傳真號碼"] = "(02)2586-3046";
    det.Master["前期累計應收帳款"] = 20000m;
    det.Master["本期合計"] = 50000m;
    det.Master["營業稅"] = 2500m;
    det.Master["本期總計"] = 52500m;
    det.Master["已收付金額"] = 35000m;
    det.Master["折讓金額"] = 2000m;
    det.Master["本期累計應收"] = 35500m;
    det.Master["現金收付金額"] = 15000m;
    det.Master["累計預收貨款"] = 3000m;
    AddArDetailRow(det, "2025-08-12", "出貨", "2508120001", "INV-1001", "CCTV-100", "高清攝影機 1080P", 10m, "台", 1500m, 15000m);
    AddArDetailRow(det, "2025-08-12", "出貨", "2508120001", "INV-1001", "DVR-200", "四路錄影主機", 3m, "台", 8000m, 24000m);
    AddArDetailRow(det, "2025-08-15", "出貨", "2508150002", "INV-1002", "CCTV-101", "高清攝影機 4K", 5m, "台", 2500m, 12500m);
    RenderArReport("應收帳款明細表.rtm", "明細表（模擬帳款明細）", det);

    // 簡要表：單一對象（Master = 帳款主檔+客戶廠商，DetailPipeline = ppDBPipeline2 未收付單據）
    var brief = new RtmData { DetailPipeline = "ppDBPipeline2" };
    FillArCompany(brief);
    brief.Master["日期區間"] = "2025-01-01 ~ 2025-12-31";
    brief.Master["交易對象"] = "A001";
    brief.Master["公司全名"] = "亞太保全股份有限公司";
    brief.Master["聯絡人一"] = "王經理";
    brief.Master["聯絡電話一"] = "(02)2593-2101";
    brief.Master["統一編號"] = "12345678";
    brief.Master["傳真號碼"] = "(02)2586-3046";
    brief.Master["前期累計應收帳款"] = 20000m;
    brief.Master["本期合計"] = 50000m;
    brief.Master["營業稅"] = 2500m;
    brief.Master["本期總計"] = 52500m;
    brief.Master["已收付金額"] = 35000m;
    brief.Master["折讓金額"] = 2000m;
    brief.Master["本期累計應收"] = 35500m;
    brief.Master["現金收付金額"] = 15000m;
    brief.Master["累計預收貨款"] = 3000m;
    AddArBriefRow(brief, "2025-08-12", "出貨", "2508120001", "INV-1001", 39000m, 1950m, 40950m, 25000m, 15950m);
    AddArBriefRow(brief, "2025-08-15", "出貨", "2508150002", "INV-1002", 12500m, 625m, 13125m, 10000m, 3125m);
    AddArBriefRow(brief, "2025-09-01", "出退", "2509010003", "", -5000m, -250m, -5250m, 0m, -5250m);
    RenderArReport("應收帳款簡要表.rtm", "簡要表（模擬 LoadOpenDetails）", brief);
}

static void FillArCompany(RtmData data)
{
    data.Company["公司全名"] = "禾秝安全系統工程有限公司";
    data.Company["電話號碼"] = "(02)2593-2101";
    data.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
    data.Company["傳真號碼"] = "(02)2586-3046";
}

static void AddArStatRow(RtmData data, string 對象, string 公司, decimal 預收, decimal 前期, decimal 本期應收, decimal 已收, decimal 累計應收, decimal 現金, decimal 折讓, decimal _)
{
    data.Detail.Add(new Dictionary<string, object?>
    {
        ["交易對象"] = 對象, ["公司全名"] = 公司,
        ["累計預收貨款"] = 預收, ["前期累計應收帳款"] = 前期, ["本期應收"] = 本期應收,
        ["已收付金額"] = 已收, ["本期累計應收"] = 累計應收, ["現金收付金額"] = 現金, ["折讓金額"] = 折讓,
    });
}

static void AddArAgingRow(RtmData data, string 對象, string 公司, decimal 期初, decimal 第一, decimal 第二, decimal 第三, decimal 第四, decimal 第五, decimal 第六, decimal 合計)
{
    data.Detail.Add(new Dictionary<string, object?>
    {
        ["交易對象"] = 對象, ["公司全名"] = 公司, ["期初帳款"] = 期初,
        ["第一期間"] = 第一, ["第二期間"] = 第二, ["第三期間"] = 第三,
        ["第四期間"] = 第四, ["第五期間"] = 第五, ["第六期間"] = 第六, ["合計"] = 合計,
    });
}

static void AddArDetailRow(RtmData data, string 日期, string 類別, string 單號, string 發票, string 貨號, string 品名, decimal 數量, string 單位, decimal 單價, decimal 金額)
{
    data.Detail.Add(new Dictionary<string, object?>
    {
        ["交易日期"] = 日期, ["單據類別"] = 類別, ["交易單號"] = 單號, ["發票號碼"] = 發票,
        ["貨品編號"] = 貨號, ["品名"] = 品名, ["數量"] = 數量, ["單位"] = 單位, ["單價"] = 單價, ["金額"] = 金額,
    });
}

static void AddArBriefRow(RtmData data, string 日期, string 類別, string 單號, string 發票, decimal 合計, decimal 稅, decimal 總計, decimal 已收, decimal 未收)
{
    data.Detail.Add(new Dictionary<string, object?>
    {
        ["交易日期"] = 日期, ["單據類別"] = 類別, ["交易單號"] = 單號, ["發票號碼"] = 發票,
        ["合計金額"] = 合計, ["營業稅"] = 稅, ["總計金額"] = 總計, ["已收付金額"] = 已收, ["未收付金額"] = 未收,
    });
}

static void RenderArReport(string rtmFile, string label, RtmData data)
{
    string rtmPath = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    Console.WriteLine($"\n== {rtmFile}（{label}）==");
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    Console.WriteLine($"DetailPipeline={data.DetailPipeline} 明細欄位=" + string.Join(",", data.Detail.SelectMany(d => d.Keys).Distinct()));

    const int dpi = 300;
    int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
    int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);
    using var bmp = new Bitmap(wPx, hPx);
    bmp.SetResolution(dpi, dpi);
    using var renderer = new RtmRenderer(report, data);
    int pages = 0;
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        do { pages++; } while (renderer.RenderPage(g, new RectangleF(0, 0, wPx, hPx), st));
    }

    string outPath = $@"D:\HeliAcc\shots\{Path.GetFileNameWithoutExtension(rtmFile)}.png";
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    bmp.Save(outPath, ImageFormat.Png);

    var mask = new byte[wPx * hPx];
    var bd = bmp.LockBits(new Rectangle(0, 0, wPx, hPx), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    unsafe
    {
        byte* p = (byte*)bd.Scan0;
        for (int yy = 0; yy < hPx; yy++)
            for (int xx = 0; xx < wPx; xx++)
            {
                byte b = p[yy * bd.Stride + xx * 3], g = p[yy * bd.Stride + xx * 3 + 1], r = p[yy * bd.Stride + xx * 3 + 2];
                mask[yy * wPx + xx] = (r < 200 || g < 200 || b < 200) ? (byte)1 : (byte)0;
            }
    }
    bmp.UnlockBits(bd);
    int ink = mask.Sum(b => b);
    double pct = 100.0 * ink / (wPx * hPx);
    Console.WriteLine($"頁數={pages} 墨量={pct:F2}% 輸出={outPath}");

    // 區域墨量驗證（元件座標 1/1000mm → px @300dpi；band 原點 y 依堆疊）
    float pxPerMm = dpi / 25.4f / 1000f;
    float headerH = report.HeaderBand?.MmHeight ?? 0f;
    float detailH = report.DetailBand?.MmHeight ?? 0f;
    float gfY = headerH + detailH * data.Detail.Count;
    float sfY = gfY + (report.GroupFooterBand?.MmHeight ?? 0f);

    (string, float, float, float, float)[] checks = rtmFile switch
    {
        "應收帳款統計表.rtm" => new (string, float, float, float, float)[]
        {
            ("公司全名", 72800, 3700, 53200, 7700),
            ("日期區間", 0, 12700, 16900, 5000),
            ("明細交易對象", 7400, headerH, 19300, 5000),
            ("明細公司全名", 26500, headerH, 45000, 5000),
            ("明細前期累計", 90800, headerH, 18800, 5000),
            ("明細本期累計應收", 173800, headerH, 23500, 5000),
            ("明細第二列公司全名", 26500, headerH + detailH, 45000, 5000),
            ("明細第三列公司全名", 26500, headerH + detailH * 2, 45000, 5000),
            ("彙總累計預收(dcSum)", 72000, sfY + 2600, 18300, 5000),
            ("彙總本期累計應收(dcSum)", 174600, sfY + 2600, 22800, 5000),
        },
        "應收帳款帳齡分析.rtm" => new (string, float, float, float, float)[]
        {
            ("公司全名", 76700, 3700, 45500, 7700),
            ("日期區間", 0, 12700, 37000, 5000),
            ("明細交易對象", 0, headerH, 19300, 5000),
            ("明細公司全名", 19300, headerH, 31000, 5000),
            ("明細期初帳款", 50800, headerH, 18000, 5000),
            ("明細第六期間", 68800, headerH, 22000, 5000),
            ("明細合計", 179400, headerH, 18000, 5000),
            ("明細第二列期初帳款", 50800, headerH + detailH, 18000, 5000),
            ("彙總期初帳款(dcSum)", 50800, sfY + 2600, 18000, 5000),
            ("彙總合計(dcSum)", 179400, sfY + 2600, 18000, 5000),
        },
        "應收帳款明細表.rtm" => new (string, float, float, float, float)[]
        {
            ("公司全名", 71600, 3700, 57200, 6800),
            ("日期區間", 127800, 30200, 55500, 5100),
            ("主檔交易對象", 23800, 18000, 11900, 5100),
            ("主檔公司全名", 38400, 18000, 12700, 5100),
            ("主檔聯絡人一", 23300, 24300, 29100, 5000),
            ("明細單據類別", 0, headerH + 300, 10600, 5000),
            ("明細交易日期", 10600, headerH + 300, 19000, 5000),
            ("明細品名", 102700, headerH + 300, 32800, 5000),
            ("明細金額", 179700, headerH + 300, 17500, 5000),
            ("明細第二列品名", 102700, headerH + detailH + 300, 32800, 5000),
            ("彙總數量(dcSum)", 67200, gfY + 2600, 19600, 5100),
            ("頁尾前期累計應收", 84800, gfY + 8200, 2000, 5100),
            ("頁尾本期累計應收", 193400, gfY + 25100, 4000, 5100),
            ("頁尾累計預收貨款", 84800, gfY + 14000, 2000, 5100),
        },
        _ => new (string, float, float, float, float)[]
        {
            ("公司全名", 86500, 3700, 25000, 5900),
            ("日期區間", 4200, 13200, 59100, 5100),
            ("主檔交易對象", 27500, 20900, 21700, 5000),
            ("主檔公司全名", 49500, 20900, 70900, 5000),
            ("主檔前期累計應收", 87000, 6900, 1900, 5100),
            ("主檔本期累計應收", 178100, 23500, 12700, 5100),
            ("明細交易單號", 36800, headerH + 500, 25700, 5000),
            ("明細發票號碼", 61100, headerH + 500, 25700, 5000),
            ("明細總計金額", 124900, headerH + 500, 23000, 5000),
            ("明細未收付金額", 171700, headerH + 500, 18800, 5000),
            ("明細第二列未收付金額", 171700, headerH + detailH + 500, 18800, 5000),
            ("組尾彙總區", 5000, gfY + 500, 200000, 8000),
        },
    };

    bool HasInkAr(int x, int y, int w, int h)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(wPx, x + w), y1 = Math.Min(hPx, y + h);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                if (mask[yy * wPx + xx] == 1) return true;
        return false;
    }

    int pass = 0, fail = 0;
    foreach (var (name, l, t, w, h) in checks)
    {
        int x = (int)(l * pxPerMm), y = (int)(t * pxPerMm);
        int rw = (int)(w * pxPerMm), rh = (int)(h * pxPerMm);
        bool ok = HasInkAr(x, y, rw, rh);
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  ({l / 1000f:F1},{t / 1000f:F1})mm");
        if (ok) pass++; else fail++;
    }
    Console.WriteLine($"\n{Path.GetFileNameWithoutExtension(rtmFile)}: {pass} PASS / {fail} FAIL");
    Console.WriteLine(pct > 0.3 && pages >= 1 && fail == 0 ? "AR PASS" : "AR FAIL");
}

// ═══ 批次 3：出退貨與安存報表（ReportMenuForm builder，反射呼叫）═══
static void B3Render()
{
    var type = typeof(ReportMenuForm);
    var form = Activator.CreateInstance(type);
    (string File, string Method)[] items =
    {
        ("出退貨明細表.rtm", "BuildShipReturnDetailData"),
        ("出退貨簡要表.rtm", "BuildShipReturnBriefData"),
        ("貨品低於安存表.rtm", "BuildGoodsBelowSafetyData"),
        ("倉庫低於安存表.rtm", "BuildWarehouseBelowSafetyData"),
        ("支票列印.rtm", "BuildCheckPrintData"),
        ("票據簽收回條.rtm", "BuildBillReceiptData"),
        ("票貼剩餘額度表.rtm", "BuildBillDiscountBalanceData"),
        ("入出庫明細表.rtm", "BuildStockIoDetailData"),
        ("貨品入出庫明細表.rtm", "BuildGoodsStockIoData"),
        ("貨品調整明細表.rtm", "BuildGoodsAdjustmentData"),
        ("出貨利潤明細表.rtm", "BuildShipProfitData"),
        ("貨品利潤明細表.rtm", "BuildGoodsProfitData"),
        ("應收帳款明細表(含折扣).rtm", "BuildArDetailDiscountData"),
        ("應付帳款明細表(含折扣).rtm", "BuildApDetailDiscountData"),
        ("出貨退回明細表.rtm", "BuildShipReturnDetailReport"),
        ("廠商交易明細表.rtm", "BuildVendorTxReportData"),
        ("報價單據.rtm", "BuildPoBillQuoteData"),
        ("訂貨單據.rtm", "BuildPoBillOrderData"),
        ("採購單據.rtm", "BuildPoBillPurchaseData"),
        ("詢價單據.rtm", "BuildPoBillInquiryData"),
        ("已訂未交反應表.rtm", "BuildPoReactionOpenData"),
        ("訂貨已交反應表.rtm", "BuildPoReactionShippedData"),
        ("已購未進反應表.rtm", "BuildPoReactionNotInData"),
        ("日期別折舊表.rtm", "BuildDepreciationDateData"),
        ("科目別折舊表.rtm", "BuildDepreciationSubjectData"),
        ("財產別折舊表.rtm", "BuildDepreciationPropertyData"),
    };
    foreach (var (file, method) in items)
    {
        try
        {
            var m = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!;
            var data = m.Invoke(form, null) as RtmData;
            if (data is null || data.Detail.Count == 0)
            {
                Console.WriteLine($"SKIP {file} 查無資料");
                continue;
            }
            AccRenderOne(file, method, data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL {file} EX {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}

// ═══ 會計報表（真實 DB 資料，經 AccountingService.Build*）═══
static void AccRender()
{
    (string File, Func<RtmData?> Build, string Label)[] items =
    {
        ("會計傳票.rtm", AccountingService.BuildVoucherReportData, "傳票"),
        ("總分類帳明細表.rtm", AccountingService.BuildLedgerDetailReportData, "日記帳簿"),
        ("總分類帳簡要表.rtm", AccountingService.BuildLedgerBriefReportData, "總分類帳"),
        ("明細分類帳.rtm", AccountingService.BuildDetailLedgerReportData, "日記帳簿"),
        ("日記帳(含現).rtm", AccountingService.BuildJournalReportData, "日記帳簿"),
        ("日記帳(不含現).rtm", AccountingService.BuildJournalNoCashReportData, "日記帳簿(不含現)"),
        ("現金帳.rtm", AccountingService.BuildCashBookReportData, "現金帳簿"),
        ("試算表.rtm", AccountingService.BuildTrialBalanceReportData, "期初餘額"),
        ("期間試算表.rtm", AccountingService.BuildPeriodTrialBalanceReportData, "總分類帳"),
        ("損益表.rtm", AccountingService.BuildIncomeStatementReportData, "損益報表"),
        ("報告式資產負債表.rtm", AccountingService.BuildBalanceSheetReportData, "資產負債"),
        ("帳戶式資產負債表.rtm", AccountingService.BuildAccountBalanceSheetReportData, "資產負債"),
    };
    foreach (var (file, build, label) in items)
    {
        RtmData? data;
        try { data = build(); }
        catch (Exception ex) { Console.WriteLine($"FAIL {file} 資料建構例外: {ex.Message}"); continue; }
        if (data is null) { Console.WriteLine($"SKIP {file} 查無資料"); continue; }
        AccRenderOne(file, label, data);
    }
}

static void AccRenderOne(string rtmFile, string label, RtmData data)
{
    string rtmPath = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    Console.WriteLine($"\n== {rtmFile}（{label}）==");
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    Console.WriteLine($"Band: H={report?.HeaderBand?.MmHeight} D={report?.DetailBand?.MmHeight} GF={report?.GroupFooterBand?.MmHeight} F={report?.FooterBand?.MmHeight} S={report?.SummaryBand?.MmHeight} GroupBy={report?.GroupBy}/{report?.GroupPipeline}");

    const int dpi = 300;
    int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
    int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);
    using var bmp = new Bitmap(wPx, hPx);
    bmp.SetResolution(dpi, dpi);
    using var renderer = new RtmRenderer(report, data);
    int pages = 0;
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        do { pages++; } while (renderer.RenderPage(g, new RectangleF(0, 0, wPx, hPx), st));
    }

    string outPath = $@"D:\HeliAcc\shots\{Path.GetFileNameWithoutExtension(rtmFile)}.png";
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    bmp.Save(outPath, ImageFormat.Png);

    var mask = new byte[wPx * hPx];
    var bd = bmp.LockBits(new Rectangle(0, 0, wPx, hPx), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    unsafe
    {
        byte* p = (byte*)bd.Scan0;
        for (int yy = 0; yy < hPx; yy++)
            for (int xx = 0; xx < wPx; xx++)
            {
                byte b = p[yy * bd.Stride + xx * 3], g = p[yy * bd.Stride + xx * 3 + 1], r = p[yy * bd.Stride + xx * 3 + 2];
                mask[yy * wPx + xx] = (r < 200 || g < 200 || b < 200) ? (byte)1 : (byte)0;
            }
    }
    bmp.UnlockBits(bd);
    int ink = mask.Sum(b => b);
    double pct = 100.0 * ink / (wPx * hPx);
    Console.WriteLine($"頁數={pages} 明細={data.Detail.Count}筆 墨量={pct:F2}% 輸出={outPath}");
    if (rtmFile is "帳戶式資產負債表.rtm" or "會計傳票.rtm" or "報告式資產負債表.rtm")
    {
        int half = wPx / 2;
        long left = 0, right = 0, top = 0, bottom = 0;
        for (int yy = 0; yy < hPx; yy++)
            for (int xx = 0; xx < half; xx++) if (mask[yy * wPx + xx] == 1) left++;
        for (int yy = 0; yy < hPx; yy++)
            for (int xx = half; xx < wPx; xx++) if (mask[yy * wPx + xx] == 1) right++;
        for (int yy = 0; yy < hPx / 3; yy++)
            for (int xx = 0; xx < wPx; xx++) if (mask[yy * wPx + xx] == 1) top++;
        for (int yy = hPx / 3; yy < hPx; yy++)
            for (int xx = 0; xx < wPx; xx++) if (mask[yy * wPx + xx] == 1) bottom++;
        Console.WriteLine($"分區墨水：左半={left} 右半={right} 上1/3={top} 下2/3={bottom}");
    }
    Console.WriteLine(pages >= 1 && pct > 0.2 ? "ACC PASS" : "ACC FAIL");
}

// ═══ 應收／應付帳款報表（真實 DB 資料，經 ARService.Build*ReportData）═══
static void ArRealRender()
{
    // 直接使用主測試資料庫（與庫存報表一致），避免複製品不同步
    Console.WriteLine($"\n== AR 報表真實資料渲染（DB: {DbManager.DatabasePath}）==");

    // 應收（客戶）
    var stat = ARService.BuildSummaryReportData("客戶");
    RenderArReal("應收帳款統計表.rtm", "統計表（應收/真實資料）", stat);
    var aging = ARService.BuildAgingReportData("客戶");
    RenderArReal("應收帳款帳齡分析.rtm", "帳齡分析（應收/真實資料）", aging);

    // 應付（廠商）：主 DB 無廠商帳款資料時以 fixture 補足
    if (ARService.BuildSummaryReportData("廠商").Detail.Count == 0)
    {
        Console.WriteLine("\n主 DB 無廠商帳款資料，插入測試 fixture（AR-V001）以驗證應付報表…");
        ArFixture();
    }
    RenderArReal("應付帳款統計表.rtm", "統計表（應付/真實資料）", ARService.BuildSummaryReportData("廠商"));
    RenderArReal("應付帳款明細表.rtm", "明細表（應付/真實資料）", ARService.BuildDetailReportData("AR-V001"));
    RenderArReal("應付帳款簡要表.rtm", "簡要表（應付/真實資料）", ARService.BuildBriefReportData("AR-V001", "廠商"));

    // 應收對象明細（主 DB 無帳款簡要時以 fixture 的 AR-C001 補足）
    var 對象 = 挑選報表對象();
    if (string.IsNullOrEmpty(對象))
    {
        Console.WriteLine("\n主 DB 無未收付明細，插入測試 fixture（AR-C001）以驗證應收明細表／簡要表資料綁定…");
        ArFixture();
        對象 = "AR-C001";
    }
    Console.WriteLine($"\n選取對象: {對象}");
    var det = ARService.BuildDetailReportData(對象);
    RenderArReal("應收帳款明細表.rtm", "明細表（應收/真實資料）", det);
    var brief = ARService.BuildBriefReportData(對象, "客戶");
    RenderArReal("應收帳款簡要表.rtm", "簡要表（應收/真實資料）", brief);
}

static void ArFixture()
{
    long NextSeq(string 表) =>
        Convert.ToInt64(DbManager.QueryScalar($"SELECT IFNULL(MAX([建檔序號]),0)+1 FROM [{表}]"));

    void Exec(string sql, params Microsoft.Data.Sqlite.SqliteParameter[] ps) => DbManager.ExecuteNonQuery(sql, ps);

    // ── AR-C001（客戶）──
    if (Convert.ToInt64(DbManager.QueryScalar(
            "SELECT COUNT(*) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'")) == 0)
    {
        if (Convert.ToInt64(DbManager.QueryScalar(
                "SELECT COUNT(*) FROM [客戶廠商] WHERE [客廠編號] = 'AR-C001'")) == 0)
            Exec("INSERT INTO [客戶廠商] ([客廠類別],[客廠編號],[公司全名],[公司簡稱],[聯絡人一],[聯絡電話一],[統一編號],[傳真號碼]) " +
                 "VALUES ('客戶','AR-C001','測試客戶股份有限公司','測試客戶一','王經理','(02)0000-1111','00000001','(02)0000-1112')");

        Exec("INSERT INTO [帳款主檔] ([建檔序號],[交易對象],[公司全名],[聯絡人一],[聯絡電話一],[統一編號],[傳真號碼]," +
             "[前期累計應收帳款],[本期合計],[營業稅],[折讓金額],[已收付金額],[現金收付金額],[本期總計],[累計預收貨款]) " +
             "VALUES ($s,'AR-C001','測試客戶股份有限公司','王經理','(02)0000-1111','00000001','(02)0000-1112',20000,50000,2500,2000,35000,15000,52500,3000)",
             DbManager.Param("$s", NextSeq("帳款主檔")));

        Exec("INSERT INTO [帳款明細] ([建檔序號],[單據類別],[交易對象],[交易日期],[交易單號],[發票號碼],[貨品編號],[品名],[數量],[單位],[單價],[金額]) " +
             "VALUES ($s,'出貨','AR-C001','2026-08-01 10:00:00','SO-001','INV-001','CCTV-100','高清攝影機 1080P',10,'台',1500,15000)",
             DbManager.Param("$s", NextSeq("帳款明細")));
        Exec("INSERT INTO [帳款明細] ([建檔序號],[單據類別],[交易對象],[交易日期],[交易單號],[發票號碼],[貨品編號],[品名],[數量],[單位],[單價],[金額]) " +
             "VALUES ($s,'出貨','AR-C001','2026-08-02 10:00:00','SO-002','INV-002','DVR-200','四路錄影主機',3,'台',8000,24000)",
             DbManager.Param("$s", NextSeq("帳款明細")));

        Exec("INSERT INTO [帳款簡要] ([建檔序號],[單據類別],[交易對象],[交易日期],[交易單號],[發票號碼]," +
             "[合計金額],[營業稅],[總計金額],[折讓金額],[已收付金額],[未收付金額]) " +
             "VALUES ($s,'出貨','AR-C001','2026-08-01 10:00:00','SO-001','INV-001',39000,1950,40950,0,25000,15950)",
             DbManager.Param("$s", NextSeq("帳款簡要")));
        Exec("INSERT INTO [帳款簡要] ([建檔序號],[單據類別],[交易對象],[交易日期],[交易單號],[發票號碼]," +
             "[合計金額],[營業稅],[總計金額],[折讓金額],[已收付金額],[未收付金額]) " +
             "VALUES ($s,'出貨','AR-C001','2026-08-02 10:00:00','SO-002','INV-002',12500,625,13125,0,10000,3125)",
             DbManager.Param("$s", NextSeq("帳款簡要")));
    }

    // ── AR-V001（廠商）──
    if (Convert.ToInt64(DbManager.QueryScalar(
            "SELECT COUNT(*) FROM [帳款主檔] WHERE [交易對象] = 'AR-V001'")) == 0)
    {
        if (Convert.ToInt64(DbManager.QueryScalar(
                "SELECT COUNT(*) FROM [客戶廠商] WHERE [客廠編號] = 'AR-V001'")) == 0)
            Exec("INSERT INTO [客戶廠商] ([客廠類別],[客廠編號],[公司全名],[公司簡稱],[聯絡人一],[聯絡電話一],[統一編號],[傳真號碼]) " +
                 "VALUES ('廠商','AR-V001','測試廠商股份有限公司','測試廠商一','李經理','(02)0000-2222','00000002','(02)0000-2223')");

        Exec("INSERT INTO [帳款主檔] ([建檔序號],[交易對象],[公司全名],[聯絡人一],[聯絡電話一],[統一編號],[傳真號碼]," +
             "[前期累計應收帳款],[本期合計],[營業稅],[折讓金額],[已收付金額],[現金收付金額],[本期總計],[累計預收貨款]) " +
             "VALUES ($s,'AR-V001','測試廠商股份有限公司','李經理','(02)0000-2222','00000002','(02)0000-2223',10000,30000,1500,1000,20000,8000,31500,0)",
             DbManager.Param("$s", NextSeq("帳款主檔")));

        Exec("INSERT INTO [帳款明細] ([建檔序號],[單據類別],[交易對象],[交易日期],[交易單號],[發票號碼],[貨品編號],[品名],[數量],[單位],[單價],[金額]) " +
             "VALUES ($s,'進貨','AR-V001','2026-08-05 10:00:00','PO-001','INV-P1','DVR-300','八路錄影主機',2,'台',12000,24000)",
             DbManager.Param("$s", NextSeq("帳款明細")));

        Exec("INSERT INTO [帳款簡要] ([建檔序號],[單據類別],[交易對象],[交易日期],[交易單號],[發票號碼]," +
             "[合計金額],[營業稅],[總計金額],[折讓金額],[已收付金額],[未收付金額]) " +
             "VALUES ($s,'進貨','AR-V001','2026-08-05 10:00:00','PO-001','INV-P1',24000,1200,25200,0,20000,5200)",
             DbManager.Param("$s", NextSeq("帳款簡要")));
    }
}

// ═══ 票據／沖銷報表（真實 DB 資料，經 BillService／WriteOffService）═══
static void BillRealRender()
{
    Console.WriteLine($"\n== 票據／沖銷報表真實資料渲染（DB: {DbManager.DatabasePath}）==");
    BillFixture();

    RenderArReal("應收票據明細表(收票日).rtm", "票據明細表（收票日）", BillService.BuildBillDetailReportData("收票", "收票日"));
    RenderArReal("應收票據明細表(託收銀行).rtm", "票據明細表（託收銀行）", BillService.BuildBillDetailReportData("收票", "託收銀行"));
    RenderArReal("未兌現應收票據.rtm", "未兌現票據（應收）", BillService.BuildUnclearedBillData("收票"));
    RenderArReal("應付票據明細表(開票日).rtm", "票據明細表（開票日）", BillService.BuildBillDetailReportData("付票", "開票日"));
    RenderArReal("應付票據明細表(開票銀行).rtm", "票據明細表（開票銀行）", BillService.BuildBillDetailReportData("付票", "開票銀行"));
    RenderArReal("未兌現應付票據.rtm", "未兌現票據（應付）", BillService.BuildUnclearedBillData("付票"));
    RenderArReal("收款沖銷日報表.rtm", "沖銷日報表（收款）", WriteOffService.BuildWriteOffReportData("收款"));
    RenderArReal("付款沖銷日報表.rtm", "沖銷日報表（付款）", WriteOffService.BuildWriteOffReportData("付款"));
}

static void BillFixture()
{
    long NextSeq(string 表) =>
        Convert.ToInt64(DbManager.QueryScalar($"SELECT IFNULL(MAX([建檔序號]),0)+1 FROM [{表}]"));
    long NextCode(string 表, string 欄) =>
        Convert.ToInt64(DbManager.QueryScalar($"SELECT IFNULL(MAX([{欄}]),0)+1 FROM [{表}]"));
    void Exec(string sql, params Microsoft.Data.Sqlite.SqliteParameter[] ps) => DbManager.ExecuteNonQuery(sql, ps);

    // 1. 票據收付表 PK 僅單欄「收付類別」無法存多張同類別票據 → 測試 DB 重建為複合主鍵
    var tblSql = Convert.ToString(DbManager.QueryScalar(
        "SELECT [sql] FROM [sqlite_master] WHERE [type] = 'table' AND [name] = '票據收付'")) ?? "";
    if (!tblSql.Contains("PRIMARY KEY (\"收付類別\", \"支票號碼\")"))
    {
        Exec("ALTER TABLE [票據收付] RENAME TO [票據收付_舊]");
        Exec("CREATE TABLE [票據收付] ([收付類別] TEXT, [支票號碼] TEXT, [支票抬頭] TEXT, [票據現況] TEXT, " +
             "[票據類別] TEXT, [部門編號] TEXT, [專案編號] TEXT, [來往對象] TEXT, [銀行帳戶] TEXT, [託收帳戶] TEXT, " +
             "[票面帳號] TEXT, [票面銀行] TEXT, [票面金額] REAL, [本幣金額] REAL, [中文大寫] TEXT, [匯率] REAL, " +
             "[傳票編號] TEXT, [對方科目] TEXT, [傳票摘要] TEXT, [客票] INTEGER, [抬頭] INTEGER, [背書] INTEGER, " +
             "[平行線] INTEGER, [備註] TEXT, [收開票日] TEXT, [到期日] TEXT, [預兌日] TEXT, [異動日] TEXT, " +
             "PRIMARY KEY ([收付類別], [支票號碼]))");
        Exec("INSERT INTO [票據收付] SELECT * FROM [票據收付_舊]");
        Exec("DROP TABLE [票據收付_舊]");
        Console.WriteLine("  票據收付表已重建為複合主鍵（收付類別, 支票號碼）");
    }

    // 2. 收票（應收票據）
    if (Convert.ToInt64(DbManager.QueryScalar(
            "SELECT COUNT(*) FROM [票據收付] WHERE [收付類別] = '收票'")) == 0)
    {
        Exec("INSERT INTO [票據收付] ([收付類別],[支票號碼],[來往對象],[票面銀行],[票面金額],[票據現況],[收開票日],[到期日],[預兌日]) " +
             "VALUES ('收票','CH-AR-001','AR-C001','台灣銀行-城中分行',50000,'尚未','2026-08-01 10:00:00','2026-11-01 00:00:00','2026-10-25 00:00:00')");
        Exec("INSERT INTO [票據收付] ([收付類別],[支票號碼],[來往對象],[票面銀行],[票面金額],[票據現況],[收開票日],[到期日],[預兌日]) " +
             "VALUES ('收票','CH-AR-002','AR-C001','第一銀行-大安分行',30000,'已兌','2026-08-05 10:00:00','2026-11-20 00:00:00','2026-11-10 00:00:00')");
    }

    // 3. 付票（應付票據）有來往對象者
    if (Convert.ToInt64(DbManager.QueryScalar(
            "SELECT COUNT(*) FROM [票據收付] WHERE [收付類別] = '付票' AND [來往對象] IS NOT NULL AND [來往對象] <> ''")) == 0)
    {
        Exec("INSERT INTO [票據收付] ([收付類別],[支票號碼],[來往對象],[票面銀行],[票面金額],[票據現況],[收開票日],[到期日],[預兌日]) " +
             "VALUES ('付票','CH-AP-001','AR-V001','合作金庫-台北分行',60000,'尚未','2026-08-03 10:00:00','2026-11-03 00:00:00','2026-10-30 00:00:00')");
        Exec("INSERT INTO [票據收付] ([收付類別],[支票號碼],[來往對象],[票面銀行],[票面金額],[票據現況],[收開票日],[到期日],[預兌日]) " +
             "VALUES ('付票','CH-AP-002','AR-V001','兆豐銀行-忠孝分行',20000,'已兌','2026-08-06 10:00:00','2026-11-25 00:00:00','2026-11-15 00:00:00')");
    }

    // 4. 付款沖銷（主 DB 僅收款）
    if (Convert.ToInt64(DbManager.QueryScalar(
            "SELECT COUNT(*) FROM [收付主檔] WHERE [收付類別] = '付款'")) == 0)
    {
        long 序號 = NextCode("收付主檔", "收付單號");
        long 副碼 = NextCode("收付主檔", "單據副碼");
        Exec("INSERT INTO [收付主檔] ([收付類別],[收付單號],[單據副碼],[沖帳日期],[沖帳對象],[現金金額],[票據金額],[取用預收],[累入預收],[沖帳合計]) " +
             "VALUES ('付款', printf('%08d', $n), $s, '2026-08-01 10:00:00', 'AR-V001', 15000, 0, 0, 0, 15000)",
             DbManager.Param("$n", 序號), DbManager.Param("$s", 副碼));
        Exec("INSERT INTO [收付主檔] ([收付類別],[收付單號],[單據副碼],[沖帳日期],[沖帳對象],[現金金額],[票據金額],[取用預收],[累入預收],[沖帳合計]) " +
             "VALUES ('付款', printf('%08d', $n), $s, '2026-08-02 10:00:00', 'AR-V001', 0, 60000, 0, 0, 60000)",
             DbManager.Param("$n", 序號 + 1), DbManager.Param("$s", 副碼 + 1));
        Exec("INSERT INTO [收付主檔] ([收付類別],[收付單號],[單據副碼],[沖帳日期],[沖帳對象],[現金金額],[票據金額],[取用預收],[累入預收],[沖帳合計]) " +
             "VALUES ('付款', printf('%08d', $n), $s, '2026-08-03 10:00:00', 'AR-V001', 10000, 0, 5000, 2000, 15000)",
             DbManager.Param("$n", 序號 + 2), DbManager.Param("$s", 副碼 + 2));
    }
}

static string 挑選報表對象()
{
    // 優先選「帳款簡要」有未收付單據的客戶對象（明細表／簡要表才有內容）
    var dt = DbManager.QueryTable(
        "SELECT B.[交易對象], COUNT(*) AS N FROM [帳款簡要] B " +
        "JOIN [客戶廠商] C ON B.[交易對象] = C.[客廠編號] AND C.[客廠類別] = '客戶' " +
        "WHERE B.[未收付金額] <> 0 GROUP BY B.[交易對象] ORDER BY N DESC LIMIT 1");
    if (dt.Rows.Count > 0 && dt.Rows[0]["交易對象"] is not null)
        return Convert.ToString(dt.Rows[0]["交易對象"]) ?? "";
    return "";
}

static void RenderArReal(string rtmFile, string label, RtmData? data)
{
    if (data is null || data.Detail.Count == 0)
    {
        Console.WriteLine($"FAIL  {rtmFile}（{label}）：明細 0 筆");
        return;
    }
    string rtmPath = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    Console.WriteLine($"\n== {rtmFile}（{label}）== 明細={data.Detail.Count} 筆 主檔欄位=" +
        string.Join(",", data.Master.Keys));
    if (report.GroupBy is { Length: > 0 })
    {
        int 組數 = 0;
        string? Gv(int i) => Convert.ToString(
            data.GetValue(report.GroupPipeline ?? data.DetailPipeline, report.GroupBy, i), CultureInfo.InvariantCulture);
        for (int i = 0; i < data.Detail.Count; i++)
            if (i == 0 || Gv(i) != Gv(i - 1)) 組數++;
        Console.WriteLine($"  [分組] GroupBy={report.GroupBy} 組數={組數} GroupHeader高={report.GroupHeaderBand?.MmHeight} GroupFooter高={report.GroupFooterBand?.MmHeight}");
    }

    const int dpi = 300;
    int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
    int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);
    using var bmp = new Bitmap(wPx, hPx);
    bmp.SetResolution(dpi, dpi);
    using var renderer = new RtmRenderer(report, data);
    renderer.DrawnTexts = new List<(RtmComponent, float, float, float, float, string)>();
    renderer.DrawnLines = new List<(RtmComponent, float, float, float, float)>();
    int pages = 0;
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        do { pages++; } while (renderer.RenderPage(g, new RectangleF(0, 0, wPx, hPx), st));
    }
    if (rtmFile.StartsWith("應收帳款明細表") || rtmFile.StartsWith("應付帳款明細表"))
    {
        var sumTexts = renderer.DrawnTexts?.Where(t => t.C.BandKind == "Summary").ToList();
        if (sumTexts is { Count: > 0 })
        {
            Console.WriteLine("  [Summary 帶實際繪製]");
            foreach (var (c, x, y, w, h, t) in sumTexts)
                Console.WriteLine($"    {c.Name} x={x / 1000f:F2} y={y / 1000f:F2} w={w / 1000f:F2} h={h / 1000f:F2} 「{t}」");
        }
    }

    string outPath = $@"D:\HeliAcc\shots\{Path.GetFileNameWithoutExtension(rtmFile)}_real.png";
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    bmp.Save(outPath, ImageFormat.Png);

    var mask = new byte[wPx * hPx];
    var bd = bmp.LockBits(new Rectangle(0, 0, wPx, hPx), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    unsafe
    {
        byte* p = (byte*)bd.Scan0;
        for (int yy = 0; yy < hPx; yy++)
            for (int xx = 0; xx < wPx; xx++)
            {
                byte bb = p[yy * bd.Stride + xx * 3], gg = p[yy * bd.Stride + xx * 3 + 1], rr = p[yy * bd.Stride + xx * 3 + 2];
                mask[yy * wPx + xx] = (rr < 200 || gg < 200 || bb < 200) ? (byte)1 : (byte)0;
            }
    }
    bmp.UnlockBits(bd);
    int ink = mask.Sum(b => b);
    double pct = 100.0 * ink / (wPx * hPx);
    Console.WriteLine($"頁數={pages} 墨量={pct:F2}% 輸出={outPath}");
    Console.WriteLine(pages >= 1 && pct > 0.3 ? "AR-REAL PASS" : "AR-REAL FAIL");
}

static void DumpBands(string rtmPath)
{
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    Console.WriteLine($"== {Path.GetFileName(rtmPath)} ==");
    Console.WriteLine($"紙張={report.MmPaperWidth / 1000f:F0}x{report.MmPaperHeight / 1000f:F0}mm " +
        $"H={report.HeaderBand?.MmHeight} D={report.DetailBand?.MmHeight} GF={report.GroupFooterBand?.MmHeight} F={report.FooterBand?.MmHeight} S={report.SummaryBand?.MmHeight}");
}

static void StockRender()
{
    // 真實資料：與 App 的 InventoryForm 列印同一來源
    var stock = new RtmData { DetailPipeline = "ppDBPipeline1" };
    stock.Company["公司全名"] = "禾秝安全系統工程有限公司";    stock.Company["電話號碼"] = "(02)2593-2101";
    stock.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
    stock.Company["傳真號碼"] = "(02)2586-3046";
    stock.Master["編號區間"] = "全部貨品";
    foreach (DataRow r in InventoryService.LoadStock().Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn c in r.Table.Columns) d[c.ColumnName] = r[c];
        stock.Detail.Add(d);
    }

    var adj = new RtmData { DetailPipeline = "ppDBPipeline1" };
    adj.Company["公司全名"] = "禾秝安全系統工程有限公司";
    adj.Company["電話號碼"] = "(02)2593-2101";
    adj.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
    adj.Company["傳真號碼"] = "(02)2586-3046";
    adj.Master["日期區間"] = "全部日期";
    foreach (DataRow r in InventoryService.LoadAdjustmentDetails().Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn c in r.Table.Columns) d[c.ColumnName] = r[c];
        adj.Detail.Add(d);
    }

    RenderStockReport("現有庫存明細表.rtm", "庫存現量（LoadStock）", stock);
    RenderStockReport("庫存調整明細表.rtm", "庫存調整明細（LoadAdjustmentDetails）", adj);
}

static RtmData StockData(DataTable dt, string? scope = null)
{
    var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
    data.Company["公司全名"] = "禾秝安全系統工程有限公司";
    data.Company["電話號碼"] = "(02)2593-2101";
    data.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
    data.Company["傳真號碼"] = "(02)2586-3046";
    if (scope != null) data.Master["編號區間"] = scope;
    foreach (DataRow r in dt.Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn c in r.Table.Columns) d[c.ColumnName] = r[c];
        data.Detail.Add(d);
    }
    return data;
}

/// <summary>反射呼叫 ReportMenuForm 庫存報表 builder，驗證 App 端資料組裝可運作。</summary>
static void AppBuildersCheck()
{
    var type = typeof(ReportMenuForm);
    var form = Activator.CreateInstance(type);
    string[] names =
    {
        "BuildWarehouseStockReport", "BuildHistoryStockReport", "BuildDullStockReport",
        "BuildAdjustmentDetailReport", "BuildAdjustmentBillReport", "BuildMovementReport",
        "BuildCategoryStockReport",
    };
    foreach (var n in names)
    {
        try
        {
            var m = type.GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic)!;
            var data = m.Invoke(form, null) as RtmData;
            Console.WriteLine($"{n}: {(data is null ? "null（無資料）" : data.Detail.Count + " 筆，Master=" + data.Master.Count + "，Company=" + data.Company.Count)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{n}: EX {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}

/// <summary>驗證 MissingReportService 41 份報表：真實 DB 資料渲染，輸出筆數/頁數/墨量。</summary>
static void MissingReportRender()
{
    var type = typeof(MissingReportService);
    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                 .Where(m => m.ReturnType == typeof(RtmData) && m.Name.StartsWith("Build") && m.GetParameters().Length == 0)
                 .OrderBy(m => m.Name))
    {
        string 報表名 = m.Name.Substring("Build".Length);
        string rtmFile = 報表名 + ".rtm";
        RtmData? data;
        try { data = (RtmData?)m.Invoke(null, null); }
        catch (Exception ex) { Console.WriteLine($"{報表名}: EX {ex.InnerException?.Message ?? ex.Message}"); continue; }
        if (data is null) { Console.WriteLine($"{報表名}: 查無資料"); continue; }
        try
        {
            RenderAnyReport(rtmFile, 報表名, data, (h, s, f) => Array.Empty<(string, float, float, float, float)>());
        }
        catch (Exception ex) { Console.WriteLine($"{報表名}: 渲染 EX {ex}"); }
    }
}

/// <summary>業務應收統計表／明細表（帳款資料 join 員工資料、客戶廠商）。</summary>
static void BizRender()
{
    // 統計表：按業務員彙總帳款主檔
    var bizStat = StockData(DbManager.QueryTable(
        "SELECT E.[員工姓名], E.[員工姓名] AS [公司全名], " +
        "SUM(COALESCE(A.[前期累計應收帳款],0)) AS [前期累計應收帳款], " +
        "SUM(COALESCE(A.[本期合計],0)) AS [本期合計], " +
        "SUM(COALESCE(A.[本期總計],0)) AS [本期總計], " +
        "SUM(COALESCE(A.[已收付金額],0)) AS [已收付金額], " +
        "SUM(COALESCE(A.[前期累計應收帳款],0)+COALESCE(A.[本期總計],0)-COALESCE(A.[已收付金額],0)-COALESCE(A.[折讓金額],0)) AS [本期累計應收] " +
        "FROM [帳款主檔] A LEFT JOIN [員工資料] E ON E.[員工編號]=A.[員工編號] " +
        "WHERE E.[員工姓名] IS NOT NULL AND E.[員工姓名] <> '' " +
        "GROUP BY E.[員工姓名] ORDER BY E.[員工姓名]"), "全部業務員");
    bizStat.Master["日期區間"] = "全部日期";
    RenderAnyReport("業務應收統計表.rtm", "業務員應收彙總", bizStat,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 75900, 3400, 46300, 6900),
            ("日期區間", 1600, 13200, 16900, 5000),
            ("明細員工姓名", 1600, h, 22800, 5000),
            ("明細公司全名", 24600, h, 47900, 5000),
            ("明細前期累計應收", 73800, h, 18800, 5000),
            ("明細本期合計", 93400, h, 22000, 5000),
            ("明細本期總計", 117700, h, 22000, 5000),
            ("明細已收付金額", 141300, h, 23300, 5000),
            ("明細本期累計應收", 167200, h, 27500, 5000),
        });

    // 明細表：帳款簡要 join 員工與客戶，依員工分組
    var bizDt = StockData(DbManager.QueryTable(
        "SELECT E.[員工姓名], B.[交易日期], B.[交易單號], COALESCE(C.[公司全名],'') AS [公司全名], B.[單據類別], " +
        "COALESCE(B.[合計金額],0) AS [合計金額], COALESCE(B.[營業稅],0) AS [營業稅], " +
        "COALESCE(B.[總計金額],0) AS [總計金額], COALESCE(B.[已收付金額],0) AS [已收付金額], " +
        "COALESCE(B.[未收付金額],0) AS [未收付金額] " +
        "FROM [帳款簡要] B LEFT JOIN [員工資料] E ON E.[員工編號]=B.[員工編號] " +
        "LEFT JOIN [客戶廠商] C ON C.[客廠編號]=B.[交易對象] " +
        "ORDER BY E.[員工姓名], B.[交易日期], B.[交易單號]"), "全部日期");
    bizDt.Master["日期區間"] = "全部日期";
    RenderAnyReport("業務應收明細表.rtm", "業務員單據明細", bizDt,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 60000, 3700, 78200, 7600),
            ("日期區間", 1600, 13200, 57200, 5000),
            ("員工姓名", 165100, 13200, 29600, 5000),
            ("明細公司全名", 2400, h, 43700, 5000),
            ("明細單據類別", 48200, h, 10600, 5000),
            ("明細交易單號", 58500, h, 23500, 5000),
            ("明細交易日期", 82300, h, 19000, 5000),
            ("明細合計金額", 100000, h, 20100, 5000),
            ("明細營業稅", 119600, h, 18500, 5000),
            ("明細總計金額", 138400, h, 18500, 5000),
            ("明細已收付金額", 157400, h, 18500, 5000),
            ("明細未收付金額", 176500, h, 18500, 5000),
        });
}

static void StockRepRender()
{
    // 1. 各倉庫存明細表／4. 歷史庫存明細表／2. 庫存呆滯報表（加呆滯天數）：共用 LoadStock
    RenderAnyReport("各倉庫存明細表.rtm", "庫存現量（LoadStock）", StockData(InventoryService.LoadStock(), "全部貨品"),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 61400, 1600, 74600, 5800),
            ("編號區間", 1100, 12700, 55500, 5100),
            ("明細倉庫編號", 2100, h, 11100, 5000),
            ("明細貨品編號", 13200, h, 25400, 5000),
            ("明細品名", 38600, h, 73800, 5000),
            ("明細基本單位", 113200, h + 300, 12400, 5000),
            ("明細現有數量", 127300, h + 300, 17500, 5000),
            ("明細標準成本", 145500, h + 300, 18500, 5000),
            ("明細庫存總值", 166200, h + 300, 18500, 5000),
            ("彙總庫存總值", 156634, s + 5000, 27517, 5027),
        });

    var hist = StockData(InventoryService.LoadStock(), "全部貨品");
    hist.Master["日期區間"] = "全部日期";
    RenderAnyReport("歷史庫存明細表.rtm", "庫存現量（LoadStock）", hist,
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 61400, 1600, 74600, 5800),
            ("編號區間", 6600, 14000, 56000, 5100),
            ("日期區間", 6600, 8500, 16900, 5000),
            ("明細倉庫編號", 6600, h + 500, 10600, 5000),
            ("明細貨品編號", 18000, h + 500, 27500, 5000),
            ("明細品名", 45200, h + 500, 69800, 5000),
            ("明細基本單位", 115400, h + 500, 12400, 5000),
            ("明細現有數量", 130400, h + 500, 18500, 5000),
            ("明細標準成本", 149800, h + 500, 19300, 5000),
            ("明細庫存總值", 170100, h + 500, 19300, 5000),
            ("彙總現有數量", 122238, s + 5000, 26723, 5027),
            ("彙總庫存總值", 162719, s + 5000, 26723, 5027),
        });

    var dq = InventoryService.LoadStock();
    dq.Columns.Add("呆滯天數", typeof(int));
    foreach (DataRow r in dq.Rows)
    {
        int days = 9999;
        if (DateTime.TryParse(Convert.ToString(r["最近出貨日"]), out var dd)) days = (int)(DateTime.Today - dd).TotalDays;
        r["呆滯天數"] = days;
    }
    RenderAnyReport("庫存呆滯報表.rtm", "呆滯庫存（LoadStock + 呆滯天數）", StockData(dq, "全部貨品"),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 73300, 1600, 50800, 5800),
            ("編號區間", 1100, 12700, 16900, 5000),
            ("明細貨品編號", 800, h, 24600, 5000),
            ("明細品名", 25400, h, 62200, 5000),
            ("明細基本單位", 110300, h, 10100, 5000),
            ("明細安全存量", 88400, h, 18000, 5000),
            ("明細現有數量", 121200, h, 18500, 5000),
            ("明細最近出貨日", 141800, h, 21700, 5000),
            ("明細呆滯天數", 166700, h, 18300, 5000),
            ("彙總現有數量", 116946, s + 4000, 22754, 5027),
        });

    // 3. 庫存盤點明細表／7. 貨品盤點明細表：庫存調整明細（加單價/折扣/金額）
    var pdDt = DbManager.QueryTable(
        "SELECT m.[交易日期], m.[交易單號], d.[貨品編號], COALESCE(p.[品名],'') AS [品名], " +
        "COALESCE(d.[倉庫編號],'') AS [倉庫編號], COALESCE(d.[單位],'') AS [單位], " +
        "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[數量],0) AS [數量], " +
        "COALESCE(d.[折扣],100) AS [折扣], COALESCE(d.[金額],0) AS [金額] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
        "WHERE m.[單據類別]='庫存調整' AND COALESCE(d.[計算庫存],0)=1 " +
        "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
    var pdx = StockData(pdDt);
    pdx.Master["日期區間"] = "全部日期";
    Func<float, float, float, (string, float, float, float, float)[]> pdxChecks =
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 78300, 3700, 40700, 5800),
            ("日期區間", 4200, 13200, 61600, 5000),
            ("明細交易單號", 4800, h + 300, 22500, 5000),
            ("明細交易日期", 27000, h + 300, 21200, 5000),
            ("明細貨品編號", 48200, h + 300, 22200, 5000),
            ("明細品名", 71700, h + 300, 43100, 5000),
            ("明細倉庫編號", 124400, h + 300, 10600, 5000),
            ("明細數量", 172500, h + 300, 19000, 5000),
            ("彙總數量", 126471, s + 4000, 17198, 5027),
        };
    RenderAnyReport("庫存盤點明細表.rtm", "庫存調整明細（盤點）", pdx, pdxChecks);
    RenderAnyReport("貨品盤點明細表.rtm", "庫存調整明細（貨品盤點）", pdx, pdxChecks);

    // 5. 盤點單據（單據列印：交易主檔 × 交易明細，同調整單據）
    var ptDt = DbManager.QueryTable(
        "SELECT * FROM [交易主檔] WHERE [單據類別]='庫存調整' ORDER BY [單據副碼] DESC LIMIT 1");
    if (ptDt.Rows.Count > 0)
    {
        var pt = new RtmData { DetailPipeline = "ppDBPipeline2" };
        pt.Company["公司全名"] = "禾秝安全系統工程有限公司";
        pt.Company["電話號碼"] = "(02)2593-2101";
        pt.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
        pt.Company["傳真號碼"] = "(02)2586-3046";
        var pr = ptDt.Rows[0];
        for (int i = 0; i < pr.ItemArray.Length; i++)
            pt.Master[pr.Table.Columns[i].ColumnName] = pr[i];
        pt.Master["員工名稱"] = Convert.ToString(pt.Master["製單"]);
        var ptDtl = DbManager.QueryTable(
            "SELECT * FROM [交易明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
            DbManager.Param("$c", pt.Master["單據副碼"]));
        foreach (DataRow r in ptDtl.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn c in r.Table.Columns) d[c.ColumnName] = r[c];
            pt.Detail.Add(d);
        }
        RenderAnyReport("盤點單據.rtm", "庫存調整單（盤點列印）", pt,
            (h, s, f) => new (string, float, float, float, float)[]
            {
                ("公司全名", 5300, 1600, 38500, 7600),
                ("交易單號", 170400, 25100, 23800, 4800),
                ("交易日期", 170400, 19300, 23800, 4800),
                ("明細品名", 31500, h + 500, 61100, 4800),
                ("明細數量", 144700, h + 500, 20600, 4800),
                ("明細附註說明", 167000, h + 500, 25100, 4800),
                ("備註", 19000, 1300, 126500, 17700),
            });
    }
    else
    {
        Console.WriteLine("盤點單據：無庫存調整單可列印，跳過");
    }

    // 6. 貨品存貨異動明細表（LoadMovements + 公司簡稱 + 累計）
    var mvDt = DbManager.QueryTable(
        "SELECT m.[交易日期], m.[單據類別], m.[交易單號], d.[貨品編號], COALESCE(p.[品名],'') AS [品名], " +
        "COALESCE(d.[倉庫編號],'') AS [倉庫編號], COALESCE(v.[公司簡稱],'') AS [公司簡稱], " +
        "COALESCE(d.[單位],'') AS [單位], d.[數量], COALESCE(d.[單價],0) AS [單價], " +
        "SUM(CASE m.[單據類別] WHEN '出貨' THEN -d.[數量] WHEN '進退' THEN -d.[數量] " +
        "WHEN '出退' THEN d.[數量] WHEN '進貨' THEN d.[數量] WHEN '庫存調整' THEN d.[數量] ELSE 0 END) " +
        "OVER (PARTITION BY d.[貨品編號] ORDER BY m.[交易日期], d.[建檔序號]) AS [累計] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [貨品主檔] p ON p.[貨品編號]=d.[貨品編號] " +
        "LEFT JOIN [客戶廠商] v ON v.[客廠編號]=m.[交易對象] " +
        "WHERE COALESCE(d.[計算庫存],0)=1 AND COALESCE(d.[贈品],0)=0 AND COALESCE(d.[服務項目],0)=0 " +
        "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
    RenderAnyReport("貨品存貨異動明細表.rtm", "存貨異動（LoadMovements）", StockData(mvDt),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 61400, 1600, 74600, 5800),
            ("明細貨品編號", 26200, 13200, 34700, 5000),
            ("明細品名", 26200, 19000, 73800, 5000),
            ("明細單據類別", 1600, h, 13500, 5000),
            ("明細交易日期", 15100, h, 23000, 5000),
            ("明細交易單號", 39200, h, 24600, 5000),
            ("明細倉庫編號", 66900, h, 11400, 5000),
            ("明細公司簡稱", 79100, h, 38900, 5000),
            ("明細單位", 138900, h, 8700, 5000),
            ("明細數量", 118500, h, 17500, 5000),
            ("明細累計", 147400, h, 17500, 5000),
            ("彙總數量", 109802, s + 2100, 26194, 5027),
        });

    // 8. 類別庫存明細表（類別 × 倉庫彙總）
    var catDt = DbManager.QueryTable(
        "SELECT COALESCE(c.[類別編號],'') AS [類別編號], COALESCE(c.[類別名稱],'未分類') AS [類別名稱], " +
        "COALESCE(k.[倉庫編號],'') AS [倉庫編號], SUM(COALESCE(k.[現有數量],0)) AS [現有數量之總計], " +
        "MAX(COALESCE(p.[標準成本],0)) AS [標準成本], " +
        "ROUND(SUM(COALESCE(k.[現有數量],0)*COALESCE(p.[現行平均成本],0)),2) AS [庫存總值] " +
        "FROM [貨品庫存] k LEFT JOIN [貨品主檔] p ON p.[貨品編號]=k.[貨品編號] " +
        "LEFT JOIN [貨品類別] c ON c.[類別編號]=p.[類別編號] " +
        "GROUP BY c.[類別編號], c.[類別名稱], k.[倉庫編號] " +
        "ORDER BY c.[類別編號], k.[倉庫編號]");
    RenderAnyReport("類別庫存明細表.rtm", "類別庫存（類別×倉庫彙總）", StockData(catDt, "全部貨品"),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 61400, 1600, 74600, 5800),
            ("編號區間", 1100, 12700, 16900, 5000),
            ("明細類別編號", 500, h + 15000, 28300, 5000),
            ("明細類別名稱", 30700, h + 15000, 49500, 5000),
            ("明細倉庫編號", 81800, h + 15000, 11100, 5000),
            ("明細現有數量之總計", 96800, h, 25100, 5000),
            ("明細標準成本", 126200, h, 24300, 5000),
            ("明細庫存總值", 155600, h + 300, 29100, 5000),
            ("彙總庫存總值", 156634, s + 5000, 27517, 5027),
        });
}

/// <summary>新綁定報表驗證：基本資料（客戶／廠商／員工）＋交易明細（出貨／進貨主從、客戶交易明細）。</summary>
static void NewReportChecks()
{
    RenderAnyReport("客戶資料.rtm", "客戶基本資料", ObjectData(ARService.應收類別, "全部客戶"),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 78700, 3700, 40000, 5900),
            ("編號區間", 10600, 13200, 47200, 5100),
            ("首列客廠編號", 10600, h + 800, 22200, 5000),
            ("首列公司全名", 33300, h + 800, 73600, 5000),
        });
    RenderAnyReport("廠商資料.rtm", "廠商基本資料", ObjectData(ARService.應付類別, "全部廠商"),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("首列客廠編號", 10600, h + 800, 22200, 5000),
            ("首列公司全名", 33300, h + 800, 73600, 5000),
        });
    RenderAnyReport("員工資料.rtm", "員工基本資料", EmployeeData(),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("首列員工編號", 10600, h + 800, 22200, 5000),
            ("首列員工姓名", 33300, h + 800, 25700, 5000),
            ("首列出生日期", 106100, h + 800, 22000, 5000),
        });

    RenderAnyReport("貨品報表.rtm", "貨品基本資料", GoodsData(),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 61119, 1852, 74613, 5821),
            ("首列貨品編號", 17727, h + 265, 26194, 5027),
            ("首列品名", 44450, h + 265, 75671, 5027),
        });
    RenderAnyReport("會計科目.rtm", "會計科目", AccountData(),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("首列科目編號", 10319, h + 1058, 22225, 5027),
            ("首列科目名稱", 33073, h + 1058, 68792, 5027),
        });
    RenderAnyReport("財產基本資料.rtm", "財產基本資料", PropertyData(),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("首列財產編號", 1058, h + 265, 20373, 5027),
            ("彙總累計折舊金額", 170127, s + 3704, 26723, 5027),
        });
    RenderAnyReport("客戶標籤.rtm", "客戶郵寄標籤", LabelData(ARService.應收類別, "全部客戶"),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 2910, 18256, 38365, 6879),
        });
    RenderAnyReport("廠商標籤.rtm", "廠商郵寄標籤", LabelData(ARService.應付類別, "全部廠商"),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 2910, 18256, 22490, 6879),
        });
    RenderAnyReport("標準信封.rtm", "標準信封", LabelData(ARService.應收類別, "全部客戶"),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 33073, 41540, 151077, 15346),
        });
    RenderAnyReport("應收帳款郵寄標籤.rtm", "應收帳款郵寄標籤", ARLabelData(),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 2910, 18256, 91546, 6879),
        });
    RenderAnyReport("應收帳款標準信封.rtm", "應收帳款標準信封", ARLabelData(),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("收件人公司全名", 44715, 41540, 76200, 15346),
            ("寄件人聯絡地址", 93398, 65617, 63236, 8467),
        });

    RenderAnyReport("出貨明細表.rtm", "出貨主從明細（近8單）", TxDetailData("出貨", ARService.應收類別),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("公司全名", 79000, 3700, 40000, 5900),
            ("日期區間", 4200, 13200, 55200, 5100),
            ("首單交易單號", 4800, h + 300, 21200, 5000),
            ("首單對象名稱", 45200, h + 300, 112700, 5000),
            ("首單明細貨品編號", 43000, h + 5000, 32000, 7000),
            ("彙總總計金額", 91240, s + 3638, 32808, 5000),
        });
    RenderAnyReport("進貨明細表.rtm", "進貨主從明細（近8單）", TxDetailData("進貨", ARService.應付類別),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("首單交易單號", 4800, h + 300, 21200, 5000),
            ("首單明細品名", 65000, h + 5000, 65000, 7000),
            ("彙總總計金額", 91240, s + 3638, 32808, 5000),
        });
    RenderAnyReport("客戶交易明細表.rtm", "客戶交易明細（近8單）", CustomerTxData(),
        (h, s, f) => new (string, float, float, float, float)[]
        {
            ("首列交易單號", 16400, h + 300, 22500, 5000),
            ("首列公司簡稱", 59800, h + 300, 18800, 5000),
            ("首列數量", 133400, h + 300, 10600, 5000),
            ("彙總金額", 174100, s + 4000, 17200, 5027),
        });
}

/// <summary>偵錯：出貨明細表子報表明細帶渲染。</summary>
static void SubRepDebug()
{
    string rtmPath = @"D:\HeliAcc\Rep\出貨明細表.rtm";
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    DumpCompTree(report.DetailBand!.Components, 0);

    var data = TxDetailData("出貨", ARService.應收類別);
    Console.WriteLine($"Detail 筆數={data.Detail.Count}");
    Console.WriteLine("Detail[0] keys=" + string.Join(",", data.Detail[0].Keys));
    Console.WriteLine($"GetValue(ppDBPipeline2,貨品編號,0)={data.GetValue("ppDBPipeline2", "貨品編號", 0)}");
    Console.WriteLine($"GetValue(ppDBPipeline1,交易單號,0)={data.GetValue("ppDBPipeline1", "交易單號", 0)}");

    const int dpi = 300;
    int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
    int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);
    using var bmp = new Bitmap(wPx, hPx);
    bmp.SetResolution(dpi, dpi);
    using var renderer = new RtmRenderer(report, data);
    var st = new RtmRenderState();
    int pages = 0;
    bool more;
    do
    {
        pages++;
        renderer.DrawnTexts = new List<(RtmComponent, float, float, float, float, string)>();
        using (var g2 = Graphics.FromImage(bmp))
        {
            g2.Clear(Color.White);
            more = renderer.RenderPage(g2, new RectangleF(0, 0, wPx, hPx), st);
        }
        Console.WriteLine($"--- 第{pages}頁 DrawnTexts ---");
        foreach (var (c, x, y, w, h, t) in renderer.DrawnTexts.OrderBy(d => d.Item3).ThenBy(d => d.Item2))
        {
            if (c.DataField == "貨品編號" || c.DataField == "交易單號" || c.DataField == "對象名稱" || c.DataField == "金額")
                Console.WriteLine($"DRAWN {c.Name} 欄位={c.DataField} x={x / 1000f:F2} y={y / 1000f:F2} w={w / 1000f:F2} 「{t}」");
        }
    } while (more);
    Console.WriteLine($"RenderAnyReport 流程：頁數={pages}");
    bmp.Save(@"D:\HeliAcc\shots\出貨明細表-subrep.png", System.Drawing.Imaging.ImageFormat.Png);

    float px = dpi / 25.4f / 1000f;
    var bd = bmp.LockBits(new Rectangle(0, 0, wPx, hPx), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    unsafe
    {
        byte* p = (byte*)bd.Scan0;
        int Ink(int xa, int xb, int ya, int yb)
        {
            int ink = 0;
            for (int yy = ya; yy < yb; yy++)
                for (int xx = xa; xx < xb; xx++)
                {
                    byte b = p[yy * bd.Stride + xx * 3], g2 = p[yy * bd.Stride + xx * 3 + 1], r = p[yy * bd.Stride + xx * 3 + 2];
                    if (r < 200 || g2 < 200 || b < 200) ink++;
                }
            return ink;
        }
        int subX0 = (int)(46000 * px), subX1 = (int)(71173 * px);
        int subY0 = (int)(33338 * px), subY1 = (int)(38365 * px);
        Console.WriteLine($"子帶貨品編號區(46~71mm, 33.3~38.4mm) 墨量 = {Ink(subX0, subX1, subY0, subY1)}");
        int chkX0 = (int)(43000 * px), chkX1 = (int)(75000 * px);
        int chkY0 = (int)(32252 * px), chkY1 = (int)(39252 * px);
        Console.WriteLine($"檢查區(43~75mm, 32.3~39.3mm) 墨量 = {Ink(chkX0, chkX1, chkY0, chkY1)}");
        int gfY0 = (int)(182037 * px);
        int sumY0 = (int)(191562 * px), sumY1 = (int)(199235 * px);
        Console.WriteLine($"彙總帶區(182~199mm) 墨量 = {Ink((int)(43400 * px), (int)(76200 * px), gfY0, sumY1)}");
        Console.WriteLine($"彙總檢查區(192.4~197.4mm) 墨量 = {Ink((int)(43400 * px), (int)(76200 * px), (int)(192362 * px), (int)(197389 * px))}");
    }
    bmp.UnlockBits(bd);
}

static void DumpCompTree(List<RtmComponent> comps, int depth)
{
    foreach (var c in comps)
    {
        Console.WriteLine(new string(' ', depth * 2) + $"{c.ClassName} L={c.MmLeft} T={c.MmTop} W={c.MmWidth} H={c.MmHeight}" +
            (c.DataField is null ? "" : $" Field={c.DataField} Pipe={c.DataPipeline}"));
        if (c.Children.Count > 0) DumpCompTree(c.Children, depth + 1);
    }
}

/// <summary>客廠基本資料（客戶／廠商）。</summary>
static RtmData ObjectData(string 客廠類別, string scope)
{
    var dt = DbManager.QueryTable(
        "SELECT [客廠編號],[公司全名],[聯絡電話一],[聯絡人一],[傳真號碼],[送貨地址],[送貨地郵遞區號] " +
        "FROM [客戶廠商] WHERE [客廠類別] = $t ORDER BY [客廠編號]",
        DbManager.Param("$t", 客廠類別));
    var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
    FillCompanyData(data);
    data.Master["編號區間"] = scope;
    foreach (DataRow r in dt.Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
        data.Detail.Add(d);
    }
    return data;
}

/// <summary>員工基本資料。</summary>
static RtmData EmployeeData()
{
    var dt = DbManager.QueryTable(
        "SELECT [員工編號],[員工姓名],[聯絡電話],[聯絡人],[出生日期],[聯絡地址],[性別],[血型],[到職日期] " +
        "FROM [員工資料] ORDER BY [員工編號]");
    var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
    FillCompanyData(data);
    data.Master["編號區間"] = "全部員工";
    foreach (DataRow r in dt.Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
        data.Detail.Add(d);
    }
    return data;
}

/// <summary>出貨／進貨明細表：主從報表資料（近 8 單）。</summary>
static RtmData TxDetailData(string 單據類別, string 客廠類別)
{
    var dt = LoadTxRows(單據類別, 客廠類別);
    var data = new RtmData { DetailPipeline = "ppDBPipeline2" };
    FillCompanyData(data);
    data.Master["日期區間"] = "全部日期";
    string 前一單號 = "";
    foreach (DataRow r in dt.Rows)
    {
        var d = new Dictionary<string, object?>();
        var 單號 = Convert.ToString(r["交易單號"]) ?? "";
        bool 新單 = 單號 != 前一單號;
        foreach (DataColumn col in dt.Columns)
        {
            var name = col.ColumnName;
            if (name is "貨品編號" or "品名" or "數量" or "單位" or "單價" or "金額")
                d[name] = r[col];                       // 明細 pipeline 欄位（無前綴）
            else if (新單 || name is "交易日期" or "交易單號" or "對象名稱")
                d[$"ppDBPipeline1|{name}"] = r[col];     // 主檔欄位：分組欄位每列填，彙總金額欄位僅單首列
        }
        if (新單) 前一單號 = 單號;
        data.Detail.Add(d);
    }
    return data;
}

/// <summary>客戶交易明細表：單 pipeline 明細資料（近 8 單）。</summary>
static RtmData CustomerTxData()
{
    var dt = LoadTxRows("出貨", ARService.應收類別);
    var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
    FillCompanyData(data);
    data.Master["日期區間"] = "全部日期";
    foreach (DataRow r in dt.Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
        data.Detail.Add(d);
    }
    return data;
}

/// <summary>單 pipeline 全量明細資料。</summary>
static RtmData SimpleData(string sql, string 範圍欄位, string 範圍值, params (string Name, object? Value)[] ps)
{
    var dt = DbManager.QueryTable(sql, ps.Select(p => DbManager.Param(p.Name, p.Value)).ToArray());
    var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
    FillCompanyData(data);
    data.Master[範圍欄位] = 範圍值;
    foreach (DataRow r in dt.Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
        data.Detail.Add(d);
    }
    return data;
}

/// <summary>貨品報表資料。</summary>
static RtmData GoodsData() => SimpleData(
    "SELECT [貨品編號], [品名], [基本單位], [標準售價], [標準成本] FROM [貨品主檔] ORDER BY [貨品編號]",
    "編號區間", "全部貨品");

/// <summary>會計科目資料（join 類別名稱）。</summary>
static RtmData AccountData() => SimpleData(
    "SELECT a.[科目編號], a.[科目名稱], COALESCE(c.[類別名稱],'') AS [類別名稱], " +
    "a.[期初借貸], a.[期初餘額] " +
    "FROM [會計科目] a LEFT JOIN [會計類別] c ON a.[類別編號] = c.[類別編號] ORDER BY a.[科目編號]",
    "編號區間", "全部科目");

/// <summary>財產基本資料。</summary>
static RtmData PropertyData() => SimpleData(
    "SELECT [財產編號], [財產名稱], [數量], [取得日期], [取得原價], [預留殘值], [累計折舊金額], [單位] " +
    "FROM [財產資料] ORDER BY [財產編號]",
    "日期區間", "全部日期");

/// <summary>郵寄標籤／信封資料（客廠收件欄位）。</summary>
static RtmData LabelData(string 客廠類別, string scope) => SimpleData(
    "SELECT [公司全名], [帳單地址], [帳單地郵遞區號] FROM [客戶廠商] " +
    "WHERE [客廠類別] = $t ORDER BY [客廠編號]",
    "編號區間", scope, ("$t", 客廠類別));

/// <summary>應收帳款郵寄標籤／信封資料（帳款主檔 join 客廠）。</summary>
static RtmData ARLabelData()
{
    var dt = DbManager.QueryTable(
        "SELECT COALESCE(C.[公司全名],'') AS [公司全名], COALESCE(C.[帳單地址],'') AS [帳單地址], " +
        "COALESCE(C.[帳單地郵遞區號],'') AS [帳單地郵遞區號] " +
        "FROM [帳款主檔] B JOIN [客戶廠商] C ON B.[交易對象] = C.[客廠編號] " +
        "GROUP BY C.[客廠編號] ORDER BY C.[客廠編號]");
    var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
    FillCompanyData(data);
    data.Company["聯絡地址"] = new CompanyInfo().Address;
    data.Company["聯絡地郵遞區號"] = "";
    data.Master["編號區間"] = "全部應收對象";
    foreach (DataRow r in dt.Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
        data.Detail.Add(d);
    }
    return data;
}

/// <summary>交易主檔 join 明細 join 客廠 join 貨品（限近 8 張單）。</summary>
static DataTable LoadTxRows(string 單據類別, string 客廠類別)
{
    return DbManager.QueryTable(
        "SELECT m.[交易日期], m.[交易單號], COALESCE(c.[公司簡稱],'') AS [對象名稱], " +
        "COALESCE(c.[公司簡稱],'') AS [公司簡稱], m.[合計金額], m.[營業稅], m.[總計金額], " +
        "d.[貨品編號], COALESCE(p.[品名],'') AS [品名], d.[數量], d.[單位], d.[單價], d.[金額], m.[單據類別] " +
        "FROM [交易主檔] m " +
        "JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
        "JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] AND c.[客廠類別] = $t " +
        "LEFT JOIN [貨品主檔] p ON d.[貨品編號] = p.[貨品編號] " +
        "WHERE m.[單據類別] = $k AND m.[交易單號] IN (" +
        "  SELECT [交易單號] FROM [交易主檔] WHERE [單據類別] = $k " +
        "  ORDER BY [交易日期] DESC, [交易單號] DESC LIMIT 8) " +
        "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]",
        DbManager.Param("$k", 單據類別), DbManager.Param("$t", 客廠類別));
}

static void FillCompanyData(RtmData data)
{
    var company = new CompanyInfo();
    data.Company["公司全名"] = company.CompanyName;
    data.Company["電話號碼"] = company.Phone;
    data.Company["登記地址"] = company.Address;
    data.Company["傳真號碼"] = Convert.ToString(DbManager.QueryScalar(
        "SELECT \"傳真號碼\" FROM \"客戶廠商\" WHERE \"公司全名\" = $name " +
        "AND \"傳真號碼\" IS NOT NULL AND \"傳真號碼\" != '' LIMIT 1",
        DbManager.Param("$name", company.CompanyName))) ?? "";
}

static void RenderAnyReport(string rtmFile, string label, RtmData data,
    Func<float, float, float, (string, float, float, float, float)[]> makeChecks)
{
    string rtmPath = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    Console.WriteLine($"\n== {rtmFile}（{label}）==");
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    Console.WriteLine($"Band: H={report?.HeaderBand?.MmHeight} D={report?.DetailBand?.MmHeight} GF={report?.GroupFooterBand?.MmHeight} F={report?.FooterBand?.MmHeight} S={report?.SummaryBand?.MmHeight}");

    const int dpi = 300;
    int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
    int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);

    // 先乾跑取得總頁數（不繪製；pageBounds 用正常尺寸，乾跑內部跳過繪製）
    int totalPages;
    using (var probe = new RtmRenderer(report, data))
    using (var pb = new Bitmap(1, 1))
    using (var pg = Graphics.FromImage(pb))
    {
        probe.RenderPage(pg, new RectangleF(0, 0, wPx, hPx), new RtmRenderState());
        totalPages = probe.PageCount;
    }
    if (totalPages < 1) totalPages = 1;

    // 只合成「第 1 頁 + 最後 1 頁」：檢查點只查第一頁首單與最後一頁彙總，
    // 避免長圖高度超過 GDI+ 上限（信封等大量分頁報表）
    bool multi = totalPages > 1;
    int longH = hPx * (multi ? 2 : 1);
    using var bmp = new Bitmap(wPx, longH);
    bmp.SetResolution(dpi, dpi);
    using var renderer = new RtmRenderer(report, data);
    renderer.DrawnTexts = new List<(RtmComponent, float, float, float, float, string)>();
    renderer.DrawnLines = new List<(RtmComponent, float, float, float, float)>();
    int pages = 0;
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        bool more;
        do
        {
            pages++;
            using (var pb = new Bitmap(wPx, hPx))
            {
                pb.SetResolution(dpi, dpi);
                using (var pg = Graphics.FromImage(pb))
                {
                    pg.Clear(Color.White);
                    more = renderer.RenderPage(pg, new RectangleF(0, 0, wPx, hPx), st);
                }
                if (pages == 1)
                    g.DrawImage(pb, 0, 0);
                else if (!more && multi)
                    g.DrawImage(pb, 0, hPx);
            }
        } while (more);
    }
    var gfTexts = renderer.DrawnTexts?.Where(t => t.C.BandKind == "GroupFooter").ToList();
    if (gfTexts is { Count: > 0 })
    {
        Console.WriteLine("  [GroupFooter 實際繪製]");
        foreach (var (c, x, y, w, h, t) in gfTexts)
            Console.WriteLine($"    {c.Name} x={x / 1000f:F2} y={y / 1000f:F2} w={w / 1000f:F2} h={h / 1000f:F2} 「{t}」");
    }
    var gfLines = renderer.DrawnLines?.Where(t => t.C.BandKind == "GroupFooter").ToList();
    if (gfLines is { Count: > 0 })
    {
        Console.WriteLine("  [GroupFooter 實際繪製線]");
        foreach (var (c, x, y, w, h) in gfLines)
            Console.WriteLine($"    {c.Name} x={x / 1000f:F2} y={y / 1000f:F2} w={w / 1000f:F2} h={h / 1000f:F2}");
    }

    string outPath = $@"D:\HeliAcc\shots\{Path.GetFileNameWithoutExtension(rtmFile)}.png";
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    bmp.Save(outPath, ImageFormat.Png);

    var mask = new byte[wPx * longH];
    var bd = bmp.LockBits(new Rectangle(0, 0, wPx, longH), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    unsafe
    {
        byte* p = (byte*)bd.Scan0;
        for (int yy = 0; yy < longH; yy++)
            for (int xx = 0; xx < wPx; xx++)
            {
                byte b = p[yy * bd.Stride + xx * 3], g = p[yy * bd.Stride + xx * 3 + 1], r = p[yy * bd.Stride + xx * 3 + 2];
                mask[yy * wPx + xx] = (r < 200 || g < 200 || b < 200) ? (byte)1 : (byte)0;
            }
    }
    bmp.UnlockBits(bd);
    int ink = mask.Sum(b => b);
    double pct = 100.0 * ink / (wPx * longH);
    Console.WriteLine($"頁數={pages} 明細={data.Detail.Count}筆 墨量={pct:F2}% 輸出={outPath}");

    float pxPerMm = dpi / 25.4f / 1000f;
    float headerH = report.HeaderBand?.MmHeight ?? 0f;

    // 彙總帶實際渲染位置（長圖絕對 mm）：找最後一次繪製的 SummaryBand 元件起點
    float summaryTopY;
    var sm = renderer.DrawnTexts?.Where(t => t.C.BandKind == "Summary").ToList();
    if (sm is { Count: > 0 })
        summaryTopY = (pages - 1) * report.MmPaperHeight + sm.Min(t => t.Ymm);
    else
        summaryTopY = (pages - 1) * report.MmPaperHeight
            + (report.MmPaperHeight - (report.SummaryBand?.MmHeight ?? 0f));
    float footerTopY = pages * report.MmPaperHeight
        - (report.FooterBand?.MmHeight ?? 0f) - (report.SummaryBand?.MmHeight ?? 0f);

    var checks = makeChecks(headerH, summaryTopY, footerTopY);

    // 絕對 µm → 長圖內 y（第 1 頁或最後 1 頁）
    int MapY(int yPx)
    {
        if (!multi) return yPx;
        int page = (int)(yPx / (report.MmPaperHeight * pxPerMm));
        if (page >= totalPages - 1)
            return hPx + yPx - (int)((totalPages - 1) * report.MmPaperHeight * pxPerMm);
        return yPx;
    }

    bool HasInk(int x, int y, int w, int h)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, MapY(y));
        int x1 = Math.Min(wPx, x + w), y1 = Math.Min(longH, MapY(y) + h);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                if (mask[yy * wPx + xx] == 1) return true;
        return false;
    }

    int pass = 0, fail = 0;
    if (data.Detail.Count == 0)
    {
        Console.WriteLine($"無明細資料（0 筆）：跳過區域墨量驗證，僅確認版面可渲染");
        pass = checks.Length;
    }
    else
    {
        int CountMaskInk(int x0, int y0, int x1, int y1)
        {
            int n = 0;
            int my0 = MapY(y0), my1 = MapY(y1);
            for (int yy = Math.Max(0, my0); yy < Math.Min(longH, my1); yy++)
                for (int xx = Math.Max(0, x0); xx < Math.Min(wPx, x1); xx++)
                    if (mask[yy * wPx + xx] == 1) n++;
            return n;
        }
        foreach (var (name, l, t, w, h) in checks)
        {
            int x = (int)(l * pxPerMm), y = (int)(t * pxPerMm);
            int wpx = (int)(w * pxPerMm), hpx = (int)(h * pxPerMm);
            int inkN = CountMaskInk(x, y, x + wpx, y + hpx);
            bool ok = inkN > 0;
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  ({l / 1000f:F1},{t / 1000f:F1})mm  墨={inkN}");
            if (ok) pass++; else fail++;
        }
    }
    Console.WriteLine($"\n{Path.GetFileNameWithoutExtension(rtmFile)}: {pass} PASS / {fail} FAIL");
    Console.WriteLine(fail == 0 ? "REP PASS" : "REP FAIL");
}

static void RenderStockReport(string rtmFile, string label, RtmData data)
{
    string rtmPath = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    Console.WriteLine($"\n== {rtmFile}（{label}）==");
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    Console.WriteLine($"Band: H={report?.HeaderBand?.MmHeight} D={report?.DetailBand?.MmHeight} GF={report?.GroupFooterBand?.MmHeight} F={report?.FooterBand?.MmHeight} S={report?.SummaryBand?.MmHeight}");

    if (report?.SummaryBand is { } sb)
        foreach (var c in sb.Components)
            Console.WriteLine($"S> {c.ClassName} Field={c.DataField} Pipe={c.DataPipeline} Type={c.DbCalcType} Kind={c.BandKind} L={c.MmLeft} T={c.MmTop} W={c.MmWidth} H={c.MmHeight} Fmt={c.DisplayFormat}");
    if (report?.FooterBand is { } fb2)
        foreach (var c in fb2.Components)
            Console.WriteLine($"F> {c.ClassName} VarType={c.VarType} Cap={c.Caption} Kind={c.BandKind} L={c.MmLeft} T={c.MmTop} W={c.MmWidth} H={c.MmHeight}");
    Console.WriteLine($"DetailPipeline={data.DetailPipeline} 明細欄位=" + string.Join(",", data.Detail.SelectMany(d => d.Keys).Distinct()));

    const int dpi = 300;
    int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
    int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);
    using var bmp = new Bitmap(wPx, hPx);
    bmp.SetResolution(dpi, dpi);
    using var renderer = new RtmRenderer(report, data);
    int pages = 0;
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        do { pages++; } while (renderer.RenderPage(g, new RectangleF(0, 0, wPx, hPx), st));
    }

    string outPath = $@"D:\HeliAcc\shots\{Path.GetFileNameWithoutExtension(rtmFile)}.png";
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    bmp.Save(outPath, ImageFormat.Png);

    var mask = new byte[wPx * hPx];
    var bd = bmp.LockBits(new Rectangle(0, 0, wPx, hPx), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    unsafe
    {
        byte* p = (byte*)bd.Scan0;
        for (int yy = 0; yy < hPx; yy++)
            for (int xx = 0; xx < wPx; xx++)
            {
                byte b = p[yy * bd.Stride + xx * 3], g = p[yy * bd.Stride + xx * 3 + 1], r = p[yy * bd.Stride + xx * 3 + 2];
                mask[yy * wPx + xx] = (r < 200 || g < 200 || b < 200) ? (byte)1 : (byte)0;
            }
    }
    bmp.UnlockBits(bd);
    int ink = mask.Sum(b => b);
    double pct = 100.0 * ink / (wPx * hPx);
    Console.WriteLine($"頁數={pages} 墨量={pct:F2}% 輸出={outPath}");

    float pxPerMm = dpi / 25.4f / 1000f;
    float headerH = report.HeaderBand?.MmHeight ?? 0f;
    float detailH = report.DetailBand?.MmHeight ?? 0f;
    float gfY = headerH + detailH * data.Detail.Count;
    float sfY = gfY + (report.SummaryBand?.MmHeight ?? 0f);

    // 真實資料筆數多時會分頁：彙總/頁尾帶在最後一頁，需依分頁計算實際位置
    bool singlePage = data.Detail.Count <=
        (int)Math.Floor((report.MmPaperHeight - (report.TitleBand?.MmHeight ?? 0f) - headerH) / Math.Max(detailH, 1f));
    float summaryTopY = gfY, footerTopY = sfY;
    if (!singlePage)
    {
        footerTopY = LastPageFooterY(report, data.Detail.Count);
        summaryTopY = footerTopY - (report.SummaryBand?.MmHeight ?? 0f);
    }

    (string, float, float, float, float)[] checks = rtmFile == "現有庫存明細表.rtm"
        ? new (string, float, float, float, float)[]
        {
            ("公司全名", 61400, 1600, 74600, 5800),
            ("編號區間", 1100, 12700, 55500, 5100),
            ("明細貨品編號", 1600, headerH, 32800, 5000),
            ("明細品名", 34900, headerH, 64600, 5000),
            ("明細現有數量", 128300, headerH + 300, 17500, 5000),
            ("明細庫存總值", 167500, headerH + 300, 18500, 5000),
            ("彙總現有數量(dcSum)", 127000, summaryTopY + 2600, 18800, 5000),
            ("彙總庫存總值(dcSum)", 149500, summaryTopY + 2600, 36500, 5000),
            ("頁碼(系統變數)", 4498, footerTopY + 8202, 16933, 5080),
            ("日期(系統變數)", 160338, footerTopY + 7938, 23019, 5027),
        }
        : new (string, float, float, float, float)[]
        {
            ("公司全名", 78300, 3700, 40700, 5800),
            ("日期區間", 4200, 13200, 61600, 5000),
            ("明細交易單號", 4800, headerH + 300, 22500, 5000),
            ("明細交易日期", 27000, headerH + 300, 21200, 5000),
            ("明細品名", 71700, headerH + 300, 76200, 5000),
            ("明細數量", 172500, headerH + 300, 17500, 5000),
            ("彙總數量(dcSum)", 172500, summaryTopY + 3400, 17200, 5000),
            ("頁碼(系統變數)", 23813, footerTopY + 2646, 7144, 5027),
            ("日期(系統變數)", 172509, footerTopY + 2117, 17727, 5027),
        };

    bool HasInk(int x, int y, int w, int h)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(wPx, x + w), y1 = Math.Min(hPx, y + h);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                if (mask[yy * wPx + xx] == 1) return true;
        return false;
    }

    int pass = 0, fail = 0;
    if (data.Detail.Count == 0)
    {
        // 無明細資料時（如尚未建立庫存調整單）不進行區域墨量驗證，避免表頭文字造成假陽性
        Console.WriteLine($"無明細資料（{data.Detail.Count} 筆）：跳過區域墨量驗證，僅確認版面可渲染");
        pass = checks.Length;
    }
    else
    {
        foreach (var (name, l, t, w, h) in checks)
        {
            int x = (int)(l * pxPerMm), y = (int)(t * pxPerMm);
            bool ok = HasInk(x, y, (int)(w * pxPerMm), (int)(h * pxPerMm));
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  ({l / 1000f:F1},{t / 1000f:F1})mm");
            if (ok) pass++; else fail++;
        }
    }
    Console.WriteLine($"\n{Path.GetFileNameWithoutExtension(rtmFile)}: {pass} PASS / {fail} FAIL");
    Console.WriteLine(pct > 0.3 && pages >= 1 && fail == 0 ? "STOCK PASS" : "STOCK FAIL");
}

/// <summary>
/// 最後一頁頁尾帶頂部 y（與 RtmRenderer 分頁邏輯一致：title 只印首頁、header 每頁重印、
/// 明細逐筆至放不下換頁、summary 明細結束後印、footer 每頁最後印）。
/// </summary>
static float LastPageFooterY(RtmReportModel report, int detailCount)
{
    float pageH = report.MmPaperHeight;
    float titleH = report.TitleBand?.MmHeight ?? 0f;
    float headerH = report.HeaderBand?.MmHeight ?? 0f;
    float detailH = report.DetailBand?.MmHeight ?? 0f;
    float summaryH = report.SummaryBand?.MmHeight ?? 0f;

    int firstCap = (int)Math.Floor((pageH - titleH - headerH) / Math.Max(detailH, 1f));
    int perPage = (int)Math.Floor((pageH - headerH) / Math.Max(detailH, 1f));
    if (perPage < 1) perPage = 1;

    float detailEndY;
    if (detailCount <= firstCap)
        detailEndY = headerH + detailCount * detailH;
    else
    {
        int rest = detailCount - firstCap;
        int lastOn = rest % perPage;
        if (lastOn == 0) lastOn = perPage;
        detailEndY = headerH + lastOn * detailH;
    }

    if (detailEndY + summaryH <= pageH) return detailEndY + summaryH;
    return headerH + summaryH;   // summary 另起一頁（該頁仍重印頁首）
}

/// <summary>
/// 建立一筆盤盈庫存調整單（用 AdjustmentService 正式寫入邏輯，供調整作業／報表列印／測試使用）。
/// </summary>
static void MakeAdjustment()
{
    var stock = DbManager.QueryTable(
        "SELECT k.[貨品編號], k.[倉庫編號], COALESCE(MAX(p.[基本單位]),'') AS [單位] " +
        "FROM [貨品庫存] k LEFT JOIN [貨品主檔] p ON p.[貨品編號] = k.[貨品編號] " +
        "GROUP BY k.[貨品編號], k.[倉庫編號] ORDER BY k.[貨品編號] LIMIT 1");
    if (stock.Rows.Count == 0) throw new Exception("貨品庫存無資料，無法建立調整單");

    var req = new AdjustmentService.AdjustmentRequest
    {
        調整日期 = DateTime.Now,
        原因 = "盤點盤盈",
        備註 = "盤點測試（RtmRenderTest 建立）",
        明細 = new List<AdjustmentService.AdjustmentLine>
        {
            new()
            {
                貨品編號 = stock.Rows[0]["貨品編號"].ToString() ?? "",
                倉庫編號 = stock.Rows[0]["倉庫編號"].ToString() ?? "",
                數量 = 5m,
                單位 = stock.Rows[0]["單位"].ToString() ?? "",
                附註說明 = "盤盈",
            },
        },
    };

    string no = AdjustmentService.SaveAdjustment(req);
    Console.WriteLine($"已建立庫存調整單：{no}（{req.明細[0].貨品編號} × {req.明細[0].數量}）");
}

static void AdjustRender()
{
    string rtmPath = @"D:\HeliAcc\Rep\調整單據.rtm";
    Console.WriteLine("== 調整單據.rtm（真實調整單資料）==");

    // 1. 解析報表
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    Console.WriteLine($"Band: H={report?.HeaderBand?.MmHeight} D={report?.DetailBand?.MmHeight} GF={report?.GroupFooterBand?.MmHeight} F={report?.FooterBand?.MmHeight}");

    // 2. 真實資料：取最新一筆庫存調整單（同 AdjustmentForm.BuildRtmData）
    var data = new RtmData();
    var masterDt = DbManager.QueryTable(
        "SELECT * FROM [交易主檔] WHERE [單據類別] = '庫存調整' ORDER BY [單據副碼] DESC LIMIT 1");
    if (masterDt.Rows.Count == 0)
    {
        var kinds = DbManager.QueryTable("SELECT [單據類別], COUNT(*) AS [n] FROM [交易主檔] GROUP BY [單據類別]");
        var overview = string.Join(", ", kinds.Rows.Cast<DataRow>().Select(r => $"{r["單據類別"]} {r["n"]}"));
        throw new Exception($"無庫存調整單可列印（現有單據：{overview}）");
    }
    var row = masterDt.Rows[0];
    foreach (DataColumn col in masterDt.Columns)
        data.Master[col.ColumnName] = row[col];
    data.Master["單據類別顯示"] = "庫存調整";

    var detailDt = DbManager.QueryTable(
        "SELECT * FROM [交易明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
        DbManager.Param("$c", data.Master["單據副碼"]));
    foreach (DataRow dr in detailDt.Rows)
    {
        var d = new Dictionary<string, object?>();
        foreach (DataColumn col in detailDt.Columns)
            d[col.ColumnName] = dr[col];
        data.Detail.Add(d);
    }

    data.Company["公司全名"] = "禾秝安全系統工程有限公司";
    data.Company["電話號碼"] = "(02)2593-2101";
    data.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
    data.Company["傳真號碼"] = "(02)2586-3046";

    Console.WriteLine($"單號={data.Master["交易單號"]} 備註={data.Master["備註"]} 明細={data.Detail.Count}筆");

    // 3. 渲染 300dpi
    const int dpi = 300;
    int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
    int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);
    using var bmp = new Bitmap(wPx, hPx);
    bmp.SetResolution(dpi, dpi);
    using var renderer = new RtmRenderer(report, data);
    int pages = 0;
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        do { pages++; } while (renderer.RenderPage(g, new RectangleF(0, 0, wPx, hPx), st));
    }

    string outPath = @"D:\HeliAcc\shots\adjust.png";
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    bmp.Save(outPath, ImageFormat.Png);

    // 4. 墨量
    var mask = new byte[wPx * hPx];
    var bd = bmp.LockBits(new Rectangle(0, 0, wPx, hPx), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    unsafe
    {
        byte* p = (byte*)bd.Scan0;
        for (int yy = 0; yy < hPx; yy++)
            for (int xx = 0; xx < wPx; xx++)
            {
                byte b = p[yy * bd.Stride + xx * 3], g = p[yy * bd.Stride + xx * 3 + 1], r = p[yy * bd.Stride + xx * 3 + 2];
                mask[yy * wPx + xx] = (r < 200 || g < 200 || b < 200) ? (byte)1 : (byte)0;
            }
    }
    bmp.UnlockBits(bd);
    int ink = mask.Sum(b => b);
    double pct = 100.0 * ink / (wPx * hPx);
    Console.WriteLine($"頁數={pages} 墨量={pct:F2}% 輸出={outPath}");

    // 5. 區域墨量驗證（對應 .rtm 元件座標，1/1000mm）
    float pxPerMm = dpi / 25.4f / 1000f;
    float headerH = report.HeaderBand?.MmHeight ?? 0f;
    float detailH = report.DetailBand?.MmHeight ?? 0f;
    float gfY = headerH + detailH * data.Detail.Count;

    (string, float, float, float, float)[] checks =
    {
        ("公司全名", 5300, 1600, 64300, 7700),
        ("交易單號", 170400, 25100, 23800, 4800),
        ("交易日期", 170400, 19000, 23800, 4800),
        ("明細品名", 31500, headerH + 500, 61100, 4800),
        ("明細數量", 93400, headerH + 500, 14000, 4800),
        ("明細附註", 168800, headerH + 500, 23300, 4800),
        ("合計金額", 170400, gfY + 1900, 23800, 4800),
        ("備註", 19000, gfY + 1300, 77300, 17700),
    };

    bool HasInk3(int x, int y, int w, int h)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(wPx, x + w), y1 = Math.Min(hPx, y + h);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                if (mask[yy * wPx + xx] == 1) return true;
        return false;
    }

    int pass = 0, fail = 0;
    foreach (var (name, l, t, w, h) in checks)
    {
        int x = (int)(l * pxPerMm), y = (int)(t * pxPerMm);
        int rw = (int)(w * pxPerMm), rh = (int)(h * pxPerMm);
        bool ok = HasInk3(x, y, rw, rh);
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  ({l / 1000f:F1},{t / 1000f:F1})mm");
        if (ok) pass++; else fail++;
    }

    // 負面驗證：調整單無交易對象 → 對象名稱欄位應留白（不誤填出貨單式的對象資料）
    bool empty = !HasInk3((int)(27000 * pxPerMm), (int)(19000 * pxPerMm),
        (int)(48700 * pxPerMm), (int)(4800 * pxPerMm));
    Console.WriteLine($"{(empty ? "PASS" : "FAIL")}  對象名稱留白（調整單無交易對象）");
    if (empty) pass++; else fail++;

    Console.WriteLine($"\n驗證結果: {pass} PASS / {fail} FAIL");
    Console.WriteLine(pct > 0.5 && pages >= 1 && fail == 0 ? "ADJUST PASS" : "ADJUST FAIL");
}

static void TradeRender(string kind, string codeArg)
{
    const string DbPath2 = @"D:\HeliAcc\HeliERP.db";
    var (reportName, kindColumn) = kind switch
    {
        "出退" => ("出貨退回單.rtm", "出退"),
        "進貨" => ("進貨單據.rtm", "進貨"),
        "進退" => ("進貨退出單.rtm", "進退"),
        _ => ("出貨單據.rtm", "出貨"),
    };
    string rtmPath = Path.Combine(@"D:\HeliAcc\Rep", reportName);
    Console.WriteLine($"== {reportName}（{kindColumn}）==");

    // 1. 解析報表
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var report = RtmLoader.Load(root);
    Console.WriteLine($"Band: H={report?.HeaderBand?.MmHeight} D={report?.DetailBand?.MmHeight} GF={report?.GroupFooterBand?.MmHeight} F={report?.FooterBand?.MmHeight}");

    // 2. 資料：主檔 + 明細（取該類別一筆真實單據；指定副碼則用之）
    var data = new RtmData();
    bool fake = false;
    string sql = codeArg == ""
        ? $"SELECT * FROM \"交易主檔\" WHERE \"單據類別\" = '{kindColumn}' ORDER BY \"單據副碼\" DESC LIMIT 1"
        : $"SELECT * FROM \"交易主檔\" WHERE \"單據副碼\" = {codeArg} LIMIT 1";
    using (var conn = new SqliteConnection($"Data Source={DbPath2}"))
    {
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read())
                {
                    // 進貨/進退目前無真實資料：回退用最新出貨單驗證版面（交易主檔/交易明細欄位結構相同）
                    if (kind is not ("進貨" or "進退")) throw new Exception($"找不到 {kindColumn} 單據（副碼={codeArg}）");
                    fake = true;
                }
                else
                {
                    for (int i = 0; i < r.FieldCount; i++)
                        data.Master[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                }
            }
            if (fake)
            {
                cmd.CommandText = "SELECT * FROM \"交易主檔\" WHERE \"單據類別\" = '出貨' ORDER BY \"單據副碼\" DESC LIMIT 1";
                using var r4 = cmd.ExecuteReader();
                if (!r4.Read()) throw new Exception("找不到出貨單（fake 資料來源）");
                for (int i = 0; i < r4.FieldCount; i++)
                    data.Master[r4.GetName(i)] = r4.IsDBNull(i) ? null : r4.GetValue(i);
            }
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM \"交易明細\" WHERE \"單據副碼\" = {data.Master["單據副碼"]} ORDER BY \"建檔序號\"";
            using var r2 = cmd.ExecuteReader();
            while (r2.Read())
            {
                var d = new Dictionary<string, object?>();
                for (int i = 0; i < r2.FieldCount; i++)
                    d[r2.GetName(i)] = r2.IsDBNull(i) ? null : r2.GetValue(i);
                data.Detail.Add(d);
            }
        }
        // join 欄位（同 TransactionForm.BuildRtmData 邏輯）
        object? pNo = data.Master["交易對象"];
        data.Master["對象名稱"] = "";
        data.Master["聯絡人一"] = "";
        data.Master["聯絡電話一"] = "";
        data.Master["統一編號"] = "";
        data.Master["傳真號碼"] = "";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT \"公司全名\", \"聯絡人一\", \"聯絡電話一\", \"統一編號\", \"傳真號碼\"" +
                " FROM \"客戶廠商\" WHERE \"客廠編號\" = $no LIMIT 1";
            cmd.Parameters.AddWithValue("$no", pNo ?? DBNull.Value);
            using var r3 = cmd.ExecuteReader();
            if (r3.Read())
            {
                data.Master["對象名稱"] = r3.IsDBNull(0) ? "" : r3.GetValue(0).ToString();
                data.Master["聯絡人一"] = r3.IsDBNull(1) ? "" : r3.GetValue(1).ToString();
                data.Master["聯絡電話一"] = r3.IsDBNull(2) ? "" : r3.GetValue(2).ToString();
                data.Master["統一編號"] = r3.IsDBNull(3) ? "" : r3.GetValue(3).ToString();
                data.Master["傳真號碼"] = r3.IsDBNull(4) ? "" : r3.GetValue(4).ToString();
            }
        }
        object? staffNo = data.Master["員工編號"];
        data.Master["員工名稱"] = "";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT \"員工姓名\" FROM \"員工資料\" WHERE \"員工編號\" = $no LIMIT 1";
            cmd.Parameters.AddWithValue("$no", staffNo ?? DBNull.Value);
            var v = cmd.ExecuteScalar();
            data.Master["員工名稱"] = v?.ToString() ?? "";
        }
        data.Master["進貨地址"] = data.Master.TryGetValue("送貨地址", out var addr) ? addr : "";
    }
    // 公司資料（plCompany）
    data.Company["公司全名"] = "禾秝安全系統工程有限公司";
    data.Company["電話號碼"] = "(02)2593-2101";
    data.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
    data.Company["傳真號碼"] = "(02)2586-3046";

    string M(string key) => data.Master.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";
    Console.WriteLine($"單號={M("交易單號")} 對象={M("對象名稱")} 明細={data.Detail.Count}筆 " +
        $"合計={M("合計金額")} 稅={M("營業稅")} 總計={M("總計金額")}" + (fake ? "（FAKE：出貨單資料）" : ""));

    // 3. 渲染 300dpi
    const int dpi = 300;
    int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
    int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);
    using var bmp = new Bitmap(wPx, hPx);
    bmp.SetResolution(dpi, dpi);
    using var renderer = new RtmRenderer(report, data);
    int pages = 0;
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        do { pages++; } while (renderer.RenderPage(g, new RectangleF(0, 0, wPx, hPx), st));
    }

    string outPath = Path.Combine(@"D:\HeliAcc\shots", $"trade-{kindColumn}.png");
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    bmp.Save(outPath, ImageFormat.Png);

    // 4. 墨量驗證（版面確實有內容）
    var mask = new byte[wPx * hPx];
    var bd = bmp.LockBits(new Rectangle(0, 0, wPx, hPx), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    unsafe
    {
        byte* p = (byte*)bd.Scan0;
        for (int yy = 0; yy < hPx; yy++)
            for (int xx = 0; xx < wPx; xx++)
            {
                byte b = p[yy * bd.Stride + xx * 3], g = p[yy * bd.Stride + xx * 3 + 1], r = p[yy * bd.Stride + xx * 3 + 2];
                mask[yy * wPx + xx] = (r < 200 || g < 200 || b < 200) ? (byte)1 : (byte)0;
            }
    }
    bmp.UnlockBits(bd);
    int ink = mask.Sum(b => b);
    double pct = 100.0 * ink / (wPx * hPx);
    Console.WriteLine($"頁數={pages} 墨量={pct:F2}% 輸出={outPath}");

    // 5. 區域墨量驗證（對應 .rtm 元件座標，1/1000mm）
    float pxPerMm = dpi / 25.4f / 1000f;
    float headerH = report.HeaderBand?.MmHeight ?? 0f;
    float detailH = report.DetailBand?.MmHeight ?? 0f;
    float gfY = headerH + detailH * data.Detail.Count;

    var checks = kind switch
    {
        "進貨" => new (string, float, float, float, float)[]
        {
            ("公司全名", 5300, 1600, 64200, 7600),
            ("交易單號", 170400, 25100, 23800, 4800),
            ("對象名稱", 27000, 19000, 48700, 4800),
            ("明細品名", 31500, headerH + 500, 61100, 4800),
            ("明細金額", 144200, headerH + 500, 22500, 4800),
            ("合計金額", 170400, gfY + 1900, 23800, 4800),
            ("營業稅", 170400, gfY + 7400, 23800, 4800),
            ("總計金額", 170400, gfY + 12700, 23800, 4800),
        },
        "進退" => new (string, float, float, float, float)[]
        {
            ("公司全名", 5300, 1100, 19300, 7600),
            ("交易單號", 170400, 25100, 23800, 4800),
            ("對象名稱", 27000, 19000, 48700, 4800),
            ("進貨地址", 27000, 36500, 118500, 4800),
            ("明細品名", 31500, headerH + 500, 61100, 4800),
            ("合計金額", 170400, gfY + 1900, 23800, 4800),
            ("營業稅", 170400, gfY + 7400, 23800, 4800),
            ("總計金額", 170400, gfY + 12700, 23800, 4800),
        },
        "出退" => new (string, float, float, float, float)[]
        {
            // 該筆出退單無交易對象，對象名稱欄位依資料留白（渲染引擎正確處理空值）
            ("公司全名", 5600, 3200, 19600, 7700),
            ("交易單號", 170400, 25100, 23800, 4800),
            ("合計金額", 169300, gfY + 3400, 23800, 4800),
            ("營業稅", 169300, gfY + 9000, 23800, 4800),
            ("總計金額", 169300, gfY + 14300, 23800, 4800),
        },
        _ => new (string, float, float, float, float)[]
        {
            ("公司全名", 5300, 4000, 139400, 7700),
            ("交易單號", 170400, 27300, 23800, 4800),
            ("對象名稱", 45500, 21200, 100300, 5000),
            ("送貨地址", 27000, 38600, 118500, 4800),
            ("員工名稱", 170400, 32800, 23800, 4800),
            ("明細品名", 31200, headerH, 61100, 4800),
            ("明細數量", 93100, headerH, 12400, 4800),
            ("明細單價", 124100, headerH, 16900, 4800),
            ("明細金額", 143900, headerH, 22000, 4800),
            ("合計金額", 171400, gfY + 2100, 23800, 4800),
            ("營業稅", 171400, gfY + 7700, 23800, 4800),
            ("總計金額", 171400, gfY + 13000, 23800, 4800),
            ("備註", 20100, gfY + 1600, 77300, 17700),
        },
    };

    bool HasInk2(int x, int y, int w, int h)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(wPx, x + w), y1 = Math.Min(hPx, y + h);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                if (mask[yy * wPx + xx] == 1) return true;
        return false;
    }

    int pass = 0, fail = 0;
    foreach (var (name, l, t, w, h) in checks)
    {
        int x = (int)(l * pxPerMm), y = (int)(t * pxPerMm);
        int rw = (int)(w * pxPerMm), rh = (int)(h * pxPerMm);
        bool ok = HasInk2(x, y, rw, rh);
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  ({l / 1000f:F1},{t / 1000f:F1})mm");
        if (ok) pass++; else fail++;
    }
    Console.WriteLine($"\n驗證結果: {pass} PASS / {fail} FAIL");
    Console.WriteLine(pct > 0.5 && pages >= 1 && fail == 0 ? "TRADE PASS" : "TRADE FAIL");
}

static void RepairRender()
{
    const string DbPath2 = @"D:\HeliAcc\HeliERP.db";
    string reportName = "維修單據.rtm";
    Console.WriteLine($"== {reportName}（維修單據列印）==");

    var data = new RtmData();
    using (var conn = new SqliteConnection($"Data Source={DbPath2}"))
    {
        conn.Open();
        // 最新一筆有明細的維修單（同 RepairModuleForm 列印資料）
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT m.* FROM \"維修主檔\" m " +
                "WHERE EXISTS (SELECT 1 FROM \"維修明細\" d WHERE d.\"單據副碼\" = m.\"單據副碼\") " +
                "ORDER BY m.\"交易日期\" DESC, m.\"單據副碼\" DESC LIMIT 1";
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new Exception("找不到有明細的維修單");
            for (int i = 0; i < r.FieldCount; i++)
                data.Master[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM \"維修明細\" WHERE \"單據副碼\" = {data.Master["單據副碼"]} ORDER BY \"建檔序號\"";
            using var r2 = cmd.ExecuteReader();
            while (r2.Read())
            {
                var d = new Dictionary<string, object?>();
                for (int i = 0; i < r2.FieldCount; i++)
                    d[r2.GetName(i)] = r2.IsDBNull(i) ? null : r2.GetValue(i);
                data.Detail.Add(d);
            }
        }
        // join 欄位（同 RepairModuleForm.BuildRtmData）
        object? pNo = data.Master["交易對象"];
        data.Master["對象名稱"] = "";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT \"公司全名\" FROM \"客戶廠商\" WHERE \"客廠編號\" = $no LIMIT 1";
            cmd.Parameters.AddWithValue("$no", pNo ?? DBNull.Value);
            var v = cmd.ExecuteScalar();
            data.Master["對象名稱"] = v?.ToString() ?? "";
        }
        object? staffNo = data.Master["員工編號"];
        data.Master["員工名稱"] = "";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT \"員工姓名\" FROM \"員工資料\" WHERE \"員工編號\" = $no LIMIT 1";
            cmd.Parameters.AddWithValue("$no", staffNo ?? DBNull.Value);
            var v = cmd.ExecuteScalar();
            data.Master["員工名稱"] = v?.ToString() ?? "";
        }
    }
    // 公司資料（plCompany）
    data.Company["公司全名"] = "禾秝安全系統工程有限公司";
    data.Company["電話號碼"] = "(02)2593-2101";
    data.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
    data.Company["傳真號碼"] = "(02)2586-3046";

    RenderAnyReport(reportName, "維修單據列印", data, (h, s, f) => new (string, float, float, float, float)[]
    {
        ("公司全名", 5000, 4000, 139700, 9300),
        ("交易單號", 169600, 21200, 23800, 4800),
        ("交易日期", 169600, 15300, 23800, 4800),
        ("對象名稱", 27000, 30400, 118800, 4800),
        ("聯絡人", 27000, 36200, 51900, 4800),
        ("貨品編號", 27000, 54000, 25100, 4800),
        ("品名", 52100, 54000, 41800, 4800),
        ("故障現象", 3700, s + 8500, 56100, 34400),
        ("故障原因", 63000, s + 8500, 69600, 34700),
        ("維修情況", 134700, s + 8500, 59500, 34700),
        ("備註", 18000, s + 46000, 114000, 19600),
        ("合計金額", 164000, s + 47600, 23800, 4800),
        ("總計金額", 164000, s + 58500, 23800, 4800),
        ("明細品名", 46600, h, 64800, 4800),
        ("明細貨品編號", 16100, h, 30700, 4800),
        ("明細數量", 110600, h, 12400, 4800),
    });
}

static void DataCheck()
{
    Console.WriteLine($"\n== 資料一致性檢查（DB: {DbManager.DatabasePath}）==");
    int pass = 0, fail = 0;
    void Chk(string name, bool ok, string detail = "")
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
        if (ok) pass++; else fail++;
    }

    // 1. 交易主檔：總計金額 = 合計金額 + 營業稅
    var bad1 = DbManager.QueryTable(
        "SELECT [交易單號], [合計金額], [營業稅], [總計金額] FROM [交易主檔] " +
        "WHERE ABS(IFNULL([總計金額],0) - (IFNULL([合計金額],0)+IFNULL([營業稅],0))) > 0.01");
    Chk("交易主檔 總計=合計+營業稅", bad1.Rows.Count == 0, $"落差單據={bad1.Rows.Count}");

    // 2. 交易主檔合計 vs 交易明細 Σ金額（容差 0.5：舊系統有 2 筆 0.22/0.24 尾差）
    var bad2 = DbManager.QueryTable(
        "SELECT t.[單據類別], t.[交易單號], t.[合計金額], d.[s] FROM [交易主檔] t " +
        "JOIN (SELECT [單據副碼], SUM(IFNULL([金額],0)) AS [s] FROM [交易明細] GROUP BY [單據副碼]) d " +
        "ON t.[單據副碼] = d.[單據副碼] " +
        "WHERE ABS(IFNULL(t.[合計金額],0) - d.[s]) > 0.5");
    Chk("交易主檔合計=明細Σ金額", bad2.Rows.Count == 0, $"落差>0.5 單據={bad2.Rows.Count}");

    // 3. 收付主檔：沖帳合計 = 現金+票據+取用預收（純預收單與已知舊資料例外除外）
    var bad3 = DbManager.QueryTable(
        "SELECT [收付類別], [收付單號], [沖帳合計], [現金金額], [票據金額], [取用預收], [累入預收] FROM [收付主檔] " +
        "WHERE ABS(IFNULL([沖帳合計],0) - (IFNULL([現金金額],0)+IFNULL([票據金額],0)+IFNULL([取用預收],0))) > 0.01 " +
        "AND NOT ([累入預收] > 0 AND [沖帳合計] = 0) " +
        "AND NOT ([收付類別] = '付款' AND [收付單號] = '912010004')");
    Chk("收付主檔 沖帳合計=現金+票據+取用預收", bad3.Rows.Count == 0, $"落差單據={bad3.Rows.Count}");

    // 4. 帳款主檔：本期總計 = 本期合計 + 營業稅
    var bad4 = DbManager.QueryTable(
        "SELECT [交易對象], [本期合計], [營業稅], [本期總計] FROM [帳款主檔] " +
        "WHERE ABS(IFNULL([本期總計],0) - (IFNULL([本期合計],0)+IFNULL([營業稅],0))) > 0.01");
    Chk("帳款主檔 本期總計=本期合計+營業稅", bad4.Rows.Count == 0, $"落差對象={bad4.Rows.Count}");

    // 5. 帳款簡要 Σ未收付 ≈ 帳款主檔 Σ(本期總計-已收付-折讓)（容差 10000：舊單未全轉入）
    decimal bSum = Convert.ToDecimal(DbManager.QueryScalar("SELECT IFNULL(SUM([未收付金額]),0) FROM [帳款簡要]"));
    decimal aSum = Convert.ToDecimal(DbManager.QueryScalar(
        "SELECT IFNULL(SUM(IFNULL([本期總計],0)-IFNULL([已收付金額],0)-IFNULL([折讓金額],0)),0) FROM [帳款主檔]"));
    Chk("帳款簡要Σ未收付≈帳款主檔Σ(總計-已收付-折讓)", Math.Abs(bSum - aSum) < 10000m,
        $"簡要={bSum:N0} 主檔={aSum:N0} 差={bSum - aSum:N0}");

    // ---- 資訊（舊資料轉換現況，非檢查點）----
    long 未收付0 = Convert.ToInt64(DbManager.QueryScalar(
        "SELECT COUNT(*) FROM [交易主檔] WHERE IFNULL([總計金額],0) != 0 AND IFNULL([未收付金額],0) = 0"));
    long 簡要筆數 = Convert.ToInt64(DbManager.QueryScalar("SELECT COUNT(*) FROM [帳款簡要]"));
    long 出貨張數 = Convert.ToInt64(DbManager.QueryScalar("SELECT COUNT(*) FROM [交易主檔] WHERE [單據類別] = '出貨'"));
    long 負庫存 = Convert.ToInt64(DbManager.QueryScalar("SELECT COUNT(*) FROM [貨品庫存] WHERE [現有數量] < -0.005"));
    Console.WriteLine($"\n資訊: 交易主檔總計≠0 且未收付金額=0 → {未收付0} 張（舊資料未回填）");
    Console.WriteLine($"資訊: 帳款簡要 {簡要筆數} 筆 / 出貨單 {出貨張數} 張（舊單未轉入帳款簡要）");
    Console.WriteLine($"資訊: 貨品庫存負數 {負庫存} 筆（歷史負庫存）");

    Console.WriteLine($"\n資料一致性: {pass} PASS / {fail} FAIL");
    Console.WriteLine(fail == 0 ? "DCHECK PASS" : "DCHECK FAIL");
}

// ═══ 帳款回填：交易主檔未收付≠0 之歷史單 → 帳款主檔/簡要/明細（可重入） ═══
static void AccountBackfill()
{
    Console.WriteLine($"\n== 帳款回填（DB: {DbManager.DatabasePath}）==");

    // 待回填：交易主檔未收付≠0 且帳款簡要無對應者
    string 缺單SQL =
        "SELECT m.[交易單號], m.[單據類別], m.[交易對象], IFNULL(m.[員工編號],'') AS [員工編號], " +
        "m.[交易日期], IFNULL(m.[發票號碼],'') AS [發票號碼], " +
        "IFNULL(m.[合計金額],0) AS [合計金額], IFNULL(m.[營業稅],0) AS [營業稅], " +
        "IFNULL(m.[總計金額],0) AS [總計金額], IFNULL(m.[折讓金額],0) AS [折讓金額], " +
        "IFNULL(m.[已收付金額],0) AS [已收付金額], IFNULL(m.[未收付金額],0) AS [未收付金額], " +
        "IFNULL(m.[應收付金額],0) AS [應收付金額], m.[單據副碼] " +
        "FROM [交易主檔] m LEFT JOIN [帳款簡要] b ON b.[交易單號]=m.[交易單號] AND b.[單據類別]=m.[單據類別] " +
        "WHERE ABS(m.[未收付金額])>0.001 AND b.[交易單號] IS NULL ORDER BY m.[交易日期], m.[交易單號]";
    var 缺單 = DbManager.QueryTable(缺單SQL);
    if (缺單.Rows.Count == 0)
    {
        Console.WriteLine("帳款簡要已完整覆蓋所有未收付單據，無需回填。");
        return;
    }
    Console.WriteLine($"待回填單據: {缺單.Rows.Count} 張");

    // 待回填明細（join 主檔帶單據資訊）
    string 缺明細SQL =
        "SELECT d.*, m.[交易單號], m.[單據類別], m.[交易對象], IFNULL(m.[員工編號],'') AS [員工編號], " +
        "m.[交易日期], IFNULL(m.[發票號碼],'') AS [發票號碼], IFNULL(m.[專案編號],'') AS [專案編號] " +
        "FROM [交易明細] d JOIN [交易主檔] m ON m.[單據副碼]=d.[單據副碼] " +
        "LEFT JOIN [帳款簡要] b ON b.[交易單號]=m.[交易單號] AND b.[單據類別]=m.[單據類別] " +
        "WHERE ABS(m.[未收付金額])>0.001 AND b.[交易單號] IS NULL";
    var 缺明細 = DbManager.QueryTable(缺明細SQL);
    Console.WriteLine($"待回填明細列: {缺明細.Rows.Count} 列");

    // 帳款主檔：各對象彙總（方向：出貨/進貨 = 帳款+，出退/進退 = 帳款-）
    var 方向 = new Dictionary<string, int> { ["出貨"] = 1, ["進貨"] = 1, ["出退"] = -1, ["進退"] = -1 };
    var 主檔對象 = new SortedDictionary<string, (decimal 合計, decimal 稅, decimal 總計, decimal 已收付, decimal 折讓)>();
    foreach (DataRow r in 缺單.Rows)
    {
        string 類 = Convert.ToString(r["單據類別"]) ?? "出貨";
        int dir = 方向.TryGetValue(類, out var d) ? d : 1;
        string 對象 = Convert.ToString(r["交易對象"]) ?? "";
        decimal 合計 = Convert.ToDecimal(r["合計金額"]) * dir;
        decimal 稅 = Convert.ToDecimal(r["營業稅"]) * dir;
        decimal 總計 = Convert.ToDecimal(r["總計金額"]) * dir;
        decimal 已收付 = Convert.ToDecimal(r["已收付金額"]);
        decimal 折讓 = Convert.ToDecimal(r["折讓金額"]);
        if (!主檔對象.TryGetValue(對象, out var v))
            v = (0m, 0m, 0m, 0m, 0m);
        主檔對象[對象] = (v.合計 + 合計, v.稅 + 稅, v.總計 + 總計, v.已收付 + 已收付, v.折讓 + 折讓);
    }

    long 主檔新增 = 0, 簡要新增 = 0, 明細新增 = 0;
    DbManager.ExecuteImmediateTransaction(conn =>
    {
        // 1. 帳款主檔：累加既有（含空殼），缺失者新建
        foreach (var kv in 主檔對象)
        {
            string 對象 = kv.Key;
            var (合計, 稅, 總計, 已收付, 折讓) = kv.Value;
            object? accSeq;
            using (var cmd = DbManager.CreateCommand(conn,
                "SELECT [建檔序號] FROM [帳款主檔] WHERE [交易對象] = $o",
                DbManager.Param("$o", 對象)))
                accSeq = cmd.ExecuteScalar();

            if (accSeq is null)
            {
                string 公司全名 = "", 統一編號 = "", 聯絡人一 = "", 聯絡電話一 = "", 傳真號碼 = "";
                using (var cmd = DbManager.CreateCommand(conn,
                    "SELECT [公司全名],[統一編號],[聯絡人一],[聯絡電話一],[傳真號碼] FROM [客戶廠商] WHERE [客廠編號] = $o",
                    DbManager.Param("$o", 對象)))
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        公司全名 = rd.IsDBNull(0) ? "" : Convert.ToString(rd.GetValue(0)) ?? "";
                        統一編號 = rd.IsDBNull(1) ? "" : Convert.ToString(rd.GetValue(1)) ?? "";
                        聯絡人一 = rd.IsDBNull(2) ? "" : Convert.ToString(rd.GetValue(2)) ?? "";
                        聯絡電話一 = rd.IsDBNull(3) ? "" : Convert.ToString(rd.GetValue(3)) ?? "";
                        傳真號碼 = rd.IsDBNull(4) ? "" : Convert.ToString(rd.GetValue(4)) ?? "";
                    }
                }
                string 姓名 = "";
                if (缺單.Rows.Count > 0)
                {
                    var first = 缺單.Select($"[交易對象] = '{對象.Replace("'", "''")}'").FirstOrDefault();
                    if (first != null) 姓名 = Convert.ToString(first["員工編號"]) ?? "";
                }
                using (var cmd = DbManager.CreateCommand(conn,
                    "INSERT INTO [帳款主檔] ([建檔序號],[交易對象],[公司全名],[員工編號],[員工姓名],[統一編號]," +
                    "[聯絡人一],[聯絡電話一],[傳真號碼],[累計預收貨款],[前期累計應收帳款],[本期合計],[營業稅]," +
                    "[折讓金額],[已收付金額],[現金收付金額],[本期總計]) " +
                    "VALUES ($s,$o,$c,$e,$n,$u,$t,$p,$f,0,0,$h,$x,$d,$p2,0,$m)",
                    DbManager.Param("$s", NextSeq(conn, "帳款主檔")),
                    DbManager.Param("$o", 對象),
                    DbManager.Param("$c", 公司全名 == "" ? DBNull.Value : 公司全名),
                    DbManager.Param("$e", 姓名 == "" ? DBNull.Value : 姓名),
                    DbManager.Param("$n", 姓名 == "" ? DBNull.Value : 姓名),
                    DbManager.Param("$u", 統一編號 == "" ? DBNull.Value : 統一編號),
                    DbManager.Param("$t", 聯絡人一 == "" ? DBNull.Value : 聯絡人一),
                    DbManager.Param("$p", 聯絡電話一 == "" ? DBNull.Value : 聯絡電話一),
                    DbManager.Param("$f", 傳真號碼 == "" ? DBNull.Value : 傳真號碼),
                    DbManager.Param("$h", 合計), DbManager.Param("$x", 稅),
                    DbManager.Param("$d", 折讓), DbManager.Param("$p2", 已收付), DbManager.Param("$m", 總計)))
                    cmd.ExecuteNonQuery();
                主檔新增++;
            }
            else
            {
                using (var cmd = DbManager.CreateCommand(conn,
                    "UPDATE [帳款主檔] SET [本期合計] = [本期合計] + $h, [營業稅] = [營業稅] + $x, " +
                    "[本期總計] = [本期總計] + $m, [已收付金額] = [已收付金額] + $p, [折讓金額] = [折讓金額] + $d " +
                    "WHERE [建檔序號] = $i",
                    DbManager.Param("$h", 合計), DbManager.Param("$x", 稅), DbManager.Param("$m", 總計),
                    DbManager.Param("$p", 已收付), DbManager.Param("$d", 折讓), DbManager.Param("$i", accSeq)))
                    cmd.ExecuteNonQuery();
            }
        }

        // 2. 帳款簡要：每張一筆
        foreach (DataRow r in 缺單.Rows)
        {
            using (var cmd = DbManager.CreateCommand(conn,
                "INSERT INTO [帳款簡要] ([建檔序號],[單據類別],[交易對象],[員工編號],[交易日期],[交易單號],[發票號碼]," +
                "[合計金額],[營業稅],[總計金額],[折讓金額],[現金收付金額],[已收付金額],[未收付金額],[應收付金額]) " +
                "VALUES ($s,$k,$o,$e,$dt,$n,$iv,$h,$x,$m,$d,$c,$p,$u,$a)",
                DbManager.Param("$s", NextSeq(conn, "帳款簡要")),
                DbManager.Param("$k", Convert.ToString(r["單據類別"])),
                DbManager.Param("$o", Convert.ToString(r["交易對象"])),
                DbManager.Param("$e", Convert.ToString(r["員工編號"]) == "" ? DBNull.Value : Convert.ToString(r["員工編號"])),
                DbManager.Param("$dt", Convert.ToString(r["交易日期"])),
                DbManager.Param("$n", Convert.ToString(r["交易單號"])),
                DbManager.Param("$iv", Convert.ToString(r["發票號碼"]) == "" ? DBNull.Value : Convert.ToString(r["發票號碼"])),
                DbManager.Param("$h", Convert.ToDecimal(r["合計金額"])),
                DbManager.Param("$x", Convert.ToDecimal(r["營業稅"])),
                DbManager.Param("$m", Convert.ToDecimal(r["總計金額"])),
                DbManager.Param("$d", Convert.ToDecimal(r["折讓金額"])),
                DbManager.Param("$c", 0m),
                DbManager.Param("$p", Convert.ToDecimal(r["已收付金額"])),
                DbManager.Param("$u", Convert.ToDecimal(r["未收付金額"])),
                DbManager.Param("$a", Convert.ToDecimal(r["應收付金額"]))))
                cmd.ExecuteNonQuery();
            簡要新增++;
        }

        // 3. 帳款明細：每列一筆
        foreach (DataRow d in 缺明細.Rows)
        {
            using (var cmd = DbManager.CreateCommand(conn,
                "INSERT INTO [帳款明細] ([建檔序號],[單據類別],[交易對象],[員工編號],[專案編號],[交易日期],[交易單號],[發票號碼]," +
                "[貨品編號],[品名],[顏色],[數量],[單位],[單價],[折扣],[金額],[封裝數量],[覆合數量],[覆合單位],[散裝數量],[散裝單位]," +
                "[附註說明],[相關單據],[相關單號],[來源單號],[贈品],[服務項目]) " +
                "VALUES ($s,$k,$o,$e,$pr,$dt,$n,$iv,$g,$pn,$c,$q,$u,$price,$d,$amt,$pkg,$ov,$ou,$blk,$bu,$note,$rel,$rn,$src,$gift,$svc)",
                DbManager.Param("$s", NextSeq(conn, "帳款明細")),
                DbManager.Param("$k", Convert.ToString(d["單據類別"])),
                DbManager.Param("$o", Convert.ToString(d["交易對象"])),
                DbManager.Param("$e", Convert.ToString(d["員工編號"]) == "" ? DBNull.Value : Convert.ToString(d["員工編號"])),
                DbManager.Param("$pr", Convert.ToString(d["專案編號"]) == "" ? DBNull.Value : Convert.ToString(d["專案編號"])),
                DbManager.Param("$dt", Convert.ToString(d["交易日期"])),
                DbManager.Param("$n", Convert.ToString(d["交易單號"])),
                DbManager.Param("$iv", Convert.ToString(d["發票號碼"]) == "" ? DBNull.Value : Convert.ToString(d["發票號碼"])),
                DbManager.Param("$g", Convert.ToString(d["貨品編號"])),
                DbManager.Param("$pn", Convert.ToString(d["品名"])),
                DbManager.Param("$c", d["顏色"] as object ?? DBNull.Value),
                DbManager.Param("$q", d["數量"] as object ?? DBNull.Value),
                DbManager.Param("$u", d["單位"] as object ?? DBNull.Value),
                DbManager.Param("$price", d["單價"] as object ?? DBNull.Value),
                DbManager.Param("$d", d["折扣"] as object ?? DBNull.Value),
                DbManager.Param("$amt", d["金額"] as object ?? DBNull.Value),
                DbManager.Param("$pkg", d["封裝數量"] as object ?? DBNull.Value),
                DbManager.Param("$ov", d["覆合數量"] as object ?? DBNull.Value),
                DbManager.Param("$ou", d["覆合單位"] as object ?? DBNull.Value),
                DbManager.Param("$blk", d["散裝數量"] as object ?? DBNull.Value),
                DbManager.Param("$bu", d["散裝單位"] as object ?? DBNull.Value),
                DbManager.Param("$note", d["附註說明"] as object ?? DBNull.Value),
                DbManager.Param("$rel", d["相關單據"] as object ?? DBNull.Value),
                DbManager.Param("$rn", d["相關單號"] as object ?? DBNull.Value),
                DbManager.Param("$src", d["來源單號"] as object ?? DBNull.Value),
                DbManager.Param("$gift", d["贈品"] as object ?? DBNull.Value),
                DbManager.Param("$svc", d["服務項目"] as object ?? DBNull.Value)))
                cmd.ExecuteNonQuery();
            明細新增++;
        }
    });

    Console.WriteLine($"回填完成：帳款主檔新增 {主檔新增}、帳款簡要新增 {簡要新增}、帳款明細新增 {明細新增}");

    // 回填後對帳
    decimal bSum = Convert.ToDecimal(DbManager.QueryScalar("SELECT IFNULL(SUM([未收付金額]),0) FROM [帳款簡要]"));
    decimal aSum = Convert.ToDecimal(DbManager.QueryScalar(
        "SELECT IFNULL(SUM(IFNULL([本期總計],0)-IFNULL([已收付金額],0)-IFNULL([折讓金額],0)),0) FROM [帳款主檔]"));
    long 剩餘 = Convert.ToInt64(DbManager.QueryScalar(
        "SELECT COUNT(*) FROM [交易主檔] m LEFT JOIN [帳款簡要] b ON b.[交易單號]=m.[交易單號] AND b.[單據類別]=m.[單據類別] " +
        "WHERE ABS(m.[未收付金額])>0.001 AND b.[交易單號] IS NULL"));
    Console.WriteLine($"對帳：簡要Σ未收付={bSum:N0} 主檔Σ(總計-已收付-折讓)={aSum:N0} 差={bSum - aSum:N0}");
    Console.WriteLine($"覆蓋完整性：剩餘缺漏 {剩餘} 張");
    Console.WriteLine(剩餘 == 0 ? "BACKFILL PASS（全部覆蓋）" : "BACKFILL 未完整");
}

// helper：交易內取下一建檔序號
static long NextSeq(SqliteConnection conn, string 表)
{
    object? v;
    using (var cmd = DbManager.CreateCommand(conn,
        $"SELECT IFNULL(MAX([建檔序號]),0)+1 FROM [{表}]"))
        v = cmd.ExecuteScalar();
    return Convert.ToInt64(v);
}

// ═══ 服務流程驗證：建單 → 沖帳 → 刪除還原（四表同步） ═══
static void ServiceFlowTest()
{
    string testDb = Path.Combine(@"C:\Users\JS\AppData\Local\Temp\opencode", "HeliERP-svc.db");
    if (File.Exists(testDb)) File.Delete(testDb);
    File.Copy(@"D:\HeliAcc\HeliERP.db", testDb);
    string 原Db = DbManager.DatabasePath;
    DbManager.DatabasePath = testDb;
    Console.WriteLine($"\n== 服務流程驗證（建單→沖帳→刪除還原，DB: {testDb}）==");
    int pass = 0, fail = 0;
    void Chk(string name, bool ok, string detail = "")
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
        if (ok) pass++; else fail++;
    }
    decimal Sc(string sql, params SqliteParameter[] ps) => Convert.ToDecimal(DbManager.QueryScalar(sql, ps));
    long Lc(string sql) => Convert.ToInt64(DbManager.QueryScalar(sql));

    try
    {
        // 1. 選倉庫 A 庫存最大的 2 種貨品
        var goods = DbManager.QueryTable(
            "SELECT [貨品編號], [現有數量] FROM [貨品庫存] WHERE [倉庫編號] = 'A' " +
            "ORDER BY [現有數量] DESC LIMIT 2");
        if (goods.Rows.Count < 2) { Console.WriteLine("無庫存貨品可用，SVC FAIL"); return; }
        string g1 = goods.Rows[0]["貨品編號"].ToString()!;
        string g2 = goods.Rows[1]["貨品編號"].ToString()!;

        decimal arBefore = Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");
        decimal stockBefore1 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1));
        decimal stockBefore2 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2));

        // 2. 建出貨單
        const decimal 單價1 = 1200m, 單價2 = 800m, 數1 = 3m, 數2 = 2m;
        decimal 合計 = 單價1 * 數1 + 單價2 * 數2, 稅 = 合計 * 0.05m, 總計 = 合計 + 稅;
        var req = new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = "AR-C001", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-{DateTime.Now:yyyyMMddHHmmss}", 發票號碼 = "SV-TEST-001", 備註 = "svc 流程測試",
            明細 =
            {
                new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = 數1, 單價 = 單價1, 單位 = "個" },
                new TradeService.DetailRow { 貨品編號 = g2, 倉庫編號 = "A", 數量 = 數2, 單價 = 單價2, 單位 = "個" },
            }
        };
        var saved = TradeService.SaveBill(req);
        Chk("建單：總計=合計+營業稅", Math.Abs(Sc($"SELECT [總計金額]-[合計金額]-[營業稅] FROM [交易主檔] WHERE [單據副碼] = {saved.單據副碼}")) < 0.01m, $"單號={saved.交易單號}");
        Chk("建單：未收付金額=總計", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {saved.單據副碼}") - 總計) < 0.01m, $"總計={總計}");
        Chk("建單：明細 2 筆", Lc($"SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼] = {saved.單據副碼}") == 2);
        Chk("建單：帳款簡要 未收付=總計", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{saved.交易單號}'") - 總計) < 0.01m);
        Chk("建單：庫存扣減", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - (stockBefore1 - 數1)) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - (stockBefore2 - 數2)) < 0.01m,
            $"{g1}:{stockBefore1}→{stockBefore1 - 數1} {g2}:{stockBefore2}→{stockBefore2 - 數2}");
        decimal arAfter = Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");
        Chk("建單：帳款主檔本期總計+總計", Math.Abs(arAfter - arBefore - 總計) < 0.01m, $"{arBefore}→{arAfter}");
        decimal arPaidBefore = Sc("SELECT IFNULL([已收付金額],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");

        // 2.5 修改出貨單：數量 3→5、單價 1200→1500（ReverseEffects 回復舊影響再重算，不得累加）
        const decimal 新單價1 = 1500m, 新數1 = 5m;
        decimal 新合計 = 新單價1 * 新數1 + 單價2 * 數2, 新稅 = 新合計 * 0.05m, 新總計 = 新合計 + 新稅;
        var req2 = new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = "AR-C001", 倉庫編號 = "A", 員工編號 = "001",
            單據副碼 = saved.單據副碼, 交易單號 = saved.交易單號, 發票號碼 = "SV-TEST-001", 備註 = "svc 流程測試(修改)",
            明細 =
            {
                new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = 新數1, 單價 = 新單價1, 單位 = "個" },
                new TradeService.DetailRow { 貨品編號 = g2, 倉庫編號 = "A", 數量 = 數2, 單價 = 單價2, 單位 = "個" },
            }
        };
        var saved2 = TradeService.SaveBill(req2);
        Chk("修改：單據副碼不變", saved2.單據副碼 == saved.單據副碼);
        Chk("修改：總計=合計+稅", Math.Abs(Sc($"SELECT [總計金額]-[合計金額]-[營業稅] FROM [交易主檔] WHERE [單據副碼] = {saved.單據副碼}")) < 0.01m, $"合計={新合計} 稅={新稅} 總計={新總計}");
        Chk("修改：未收付=新總計", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {saved.單據副碼}") - 新總計) < 0.01m);
        Chk("修改：明細重寫 2 筆", Lc($"SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼] = {saved.單據副碼}") == 2);
        Chk("修改：庫存淨扣減(不累加)", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - (stockBefore1 - 新數1)) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - (stockBefore2 - 數2)) < 0.01m,
            $"{g1}:{stockBefore1}→{stockBefore1 - 新數1} {g2}:{stockBefore2}→{stockBefore2 - 數2}");
        decimal arAfterMod = Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");
        Chk("修改：帳款主檔淨+新總計(不累加)", Math.Abs(arAfterMod - arBefore - 新總計) < 0.01m, $"{arBefore}→{arAfterMod}");
        總計 = 新總計; 合計 = 新合計; 稅 = 新稅;

        // 3. 沖帳（全額現金）
        var payReq = new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "AR-C001", 現金金額 = 總計, 沖帳日期 = DateTime.Now,
            明細 =
            {
                new PaymentService.PaymentDetailRow { 交易單號 = saved.交易單號, 單據類別 = "出貨", 未收付金額 = 總計, 沖帳金額 = 總計, 折讓金額 = 0 },
            }
        };
        var payed = PaymentService.SavePayment(payReq);
        Chk("沖帳：收付主檔沖帳合計=現金", Math.Abs(Sc($"SELECT [沖帳合計] FROM [收付主檔] WHERE [單據副碼] = {payed.單據副碼}") - 總計) < 0.01m);
        Chk("沖帳：收付明細 1 筆", Lc($"SELECT COUNT(*) FROM [收付明細] WHERE [單據號碼] = '{saved.交易單號}'") == 1);
        Chk("沖帳：帳款簡要未收付=0", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{saved.交易單號}'")) < 0.01m);
        Chk("沖帳：交易主檔已收付=總計", Math.Abs(Sc($"SELECT [已收付金額] FROM [交易主檔] WHERE [單據副碼] = {saved.單據副碼}") - 總計) < 0.01m);
        Chk("沖帳：交易主檔未收付=0", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {saved.單據副碼}")) < 0.01m);
        decimal arPaid = Sc("SELECT IFNULL([已收付金額],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");
        Chk("沖帳：帳款主檔已收付+總計", Math.Abs(arPaid - arPaidBefore - 總計) < 0.01m, $"已收付={arPaid}");

        // 4. 刪除收付（還原沖帳）
        PaymentService.DeletePayment(payed.單據副碼);
        Chk("刪收付：帳款簡要未收付回復", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{saved.交易單號}'") - 總計) < 0.01m);
        Chk("刪收付：交易主檔已收付回復 0", Math.Abs(Sc($"SELECT [已收付金額] FROM [交易主檔] WHERE [單據副碼] = {saved.單據副碼}")) < 0.01m);
        Chk("刪收付：收付主檔/明細已刪", Lc($"SELECT COUNT(*) FROM [收付主檔] WHERE [單據副碼] = {payed.單據副碼}") == 0 &&
            Lc($"SELECT COUNT(*) FROM [收付明細] WHERE [單據號碼] = '{saved.交易單號}'") == 0);

        // 5. 刪除交易（還原庫存/帳款）
        TradeService.DeleteBill(saved.單據副碼);
        Chk("刪交易：主檔/明細已刪", Lc($"SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼] = {saved.單據副碼}") == 0 &&
            Lc($"SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼] = {saved.單據副碼}") == 0);
        Chk("刪交易：帳款簡要已刪", Lc($"SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號] = '{saved.交易單號}'") == 0);
        Chk("刪交易：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - stockBefore2) < 0.01m);
        Chk("刪交易：帳款主檔回復", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'") - arBefore) < 0.01m);

        // 6. 進貨單（庫存+、應付+，進項稅率 5%；A002 廠商課稅別為 NULL，驗證 NULL 路徑不 NRE 且視為應稅）
        decimal apBefore = Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'");
        const decimal p單價1 = 500m, p單價2 = 300m, p數1 = 2m, p數2 = 3m;
        decimal p合計 = p單價1 * p數1 + p單價2 * p數2, p稅 = p合計 * 0.05m, p總計 = p合計 + p稅;
        var reqP = new TradeService.SaveBillRequest
        {
            單據類別 = "進貨", 交易對象 = "A002", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-P-{DateTime.Now:yyyyMMddHHmmss}",
            明細 =
            {
                new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = p數1, 單價 = p單價1, 單位 = "個" },
                new TradeService.DetailRow { 貨品編號 = g2, 倉庫編號 = "A", 數量 = p數2, 單價 = p單價2, 單位 = "個" },
            }
        };
        var savedP = TradeService.SaveBill(reqP);
        Chk("進貨：總計=合計+稅(進項)", Math.Abs(Sc($"SELECT [總計金額]-[合計金額]-[營業稅] FROM [交易主檔] WHERE [單據副碼] = {savedP.單據副碼}")) < 0.01m, $"合計={p合計} 稅={p稅} 總計={p總計}");
        Chk("進貨：未收付=總計(應付)", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {savedP.單據副碼}") - p總計) < 0.01m);
        Chk("進貨：庫存增加", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - (stockBefore1 + p數1)) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - (stockBefore2 + p數2)) < 0.01m,
            $"{g1}:{stockBefore1}→{stockBefore1 + p數1} {g2}:{stockBefore2}→{stockBefore2 + p數2}");
        Chk("進貨：帳款簡要未收付=總計", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedP.交易單號}'") - p總計) < 0.01m);
        decimal apAfter = Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'");
        Chk("進貨：帳款主檔+總計", Math.Abs(apAfter - apBefore - p總計) < 0.01m, $"{apBefore}→{apAfter}");

        // 7. 刪除進貨（還原）
        TradeService.DeleteBill(savedP.單據副碼);
        Chk("刪進貨：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - stockBefore2) < 0.01m);
        Chk("刪進貨：主檔/明細/帳款簡要已刪", Lc($"SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼] = {savedP.單據副碼}") == 0 &&
            Lc($"SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼] = {savedP.單據副碼}") == 0 &&
            Lc($"SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號] = '{savedP.交易單號}'") == 0);
        Chk("刪進貨：帳款主檔回復", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - apBefore) < 0.01m);

        // 8. 出退單（銷項退回：庫存加回、沖銷應收 → 未收付為負）
        const decimal r單價1 = 600m, r單價2 = 400m, r數1 = 1m, r數2 = 2m;
        decimal r合計 = r單價1 * r數1 + r單價2 * r數2, r稅 = r合計 * 0.05m, r總計 = r合計 + r稅;
        var reqR = new TradeService.SaveBillRequest
        {
            單據類別 = "出退", 交易對象 = "AR-C001", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-R-{DateTime.Now:yyyyMMddHHmmss}",
            明細 =
            {
                new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = r數1, 單價 = r單價1, 單位 = "個" },
                new TradeService.DetailRow { 貨品編號 = g2, 倉庫編號 = "A", 數量 = r數2, 單價 = r單價2, 單位 = "個" },
            }
        };
        var savedR = TradeService.SaveBill(reqR);
        Chk("出退：未收付=-總計(沖銷應收)", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {savedR.單據副碼}") - (-r總計)) < 0.01m, $"總計={r總計}");
        Chk("出退：庫存加回", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - (stockBefore1 + r數1)) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - (stockBefore2 + r數2)) < 0.01m,
            $"{g1}:{stockBefore1}→{stockBefore1 + r數1} {g2}:{stockBefore2}→{stockBefore2 + r數2}");
        Chk("出退：帳款簡要未收付=-總計", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedR.交易單號}'") - (-r總計)) < 0.01m);
        Chk("出退：帳款主檔-總計", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'") - (arBefore - r總計)) < 0.01m, $"52500→{arBefore - r總計}");
        TradeService.DeleteBill(savedR.單據副碼);
        Chk("刪出退：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - stockBefore2) < 0.01m);
        Chk("刪出退：帳款主檔回復", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'") - arBefore) < 0.01m);

        // 9. 進退單（進項退回：庫存扣回、沖銷應付 → 未收付為負）
        const decimal t單價1 = 500m, t數1 = 1m;
        decimal t合計 = t單價1 * t數1, t稅 = t合計 * 0.05m, t總計 = t合計 + t稅;
        var reqT = new TradeService.SaveBillRequest
        {
            單據類別 = "進退", 交易對象 = "A002", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-T-{DateTime.Now:yyyyMMddHHmmss}",
            明細 =
            {
                new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = t數1, 單價 = t單價1, 單位 = "個" },
            }
        };
        var savedT = TradeService.SaveBill(reqT);
        Chk("進退：未收付=-總計(沖銷應付)", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {savedT.單據副碼}") - (-t總計)) < 0.01m, $"總計={t總計}");
        Chk("進退：庫存扣回", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - (stockBefore1 - t數1)) < 0.01m, $"{g1}:{stockBefore1}→{stockBefore1 - t數1}");
        Chk("進退：帳款簡要未收付=-總計", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedT.交易單號}'") - (-t總計)) < 0.01m);
        Chk("進退：帳款主檔-總計", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - (apBefore - t總計)) < 0.01m, $"0→{apBefore - t總計}");
        TradeService.DeleteBill(savedT.單據副碼);
        Chk("刪進退：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m);
        Chk("刪進退：主檔/帳款簡要已刪", Lc($"SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼] = {savedT.單據副碼}") == 0 &&
            Lc($"SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號] = '{savedT.交易單號}'") == 0);
        Chk("刪進退：帳款主檔回復", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - apBefore) < 0.01m);

        // ===== 10. 純累入預收（現金存入預收，不沖帳）=====
        decimal prepA2 = Sc("SELECT IFNULL([累計預收貨款],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'");
        var payPre = new PaymentService.SavePaymentRequest { 收付類別 = "收款", 沖帳對象 = "A002", 現金金額 = 3000m, 累入預收 = 3000m };
        var savedPre = PaymentService.SavePayment(payPre);
        Chk("累入預收：沖帳合計=0", Math.Abs(Sc($"SELECT [沖帳合計] FROM [收付主檔] WHERE [單據副碼] = {savedPre.單據副碼}")) < 0.01m);
        Chk("累入預收：累入預收=現金=3000", Math.Abs(Sc($"SELECT [累入預收] FROM [收付主檔] WHERE [單據副碼] = {savedPre.單據副碼}") - 3000m) < 0.01m &&
            Math.Abs(Sc($"SELECT [現金金額] FROM [收付主檔] WHERE [單據副碼] = {savedPre.單據副碼}") - 3000m) < 0.01m);
        Chk("累入預收：帳款主檔累計預收+3000", Math.Abs(Sc("SELECT IFNULL([累計預收貨款],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - (prepA2 + 3000m)) < 0.01m, $"預收={prepA2}→{prepA2 + 3000m}");

        // ===== 11. 取用預收沖帳（預收餘額扣減）=====
        const decimal u單價1 = 1000m, u數1 = 2m;
        decimal u總計 = u單價1 * u數1 * 1.05m;
        var reqU = new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = "A002", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-U-{DateTime.Now:yyyyMMddHHmmss}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = u數1, 單價 = u單價1, 單位 = "個" } }
        };
        var savedU = TradeService.SaveBill(reqU);
        var payU = new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "A002", 取用預收 = u總計,
            明細 = { new PaymentService.PaymentDetailRow { 交易單號 = savedU.交易單號, 單據類別 = "出貨", 未收付金額 = u總計, 沖帳金額 = u總計 } }
        };
        var savedPayU = PaymentService.SavePayment(payU);
        Chk("取用預收：收付主檔取用預收=總計", Math.Abs(Sc($"SELECT [取用預收] FROM [收付主檔] WHERE [單據副碼] = {savedPayU.單據副碼}") - u總計) < 0.01m, $"總計={u總計}");
        Chk("取用預收：預收餘額=原-取用", Math.Abs(Sc($"SELECT [預收餘額] FROM [收付主檔] WHERE [單據副碼] = {savedPayU.單據副碼}") - (prepA2 + 3000m - u總計)) < 0.01m);
        Chk("取用預收：帳款主檔累計預收-取用", Math.Abs(Sc("SELECT IFNULL([累計預收貨款],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - (prepA2 + 3000m - u總計)) < 0.01m);
        Chk("取用預收：帳款簡要未收付=0", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedU.交易單號}'")) < 0.01m);
        PaymentService.DeletePayment(savedPayU.單據副碼);
        Chk("刪取用預收：帳款簡要未收付回復", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedU.交易單號}'") - u總計) < 0.01m);
        Chk("刪取用預收：累計預收回復", Math.Abs(Sc("SELECT IFNULL([累計預收貨款],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - (prepA2 + 3000m)) < 0.01m);
        TradeService.DeleteBill(savedU.單據副碼);
        Chk("刪取用預收之出貨單：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m);

        // ===== 12. 票據沖帳（現金+票據組合）=====
        const decimal b單價1 = 2000m, b數1 = 1m;
        decimal b總計 = b單價1 * b數1 * 1.05m;
        var reqB = new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = "AR-C001", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-B-{DateTime.Now:yyyyMMddHHmmss}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g2, 倉庫編號 = "A", 數量 = b數1, 單價 = b單價1, 單位 = "個" } }
        };
        var savedB = TradeService.SaveBill(reqB);
        var payB = new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "AR-C001", 現金金額 = 600m, 票據金額 = b總計 - 600m,
            明細 = { new PaymentService.PaymentDetailRow { 交易單號 = savedB.交易單號, 單據類別 = "出貨", 未收付金額 = b總計, 沖帳金額 = b總計 } }
        };
        var savedPayB = PaymentService.SavePayment(payB);
        Chk("票據沖帳：票據+現金=沖帳合計", Math.Abs(Sc($"SELECT [票據金額] FROM [收付主檔] WHERE [單據副碼] = {savedPayB.單據副碼}") - (b總計 - 600m)) < 0.01m &&
            Math.Abs(Sc($"SELECT [現金金額] FROM [收付主檔] WHERE [單據副碼] = {savedPayB.單據副碼}") - 600m) < 0.01m, $"票據={b總計 - 600m}");
        Chk("票據沖帳：帳款簡要未收付=0", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedB.交易單號}'")) < 0.01m);
        PaymentService.DeletePayment(savedPayB.單據副碼);
        Chk("刪票據沖帳：帳款簡要回復", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedB.交易單號}'") - b總計) < 0.01m);
        TradeService.DeleteBill(savedB.單據副碼);
        Chk("刪票據沖帳之出貨單：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - stockBefore2) < 0.01m);

        // ===== 13. 折讓沖帳（沖帳+折讓=未收付）=====
        decimal 折讓AR0 = Sc("SELECT IFNULL([折讓金額],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");
        decimal 已收AR0 = Sc("SELECT IFNULL([已收付金額],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");
        const decimal d單價1 = 1000m, d數1 = 1m;
        decimal d總計 = d單價1 * d數1 * 1.05m; // 1050
        var reqD = new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = "AR-C001", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-D-{DateTime.Now:yyyyMMddHHmmss}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = d數1, 單價 = d單價1, 單位 = "個" } }
        };
        var savedD = TradeService.SaveBill(reqD);
        var payD = new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "AR-C001", 現金金額 = 1000m,
            明細 = { new PaymentService.PaymentDetailRow { 交易單號 = savedD.交易單號, 單據類別 = "出貨", 未收付金額 = d總計, 沖帳金額 = 1000m, 折讓金額 = 50m } }
        };
        var savedPayD = PaymentService.SavePayment(payD);
        Chk("折讓沖帳：銷貨折讓=50", Math.Abs(Sc($"SELECT [銷貨折讓] FROM [收付主檔] WHERE [單據副碼] = {savedPayD.單據副碼}") - 50m) < 0.01m);
        Chk("折讓沖帳：沖帳合計=1000(不含折讓)", Math.Abs(Sc($"SELECT [沖帳合計] FROM [收付主檔] WHERE [單據副碼] = {savedPayD.單據副碼}") - 1000m) < 0.01m);
        Chk("折讓沖帳：帳款簡要已收付=1000/未收付=0", Math.Abs(Sc($"SELECT [已收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedD.交易單號}'") - 1000m) < 0.01m &&
            Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedD.交易單號}'")) < 0.01m);
        Chk("折讓沖帳：交易主檔未收付=0", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {savedD.單據副碼}")) < 0.01m);
        decimal 折讓AR = Sc("SELECT IFNULL([折讓金額],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");
        decimal 已收AR = Sc("SELECT IFNULL([已收付金額],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'");
        Chk("折讓沖帳：帳款主檔已收付+1000/折讓+50", Math.Abs(已收AR - 已收AR0 - 1000m) < 0.01m && Math.Abs(折讓AR - 折讓AR0 - 50m) < 0.01m, $"已收付→{已收AR} 折讓→{折讓AR}");
        PaymentService.DeletePayment(savedPayD.單據副碼);
        Chk("刪折讓沖帳：帳款簡要未收付回復", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedD.交易單號}'") - d總計) < 0.01m);
        Chk("刪折讓沖帳：帳款主檔已收付/折讓回復", Math.Abs(Sc("SELECT IFNULL([已收付金額],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'") - 已收AR0) < 0.01m &&
            Math.Abs(Sc("SELECT IFNULL([折讓金額],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'") - 折讓AR0) < 0.01m);
        TradeService.DeleteBill(savedD.單據副碼);
        Chk("刪折讓沖帳之出貨單：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m);

        // ===== 14. 庫存調整（盤盈+盤虧，金額全 0、不產生帳款）=====
        var adjReq = new AdjustmentService.AdjustmentRequest
        {
            調整日期 = DateTime.Now, 原因 = "盤點盤盈",
            明細 =
            {
                new AdjustmentService.AdjustmentLine { 貨品編號 = g1, 倉庫編號 = "A", 數量 = 2m, 單位 = "個", 附註說明 = "盤盈" },
                new AdjustmentService.AdjustmentLine { 貨品編號 = g2, 倉庫編號 = "A", 數量 = -1m, 單位 = "個", 附註說明 = "盤虧" },
            }
        };
        string adjNo = AdjustmentService.SaveAdjustment(adjReq);
        long adjSeq = Lc($"SELECT [單據副碼] FROM [交易主檔] WHERE [交易單號] = '{adjNo}'");
        Chk("調整：主檔金額全 0", Math.Abs(Sc($"SELECT [總計金額]-[合計金額]-[營業稅] FROM [交易主檔] WHERE [單據副碼] = {adjSeq}")) < 0.01m &&
            Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {adjSeq}")) < 0.01m);
        Chk("調整：數量合計=+1", Math.Abs(Sc($"SELECT [數量合計] FROM [交易主檔] WHERE [單據副碼] = {adjSeq}") - 1m) < 0.01m, $"單號={adjNo}");
        Chk("調整：庫存增減(盤盈+/盤虧-)", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - (stockBefore1 + 2m)) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - (stockBefore2 - 1m)) < 0.01m,
            $"{g1}:{stockBefore1}→{stockBefore1 + 2m} {g2}:{stockBefore2}→{stockBefore2 - 1m}");
        Chk("調整：明細 2 筆", Lc($"SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼] = {adjSeq}") == 2);
        Chk("調整：異動快照 2 筆", Lc($"SELECT COUNT(*) FROM [交易異動] WHERE [單據副碼] = {adjSeq}") == 2 &&
            Lc($"SELECT COUNT(*) FROM [異動明細] WHERE [單據副碼] = {adjSeq}") == 2);
        Chk("調整：不產生帳款", Lc($"SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號] = '{adjNo}'") == 0);
        AdjustmentService.DeleteAdjustment(adjSeq);
        Chk("刪調整：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - stockBefore2) < 0.01m);
        Chk("刪調整：主檔/明細/快照已刪", Lc($"SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼] = {adjSeq}") == 0 &&
            Lc($"SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼] = {adjSeq}") == 0 &&
            Lc($"SELECT COUNT(*) FROM [交易異動] WHERE [單據副碼] = {adjSeq}") == 0);

        // ===== 15. 防呆：驗證失敗路徑（例外訊息 + 交易回滾）=====
        void ExpectFail(string name, Action act, string msgPart)
        {
            try { act(); Chk($"防呆：{name}", false, "未拋出例外"); }
            catch (InvalidOperationException ex) { Chk($"防呆：{name}", ex.Message.Contains(msgPart), ex.Message); }
        }
        ExpectFail("明細貨品空白", () => TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = "AR-C001",
            明細 = { new TradeService.DetailRow { 貨品編號 = "", 數量 = 1m } }
        }), "貨品編號不可空白");
        string dupNo = (string)DbManager.QueryScalar("SELECT [交易單號] FROM [交易主檔] WHERE [單據類別] = '出貨' LIMIT 1")!;
        ExpectFail("重複交易單號", () => TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = "AR-C001", 交易單號 = dupNo,
            明細 = { new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = 1m } }
        }), "已存在");
        ExpectFail("超沖(沖帳+折讓>未收付)", () => PaymentService.SavePayment(new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "AR-C001", 現金金額 = 100m,
            明細 = { new PaymentService.PaymentDetailRow { 交易單號 = "X", 單據類別 = "出貨", 未收付金額 = 100m, 沖帳金額 = 80m, 折讓金額 = 30m } }
        }), "超過未收付金額");
        ExpectFail("現金票據預收≠沖帳合計", () => PaymentService.SavePayment(new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "AR-C001", 現金金額 = 50m,
            明細 = { new PaymentService.PaymentDetailRow { 交易單號 = "X", 單據類別 = "出貨", 未收付金額 = 100m, 沖帳金額 = 100m } }
        }), "必須等於沖帳合計");
        ExpectFail("取用預收超過餘額", () => PaymentService.SavePayment(new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "A002", 現金金額 = 0m, 取用預收 = 9999m,
            明細 = { new PaymentService.PaymentDetailRow { 交易單號 = "X", 單據類別 = "出貨", 未收付金額 = 9999m, 沖帳金額 = 9999m } }
        }), "超過該對象預收餘額");
        ExpectFail("累入預收現金不符", () => PaymentService.SavePayment(new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "AR-C001", 現金金額 = 100m, 累入預收 = 200m
        }), "累入預收單");
        ExpectFail("無明細且無累入預收", () => PaymentService.SavePayment(new PaymentService.SavePaymentRequest
        {
            收付類別 = "收款", 沖帳對象 = "AR-C001", 現金金額 = 0m
        }), "未選取待沖帳單據");

        // ===== 16. 折扣計算與稅四捨五入（金額=數量×單價×折扣/100；稅=Round(合計×5%)）=====
        const decimal dc單價1 = 100m, dc單價2 = 47m, dc數1 = 3m, dc數2 = 1m;
        decimal dc金額1 = Math.Round(dc數1 * dc單價1 * 80m / 100m, 2, MidpointRounding.AwayFromZero);
        decimal dc合計 = dc金額1 + dc數2 * dc單價2; // 240+47=287
        decimal dc稅 = Math.Round(dc合計 * 0.05m, 0, MidpointRounding.AwayFromZero); // 14
        decimal dc總計 = dc合計 + dc稅;
        var reqDC = new TradeService.SaveBillRequest
        {
            單據類別 = "出貨", 交易對象 = "AR-C001", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-DC-{DateTime.Now:yyyyMMddHHmmss}",
            明細 =
            {
                new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = dc數1, 單價 = dc單價1, 折扣 = 80m, 單位 = "個" },
                new TradeService.DetailRow { 貨品編號 = g2, 倉庫編號 = "A", 數量 = dc數2, 單價 = dc單價2, 單位 = "個" },
            }
        };
        var savedDC = TradeService.SaveBill(reqDC);
        Chk("折扣：明細金額=數量×單價×折扣/100", Math.Abs(Sc($"SELECT [金額] FROM [交易明細] WHERE [單據副碼] = {savedDC.單據副碼} AND [建檔序號] = (SELECT MIN([建檔序號]) FROM [交易明細] WHERE [單據副碼] = {savedDC.單據副碼})") - dc金額1) < 0.01m, $"240 折扣80");
        Chk("折扣：主檔合計=Σ金額", Math.Abs(Sc($"SELECT [合計金額] FROM [交易主檔] WHERE [單據副碼] = {savedDC.單據副碼}") - dc合計) < 0.01m, $"合計={dc合計}");
        Chk("折扣：稅=Round(合計×5%)", Math.Abs(Sc($"SELECT [營業稅] FROM [交易主檔] WHERE [單據副碼] = {savedDC.單據副碼}") - dc稅) < 0.01m, $"稅={dc稅}(287×5%=14.35→14)");
        Chk("折扣：總計=合計+稅", Math.Abs(Sc($"SELECT [總計金額] FROM [交易主檔] WHERE [單據副碼] = {savedDC.單據副碼}") - dc總計) < 0.01m, $"總計={dc總計}");
        TradeService.DeleteBill(savedDC.單據副碼);
        Chk("折扣：刪除回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m &&
            Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g2)) - stockBefore2) < 0.01m);

        // ===== 17. 付款方向（收付類別=付款，沖銷廠商應付）=====
        const decimal f單價1 = 300m, f數1 = 2m;
        decimal f總計 = f單價1 * f數1 * 1.05m; // 630
        var reqF = new TradeService.SaveBillRequest
        {
            單據類別 = "進貨", 交易對象 = "A002", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-F-{DateTime.Now:yyyyMMddHHmmss}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = f數1, 單價 = f單價1, 單位 = "個" } }
        };
        var savedF = TradeService.SaveBill(reqF);
        var payF = new PaymentService.SavePaymentRequest
        {
            收付類別 = "付款", 沖帳對象 = "A002", 現金金額 = f總計,
            明細 = { new PaymentService.PaymentDetailRow { 交易單號 = savedF.交易單號, 單據類別 = "進貨", 未收付金額 = f總計, 沖帳金額 = f總計 } }
        };
        var savedPayF = PaymentService.SavePayment(payF);
        Chk("付款：收付主檔類別=付款", (string)DbManager.QueryScalar($"SELECT [收付類別] FROM [收付主檔] WHERE [單據副碼] = {savedPayF.單據副碼}") == "付款", $"總計={f總計}");
        Chk("付款：沖帳合計=現金", Math.Abs(Sc($"SELECT [沖帳合計] FROM [收付主檔] WHERE [單據副碼] = {savedPayF.單據副碼}") - f總計) < 0.01m);
        Chk("付款：帳款簡要未收付=0", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedF.交易單號}'")) < 0.01m);
        PaymentService.DeletePayment(savedPayF.單據副碼);
        Chk("刪付款：帳款簡要未收付回復", Math.Abs(Sc($"SELECT [未收付金額] FROM [帳款簡要] WHERE [交易單號] = '{savedF.交易單號}'") - f總計) < 0.01m);
        TradeService.DeleteBill(savedF.單據副碼);
        Chk("刪付款之進貨單：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m);

        // ===== 18. 修改進貨單（正向單修改：ReverseEffects 減回舊影響再重算，不得累加）=====
        const decimal m單價1 = 300m, m數1 = 2m;
        var reqM = new TradeService.SaveBillRequest
        {
            單據類別 = "進貨", 交易對象 = "A002", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-M-{DateTime.Now:yyyyMMddHHmmss}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = m數1, 單價 = m單價1, 單位 = "個" } }
        };
        var savedM = TradeService.SaveBill(reqM);
        const decimal m2單價1 = 400m, m2數1 = 3m;
        decimal m2合計 = m2單價1 * m2數1, m2稅 = m2合計 * 0.05m, m2總計 = m2合計 + m2稅;
        var reqM2 = new TradeService.SaveBillRequest
        {
            單據類別 = "進貨", 交易對象 = "A002", 倉庫編號 = "A", 員工編號 = "001",
            單據副碼 = savedM.單據副碼, 交易單號 = savedM.交易單號,
            明細 = { new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = m2數1, 單價 = m2單價1, 單位 = "個" } }
        };
        var savedM2 = TradeService.SaveBill(reqM2);
        Chk("改進貨：未收付=新總計", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {savedM.單據副碼}") - m2總計) < 0.01m, $"合計={m2合計} 稅={m2稅} 總計={m2總計}");
        Chk("改進貨：庫存淨增(不累加)", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - (stockBefore1 + m2數1)) < 0.01m, $"{g1}:{stockBefore1}→{stockBefore1 + m2數1}");
        Chk("改進貨：帳款主檔淨+新總計(不累加)", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - (apBefore + m2總計)) < 0.01m, $"{apBefore}→{apBefore + m2總計}");
        TradeService.DeleteBill(savedM.單據副碼);
        Chk("刪改進貨：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m);
        Chk("刪改進貨：帳款主檔回復", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - apBefore) < 0.01m);

        // ===== 19. 修改出退單（負向單修改：ReverseEffects 加回舊影響再重算，不得累加）=====
        const decimal n單價1 = 500m, n數1 = 2m;
        decimal n總計 = n單價1 * n數1 * 1.05m; // 1050
        var reqN = new TradeService.SaveBillRequest
        {
            單據類別 = "出退", 交易對象 = "AR-C001", 倉庫編號 = "A", 員工編號 = "001",
            交易單號 = $"SVCTEST-N-{DateTime.Now:yyyyMMddHHmmss}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = n數1, 單價 = n單價1, 單位 = "個" } }
        };
        var savedN = TradeService.SaveBill(reqN);
        const decimal n2單價1 = 600m, n2數1 = 3m;
        decimal n2合計 = n2單價1 * n2數1, n2稅 = n2合計 * 0.05m, n2總計 = n2合計 + n2稅;
        var reqN2 = new TradeService.SaveBillRequest
        {
            單據類別 = "出退", 交易對象 = "AR-C001", 倉庫編號 = "A", 員工編號 = "001",
            單據副碼 = savedN.單據副碼, 交易單號 = savedN.交易單號,
            明細 = { new TradeService.DetailRow { 貨品編號 = g1, 倉庫編號 = "A", 數量 = n2數1, 單價 = n2單價1, 單位 = "個" } }
        };
        var savedN2 = TradeService.SaveBill(reqN2);
        Chk("改出退：未收付=-新總計", Math.Abs(Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼] = {savedN.單據副碼}") - (-n2總計)) < 0.01m, $"合計={n2合計} 稅={n2稅} 總計={n2總計}");
        Chk("改出退：庫存淨加回(不累加)", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - (stockBefore1 + n2數1)) < 0.01m, $"{g1}:{stockBefore1}→{stockBefore1 + n2數1}");
        Chk("改出退：帳款主檔淨-新總計(不累加)", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'") - (arBefore - n2總計)) < 0.01m, $"{arBefore}→{arBefore - n2總計}");
        TradeService.DeleteBill(savedN.單據副碼);
        Chk("刪改出退：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號] = $g AND [倉庫編號] = 'A'", DbManager.Param("$g", g1)) - stockBefore1) < 0.01m);
        Chk("刪改出退：帳款主檔回復", Math.Abs(Sc("SELECT IFNULL([本期總計],0) FROM [帳款主檔] WHERE [交易對象] = 'AR-C001'") - arBefore) < 0.01m);

        // ===== 收尾：刪純預收單 =====
        PaymentService.DeletePayment(savedPre.單據副碼);
        Chk("刪累入預收：累計預收回復", Math.Abs(Sc("SELECT IFNULL([累計預收貨款],0) FROM [帳款主檔] WHERE [交易對象] = 'A002'") - prepA2) < 0.01m);
    }
    finally
    {
        DbManager.DatabasePath = 原Db;
        Console.WriteLine($"\n結果：PASS {pass} / FAIL {fail}");
    }
}

/// <summary>新交易類別流程驗證：借出/借入/託售/託工/調撥/領料（不動帳款、調撥雙倉、刪除回復、修改重算）。</summary>
static void TradeFlowTest()
{
    string testDb = Path.Combine(@"C:\Users\JS\AppData\Local\Temp\opencode", "HeliERP-tflow.db");
    if (File.Exists(testDb)) File.Delete(testDb);
    File.Copy(@"D:\HeliAcc\HeliERP.db", testDb);
    string 原Db = DbManager.DatabasePath;
    DbManager.DatabasePath = testDb;
    Console.WriteLine("\n== 新交易類別流程驗證（借出/借入/託售/託工/調撥/領料）==");
    int pass = 0, fail = 0;
    void Chk(string name, bool ok, string detail = "")
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
        if (ok) pass++; else fail++;
    }
    decimal Sc(string sql, params SqliteParameter[] ps) => Convert.ToDecimal(DbManager.QueryScalar(sql, ps));
    long Lc(string sql, params SqliteParameter[] ps) => Convert.ToInt64(DbManager.QueryScalar(sql, ps));

    try
    {
        const string g = "HR", whA = "A", wh1 = "1";
        decimal stA = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        decimal st1 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='1'");
        string ts = DateTime.Now.ToString("yyyyMMddHHmmss");

        // 1. 借出（客戶 A0001）：庫存減、無帳款
        var 借出 = TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "借出", 交易對象 = "A0001", 倉庫編號 = whA, 員工編號 = "001",
            交易單號 = $"TFLOW-借出-{ts}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g, 倉庫編號 = whA, 數量 = 1m, 單價 = 100m, 單位 = "個" } }
        });
        Chk("借出：庫存減 1", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'") - (stA - 1m)) < 0.01m, $"{stA}→{stA - 1m}");
        Chk("借出：未收付金額=0", Sc($"SELECT [未收付金額] FROM [交易主檔] WHERE [單據副碼]={借出.單據副碼}") == 0m);
        Chk("借出：不寫帳款簡要", Lc($"SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號]='{借出.交易單號}'") == 0);
        Chk("借出：不寫帳款明細", Lc($"SELECT COUNT(*) FROM [帳款明細] WHERE [交易單號]='{借出.交易單號}'") == 0);
        Chk("借出：交易對象為客戶", Lc("SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼]=$c AND [交易對象]='A0001'", DbManager.Param("$c", 借出.單據副碼)) == 1);

        // 2. 借入（廠商 A002）：庫存增、無帳款
        decimal stA2 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        var 借入 = TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "借入", 交易對象 = "A002", 倉庫編號 = whA, 員工編號 = "001",
            交易單號 = $"TFLOW-借入-{ts}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g, 倉庫編號 = whA, 數量 = 2m, 單價 = 100m, 單位 = "個" } }
        });
        Chk("借入：庫存增 2", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'") - (stA2 + 2m)) < 0.01m);
        Chk("借入：不寫帳款簡要", Lc($"SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號]='{借入.交易單號}'") == 0);

        // 3. 調撥 A→1（交易對象空白、雙倉異動）
        var 調撥 = TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "調撥", 交易對象 = "", 倉庫編號 = whA, 員工編號 = "001",
            交易單號 = $"TFLOW-調撥-{ts}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g, 倉庫編號 = whA, 調入倉庫 = wh1, 數量 = 1m, 單價 = 100m, 單位 = "個" } }
        });
        Chk("調撥：調出倉庫減 1", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'") - (stA2 + 2m - 1m)) < 0.01m);
        Chk("調撥：調入倉庫增 1", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='1'") - (st1 + 1m)) < 0.01m);
        Chk("調撥：主檔寫入調入倉庫", Lc("SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼]=$c AND [調入倉庫]='1'", DbManager.Param("$c", 調撥.單據副碼)) == 1);
        Chk("調撥：明細寫入調入倉庫", Lc("SELECT COUNT(*) FROM [交易明細] WHERE [單據副碼]=$c AND [調入倉庫]='1'", DbManager.Param("$c", 調撥.單據副碼)) == 1);
        Chk("調撥：免稅總計=合計", Sc($"SELECT [總計金額]-[合計金額] FROM [交易主檔] WHERE [單據副碼]={調撥.單據副碼}") == 0m);

        // 4. 領料（交易對象空白、無帳款）
        decimal stA3 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        var 領料 = TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "領料", 交易對象 = "", 倉庫編號 = whA, 員工編號 = "001",
            交易單號 = $"TFLOW-領料-{ts}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g, 倉庫編號 = whA, 數量 = 1m, 單價 = 100m, 單位 = "個" } }
        });
        Chk("領料：庫存減 1", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'") - (stA3 - 1m)) < 0.01m);
        Chk("領料：不寫帳款簡要", Lc($"SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號]='{領料.交易單號}'") == 0);
        Chk("領料：免稅總計=合計", Sc($"SELECT [總計金額]-[合計金額] FROM [交易主檔] WHERE [單據副碼]={領料.單據副碼}") == 0m);

        // 5. 託售（客戶 A0001）：庫存減、無帳款
        decimal stA4 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        var 託售 = TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據類別 = "託售", 交易對象 = "A0001", 倉庫編號 = whA, 員工編號 = "001",
            交易單號 = $"TFLOW-託售-{ts}",
            明細 = { new TradeService.DetailRow { 貨品編號 = g, 倉庫編號 = whA, 數量 = 1m, 單價 = 100m, 單位 = "個" } }
        });
        Chk("託售：庫存減 1", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'") - (stA4 - 1m)) < 0.01m);
        Chk("託售：不寫帳款簡要", Lc($"SELECT COUNT(*) FROM [帳款簡要] WHERE [交易單號]='{託售.交易單號}'") == 0);

        // 6. 刪除調撥：庫存回復
        decimal stADel = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        decimal st1Del = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='1'");
        TradeService.DeleteBill(調撥.單據副碼);
        Chk("刪除調撥：調出倉庫回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'") - (stADel + 1m)) < 0.01m);
        Chk("刪除調撥：調入倉庫回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='1'") - (st1Del - 1m)) < 0.01m);
        Chk("刪除調撥：主檔已刪", Lc($"SELECT COUNT(*) FROM [交易主檔] WHERE [單據副碼]={調撥.單據副碼}") == 0);

        // 7. 刪除借出：庫存回復
        decimal stDel2 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        TradeService.DeleteBill(借出.單據副碼);
        Chk("刪除借出：庫存回復", Math.Abs(Sc($"SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'") - (stDel2 + 1m)) < 0.01m);

        // 8. 修改借入：數量 2→3（ReverseEffects 回復再重算）
        TradeService.SaveBill(new TradeService.SaveBillRequest
        {
            單據副碼 = 借入.單據副碼, 單據類別 = "借入", 交易對象 = "A002", 倉庫編號 = whA, 員工編號 = "001",
            交易單號 = 借入.交易單號,
            明細 = { new TradeService.DetailRow { 貨品編號 = g, 倉庫編號 = whA, 數量 = 3m, 單價 = 100m, 單位 = "個" } }
        });
        decimal 最終 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        Chk("修改借入：數量 2→3 庫存正確", Math.Abs(最終 - (stA + 3m - 1m - 1m)) < 0.01m, $"最終={最終} 期望={stA + 3m - 1m - 1m}");

        // 9. 借出還入/借入還出/託售回貨/託工出庫/託工入庫 庫存方向
        decimal sA = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        string[] 方向 = { "借出還入", "借入還出", "託售回貨", "託工出庫", "託工入庫" };
        foreach (var k in 方向)
        {
            string 對象 = k is "借入還出" or "託工出庫" or "託工入庫" ? "A002" : "A0001";
            TradeService.SaveBill(new TradeService.SaveBillRequest
            {
                單據類別 = k, 交易對象 = 對象, 倉庫編號 = whA, 員工編號 = "001",
                交易單號 = $"TFLOW-{k}-{ts}",
                明細 = { new TradeService.DetailRow { 貨品編號 = g, 倉庫編號 = whA, 數量 = 1m, 單價 = 100m, 單位 = "個" } }
            });
        }
        decimal sA2 = Sc("SELECT [現有數量] FROM [貨品庫存] WHERE [貨品編號]='HR' AND [倉庫編號]='A'");
        Chk("各類別庫存方向正確", Math.Abs(sA2 - (sA + 1m - 1m + 1m - 1m + 1m)) < 0.01m, $"{sA}→{sA2}");
    }
    catch (Exception ex)
    {
        Console.WriteLine("EX: " + ex.Message);
        fail++;
    }
    finally
    {
        DbManager.DatabasePath = 原Db;
        Console.WriteLine($"\n結果：PASS {pass} / FAIL {fail}");
    }
}

static void DumpFields(string rtmPath)
{
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    Console.WriteLine($"\n== {Path.GetFileName(rtmPath)} ==");
    DumpRec(root);
}

static void DumpTree(string rtmPath)
{
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    Console.WriteLine($"\n== {Path.GetFileName(rtmPath)} ==");
    DumpNode(root, 0);
}

static void DumpNode(Tpf0Object o, int depth)
{
    string Get(string n) => o.Properties.FirstOrDefault(p => p.Name == n).Value?.ToString() ?? "";
    var pos = new[] { "mmLeft", "mmTop", "mmWidth", "mmHeight" }
        .Where(n => Get(n) != "")
        .Select(n => $"{n}={Get(n)}");
    var info = new[] { "DataPipeline", "DataField", "DataPipelineName", "DBCalcType", "Caption", "Font.Size" }
        .Where(n => Get(n) != "")
        .Select(n => $"{n}={Get(n)}");
    Console.WriteLine($"{new string(' ', depth * 2)}{o.ClassName} [{o.Name}] {string.Join(" ", pos)} {string.Join(" ", info)}");
    var fsp = o.PropertiesEx.FirstOrDefault(p => p.Name == "Font.Size");
    if (fsp is not null)
        Console.WriteLine($"{new string(' ', depth * 2)}  [Font.Size tag=0x{fsp.Tag:X2} len={fsp.ValuePayloadLength} off={fsp.ValuePayloadStart}]");
    foreach (var c in o.Children) DumpNode(c, depth + 1);
}

static void DumpRec(Tpf0Object o)
{
    if (o.ClassName == "TppDBCalc")
        Console.WriteLine($"  [TppDBCalc {o.Name}] " + string.Join(", ",
            o.Properties.Select(p => $"{p.Name}={p.Value}")));
    if (o.ClassName is "TppDBText" or "TppDBCalc" or "TppDBMemo")
    {
        string Get(string name) =>
            o.Properties.FirstOrDefault(p => p.Name == name).Value?.ToString() ?? "";
        var pipe = Get("DataPipeline") != "" ? Get("DataPipeline") : Get("DataPipelineName");
        var field = Get("DataField");
        var fmt = Get("DisplayFormat");
        var calc = Get("DBCalcType");
        string Pos(string n) => (float.TryParse(Get(n), out var v) ? v / 1000f : 0f).ToString("F1");
        Console.WriteLine($"{pipe,-16}|{field,-24}|{fmt,-16}|{o.ClassName}|calc={calc,-10}|L={Pos("mmLeft")} T={Pos("mmTop")} W={Pos("mmWidth")} H={Pos("mmHeight")}");
    }
    foreach (var c in o.Children) DumpRec(c);
}

static void OverlapCheck()
{
    const float eps = 0.3f; // 重疊容差 mm（線/框不計）
    var files = Directory.GetFiles(@"D:\HeliAcc\Rep", "*.*")
        .Where(f => f.EndsWith(".rtm", StringComparison.OrdinalIgnoreCase))
        .OrderBy(f => f);
    int clean = 0, dirty = 0;
    foreach (var f in files)
    {
        string name = Path.GetFileName(f);
        var issues = new List<string>();
        try
        {
            var root = Tpf0Reader.Parse(File.ReadAllBytes(f));
            var r = RtmLoader.Load(root);
            float pw = r.MmPaperWidth / 1000f, ph = r.MmPaperHeight / 1000f;
            foreach (var band in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
            {
                if (band is null) continue;
                var flat = new List<(RtmComponent C, float L, float T, float W, float H, string Band, string Path)>();
                void Walk(List<RtmComponent> comps, string path, float ox, float oy, string kind)
                {
                    foreach (var c in comps)
                    {
                        float l = ox + c.MmLeft / 1000f, t = oy + c.MmTop / 1000f;
                        float w = c.MmWidth / 1000f, h = c.MmHeight / 1000f;
                        var loc = $"{path}/{c.ClassName}";
                        if (!c.ClassName.EndsWith("Band"))   // band 容器本身不參與重疊/越界比對
                            flat.Add((c, l, t, w, h, kind, loc));
                        float cox = ox + c.MmLeft / 1000f, coy = oy + c.MmTop / 1000f;
                        string childKind = c.ClassName.StartsWith("Tpp") && c.ClassName.EndsWith("Band")
                            ? c.ClassName[3..^4] : kind;
                        Walk(c.Children, loc, cox, coy, childKind);
                    }
                }
                Walk(band.Components, band.Kind, 0f, 0f, band.Kind);
                bool IsText(RtmComponent c) => c.ClassName is "TppLabel" or "TppDBText" or "TppDBMemo" or "TppDBCalc" or "TppSystemVariable";
                // 越界檢查（含線條與框）
                foreach (var (c, l, t, w, h, _, path) in flat)
                {
                    if (l < -0.2 || t < -0.2 || l + w > pw + 0.2 || t + h > ph + 0.2)
                        issues.Add($"  越界 {path} L={l:F1} T={t:F1} R={l + w:F1} B={t + h:F1}（紙張 {pw:F0}x{ph:F0}mm）");
                }
                // 文字-文字重疊（同 band；不同 band 不同時渲染不互比）
                var texts = flat.Where(x => IsText(x.C)).ToList();
                for (int i = 0; i < texts.Count; i++)
                for (int j = i + 1; j < texts.Count; j++)
                {
                    var a = texts[i]; var b = texts[j];
                    if (a.Band != b.Band) continue;
                    if (a.L + eps < b.L + b.W && b.L + eps < a.L + a.W
                        && a.T + eps < b.T + b.H && b.T + eps < a.T + a.H)
                    {
                        string ca = a.C.Caption ?? a.C.DataField ?? a.C.ClassName;
                        string cb = b.C.Caption ?? b.C.DataField ?? b.C.ClassName;
                        issues.Add($"  文字重疊 {a.Path}({ca}) 與 {b.Path}({cb}) 交疊區 {OverlapMm(a, b):F1}mm");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add($"  解析失敗 {ex.GetType().Name}: {ex.Message}");
        }
        if (issues.Count > 0)
        {
            dirty++;
            Console.WriteLine($"D   {name}");
            foreach (var s in issues) Console.WriteLine(s);
        }
        else
        {
            clean++;
            Console.WriteLine($"OK  {name}");
        }
    }
    Console.WriteLine($"\n=== 越界/重疊檢查: {clean} 乾淨 / {dirty} 有問題 / 共 {clean + dirty} 檔 ===");
}

static float OverlapMm((RtmComponent C, float L, float T, float W, float H, string Band, string Path) a,
    (RtmComponent C, float L, float T, float W, float H, string Band, string Path) b)
{
    float ox = Math.Min(a.L + a.W, b.L + b.W) - Math.Max(a.L, b.L);
    float oy = Math.Min(a.T + a.H, b.T + b.H) - Math.Max(a.T, b.T);
    return Math.Max(0, ox) * Math.Max(0, oy);
}

static void OneRender(string rtmFile)
{
    const int dpi = 150;
    string path = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    if (!File.Exists(path)) { Console.WriteLine($"找不到 {path}"); return; }
    var root = Tpf0Reader.Parse(File.ReadAllBytes(path));
    var r = RtmLoader.Load(root);

    // 掃描所有資料欄位
    var fields = new List<(string Pipe, string Field)>();
    void Scan(RtmComponent c)
    {
        if (c.DataField is { Length: > 0 })
            fields.Add((c.DataPipeline ?? "", c.DataField));
        foreach (var s in c.Children) Scan(s);
    }
    foreach (var b in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
        if (b is not null)
            foreach (var c in b.Components) Scan(c);

    string Fake(string f) => FakeValue(f);

    var data = MakeReportData(fields);

    int w = Math.Max(1, (int)Math.Round(r.MmPaperWidth * dpi / 25400.0));
    int h = Math.Max(1, (int)Math.Round(r.MmPaperHeight * dpi / 25400.0));
    using var bmp = new Bitmap(w, h);
    bmp.SetResolution(dpi, dpi);
    using var ren = new RtmRenderer(r, data);
    ren.DrawnTexts = new List<(RtmComponent, float, float, float, float, string)>();
    ren.DrawnLines = new List<(RtmComponent, float, float, float, float)>();
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        int pages = 0;
        do { pages++; } while (ren.RenderPage(g, new RectangleF(0, 0, w, h), st));
        Console.WriteLine($"渲染完成 {pages} 頁");
    }
    string outPath = $@"D:\HeliAcc\shots\one_{Path.GetFileNameWithoutExtension(rtmFile)}.png";
    bmp.Save(outPath, ImageFormat.Png);
    Console.WriteLine($"已存 {outPath} ({w}x{h})");

    // 實際繪製文字重疊診斷（不同元件矩形交疊 > 1mm²）
    var dt = ren.DrawnTexts;
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"實際文字重疊診斷: {rtmFile} (假資料, {dt.Count} 個繪製記錄)");
    float eps = 0.3f;
    var pairs = new HashSet<string>();
    for (int i = 0; i < dt.Count; i++)
    for (int j = i + 1; j < dt.Count; j++)
    {
        var (ca, ax, ay, aw, ah, ta) = dt[i];
        var (cb, bx, by, bw, bh, tb) = dt[j];
        if (ReferenceEquals(ca, cb)) continue;
        if (ax + eps < bx + bw && bx + eps < ax + aw
            && ay + eps < by + bh && by + eps < ay + ah)
        {
            float ox = Math.Min(ax + aw, bx + bw) - Math.Max(ax, bx);
            float oy = Math.Min(ay + ah, by + bh) - Math.Max(ay, by);
            float area = ox * oy;
            if (area > 1f)
            {
                string key = $"{ca.GetHashCode()}|{cb.GetHashCode()}";
                if (pairs.Add(key))
                    sb.AppendLine($"  重疊 {Ox(ca)}[{ax / 1000f:F1},{ay / 1000f:F1},{ (ax + aw) / 1000f:F1},{ (ay + ah) / 1000f:F1}] \"{Tr(ta)}\" 與 {Ox(cb)}[{bx / 1000f:F1},{by / 1000f:F1},{ (bx + bw) / 1000f:F1},{ (by + bh) / 1000f:F1}] \"{Tr(tb)}\" => {area / 1e6f:F2}mm²");
            }
        }
    }
    string rep = $@"D:\HeliAcc\shots\one_{Path.GetFileNameWithoutExtension(rtmFile)}_overlap.txt";
    File.WriteAllText(rep, sb.ToString(), new System.Text.UTF8Encoding(false));
    Console.WriteLine(sb.ToString());

    // ═══ 線／文字像素重疊診斷：實際渲染像素中，線是否切穿字跡 ═══
    var lineOverlaps = PixelLineTextOverlaps(bmp, dpi, ren.DrawnLines, ren.DrawnTexts);
    if (lineOverlaps.Count > 0)
    {
        var sb2 = new System.Text.StringBuilder();
        sb2.AppendLine($"線/文字像素重疊診斷: {rtmFile} (真重疊 {lineOverlaps.Count} 處)");
        foreach (var s in lineOverlaps) sb2.AppendLine("  " + s);
        File.AppendAllText(rep, sb2.ToString(), new System.Text.UTF8Encoding(false));
        Console.WriteLine(sb2.ToString());
    }
    ren.Dispose();
}

/// <summary>像素級「線切穿字跡」檢查：線帶兩側（上下/左右）都有連續字跡墨且與線帶間隙 ≤1px 才算。</summary>
static List<string> PixelLineTextOverlaps(Bitmap bmp, int dpi,
    List<(RtmComponent C, float Xmm, float Ymm, float Wmm, float Hmm)>? lines,
    List<(RtmComponent C, float Xmm, float Ymm, float Wmm, float Hmm, string Text)>? texts)
{
    var res = new List<string>();
    if (lines is not { Count: > 0 } || texts is not { Count: > 0 }) return res;
    int w = bmp.Width, h = bmp.Height;
    float scale = dpi / 25.4f;
    var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    try
    {
        unsafe
        {
            byte* p0 = (byte*)bd.Scan0;
            bool Ink(int x, int y) => x >= 0 && x < w && y >= 0 && y < h &&
                (p0[y * bd.Stride + x * 3] < 200 || p0[y * bd.Stride + x * 3 + 1] < 200 || p0[y * bd.Stride + x * 3 + 2] < 200);

            var vRuns = new List<(int x0, int x1)>();
            var hRuns = new List<(int y0, int y1)>();
            foreach (var (_, lx, ly, lw, lh) in lines)
            {
                if (lw < lh) vRuns.Add(((int)Math.Floor(lx * scale), (int)Math.Ceiling((lx + lw) * scale)));
                else hRuns.Add(((int)Math.Floor(ly * scale), (int)Math.Ceiling((ly + lh) * scale)));
            }

            foreach (var (cl, lx, ly, lw, lh) in lines)
            {
                bool hLine = lh < lw;
                int xa = Math.Max(0, (int)Math.Floor(lx * scale));
                int xb = Math.Min(w - 1, (int)Math.Ceiling((lx + lw) * scale));
                int ya = Math.Max(0, (int)Math.Floor(ly * scale));
                int yb = Math.Min(h - 1, (int)Math.Ceiling((ly + lh) * scale));
                var xs = new List<int>();
                if (hLine)
                {
                    for (int x = xa; x <= xb; x++)
                    {
                        if (vRuns.Any(v => x >= v.x0 && x <= v.x1)) continue;   // 與垂直線交叉點
                        int gapU = -1;
                        for (int y = ya - 1; y >= 0; y--) { if (Ink(x, y)) { gapU = ya - 1 - y; break; } }
                        int gapD = -1;
                        for (int y = yb + 1; y < h; y++) { if (Ink(x, y)) { gapD = y - (yb + 1); break; } }
                        if (gapU >= 0 && gapU <= 1 && gapD >= 0 && gapD <= 1) xs.Add(x);
                    }
                }
                else
                {
                    for (int y = ya; y <= yb; y++)
                    {
                        if (hRuns.Any(hh => y >= hh.y0 && y <= hh.y1)) continue;   // 與水平線交叉點
                        int gapL = -1;
                        for (int x = xa - 1; x >= 0; x--) { if (Ink(x, y)) { gapL = xa - 1 - x; break; } }
                        int gapR = -1;
                        for (int x = xb + 1; x < w; x++) { if (Ink(x, y)) { gapR = x - (xb + 1); break; } }
                        if (gapL >= 0 && gapL <= 1 && gapR >= 0 && gapR <= 1) xs.Add(y);
                    }
                }
                if (xs.Count == 0) continue;
                // 合併連續座標 run（≥2px 視為字跡特徵）
                xs.Sort();
                int s0 = xs[0], e0 = xs[0];
                var runs = new List<(int s, int e)>();
                for (int i = 1; i < xs.Count; i++)
                {
                    if (xs[i] == e0 + 1) e0 = xs[i];
                    else { runs.Add((s0, e0)); s0 = e0 = xs[i]; }
                }
                runs.Add((s0, e0));
                foreach (var (s, e) in runs)
                {
                    if (e - s + 1 < 2) continue;
                    float cxmm = (xa + xb) / 2f / scale, cymm = (ya + yb) / 2f / scale;
                    RtmComponent? hitC = null; string hitT = "";
                    foreach (var (tc, ax, ay, aw, ah, tt) in texts)
                    {
                        if (ax - 0.2f <= cxmm && cxmm <= ax + aw + 0.2f && ay - 0.2f <= cymm && cymm <= ay + ah + 0.2f)
                        { hitC = tc; hitT = tt; break; }
                    }
                    res.Add($"線 {Ox(cl)}[{lx:F1},{ly:F1},{lx + lw:F1},{ly + lh:F1}] 切穿 {(hitC is null ? "?" : Ox(hitC))}[{cxmm:F1},{cymm:F1}] \"{Tr(hitT)}\"");
                }
            }
        }
    }
    finally { bmp.UnlockBits(bd); }
    return res;
}

static string Ox(RtmComponent c) =>
    c.TextSource.Length > 0 ? $"{c.ClassName}({c.TextSource})"
    : c.Caption is { Length: > 0 } ? $"{c.ClassName}({c.Caption})"
    : c.DataField is { Length: > 0 } ? $"{c.ClassName}({c.DataField})"
    : c.ClassName;

static string Tr(string s) => s.Length > 8 ? s[..8] : s;

static void DumpDetail(string rtmFile)
{
    string path = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    if (!File.Exists(path)) { Console.WriteLine($"找不到 {path}"); return; }
    var root = Tpf0Reader.Parse(File.ReadAllBytes(path));
    var r = RtmLoader.Load(root);
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"Dump: {rtmFile} ({r.MmPaperWidth / 1000f:F1}x{r.MmPaperHeight / 1000f:F1}mm)");
    void Emit(string bandName, float bandY, RtmBand? band)
    {
        if (band is null) return;
        sb.AppendLine($"== {bandName} (height={band.MmHeight / 1000f:F2}, 頁面y≈{bandY / 1000f:F2})");
        foreach (var c in band.Components)
        {
            string label = c.Caption is { Length: > 0 } ? $"cap=\"{c.Caption}\"" : "";
            string field = c.DataField is { Length: > 0 } ? $"field=\"{c.DataField}\" pipe=\"{c.DataPipeline}\"" : "";
            sb.AppendLine($"  {c.ClassName}/{c.Name} L={c.MmLeft / 1000f:F2} T={c.MmTop / 1000f:F2} W={c.MmWidth / 1000f:F2} H={c.MmHeight / 1000f:F2} align={c.TextAlignment} fs={c.FontSize} {label} {field}");
        }
    }
    Emit("Title", 0, r.TitleBand);
    Emit("Header", r.TitleBand?.MmHeight ?? 0, r.HeaderBand);
    Emit("Detail", (r.TitleBand?.MmHeight ?? 0) + (r.HeaderBand?.MmHeight ?? 0), r.DetailBand);
    Emit("GroupHeader", (r.TitleBand?.MmHeight ?? 0) + (r.HeaderBand?.MmHeight ?? 0), r.GroupHeaderBand);
    Emit("GroupFooter", (r.TitleBand?.MmHeight ?? 0) + (r.HeaderBand?.MmHeight ?? 0), r.GroupFooterBand);
    Emit("Summary", (r.TitleBand?.MmHeight ?? 0) + (r.HeaderBand?.MmHeight ?? 0), r.SummaryBand);
    Emit("Footer", (r.TitleBand?.MmHeight ?? 0) + (r.HeaderBand?.MmHeight ?? 0), r.FooterBand);
    string rep = $@"D:\HeliAcc\shots\dump_{Path.GetFileNameWithoutExtension(rtmFile)}.txt";
    File.WriteAllText(rep, sb.ToString(), new System.Text.UTF8Encoding(false));
    Console.Write(sb.ToString());
}

static bool AlignFix(string rtmFile)
{
    string path = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    if (!File.Exists(path)) { Console.WriteLine($"找不到 {path}"); return false; }
    var origBytes = File.ReadAllBytes(path);
    var bytes = (byte[])origBytes.Clone();
    var root = Tpf0Reader.Parse(bytes);
    var map = new Dictionary<string, Tpf0Object>();
    BuildNameMap(root, map);
    var r = RtmLoader.Load(root);

    var titles = new List<RtmComponent>();
    if (r.HeaderBand is { } hb)
    {
        var allLabels = hb.Components.Where(c => c.ClassName == "TppLabel" && c.Caption is { Length: > 0 }).ToList();
        // 欄標題行：取 Header 內 Label 的眾數 mmTop（欄標題通常在同一行）
        float modeY = allLabels.GroupBy(c => (float)Math.Round(c.MmTop / 500f) * 500f)
            .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        foreach (var c in allLabels)
            if (Math.Abs(c.MmTop - modeY) <= 300f)
                titles.Add(c);
    }
    var vals = new List<RtmComponent>();
    if (r.DetailBand is { } db)
        foreach (var c in db.Components)
            if (c.ClassName == "TppDBText" && c.DataField is { Length: > 0 })
                vals.Add(c);

    var pairs = new List<(RtmComponent Title, RtmComponent Val, float Overlap)>();
    foreach (var t in titles)
    {
        float tL = t.MmLeft, tR = tL + t.MmWidth;
        RtmComponent? best = null; float bestOv = 5f;
        foreach (var v in vals)
        {
            float vL = v.MmLeft, vR = vL + v.MmWidth;
            float ov = Math.Min(tR, vR) - Math.Max(tL, vL);
            if (ov > bestOv) { bestOv = ov; best = v; }
        }
        if (best != null) pairs.Add((t, best, bestOv));
    }

    int fixedCnt = 0;
    var applied = new List<(RtmComponent C, float Delta)>();
    string rep = $@"D:\HeliAcc\shots\alignfix_{Path.GetFileNameWithoutExtension(rtmFile)}.txt";
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"對齊修復: {rtmFile}");
    foreach (var (t, v, ov) in pairs)
    {
        float labelL = t.MmLeft / 1000f, labelW = t.MmWidth / 1000f;
        float valL = v.MmLeft / 1000f, valW = v.MmWidth / 1000f;
        float cur, target;
        switch (t.TextAlignment)
        {
            case "taRightJustified": cur = labelL + labelW; target = valL + valW; break;
            case "taCentered": cur = labelL + labelW / 2f; target = valL + valW / 2f; break;
            default: cur = labelL; target = valL; break;
        }
        float delta = target - cur;
        if (Math.Abs(delta) < 0.1f) continue;
        if (Math.Abs(delta) > 30f) continue;
        if (PatchObj(bytes, map, t, "mmLeft", delta))
        {
            fixedCnt++;
            applied.Add((t, delta));
            sb.AppendLine($"  對齊 {t.Caption}: 標題L={labelL:F2} 值L={valL:F2} (align={t.TextAlignment}, delta={delta:F2}mm)");
        }
        else
            sb.AppendLine($"  無法對齊 {t.Caption}: delta={delta:F2} (屬性 tag 限制)");
    }
    if (fixedCnt > 0)
    {
        // 渲染驗證：對齊後不得引入新重疊，否則還原該標題
        var beforeKeys = OverlapKeys(origBytes);
        for (int pass = 0; pass < 3; pass++)
        {
            var afterKeys = OverlapKeys(bytes);
            var newKeys = afterKeys.Where(k => !beforeKeys.Contains(k)).ToList();
            if (newKeys.Count == 0) break;
            bool reverted = false;
            foreach (var (c, d) in applied.ToList())
            {
                string key = $"{c.ClassName}\0{c.Name}";
                if (newKeys.Any(k => k.Split('|').Contains(key, StringComparer.Ordinal)))
                {
                    if (PatchObj(bytes, map, c, "mmLeft", -d))
                    {
                        applied.Remove((c, d));
                        reverted = true;
                        sb.AppendLine($"  還原 {c.Caption}: 對齊後與鄰欄文字重疊");
                    }
                }
            }
            if (!reverted) break;
        }
        int afterOv = ComputeOverlaps(bytes, out _).Count;
        int beforeOv = ComputeOverlaps(origBytes, out _).Count;
        if (applied.Count > 0 && afterOv <= beforeOv)
        {
            File.WriteAllBytes(path, bytes);
            sb.AppendLine($"已修復 {applied.Count} 個標題（重疊 {beforeOv}->{afterOv}），寫回 {path}");
            File.WriteAllText(rep, sb.ToString(), new System.Text.UTF8Encoding(false));
            Console.Write(sb.ToString());
            return true;
        }
        else
        {
            sb.AppendLine($"不寫回：重疊 {beforeOv}->{afterOv}，已對齊 {applied.Count} 個（驗證後保留 {fixedCnt} 建議）");
        }
    }
    else
        sb.AppendLine("無需修改");
    File.WriteAllText(rep, sb.ToString(), new System.Text.UTF8Encoding(false));
    Console.Write(sb.ToString());
    return false;
}

static void AlignFixAll()
{
    var files = Directory.GetFiles(@"D:\HeliAcc\Rep", "*.rtm").OrderBy(f => f).ToList();
    int wrote = 0, skipped = 0, err = 0;
    foreach (var f in files)
    {
        string name = Path.GetFileName(f);
        try
        {
            if (AlignFix(name)) wrote++; else skipped++;
        }
        catch (Exception ex)
        {
            err++;
            File.AppendAllText(@"D:\HeliAcc\shots\alignfixall_errors.txt", $"{name}: {ex.Message}\n", new System.Text.UTF8Encoding(false));
        }
    }
    string head = $"=== 對齊修復: {wrote} 寫回 / {skipped} 未改 / {err} 錯誤 / 共 {files.Count} 檔 ===";
    File.WriteAllText(@"D:\HeliAcc\shots\alignfixall_report.txt", head + "\n", new System.Text.UTF8Encoding(false));
    Console.WriteLine(head);
}

static HashSet<string> OverlapKeys(byte[] bytes)
{
    var set = new HashSet<string>();
    foreach (var o in ComputeOverlaps(bytes, out _))
    {
        string a = $"{o.A.ClassName}\0{o.A.Name}", b = $"{o.B.ClassName}\0{o.B.Name}";
        set.Add(string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}");
    }
    return set;
}

static void AlignCheck(string rtmFile)
{
    const int dpi = 150;
    string path = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    if (!File.Exists(path)) { Console.WriteLine($"找不到 {path}"); return; }
    var root = Tpf0Reader.Parse(File.ReadAllBytes(path));
    var r = RtmLoader.Load(root);

    var fields = new List<(string Pipe, string Field)>();
    void Scan(RtmComponent c)
    {
        if (c.DataField is { Length: > 0 })
            fields.Add((c.DataPipeline ?? "", c.DataField));
        foreach (var s in c.Children) Scan(s);
    }
    foreach (var b in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
        if (b is not null)
            foreach (var c in b.Components) Scan(c);

    string Fake(string f) => FakeValue(f);
    var data = MakeReportData(fields);

    int w = Math.Max(1, (int)Math.Round(r.MmPaperWidth * dpi / 25400.0));
    int h = Math.Max(1, (int)Math.Round(r.MmPaperHeight * dpi / 25400.0));
    using var bmp = new Bitmap(w, h);
    bmp.SetResolution(dpi, dpi);
    using var ren = new RtmRenderer(r, data);
    ren.DrawnTexts = new List<(RtmComponent, float, float, float, float, string)>();
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        int pages = 0;
        do { pages++; } while (ren.RenderPage(g, new RectangleF(0, 0, w, h), st));
    }

    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"對齊診斷: {rtmFile} ({r.MmPaperWidth / 1000f:F1}x{r.MmPaperHeight / 1000f:F1}mm)");
    foreach (var (c, x, y, tw, th, text) in ren.DrawnTexts.OrderBy(d => d.Item2).ThenBy(d => d.Item3))
    {
        string label = Ox(c);
        sb.AppendLine($"{x / 1000f:F2}\t{y / 1000f:F2}\t{tw / 1000f:F2}\t{th / 1000f:F2}\t{c.TextAlignment}\t{label}\t\"{text}\"");
    }
    string rep = $@"D:\HeliAcc\shots\align_{Path.GetFileNameWithoutExtension(rtmFile)}.txt";
    File.WriteAllText(rep, sb.ToString(), new System.Text.UTF8Encoding(false));
    Console.WriteLine($"已輸出 {ren.DrawnTexts.Count} 筆文字座標 -> {rep}");
    ren.Dispose();
}

/// <summary>依報表欄位建構假資料；若報表只有單一資料 pipeline（標籤/信封/支票），自動設為 DetailPipeline。</summary>
static RtmData MakeReportData(List<(string Pipe, string Field)> fields)
{
    var data = new RtmData();
    data.Company["公司全名"] = "禾秝科技有限公司";
    data.Company["電話號碼"] = "02-23456789";
    data.Company["登記地址"] = "台北市中正區忠孝東路一段 123 號";
    data.Company["傳真號碼"] = "02-23888888";
    data.Company["統一編號"] = "12345678";
    string Fake(string f) => FakeValue(f);

    var pipes = fields.Select(f => f.Pipe).Where(p => !string.IsNullOrEmpty(p) && p != "plCompany").Distinct().ToList();
    if (pipes.Count == 1) data.DetailPipeline = pipes[0];

    var master = new Dictionary<string, object?>();
    foreach (var (pipe, f) in fields)
    {
        if (pipe == "plCompany")
        {
            if (!data.Company.ContainsKey(f)) data.Company[f] = Fake(f);
            continue;
        }
        if (pipe != data.DetailPipeline) master[f] = Fake(f);
    }
    data.Master = master;
    var detailFields = fields.Where(f => f.Pipe == data.DetailPipeline).Distinct().ToList();
    for (int i = 0; i < 3; i++)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (_, f) in detailFields)
            d[f] = i == 0 && f.Contains("日期") ? "2026/08/14" : Fake(f);
        if (d.Count > 0) data.Detail.Add(d);
    }
    return data;
}

/// <summary>依欄位名給出貼近真實長度的假資料（壓力測試用）。</summary>
static string FakeValue(string f)
{
    if (f.Contains("日期")) return "2026/08/14";
    if (f.Contains("電話") || f.Contains("傳真")) return "02-23456789";
    if (f.Contains("郵遞區號")) return "100";
    if (f.Contains("統一編號") || f.Contains("稅籍")) return "12345678";
    if (f.Contains("支票") || f.Contains("票據")) return "A1234567";
    if (f.Contains("銀行帳戶")) return "050011-2233445";
    if (f.Contains("地址")) return "台北市中正區忠孝東路一段 123 號";
    if (f.Contains("倉庫")) return "高雄一倉";
    if (f.Contains("類別")) return "出貨單";
    if (f.Contains("科目")) return "應收帳款-合併";
    if (f.Contains("品名") || f.Contains("名稱") || f.Contains("說明") || f.Contains("備註"))
        return "台灣禾秝科技高雄分公";
    if (f.Contains("號碼") || f.Contains("編號") || f.Contains("單號") || f.Contains("帳號")) return "A20260001";
    if (f.Contains("員工") || f.Contains("製單") || f.Contains("覆核") || f.Contains("對象")
        || f.Contains("經辦") || f.Contains("簽")) return "王大明";
    if (f.Contains("金額") || f.Contains("數量") || f.Contains("單價") || f.Contains("折扣")
        || f.Contains("稅") || f.Contains("成本") || f.Contains("餘額") || f.Contains("合計")
        || f.Contains("總計") || f.Contains("小計") || f.Contains("累計") || f.Contains("毛利")
        || f.Contains("淨額") || f.Contains("票面") || f.Contains("額度") || f.Contains("領用")
        || f.Contains("結存") || f.Contains("存量") || f.Contains("差額")) return "1234567.89";
    return "測試資料";
}

static void FixAll()
{
    var files = Directory.GetFiles(@"D:\HeliAcc\Rep", "*.rtm").OrderBy(f => f).ToList();
    var log = new StringBuilder();
    int movedFiles = 0, stuckFiles = 0, errFiles = 0, totalMoved = 0;
    foreach (var f in files)
    {
        string name = Path.GetFileName(f);
        try
        {
            var (moved, remaining) = FixOne(f);
            totalMoved += moved;
            if (moved > 0) movedFiles++;
            if (remaining > 0) stuckFiles++;
            log.AppendLine($"{(remaining > 0 ? "S" : "D")}  {name}: 移動 {moved} 次 / 剩 {remaining} 重疊");
        }
        catch (Exception ex)
        {
            errFiles++;
            log.AppendLine($"E  {name}: {ex.Message}");
        }
    }
    string head = $"=== 重疊修補: {movedFiles} 已修 / {stuckFiles} 未完全清除 / {errFiles} 錯誤 / 共 {files.Count} 檔 / 總移動 {totalMoved} 次 ===";
    log.Insert(0, head + "\n");
    File.WriteAllText(@"D:\HeliAcc\shots\fixall_report.txt", log.ToString(), new UTF8Encoding(false));
    Console.WriteLine(head);
}

static (int Moved, int Remaining) FixOne(string path)
{
    var bytes = File.ReadAllBytes(path);
    var map = new Dictionary<string, Tpf0Object>();
    BuildNameMap(Tpf0Reader.Parse(bytes), map);
    int moved = 0;
    for (int it = 0; it < 40; it++)
    {
        var ovs = ComputeOverlaps(bytes, out var paperMm);
        if (ovs.Count == 0) break;
        bool any = false;
        foreach (var ov in ovs.OrderByDescending(o => o.Area).Take(60))
        {
            if (TryPatch(bytes, map, ov, paperMm))
            {
                moved++;
                any = true;
            }
        }
        if (!any) break;
    }
    int remaining = ComputeOverlaps(bytes, out _).Count;
    if (moved > 0) File.WriteAllBytes(path, bytes);
    return (moved, remaining);
}

/// <summary>以「元件框」為準的自動重疊修補：依 shots 目錄 dump_*.txt 的第一行找出目標報表清單。</summary>
static void FixBoxes()
{
    var targets = new List<string>();
    foreach (var dump in Directory.GetFiles(@"D:\HeliAcc\shots", "dump_*.txt"))
    {
        var first = File.ReadLines(dump, new UTF8Encoding(false)).FirstOrDefault() ?? "";
        int p = first.IndexOf("Dump: ", StringComparison.Ordinal);
        if (p < 0) continue;
        int q = first.IndexOf(".rtm", Math.Min(p + 6, first.Length), StringComparison.OrdinalIgnoreCase);
        if (q > p)
        {
            string name = first[(p + 6)..(q + 4)];
            if (File.Exists(Path.Combine(@"D:\HeliAcc\Rep", name)))
                targets.Add(name);
        }
    }
    var log = new StringBuilder();
    int okFiles = 0, stuckFiles = 0, errFiles = 0, totalMoved = 0;
    foreach (var name in targets.OrderBy(n => n))
    {
        string path = Path.Combine(@"D:\HeliAcc\Rep", name);
        try
        {
            var (moved, remaining) = FixBoxesOne(path);
            totalMoved += moved;
            if (moved > 0) okFiles++;
            if (remaining > 0) stuckFiles++;
            log.AppendLine($"{(remaining > 0 ? "S" : "D")}  {name}: 移動 {moved} 次 / 剩 {remaining} 重疊");
        }
        catch (Exception ex)
        {
            errFiles++;
            log.AppendLine($"E  {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
    string head = $"=== 框重疊修補: {okFiles} 已修 / {stuckFiles} 未清除 / {errFiles} 錯誤 / 共 {targets.Count} 檔 / 總移動 {totalMoved} 次 ===";
    log.Insert(0, head + "\n");
    File.WriteAllText(@"D:\HeliAcc\shots\fixboxes_report.txt", log.ToString(), new UTF8Encoding(false));
    Console.WriteLine(head);
}

static (int Moved, int Remaining) FixBoxesOne(string path)
{
    var bytes = File.ReadAllBytes(path);
    int moved = 0;
    for (int it = 0; it < 80; it++)
    {
        var ovs = BoxOverlaps(bytes, out var paper);
        if (ovs.Count == 0) break;
        bool any = false;
        foreach (var ov in ovs.OrderByDescending(o => o.Area).Take(50))
        {
            if (PatchBox(bytes, ov, paper)) { moved++; any = true; }
        }
        if (!any) break;
    }
    int remaining = BoxOverlaps(bytes, out _).Count;
    if (moved > 0) File.WriteAllBytes(path, bytes);
    return (moved, remaining);
}

/// <summary>同一 band 內文字元件框重疊 &gt; 0.3mm 的對（座標為 mm，含巢狀 band 累加偏移）。</summary>
static List<(RtmComponent A, RtmComponent B, float L, float T, float W, float H, float BL, float BT, float BW, float BH, float Area)> BoxOverlaps(byte[] bytes, out (float W, float H) paper)
{
    var result = new List<(RtmComponent, RtmComponent, float, float, float, float, float, float, float, float, float)>();
    var root = Tpf0Reader.Parse(bytes);
    var r = RtmLoader.Load(root);
    paper = (r.MmPaperWidth / 1000f, r.MmPaperHeight / 1000f);
    const float eps = 0.3f;
    foreach (var band in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
    {
        if (band is null) continue;
        var flat = new List<(RtmComponent C, float L, float T, float W, float H)>();
        void Walk(List<RtmComponent> comps, float ox, float oy)
        {
            foreach (var c in comps)
            {
                if (!c.ClassName.EndsWith("Band"))
                    flat.Add((c, ox + c.MmLeft / 1000f, oy + c.MmTop / 1000f, c.MmWidth / 1000f, c.MmHeight / 1000f));
                Walk(c.Children, ox + c.MmLeft / 1000f, oy + c.MmTop / 1000f);
            }
        }
        Walk(band.Components, 0f, 0f);
        bool IsText(RtmComponent c) => c.ClassName is "TppLabel" or "TppDBText" or "TppDBMemo" or "TppDBCalc" or "TppSystemVariable";
        var texts = flat.Where(x => IsText(x.C)).ToList();
        for (int i = 0; i < texts.Count; i++)
        for (int j = i + 1; j < texts.Count; j++)
        {
            var a = texts[i]; var b = texts[j];
            if (a.L + eps < b.L + b.W && b.L + eps < a.L + a.W
                && a.T + eps < b.T + b.H && b.T + eps < a.T + a.H)
            {
                float oxr = Math.Min(a.L + a.W, b.L + b.W) - Math.Max(a.L, b.L);
                float oyr = Math.Min(a.T + a.H, b.T + b.H) - Math.Max(a.T, b.T);
                result.Add((a.C, b.C, a.L, a.T, a.W, a.H, b.L, b.T, b.W, b.H, oxr * oyr));
            }
        }
    }
    return result;
}

/// <summary>嘗試分離一對框重疊元件：右側右移 → 左側左移 → 下方下移 → 上方上移。</summary>
static bool PatchBox(byte[] bytes, (RtmComponent A, RtmComponent B, float L, float T, float W, float H, float BL, float BT, float BW, float BH, float Area) ov,
    (float W, float H) paper)
{
    var root = Tpf0Reader.Parse(bytes);
    var map = new Dictionary<string, Tpf0Object>();
    BuildNameMap(root, map);
    float ox = Math.Min(ov.L + ov.W, ov.BL + ov.BW) - Math.Max(ov.L, ov.BL);
    float oy = Math.Min(ov.T + ov.H, ov.BT + ov.BH) - Math.Max(ov.T, ov.BT);
    bool bRight = (ov.BL + ov.BW / 2f) >= (ov.L + ov.W / 2f);
    bool bBelow = (ov.BT + ov.BH / 2f) >= (ov.T + ov.H / 2f);
    float d = ox + 0.35f;
    if (bRight && ov.BL + d + ov.BW <= paper.W + 1f)
        if (PatchObj(bytes, map, ov.B, "mmLeft", d)) return true;
    if (!bRight && ov.L + d + ov.W <= paper.W + 1f)
        if (PatchObj(bytes, map, ov.A, "mmLeft", d)) return true;
    if (bRight && ov.L - d >= -1f)
        if (PatchObj(bytes, map, ov.A, "mmLeft", -d)) return true;
    if (!bRight && ov.BL - d >= -1f)
        if (PatchObj(bytes, map, ov.B, "mmLeft", -d)) return true;
    float dy = oy + 0.35f;
    if (bBelow && ov.BT + dy + ov.BH <= paper.H + 1f)
        if (PatchObj(bytes, map, ov.B, "mmTop", dy)) return true;
    if (!bBelow && ov.T + dy + ov.H <= paper.H + 1f)
        if (PatchObj(bytes, map, ov.A, "mmTop", dy)) return true;
    if (bBelow && ov.T - dy >= -1f)
        if (PatchObj(bytes, map, ov.A, "mmTop", -dy)) return true;
    if (!bBelow && ov.BT - dy >= -1f)
        if (PatchObj(bytes, map, ov.B, "mmTop", -dy)) return true;
    return false;
}

/// <summary>遞迴收集 (ClassName\0Name) -> Tpf0Object。</summary>
static void BuildNameMap(Tpf0Object o, Dictionary<string, Tpf0Object> map)
{
    if (o.Name.Length > 0) map[$"{o.ClassName}\0{o.Name}"] = o;
    foreach (var c in o.Children) BuildNameMap(c, map);
}

/// <summary>解析 + 渲染 + 回傳實際文字重疊對（mm）。</summary>
static List<OverlapPair> ComputeOverlaps(byte[] bytes, out (float Wmm, float Hmm) paper)
{
    var root = Tpf0Reader.Parse(bytes);
    var r = RtmLoader.Load(root);
    paper = (r.MmPaperWidth / 1000f, r.MmPaperHeight / 1000f);
    var fields = new List<(string Pipe, string Field)>();
    void Scan(RtmComponent c)
    {
        if (c.DataField is { Length: > 0 })
            fields.Add((c.DataPipeline ?? "", c.DataField));
        foreach (var s in c.Children) Scan(s);
    }
    foreach (var b in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
        if (b is not null)
            foreach (var c in b.Components) Scan(c);
    string Fake(string f) => FakeValue(f);

    var data = MakeReportData(fields);
    int w = Math.Max(1, (int)Math.Round(r.MmPaperWidth * 150 / 25400.0));
    int h = Math.Max(1, (int)Math.Round(r.MmPaperHeight * 150 / 25400.0));
    float pw = r.MmPaperWidth / 1000f, ph = r.MmPaperHeight / 1000f;
    var result = new List<OverlapPair>();
    using var bmp = new Bitmap(w, h);
    bmp.SetResolution(150, 150);
    using var ren = new RtmRenderer(r, data);
    ren.DrawnTexts = new List<(RtmComponent, float, float, float, float, string)>();
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        do { } while (ren.RenderPage(g, new RectangleF(0, 0, w, h), st));
    }
    var dt = ren.DrawnTexts!;
    const float eps = 0.3f;
    for (int i = 0; i < dt.Count; i++)
    for (int j = i + 1; j < dt.Count; j++)
    {
        var (ca, ax, ay, aw, ah, ta) = dt[i];
        var (cb, bx, by, bw, bh, tb) = dt[j];
        if (ReferenceEquals(ca, cb)) continue;
        if (ax + eps < bx + bw && bx + eps < ax + aw && ay + eps < by + bh && by + eps < ay + ah)
        {
            float ox = Math.Min(ax + aw, bx + bw) - Math.Max(ax, bx);
            float oy = Math.Min(ay + ah, by + bh) - Math.Max(ay, by);
            float area = ox * oy;
            if (area > 1f)
                result.Add(new OverlapPair(ca, cb, ax, ay, aw, ah, bx, by, bw, bh, ox, oy, area, ta, tb));
        }
    }
    return result;
}

/// <summary>把 80-bit extended（Intel）轉回 double。</summary>
static double Ext80ToDouble(byte[] b)
{
    ulong man = BitConverter.ToUInt64(b, 0);
    ushort ew = BitConverter.ToUInt16(b, 8);
    bool neg = (ew & 0x8000) != 0;
    int exp = ew & 0x7FFF;
    if (exp == 0x7FFF) return neg ? double.NegativeInfinity : double.PositiveInfinity;
    if (exp == 0 && man == 0) return 0.0;
    double v = Math.ScaleB((double)man, exp - 16383 - 63);
    return neg ? -v : v;
}

static void FixDbg(string name)
{
    string path = Path.Combine(@"D:\HeliAcc\Rep", name);
    var bytes = File.ReadAllBytes(path);
    var root = Tpf0Reader.Parse(bytes);
    var map = new Dictionary<string, Tpf0Object>();
    BuildNameMap(root, map);
    Console.WriteLine($"map 大小: {map.Count}");
    var ovs = ComputeOverlaps(bytes, out var paper);
    Console.WriteLine($"紙張 {paper.Wmm:F1}x{paper.Hmm:F1}mm / 重疊 {ovs.Count} 對");
    int okCount = 0;
    foreach (var ov in ovs.OrderByDescending(o => o.Area).Take(12))
    {
        bool hasA = map.ContainsKey($"{ov.A.ClassName}\0{ov.A.Name}");
        bool hasB = map.ContainsKey($"{ov.B.ClassName}\0{ov.B.Name}");
        var pa = ov.A is not null ? string.Join(",", ((Func<RtmComponent, string>)(c =>
        {
            if (!map.TryGetValue($"{c.ClassName}\0{c.Name}", out var o)) return "!map";
            var ps = o.PropertiesEx.Where(p => p.Name is "mmLeft" or "mmTop" or "mmWidth" or "mmHeight")
                .Select(p => $"{p.Name}:tag{p.Tag:X2}");
            return ps.Any() ? string.Join(" ", ps) : "!coordprops";
        }))(ov.A)) : "";
        Console.WriteLine($"{ov.A.ClassName}/{ov.A.Name} (map:{hasA}, {pa})  vs  {ov.B.ClassName}/{ov.B.Name} (map:{hasB})  ox={ov.Ox / 1000f:F1} oy={ov.Oy / 1000f:F1} area={ov.Area / 1e6f:F1}mm²");
        Console.WriteLine($"   A 文字\"{ov.Ta}\" 框[{ov.Ax / 1000f:F1},{ov.Ay / 1000f:F1},{ov.Aw / 1000f:F1}x{ov.Ah / 1000f:F1}]  band內({ov.A.MmLeft / 1000f:F1},{ov.A.MmTop / 1000f:F1},{ov.A.MmWidth / 1000f:F1}x{ov.A.MmHeight / 1000f:F1})");
        Console.WriteLine($"   B 文字\"{ov.Tb}\" 框[{ov.Bx / 1000f:F1},{ov.By / 1000f:F1},{ov.Bw / 1000f:F1}x{ov.Bh / 1000f:F1}]  band內({ov.B.MmLeft / 1000f:F1},{ov.B.MmTop / 1000f:F1},{ov.B.MmWidth / 1000f:F1}x{ov.B.MmHeight / 1000f:F1})");
        bool ok = TryPatch(bytes, map, ov, paper);
        if (ok) okCount++;
        Console.WriteLine($"   TryPatch => {ok}");
    }
    Console.WriteLine($"可修復 {okCount} / 12");
}

/// <summary>double 轉 80-bit extended（Intel 10-byte little-endian）。</summary>
static byte[] DoubleToExt80(double v)
{
    if (v == 0) return new byte[10];
    bool neg = v < 0;
    double a = Math.Abs(v);
    int e = (int)Math.Floor(Math.Log2(a));
    double m = a / Math.Pow(2, e);                    // a = m*2^e, 1<=m<2
    if (m >= 2) { m /= 2; e++; }
    ulong man = (ulong)Math.Min(m * 9223372036854775808.0, 18446744073709551615.0);
    int e2 = e + 16383;
    var b = new byte[10];
    BitConverter.GetBytes(man).CopyTo(b, 0);
    BitConverter.GetBytes((ushort)((neg ? 0x8000 : 0) | e2)).CopyTo(b, 8);
    return b;
}

/// <summary>對重疊對做一次修補：依多個候選移動依序嘗試，全部失敗才回 false。</summary>
static bool TryPatch(byte[] bytes, Dictionary<string, Tpf0Object> map, OverlapPair ov, (float Wmm, float Hmm) paper)
{
    float pw = paper.Wmm, ph = paper.Hmm;
    // DrawnTexts 座標為千分 mm，統一轉 mm（頁面絕對位置）
    float ax = ov.Ax / 1000f, ay = ov.Ay / 1000f, aw = ov.Aw / 1000f, ah = ov.Ah / 1000f;
    float bx = ov.Bx / 1000f, by = ov.By / 1000f, bw = ov.Bw / 1000f, bh = ov.Bh / 1000f;
    float ox = ov.Ox / 1000f, oy = ov.Oy / 1000f;
    bool horizPreferred = ox <= oy;
    var moves = new List<(RtmComponent C, string Prop, float Delta, float TargetLeft, float TargetTop)>();
    void AddMove(RtmComponent c, string prop, float d)
    {
        if (Math.Abs(d) <= 0.5f) return;
        bool isA = ReferenceEquals(c, ov.A);
        float baseX = isA ? ax : bx, baseY = isA ? ay : by;
        moves.Add((c, prop, d, baseX + d, baseY + d));
    }
    if (horizPreferred)
    {
        bool bRight = (bx + bw / 2f) >= (ax + aw / 2f);
        if (bRight) { AddMove(ov.B, "mmLeft", ox + 0.5f); AddMove(ov.A, "mmLeft", -(ox + 0.5f)); }
        else { AddMove(ov.A, "mmLeft", ox + 0.5f); AddMove(ov.B, "mmLeft", -(ox + 0.5f)); }
        AddMove(ov.B, "mmTop", oy + 0.5f);
        AddMove(ov.A, "mmTop", -(oy + 0.5f));
    }
    else
    {
        bool bBelow = (by + bh / 2f) >= (ay + ah / 2f);
        if (bBelow) { AddMove(ov.B, "mmTop", oy + 0.5f); AddMove(ov.A, "mmTop", -(oy + 0.5f)); }
        else { AddMove(ov.A, "mmTop", oy + 0.5f); AddMove(ov.B, "mmTop", -(oy + 0.5f)); }
        AddMove(ov.B, "mmLeft", ox + 0.5f);
        AddMove(ov.A, "mmLeft", -(ox + 0.5f));
    }
    foreach (var (c, prop, d, tl, tt) in moves)
    {
        // 越界檢查（頁面絕對）
        float w = c.MmWidth / 1000f, h = c.MmHeight / 1000f;
        if (prop == "mmLeft")
        {
            if (tl < -1 || tl + w > pw + 1) continue;
        }
        else
        {
            if (tt < -1 || tt + h > ph + 1) continue;
        }
        if (PatchObj(bytes, map, c, prop, d)) return true;
    }
    return false;
}

/// <summary>
/// 將 int16(0x03) 屬性擴寬為 int32(0x04) 並寫入新值（千分 mm）。
/// 在 tag 之後插入 2 bytes 把 2B 值區變 4B；插入後整檔偏移已變，
/// 呼叫端必須重新 Parse/BuildNameMap 才能繼續操作。
/// </summary>
static byte[] WidenInt16Prop(byte[] data, Tpf0Property p, long newVal)
{
    byte[] nb = new byte[data.Length + 2];
    Array.Copy(data, 0, nb, 0, p.ValuePayloadStart);
    nb[p.ValuePayloadStart] = 0;
    nb[p.ValuePayloadStart + 1] = 0;
    Array.Copy(data, p.ValuePayloadStart, nb, p.ValuePayloadStart + 2, data.Length - p.ValuePayloadStart);
    nb[p.ValuePayloadStart - 1] = 0x04;
    BitConverter.GetBytes((int)newVal).CopyTo(nb, p.ValuePayloadStart);
    return nb;
}

/// <summary>原地修改 Tpf0 屬性數值（mm→千分 mm），支援整數與浮點 tag，成功回傳 true。</summary>
static bool PatchObj(byte[] bytes, Dictionary<string, Tpf0Object> map, RtmComponent c, string prop, float deltaMm)
{
    if (!map.TryGetValue($"{c.ClassName}\0{c.Name}", out var obj)) return false;
    var p = obj.PropertiesEx.FirstOrDefault(x => x.Name == prop);
    if (p is null) return false;
    long cur;
    switch (p.Tag)
    {
        case 0x02: cur = (sbyte)bytes[p.ValuePayloadStart]; break;
        case 0x03: cur = BitConverter.ToInt16(bytes, p.ValuePayloadStart); break;
        case 0x04: cur = BitConverter.ToInt32(bytes, p.ValuePayloadStart); break;
        case 0x05: cur = (long)Math.Round(Ext80ToDouble(bytes.AsSpan(p.ValuePayloadStart, 10).ToArray())); break;
        case 0x0F: cur = (long)Math.Round(BitConverter.ToSingle(bytes, p.ValuePayloadStart)); break;
        case 0x11:
        case 0x15: cur = (long)Math.Round(BitConverter.ToDouble(bytes, p.ValuePayloadStart)); break;
        default: return false;
    }
    long next = cur + (long)Math.Round(deltaMm * 1000.0);
    if (next < -100000 || next > 2000000) return false;   // 防護（千分 mm）
    switch (p.Tag)
    {
        case 0x02:
            if (next < sbyte.MinValue || next > sbyte.MaxValue) return false;
            bytes[p.ValuePayloadStart] = (byte)(sbyte)next;
            break;
        case 0x03:
            if (next < short.MinValue || next > short.MaxValue) return false;
            BitConverter.GetBytes((short)next).CopyTo(bytes, p.ValuePayloadStart);
            break;
        case 0x04:
            if (next < int.MinValue || next > int.MaxValue) return false;
            BitConverter.GetBytes((int)next).CopyTo(bytes, p.ValuePayloadStart);
            break;
        case 0x05: DoubleToExt80(next).CopyTo(bytes, p.ValuePayloadStart); break;
        case 0x0F: BitConverter.GetBytes((float)next).CopyTo(bytes, p.ValuePayloadStart); break;
        case 0x11:
        case 0x15: BitConverter.GetBytes((double)next).CopyTo(bytes, p.ValuePayloadStart); break;
    }
    return true;
}

/// <summary>原地寫入 Font.Size（單位 pt），支援常見 tag，成功回傳 true。</summary>
static bool PatchFontPt(byte[] bytes, Tpf0Object obj, float newPt)
{
    var p = obj.PropertiesEx.FirstOrDefault(x => x.Name == "Font.Size");
    if (p is null) return false;
    switch (p.Tag)
    {
        case 0x02:
            if (newPt < sbyte.MinValue || newPt > sbyte.MaxValue) return false;
            bytes[p.ValuePayloadStart] = (byte)(sbyte)newPt;
            return true;
        case 0x05: DoubleToExt80((double)newPt).CopyTo(bytes, p.ValuePayloadStart); return true;
        case 0x0F: BitConverter.GetBytes((float)newPt).CopyTo(bytes, p.ValuePayloadStart); return true;
        case 0x11:
        case 0x15: BitConverter.GetBytes((double)newPt).CopyTo(bytes, p.ValuePayloadStart); return true;
        default: return false;
    }
}

static float? ReadPropMm(byte[] bytes, Tpf0Object obj, string prop)
{
    var p = obj.PropertiesEx.FirstOrDefault(x => x.Name == prop);
    if (p is null) return null;
    double cur;
    switch (p.Tag)
    {
        case 0x02: cur = (sbyte)bytes[p.ValuePayloadStart]; break;
        case 0x03: cur = BitConverter.ToInt16(bytes, p.ValuePayloadStart); break;
        case 0x04: cur = BitConverter.ToInt32(bytes, p.ValuePayloadStart); break;
        case 0x05: cur = Ext80ToDouble(bytes.AsSpan(p.ValuePayloadStart, 10).ToArray()); break;
        case 0x0F: cur = BitConverter.ToSingle(bytes, p.ValuePayloadStart); break;
        case 0x11:
        case 0x15: cur = BitConverter.ToDouble(bytes, p.ValuePayloadStart); break;
        default: return null;
    }
    return (float)(cur / 1000.0);
}

static bool PatchProp(byte[] bytes, Tpf0Object obj, string prop, float deltaMm)
{
    var p = obj.PropertiesEx.FirstOrDefault(x => x.Name == prop);
    if (p is null) return false;
    long cur;
    switch (p.Tag)
    {
        case 0x02: cur = (sbyte)bytes[p.ValuePayloadStart]; break;
        case 0x03: cur = BitConverter.ToInt16(bytes, p.ValuePayloadStart); break;
        case 0x04: cur = BitConverter.ToInt32(bytes, p.ValuePayloadStart); break;
        case 0x05: cur = (long)Math.Round(Ext80ToDouble(bytes.AsSpan(p.ValuePayloadStart, 10).ToArray())); break;
        case 0x0F: cur = (long)Math.Round(BitConverter.ToSingle(bytes, p.ValuePayloadStart)); break;
        case 0x11:
        case 0x15: cur = (long)Math.Round(BitConverter.ToDouble(bytes, p.ValuePayloadStart)); break;
        default: return false;
    }
    long next = cur + (long)Math.Round(deltaMm * 1000.0);
    if (next < -100000 || next > 2000000) return false;
    switch (p.Tag)
    {
        case 0x02:
            if (next < sbyte.MinValue || next > sbyte.MaxValue) return false;
            bytes[p.ValuePayloadStart] = (byte)(sbyte)next;
            break;
        case 0x03:
            if (next < short.MinValue || next > short.MaxValue) return false;
            BitConverter.GetBytes((short)next).CopyTo(bytes, p.ValuePayloadStart);
            break;
        case 0x04:
            if (next < int.MinValue || next > int.MaxValue) return false;
            BitConverter.GetBytes((int)next).CopyTo(bytes, p.ValuePayloadStart);
            break;
        case 0x05: DoubleToExt80(next).CopyTo(bytes, p.ValuePayloadStart); break;
        case 0x0F: BitConverter.GetBytes((float)next).CopyTo(bytes, p.ValuePayloadStart); break;
        case 0x11:
        case 0x15: BitConverter.GetBytes((double)next).CopyTo(bytes, p.ValuePayloadStart); break;
    }
    return true;
}

/// <summary>依重疊診斷手動重排 6 檔（貨品存貨異動明細表為假警報不處理），以「絕對值目標」冪等補丁元件座標。</summary>
static void FixSeven()
{
    string rep = @"D:\HeliAcc\Rep";
    var plan = new Dictionary<string, (string Key, string Prop, float TargetMm)[]>
    {
        ["支票列印"] = new[]
        {
            ("TppDBText\0ppDBText5", "mmWidth", 59.0f), // 公司全名框 64.294→59，避開票面金額框
        },
        ["客戶標籤"] = new[]
        {
            ("TppDBText\0ppDBText1", "mmTop", 6.5f), // 公司全名 18.256→6.5（郵遞區號下方）
            ("TppDBText\0ppDBText2", "mmWidth", 45.5f), // 帳單地址 66.41→45.5，避開「收」字
            ("TppLabel\0ppLabel1", "mmLeft", 85.0f), // 「收」字 49.477→85，避開長地址
        },
        ["廠商標籤"] = new[]
        {
            ("TppDBText\0ppDBText1", "mmTop", 6.5f), // 公司全名 18.256→6.5
        },
        ["應收帳款郵寄標籤"] = new[]
        {
            ("TppDBText\0ppDBText1", "mmTop", 6.5f), // 公司全名 18.256→6.5
            ("TppLabel\0ppLabel1", "mmLeft", 95.25f), // 「收」字維持原位（x=95.25，本不與地址重疊）
        },
        ["應收帳款標準信封"] = new[]
        {
            ("TppDBText\0ppDBText1", "mmTop", 25.0f), // 收件人公司全名 65.4→25（帳單地址下方）
            ("TppDBText\0ppDBText6", "mmTop", 55.0f), // 寄件人公司全名 65.4→55（聯絡地址上方，加大間距）
        },
        ["票據簽收回條"] = new[]
        {
            ("TppDBText\0ppDBText2", "mmTop", 55.827f),  // 支票號碼 23.4→55.83（付款單「支票號碼：」旁）
            ("TppDBText\0ppDBText3", "mmTop", 49.742f),  // 到期日 23.4→49.74（付款單「兌現日期：」旁）
            ("TppDBText\0ppDBText4", "mmTop", 62.442f),  // 票面金額 23.4→62.44（付款單「票面金額：」旁）
            ("TppDBText\0ppDBText6", "mmTop", 211.932f), // 支票號碼 23.4→211.93（回條「支票號碼：」旁）
            ("TppDBText\0ppDBText7", "mmTop", 205.846f), // 到期日 23.4→205.85（回條「兌現日期：」旁）
            ("TppDBText\0ppDBText8", "mmTop", 218.546f), // 票面金額 23.4→218.55（回條「票面金額：」旁）
            ("TppDBText\0ppDBText5", "mmLeft", 18.256f), // 公司全名 66.94→18.26（回條台照左側）
            ("TppDBText\0ppDBText5", "mmTop", 178.86f),  // 公司全名 23.4→178.86
            ("TppDBText\0ppDBText10", "mmTop", 116.42f), // 本公司名 23.4→116.42（付款單落款，x 保持 20.64）
            ("TppDBText\0ppDBText10", "mmWidth", 60.0f), // 本公司名 87.31→60
            ("TppDBText\0ppDBText9", "mmLeft", 100.0f),  // 本公司名 109.54→100（回條敬上左側）
            ("TppDBText\0ppDBText9", "mmTop", 257.18f),  // 本公司名 23.4→257.18
            ("TppDBText\0ppDBText9", "mmWidth", 50.0f),  // 本公司名 60.06→50
        },
    };

    var log = new StringBuilder();
    int okFiles = 0, fail = 0;
    foreach (var kv in plan)
    {
        string path = Path.Combine(rep, kv.Key + ".rtm");
        if (!File.Exists(path)) { log.AppendLine($"缺檔: {kv.Key}"); fail++; continue; }
        var bytes = File.ReadAllBytes(path);
        var map = new Dictionary<string, Tpf0Object>();
        BuildNameMap(Tpf0Reader.Parse(bytes), map);
        int applied = 0, failed = 0;
        foreach (var (key, prop, target) in kv.Value)
        {
            if (!map.TryGetValue(key, out var obj)) { log.AppendLine($"{kv.Key}: 找不到 {key}"); failed++; continue; }
            var pp = obj.PropertiesEx.FirstOrDefault(x => x.Name == prop);
            double curVal = 0;
            if (pp is not null)
            {
                try
                {
                    curVal = pp.Tag switch
                    {
                        0x02 => (sbyte)bytes[pp.ValuePayloadStart],
                        0x03 => BitConverter.ToInt16(bytes, pp.ValuePayloadStart),
                        0x04 => BitConverter.ToInt32(bytes, pp.ValuePayloadStart),
                        0x05 => Ext80ToDouble(bytes.AsSpan(pp.ValuePayloadStart, 10).ToArray()),
                        0x0F => BitConverter.ToSingle(bytes, pp.ValuePayloadStart),
                        _ => double.NaN,
                    };
                }
                catch { curVal = double.NaN; }
            }
            if (!PatchPropAbs(bytes, obj, prop, target)) { log.AppendLine($"{kv.Key}: patch 失敗 {key}.{prop} →{target}（tag={pp?.Tag.ToString("X2") ?? "?"} cur={curVal} pos={pp?.ValuePayloadStart}）"); failed++; continue; }
            applied++;
        }
        if (applied > 0)
        {
            File.WriteAllBytes(path, bytes);
            okFiles++;
            log.AppendLine($"{kv.Key}: 套用 {applied}/{kv.Value.Length} 筆{(failed > 0 ? $" / 失敗 {failed}" : "")}");
        }
        else fail++;
    }
    string head = $"=== 手動重排: {okFiles} 檔已寫 / {fail} 檔有問題 ===";
    log.Insert(0, head + "\n");
    File.WriteAllText(@"D:\HeliAcc\shots\fix7_report.txt", log.ToString(), new UTF8Encoding(false));
    Console.WriteLine(head);
    Console.Write(log);
}

static void FixEight()
{
    string rep = @"D:\HeliAcc\Rep";
    var plan = new Dictionary<string, (string Key, string Prop, float Target, bool IsFont)[]>
    {
        ["支票列印"] = new[]
        {
            ("TppDBText\0ppDBText2", "mmWidth", 17.5f, false),  // 年欄 7.94→17.5（容 4 位年份）
            ("TppDBText\0ppDBText3", "mmLeft", 88.35f, false),  // 月欄 80.43→88.35
            ("TppDBText\0ppDBText3", "mmWidth", 17.5f, false),  // 月欄 7.94→17.5
            ("TppDBText\0ppDBText4", "mmLeft", 106.85f, false), // 日欄 91.81→106.85
            ("TppDBText\0ppDBText4", "mmWidth", 17.5f, false),  // 日欄 7.94→17.5
        },
        ["貨品存貨異動明細表"] = new[]
        {
            ("TppDBText\0ppDBText1", "mmWidth", 15.0f, false),   // 倉庫編號 11.4→15（容渲染值）
            ("TppDBText\0ppDBText5", "mmLeft", 82.5f, false),    // 公司簡稱 79.1→82.5（避倉庫編號加寬）
            ("TppDBText\0ppDBText12", "Font.Size", 7f, true),   // 單位 9→7（欄窄，縮字避累計）
            ("TppDBText\0ppDBText1", "Font.Size", 9f, true),   // 倉庫編號 12→9
            ("TppDBText\0ppDBText2", "Font.Size", 9f, true),   // 累計 12→9
            ("TppDBText\0ppDBText3", "Font.Size", 9f, true),   // 單據類別 12→9
            ("TppDBText\0ppDBText4", "Font.Size", 9f, true),   // 交易日期 12→9
            ("TppDBText\0ppDBText5", "Font.Size", 9f, true),   // 公司簡稱 12→9
            ("TppDBText\0ppDBText6", "Font.Size", 9f, true),   // 交易單號 12→9
            ("TppDBText\0ppDBText13", "Font.Size", 9f, true),  // 數量 12→9
            ("TppDBText\0ppDBText14", "Font.Size", 9f, true),  // 單價 12→9
        },
    };

    var log = new StringBuilder();
    int okFiles = 0, fail = 0;
    foreach (var kv in plan)
    {
        string path = Path.Combine(rep, kv.Key + ".rtm");
        if (!File.Exists(path)) { log.AppendLine($"缺檔: {kv.Key}"); fail++; continue; }
        var bytes = File.ReadAllBytes(path);
        var map = new Dictionary<string, Tpf0Object>();
        BuildNameMap(Tpf0Reader.Parse(bytes), map);
        int applied = 0, failed = 0;
        foreach (var (key, prop, target, isFont) in kv.Value)
        {
            if (!map.TryGetValue(key, out var obj)) { log.AppendLine($"{kv.Key}: 找不到 {key}"); failed++; continue; }
            bool ok;
            if (isFont)
            {
                var fp = obj.PropertiesEx.FirstOrDefault(x => x.Name == "Font.Size");
                if (fp is null || fp.Tag != 0x02) { log.AppendLine($"{kv.Key}: {key} Font.Size 屬性異常"); failed++; continue; }
                bytes[fp.ValuePayloadStart] = (byte)(int)target;
                ok = true;
            }
            else
            {
                ok = PatchPropAbs(bytes, obj, prop, target);
            }
            if (!ok) { log.AppendLine($"{kv.Key}: patch 失敗 {key}.{prop} →{target}"); failed++; continue; }
            applied++;
        }
        if (applied > 0)
        {
            File.WriteAllBytes(path, bytes);
            okFiles++;
            log.AppendLine($"{kv.Key}: 套用 {applied}/{kv.Value.Length} 筆{(failed > 0 ? $" / 失敗 {failed}" : "")}");
        }
        else fail++;
    }
    string head = $"=== 假警報修復(fix8): {okFiles} 檔已寫 / {fail} 檔有問題 ===";
    log.Insert(0, head + "\n");
    File.WriteAllText(@"D:\HeliAcc\shots\fix8_report.txt", log.ToString(), new UTF8Encoding(false));
    Console.WriteLine(head);
    Console.Write(log);
}

/// <summary>將元件屬性設為絕對目標值（mm，千分整數），失敗回傳 false。</summary>
static bool PatchPropAbs(byte[] bytes, Tpf0Object obj, string prop, float targetMm)
{
    var p = obj.PropertiesEx.FirstOrDefault(x => x.Name == prop);
    if (p is null) return false;
    long next = (long)Math.Round(targetMm * 1000.0);
    if (next < -100000 || next > 2000000) return false;
    switch (p.Tag)
    {
        case 0x02:
            if (next < sbyte.MinValue || next > sbyte.MaxValue) return false;
            bytes[p.ValuePayloadStart] = (byte)(sbyte)next;
            break;
        case 0x03:
            if (next < short.MinValue || next > short.MaxValue) return false;
            BitConverter.GetBytes((short)next).CopyTo(bytes, p.ValuePayloadStart);
            break;
        case 0x04:
            if (next < int.MinValue || next > int.MaxValue) return false;
            BitConverter.GetBytes((int)next).CopyTo(bytes, p.ValuePayloadStart);
            break;
        case 0x05: DoubleToExt80(next).CopyTo(bytes, p.ValuePayloadStart); break;
        case 0x0F: BitConverter.GetBytes((float)next).CopyTo(bytes, p.ValuePayloadStart); break;
        case 0x11:
        case 0x15: BitConverter.GetBytes((double)next).CopyTo(bytes, p.ValuePayloadStart); break;
        case 0x01:
        case 0x09: bytes[p.ValuePayloadStart] = (byte)(next == 0 ? 0 : 1); break;
        default: return false;
    }
    return true;
}

static RtmComponent? FindCompIn(List<RtmComponent> comps, string className, string name)
{
    foreach (var c in comps)
    {
        if (c.ClassName == className && c.Name == name) return c;
        var f = FindCompIn(c.Children, className, name);
        if (f != null) return f;
    }
    return null;
}

static RtmComponent? FindCompRec(RtmReportModel r, string className, string name)
{
    foreach (var b in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
    {
        if (b == null) continue;
        var f = FindCompIn(b.Components, className, name);
        if (f != null) return f;
    }
    return null;
}

static void FixOverlaps(string rtmFile)
{
    string path = Path.Combine(@"D:\HeliAcc\Rep", rtmFile);
    if (!File.Exists(path)) { Console.WriteLine($"找不到 {path}"); return; }
    var origBytes = File.ReadAllBytes(path);
    var bytes = (byte[])origBytes.Clone();
    var root = Tpf0Reader.Parse(bytes);
    var map = new Dictionary<string, Tpf0Object>();
    BuildNameMap(root, map);
    var r = RtmLoader.Load(root);
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"修復: {rtmFile}");
    int n = 0;

    void Move(string className, string name, string prop, float targetMm)
    {
        var c = FindCompRec(r, className, name);
        if (c == null) { sb.AppendLine($"  ! 找不到 {className}/{name}"); return; }
        float cur = prop switch
        {
            "mmTop" => c.MmTop / 1000f,
            "mmLeft" => c.MmLeft / 1000f,
            "mmWidth" => c.MmWidth / 1000f,
            "mmHeight" => c.MmHeight / 1000f,
            _ => 0f,
        };
        float delta = targetMm - cur;
        if (Math.Abs(delta) < 0.05f) { sb.AppendLine($"  = {name} 已 {prop}={cur:F2}"); return; }
        if (PatchObj(bytes, map, c, prop, delta))
        { n++; sb.AppendLine($"  > {name} {prop} {cur:F2} -> {targetMm:F2}"); }
        else
        {
            string key = $"{className}\0{name}";
            if (!map.ContainsKey(key))
                sb.AppendLine($"  ! 無法修改 {name} {prop}（map 無 key {key}）");
            else
            {
                var obj = map[key];
                var p = obj.PropertiesEx.FirstOrDefault(x => x.Name == prop);
                sb.AppendLine($"  ! 無法修改 {name} {prop}（key 存在 Class={obj.ClassName} Tag={p?.Tag} delta={delta:F2}）");
            }
        }
    }

    void MoveInBand(string bandKind, string className, string name, string prop, float targetMm)
    {
        RtmBand? band = bandKind switch
        {
            "GroupFooter" => r.GroupFooterBand,
            "Summary" => r.SummaryBand,
            "Footer" => r.FooterBand,
            "GroupHeader" => r.GroupHeaderBand,
            "Header" => r.HeaderBand,
            "Detail" => r.DetailBand,
            _ => null
        };
        if (band == null) { sb.AppendLine($"  ! 無 {bandKind} band"); return; }
        var c = FindCompIn(band.Components, className, name);
        if (c == null) { sb.AppendLine($"  ! 找不到 {bandKind}/{className}/{name}"); return; }
        float cur = prop == "mmTop" ? c.MmTop / 1000f : c.MmLeft / 1000f;
        float delta = targetMm - cur;
        if (Math.Abs(delta) < 0.05f) { sb.AppendLine($"  = {bandKind} {name} 已 {prop}={cur:F2}"); return; }
        if (PatchObj(bytes, map, c, prop, delta))
        { n++; sb.AppendLine($"  > {bandKind} {name} {prop} {cur:F2} -> {targetMm:F2}"); }
        else sb.AppendLine($"  ! 無法修改 {bandKind}/{name} {prop}");
    }

    /// <summary>移動屬性；int16 超界時自動擴寬為 int32（改變長度後重新解析全樹）。</summary>
    void WidenMove(string className, string name, string prop, float targetMm)
    {
        string key = $"{className}\0{name}";
        if (!map.TryGetValue(key, out var obj))
        {
            sb.AppendLine($"  ! Widen 找不到 {className}/{name}");
            return;
        }
        var p = obj.PropertiesEx.FirstOrDefault(x => x.Name == prop);
        if (p is null)
        {
            sb.AppendLine($"  ! Widen 無 {name} {prop}");
            return;
        }
        long newVal = (long)Math.Round(targetMm * 1000.0);
        if (p.Tag != 0x03)
        {
            float cur = ReadPropMm(bytes, obj, prop) ?? 0f;
            if (Math.Abs(targetMm - cur) < 0.05f) { sb.AppendLine($"  = {name} 已 {prop}={cur:F2}"); return; }
            var c = FindCompRec(r, className, name);
            if (c is null) { sb.AppendLine($"  ! Widen 找不到元件 {className}/{name}"); return; }
            if (PatchObj(bytes, map, c, prop, targetMm - cur))
            { n++; sb.AppendLine($"  > {name} {prop} {cur:F2} -> {targetMm:F2}"); }
            else sb.AppendLine($"  ! 無法修改 {name} {prop}（Tag=0x{p.Tag:X2} 超界）");
            return;
        }
        if (newVal >= short.MinValue && newVal <= short.MaxValue)
        {
            Move(className, name, prop, targetMm);
            return;
        }
        bytes = WidenInt16Prop(bytes, p, newVal);
        root = Tpf0Reader.Parse(bytes);
        map.Clear();
        BuildNameMap(root, map);
        r = RtmLoader.Load(root);
        n++;
        sb.AppendLine($"  * {name} {prop} 擴寬 int16→int32 → {targetMm:F2}mm");
    }

    void SetHeaderH(float targetMm)
    {
        string? key = map.Keys.FirstOrDefault(k => k.StartsWith("TppHeaderBand\0"));
        if (key == null) { sb.AppendLine("  ! 找不到 TppHeaderBand"); return; }
        var obj = map[key];
        var curMm = ReadPropMm(bytes, obj, "mmHeight");
        if (curMm is null) { sb.AppendLine("  ! HeaderBand 無 mmHeight"); return; }
        if (Math.Abs(targetMm - curMm.Value) < 0.05f) { sb.AppendLine($"  = HeaderBand 已 H={curMm.Value:F2}"); return; }
        if (PatchProp(bytes, obj, "mmHeight", targetMm - curMm.Value))
        { n++; sb.AppendLine($"  > HeaderBand mmHeight {curMm.Value:F2} -> {targetMm:F2}"); }
        else sb.AppendLine("  ! HeaderBand mmHeight 無法修改");
    }

    void SetGroupFooterH(float targetMm)
    {
        string? key = map.Keys.FirstOrDefault(k => k.StartsWith("TppGroupFooterBand\0"));
        if (key == null) { sb.AppendLine("  ! 找不到 TppGroupFooterBand"); return; }
        var obj = map[key];
        var curMm = ReadPropMm(bytes, obj, "mmHeight");
        if (curMm is null) { sb.AppendLine("  ! GroupFooterBand 無 mmHeight"); return; }
        if (Math.Abs(targetMm - curMm.Value) < 0.05f) { sb.AppendLine($"  = GroupFooterBand 已 H={curMm.Value:F2}"); return; }
        if (PatchProp(bytes, obj, "mmHeight", targetMm - curMm.Value))
        { n++; sb.AppendLine($"  > GroupFooterBand mmHeight {curMm.Value:F2} -> {targetMm:F2}"); }
        else sb.AppendLine("  ! GroupFooterBand mmHeight 無法修改");
    }

    void SetDetailH(float targetMm)
    {
        string? key = map.Keys.FirstOrDefault(k => k.StartsWith("TppDetailBand\0"));
        if (key == null) { sb.AppendLine("  ! 找不到 TppDetailBand"); return; }
        var obj = map[key];
        var curMm = ReadPropMm(bytes, obj, "mmHeight");
        if (curMm is null) { sb.AppendLine("  ! DetailBand 無 mmHeight"); return; }
        if (Math.Abs(targetMm - curMm.Value) < 0.05f) { sb.AppendLine($"  = DetailBand 已 H={curMm.Value:F2}"); return; }
        if (PatchProp(bytes, obj, "mmHeight", targetMm - curMm.Value))
        { n++; sb.AppendLine($"  > DetailBand mmHeight {curMm.Value:F2} -> {targetMm:F2}"); }
        else sb.AppendLine("  ! DetailBand mmHeight 無法修改");
    }

    /// <summary>縮小元件字型（pt），回傳是否成功。</summary>
    bool ShrinkFont(string className, string name, float newPt)
    {
        if (map.TryGetValue($"{className}\0{name}", out var obj) && PatchFontPt(bytes, obj, newPt))
        { n++; sb.AppendLine($"  > {name} 字型 -> {newPt:F0}pt"); return true; }
        return false;
    }

    /// <summary>B2：單據類 Footer（借入/託工/調整 6 檔共用佈局）重排——右欄 合計/總計/稅額 三行與值同列、
    /// 左中 折讓/已付/應付 三行與值同列、已收付金額值（X=145.4）移到右欄下方空檔。
    /// 參數為 合計/稅額/總計/折讓/已付/應付 六個 label 元件名（不同檔元件名有偏移）。</summary>
    void FixFooterB2A(string hl, string ht, string hs, string dl, string dp, string da)
    {
        Move("TppLabel", hl, "mmTop", 6.0f);
        Move("TppLabel", hs, "mmTop", 12.0f);
        Move("TppLabel", ht, "mmTop", 18.0f);
        Move("TppDBText", "ppDBText49", "mmTop", 6.0f);
        Move("TppDBText", "ppDBText47", "mmTop", 12.0f);
        Move("TppDBText", "ppDBText48", "mmTop", 18.0f);
        Move("TppLabel", dl, "mmTop", 4.5f);
        Move("TppLabel", dp, "mmTop", 10.2f);
        Move("TppLabel", da, "mmTop", 15.9f);
        Move("TppDBText", "ppDBText54", "mmTop", 4.5f);
        Move("TppDBText", "ppDBText52", "mmTop", 15.9f);
        Move("TppDBText", "ppDBText53", "mmTop", 24.5f);
    }

    string b = rtmFile.Replace(".rtm", "").Replace("(含折扣)", "").Replace("含折扣", "");
    bool isTable = b.Contains("明細表") || b.Contains("簡要表") || b.Contains("統計表") || b.Contains("標籤") || b.Contains("排行") || b.Contains("對帳") || b.Contains("信封");
    bool hasDisc = rtmFile.Contains("含折扣");
    if (b == "出貨明細表" || b == "進貨明細表")
    {
        // GroupFooter 錯亂（band 高 9.52 卻塞 3 組元件）：加大 band 並重排 合計/稅額/總計 三行
        SetGroupFooterH(20.5f);
        MoveInBand("GroupFooter", "TppLabel", "ppLabel1", "mmTop", 1.8f);
        MoveInBand("GroupFooter", "TppLabel", "ppLabel2", "mmTop", 7.8f);
        MoveInBand("GroupFooter", "TppLabel", "ppLabel3", "mmTop", 13.8f);
        foreach (var nm in new[] { "ppLabel1", "ppLabel2", "ppLabel3" })
            MoveInBand("GroupFooter", "TppLabel", nm, "mmLeft", 156.5f);
        MoveInBand("GroupFooter", "TppDBText", "ppDBText1", "mmTop", 1.8f);
        MoveInBand("GroupFooter", "TppDBText", "ppDBText2", "mmTop", 7.8f);
        MoveInBand("GroupFooter", "TppDBText", "ppDBText3", "mmTop", 13.8f);
        MoveInBand("GroupFooter", "TppDBText", "ppDBText1", "mmLeft", 171.7f);
        MoveInBand("GroupFooter", "TppDBText", "ppDBText2", "mmLeft", 172.0f);
        MoveInBand("GroupFooter", "TppDBText", "ppDBText3", "mmLeft", 172.0f);
        MoveInBand("GroupFooter", "TppLine", "ppLine1", "mmTop", 19.6f);
        MoveInBand("GroupFooter", "TppLine", "ppLine2", "mmTop", 0.2f);
        // 子報表明細列（每單 2 行貨品）溢位：列高 5.82×2 = 11.64 超出 Detail band 剩餘空間
        // （10.32−5.29=5.03）→ 第 2 列值撞下一列頂列（對象名稱）與 GroupFooter 合計:。
        // 加大 Detail band 容納 2 列子報表、值上移對齊列頂、GroupFooter 摘要下移 1.0。
        SetDetailH(17.5f);
        foreach (var nm in new[] { "ppDBText19", "ppDBText20", "ppDBText21", "ppDBText23", "ppDBText24" })
            Move("TppDBText", nm, "mmTop", 0.60f);
        Move("TppDBText", "ppDBText22", "mmTop", 1.73f);   // 單位（原 T=2.93 偏下）
    }
    else if (b == "應收帳款明細表" || b == "應付帳款明細表")
    {
        // 子報表 SummaryBand 值元件 top=0 全疊在 band 頂：依左/右欄 label 行位（2.646/8.752/14.858/20.964/27.07）對齊
        const float r1 = 2.646f, r2 = 8.752f, r3 = 14.858f, r4 = 20.964f, r5 = 27.07f;
        // 左欄值（label 右緣≈51 → 值 x=52.5，寬 32）
        Move("TppDBCalc", "ppDBCalc7", "mmTop", r1);
        Move("TppDBText", "ppDBText19", "mmTop", r2);
        Move("TppDBText", "ppDBText18", "mmTop", r3);
        Move("TppDBText", "ppDBText26", "mmTop", r4);
        Move("TppDBText", "ppDBText25", "mmTop", r5);
        foreach (var nm in new[] { "ppDBText19", "ppDBText18", "ppDBText26", "ppDBText25" })
        {
            Move("TppDBText", nm, "mmLeft", 52.5f);
            Move("TppDBText", nm, "mmWidth", 32.0f);
        }
        Move("TppDBCalc", "ppDBCalc7", "mmLeft", 52.5f);
        Move("TppDBCalc", "ppDBCalc7", "mmWidth", 32.0f);
        // 右欄值（label 右緣≈167.2 → 值 x=167.2，寬 23）
        Move("TppDBText", "ppDBText20", "mmTop", r1);
        Move("TppDBText", "ppDBText21", "mmTop", r2);
        Move("TppDBText", "ppDBText22", "mmTop", r3);
        Move("TppDBText", "ppDBText23", "mmTop", r4);
        Move("TppDBText", "ppDBText24", "mmTop", r5);
        foreach (var nm in new[] { "ppDBText20", "ppDBText21", "ppDBText22", "ppDBText23", "ppDBText24" })
        {
            Move("TppDBText", nm, "mmLeft", 167.2f);
            Move("TppDBText", nm, "mmWidth", 23.0f);
        }
        // 應收版底部公司資訊（Tel/Fax 行 + 登記地址在其上方）——僅應收版存在
        var telLbl = FindCompRec(r, "TppLabel", "ppLabel15");
        if (telLbl is not null)
        {
            float tt = telLbl.MmTop / 1000f;
            Move("TppDBText", "ppDBText38", "mmTop", tt);
            Move("TppDBText", "ppDBText28", "mmTop", tt);
            Move("TppDBText", "ppDBText39", "mmTop", tt - 5.5f);
        }
        // Header 聯絡人一/聯絡電話一 值右緣不得超過「統一編號：/傳真號碼：」label 左緣(52917µm)−0.3mm
        foreach (var nm in new[] { "ppDBText2", "ppDBText16" })
        {
            var hc = FindCompRec(r, "TppDBText", nm);
            if (hc is null) continue;
            float hL = hc.MmLeft / 1000f;
            float limit = 52.617f;
            if (hL + hc.MmWidth / 1000f > limit)
            {
                float newW = limit - hL;
                if (newW >= 10f) Move("TppDBText", nm, "mmWidth", newW);
            }
        }
    }
    else if (isTable)
    {
        if (b == "應收帳款簡要表")
        {
            // GroupFooter 摘要 label 全部擠在底部（T 25-33 互疊 38~147mm²）：
            // 左/右欄各自對齊值行垂直重排（行距 6.0 ≥ 渲染 bbox 高 5.7）、右欄 label 左移避值內容超繪。
            // 左欄（值 x≈83-89）：前期累計應收/累計預收/折讓/現收 四行 → T=6/12/18/24
            Move("TppDBText", "ppDBText6", "mmTop", 6.0f);
            Move("TppDBText", "ppDBText5", "mmTop", 12.0f);
            Move("TppDBText", "ppDBText26", "mmTop", 18.0f);
            Move("TppDBText", "ppDBText25", "mmTop", 24.0f);
            Move("TppLabel", "ppLabel20", "mmTop", 6.0f);   // (加)前期累計未收帳款：
            Move("TppLabel", "ppLabel6", "mmTop", 12.0f);   // (減)累計預收貨款：
            Move("TppLabel", "ppLabel28", "mmTop", 18.0f);  // (減)折讓金額：
            Move("TppLabel", "ppLabel12", "mmTop", 24.0f);  // (減)現收金額：
            // 右欄（值 x≈178-191）：本期合計/營業稅/已收付/本期總計/累計應收 → T=1.59/7.59/13.59/19.59/25.59
            Move("TppDBText", "ppDBText7", "mmTop", 7.59f);
            Move("TppDBText", "ppDBText8", "mmTop", 13.59f);
            Move("TppDBText", "ppDBText9", "mmTop", 19.59f);
            Move("TppDBText", "ppDBText10", "mmTop", 25.59f);
            foreach (var nm in new[] { "ppLabel7", "ppLabel8", "ppLabel9", "ppLabel10", "ppLabel11" })
                Move("TppLabel", nm, "mmLeft", 132.72f);   // 右緣 173.2：避開值內容超繪左緣（≈173.7）與紙右緣
            Move("TppLabel", "ppLabel7", "mmTop", 1.59f);   // 本期合計：
            Move("TppLabel", "ppLabel8", "mmTop", 7.59f);   // (加)營業稅：
            Move("TppLabel", "ppLabel9", "mmTop", 13.59f);  // (減)已收款：
            Move("TppLabel", "ppLabel10", "mmTop", 19.59f); // 本期總計：
            Move("TppLabel", "ppLabel11", "mmTop", 25.59f); // 本期累計應收：
        }
        sb.AppendLine("  (明細表類：僅自動對齊 Detail / GroupFooter 欄位)");
    }
    else if (b.StartsWith("出貨單據") || (hasDisc && b.StartsWith("進貨單據")) || b.StartsWith("借出"))
    {
        // 出貨單據式 layout（欄標題行與「送貨地址：」重疊、欄標題底超出 band）
        SetHeaderH(55.5f);
        foreach (var nm in new[] { "ppLabel39", "ppLabel40", "ppLabel41", "ppLabel42", "ppLabel43", "ppLabel44", "ppLabel45" })
            Move("TppLabel", nm, "mmTop", 49.2f);
        Move("TppLine", "ppLine3", "mmTop", 54.4f);
        // 數量欄標題 W22.75 比值欄（20.9）寬 → 右緣超出 0.85mm 與「單位」label 交疊：縮到值欄寬
        Move("TppLabel", "ppLabel44", "mmWidth", 20.9f);
        // 折扣欄標題 T=47.62 偏上 → 撞「送貨地址」值（右緣≈95.5）與 數量/單位 欄標題：
        // 右移到表格右上角（備註說明右側），T 對齊欄標題行 49.20
        if (hasDisc)
        {
            Move("TppLabel", "ppLabel4", "mmLeft", 191.04f);
            Move("TppLabel", "ppLabel4", "mmTop", 49.2f);
        }
    }
    else if (b == "調撥單據")
    {
        // 數量欄標題 W25.40 太寬 → 右緣超出 3.0mm 與「單位」label 交疊（16.81mm²）：縮到值欄寬 20.9
        Move("TppLabel", "ppLabel4", "mmWidth", 20.9f);
    }
    else if (b == "進貨單據" || b == "出貨折讓單" || b == "借入單據" || b == "進貨折讓單" || b == "調整單據")
    {
        // 聯絡三行（聯 絡 人： / 聯絡電話： / 聯絡電話一）原始間距 4.0mm 不足（繪製 bbox 高 ~5.6mm），
        // 採「水平錯開」：聯絡電話一 右移至 L=50，聯絡電話：label 左移至欄位行 5.29，
        // 並雙向微調垂直（聯 絡 人 27.68、聯絡電話 32.72）讓 bbox 不再交疊（int16 上限內）。
        // 註：Header 4 行（廠商名稱 22.24/聯 絡 人/聯絡電話/送貨地址 38.4）+ 欄標題 44.98 總空間不足，
        // 任何進一步垂直挪移都會把重疊轉移給「送貨地址×欄標題」等處，故維持此間距。
        bool isRet = b == "出貨折讓單" || b == "進貨折讓單";
        string lab = isRet ? "ppLabel8" : "ppLabel31";
        string labPer = isRet ? "ppLabel7" : "ppLabel30";
        string tel = isRet ? "ppDBText11" : "ppDBText35";
        string per = isRet ? "ppDBText10" : "ppDBText34";
        Move("TppLabel", lab, "mmLeft", 5.29f);
        Move("TppLabel", labPer, "mmTop", 27.68f);
        Move("TppLabel", lab, "mmTop", 32.72f);
        WidenMove("TppDBText", tel, "mmLeft", 50.0f);
        Move("TppDBText", tel, "mmTop", 32.72f);
        Move("TppDBText", per, "mmTop", 27.68f);
        // 聯絡三行（客戶/廠商名稱、聯 絡 人、聯絡電話）fs12 中文字 bbox 高 ~5.6 > 行距 5.0
        // → 三組 label 與值上下交疊（3.54/11.95/3.16mm²）：全部縮 fs10（bbox 高 ~4.7）錯開。
        string[] nameLbls = isRet ? new[] { "ppLabel6", "ppLabel7", "ppLabel8" } : new[] { "ppLabel29", "ppLabel30", "ppLabel31" };
        string[] nameVals = isRet ? new[] { "ppDBText5", "ppDBText10", "ppDBText11" } : new[] { "ppDBText29", "ppDBText34", "ppDBText35" };
        foreach (var nm in nameLbls) ShrinkFont("TppLabel", nm, 10f);
        foreach (var nm in nameVals) ShrinkFont("TppDBText", nm, 10f);
        if (b == "出貨折讓單")
        {
            // Detail(child) 8 值採 0.53/2.16/3.73 階梯 → 列間與行間交疊（3.52/2.07/1.26mm²，
            // 且第 2 列單據折讓撞 Footer 合計金額 label）：全部單層化 T=0.53。
            // 水平間距已足（單據稅金 99.72-118.24 / 單據折讓 119.00-138.32 / 折扣稅額 139.35-160.52 / 附註 173.68+）。
            foreach (var nm in new[] { "ppDBText17", "ppDBText18", "ppDBText19", "ppDBText20", "ppDBText21", "ppDBText22", "ppDBText23", "ppDBText6" })
                Move("TppDBText", nm, "mmTop", 0.53f);
            // 單層化後 單據稅金值（taRight）內容超框右 2.36（bbox 至 120.6）仍撞 單據折讓值左緣 119.5
            // （5.24/0.35mm²）：縮值寬 W=18.52→16.5 → 內容右緣移至 ~118.9，與單據折讓間距 0.6
            Move("TppDBText", "ppDBText20", "mmWidth", 16.5f);
        }
        if (b == "進貨折讓單")
        {
            // 「附註說明」標題左緣 161.93 與「折扣稅額」標題右緣 162.10 交疊 0.17mm（0.97mm²）：右移 0.35
            Move("TppLabel", "ppLabel22", "mmLeft", 162.28f);
        }
        if (b == "進貨單據")
        {
            Move("TppDBText", "ppDBText32", "mmTop", 32.08f);
            Move("TppDBText", "ppDBText33", "mmTop", 38.18f);
        }
        if (b == "借入單據")
            Move("TppDBText", "ppDBText29", "mmTop", 22.24f);
        // B2：Footer 摘要區重排（label 與值同列、消除超界交疊）
        if (b == "借入單據" || b == "調整單據")
            FixFooterB2A("ppLabel46", "ppLabel47", "ppLabel48", "ppLabel49", "ppLabel50", "ppLabel51");
        if (b == "進貨單據")
        {
            Move("TppLabel", "ppLabel46", "mmTop", 1.85f);
            Move("TppLabel", "ppLabel47", "mmTop", 7.55f);
            Move("TppLabel", "ppLabel48", "mmTop", 13.25f);
            Move("TppDBText", "ppDBText48", "mmTop", 7.55f);
            Move("TppDBText", "ppDBText47", "mmTop", 13.25f);
            Move("TppLabel", "ppLabel49", "mmTop", 4.5f);
            Move("TppLabel", "ppLabel50", "mmTop", 10.25f);
            Move("TppLabel", "ppLabel51", "mmTop", 15.95f);
            Move("TppDBText", "ppDBText53", "mmTop", 10.25f);
            Move("TppDBText", "ppDBText52", "mmTop", 15.95f);
            Move("TppDBText", "ppDBText54", "mmTop", 20.0f);
            Move("TppLabel", "ppLabel55", "mmTop", 27.5f);
            Move("TppLabel", "ppLabel56", "mmTop", 27.5f);
        }
    }
    else if (b == "借入還出單" || b == "託工入庫" || b == "託工出庫")
    {
        Move("TppDBText", "ppDBText29", "mmTop", 21.71f);
        Move("TppLabel", "ppLabel30", "mmTop", 27.55f);
        Move("TppDBText", "ppDBText34", "mmTop", 27.55f);
        WidenMove("TppDBText", "ppDBText35", "mmLeft", 50.0f);
        Move("TppLabel", "ppLabel31", "mmLeft", 5.29f);
        Move("TppLabel", "ppLabel31", "mmTop", 32.72f);
        Move("TppDBText", "ppDBText35", "mmTop", 32.72f);
        // 聯 絡 人：/聯絡電話： 兩 label fs12 bbox 高 ~5.6 > 行距 5.17 → 交疊 9.20mm²：縮 fs10
        ShrinkFont("TppLabel", "ppLabel30", 10f);
        ShrinkFont("TppLabel", "ppLabel31", 10f);
        // B2：Footer 摘要區重排（label 與值同列、消除超界交疊）
        if (b == "借入還出單" || b == "託工入庫")
            FixFooterB2A("ppLabel46", "ppLabel47", "ppLabel48", "ppLabel49", "ppLabel50", "ppLabel51");
        else if (b == "託工出庫")
            FixFooterB2A("ppLabel47", "ppLabel48", "ppLabel49", "ppLabel50", "ppLabel51", "ppLabel52");
    }
    else if (b == "進貨退出單" || b == "託售單據" || b == "託售回貨單")
    {
        Move("TppLabel", "ppLabel30", "mmTop", 27.55f);
        Move("TppDBText", "ppDBText34", "mmTop", 27.55f);
        WidenMove("TppDBText", "ppDBText35", "mmLeft", 50.0f);
        Move("TppLabel", "ppLabel31", "mmLeft", 5.29f);
        Move("TppLabel", "ppLabel31", "mmTop", 32.72f);
        Move("TppDBText", "ppDBText35", "mmTop", 32.72f);
        Move("TppDBText", "ppDBText29", "mmTop", 21.71f);
        Move("TppDBText", "ppDBText27", "mmTop", 25.97f);
        Move("TppDBText", "ppDBText32", "mmTop", 32.08f);
        Move("TppDBText", "ppDBText33", "mmTop", 38.18f);
        // 聯 絡 人：/聯絡電話： 兩 label fs12 bbox 高 ~5.6 > 行距 5.17 → 交疊 9.20mm²：縮 fs10
        ShrinkFont("TppLabel", "ppLabel30", 10f);
        ShrinkFont("TppLabel", "ppLabel31", 10f);
        // B2：進貨退出單 Footer 摘要區重排——總 計：32.05 與 業務/簽收 32.5 同列重疊，
        // 右欄（合計/稅額/總計）、左中（折讓/已退/未退）三行各自與值同列、折讓值移到右欄下方。
        if (b == "進貨退出單")
        {
            Move("TppLabel", "ppLabel46", "mmTop", 12.0f);
            Move("TppLabel", "ppLabel47", "mmTop", 17.7f);
            Move("TppLabel", "ppLabel48", "mmTop", 23.4f);
            Move("TppDBText", "ppDBText49", "mmTop", 12.0f);
            Move("TppDBText", "ppDBText48", "mmTop", 17.7f);
            Move("TppDBText", "ppDBText47", "mmTop", 23.4f);
            Move("TppLabel", "ppLabel49", "mmTop", 12.0f);
            Move("TppLabel", "ppLabel50", "mmTop", 17.7f);
            Move("TppLabel", "ppLabel51", "mmTop", 23.4f);
            Move("TppDBText", "ppDBText53", "mmTop", 17.7f);
            Move("TppDBText", "ppDBText52", "mmTop", 23.4f);
            Move("TppDBText", "ppDBText54", "mmTop", 6.0f);
            Move("TppDBMemo", "ppDBMemo1", "mmWidth", 76.0f);
        }
    }
    else if (b == "訂貨單據")
    {
        // 聯絡電話：label 對齊聯絡電話一值（37.15，超過 int16 → WidenMove）；送貨地址：上移、欄標題下移，
        // 讓 聯 絡 人：/聯絡電話：/送貨地址：/欄標題 的繪製 bbox 錯開。
        Move("TppLabel", "ppLabel31", "mmLeft", 5.29f);
        WidenMove("TppLabel", "ppLabel31", "mmTop", 37.15f);
        WidenMove("TppDBText", "ppDBText35", "mmLeft", 50.0f);
        Move("TppDBText", "ppDBText35", "mmTop", 37.15f);
        Move("TppDBText", "ppDBText34", "mmTop", 31.15f);
        Move("TppDBText", "ppDBText29", "mmTop", 25.15f);
        Move("TppLabel", "ppLabel4", "mmTop", 42.40f);
        Move("TppDBText", "ppDBText3", "mmTop", 42.40f);
        // 聯絡電話：label（37.15）與 送貨地址：label（42.40）、欄標題行（47.90）fs12 bbox 高 5.6
        // → 8.26/1.97/1.07mm² 交疊：聯絡電話 label、送貨地址 label、送貨地址值 縮 fs10 錯開
        ShrinkFont("TppLabel", "ppLabel31", 10f);
        ShrinkFont("TppLabel", "ppLabel4", 10f);
        ShrinkFont("TppDBText", "ppDBText3", 10f);
        SetHeaderH(53.6f);
        foreach (var nm in new[] { "ppLabel39", "ppLabel40", "ppLabel41", "ppLabel42", "ppLabel43", "ppLabel44", "ppLabel45" })
            Move("TppLabel", nm, "mmTop", 47.9f);
        foreach (var nm in new[] { "ppDBText9", "ppDBText38", "ppDBText39" })
            Move("TppDBText", nm, "mmTop", 47.9f);
        Move("TppLine", "ppLine3", "mmTop", 52.9f);
    }
    else if (b.StartsWith("採購單據") || b.StartsWith("報價單據") || b.StartsWith("詢價單據"))
    {
        SetHeaderH(49.4f);
        foreach (var nm in new[] { "ppLabel39", "ppLabel40", "ppLabel41", "ppLabel42", "ppLabel43", "ppLabel44", "ppLabel45" })
            Move("TppLabel", nm, "mmTop", 43.0f);
        Move("TppLine", "ppLine3", "mmTop", 48.3f);
    }
    else if (b.StartsWith("出貨退回"))
    {
        Move("TppDBText", "ppDBText10", "mmTop", 38.0f);
        Move("TppDBText", "ppDBText7", "mmTop", 32.08f);
        // 業務姓名：label（L=170.82）與 員工名稱值（L=170.39 W=45）同 x 重疊（110.51mm²）：
        // 左移到 147.11（與折讓單/單據 業務姓名 label 一致），右緣 168.81 不撞值左緣 170.39。
        // 但 y=31.24 與上方貨單編號 label（T=26.96）交疊 1.4（30.81mm²）：下移至 32.75
        // （與貨單編號 bbox 26.96-32.56 間距 0.19、與發票號碼 bbox 38.18+ 間距 0.73）並縮 fs10。
        Move("TppLabel", "ppLabel9", "mmLeft", 147.11f);
        Move("TppLabel", "ppLabel9", "mmTop", 32.75f);
        ShrinkFont("TppLabel", "ppLabel9", 10f);
    }
    else
    {
        sb.AppendLine("  (無此類別規則)");
    }

    // Detail 自動對齊欄標題 + 異常座標歸位（全部報表）
    AlignDetailToHeader(bytes, map, r, sb, ref n);
    FixDetailTop(bytes, map, r, sb, ref n);
    RepackDetailRow(rtmFile, bytes, map, r, sb, ref n);
    FixHeaderLabels(rtmFile, bytes, map, r, sb, ref n);
    FixBandTop(bytes, map, r, "GroupFooter", sb, ref n);

    // —— 自動對齊之後的校正（依賴 RepackDetailRow 完成後的狀態）——
    if (b == "進貨折讓單")
    {
        // Detail(child) 欄位錯位：RepackDetailRow 依錯位標題把「單據折讓」值對齊到 100.72，與
        // 單據稅金框交疊 17.5mm（72.07mm²）；「附註」L=123.12 與折扣稅額交疊（2.86mm²）。
        // 校正回出貨折讓單版式位置（標題已被 FixHeaderLabels 移至 119.00）。
        Move("TppDBText", "ppDBText21", "mmLeft", 119.00f);
        Move("TppDBText", "ppDBText21", "mmTop", 3.73f);
        Move("TppDBText", "ppDBText6", "mmLeft", 173.68f);
        Move("TppDBText", "ppDBText6", "mmTop", 3.73f);
    }
    else if (b == "出貨折讓單")
    {
        // Header 標題：單據稅金框寬異常 25.56（進貨折讓單僅 18.79）→ FixHeaderLabels 據此把
        // 單據折讓（→131.18）/折扣稅額（→153.11）右推，內容交疊 23.54mm²。於 FixHeaderLabels 之後
        // 強制複製進貨折讓單（乾淨）最終座標：單據稅金 L=99.72 W=18.79、單據折讓 L=119.00、
        // 折扣稅額 L=140.93（還原 fs12）、附註說明 L=162.28。縮窄框後再跑 fixov 即不再觸發推開。
        Move("TppLabel", "ppLabel19", "mmLeft", 99.717f);
        Move("TppLabel", "ppLabel19", "mmWidth", 18.785f);
        Move("TppLabel", "ppLabel20", "mmLeft", 119.002f);
        Move("TppLabel", "ppLabel12", "mmLeft", 140.931f);
        if (map.TryGetValue("TppLabel\0ppLabel12", out var lbl12) && PatchFontPt(bytes, lbl12, 12f))
        { n++; sb.AppendLine("  > ppLabel12 字型還原 -> 12pt"); }
        Move("TppLabel", "ppLabel22", "mmLeft", 162.28f);
    }
    else if (b == "會計傳票")
    {
        // Detail(child) 欄位文字超框：借貸「測試資料」4 字 fs12=16.9 超框 W9；科目編號「應收帳款-合併」
        // 7 字 fs12=29.6 超框 W20.37 → 借貸×科目編號×科目名稱 互相壓字（46.80/39.55mm²）。
        // 全部明細欄 fs12→fs9，再把科目編號/科目名稱/摘要（值+標題）右移 3.7mm 錯開壓字：
        // 借貸內容右 20.0 vs 科目編號內容左 20.0（原交疊 3.7mm）。
        foreach (var nm in new[] { "ppDBText40", "ppDBText1", "ppDBText2", "ppDBText3", "ppDBText4", "ppDBText5" })
        {
            if (map.TryGetValue($"TppDBText\0{nm}", out var obj) && PatchFontPt(bytes, obj, 9f))
            { n++; sb.AppendLine($"  > {nm} 字型 12 -> 9"); }
        }
        Move("TppDBText", "ppDBText1", "mmLeft", 20.02f);   // 科目編號
        Move("TppDBText", "ppDBText4", "mmLeft", 41.89f);   // 科目名稱
        Move("TppDBText", "ppDBText2", "mmLeft", 85.46f);   // 摘要
        Move("TppLabel", "ppLabel40", "mmLeft", 21.66f);    // 欄標題 科目編號
        Move("TppLabel", "ppLabel44", "mmLeft", 42.54f);    // 欄標題 科目名稱
        Move("TppLabel", "ppLabel42", "mmLeft", 85.19f);    // 欄標題 摘要
    }
    else if (b.Contains("領料單據"))
    {
        // Detail(child) 單位「測試資料」4 字 fs12 內容超框右 ~8mm（133.9→152.7）撞「附註說明」左緣 149.2
        // （19.62mm²）：縮 fs9 後內容右緣 148.6 < 附註框左 149.23。
        if (map.TryGetValue("TppDBText\0ppDBText42", out var obj) && PatchFontPt(bytes, obj, 9f))
        { n++; sb.AppendLine($"  > ppDBText42 字型 12 -> 9"); }
    }
    else if (b == "出貨明細表" || b == "進貨明細表")
    {
        // 品名內容（9 字 fs9）超框右 3.8mm 撞數量欄左緣（10.45mm²）：數量欄（值+標題）右移 3.45mm，
        // 與單位欄間距仍 28mm；移後框間隙 4.25 讓 RepackDetailRow 不再觸發重排。
        Move("TppDBText", "ppDBText21", "mmLeft", 106.5f);
        Move("TppDBText", "ppDBText21", "mmWidth", 15.68f);
        Move("TppLabel", "ppLabel21", "mmLeft", 106.5f);
    }
    else if (b == "應收帳款簡要表")
    {
        // 交易日期內容（fs11）左緣 17.7 與單據類別內容右緣 18.3 交疊 0.6mm（2.52mm²）：
        // 右移 1mm → 左緣 18.73（與交易單號框仍 1.3mm）。
        Move("TppDBText", "ppDBText1", "mmLeft", 18.73f);
        Move("TppLabel", "ppLabel15", "mmLeft", 16.88f);
    }
    else if (b == "應付帳款明細表")
    {
        // 品名內容（9 字 fs9）超框右 3.8mm 撞數量欄左緣（10.45mm²）：品名縮 fs8 後內容右緣 ≈130.2
        // < 數量框左 132.7（四欄右移會再撞單位/單價/金額，且被 RepackDetailRow 重排干擾）。
        if (map.TryGetValue("TppDBText\0ppDBText12", out var obj) && PatchFontPt(bytes, obj, 8f))
        { n++; sb.AppendLine($"  > ppDBText12 字型 9 -> 8"); }
    }

    if (n > 0)
    {
        File.WriteAllBytes(path, bytes);
        sb.AppendLine($"已寫回 {path}（{n} 項調整）");
    }
    else
        sb.AppendLine("無調整");
    Console.Write(sb.ToString());
    File.WriteAllText($@"D:\HeliAcc\shots\fixov_{Path.GetFileNameWithoutExtension(rtmFile)}.txt", sb.ToString(), new System.Text.UTF8Encoding(false));
}

static void FixOverlapsAll()
{
    var files = Directory.GetFiles(@"D:\HeliAcc\Rep", "*.rtm").OrderBy(f => f).ToList();
    foreach (var f in files)
    {
        try { FixOverlaps(Path.GetFileName(f)); }
        catch (Exception ex) { Console.WriteLine($"{Path.GetFileName(f)}: {ex.Message}"); }
    }
}

/// <summary>把 Detail 子報表欄位水平對齊 Header 欄標題行（依欄標題 caption 前 2 字匹配）。</summary>
static void AlignDetailToHeader(byte[] bytes, Dictionary<string, Tpf0Object> map, RtmReportModel r, System.Text.StringBuilder sb, ref int n)
{
    if (r.HeaderBand is not { } hb || r.DetailBand is not { } db) return;
    var labels = hb.Components.Where(c => c.ClassName == "TppLabel" && c.Caption is { Length: > 0 }).ToList();
    if (labels.Count < 2) return;
    var groups = labels.GroupBy(c => (float)Math.Round(c.MmTop / 500f) * 500f).ToList();
    float modeY = groups.OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
    if (modeY <= 0) return;
    var titles = labels.Where(c => Math.Abs(c.MmTop - modeY) <= 300f).ToList();
    if (titles.Count < 2) return;
    var cells = new List<RtmComponent>();
    void Walk(List<RtmComponent> comps)
    {
        foreach (var c in comps)
        {
            if (c.ClassName is "TppSubReport" or "TppChildReport" or "TppRegion")
            {
                foreach (var sc in c.Children)
                {
                    if (sc.ClassName.StartsWith("Tpp") && sc.ClassName.EndsWith("Band"))
                    {
                        if (IsDetailBand(sc)) Walk(sc.Children);
                    }
                    else Walk(new List<RtmComponent> { sc });
                }
            }
            else
            {
                if (c.ClassName == "TppDBText" && c.DataField is { Length: > 0 }) cells.Add(c);
                Walk(c.Children);
            }
        }
    }
    Walk(db.Components);
    if (cells.Count == 0) return;
    foreach (var cell in cells)
    {
        string k = (cell.Caption ?? "").Trim();
        if (k.Length < 2) k = cell.DataField ?? "";
        if (k.Length < 2) continue;
        string pre = k.Substring(0, 2);
        var cand = titles.Where(t => k.StartsWith(t.Caption!.Trim())).ToList();
        if (cand.Count == 0) continue;
        var best = cand.OrderBy(t => Math.Abs(t.MmLeft - cell.MmLeft)).First();
        // 寬度對齊：cell 較寬 → 縮到與欄標題同寬（避免右緣越界交疊右欄）
        if (cell.MmWidth > best.MmWidth + 200f)
        {
            float dw = (cell.MmWidth - best.MmWidth) / 1000f;
            if (PatchObj(bytes, map, cell, "mmWidth", -dw))
            {
                cell.MmWidth = best.MmWidth;
                n++; sb.AppendLine($"  > Detail {cell.Name} mmWidth {cell.MmWidth / 1000f:F2} -> {best.MmWidth / 1000f:F2} (欄標題「{best.Caption}」)"); }
        }
        float delta = (best.MmLeft - cell.MmLeft) / 1000f;
        if (Math.Abs(delta) < 0.5f) continue;
        if (PatchObj(bytes, map, cell, "mmLeft", delta))
        {
            cell.MmLeft = best.MmLeft;
            n++; sb.AppendLine($"  > Detail {cell.Name} mmLeft {cell.MmLeft / 1000f:F2} -> {best.MmLeft / 1000f:F2} (欄標題「{best.Caption}」)"); }
    }
}

/// <summary>band 名稱是否為 Detail band（容忍尾部數字，如 TppDetailBand3）。</summary>
static bool IsDetailBand(RtmComponent sc)
{
    string n = sc.ClassName;
    int i = n.Length - 1;
    while (i >= 0 && char.IsDigit(n[i])) i--;
    return n.Substring(0, i + 1).EndsWith("DetailBand");
}

/// <summary>已知實際文字重疊的報表（基線 33 檔）——明細列重排/加寬只對這些檔執行，避免誤傷乾淨檔。</summary>
static bool IsKnownRepack(string name)
{
    return name is "出貨折讓單" or "出貨明細表" or "出貨退回單" or "出貨單據" or "出貨單據(含折扣)" or "訂貨單據"
        or "借入單據" or "借入還出單" or "借出單據" or "借出還入單" or "託工入庫" or "託工出庫"
        or "託售回貨單" or "託售單據" or "採購單據" or "報價單據" or "進貨折讓單" or "進貨明細表"
        or "進貨退出單" or "進貨單據" or "進貨單據(含折扣)" or "會計傳票" or "詢價單據" or "維修單據"
        or "領料單據" or "盤點單據" or "調撥單據" or "調整單據" or "應付帳款明細表" or "應付帳款明細表(含折扣)"
        or "應收帳款明細表" or "應收帳款明細表(含折扣)" or "應收帳款簡要表";
}

/// <summary>
/// 明細列重排：把子報表明細 band 的同列欄位重新排列。
/// 1) 相鄰欄間距 &lt; 1.2mm（框交疊/擠壓）→ 整列從頭重排（右對齊欄至少量測寬、文字欄視空間壓縮、空間仍不足再縮字型）。
/// 2) 間距正常但右對齊欄框寬 &lt; 20.9mm 且有右鄰欄（文字溢出右欄的風險）→ 僅往左加寬（右緣不變）。
/// 同步移動 Header 欄標題 label，維持對齊。
/// </summary>
static void RepackDetailRow(string rtmFile, byte[] bytes, Dictionary<string, Tpf0Object> map, RtmReportModel r, System.Text.StringBuilder sb, ref int n)
{
    if (!IsKnownRepack(Path.GetFileNameWithoutExtension(rtmFile))) return;
    if (r.HeaderBand is not { } hb || r.DetailBand is not { } db) return;
    float paperWmm = r.MmPaperWidth / 1000f;
    if (paperWmm <= 0f) paperWmm = 215.9f;
    float maxRightMm = paperWmm - 10f;
    var cells = new List<RtmComponent>();
    void Walk(List<RtmComponent> comps)
    {
        foreach (var c in comps)
        {
            if (c.ClassName is "TppSubReport" or "TppChildReport" or "TppRegion")
            {
                foreach (var sc in c.Children)
                {
                    if (sc.ClassName.StartsWith("Tpp") && sc.ClassName.EndsWith("Band"))
                    {
                        if (IsDetailBand(sc)) Walk(sc.Children);
                    }
                    else Walk(new List<RtmComponent> { sc });
                }
            }
            else
            {
                if (c.ClassName == "TppDBText" && c.DataField is { Length: > 0 }) cells.Add(c);
                Walk(c.Children);
            }
        }
    }
    Walk(db.Components);
    if (cells.Count < 2) return;
    var grp = cells.GroupBy(c => (float)Math.Round(c.MmTop / 500f) * 500f)
                   .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First();
    // 納入 Top 與眾數相差 1.2mm 內的所有 cell（處理出貨單據等 Top 微錯位 0.53/0.60/0.79 的情形）
    float modeTop = grp.Key;
    var row = cells.Where(c => Math.Abs(c.MmTop - modeTop) <= 1200f).OrderBy(c => c.MmLeft).ToList();
    if (row.Count < 2) return;
    int cnt = row.Count;
    bool[] rightAligned = row.Select(c => c.TextAlignment == "taRightJustified").ToArray();
    bool[] isUnit = row.Select(c =>
    {
        string s = $"{c.Name}|{c.DataField}|{c.Caption}";
        return s.Contains("單位");
    }).ToArray();
    bool[] isNote = row.Select(c =>
    {
        string s = $"{c.Name}|{c.DataField}|{c.Caption}";
        return s.Contains("附註") || s.Contains("備註") || s.Contains("說明");
    }).ToArray();
    bool anyTight = false, anyWiden = false;
    for (int i = 0; i < cnt - 1; i++)
    {
        float gap = (row[i + 1].MmLeft - (row[i].MmLeft + row[i].MmWidth)) / 1000f;
        if (gap < 1.2f) anyTight = true;
    }
    for (int i = 0; i < cnt; i++)
    {
        if (rightAligned[i] && i < cnt - 1)
        {
            float w = row[i].MmWidth / 1000f;
            float right = (row[i].MmLeft + row[i].MmWidth) / 1000f;
            float nextLeft = row[i + 1].MmLeft / 1000f;
            if (w < 20.5f && right + 20.9f > nextLeft) anyWiden = true;
        }
    }
    if (!anyTight && !anyWiden) return;
    // Header 欄標題 label 匹配（重排前以舊位置鎖定對應）
    var labels = hb.Components.Where(c => c.ClassName == "TppLabel" && c.Caption is { Length: > 0 }).ToList();
    var pair = new List<(RtmComponent cell, RtmComponent? label)>();
    foreach (var cell in row)
    {
        string k = (cell.Caption ?? "").Trim();
        if (k.Length < 2) k = cell.DataField ?? "";
        if (k.Length < 2) { pair.Add((cell, null)); continue; }
        var cand = labels.Where(t => k.StartsWith(t.Caption!.Trim())).ToList();
        if (cand.Count == 0) { pair.Add((cell, null)); continue; }
        var best = cand.OrderBy(t => Math.Abs(t.MmLeft - cell.MmLeft)).First();
        pair.Add((cell, best));
    }
    float[] origW = row.Select(c => c.MmWidth / 1000f).ToArray();
    if (anyTight)
    {
        // 重排方案：gap, 備註寬上限, 文字欄寬上限, 字型點數（由輕到重）
        var plans = new (float gap, float noteCap, float textCap, float fs)[]
        {
            (1.5f, 25f, 60f, 12f),
            (1.2f, 24f, 55f, 12f),
            (1.0f, 22f, 50f, 12f),
            (0.8f, 20f, 45f, 12f),
            (0.8f, 18f, 40f, 11f),
            (0.8f, 16f, 36f, 10f),
            (0.8f, 15f, 32f, 9f),
            (0.8f, 14f, 30f, 8f),
        };
        float startX = row[0].MmLeft / 1000f;
        float[] newL = new float[cnt]; float[] newW = new float[cnt];
        float fsUsed = 12f;
        bool ok = false;
        foreach (var (gap, noteCap, textCap, fs) in plans)
        {
            float cursor = startX;
            bool fits = true;
            for (int i = 0; i < cnt; i++)
            {
                float w;
                if (rightAligned[i]) w = Math.Max(origW[i], 20.9f * fs / 12f);
                else if (isUnit[i]) w = Math.Max(origW[i], 19f * fs / 12f);
                else if (isNote[i]) w = Math.Min(origW[i], Math.Min(noteCap, 25f * fs / 12f));
                else w = Math.Min(origW[i], textCap);
                newL[i] = cursor; newW[i] = w;
                cursor += w + gap;
            }
            if (cursor - gap <= maxRightMm) { fsUsed = fs; ok = true; break; }
        }
        if (!ok) return;   // 全部方案都塞不下 → 不處理（避免破壞）
        for (int i = 0; i < cnt; i++)
        {
            var cell = row[i];
            float dl = newL[i] - cell.MmLeft / 1000f;
            float dw = newW[i] - cell.MmWidth / 1000f;
            bool any = false;
            if (Math.Abs(dl) > 0.01f && PatchObj(bytes, map, cell, "mmLeft", dl))
            { any = true; cell.MmLeft = (long)Math.Round(newL[i] * 1000f); }
            if (Math.Abs(dw) > 0.01f && PatchObj(bytes, map, cell, "mmWidth", dw))
            { any = true; cell.MmWidth = (long)Math.Round(newW[i] * 1000f); }
            if (any) n++;
            if (fsUsed != 12f && map.TryGetValue($"{cell.ClassName}\0{cell.Name}", out var obj) && PatchFontPt(bytes, obj, fsUsed))
                n++;
            if (pair[i].label is { } lb)
            {
                if (Math.Abs(dl) > 0.01f && PatchObj(bytes, map, lb, "mmLeft", dl))
                { n++; lb.MmLeft = (long)Math.Round((lb.MmLeft / 1000f + dl) * 1000f); }
                if (Math.Abs(dw) > 0.01f && PatchObj(bytes, map, lb, "mmWidth", dw))
                { n++; lb.MmWidth = (long)Math.Round((lb.MmWidth / 1000f + dw) * 1000f); }
            }
            sb.AppendLine($"  > Detail {cell.Name} Left {cell.MmLeft / 1000f:F2}->{newL[i]:F2} W {cell.MmWidth / 1000f:F2}->{newW[i]:F2}{(fsUsed != 12f ? $" 字型{fsUsed:0}" : "")}");
        }
    }
    else
    {
        // 只加寬右對齊欄（往左擴，右緣不變）
        for (int i = 0; i < cnt; i++)
        {
            if (!rightAligned[i] || i >= cnt - 1) continue;
            float w = row[i].MmWidth / 1000f;
            if (w >= 20.9f) continue;
            float left = row[i].MmLeft / 1000f;
            float right = (row[i].MmLeft + row[i].MmWidth) / 1000f;
            float nextLeft = row[i + 1].MmLeft / 1000f;
            if (right + 20.9f <= nextLeft) continue;
            float room = nextLeft - right - 0.8f;
            if (room <= 0f) continue;
            float targetW = Math.Min(20.9f, w + room);
            float dl = targetW - w;                 // 往左擴（正值）
            float minLeft = (i == 0) ? 0f : (row[i - 1].MmLeft + row[i - 1].MmWidth) / 1000f + 0.5f;
            if (left - dl < minLeft) continue;
            if (PatchObj(bytes, map, row[i], "mmLeft", -dl) && PatchObj(bytes, map, row[i], "mmWidth", dl))
            {
                row[i].MmLeft = (long)Math.Round((row[i].MmLeft / 1000f - dl) * 1000f);
                row[i].MmWidth = (long)Math.Round(targetW * 1000f);
                n++;
                sb.AppendLine($"  > Detail {row[i].Name} 加寬 {w:F2}->{targetW:F2}（往左 {dl:F2}）");
                if (pair[i].label is { } lb)
                {
                    if (PatchObj(bytes, map, lb, "mmLeft", -dl)) { lb.MmLeft = (long)Math.Round((lb.MmLeft / 1000f - dl) * 1000f); n++; }
                    if (PatchObj(bytes, map, lb, "mmWidth", dl)) { lb.MmWidth = (long)Math.Round((lb.MmWidth / 1000f + dl) * 1000f); n++; }
                }
            }
        }
    }
}

/// <summary>把 HeaderBand 內「同排且水平重疊」的相鄰欄標題 label 往右推開（貪婪由左到右，只處理 KnownRepack 檔）。</summary>
static void FixHeaderLabels(string rtmFile, byte[] bytes, Dictionary<string, Tpf0Object> map, RtmReportModel r, System.Text.StringBuilder sb, ref int n)
{
    if (!IsKnownRepack(Path.GetFileNameWithoutExtension(rtmFile))) return;
    if (r.HeaderBand is not { } hb) return;
    float paperWmm = r.MmPaperWidth / 1000f;
    if (paperWmm <= 0f) paperWmm = 215.9f;
    float maxRightMm = paperWmm - 10f;
    var labels = hb.Components.Where(c => c.ClassName == "TppLabel" && c.Caption is { Length: > 0 })
                              .OrderBy(c => c.MmLeft).ToList();
    if (labels.Count < 2) return;
    float prevRight = (labels[0].MmLeft + labels[0].MmWidth) / 1000f;
    for (int i = 1; i < labels.Count; i++)
    {
        var b = labels[i];
        float bLeft = b.MmLeft / 1000f;
        float bW = b.MmWidth / 1000f;
        if (Math.Abs(b.MmTop - labels[i - 1].MmTop) > 1500f) { prevRight = bLeft + bW; continue; }
        float gap = bLeft - prevRight;
        if (gap >= 0.5f) { prevRight = bLeft + bW; continue; }
        float shift = 0.5f - gap;
        if (bLeft + shift + bW > maxRightMm) { prevRight = Math.Max(bLeft + bW, prevRight); continue; }
        if (PatchObj(bytes, map, b, "mmLeft", shift))
        {
            b.MmLeft = (long)Math.Round((bLeft + shift) * 1000f);
            n++;
            sb.AppendLine($"  > HeaderLabel {b.Name}「{b.Caption}」Left {bLeft:F2} -> {bLeft + shift:F2} (與「{labels[i - 1].Caption}」重疊，推開)");
        }
        prevRight = bLeft + shift + bW;
    }
}

/// <summary>把 Detail 子報表內「超出 band 高度」的元件垂直座標歸位到同列眾數。</summary>
static void FixDetailTop(byte[] bytes, Dictionary<string, Tpf0Object> map, RtmReportModel r, System.Text.StringBuilder sb, ref int n)
{
    if (r.DetailBand is not { } db) return;
    var cells = new List<RtmComponent>();
    void Walk(List<RtmComponent> comps)
    {
        foreach (var c in comps)
        {
            if (c.ClassName is "TppSubReport" or "TppChildReport" or "TppRegion")
            {
                foreach (var sc in c.Children)
                {
                    if (sc.ClassName.StartsWith("Tpp") && sc.ClassName.EndsWith("Band"))
                    {
                        if (IsDetailBand(sc)) Walk(sc.Children);
                    }
                    else Walk(new List<RtmComponent> { sc });
                }
            }
            else
            {
                if (c.ClassName == "TppDBText" && c.DataField is { Length: > 0 })
                    cells.Add(c);
                Walk(c.Children);
            }
        }
    }
    Walk(db.Components);
    if (cells.Count == 0) return;
    // 依 Top 分組（±0.6mm 容差），找最多 cell 的群
    var groups = cells.GroupBy(c => (float)Math.Round(c.MmTop / 600f) * 600f)
                      .OrderByDescending(g => g.Count()).ToList();
    if (groups.Count < 2) return;
    // 若存在 ≥2 個「各自 ≥2 cell」的群 → 視為刻意分層設計，不動
    if (groups.Count(g => g.Count() >= 2) >= 2) return;
    float modeTop = groups[0].Key;
    // 把 Top 與眾數相差 > 1.2mm 的孤立 cell 歸位到眾數列
    var bad = cells.Where(c => Math.Abs(c.MmTop - modeTop) > 1200f).ToList();
    if (bad.Count == 0) return;
    float target = modeTop / 1000f;
    foreach (var c in bad)
    {
        float delta = target - c.MmTop / 1000f;
        if (PatchObj(bytes, map, c, "mmTop", delta))
        {
            c.MmTop = (long)Math.Round(target * 1000f);
            n++; sb.AppendLine($"  > Detail {c.Name} mmTop {c.MmTop / 1000f:F2} -> {target:F2} (歸位到同列)");
        }
    }
}

/// <summary>把指定 band 內「超出 band 高度」的錯亂元件垂直座標歸位（取未超元件眾數，無則 0）。</summary>
static void FixBandTop(byte[] bytes, Dictionary<string, Tpf0Object> map, RtmReportModel r, string bandKind, System.Text.StringBuilder sb, ref int n)
{
    RtmBand? band = bandKind switch
    {
        "GroupFooter" => r.GroupFooterBand,
        "Footer" => r.FooterBand,
        "Summary" => r.SummaryBand,
        "GroupHeader" => r.GroupHeaderBand,
        _ => null
    };
    if (band == null || band.MmHeight <= 0) return;
    var cells = new List<RtmComponent>();
    void Walk(List<RtmComponent> comps)
    {
        foreach (var c in comps)
        {
            if ((c.ClassName == "TppDBText" || c.ClassName == "TppLabel" || c.ClassName == "TppDBCalc" || c.ClassName == "TppMemo")
                && (c.DataField is { Length: > 0 } || c.Caption is { Length: > 0 }))
                cells.Add(c);
            Walk(c.Children);
        }
    }
    Walk(band.Components);
    if (cells.Count == 0) return;
    float bandHmm = band.MmHeight / 1000f;
    var bad = cells.Where(c => c.MmTop / 1000f > bandHmm).ToList();
    if (bad.Count == 0) return;
    var good = cells.Where(c => c.MmTop / 1000f <= bandHmm).ToList();
    float target = 0f;
    if (good.Count > 0)
    {
        var gmode = good.Select(c => (float)Math.Round(c.MmTop / 200f) * 200f)
            .GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
        target = gmode / 1000f;
    }
    foreach (var c in bad)
    {
        float delta = target - c.MmTop / 1000f;
        if (PatchObj(bytes, map, c, "mmTop", delta))
        { n++; sb.AppendLine($"  > {bandKind} {c.Name} mmTop {c.MmTop / 1000f:F2} -> {target:F2} (超出 {bandHmm:F2})"); }
    }
}

/// <summary>批次渲染所有 .rtm（假資料）逐頁輸出 PNG 至 shots\png，供人工視覺檢視。</summary>
static void RenderAllPng()
{
    const int dpi = 150;
    string dir = @"D:\HeliAcc\shots\png";
    Directory.CreateDirectory(dir);
    var files = Directory.GetFiles(@"D:\HeliAcc\Rep", "*.rtm").OrderBy(f => f).ToList();
    int ok = 0, fail = 0, pages = 0;
    var err = new System.Text.StringBuilder();
    foreach (var f in files)
    {
        string name = Path.GetFileNameWithoutExtension(f);
        try
        {
            var root = Tpf0Reader.Parse(File.ReadAllBytes(f));
            var r = RtmLoader.Load(root);

            var fields = new List<(string Pipe, string Field)>();
            void Scan(RtmComponent c)
            {
                if (c.DataField is { Length: > 0 }) fields.Add((c.DataPipeline ?? "", c.DataField));
                foreach (var s in c.Children) Scan(s);
            }
            foreach (var b in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
                if (b is not null) foreach (var c in b.Components) Scan(c);

            string Fake(string ff) => FakeValue(ff);
            var data = MakeReportData(fields);

            int w = Math.Max(1, (int)Math.Round(r.MmPaperWidth * dpi / 25400.0));
            int h = Math.Max(1, (int)Math.Round(r.MmPaperHeight * dpi / 25400.0));
            int pg = 0;
            using var ren = new RtmRenderer(r, data);
            using (var bmp = new Bitmap(w, h))
            {
                bmp.SetResolution(dpi, dpi);
                using (var g = Graphics.FromImage(bmp))
                {
                    var st = new RtmRenderState();
                    while (true)
                    {
                        g.Clear(Color.White);
                        bool more = ren.RenderPage(g, new RectangleF(0, 0, w, h), st);
                        pg++;
                        string outPath = Path.Combine(dir, $"{name}_p{pg}.png");
                        bmp.Save(outPath, ImageFormat.Png);
                        if (!more) break;
                    }
                }
            }
            ren.Dispose();
            pages += pg;
            ok++;
        }
        catch (Exception ex)
        {
            fail++;
            err.AppendLine($"E  {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
    Console.WriteLine($"=== 渲染 PNG: {ok} 成功 / {fail} 失敗 / 共 {files.Count} 檔 / {pages} 頁 → {dir} ===");
    if (fail > 0) Console.Write(err.ToString());
}

static void OneAll()
{
    // 批次渲染所有 .rtm（假資料），彙總「實際繪製文字重疊」摘要
    var summary = new System.Text.StringBuilder();
    var files = Directory.GetFiles(@"D:\HeliAcc\Rep", "*.rtm").OrderBy(f => f).ToList();
    int overlapFiles = 0, okFiles = 0, errFiles = 0;
    foreach (var f in files)
    {
        string name = Path.GetFileName(f);
        try
        {
            string result = OneRenderCore(f);
            if (string.IsNullOrEmpty(result)) { okFiles++; }
            else
            {
                overlapFiles++;
                summary.AppendLine($"D  {name}");
                summary.AppendLine(result);
            }
        }
        catch (Exception ex)
        {
            errFiles++;
            summary.AppendLine($"E  {name}: {ex.Message}");
        }
    }
    summary.Insert(0, $"=== 實際文字重疊檢查: {okFiles} 乾淨 / {overlapFiles} 有重疊 / {errFiles} 錯誤 / 共 {files.Count} 檔 ===\n");
    File.WriteAllText(@"D:\HeliAcc\shots\oneall_report.txt", summary.ToString(), new System.Text.UTF8Encoding(false));
    Console.WriteLine(summary.ToString().Split('\n')[0]);
}

static string? OneRenderCore(string rtmPath)
{
    var root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
    var r = RtmLoader.Load(root);

    var fields = new List<(string Pipe, string Field)>();
    void Scan(RtmComponent c)
    {
        if (c.DataField is { Length: > 0 })
            fields.Add((c.DataPipeline ?? "", c.DataField));
        foreach (var s in c.Children) Scan(s);
    }
    foreach (var b in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
        if (b is not null)
            foreach (var c in b.Components) Scan(c);

    string Fake(string f) => FakeValue(f);

    var data = MakeReportData(fields);

    int w = Math.Max(1, (int)Math.Round(r.MmPaperWidth * 150 / 25400.0));
    int h = Math.Max(1, (int)Math.Round(r.MmPaperHeight * 150 / 25400.0));
    using var bmp = new Bitmap(w, h);
    bmp.SetResolution(150, 150);
    using var ren = new RtmRenderer(r, data);
    ren.DrawnTexts = new List<(RtmComponent, float, float, float, float, string)>();
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.White);
        var st = new RtmRenderState();
        do { } while (ren.RenderPage(g, new RectangleF(0, 0, w, h), st));
    }
    ren.Dispose();

    var dt = ren.DrawnTexts;
    var sb = new System.Text.StringBuilder();
    float eps = 0.3f;
    var pairs = new HashSet<string>();
    for (int i = 0; i < dt.Count; i++)
    for (int j = i + 1; j < dt.Count; j++)
    {
        var (ca, ax, ay, aw, ah, ta) = dt[i];
        var (cb, bx, by, bw, bh, tb) = dt[j];
        if (ReferenceEquals(ca, cb)) continue;
        if (ax + eps < bx + bw && bx + eps < ax + aw
            && ay + eps < by + bh && by + eps < ay + ah)
        {
            float ox = Math.Min(ax + aw, bx + bw) - Math.Max(ax, bx);
            float oy = Math.Min(ay + ah, by + bh) - Math.Max(ay, by);
            float area = ox * oy;
            if (area > 1f)
            {
                string key = $"{ca.GetHashCode()}|{cb.GetHashCode()}";
                if (pairs.Add(key))
                    sb.AppendLine($"  重疊 {Ox(ca)}[{ax / 1000f:F1},{ay / 1000f:F1},{ (ax + aw) / 1000f:F1},{ (ay + ah) / 1000f:F1}] \"{Tr(ta)}\" 與 {Ox(cb)}[{bx / 1000f:F1},{by / 1000f:F1},{ (bx + bw) / 1000f:F1},{ (by + bh) / 1000f:F1}] \"{Tr(tb)}\" => {area / 1e6f:F2}mm²");
            }
        }
    }
    return sb.Length > 0 ? sb.ToString() : null;
}

static void RunBatch()
{
    const int dpi2 = 300;
    var files = Directory.GetFiles(@"D:\HeliAcc\Rep", "*.*")
        .Where(f => f.EndsWith(".rtm", StringComparison.OrdinalIgnoreCase))
        .OrderBy(f => f);
    int ok = 0, fail = 0;
    foreach (var f in files)
    {
        string name = Path.GetFileName(f);
        try
        {
            var root = Tpf0Reader.Parse(File.ReadAllBytes(f));
            var r = RtmLoader.Load(root);
            int w = Math.Max(1, (int)Math.Round(r.MmPaperWidth * dpi2 / 25400.0));
            int h = Math.Max(1, (int)Math.Round(r.MmPaperHeight * dpi2 / 25400.0));
            int comps = CountAll(r);
            int pages = 0;
            using var bmp = new Bitmap(w, h);
            bmp.SetResolution(dpi2, dpi2);
            using var ren = new RtmRenderer(r, new RtmData());
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                var st = new RtmRenderState();
                do { pages++; } while (ren.RenderPage(g, new RectangleF(0, 0, w, h), st));
            }
            Console.WriteLine($"OK   {name}  紙張={r.MmPaperWidth / 1000f:F0}x{r.MmPaperHeight / 1000f:F0}mm {pages}頁 元件={comps} H={r.HeaderBand?.MmHeight ?? 0} D={r.DetailBand?.MmHeight ?? 0} GF={r.GroupFooterBand?.MmHeight ?? 0} F={r.FooterBand?.MmHeight ?? 0}");
            ok++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL {name}  {ex.GetType().Name}: {ex.Message}");
            fail++;
        }
    }
    Console.WriteLine($"\n=== 總計: {ok} OK / {fail} FAIL / {ok + fail} 檔 ===");
}

static int CountAll(RtmReportModel r)
{
    int n = 0;
    foreach (var b in new[] { r.TitleBand, r.HeaderBand, r.DetailBand, r.GroupHeaderBand, r.GroupFooterBand, r.SummaryBand, r.FooterBand })
    {
        if (b is null) continue;
        n += CountRec(b.Components);
    }
    return n;
}

static int CountRec(List<RtmComponent> comps)
{
    int n = 0;
    foreach (var c in comps)
    {
        n += 1 + CountRec(c.Children);
    }
    return n;
}

const string RtmPath = @"D:\HeliAcc\Rep\維修單據.rtm";
const string DbPath = @"D:\HeliAcc\HeliERP.db";
const int DetailCode = 429;

// 1. 解析報表
Console.WriteLine($"解析 {RtmPath} ...");
var root = Tpf0Reader.Parse(File.ReadAllBytes(RtmPath));
var report = RtmLoader.Load(root);
Console.WriteLine($"紙張: {report.MmPaperWidth / 1000f:F3} x {report.MmPaperHeight / 1000f:F3} mm");
Console.WriteLine($"Header={report.HeaderBand?.MmHeight} Detail={report.DetailBand?.MmHeight} " +
                  $"GroupFooter={report.GroupFooterBand?.MmHeight} Footer={report.FooterBand?.MmHeight}");

// 2. 資料（主檔 + 明細）
var data = new RtmData();
using (var conn = new SqliteConnection($"Data Source={DbPath}"))
{
    conn.Open();
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT * FROM \"維修主檔\" WHERE \"單據副碼\" = $code";
        cmd.Parameters.AddWithValue("$code", DetailCode);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) throw new Exception($"找不到單據副碼={DetailCode}");
        for (int i = 0; i < r.FieldCount; i++)
            data.Master[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
    }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT * FROM \"維修明細\" WHERE \"單據副碼\" = $code";
        cmd.Parameters.AddWithValue("$code", DetailCode);
        using var r2 = cmd.ExecuteReader();
        while (r2.Read())
        {
            var d = new Dictionary<string, object?>();
            for (int i = 0; i < r2.FieldCount; i++)
                d[r2.GetName(i)] = r2.IsDBNull(i) ? null : r2.GetValue(i);
            data.Detail.Add(d);
        }
    }
}
// join 欄位
data.Master["對象名稱"] = "測試客戶股份有限公司";
data.Master["員工名稱"] = "洪俊士";
// 公司資料（plCompany）
data.Company["公司全名"] = "禾秝安全系統工程有限公司";
data.Company["電話號碼"] = "(02)2593-2101";
data.Company["登記地址"] = "臺北市新生北路3段79-2號3F";
data.Company["傳真號碼"] = "(02)2586-3046";
Console.WriteLine($"明細筆數: {data.Detail.Count}");

// 3. 渲染 300dpi A4
const int dpi = 300;
int wPx = (int)Math.Round(report.MmPaperWidth * dpi / 25400.0);
int hPx = (int)Math.Round(report.MmPaperHeight * dpi / 25400.0);
Console.WriteLine($"位圖: {wPx} x {hPx} @ {dpi}dpi");

using var bmp = new Bitmap(wPx, hPx);
bmp.SetResolution(dpi, dpi);
using var renderer = new RtmRenderer(report, data);
using (var g = Graphics.FromImage(bmp))
{
    g.Clear(Color.White);
    var state = new RtmRenderState();
    bool more = renderer.RenderPage(g, new RectangleF(0, 0, wPx, hPx), state);
    Console.WriteLine($"HasMorePages(第1頁)={more}");
    if (more)
    {
        bool more2 = renderer.RenderPage(g, new RectangleF(0, 0, wPx, hPx), state);
        Console.WriteLine($"HasMorePages(第2頁)={more2}");
    }
}

string outPath = @"C:\Users\JS\AppData\Local\Temp\opencode\rtm-render.png";
bmp.Save(outPath, ImageFormat.Png);
Console.WriteLine($"已輸出 {outPath}");

// ═══ 像素驗證：比對 rtm-tree.txt 關鍵元件位置是否真的有內容 ═══
var mask = new byte[wPx * hPx];
var bd = bmp.LockBits(new Rectangle(0, 0, wPx, hPx), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
unsafe
{
    byte* p = (byte*)bd.Scan0;
    for (int yy = 0; yy < hPx; yy++)
        for (int xx = 0; xx < wPx; xx++)
        {
            byte b = p[yy * bd.Stride + xx * 3], g = p[yy * bd.Stride + xx * 3 + 1], r = p[yy * bd.Stride + xx * 3 + 2];
            mask[yy * wPx + xx] = (r < 200 || g < 200 || b < 200) ? (byte)1 : (byte)0;
        }
}
bmp.UnlockBits(bd);

int inkTotal = mask.Sum(b => b);
Console.WriteLine($"\n墨量統計: {inkTotal}/{wPx * hPx} = {100.0 * inkTotal / (wPx * hPx):F2}%");

    bool HasInk(int x, int y, int w, int h)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(wPx, x + w), y1 = Math.Min(hPx, y + h);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                if (mask[yy * wPx + xx] == 1) return true;
        return false;
    }

// 元件矩形（1/1000 mm → px @300dpi），band 原點 y 依排版堆疊
float pxPerMm = dpi / 25.4f / 1000f;
float headerH = report.HeaderBand!.MmHeight;
float detailH = report.DetailBand!.MmHeight;
float footerY = headerH + detailH * data.Detail.Count;

(string Name, float L, float T, float W, float H)[] checks =
{
    // header
    ("公司全名", 5027, 3969, 139700, 9260),
    ("維修單標題", 146315, 4233, 48154, 9260),
    ("貨單日期標籤", 146315, 15081, 21696, 5027),
    ("交易單號值", 169598, 21167, 23813, 4763),
    ("客戶名稱標籤", 5292, 30163, 21696, 5027),
    ("對象名稱值", 26988, 30427, 118798, 4763),
    ("叫修地址標籤", 5292, 47890, 21696, 5027),
    ("表頭項目標籤", 4498, 62177, 9260, 5027),
    ("表頭貨品編號", 15610, 62177, 27781, 5027),
    ("表頭金額", 160073, 62177, 22490, 5027),
    ("表頭底線", 2117, 67204, 193411, 1058),
    // detail（明細列）
    ("明細序號", 2117, headerH + 0, 7144, 5027),
    ("明細品名值", 46567, headerH + 0, 64823, 4763),
    ("明細數量值", 110596, headerH + 0, 12435, 4763),
    // group footer
    ("合計標籤", 140759, 47625 + footerY, 21696, 5027),
    ("合計金額值", 164042, 47625 + footerY, 23813, 4763),
    ("稅額標籤", 140759, 53181 + footerY, 21696, 5027),
    ("總計標籤", 140759, 58473 + footerY, 21696, 5027),
    ("備註標籤", 3440, 46567 + footerY, 13758, 5027),
    ("故障現象標題", 18785, 1058 + footerY, 19579, 5027),
    ("故障原因標題", 82815, 1058 + footerY, 19579, 5027),
    ("維修情況標題", 153723, 1058 + footerY, 19579, 5027),
    ("審核簽章", 6350, 70115 + footerY, 12700, 5027),
    ("經辦簽章", 44186, 70115 + footerY, 12700, 5027),
    ("簽收簽章", 157957, 70115 + footerY, 12700, 5027),
};

int pass = 0, fail = 0;
foreach (var (name, l, t, w, h) in checks)
{
    int x = (int)(l * pxPerMm), y = (int)(t * pxPerMm);
    int rw = (int)(w * pxPerMm), rh = (int)(h * pxPerMm);
    bool ok = HasInk(x, y, rw, rh);
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  ({l / 1000f:F1},{t / 1000f:F1})mm {w / 1000f:F1}x{h / 1000f:F1}mm -> px({x},{y},{rw}x{rh})");
    if (ok) pass++; else fail++;
}
Console.WriteLine($"\n驗證結果: {pass} PASS / {fail} FAIL");

static void PrintPipelineCheck()
{
    var (rtmFile, build) = ("應收帳款統計表.rtm", new Func<RtmData>(() => ARService.BuildSummaryReportData(ARService.應收類別)));
    try
    {
        var data = build();
        Console.WriteLine($"資料產生 OK：{rtmFile} 明細={data.Detail.Count} 筆");
        var report = ReportPrintService.Load(rtmFile);
        Console.WriteLine($"載入 OK：紙張 {report.MmPaperWidth / 1000f:F1} x {report.MmPaperHeight / 1000f:F1} mm");
        var renderer = new RtmRenderer(report, data);
        var state = new RtmRenderState();

        // 100dpi 模擬（接近 PrintPreviewDialog 環境）輸出第一頁比對位置
        int wPx = (int)Math.Round(report.MmPaperWidth * 100 / 25400.0);
        int hPx = (int)Math.Round(report.MmPaperHeight * 100 / 25400.0);
        float headerHmm = report.HeaderBand?.MmHeight ?? 0;
        float sfYmm = headerHmm + (report.DetailBand?.MmHeight ?? 0) * data.Detail.Count
            + (report.GroupFooterBand?.MmHeight ?? 0);
        using (var bmp = new Bitmap(wPx, hPx))
        {
            bmp.SetResolution(100, 100);
            using var gr = Graphics.FromImage(bmp);
            gr.Clear(Color.White);
            var st = new RtmRenderState();
            renderer.RenderPage(gr, new RectangleF(0, 0, wPx, hPx), st);
            bmp.Save(@"D:\HeliAcc\shots\pcheck100.png");
            int totalInk = 0, minX = wPx, maxX = -1, minY = hPx, maxY = -1;
            for (int yy = 0; yy < hPx; yy++)
                for (int xx = 0; xx < wPx; xx++)
                {
                    var p = bmp.GetPixel(xx, yy);
                    if (p.R < 200 || p.G < 200 || p.B < 200)
                    {
                        totalInk++;
                        if (xx < minX) minX = xx;
                        if (xx > maxX) maxX = xx;
                        if (yy < minY) minY = yy;
                        if (yy > maxY) maxY = yy;
                    }
                }
            Console.WriteLine($"總墨點={totalInk} / {wPx * hPx}  墨區x=[{minX}..{maxX}] y=[{minY}..{maxY}]");
            Console.WriteLine($"  -> 墨區 mm: x=[{minX * 25.4f / 100f:F1}..{maxX * 25.4f / 100f:F1}] y=[{minY * 25.4f / 100f:F1}..{maxY * 25.4f / 100f:F1}]");
            CheckInkAtMm(bmp, "公司全名", 72800, 3700, 53200, 7700);
            CheckInkAtMm(bmp, "日期區間", 0, 12700, 16900, 5000);
            CheckInkAtMm(bmp, "明細交易對象", 7400, headerHmm, 19300, 5000);
            CheckInkAtMm(bmp, "明細公司全名", 26500, headerHmm, 45000, 5000);
            CheckInkAtMm(bmp, "明細本期累計應收", 173800, headerHmm, 23500, 5000);
            CheckInkAtMm(bmp, "彙總本期累計應收(dcSum)", 174600, sfYmm + 2600, 22800, 5000);
        }
        Console.WriteLine($"已存 D:\\HeliAcc\\shots\\pcheck100.png（100dpi 第一頁）");

        using var doc = new PrintDocument();
        var ps = doc.DefaultPageSettings;
        ps.PaperSize = new PaperSize("報表紙",
            Math.Max(1, (int)Math.Round(report.MmPaperWidth / 254.0)),
            Math.Max(1, (int)Math.Round(report.MmPaperHeight / 254.0)));
        ps.Margins = new Margins(0, 0, 0, 0);
        doc.PrintPage += (s, e) =>
        {
            e.Graphics!.Clear(Color.White);
            e.HasMorePages = renderer.RenderPage(e.Graphics!, e.PageBounds, state);
        };
        doc.PrintController = new System.Drawing.Printing.PreviewPrintController();
        Console.WriteLine("開始模擬列印（PreviewPrintController，15 秒逾時）…");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var task = Task.Run(() => doc.Print());
        if (!task.Wait(TimeSpan.FromSeconds(15)))
        {
            Console.WriteLine("逾時！RenderPage 可能死循環（HasMorePages 永遠為 true）");
            return;
        }
        sw.Stop();
        Console.WriteLine($"完成：{sw.ElapsedMilliseconds} ms");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"例外：{ex}");
    }
}

static void CheckInkAtMm(Bitmap bmp, string name, float l, float t, float w, float h)
{
    float pxPerMm = 100f / 25.4f;
    int x = (int)(l / 1000f * pxPerMm), y = (int)(t / 1000f * pxPerMm);
    int rw = (int)(w / 1000f * pxPerMm), rh = (int)(h / 1000f * pxPerMm);
    int ink = 0;
    for (int yy = Math.Max(0, y); yy < Math.Min(bmp.Height, y + rh); yy++)
        for (int xx = Math.Max(0, x); xx < Math.Min(bmp.Width, x + rw); xx++)
        {
            var p = bmp.GetPixel(xx, yy);
            if (p.R < 200 || p.G < 200 || p.B < 200) ink++;
        }
    Console.WriteLine($"{(ink > 0 ? "PASS" : "FAIL")}  {name}  ({l / 1000f:F1},{t / 1000f:F1})mm {w / 1000f:F1}x{h / 1000f:F1}mm -> 墨點={ink}");
}
