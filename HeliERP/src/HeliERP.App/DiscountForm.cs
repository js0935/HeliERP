// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（新增折讓作業）
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Printing;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>
/// 折讓作業：出貨折讓／進貨折讓單。折讓不異動庫存，僅沖減應收／應付帳款，
/// 並可供「報表列印 → 折讓」區段列印折讓單與折讓明細表。
/// 上區編輯折讓單（新增），下區為既有折讓單清單（檢視／刪除）。
/// </summary>
public sealed class DiscountForm : Form
{
    private ComboBox _cmbKind = null!;
    private TextBox _txtNo = null!;
    private DateTimePicker _dtpDate = null!;
    private DateTimePicker _dtpAccDate = null!;
    private ComboBox _cmbParty = null!;
    private ComboBox _cmbStaff = null!;
    private TextBox _txtRemark = null!;
    private DataGridView _gridDetail = null!;
    private DataGridView _gridList = null!;
    private TextBox _txtFilterNo = null!;

    private Label _lblTotal = null!;
    private Label _lblStatus = null!;

    private bool _loading;
    private bool _viewing;
    private long _viewingKey;

    public DiscountForm()
    {
        Text = "折讓作業";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        BackColor = UiTheme.Background;

        var header = UiTheme.BuildHeader("折讓作業", "出貨折讓／進貨折讓，沖減應收／應付帳款（不異動庫存）");
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
                DiscountService.EnsureDiscountSchema();
                _cmbKind.SelectedIndex = 0;
                _txtNo.Text = DiscountService.PreviewBillNo(_cmbKind.SelectedItem?.ToString() ?? "出貨折讓");
                LoadList();
                _lblStatus.Text = "狀態: 就緒";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "狀態: 載入失敗 - " + ex.Message;
            }
        };

        ShortcutHelper.Enable(this, onDelete: DeleteSelected, onSearch: LoadList);
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

        var btnSave = new ModernButton { Text = "儲存折讓單", Width = 140 };
        btnSave.Click += (s, e) => Save();
        var btnLoadView = new ModernButton { Text = "載入檢視", Width = 120, IsPrimary = false };
        btnLoadView.Click += (s, e) => LoadSelectedView();
        var btnDelete = new ModernButton { Text = "刪除折讓單", Width = 120, IsPrimary = false };
        btnDelete.Click += (s, e) => DeleteSelected();
        var btnPrint = new ModernButton { Text = "列印", Width = 120, IsPrimary = false };
        btnPrint.Click += (s, e) => PrintBill();
        var btnReload = new ModernButton { Text = "重讀", Width = 120, IsPrimary = false };
        btnReload.Click += (s, e) => { ClearEditor(); LoadList(); };
        var btnHelp = new ModernButton { Text = "說明", Width = 120, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show(
                "折讓作業功能說明：\n" +
                "1. 折讓為「價格調整」性質，不影響庫存（退貨請用出貨退回／進貨退出單）。\n" +
                "2. 出貨折讓沖減應收帳款；進貨折讓沖減應付帳款。\n" +
                "3. 明細輸入原貨單編號後自動帶入發票與金額供參考；折讓金額必填且大於 0。\n" +
                "4. 稅額依系統參數自動計算（銷項／進項稅率）。\n" +
                "5. 存檔後可由「報表列印 → 折讓」列印折讓單或折讓明細表。\n" +
                "6. 刪除會沖銷帳款；已被收付款沖帳的折讓單無法刪除。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 120, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSave); Add(btnLoadView); Add(btnDelete); Add(btnPrint); Add(btnReload); Add(btnHelp); Add(btnExit);

        Controls.Add(bar);
    }

    private void BuildMasterCard()
    {
        var card = new Panel { Dock = DockStyle.Top, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingSm) };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            AutoSize = true,
            BackColor = UiTheme.Card,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));

        _cmbKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        foreach (var k in DiscountService.Kinds)
            _cmbKind.Items.Add(k.Name);
        _cmbKind.SelectedIndexChanged += (s, e) =>
        {
            if (_loading) return;
            var kind = DiscountService.GetKind(_cmbKind.SelectedItem?.ToString() ?? "出貨折讓");
            LoadPartyCombo(kind.ObjectType);
            _txtNo.Text = DiscountService.PreviewBillNo(kind.Name);
        };
        UiTheme.StyleComboBox(_cmbKind);

        _txtNo = new TextBox { ReadOnly = true };
        UiTheme.StyleTextBox(_txtNo, true);

        _dtpDate = new DateTimePicker { Format = DateTimePickerFormat.Short };
        UiTheme.StyleDateTimePicker(_dtpDate);

        _dtpAccDate = new DateTimePicker { Format = DateTimePickerFormat.Short };
        UiTheme.StyleDateTimePicker(_dtpAccDate);

        _cmbParty = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        UiTheme.StyleComboBox(_cmbParty);

        _cmbStaff = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        UiTheme.StyleComboBox(_cmbStaff);

        _txtRemark = new TextBox();
        UiTheme.StyleTextBox(_txtRemark);

        var lblAccHint = new Label { Text = "帳款日期：", Font = UiTheme.Font(10F), ForeColor = UiTheme.TextSub, AutoSize = true };

        panel.Controls.Add(MakeField("單據類別：", _cmbKind), 0, 0);
        panel.Controls.Add(MakeField("折讓單號：", _txtNo), 2, 0);
        panel.Controls.Add(MakeField("折讓日期：", _dtpDate), 4, 0);
        panel.Controls.Add(MakeField("交易對象：", _cmbParty), 6, 0);
        panel.Controls.Add(MakeField("帳款日期：", _dtpAccDate), 0, 1);
        panel.Controls.Add(MakeField("業務人員：", _cmbStaff), 2, 1);
        panel.Controls.Add(MakeField("備註：", _txtRemark), 4, 1);
        panel.SetColumnSpan(panel.Controls[panel.Controls.Count - 1], 3);

        card.Controls.Add(panel);
        Controls.Add(card);
    }

    private Control MakeField(string label, Control field)
    {
        var wrap = new Panel { AutoSize = true, BackColor = UiTheme.Card, Padding = new Padding(0, 0, UiTheme.SpacingMd, 0) };
        wrap.Controls.Add(new Label
        {
            Text = label,
            Font = UiTheme.Font(10F, FontStyle.Bold),
            ForeColor = UiTheme.TextMain,
            AutoSize = true,
            Location = new Point(0, 8),
        });
        field.Location = new Point(field.Width > 60 ? 76 : 68, 4);
        wrap.Controls.Add(field);
        wrap.Height = 34;
        return wrap;
    }

    private void BuildDetailCard()
    {
        var card = new Panel { Dock = DockStyle.Top, Height = 300, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingSm) };
        var lbl = new Label
        {
            Text = "折讓明細",
            Font = UiTheme.Font(12F, FontStyle.Bold),
            ForeColor = UiTheme.Primary,
            AutoSize = true,
            Location = new Point(UiTheme.SpacingSm, 2),
        };
        card.Controls.Add(lbl);

        _gridDetail = new DataGridView
        {
            Location = new Point(UiTheme.SpacingSm, 30),
            Size = new Size(1100, 254),
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            MultiSelect = false,
        };
        UiTheme.StyleDataGridView(_gridDetail);
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "貨單編號", HeaderText = "原貨單編號", Width = 130 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "發票編號", HeaderText = "發票編號", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "發票日期", HeaderText = "發票日期", Width = 90 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單據金額", HeaderText = "單據金額", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單據稅金", HeaderText = "單據稅金", Width = 100 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "折讓金額", HeaderText = "折讓金額", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "折扣金額", HeaderText = "折扣金額", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "附註", HeaderText = "附註", Width = 260 });
        _gridDetail.Columns["單據金額"].DefaultCellStyle.Format = "N2";
        _gridDetail.Columns["單據稅金"].DefaultCellStyle.Format = "N2";
        _gridDetail.Columns["折讓金額"].DefaultCellStyle.Format = "N2";
        _gridDetail.Columns["折扣金額"].DefaultCellStyle.Format = "N2";
        _gridDetail.Columns["貨單編號"].DefaultCellStyle.BackColor = UiTheme.FocusBack;
        _gridDetail.CellEndEdit += OnDetailCellEdited;
        _gridDetail.EditingControlShowing += (s, e) =>
        {
            if (e.Control is TextBox tb && _gridDetail.CurrentCell is { } cell &&
                cell.OwningColumn.Name == "貨單編號")
                tb.KeyDown += DetailBillNoKeyDown;
        };
        card.Controls.Add(_gridDetail);

        _lblTotal = new Label
        {
            Text = "折讓金額合計: 0",
            Font = UiTheme.Font(10.5F, FontStyle.Bold),
            ForeColor = UiTheme.PrimaryDark,
            AutoSize = true,
            Location = new Point(UiTheme.SpacingSm, 288),
        };
        card.Controls.Add(_lblTotal);
        Controls.Add(card);
    }

    private void DetailBillNoKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        if (_gridDetail.CurrentCell is { } cell && cell.RowIndex >= 0)
            FillBillInfo(cell.RowIndex);
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void OnDetailCellEdited(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || _viewing || e.RowIndex < 0) return;
        var row = _gridDetail.Rows[e.RowIndex];
        if (e.ColumnIndex == _gridDetail.Columns["貨單編號"].Index)
            FillBillInfo(e.RowIndex);
        else if (e.ColumnIndex == _gridDetail.Columns["折讓金額"].Index ||
                 e.ColumnIndex == _gridDetail.Columns["折扣金額"].Index)
            RecalcTotal();
    }

    /// <summary>依原貨單編號帶入發票與金額（僅填空白欄，不覆蓋使用者輸入）。</summary>
    private void FillBillInfo(int rowIndex)
    {
        var row = _gridDetail.Rows[rowIndex];
        var no = (row.Cells["貨單編號"].Value as string ?? "").Trim();
        if (no.Length == 0) return;
        var info = DiscountService.LookupBillForDiscount(no);
        if (info is null)
        {
            MessageBox.Show($"找不到交易單號「{no}」的資料。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _loading = true;
        if (row.Cells["發票編號"].Value is null or "")
            row.Cells["發票編號"].Value = info.TryGetValue("發票號碼", out var inv) ? Str(inv) : "";
        if (row.Cells["發票日期"].Value is null or "")
            row.Cells["發票日期"].Value = info.TryGetValue("交易日期", out var d) ? Str(d) : "";
        if (row.Cells["單據金額"].Value is null or "")
            row.Cells["單據金額"].Value = info.TryGetValue("總計金額", out var amt) ? amt : 0m;
        if (row.Cells["單據稅金"].Value is null or "")
            row.Cells["單據稅金"].Value = info.TryGetValue("營業稅", out var tax) ? tax : 0m;
        _loading = false;
    }

    private void RecalcTotal()
    {
        decimal total = 0m;
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            total += Dec(r.Cells["折讓金額"].Value);
        }
        _lblTotal.Text = $"折讓金額合計: {total:N2}";
    }

    private void BuildListCard()
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingSm) };
        var filterRow = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = UiTheme.Card };

        var lblFilter = new Label
        {
            Text = "篩選單號：",
            Font = UiTheme.Font(10F),
            ForeColor = UiTheme.TextSub,
            AutoSize = true,
            Location = new Point(4, 12),
        };
        _txtFilterNo = new TextBox { Location = new Point(76, 8), Width = 200 };
        UiTheme.StyleTextBox(_txtFilterNo);
        var btnSearch = new ModernButton
        {
            Text = "查詢",
            Width = 90,
            Height = 34,
            Location = new Point(288, 4),
            IsPrimary = false,
            DrawShadow = false,
        };
        btnSearch.Click += (s, e) => LoadList();
        filterRow.Controls.Add(lblFilter);
        filterRow.Controls.Add(_txtFilterNo);
        filterRow.Controls.Add(btnSearch);
        card.Controls.Add(filterRow);

        _gridList = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        UiTheme.StyleDataGridView(_gridList);
        card.Controls.Add(_gridList);
        Controls.Add(card);
    }

    private void BuildStatusBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = UiTheme.PrimaryDark };
        _lblStatus = new Label
        {
            Text = "狀態: 就緒",
            Font = UiTheme.Font(9.5F),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(UiTheme.SpacingMd, 6),
        };
        bar.Controls.Add(_lblStatus);
        Controls.Add(bar);
    }

    // ==================== 資料載入 ====================

    private void LoadPartyCombo(string 客廠類別)
    {
        _loading = true;
        _cmbParty.DataSource = null;
        _cmbParty.DisplayMember = "";
        _cmbParty.ValueMember = "";
        var dt = TradeService.LoadCustomerCombo(客廠類別);
        _cmbParty.DataSource = dt;
        _cmbParty.DisplayMember = "公司簡稱";
        _cmbParty.ValueMember = "客廠編號";
        _loading = false;
    }

    private void LoadStaffCombo()
    {
        _loading = true;
        var dt = TradeService.LoadStaffCombo();
        _cmbStaff.DataSource = dt;
        _cmbStaff.DisplayMember = "員工姓名";
        _cmbStaff.ValueMember = "員工編號";
        _loading = false;
    }

    private void LoadList()
    {
        var kind = _cmbKind.SelectedItem?.ToString() ?? "";
        string filter = _txtFilterNo.Text.Trim();
        string where = kind.Length > 0 ? "[單據類別] = $k" : "1=1";
        var pars = new List<Microsoft.Data.Sqlite.SqliteParameter>();
        if (kind.Length > 0) pars.Add(DbManager.Param("$k", kind));
        if (filter.Length > 0)
        {
            where += " AND [折讓單號] LIKE $f";
            pars.Add(DbManager.Param("$f", "%" + filter + "%"));
        }
        var dt = DbManager.QueryTable(
            "SELECT [單據副碼],[折讓單號],[折讓日期],[對象編號],[總計金額],[備註] " +
            "FROM [折讓主檔] WHERE " + where + " ORDER BY [折讓日期] DESC, [折讓單號] DESC",
            pars.ToArray());
        _loading = true;
        _gridList.DataSource = dt;
        if (_gridList.Columns.Contains("總計金額"))
            _gridList.Columns["總計金額"].DefaultCellStyle.Format = "N2";
        _loading = false;
    }

    private void Save()
    {
        if (_viewing)
        {
            MessageBox.Show("目前為檢視模式（已載入既有折讓單），請按「重讀」開始新單。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_cmbParty.SelectedValue is not string party || party.Length == 0)
        {
            MessageBox.Show("請選擇交易對象。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var req = new DiscountService.SaveDiscountRequest
        {
            單據類別 = _cmbKind.SelectedItem?.ToString() ?? "出貨折讓",
            折讓日期 = _dtpDate.Value.Date,
            帳款日期 = _dtpAccDate.Value.Date,
            交易對象 = party,
            員工編號 = _cmbStaff.SelectedValue is string s ? s : "",
            備註 = _txtRemark.Text.Trim(),
        };
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            var row = new DiscountService.DiscountDetailRow
            {
                貨單編號 = Str(r.Cells["貨單編號"].Value),
                發票編號 = Str(r.Cells["發票編號"].Value),
                發票日期 = Str(r.Cells["發票日期"].Value),
                單據金額 = Dec(r.Cells["單據金額"].Value),
                單據稅金 = Dec(r.Cells["單據稅金"].Value),
                折讓金額 = Dec(r.Cells["折讓金額"].Value),
                折扣金額 = Dec(r.Cells["折扣金額"].Value),
                附註 = Str(r.Cells["附註"].Value),
            };
            if (row.折讓金額 <= 0 && row.折扣金額 <= 0) continue;
            req.明細.Add(row);
        }
        try
        {
            var result = DiscountService.SaveDiscount(req);
            MessageBox.Show($"折讓單「{result.折讓單號}」已儲存，帳款已沖減。", "完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearEditor();
            LoadList();
            _lblStatus.Text = $"狀態: 已儲存 {result.折讓單號}";
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
        _dtpDate.Value = DateTime.Now;
        _dtpAccDate.Value = DateTime.Now;
        _txtNo.Text = DiscountService.PreviewBillNo(_cmbKind.SelectedItem?.ToString() ?? "出貨折讓");
        _gridDetail.Rows.Clear();
        RecalcTotal();
        _lblStatus.Text = "狀態: 就緒";
    }

    private void LoadSelectedView()
    {
        if (_gridList.SelectedRows.Count == 0 || _gridList.SelectedRows[0].IsNewRow)
        {
            MessageBox.Show("請先於清單選取一筆折讓單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var row = _gridList.SelectedRows[0];
        long 副碼 = Convert.ToInt64(row.Cells["單據副碼"].Value);

        DiscountService.EnsureDiscountSchema();
        var m = DbManager.QueryTable(
            "SELECT m.*, COALESCE(c.[公司簡稱],'') AS [對象名稱] FROM [折讓主檔] m " +
            "LEFT JOIN [客戶廠商] c ON m.[對象編號] = c.[客廠編號] WHERE m.[單據副碼] = $c",
            DbManager.Param("$c", 副碼));
        if (m.Rows.Count == 0)
        {
            MessageBox.Show("找不到該折讓單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var master = m.Rows[0];

        _loading = true;
        _viewing = true;
        _viewingKey = 副碼;
        _cmbKind.SelectedItem = Str(master["單據類別"]);
        _txtNo.Text = Str(master["折讓單號"]);
        _dtpDate.Value = DateTime.TryParse(Str(master["折讓日期"]), out var d) ? d : DateTime.Now;
        _dtpAccDate.Value = _dtpDate.Value;
        _cmbParty.SelectedValue = master["對象編號"] is DBNull or null ? null : master["對象編號"].ToString();
        _cmbStaff.SelectedValue = master["員編編號"] is DBNull or null ? null : master["員編編號"].ToString();
        _txtRemark.Text = Str(master["備註"]);
        _loading = false;

        var dt = DbManager.QueryTable(
            "SELECT * FROM [折讓明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
            DbManager.Param("$c", 副碼));
        _gridDetail.Rows.Clear();
        foreach (DataRow r in dt.Rows)
        {
            int i = _gridDetail.Rows.Add();
            var gr = _gridDetail.Rows[i];
            gr.Cells["貨單編號"].Value = Str(r["貨單編號"]);
            gr.Cells["發票編號"].Value = Str(r["發票編號"]);
            gr.Cells["發票日期"].Value = Str(r["發票日期"]);
            gr.Cells["單據金額"].Value = r["單據金額"];
            gr.Cells["單據稅金"].Value = r["單據稅金"];
            gr.Cells["折讓金額"].Value = r["折讓金額"];
            gr.Cells["折扣金額"].Value = r["折扣金額"];
            gr.Cells["附註"].Value = Str(r["附註"]);
        }
        RecalcTotal();
        _lblStatus.Text = $"狀態: 檢視 {_txtNo.Text}（刪除請用工具列按鈕）";
    }

    private void DeleteSelected()
    {
        if (_gridList.SelectedRows.Count == 0 || _gridList.SelectedRows[0].IsNewRow)
        {
            MessageBox.Show("請先於清單選取一筆折讓單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var row = _gridList.SelectedRows[0];
        string 單號 = Str(row.Cells["折讓單號"].Value);
        long 副碼 = Convert.ToInt64(row.Cells["單據副碼"].Value);
        var confirm = MessageBox.Show($"確定刪除折讓單「{單號}」？刪除後將沖銷帳款影響。",
            "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        try
        {
            DiscountService.DeleteDiscount(副碼);
            MessageBox.Show($"折讓單「{單號}」已刪除，帳款已沖銷。", "完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("請先於清單選取並載入一筆折讓單，再按列印。", "列印",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string kind = _cmbKind.SelectedItem?.ToString() ?? "出貨折讓";
        string rtmFile = kind == "進貨折讓" ? "進貨折讓單.rtm" : "出貨折讓單.rtm";
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
        var data = BuildRtmData();

        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument
        {
            DocumentName = $"{rtmFile}-{_viewingKey}",
        };
        doc.DefaultPageSettings.PaperSize = new PaperSize("A4",
            (int)Math.Round(report.MmPaperWidth / 25.4 * 100),
            (int)Math.Round(report.MmPaperHeight / 25.4 * 100));
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
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

    /// <summary>建立折讓單報表資料：主檔（ppDBPipeline1）＋公司（plCompany）＋明細（ppDBPipeline2）。</summary>
    private RtmData BuildRtmData()
    {
        var data = new RtmData();

        var dt = DbManager.QueryTable(
            "SELECT m.[折讓單號], m.[折讓日期] AS [交易日期], " +
            "COALESCE(c.[公司全名],'') AS [對象名稱], COALESCE(c.[送貨地址],'') AS [送貨地址], " +
            "COALESCE(c.[聯絡人一],'') AS [聯絡人一], COALESCE(c.[聯絡電話一],'') AS [聯絡電話一], " +
            "COALESCE(c.[統一編號],'') AS [統一編號], COALESCE(c.[傳真號碼],'') AS [傳真號碼], " +
            "COALESCE(e.[員工姓名],'') AS [員工姓名], " +
            "COALESCE(m.[淨計金額],0) AS [合計金額], COALESCE(m.[稅額合計],0) AS [稅金合計], " +
            "COALESCE(m.[折讓金額],0) AS [折讓金額], COALESCE(m.[退稅],0) AS [扣抵稅額], " +
            "COALESCE(m.[總計金額],0) AS [總計金額] " +
            "FROM [折讓主檔] m " +
            "LEFT JOIN [客戶廠商] c ON m.[對象編號] = c.[客廠編號] " +
            "LEFT JOIN [員工資料] e ON m.[員編編號] = e.[員工編號] " +
            "WHERE m.[單據副碼] = $c",
            DbManager.Param("$c", _viewingKey));
        if (dt.Rows.Count > 0)
            foreach (DataColumn col in dt.Columns)
                data.Master[col.ColumnName] = dt.Rows[0][col];

        var company = new CompanyInfo();
        data.Company["公司全名"] = company.CompanyName;
        data.Company["電話號碼"] = company.Phone;
        data.Company["登記地址"] = company.Address;
        data.Company["傳真號碼"] = LookupCompanyFax(company.CompanyName);

        var detailDt = DbManager.QueryTable(
            "SELECT [貨單編號], [發票編號], [發票日期], [單據金額], [單據稅金], " +
            "[單據折讓], [折扣稅額], [附註] FROM [折讓明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
            DbManager.Param("$c", _viewingKey));
        foreach (DataRow dr in detailDt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in detailDt.Columns)
                d[col.ColumnName] = dr[col];
            data.Detail.Add(d);
        }
        return data;
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
