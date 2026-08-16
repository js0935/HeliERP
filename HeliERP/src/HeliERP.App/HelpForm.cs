// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;

namespace HeliERP.App;

/// <summary>
/// 說明中心：快捷鍵總表、模組導覽（依側邊導覽分類）、常見作業流程。
/// 由「說明」選單開啟；傳入側邊導覽模組清單時顯示一致的分類與說明。
/// </summary>
public sealed class HelpForm : Form
{
    public HelpForm((string Group, string Name, string Desc, Action Open, bool Dev)[]? modules = null)
    {
        Text = "說明中心";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(660, 520);
        BackColor = UiTheme.Background;
        Font = UiTheme.Font(10F);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        Controls.Add(UiTheme.BuildHeader("說明中心",
            "快捷鍵、模組導覽與常見作業流程", 56));

        var tabs = new TabControl { Dock = DockStyle.Fill };
        UiTheme.StyleTabControl(tabs);
        tabs.TabPages.Add(BuildShortcutPage());
        tabs.TabPages.Add(BuildModulePage(modules));
        tabs.TabPages.Add(BuildFlowPage());
        Controls.Add(tabs);

        var btnClose = new ModernButton
        {
            Text = "關　閉",
            Width = 110,
            Height = 40,
            IsPrimary = false,
            DrawShadow = false,
            Location = new Point(ClientSize.Width - 130, ClientSize.Height - 54),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnClose.Click += (s, e) => Close();
        Controls.Add(btnClose);
        AcceptButton = btnClose;
        UiTheme.ScaleForDpi(this);
        UiTheme.ClampToScreen(this);
    }

    private static TabPage BuildShortcutPage()
    {
        var dt = new DataTable();
        dt.Columns.Add("快捷鍵", typeof(string));
        dt.Columns.Add("功能", typeof(string));
        dt.Columns.Add("適用範圍", typeof(string));
        dt.Rows.Add("F1", "開啟說明中心", "主畫面");
        dt.Rows.Add("F2", "新增資料", "各資料維護畫面");
        dt.Rows.Add("F3", "修改／編輯資料", "各資料維護畫面");
        dt.Rows.Add("F4", "刪除資料", "各資料維護畫面");
        dt.Rows.Add("F5", "重整／重新載入資料", "查詢與維護畫面");
        dt.Rows.Add("Ctrl + F", "搜尋／查詢", "各資料維護畫面");
        dt.Rows.Add("Ctrl + K", "全域快速搜尋", "主畫面");
        dt.Rows.Add("Esc", "關閉視窗／取消", "對話框");
        dt.Rows.Add("雙擊資料列", "開啟編輯", "表格型維護畫面");
        var page = new TabPage("快捷鍵");
        page.Controls.Add(MakeGrid(dt, new[] { 110, 240, 200 }, "快捷鍵"));
        return page;
    }

    private static TabPage BuildModulePage((string Group, string Name, string Desc, Action Open, bool Dev)[]? modules)
    {
        var dt = new DataTable();
        dt.Columns.Add("分類", typeof(string));
        dt.Columns.Add("模組", typeof(string));
        dt.Columns.Add("功能", typeof(string));
        var list = modules ?? DefaultModules();
        foreach (var (group, name, desc, open, dev) in list)
            dt.Rows.Add(group, name, desc);
        var page = new TabPage("模組導覽");
        page.Controls.Add(MakeGrid(dt, new[] { 90, 110, 340 }, "分類"));
        return page;
    }

    private static TabPage BuildFlowPage()
    {
        var dt = new DataTable();
        dt.Columns.Add("流程", typeof(string));
        dt.Columns.Add("步驟", typeof(string));
        dt.Rows.Add("多層核准", "新增／修改單據 → 送審 → 依權限層級核准 → 核准後單據生效");
        dt.Rows.Add("維修作業", "叫修登錄 → 派工（內修／外送）→ 維修 → 交貨 → 保固追蹤 → 帳款");
        dt.Rows.Add("電子發票", "字軌建置 → 自動配號 → 開立 → 作廢／退回（皆留稽核紀錄）");
        dt.Rows.Add("應收帳款", "對象餘額總覽 → 帳齡分析 → 對帳 → 收付沖銷 → 逾期追蹤");
        dt.Rows.Add("庫存調整", "選擇調整類別（盤點盤盈虧／報廢／進出貨）→ 填寫數量成本 → 過帳至庫存歷史");
        dt.Rows.Add("系統健康檢查", "DB 完整性 → WAL 檢查 → 資料庫備份 → 磁碟空間自檢");
        var page = new TabPage("作業流程");
        page.Controls.Add(MakeGrid(dt, new[] { 110, 440 }, "流程"));
        return page;
    }

    private static (string Group, string Name, string Desc, Action Open, bool Dev)[] DefaultModules()
        => new (string Group, string Name, string Desc, Action Open, bool Dev)[]
        {
            ("營運核心", "貿易系統", "進銷存 / 報價 / 訂單", () => { }, false),
        ("營運核心", "庫存系統", "庫存現量 / 異動歷史", () => { }, false),
        ("營運核心", "報表列印", "報表預覽 / 列印", () => { }, false),
        ("營運核心", "基本資料", "資料表維護總覽", () => { }, false),
        ("財務會計", "收付系統", "應收應付 / 帳款", () => { }, false),
        ("財務會計", "應收帳款", "餘額總覽 / 帳齡分析", () => { }, false),
        ("財務會計", "票據系統", "應收／應付票據管理與報表", () => { }, false),
        ("財務會計", "會計系統", "傳票 / 會計科目", () => { }, false),
        ("財務會計", "折讓作業", "出貨 / 進貨折讓", () => { }, false),
        ("財務會計", "電子發票", "字軌建置 / 自動配號 / 開立紀錄", () => { }, false),
        ("財務會計", "核准中心", "多層核准單據查核", () => { }, false),
        ("財務會計", "薪資系統", "出缺勤 / 薪資計算", () => { }, false),
        ("生產作業", "採訂系統", "採購 / 訂單", () => { }, false),
        ("生產作業", "生管系統", "驗貨品檢 / 託運出貨", () => { }, false),
        ("生產作業", "維修管理", "叫修 / 維修 / 保固", () => { }, false),
        ("生產作業", "貨品主檔", "貨品資料維護", () => { }, false),
        ("系統管理", "系統健康檢查", "DB 完整性 / WAL / 備份 / 磁碟自檢", () => { }, false),
        ("系統管理", "系統設定", "帳號權限 / 公司資料 / 密碼", () => { }, false),
    };

    private static DataGridView MakeGrid(DataTable dt, int[] widths, string accentColumn)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = dt,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = UiTheme.Card,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        };
        UiTheme.StyleDataGridView(grid);
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = dt.Columns[i].ColumnName,
                DataPropertyName = dt.Columns[i].ColumnName,
                Width = i < widths.Length ? widths[i] : 120,
            };
            if (dt.Columns[i].ColumnName == accentColumn)
                UiTheme.StyleHeaderBold(col);
            grid.Columns.Add(col);
        }
        return grid;
    }
}
