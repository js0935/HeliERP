// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.2.0（改為查詢式版面：全螢幕清單＋彈出編輯框）
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Printing;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>
/// 折讓作業：出貨折讓／進貨折讓單。折讓不異動庫存，僅沖減應收／應付帳款，
/// 並可供「報表列印 → 折讓」區段列印折讓單與折讓明細表。
/// 查詢式版面：頂部工具列＋篩選列，中央為折讓單清單，彈出式編輯框輸入。
/// </summary>
public sealed class DiscountForm : Form
{
    private readonly ComboBox _cmbKind = new();
    private readonly DateTimePicker _dtFrom = new(), _dtTo = new();
    private readonly TextBox _txtKeyword = new();
    private readonly DataGridView _grid = new();
    private readonly ToolStripStatusLabel _lblCount = new(), _lblTotal = new();

    public DiscountForm()
    {
        Text = "折讓作業";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 660);
        BackColor = UiTheme.Background;

        Controls.Add(UiTheme.BuildHeader("折讓作業", "出貨折讓／進貨折讓，沖減應收／應付帳款（不異動庫存）"));

        BuildToolbar();
        BuildFilterBar();
        BuildGrid();
        BuildStatusBar();

        _cmbKind.SelectedIndex = 0;
        _dtFrom.Value = new DateTime(DateTime.Today.Year, 1, 1);
        _dtTo.Value = DateTime.Today.AddYears(1);
        DiscountService.EnsureDiscountSchema();
        LoadList();

        ShortcutHelper.Enable(this, onDelete: DeleteSelected, onSearch: LoadList, onReload: LoadList);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    // ==================== UI ====================

    private void BuildToolbar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 52 };
        UiTheme.StyleTopBar(bar);
        int x = UiTheme.SpacingMd;
        void Add(ModernButton b) { b.Location = new Point(x, 6); b.Height = 40; b.DrawShadow = false; bar.Controls.Add(b); x += b.Width + UiTheme.SpacingSm; }
        void Sep() { bar.Controls.Add(new Panel { Location = new Point(x, 10), Size = new Size(2, 32), BackColor = UiTheme.Border }); x += UiTheme.SpacingSm + 2; }

        var btnSearch = new ModernButton { Text = "搜尋", Width = 110 };
        btnSearch.Click += (s, e) => { LoadList(); _txtKeyword.Focus(); };
        var btnNew = new ModernButton { Text = "新增折讓單", Width = 130 };
        btnNew.Click += (s, e) => EditBill(null);
        var btnEdit = new ModernButton { Text = "修改", Width = 100, IsPrimary = false };
        btnEdit.Click += (s, e) => EditBill(GetSelectedRow());
        var btnView = new ModernButton { Text = "開啟檢視", Width = 110, IsPrimary = false };
        btnView.Click += (s, e) => ViewSelected();
        var btnDel = new ModernButton { Text = "刪除", Width = 100, IsPrimary = false };
        btnDel.Click += (s, e) => DeleteSelected();
        Sep();
        var btnPrint = new ModernButton { Text = "列印", Width = 110, IsPrimary = false };
        btnPrint.Click += (s, e) => PrintBill();
        Sep();
        var btnHelp = new ModernButton { Text = "說明", Width = 100, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show(
                "折讓作業功能說明：\n" +
                "1. 折讓為「價格調整」性質，不影響庫存（退貨請用出貨退回／進貨退出單）。\n" +
                "2. 出貨折讓沖減應收帳款；進貨折讓沖減應付帳款。\n" +
                "3. 明細輸入原貨單編號後自動帶入發票與金額供參考；折讓金額必填且大於 0。\n" +
                "4. 稅額依系統參數自動計算（銷項／進項稅率）。\n" +
                "5. 刪除會沖銷帳款；已被收付款沖帳的折讓單無法刪除。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 100, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSearch); Add(btnNew); Add(btnEdit); Add(btnView); Add(btnDel);
        Sep();
        Add(btnPrint);
        Sep();
        Add(btnHelp); Add(btnExit);

        Controls.Add(bar);
    }

    private void BuildFilterBar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.Background, Padding = new Padding(UiTheme.SpacingMd, 10, UiTheme.SpacingMd, 8) };
        int x = UiTheme.SpacingMd;
        void Field(string label, Control c, int w)
        {
            bar.Controls.Add(new Label { Text = label, Font = UiTheme.Font(10F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(x, 18) });
            x += 68;
            c.Location = new Point(x, 12);
            c.Width = w;
            bar.Controls.Add(c);
            x += w + UiTheme.SpacingLg;
        }

        _cmbKind.Items.AddRange(new object[] { "全部類別" }.Concat(DiscountService.Kinds.Select(k => k.Name)).ToArray());
        _dtFrom.Format = _dtTo.Format = DateTimePickerFormat.Short;
        _txtKeyword.PlaceholderText = "折讓單號";

        Field("類別", _cmbKind, 120);
        Field("單號", _txtKeyword, 200);
        Field("日期從", _dtFrom, 110);
        Field("至", _dtTo, 110);
        bar.Controls.Add(new Label { Text = "（單號留空 = 全部）", Font = UiTheme.Font(9F), ForeColor = UiTheme.TextFaint, AutoSize = true, Location = new Point(x + 6, 18) });
        Controls.Add(bar);
    }

    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        UiTheme.StyleDataGridView(_grid);
        _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ViewSelected(); };
        Controls.Add(_grid);
    }

    private void BuildStatusBar()
    {
        var bar = new StatusStrip { SizingGrip = false, BackColor = UiTheme.Card, Padding = new Padding(12, 2, 8, 2) };
        _lblCount.Text = "共 0 筆";
        _lblTotal.Text = "";
        bar.Items.Add(_lblCount);
        bar.Items.Add(new ToolStripStatusLabel("  |  "));
        bar.Items.Add(_lblTotal);
        Controls.Add(bar);
    }

    // ==================== 資料 ====================

    private DataRow? GetSelectedRow()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.DataBoundItem is not DataRowView drv)
            return null;
        return drv.Row;
    }

    private void LoadList()
    {
        string kind = _cmbKind.SelectedIndex > 0 ? _cmbKind.SelectedItem?.ToString() ?? "" : "";
        string filter = _txtKeyword.Text.Trim();
        string from = _dtFrom.Value.ToString("yyyy-MM-dd 00:00:00");
        string to = _dtTo.Value.ToString("yyyy-MM-dd 23:59:59");

        var where = new List<string> { "1=1" };
        var pars = new List<Microsoft.Data.Sqlite.SqliteParameter>();
        if (kind.Length > 0)
        {
            where.Add("[m].[單據類別] = $k");
            pars.Add(DbManager.Param("$k", kind));
        }
        if (filter.Length > 0)
        {
            where.Add("[m].[折讓單號] LIKE $f");
            pars.Add(DbManager.Param("$f", "%" + filter + "%"));
        }
        where.Add("[m].[折讓日期] >= $from AND [m].[折讓日期] <= $to");
        pars.Add(DbManager.Param("$from", from));
        pars.Add(DbManager.Param("$to", to));

        var dt = DbManager.QueryTable(
            "SELECT [m].[單據副碼],[m].[折讓單號],[m].[單據類別],[m].[折讓日期]," +
            "COALESCE([c].[公司簡稱],'') AS [對象名稱],COALESCE([m].[總計金額],0) AS [總計金額]," +
            "COALESCE([m].[備註],'') AS [備註] " +
            "FROM [折讓主檔] m LEFT JOIN [客戶廠商] c ON m.[對象編號] = c.[客廠編號] " +
            "WHERE " + string.Join(" AND ", where) + " ORDER BY [m].[折讓日期] DESC, [m].[折讓單號] DESC",
            pars.ToArray());
        _grid.DataSource = dt;
        _grid.Columns["單據副碼"].Visible = false;
        if (_grid.Columns.Count > 0)
        {
            _grid.Columns["折讓單號"].Width = 130;
            _grid.Columns["單據類別"].Width = 90;
            _grid.Columns["折讓日期"].Width = 110;
            _grid.Columns["對象名稱"].Width = 150;
            _grid.Columns["總計金額"].Width = 120;
            _grid.Columns["總計金額"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["總計金額"].DefaultCellStyle.Format = "N2";
            _grid.Columns["備註"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }
        _lblCount.Text = $"共 {dt.Rows.Count} 筆";
        var total = dt.AsEnumerable().Sum(r => Convert.ToDecimal(r["總計金額"]));
        _lblTotal.Text = $"總計金額合計：{total:N0}";
    }

    private void EditBill(DataRow? row)
    {
        long? 副碼 = null;
        if (row is not null)
            副碼 = Convert.ToInt64(row["單據副碼"]);
        var result = DiscountEditDialog.Show(this, 副碼);
        if (result is null)
            return;
        try
        {
            var req = new DiscountService.SaveDiscountRequest
            {
                單據副碼 = 副碼,
                單據類別 = result.單據類別,
                折讓日期 = result.折讓日期,
                帳款日期 = result.帳款日期,
                交易對象 = result.交易對象,
                員工編號 = result.員工編號,
                備註 = result.備註,
                明細 = result.明細,
            };
            var saved = DiscountService.SaveDiscount(req);
            MessageBox.Show($"折讓單「{saved.折讓單號}」已儲存，帳款已沖減。", "完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ViewSelected()
    {
        var row = GetSelectedRow();
        if (row is null)
        {
            MessageBox.Show("請先於清單選取一筆折讓單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        DiscountEditDialog.ShowView(this, Convert.ToInt64(row["單據副碼"]));
    }

    private void DeleteSelected()
    {
        var row = GetSelectedRow();
        if (row is null)
        {
            MessageBox.Show("請先於清單選取一筆折讓單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string 單號 = Convert.ToString(row["折讓單號"]) ?? "";
        long 副碼 = Convert.ToInt64(row["單據副碼"]);
        var confirm = MessageBox.Show($"確定刪除折讓單「{單號}」？刪除後將沖銷帳款影響。",
            "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        try
        {
            DiscountService.DeleteDiscount(副碼);
            MessageBox.Show($"折讓單「{單號}」已刪除，帳款已沖銷。", "完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刪除失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ==================== 列印 ====================

    private static string ReportDir => ReportPrintService.RepDirectory;

    private void PrintBill()
    {
        var row = GetSelectedRow();
        if (row is null)
        {
            MessageBox.Show("請先於清單選取一筆折讓單，再按列印。", "列印",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        long 副碼 = Convert.ToInt64(row["單據副碼"]);
        string kind = Convert.ToString(row["單據類別"]) ?? "出貨折讓";
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
        var data = BuildRtmData(副碼);

        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument
        {
            DocumentName = $"{rtmFile}-{副碼}",
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
    private static RtmData BuildRtmData(long 副碼)
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
            DbManager.Param("$c", 副碼));
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
            DbManager.Param("$c", 副碼));
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

// ==================== 彈出式編輯框 ====================

/// <summary>折讓單編輯框：新增／修改（全單重算）／檢視（唯讀）。</summary>
public sealed class DiscountEditDialog : Form
{
    private readonly ComboBox _cmbKind = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtNo = new() { ReadOnly = true, BackColor = UiTheme.BorderLight };
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker _dtpAccDate = new() { Format = DateTimePickerFormat.Short };
    private readonly ComboBox _cmbParty = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox _cmbStaff = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly TextBox _txtRemark = new();
    private readonly DataGridView _gridDetail = new();
    private readonly Label _lblTotal = new();
    private readonly bool _readOnly;
    private readonly long? _副碼;
    private bool _loading;

    public sealed record Result(
        string 單據類別, DateTime 折讓日期, DateTime 帳款日期,
        string 交易對象, string 員工編號, string 備註,
        List<DiscountService.DiscountDetailRow> 明細);

    private DiscountEditDialog(long? 副碼, bool readOnly)
    {
        _副碼 = 副碼;
        _readOnly = readOnly;
        Text = readOnly ? "檢視折讓單" : (副碼 is null ? "新增折讓單" : "修改折讓單");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UiTheme.Background;
        Font = UiTheme.Font(10F);
        ClientSize = new Size(1020, 600);

        foreach (var k in DiscountService.Kinds)
            _cmbKind.Items.Add(k.Name);

        int y = 18;
        void Field(string label, Control c, int x)
        {
            Controls.Add(new Label { Text = label + "：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(x, y + 4) });
            c.Location = new Point(x + 76, y);
            Controls.Add(c);
        }

        _txtNo.Width = 140;
        _cmbKind.Width = 120;
        _dtpDate.Width = 120;
        _dtpAccDate.Width = 120;
        _txtRemark.Width = 260;
        Field("單據類別", _cmbKind, 20);
        Field("折讓單號", _txtNo, 200);
        Field("折讓日期", _dtpDate, 360);
        Field("帳款日期", _dtpAccDate, 540);
        Field("交易對象", _cmbParty, 700);
        y = 56;
        Field("業務人員", _cmbStaff, 20);
        Field("備註", _txtRemark, 220);

        _cmbKind.SelectedIndexChanged += (s, e) =>
        {
            if (_loading || _readOnly || _副碼 is not null) return;
            var kind = DiscountService.GetKind(_cmbKind.SelectedItem?.ToString() ?? "出貨折讓");
            LoadPartyCombo(kind.ObjectType);
            _txtNo.Text = DiscountService.PreviewBillNo(kind.Name);
        };
        LoadStaffCombo();

        _gridDetail.Location = new Point(20, 96);
        _gridDetail.Size = new Size(980, 400);
        _gridDetail.RowHeadersVisible = false;
        _gridDetail.AllowUserToAddRows = _readOnly ? false : true;
        _gridDetail.AllowUserToDeleteRows = _readOnly ? false : true;
        _gridDetail.MultiSelect = false;
        _gridDetail.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _gridDetail.RowTemplate.Height = 30;
        UiTheme.StyleDataGridView(_gridDetail);
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "貨單編號", HeaderText = "原貨單編號", Width = 130 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "發票編號", HeaderText = "發票編號", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "發票日期", HeaderText = "發票日期", Width = 90 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單據金額", HeaderText = "單據金額", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單據稅金", HeaderText = "單據稅金", Width = 100 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "折讓金額", HeaderText = "折讓金額", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "折扣金額", HeaderText = "折扣金額", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "附註", HeaderText = "附註", Width = 140 });
        _gridDetail.Columns["單據金額"].DefaultCellStyle.Format = "N2";
        _gridDetail.Columns["單據稅金"].DefaultCellStyle.Format = "N2";
        _gridDetail.Columns["折讓金額"].DefaultCellStyle.Format = "N2";
        _gridDetail.Columns["折扣金額"].DefaultCellStyle.Format = "N2";
        _gridDetail.Columns["貨單編號"].DefaultCellStyle.BackColor = UiTheme.FocusBack;
        _gridDetail.CellEndEdit += OnDetailCellEdited;
        _gridDetail.EditingControlShowing += (s, e) =>
        {
            if (e.Control is TextBox tb && _gridDetail.CurrentCell is { } cell &&
                cell.OwningColumn.Name == "貨單編號" && !_readOnly)
                tb.KeyDown += DetailBillNoKeyDown;
        };

        _lblTotal.Text = "折讓金額合計: 0";
        _lblTotal.AutoSize = true;
        _lblTotal.ForeColor = UiTheme.Primary;
        _lblTotal.Font = UiTheme.Font(10.5F, FontStyle.Bold);
        _lblTotal.Location = new Point(20, 506);

        var btnOk = new ModernButton { Text = readOnly ? "關閉" : "確定", Size = new Size(96, 40), Location = new Point(1020 - 250, 540), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(96, 40), Location = new Point(1020 - 140, 540), IsPrimary = false, DrawShadow = false };
        btnOk.Click += (s, e) => Finish();
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        btnOk.Enabled = !_readOnly;

        Controls.AddRange(new Control[] { _gridDetail, _lblTotal, btnOk, btnCancel });

        UiTheme.ScaleForDpi(this);
        UiTheme.ClampToScreen(this);
    }

    /// <summary>新增或修改（副碼非 null）模式。</summary>
    public static Result? Show(Form owner, long? 副碼)
    {
        using var dlg = new DiscountEditDialog(副碼, readOnly: false);
        dlg._loading = true;
        if (副碼 is null)
        {
            dlg._cmbKind.SelectedIndex = 0;
            var kind = DiscountService.GetKind(dlg._cmbKind.SelectedItem?.ToString() ?? "出貨折讓");
            dlg.LoadPartyCombo(kind.ObjectType);
            dlg._txtNo.Text = DiscountService.PreviewBillNo(kind.Name);
        }
        else
        {
            var m = DbManager.QueryTable(
                "SELECT m.*, COALESCE(c.[公司簡稱],'') AS [對象名稱] FROM [折讓主檔] m " +
                "LEFT JOIN [客戶廠商] c ON m.[對象編號] = c.[客廠編號] WHERE m.[單據副碼] = $c",
                DbManager.Param("$c", 副碼.Value));
            if (m.Rows.Count == 0)
            {
                MessageBox.Show("找不到該折讓單，可能已被刪除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            var master = m.Rows[0];
            dlg._cmbKind.SelectedItem = Convert.ToString(master["單據類別"]);
            dlg.LoadPartyCombo(DiscountService.GetKind(dlg._cmbKind.SelectedItem?.ToString() ?? "出貨折讓").ObjectType);
            dlg._txtNo.Text = Convert.ToString(master["折讓單號"]);
            if (DateTime.TryParse(Convert.ToString(master["折讓日期"]), out var d)) dlg._dtpDate.Value = d;
            if (master.Table.Columns.Contains("帳款日期") && DateTime.TryParse(Convert.ToString(master["帳款日期"]), out var ad)) dlg._dtpAccDate.Value = ad;
            else dlg._dtpAccDate.Value = dlg._dtpDate.Value;
            dlg._cmbParty.SelectedValue = master["對象編號"] is DBNull or null ? null : master["對象編號"].ToString();
            dlg._cmbStaff.SelectedValue = master["員編編號"] is DBNull or null ? null : master["員編編號"].ToString();
            dlg._txtRemark.Text = Convert.ToString(master["備註"]);
            dlg.LoadDetails(副碼.Value);
        }
        dlg._loading = false;
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.BuildResult() : null;
    }

    /// <summary>檢視模式（唯讀）。</summary>
    public static void ShowView(Form owner, long 副碼)
    {
        using var dlg = new DiscountEditDialog(副碼, readOnly: true);
        var m = DbManager.QueryTable(
            "SELECT m.*, COALESCE(c.[公司簡稱],'') AS [對象名稱] FROM [折讓主檔] m " +
            "LEFT JOIN [客戶廠商] c ON m.[對象編號] = c.[客廠編號] WHERE m.[單據副碼] = $c",
            DbManager.Param("$c", 副碼));
        if (m.Rows.Count == 0)
        {
            MessageBox.Show("找不到該折讓單，可能已被刪除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var master = m.Rows[0];
        dlg._cmbKind.SelectedItem = Convert.ToString(master["單據類別"]);
        dlg.LoadPartyCombo(DiscountService.GetKind(dlg._cmbKind.SelectedItem?.ToString() ?? "出貨折讓").ObjectType);
        dlg._txtNo.Text = Convert.ToString(master["折讓單號"]);
        if (DateTime.TryParse(Convert.ToString(master["折讓日期"]), out var d)) dlg._dtpDate.Value = d;
        if (master.Table.Columns.Contains("帳款日期") && DateTime.TryParse(Convert.ToString(master["帳款日期"]), out var ad)) dlg._dtpAccDate.Value = ad;
        else dlg._dtpAccDate.Value = dlg._dtpDate.Value;
        dlg._cmbParty.SelectedValue = master["對象編號"] is DBNull or null ? null : master["對象編號"].ToString();
        dlg._cmbStaff.SelectedValue = master["員編編號"] is DBNull or null ? null : master["員編編號"].ToString();
        dlg._txtRemark.Text = Convert.ToString(master["備註"]);
        dlg.LoadDetails(副碼);
        dlg.ShowDialog(owner);
    }

    private void LoadDetails(long 副碼)
    {
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
    }

    private Result BuildResult()
    {
        var rows = new List<DiscountService.DiscountDetailRow>();
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
            rows.Add(row);
        }
        return new Result(
            _cmbKind.SelectedItem?.ToString() ?? "出貨折讓",
            _dtpDate.Value.Date,
            _dtpAccDate.Value.Date,
            _cmbParty.SelectedValue is string p ? p : "",
            _cmbStaff.SelectedValue is string s ? s : "",
            _txtRemark.Text.Trim(),
            rows);
    }

    private void Finish()
    {
        if (_cmbParty.SelectedValue is not string party || party.Length == 0)
        {
            MessageBox.Show(this, "請選擇交易對象。", "請修正", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (BuildResult().明細.Count == 0)
        {
            MessageBox.Show(this, "請至少輸入一筆明細。", "請修正", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        DialogResult = DialogResult.OK;
    }

    private void LoadPartyCombo(string 客廠類別)
    {
        _loading = true;
        _cmbParty.DataSource = null;
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
        if (_loading || _readOnly || e.RowIndex < 0) return;
        var row = _gridDetail.Rows[e.RowIndex];
        if (e.ColumnIndex == _gridDetail.Columns["貨單編號"].Index)
            FillBillInfo(e.RowIndex);
        else if (e.ColumnIndex == _gridDetail.Columns["折讓金額"].Index ||
                 e.ColumnIndex == _gridDetail.Columns["折扣金額"].Index)
            RecalcTotal();
    }

    /// <summary>依原貨單編號帶入發票與金額（僅填空白欄）。</summary>
    private void FillBillInfo(int rowIndex)
    {
        var row = _gridDetail.Rows[rowIndex];
        var no = Convert.ToString(row.Cells["貨單編號"].Value)?.Trim() ?? "";
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

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    private static decimal Dec(object? v) =>
        v is null or DBNull ? 0m : (decimal.TryParse(v.ToString(), out var m) ? m : 0m);
}
