// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using HeliERP.Data;
using HeliERP.Models;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 維修管理子系統：維修單查詢/新增/編輯/狀態管理。
/// 流程：叫修登記 → 收件 → 維修（內修/外送）→ 交貨 → 保固追蹤 → 帳款。
/// 完整 94 欄位可編輯（分頁：基本、客戶叫修、費用帳款、維修內容、全部欄位）。
/// </summary>
public class RepairModuleForm : Form
{
    private const string TableName = "維修主檔";
    private static string ReportDir => ReportPrintService.RepDirectory;

    // 列表
    private DataTable _listDt = new();
    private DataGridView _grid = null!;
    private TextBox _txtNo = null!, _txtCustomer = null!, _txtGoods = null!;
    private ComboBox _cmbStatusFilter = null!;
    private DateTimePicker _dtFrom = null!, _dtTo = null!;

    // 編輯用（基本 + 客戶叫修 + 費用帳款）
    private readonly Dictionary<string, Control> _editors = new();
    private TextBox _txtCustomerName = null!, _txtGoodsName = null!;
    private TextBox _txtFault = null!, _txtCause = null!, _txtSituation = null!, _txtRemark = null!;
    private DataGridView _gridAll = null!;

    // 導航列 + 狀態列
    private ModernButton _btnFirst = null!, _btnPrev = null!, _btnNext = null!, _btnLast = null!;
    private Label _lblRecord = null!, _lblStatus = null!;
    private int _currentIndex = -1;

    // 狀態
    private DataRow? _currentRow;
    private string _currentKey = "";

    private readonly AppUser? _user;

    private readonly List<string> _dateFields = new();
    private readonly List<string> _timeFields = new();
    private readonly List<string> _moneyFields = new();
    private readonly List<string> _memoFields = new();

    private static readonly string[] StatusList = { "未處理", "內修", "外送", "交貨", "結案" };
    private static readonly string[] RepairTypeList = { "保固內維修", "保固外維修", "合約內維修" };
    private static readonly string[] RepairWayList = { "自取", "到府服務", "快遞收送" };
    private static readonly string[] BuyTypeList = { "客戶外購", "公司銷售" };
    private static readonly string[] TimeSlots =
        Enumerable.Range(0, 48).Select(i => $"{(i / 2):00}:{(i % 2 == 0 ? "00" : "30")}").ToArray();

    public RepairModuleForm(AppUser? user = null)
    {
        _user = user;
        var table = SchemaReader.GetTable(TableName)
            ?? throw new InvalidOperationException($"找不到資料表「{TableName}」");
        var cols = table.Columns;
        foreach (var c in cols)
        {
            var name = c.Name;
            if (name.Contains("日期") || (name.Length <= 3 && name[0] == 'D' && name.All(ch => char.IsDigit(ch) || ch == 'D')))
                _dateFields.Add(name);
            else if (name.Contains("時間"))
                _timeFields.Add(name);
            else if (name.Contains("金額") || name.Contains("費用") || name.Contains("稅") ||
                     name.Contains("單價") || name.Contains("數量") || name.Contains("折讓") ||
                     name.Contains("理賠") || name.Contains("里程") || name.Contains("合計") ||
                     name.Contains("總計"))
                _moneyFields.Add(name);
            else if (c.Type.Contains("MEMO") || name.Contains("現象") || name.Contains("原因") ||
                     name.Contains("情況"))
                _memoFields.Add(name);
        }

        Text = "維修管理";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 660);
        UiTheme.Apply(this);

        Controls.Add(UiTheme.BuildHeader("維修管理", "叫修登記 → 收件 → 維修（內修/外送）→ 交貨 → 保固追蹤 → 帳款"));

        BuildToolbar();
        BuildSearchPanel();
        BuildListGrid();
        BuildDetailTabs();

        LoadList();

        ShortcutHelper.Enable(this,
            NewBill,
            () =>
            {
                if (string.IsNullOrEmpty(_currentKey)) return;
                LoadBill(_currentKey);
                _lblStatus.Text = "狀態: 修改中";
            },
            DeleteBill,
            LoadList,
            LoadList);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    // ==================== UI 建立 ====================

    private void BuildToolbar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 52 };
        UiTheme.StyleTopBar(bar);

        // 按舊系統工具列配置：搜尋 重讀 新增 修改 刪除 列印 | 儲存 復原 | 首筆 上筆 下筆 尾筆 | 說明 離開
        int x = UiTheme.SpacingMd;
        void Add(ModernButton b)
        {
            b.Location = new Point(x, 6);
            b.Height = 40;
            b.DrawShadow = false;
            bar.Controls.Add(b);
            x += b.Width + UiTheme.SpacingSm;
        }
        void Sep()
        {
            bar.Controls.Add(new Panel
            {
                Location = new Point(x, 10),
                Size = new Size(2, 32),
                BackColor = UiTheme.Border,
            });
            x += UiTheme.SpacingSm + 2;
        }

        var btnSearch = new ModernButton { Text = "搜尋", Width = 120 };
        btnSearch.Click += (s, e) => { LoadList(); _txtNo.Focus(); };
        var btnReload = new ModernButton { Text = "重讀", Width = 120, IsPrimary = false };
        btnReload.Click += (s, e) => LoadList();
        var btnNew = new ModernButton { Text = "新增", Width = 120 };
        btnNew.Click += (s, e) => NewBill();
        var btnEdit = new ModernButton { Text = "修改", Width = 120, IsPrimary = false };
        btnEdit.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(_currentKey)) return;
            LoadBill(_currentKey);
            _lblStatus.Text = "狀態: 修改中";
        };
        var btnDel = new ModernButton { Text = "刪除", Width = 120, IsPrimary = false };
        btnDel.Click += (s, e) => DeleteBill();
        var btnPrint = new ModernButton { Text = "列印", Width = 120, IsPrimary = false };
        btnPrint.Click += (s, e) => PrintBill();

        Add(btnSearch); Add(btnReload); Add(btnNew); Add(btnEdit); Add(btnDel); Add(btnPrint);
        Sep();

        var btnSave = new ModernButton { Text = "儲存", Width = 120 };
        btnSave.Click += (s, e) => SaveBill();
        var btnRevert = new ModernButton { Text = "復原", Width = 120, IsPrimary = false };
        btnRevert.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(_currentKey)) return;
            LoadBill(_currentKey);
            _lblStatus.Text = "狀態: 已復原";
        };

        Add(btnSave); Add(btnRevert);
        Sep();

        _btnFirst = new ModernButton { Text = "首筆", Width = 128, IsPrimary = false };
        _btnPrev = new ModernButton { Text = "上筆", Width = 128, IsPrimary = false };
        _btnNext = new ModernButton { Text = "下筆", Width = 128, IsPrimary = false };
        _btnLast = new ModernButton { Text = "尾筆", Width = 128, IsPrimary = false };
        _btnFirst.Click += (s, e) => SelectRow(0);
        _btnPrev.Click += (s, e) => SelectRow(_currentIndex - 1);
        _btnNext.Click += (s, e) => SelectRow(_currentIndex + 1);
        _btnLast.Click += (s, e) => SelectRow(_grid.Rows.Count - 1);

        Add(_btnFirst); Add(_btnPrev); Add(_btnNext); Add(_btnLast);
        Sep();

        var btnHelp = new ModernButton { Text = "說明", Width = 120, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show("維修管理系統 v1.0\n流程：叫修登記 → 收件 → 維修（內修/外送）→ 交貨 → 保固追蹤 → 帳款。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 120, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnHelp); Add(btnExit);

        // 資料表資訊（右上）
        var info = new Label
        {
            Text = $"資料表：{TableName}（{SchemaReader.GetTable(TableName)!.Columns.Count} 欄）",
            ForeColor = Color.White,
            Font = UiTheme.Font(10.5F),
            AutoSize = true,
        };
        info.Location = new Point(bar.Width - info.Width - 16, 16);
        bar.Resize += (s, e) => info.Location = new Point(bar.Width - info.Width - 16, 16);
        bar.Controls.Add(info);

        Controls.Add(bar);
    }

    private void BuildSearchPanel()
    {
        var card = new Panel { Dock = DockStyle.Top, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingSm, UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs) };
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, UiTheme.SpacingSm),
            WrapContents = true,
            BackColor = UiTheme.Card,
        };
        var lblNo = new Label { Text = "單號：", Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblNo, sub: true);
        panel.Controls.Add(lblNo);
        _txtNo = new TextBox { Width = 120 };
        UiTheme.StyleTextBox(_txtNo);
        panel.Controls.Add(_txtNo);
        var lblCust = new Label { Text = "客戶：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblCust, sub: true);
        panel.Controls.Add(lblCust);
        _txtCustomer = new TextBox { Width = 160 };
        UiTheme.StyleTextBox(_txtCustomer);
        panel.Controls.Add(_txtCustomer);
        var lblGoods = new Label { Text = "品名：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblGoods, sub: true);
        panel.Controls.Add(lblGoods);
        _txtGoods = new TextBox { Width = 140 };
        UiTheme.StyleTextBox(_txtGoods);
        panel.Controls.Add(_txtGoods);
        var lblStatus = new Label { Text = "狀態：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblStatus, sub: true);
        panel.Controls.Add(lblStatus);
        _cmbStatusFilter = new ComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbStatusFilter);
        _cmbStatusFilter.Items.AddRange(new object[] { "全部", "未處理", "內修", "外送", "交貨", "結案" });
        _cmbStatusFilter.SelectedIndex = 0;
        panel.Controls.Add(_cmbStatusFilter);
        var lblFrom = new Label { Text = "叫修日期：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblFrom, sub: true);
        panel.Controls.Add(lblFrom);
        _dtFrom = new DateTimePicker { Width = 110, Format = DateTimePickerFormat.Short, Checked = false, ShowCheckBox = true };
        UiTheme.StyleDateTimePicker(_dtFrom);
        panel.Controls.Add(_dtFrom);
        var lblTo = new Label { Text = "～", Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblTo, sub: true);
        panel.Controls.Add(lblTo);
        _dtTo = new DateTimePicker { Width = 110, Format = DateTimePickerFormat.Short, Checked = false, ShowCheckBox = true };
        UiTheme.StyleDateTimePicker(_dtTo);
        panel.Controls.Add(_dtTo);
        var btnSearch = new ModernButton { Text = "查詢", Width = 84, Height = 34, IsPrimary = true };
        btnSearch.Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0);
        btnSearch.Click += (s, e) => LoadList();
        panel.Controls.Add(btnSearch);
        var btnClear = new ModernButton { Text = "清除條件", Width = 96, Height = 34, IsPrimary = false };
        btnClear.Margin = new Padding(UiTheme.SpacingSm, UiTheme.SpacingSm, 0, 0);
        btnClear.Click += (s, e) =>
        {
            _txtNo.Clear(); _txtCustomer.Clear(); _txtGoods.Clear();
            _cmbStatusFilter.SelectedIndex = 0;
            _dtFrom.Checked = _dtTo.Checked = false;
            LoadList();
        };
        panel.Controls.Add(btnClear);
        card.Controls.Add(panel);
        Controls.Add(card);
    }

    private void BuildListGrid()
    {
        _grid = new DataGridView
        {
            Dock = DockStyle.Top,
            Height = 150,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            RowHeadersVisible = true,
            RowHeadersWidth = 44,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        UiTheme.StyleDataGridView(_grid);
        _grid.RowTemplate.Height = 34;
        _grid.SelectionChanged += (s, e) => OnRowSelected();
        Controls.Add(_grid);
    }

    private void BuildStatusBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = UiTheme.BorderLight };
        _lblRecord = new Label
        {
            Text = "記錄: 0 / 0",
            AutoSize = true,
            Location = new Point(12, 5),
            ForeColor = UiTheme.Primary,
            Font = UiTheme.Font(10.5F, FontStyle.Bold),
        };
        _lblStatus = new Label
        {
            Text = "狀態: 檢視",
            AutoSize = true,
            ForeColor = UiTheme.TextSub,
            Font = UiTheme.Font(9.5F),
        };
        _lblStatus.Location = new Point(bar.Width - _lblStatus.Width - 12, 5);
        bar.Resize += (s, e) => _lblStatus.Location = new Point(bar.Width - _lblStatus.Width - 12, 5);
        bar.Controls.Add(_lblRecord);
        bar.Controls.Add(_lblStatus);
        Controls.Add(bar);
    }

    private void BuildDetailTabs()
    {
        BuildStatusBar();
        var tabs = new TabControl { Dock = DockStyle.Fill };
        UiTheme.StyleTabControl(tabs);
        tabs.TabPages.Add(BuildRepairCard());
        tabs.TabPages.Add(BuildMoneyTab());
        tabs.TabPages.Add(BuildAllFieldsTab());
        Controls.Add(tabs);
        tabs.BringToFront();
    }

    private TabPage BuildRepairCard()
    {
        var page = new TabPage("維修單");
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UiTheme.Background };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 8,
            RowCount = 11,
            Padding = new Padding(UiTheme.SpacingXl, UiTheme.SpacingMd, UiTheme.SpacingXl, UiTheme.SpacingSm),
        };
        for (int i = 0; i < 8; i++)
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        for (int i = 0; i < 11; i++)
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, i is 7 or 8 or 9 or 10 ? 62 : 42));

        AddEditor(panel, "維修單號", "交易單號", 0, 0, readOnly: true);
        AddEditor(panel, "目前狀態", "目前狀態", 0, 1, comboItems: StatusList);
        AddEditor(panel, "維修類別", "維修類別", 0, 2, comboItems: RepairTypeList);
        AddEditor(panel, "交易日期", "交易日期", 0, 3, isDate: true);

        AddEditor(panel, "客戶編號", "交易對象", 1, 0);
        _txtCustomerName = new TextBox();
        UiTheme.StyleTextBox(_txtCustomerName, readOnly: true);
        var (lblCust, ctrlCust) = CreatePair(panel, "客戶名稱", _txtCustomerName);
        panel.Controls.Add(lblCust, 2, 1);
        panel.Controls.Add(ctrlCust, 3, 1);
        AddEditor(panel, "貨品編號", "貨品編號", 1, 2);
        _txtGoodsName = new TextBox();
        UiTheme.StyleTextBox(_txtGoodsName, readOnly: true);
        var (lblGoods, ctrlGoods) = CreatePair(panel, "品名", _txtGoodsName);
        panel.Controls.Add(lblGoods, 6, 1);
        panel.Controls.Add(ctrlGoods, 7, 1);

        AddEditor(panel, "批號(貨品序號)", "批號", 2, 0);
        AddEditor(panel, "數量合計", "數量合計", 2, 1);
        AddEditor(panel, "送修方式", "送修方式", 2, 2, comboItems: RepairWayList);
        AddEditor(panel, "購買類別", "購買類別", 2, 3, comboItems: BuyTypeList);

        AddEditor(panel, "聯絡人", "聯絡人", 3, 0);
        AddEditor(panel, "聯絡電話", "聯絡電話", 3, 1);
        AddEditor(panel, "行動電話", "行動電話", 3, 2);
        AddEditor(panel, "叫修日期", "叫修日期", 3, 3, isDate: true);

        AddEditor(panel, "約定日期", "約定日期", 4, 0, isDate: true);
        AddEditor(panel, "約定時間", "約定時間", 4, 1, isTime: true);
        AddEditor(panel, "收件日期", "收件日期", 4, 2, isDate: true);
        AddEditor(panel, "交貨日期", "交貨日期", 4, 3, isDate: true);

        AddEditor(panel, "外送廠商", "外送廠商", 5, 0);
        AddEditor(panel, "員工編號", "員工編號", 5, 1);
        AddEditor(panel, "保固日期", "保固日期", 5, 2, isDate: true);
        AddEditor(panel, "總計金額", "總計金額", 5, 3);

        AddEditor(panel, "叫修地址", "叫修地址", 6, 0, spanCol: 4);
        TextBox AddMemo(string label, int row)
        {
            var tb = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
            UiTheme.StyleTextBox(tb);
            var (lbl, ctrl) = CreatePair(panel, label, tb);
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(ctrl, 1, row);
            panel.SetColumnSpan(ctrl, 7);
            return tb;
        }
        _txtFault = AddMemo("故障現象", 7);
        _txtCause = AddMemo("故障原因", 8);
        _txtSituation = AddMemo("維修情況", 9);
        _txtRemark = AddMemo("備註", 10);

        scroll.Controls.Add(panel);
        page.Controls.Add(scroll);
        return page;
    }

    private TabPage BuildMoneyTab()
    {
        var page = new TabPage("費用與帳款");
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingMd, UiTheme.SpacingLg, UiTheme.SpacingMd) };
        for (int i = 0; i < 4; i++)
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddEditor(panel, "規格", "規格", 0, 0, spanCol: 2);
        AddEditor(panel, "合計金額", "合計金額", 0, 2);
        AddEditor(panel, "營業稅", "營業稅", 0, 3);
        AddEditor(panel, "工資費用", "工資費用", 1, 0);
        AddEditor(panel, "零件費用", "零件費用", 1, 1);
        AddEditor(panel, "理賠金額", "理賠金額", 1, 2);
        AddEditor(panel, "折讓金額", "折讓金額", 1, 3);
        AddEditor(panel, "已收付金額", "已收付金額", 2, 0);
        AddEditor(panel, "未收付金額", "未收付金額", 2, 1);
        AddEditor(panel, "應收付金額", "應收付金額", 2, 2);
        AddEditor(panel, "現金收付金額", "現金收付金額", 2, 3);
        AddEditor(panel, "課稅類別", "課稅類別", 3, 0);
        AddEditor(panel, "售價稅別", "售價稅別", 3, 1);
        AddEditor(panel, "發票聯式", "發票聯式", 3, 2);
        AddEditor(panel, "發票日期", "發票日期", 3, 3, isDate: true);
        AddEditor(panel, "發票號碼", "發票號碼", 4, 0);
        AddEditor(panel, "發票金額", "發票金額", 4, 1);
        AddEditor(panel, "開立方式", "開立方式", 4, 2);
        AddEditor(panel, "帳款日期", "帳款日期", 4, 3, isDate: true);
        AddEditor(panel, "數量", "數量", 5, 0);
        AddEditor(panel, "單價", "單價", 5, 1);
        return page;
    }

    private TabPage BuildAllFieldsTab()
    {
        var page = new TabPage("全部欄位");
        var label = new Label
        {
            Text = "※ 下列為資料表完整欄位（含彈性欄位 C01-C10 / N01-N10 / D01-D05 / L01-L05），可直接編輯值後儲存。",
            Dock = DockStyle.Top, Padding = new Padding(UiTheme.SpacingSm, UiTheme.SpacingSm, UiTheme.SpacingSm, UiTheme.SpacingXs), AutoSize = true,
        };
        _gridAll = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            RowHeadersVisible = false,
        };
        UiTheme.StyleDataGridView(_gridAll);
        _gridAll.CellEndEdit += (s, e) =>
        {
            if (_currentRow is null || e.RowIndex < 0 || e.RowIndex >= _gridAll.Rows.Count)
                return;
            var row = _gridAll.Rows[e.RowIndex];
            var fieldName = row.Cells["欄位名稱"].Value as string ?? "";
            var newVal = row.Cells["欄位值"].Value as string ?? "";
            if (_currentRow.Table.Columns.Contains(fieldName))
                _currentRow[fieldName] = string.IsNullOrEmpty(newVal) ? DBNull.Value : newVal;
        };
        page.Controls.Add(_gridAll);
        page.Controls.Add(label);
        return page;
    }

    // ==================== 編輯控制項 ====================

    private void AddEditor(TableLayoutPanel panel, string label, string field, int row, int col,
        bool readOnly = false, bool isDate = false, bool isTime = false, int spanCol = 1,
        string[]? comboItems = null, bool multiline = false)
    {
        var (lbl, ctrl) = CreatePair(panel, label, MakeEditor(field, readOnly, isDate, isTime, comboItems, multiline));
        panel.Controls.Add(lbl, col * 2, row);
        panel.Controls.Add(ctrl, col * 2 + 1, row);
        if (spanCol > 1)
            panel.SetColumnSpan(ctrl, spanCol * 2 - 1);
        _editors[field] = ctrl;
    }

    private (Label, Control) CreatePair(TableLayoutPanel panel, string label, Control ctrl)
    {
        var lbl = new Label { Text = label + "：", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, 0) };
        UiTheme.StyleLabel(lbl);
        ctrl.Dock = DockStyle.Fill;
        ctrl.Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingLg, UiTheme.SpacingXs);
        ctrl.Tag = label;
        return (lbl, ctrl);
    }

    private Control MakeEditor(string field, bool readOnly, bool isDate, bool isTime, string[]? comboItems, bool multiline)
    {
        Control ctrl;
        if (isDate)
        {
            var dtp = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
            UiTheme.StyleDateTimePicker(dtp);
            dtp.ValueChanged += (s, e) => { if (_currentRow is not null && dtp.Checked) _currentRow[field] = dtp.Value.ToString("yyyy-MM-dd HH:mm:ss"); };
            ctrl = dtp;
        }
        else if (isTime || comboItems is not null)
        {
            var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            UiTheme.StyleComboBox(cmb);
            if (isTime)
                cmb.Items.AddRange(TimeSlots);
            else if (comboItems is not null)
                cmb.Items.AddRange(comboItems);
            ctrl = cmb;
        }
        else
        {
            var tb = new TextBox();
            UiTheme.StyleTextBox(tb);
            if (multiline)
            {
                tb.Multiline = true;
                tb.ScrollBars = ScrollBars.Vertical;
            }
            ctrl = tb;
        }
        ctrl.Enabled = !readOnly;
        return ctrl;
    }

    // ==================== 資料操作 ====================

    private void LoadList()
    {
        var where = new List<string>();
        var pars = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(_txtNo.Text))
        {
            where.Add("[交易單號] LIKE $no");
            pars.Add(DbManager.Param("$no", "%" + _txtNo.Text.Trim() + "%"));
        }
        if (!string.IsNullOrWhiteSpace(_txtCustomer.Text))
        {
            where.Add("(r.[交易對象] LIKE $cust OR COALESCE(c.[公司簡稱], r.[交易對象]) LIKE $cust)");
            pars.Add(DbManager.Param("$cust", "%" + _txtCustomer.Text.Trim() + "%"));
        }
        if (!string.IsNullOrWhiteSpace(_txtGoods.Text))
        {
            where.Add("[品名] LIKE $goods");
            pars.Add(DbManager.Param("$goods", "%" + _txtGoods.Text.Trim() + "%"));
        }
        if (_cmbStatusFilter.SelectedIndex > 0)
        {
            where.Add("[目前狀態] = $st");
            pars.Add(DbManager.Param("$st", _cmbStatusFilter.SelectedItem!.ToString()));
        }
        if (_dtFrom.Checked)
        {
            where.Add("[叫修日期] >= $from");
            pars.Add(DbManager.Param("$from", _dtFrom.Value.ToString("yyyy-MM-dd 00:00:00")));
        }
        if (_dtTo.Checked)
        {
            where.Add("[叫修日期] <= $to");
            pars.Add(DbManager.Param("$to", _dtTo.Value.ToString("yyyy-MM-dd 23:59:59")));
        }

        var sql = $"""
            SELECT r.[交易單號], r.[交易日期], r.[目前狀態], r.[維修類別], r.[交易對象],
                   COALESCE(CAST(COALESCE(c.[公司簡稱], r.[交易對象]) AS TEXT), '') AS [客戶名稱],
                   r.[品名], r.[總計金額], r.[叫修日期], r.[交貨日期], r.[保固日期]
            FROM [維修主檔] r
            LEFT JOIN [客戶廠商] c ON r.[交易對象] = c.[客廠編號]
            """;
        if (where.Count > 0)
            sql += " WHERE " + string.Join(" AND ", where);
        sql += " ORDER BY r.[交易單號] DESC";

        _listDt = DbManager.QueryTable(sql, pars.ToArray());
        _grid.DataSource = _listDt;
        _grid.Columns["交易單號"].HeaderText = "單號";
        _grid.Columns["交易日期"].HeaderText = "交易日期";
        _grid.Columns["目前狀態"].HeaderText = "狀態";
        _grid.Columns["維修類別"].HeaderText = "類別";
        _grid.Columns["交易對象"].HeaderText = "對象編號";
        _grid.Columns["客戶名稱"].HeaderText = "客戶名稱";
        _grid.Columns["品名"].HeaderText = "品名";
        _grid.Columns["總計金額"].HeaderText = "總計金額";
        _grid.Columns["叫修日期"].HeaderText = "叫修日期";
        _grid.Columns["交貨日期"].HeaderText = "交貨日期";
        _grid.Columns["保固日期"].HeaderText = "保固日期";
        if (_listDt.Rows.Count > 0)
            _grid.Rows[0].Selected = true;
    }

    private void OnRowSelected()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].IsNewRow)
            return;
        _currentIndex = _grid.SelectedRows[0].Index;
        UpdateNav();
        var billNo = _grid.SelectedRows[0].Cells["交易單號"].Value as string ?? "";
        LoadBill(billNo);
    }

    private void SelectRow(int index)
    {
        if (_grid.Rows.Count == 0)
            return;
        if (index < 0) index = 0;
        if (index >= _grid.Rows.Count) index = _grid.Rows.Count - 1;
        _grid.Rows[index].Selected = true;
        _grid.CurrentCell = _grid.Rows[index].Cells[0];
    }

    private void UpdateNav()
    {
        _lblRecord.Text = $"記錄: {_currentIndex + 1} / {_grid.Rows.Count}";
        _lblStatus.Text = "狀態: 檢視";
    }

    private void LoadBill(string billNo)
    {
        var dt = DbManager.QueryTable(
            $"SELECT * FROM \"{TableName}\" WHERE [交易單號] = $no",
            DbManager.Param("$no", billNo));
        if (dt.Rows.Count == 0)
            return;
        _currentRow = dt.Rows[0];
        _currentKey = billNo;

        // 填寫編輯控制項
        var cols = _currentRow.Table.Columns;
        foreach (var (field, ctrl) in _editors)
        {
            if (!cols.Contains(field)) continue;
            var val = _currentRow[field];
            switch (ctrl)
            {
                case DateTimePicker dtp:
                    if (val is DBNull or null)
                        dtp.Checked = false;
                    else if (DateTime.TryParse(val.ToString(), out var d))
                    {
                        dtp.Value = d;
                        dtp.Checked = true;
                    }
                    break;
                case ComboBox cmb:
                    cmb.Text = val is DBNull or null ? "" : val.ToString();
                    break;
                case TextBox tb:
                    tb.Text = val is DBNull or null ? "" : val.ToString();
                    break;
            }
        }
        _txtFault.Text = Str(_currentRow["故障現象"]);
        _txtCause.Text = Str(_currentRow["故障原因"]);
        _txtSituation.Text = Str(_currentRow["維修情況"]);
        _txtRemark.Text = Str(_currentRow["備註"]);

        // 客戶/貨品名稱
        _txtCustomerName.Text = DbManager.QueryScalar(
            "SELECT [公司簡稱] FROM [客戶廠商] WHERE [客廠編號] = $id",
            DbManager.Param("$id", Str(_currentRow["交易對象"]))) as string ?? "";
        _txtGoodsName.Text = DbManager.QueryScalar(
            "SELECT [品名] FROM [貨品主檔] WHERE [貨品編號] = $id",
            DbManager.Param("$id", Str(_currentRow["貨品編號"]))) as string ?? "";

        // 全部欄位頁
        var allDt = new DataTable();
        allDt.Columns.Add("欄位名稱", typeof(string));
        allDt.Columns.Add("欄位值", typeof(string));
        foreach (DataColumn col in cols)
        {
            var v = _currentRow[col];
            allDt.Rows.Add(col.ColumnName, v is DBNull or null ? "" : v.ToString());
        }
        _gridAll.DataSource = allDt;
        _gridAll.Columns["欄位名稱"].ReadOnly = true;
        _gridAll.Columns["欄位名稱"].Width = 220;
        _gridAll.Columns["欄位值"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void NewBill()
    {
        var billNo = GenerateBillNo();
        try
        {
            var empty = DbManager.QueryTable(
                $"SELECT * FROM \"{TableName}\" WHERE 1=0");
            var row = empty.NewRow();
            row["交易單號"] = billNo;
            row["單據副碼"] = Convert.ToInt64(DbManager.QueryScalar(
                $"SELECT COALESCE(MAX([單據副碼]), 0) FROM \"{TableName}\"") ?? 0L) + 1;
            row["交易日期"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            row["叫修日期"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            row["目前狀態"] = "未處理";
            row["維修類別"] = "保固外維修";
            row["明細筆數"] = 0;
            empty.Rows.Add(row);

            var cols = row.Table.Columns;
            var colNames = new List<string>();
            var pars = new List<SqliteParameter>();
            foreach (DataColumn col in cols)
            {
                colNames.Add(col.ColumnName);
                pars.Add(DbManager.Param($"${col.ColumnName}", row[col] is DBNull ? null : row[col]));
            }
            DbManager.ExecuteNonQuery(
                $"INSERT INTO \"{TableName}\" (\"{string.Join("\",\"", colNames)}\") VALUES ({string.Join(",", colNames.Select(c => $"${c}"))})",
                pars.ToArray());

            _currentRow = row;
            _currentKey = billNo;

            foreach (var (field, ctrl) in _editors)
            {
                if (!row.Table.Columns.Contains(field)) continue;
                switch (ctrl)
                {
                    case DateTimePicker dtp: dtp.Checked = false; break;
                    case ComboBox cmb: cmb.Text = ""; break;
                    case TextBox tb: tb.Text = ""; break;
                }
            }
            _editors["交易單號"].Text = billNo;
            _editors["目前狀態"].Text = "未處理";
            _editors["維修類別"].Text = "保固外維修";
            _txtFault.Clear(); _txtCause.Clear(); _txtSituation.Clear(); _txtRemark.Clear();
            _txtCustomerName.Clear(); _txtGoodsName.Clear();

            var allDt = new DataTable();
            allDt.Columns.Add("欄位名稱", typeof(string));
            allDt.Columns.Add("欄位值", typeof(string));
            foreach (DataColumn col in row.Table.Columns)
                allDt.Rows.Add(col.ColumnName, row[col] is DBNull or null ? "" : row[col].ToString());
            _gridAll.DataSource = allDt;
            _gridAll.Columns["欄位名稱"].ReadOnly = true;
            _gridAll.Columns["欄位名稱"].Width = 220;
            _gridAll.Columns["欄位值"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            MessageBox.Show($"已建立新維修單：{billNo}", "新增", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadList();
            _lblStatus.Text = "狀態: 新增中";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"新增失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string GenerateBillNo()
    {
        var today = DateTime.Now;
        var prefix = today.ToString("yyMMdd");
        var max = DbManager.QueryScalar(
            $"SELECT MAX([交易單號]) FROM \"{TableName}\" WHERE [交易單號] LIKE $p",
            DbManager.Param("$p", prefix + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= prefix.Length + 4 &&
            int.TryParse(max.Substring(prefix.Length, 4), out var last))
            seq = last + 1;
        return prefix + seq.ToString("0000");
    }

    private void SaveBill()
    {
        if (_currentRow is null)
        {
            MessageBox.Show("請先選取或新增一筆維修單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            // 從編輯控制項收集值
            var cols = _currentRow.Table.Columns;
            foreach (var (field, ctrl) in _editors)
            {
                if (!cols.Contains(field)) continue;
                _currentRow[field] = ReadEditor(field, ctrl);
            }
            _currentRow["故障現象"] = _txtFault.Text;
            _currentRow["故障原因"] = _txtCause.Text;
            _currentRow["維修情況"] = _txtSituation.Text;
            _currentRow["備註"] = _txtRemark.Text;

            var colNames = new List<string>();
            var pars = new List<SqliteParameter>();
            foreach (DataColumn col in cols)
            {
                colNames.Add(col.ColumnName);
                pars.Add(DbManager.Param($"${col.ColumnName}",
                    _currentRow[col] is DBNull ? null : _currentRow[col]));
            }
            var sets = string.Join(",", colNames.Where(c => c != "交易單號" && c != "單據副碼").Select(c => $"\"{c}\" = ${c}"));
            DbManager.ExecuteNonQuery(
                $"UPDATE \"{TableName}\" SET {sets} WHERE [交易單號] = $交易單號",
                pars.ToArray());

            MessageBox.Show("儲存成功。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadList();
            _lblStatus.Text = "狀態: 已儲存";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private object ReadEditor(string field, Control ctrl)
    {
        switch (ctrl)
        {
            case DateTimePicker dtp:
                if (!dtp.Checked) return DBNull.Value;
                return dtp.Value.ToString("yyyy-MM-dd HH:mm:ss");
            case ComboBox cmb:
                return string.IsNullOrWhiteSpace(cmb.Text) ? DBNull.Value : cmb.Text;
            case TextBox tb:
                return string.IsNullOrWhiteSpace(tb.Text) ? DBNull.Value : tb.Text;
            default:
                return DBNull.Value;
        }
    }

    private void PrintBill()
    {
        if (_currentRow is null || string.IsNullOrEmpty(_currentKey))
        {
            MessageBox.Show("請先選取一筆維修單再列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 1. 載入並解析報表範本（維修單據.rtm，TPF0 格式）
        string rtmPath = Path.Combine(ReportDir, "維修單據.rtm");
        if (!File.Exists(rtmPath))
        {
            MessageBox.Show($"找不到報表檔：{rtmPath}", "列印", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Tpf0Object root;
        try
        {
            root = Tpf0Reader.Parse(File.ReadAllBytes(rtmPath));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"報表檔解析失敗：{ex.Message}", "列印", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var report = RtmLoader.Load(root);
        var data = BuildRtmData();

        // 2. 依 .rtm 版面渲染（RtmRenderer 取代原本手繪）
        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument
        {
            DocumentName = $"維修單據-{_currentKey}",
        };
        doc.DefaultPageSettings.PaperSize = new PaperSize("A4",
            (int)Math.Round(report.MmPaperWidth / 25.4 * 100),
            (int)Math.Round(report.MmPaperHeight / 25.4 * 100));
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);   // 邊界已含在 .rtm 座標內
        doc.PrintPage += (s, e) =>
        {
            try
            {
                e.HasMorePages = renderer.RenderPage(e.Graphics!, e.PageBounds, state);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"列印發生錯誤：{ex.Message}", "列印", MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.HasMorePages = false;
            }
        };

        using var dlg = new PrintPreviewDialog
        {
            Document = doc,
            Width = 960,
            Height = 720,
            StartPosition = FormStartPosition.CenterScreen,
        };
        dlg.ShowDialog(this);
    }

    /// <summary>建立報表資料：主檔（ppDBPipeline1）+ 公司（plCompany）+ 明細（ppDBPipeline2）。</summary>
    private RtmData BuildRtmData()
    {
        var row = _currentRow!;

        var master = new Dictionary<string, object?>();
        foreach (DataColumn col in row.Table.Columns)
            master[col.ColumnName] = row[col];
        // join 欄位：舊系統報表使用「對象名稱」「員工名稱」，主檔僅存編號
        master["對象名稱"] = string.IsNullOrWhiteSpace(_txtCustomerName.Text)
            ? LookupCustomerName(row["交易對象"])
            : _txtCustomerName.Text;
        master["員工名稱"] = LookupStaffName(row["員工編號"]);

        var data = new RtmData { Master = master };

        // 公司基本資料（plCompany）
        var company = new CompanyInfo();
        data.Company["公司全名"] = company.CompanyName;
        data.Company["電話號碼"] = company.Phone;
        data.Company["登記地址"] = company.Address;
        data.Company["傳真號碼"] = LookupCompanyFax(company.CompanyName);

        // 明細（維修明細，關聯鍵 = 單據副碼）
        var dt = DbManager.QueryTable(
            "SELECT * FROM \"維修明細\" WHERE \"單據副碼\" = $code",
            DbManager.Param("$code", row["單據副碼"]));
        foreach (DataRow dr in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns)
                d[col.ColumnName] = dr[col];
            data.Detail.Add(d);
        }
        return data;
    }

    private static string LookupCustomerName(object? customerNo)
    {
        if (customerNo is null or DBNull) return "";
        var v = DbManager.QueryScalar(
            "SELECT \"公司全名\" FROM \"客戶廠商\" WHERE \"客廠編號\" = $no LIMIT 1",
            DbManager.Param("$no", customerNo));
        return v?.ToString() ?? "";
    }

    private static string LookupStaffName(object? staffNo)
    {
        if (staffNo is null or DBNull) return "";
        var v = DbManager.QueryScalar(
            "SELECT \"員工姓名\" FROM \"員工資料\" WHERE \"員工編號\" = $no LIMIT 1",
            DbManager.Param("$no", staffNo));
        return v?.ToString() ?? "";
    }

    private static string LookupCompanyFax(string companyName)
    {
        var v = DbManager.QueryScalar(
            "SELECT \"傳真號碼\" FROM \"客戶廠商\" WHERE \"公司全名\" = $name" +
            " AND \"傳真號碼\" IS NOT NULL AND \"傳真號碼\" != '' LIMIT 1",
            DbManager.Param("$name", companyName));
        return v?.ToString() ?? "";
    }

    private void DeleteBill()
    {
        if (_currentRow is null)
        {
            MessageBox.Show("請先選取一筆維修單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show($"確定要刪除維修單「{_currentKey}」嗎？此動作無法復原。",
                "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try
        {
            DbManager.ExecuteNonQuery(
                $"DELETE FROM \"{TableName}\" WHERE [交易單號] = $no",
                DbManager.Param("$no", _currentKey));
            MessageBox.Show("已刪除。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _currentRow = null;
            LoadList();
            _lblStatus.Text = "狀態: 已刪除";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刪除失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ChangeStatus(string status)
    {
        if (_currentRow is null)
        {
            MessageBox.Show("請先選取一筆維修單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            DbManager.ExecuteNonQuery(
                $"UPDATE \"{TableName}\" SET [目前狀態] = $st WHERE [交易單號] = $no",
                DbManager.Param("$st", status), DbManager.Param("$no", _currentKey));
            if (_editors.TryGetValue("目前狀態", out var ctrl))
                ctrl.Text = status;
            MessageBox.Show($"狀態已變更為「{status}」。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadList();
            _lblStatus.Text = $"狀態: 已變更為 {status}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"狀態變更失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string Str(object v) => v is DBNull or null ? "" : v.ToString() ?? "";
}
