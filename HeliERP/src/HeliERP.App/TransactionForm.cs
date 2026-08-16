// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Printing;
using HeliERP.Data;
using HeliERP.Models;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 貿易系統交易作業：出貨 / 出貨退回 / 進貨 / 進貨退出 四類單據之查詢、新增、修改、刪除。
/// 資料流由 TradeService 統一處理（主檔 → 明細 → 庫存 → 帳款 → 異動快照，單一交易）。
/// 單據類別切換即切換作業（客戶/廠商下拉、稅率、列表隨之更新）。
/// </summary>
public class TransactionForm : Form
{
    private readonly AppUser? _user;

    // 目前作業類別
    private TradeService.TradeKind _kind = TradeService.GetKind("出貨");
    private decimal _taxRate = 5m;
    private bool _taxExempt;
    private string _defaultWarehouse = "A";

    // 列表
    private DataTable _listDt = new();
    private DataGridView _grid = null!;
    private ComboBox _cmbKind = null!;
    private TextBox _txtNo = null!, _txtCustomer = null!;
    private DateTimePicker _dtFrom = null!, _dtTo = null!;

    // 主檔編輯
    private TextBox _txtBillNo = null!, _txtInvoice = null!, _txtRemark = null!;
    private ComboBox _cmbCustomer = null!, _cmbWarehouse = null!, _cmbStaff = null!;
    private DateTimePicker _dtDate = null!, _dtDue = null!;
    private Label _lblCustomerName = null!, _lblStaffName = null!;
    private Label _lblTaxType = null!, _lblPriceTax = null!;
    private Label _lblSubtotal = null!, _lblTax = null!, _lblTotal = null!;

    // 明細
    private DataGridView _gridDetail = null!;

    // 導覽 + 狀態
    private ModernButton _btnFirst = null!, _btnPrev = null!, _btnNext = null!, _btnLast = null!;
    private CheckBox _chkDiscount = null!;
    private Label _lblRecord = null!, _lblStatus = null!;
    private int _currentIndex = -1;

    // 編輯狀態
    private long _currentKey;
    private bool _editing;
    private bool _loading;

    public TransactionForm(AppUser? user = null)
    {
        _user = user;
        Text = "貿易系統 - 交易作業";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 660);
        UiTheme.Apply(this);

        Controls.Add(UiTheme.BuildHeader("貿易系統", "出貨 / 出貨退回 / 進貨 / 進貨退出 單據作業"));

        BuildToolbar();
        BuildSearchPanel();
        BuildListGrid();
        BuildStatusBar();
        BuildMasterPanel();
        BuildDetailPanel();

        var p = TradeService.LoadParams();
        _taxRate = _kind.TaxSource == "進項" ? p.進項稅率 : p.銷項稅率;
        _defaultWarehouse = p.常用倉庫;

        ReloadCustomerCombo();
        LoadList();

        ShortcutHelper.Enable(this, NewBill, EditBill, DeleteBill, LoadList);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    // ==================== UI 建立 ====================

    private void BuildToolbar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.PrimaryDark };
        bar.Paint += (s, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                bar.ClientRectangle, UiTheme.Primary, UiTheme.PrimaryDark, System.Drawing.Drawing2D.LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(brush, bar.ClientRectangle);
        };

        int x = UiTheme.SpacingMd;
        void Add(ModernButton b)
        {
            b.Location = new Point(x, 6);
            b.Height = 40;
            b.DrawShadow = false;
            bar.Controls.Add(b);
            x += b.Width + UiTheme.SpacingSm;
        }

        var btnSearch = new ModernButton { Text = "搜尋", Width = 80, IsPrimary = false };
        btnSearch.Click += (s, e) => { LoadList(); _txtNo.Focus(); _txtNo.SelectAll(); };
        var btnReload = new ModernButton { Text = "重讀", Width = 80, IsPrimary = false };
        btnReload.Click += (s, e) => LoadList();
        var btnNew = new ModernButton { Text = "新增", Width = 80, IsPrimary = false };
        btnNew.Click += (s, e) => NewBill();
        var btnSave = new ModernButton { Text = "儲存", Width = 80, IsPrimary = true };
        btnSave.Click += (s, e) => SaveBill();
        var btnEdit = new ModernButton { Text = "修改", Width = 80, IsPrimary = false };
        btnEdit.Click += (s, e) => EditBill();
        var btnDel = new ModernButton { Text = "刪除", Width = 80, IsPrimary = false };
        btnDel.Click += (s, e) => DeleteBill();
        var btnPrint = new ModernButton { Text = "列印", Width = 80, IsPrimary = false };
        btnPrint.Click += (s, e) => PrintBill();
        var btnRevert = new ModernButton { Text = "復原", Width = 80, IsPrimary = false };
        btnRevert.Click += (s, e) => RevertBill();
        var btnExit = new ModernButton { Text = "離開", Width = 80, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSearch); Add(btnReload); Add(btnNew); Add(btnSave); Add(btnEdit);
        Add(btnDel); Add(btnPrint); Add(btnRevert);

        _chkDiscount = new CheckBox
        {
            Text = "含折扣",
            AutoSize = true,
            ForeColor = Color.White,
            Location = new Point(x, 14),
        };
        bar.Controls.Add(_chkDiscount);

        Add(btnExit);

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

        var lblKind = new Label { Text = "單據類別：", Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblKind, sub: true);
        panel.Controls.Add(lblKind);
        _cmbKind = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbKind);
        foreach (var k in TradeService.Kinds)
            _cmbKind.Items.Add(k.Display);
        _cmbKind.SelectedIndex = 0;
        _cmbKind.SelectedIndexChanged += (s, e) => SwitchKind(TradeService.Kinds[_cmbKind.SelectedIndex]);
        panel.Controls.Add(_cmbKind);

        var lblNo = new Label { Text = "單號：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblNo, sub: true);
        panel.Controls.Add(lblNo);
        _txtNo = new TextBox { Width = 120 };
        UiTheme.StyleTextBox(_txtNo);
        panel.Controls.Add(_txtNo);

        var lblCust = new Label { Text = "對象：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblCust, sub: true);
        panel.Controls.Add(lblCust);
        _txtCustomer = new TextBox { Width = 140 };
        UiTheme.StyleTextBox(_txtCustomer);
        panel.Controls.Add(_txtCustomer);

        var lblFrom = new Label { Text = "交易日期：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
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
            _txtNo.Clear(); _txtCustomer.Clear();
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
        _grid.RowTemplate.Height = 32;
        _grid.SelectionChanged += (s, e) => OnRowSelected();
        // 儲存格內容超出欄寬時，滑鼠停懸顯示完整文字
        _grid.CellToolTipTextNeeded += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (cell.Value is null || cell.Value == DBNull.Value)
                return;
            var text = Convert.ToString(cell.Value);
            if (string.IsNullOrEmpty(text))
                return;
            var cw = _grid.Columns[e.ColumnIndex] is { } col && col.Visible ? col.Width : 0;
            if (cw == 0 || TextRenderer.MeasureText(text, _grid.Font).Width > cw - 8)
                e.ToolTipText = text;
        };
        Controls.Add(_grid);
    }

    private void BuildStatusBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = UiTheme.BorderLight };

        _lblStatus = new Label
        {
            Text = "狀態: 檢視",
            AutoSize = true,
            Location = new Point(12, 8),
            ForeColor = UiTheme.TextSub,
            Font = UiTheme.Font(9.5F),
        };

        _lblRecord = new Label
        {
            Text = "記錄: 0/0",
            AutoSize = true,
            ForeColor = UiTheme.Primary,
            Font = UiTheme.Font(10.5F, FontStyle.Bold),
        };

        _btnFirst = new ModernButton { Text = "首筆", Width = 56, Height = 26, IsPrimary = false };
        _btnPrev = new ModernButton { Text = "上筆", Width = 56, Height = 26, IsPrimary = false };
        _btnNext = new ModernButton { Text = "下筆", Width = 56, Height = 26, IsPrimary = false };
        _btnLast = new ModernButton { Text = "尾筆", Width = 56, Height = 26, IsPrimary = false };
        _btnFirst.Click += (s, e) => SelectRow(0);
        _btnPrev.Click += (s, e) => SelectRow(_currentIndex - 1);
        _btnNext.Click += (s, e) => SelectRow(_currentIndex + 1);
        _btnLast.Click += (s, e) => SelectRow(_grid.Rows.Count - 1);
        foreach (var b in new[] { _btnFirst, _btnPrev, _btnNext, _btnLast })
        {
            b.DrawShadow = false;
            bar.Controls.Add(b);
        }

        bar.Controls.Add(_lblStatus);
        bar.Controls.Add(_lblRecord);
        bar.Resize += (s, e) =>
        {
            int right = bar.ClientSize.Width - 8;
            foreach (var b in new[] { _btnLast, _btnNext, _btnPrev, _btnFirst })
            {
                b.Location = new Point(right - b.Width, 3);
                right -= b.Width + 6;
            }
            _lblRecord.Location = new Point(right - _lblRecord.Width, 8);
        };
        Controls.Add(bar);
    }

    private void BuildMasterPanel()
    {
        var card = new Panel { Dock = DockStyle.Top, Height = 258, BackColor = UiTheme.Background, Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingSm, UiTheme.SpacingLg, UiTheme.SpacingSm) };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 6,
        };
        for (int i = 0; i < 6; i++)
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
        for (int i = 0; i < 6; i++)
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 5 ? 88 : 34));

        _txtBillNo = new TextBox();
        UiTheme.StyleTextBox(_txtBillNo, readOnly: true);
        AddPair(panel, "單號", _txtBillNo, 0, 0);

        _cmbCustomer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbCustomer);
        _cmbCustomer.SelectedIndexChanged += (s, e) => OnCustomerChanged();
        AddPair(panel, "對象", _cmbCustomer, 0, 1);

        _dtDate = new DateTimePicker { Format = DateTimePickerFormat.Short };
        UiTheme.StyleDateTimePicker(_dtDate);
        _dtDate.ValueChanged += (s, e) => RecalcSummary();
        AddPair(panel, "交易日期", _dtDate, 0, 2);

        _lblCustomerName = new Label();
        UiTheme.StyleLabel(_lblCustomerName, sub: true);
        AddPair(panel, "對象名稱", _lblCustomerName, 1, 0, spanCol: 2);

        _cmbWarehouse = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbWarehouse);
        _cmbWarehouse.DataSource = TradeService.LoadWarehouseCombo();
        _cmbWarehouse.DisplayMember = "倉庫編號";
        _cmbWarehouse.ValueMember = "倉庫編號";
        AddPair(panel, "倉庫", _cmbWarehouse, 1, 1);

        _cmbStaff = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbStaff);
        _cmbStaff.SelectedIndexChanged += (s, e) => OnStaffChanged();
        AddPair(panel, "員工", _cmbStaff, 1, 2);

        _lblTaxType = new Label();
        UiTheme.StyleLabel(_lblTaxType, sub: true);
        AddPair(panel, "課稅別", _lblTaxType, 2, 0);

        _lblPriceTax = new Label();
        UiTheme.StyleLabel(_lblPriceTax, sub: true);
        AddPair(panel, "售價稅別", _lblPriceTax, 2, 1);

        _lblStaffName = new Label();
        UiTheme.StyleLabel(_lblStaffName, sub: true);
        AddPair(panel, "員工姓名", _lblStaffName, 2, 2);

        _dtDue = new DateTimePicker { Format = DateTimePickerFormat.Short };
        UiTheme.StyleDateTimePicker(_dtDue);
        AddPair(panel, "帳款日期", _dtDue, 3, 0);

        _txtInvoice = new TextBox();
        UiTheme.StyleTextBox(_txtInvoice);
        AddPair(panel, "發票號碼", _txtInvoice, 3, 1);

        var lblSum = new Label { Text = "合計", Font = UiTheme.Font(10F, FontStyle.Bold), ForeColor = UiTheme.TextSub, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        _lblSubtotal = new Label { Text = "0", Font = UiTheme.Font(13F, FontStyle.Bold), ForeColor = UiTheme.PrimaryDark, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        var sumBox = new Panel { Dock = DockStyle.Fill };
        sumBox.Controls.Add(_lblSubtotal);
        sumBox.Controls.Add(lblSum);
        panel.Controls.Add(lblSum, 4, 3);
        panel.Controls.Add(_lblSubtotal, 5, 3);

        var lblTaxSum = new Label { Text = "營業稅", Font = UiTheme.Font(10F, FontStyle.Bold), ForeColor = UiTheme.TextSub, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        _lblTax = new Label { Text = "0", Font = UiTheme.Font(13F, FontStyle.Bold), ForeColor = UiTheme.PrimaryDark, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        panel.Controls.Add(lblTaxSum, 4, 4);
        panel.Controls.Add(_lblTax, 5, 4);

        var lblTotalSum = new Label { Text = "總計", Font = UiTheme.Font(10F, FontStyle.Bold), ForeColor = UiTheme.TextSub, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        _lblTotal = new Label { Text = "0", Font = UiTheme.Font(14F, FontStyle.Bold), ForeColor = UiTheme.AccentDark, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        panel.Controls.Add(lblTotalSum, 4, 5);
        panel.Controls.Add(_lblTotal, 5, 5);

        _txtRemark = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
        UiTheme.StyleTextBox(_txtRemark);
        var lblRemark = new Label { Text = "備註：", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, 0) };
        UiTheme.StyleLabel(lblRemark);
        _txtRemark.Dock = DockStyle.Fill;
        _txtRemark.Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingLg, UiTheme.SpacingXs);
        panel.Controls.Add(lblRemark, 0, 5);
        panel.Controls.Add(_txtRemark, 1, 5);
        panel.SetColumnSpan(_txtRemark, 3);

        card.Controls.Add(panel);
        Controls.Add(card);
    }

    private void AddPair(TableLayoutPanel panel, string label, Control ctrl, int row, int col, int spanCol = 1)
    {
        var lbl = new Label { Text = label + "：", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, 0) };
        UiTheme.StyleLabel(lbl);
        ctrl.Dock = DockStyle.Fill;
        ctrl.Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingLg, UiTheme.SpacingXs);
        panel.Controls.Add(lbl, col * 2, row);
        panel.Controls.Add(ctrl, col * 2 + 1, row);
        if (spanCol > 1)
            panel.SetColumnSpan(ctrl, spanCol * 2);
    }

    private void BuildDetailPanel()
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background, Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingXs, UiTheme.SpacingLg, UiTheme.SpacingSm) };

        var bar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = UiTheme.Card };
        var lbl = new Label
        {
            Text = "明細",
            AutoSize = true,
            Location = new Point(12, 10),
            ForeColor = UiTheme.Primary,
            Font = UiTheme.Font(11F, FontStyle.Bold),
        };
        bar.Controls.Add(lbl);
        var btnAdd = new ModernButton { Text = "新增明細列", Width = 110, Height = 30, IsPrimary = false };
        btnAdd.Location = new Point(80, 5);
        btnAdd.Click += (s, e) => AddDetailRow();
        bar.Controls.Add(btnAdd);
        var btnRemove = new ModernButton { Text = "刪除明細列", Width = 110, Height = 30, IsPrimary = false };
        btnRemove.Location = new Point(200, 5);
        btnRemove.Click += (s, e) => RemoveDetailRow();
        bar.Controls.Add(btnRemove);
        card.Controls.Add(bar);

        _gridDetail = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
        };
        UiTheme.StyleDataGridView(_gridDetail);
        _gridDetail.RowTemplate.Height = 30;

        var goodsList = DbManager.QueryTable(
            "SELECT [貨品編號], [品名] FROM [貨品主檔] ORDER BY [貨品編號]");
        goodsList.Columns.Add("顯示", typeof(string));
        foreach (DataRow r in goodsList.Rows)
            r["顯示"] = $"{r["貨品編號"]}  {r["品名"]}";

        var colGoods = new DataGridViewComboBoxColumn
        {
            Name = "貨品編號",
            HeaderText = "貨品編號",
            Width = 170,
            DataSource = goodsList,
            DisplayMember = "顯示",
            ValueMember = "貨品編號",
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
        };
        _gridDetail.Columns.Add(colGoods);
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "品名", HeaderText = "品名", Width = 130, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "倉庫編號", HeaderText = "倉庫", Width = 54 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "調入倉庫", HeaderText = "調入倉", Width = 54 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "數量", HeaderText = "數量", Width = 62 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單位", HeaderText = "單位", Width = 44 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單價", HeaderText = "單價", Width = 70 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "成本", HeaderText = "成本", Width = 70 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "折扣", HeaderText = "折扣%", Width = 56 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "金額", HeaderText = "金額", Width = 80, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewCheckBoxColumn { Name = "贈品", HeaderText = "贈品", Width = 40 });
        _gridDetail.Columns.Add(new DataGridViewCheckBoxColumn { Name = "服務項目", HeaderText = "服務", Width = 40 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "附註說明", HeaderText = "附註說明", Width = 130 });
        _gridDetail.Columns["貨品編號"].Frozen = true;
        UiTheme.StyleHeaderBold(_gridDetail.Columns["貨品編號"]);

        // 儲存格內容超出欄寬時，滑鼠停懸顯示完整文字
        _gridDetail.CellToolTipTextNeeded += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            var cell = _gridDetail.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (cell.Value is null || cell.Value == DBNull.Value)
                return;
            var text = Convert.ToString(cell.Value);
            if (string.IsNullOrEmpty(text))
                return;
            var cw = _gridDetail.Columns[e.ColumnIndex] is { } col && col.Visible ? col.Width : 0;
            if (cw == 0 || TextRenderer.MeasureText(text, _gridDetail.Font).Width > cw - 8)
                e.ToolTipText = text;
        };

        _gridDetail.CellEndEdit += OnDetailCellEndEdit;
        _gridDetail.CellValueChanged += OnDetailCellValueChanged;
        _gridDetail.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_gridDetail.IsCurrentCellDirty && _gridDetail.CurrentCell is { ColumnIndex: var ci } &&
                _gridDetail.Columns[ci].Name == "貨品編號")
                _gridDetail.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        card.Controls.Add(_gridDetail);
        Controls.Add(card);
    }

    // ==================== 事件 ====================

    private void SwitchKind(TradeService.TradeKind kind)
    {
        _kind = kind;
        var p = TradeService.LoadParams();
        _taxRate = kind.TaxSource == "進項" ? p.進項稅率 : p.銷項稅率;
        _defaultWarehouse = p.常用倉庫;
        ReloadCustomerCombo();
        ClearEdit();
        LoadList();
        _lblStatus.Text = $"狀態: {kind.Display}";
    }

    private void ReloadCustomerCombo()
    {
        // 調撥/領料無交易對象（倉庫層級作業），停用對象下拉
        if (_kind.ObjectType is not ("客戶" or "廠商"))
        {
            _cmbCustomer.DataSource = null;
            _cmbCustomer.Enabled = false;
            return;
        }
        _cmbCustomer.Enabled = true;
        var dt = TradeService.LoadCustomerCombo(_kind.ObjectType);
        dt.Columns.Add("顯示", typeof(string));
        foreach (DataRow r in dt.Rows)
        {
            var 簡稱 = r["公司簡稱"] is DBNull ? "" : r["公司簡稱"].ToString();
            r["顯示"] = string.IsNullOrWhiteSpace(簡稱) ? r["客廠編號"].ToString() : $"{r["客廠編號"]}  {簡稱}";
        }
        _cmbCustomer.DataSource = dt;
        _cmbCustomer.DisplayMember = "顯示";
        _cmbCustomer.ValueMember = "客廠編號";
        _cmbCustomer.SelectedIndex = -1;
    }

    private void OnCustomerChanged()
    {
        if (_loading) return;
        var no = SelectedCustomer();
        _lblCustomerName.Text = "";
        _lblTaxType.Text = "";
        _lblPriceTax.Text = "";
        _taxExempt = false;
        if (string.IsNullOrEmpty(no)) return;
        var info = TradeService.LookupCustomerInfo(no, _kind.ObjectType);
        if (info is null) return;
        _lblCustomerName.Text = Str(info.TryGetValue("公司簡稱", out var s1) ? s1 : null);
        _lblTaxType.Text = Str(info.TryGetValue("課稅別", out var s2) ? s2 : null);
        _lblPriceTax.Text = Str(info.TryGetValue("售價稅別", out var s3) ? s3 : null);
        _taxExempt = _lblTaxType.Text.Contains("免");
        RecalcSummary();
    }

    private void OnStaffChanged()
    {
        if (_loading) return;
        var no = _cmbStaff.SelectedValue as string ?? "";
        _lblStaffName.Text = string.IsNullOrEmpty(no) ? "" : TradeService.LookupStaffName(no) ?? "";
    }

    private void OnDetailCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || e.RowIndex < 0 || e.RowIndex >= _gridDetail.Rows.Count) return;
        var row = _gridDetail.Rows[e.RowIndex];
        if (row.IsNewRow) return;
        if (e.ColumnIndex != _gridDetail.Columns["貨品編號"].Index) return;
        var code = (row.Cells["貨品編號"].Value as string ?? "").Trim();
        if (code.Length == 0) return;
        var g = TradeService.LookupGoodsInfo(code);
        if (g is null)
        {
            MessageBox.Show($"找不到貨品「{code}」，請確認貨品編號。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _loading = true;
        row.Cells["品名"].Value = Str(g.TryGetValue("品名", out var n) ? n : null);
        row.Cells["單位"].Value = Str(g.TryGetValue("基本單位", out var u) ? u : null);
        row.Cells["單價"].Value = PickPrice(g);
        row.Cells["成本"].Value = PickCost(g);
        if (row.Cells["倉庫編號"].Value is null or DBNull or "")
            row.Cells["倉庫編號"].Value = _defaultWarehouse;
        _loading = false;
        RecalcRowAmount(e.RowIndex);
        RecalcSummary();
    }

    private void OnDetailCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || e.RowIndex < 0 || e.RowIndex >= _gridDetail.Rows.Count) return;
        var row = _gridDetail.Rows[e.RowIndex];
        if (row.IsNewRow) return;
        var c = _gridDetail.Columns[e.ColumnIndex];
        if (c.Name is "數量" or "單價" or "折扣")
        {
            RecalcRowAmount(e.RowIndex);
            RecalcSummary();
        }
    }

    private void RecalcRowAmount(int rowIndex)
    {
        var row = _gridDetail.Rows[rowIndex];
        decimal 數量 = Dec(row.Cells["數量"].Value);
        decimal 單價 = Dec(row.Cells["單價"].Value);
        decimal 折扣 = Dec(row.Cells["折扣"].Value) == 0m ? 100m : Dec(row.Cells["折扣"].Value);
        row.Cells["金額"].Value = Math.Round(數量 * 單價 * 折扣 / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private decimal PickPrice(Dictionary<string, object?> g)
    {
        if (_kind.TaxSource == "進項")
        {
            var 現行成本 = Dec(g.TryGetValue("現行成本", out var c1) ? c1 : null);
            return 現行成本 != 0m ? 現行成本 : Dec(g.TryGetValue("標準成本", out var c2) ? c2 : null);
        }
        var 類別 = "";
        var info = TradeService.LookupCustomerInfo(SelectedCustomer(), _kind.ObjectType);
        if (info is not null && info.TryGetValue("售價類別", out var t) && t is not null)
            類別 = t.ToString() ?? "";
        return 類別 switch
        {
            "售價A" => Dec(g.TryGetValue("售價A", out var a) ? a : null),
            "最近售價" => Dec(g.TryGetValue("最近售價", out var r) ? r : null),
            _ => Dec(g.TryGetValue("標準售價", out var s) ? s : null),
        };
    }

    private decimal PickCost(Dictionary<string, object?> g)
    {
        if (_kind.TaxSource == "進項")
            return PickPrice(g);
        var 成本 = Dec(g.TryGetValue("現行平均成本", out var c) ? c : null);
        return 成本 != 0m ? 成本 : Dec(g.TryGetValue("標準成本", out var s) ? s : null);
    }

    private void AddDetailRow()
    {
        int i = _gridDetail.Rows.Add();
        var row = _gridDetail.Rows[i];
        row.Cells["倉庫編號"].Value = _defaultWarehouse;
        row.Cells["數量"].Value = 1m;
        row.Cells["折扣"].Value = 100m;
        _gridDetail.CurrentCell = row.Cells["貨品編號"];
    }

    private void RemoveDetailRow()
    {
        if (_gridDetail.SelectedCells.Count == 0) return;
        int i = _gridDetail.SelectedCells[0].RowIndex;
        if (i < 0 || i >= _gridDetail.Rows.Count) return;
        _gridDetail.Rows.RemoveAt(i);
        RecalcSummary();
    }

    private void RecalcSummary()
    {
        var req = new TradeService.SaveBillRequest { 單據類別 = _kind.Name, 明細 = CollectDetails() };
        var t = TradeService.CalcTotals(req, _taxRate, _taxExempt);
        _lblSubtotal.Text = t.合計.ToString("N2");
        _lblTax.Text = t.稅.ToString("N2");
        _lblTotal.Text = t.總計.ToString("N2");
    }

    // ==================== 資料操作 ====================

    private void LoadList()
    {
        var where = new List<string> { "r.[單據類別] = $k" };
        var pars = new List<SqliteParameter> { DbManager.Param("$k", _kind.Name) };

        if (!string.IsNullOrWhiteSpace(_txtNo.Text))
        {
            where.Add("r.[交易單號] LIKE $no");
            pars.Add(DbManager.Param("$no", "%" + _txtNo.Text.Trim() + "%"));
        }
        if (!string.IsNullOrWhiteSpace(_txtCustomer.Text))
        {
            where.Add("(r.[交易對象] LIKE $cust OR COALESCE(c.[公司簡稱], r.[交易對象]) LIKE $cust)");
            pars.Add(DbManager.Param("$cust", "%" + _txtCustomer.Text.Trim() + "%"));
        }
        if (_dtFrom.Checked)
        {
            where.Add("r.[交易日期] >= $from");
            pars.Add(DbManager.Param("$from", _dtFrom.Value.ToString("yyyy-MM-dd 00:00:00")));
        }
        if (_dtTo.Checked)
        {
            where.Add("r.[交易日期] <= $to");
            pars.Add(DbManager.Param("$to", _dtTo.Value.ToString("yyyy-MM-dd 23:59:59")));
        }

        var sql = $"""
            SELECT r.[單據副碼], r.[交易單號], r.[交易日期], r.[交易對象],
                   COALESCE(CAST(c.[公司簡稱] AS TEXT), r.[交易對象]) AS [客戶名稱],
                   r.[合計金額], r.[營業稅], r.[總計金額], r.[帳款日期],
                   r.[未收付金額], r.[明細總筆數]
            FROM [交易主檔] r
            LEFT JOIN [客戶廠商] c ON r.[交易對象] = c.[客廠編號]
            WHERE {string.Join(" AND ", where)}
            ORDER BY r.[交易單號] DESC
            """;

        _listDt = DbManager.QueryTable(sql, pars.ToArray());
        _grid.DataSource = _listDt;
        _grid.Columns["單據副碼"].Visible = false;
        _grid.Columns["交易單號"].HeaderText = "單號";
        _grid.Columns["交易日期"].HeaderText = "日期";
        _grid.Columns["交易對象"].HeaderText = "對象編號";
        _grid.Columns["客戶名稱"].HeaderText = "對象名稱";
        _grid.Columns["合計金額"].HeaderText = "合計";
        _grid.Columns["營業稅"].HeaderText = "稅額";
        _grid.Columns["總計金額"].HeaderText = "總計";
        _grid.Columns["帳款日期"].HeaderText = "帳款日";
        _grid.Columns["未收付金額"].HeaderText = "未收付";
        _grid.Columns["明細總筆數"].HeaderText = "筆數";
        if (_listDt.Rows.Count > 0)
            _grid.Rows[0].Selected = true;
        else
        {
            ClearEdit();
            _lblRecord.Text = "記錄: 0/0";
        }
    }

    private void OnRowSelected()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].IsNewRow) return;
        _currentIndex = _grid.SelectedRows[0].Index;
        _lblRecord.Text = $"記錄: {_currentIndex + 1}/{_grid.Rows.Count}";
        var 副碼 = Convert.ToInt64(_grid.SelectedRows[0].Cells["單據副碼"].Value);
        LoadBill(副碼);
    }

    private void SelectRow(int index)
    {
        if (_grid.Rows.Count == 0) return;
        if (index < 0) index = 0;
        if (index >= _grid.Rows.Count) index = _grid.Rows.Count - 1;
        _grid.Rows[index].Selected = true;
        _grid.CurrentCell = _grid.Rows[index].Cells[0];
    }

    private void LoadBill(long 副碼)
    {
        var dt = DbManager.QueryTable(
            "SELECT * FROM [交易主檔] WHERE [單據副碼] = $c",
            DbManager.Param("$c", 副碼));
        if (dt.Rows.Count == 0) return;
        var m = dt.Rows[0];
        _currentKey = 副碼;
        _editing = false;

        _loading = true;
        _txtBillNo.Text = Str(m["交易單號"]);
        _cmbCustomer.SelectedValue = Str(m["交易對象"]);
        _cmbWarehouse.SelectedValue = Str(m["倉庫編號"]);
        _cmbStaff.SelectedValue = Str(m["員工編號"]);
        TrySetDate(_dtDate, m["交易日期"]);
        TrySetDate(_dtDue, m["帳款日期"]);
        _txtInvoice.Text = Str(m["發票號碼"]);
        _txtRemark.Text = Str(m["備註"]);
        _loading = false;

        OnCustomerChanged();
        OnStaffChanged();

        var details = DbManager.QueryTable(
            "SELECT * FROM [交易明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
            DbManager.Param("$c", 副碼));
        FillDetailGrid(details);

        SetEditMode(false);
        _lblStatus.Text = $"狀態: 檢視 {_txtBillNo.Text}（可按「修改」後儲存）";
    }

    private void FillDetailGrid(DataTable details)
    {
        _loading = true;
        _gridDetail.Rows.Clear();
        foreach (DataRow dr in details.Rows)
        {
            int i = _gridDetail.Rows.Add();
            var row = _gridDetail.Rows[i];
            row.Cells["貨品編號"].Value = Str(dr["貨品編號"]);
            row.Cells["倉庫編號"].Value = Str(dr["倉庫編號"]);
            row.Cells["調入倉庫"].Value = Str(dr["調入倉庫"]);
            row.Cells["數量"].Value = Dec(dr["數量"]);
            row.Cells["單位"].Value = Str(dr["單位"]);
            row.Cells["單價"].Value = Dec(dr["單價"]);
            row.Cells["成本"].Value = Dec(dr["成本"]);
            row.Cells["折扣"].Value = Dec(dr["折扣"]);
            row.Cells["金額"].Value = Dec(dr["金額"]);
            row.Cells["附註說明"].Value = Str(dr["附註說明"]);
            row.Cells["贈品"].Value = Dec(dr["贈品"]) == 1m;
            row.Cells["服務項目"].Value = Dec(dr["服務項目"]) == 1m;
            row.Cells["品名"].Value = TradeService.LookupGoodsInfo(Str(dr["貨品編號"])) is { } g
                && g.TryGetValue("品名", out var n) ? Str(n) : "";
        }
        _loading = false;
        RecalcSummary();
    }

    private void NewBill()
    {
        _currentKey = 0;
        _editing = true;
        _loading = true;
        _txtBillNo.Text = TradeService.PreviewBillNo(_kind.Name);
        _dtDate.Value = DateTime.Now;
        _dtDue.Value = DateTime.Now;
        _cmbCustomer.SelectedIndex = -1;
        _cmbWarehouse.SelectedValue = _defaultWarehouse;
        _cmbStaff.SelectedIndex = -1;
        _txtInvoice.Clear();
        _txtRemark.Clear();
        _lblCustomerName.Text = "";
        _lblStaffName.Text = "";
        _lblTaxType.Text = "";
        _lblPriceTax.Text = "";
        _taxExempt = false;
        _gridDetail.Rows.Clear();
        _loading = false;
        RecalcSummary();
        SetEditMode(true);
        _lblStatus.Text = "狀態: 新增中";
        _cmbCustomer.Focus();
    }

    private void EditBill()
    {
        if (_currentKey == 0)
        {
            MessageBox.Show("請先選取一筆單據。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _editing = true;
        SetEditMode(true);
        _lblStatus.Text = "狀態: 修改中";
        _cmbCustomer.Focus();
    }

    private void SaveBill()
    {
        if (!_editing)
        {
            MessageBox.Show("請先按「新增」或「修改」再儲存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrEmpty(SelectedCustomer()) && _kind.Name is not ("調撥" or "領料"))
        {
            MessageBox.Show("請選擇交易對象。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var req = new TradeService.SaveBillRequest
        {
            單據類別 = _kind.Name,
            單據副碼 = _currentKey == 0 ? null : _currentKey,
            交易單號 = _currentKey == 0 ? null : _txtBillNo.Text,
            交易日期 = _dtDate.Value,
            帳款日期 = _dtDue.Value,
            交易對象 = SelectedCustomer(),
            倉庫編號 = _cmbWarehouse.SelectedValue as string ?? _defaultWarehouse,
            員工編號 = _cmbStaff.SelectedValue as string ?? "",
            發票號碼 = _txtInvoice.Text.Trim(),
            備註 = _txtRemark.Text,
            明細 = CollectDetails(),
        };
        try
        {
            var r = TradeService.SaveBill(req);
            MessageBox.Show($"儲存成功：{r.交易單號}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _editing = false;
            LoadList();
            LocateBill(r.單據副碼);
            _lblStatus.Text = "狀態: 已儲存";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RevertBill()
    {
        if (!_editing) return;
        if (_currentKey == 0)
        {
            NewBill();
            _lblStatus.Text = "狀態: 已復原（新增）";
        }
        else
        {
            LoadBill(_currentKey);
            _lblStatus.Text = "狀態: 已復原";
        }
    }

    private void DeleteBill()
    {
        if (_currentKey == 0)
        {
            MessageBox.Show("請先選取一筆單據。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show($"確定要刪除單據「{_txtBillNo.Text}」嗎？庫存與帳款將一併回復，此動作無法復原。",
                "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try
        {
            TradeService.DeleteBill(_currentKey);
            MessageBox.Show("已刪除並回復庫存/帳款。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _currentKey = 0;
            _editing = false;
            ClearEdit();
            LoadList();
            _lblStatus.Text = "狀態: 已刪除";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刪除失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearEdit()
    {
        _currentKey = 0;
        _editing = false;
        _loading = true;
        _txtBillNo.Clear();
        _cmbCustomer.SelectedIndex = -1;
        _cmbWarehouse.SelectedValue = _defaultWarehouse;
        _cmbStaff.SelectedIndex = -1;
        _txtInvoice.Clear();
        _txtRemark.Clear();
        _lblCustomerName.Text = "";
        _lblStaffName.Text = "";
        _lblTaxType.Text = "";
        _lblPriceTax.Text = "";
        _taxExempt = false;
        _gridDetail.Rows.Clear();
        _loading = false;
        RecalcSummary();
        SetEditMode(false);
    }

    private void LocateBill(long 副碼)
    {
        foreach (DataGridViewRow r in _grid.Rows)
        {
            if (Convert.ToInt64(r.Cells["單據副碼"].Value) == 副碼)
            {
                r.Selected = true;
                _grid.CurrentCell = r.Cells[0];
                return;
            }
        }
    }

    private List<TradeService.DetailRow> CollectDetails()
    {
        var list = new List<TradeService.DetailRow>();
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            if (string.IsNullOrWhiteSpace(Str(r.Cells["貨品編號"].Value))) continue;
            list.Add(new TradeService.DetailRow
            {
                貨品編號 = Str(r.Cells["貨品編號"].Value).Trim(),
                倉庫編號 = Str(r.Cells["倉庫編號"].Value),
                調入倉庫 = Str(r.Cells["調入倉庫"].Value),
                數量 = Dec(r.Cells["數量"].Value),
                單位 = Str(r.Cells["單位"].Value),
                單價 = Dec(r.Cells["單價"].Value),
                成本 = Dec(r.Cells["成本"].Value),
                折扣 = Dec(r.Cells["折扣"].Value) == 0m ? 100m : Dec(r.Cells["折扣"].Value),
                附註說明 = Str(r.Cells["附註說明"].Value),
                贈品 = r.Cells["贈品"].Value is true,
                服務項目 = r.Cells["服務項目"].Value is true,
            });
        }
        return list;
    }

    private void SetEditMode(bool editing)
    {
        _cmbCustomer.Enabled = editing;
        _cmbWarehouse.Enabled = editing;
        _cmbStaff.Enabled = editing;
        _dtDate.Enabled = editing;
        _dtDue.Enabled = editing;
        _txtInvoice.Enabled = editing;
        _txtRemark.Enabled = editing;
        _gridDetail.ReadOnly = !editing;
    }

    private string SelectedCustomer() => _cmbCustomer.SelectedValue as string ?? "";

    private static void TrySetDate(DateTimePicker picker, object? value)
    {
        if (value is DBNull or null || !DateTime.TryParse(value.ToString(), out var d))
        {
            picker.Value = DateTime.Now;
            return;
        }
        picker.Value = d;
    }

    private static decimal Dec(object? v) =>
        v is null or DBNull ? 0m : (decimal.TryParse(v.ToString(), out var d) ? d : 0m);

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    // ==================== 列印 ====================

    private static string ReportDir => ReportPrintService.RepDirectory;

    /// <summary>依目前作業類別選擇報表範本檔；勾選「含折扣」時出貨／進貨改用含折扣版。</summary>
    private string GetReportFile() => _kind.Name switch
    {
        "出貨" => _chkDiscount.Checked ? "出貨單據(含折扣).rtm" : "出貨單據.rtm",
        "出退" => "出貨退回單.rtm",
        "進貨" => _chkDiscount.Checked ? "進貨單據(含折扣).rtm" : "進貨單據.rtm",
        "進退" => "進貨退出單.rtm",
        _ => _chkDiscount.Checked ? "出貨單據(含折扣).rtm" : "出貨單據.rtm",
    };

    private void PrintBill()
    {
        if (_currentKey == 0 || _editing)
        {
            MessageBox.Show("請先載入一筆已存檔的單據再列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 1. 載入並解析報表範本（出貨/進貨等單據 .rtm，TPF0 格式）
        string rtmPath = Path.Combine(ReportDir, GetReportFile());
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
        var billNo = data.Master.TryGetValue("交易單號", out var no) ? Str(no) : _currentKey.ToString();

        // 2. 依 .rtm 版面渲染（RtmRenderer 取代原本手繪）
        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument
        {
            DocumentName = $"{_kind.Display}-{billNo}",
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
        var data = new RtmData();

        // 主檔
        var dt = DbManager.QueryTable(
            "SELECT * FROM [交易主檔] WHERE [單據副碼] = $c",
            DbManager.Param("$c", _currentKey));
        if (dt.Rows.Count == 0) return data;
        var row = dt.Rows[0];
        foreach (DataColumn col in dt.Columns)
            data.Master[col.ColumnName] = row[col];

        // join 欄位：舊系統報表使用「對象名稱」「員工名稱」「聯絡人」等，主檔僅存編號
        foreach (var (k, v) in LoadPartner(row["交易對象"]))
            data.Master[k] = v;
        data.Master["員工名稱"] = LookupStaffName(row["員工編號"]);
        // 進貨退出單報表參考「進貨地址」，交易主檔無此欄位，沿用送貨地址欄位值
        data.Master["進貨地址"] = data.Master.TryGetValue("送貨地址", out var addr) ? addr : "";

        // 公司基本資料（plCompany）
        var company = new CompanyInfo();
        data.Company["公司全名"] = company.CompanyName;
        data.Company["電話號碼"] = company.Phone;
        data.Company["登記地址"] = company.Address;
        data.Company["傳真號碼"] = LookupCompanyFax(company.CompanyName);

        // 明細（交易明細，關聯鍵 = 單據副碼）
        var detailDt = DbManager.QueryTable(
            "SELECT * FROM [交易明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
            DbManager.Param("$c", _currentKey));
        foreach (DataRow dr in detailDt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in detailDt.Columns)
                d[col.ColumnName] = dr[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>查詢交易對象（客戶/廠商）的報表 join 欄位。</summary>
    private static Dictionary<string, object?> LoadPartner(object? partnerNo)
    {
        var result = new Dictionary<string, object?>
        {
            ["對象名稱"] = "", ["聯絡人一"] = "", ["聯絡電話一"] = "", ["統一編號"] = "", ["傳真號碼"] = "",
        };
        if (partnerNo is null or DBNull) return result;
        var dt = DbManager.QueryTable(
            "SELECT \"公司全名\", \"聯絡人一\", \"聯絡電話一\", \"統一編號\", \"傳真號碼\"" +
            " FROM \"客戶廠商\" WHERE \"客廠編號\" = $no LIMIT 1",
            DbManager.Param("$no", partnerNo));
        if (dt.Rows.Count == 0) return result;
        var r = dt.Rows[0];
        result["對象名稱"] = Str(r["公司全名"]);
        result["聯絡人一"] = Str(r["聯絡人一"]);
        result["聯絡電話一"] = Str(r["聯絡電話一"]);
        result["統一編號"] = Str(r["統一編號"]);
        result["傳真號碼"] = Str(r["傳真號碼"]);
        return result;
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
}
