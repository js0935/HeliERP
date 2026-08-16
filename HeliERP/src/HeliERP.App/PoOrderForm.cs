// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（改為查詢式版面：全螢幕清單＋彈出編輯框）
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Printing;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>
/// 採購訂貨作業：報價／訂貨／採購／詢價單輸入與列印。
/// 查詢式版面：頂部工具列＋篩選列，中央為單據清單，彈出式編輯框輸入。
/// </summary>
public sealed class PoOrderForm : Form
{
    private readonly ComboBox _cmbKind = new();
    private readonly DateTimePicker _dtFrom = new(), _dtTo = new();
    private readonly TextBox _txtKeyword = new();
    private readonly DataGridView _grid = new();
    private readonly ToolStripStatusLabel _lblCount = new(), _lblTotal = new();

    public PoOrderForm()
    {
        Text = "採購訂貨作業";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 660);
        BackColor = UiTheme.Background;

        Controls.Add(UiTheme.BuildHeader("採購訂貨作業", "報價／訂貨／採購／詢價單輸入與列印"));

        BuildToolbar();
        BuildFilterBar();
        BuildGrid();
        BuildStatusBar();

        _cmbKind.Items.AddRange(PoOrderService.Kinds.Select(k => k.Name).ToArray());
        _cmbKind.SelectedIndex = 0;
        _dtFrom.Value = new DateTime(DateTime.Today.Year, 1, 1);
        _dtTo.Value = DateTime.Today.AddYears(1);
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
        var btnNew = new ModernButton { Text = "新增單據", Width = 130 };
        btnNew.Click += (s, e) => EditBill(null);
        var btnEdit = new ModernButton { Text = "修改", Width = 100, IsPrimary = false };
        btnEdit.Click += (s, e) => EditBill(GetSelectedRow());
        var btnDel = new ModernButton { Text = "刪除", Width = 100, IsPrimary = false };
        btnDel.Click += (s, e) => DeleteSelected();
        Sep();
        var btnPrint = new ModernButton { Text = "列印", Width = 110, IsPrimary = false };
        btnPrint.Click += (s, e) => PrintBill();
        Sep();
        var btnHelp = new ModernButton { Text = "說明", Width = 100, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show(
                "採購訂貨作業功能說明：\n" +
                "1. 單據類別分報價／訂貨／採購／詢價；儲存後自動送審（若有核准流程）。\n" +
                "2. 輸入貨品編號後自動帶入品名、單位與建議單價。\n" +
                "3. 金額 = 數量 × 單價 × 折扣%；稅額依課稅類別與稅率自動計算。\n" +
                "4. 修改會更新原單據並重新送審；列印請先選取清單中的單據。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 100, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSearch); Add(btnNew); Add(btnEdit); Add(btnDel);
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

        _dtFrom.Format = _dtTo.Format = DateTimePickerFormat.Short;
        _txtKeyword.PlaceholderText = "單據號碼";

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
        _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditBill(GetSelectedRow()); };
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
        if (_cmbKind.SelectedItem is not string kindName) return;
        string filter = _txtKeyword.Text.Trim();
        string from = _dtFrom.Value.ToString("yyyy-MM-dd 00:00:00");
        string to = _dtTo.Value.ToString("yyyy-MM-dd 23:59:59");

        var dt = PoOrderService.LoadPoList(kindName, filter, from, to);
        _grid.DataSource = dt;
        _grid.Columns["單據副碼"].Visible = false;
        _grid.Columns["交易對象"].Visible = false;
        if (_grid.Columns.Count > 0)
        {
            _grid.Columns["交易單號"].Width = 120;
            _grid.Columns["交易日期"].Width = 110;
            _grid.Columns["交貨日期"].Width = 110;
            _grid.Columns["對象名稱"].Width = 130;
            _grid.Columns["合計金額"].Width = 110;
            _grid.Columns["合計金額"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["合計金額"].DefaultCellStyle.Format = "N2";
            _grid.Columns["營業稅"].Width = 90;
            _grid.Columns["營業稅"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["營業稅"].DefaultCellStyle.Format = "N2";
            _grid.Columns["總計金額"].Width = 110;
            _grid.Columns["總計金額"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["總計金額"].DefaultCellStyle.Format = "N2";
            _grid.Columns["明細筆數"].HeaderText = "筆數";
            _grid.Columns["明細筆數"].Width = 55;
            _grid.Columns["未交完數"].HeaderText = "未交";
            _grid.Columns["未交完數"].Width = 60;
            _grid.Columns["製單"].Width = 80;
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
        var result = PoBillEditDialog.Show(this, 副碼);
        if (result is null)
            return;
        try
        {
            var req = new PoOrderService.PoBillRequest
            {
                單據副碼 = 副碼,
                單據類別 = result.單據類別,
                交易日期 = result.交易日期,
                交貨日期 = result.交貨日期,
                交易對象 = result.交易對象,
                部門編號 = result.部門編號,
                員工編號 = result.員工編號,
                送貨地址 = result.送貨地址,
                課稅類別 = result.課稅類別,
                備註 = result.備註,
                明細 = result.明細,
            };
            var saved = PoOrderService.SavePoBill(req);
            decimal 合計 = result.明細.Sum(d => PoOrderService.CalcDetailAmount(d));
            var flowSeq = ApprovalService.Submit(req.單據類別, saved.交易單號, 合計,
                AuditService.CurrentUser, req.備註);
            MessageBox.Show($"單據「{saved.交易單號}」已儲存。"
                + (flowSeq is null ? "" : "\n已自動送審（待核准）。"),
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteSelected()
    {
        var row = GetSelectedRow();
        if (row is null)
        {
            MessageBox.Show("請先於清單選取一筆單據。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string 單號 = Convert.ToString(row["交易單號"]) ?? "";
        long 副碼 = Convert.ToInt64(row["單據副碼"]);
        var confirm = MessageBox.Show($"確定刪除單據「{單號}」？",
            "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        try
        {
            PoOrderService.DeletePoBill(副碼);
            MessageBox.Show($"單據「{單號}」已刪除。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("請先於清單選取一筆單據，再按列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        long 副碼 = Convert.ToInt64(row["單據副碼"]);
        string kindName = Convert.ToString(row["單據類別"]) ?? "報價";
        var kind = PoOrderService.GetKind(kindName);
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
        var data = BuildRtmData(副碼);
        var billNo = data.Master.TryGetValue("ppDBPipeline1|交易單號", out var no) ? Convert.ToString(no) ?? "" : 副碼.ToString();

        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument
        {
            DocumentName = $"{kind.Name}單-{billNo}",
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

    /// <summary>建立報表資料：主檔（ppDBPipeline1）+ 公司（plCompany）+ 明細（ppDBPipeline2）。</summary>
    private static RtmData BuildRtmData(long 副碼)
    {
        var data = new RtmData { DetailPipeline = "ppDBPipeline2" };

        var dt = PoOrderService.LoadPoMaster(副碼);
        if (dt.Rows.Count > 0)
        {
            var row = dt.Rows[0];
            foreach (DataColumn col in dt.Columns)
                data.Master[$"ppDBPipeline1|{col.ColumnName}"] = row[col];
        }

        ARService.FillCompany(data);

        var detailDt = PoOrderService.LoadPoPrintDetails(副碼);
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

// ==================== 彈出式編輯框 ====================

/// <summary>採訂單編輯框：新增／修改／檢視（唯讀）。</summary>
public sealed class PoBillEditDialog : Form
{
    private readonly ComboBox _cmbKind = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtNo = new() { ReadOnly = true, BackColor = UiTheme.BorderLight };
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker _dtpDelivery = new() { Format = DateTimePickerFormat.Short };
    private readonly ComboBox _cmbTax = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbObject = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbDept = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbStaff = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtShipAddr = new();
    private readonly TextBox _txtRemark = new();
    private readonly DataGridView _gridDetail = new();
    private readonly Label _lblAmount = new() { AutoSize = true, ForeColor = UiTheme.Primary, Font = UiTheme.Font(11F, FontStyle.Bold) };
    private readonly Label _lblTax = new() { AutoSize = true, ForeColor = UiTheme.Primary, Font = UiTheme.Font(11F, FontStyle.Bold) };
    private readonly Label _lblTotal = new() { AutoSize = true, ForeColor = UiTheme.Danger, Font = UiTheme.Font(12F, FontStyle.Bold) };
    private readonly bool _readOnly;
    private readonly long? _副碼;
    private bool _loading;

    private static readonly string[] 課稅類別選項 = { "外加", "內含", "免稅" };

    public sealed record Result(
        string 單據類別, DateTime 交易日期, DateTime 交貨日期, string 課稅類別,
        string 交易對象, string 部門編號, string 員工編號, string 送貨地址, string 備註,
        List<PoOrderService.PoLine> 明細);

    private PoBillEditDialog(long? 副碼, bool readOnly)
    {
        _副碼 = 副碼;
        _readOnly = readOnly;
        Text = readOnly ? "檢視單據" : (副碼 is null ? "新增單據" : "修改單據");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UiTheme.Background;
        Font = UiTheme.Font(10F);
        ClientSize = new Size(1040, 660);

        foreach (var k in PoOrderService.Kinds)
            _cmbKind.Items.Add(k.Name);
        _cmbTax.Items.AddRange(課稅類別選項);

        int y = 18;
        void Field(string label, Control c, int x, int w = 0)
        {
            Controls.Add(new Label { Text = label + "：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(x, y + 4) });
            c.Location = new Point(x + 76, y);
            if (w > 0) c.Width = w;
            Controls.Add(c);
        }

        _txtNo.Width = 140;
        _cmbKind.Width = 110;
        _cmbTax.Width = 80;
        _dtpDate.Width = 120;
        _dtpDelivery.Width = 120;
        _cmbObject.Width = 220;
        _cmbDept.Width = 120;
        _cmbStaff.Width = 120;
        Field("單據類別", _cmbKind, 20);
        Field("單據號碼", _txtNo, 190);
        Field("交易日期", _dtpDate, 360);
        Field("交貨日期", _dtpDelivery, 540);
        Field("課稅類別", _cmbTax, 720);
        y = 56;
        Field("交易對象", _cmbObject, 20);
        Field("部門", _cmbDept, 320);
        Field("員工", _cmbStaff, 520);
        Field("送貨地址", _txtShipAddr, 700, 300);
        y = 94;
        Field("備註", _txtRemark, 20, 960);

        _cmbKind.SelectedIndexChanged += (s, e) =>
        {
            if (_loading || _readOnly || _副碼 is not null) return;
            LoadObjectCombo();
            _txtNo.Text = PoOrderService.PreviewPoNo(_cmbKind.SelectedItem?.ToString() ?? "報價");
        };

        _gridDetail.Location = new Point(20, 134);
        _gridDetail.Size = new Size(1000, 390);
        _gridDetail.RowHeadersVisible = false;
        _gridDetail.AllowUserToAddRows = _readOnly ? false : true;
        _gridDetail.AllowUserToDeleteRows = _readOnly ? false : true;
        _gridDetail.MultiSelect = false;
        _gridDetail.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _gridDetail.RowTemplate.Height = 30;
        UiTheme.StyleDataGridView(_gridDetail);
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "貨品編號", HeaderText = "貨品編號", Width = 110 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "品名", HeaderText = "品名", Width = 170, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "倉庫", HeaderText = "倉庫", Width = 55 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "數量", HeaderText = "數量", Width = 80 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "交易數量", HeaderText = "交易數量", Width = 80, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單位", HeaderText = "單位", Width = 48, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單價", HeaderText = "單價", Width = 90 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "折扣", HeaderText = "折扣", Width = 60 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "金額", HeaderText = "金額", Width = 100, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "附註說明", HeaderText = "附註說明", Width = 140 });
        _gridDetail.Columns["貨品編號"].Frozen = true;
        _gridDetail.CellEndEdit += OnDetailCellEndEdit;

        var lblAmountT = new Label { Text = "合計金額：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(520, 536) };
        _lblAmount.Location = new Point(592, 536);
        var lblTaxT = new Label { Text = "稅額：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(680, 536) };
        _lblTax.Location = new Point(728, 536);
        var lblTotalT = new Label { Text = "總計：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(810, 536) };
        _lblTotal.Location = new Point(858, 534);
        _lblAmount.Text = "0";
        _lblTax.Text = "0";
        _lblTotal.Text = "0";

        var btnOk = new ModernButton { Text = readOnly ? "關閉" : "確定", Size = new Size(96, 40), Location = new Point(1040 - 250, 600), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(96, 40), Location = new Point(1040 - 140, 600), IsPrimary = false, DrawShadow = false };
        btnOk.Click += (s, e) => Finish();
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        btnOk.Enabled = !_readOnly;

        Controls.AddRange(new Control[] {
            _gridDetail, lblAmountT, _lblAmount, lblTaxT, _lblTax, lblTotalT, _lblTotal,
            btnOk, btnCancel,
        });

        UiTheme.ScaleForDpi(this);
        UiTheme.ClampToScreen(this);
    }

    /// <summary>新增（副碼 null）或修改模式。</summary>
    public static Result? Show(Form owner, long? 副碼)
    {
        using var dlg = new PoBillEditDialog(副碼, readOnly: false);
        dlg._loading = true;
        if (副碼 is null)
        {
            dlg._cmbKind.SelectedIndex = 0;
            dlg.LoadObjectCombo();
            dlg._txtNo.Text = PoOrderService.PreviewPoNo(dlg._cmbKind.SelectedItem?.ToString() ?? "報價");
            dlg._cmbTax.SelectedIndex = 0;
        }
        else
        {
            var master = PoOrderService.LoadPoMaster(副碼.Value);
            if (master.Rows.Count == 0)
            {
                MessageBox.Show("找不到該單據，可能已被刪除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            var m = master.Rows[0];
            dlg._cmbKind.SelectedItem = Str(m["單據類別"]);
            dlg.LoadObjectCombo();
            dlg._txtNo.Text = Str(m["交易單號"]);
            if (DateTime.TryParse(Str(m["交易日期"]), out var d)) dlg._dtpDate.Value = d;
            if (DateTime.TryParse(Str(m["交貨日期"]), out var dl)) dlg._dtpDelivery.Value = dl;
            dlg._cmbTax.SelectedItem = Str(m["課稅類別"]).Length > 0 ? Str(m["課稅類別"]) : "外加";
            SelectComboValue(dlg._cmbObject, Str(m["交易對象"]));
            SelectComboValue(dlg._cmbDept, Str(m["部門編號"]));
            SelectComboValue(dlg._cmbStaff, Str(m["員工編號"]));
            dlg._txtShipAddr.Text = Str(m["送貨地址"]);
            dlg._txtRemark.Text = Str(m["備註"]);
            dlg.LoadDetails(副碼.Value);
        }
        dlg._loading = false;
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.BuildResult() : null;
    }

    /// <summary>檢視模式（唯讀）。</summary>
    public static void ShowView(Form owner, long 副碼)
    {
        using var dlg = new PoBillEditDialog(副碼, readOnly: true);
        var master = PoOrderService.LoadPoMaster(副碼);
        if (master.Rows.Count == 0)
        {
            MessageBox.Show("找不到該單據，可能已被刪除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var m = master.Rows[0];
        dlg._cmbKind.SelectedItem = Str(m["單據類別"]);
        dlg.LoadObjectCombo();
        dlg._txtNo.Text = Str(m["交易單號"]);
        if (DateTime.TryParse(Str(m["交易日期"]), out var d)) dlg._dtpDate.Value = d;
        if (DateTime.TryParse(Str(m["交貨日期"]), out var dl)) dlg._dtpDelivery.Value = dl;
        dlg._cmbTax.SelectedItem = Str(m["課稅類別"]).Length > 0 ? Str(m["課稅類別"]) : "外加";
        SelectComboValue(dlg._cmbObject, Str(m["交易對象"]));
        SelectComboValue(dlg._cmbDept, Str(m["部門編號"]));
        SelectComboValue(dlg._cmbStaff, Str(m["員工編號"]));
        dlg._txtShipAddr.Text = Str(m["送貨地址"]);
        dlg._txtRemark.Text = Str(m["備註"]);
        dlg.LoadDetails(副碼);
        dlg.ShowDialog(owner);
    }

    private void LoadObjectCombo()
    {
        _loading = true;
        var obj = PoOrderService.LoadObjectCombo(_cmbKind.SelectedItem?.ToString() ?? "報價");
        _cmbObject.DataSource = obj;
        _cmbObject.DisplayMember = "公司簡稱";
        _cmbObject.ValueMember = "客廠編號";
        _loading = false;
    }

    private void LoadDetails(long 副碼)
    {
        var dt = PoOrderService.LoadPoDetails(副碼);
        _gridDetail.Rows.Clear();
        foreach (DataRow r in dt.Rows)
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
    }

    private Result BuildResult()
    {
        var lines = new List<PoOrderService.PoLine>();
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            var code = Convert.ToString(r.Cells["貨品編號"].Value)?.Trim() ?? "";
            if (code.Length == 0) continue;
            lines.Add(new PoOrderService.PoLine
            {
                貨品編號 = code,
                倉庫編號 = Convert.ToString(r.Cells["倉庫"].Value)?.Trim() ?? "",
                數量 = Dec(r.Cells["數量"].Value),
                單位 = Convert.ToString(r.Cells["單位"].Value)?.Trim() ?? "",
                單價 = Dec(r.Cells["單價"].Value),
                折扣 = Dec(r.Cells["折扣"].Value) == 0 ? 100m : Dec(r.Cells["折扣"].Value),
                附註說明 = Convert.ToString(r.Cells["附註說明"].Value)?.Trim() ?? "",
            });
        }
        return new Result(
            _cmbKind.SelectedItem?.ToString() ?? "報價",
            _dtpDate.Value.Date,
            _dtpDelivery.Value.Date,
            _cmbTax.SelectedItem?.ToString() ?? "外加",
            _cmbObject.SelectedValue is string o ? o : "",
            _cmbDept.SelectedValue is string dep ? dep : "",
            _cmbStaff.SelectedValue is string st ? st : "",
            _txtShipAddr.Text.Trim(),
            _txtRemark.Text.Trim(),
            lines);
    }

    private void Finish()
    {
        if (_cmbObject.SelectedValue is not string o || o.Length == 0)
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

    private void OnDetailCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || _readOnly || e.RowIndex < 0 || e.RowIndex >= _gridDetail.Rows.Count) return;
        var row = _gridDetail.Rows[e.RowIndex];
        if (row.IsNewRow) return;
        var col = _gridDetail.Columns[e.ColumnIndex].Name;

        if (col == "貨品編號")
        {
            var code = Convert.ToString(row.Cells["貨品編號"].Value)?.Trim() ?? "";
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

    private static decimal PickUnitPrice(Dictionary<string, object?> g)
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
        var kind = PoOrderService.GetKind(_cmbKind.SelectedItem?.ToString() ?? "報價");
        bool 免稅 = _cmbTax.SelectedItem is string t && t.Contains("免");
        decimal 稅率 = kind.TaxSource == "進項"
            ? TradeService.LoadParams().進項稅率
            : TradeService.LoadParams().銷項稅率;
        decimal 稅 = 免稅 ? 0m : Math.Round(合計 * 稅率 / 100m, 0, MidpointRounding.AwayFromZero);
        _lblAmount.Text = 合計.ToString("N2");
        _lblTax.Text = 稅.ToString("N2");
        _lblTotal.Text = (合計 + 稅).ToString("N2");
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

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    private static decimal Dec(object? v) =>
        v is null or DBNull ? 0m : (decimal.TryParse(v.ToString(), out var m) ? m : 0m);
}
