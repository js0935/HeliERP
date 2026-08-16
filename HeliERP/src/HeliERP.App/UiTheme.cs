// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（UI/UX 精緻化升級）
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Drawing2D;

namespace HeliERP.App;

/// <summary>
/// 全系統統一主題：色彩、字型、間距、控制項樣式。
/// 商務 ERP 風格：深藍主色 + 金色點綴、淺灰背景、統一資料表格樣式。
/// 1.1.0：新增間距/圓角令牌、柔和陰影、卡片容器、表單標題列、工具列/樹狀樣式、懸停反饋。
/// </summary>
public static class UiTheme
{
    // ── 主色系 ──
    public static readonly Color Primary = Color.FromArgb(31, 78, 121);      // #1F4E79 深藍
    public static readonly Color PrimaryDark = Color.FromArgb(22, 54, 92);   // #16365C
    public static readonly Color PrimaryLight = Color.FromArgb(43, 89, 154); // #2B579A
    public static readonly Color Accent = Color.FromArgb(232, 163, 61);      // #E8A33D 金
    public static readonly Color AccentDark = Color.FromArgb(198, 131, 32);  // #C68320

    // ── 中性色 ──
    public static readonly Color Background = Color.FromArgb(242, 244, 247); // #F2F4F7 視窗底（冷調微亮）
    public static readonly Color Card = Color.White;                          // 卡片/輸入框底
    public static readonly Color TextMain = Color.FromArgb(33, 37, 41);      // #212529
    public static readonly Color TextSub = Color.FromArgb(108, 117, 125);    // #6C757D
    public static readonly Color TextFaint = Color.FromArgb(154, 160, 166);  // 更淡輔助文字
    public static readonly Color Border = Color.FromArgb(222, 226, 230);     // #DEE2E6
    public static readonly Color BorderLight = Color.FromArgb(233, 236, 240);// 極淡分隔線
    public static readonly Color GridLine = Color.FromArgb(233, 236, 239);   // #E9ECEF
    public static readonly Color RowAlt = Color.FromArgb(247, 249, 252);     // #F7F9FC 奇偶列
    public static readonly Color SelectBack = Color.FromArgb(222, 235, 247); // 選取列淺藍
    public static readonly Color HoverRow = Color.FromArgb(236, 243, 252);   // 表格列懸停
    public static readonly Color FocusBack = Color.FromArgb(248, 251, 255);  // 輸入框聚焦底
    public static readonly Color Hover = Color.FromArgb(56, 110, 168);       // 懸停亮
    public static readonly Color Pressed = Color.FromArgb(16, 45, 78);       // 按下暗

    // ── 側邊導覽 ──
    public static readonly Color Sidebar = Color.FromArgb(24, 36, 52);       // #182434
    public static readonly Color SidebarHover = Color.FromArgb(38, 56, 78);
    public static readonly Color SidebarActive = Color.FromArgb(31, 78, 121);

    // ── 狀態色 ──
    public static readonly Color Ok = Color.FromArgb(40, 167, 69);           // 成功
    public static readonly Color Warn = Color.FromArgb(255, 193, 7);         // 警告
    public static readonly Color Danger = Color.FromArgb(220, 53, 69);       // 錯誤

    // ── 間距令牌（統一版面節奏）──
    public const int SpacingXs = 4;
    public const int SpacingSm = 8;
    public const int SpacingMd = 12;
    public const int SpacingLg = 16;
    public const int SpacingXl = 24;
    public const int SpacingXxl = 32;

    // ── 圓角令牌 ──
    public const int RadiusSm = 6;
    public const int RadiusMd = 10;
    public const int RadiusLg = 14;

    /// <summary>取得系統字型（微軟正黑體）</summary>
    public static Font Font(float size, FontStyle style = FontStyle.Regular) =>
        new("Microsoft JhengHei UI", size, style);

    /// <summary>套用表單基礎樣式</summary>
    public static void Apply(Form form)
    {
        form.BackColor = Background;
        form.Font = Font(11F);
    }

    /// <summary>
    /// 高 DPI（如 200% 縮放）下確保表單不超出螢幕工作區：
    /// 一般表單在 Load 後若尺寸超過工作區，將整張表單等比縮放並重新置中；
    /// 最大化表單則量測內容需求（上/下/左/右停靠與固定控制），超出時收斂縮放子控制。
    /// 搭配 AutoScaleMode.Dpi 使用：控制與字體維持設計比例，僅在空間不足時縮小。
    /// </summary>
    public static void ClampToScreen(Form form)
    {
        form.Load += (s, e) =>
        {
            try
            {
                var wa = Screen.FromControl(form).WorkingArea;

                if (form.WindowState == FormWindowState.Maximized)
                {
                    int topH = 0, bottomH = 0, leftW = 0, rightW = 0;
                    int needW = 0, needH = 0;
                    foreach (Control c in form.Controls)
                    {
                        switch (c.Dock)
                        {
                            case DockStyle.Top:
                                topH += c.Height;
                                needW = Math.Max(needW, c.PreferredSize.Width);
                                break;
                            case DockStyle.Bottom:
                                bottomH += c.Height;
                                needW = Math.Max(needW, c.PreferredSize.Width);
                                break;
                            case DockStyle.Left:
                                leftW += c.Width;
                                needH = Math.Max(needH, c.PreferredSize.Height);
                                break;
                            case DockStyle.Right:
                                rightW += c.Width;
                                needH = Math.Max(needH, c.PreferredSize.Height);
                                break;
                            default:
                                needW = Math.Max(needW, c.Right);
                                needH = Math.Max(needH, c.Bottom);
                                break;
                        }
                    }
                    int cw = form.ClientSize.Width, ch = form.ClientSize.Height;
                    needW = Math.Max(needW, leftW + rightW);
                    needH = Math.Max(needH, topH + bottomH + 120);
                    float k = Math.Min(cw / (float)Math.Max(1, needW), ch / (float)Math.Max(1, needH));
                    if (k >= 1f - 0.01f) return;
                    form.SuspendLayout();
                    foreach (Control c in form.Controls)
                        c.Scale(new SizeF(k, k));
                    form.ResumeLayout(true);
                }
                else
                {
                    if (form.Width <= wa.Width && form.Height <= wa.Height) return;
                    float k = Math.Min(wa.Width / (float)form.Width, wa.Height / (float)form.Height);
                    form.SuspendLayout();
                    form.Scale(new SizeF(k, k));
                    form.ResumeLayout(true);
                    form.Location = new Point(
                        wa.X + (wa.Width - form.Width) / 2,
                        wa.Y + (wa.Height - form.Height) / 2);
                }
            }
            catch
            {
                // 縮放失敗時維持原樣，不影響啟動
            }
        };
    }

    /// <summary>
    /// 統一的表單標題列：標題 + 副標題 + 金色短線。
    /// 回傳 Top-Dock Panel，各表單於建構時第一個加入（置於工具列上方）。
    /// </summary>
    public static Panel BuildHeader(string title, string subtitle = "", int height = 64)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = height,
            BackColor = Card,
            Padding = new Padding(SpacingXl, 0, SpacingXl, 0),
        };
        var lblTitle = new Label
        {
            Text = title,
            Font = Font(15F, FontStyle.Bold),
            ForeColor = PrimaryDark,
            AutoSize = true,
            Location = new Point(SpacingXl, 15),
        };
        panel.Controls.Add(lblTitle);
        if (!string.IsNullOrEmpty(subtitle))
        {
            var lblSub = new Label
            {
                Text = subtitle,
                Font = Font(9F),
                ForeColor = TextSub,
                AutoSize = true,
                Location = new Point(SpacingXl + 2, 40),
            };
            panel.Controls.Add(lblSub);
        }
        panel.Controls.Add(new Panel
        {
            BackColor = Accent,
            Size = new Size(54, 3),
            Location = new Point(SpacingXl, height - 6),
        });
        return panel;
    }

    // ════════════════════ 控制項樣式 ════════════════════

    /// <summary>DataGridView 統一風格：主色列頭、斑馬紋、懸停列高亮、選取列淺藍</summary>
    public static void StyleDataGridView(DataGridView grid)
    {
        grid.BackgroundColor = Card;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Primary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = Font(10.5F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.BackColor = Card;
        grid.DefaultCellStyle.ForeColor = TextMain;
        grid.DefaultCellStyle.SelectionBackColor = SelectBack;
        grid.DefaultCellStyle.SelectionForeColor = PrimaryDark;
        grid.DefaultCellStyle.Font = Font(10F);
        grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = RowAlt;
        grid.RowTemplate.Height = 32;
        grid.GridColor = GridLine;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 239, 243);
        grid.RowHeadersDefaultCellStyle.ForeColor = TextSub;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = SelectBack;
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.RowHeadersWidth = 52;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        // 懸停列高亮（選取列維持 SelectionBackColor）
        grid.CellMouseEnter += (s, e) =>
        {
            if (e.RowIndex >= 0 && (grid.CurrentCell == null || e.RowIndex != grid.CurrentCell.RowIndex))
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = HoverRow;
        };
        grid.CellMouseLeave += (s, e) =>
        {
            if (e.RowIndex >= 0)
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = (e.RowIndex % 2 == 1) ? RowAlt : Card;
        };
    }

    /// <summary>主鍵欄列頭標示：深藍底白字粗體，與一般欄位區別</summary>
    public static void StyleHeaderBold(DataGridViewColumn column)
    {
        column.HeaderCell.Style.BackColor = PrimaryDark;
        column.HeaderCell.Style.ForeColor = Color.White;
        column.HeaderCell.Style.Font = Font(10.5F, FontStyle.Bold);
    }

    /// <summary>Label 統一風格：主文字或次要文字色</summary>
    public static void StyleLabel(Label label, bool sub = false)
    {
        label.AutoSize = true;
        label.ForeColor = sub ? TextSub : TextMain;
        label.Font = Font(10.5F);
    }

    /// <summary>TextBox 統一風格；唯讀時淡藍底加粗；可編輯時聚焦淡藍底反饋</summary>
    public static void StyleTextBox(TextBox box, bool readOnly = false)
    {
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = Font(10.5F);
        box.BackColor = Card;
        if (readOnly)
        {
            box.ReadOnly = true;
            box.BackColor = SelectBack;
            box.ForeColor = PrimaryDark;
            box.Font = Font(10.5F, FontStyle.Bold);
        }
        else
        {
            box.Enter += (s, e) => box.BackColor = FocusBack;
            box.Leave += (s, e) => box.BackColor = Card;
        }
    }

    /// <summary>ComboBox 統一風格</summary>
    public static void StyleComboBox(ComboBox box)
    {
        box.FlatStyle = FlatStyle.Flat;
        box.Font = Font(10.5F);
        box.BackColor = Card;
        box.DropDownHeight = 240;
        box.IntegralHeight = false;
        AutoWiden(box);
    }

    /// <summary>
    /// 掛載「展開時自動加寬下拉清單」：依項目顯示文字的最長寬度調整 DropDownWidth，
    /// 避免長名稱（公司簡稱、科目名稱、貨品名稱等）被截斷而只能看到前面幾個字。
    /// 對話框等未呼叫 StyleComboBox 的地方，建立 ComboBox 後呼叫本方法即可。
    /// </summary>
    public static void AutoWiden(ComboBox box)
    {
        box.DropDown += (s, e) =>
        {
            int w = box.Width;
            try
            {
                using var g = box.CreateGraphics();
                foreach (var item in box.Items)
                {
                    string text = ComboItemText(item, box.DisplayMember);
                    if (string.IsNullOrEmpty(text)) continue;
                    int tw = (int)g.MeasureString(text, box.Font).Width + 48;
                    if (tw > w) w = tw;
                }
            }
            catch { /* 量測失敗時維持原寬度 */ }
            box.DropDownWidth = w;
        };
    }

    /// <summary>取得下拉項目實際顯示文字（處理資料繫結 DisplayMember／DataRowView）。</summary>
    private static string ComboItemText(object? item, string displayMember)
    {
        if (item is DataRowView drv && !string.IsNullOrEmpty(displayMember))
            return Convert.ToString(drv.Row[displayMember]) ?? "";
        if (item is null) return "";
        if (!string.IsNullOrEmpty(displayMember))
        {
            var prop = item.GetType().GetProperty(displayMember);
            if (prop is not null) return Convert.ToString(prop.GetValue(item)) ?? "";
        }
        return item.ToString() ?? "";
    }

    /// <summary>DateTimePicker 統一風格（含月曆配色）</summary>
    public static void StyleDateTimePicker(DateTimePicker picker)
    {
        picker.Font = Font(10.5F);
        picker.CalendarMonthBackground = Card;
        picker.CalendarForeColor = TextMain;
        picker.CalendarTitleBackColor = Primary;
        picker.CalendarTitleForeColor = Color.White;
        picker.CalendarTrailingForeColor = TextSub;
    }

    /// <summary>TabControl 統一風格：自繪標籤，選取=主色底白字＋頂金線，懸停=淺藍底，未選取=白底主色字</summary>
    public static void StyleTabControl(TabControl tabs)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(118, 36);
        foreach (TabPage page in tabs.TabPages)
            page.BackColor = Card;
        int hoverIndex = -1;
        tabs.MouseMove += (s, e) =>
        {
            int ni = -1;
            for (int i = 0; i < tabs.TabCount; i++)
                if (tabs.GetTabRect(i).Contains(e.Location)) { ni = i; break; }
            if (ni != hoverIndex) { hoverIndex = ni; tabs.Invalidate(); }
        };
        tabs.MouseLeave += (s, e) => { if (hoverIndex != -1) { hoverIndex = -1; tabs.Invalidate(); } };
        tabs.DrawItem += (s, e) =>
        {
            var page = tabs.TabPages[e.Index];
            bool selected = e.Index == tabs.SelectedIndex;
            var rect = e.Bounds;
            Color back = selected ? Primary : (e.Index == hoverIndex ? SelectBack : Card);
            using var fill = new SolidBrush(back);
            e.Graphics.FillRectangle(fill, rect);
            if (selected)
            {
                using var line = new SolidBrush(Accent);
                e.Graphics.FillRectangle(line, rect.X, rect.Y, rect.Width, 3);
            }
            float scale = tabs.DeviceDpi / 96f;
            Color fore = selected ? Color.White : (e.Index == hoverIndex ? Primary : PrimaryLight);
            TextRenderer.DrawText(e.Graphics, page.Text, Font(10.5F * scale, FontStyle.Bold), rect,
                fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };
    }

    /// <summary>TreeView 統一風格：全列選取、自繪節點、選取列金條指示</summary>
    public static void StyleTreeView(TreeView tree)
    {
        tree.BackColor = Card;
        tree.ForeColor = TextMain;
        tree.Font = Font(10.5F);
        tree.BorderStyle = BorderStyle.None;
        tree.HideSelection = false;
        tree.FullRowSelect = true;
        tree.LineColor = Border;
        tree.ItemHeight = 28;
        tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
        tree.DrawNode += (s, e) =>
        {
            if (e.Node == null) return;
            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            bool root = e.Node.Level == 0;
            var bounds = e.Bounds;
            using (var bg = new SolidBrush(selected ? SelectBack : (root ? Color.FromArgb(246, 248, 251) : Card)))
                e.Graphics.FillRectangle(bg, bounds);
            if (selected)
            {
                using var bar = new SolidBrush(Accent);
                e.Graphics.FillRectangle(bar, bounds.Left - 2, bounds.Y, 3, bounds.Height);
            }
            var fore = selected ? PrimaryDark : (root ? Primary : TextMain);
            var font = Font(10F, root || selected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(e.Graphics, e.Node.Text ?? "", font,
                new Rectangle(bounds.Left + 2, bounds.Y, bounds.Width - 4, bounds.Height),
                fore, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
    }

    /// <summary>ToolStrip 統一風格：白底扁平工具列</summary>
    public static void StyleToolStrip(ToolStrip strip)
    {
        strip.GripStyle = ToolStripGripStyle.Hidden;
        strip.Padding = new Padding(SpacingLg, SpacingSm, SpacingLg, SpacingSm);
        strip.BackColor = Card;
        strip.ForeColor = TextMain;
        strip.Renderer = new ToolStripProfessionalRenderer(new ThemeColorTable());
        strip.ImageScalingSize = new Size(18, 18);
        foreach (ToolStripItem item in strip.Items)
        {
            item.Font = Font(10F);
            if (item is ToolStripButton btn)
            {
                btn.ForeColor = TextMain;
                btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
                btn.Padding = new Padding(10, 4, 10, 4);
                btn.AutoSize = false;
            }
            else if (item is ToolStripSeparator)
            {
                item.Margin = new Padding(SpacingSm, 2, SpacingSm, 2);
            }
        }
    }

    /// <summary>卡片容器：白底圓角邊框 + 上緣金色短線（配合 StyleCardPanel 使用時加陰影）</summary>
    public static void StyleCardPanel(Panel panel, int padding = SpacingLg)
    {
        panel.BackColor = Card;
        panel.Padding = new Padding(padding);
        panel.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using var pen = new Pen(Color.FromArgb(36, Border));
            using var path = RoundedRect(rect, RadiusMd);
            e.Graphics.DrawPath(pen, path);
            using var line = new Pen(Accent, 2F);
            e.Graphics.DrawLine(line, 24, 0, Math.Min(78, panel.Width - 24), 0);
        };
    }

    // ════════════════════ 繪圖輔助 ════════════════════

    /// <summary>建立圓角矩形路徑</summary>
    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>以漸層填滿圓角矩形</summary>
    public static void FillRounded(Graphics g, Rectangle r, int radius, Color top, Color bottom, LinearGradientMode mode = LinearGradientMode.Vertical)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new LinearGradientBrush(r, top, bottom, mode);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPath(brush, path);
    }

    /// <summary>柔和陰影：由外而內疊加半透明深色層</summary>
    public static void DrawShadow(Graphics g, Rectangle r, int radius, int depth = 4)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = depth; i >= 1; i--)
        {
            int alpha = 2 + (depth - i) * 2;
            using var path = RoundedRect(Rectangle.Inflate(r, i, i), radius + i);
            using var brush = new SolidBrush(Color.FromArgb(alpha, 10, 18, 30));
            g.FillPath(brush, path);
        }
    }

    /// <summary>卡片：柔和陰影 + 白底圓角 + 細邊框</summary>
    public static void DrawCard(Graphics g, Rectangle r, int radius = RadiusMd, bool shadow = true)
    {
        if (shadow) DrawShadow(g, r, radius, 5);
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(Card);
        g.FillPath(brush, path);
        using var pen = new Pen(Color.FromArgb(32, Border));
        g.DrawPath(pen, path);
    }

    /// <summary>繪製文字（垂直/水平置中，可省略號）</summary>
    public static void DrawCenteredText(Graphics g, string text, Font font, Color color, Rectangle rect)
    {
        TextRenderer.DrawText(g, text, font, rect, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    /// <summary>ContextMenuStrip 主題配色</summary>
    public sealed class ThemeColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => SelectBack;
        public override Color MenuItemBorder => SelectBack;
        public override Color MenuItemSelectedGradientBegin => SelectBack;
        public override Color MenuItemSelectedGradientEnd => SelectBack;
        public override Color MenuItemPressedGradientBegin => Primary;
        public override Color MenuItemPressedGradientEnd => PrimaryDark;
        public override Color ToolStripDropDownBackground => Card;
        public override Color ImageMarginGradientBegin => Card;
        public override Color ImageMarginGradientMiddle => Card;
        public override Color ImageMarginGradientEnd => Card;
    }
}
