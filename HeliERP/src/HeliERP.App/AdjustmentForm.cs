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
/// 庫存調整單：盤點盤盈／盤虧、報廢、贈品、損耗等非進出貨庫存異動。
/// 查詢式版面：頂部工具列＋篩選列，中央為調整單清單，彈出式編輯框輸入新單。
/// </summary>
public sealed class AdjustmentForm : Form
{
    private readonly ComboBox _cmbReason = new();
    private readonly DateTimePicker _dtFrom = new(), _dtTo = new();
    private readonly TextBox _txtKeyword = new();
    private readonly DataGridView _grid = new();
    private readonly ToolStripStatusLabel _lblCount = new(), _lblTotal = new();

    public AdjustmentForm()
    {
        Text = "庫存調整單";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 660);
        BackColor = UiTheme.Background;

        Controls.Add(UiTheme.BuildHeader("庫存調整單", "盤點盤盈／盤虧、報廢、贈品、損耗等非進出貨庫存異動"));

        BuildToolbar();
        BuildFilterBar();
        BuildGrid();
        BuildStatusBar();

        _cmbReason.SelectedIndex = 0;
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
        var btnNew = new ModernButton { Text = "新增調整單", Width = 130 };
        btnNew.Click += (s, e) => AddNew();
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
                "庫存調整單功能說明：\n" +
                "1. 調整數量為帶方向之數值：正數 = 盤盈（庫存增加）、負數 = 盤虧（庫存減少）。\n" +
                "2. 輸入貨品編號與倉庫後，畫面自動帶入品名、目前庫存與安全存量供參考。\n" +
                "3. 調整單不產生帳款，僅異動貨品庫存並記錄於庫存異動歷史。\n" +
                "4. 刪除會回復庫存；列印請先選取清單中的調整單。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 100, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSearch); Add(btnNew); Add(btnView); Add(btnDel);
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

        _cmbReason.Items.AddRange(new object[] { "全部原因", "盤點盤盈", "盤點盤虧", "報廢", "贈品", "損耗", "其他" });
        _dtFrom.Format = _dtTo.Format = DateTimePickerFormat.Short;
        _txtKeyword.PlaceholderText = "調整單號";

        Field("單號", _txtKeyword, 200);
        Field("日期從", _dtFrom, 110);
        Field("至", _dtTo, 110);
        Field("原因", _cmbReason, 120);
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
        string keyword = _txtKeyword.Text.Trim();
        string reason = _cmbReason.SelectedIndex > 0 ? _cmbReason.SelectedItem?.ToString() ?? "" : "";
        string from = _dtFrom.Value.ToString("yyyy-MM-dd 00:00:00");
        string to = _dtTo.Value.ToString("yyyy-MM-dd 23:59:59");

        var dt = AdjustmentService.LoadAdjustmentList(keyword, from, to, reason);
        _grid.DataSource = dt;
        _grid.Columns["單據副碼"].Visible = false;
        if (_grid.Columns.Count > 0)
        {
            _grid.Columns["交易單號"].HeaderText = "調整單號";
            _grid.Columns["交易單號"].Width = 120;
            _grid.Columns["交易日期"].Width = 130;
            _grid.Columns["數量合計"].Width = 100;
            _grid.Columns["數量合計"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["明細總筆數"].HeaderText = "筆數";
            _grid.Columns["明細總筆數"].Width = 60;
            _grid.Columns["備註"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns["製單"].Width = 90;
        }
        _lblCount.Text = $"共 {dt.Rows.Count} 筆";
        var total = dt.AsEnumerable().Sum(r => Convert.ToDecimal(r["數量合計"]));
        _lblTotal.Text = $"調整數量合計：{total:N0}";
    }

    private void AddNew()
    {
        var result = AdjustmentEditDialog.ShowForNew(this);
        if (result is null)
            return;
        try
        {
            var req = new AdjustmentService.AdjustmentRequest
            {
                調整日期 = result.調整日期,
                原因 = result.原因,
                備註 = result.備註,
                明細 = result.明細,
            };
            string no = AdjustmentService.SaveAdjustment(req);
            MessageBox.Show($"調整單「{no}」已儲存，庫存已更新。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("請先於清單選取一筆調整單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        long 副碼 = Convert.ToInt64(row["單據副碼"]);
        AdjustmentEditDialog.ShowForView(this, 副碼);
    }

    private void DeleteSelected()
    {
        var row = GetSelectedRow();
        if (row is null)
        {
            MessageBox.Show("請先於清單選取一筆調整單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string 單號 = Convert.ToString(row["交易單號"]) ?? "";
        long 副碼 = Convert.ToInt64(row["單據副碼"]);
        var confirm = MessageBox.Show($"確定刪除調整單「{單號}」？刪除後將回復庫存。",
            "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        try
        {
            AdjustmentService.DeleteAdjustment(副碼);
            MessageBox.Show($"調整單「{單號}」已刪除，庫存已回復。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("請先於清單選取一筆調整單，再按列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        long 副碼 = Convert.ToInt64(row["單據副碼"]);

        string rtmPath = Path.Combine(ReportDir, "調整單據.rtm");
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
        var billNo = data.Master.TryGetValue("交易單號", out var no) ? Convert.ToString(no) ?? "" : 副碼.ToString();

        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument
        {
            DocumentName = $"庫存調整單-{billNo}",
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
        var data = new RtmData();

        var dt = DbManager.QueryTable(
            "SELECT * FROM [交易主檔] WHERE [單據副碼] = $c",
            DbManager.Param("$c", 副碼));
        if (dt.Rows.Count == 0) return data;
        var row = dt.Rows[0];
        foreach (DataColumn col in dt.Columns)
            data.Master[col.ColumnName] = row[col];

        var company = new CompanyInfo();
        data.Company["公司全名"] = company.CompanyName;
        data.Company["電話號碼"] = company.Phone;
        data.Company["登記地址"] = company.Address;
        data.Company["傳真號碼"] = LookupCompanyFax(company.CompanyName);

        var detailDt = DbManager.QueryTable(
            "SELECT * FROM [交易明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
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

/// <summary>調整單編輯框：新增（自動帶號＋明細輸入）與檢視（唯讀）共用。</summary>
public sealed class AdjustmentEditDialog : Form
{
    private readonly TextBox _txtNo = new() { ReadOnly = true, BackColor = UiTheme.BorderLight };
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short };
    private readonly ComboBox _cmbReason = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtRemark = new();
    private readonly DataGridView _gridDetail = new();
    private readonly Label _lblQtyTotal = new();
    private readonly bool _readOnly;

    public sealed record Result(DateTime 調整日期, string 原因, string 備註, List<AdjustmentService.AdjustmentLine> 明細);

    private AdjustmentEditDialog(bool readOnly)
    {
        _readOnly = readOnly;
        Text = readOnly ? "檢視調整單" : "新增調整單";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UiTheme.Background;
        Font = UiTheme.Font(10F);
        ClientSize = new Size(920, 560);

        var lblNo = MakeLabel("調整單號");
        _txtNo.Width = 130;
        _txtNo.Location = new Point(90, 18);
        var lblDate = MakeLabel("調整日期");
        lblDate.Location = new Point(260, 20);
        _dtpDate.Location = new Point(348, 16);
        var lblReason = MakeLabel("調整原因");
        lblReason.Location = new Point(510, 20);
        _cmbReason.Location = new Point(598, 16);
        _cmbReason.Width = 140;
        var lblRemark = MakeLabel("備註");
        lblRemark.Location = new Point(20, 58);
        _txtRemark.Location = new Point(90, 54);
        _txtRemark.Width = 640;

        _cmbReason.Items.AddRange(AdjustmentService.調整原因);

        _gridDetail.Location = new Point(20, 90);
        _gridDetail.Size = new Size(880, 380);
        _gridDetail.RowHeadersVisible = false;
        _gridDetail.AllowUserToAddRows = _readOnly ? false : true;
        _gridDetail.AllowUserToDeleteRows = _readOnly ? false : true;
        _gridDetail.MultiSelect = false;
        _gridDetail.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _gridDetail.RowTemplate.Height = 30;
        UiTheme.StyleDataGridView(_gridDetail);
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "貨品編號", HeaderText = "貨品編號", Width = 100 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "品名", HeaderText = "品名", Width = 160, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "倉庫", HeaderText = "倉庫", Width = 60 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "調整數量", HeaderText = "調整數量", Width = 90 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "目前庫存", HeaderText = "目前庫存", Width = 80, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "安全存量", HeaderText = "安全存量", Width = 80, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單位", HeaderText = "單位", Width = 48, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "附註說明", HeaderText = "附註說明", Width = 160 });
        _gridDetail.CellEndEdit += OnDetailCellEndEdit;
        _gridDetail.CellValueChanged += (s, e) => { if (e.RowIndex >= 0 && _gridDetail.Columns[e.ColumnIndex].Name == "調整數量") RecalcQtyTotal(); };
        _gridDetail.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_gridDetail.Columns[e.ColumnIndex].Name != "調整數量") return;
            decimal q = Dec(_gridDetail.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
            if (e.CellStyle is null) return;
            e.CellStyle.ForeColor = q < 0 ? UiTheme.Danger : UiTheme.Ok;
        };

        _lblQtyTotal.Text = "調整數量合計: 0";
        _lblQtyTotal.AutoSize = true;
        _lblQtyTotal.ForeColor = UiTheme.Primary;
        _lblQtyTotal.Font = UiTheme.Font(10.5F, FontStyle.Bold);
        _lblQtyTotal.Location = new Point(20, 478);

        var btnOk = new ModernButton { Text = readOnly ? "關閉" : "確定", Size = new Size(96, 40), Location = new Point(920 - 250, 512), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(96, 40), Location = new Point(920 - 140, 512), IsPrimary = false, DrawShadow = false };
        btnOk.Click += (s, e) => Finish();
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        btnOk.Enabled = !_readOnly;

        Controls.AddRange(new Control[] {
            lblNo, _txtNo, lblDate, _dtpDate, lblReason, _cmbReason, lblRemark, _txtRemark,
            _gridDetail, _lblQtyTotal, btnOk, btnCancel,
        });

        UiTheme.ScaleForDpi(this);
        UiTheme.ClampToScreen(this);
    }

    /// <summary>新增模式：預覽單號（唯讀），輸入後儲存。</summary>
    public static Result? ShowForNew(Form owner)
    {
        using var dlg = new AdjustmentEditDialog(readOnly: false);
        dlg._txtNo.Text = AdjustmentService.PreviewAdjustmentNo();
        dlg._cmbReason.SelectedIndex = 0;
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.BuildResult() : null;
    }

    /// <summary>檢視模式：載入既有單據，全部唯讀。</summary>
    public static void ShowForView(Form owner, long 副碼)
    {
        var m = DbManager.QueryTable(
            "SELECT [交易單號],[交易日期],COALESCE([備註],'') AS [備註] FROM [交易主檔] WHERE [單據副碼] = $c",
            DbManager.Param("$c", 副碼));
        if (m.Rows.Count == 0)
        {
            MessageBox.Show("找不到該調整單，可能已被刪除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new AdjustmentEditDialog(readOnly: true);
        var row = m.Rows[0];
        dlg._txtNo.Text = Convert.ToString(row["交易單號"]) ?? "";
        if (DateTime.TryParse(Convert.ToString(row["交易日期"]), out var d))
            dlg._dtpDate.Value = d;
        string remark = Convert.ToString(row["備註"]) ?? "";
        dlg._cmbReason.SelectedIndex = Math.Max(0, dlg._cmbReason.Items.IndexOf("其他"));
        dlg._txtRemark.Text = remark;

        var dt = AdjustmentService.LoadAdjustmentDetails(副碼);
        foreach (DataRow r in dt.Rows)
        {
            int i = dlg._gridDetail.Rows.Add();
            var gr = dlg._gridDetail.Rows[i];
            gr.Cells["貨品編號"].Value = Convert.ToString(r["貨品編號"]);
            gr.Cells["品名"].Value = Convert.ToString(r["品名"]);
            gr.Cells["倉庫"].Value = Convert.ToString(r["倉庫編號"]);
            gr.Cells["調整數量"].Value = r["調整數量"];
            gr.Cells["單位"].Value = Convert.ToString(r["單位"]);
            gr.Cells["附註說明"].Value = Convert.ToString(r["附註說明"]);
            dlg.FillStockInfo(i);
        }
        dlg.RecalcQtyTotal();
        dlg.ShowDialog(owner);
    }

    private Result BuildResult()
    {
        var lines = new List<AdjustmentService.AdjustmentLine>();
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            var code = Convert.ToString(r.Cells["貨品編號"].Value)?.Trim() ?? "";
            if (code.Length == 0) continue;
            lines.Add(new AdjustmentService.AdjustmentLine
            {
                貨品編號 = code,
                倉庫編號 = Convert.ToString(r.Cells["倉庫"].Value)?.Trim() ?? "",
                數量 = Dec(r.Cells["調整數量"].Value),
                單位 = Convert.ToString(r.Cells["單位"].Value)?.Trim() ?? "",
                附註說明 = Convert.ToString(r.Cells["附註說明"].Value)?.Trim() ?? "",
            });
        }
        return new Result(_dtpDate.Value.Date,
            _cmbReason.SelectedItem as string ?? "",
            _txtRemark.Text.Trim(),
            lines);
    }

    private void Finish()
    {
        if (AdjustmentService.調整原因.Contains(_cmbReason.SelectedItem as string ?? "") == false)
        {
            MessageBox.Show(this, "請選擇調整原因。", "請修正", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (BuildResult().明細.Count == 0)
        {
            MessageBox.Show(this, "請至少輸入一筆調整明細。", "請修正", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        DialogResult = DialogResult.OK;
    }

    // ==================== 明細編輯 ====================

    private void OnDetailCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_readOnly || e.RowIndex < 0 || e.RowIndex >= _gridDetail.Rows.Count) return;
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
            row.Cells["品名"].Value = Convert.ToString(g.TryGetValue("品名", out var n) ? n : null);
            row.Cells["單位"].Value = Convert.ToString(g.TryGetValue("基本單位", out var u) ? u : null);
            if (row.Cells["倉庫"].Value is null or DBNull or "")
                row.Cells["倉庫"].Value = TradeService.LoadParams().常用倉庫;
            FillStockInfo(e.RowIndex);
        }
        else if (col == "倉庫")
        {
            if (row.Cells["貨品編號"].Value is null or DBNull or "") return;
            FillStockInfo(e.RowIndex);
        }
    }

    private void FillStockInfo(int rowIndex)
    {
        var row = _gridDetail.Rows[rowIndex];
        var code = Convert.ToString(row.Cells["貨品編號"].Value)?.Trim() ?? "";
        var wh = Convert.ToString(row.Cells["倉庫"].Value)?.Trim() ?? "";
        if (code.Length == 0) return;
        var info = AdjustmentService.LoadStockInfo(code, wh);
        if (info is null)
        {
            row.Cells["目前庫存"].Value = 0m;
            row.Cells["安全存量"].Value = 0m;
        }
        else
        {
            row.Cells["目前庫存"].Value = info.TryGetValue("現有數量", out var q) ? q : 0m;
            row.Cells["安全存量"].Value = info.TryGetValue("安全存量", out var s) ? s : 0m;
        }
    }

    private void RecalcQtyTotal()
    {
        decimal total = 0m;
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            total += Dec(r.Cells["調整數量"].Value);
        }
        _lblQtyTotal.Text = $"調整數量合計: {total:N2}";
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text + "：",
        Font = UiTheme.Font(9.5F),
        ForeColor = UiTheme.TextMain,
        AutoSize = true,
        Location = new Point(20, 20),
    };

    private static decimal Dec(object? v) =>
        v is null or DBNull ? 0m : (decimal.TryParse(v.ToString(), out var m) ? m : 0m);
}
