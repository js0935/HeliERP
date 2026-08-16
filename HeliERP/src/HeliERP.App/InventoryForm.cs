// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Printing;
using System.Text;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>
/// 庫存管理：庫存現量查詢（含安全存量警示）、異動歷史、類別彙總。
/// 現量選列後自動載入該貨品之異動歷史；資料一律經 InventoryService 取得。
/// </summary>
public sealed class InventoryForm : Form
{
    private DataGridView _gridStock = null!;   // 庫存現量
    private DataGridView _gridMove = null!;    // 異動歷史
    private DataGridView _gridCat = null!;     // 類別彙總
    private TabControl _tab = null!;

    private TextBox _txtGoods = null!;         // 貨品編號
    private TextBox _txtName = null!;          // 品名
    private ComboBox _cmbWarehouse = null!;    // 倉庫
    private ComboBox _cmbCategory = null!;     // 類別
    private CheckBox _chkShort = null!;        // 僅不足

    private Label _lblRecord = null!;
    private Label _lblStatus = null!;

    private bool _loading;

    private static readonly string[] _stockQtyColumns =
        { "期初數量", "現有數量", "安全存量" };
    private static readonly string[] _stockMoneyColumns =
        { "平均成本", "標準成本", "庫存總值" };
    private static readonly string[] _moveQtyColumns =
        { "數量", "異動數量" };
    private static readonly string[] _moveMoneyColumns =
        { "單價", "金額" };
    private static readonly string[] _catNumColumns =
        { "貨品數", "期初數量合計", "現有數量合計", "庫存總值合計" };

    public InventoryForm()
    {
        Text = "庫存管理";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        BackColor = UiTheme.Background;

        var header = UiTheme.BuildHeader("庫存管理", "庫存現量查詢 / 異動歷史 / 類別彙總");
        header.Dock = DockStyle.Top;
        Controls.Add(header);

        BuildToolbar();
        BuildSearchPanel();
        BuildTabPanel();
        BuildStatusBar();

        Load += (s, e) =>
        {
            try
            {
                _cmbWarehouse.DataSource = InventoryService.LoadWarehouses();
                _cmbWarehouse.DisplayMember = "倉庫編號";
                _cmbWarehouse.ValueMember = "倉庫編號";
                _cmbWarehouse.SelectedIndex = -1;

                _cmbCategory.DataSource = InventoryService.LoadCategories();
                _cmbCategory.DisplayMember = "類別編號";
                _cmbCategory.ValueMember = "類別編號";
                _cmbCategory.SelectedIndex = -1;

                LoadStock();
                LoadCategorySummary();
                _lblStatus.Text = "狀態: 就緒";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "狀態: 載入失敗 - " + ex.Message;
            }
        };

        ShortcutHelper.Enable(this, onSearch: LoadStock);
        UiTheme.ScaleForDpi(this);

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

        var btnSearch = new ModernButton { Text = "查詢", Width = 120 };
        btnSearch.Click += (s, e) => LoadStock();
        var btnAdjust = new ModernButton { Text = "庫存調整", Width = 120, IsPrimary = false };
        btnAdjust.Click += (s, e) =>
        {
            using var form = new AdjustmentForm();
            form.ShowDialog(this);
            LoadStock();
            LoadCategorySummary();
        };
        var btnReload = new ModernButton { Text = "重讀", Width = 120, IsPrimary = false };
        btnReload.Click += (s, e) => { LoadStock(); LoadCategorySummary(); };
        var btnExport = new ModernButton { Text = "匯出 CSV", Width = 120, IsPrimary = false };
        btnExport.Click += (s, e) => ExportCsv();
        var btnPrint = new ModernButton { Text = "列印", Width = 120, IsPrimary = false };
        var printMenu = new ContextMenuStrip();
        printMenu.Items.Add("現有庫存明細表", null, (s, e) => PrintStockReport());
        printMenu.Items.Add("庫存調整明細表", null, (s, e) => PrintAdjustmentReport());
        btnPrint.Click += (s, e) => printMenu.Show(btnPrint, new Point(0, btnPrint.Height));
        var btnHelp = new ModernButton { Text = "說明", Width = 120, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show(
                "庫存管理功能說明：\n" +
                "1. 庫存現量：以「貨品庫存」為準（出貨扣、進貨加），顯示期初/現有數量、安全存量與庫存總值（現量×平均成本）。\n" +
                "2. 現有數量低於安全存量之列以紅色標示；勾選「僅顯示不足」可過濾。\n" +
                "3. 異動歷史：選取現量列後顯示該貨品之出貨/進貨/退回異動（出貨與進退為負、出退與進貨為正）。\n" +
                "4. 類別彙總：依貨品類別統計貨品數、現有數量與庫存總值。\n" +
                "5. 「匯出 CSV」可匯出目前分頁資料；「列印」可印現有庫存明細表或庫存調整明細表。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 120, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSearch); Add(btnAdjust); Add(btnReload); Add(btnExport); Add(btnPrint); Add(btnHelp); Add(btnExit);

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

        var lblGoods = new Label { Text = "貨品編號：", Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblGoods, sub: true);
        panel.Controls.Add(lblGoods);
        _txtGoods = new TextBox { Width = 110 };
        panel.Controls.Add(_txtGoods);

        var lblName = new Label { Text = "品名：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblName, sub: true);
        panel.Controls.Add(lblName);
        _txtName = new TextBox { Width = 130 };
        panel.Controls.Add(_txtName);

        var lblWarehouse = new Label { Text = "倉庫：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblWarehouse, sub: true);
        panel.Controls.Add(lblWarehouse);
        _cmbWarehouse = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbWarehouse);
        panel.Controls.Add(_cmbWarehouse);

        var lblCategory = new Label { Text = "類別：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblCategory, sub: true);
        panel.Controls.Add(lblCategory);
        _cmbCategory = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbCategory);
        panel.Controls.Add(_cmbCategory);

        _chkShort = new CheckBox
        {
            Text = "僅顯示不足",
            AutoSize = true,
            Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0),
            ForeColor = UiTheme.TextSub,
            Font = UiTheme.Font(10.5F),
        };
        panel.Controls.Add(_chkShort);

        var btnSearch = new ModernButton { Text = "查詢", Width = 84, Height = 34, IsPrimary = true };
        btnSearch.Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0);
        btnSearch.Click += (s, e) => LoadStock();
        panel.Controls.Add(btnSearch);
        var btnClear = new ModernButton { Text = "清除條件", Width = 96, Height = 34, IsPrimary = false };
        btnClear.Margin = new Padding(UiTheme.SpacingSm, UiTheme.SpacingSm, 0, 0);
        btnClear.Click += (s, e) =>
        {
            _txtGoods.Clear();
            _txtName.Clear();
            _cmbWarehouse.SelectedIndex = -1;
            _cmbCategory.SelectedIndex = -1;
            _chkShort.Checked = false;
            LoadStock();
        };
        panel.Controls.Add(btnClear);

        card.Controls.Add(panel);
        Controls.Add(card);
    }

    private void BuildTabPanel()
    {
        _tab = new TabControl { Dock = DockStyle.Fill };

        var tabStock = new TabPage("庫存現量") { Name = "庫存現量", Padding = new Padding(UiTheme.SpacingSm) };
        _gridStock = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            RowHeadersVisible = true,
            RowHeadersWidth = 52,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        UiTheme.StyleDataGridView(_gridStock);
        _gridStock.RowTemplate.Height = 30;
        _gridStock.SelectionChanged += (s, e) => LoadMovements();
        _gridStock.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = _gridStock.Columns[e.ColumnIndex];
            if (col.Name != "現有數量" && col.Name != "安全存量") return;
            var row = _gridStock.Rows[e.RowIndex];
            decimal cur = row.Cells["現有數量"].Value is DBNull or null
                ? 0m : Convert.ToDecimal(row.Cells["現有數量"].Value);
            decimal safe = row.Cells["安全存量"].Value is DBNull or null
                ? 0m : Convert.ToDecimal(row.Cells["安全存量"].Value);
            if (cur < safe)
            {
                if (e.CellStyle is null) return;
                e.CellStyle.ForeColor = UiTheme.Danger;
            }
        };
        tabStock.Controls.Add(_gridStock);

        var tabMove = new TabPage("異動歷史") { Name = "異動歷史", Padding = new Padding(UiTheme.SpacingSm) };
        _gridMove = new DataGridView
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
        UiTheme.StyleDataGridView(_gridMove);
        _gridMove.RowTemplate.Height = 28;
        _gridMove.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = _gridMove.Columns[e.ColumnIndex];
            if (col.Name != "異動數量") return;
            var v = _gridMove.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            decimal q = v is DBNull or null ? 0m : Convert.ToDecimal(v);
            if (e.CellStyle is null) return;
            e.CellStyle.ForeColor = q < 0 ? UiTheme.Danger : UiTheme.Ok;
        };
        tabMove.Controls.Add(_gridMove);

        var tabCat = new TabPage("類別彙總") { Name = "類別彙總", Padding = new Padding(UiTheme.SpacingSm) };
        _gridCat = new DataGridView
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
        UiTheme.StyleDataGridView(_gridCat);
        _gridCat.RowTemplate.Height = 30;
        tabCat.Controls.Add(_gridCat);

        _tab.TabPages.Add(tabStock);
        _tab.TabPages.Add(tabMove);
        _tab.TabPages.Add(tabCat);
        UiTheme.StyleTabControl(_tab);
        Controls.Add(_tab);
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
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(ClientSize.Width - 160, 5),
            ForeColor = UiTheme.TextSub,
            Font = UiTheme.Font(10.5F, FontStyle.Bold),
        };
        bar.Controls.Add(_lblRecord);
        bar.Controls.Add(_lblStatus);
        Controls.Add(bar);
    }

    // ==================== 資料操作 ====================

    private void LoadStock()
    {
        var dt = InventoryService.LoadStock(
            貨品編號: _txtGoods.Text.Trim(),
            品名: _txtName.Text.Trim(),
            倉庫編號: _cmbWarehouse.SelectedValue as string,
            類別編號: _cmbCategory.SelectedValue as string,
            僅不足: _chkShort.Checked);

        _loading = true;
        _gridStock.DataSource = dt;
        foreach (DataGridViewColumn c in _gridStock.Columns)
        {
            if (_stockQtyColumns.Contains(c.Name) || _stockMoneyColumns.Contains(c.Name))
            {
                c.DefaultCellStyle.Format = "N2";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }
        _loading = false;
        _lblRecord.Text = $"記錄: 0 / {dt.Rows.Count}";

        if (dt.Rows.Count > 0)
        {
            _loading = true;
            _gridStock.Rows[0].Selected = true;
            _gridStock.CurrentCell = _gridStock.Rows[0].Cells[0];
            _loading = false;
        }
        else
        {
            _gridMove.DataSource = null;
            _lblRecord.Text = "記錄: 0 / 0";
        }
    }

    private void LoadMovements()
    {
        if (_loading) return;
        if (_gridStock.SelectedRows.Count == 0 || _gridStock.SelectedRows[0].IsNewRow)
        {
            _gridMove.DataSource = null;
            _lblRecord.Text = $"記錄: 0 / {_gridStock.Rows.Count}";
            return;
        }
        var row = _gridStock.SelectedRows[0];
        var 貨品編號 = Str(row.Cells["貨品編號"].Value);
        _lblRecord.Text = $"記錄: {row.Index + 1} / {_gridStock.Rows.Count}";
        BindMovement(InventoryService.LoadMovements(貨品編號));
    }

    private void BindMovement(DataTable dt)
    {
        _loading = true;
        _gridMove.DataSource = dt;
        foreach (DataGridViewColumn c in _gridMove.Columns)
        {
            if (_moveQtyColumns.Contains(c.Name) || _moveMoneyColumns.Contains(c.Name))
            {
                c.DefaultCellStyle.Format = "N2";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }
        _loading = false;
    }

    private void LoadCategorySummary()
    {
        var dt = InventoryService.LoadCategorySummary();
        _loading = true;
        _gridCat.DataSource = dt;
        foreach (DataGridViewColumn c in _gridCat.Columns)
        {
            if (_catNumColumns.Contains(c.Name))
            {
                c.DefaultCellStyle.Format = "N2";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }
        if (_gridCat.Columns.Contains("庫存總值合計"))
            _gridCat.Columns["庫存總值合計"].DefaultCellStyle.Font = UiTheme.Font(10F, FontStyle.Bold);
        _loading = false;
    }

    // ==================== 匯出 CSV ====================

    private void ExportCsv()
    {
        var grid = _tab.SelectedTab?.Name switch
        {
            "異動歷史" => _gridMove,
            "類別彙總" => _gridCat,
            _ => _gridStock,
        };
        if (ExportService.ExportGrid(this, grid, "庫存管理.csv", "匯出 CSV"))
            _lblStatus.Text = "狀態: 已匯出 CSV";
    }

    // ==================== 列印報表 ====================

    private static string ReportDir => ReportPrintService.RepDirectory;

    private void PrintStockReport()
    {
        var dt = InventoryService.LoadStock(
            貨品編號: _txtGoods.Text.Trim(),
            品名: _txtName.Text.Trim(),
            倉庫編號: _cmbWarehouse.SelectedValue as string,
            類別編號: _cmbCategory.SelectedValue as string,
            僅不足: _chkShort.Checked);
        if (dt.Rows.Count == 0)
        {
            MessageBox.Show("目前查詢條件沒有庫存資料可列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = ScopeDescription();
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        PrintRtm("現有庫存明細表.rtm", data, "現有庫存明細表");
    }

    private void PrintAdjustmentReport()
    {
        var dt = InventoryService.LoadAdjustmentDetails();
        if (dt.Rows.Count == 0)
        {
            MessageBox.Show("目前沒有庫存調整明細可列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        PrintRtm("庫存調整明細表.rtm", data, "庫存調整明細表");
    }

    private string ScopeDescription()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_txtGoods.Text)) parts.Add($"貨品 {_txtGoods.Text.Trim()}*");
        if (!string.IsNullOrWhiteSpace(_txtName.Text)) parts.Add($"品名含「{_txtName.Text.Trim()}」");
        if (_cmbWarehouse.SelectedValue is string w) parts.Add($"倉庫 {w}");
        if (_cmbCategory.SelectedValue is string c) parts.Add($"類別 {c}");
        if (_chkShort.Checked) parts.Add("僅顯示不足");
        return parts.Count == 0 ? "全部" : string.Join("、", parts);
    }

    private static void FillCompany(RtmData data)
    {
        var company = new CompanyInfo();
        data.Company["公司全名"] = company.CompanyName;
        data.Company["電話號碼"] = company.Phone;
        data.Company["登記地址"] = company.Address;
        data.Company["傳真號碼"] = LookupCompanyFax(company.CompanyName);
    }

    private static string LookupCompanyFax(string companyName)
    {
        var v = DbManager.QueryScalar(
            "SELECT \"傳真號碼\" FROM \"客戶廠商\" WHERE \"公司全名\" = $name" +
            " AND \"傳真號碼\" IS NOT NULL AND \"傳真號碼\" != '' LIMIT 1",
            DbManager.Param("$name", companyName));
        return v?.ToString() ?? "";
    }

    private void PrintRtm(string rtmFile, RtmData data, string docName)
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
    }

    // ==================== 工具 ====================

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";
}
