// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Printing;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>
/// 採購訂貨作業：報價／訂貨／採購／詢價單輸入與列印。
/// 上區編輯單據（新增），下區為既有單據清單（檢視／刪除／列印）。
/// </summary>
public sealed class PoOrderForm : Form
{
    private ComboBox _cmbKind = null!;
    private TextBox _txtNo = null!;
    private DateTimePicker _dtpDate = null!;
    private DateTimePicker _dtpDelivery = null!;
    private ComboBox _cmbTax = null!;
    private ComboBox _cmbObject = null!;
    private ComboBox _cmbDept = null!;
    private ComboBox _cmbStaff = null!;
    private TextBox _txtShipAddr = null!;
    private TextBox _txtRemark = null!;
    private DataGridView _gridDetail = null!;
    private DataGridView _gridList = null!;
    private TextBox _txtFilterNo = null!;

    private Label _lblAmount = null!;
    private Label _lblTax = null!;
    private Label _lblTotal = null!;
    private Label _lblStatus = null!;
    private Label _lblPos = null!;   // 記錄位置（i / N）

    private bool _loading;
    private bool _viewing;   // 檢視模式：清單選取後載入，禁止儲存
    private long _viewingKey;   // 目前檢視中單據的單據副碼（0 = 無）
    private readonly List<ModernButton> _navButtons = new();

    private static readonly string[] 課稅類別選項 = { "外加", "內含", "免稅" };

    public PoOrderForm()
    {
        Text = "採購訂貨作業";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        BackColor = UiTheme.Background;

        var header = UiTheme.BuildHeader("採購訂貨作業", "報價／訂貨／採購／詢價單輸入與列印");
        header.Dock = DockStyle.Top;
        Controls.Add(header);

        BuildToolbar();
        BuildMasterCard();
        BuildDetailCard();
        BuildListCard();
        BuildStatusBar();

        Load += (s, e) =>
        {
            try
            {
                _cmbKind.Items.AddRange(PoOrderService.Kinds.Select(k => k.Name).ToArray());
                _cmbKind.SelectedIndex = 0;
                _cmbTax.Items.AddRange(課稅類別選項);
                _cmbTax.SelectedIndex = 0;
                ReloadKindDependent();
                _lblStatus.Text = "狀態: 就緒";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "狀態: 載入失敗 - " + ex.Message;
            }
        };
        UiTheme.ClampToScreen(this);
    }

    // ==================== 版面建構 ====================

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
        btnSearch.Click += (s, e) => FocusListSearch();
        var btnReload = new ModernButton { Text = "重讀", Width = 80, IsPrimary = false };
        btnReload.Click += (s, e) => { ClearEditor(); LoadList(); };
        var btnNew = new ModernButton { Text = "新增", Width = 80, IsPrimary = false };
        btnNew.Click += (s, e) => ClearEditor();
        var btnSave = new ModernButton { Text = "儲存", Width = 80, IsPrimary = true };
        btnSave.Click += (s, e) => Save();
        var btnEdit = new ModernButton { Text = "修改", Width = 80, IsPrimary = false };
        btnEdit.Click += (s, e) => StartEdit();
        var btnDelete = new ModernButton { Text = "刪除", Width = 80, IsPrimary = false };
        btnDelete.Click += (s, e) => DeleteSelected();
        var btnPrint = new ModernButton { Text = "列印", Width = 80, IsPrimary = false };
        btnPrint.Click += (s, e) => PrintBill();
        var btnUndo = new ModernButton { Text = "復原", Width = 80, IsPrimary = false };
        btnUndo.Click += (s, e) => RestoreEditor();
        var btnExit = new ModernButton { Text = "離開", Width = 80, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSearch); Add(btnReload); Add(btnNew); Add(btnSave); Add(btnEdit);
        Add(btnDelete); Add(btnPrint); Add(btnUndo); Add(btnExit);

        Controls.Add(bar);
    }

    private void BuildMasterCard()
    {
        var card = new Panel { Dock = DockStyle.Top, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingSm) };
        var rows = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.Card,
        };
        for (int i = 0; i < 3; i++)
            rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        rows.Controls.Add(BuildMasterRow1(), 0, 0);
        rows.Controls.Add(BuildMasterRow2(), 0, 1);
        rows.Controls.Add(BuildMasterRow3(), 0, 2);

        card.Controls.Add(rows);
        Controls.Add(card);
    }

    private TableLayoutPanel BuildMasterRow1()
    {
        var panel = NewMasterRow();
        AddPair(panel, "單據類別", _cmbKind = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList }, 0);
        AddPair(panel, "單據號碼", _txtNo = new TextBox { Width = 110, ReadOnly = true, BackColor = UiTheme.BorderLight }, 1);
        AddPair(panel, "交易日期", _dtpDate = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short }, 2);
        AddPair(panel, "交貨日期", _dtpDelivery = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short }, 3);
        AddPair(panel, "課稅類別", _cmbTax = new ComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList }, 4);
        _cmbKind.SelectedIndexChanged += (s, e) => ReloadKindDependent();
        return panel;
    }

    private TableLayoutPanel BuildMasterRow2()
    {
        var panel = NewMasterRow();
        AddPair(panel, "交易對象", _cmbObject = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList }, 0);
        UiTheme.AutoWiden(_cmbObject);
        AddPair(panel, "部門", _cmbDept = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList }, 1);
        UiTheme.AutoWiden(_cmbDept);
        AddPair(panel, "員工", _cmbStaff = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList }, 2);
        UiTheme.AutoWiden(_cmbStaff);
        AddPair(panel, "送貨地址", _txtShipAddr = new TextBox { Width = 320 }, 3);
        return panel;
    }

    private TableLayoutPanel BuildMasterRow3()
    {
        var panel = NewMasterRow();
        AddPair(panel, "備註", _txtRemark = new TextBox { Width = 360 }, 0);
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var lblAmountT = new Label { Text = "合計金額：", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, 0) };
        UiTheme.StyleLabel(lblAmountT);
        _lblAmount = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, UiTheme.SpacingSm, UiTheme.SpacingLg, 0), ForeColor = UiTheme.Primary, Font = UiTheme.Font(11F, FontStyle.Bold) };
        var lblTaxT = new Label { Text = "稅額：", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, 0) };
        UiTheme.StyleLabel(lblTaxT);
        _lblTax = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, UiTheme.SpacingSm, UiTheme.SpacingLg, 0), ForeColor = UiTheme.Primary, Font = UiTheme.Font(11F, FontStyle.Bold) };
        var lblTotalT = new Label { Text = "總計：", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, 0) };
        UiTheme.StyleLabel(lblTotalT);
        _lblTotal = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, UiTheme.SpacingSm, 0, 0), ForeColor = UiTheme.Danger, Font = UiTheme.Font(12F, FontStyle.Bold) };
        panel.Controls.Add(lblAmountT, 2, 0);
        panel.Controls.Add(_lblAmount, 3, 0);
        panel.Controls.Add(lblTaxT, 4, 0);
        panel.Controls.Add(_lblTax, 5, 0);
        panel.Controls.Add(lblTotalT, 6, 0);
        panel.Controls.Add(_lblTotal, 7, 0);
        return panel;
    }

    private static TableLayoutPanel NewMasterRow()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            AutoSize = true,
            BackColor = UiTheme.Card,
        };
        for (int i = 0; i < 8; i++)
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return panel;
    }

    private void AddPair(TableLayoutPanel panel, string label, Control ctrl, int col)
    {
        var lbl = new Label { Text = label + "：", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, 0) };
        UiTheme.StyleLabel(lbl);
        ctrl.Dock = DockStyle.Fill;
        ctrl.Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingLg, UiTheme.SpacingXs);
        panel.Controls.Add(lbl, col * 2, 0);
        panel.Controls.Add(ctrl, col * 2 + 1, 0);
    }

    private void BuildDetailCard()
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background, Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingXs, UiTheme.SpacingLg, UiTheme.SpacingSm) };

        var bar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = UiTheme.Card };
        var lbl = new Label
        {
            Text = "單據明細",
            AutoSize = true,
            Location = new Point(12, 10),
            ForeColor = UiTheme.Primary,
            Font = UiTheme.Font(11F, FontStyle.Bold),
        };
        bar.Controls.Add(lbl);
        var btnAdd = new ModernButton { Text = "新增明細列", Width = 110, Height = 30, IsPrimary = false };
        btnAdd.Location = new Point(100, 5);
        btnAdd.Click += (s, e) => AddDetailRow();
        bar.Controls.Add(btnAdd);
        var btnRemove = new ModernButton { Text = "刪除明細列", Width = 110, Height = 30, IsPrimary = false };
        btnRemove.Location = new Point(220, 5);
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

        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "貨品編號", HeaderText = "貨品編號", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "品名", HeaderText = "品名", Width = 170, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "倉庫", HeaderText = "倉庫", Width = 55 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "數量", HeaderText = "數量", Width = 80 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "交易數量", HeaderText = "交易數量", Width = 80, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單位", HeaderText = "單位", Width = 48, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單價", HeaderText = "單價", Width = 90 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "折扣", HeaderText = "折扣", Width = 60 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "金額", HeaderText = "金額", Width = 100, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "附註說明", HeaderText = "附註說明", Width = 160 });
        _gridDetail.Columns["貨品編號"].Frozen = true;

        _gridDetail.CellEndEdit += OnDetailCellEndEdit;
        card.Controls.Add(_gridDetail);
        Controls.Add(card);
    }

    private void BuildListCard()
    {
        var card = new Panel { Dock = DockStyle.Bottom, Height = 210, BackColor = UiTheme.Background, Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingXs, UiTheme.SpacingLg, UiTheme.SpacingSm) };

        var bar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = UiTheme.Card };
        var lbl = new Label
        {
            Text = "單據清單",
            AutoSize = true,
            Location = new Point(12, 9),
            ForeColor = UiTheme.Primary,
            Font = UiTheme.Font(11F, FontStyle.Bold),
        };
        bar.Controls.Add(lbl);
        var lblFilter = new Label { Text = "單號：", AutoSize = true, Location = new Point(110, 11), ForeColor = UiTheme.TextSub, Font = UiTheme.Font(10F) };
        bar.Controls.Add(lblFilter);
        _txtFilterNo = new TextBox { Width = 120, Location = new Point(152, 7) };
        bar.Controls.Add(_txtFilterNo);
        var btnFilter = new ModernButton { Text = "查詢", Width = 70, Height = 26, IsPrimary = false };
        btnFilter.Location = new Point(282, 5);
        btnFilter.Click += (s, e) => LoadList();
        bar.Controls.Add(btnFilter);

        void AddNav(string text, int delta, bool extreme)
        {
            var b = new ModernButton { Text = text, Width = 56, Height = 26, IsPrimary = false };
            b.Click += (s, e) => MoveBillSelection(delta, extreme);
            bar.Controls.Add(b);
            _navButtons.Add(b);
        }
        AddNav("首筆", 0, true);
        AddNav("上筆", -1, false);
        AddNav("下筆", 1, false);
        AddNav("尾筆", 1, true);
        bar.Resize += (s, e) =>
        {
            int right = bar.ClientSize.Width - 8;
            for (int i = _navButtons.Count - 1; i >= 0; i--)
            {
                var b = _navButtons[i];
                b.Location = new Point(right - b.Width, 5);
                right -= b.Width + 8;
            }
        };
        card.Controls.Add(bar);

        _gridList = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        UiTheme.StyleDataGridView(_gridList);
        _gridList.RowTemplate.Height = 28;
        _gridList.SelectionChanged += (s, e) => UpdateListPos();
        card.Controls.Add(_gridList);
        Controls.Add(card);
    }

    private void BuildStatusBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = UiTheme.BorderLight };
        _lblStatus = new Label
        {
            Text = "狀態: 就緒",
            AutoSize = true,
            Location = new Point(12, 5),
            ForeColor = UiTheme.TextSub,
            Font = UiTheme.Font(10.5F, FontStyle.Bold),
        };
        bar.Controls.Add(_lblStatus);
        _lblPos = new Label
        {
            Text = "記錄: 0/0",
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(0, 5),
            ForeColor = UiTheme.TextSub,
            Font = UiTheme.Font(10.5F, FontStyle.Bold),
        };
        bar.Controls.Add(_lblPos);
        Controls.Add(bar);
    }

    // ==================== 類別相依 ====================

    private void ReloadKindDependent()
    {
        if (_cmbKind.SelectedItem is not string kindName) return;
        _loading = true;

        var obj = PoOrderService.LoadObjectCombo(kindName);
        _cmbObject.DataSource = obj;
        _cmbObject.DisplayMember = "公司簡稱";
        _cmbObject.ValueMember = "客廠編號";

        var dept = PoOrderService.LoadDepartmentCombo();
        _cmbDept.DataSource = dept;
        _cmbDept.DisplayMember = "部門名稱";
        _cmbDept.ValueMember = "部門編號";

        var staff = TradeService.LoadStaffCombo();
        _cmbStaff.DataSource = staff;
        _cmbStaff.DisplayMember = "員工姓名";
        _cmbStaff.ValueMember = "員工編號";

        _loading = false;
        ClearEditor();
        LoadList();
    }

    // ==================== 明細編輯 ====================

    private void AddDetailRow()
    {
        if (_viewing) return;
        int i = _gridDetail.Rows.Add();
        var row = _gridDetail.Rows[i];
        row.Cells["倉庫"].Value = TradeService.LoadParams().常用倉庫;
        row.Cells["折扣"].Value = 100m;
        row.Cells["交易數量"].Value = 0m;
        _gridDetail.CurrentCell = row.Cells["貨品編號"];
    }

    private void RemoveDetailRow()
    {
        if (_viewing) return;
        if (_gridDetail.SelectedCells.Count == 0) return;
        int i = _gridDetail.SelectedCells[0].RowIndex;
        if (i < 0 || i >= _gridDetail.Rows.Count) return;
        _gridDetail.Rows.RemoveAt(i);
        RecalcTotals();
    }

    private void OnDetailCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || _viewing || e.RowIndex < 0 || e.RowIndex >= _gridDetail.Rows.Count) return;
        var row = _gridDetail.Rows[e.RowIndex];
        if (row.IsNewRow) return;
        var col = _gridDetail.Columns[e.ColumnIndex].Name;

        if (col == "貨品編號")
        {
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
            decimal 建議單價 = PickUnitPrice(g);
            row.Cells["單價"].Value = 建議單價;
            if (row.Cells["倉庫"].Value is null or DBNull or "")
                row.Cells["倉庫"].Value = TradeService.LoadParams().常用倉庫;
            _loading = false;
            RecalcRowAmount(e.RowIndex);
        }
        else if (col is "數量" or "單價" or "折扣")
        {
            RecalcRowAmount(e.RowIndex);
        }
    }

    private void RecalcRowAmount(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _gridDetail.Rows.Count) return;
        var row = _gridDetail.Rows[rowIndex];
        decimal 數量 = Dec(row.Cells["數量"].Value);
        decimal 單價 = Dec(row.Cells["單價"].Value);
        decimal 折扣 = Dec(row.Cells["折扣"].Value);
        decimal 金額 = Math.Round(數量 * 單價 * 折扣 / 100m, 2, MidpointRounding.AwayFromZero);
        _loading = true;
        row.Cells["金額"].Value = 金額;
        _loading = false;
        RecalcTotals();
    }

    private decimal PickUnitPrice(Dictionary<string, object?> g)
    {
        decimal 售價A = Dec(g.TryGetValue("售價A", out var a) ? a : null);
        if (售價A > 0) return 售價A;
        decimal 標準售價 = Dec(g.TryGetValue("標準售價", out var s) ? s : null);
        return 標準售價;
    }

    private void RecalcTotals()
    {
        decimal 合計 = 0m;
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            合計 += Dec(r.Cells["金額"].Value);
        }
        var kind = PoOrderService.GetKind(_cmbKind.SelectedItem as string ?? "報價");
        bool 免稅 = _cmbTax.SelectedItem is string t && t.Contains("免");
        decimal 稅率 = kind.TaxSource == "進項"
            ? TradeService.LoadParams().進項稅率
            : TradeService.LoadParams().銷項稅率;
        decimal 稅 = 免稅 ? 0m : Math.Round(合計 * 稅率 / 100m, 0, MidpointRounding.AwayFromZero);
        _lblAmount.Text = 合計.ToString("N2");
        _lblTax.Text = 稅.ToString("N2");
        _lblTotal.Text = (合計 + 稅).ToString("N2");
    }

    // ==================== 存檔 / 檢視 / 刪除 ====================

    private void Save()
    {
        if (_viewing)
        {
            MessageBox.Show("目前為檢視模式（已載入既有單據），請按「重讀」開始新單。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_cmbObject.SelectedValue is not string 對象 || 對象.Length == 0)
        {
            MessageBox.Show("請選擇交易對象。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var req = new PoOrderService.PoBillRequest
        {
            單據副碼 = _viewingKey == 0 ? null : _viewingKey,
            單據類別 = _cmbKind.SelectedItem as string ?? "報價",
            交易日期 = _dtpDate.Value.Date,
            交貨日期 = _dtpDelivery.Value.Date,
            交易對象 = 對象,
            部門編號 = _cmbDept.SelectedValue as string ?? "",
            員工編號 = _cmbStaff.SelectedValue as string ?? "",
            送貨地址 = _txtShipAddr.Text.Trim(),
            課稅類別 = _cmbTax.SelectedItem as string ?? "外加",
            備註 = _txtRemark.Text.Trim(),
        };
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            var code = (r.Cells["貨品編號"].Value as string ?? "").Trim();
            if (code.Length == 0) continue;
            req.明細.Add(new PoOrderService.PoLine
            {
                貨品編號 = code,
                倉庫編號 = (r.Cells["倉庫"].Value as string ?? "").Trim(),
                數量 = Dec(r.Cells["數量"].Value),
                單位 = (r.Cells["單位"].Value as string ?? "").Trim(),
                單價 = Dec(r.Cells["單價"].Value),
                折扣 = Dec(r.Cells["折扣"].Value),
                附註說明 = (r.Cells["附註說明"].Value as string ?? "").Trim(),
            });
        }
        try
        {
            long editedKey = _viewingKey;
            var result = PoOrderService.SavePoBill(req);
            decimal 合計 = req.明細.Sum(d => PoOrderService.CalcDetailAmount(d));
            var flowSeq = ApprovalService.Submit(req.單據類別, result.交易單號, 合計,
                AuditService.CurrentUser, req.備註);
            MessageBox.Show($"單據「{result.交易單號}」已儲存。"
                + (flowSeq is null ? "" : "\n已自動送審（待核准）。"),
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (editedKey > 0)
            {
                LoadList();
                LoadBillByKey(editedKey);
            }
            else
            {
                ClearEditor();
                LoadList();
                _lblStatus.Text = $"狀態: 已儲存 {result.交易單號}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearEditor()
    {
        _viewing = false;
        _viewingKey = 0;
        _txtRemark.Clear();
        _txtShipAddr.Clear();
        _dtpDate.Value = DateTime.Now;
        _dtpDelivery.Value = DateTime.Now;
        if (_cmbTax.Items.Count > 0) _cmbTax.SelectedIndex = 0;
        _txtNo.Text = PoOrderService.PreviewPoNo(_cmbKind.SelectedItem as string ?? "報價");
        _gridDetail.Rows.Clear();
        RecalcTotals();
        _lblStatus.Text = "狀態: 就緒";
    }

    private void LoadList()
    {
        if (_cmbKind.SelectedItem is not string kindName) return;
        var dt = PoOrderService.LoadPoList(kindName, _txtFilterNo.Text.Trim());
        _loading = true;
        _gridList.DataSource = dt;
        if (_gridList.Columns.Contains("合計金額"))
            _gridList.Columns["合計金額"].DefaultCellStyle.Format = "N2";
        if (_gridList.Columns.Contains("營業稅"))
            _gridList.Columns["營業稅"].DefaultCellStyle.Format = "N2";
        if (_gridList.Columns.Contains("總計金額"))
            _gridList.Columns["總計金額"].DefaultCellStyle.Format = "N2";
        _loading = false;
        UpdateListPos();
    }

    private void LoadSelectedView()
    {
        if (_gridList.SelectedRows.Count == 0 || _gridList.SelectedRows[0].IsNewRow)
        {
            MessageBox.Show("請先於清單選取一筆單據。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        long 副碼 = Convert.ToInt64(_gridList.SelectedRows[0].Cells["單據副碼"].Value);
        LoadBillByKey(副碼);
    }

    /// <summary>依單據副碼載入檢視（檢視模式：可列印／刪除／修改，禁止直接儲存）。</summary>
    private void LoadBillByKey(long 副碼)
    {
        var master = PoOrderService.LoadPoMaster(副碼);
        if (master.Rows.Count == 0) return;
        var m = master.Rows[0];
        var details = PoOrderService.LoadPoDetails(副碼);

        _viewing = true;
        _viewingKey = 副碼;

        _loading = true;
        _cmbKind.SelectedItem = Str(m["單據類別"]);
        _txtNo.Text = Str(m["交易單號"]);
        _dtpDate.Value = DateTime.TryParse(Str(m["交易日期"]), out var d) ? d : DateTime.Now;
        _dtpDelivery.Value = DateTime.TryParse(Str(m["交貨日期"]), out var dl) ? dl : DateTime.Now;
        _cmbTax.SelectedItem = Str(m["課稅類別"]).Length > 0 ? Str(m["課稅類別"]) : "外加";
        SelectComboValue(_cmbObject, Str(m["交易對象"]));
        SelectComboValue(_cmbDept, Str(m["部門編號"]));
        SelectComboValue(_cmbStaff, Str(m["員工編號"]));
        _txtShipAddr.Text = Str(m["送貨地址"]);
        _txtRemark.Text = Str(m["備註"]);
        _loading = false;

        _gridDetail.Rows.Clear();
        foreach (DataRow r in details.Rows)
        {
            int i = _gridDetail.Rows.Add();
            var gr = _gridDetail.Rows[i];
            gr.Cells["貨品編號"].Value = Str(r["貨品編號"]);
            gr.Cells["品名"].Value = Str(r["品名"]);
            gr.Cells["倉庫"].Value = Str(r["倉庫編號"]);
            gr.Cells["數量"].Value = r["數量"];
            gr.Cells["交易數量"].Value = r["交易數量"];
            gr.Cells["單位"].Value = Str(r["單位"]);
            gr.Cells["單價"].Value = r["單價"];
            gr.Cells["折扣"].Value = r["折扣"];
            gr.Cells["金額"].Value = r["金額"];
            gr.Cells["附註說明"].Value = Str(r["附註說明"]);
        }
        RecalcTotals();
        _lblStatus.Text = $"狀態: 檢視 {_txtNo.Text}（可按「修改」後儲存）";
        UpdateListPos();
    }

    /// <summary>修改：解除檢視鎖定，允許編輯後儲存（更新既有單據）。</summary>
    private void StartEdit()
    {
        if (_viewingKey == 0)
        {
            MessageBox.Show("請先於清單選取並載入一筆單據，再按「修改」。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _viewing = false;
        _lblStatus.Text = $"狀態: 修改 {_txtNo.Text}（儲存將更新此單據）";
    }

    /// <summary>復原：捨棄未存變更（修改中回到原單據內容；新增中清空）。</summary>
    private void RestoreEditor()
    {
        if (_viewingKey > 0)
        {
            LoadBillByKey(_viewingKey);
            return;
        }
        ClearEditor();
    }

    /// <summary>搜尋：聚焦清單單號過濾框。</summary>
    private void FocusListSearch()
    {
        _txtFilterNo.Focus();
        _txtFilterNo.SelectAll();
    }

    /// <summary>原系統風導覽：extreme 為 true 時 delta 0=首筆、非 0=尾筆；否則上／下一筆。</summary>
    private void MoveBillSelection(int delta, bool extreme)
    {
        if (_gridList.SelectedRows.Count == 0) return;
        int idx = _gridList.SelectedRows[0].Index;
        int target = extreme
            ? (delta == 0 ? 0 : _gridList.Rows.Count - 1)
            : Math.Clamp(idx + delta, 0, _gridList.Rows.Count - 1);
        if (target < 0 || target >= _gridList.Rows.Count || target == idx) return;
        _gridList.ClearSelection();
        _gridList.Rows[target].Selected = true;
        _gridList.CurrentCell = _gridList.Rows[target].Cells[0];
        LoadSelectedView();
    }

    private void UpdateListPos()
    {
        int n = _gridList.Rows.Count;
        int i = _gridList.SelectedRows.Count > 0 ? _gridList.SelectedRows[0].Index + 1 : 0;
        _lblPos.Text = $"記錄: {i}/{n}";
    }

    private static void SelectComboValue(ComboBox cmb, string value)
    {
        if (cmb.Items.Count == 0 || value.Length == 0) return;
        try
        {
            cmb.SelectedValue = value;
            if (cmb.SelectedIndex >= 0) return;
        }
        catch
        {
            // SelectedValue 類型不符時改以文字比對
        }
        for (int i = 0; i < cmb.Items.Count; i++)
        {
            if (cmb.GetItemText(cmb.Items[i]) == value)
            {
                cmb.SelectedIndex = i;
                return;
            }
        }
    }

    private void DeleteSelected()
    {
        if (_gridList.SelectedRows.Count == 0 || _gridList.SelectedRows[0].IsNewRow)
        {
            MessageBox.Show("請先於清單選取一筆單據。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var row = _gridList.SelectedRows[0];
        string 單號 = Str(row.Cells["交易單號"].Value);
        long 副碼 = Convert.ToInt64(row.Cells["單據副碼"].Value);
        var confirm = MessageBox.Show($"確定刪除單據「{單號}」？",
            "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        try
        {
            PoOrderService.DeletePoBill(副碼);
            MessageBox.Show($"單據「{單號}」已刪除。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearEditor();
            LoadList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刪除失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ==================== 工具 ====================

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    private static decimal Dec(object? v) =>
        v is null or DBNull ? 0m : (decimal.TryParse(v.ToString(), out var m) ? m : 0m);

    // ==================== 列印 ====================

    private static string ReportDir => ReportPrintService.RepDirectory;

    private void PrintBill()
    {
        if (_viewingKey == 0)
        {
            MessageBox.Show("請先於清單選取並載入一筆單據，再按列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var kind = PoOrderService.GetKind(Str(_cmbKind.SelectedItem));
        string rtmPath = Path.Combine(ReportDir, kind.ReportFile);
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
        var billNo = Str(_txtNo.Text);

        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument
        {
            DocumentName = $"{kind.Name}單-{billNo}",
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
        var data = new RtmData { DetailPipeline = "ppDBPipeline2" };

        var dt = PoOrderService.LoadPoMaster(_viewingKey);
        if (dt.Rows.Count > 0)
        {
            var row = dt.Rows[0];
            foreach (DataColumn col in dt.Columns)
                data.Master[$"ppDBPipeline1|{col.ColumnName}"] = row[col];
        }

        ARService.FillCompany(data);

        var detailDt = PoOrderService.LoadPoPrintDetails(_viewingKey);
        foreach (DataRow dr in detailDt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in detailDt.Columns)
                d[col.ColumnName] = dr[col];
            data.Detail.Add(d);
        }
        return data;
    }
}
