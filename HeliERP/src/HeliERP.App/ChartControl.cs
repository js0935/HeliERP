// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（商業智慧儀表板）
// ════════════════════════════════════════════════════════
using System.Drawing.Drawing2D;

namespace HeliERP.App;

/// <summary>
/// 自繪統計圖表（純 GDI+，無第三方元件）：
/// 長條圖（多數列並排）／折線圖（多數列重疊），含 Y 軸刻度、格線、資料標籤、圖例。
/// 支援「萬／億」自動縮寫、空資料提示，與 UiTheme 主題一致。
/// </summary>
public sealed class ChartControl : Panel
{
    public sealed class Series
    {
        public required string Name;
        public required decimal[] Values;
        public required Color Color;
        /// <summary>逐長條覆蓋色（長條圖用；null 時全部使用 Color）。</summary>
        public Color[]? BarColors;
    }

    private readonly List<Series> _series = new();
    public IReadOnlyList<Series> SeriesList => _series;

    public string ChartTitle = "";
    public string[] Labels = Array.Empty<string>();
    /// <summary>true = 長條圖（並排）；false = 折線圖。</summary>
    public bool BarMode = true;

    private readonly ToolTip _tip = new() { AutoPopDelay = 5000, InitialDelay = 200 };
    private readonly List<Rectangle> _hitAreas = new();
    private readonly List<string> _hitTexts = new();

    public ChartControl()
    {
        BackColor = UiTheme.Card;
        ResizeRedraw = true;
        DoubleBuffered = true;
        Paint += DrawChart;
        MouseMove += (s, e) =>
        {
            for (int i = 0; i < _hitAreas.Count; i++)
            {
                if (_hitAreas[i].Contains(e.Location))
                {
                    _tip.SetToolTip(this, _hitTexts[i]);
                    return;
                }
            }
            _tip.Hide(this);
        };
    }

    public void ClearSeries() => _series.Clear();

    public void AddSeries(string name, decimal[] values, Color color, Color[]? barColors = null) =>
        _series.Add(new Series { Name = name, Values = values, Color = color, BarColors = barColors });

    public decimal MaxValue
    {
        get
        {
            decimal m = 0m;
            foreach (var s in _series)
                foreach (var v in s.Values)
                    if (v > m) m = v;
            return m;
        }
    }

    private void DrawChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(UiTheme.Card);

        _hitAreas.Clear();
        _hitTexts.Clear();

        int padL = 12, padT = 14, padR = 12, padB = 34;
        int titleH = string.IsNullOrEmpty(ChartTitle) ? 0 : 26;
        var area = new Rectangle(padL, padT + titleH, Width - padL - padR, Height - padT - padB - titleH);

        // 標題
        if (titleH > 0)
        {
            var titleFont = UiTheme.Font(12F, FontStyle.Bold);
            TextRenderer.DrawText(g, ChartTitle, titleFont, new Rectangle(0, 4, Width, 22),
                UiTheme.PrimaryDark, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
        }

        int count = Labels.Length;
        if (count == 0 || _series.Count == 0 || _series.All(s => s.Values.Length == 0))
        {
            TextRenderer.DrawText(g, "（暫無資料）", UiTheme.Font(10F),
                new Rectangle(0, area.Top + area.Height / 2 - 10, Width, 20),
                UiTheme.TextFaint, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            return;
        }
        count = Math.Min(count, _series.Min(s => s.Values.Length));

        // Y 軸範圍
        decimal rawMax = MaxValue;
        decimal niceMax = NiceCeil(rawMax);
        int steps = 4;

        // 繪圖區
        int plotTop = area.Top + 8;
        int plotBottom = area.Bottom;
        int plotHeight = plotBottom - plotTop;
        var axisPen = new Pen(Color.FromArgb(40, UiTheme.TextMain), 1);
        var gridPen = new Pen(Color.FromArgb(24, UiTheme.TextMain), 1);
        gridPen.DashStyle = DashStyle.Dash;
        g.DrawLine(axisPen, area.Left, plotTop, area.Left, plotBottom);
        g.DrawLine(axisPen, area.Left, plotBottom, area.Right, plotBottom);

        // 格線與刻度
        var tickFont = UiTheme.Font(8.5F);
        for (int i = 0; i <= steps; i++)
        {
            decimal v = niceMax * i / steps;
            int y = plotBottom - (int)Math.Round(plotHeight * (double)i / steps);
            g.DrawLine(gridPen, area.Left, y, area.Right, y);
            string label = FormatAmount(v);
            var sz = TextRenderer.MeasureText(label, tickFont);
            TextRenderer.DrawText(g, label, tickFont,
                new Rectangle(area.Left - sz.Width - 4, y - sz.Height / 2 - 1, sz.Width + 4, sz.Height),
                UiTheme.TextSub, TextFormatFlags.NoPrefix);
        }

        int slotW = area.Width / count;
        int plotLeft = area.Left + 2;
        double scale = niceMax == 0 ? 0 : (double)plotHeight / (double)niceMax;

        int seriesCount = _series.Count;
        int barGap = seriesCount > 1 ? 2 : 6;
        int barW = BarMode ? Math.Max(2, Math.Min(26, (slotW - 8) / seriesCount - barGap)) : 4;
        int barTop = plotTop;

        for (int i = 0; i < count; i++)
        {
            int cx = plotLeft + slotW * i + slotW / 2;
            int labelY = plotBottom + 10;

            for (int si = 0; si < seriesCount; si++)
            {
                var s = _series[si];
                decimal val = i < s.Values.Length ? s.Values[i] : 0m;
                double h = (double)val * scale;
                var color = s.BarColors is { Length: > 0 } && i < s.BarColors.Length
                    ? s.BarColors[i] : s.Color;

                if (BarMode)
                {
                    int x = cx - (barW * seriesCount + barGap * (seriesCount - 1)) / 2 + si * (barW + barGap);
                    int y = plotBottom - (int)Math.Round(h);
                    var rect = new Rectangle(x, y, barW, (int)Math.Round(h));
                    if (rect.Height > 0)
                    {
                        using var brush = new LinearGradientBrush(rect, Lighten(color, 1.15f), color, LinearGradientMode.Vertical);
                        g.FillRectangle(brush, rect);
                        g.FillRectangle(new SolidBrush(color), new Rectangle(x, y + (int)Math.Round(h) - 2, barW, 2));
                        if (rect.Height > 14)
                            DrawValueLabel(g, val, cx + (si - (seriesCount - 1) / 2f) * (barW + barGap), y - 14);
                    }
                    _hitAreas.Add(rect);
                    _hitTexts.Add($"{s.Name} {Labels[i]}\n{val:N0}");
                }
                else
                {
                    int y = plotBottom - (int)Math.Round(h);
                    int dot = si == 0 ? cx : cx + (si - (seriesCount - 1) / 2) * 10;
                    if (val > 0)
                    {
                        using var pen = new Pen(color, 2.2f);
                        if (i > 0)
                        {
                            int prevCx = plotLeft + slotW * (i - 1) + slotW / 2;
                            int prevDot = si == 0 ? prevCx : prevCx + (si - (seriesCount - 1) / 2) * 10;
                            decimal prevVal = (i - 1) < s.Values.Length ? s.Values[i - 1] : 0m;
                            int prevY = plotBottom - (int)Math.Round((double)prevVal * scale);
                            g.DrawLine(pen, prevDot, prevY, dot, y);
                        }
                        g.FillEllipse(new SolidBrush(color), dot - 4, y - 4, 8, 8);
                        g.FillEllipse(Brushes.White, dot - 2, y - 2, 4, 4);
                        if (si == seriesCount - 1)
                            DrawValueLabel(g, val, dot, y - 14);
                        _hitAreas.Add(new Rectangle(dot - 6, y - 6, 12, 12));
                        _hitTexts.Add($"{s.Name} {Labels[i]}\n{val:N0}");
                    }
                }
            }

            // X 軸標籤
            var lsz = TextRenderer.MeasureText(Labels[i], tickFont);
            int lx = cx - lsz.Width / 2;
            if (lx < area.Left) lx = area.Left;
            if (lx + lsz.Width > area.Right) lx = area.Right - lsz.Width;
            TextRenderer.DrawText(g, Labels[i], tickFont,
                new Rectangle(lx, labelY, lsz.Width, 16), UiTheme.TextSub, TextFormatFlags.NoPrefix);
        }

        // 圖例
        int legendY = plotBottom + 22;
        if (legendY + 12 < Height && seriesCount > 1)
        {
            int tx = area.Left;
            var legFont = UiTheme.Font(8.5F);
            for (int si = 0; si < seriesCount; si++)
            {
                var s = _series[si];
                string name = s.Name;
                var nsz = TextRenderer.MeasureText(name, legFont);
                g.FillRectangle(new SolidBrush(s.Color), tx, legendY + 3, 10, 8);
                TextRenderer.DrawText(g, name, legFont,
                    new Rectangle(tx + 14, legendY, nsz.Width, 12), UiTheme.TextSub, TextFormatFlags.NoPrefix);
                tx += 14 + nsz.Width + 12;
                if (tx > area.Right) break;
            }
        }
    }

    private void DrawValueLabel(Graphics g, decimal val, float cx, float y)
    {
        var font = UiTheme.Font(8F);
        string text = FormatAmount(val);
        var sz = TextRenderer.MeasureText(text, font);
        var rect = new RectangleF(cx - sz.Width / 2f, Math.Max(0, y), sz.Width, sz.Height);
        TextRenderer.DrawText(g, text, font, Rectangle.Round(rect), UiTheme.TextMain,
            TextFormatFlags.NoPrefix);
    }

    /// <summary>金額短格式：&gt;=1 億顯示「X.X億」；&gt;=1 萬顯示「X.X萬」；否則 N0。</summary>
    private static string FormatAmount(decimal v)
    {
        if (v >= 100000000m) return (v / 100000000m).ToString("0.#") + "億";
        if (v >= 10000m) return (v / 10000m).ToString("0.#") + "萬";
        return v.ToString("N0");
    }

    /// <summary>取方便刻度上限（至少比最大值高 8%）。</summary>
    private static decimal NiceCeil(decimal v)
    {
        if (v <= 0) return 0m;
        decimal target = v * 1.08m;
        if (target >= 1000000m) return Math.Ceiling(target / 500000m) * 500000m;
        if (target >= 100000m) return Math.Ceiling(target / 100000m) * 100000m;
        if (target >= 10000m) return Math.Ceiling(target / 10000m) * 10000m;
        if (target >= 1000m) return Math.Ceiling(target / 1000m) * 1000m;
        if (target >= 100m) return Math.Ceiling(target / 100m) * 100m;
        if (target >= 10m) return Math.Ceiling(target / 10m) * 10m;
        return Math.Ceiling(target);
    }

    private static Color Lighten(Color c, float factor) => Color.FromArgb(
        Math.Min(255, (int)(c.R * factor)),
        Math.Min(255, (int)(c.G * factor)),
        Math.Min(255, (int)(c.B * factor)));
}
