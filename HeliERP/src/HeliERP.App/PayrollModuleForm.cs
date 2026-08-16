// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 薪資系統：出缺勤（主從）、薪資設定、薪資計算。
/// </summary>
public sealed class PayrollModuleForm : Form
{
    // 出缺勤
    private readonly DataGridView _gridAtt = new(), _gridAttDetail = new();
    private string _attEmp = "";
    private int _attYear, _attMonth;
    private int _attCount;

    // 薪資設定
    private readonly DataGridView _gridCfg = new();
    private int _cfgCount;

    // 薪資計算
    private readonly DataGridView _gridPay = new();
    private int _payCount;

    private readonly TabControl _tabs = new();
    private Label _lblStatus = null!;

    public PayrollModuleForm()
    {
        Text = "薪資系統 - 出缺勤 / 薪資設定 / 薪資計算";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 660);
        UiTheme.Apply(this);
        Controls.Add(UiTheme.BuildHeader("薪資系統", "出缺勤記錄、計薪項目設定與月薪計算"));

        BuildGlobalToolbar();
        BuildStatusBar();

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = UiTheme.Font(10.5F);
        _tabs.Controls.Add(BuildAttendanceTab());
        _tabs.Controls.Add(BuildConfigTab());
        _tabs.Controls.Add(BuildPayrollTab());
        _tabs.SelectedIndexChanged += (s, e) => UpdateStatusBar();
        Controls.Add(_tabs);

        Load += (s, e) =>
        {
            LoadAttendance();
            LoadConfig();
            LoadPayroll();
            UpdateStatusBar();
        };

        ShortcutHelper.Enable(this, AddCurrent, EditCurrent, DeleteCurrent, ReloadCurrent, ReloadCurrent);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    // ==================== 全域工具列 ====================

    private void BuildGlobalToolbar()
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
        btnSearch.Click += (s, e) => ReloadCurrent();
        var btnReload = new ModernButton { Text = "重讀", Width = 120, IsPrimary = false };
        btnReload.Click += (s, e) => ReloadCurrent();
        var btnNew = new ModernButton { Text = "新增", Width = 120 };
        btnNew.Click += (s, e) => AddCurrent();
        var btnEdit = new ModernButton { Text = "修改", Width = 120, IsPrimary = false };
        btnEdit.Click += (s, e) => EditCurrent();
        var btnDel = new ModernButton { Text = "刪除", Width = 120, IsPrimary = false };
        btnDel.Click += (s, e) => DeleteCurrent();

        Add(btnSearch); Add(btnReload); Add(btnNew); Add(btnEdit); Add(btnDel);
        Sep();

        var btnHelp = new ModernButton { Text = "說明", Width = 120, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show("薪資系統 v1.0\n出缺勤：依員工／年度／月份記錄每日出缺班別與時間。\n薪資設定：計薪項目（加減項、單位金額、公式）維護。\n薪資計算：依年度／月份執行計算並檢視結果。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 120, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnHelp); Add(btnExit);

        Controls.Add(bar);
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

    private void UpdateStatusBar()
    {
        _lblStatus.Text = _tabs.SelectedIndex switch
        {
            1 => $"薪資設定：共 {_cfgCount} 項計薪設定",
            2 => $"薪資計算：共 {_payCount} 筆薪資",
            _ => $"出缺勤：共 {_attCount} 筆出缺勤",
        };
    }

    private void AddCurrent()
    {
        if (_tabs.SelectedIndex == 1) EditConfig(null);
        else EditAttendance(null);
    }

    private void EditCurrent()
    {
        if (_tabs.SelectedIndex == 1) EditConfig(CfgRow());
        else EditAttendance(AttRow());
    }

    private void DeleteCurrent()
    {
        if (_tabs.SelectedIndex == 1) DeleteConfig();
        else DeleteAttendance();
    }

    private void ReloadCurrent()
    {
        if (_tabs.SelectedIndex == 1) LoadConfig();
        else if (_tabs.SelectedIndex == 2) LoadPayroll();
        else LoadAttendance();
    }

    // ==================== 出缺勤 ====================

    private TabPage BuildAttendanceTab()
    {
        var page = new TabPage("出缺勤") { BackColor = UiTheme.Background };
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 280 };

        var top = new Panel { Dock = DockStyle.Fill };
        top.Controls.Add(_gridAtt);
        _gridAtt.Dock = DockStyle.Fill;
        StyleGrid(_gridAtt);
        _gridAtt.SelectionChanged += (s, e) => LoadAttendanceDetail();

        var bottom = new Panel { Dock = DockStyle.Fill };
        var bottomBar = BuildBar(new (string Text, Action Action)[] {
            ("新增明細", () => EditAttendanceDetail(null)),
            ("修改明細", () => EditAttendanceDetail(AttDetailRow())),
            ("刪除明細", () => DeleteAttendanceDetail()),
        });
        bottom.Controls.Add(bottomBar);
        bottom.Controls.Add(_gridAttDetail);
        bottomBar.Dock = DockStyle.Top;
        _gridAttDetail.Dock = DockStyle.Fill;
        StyleGrid(_gridAttDetail);

        split.Panel1.Controls.Add(top);
        split.Panel2.Controls.Add(bottom);
        page.Controls.Add(split);
        return page;
    }

    private DataRow? AttRow()
    {
        if (_gridAtt.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private DataRow? AttDetailRow()
    {
        if (_gridAttDetail.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private string AttPrefix() => $"{_attEmp}|{_attYear:0000}-{_attMonth:00}";

    private void LoadAttendance()
    {
        var dt = DbManager.QueryTable(
            "SELECT a.[員工編號], COALESCE(e.[員工姓名],'') AS 員工姓名, a.[出勤年度], a.[出勤月份], a.[所屬部門], a.[出勤部門] " +
            "FROM [出缺主檔] a LEFT JOIN [員工資料] e ON e.[員工編號]=a.[員工編號] " +
            "ORDER BY a.[出勤年度] DESC, a.[出勤月份] DESC, a.[員工編號]");
        _gridAtt.DataSource = dt;
        _attCount = dt.Rows.Count;
        UpdateStatusBar();
        if (dt.Rows.Count > 0)
        {
            _gridAtt.Rows[0].Selected = true;
            SetAttKey(dt.Rows[0]);
            LoadAttendanceDetail();
        }
        else
        {
            _attEmp = "";
            _gridAttDetail.DataSource = null;
        }
    }

    private void SetAttKey(DataRow row)
    {
        _attEmp = Convert.ToString(row["員工編號"]) ?? "";
        _attYear = Convert.ToInt32(row["出勤年度"]);
        _attMonth = Convert.ToInt32(row["出勤月份"]);
    }

    private void LoadAttendanceDetail()
    {
        var row = AttRow();
        if (row is null) { _attEmp = ""; _gridAttDetail.DataSource = null; return; }
        SetAttKey(row);
        if (_attEmp.Length == 0) { _gridAttDetail.DataSource = null; return; }
        var dt = DbManager.QueryTable(
            "SELECT [出缺編號] AS __id, [出缺類別], [日], [星期], [常日上班], [常日下班], [常日班別], " +
            "[晚班上班], [晚班下班], [晚班別], [小夜上班], [小夜下班], [小夜班別], [大夜上班], [大夜下班], [大夜班別] " +
            "FROM [出缺明細] WHERE [出缺編號] LIKE $p ORDER BY [出缺編號]",
            DbManager.Param("$p", AttPrefix() + "|%"));
        _gridAttDetail.DataSource = dt;
        if (_gridAttDetail.Columns.Contains("__id"))
            _gridAttDetail.Columns["__id"].Visible = false;
    }

    private void EditAttendance(DataRow? row)
    {
        var values = AttendanceDialogs.ShowMain(this, row);
        if (values is null) return;
        try
        {
            DbManager.ExecuteNonQuery(
                "INSERT OR REPLACE INTO [出缺主檔] ([員工編號],[出勤年度],[出勤月份],[所屬部門],[出勤部門]) " +
                "VALUES ($e,$y,$m,$od,$wd)",
                DbManager.Param("$e", values["員工編號"]), DbManager.Param("$y", values["出勤年度"]),
                DbManager.Param("$m", values["出勤月份"]), DbManager.Param("$od", values["所屬部門"]),
                DbManager.Param("$wd", values["出勤部門"]));
            LoadAttendance();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "出缺勤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteAttendance()
    {
        var row = AttRow();
        if (row is null) return;
        if (MessageBox.Show(this, $"確定刪除 {row["員工編號"]} {_attYear} 年 {_attMonth} 月出缺勤（含明細）？", "刪除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        DbManager.ExecuteTransaction(tx =>
        {
            DbManager.CreateCommand(tx, "DELETE FROM [出缺明細] WHERE [出缺編號] LIKE $p",
                DbManager.Param("$p", AttPrefix() + "|%")).ExecuteNonQuery();
            DbManager.CreateCommand(tx, "DELETE FROM [出缺主檔] WHERE [員工編號]=$e AND [出勤年度]=$y AND [出勤月份]=$m",
                DbManager.Param("$e", _attEmp), DbManager.Param("$y", _attYear), DbManager.Param("$m", _attMonth))
                .ExecuteNonQuery();
        });
        LoadAttendance();
    }

    private void EditAttendanceDetail(DataRow? row)
    {
        if (_attEmp.Length == 0) { MessageBox.Show(this, "請先選擇一筆出缺勤。", "出缺勤", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var values = AttendanceDialogs.ShowDetail(this, row);
        if (values is null) return;
        try
        {
            var id = $"{AttPrefix()}|{values["日"]}";
            DbManager.ExecuteNonQuery(
                "INSERT OR REPLACE INTO [出缺明細] ([出缺編號],[出缺類別],[日],[星期],[常日上班],[常日下班],[常日班別]," +
                "[晚班上班],[晚班下班],[晚班別],[小夜上班],[小夜下班],[小夜班別],[大夜上班],[大夜下班],[大夜班別]) " +
                "VALUES ($id,$kind,$day,$week,$odi,$odo,$odshift,$evi,$evo,$evshift,$nii,$nio,$nishift,$dni,$dno,$dnshift)",
                DbManager.Param("$id", id), DbManager.Param("$kind", values["出缺類別"]), DbManager.Param("$day", values["日"]),
                DbManager.Param("$week", values["星期"]), DbManager.Param("$odi", values["常日上班"]), DbManager.Param("$odo", values["常日下班"]),
                DbManager.Param("$odshift", values["常日班別"]), DbManager.Param("$evi", values["晚班上班"]), DbManager.Param("$evo", values["晚班下班"]),
                DbManager.Param("$evshift", values["晚班別"]), DbManager.Param("$nii", values["小夜上班"]), DbManager.Param("$nio", values["小夜下班"]),
                DbManager.Param("$nishift", values["小夜班別"]), DbManager.Param("$dni", values["大夜上班"]), DbManager.Param("$dno", values["大夜下班"]),
                DbManager.Param("$dnshift", values["大夜班別"]));
            LoadAttendanceDetail();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "出缺勤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteAttendanceDetail()
    {
        var row = AttDetailRow();
        if (row is null) return;
        var id = Convert.ToString(row["__id"]);
        DbManager.ExecuteNonQuery("DELETE FROM [出缺明細] WHERE [出缺編號]=$id", DbManager.Param("$id", id));
        LoadAttendanceDetail();
    }

    // ==================== 薪資設定 ====================

    private TabPage BuildConfigTab()
    {
        var page = new TabPage("薪資設定") { BackColor = UiTheme.Background };
        page.Controls.Add(_gridCfg);
        _gridCfg.Dock = DockStyle.Fill;
        StyleGrid(_gridCfg);
        return page;
    }

    private DataRow? CfgRow()
    {
        if (_gridCfg.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private void LoadConfig()
    {
        var dt = DbManager.QueryTable(
            "SELECT c.[員工編號], COALESCE(e.[員工姓名],'') AS 員工姓名, c.[計薪編號], c.[計薪名稱], c.[單位], c.[加減], " +
            "c.[計稅別], c.[單位金額], c.[金額公式編號], c.[數量公式編號], c.[轉帳科目] " +
            "FROM [薪資設定] c LEFT JOIN [員工資料] e ON e.[員工編號]=c.[員工編號] " +
            "ORDER BY c.[員工編號], c.[計薪編號]");
        _gridCfg.DataSource = dt;
        _cfgCount = dt.Rows.Count;
        UpdateStatusBar();
    }

    private void EditConfig(DataRow? row)
    {
        var values = PayrollConfigDialogs.ShowEdit(this, row);
        if (values is null) return;
        try
        {
            DbManager.ExecuteNonQuery(
                "INSERT OR REPLACE INTO [薪資設定] ([員工編號],[計薪編號],[計薪名稱],[單位],[加減],[計稅別],[單位金額],[金額公式編號],[數量公式編號],[轉帳科目]) " +
                "VALUES ($e,$no,$name,$unit,$addsub,$tax,$amt,$af,$qf,$acct)",
                DbManager.Param("$e", values["員工編號"]), DbManager.Param("$no", values["計薪編號"]),
                DbManager.Param("$name", values["計薪名稱"]), DbManager.Param("$unit", values["單位"]),
                DbManager.Param("$addsub", values["加減"]), DbManager.Param("$tax", values["計稅別"]),
                DbManager.Param("$amt", values["單位金額"]), DbManager.Param("$af", values["金額公式編號"]),
                DbManager.Param("$qf", values["數量公式編號"]), DbManager.Param("$acct", values["轉帳科目"]));
            LoadConfig();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "薪資設定", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteConfig()
    {
        var row = CfgRow();
        if (row is null) return;
        DbManager.ExecuteNonQuery(
            "DELETE FROM [薪資設定] WHERE [員工編號]=$e AND [計薪編號]=$no",
            DbManager.Param("$e", row["員工編號"]), DbManager.Param("$no", row["計薪編號"]));
        LoadConfig();
    }

    // ==================== 薪資計算 ====================

    private TabPage BuildPayrollTab()
    {
        var page = new TabPage("薪資計算") { BackColor = UiTheme.Background };

        var topBar = new Panel { Height = 52, Dock = DockStyle.Top, BackColor = UiTheme.Card, Padding = new Padding(12, 8, 12, 8) };
        var numYear = new NumericUpDown { Minimum = 2000, Maximum = 2100, Value = DateTime.Today.Year, Width = 80, Location = new Point(90, 14) };
        var numMonth = new NumericUpDown { Minimum = 1, Maximum = 12, Value = DateTime.Today.Month, Width = 60, Location = new Point(210, 14) };
        var lblMsg = new Label { Text = "薪資計算結果", ForeColor = UiTheme.TextSub, AutoSize = true, Location = new Point(300, 18) };
        topBar.Controls.Add(new Label { Text = "年度", ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(24, 18) });
        topBar.Controls.Add(numYear);
        topBar.Controls.Add(new Label { Text = "月份", ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(152, 18) });
        topBar.Controls.Add(numMonth);
        var btnCalc = new ModernButton { Text = "執行薪資計算", IsPrimary = true, DrawShadow = false, CornerRadius = 6, Size = new Size(140, 34), Location = new Point(286, 9) };
        topBar.Controls.Add(btnCalc);
        lblMsg.Location = new Point(545, 18);

        page.Controls.Add(topBar);
        page.Controls.Add(_gridPay);
        _gridPay.Dock = DockStyle.Fill;
        StyleGrid(_gridPay);

        btnCalc.Click += (s, e) =>
        {
            try
            {
                var summary = PayrollService.Calculate((int)numYear.Value, (int)numMonth.Value);
                lblMsg.Text = "計算完成";
                MessageBox.Show(this, summary, "薪資計算", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadPayroll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "計算失敗：" + ex.Message, "薪資計算", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        return page;
    }

    private void LoadPayroll()
    {
        var dt = DbManager.QueryTable(
            "SELECT p.[員工編號], COALESCE(e.[員工姓名],'') AS 員工姓名, p.[薪資年度], p.[薪資月份], p.[所屬部門], p.[出勤部門], " +
            "p.[應領金額], p.[扣領金額], p.[實領金額], p.[給付金額], p.[稅項加總] " +
            "FROM [薪資主檔] p LEFT JOIN [員工資料] e ON e.[員工編號]=p.[員工編號] " +
            "ORDER BY p.[薪資年度] DESC, p.[薪資月份] DESC, p.[員工編號]");
        _gridPay.DataSource = dt;
        _payCount = dt.Rows.Count;
        UpdateStatusBar();
    }

    // ==================== 共用 ====================

    private Panel BuildBar((string Text, Action Action)[] buttons)
    {
        var bar = new Panel { Height = 46, BackColor = UiTheme.Background, Padding = new Padding(10, 6, 10, 6) };
        int x = 12;
        foreach (var (text, action) in buttons)
        {
            var b = new ModernButton { Text = text, Width = 100, Height = 34, IsPrimary = x == 12, DrawShadow = false, CornerRadius = 6 };
            b.Location = new Point(x, 6);
            x += b.Width + 8;
            b.Click += (s, e) => action();
            bar.Controls.Add(b);
        }
        return bar;
    }

    private static void StyleGrid(DataGridView grid)
    {
        UiTheme.StyleDataGridView(grid);
        grid.ReadOnly = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AllowUserToAddRows = false;
        grid.RowHeadersVisible = false;
    }
}
