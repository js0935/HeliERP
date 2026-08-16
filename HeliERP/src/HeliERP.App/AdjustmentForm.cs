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
/// 庫存調整單：盤點盤盈／盤虧、報廢、贈品、損耗等非進出貨庫存異動。
/// 上區編輯調整單（新增），下區為既有調整單清單（檢視／刪除）。
/// </summary>
public sealed class AdjustmentForm : Form
{
    private TextBox _txtNo = null!;
    private DateTimePicker _dtpDate = null!;
    private ComboBox _cmbReason = null!;
    private TextBox _txtRemark = null!;
    private DataGridView _gridDetail = null!;
    private DataGridView _gridList = null!;
    private TextBox _txtFilterNo = null!;

    private Label _lblQtyTotal = null!;
    private Label _lblStatus = null!;

    private bool _loading;
    private bool _viewing;   // 檢視模式：清單選取後載入，禁止儲存
    private long _viewingKey;   // 目前檢視中調整單的單據副碼（0 = 無）

    public AdjustmentForm()
    {
        Text = "庫存調整單";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        BackColor = UiTheme.Background;

        var header = UiTheme.BuildHeader("庫存調整單", "盤點盤盈／盤虧、報廢、贈品、損耗等非進出貨庫存異動");
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
                _cmbReason.Items.AddRange(AdjustmentService.調整原因);
                _cmbReason.SelectedIndex = 0;
                _txtNo.Text = AdjustmentService.PreviewAdjustmentNo();
                LoadList();
                _lblStatus.Text = "狀態: 就緒";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "狀態: 載入失敗 - " + ex.Message;
            }
        };

        ShortcutHelper.Enable(this, onDelete: DeleteSelected, onSearch: LoadList);
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

        var btnSave = new ModernButton { Text = "儲存調整單", Width = 140 };
        btnSave.Click += (s, e) => Save();
        var btnLoadView = new ModernButton { Text = "載入檢視", Width = 120, IsPrimary = false };
        btnLoadView.Click += (s, e) => LoadSelectedView();
        var btnDelete = new ModernButton { Text = "刪除調整單", Width = 120, IsPrimary = false };
        btnDelete.Click += (s, e) => DeleteSelected();
        var btnPrint = new ModernButton { Text = "列印", Width = 120, IsPrimary = false };
        btnPrint.Click += (s, e) => PrintBill();
        var btnReload = new ModernButton { Text = "重讀", Width = 120, IsPrimary = false };
        btnReload.Click += (s, e) => { ClearEditor(); LoadList(); };
        var btnHelp = new ModernButton { Text = "說明", Width = 120, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show(
                "庫存調整單功能說明：\n" +
                "1. 調整數量為帶方向之數值：正數 = 盤盈（庫存增加）、負數 = 盤虧（庫存減少）。\n" +
                "2. 輸入貨品編號與倉庫後，畫面自動帶入品名、目前庫存與安全存量供參考。\n" +
                "3. 調整單不產生帳款，僅異動貨品庫存並記錄於庫存異動歷史。\n" +
                "4. 下方清單可載入檢視或刪除既有調整單（刪除會回復庫存）。\n" +
                "5. 若庫存參數開啟「檢查庫存量」，盤虧不得低於 0。",
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
            ColumnCount = 6,
            RowCount = 1,
            AutoSize = true,
            BackColor = UiTheme.Card,
        };
        for (int i = 0; i < 6; i++)
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddPair(panel, "調整單號", _txtNo = new TextBox { Width = 110, ReadOnly = true, BackColor = UiTheme.BorderLight }, 0);
        AddPair(panel, "調整日期", _dtpDate = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short }, 1);
        AddPair(panel, "調整原因", _cmbReason = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList }, 2);
        AddPair(panel, "備註", _txtRemark = new TextBox { Width = 300 }, 3);

        card.Controls.Add(panel);
        Controls.Add(card);
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
            Text = "調整明細",
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
        _lblQtyTotal = new Label
        {
            Text = "調整數量合計: 0",
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(bar.Width - 220, 10),
            ForeColor = UiTheme.Primary,
            Font = UiTheme.Font(11F, FontStyle.Bold),
        };
        bar.Resize += (s, e) => _lblQtyTotal.Location = new Point(bar.Width - 240, 10);
        bar.Controls.Add(_lblQtyTotal);
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
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "品名", HeaderText = "品名", Width = 160, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "倉庫", HeaderText = "倉庫", Width = 60 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "調整數量", HeaderText = "調整數量", Width = 90 });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "目前庫存", HeaderText = "目前庫存", Width = 80, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "安全存量", HeaderText = "安全存量", Width = 80, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "單位", HeaderText = "單位", Width = 48, ReadOnly = true });
        _gridDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "附註說明", HeaderText = "附註說明", Width = 180 });
        _gridDetail.Columns["貨品編號"].Frozen = true;

        _gridDetail.CellEndEdit += OnDetailCellEndEdit;
        _gridDetail.CellValueChanged += OnDetailCellValueChanged;
        _gridDetail.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = _gridDetail.Columns[e.ColumnIndex];
            if (col.Name != "調整數量") return;
            decimal q = Dec(_gridDetail.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
            if (e.CellStyle is null) return;
            e.CellStyle.ForeColor = q < 0 ? UiTheme.Danger : UiTheme.Ok;
        };
        card.Controls.Add(_gridDetail);
        Controls.Add(card);
    }

    private void BuildListCard()
    {
        var card = new Panel { Dock = DockStyle.Bottom, Height = 200, BackColor = UiTheme.Background, Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingXs, UiTheme.SpacingLg, UiTheme.SpacingSm) };

        var bar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = UiTheme.Card };
        var lbl = new Label
        {
            Text = "調整單清單",
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
        Controls.Add(bar);
    }

    // ==================== 明細編輯 ====================

    private void AddDetailRow()
    {
        if (_viewing) return;
        int i = _gridDetail.Rows.Add();
        var row = _gridDetail.Rows[i];
        row.Cells["倉庫"].Value = TradeService.LoadParams().常用倉庫;
        _gridDetail.CurrentCell = row.Cells["貨品編號"];
        RecalcQtyTotal();
    }

    private void RemoveDetailRow()
    {
        if (_viewing) return;
        if (_gridDetail.SelectedCells.Count == 0) return;
        int i = _gridDetail.SelectedCells[0].RowIndex;
        if (i < 0 || i >= _gridDetail.Rows.Count) return;
        _gridDetail.Rows.RemoveAt(i);
        RecalcQtyTotal();
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
            if (row.Cells["倉庫"].Value is null or DBNull or "")
                row.Cells["倉庫"].Value = TradeService.LoadParams().常用倉庫;
            _loading = false;
            FillStockInfo(e.RowIndex);
        }
        else if (col == "倉庫")
        {
            if (row.Cells["貨品編號"].Value is null or DBNull or "") return;
            FillStockInfo(e.RowIndex);
        }
    }

    private void OnDetailCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || _viewing || e.RowIndex < 0) return;
        if (_gridDetail.Columns[e.ColumnIndex].Name == "調整數量")
            RecalcQtyTotal();
    }

    private void FillStockInfo(int rowIndex)
    {
        var row = _gridDetail.Rows[rowIndex];
        var code = (row.Cells["貨品編號"].Value as string ?? "").Trim();
        var wh = (row.Cells["倉庫"].Value as string ?? "").Trim();
        if (code.Length == 0) return;
        var info = AdjustmentService.LoadStockInfo(code, wh);
        _loading = true;
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
        _loading = false;
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

    // ==================== 存檔 / 檢視 / 刪除 ====================

    private void Save()
    {
        if (_viewing)
        {
            MessageBox.Show("目前為檢視模式（已載入既有調整單），請按「重讀」開始新單。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var req = new AdjustmentService.AdjustmentRequest
        {
            調整日期 = _dtpDate.Value.Date,
            原因 = _cmbReason.SelectedItem as string ?? "",
            備註 = _txtRemark.Text.Trim(),
        };
        foreach (DataGridViewRow r in _gridDetail.Rows)
        {
            if (r.IsNewRow) continue;
            var code = (r.Cells["貨品編號"].Value as string ?? "").Trim();
            if (code.Length == 0) continue;
            req.明細.Add(new AdjustmentService.AdjustmentLine
            {
                貨品編號 = code,
                倉庫編號 = (r.Cells["倉庫"].Value as string ?? "").Trim(),
                數量 = Dec(r.Cells["調整數量"].Value),
                單位 = (r.Cells["單位"].Value as string ?? "").Trim(),
                附註說明 = (r.Cells["附註說明"].Value as string ?? "").Trim(),
            });
        }
        try
        {
            string no = AdjustmentService.SaveAdjustment(req);
            MessageBox.Show($"調整單「{no}」已儲存，庫存已更新。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearEditor();
            LoadList();
            _lblStatus.Text = $"狀態: 已儲存 {no}";
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
        _cmbReason.SelectedIndex = 0;
        _dtpDate.Value = DateTime.Now;
        _txtNo.Text = AdjustmentService.PreviewAdjustmentNo();
        _gridDetail.Rows.Clear();
        RecalcQtyTotal();
        _lblStatus.Text = "狀態: 就緒";
    }

    private void LoadList()
    {
        var dt = AdjustmentService.LoadAdjustmentList(_txtFilterNo.Text.Trim());
        _loading = true;
        _gridList.DataSource = dt;
        if (_gridList.Columns.Contains("數量合計"))
            _gridList.Columns["數量合計"].DefaultCellStyle.Format = "N2";
        _loading = false;
    }

    private void LoadSelectedView()
    {
        if (_gridList.SelectedRows.Count == 0 || _gridList.SelectedRows[0].IsNewRow)
        {
            MessageBox.Show("請先於清單選取一筆調整單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var row = _gridList.SelectedRows[0];
        long 副碼 = Convert.ToInt64(row.Cells["單據副碼"].Value);
        var dt = AdjustmentService.LoadAdjustmentDetails(副碼);

        _viewing = true;
        _viewingKey = 副碼;
        _txtNo.Text = Str(row.Cells["交易單號"].Value);
        _dtpDate.Value = DateTime.TryParse(Str(row.Cells["交易日期"].Value), out var d) ? d : DateTime.Now;
        _txtRemark.Text = Str(row.Cells["備註"].Value);
        _cmbReason.SelectedIndex = _cmbReason.Items.IndexOf("其他");

        _gridDetail.Rows.Clear();
        foreach (DataRow r in dt.Rows)
        {
            int i = _gridDetail.Rows.Add();
            var gr = _gridDetail.Rows[i];
            gr.Cells["貨品編號"].Value = Str(r["貨品編號"]);
            gr.Cells["品名"].Value = Str(r["品名"]);
            gr.Cells["倉庫"].Value = Str(r["倉庫編號"]);
            gr.Cells["調整數量"].Value = r["調整數量"];
            gr.Cells["單位"].Value = Str(r["單位"]);
            gr.Cells["附註說明"].Value = Str(r["附註說明"]);
            FillStockInfo(i);
        }
        RecalcQtyTotal();
        _lblStatus.Text = $"狀態: 檢視 {_txtNo.Text}（刪除請用工具列按鈕）";
    }

    private void DeleteSelected()
    {
        if (_gridList.SelectedRows.Count == 0 || _gridList.SelectedRows[0].IsNewRow)
        {
            MessageBox.Show("請先於清單選取一筆調整單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var row = _gridList.SelectedRows[0];
        string 單號 = Str(row.Cells["交易單號"].Value);
        long 副碼 = Convert.ToInt64(row.Cells["單據副碼"].Value);
        var confirm = MessageBox.Show($"確定刪除調整單「{單號}」？刪除後將回復庫存。",
            "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        try
        {
            AdjustmentService.DeleteAdjustment(副碼);
            MessageBox.Show($"調整單「{單號}」已刪除，庫存已回復。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show("請先於清單選取並載入一筆調整單，再按列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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
        var data = BuildRtmData();
        var billNo = data.Master.TryGetValue("交易單號", out var no) ? Str(no) : _viewingKey.ToString();

        var state = new RtmRenderState();
        using var renderer = new RtmRenderer(report, data);
        using var doc = new PrintDocument
        {
            DocumentName = $"庫存調整單-{billNo}",
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

    /// <summary>建立報表資料：主檔（ppDBPipeline1）+ 公司（plCompany）+ 明細（ppDBPipeline2，join 品名）。</summary>
    private RtmData BuildRtmData()
    {
        var data = new RtmData();

        // 主檔（調整單與交易單據共用交易主檔；金額欄位為 0、無交易對象）
        var dt = DbManager.QueryTable(
            "SELECT * FROM [交易主檔] WHERE [單據副碼] = $c",
            DbManager.Param("$c", _viewingKey));
        if (dt.Rows.Count == 0) return data;
        var row = dt.Rows[0];
        foreach (DataColumn col in dt.Columns)
            data.Master[col.ColumnName] = row[col];

        // 公司基本資料（plCompany）
        var company = new CompanyInfo();
        data.Company["公司全名"] = company.CompanyName;
        data.Company["電話號碼"] = company.Phone;
        data.Company["登記地址"] = company.Address;
        data.Company["傳真號碼"] = LookupCompanyFax(company.CompanyName);

        // 明細（交易明細，品名於存檔時寫入——調整單報表列示品名）
        var detailDt = DbManager.QueryTable(
            "SELECT * FROM [交易明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
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
