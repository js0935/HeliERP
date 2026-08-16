// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Drawing2D;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>
/// 主視窗：八子系統模組選單（貿易/庫存/收付/薪資/會計/票據/生管/採訂）
/// + 維修管理 + 系統設定。依系統參數決定啟用哪些模組。
/// </summary>
public class MainForm : Form
{
    private readonly DbConfig _config;
    private readonly AppUser _user;
    private readonly ToolTip _cardTip = new() { AutoPopDelay = 6000, InitialDelay = 350, ReshowDelay = 120 };

    /// <summary>模組按鈕資料</summary>
    private record ModuleDef(string Name, string Desc, Func<Form> Open);

    /// <summary>全部模組：側邊導覽與選單列共用（Group 分組、Dev 標示規劃中）</summary>
    private readonly (string Group, string Name, string Desc, Action Open, bool Dev)[] _modules;

    public MainForm(DbConfig config, AppUser user)
    {
        _config = config;
        _user = user;
        _modules = new (string Group, string Name, string Desc, Action Open, bool Dev)[]
        {
            ("營運核心", "貿易系統", "進銷存 / 報價 / 訂單", OpenTradeModule, false),
            ("營運核心", "庫存系統", "庫存現量 / 異動歷史", OpenInventory, false),
            ("營運核心", "收付系統", "應收應付 / 帳款", OpenPaymentModule, false),
            ("營運核心", "應收帳款", "餘額總覽 / 帳齡分析", OpenAccountReceivable, false),
            ("營運核心", "報表列印", "報表預覽 / 列印", OpenReportMenu, false),
            ("作業管理", "採訂系統", "採購 / 訂單", OpenPoOrderModule, false),
            ("作業管理", "維修管理", "叫修 / 維修 / 保固", OpenRepairModule, false),
            ("作業管理", "貨品主檔", "貨品資料維護", OpenProductMaintenance, false),
            ("作業管理", "折讓作業", "出貨 / 進貨折讓", OpenDiscountModule, false),
            ("作業管理", "電子發票", "字軌建置 / 自動配號 / 開立紀錄", () => new InvoiceTrackForm().ShowDialog(this), false),
            ("作業管理", "核准中心", "採購 / 訂貨 / 收付單據多層核准", () => new ApprovalForm().ShowDialog(this), false),
            ("作業管理", "系統健康檢查", "DB 完整性 / WAL / 備份 / 磁碟自檢", () => new HealthCheckForm(_config).ShowDialog(this), false),
            ("作業管理", "基本資料", "資料表維護總覽", OpenTableBrowser, false),
            ("營運核心", "薪資系統", "出缺勤 / 薪資計算", () => new PayrollModuleForm().ShowDialog(this), false),
            ("營運核心", "會計系統", "傳票 / 會計科目", () => new AccountingModuleForm().ShowDialog(this), false),
            ("營運核心", "票據系統", "應收／應付票據管理與報表", () => new BillModuleForm().ShowDialog(this), false),
            ("營運核心", "生管系統", "驗貨品檢 / 託運出貨", () => new ProductionModuleForm().ShowDialog(this), false),
            ("作業管理", "系統設定", "帳號權限 / 公司資料 / 密碼", () => new SystemSettingsForm(_config, _user).ShowDialog(this), false),
        };
        var company = config.Company;

        Text = $"{CompanyTitle()} - 企業資源規劃系統";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1024, 700);
        Font = new Font("Microsoft JhengHei UI", 11F);

        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.K)
            {
                using var search = new GlobalSearchForm();
                search.ShowDialog(this);
            }
        };

        AuditService.CurrentAccount = _user.UserId;
        AuditService.CurrentUser = _user.DisplayName;

        BuildMenu();
        BuildQuickToolbar();
        BuildModules();
        BuildStatusBar();
        Shown += (s, e) =>
        {
            try { BackupService.AutoBackupIfDue(_config); }
            catch { /* 備份失敗不阻擋啟動 */ }
            try
            {
                var issues = HealthCheckService.RunAll(_config)
                    .Where(i => i.Status != HealthCheckService.狀態.正常)
                    .ToList();
                if (issues.Count > 0)
                {
                    string msg = string.Join("\n", issues.Select(i => $"• {i.項目}：{i.說明}"));
                    MessageBox.Show($"系統健康檢查發現以下事項：\n\n{msg}",
                        "健康檢查提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch { /* 檢查失敗不阻擋啟動 */ }
        };
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    private void BuildMenu()
    {
        var menu = new MenuStrip();
        var mSystem = new ToolStripMenuItem("系統(&S)");
        mSystem.DropDownItems.Add("資料庫設定(&D)", null, (s, e) => OpenConfig());
        mSystem.DropDownItems.Add("重新整理資料結構(&R)", null, (s, e) =>
        {
            SchemaReader.Reload();
            MessageBox.Show("資料表結構已重新載入。", "資訊", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        mSystem.DropDownItems.Add(new ToolStripSeparator());
        mSystem.DropDownItems.Add("變更密碼(&P)", null, (s, e) => SystemSettingsForm.ShowChangePassword(this, _user));
        if (_user.IsAdmin)
            mSystem.DropDownItems.Add("稽核日誌(&A)", null, (s, e) =>
            {
                using var log = new AuditLogForm();
                log.ShowDialog(this);
            });
        mSystem.DropDownItems.Add("登出(&L)", null, (s, e) => Logout());
        mSystem.DropDownItems.Add("結束(&X)", null, (s, e) => Application.Exit());
        menu.Items.Add(mSystem);

        foreach (var group in new[] { "營運核心", "作業管理" })
        {
            var items = _modules
                .Where(m => m.Group == group && m.Name != "基本資料" && m.Name != "系統設定")
                .ToList();
            if (items.Count == 0) continue;
            var key = group == "營運核心" ? "C" : "W";
            var mGroup = new ToolStripMenuItem($"{group}(&{key})");
            foreach (var (_, name, desc, open, dev) in items)
            {
                var item = new ToolStripMenuItem(dev ? $"{name}（規劃中）" : name) { ToolTipText = desc };
                item.Click += (s, e) => open();
                mGroup.DropDownItems.Add(item);
            }
            menu.Items.Add(mGroup);
        }

        var mBasic = new ToolStripMenuItem("基本資料(&M)");
        BuildBasicDataMenu(mBasic);
        menu.Items.Add(mBasic);

        var mHelp = new ToolStripMenuItem("說明(&H)");
        mHelp.DropDownItems.Add("快捷鍵與操作說明(&K)", null, (s, e) =>
        {
            using var help = new HelpForm();
            help.ShowDialog(this);
        });
        mHelp.DropDownItems.Add("關於本系統(&A)", null, (s, e) => ShowAbout());
        menu.Items.Add(mHelp);

        MainMenuStrip = menu;
        Controls.Add(menu);
    }

    private void BuildQuickToolbar()
    {
        var toolbar = new ToolStrip();
        UiTheme.StyleToolStrip(toolbar);
        void Add(string text, string tip, Action act)
        {
            var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = tip };
            b.Click += (s, e) => act();
            toolbar.Items.Add(b);
        }
        Add("基本資料", "資料表維護總覽", OpenTableBrowser);
        Add("貨品主檔", "貨品資料維護", OpenProductMaintenance);
        Add("全域搜尋", "全系統快速搜尋 (Ctrl+K)", () => new GlobalSearchForm().ShowDialog(this));
        toolbar.Items.Add(new ToolStripSeparator());
        Add("變更密碼", "變更登入密碼", () => SystemSettingsForm.ShowChangePassword(this, _user));
        Add("登出", "登出目前使用者", Logout);
        Controls.Add(toolbar);
        toolbar.Dock = DockStyle.Top;
    }

    private void BuildModules()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(BuildSidebar(), 0, 0);
        layout.Controls.Add(BuildContent(), 1, 0);
        Controls.Add(layout);
        layout.BringToFront();
    }

    private Control BuildSidebar()
    {
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Sidebar };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = UiTheme.Sidebar,
            Padding = new Padding(0, 0, 8, 12),
        };

        var brand = new Panel
        {
            AutoSize = true,
            Margin = new Padding(20, 18, 12, 0),
            BackColor = Color.Transparent,
        };
        var logo = new Panel
        {
            Size = new Size(42, 42),
            Location = new Point(0, 0),
            BackColor = Color.Transparent,
        };
        logo.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, 41, 41);
            using var path = UiTheme.RoundedRect(rect, UiTheme.RadiusMd);
            using var brush = new SolidBrush(UiTheme.Accent);
            g.FillPath(brush, path);
            UiTheme.DrawCenteredText(g, "禾", UiTheme.Font(18F, FontStyle.Bold), Color.White, rect);
        };
        var lblBrand = new Label
        {
            Text = "HeliERP",
            Font = UiTheme.Font(18F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(54, 2),
        };
        var lblSub = new Label
        {
            Text = "企業資源規劃系統",
            Font = UiTheme.Font(9.5F),
            ForeColor = Color.FromArgb(170, 255, 255, 255),
            AutoSize = true,
            Location = new Point(56, 27),
        };
        brand.Controls.Add(logo);
        brand.Controls.Add(lblBrand);
        brand.Controls.Add(lblSub);
        var line1 = new Panel { Height = 2, BackColor = UiTheme.Accent, Margin = new Padding(24, 10, 12, 14), Size = new Size(60, 2) };
        var lblUser = new Label
        {
            Text = _user.DisplayName,
            Font = UiTheme.Font(12F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(22, 2, 0, 0),
        };
        var lblId = new Label
        {
            Text = $"帳號：{_user.UserId}" + (_user.IsAdmin ? "　（系統管理員）" : ""),
            Font = UiTheme.Font(9F),
            ForeColor = Color.FromArgb(150, 255, 255, 255),
            AutoSize = true,
            Margin = new Padding(24, 0, 0, 10),
        };
        var line2 = new Panel { Height = 1, BackColor = Color.FromArgb(60, 255, 255, 255), Margin = new Padding(20, 4, 12, 10), Size = new Size(196, 1) };

        flow.Controls.Add(brand);
        flow.Controls.Add(line1);
        flow.Controls.Add(lblUser);
        flow.Controls.Add(lblId);
        flow.Controls.Add(line2);

        var modules = _modules;

        string? lastGroup = null;
        using var tip = new ToolTip { AutoPopDelay = 6000, InitialDelay = 350, ReshowDelay = 120 };
        foreach (var (group, name, desc, open, dev) in modules)
        {
            if (group != lastGroup)
            {
                flow.Controls.Add(new Label
                {
                    Text = group,
                    Font = UiTheme.Font(9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(130, 255, 255, 255),
                    AutoSize = true,
                    Margin = new Padding(26, 12, 0, 0),
                });
                lastGroup = group;
            }
            var btn = new ModernButton
            {
                Text = dev ? $"{name}（規劃中）" : name,
                SidebarMode = true,
                SidebarMuted = dev,
                Font = UiTheme.Font(11.5F, FontStyle.Bold),
                Size = new Size(212, 42),
                Margin = new Padding(20, 3, 16, 3),
                CornerRadius = 6,
            };
            tip.SetToolTip(btn, desc);
            btn.Click += (s, e) => open();
            flow.Controls.Add(btn);
        }

        var lblVer = new Label
        {
            Text = "v1.0.0　禾秝軟體開發團隊",
            Font = UiTheme.Font(8.5F),
            ForeColor = Color.FromArgb(110, 255, 255, 255),
            AutoSize = true,
            Margin = new Padding(22, 14, 0, 0),
        };
        flow.Controls.Add(lblVer);

        sidebar.Controls.Add(flow);
        return sidebar;
    }

    private Control BuildContent()
    {
        var content = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
        var zh = new System.Globalization.CultureInfo("zh-TW");

        var header = UiTheme.BuildHeader(
            $"歡迎回來，{_user.DisplayName}",
            "今天是 " + DateTime.Now.ToString("yyyy年M月d日 dddd", zh));
        var btnGlobalSearch = new ModernButton
        {
            Text = "全域搜尋  (Ctrl+K)",
            Width = 170,
            Height = 38,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            IsPrimary = false,
            DrawShadow = false,
        };
        btnGlobalSearch.Click += (s, e) =>
        {
            using var search = new GlobalSearchForm();
            search.ShowDialog(this);
        };
        header.Resize += (s, e) => btnGlobalSearch.Location = new Point(header.Width - btnGlobalSearch.Width - 20, 13);
        header.Controls.Add(btnGlobalSearch);
        content.Controls.Add(header);

        var box = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = UiTheme.Background,
            Padding = new Padding(UiTheme.SpacingXl, UiTheme.SpacingLg, UiTheme.SpacingXl, UiTheme.SpacingXl),
        };

        var dash = DashboardService.Load();
        var c = _config.Company;
        var cards = new (string Title, string[] Lines, Color Accent)[]
        {
            ("公司資訊", new[]
            {
                string.IsNullOrWhiteSpace(c.CompanyName) ? "（尚未設定公司資料）" : c.CompanyName,
                $"統一編號：{c.TaxId}", $"電話：{c.Phone}",
            }, UiTheme.Primary),
        };
        var cardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = UiTheme.Background,
            Margin = new Padding(0, 0, 0, UiTheme.SpacingSm),
        };
        foreach (var (title, lines, accent) in cards)
        {
            var card = new Panel
            {
                Size = new Size(300, 132),
                Margin = new Padding(0, 0, UiTheme.SpacingLg, UiTheme.SpacingLg),
            };
            UiTheme.StyleCardPanel(card, UiTheme.SpacingLg);
            card.Controls.Add(new Panel { Size = new Size(4, 116), BackColor = accent, Location = new Point(0, 8) });
            card.Controls.Add(new Label { Text = title, Font = UiTheme.Font(12F, FontStyle.Bold), ForeColor = UiTheme.Primary, AutoSize = true, Location = new Point(22, 14) });
            int ly = 44;
            foreach (var line in lines)
            {
                card.Controls.Add(new Label { Text = line, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(22, ly), MaximumSize = new Size(252, 0) });
                ly += 22;
            }
            cardsFlow.Controls.Add(card);
        }
        cardsFlow.Controls.Add(StatCard("庫存不足", $"{dash.庫存不足筆數} 項",
            "現有數量低於安全存量", dash.庫存不足筆數 > 0 ? UiTheme.Danger : UiTheme.Ok, OpenInventory));
        cardsFlow.Controls.Add(StatCard("應收帳款餘額", dash.應收餘額.ToString("N0"),
            $"未收單據 {dash.未收單據筆數} 筆", UiTheme.Primary, OpenAccountReceivable));
        cardsFlow.Controls.Add(StatCard("應付帳款餘額", dash.應付餘額.ToString("N0"),
            $"未付單據 {dash.未付單據筆數} 筆", UiTheme.AccentDark, OpenPaymentModule));
        cardsFlow.Controls.Add(StatCard("今日出貨", dash.今日出貨金額.ToString("N0"),
            $"出貨 {dash.今日出貨筆數} 筆", UiTheme.Ok, OpenTradeModule));
        cardsFlow.Controls.Add(StatCard("本月進貨", dash.本月進貨金額.ToString("N0"),
            $"進貨 {dash.本月進貨筆數} 筆", UiTheme.PrimaryLight, OpenTradeModule));
        cardsFlow.Controls.Add(StatCard("庫存總額", dash.庫存總額.ToString("N0"),
            $"{dash.貨品數} 項貨品", UiTheme.PrimaryLight, OpenInventory));
        cardsFlow.Controls.Add(StatCard("本月折讓", dash.本月折讓金額.ToString("N0"),
            $"今日折讓 {dash.今日折讓筆數} 筆 / 本月折讓 {dash.本月折讓筆數} 筆", UiTheme.AccentDark, OpenDiscountModule));
        box.Controls.Add(cardsFlow);

        box.Controls.Add(ShortStockCard(dash));

        box.Controls.Add(BuildChartRow(dash));

        var quickCard = new Panel
        {
            Size = new Size(640, 96),
            Margin = new Padding(0, UiTheme.SpacingSm, 0, 0),
        };
        UiTheme.StyleCardPanel(quickCard, UiTheme.SpacingLg);
        quickCard.Controls.Add(new Label
        {
            Text = "快速入口",
            Font = UiTheme.Font(13F, FontStyle.Bold),
            ForeColor = UiTheme.TextMain,
            AutoSize = true,
            Location = new Point(UiTheme.SpacingLg, UiTheme.SpacingLg),
        });
        var btnRepair = new ModernButton
        {
            Text = "開啟維修管理",
            Size = new Size(180, 44),
            Location = new Point(UiTheme.SpacingLg, 48),
            IsPrimary = true,
        };
        btnRepair.Click += (s, e) => OpenRepairModule();
        var btnGoods = new ModernButton
        {
            Text = "貨品主檔",
            Size = new Size(150, 44),
            Location = new Point(UiTheme.SpacingLg + 192, 48),
            IsPrimary = false,
            DrawShadow = false,
        };
        btnGoods.Click += (s, e) => OpenProductMaintenance();
        var btnTables = new ModernButton
        {
            Text = "基本資料維護",
            Size = new Size(150, 44),
            Location = new Point(UiTheme.SpacingLg + 354, 48),
            IsPrimary = false,
            DrawShadow = false,
        };
        btnTables.Click += (s, e) => OpenTableBrowser();
        quickCard.Controls.Add(btnRepair);
        quickCard.Controls.Add(btnGoods);
        quickCard.Controls.Add(btnTables);
        box.Controls.Add(quickCard);

        content.Controls.Add(box);
        return content;
    }

    private Panel StatCard(string title, string big, string sub, Color accent, Action? onOpen = null)
    {
        var card = new Panel
        {
            Size = new Size(300, 132),
            Margin = new Padding(0, 0, UiTheme.SpacingLg, UiTheme.SpacingLg),
        };
        UiTheme.StyleCardPanel(card, UiTheme.SpacingLg);
        card.Controls.Add(new Panel { Size = new Size(4, 116), BackColor = accent, Location = new Point(0, 8) });
        card.Controls.Add(new Label
        {
            Text = title,
            Font = UiTheme.Font(12F, FontStyle.Bold),
            ForeColor = UiTheme.Primary,
            AutoSize = true,
            Location = new Point(22, 12),
        });
        card.Controls.Add(new Label
        {
            Text = big,
            Font = UiTheme.Font(22F, FontStyle.Bold),
            ForeColor = accent,
            AutoSize = true,
            Location = new Point(22, 42),
            MaximumSize = new Size(252, 0),
        });
        card.Controls.Add(new Label
        {
            Text = sub,
            Font = UiTheme.Font(9.5F),
            ForeColor = UiTheme.TextSub,
            AutoSize = true,
            Location = new Point(22, 102),
        });
        if (onOpen != null)
            MakeCardClickable(card, onOpen, $"點擊開啟「{title}」");
        return card;
    }

    /// <summary>讓整張卡片可點擊：手型游標、hover 底色、提示與子控制項點擊轉發。</summary>
    private void MakeCardClickable(Control root, Action onOpen, string tip)
    {
        root.Cursor = Cursors.Hand;
        _cardTip.SetToolTip(root, tip);
        void Enter(object? s, EventArgs e) => root.BackColor = UiTheme.HoverRow;
        void Leave(object? s, EventArgs e) => root.BackColor = UiTheme.Card;
        root.MouseEnter += Enter;
        root.MouseLeave += Leave;
        root.Click += (s, e) => onOpen();
        foreach (Control c in root.Controls)
        {
            c.Cursor = Cursors.Hand;
            c.MouseEnter += Enter;
            c.MouseLeave += Leave;
            c.Click += (s, e) => onOpen();
        }
    }

    private Panel ShortStockCard(DashboardData dash)
    {
        var card = new Panel
        {
            Size = new Size(640, 190),
            Margin = new Padding(0, UiTheme.SpacingSm, 0, 0),
        };
        UiTheme.StyleCardPanel(card, UiTheme.SpacingLg);
        card.Controls.Add(new Label
        {
            Text = "庫存不足警示",
            Font = UiTheme.Font(13F, FontStyle.Bold),
            ForeColor = UiTheme.TextMain,
            AutoSize = true,
            Location = new Point(UiTheme.SpacingLg, UiTheme.SpacingLg),
        });
        var btnInv = new ModernButton
        {
            Text = "開啟庫存管理",
            Size = new Size(130, 32),
            Location = new Point(494, 10),
            IsPrimary = false,
            DrawShadow = false,
        };
        btnInv.Click += (s, e) => OpenInventory();
        card.Controls.Add(btnInv);

        if (dash.庫存不足清單.Rows.Count == 0)
        {
            card.Controls.Add(new Label
            {
                Text = "✔ 庫存正常，目前沒有低於安全存量的貨品",
                Font = UiTheme.Font(11F),
                ForeColor = UiTheme.Ok,
                AutoSize = true,
                Location = new Point(UiTheme.SpacingLg, 56),
            });
            return card;
        }

        var grid = new DataGridView
        {
            Location = new Point(16, 52),
            Size = new Size(608, 124),
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
        };
        UiTheme.StyleDataGridView(grid);
        grid.RowTemplate.Height = 26;
        grid.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Name is "貨品編號" or "品名" or "倉庫名稱" or "現有數量" or "安全存量")
                    continue;
                col.Visible = false;
            }
            if (grid.Columns.Contains("現有數量"))
                grid.Columns["現有數量"].DefaultCellStyle.Format = "N1";
            if (grid.Columns.Contains("安全存量"))
                grid.Columns["安全存量"].DefaultCellStyle.Format = "N1";
        };
        grid.DataSource = dash.庫存不足清單;
        card.Controls.Add(grid);
        return card;
    }

    // ==================== 商業智慧圖表（2026 儀表板） ====================

    private Panel BuildChartRow(DashboardData dash)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = UiTheme.Background,
            Margin = new Padding(0, UiTheme.SpacingSm, 0, 0),
        };
        row.Controls.Add(TrendChartCard(dash));
        row.Controls.Add(AgingChartCard(dash));
        row.Controls.Add(TopCustomerChartCard(dash));
        row.Controls.Add(BusinessKpiCards(dash));
        return row;
    }

    private Panel ChartCard(string title, ChartControl chart, int width = 640, int height = 300)
    {
        var card = new Panel
        {
            Size = new Size(width, height),
            Margin = new Padding(0, 0, UiTheme.SpacingLg, UiTheme.SpacingLg),
        };
        UiTheme.StyleCardPanel(card, UiTheme.SpacingMd);
        chart.ChartTitle = title;
        chart.Dock = DockStyle.Fill;
        card.Controls.Add(chart);
        return card;
    }

    private Panel TrendChartCard(DashboardData dash)
    {
        var chart = new ChartControl { BarMode = true };
        chart.AddSeries("出貨", dash.近12月營收, UiTheme.Primary);
        chart.AddSeries("進貨", dash.近12月進貨, UiTheme.Accent);
        chart.AddSeries("折讓", dash.近12月折讓, UiTheme.Danger);
        chart.Labels = dash.月份標籤;
        return ChartCard("近 12 個月 出貨／進貨／折讓 金額趨勢", chart);
    }

    private Panel AgingChartCard(DashboardData dash)
    {
        var chart = new ChartControl { BarMode = true };
        chart.AddSeries("應收金額", dash.應收帳齡, UiTheme.Primary, new[]
        {
            UiTheme.Ok, UiTheme.Accent, UiTheme.Warn, UiTheme.Danger, Color.FromArgb(150, 30, 40),
        });
        chart.Labels = new[] { "未逾期", "1-30天", "31-60天", "61-90天", "90天+" };
        return ChartCard($"應收帳款帳齡（逾期合計 {dash.逾期未收金額:N0}）", chart);
    }

    private Panel TopCustomerChartCard(DashboardData dash)
    {
        var chart = new ChartControl { BarMode = true };
        var labels = new List<string>();
        var values = new List<decimal>();
        foreach (DataRow r in dash.客戶業績TOP.Rows)
        {
            string name = r["客戶"]?.ToString() ?? "";
            labels.Add(name.Length > 6 ? name[..6] + "…" : name);
            values.Add(r["業績"] is DBNull or null ? 0m : Convert.ToDecimal(r["業績"]));
        }
        chart.AddSeries("業績", values.ToArray(), UiTheme.PrimaryLight);
        chart.Labels = labels.ToArray();
        return ChartCard("近 6 個月 客戶業績 TOP 8", chart);
    }

    private Panel BusinessKpiCards(DashboardData dash)
    {
        var row = new Panel
        {
            Size = new Size(640, 300),
            Margin = new Padding(0, 0, UiTheme.SpacingLg, UiTheme.SpacingLg),
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = UiTheme.Card,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        grid.Controls.Add(MiniKpi("貨品數", $"{dash.貨品數:N0}", "現行貨品主檔筆數", UiTheme.Primary), 0, 0);
        grid.Controls.Add(MiniKpi("客戶數", $"{dash.客戶數:N0}", "往來客戶家數", UiTheme.Ok), 1, 0);
        grid.Controls.Add(MiniKpi("廠商數", $"{dash.廠商數:N0}", "往來廠商家數", UiTheme.AccentDark), 0, 1);
        grid.Controls.Add(MiniKpi("庫存現值", dash.庫存總額.ToString("N0"), "以平均成本估算", UiTheme.PrimaryLight), 1, 1);
        row.Controls.Add(grid);
        return row;
    }

    private Panel MiniKpi(string title, string big, string sub, Color accent)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(UiTheme.SpacingSm),
            BackColor = UiTheme.Card,
        };
        UiTheme.StyleCardPanel(card, UiTheme.SpacingMd);
        card.Controls.Add(new Panel { Size = new Size(4, 90), BackColor = accent, Location = new Point(0, 8) });
        card.Controls.Add(new Label
        {
            Text = title,
            Font = UiTheme.Font(10.5F, FontStyle.Bold),
            ForeColor = UiTheme.TextSub,
            AutoSize = true,
            Location = new Point(18, 12),
        });
        card.Controls.Add(new Label
        {
            Text = big,
            Font = UiTheme.Font(17F, FontStyle.Bold),
            ForeColor = accent,
            AutoSize = true,
            Location = new Point(18, 40),
        });
        card.Controls.Add(new Label
        {
            Text = sub,
            Font = UiTheme.Font(9F),
            ForeColor = UiTheme.TextFaint,
            AutoSize = true,
            Location = new Point(18, 82),
        });
        return card;
    }

    /// <summary>主視窗標題用公司名稱；尚未設定時顯示 HeliERP</summary>
    private string CompanyTitle()
    {
        var name = _config.Company.CompanyName;
        return string.IsNullOrWhiteSpace(name) ? "HeliERP" : name;
    }

    private void BuildStatusBar()
    {
        var status = new StatusStrip { SizingGrip = false, BackColor = UiTheme.Card, Padding = new Padding(12, 2, 8, 2) };
        var company = _config.Company;
        status.Items.Add(new ToolStripStatusLabel($"{CompanyTitle()}　統一編號 {company.TaxId}"));
        status.Items.Add(new ToolStripStatusLabel("  |  "));
        status.Items.Add(new ToolStripStatusLabel($"使用者：{_user.DisplayName}（{_user.UserId}）"));
        status.Items.Add(new ToolStripStatusLabel("  |  "));
        var clock = new ToolStripStatusLabel();
        clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss dddd", new System.Globalization.CultureInfo("zh-TW"));
        status.Items.Add(clock);
        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) =>
            clock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss dddd", new System.Globalization.CultureInfo("zh-TW"));
        timer.Start();
        Controls.Add(status);
    }

    private void OpenRepairModule()
    {
        using var form = new RepairModuleForm(_user);
        form.ShowDialog(this);
    }

    private void OpenTradeModule()
    {
        using var form = new TransactionForm(_user);
        form.ShowDialog(this);
    }

    private void OpenPaymentModule()
    {
        using var form = new PaymentForm();
        form.ShowDialog(this);
    }

    private void OpenAccountReceivable()
    {
        using var form = new AccountReceivableForm();
        form.ShowDialog(this);
    }

    private void OpenReportMenu()
    {
        using var form = new ReportMenuForm();
        form.ShowDialog(this);
    }

    private void OpenInventory()
    {
        using var form = new InventoryForm();
        form.ShowDialog(this);
    }

    private void OpenPoOrderModule()
    {
        using var form = new PoOrderForm();
        form.ShowDialog(this);
    }

    private void OpenDiscountModule()
    {
        using var form = new DiscountForm();
        form.ShowDialog(this);
    }

    private void OpenProductMaintenance()
    {
        using var form = new ProductMaintenanceForm();
        form.ShowDialog(this);
    }

    private void OpenTableBrowser()
    {
        using var form = new TableBrowserForm();
        form.ShowDialog(this);
    }

    private void OpenGenericTable(string tableName)
    {
        try
        {
            using var form = new GenericTableForm(tableName);
            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"開啟「{tableName}」失敗：{ex.Message}", "錯誤",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BuildBasicDataMenu(ToolStripMenuItem root)
    {
        foreach (var main in new[] { "基本資料", "系統設定" })
        {
            var mMain = new ToolStripMenuItem(main);
            foreach (var sub in TableCatalog.GetSubs(main))
            {
                var tables = TableCatalog.GetTables(main, sub);
                if (tables.Count == 0) continue;
                var mSub = new ToolStripMenuItem(sub);
                foreach (var t in tables)
                {
                    var name = t.Name;
                    mSub.DropDownItems.Add(name, null, (s, e) => OpenGenericTable(name));
                }
                mMain.DropDownItems.Add(mSub);
            }
            if (mMain.DropDownItems.Count > 0)
                root.DropDownItems.Add(mMain);
        }
    }

    private void OpenConfig()
    {
        using var form = new ConfigForm(_config);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            DbManager.DatabasePath = _config.DatabasePath;
            SchemaReader.Reload();
            Text = $"{CompanyTitle()} - 企業資源規劃系統";
        }
    }

    private void Logout()
    {
        var confirm = MessageBox.Show("確定要登出嗎？", "登出", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;
        AuditService.Log(AuditService.登出, "系統", _user.UserId, "成功", $"使用者 {_user.DisplayName} 登出");
        var config = DbConfig.Load();
        DbManager.DatabasePath = config.DatabasePath;
        using var login = new LoginForm(config);
        if (login.ShowDialog(this) == DialogResult.OK)
        {
            // 重新以新使用者啟動（簡易做法：重新執行流程）
            var newUser = login.LoggedInUser!;
            var main = new MainForm(config, newUser);
            main.Show();
            Hide();
        }
    }

    private void ShowAbout()
    {
        var c = _config.Company;
        MessageBox.Show(
            $"{CompanyTitle()}\n企業資源規劃系統 v1.0.0\n\n" +
            "軟體屬名：禾秝軟體開發團隊\n代碼：洪俊士\n版本：1.0.0\n\n" +
            $"資料庫：{DbManager.DatabasePath}\n\n© 2026 {c.Owner}",
            "關於本系統", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
