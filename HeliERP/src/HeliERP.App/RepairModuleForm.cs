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
/// 查詢式版面：全螢幕清單 ＋ 彈出編輯框（RepairEditDialog，分頁：基本、費用帳款、全部欄位）。
/// </summary>
public sealed class RepairModuleForm : Form
{
    private const string TableName = "維修主檔";

    // 列表
    private DataGridView _grid = null!;
    private TextBox _txtNo = null!, _txtCustomer = null!, _txtGoods = null!;
    private ComboBox _cmbStatusFilter = null!;
    private DateTimePicker _dtFrom = null!, _dtTo = null!;

    // 狀態列
    private Label _lblRecord = null!, _lblStatus = null!;
    private int _currentIndex = -1;

    // 目前選取資料
    private DataRow? _currentRow;
    private string _currentKey = "";

    private bool _loading;

    private readonly AppUser? _user;

    private static string ReportDir => ReportPrintService.RepDirectory;

    public RepairModuleForm(AppUser? user = null)
    {
        _user = user;
        Text = "維修管理";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 660);
        UiTheme.Apply(this);

        Controls.Add(UiTheme.BuildHeader("維修管理", "叫修登記 → 收件 → 維修（內修/外送）→ 交貨 → 保固追蹤 → 帳款"));

        BuildToolbar();
        BuildSearchPanel();
        BuildListGrid();
        BuildStatusBar();

        Load += (s, e) => LoadList();

        ShortcutHelper.Enable(this,
            NewBill,
            EditBill,
            DeleteBill,
            LoadList,
            LoadList);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    // ==================== 版面建構 ====================

    private void BuildToolbar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 52 };
        UiTheme.StyleTopBar(bar);

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
        btnEdit.Click += (s, e) => EditBill();
        var btnDel = new ModernButton { Text = "刪除", Width = 120, IsPrimary = false };
        btnDel.Click += (s, e) => DeleteBill();
        var btnPrint = new ModernButton { Text = "列印", Width = 120, IsPrimary = false };
        btnPrint.Click += (s, e) => PrintBill();

        Add(btnSearch); Add(btnReload); Add(btnNew); Add(btnEdit); Add(btnDel); Add(btnPrint);
        Sep();

        var btnHelp = new ModernButton { Text = "說明", Width = 120, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show("維修管理系統 v1.0\n流程：叫修登記 → 收件 → 維修（內修/外送）→ 交貨 → 保固追蹤 → 帳款。",
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
        var lblNo = new Label { Text = "單號：", Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblNo, sub: true);
        panel.Controls.Add(lblNo);
        _txtNo = new TextBox { Width = 120 };
        UiTheme.StyleTextBox(_txtNo);
        _txtNo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LoadList(); e.SuppressKeyPress = true; } };
        panel.Controls.Add(_txtNo);
        var lblCust = new Label { Text = "客戶：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblCust, sub: true);
        panel.Controls.Add(lblCust);
        _txtCustomer = new TextBox { Width = 160 };
        UiTheme.StyleTextBox(_txtCustomer);
        _txtCustomer.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LoadList(); e.SuppressKeyPress = true; } };
        panel.Controls.Add(_txtCustomer);
        var lblGoods = new Label { Text = "品名：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblGoods, sub: true);
        panel.Controls.Add(lblGoods);
        _txtGoods = new TextBox { Width = 140 };
        UiTheme.StyleTextBox(_txtGoods);
        _txtGoods.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LoadList(); e.SuppressKeyPress = true; } };
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
            Dock = DockStyle.Fill,
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
        _grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                EditBill();
        };
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

        _loading = true;
        var dt = DbManager.QueryTable(sql, pars.ToArray());
        _grid.DataSource = dt;
        foreach (DataGridViewColumn c in _grid.Columns)
        {
            c.HeaderText = c.Name switch
            {
                "交易單號" => "單號",
                "交易日期" => "交易日期",
                "目前狀態" => "狀態",
                "維修類別" => "類別",
                "交易對象" => "對象編號",
                "客戶名稱" => "客戶名稱",
                "品名" => "品名",
                "總計金額" => "總計金額",
                "叫修日期" => "叫修日期",
                "交貨日期" => "交貨日期",
                "保固日期" => "保固日期",
                _ => c.HeaderText,
            };
            if (c.Name == "總計金額")
                c.DefaultCellStyle.Format = "N2";
        }
        _loading = false;
        _lblRecord.Text = $"記錄: 0 / {dt.Rows.Count}";

        if (dt.Rows.Count > 0)
        {
            _loading = true;
            _grid.Rows[0].Selected = true;
            _grid.CurrentCell = _grid.Rows[0].Cells[0];
            _loading = false;
        }
        else
        {
            _currentRow = null;
            _currentKey = "";
        }
    }

    private void OnRowSelected()
    {
        if (_loading || _grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].IsNewRow)
            return;
        _currentIndex = _grid.SelectedRows[0].Index;
        _lblRecord.Text = $"記錄: {_currentIndex + 1} / {_grid.Rows.Count}";
        var billNo = _grid.SelectedRows[0].Cells["交易單號"].Value as string ?? "";
        LoadBill(billNo);
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

            using var dlg = new RepairEditDialog(row);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            var colNames = new List<string>();
            var pars = new List<SqliteParameter>();
            foreach (DataColumn col in row.Table.Columns)
            {
                colNames.Add(col.ColumnName);
                pars.Add(DbManager.Param($"${col.ColumnName}", row[col] is DBNull ? null : row[col]));
            }
            DbManager.ExecuteNonQuery(
                $"INSERT INTO \"{TableName}\" (\"{string.Join("\",\"", colNames)}\") VALUES ({string.Join(",", colNames.Select(c => $"${c}"))})",
                pars.ToArray());

            MessageBox.Show($"已建立新維修單：{billNo}", "新增", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadList();
            SelectBill(billNo);
            _lblStatus.Text = "狀態: 新增完成";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"新增失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EditBill()
    {
        if (string.IsNullOrEmpty(_currentKey))
        {
            MessageBox.Show("請先選取一筆維修單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            LoadBill(_currentKey);
            using var dlg = new RepairEditDialog(_currentRow!);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;
            SaveBill(_currentRow!);
            _lblStatus.Text = "狀態: 已儲存";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveBill(DataRow row)
    {
        var cols = row.Table.Columns;
        var colNames = new List<string>();
        var pars = new List<SqliteParameter>();
        foreach (DataColumn col in cols)
        {
            colNames.Add(col.ColumnName);
            pars.Add(DbManager.Param($"${col.ColumnName}",
                row[col] is DBNull ? null : row[col]));
        }
        var sets = string.Join(",", colNames.Where(c => c != "交易單號" && c != "單據副碼").Select(c => $"\"{c}\" = ${c}"));
        DbManager.ExecuteNonQuery(
            $"UPDATE \"{TableName}\" SET {sets} WHERE [交易單號] = $交易單號",
            pars.ToArray());
        LoadList();
        SelectBill(_currentKey);
    }

    private void SelectBill(string billNo)
    {
        for (int i = 0; i < _grid.Rows.Count; i++)
        {
            if (string.Equals(_grid.Rows[i].Cells["交易單號"].Value as string, billNo, StringComparison.OrdinalIgnoreCase))
            {
                _loading = true;
                _grid.Rows[i].Selected = true;
                _grid.CurrentCell = _grid.Rows[i].Cells[0];
                _loading = false;
                OnRowSelected();
                return;
            }
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
            _currentKey = "";
            LoadList();
            _lblStatus.Text = "狀態: 已刪除";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刪除失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ==================== 列印 ====================

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
        master["對象名稱"] = LookupCustomerName(row["交易對象"]);
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

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";
}

/// <summary>
/// 維修單編輯對話框：分頁（維修單 / 費用與帳款 / 全部欄位）編輯 94 欄位。
/// 確定後將值寫回資料列（呼叫端再儲存）。
/// </summary>
public sealed class RepairEditDialog : Form
{
    private const string TableName = "維修主檔";

    private readonly DataRow _row;
    private readonly Dictionary<string, Control> _editors = new();

    // 維修單 tab
    private TextBox _txtCustomerName = null!, _txtGoodsName = null!;
    private TextBox _txtFault = null!, _txtCause = null!, _txtSituation = null!, _txtRemark = null!;

    // 全部欄位 tab
    private DataGridView _gridAll = null!;

    private static readonly string[] StatusList = { "未處理", "內修", "外送", "交貨", "結案" };
    private static readonly string[] RepairTypeList = { "保固內維修", "保固外維修", "合約內維修" };
    private static readonly string[] RepairWayList = { "自取", "到府服務", "快遞收送" };
    private static readonly string[] BuyTypeList = { "客戶外購", "公司銷售" };
    private static readonly string[] TimeSlots =
        Enumerable.Range(0, 48).Select(i => $"{(i / 2):00}:{(i % 2 == 0 ? "00" : "30")}").ToArray();

    public RepairEditDialog(DataRow row)
    {
        _row = row;
        Text = row.RowState == DataRowState.Detached ? "新增維修單" : "修改維修單";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UiTheme.Background;
        Font = UiTheme.Font(10F);
        ClientSize = new Size(1000, 720);
        MinimumSize = new Size(900, 640);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        UiTheme.StyleTabControl(tabs);
        tabs.TabPages.Add(BuildRepairCard());
        tabs.TabPages.Add(BuildMoneyTab());
        tabs.TabPages.Add(BuildAllFieldsTab());
        Controls.Add(tabs);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = UiTheme.BorderLight };
        var btnOk = new ModernButton
        {
            Text = "確定",
            Size = new Size(96, 40),
            Location = new Point(ClientSize.Width - 214, 7),
            IsPrimary = true,
        };
        var btnCancel = new ModernButton
        {
            Text = "取消",
            Size = new Size(96, 40),
            Location = new Point(ClientSize.Width - 108, 7),
            IsPrimary = false,
            DrawShadow = false,
        };
        btnOk.Click += (s, e) =>
        {
            ApplyToRow();
            DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        bar.Controls.Add(btnOk);
        bar.Controls.Add(btnCancel);
        Controls.Add(bar);

        Populate();

        UiTheme.ScaleForDpi(this);
        UiTheme.ClampToScreen(this);
    }

    // ==================== 分頁版面 ====================

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
        _txtCustomerName = MakeReadOnlyTextBox();
        var (lblCust, ctrlCust) = CreatePair(panel, "客戶名稱", _txtCustomerName);
        panel.Controls.Add(lblCust, 2, 1);
        panel.Controls.Add(ctrlCust, 3, 1);
        AddEditor(panel, "貨品編號", "貨品編號", 1, 2);
        _txtGoodsName = MakeReadOnlyTextBox();
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
        TextBox AddMemo(string label, int rowIndex)
        {
            var tb = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
            UiTheme.StyleTextBox(tb);
            var (lbl, ctrl) = CreatePair(panel, label, tb);
            panel.Controls.Add(lbl, 0, rowIndex);
            panel.Controls.Add(ctrl, 1, rowIndex);
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
            if (e.RowIndex < 0 || e.RowIndex >= _gridAll.Rows.Count)
                return;
            var row = _gridAll.Rows[e.RowIndex];
            var fieldName = row.Cells["欄位名稱"].Value as string ?? "";
            var newVal = row.Cells["欄位值"].Value as string ?? "";
            if (_row.Table.Columns.Contains(fieldName))
                _row[fieldName] = string.IsNullOrEmpty(newVal) ? DBNull.Value : newVal;
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

    private static (Label, Control) CreatePair(TableLayoutPanel panel, string label, Control ctrl)
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
            if (readOnly)
            {
                tb.ReadOnly = true;
                tb.BackColor = UiTheme.BorderLight;
            }
            UiTheme.StyleTextBox(tb);
            if (multiline)
            {
                tb.Multiline = true;
                tb.ScrollBars = ScrollBars.Vertical;
            }
            ctrl = tb;
        }
        return ctrl;
    }

    private static TextBox MakeReadOnlyTextBox()
    {
        var tb = new TextBox { ReadOnly = true, BackColor = UiTheme.BorderLight };
        UiTheme.StyleTextBox(tb);
        return tb;
    }

    // ==================== 填值 / 收集 ====================

    private void Populate()
    {
        var cols = _row.Table.Columns;
        foreach (var (field, ctrl) in _editors)
        {
            if (!cols.Contains(field)) continue;
            var val = _row[field];
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
        _txtFault.Text = Str(_row["故障現象"]);
        _txtCause.Text = Str(_row["故障原因"]);
        _txtSituation.Text = Str(_row["維修情況"]);
        _txtRemark.Text = Str(_row["備註"]);

        UpdateCustomerName();
        UpdateGoodsName();

        var ed = _editors["交易對象"];
        ed.TextChanged += (s, e) => UpdateCustomerName();
        var eg = _editors["貨品編號"];
        eg.TextChanged += (s, e) => UpdateGoodsName();

        // 全部欄位頁
        var allDt = new DataTable();
        allDt.Columns.Add("欄位名稱", typeof(string));
        allDt.Columns.Add("欄位值", typeof(string));
        foreach (DataColumn col in cols)
        {
            var v = _row[col];
            allDt.Rows.Add(col.ColumnName, v is DBNull or null ? "" : v.ToString());
        }
        _gridAll.DataSource = allDt;
        _gridAll.Columns["欄位名稱"].ReadOnly = true;
        _gridAll.Columns["欄位名稱"].Width = 220;
        _gridAll.Columns["欄位值"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void UpdateCustomerName()
    {
        var id = ((TextBox)_editors["交易對象"]).Text.Trim();
        _txtCustomerName.Text = string.IsNullOrEmpty(id)
            ? ""
            : DbManager.QueryScalar(
                "SELECT [公司簡稱] FROM [客戶廠商] WHERE [客廠編號] = $id",
                DbManager.Param("$id", id)) as string ?? "";
    }

    private void UpdateGoodsName()
    {
        var id = ((TextBox)_editors["貨品編號"]).Text.Trim();
        _txtGoodsName.Text = string.IsNullOrEmpty(id)
            ? ""
            : DbManager.QueryScalar(
                "SELECT [品名] FROM [貨品主檔] WHERE [貨品編號] = $id",
                DbManager.Param("$id", id)) as string ?? "";
    }

    private void ApplyToRow()
    {
        var cols = _row.Table.Columns;
        foreach (var (field, ctrl) in _editors)
        {
            if (!cols.Contains(field)) continue;
            _row[field] = ReadEditor(ctrl);
        }
        _row["故障現象"] = _txtFault.Text;
        _row["故障原因"] = _txtCause.Text;
        _row["維修情況"] = _txtSituation.Text;
        _row["備註"] = _txtRemark.Text;

        // 全部欄位頁：套用未以主編輯控制項編輯的彈性欄位（editor 優先）
        foreach (DataGridViewRow g in _gridAll.Rows)
        {
            var fieldName = g.Cells["欄位名稱"].Value as string ?? "";
            if (fieldName.Length == 0 || _editors.ContainsKey(fieldName) || !cols.Contains(fieldName))
                continue;
            if (fieldName is "故障現象" or "故障原因" or "維修情況" or "備註")
                continue;
            var newVal = g.Cells["欄位值"].Value as string ?? "";
            _row[fieldName] = string.IsNullOrEmpty(newVal) ? DBNull.Value : newVal;
        }
    }

    private static object ReadEditor(Control ctrl)
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

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";
}
