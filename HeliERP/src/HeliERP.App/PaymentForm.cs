// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Printing;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 收付作業（收款 / 付款沖帳）：新增沖帳單、撤銷沖帳（刪除）。
/// 沖帳單一經儲存不可修改，錯誤單據以「刪除」撤銷沖帳後重做。
/// </summary>
public sealed class PaymentForm : Form
{
    private readonly PaymentService.PaymentKind _kind = PaymentService.Kinds[0];

    private DataGridView _grid = null!;
    private DataGridView _gridDetail = null!;

    private ComboBox _cmbKind = null!;          // 搜尋面板：收付類別（收款/付款/全部）
    private TextBox _txtNo = null!;             // 搜尋面板：單號
    private TextBox _txtObject = null!;         // 搜尋面板：對象
    private DateTimePicker _dtFrom = null!;
    private DateTimePicker _dtTo = null!;

    private ComboBox _cmbKindMain = null!;      // 主檔：收付類別
    private TextBox _txtPaymentNo = null!;      // 主檔：收付單號（唯讀）
    private DateTimePicker _dtDate = null!;     // 主檔：沖帳日期
    private ComboBox _cmbObject = null!;        // 主檔：沖帳對象
    private Label _lblObjectName = null!;       // 主檔：對象名稱
    private TextBox _txtCash = null!;           // 主檔：現金金額
    private TextBox _txtCheck = null!;          // 主檔：票據金額
    private TextBox _txtPrepaidUse = null!;     // 主檔：取用預收
    private TextBox _txtPrepaidAdd = null!;     // 主檔：累入預收
    private Label _lblPrepaidBalance = null!;   // 主檔：預收餘額
    private Label _lblTotal = null!;            // 主檔：沖帳合計

    private Label _lblRecord = null!;
    private Label _lblStatus = null!;

    private long _currentKey;
    private int _currentIndex = -1;
    private bool _editing;
    private bool _loading;

    public PaymentForm()
    {
        Text = "收付系統 - HeliERP";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        BackColor = UiTheme.Background;

        var header = UiTheme.BuildHeader("收付系統", "收款（客戶應收沖帳）／付款（廠商應付沖帳）作業");
        header.Dock = DockStyle.Top;
        Controls.Add(header);

        BuildToolbar();
        BuildSearchPanel();
        BuildMasterPanel();
        BuildDetailPanel();
        BuildListGrid();
        BuildStatusBar();

        // 初次載入沖帳對象下拉（收付類別預設收款→客戶清單），避免進入畫面時對象無法選擇
        ReloadObjectCombo();

        Load += (s, e) =>
        {
            try
            {
                _cmbKind.SelectedIndex = 0;
                _cmbKindMain.SelectedIndex = 0;
                _lblStatus.Text = "狀態: 就緒";
                LoadList();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "狀態: 載入失敗 - " + ex.Message;
            }
        };

        ShortcutHelper.Enable(this, NewPayment, null, DeletePayment, LoadList);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

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
        void Sep()
        {
            bar.Controls.Add(new Panel
            {
                Location = new Point(x, 10),
                Size = new Size(2, 32),
                BackColor = Color.FromArgb(70, Color.White),
            });
            x += UiTheme.SpacingSm + 2;
        }

        var btnSearch = new ModernButton { Text = "搜尋", Width = 120 };
        btnSearch.Click += (s, e) => { LoadList(); _txtNo.Focus(); };
        var btnReload = new ModernButton { Text = "重讀", Width = 120, IsPrimary = false };
        btnReload.Click += (s, e) => LoadList();
        var btnNew = new ModernButton { Text = "新增沖帳", Width = 120 };
        btnNew.Click += (s, e) => NewPayment();
        var btnDel = new ModernButton { Text = "刪除(撤銷)", Width = 128, IsPrimary = false };
        btnDel.Click += (s, e) => DeletePayment();
        var btnPrint = new ModernButton { Text = "列印", Width = 120, IsPrimary = false };
        btnPrint.Click += (s, e) => ShowPrintMenu();

        Add(btnSearch); Add(btnReload); Add(btnNew); Add(btnDel); Add(btnPrint);
        Sep();

        var btnSave = new ModernButton { Text = "儲存", Width = 120 };
        btnSave.Click += (s, e) => SavePayment();
        var btnRevert = new ModernButton { Text = "復原", Width = 120, IsPrimary = false };
        btnRevert.Click += (s, e) => Revert();

        Add(btnSave); Add(btnRevert);
        Sep();

        var btnFirst = new ModernButton { Text = "首筆", Width = 128, IsPrimary = false };
        btnFirst.Click += (s, e) => SelectRow(0);
        var btnPrev = new ModernButton { Text = "上筆", Width = 128, IsPrimary = false };
        btnPrev.Click += (s, e) => SelectRow(_currentIndex - 1);
        var btnNext = new ModernButton { Text = "下筆", Width = 128, IsPrimary = false };
        btnNext.Click += (s, e) => SelectRow(_currentIndex + 1);
        var btnLast = new ModernButton { Text = "尾筆", Width = 128, IsPrimary = false };
        btnLast.Click += (s, e) => SelectRow(_grid.Rows.Count - 1);

        Add(btnFirst); Add(btnPrev); Add(btnNext); Add(btnLast);
        Sep();

        var btnHelp = new ModernButton { Text = "說明", Width = 120, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show("收付系統沖帳作業\n收款單：沖銷客戶應收帳款；付款單：沖銷廠商應付帳款。\n" +
                "新增 → 選取對象 → 明細自動帶出未沖帳單據 → 調整沖帳金額與現金/票據 → 儲存。\n" +
                "沖帳單一經儲存不可修改，錯誤單據請按「刪除(撤銷)」回復帳款後重做。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 120, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnHelp); Add(btnExit);

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

        var lblKind = new Label { Text = "收付類別：", Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblKind, sub: true);
        panel.Controls.Add(lblKind);
        _cmbKind = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbKind);
        _cmbKind.Items.AddRange(new object[] { "收款", "付款", "全部" });
        _cmbKind.SelectedIndexChanged += (s, e) => LoadList();
        panel.Controls.Add(_cmbKind);

        var lblNo = new Label { Text = "單號：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblNo, sub: true);
        panel.Controls.Add(lblNo);
        _txtNo = new TextBox { Width = 120 };
        UiTheme.StyleTextBox(_txtNo);
        panel.Controls.Add(_txtNo);

        var lblObject = new Label { Text = "對象：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblObject, sub: true);
        panel.Controls.Add(lblObject);
        _txtObject = new TextBox { Width = 140 };
        UiTheme.StyleTextBox(_txtObject);
        panel.Controls.Add(_txtObject);

        var lblFrom = new Label { Text = "沖帳日期：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
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
            _txtNo.Clear(); _txtObject.Clear();
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
            Text = "狀態: 就緒",
            AutoSize = true,
            Location = new Point(200, 5),
            ForeColor = UiTheme.TextSub,
            Font = UiTheme.Font(10.5F, FontStyle.Bold),
        };
        bar.Controls.Add(_lblRecord);
        bar.Controls.Add(_lblStatus);
        Controls.Add(bar);
    }

    private void BuildMasterPanel()
    {
        var card = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = UiTheme.Background, Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingSm, UiTheme.SpacingLg, UiTheme.SpacingSm) };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 12,
            RowCount = 3,
        };
        for (int i = 0; i < 12; i++)
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 12));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        _txtPaymentNo = new TextBox();
        UiTheme.StyleTextBox(_txtPaymentNo, readOnly: true);
        AddPair(panel, "收付單號", _txtPaymentNo, 0, 0);

        _cmbKindMain = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbKindMain);
        foreach (var k in PaymentService.Kinds)
            _cmbKindMain.Items.Add(k.Display);
        _cmbKindMain.SelectedIndexChanged += (s, e) => SwitchKind(PaymentService.Kinds[Math.Max(0, _cmbKindMain.SelectedIndex)]);
        AddPair(panel, "收付類別", _cmbKindMain, 0, 1);

        _dtDate = new DateTimePicker { Format = DateTimePickerFormat.Short };
        UiTheme.StyleDateTimePicker(_dtDate);
        AddPair(panel, "沖帳日期", _dtDate, 0, 2);

        _cmbObject = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbObject);
        _cmbObject.SelectedIndexChanged += (s, e) => OnObjectChanged();
        AddPair(panel, "沖帳對象", _cmbObject, 0, 3);

        _lblPrepaidBalance = new Label();
        UiTheme.StyleLabel(_lblPrepaidBalance, sub: true);
        AddPair(panel, "預收餘額", _lblPrepaidBalance, 0, 4);

        _txtCash = new TextBox();
        UiTheme.StyleTextBox(_txtCash);
        _txtCash.TextChanged += (s, e) => RecalcSummary();
        AddPair(panel, "現金金額", _txtCash, 1, 0);

        _txtCheck = new TextBox();
        UiTheme.StyleTextBox(_txtCheck);
        _txtCheck.TextChanged += (s, e) => RecalcSummary();
        AddPair(panel, "票據金額", _txtCheck, 1, 1);

        _txtPrepaidUse = new TextBox();
        UiTheme.StyleTextBox(_txtPrepaidUse);
        _txtPrepaidUse.TextChanged += (s, e) => RecalcSummary();
        AddPair(panel, "取用預收", _txtPrepaidUse, 1, 2);

        _txtPrepaidAdd = new TextBox();
        UiTheme.StyleTextBox(_txtPrepaidAdd);
        _txtPrepaidAdd.TextChanged += (s, e) => RecalcSummary();
        AddPair(panel, "累入預收", _txtPrepaidAdd, 1, 3);

        var lblTotalSum = new Label { Text = "沖帳合計", Font = UiTheme.Font(10F, FontStyle.Bold), ForeColor = UiTheme.TextSub, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        _lblTotal = new Label { Text = "0", Font = UiTheme.Font(14F, FontStyle.Bold), ForeColor = UiTheme.AccentDark, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        panel.Controls.Add(lblTotalSum, 8, 1);
        panel.Controls.Add(_lblTotal, 10, 1);
        panel.SetColumnSpan(_lblTotal, 2);

        _lblObjectName = new Label();
        UiTheme.StyleLabel(_lblObjectName, sub: true);
        AddPair(panel, "對象名稱", _lblObjectName, 2, 0, spanCol: 2);

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
            Text = "沖帳明細（新增時自動帶入該對象未沖帳單據；檢視時顯示已沖帳明細）",
            AutoSize = true,
            Location = new Point(12, 10),
            ForeColor = UiTheme.Primary,
            Font = UiTheme.Font(11F, FontStyle.Bold),
        };
        bar.Controls.Add(lbl);
        card.Controls.Add(bar);

        _gridDetail = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
        };
        UiTheme.StyleDataGridView(_gridDetail);
        _gridDetail.RowTemplate.Height = 30;
        _gridDetail.CellValueChanged += OnDetailCellValueChanged;
        card.Controls.Add(_gridDetail);

        Controls.Add(card);
    }

    // ==================== 事件 ====================

    private void SwitchKind(PaymentService.PaymentKind kind)
    {
        if (_loading) return;
        ReloadObjectCombo();
        ClearEdit();
        LoadList();
        _lblStatus.Text = $"狀態: {kind.Display}";
    }

    private void ReloadObjectCombo()
    {
        var kind = PaymentService.Kinds[Math.Max(0, _cmbKindMain.SelectedIndex)];
        var dt = PaymentService.LoadObjectCombo(kind.ObjectType);
        dt.Columns.Add("顯示", typeof(string));
        foreach (DataRow r in dt.Rows)
        {
            var 簡稱 = r["公司簡稱"] is DBNull ? "" : r["公司簡稱"].ToString();
            r["顯示"] = string.IsNullOrWhiteSpace(簡稱) ? r["客廠編號"].ToString() : $"{r["客廠編號"]}  {簡稱}";
        }
        _cmbObject.DataSource = dt;
        _cmbObject.DisplayMember = "顯示";
        _cmbObject.ValueMember = "客廠編號";
        _cmbObject.SelectedIndex = -1;
    }

    private void OnObjectChanged()
    {
        if (_loading) return;
        var no = SelectedObject();
        _lblObjectName.Text = string.IsNullOrEmpty(no) ? "" : PaymentService.LookupObjectName(no) ?? "";
        _lblPrepaidBalance.Text = string.IsNullOrEmpty(no) ? "0" : PaymentService.LookupPrepaidBalance(no).ToString("N2");
        if (_editing)
            LoadOpenBills();
    }

    private void OnDetailCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || e.RowIndex < 0 || e.RowIndex >= _gridDetail.Rows.Count) return;
        if (_gridDetail.DataSource is not DataTable dt || !dt.Columns.Contains("沖帳金額")) return;
        var 折讓欄 = dt.Columns.Contains("折讓金額") ? _gridDetail.Columns["折讓金額"].Index : -1;
        if (e.ColumnIndex != _gridDetail.Columns["沖帳金額"].Index && e.ColumnIndex != 折讓欄) return;
        var row = _gridDetail.Rows[e.RowIndex];
        if (row.IsNewRow) return;
        var 未收 = Dec(row.Cells["未收付金額"].Value);
        var 沖 = Dec(row.Cells["沖帳金額"].Value);
        var 折讓 = 折讓欄 < 0 ? 0m : Dec(row.Cells["折讓金額"].Value);
        if (折讓欄 >= 0 && e.ColumnIndex == 折讓欄 && 沖 <= 0m)
        {
            _loading = true;
            row.Cells[折讓欄].Value = 0m;
            _loading = false;
            MessageBox.Show("請先輸入沖帳金額，再列折讓。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (沖 + 折讓 > 未收)
        {
            _loading = true;
            row.Cells[e.ColumnIndex].Value = 0m;
            _loading = false;
            MessageBox.Show($"沖帳 + 折讓不得超過未收付金額 {未收:N2}。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        RecalcSummary();
    }

    private void RecalcSummary()
    {
        decimal 合計 = 0m;
        if (_gridDetail.DataSource is DataTable dt && dt.Columns.Contains("沖帳金額"))
        {
            foreach (DataGridViewRow r in _gridDetail.Rows)
            {
                if (r.IsNewRow) continue;
                合計 += Dec(r.Cells["沖帳金額"].Value);
            }
        }
        _lblTotal.Text = 合計.ToString("N2");
    }

    // ==================== 資料操作 ====================

    private void LoadList()
    {
        string? 類別 = _cmbKind.SelectedIndex switch
        {
            0 => "收款",
            1 => "付款",
            _ => null,
        };
        var 起 = _dtFrom.Checked ? _dtFrom.Value : (DateTime?)null;
        var 迄 = _dtTo.Checked ? _dtTo.Value : (DateTime?)null;
        var dt = PaymentService.LoadPayments(類別, _txtObject.Text, 起, 迄);
        if (!string.IsNullOrWhiteSpace(_txtNo.Text))
        {
            var rows = dt.Select($"[收付單號] LIKE '%{_txtNo.Text.Trim().Replace("'", "''")}%'");
            var filtered = dt.Clone();
            foreach (var r in rows)
                filtered.ImportRow(r);
            dt = filtered;
        }
        _grid.DataSource = dt;
        _grid.Columns["單據副碼"].Visible = false;
        _grid.Columns["收付單號"].HeaderText = "收付單號";
        _grid.Columns["收付類別"].HeaderText = "類別";
        _grid.Columns["沖帳日期"].HeaderText = "沖帳日期";
        _grid.Columns["沖帳對象"].HeaderText = "對象編號";
        _grid.Columns["對象名稱"].HeaderText = "對象名稱";
        _grid.Columns["現金金額"].HeaderText = "現金";
        _grid.Columns["票據金額"].HeaderText = "票據";
        _grid.Columns["沖帳合計"].HeaderText = "沖帳合計";
        _grid.Columns["應收餘額"].HeaderText = "沖帳後餘額";
        foreach (DataGridViewColumn c in _grid.Columns)
        {
            if (c.Name is "現金金額" or "票據金額" or "沖帳合計" or "應收餘額")
                c.DefaultCellStyle.Format = "N2";
        }
        if (dt.Rows.Count > 0)
            _grid.Rows[0].Selected = true;
        else
        {
            ClearEdit();
            _lblRecord.Text = "記錄: 0 / 0";
        }
    }

    private void OnRowSelected()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].IsNewRow) return;
        _currentIndex = _grid.SelectedRows[0].Index;
        _lblRecord.Text = $"記錄: {_currentIndex + 1} / {_grid.Rows.Count}";
        var 副碼 = Convert.ToInt64(_grid.SelectedRows[0].Cells["單據副碼"].Value);
        LoadPayment(副碼);
    }

    private void SelectRow(int index)
    {
        if (_grid.Rows.Count == 0) return;
        if (index < 0) index = 0;
        if (index >= _grid.Rows.Count) index = _grid.Rows.Count - 1;
        _grid.Rows[index].Selected = true;
        _grid.CurrentCell = _grid.Rows[index].Cells[0];
    }

    private void LoadPayment(long 副碼)
    {
        var dt = DbManager.QueryTable(
            "SELECT * FROM [收付主檔] WHERE [單據副碼] = $c",
            DbManager.Param("$c", 副碼));
        if (dt.Rows.Count == 0) return;
        var m = dt.Rows[0];
        _currentKey = 副碼;
        _editing = false;

        _loading = true;
        _txtPaymentNo.Text = Str(m["收付單號"]);
        _cmbKindMain.SelectedIndex = PaymentService.GetKind(Str(m["收付類別"])).Name == "收款" ? 0 : 1;
        // LoadPayment 期間 _loading = true 會擋掉 SwitchKind 的 ReloadObjectCombo，故在此補載入正確對象清單
        ReloadObjectCombo();
        TrySetDate(_dtDate, m["沖帳日期"]);
        _cmbObject.SelectedValue = Str(m["沖帳對象"]);
        _txtCash.Text = Dec(m["現金金額"]).ToString("0.##");
        _txtCheck.Text = Dec(m["票據金額"]).ToString("0.##");
        _txtPrepaidUse.Text = Dec(m["取用預收"]).ToString("0.##");
        _txtPrepaidAdd.Text = Dec(m["累入預收"]).ToString("0.##");
        _lblPrepaidBalance.Text = Dec(m["預收餘額"]).ToString("N2");
        _loading = false;

        _lblObjectName.Text = string.IsNullOrEmpty(SelectedObject()) ? "" : PaymentService.LookupObjectName(SelectedObject()) ?? "";
        var details = PaymentService.LoadPaymentDetails(副碼);
        BindDetailGrid(details);
        RecalcSummary();
        SetEditMode(false);
        _lblStatus.Text = "狀態: 檢視（沖帳單不可修改，刪除即撤銷沖帳）";
    }

    private void BindDetailGrid(DataTable dt)
    {
        _loading = true;
        _gridDetail.DataSource = dt;
        _gridDetail.ReadOnly = true;
        foreach (DataGridViewColumn c in _gridDetail.Columns)
        {
            c.HeaderText = c.Name switch
            {
                "單據號碼" => "交易單號",
                "單別" => "單別",
                "單據日期" => "交易日期",
                "現行餘額" => "沖帳前餘額",
                "折讓金額" => "折讓",
                "沖帳金額" => "沖帳金額",
                _ => c.Name,
            };
            if (c.Name is "現行餘額" or "折讓金額" or "沖帳金額")
                c.DefaultCellStyle.Format = "N2";
        }
        _loading = false;
    }

    private void LoadOpenBills()
    {
        var no = SelectedObject();
        if (string.IsNullOrEmpty(no))
        {
            _gridDetail.DataSource = null;
            RecalcSummary();
            return;
        }
        var dt = PaymentService.LoadOpenBills(no);
        BindOpenBillGrid(dt);
    }

    private void BindOpenBillGrid(DataTable dt)
    {
        _loading = true;
        _gridDetail.DataSource = dt;
        _gridDetail.ReadOnly = false;
        foreach (DataGridViewColumn c in _gridDetail.Columns)
        {
            c.HeaderText = c.Name switch
            {
                "交易單號" => "交易單號",
                "單據類別" => "單別",
                "交易日期" => "交易日期",
                "總計金額" => "總計金額",
                "已收付金額" => "已收付",
                "未收付金額" => "未收付",
                "沖帳金額" => "沖帳金額(可改)",
                "折讓金額" => "折讓(可改)",
                _ => c.Name,
            };
            if (c.Name is "總計金額" or "已收付金額" or "未收付金額" or "沖帳金額" or "折讓金額")
                c.DefaultCellStyle.Format = "N2";
        }
        _loading = false;
        RecalcSummary();
    }

    private void NewPayment()
    {
        _currentKey = 0;
        _editing = true;
        _loading = true;
        _txtPaymentNo.Text = PaymentService.PreviewPaymentNo();
        _dtDate.Value = DateTime.Now;
        _cmbObject.SelectedIndex = -1;
        _txtCash.Text = "0";
        _txtCheck.Text = "0";
        _txtPrepaidUse.Text = "0";
        _txtPrepaidAdd.Text = "0";
        _lblPrepaidBalance.Text = "0";
        _gridDetail.DataSource = null;
        _lblObjectName.Text = "";
        _loading = false;
        RecalcSummary();
        SetEditMode(true);
        _lblStatus.Text = "狀態: 新增沖帳中（選取對象帶入未沖帳單據）";
        _cmbObject.Focus();
    }

    private void SavePayment()
    {
        if (!_editing)
        {
            MessageBox.Show("請先按「新增沖帳」再儲存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrEmpty(SelectedObject()))
        {
            MessageBox.Show("請選擇沖帳對象。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var 明細 = CollectDetails();
        decimal 現金 = Dec(_txtCash.Text);
        decimal 票據 = Dec(_txtCheck.Text);
        decimal 取用預收 = Dec(_txtPrepaidUse.Text);
        decimal 累入預收 = Dec(_txtPrepaidAdd.Text);
        if (明細.Count == 0)
        {
            if (累入預收 <= 0m)
            {
                MessageBox.Show("該對象沒有可沖帳的未收付單據。可輸入「累入預收」存入預收貨款，或取消。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (Math.Abs(現金 - 累入預收) > 0.005m || 票據 != 0m || 取用預收 != 0m)
            {
                MessageBox.Show("純累入預收單：現金金額必須等於累入預收，且不可同時沖帳或取用預收。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        else
        {
            decimal 合計 = 明細.Sum(d => d.沖帳金額);
            if (Math.Abs(現金 + 票據 + 取用預收 - 合計) > 0.005m)
            {
                MessageBox.Show($"現金 + 票據 + 取用預收（{現金:N2} + {票據:N2} + {取用預收:N2}）必須等於沖帳合計 {合計:N2}。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }
        var req = new PaymentService.SavePaymentRequest
        {
            收付類別 = PaymentService.GetKind(PaymentService.Kinds[Math.Max(0, _cmbKindMain.SelectedIndex)].Name).Name,
            沖帳日期 = _dtDate.Value,
            沖帳對象 = SelectedObject(),
            現金金額 = 現金,
            票據金額 = 票據,
            取用預收 = 取用預收,
            累入預收 = 累入預收,
            明細 = 明細,
        };
        try
        {
            var r = PaymentService.SavePayment(req);
            decimal 沖帳合計 = req.明細.Sum(d => d.沖帳金額);
            var flowSeq = ApprovalService.Submit(req.收付類別, r.收付單號, 沖帳合計,
                AuditService.CurrentUser, "");
            MessageBox.Show($"儲存成功：{r.收付單號}"
                + (flowSeq is null ? "" : "\n已自動送審（待核准）。"), "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _editing = false;
            LoadList();
            LocatePayment(r.單據副碼);
            _lblStatus.Text = "狀態: 已儲存";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Revert()
    {
        if (!_editing) return;
        if (_currentKey == 0)
        {
            NewPayment();
            _lblStatus.Text = "狀態: 已復原（新增）";
        }
        else
        {
            LoadPayment(_currentKey);
            _lblStatus.Text = "狀態: 已復原";
        }
    }

    private void DeletePayment()
    {
        if (_currentKey == 0)
        {
            MessageBox.Show("請先選取一張收付單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show($"確定要刪除收付單「{_txtPaymentNo.Text}」嗎？\n此動作將撤銷沖帳並回復該對象帳款，無法復原。",
                "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try
        {
            PaymentService.DeletePayment(_currentKey);
            MessageBox.Show("已刪除並回復帳款。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _currentKey = 0;
            _editing = false;
            ClearEdit();
            LoadList();
            _lblStatus.Text = "狀態: 已撤銷沖帳";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刪除失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ==================== 列印報表 ====================

    private static string ReportDir => ReportPrintService.RepDirectory;

    private void ShowPrintMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("收款沖銷日報表", null, (s, e) => PrintWriteOffReport("收款"));
        menu.Items.Add("付款沖銷日報表", null, (s, e) => PrintWriteOffReport("付款"));
        menu.Show(Cursor.Position);
    }

    private void PrintWriteOffReport(string 收付類別)
    {
        var data = WriteOffService.BuildWriteOffReportData(收付類別);
        if (data.Detail.Count == 0)
        {
            MessageBox.Show($"目前沒有「{收付類別}」沖銷資料可列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        bool 付款 = 收付類別 == WriteOffService.付款類別;
        PrintReport(付款 ? "付款沖銷日報表.rtm" : "收款沖銷日報表.rtm", data, 付款 ? "付款沖銷日報表" : "收款沖銷日報表");
    }

    private void PrintReport(string rtmFile, RtmData data, string docName)
    {
        string rtmPath = Path.Combine(ReportDir, rtmFile);
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
        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument { DocumentName = docName };
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
        _lblStatus.Text = "狀態: 已開啟報表預覽";
    }

    private void ClearEdit()
    {
        _currentKey = 0;
        _editing = false;
        _loading = true;
        _txtPaymentNo.Clear();
        _cmbObject.SelectedIndex = -1;
        _txtCash.Clear();
        _txtCheck.Clear();
        _txtPrepaidUse.Clear();
        _txtPrepaidAdd.Clear();
        _lblPrepaidBalance.Text = "0";
        _lblObjectName.Text = "";
        _gridDetail.DataSource = null;
        _loading = false;
        RecalcSummary();
        SetEditMode(false);
    }

    private void LocatePayment(long 副碼)
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

    private List<PaymentService.PaymentDetailRow> CollectDetails()
    {
        var list = new List<PaymentService.PaymentDetailRow>();
        if (_gridDetail.DataSource is not DataTable dt) return list;
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            var 沖 = Dec(r.Cells["沖帳金額"].Value);
            var 折讓 = dt.Columns.Contains("折讓金額") ? Dec(r.Cells["折讓金額"].Value) : 0m;
            if (沖 <= 0m) continue;
            list.Add(new PaymentService.PaymentDetailRow
            {
                交易單號 = Str(r.Cells["交易單號"].Value),
                單據類別 = Str(r.Cells["單據類別"].Value),
                交易日期 = Str(r.Cells["交易日期"].Value),
                未收付金額 = Dec(r.Cells["未收付金額"].Value),
                沖帳金額 = 沖,
                折讓金額 = 折讓,
            });
        }
        return list;
    }

    private void SetEditMode(bool editing)
    {
        _cmbKindMain.Enabled = editing;
        _cmbObject.Enabled = editing;
        _dtDate.Enabled = editing;
        _txtCash.Enabled = editing;
        _txtCheck.Enabled = editing;
        _txtPrepaidUse.Enabled = editing;
        _txtPrepaidAdd.Enabled = editing;
        _gridDetail.ReadOnly = !editing;
    }

    private string SelectedObject() => _cmbObject.SelectedValue as string ?? "";

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
}
