// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 票據系統：應收票據（收票）／應付票據（付票）之建立、查詢、狀態管理與報表。
/// 資料表：票據收付。狀態：尚未 / 託收中 / 已兌 / 退票 / 作廢。
/// </summary>
public sealed class BillModuleForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly ComboBox _cmbKind = new(), _cmbStatus = new();
    private readonly DateTimePicker _dtFrom = new(), _dtTo = new();
    private readonly TextBox _txtKeyword = new();
    private readonly ToolStripStatusLabel _lblCount = new(), _lblTotal = new();

    private static readonly string[] 現況選項 = { "尚未", "託收中", "已兌", "退票", "作廢" };

    public BillModuleForm()
    {
        Text = "票據系統 - 票據管理";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        MinimumSize = new Size(1100, 660);
        UiTheme.Apply(this);
        Controls.Add(UiTheme.BuildHeader("票據系統", "應收票據（收票）／應付票據（付票）管理與狀態作業"));

        BuildToolbar();
        BuildFilterBar();
        BuildGrid();
        BuildStatusBar();

        _cmbKind.SelectedIndex = 0;
        _cmbStatus.SelectedIndex = 0;
        _dtFrom.Value = new DateTime(DateTime.Today.Year, 1, 1);
        _dtTo.Value = DateTime.Today.AddYears(1);
        LoadList();

        ShortcutHelper.Enable(this, () => EditBill(null), () => EditBill(GetSelectedRow()), DeleteSelected, LoadList);
        UiTheme.ClampToScreen(this);
    }

    // ==================== UI ====================

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
        void Add(ModernButton b) { b.Location = new Point(x, 6); b.Height = 40; b.DrawShadow = false; bar.Controls.Add(b); x += b.Width + UiTheme.SpacingSm; }
        void Sep() { bar.Controls.Add(new Panel { Location = new Point(x, 10), Size = new Size(2, 32), BackColor = Color.FromArgb(70, Color.White) }); x += UiTheme.SpacingSm + 2; }

        var btnSearch = new ModernButton { Text = "搜尋", Width = 110 };
        btnSearch.Click += (s, e) => { LoadList(); _txtKeyword.Focus(); };
        var btnNew = new ModernButton { Text = "新增票據", Width = 130 };
        btnNew.Click += (s, e) => EditBill(null);
        var btnEdit = new ModernButton { Text = "修改", Width = 100, IsPrimary = false };
        btnEdit.Click += (s, e) => EditBill(GetSelectedRow());
        var btnDel = new ModernButton { Text = "刪除", Width = 100, IsPrimary = false };
        btnDel.Click += (s, e) => DeleteSelected();
        Sep();
        var btnClear = new ModernButton { Text = "標記兌現", Width = 110, IsPrimary = false };
        btnClear.Click += (s, e) => SetStatus("已兌");
        var btnReject = new ModernButton { Text = "標記退票", Width = 110, IsPrimary = false };
        btnReject.Click += (s, e) => SetStatus("退票");
        var btnVoid = new ModernButton { Text = "標記作廢", Width = 110, IsPrimary = false };
        btnVoid.Click += (s, e) => SetStatus("作廢");
        var btnTrust = new ModernButton { Text = "標記託收中", Width = 120, IsPrimary = false };
        btnTrust.Click += (s, e) => SetStatus("託收中");
        Sep();
        var btnReport = new ModernButton { Text = "報表 ▾", Width = 100, IsPrimary = false };
        btnReport.Click += (s, e) => ShowReportMenu(btnReport);
        var btnCsv = new ModernButton { Text = "匯出 CSV", Width = 110, IsPrimary = false };
        btnCsv.Click += (s, e) => ExportCsv();

        Add(btnSearch); Add(btnNew); Add(btnEdit); Add(btnDel);
        Sep();
        Add(btnTrust); Add(btnClear); Add(btnReject); Add(btnVoid);
        Sep();
        Add(btnReport); Add(btnCsv);
        Controls.Add(bar);
    }

    private void BuildFilterBar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(243, 245, 248), Padding = new Padding(UiTheme.SpacingMd, 10, UiTheme.SpacingMd, 8) };
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

        _cmbKind.Items.AddRange(new object[] { "全部", "收票", "付票" });
        _cmbStatus.Items.AddRange(new object[] { "全部現況", "尚未", "託收中", "已兌", "退票", "作廢" });
        _dtFrom.Format = _dtTo.Format = DateTimePickerFormat.Short;
        _txtKeyword.PlaceholderText = "支票號碼 / 來往對象";

        Field("類別", _cmbKind, 90);
        Field("現況", _cmbStatus, 100);
        Field("收票日從", _dtFrom, 110);
        Field("至", _dtTo, 110);
        Field("關鍵字", _txtKeyword, 220);
        bar.Controls.Add(new Label { Text = "（無欄位時留空 = 全部）", Font = UiTheme.Font(9F), ForeColor = UiTheme.TextFaint, AutoSize = true, Location = new Point(x + 6, 18) });
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
        var bar = new StatusStrip { SizingGrip = false };
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
        var where = new System.Text.StringBuilder("WHERE 1=1");
        if (_cmbKind.SelectedIndex > 0)
            where.Append($" AND [收付類別] = $kind");
        if (_cmbStatus.SelectedIndex > 0)
            where.Append($" AND [票據現況] = $status");
        where.Append(" AND [收開票日] >= $from AND [收開票日] <= $to");
        if (!string.IsNullOrWhiteSpace(_txtKeyword.Text))
        {
            where.Append(" AND ([支票號碼] LIKE $kw OR [來往對象] LIKE $kw OR [支票抬頭] LIKE $kw)");
        }

        var sql =
            "SELECT rowid AS __rid, [收付類別], [支票號碼], [支票抬頭], [來往對象], [票面金額], [票面銀行], " +
            "[票據現況], [收開票日], [到期日], [預兌日], [銀行帳戶], [備註] " +
            $"FROM [票據收付] {where} ORDER BY [收開票日], [支票號碼]";

        var dt = DbManager.QueryTable(sql,
            DbManager.Param("$kind", _cmbKind.SelectedItem?.ToString()),
            DbManager.Param("$status", _cmbStatus.SelectedItem?.ToString()),
            DbManager.Param("$from", _dtFrom.Value.ToString("yyyy-MM-dd 00:00:00")),
            DbManager.Param("$to", _dtTo.Value.ToString("yyyy-MM-dd 23:59:59")),
            DbManager.Param("$kw", $"%{_txtKeyword.Text.Trim()}%"));

        _grid.DataSource = dt;
        _grid.Columns["__rid"].Visible = false;
        if (_grid.Columns.Count > 0)
        {
            _grid.Columns["收付類別"].Width = 70;
            _grid.Columns["支票號碼"].Width = 110;
            _grid.Columns["支票抬頭"].Width = 130;
            _grid.Columns["來往對象"].Width = 90;
            _grid.Columns["票面金額"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["票面金額"].Width = 110;
            _grid.Columns["票面銀行"].Width = 150;
            _grid.Columns["票據現況"].Width = 80;
            _grid.Columns["收開票日"].Width = 130;
            _grid.Columns["到期日"].Width = 130;
            _grid.Columns["預兌日"].Width = 130;
            _grid.Columns["備註"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }
        _lblCount.Text = $"共 {dt.Rows.Count} 筆";
        var total = dt.AsEnumerable().Sum(r => Convert.ToDecimal(r["票面金額"]));
        _lblTotal.Text = $"票面金額合計：{total:N0}";
    }

    private void EditBill(DataRow? row)
    {
        var values = BillEditDialog.Show(this, row);
        if (values is null)
            return;
        try
        {
            if (row is null)
            {
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [票據收付] ([收付類別],[支票號碼],[支票抬頭],[票據現況],[票據類別],[部門編號],[來往對象]," +
                    "[銀行帳戶],[託收帳戶],[票面帳號],[票面銀行],[票面金額],[本幣金額],[中文大寫],[匯率],[對方科目],[傳票摘要]," +
                    "[客票],[抬頭],[背書],[平行線],[備註],[收開票日],[到期日],[預兌日],[異動日]) " +
                    "VALUES ($kind,$no,$holder,$status,$btype,$dept,$party,$bank,$trust,$acct,$bname,$amt,$lcl,$upper,$rate,$subject,$summary," +
                    "$cust,$draw,$endorse,$par,$remark,$od,$due,$predue,$chg)",
                    Values(("$kind", values["收付類別"]), ("$no", values["支票號碼"]), ("$holder", values["支票抬頭"]),
                        ("$status", values["票據現況"]), ("$btype", values["票據類別"]), ("$dept", values["部門編號"]),
                        ("$party", values["來往對象"]), ("$bank", values["銀行帳戶"]), ("$trust", values["託收帳戶"]),
                        ("$acct", values["票面帳號"]), ("$bname", values["票面銀行"]), ("$amt", values["票面金額"]),
                        ("$lcl", values["本幣金額"]), ("$upper", values["中文大寫"]), ("$rate", values["匯率"]),
                        ("$subject", values["對方科目"]), ("$summary", values["傳票摘要"]), ("$cust", values["客票"]),
                        ("$draw", values["抬頭"]), ("$endorse", values["背書"]), ("$par", values["平行線"]),
                        ("$remark", values["備註"]), ("$od", values["收開票日"]), ("$due", values["到期日"]),
                        ("$predue", values["預兌日"]), ("$chg", values["異動日"])));
            }
            else
            {
                var rid = Convert.ToInt64(row["__rid"]);
                DbManager.ExecuteNonQuery(
                    "UPDATE [票據收付] SET [收付類別]=$kind,[支票號碼]=$no,[支票抬頭]=$holder,[票據現況]=$status,[票據類別]=$btype,[部門編號]=$dept," +
                    "[來往對象]=$party,[銀行帳戶]=$bank,[託收帳戶]=$trust,[票面帳號]=$acct,[票面銀行]=$bname,[票面金額]=$amt,[本幣金額]=$lcl,[中文大寫]=$upper," +
                    "[匯率]=$rate,[對方科目]=$subject,[傳票摘要]=$summary,[客票]=$cust,[抬頭]=$draw,[背書]=$endorse,[平行線]=$par,[備註]=$remark," +
                    "[收開票日]=$od,[到期日]=$due,[預兌日]=$predue WHERE rowid=$rid",
                    Values(("$kind", values["收付類別"]), ("$no", values["支票號碼"]), ("$holder", values["支票抬頭"]),
                        ("$status", values["票據現況"]), ("$btype", values["票據類別"]), ("$dept", values["部門編號"]),
                        ("$party", values["來往對象"]), ("$bank", values["銀行帳戶"]), ("$trust", values["託收帳戶"]),
                        ("$acct", values["票面帳號"]), ("$bname", values["票面銀行"]), ("$amt", values["票面金額"]),
                        ("$lcl", values["本幣金額"]), ("$upper", values["中文大寫"]), ("$rate", values["匯率"]),
                        ("$subject", values["對方科目"]), ("$summary", values["傳票摘要"]), ("$cust", values["客票"]),
                        ("$draw", values["抬頭"]), ("$endorse", values["背書"]), ("$par", values["平行線"]),
                        ("$remark", values["備註"]), ("$od", values["收開票日"]), ("$due", values["到期日"]),
                        ("$predue", values["預兌日"]),
                        ("$rid", rid)));
            }
            LoadList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "票據作業", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static SqliteParameter[] Values(params (string Name, object? Value)[] pairs) =>
        pairs.Select(p => DbManager.Param(p.Name, p.Value)).ToArray();

    private void DeleteSelected()
    {
        var row = GetSelectedRow();
        if (row is null) return;
        if (MessageBox.Show(this, $"確定要刪除票據 {row["支票號碼"]}？", "刪除票據",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        DbManager.ExecuteNonQuery("DELETE FROM [票據收付] WHERE rowid=$rid",
            DbManager.Param("$rid", Convert.ToInt64(row["__rid"])));
        LoadList();
    }

    private void SetStatus(string status)
    {
        var row = GetSelectedRow();
        if (row is null) return;
        var label = status switch { "已兌" => "兌現", "退票" => "退票", "作廢" => "作廢", _ => "託收" };
        if (MessageBox.Show(this, $"確定要將票據 {row["支票號碼"]} 標記為「{label}」？", "票據狀態",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        DbManager.ExecuteNonQuery(
            "UPDATE [票據收付] SET [票據現況]=$status, [異動日]=$chg WHERE rowid=$rid",
            DbManager.Param("$status", status),
            DbManager.Param("$chg", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            DbManager.Param("$rid", Convert.ToInt64(row["__rid"])));
        LoadList();
    }

    // ==================== 報表 / 匯出 ====================

    private void ShowReportMenu(Control anchor)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("應收票據明細表（收票日）", null, (s, e) => PreviewBill(BillService.收票類別, "收票日", "應收票據明細表(收票日).rtm"));
        menu.Items.Add("應收票據明細表（託收銀行）", null, (s, e) => PreviewBill(BillService.收票類別, "銀行", "應收票據明細表(託收銀行).rtm"));
        menu.Items.Add("應付票據明細表（開票日）", null, (s, e) => PreviewBill(BillService.付票類別, "開票日", "應付票據明細表(開票日).rtm"));
        menu.Items.Add("應付票據明細表（開票銀行）", null, (s, e) => PreviewBill(BillService.付票類別, "銀行", "應付票據明細表(開票銀行).rtm"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("未兌現應收票據", null, (s, e) => PreviewUncleared(BillService.收票類別, "未兌現應收票據.rtm"));
        menu.Items.Add("未兌現應付票據", null, (s, e) => PreviewUncleared(BillService.付票類別, "未兌現應付票據.rtm"));
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    private void PreviewBill(string kind, string sort, string rtmFile) =>
        PreviewData(BillService.BuildBillDetailReportData(kind, sort), rtmFile);

    private void PreviewUncleared(string kind, string rtmFile) =>
        PreviewData(BillService.BuildUnclearedBillData(kind), rtmFile);

    private static void PreviewData(RtmData data, string rtmFile)
    {
        if (data is null || data.Detail.Count == 0)
        {
            MessageBox.Show("查無可列印資料。", "報表列印", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var path = Path.Combine(ReportPrintService.RepDirectory, rtmFile);
        if (!File.Exists(path))
        {
            MessageBox.Show($"找不到報表檔：{path}", "報表列印", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ReportPrintService.Preview(ReportPrintService.Load(rtmFile), data);
    }

    private void ExportCsv()
    {
        var dt = (DataTable?)_grid.DataSource;
        if (dt is null) return;
        ExportService.ExportCsv(this, dt, $"票據清單_{DateTime.Now:yyyyMMdd}.csv", "匯出票據清單 CSV", c => c != "__rid");
    }
}
