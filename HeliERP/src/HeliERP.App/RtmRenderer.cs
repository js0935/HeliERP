// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace HeliERP.App;

/// <summary>報表元件（由 Tpf0Object 轉成渲染用的平面模型）。座標單位：1/1000 mm。</summary>
public sealed class RtmComponent
{
    public string ClassName = "";
    public string Name = "";
    public float MmLeft, MmTop, MmWidth, MmHeight;

    // 文字
    public string? Caption, DataField, DataPipeline, TextAlignment, DisplayFormat, DbCalcType, VarType;
    public string FontName = "新細明體";
    public float FontSize = 12f;
    public bool FontBold;
    public Color ForeColor = Color.Black;
    public bool WordWrap;

    // 線條
    public string? LinePosition;   // lpTop / lpMiddle / lpBottom / lpLeft / lpRight
    public float PenWeight = 0.75f;
    public Color PenColor = Color.Green;

    // 形狀
    public string? ShapeType;      // stRectangle / stRoundRect / stEllipse 等

    /// <summary>實際繪製文字時的文字來源（Caption / DataField / 計算值），供重疊診斷。</summary>
    public string TextSource = "";

    /// <summary>所屬 band 種類（Header / Detail / Footer / GroupHeader / GroupFooter）。</summary>
    public string BandKind = "";

    /// <summary>子報表 / 子 band 的元件。</summary>
    public List<RtmComponent> Children = new();
}

/// <summary>報表 band（Header/Detail/Footer/GroupHeader/GroupFooter）。</summary>
public sealed class RtmBand
{
    public string Kind = "";       // Header / Detail / Footer / GroupHeader / GroupFooter
    public float MmHeight;
    public List<RtmComponent> Components = new();
}

/// <summary>解析 .rtm 後的報表模型（座標為 1/1000 mm，原點 = 整頁左上角含邊界）。</summary>
public sealed class RtmReportModel
{
    public float MmPaperWidth = 210079f;
    public float MmPaperHeight = 297127f;
    public RtmBand? TitleBand, HeaderBand, DetailBand, FooterBand, GroupHeaderBand, GroupFooterBand, SummaryBand;
    /// <summary>分組欄位（TppGroup.BreakName）：明細列此欄位值變化時印 GroupHeader／GroupFooter。</summary>
    public string? GroupBy;
    /// <summary>分組欄位所屬管線。</summary>
    public string? GroupPipeline;
}

/// <summary>報表資料來源：依 DataPipeline 名稱提供欄位值。</summary>
public sealed class RtmData
{
    /// <summary>主檔資料（ppDBPipeline1）。</summary>
    public Dictionary<string, object?> Master = new();
    /// <summary>公司基本資料（plCompany）。</summary>
    public Dictionary<string, object?> Company = new();
    /// <summary>明細資料（每列一個字典）。</summary>
    public List<Dictionary<string, object?>> Detail = new();
    /// <summary>明細資料所屬管線名稱（單據類報表為 ppDBPipeline2；純明細報表常為 ppDBPipeline1）。</summary>
    public string DetailPipeline { get; set; } = "ppDBPipeline2";

    public object? GetValue(string pipeline, string field, int detailIndex)
    {
        if (pipeline == "plCompany")
            return Company.TryGetValue(field, out var c) ? c : null;
        if (pipeline == DetailPipeline)
        {
            // Header/Title 帶內引用明細 pipeline 的元件（如貨品存貨異動明細表標頭）回退取首筆明細
            if (detailIndex < 0) detailIndex = 0;
            if (detailIndex < Detail.Count)
                return Detail[detailIndex].TryGetValue(field, out var d) ? d : null;
        }
        // 主從報表：主檔 pipeline 欄位若該列已提供前綴鍵（{pipeline}|{field}）優先取當列值
        if (detailIndex >= 0 && detailIndex < Detail.Count
            && Detail[detailIndex].TryGetValue($"{pipeline}|{field}", out var pfx))
            return pfx;
        return Master.TryGetValue(field, out var m) ? m : null;
    }
}

/// <summary>跨頁渲染狀態（PrintPage 事件間保留）。</summary>
public sealed class RtmRenderState
{
    public int DetailIndex;        // 下一筆要畫的明細列索引
    public bool GroupFooterDone;   // 組尾是否已畫完
    public bool SummaryDone;       // 彙總區是否已畫完
}

/// <summary>把 Tpf0Object 物件樹轉成 RtmReportModel。</summary>
public static class RtmLoader
{
    public static RtmReportModel Load(Tpf0Object root)
    {
        var m = new RtmReportModel();
        m.MmPaperWidth = GetFloat(root, "PrinterSetup.mmPaperWidth", m.MmPaperWidth);
        m.MmPaperHeight = GetFloat(root, "PrinterSetup.mmPaperHeight", m.MmPaperHeight);

        foreach (var child in root.Children)
        {
            switch (child.ClassName)
            {
                case "TppTitleBand": m.TitleBand = LoadBand(child, "Title"); break;
                case "TppHeaderBand": m.HeaderBand = LoadBand(child, "Header"); break;
                case "TppDetailBand": m.DetailBand = LoadBand(child, "Detail"); break;
                case "TppFooterBand": m.FooterBand = LoadBand(child, "Footer"); break;
                case "TppSummaryBand": m.SummaryBand = LoadBand(child, "Summary"); break;
                case "TppGroup":
                    m.GroupBy = GetString(child, "BreakName");
                    m.GroupPipeline = GetString(child, "DataPipeline") ?? GetString(child, "DataPipelineName");
                    foreach (var gc in child.Children)
                    {
                        if (gc.ClassName == "TppGroupHeaderBand")
                            m.GroupHeaderBand = LoadBand(gc, "GroupHeader");
                        else if (gc.ClassName == "TppGroupFooterBand")
                            m.GroupFooterBand = LoadBand(gc, "GroupFooter");
                    }
                    break;
            }
        }
        return m;
    }

    private static RtmBand LoadBand(Tpf0Object obj, string kind)
    {
        var b = new RtmBand { Kind = kind, MmHeight = GetFloat(obj, "mmHeight", 0f) };
        foreach (var c in obj.Children)
        {
            var comp = LoadComponent(c);
            if (comp is not null)
            {
                comp.BandKind = kind;
                b.Components.Add(comp);
            }
        }
        return b;
    }

    private static RtmComponent? LoadComponent(Tpf0Object obj)
    {
        var c = new RtmComponent
        {
            ClassName = obj.ClassName,
            Name = obj.Name,
            MmLeft = GetFloat(obj, "mmLeft", 0f),
            MmTop = GetFloat(obj, "mmTop", 0f),
            MmWidth = GetFloat(obj, "mmWidth", 0f),
            MmHeight = GetFloat(obj, "mmHeight", 0f),
        };

        switch (obj.ClassName)
        {
            case "TppLabel":
                c.Caption = GetString(obj, "Caption");
                c.TextAlignment = GetString(obj, "TextAlignment");
                LoadFont(obj, c);
                break;
            case "TppDBText":
                c.DataField = GetString(obj, "DataField");
                c.DataPipeline = GetString(obj, "DataPipeline") ?? GetString(obj, "DataPipelineName");
                c.DisplayFormat = GetString(obj, "DisplayFormat");
                c.TextAlignment = GetString(obj, "TextAlignment");
                c.WordWrap = GetBool(obj, "WordWrap");
                LoadFont(obj, c);
                break;
            case "TppDBMemo":
                c.DataField = GetString(obj, "DataField");
                c.DataPipeline = GetString(obj, "DataPipeline") ?? GetString(obj, "DataPipelineName");
                c.WordWrap = true;
                LoadFont(obj, c);
                break;
            case "TppDBCalc":
                c.DataField = GetString(obj, "DataField");
                c.DbCalcType = GetString(obj, "DBCalcType");
                c.DataPipeline = GetString(obj, "DataPipeline") ?? GetString(obj, "DataPipelineName");
                c.TextAlignment = GetString(obj, "TextAlignment");
                c.DisplayFormat = GetString(obj, "DisplayFormat");
                LoadFont(obj, c);
                break;
            case "TppSystemVariable":
                c.VarType = GetString(obj, "VarType") ?? GetString(obj, "VarTypeName");
                c.Caption = GetString(obj, "Caption");
                c.TextAlignment = GetString(obj, "TextAlignment");
                LoadFont(obj, c);
                break;
            case "TppLine":
                c.LinePosition = GetString(obj, "Position");
                c.PenWeight = GetFloat(obj, "Weight", 0.75f);
                c.PenColor = ParseColor(GetString(obj, "Pen.Color") ?? "clBlack");
                break;
            case "TppShape":
                c.ShapeType = GetString(obj, "ShapeType");
                LoadFont(obj, c);
                break;
            case "TppImage":
            case "TppSubReport":
            case "TppChildReport":
            case "TppHeaderBand":
            case "TppDetailBand":
            case "TppFooterBand":
            case "TppSummaryBand":
            case "TppGroupHeaderBand":
            case "TppGroupFooterBand":
            case "TppRegion":
                // 子報表 / 子 band：遞迴載入 children；TppImage 圖形資料 v1 不支援
                foreach (var sub in obj.Children)
                {
                    var sc = LoadComponent(sub);
                    if (sc is not null) c.Children.Add(sc);
                }
                break;
            default:
                // 未知元件（如 TppParameterList 內物件）略過
                return null;
        }
        return c;
    }

    private static void LoadFont(Tpf0Object obj, RtmComponent c)
    {
        c.FontName = GetString(obj, "Font.Name") ?? c.FontName;
        var size = GetFloat(obj, "Font.Size", -1f);
        if (size > 0) c.FontSize = size;
        var style = GetValue(obj, "Font.Style");
        if (style is IReadOnlyList<string> set) c.FontBold = set.Contains("fsBold");
        c.ForeColor = ParseColor(GetString(obj, "Font.Color") ?? "clBlack");
    }

    private static Color ParseColor(string name) => name switch
    {
        "clBlack" => Color.Black,
        "clNavy" => Color.Navy,
        "clGreen" => Color.Green,
        "clRed" => Color.Red,
        "clGray" => Color.Gray,
        "clSilver" => Color.Silver,
        "clBlue" => Color.Blue,
        "clWhite" => Color.White,
        "clYellow" => Color.Yellow,
        "clWindowText" => Color.FromKnownColor(KnownColor.WindowText),
        "clBtnFace" => Color.FromKnownColor(KnownColor.Control),
        _ => Color.Black,
    };

    private static object? GetValue(Tpf0Object o, string name)
    {
        foreach (var (pn, pv) in o.Properties)
            if (pn == name) return pv;
        return null;
    }

    private static string? GetString(Tpf0Object o, string name)
    {
        var v = GetValue(o, name);
        return v switch
        {
            null => null,
            NilValue => null,
            _ => v.ToString(),
        };
    }

    private static bool GetBool(Tpf0Object o, string name) =>
        GetValue(o, name) is true;

    private static float GetFloat(Tpf0Object o, string name, float def)
    {
        var v = GetValue(o, name);
        if (v is null || v is NilValue) return def;
        try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); }
        catch { return def; }
    }
}

/// <summary>把 RtmReportModel 渲染到 Graphics（GDI+），分頁由 RtmRenderState 控制。</summary>
public sealed class RtmRenderer : IDisposable
{
    private readonly RtmReportModel _report;
    private readonly RtmData _data;
    private float _dpiX = 96f, _dpiY = 96f;

    /// <summary>若設定，DrawText 每次實際繪製文字時記錄其真實文字矩形（頁面 mm）。供重疊診斷。</summary>
    public List<(RtmComponent C, float Xmm, float Ymm, float Wmm, float Hmm, string Text)>? DrawnTexts;
    /// <summary>若設定，DrawLine 每次實際繪製線段時記錄其墨水矩形（含筆寬，頁面 mm）。供重疊診斷。</summary>
    public List<(RtmComponent C, float Xmm, float Ymm, float Wmm, float Hmm)>? DrawnLines;
    /// <summary>總頁數（第一次 RenderPage 前乾跑計算）。</summary>
    public int PageCount => _pageCount;
    private readonly Dictionary<(string Name, float Size, bool Bold), Font> _fontCache = new();
    private readonly Dictionary<string, decimal> _calcSums = new();
    private readonly Dictionary<string, long> _calcCounts = new();
    private readonly List<(RtmBand Band, float Xmm)> _deferredSubBands = new();   // 子報表的彙總／頁尾 band（明細全部結束後畫一次）
    private int _pageCount;          // 總頁數（第一次 RenderPage 前乾跑計算）
    private int _pageIndex;          // 目前頁碼（1-based）
    private bool _dryRun;            // 乾跑模式：只算分頁、不實際繪製
    private bool _titlePrinted;      // 標題 band 是否已印（僅第一頁印一次）

    public RtmRenderer(RtmReportModel report, RtmData data)
    {
        _report = report;
        _data = data;
        PrecomputeCalcs();
    }

    private void PrecomputeCalcs()
    {
        foreach (var row in _data.Detail)
        {
            foreach (var (k, v) in row)
            {
                // 前綴鍵（如「ppDBPipeline1|交易單號」）已是完整彙總鍵，直接累加
                var key = k.IndexOf('|') >= 0 ? k : $"{_data.DetailPipeline}|{k}";
                _calcCounts[key] = _calcCounts.TryGetValue(key, out var n) ? n + 1 : 1;
                if (v is null || v is DBNull) continue;
                if (v is decimal dm)
                {
                    _calcSums[key] = _calcSums.TryGetValue(key, out var s0) ? s0 + dm : dm;
                    continue;
                }
                if (decimal.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                        NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                    _calcSums[key] = _calcSums.TryGetValue(key, out var s1) ? s1 + d : d;
            }
        }
    }

    private decimal CalcSum(string pipeline, string field)
        => _calcSums.TryGetValue($"{pipeline}|{field}", out var s) ? s : 0m;

    private long CalcCount(string pipeline, string field)
        => _calcCounts.TryGetValue($"{pipeline}|{field}", out var n) ? n : 0;

    /// <summary>分組欄位值（供組變化偵測）。</summary>
    private string? GroupValue(int index)
    {
        if (index < 0 || index >= _data.Detail.Count) return null;
        return Convert.ToString(
            _data.GetValue(_report.GroupPipeline ?? _data.DetailPipeline, _report.GroupBy ?? "", index),
            CultureInfo.InvariantCulture);
    }

    private float MmToX(float mm) => mm * _dpiX / 25400f;
    private float MmToY(float mm) => mm * _dpiY / 25400f;

    private Font GetFont(string name, float sizePt, bool bold)
    {
        var key = (name, sizePt, bold);
        if (!_fontCache.TryGetValue(key, out var f))
        {
            // 以有效解析度把 pt 換算成 px（GraphicsUnit.Pixel 不受 Graphics.DpiY 影響）
            float sizePx = sizePt * _dpiY / 72f;
            f = new Font(name, sizePx, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            _fontCache[key] = f;
        }
        return f;
    }

    /// <summary>渲染一頁。回傳 true 表示還有下一頁（呼叫端設 e.HasMorePages）。</summary>
    public bool RenderPage(Graphics g, RectangleF pageBounds, RtmRenderState state)
    {
        // 有效解析度由「報表紙張物理尺寸 vs PageBounds 像素尺寸」反推，
        // 不直接信任 g.DpiX：印表機/列印預覽的 Graphics Dpi 與 PageBounds（1/100 吋像素）可能不一致。
        _dpiX = pageBounds.Width > 0 ? pageBounds.Width * 25400f / _report.MmPaperWidth : g.DpiX;
        _dpiY = pageBounds.Height > 0 ? pageBounds.Height * 25400f / _report.MmPaperHeight : g.DpiY;

        // 第一次呼叫前先乾跑一遍，算出總頁數（供 TppSystemVariable 頁碼/總頁數使用）
        if (_pageCount == 0)
        {
            _dryRun = true;
            _titlePrinted = false;
            var st = new RtmRenderState();
            while (true)
            {
                _pageCount++;
                if (!RenderCore(g, pageBounds, st)) break;
            }
            _dryRun = false;
            _titlePrinted = false;   // 真實渲染從頭再印標題
        }

        _pageIndex++;
        g.Clear(Color.White);   // 每頁獨立渲染，避免多頁疊畫在同一 Graphics 上
        return RenderCore(g, pageBounds, state);
    }

    private bool RenderCore(Graphics g, RectangleF pageBounds, RtmRenderState state)
    {
        float pageHmm = pageBounds.Height * 25400f / _dpiY;   // 頁面高度（1/1000 mm）
        float y = 0;

        // 0. 標題 band：整份報表只在第一頁印一次（位於頁首之前）
        if (!_titlePrinted && _report.TitleBand is { } title)
        {
            if (y + title.MmHeight > pageHmm)
                return true;
            RenderBand(g, title, 0f, y, -1);
            y += title.MmHeight;
            _titlePrinted = true;
        }

        // 1. 頁首 band：每頁重印
        if (_report.HeaderBand is { } header)
        {
            RenderBand(g, header, 0f, y, -1);
            // 子報表內容會垂直展開（明細帶＋組帶＋彙總帶），有效高度取固定高度與子報表帶高總和較大者，
            // 否則子報表內容溢出 band 邊界與後續（頁尾等）重疊。
            y += Math.Max(header.MmHeight, CalcSubReportBandHeight(header));
        }

        // 2. 明細 band：依資料筆數逐筆（含分組：組欄位值變化時印 GroupHeader／組尾 GroupFooter）
        if (_report.DetailBand is { } detail)
        {
            bool hasGroup = _report.GroupBy is { Length: > 0 };
            while (state.DetailIndex < _data.Detail.Count)
            {
                if (hasGroup)
                {
                    var curVal = GroupValue(state.DetailIndex);
                    var prevVal = state.DetailIndex > 0 ? GroupValue(state.DetailIndex - 1) : null;
                    bool newGroup = state.DetailIndex == 0 || !string.Equals(curVal, prevVal, StringComparison.Ordinal);
                    if (newGroup)
                    {
                        // 上一組的組尾（第一組無前組）
                        if (state.DetailIndex > 0 && !state.GroupFooterDone && _report.GroupFooterBand is { } gf)
                        {
                            if (y + gf.MmHeight > pageHmm)
                                return true;
                            RenderBand(g, gf, 0f, y, state.DetailIndex - 1);
                            y += gf.MmHeight;
                        }
                        // 本組標題
                        if (_report.GroupHeaderBand is { } gh)
                        {
                            if (y + gh.MmHeight > pageHmm)
                                return true;
                            RenderBand(g, gh, 0f, y, state.DetailIndex);
                            y += gh.MmHeight;
                        }
                    }
                }
                if (y + detail.MmHeight > pageHmm)
                    return true;   // 放不下 → 換頁續印
                RenderBand(g, detail, 0f, y, state.DetailIndex);
                y += detail.MmHeight;
                state.DetailIndex++;
            }
        }

        // 2b. 子報表的彙總／頁尾 band：全部明細結束後畫一次
        foreach (var (subBand, subX) in _deferredSubBands)
        {
            if (y + subBand.MmHeight > pageHmm)
                return true;
            RenderBand(g, subBand, subX, y, -1);
            y += subBand.MmHeight;
        }

        // 3. 組尾 band：最後一組（或未分組時）明細畫完印一次
        if (!state.GroupFooterDone && _report.GroupFooterBand is { } footer)
        {
            if (y + footer.MmHeight > pageHmm)
                return true;
            RenderBand(g, footer, 0f, y, Math.Max(0, state.DetailIndex - 1));
            y += footer.MmHeight;
            state.GroupFooterDone = true;
        }

        // 4. 彙總 band（TppSummaryBand）：整份報表結尾印一次
        if (!state.SummaryDone && _report.SummaryBand is { } summary)
        {
            if (y + summary.MmHeight > pageHmm)
                return true;
            RenderBand(g, summary, 0f, y, -1);
            y += summary.MmHeight;
            state.SummaryDone = true;
        }

        // 5. 頁尾 band
        if (_report.FooterBand is { } pageFooter)
            RenderBand(g, pageFooter, 0f, y, -1);

        return false;
    }

    private void RenderBand(Graphics g, RtmBand band, float bandXmm, float bandYmm, int detailIndex)
    {
        foreach (var c in band.Components)
            RenderComponent(g, c, bandXmm + c.MmLeft, bandYmm + c.MmTop, detailIndex);
    }

    /// <summary>band 內子報表（TppSubReport／TppChildReport／TppRegion）垂直展開的帶高總和；
    /// 並排的多個子報表版面取較高者。</summary>
    private float CalcSubReportBandHeight(RtmBand band)
    {
        float best = 0f;
        foreach (var c in band.Components)
        {
            if (c.ClassName is "TppSubReport" or "TppChildReport" or "TppRegion")
                best = Math.Max(best, SubReportStackHeight(c));
        }
        return best;
    }

    private float SubReportStackHeight(RtmComponent c)
    {
        float total = 0f;
        foreach (var sc in c.Children)
        {
            if (sc.ClassName.StartsWith("Tpp") && sc.ClassName.EndsWith("Band"))
                total += sc.MmHeight;
            else
                total += SubReportStackHeight(sc);
        }
        return total;
    }

    private void RenderComponent(Graphics g, RtmComponent c, float xmm, float ymm, int detailIndex)
    {
        switch (c.ClassName)
        {
            case "TppLabel":
                c.TextSource = c.Caption ?? "";
                DrawText(g, c.Caption ?? "", c, xmm, ymm, null);
                break;
            case "TppDBText":
            case "TppDBMemo":
            {
                var v = _data.GetValue(c.DataPipeline ?? "", c.DataField ?? "", detailIndex);
                c.TextSource = c.DataField ?? "";
                DrawText(g, FormatValue(v, c.DisplayFormat), c, xmm, ymm, null);
                break;
            }
            case "TppDBCalc":
            {
                var pipe = c.DataPipeline ?? _data.DetailPipeline;
                var field = c.DataField ?? "";
                string text;
                if (c.DbCalcType == "dcCount")
                    text = detailIndex >= 0 ? (detailIndex + 1).ToString()
                        : CalcCount(pipe, field).ToString();
                else if (detailIndex >= 0)
                    text = (detailIndex + 1).ToString();
                else if (c.BandKind is "Footer" or "GroupFooter" or "Summary")
                    text = FormatValue(CalcSum(pipe, field), c.DisplayFormat);
                else
                    text = CalcCount(pipe, field).ToString();
                c.TextSource = c.DbCalcType == "dcCount" ? "列數" : (c.DataField ?? "計算");
                DrawText(g, text, c, xmm, ymm, null);
                break;
            }
            case "TppSystemVariable":
            {
                var text = (c.VarType ?? "vtDateTime") switch
                {
                    "vtPageNo" => _pageIndex.ToString(),
                    "vtPageCount" or "vtTotalPages" => _pageCount.ToString(),
                    "vtPageSet" => $"第 {_pageIndex} 頁",
                    "vtPageSetDesc" => $"第 {_pageIndex} 頁，共 {_pageCount} 頁",
                    "vtDateTime" or "vtDate" => DateTime.Now.ToString("yyyy-MM-dd"),
                    "vtTime" => DateTime.Now.ToString("HH:mm"),
                    "vtReportTitle" => c.Caption ?? "",
                    "vtUserName" => Environment.UserName,
                    _ => c.Caption ?? "",
                };
                c.TextSource = c.VarType ?? "日期時間";
                DrawText(g, text, c, xmm, ymm, null);
                break;
            }
            case "TppLine":
                DrawLine(g, c, xmm, ymm);
                break;
            case "TppShape":
                DrawShape(g, c, xmm, ymm);
                break;
            case "TppSubReport":
            case "TppChildReport":
            case "TppRegion":
                // 子報表 band 依序垂直排列（Title→Header→Detail→Group…），彙總／頁尾延後到明細結束
                float subBandY = ymm;
                foreach (var sc in c.Children)
                {
                    if (sc.ClassName.StartsWith("Tpp") && sc.ClassName.EndsWith("Band"))
                    {
                        var subKind = sc.ClassName[3..^4];
                        foreach (var subComp in sc.Children) subComp.BandKind = subKind;
                        if (detailIndex >= 0 && subKind is "Summary" or "Footer")
                        {
                            // 子報表的彙總／頁尾 band：明細列不重複畫，延後到全部明細結束後一次
                            var band = new RtmBand { Kind = subKind, Components = sc.Children, MmHeight = sc.MmHeight };
                            if (!_deferredSubBands.Any(d => ReferenceEquals(d.Band.Components, sc.Children)))
                                _deferredSubBands.Add((band, xmm + sc.MmLeft));
                            continue;
                        }
                        RenderBand(g, new RtmBand { Kind = subKind, Components = sc.Children, MmHeight = sc.MmHeight },
                            xmm + sc.MmLeft, subBandY + sc.MmTop, detailIndex);
                        subBandY += sc.MmHeight;
                    }
                    else
                    {
                        RenderComponent(g, sc, xmm + sc.MmLeft, ymm + sc.MmTop, detailIndex);
                    }
                }
                break;
            // TppImage：圖形資料 v1 不支援，略過
        }
    }

    private void DrawText(Graphics g, string text, RtmComponent c, float xmm, float ymm, string? _)
    {
        if (_dryRun) return;
        if (string.IsNullOrEmpty(text)) return;
        if (text.IndexOf('\0') >= 0) text = text.Replace("\0", "");
        var font = GetFont(c.FontName, c.FontSize, c.FontBold);
        var brush = new SolidBrush(c.ForeColor);
        float x = MmToX(xmm), y = MmToY(ymm);
        float w = MmToX(c.MmWidth), h = MmToY(c.MmHeight);

        var align = c.TextAlignment switch
        {
            "taRightJustified" => StringAlignment.Far,
            "taCenter" => StringAlignment.Center,
            _ => StringAlignment.Near,
        };

        try
        {
            if (c.WordWrap)
            {
                using var sf = new StringFormat
                {
                    Alignment = align,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.None,
                };
                g.DrawString(text, font, brush, new RectangleF(x, y, w, h), sf);
                // 以實際換行後文字範圍記錄（限制在框內），供重疊診斷
                var sz2 = g.MeasureString(text, font, new SizeF(w, h));
                float iw = Math.Min(sz2.Width, w), ih = Math.Min(sz2.Height, h);
                float ix = align switch
                {
                    StringAlignment.Far => x + Math.Max(0, w - iw),
                    StringAlignment.Center => x + Math.Max(0, (w - iw) / 2f),
                    _ => x,
                };
                float iy = y + Math.Max(0, (h - ih) / 2f);
                DrawnTexts?.Add((c,
                    ix * 25400f / _dpiX, iy * 25400f / _dpiY,
                    iw * 25400f / _dpiX, ih * 25400f / _dpiY, text));
            }
            else
            {
                var sz = g.MeasureString(text, font);
                float tx = align switch
                {
                    StringAlignment.Far => x + Math.Max(0, w - sz.Width),
                    StringAlignment.Center => x + Math.Max(0, (w - sz.Width) / 2f),
                    _ => x,
                };
                float ty = y + Math.Max(0, (h - sz.Height) / 2f);
                g.DrawString(text, font, brush, tx, ty);
                DrawnTexts?.Add((c,
                    tx * 25400f / _dpiX, ty * 25400f / _dpiY,
                    sz.Width * 25400f / _dpiX, sz.Height * 25400f / _dpiY, text));
            }
        }
        finally
        {
            brush.Dispose();
        }
    }

    private void DrawLine(Graphics g, RtmComponent c, float xmm, float ymm)
    {
        if (_dryRun) return;
        float x1 = MmToX(xmm), y1 = MmToY(ymm);
        float x2 = x1 + MmToX(c.MmWidth), y2 = y1 + MmToY(c.MmHeight);
        float penPx = Math.Max(0.5f, c.PenWeight * _dpiY / 72f);   // pt → px
        using var pen = new Pen(c.PenColor, penPx);

        switch (c.LinePosition)
        {
            case "lpBottom":   // 水平線，貼元件底部
                y2 = y1;
                g.DrawLine(pen, x1, y2, x2, y2);
                RecordLine(c, x1, y1, x2, y2, penPx);
                break;
            case "lpLeft":     // 垂直線，貼元件左緣
                x2 = x1;
                g.DrawLine(pen, x2, y1, x2, y2);
                RecordLine(c, x1, y1, x2, y2, penPx);
                break;
            case "lpRight":    // 垂直線，貼元件右緣
                x2 = x1 + MmToX(c.MmWidth);
                g.DrawLine(pen, x2, y1, x2, y2);
                RecordLine(c, x1, y1, x2, y2, penPx);
                break;
            default:           // 水平線（lpTop 或未指定），貼元件頂部
                g.DrawLine(pen, x1, y1, x2, y1);
                RecordLine(c, x1, y1, x2, y1, penPx);
                break;
        }
    }

    private void RecordLine(RtmComponent c, float x1Px, float y1Px, float x2Px, float y2Px, float penPx)
    {
        if (DrawnLines is null) return;
        float half = penPx / 2f;
        bool horizontal = Math.Abs(y1Px - y2Px) < 0.01f;
        float xa = Math.Min(x1Px, x2Px), xb = Math.Max(x1Px, x2Px);
        float ya = Math.Min(y1Px, y2Px), yb = Math.Max(y1Px, y2Px);
        float lx = horizontal ? xa - half : xa;
        float ly = horizontal ? ya - half : ya - half;
        float lw = horizontal ? (xb - xa) + penPx : penPx;
        float lh = horizontal ? penPx : (yb - ya) + penPx;
        DrawnLines.Add((c,
            lx * 25400f / _dpiX,
            ly * 25400f / _dpiY,
            lw * 25400f / _dpiX,
            lh * 25400f / _dpiY));
    }

    private void DrawShape(Graphics g, RtmComponent c, float xmm, float ymm)
    {
        if (_dryRun) return;
        float x = MmToX(xmm), y = MmToY(ymm);
        float w = MmToX(c.MmWidth), h = MmToY(c.MmHeight);
        if (w <= 0 || h <= 0) return;
        float penPx = Math.Max(0.5f, c.PenWeight * _dpiY / 72f);
        using var pen = new Pen(c.PenColor, penPx);
        var rect = new RectangleF(x, y, w, h);

        switch (c.ShapeType)
        {
            case "stEllipse":
                g.DrawEllipse(pen, rect);
                break;
            case "stRoundRect":
                using (var path = RoundedRect(rect, Math.Min(6f, w / 2f)))
                    g.DrawPath(pen, path);
                break;
            default:
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                break;
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2f;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>依 DisplayFormat（如 "#,0.####;-#,0.####"）格式化數值。</summary>
    private static string FormatValue(object? v, string? displayFormat)
    {
        if (v is null or NilValue) return "";
        if (v is string s) return s;
        if (v is DateTime dt) return dt.ToString("yyyy-MM-dd");

        if (v is IFormattable num && !string.IsNullOrEmpty(displayFormat)
            && v is not bool)
        {
            // 取分號前的主格式
            var fmt = displayFormat.Split(';')[0];
            try { return num.ToString(fmt, CultureInfo.CurrentCulture); }
            catch { return v.ToString() ?? ""; }
        }
        return v.ToString() ?? "";
    }

    public void Dispose()
    {
        foreach (var f in _fontCache.Values) f.Dispose();
        _fontCache.Clear();
    }
}
