// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（UI/UX 精緻化升級）
// ════════════════════════════════════════════════════════
using System.Drawing.Drawing2D;

namespace HeliERP.App;

/// <summary>
/// 現代化按鈕：圓角、垂直漸層、柔和陰影、懸停/按下/聚焦狀態。
/// Primary（主色底白字）/ Secondary（白底主色字）兩種樣式。
/// </summary>
public class ModernButton : Button
{
    private bool _hover;
    private bool _pressed;

    /// <summary>圓角半徑</summary>
    public int CornerRadius { get; set; } = 8;

    /// <summary>是否繪製柔和陰影（側邊導覽模式自動關閉）</summary>
    public bool DrawShadow { get; set; } = true;

    /// <summary>true=主色底白字；false=白底主色字</summary>
    private bool _isPrimary = true;
    public bool IsPrimary
    {
        get => _isPrimary;
        set { _isPrimary = value; UpdateColors(); Invalidate(); }
    }

    /// <summary>側邊導覽模式：深色半透明底、懸停亮、啟用時主色底＋左強調邊</summary>
    private bool _sidebarMode;
    public bool SidebarMode
    {
        get => _sidebarMode;
        set
        {
            _sidebarMode = value;
            if (value)
            {
                // 導覽按鈕不取得焦點：避免 FlowLayoutPanel 自動捲動把側欄拉回焦點按鈕位置
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
            }
        }
    }

    /// <summary>側邊導覽啟用狀態（目前所在模組）</summary>
    public bool IsActive { get; set; }

    /// <summary>側邊導覽灰化狀態（規劃中模組）：文字更淡、不強調 hover</summary>
    public bool SidebarMuted { get; set; }

    /// <summary>懸停時顯示強調色邊框（導覽按鈕用）</summary>
    public bool AccentBorder { get; set; }

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = UiTheme.Font(11F, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Height = 44;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UpdateColors();
    }

    private void UpdateColors()
    {
        ForeColor = IsPrimary ? Color.White : UiTheme.Primary;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    private void DrawButtonShadow(Graphics g, Rectangle rect, int radius)
    {
        for (int i = 3; i >= 1; i--)
        {
            int alpha = 5 + (3 - i) * 4;
            using var path = UiTheme.RoundedRect(
                new Rectangle(rect.X + i, rect.Y + i + 1, rect.Width, rect.Height), radius + i);
            using var brush = new SolidBrush(Color.FromArgb(alpha, 10, 18, 30));
            g.FillPath(brush, path);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Math.Min(CornerRadius, Math.Min(Width, Height) / 2);

        Color top, bottom;
        if (SidebarMode)
        {
            if (IsActive)
            {
                top = bottom = UiTheme.SidebarActive;
                UiTheme.FillRounded(g, rect, radius, top, bottom);
                using var bar = new SolidBrush(UiTheme.Accent);
                g.FillRectangle(bar, 0, 8, 4, Height - 16);
            }
            else
            {
                if (SidebarMuted)
                    top = bottom = UiTheme.Sidebar;
                else
                {
                    top = _pressed ? UiTheme.SidebarHover : (_hover ? UiTheme.SidebarHover : UiTheme.Sidebar);
                    bottom = top;
                }
                UiTheme.FillRounded(g, rect, radius, top, bottom);
            }
            var textColor = SidebarMuted
                ? (_hover ? Color.FromArgb(150, 255, 255, 255) : Color.FromArgb(110, 255, 255, 255))
                : (_hover || IsActive ? Color.White : Color.FromArgb(200, 255, 255, 255));
            UiTheme.DrawCenteredText(g, Text, Font, textColor, rect);
            return;
        }

        if (!_pressed && DrawShadow)
            DrawButtonShadow(g, rect, radius);

        if (IsPrimary)
        {
            if (_pressed) { top = UiTheme.Pressed; bottom = UiTheme.PrimaryDark; }
            else if (_hover) { top = UiTheme.Hover; bottom = UiTheme.Primary; }
            else { top = UiTheme.Primary; bottom = UiTheme.PrimaryDark; }
            UiTheme.FillRounded(g, rect, radius, top, bottom);
        }
        else
        {
            if (_pressed) { top = UiTheme.SelectBack; bottom = UiTheme.SelectBack; }
            else if (_hover) { top = UiTheme.HoverRow; bottom = UiTheme.HoverRow; }
            else { top = Color.White; bottom = Color.White; }
            UiTheme.FillRounded(g, rect, radius, top, bottom);
            using var pen = new Pen(_hover ? UiTheme.Primary : UiTheme.Border);
            using var path = UiTheme.RoundedRect(rect, radius);
            g.DrawPath(pen, path);
        }

        if (AccentBorder && _hover)
        {
            using var pen = new Pen(UiTheme.Accent, 1.5F);
            using var path = UiTheme.RoundedRect(rect, radius);
            g.DrawPath(pen, path);
        }

        // 鍵盤聚焦指示（滑鼠操作自動不顯示）
        if (Focused && ShowFocusCues && !_pressed)
        {
            using var pen = new Pen(UiTheme.Accent, 1F) { DashStyle = DashStyle.Dot };
            using var path = UiTheme.RoundedRect(new Rectangle(3, 3, Width - 7, Height - 7), Math.Max(2, radius - 3));
            g.DrawPath(pen, path);
        }

        UiTheme.DrawCenteredText(g, Text, Font, IsPrimary ? Color.White : UiTheme.Primary, rect);
    }
}
