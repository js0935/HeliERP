// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Printing;
using System.Text;

namespace HeliERP.App;

/// <summary>
/// 應收帳款管理（含廠商應付）：對象餘額總覽、未收付明細、帳齡分析。
/// 類別切換（應收帳款／應付帳款）依 ARService.Kinds，資料一律經 ARService 取得。
/// 對象下拉僅供快速定位（捲動並選中總覽該列）；總覽固定顯示該類別全部對象。
/// </summary>
public sealed class AccountReceivableForm : Form
{
    private DataGridView _gridSummary = null!;  // 對象餘額總覽
    private DataGridView _gridDetail = null!;   // 未收付明細
    private DataGridView _gridAging = null!;    // 帳齡分析
    private TabControl _tab = null!;

    private ComboBox _cmbKind = null!;         // 搜尋列：類別（應收帳款／應付帳款）
    private ComboBox _cmbObject = null!;       // 搜尋列：對象（快速定位）

    private Label _lblRecord = null!;
    private Label _lblStatus = null!;

    private bool _loading;

    // 總覽金額欄（自「前期累計應收帳款」起）
    private static readonly string[] _summaryMoneyColumns =
        { "前期累計應收帳款", "本期總計", "折讓金額", "已收付金額", "累計預收貨款", "未收付合計" };

    // 未收付明細金額欄
    private static readonly string[] _detailMoneyColumns =
        { "總計金額", "折讓金額", "已收付金額", "未收付金額" };

    /// <summary>開啟帳款管理；<paramref name="初始顯示類別"/> 指定後直接切至該類別（預設應收）。</summary>
    public AccountReceivableForm(string? 初始顯示類別 = null)
    {
        Text = "應收帳款管理";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        BackColor = UiTheme.Background;

        var header = UiTheme.BuildHeader("應收帳款管理", "客戶應收／廠商應付帳款查詢與帳齡分析");
        header.Dock = DockStyle.Top;
        Controls.Add(header);

        BuildToolbar();
        BuildSearchPanel();
        BuildSummaryGrid();
        BuildTabPanel();
        BuildStatusBar();

        Load += (s, e) =>
        {
            _cmbKind.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(初始顯示類別))
            {
                int idx = Array.IndexOf(ARService.Kinds, 初始顯示類別);
                if (idx >= 0) _cmbKind.SelectedIndex = idx;
            }
            _lblStatus.Text = "狀態: 就緒";
        };

        ShortcutHelper.Enable(this, onSearch: LoadSummary, onReload: LoadSummary);
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

        var btnSearch = new ModernButton { Text = "查詢", Width = 120 };
        btnSearch.Click += (s, e) => LoadSummary();
        var btnReload = new ModernButton { Text = "重讀", Width = 120, IsPrimary = false };
        btnReload.Click += (s, e) => LoadSummary();
        var btnPrint = new ModernButton { Text = "列印", Width = 120, IsPrimary = false };
        btnPrint.Click += (s, e) => ShowPrintMenu();
        var btnPeriod = new ModernButton { Text = "期間設定", Width = 120, IsPrimary = false };
        btnPeriod.Click += (s, e) => ShowAgePeriodDialog();
        var btnExport = new ModernButton { Text = "匯出 CSV", Width = 120, IsPrimary = false };
        btnExport.Click += (s, e) => ExportCsv();
        var btnHelp = new ModernButton { Text = "說明", Width = 120, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show(
                "應收帳款管理功能說明：\n" +
                "1. 對象餘額總覽：顯示該類別（應收／應付）全部客廠之帳款彙總，選取對象後下方顯示其未收付明細與帳齡分析。\n" +
                "2. 帳齡分析：以基準日＝今天，依帳款簡要交易日期分桶至第一～第六期間；超過第六期間歸期初。\n" +
                "3. 負數單據（出退／進退抵銷）列為貸項；前期累計應收帳款歸期初。\n" +
                "4. 「期間設定」可調整六個期間天數；「匯出 CSV」可匯出目前分頁資料。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 120, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSearch); Add(btnReload); Add(btnPrint); Add(btnPeriod); Add(btnExport); Add(btnHelp); Add(btnExit);

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

        var lblKind = new Label { Text = "類別：", Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblKind, sub: true);
        panel.Controls.Add(lblKind);
        _cmbKind = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbKind);
        _cmbKind.Items.AddRange(ARService.Kinds);
        _cmbKind.SelectedIndexChanged += (s, e) =>
        {
            if (_loading) return;
            ReloadObjectCombo();
            LoadSummary();
        };
        panel.Controls.Add(_cmbKind);

        var lblObject = new Label { Text = "對象：", Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0) };
        UiTheme.StyleLabel(lblObject, sub: true);
        panel.Controls.Add(lblObject);
        _cmbObject = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        UiTheme.StyleComboBox(_cmbObject);
        panel.Controls.Add(_cmbObject);

        var btnSearch = new ModernButton { Text = "查詢", Width = 84, Height = 34, IsPrimary = true };
        btnSearch.Margin = new Padding(UiTheme.SpacingMd, UiTheme.SpacingSm, 0, 0);
        btnSearch.Click += (s, e) => LoadSummary();
        panel.Controls.Add(btnSearch);
        var btnClear = new ModernButton { Text = "清除條件", Width = 96, Height = 34, IsPrimary = false };
        btnClear.Margin = new Padding(UiTheme.SpacingSm, UiTheme.SpacingSm, 0, 0);
        btnClear.Click += (s, e) =>
        {
            _cmbObject.SelectedIndex = -1;
            LoadSummary();
        };
        panel.Controls.Add(btnClear);

        card.Controls.Add(panel);
        Controls.Add(card);
    }

    private void BuildSummaryGrid()
    {
        _gridSummary = new DataGridView
        {
            Dock = DockStyle.Top,
            Height = 190,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            RowHeadersVisible = true,
            RowHeadersWidth = 52,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        UiTheme.StyleDataGridView(_gridSummary);
        _gridSummary.RowTemplate.Height = 32;
        _gridSummary.SelectionChanged += (s, e) => LoadDetailAndAging();
        Controls.Add(_gridSummary);
    }

    private void BuildTabPanel()
    {
        _tab = new TabControl { Dock = DockStyle.Fill };

        var tabDetail = new TabPage("未收付明細") { Name = "未收付明細", Padding = new Padding(UiTheme.SpacingSm) };
        _gridDetail = new DataGridView
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
        UiTheme.StyleDataGridView(_gridDetail);
        _gridDetail.RowTemplate.Height = 30;
        tabDetail.Controls.Add(_gridDetail);

        var tabAging = new TabPage("帳齡分析") { Name = "帳齡分析", Padding = new Padding(UiTheme.SpacingSm) };
        _gridAging = new DataGridView
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
        UiTheme.StyleDataGridView(_gridAging);
        _gridAging.RowTemplate.Height = 30;
        tabAging.Controls.Add(_gridAging);

        _tab.TabPages.Add(tabDetail);
        _tab.TabPages.Add(tabAging);
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

    private void ReloadObjectCombo()
    {
        var 類別 = ARService.客廠類別For(ARService.Kinds[Math.Max(0, _cmbKind.SelectedIndex)]);
        var dt = ARService.LoadObjectCombo(類別);
        dt.Columns.Add("顯示", typeof(string));
        foreach (DataRow r in dt.Rows)
        {
            var 簡稱 = r["公司簡稱"] is DBNull ? "" : r["公司簡稱"].ToString();
            r["顯示"] = string.IsNullOrWhiteSpace(簡稱) ? r["客廠編號"].ToString() : $"{r["客廠編號"]}  {簡稱}";
        }
        _loading = true;
        _cmbObject.DataSource = dt;
        _cmbObject.DisplayMember = "顯示";
        _cmbObject.ValueMember = "客廠編號";
        _cmbObject.SelectedIndex = -1;
        _loading = false;
    }

    private void LoadSummary()
    {
        var 類別 = ARService.客廠類別For(ARService.Kinds[Math.Max(0, _cmbKind.SelectedIndex)]);
        var dt = ARService.LoadObjectSummary(類別);

        _loading = true;
        _gridSummary.DataSource = dt;
        foreach (DataGridViewColumn c in _gridSummary.Columns)
        {
            if (_summaryMoneyColumns.Contains(c.Name))
            {
                c.DefaultCellStyle.Format = "N2";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }
        if (_gridSummary.Columns.Contains("未收付合計"))
            _gridSummary.Columns["未收付合計"].DefaultCellStyle.Font = UiTheme.Font(10F, FontStyle.Bold);
        _loading = false;

        // 對象下拉有選值時捲動並選中該列；否則自動選第一列載入明細／帳齡
        var no = _cmbObject.SelectedValue as string;
        if (!string.IsNullOrEmpty(no) && ContainsObject(no))
            SelectObjectByNo(no);
        else if (dt.Rows.Count > 0)
        {
            _loading = true;
            _gridSummary.Rows[0].Selected = true;
            _gridSummary.CurrentCell = _gridSummary.Rows[0].Cells[0];
            _loading = false;
            LoadDetailAndAging();
        }
        else
        {
            _gridDetail.DataSource = null;
            _gridAging.DataSource = null;
            _lblRecord.Text = "記錄: 0 / 0";
        }
    }

    private void LoadDetailAndAging()
    {
        if (_loading) return;
        if (_gridSummary.SelectedRows.Count == 0 || _gridSummary.SelectedRows[0].IsNewRow)
        {
            _gridDetail.DataSource = null;
            _gridAging.DataSource = null;
            _lblRecord.Text = $"記錄: 0 / {_gridSummary.Rows.Count}";
            return;
        }
        var row = _gridSummary.SelectedRows[0];
        var 對象 = Str(row.Cells["交易對象"].Value);
        _lblRecord.Text = $"記錄: {row.Index + 1} / {_gridSummary.Rows.Count}";
        var 期間 = ARService.LoadAgePeriods();
        BindDetail(ARService.LoadOpenDetails(對象));
        BindAging(ARService.AgingAnalysis(對象, 期間, DateTime.Today));
    }

    private void SelectObjectByNo(string no)
    {
        foreach (DataGridViewRow r in _gridSummary.Rows)
        {
            if (Str(r.Cells["交易對象"].Value) == no)
            {
                _loading = true;
                r.Selected = true;
                _gridSummary.CurrentCell = r.Cells[0];
                _gridSummary.FirstDisplayedScrollingRowIndex = r.Index;
                _loading = false;
                LoadDetailAndAging();
                return;
            }
        }
        if (_gridSummary.Rows.Count > 0)
        {
            _loading = true;
            _gridSummary.Rows[0].Selected = true;
            _gridSummary.CurrentCell = _gridSummary.Rows[0].Cells[0];
            _loading = false;
            LoadDetailAndAging();
        }
    }

    private bool ContainsObject(string no)
    {
        foreach (DataGridViewRow r in _gridSummary.Rows)
            if (Str(r.Cells["交易對象"].Value) == no) return true;
        return false;
    }

    private void BindDetail(DataTable dt)
    {
        _loading = true;
        _gridDetail.DataSource = dt;
        foreach (DataGridViewColumn c in _gridDetail.Columns)
        {
            if (c.Name == "發票號碼")
                c.FillWeight = 60;
            if (_detailMoneyColumns.Contains(c.Name))
            {
                c.DefaultCellStyle.Format = "N2";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (c.Name == "交易日期" && c.ValueType == typeof(DateTime))
                c.DefaultCellStyle.Format = "yyyy-MM-dd";
        }
        _loading = false;
    }

    private void BindAging(DataTable dt)
    {
        _loading = true;
        _gridAging.DataSource = dt;
        foreach (DataGridViewColumn c in _gridAging.Columns)
        {
            if (c.Name == "期初帳款" || c.Name == "貸項" || c.Name == "合計" || ARService.期間欄位.Contains(c.Name))
            {
                c.DefaultCellStyle.Format = "N2";
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }
        if (_gridAging.Columns.Contains("合計"))
            _gridAging.Columns["合計"].DefaultCellStyle.Font = UiTheme.Font(10F, FontStyle.Bold);
        _loading = false;
    }

    // ==================== 期間設定 ====================

    private void ShowAgePeriodDialog()
    {
        var p = ARService.LoadAgePeriods();
        using var dlg = new Form
        {
            Text = "帳齡期間設定",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(420, 280),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(11F),
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 132,
            Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingLg, UiTheme.SpacingLg, 0),
        };
        table.ColumnCount = 4;
        table.RowCount = 3;
        foreach (int w in new[] { 80, 100, 80, 100 })
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
        for (int i = 0; i < 3; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        int[] 初始 = { p.第一期間, p.第二期間, p.第三期間, p.第四期間, p.第五期間, p.第六期間 };
        var nud = new NumericUpDown[6];
        for (int i = 0; i < 6; i++)
        {
            var lbl = new Label
            {
                Text = ARService.期間欄位[i],
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(UiTheme.SpacingXs, 0, 0, 0),
                ForeColor = UiTheme.TextSub,
                Font = UiTheme.Font(10.5F),
            };
            nud[i] = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 3650,
                Value = 初始[i],
                Dock = DockStyle.Fill,
                Margin = new Padding(UiTheme.SpacingSm, 0, UiTheme.SpacingLg, 0),
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiTheme.Font(10.5F),
                TextAlign = HorizontalAlignment.Right,
            };
            int col = (i % 2) * 2, row = i / 2;
            table.Controls.Add(lbl, col, row);
            table.Controls.Add(nud[i], col + 1, row);
        }
        dlg.Controls.Add(table);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(UiTheme.SpacingLg, UiTheme.SpacingMd, UiTheme.SpacingLg, UiTheme.SpacingSm),
            BackColor = UiTheme.Card,
        };
        var btnOk = new ModernButton { Text = "確定", Width = 96, Height = 36, DrawShadow = false };
        btnOk.Click += (s, e) =>
        {
            int[] 值 = new int[6];
            for (int i = 0; i < 6; i++) 值[i] = Convert.ToInt32(nud[i].Value);
            for (int i = 1; i < 6; i++)
            {
                if (值[i] <= 值[i - 1])
                {
                    MessageBox.Show("期間天數必須由小到大遞增", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            p.第一期間 = 值[0];
            p.第二期間 = 值[1];
            p.第三期間 = 值[2];
            p.第四期間 = 值[3];
            p.第五期間 = 值[4];
            p.第六期間 = 值[5];
            try
            {
                ARService.SaveAgePeriods(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            dlg.DialogResult = DialogResult.OK;
        };
        var btnCancel = new ModernButton { Text = "取消", Width = 96, Height = 36, IsPrimary = false, DrawShadow = false };
        btnCancel.Click += (s, e) => dlg.DialogResult = DialogResult.Cancel;
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(btnCancel);
        dlg.Controls.Add(bottom);
        UiTheme.ScaleForDpi(dlg);
        UiTheme.ClampToScreen(dlg);

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            LoadDetailAndAging();
            _lblStatus.Text = "狀態: 期間設定已更新";
            MessageBox.Show("帳齡期間設定已儲存。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ==================== 列印報表 ====================

    private static string ReportDir => ReportPrintService.RepDirectory;

    /// <summary>目前選取列的交易對象（無選取回傳空字串）。</summary>
    private string CurrentObject() =>
        _gridSummary.SelectedRows.Count > 0 ? Str(_gridSummary.SelectedRows[0].Cells["交易對象"].Value) : "";

    private string CurrentKind() =>
        ARService.客廠類別For(ARService.Kinds[Math.Max(0, _cmbKind.SelectedIndex)]);

    private void ShowPrintMenu()
    {
        bool 應付 = CurrentKind() == ARService.應付類別;
        string 前綴 = 應付 ? "應付" : "應收";
        var menu = new ContextMenuStrip();
        menu.Items.Add($"{前綴}帳款統計表（全部對象）", null, (s, e) => PrintStatReport());
        if (!應付)
            menu.Items.Add("應收帳款帳齡分析（全部對象）", null, (s, e) => PrintAgingReport());
        menu.Items.Add($"{前綴}帳款明細表（選取對象）", null, (s, e) => PrintDetailReport());
        menu.Items.Add($"{前綴}帳款簡要表（選取對象）", null, (s, e) => PrintBriefReport());
        menu.Show(Cursor.Position);
    }

    private void PrintStatReport()
    {
        var data = ARService.BuildSummaryReportData(CurrentKind());
        if (data.Detail.Count == 0)
        {
            MessageBox.Show("目前沒有帳款彙總資料可列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string 標題 = CurrentKind() == ARService.應付類別 ? "應付帳款統計表" : "應收帳款統計表";
        PrintReport(CurrentKind() == ARService.應付類別 ? "應付帳款統計表.rtm" : "應收帳款統計表.rtm", data, 標題);
    }

    private void PrintAgingReport()
    {
        var data = ARService.BuildAgingReportData(CurrentKind());
        if (data.Detail.Count == 0)
        {
            MessageBox.Show("目前沒有帳款帳齡資料可列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        PrintReport("應收帳款帳齡分析.rtm", data, "應收帳款帳齡分析");
    }

    private void PrintDetailReport()
    {
        var 對象 = CurrentObject();
        if (string.IsNullOrEmpty(對象))
        {
            MessageBox.Show("請先在總覽選取一個對象，再列印明細表。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var data = ARService.BuildDetailReportData(對象);
        if (data is null)
        {
            MessageBox.Show($"找不到對象「{對象}」的帳款主檔資料。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (data.Detail.Count == 0)
        {
            MessageBox.Show($"對象「{對象}」沒有明細資料可列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string 標題 = CurrentKind() == ARService.應付類別 ? "應付帳款明細表" : "應收帳款明細表";
        PrintReport(CurrentKind() == ARService.應付類別 ? "應付帳款明細表.rtm" : "應收帳款明細表.rtm", data, $"{標題}-{對象}");
    }

    private void PrintBriefReport()
    {
        var 對象 = CurrentObject();
        if (string.IsNullOrEmpty(對象))
        {
            MessageBox.Show("請先在總覽選取一個對象，再列印簡要表。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var data = ARService.BuildBriefReportData(對象, CurrentKind());
        if (data is null)
        {
            MessageBox.Show($"找不到對象「{對象}」的帳款主檔資料。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (data.Detail.Count == 0)
        {
            MessageBox.Show($"對象「{對象}」沒有未收付單據可列印。", "列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string 標題 = CurrentKind() == ARService.應付類別 ? "應付帳款簡要表" : "應收帳款簡要表";
        PrintReport(CurrentKind() == ARService.應付類別 ? "應付帳款簡要表.rtm" : "應收帳款簡要表.rtm", data, $"{標題}-{對象}");
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

    // ==================== 匯出 CSV ====================

    private void ExportCsv()
    {
        var grid = _tab.SelectedTab?.Name == "未收付明細" ? _gridDetail : _gridAging;
        if (ExportService.ExportGrid(this, grid, "應收帳款-明細.csv", "匯出 CSV"))
            _lblStatus.Text = "狀態: 已匯出 CSV";
    }

    // ==================== 工具 ====================

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";
}
