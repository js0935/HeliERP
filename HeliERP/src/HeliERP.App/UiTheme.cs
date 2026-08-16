// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.2.0（極簡高級灰主題）
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Drawing2D;

namespace HeliERP.App;

/// <summary>
/// 全系統統一主題：色彩、字型、間距、控制項樣式。
/// 極簡高級灰：深炭側欄、極淺灰表面、單一深藍強調、表格列頭淺灰底深字、大量留白。
/// 1.2.0：全面現代化——汰除舊式深藍/金，採用極簡高級灰配色與現代表格列頭。
/// 1.1.0：新增間距/圓角令牌、柔和陰影、卡片容器、表單標題列、工具列/樹狀樣式、懸停反饋。
/// </summary>
public static class UiTheme
{
    // ── 主色系（極簡高級灰：單一深藍強調）──
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);        // #2563EB 深藍
    public static readonly Color PrimaryDark = Color.FromArgb(29, 78, 216);    // #1D4ED8
    public static readonly Color PrimaryLight = Color.FromArgb(96, 165, 250);  // #60A5FA
    public static readonly Color Accent = Color.FromArgb(59, 130, 246);        // #3B82F6 藍
    public static readonly Color AccentDark = Color.FromArgb(30, 64, 175);     // #1E40AF

    // ── 中性色（極淺灰表面）──
    public static readonly Color Background = Color.FromArgb(245, 245, 247);   // #F5F5F7 視窗底
    public static readonly Color Card = Color.White;                          // 卡片/輸入框底
    public static readonly Color TextMain = Color.FromArgb(26, 26, 30);       // #1A1A1E
    public static readonly Color TextSub = Color.FromArgb(110, 110, 115);     // #6E6E73
    public static readonly Color TextFaint = Color.FromArgb(161, 161, 170);   // 更淡輔助文字
    public static readonly Color Border = Color.FromArgb(228, 228, 231);      // #E4E4E7
    public static readonly Color BorderLight = Color.FromArgb(240, 240, 242); // 極淡分隔線
    public static readonly Color GridLine = Color.FromArgb(237, 237, 240);    // #EDEDF0
    public static readonly Color RowAlt = Color.FromArgb(250, 250, 250);      // #FAFAFA 奇偶列
    public static readonly Color SelectBack = Color.FromArgb(234, 241, 254);  // 選取列淺藍
    public static readonly Color HoverRow = Color.FromArgb(244, 244, 245);    // 表格列懸停
    public static readonly Color FocusBack = Color.FromArgb(251, 251, 253);   // 輸入框聚焦底
    public static readonly Color Hover = Color.FromArgb(59, 130, 246);        // 懸停亮
    public static readonly Color Pressed = Color.FromArgb(30, 64, 175);       // 按下暗

    // ── 側邊導覽（深炭）──
    public static readonly Color Sidebar = Color.FromArgb(24, 24, 27);        // #18181B
    public static readonly Color SidebarHover = Color.FromArgb(38, 38, 42);
    public static readonly Color SidebarActive = Color.FromArgb(37, 99, 235);

    // ── 狀態色（沉穩低飽和）──
    public static readonly Color Ok = Color.FromArgb(22, 163, 74);            // 成功
    public static readonly Color Warn = Color.FromArgb(217, 119, 6);          // 警告
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);        // 錯誤

    // ── 間距令牌（統一版面節奏）──
    public const int SpacingXs = 4;
    public const int SpacingSm = 8;
    public const int SpacingMd = 12;
    public const int SpacingLg = 16;
    public const int SpacingXl = 24;
    public const int SpacingXxl = 32;

    // ── 圓角令牌 ──
    public const int RadiusSm = 6;
    public const int RadiusMd = 12;
    public const int RadiusLg = 16;

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
    /// 表單採預設 Font 縮放（PerMonitorV2 下 ScaleFactor=1，維持設計邏輯尺寸與字體），
    /// 本方法僅在空間不足時縮小，避免 AutoScaleMode.Dpi 造成的雙重放大。
    /// </summary>
    public static void ClampToScreen(Form form)
    {
        void DoClamp()
        {
            try
            {
                var wa = Screen.FromControl(form).WorkingArea;
                // 實體基準：工作區邏輯尺寸換算成實體像素，與（可能已依 DPI 縮放的）表單實體尺寸比對
                var dpiScale = form.DeviceDpi / 96f;
                if (dpiScale > 1.001f)
                {
                    wa.X = (int)Math.Round(wa.X * dpiScale);
                    wa.Y = (int)Math.Round(wa.Y * dpiScale);
                    wa.Width = (int)Math.Round(wa.Width * dpiScale);
                    wa.Height = (int)Math.Round(wa.Height * dpiScale);
                }

                if (form.WindowState == FormWindowState.Maximized)
                {
                    int topH = 0, bottomH = 0, leftW = 0, rightW = 0;
                    int needW = 0, needH = 0;
                    foreach (Control c in form.Controls)
                    {
                        switch (c.Dock)
                        {
                            case DockStyle.Fill:
                                // Fill 控制自動伸展填滿剩餘空間，不列入固定需求
                                break;
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
        }

        form.Load += (s, e) => DoClamp();
        form.Shown += (s, e) => DoClamp();
    }

    /// <summary>
    /// 高 DPI（200% 縮放）下依 DeviceDpi 等比放大表單與控制項。
    /// PerMonitorV2 環境中手動建置的表單（AutoScale 係數為 1）不縮放，
    /// 但字體依 DPI 物理放大，導致固定尺寸表單的內容溢出；
    /// 此處僅縮放控制項座標與大小、不調整字體，使表單實體尺寸與字體比例一致。
    /// 對 DataGridView 列高、TreeView、TabControl、TableLayoutPanel 絕對欄寬、
    /// ToolStrip 按鈕等 Scale 不會自動處理的屬性一併放大。
    /// </summary>
    public static void ScaleForDpi(Form form)
    {
        form.HandleCreated += (s, e) =>
        {
            try
            {
                var factor = form.DeviceDpi / 96f;
                if (Math.Abs(factor - 1f) < 0.01f) return;
                form.SuspendLayout();
                form.Scale(new SizeF(factor, factor));
                ScaleChildren(form, factor);
                form.ResumeLayout(true);
            }
            catch
            {
                // 縮放失敗時維持原樣，不影響啟動
            }
        };
    }

    /// <summary>遞迴補償 Control.Scale 不會自動處理的控制項屬性（列高、索引籤尺寸、絕對欄寬等）</summary>
    private static void ScaleChildren(Control root, float factor)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case DataGridView g:
                    g.ColumnHeadersHeight = (int)Math.Round(g.ColumnHeadersHeight * factor);
                    g.RowHeadersWidth = (int)Math.Round(g.RowHeadersWidth * factor);
                    g.RowTemplate.Height = (int)Math.Round(g.RowTemplate.Height * factor);
                    break;
                case TreeView tv:
                    tv.ItemHeight = (int)Math.Round(tv.ItemHeight * factor);
                    break;
                case TabControl tc:
                    tc.ItemSize = new Size(
                        (int)Math.Round(tc.ItemSize.Width * factor),
                        (int)Math.Round(tc.ItemSize.Height * factor));
                    break;
                case TableLayoutPanel tlp:
                    foreach (ColumnStyle cs in tlp.ColumnStyles)
                        if (cs.SizeType == SizeType.Absolute)
                            cs.Width = (int)Math.Round(cs.Width * factor);
                    foreach (RowStyle rs in tlp.RowStyles)
                        if (rs.SizeType == SizeType.Absolute)
                            rs.Height = (int)Math.Round(rs.Height * factor);
                    break;
                case ToolStrip ts:
                    foreach (ToolStripItem item in ts.Items)
                    {
                        if (!item.AutoSize)
                        {
                            item.Size = new Size(
                                (int)Math.Round(item.Size.Width * factor),
                                (int)Math.Round(item.Size.Height * factor));
                        }
                        item.Padding = new Padding(
                            (int)Math.Round(item.Padding.Left * factor),
                            (int)Math.Round(item.Padding.Top * factor),
                            (int)Math.Round(item.Padding.Right * factor),
                            (int)Math.Round(item.Padding.Bottom * factor));
                    }
                    break;
            }
            ScaleChildren(c, factor);
        }
    }

    /// <summary>
    /// 統一的表單標題列：標題 + 副標題 + 強調短線。
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

    /// <summary>工具列白底化：替換舊式深藍漸層列，改為白底＋底部細分隔線（極簡風）</summary>
    public static void StyleTopBar(Panel bar)
    {
        bar.BackColor = Color.White;
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
        };
    }

    // ════════════════════ 控制項樣式 ════════════════════

    /// <summary>DataGridView 統一風格：主色列頭、斑馬紋、懸停列高亮、選取列淺藍</summary>
    public static void StyleDataGridView(DataGridView grid)
    {
        grid.BackgroundColor = Card;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 244, 245);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(244, 244, 245);
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextMain;
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
        grid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 244, 245);
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

    /// <summary>主鍵欄列頭標示：淺藍底深藍字粗體，與一般欄位區別</summary>
    public static void StyleHeaderBold(DataGridViewColumn column)
    {
        column.HeaderCell.Style.BackColor = SelectBack;
        column.HeaderCell.Style.ForeColor = PrimaryDark;
        column.HeaderCell.Style.SelectionBackColor = SelectBack;
        column.HeaderCell.Style.SelectionForeColor = PrimaryDark;
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

    /// <summary>TabControl 統一風格：白底頁籤、選取頁籤深藍字＋底部藍線、未選取淺灰字</summary>
    public static void StyleTabControl(TabControl tabs)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(130, 36);
        tabs.Padding = new Point(20, 6);
        tabs.Appearance = TabAppearance.Normal;
        foreach (TabPage page in tabs.TabPages)
            page.BackColor = Card;
        tabs.DrawItem += (s, e) =>
        {
            var g = e.Graphics;
            var rect = tabs.GetTabRect(e.Index);
            bool selected = e.Index == tabs.SelectedIndex;
            using (var bg = new SolidBrush(selected ? Card : Color.FromArgb(243, 243, 245)))
                g.FillRectangle(bg, rect);
            if (selected)
            {
                using var line = new SolidBrush(Accent);
                g.FillRectangle(line, rect.X, rect.Bottom - 3, rect.Width, 3);
            }
            var fore = selected ? PrimaryDark : TextSub;
            var font = Font(10.5F, selected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, tabs.TabPages[e.Index].Text, font,
                new Rectangle(rect.X, rect.Y - 1, rect.Width, rect.Height),
                fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
    }

    /// <summary>TreeView 統一風格：全列選取、自繪節點、選取列藍色指示</summary>
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
            using (var bg = new SolidBrush(selected ? SelectBack : (root ? Color.FromArgb(241, 244, 249) : Card)))
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

    /// <summary>卡片容器：白底圓角邊框 + 上緣強調短線</summary>
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
