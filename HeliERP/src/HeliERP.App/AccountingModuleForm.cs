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
/// 會計系統：傳票作業（主從）、會計科目、常用分錄（主從）。
/// </summary>
public sealed class AccountingModuleForm : Form
{
    // 傳票
    private readonly DataGridView _gridVoucher = new(), _gridVoucherDetail = new();
    private readonly Label _lblVoucher = new();
    private long _voucherKey = -1;
    private string _voucherNo = "", _voucherKind = "";

    // 會計科目
    private readonly DataGridView _gridTitle = new();
    private readonly Label _lblTitle = new();

    // 常用分錄
    private readonly DataGridView _gridJournal = new(), _gridJournalDetail = new();
    private readonly Label _lblJournal = new();
    private string _journalNo = "";

    private readonly TabControl _tabs = new();

    public AccountingModuleForm()
    {
        Text = "會計系統 - 傳票 / 會計科目 / 常用分錄";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        MinimumSize = new Size(1100, 660);
        UiTheme.Apply(this);
        Controls.Add(UiTheme.BuildHeader("會計系統", "會計傳票登錄、會計科目維護與常用分錄管理"));

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = UiTheme.Font(10.5F);
        _tabs.Controls.Add(BuildVoucherTab());
        _tabs.Controls.Add(BuildTitleTab());
        _tabs.Controls.Add(BuildJournalTab());
        Controls.Add(_tabs);

        LoadVouchers();
        LoadTitles();
        LoadJournals();

        ShortcutHelper.Enable(this,
            () =>
            {
                if (_tabs.SelectedIndex == 1) EditTitle(null);
                else if (_tabs.SelectedIndex == 2) EditJournal(null);
                else EditVoucher(null);
            },
            () =>
            {
                if (_tabs.SelectedIndex == 1) EditTitle(TitleRow());
                else if (_tabs.SelectedIndex == 2) EditJournal(JournalRow());
                else EditVoucher(VoucherRow());
            },
            () =>
            {
                if (_tabs.SelectedIndex == 1) DeleteTitle();
                else if (_tabs.SelectedIndex == 2) DeleteJournal();
                else DeleteVoucher();
            },
            () =>
            {
                if (_tabs.SelectedIndex == 1) LoadTitles();
                else if (_tabs.SelectedIndex == 2) LoadJournals();
                else LoadVouchers();
            });
    }

    // ==================== 傳票作業 ====================

    private TabPage BuildVoucherTab()
    {
        var page = new TabPage("傳票作業") { BackColor = UiTheme.Background };
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };

        var top = new Panel { Dock = DockStyle.Fill };
        var topBar = BuildBar(new (string Text, Action Action)[] {
            ("新增", () => EditVoucher(null)),
            ("修改", () => EditVoucher(VoucherRow())),
            ("刪除", () => DeleteVoucher()),
            ("重新整理", () => LoadVouchers()),
        }, _lblVoucher);
        top.Controls.Add(topBar);
        top.Controls.Add(_gridVoucher);
        topBar.Dock = DockStyle.Top;
        _gridVoucher.Dock = DockStyle.Fill;
        StyleGrid(_gridVoucher);
        _gridVoucher.SelectionChanged += (s, e) => LoadVoucherDetail();

        var bottom = new Panel { Dock = DockStyle.Fill };
        var bottomBar = BuildBar(new (string Text, Action Action)[] {
            ("新增明細", () => EditVoucherDetail(null)),
            ("修改明細", () => EditVoucherDetail(VoucherDetailRow())),
            ("刪除明細", () => DeleteVoucherDetail()),
        }, null);
        bottom.Controls.Add(bottomBar);
        bottom.Controls.Add(_gridVoucherDetail);
        bottomBar.Dock = DockStyle.Top;
        _gridVoucherDetail.Dock = DockStyle.Fill;
        StyleGrid(_gridVoucherDetail);

        split.Panel1.Controls.Add(top);
        split.Panel2.Controls.Add(bottom);
        page.Controls.Add(split);
        return page;
    }

    private DataRow? VoucherRow()
    {
        if (_gridVoucher.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private DataRow? VoucherDetailRow()
    {
        if (_gridVoucherDetail.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private void LoadVouchers()
    {
        var dt = DbManager.QueryTable(
            "SELECT [單據副碼] AS __key, [傳票編號], [傳票日期], [傳票類別], [部門編號], [覆核], [製單], [借方合計], [貸方合計] " +
            "FROM [傳票主檔] ORDER BY [傳票日期] DESC, [傳票編號]");
        _gridVoucher.DataSource = dt;
        if (_gridVoucher.Columns.Contains("__key"))
            _gridVoucher.Columns["__key"].Visible = false;
        _lblVoucher.Text = $"共 {dt.Rows.Count} 張傳票";
        if (dt.Rows.Count > 0)
        {
            _gridVoucher.Rows[0].Selected = true;
            SetVoucherKey(dt.Rows[0]);
            LoadVoucherDetail();
        }
        else
        {
            _voucherKey = -1;
            _gridVoucherDetail.DataSource = null;
        }
    }

    private void SetVoucherKey(DataRow row)
    {
        _voucherKey = Convert.ToInt64(row["__key"]);
        _voucherNo = Convert.ToString(row["傳票編號"]) ?? "";
        _voucherKind = Convert.ToString(row["傳票類別"]) ?? "";
    }

    private void LoadVoucherDetail()
    {
        var row = VoucherRow();
        if (row is null) { _voucherKey = -1; _gridVoucherDetail.DataSource = null; return; }
        SetVoucherKey(row);
        if (_voucherKey < 0) { _gridVoucherDetail.DataSource = null; return; }
        var dt = DbManager.QueryTable(
            "SELECT d.[建檔序號] AS __seq, d.[借貸], d.[科目編號], COALESCE(t.[科目名稱],'') AS 科目名稱, d.[金額], d.[借方金額], d.[貸方金額], d.[摘要], d.[部門編號], d.[專案編號] " +
            "FROM [傳票明細] d LEFT JOIN [會計科目] t ON t.[科目編號] = d.[科目編號] WHERE d.[單據副碼] = $k ORDER BY d.[建檔序號]",
            DbManager.Param("$k", _voucherKey));
        _gridVoucherDetail.DataSource = dt;
        if (_gridVoucherDetail.Columns.Contains("__seq"))
            _gridVoucherDetail.Columns["__seq"].Visible = false;
    }

    private void EditVoucher(DataRow? row)
    {
        var values = VoucherDialogs.ShowMain(this, row);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                var key = NextSubKey("傳票主檔");
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [傳票主檔] ([傳票編號],[單據副碼],[傳票日期],[傳票類別],[部門編號],[覆核],[製單],[借方合計],[貸方合計]) " +
                    "VALUES ($no,$key,$date,$kind,$dept,$review,$maker,0,0)",
                    DbManager.Param("$no", values["傳票編號"]), DbManager.Param("$key", key),
                    DbManager.Param("$date", values["傳票日期"]), DbManager.Param("$kind", values["傳票類別"]),
                    DbManager.Param("$dept", values["部門編號"]), DbManager.Param("$review", values["覆核"]),
                    DbManager.Param("$maker", values["製單"]));
            }
            else
            {
                var key = Convert.ToInt64(row["__key"]);
                DbManager.ExecuteNonQuery(
                    "UPDATE [傳票主檔] SET [傳票編號]=$no,[傳票日期]=$date,[傳票類別]=$kind,[部門編號]=$dept,[覆核]=$review,[製單]=$maker " +
                    "WHERE [單據副碼]=$key",
                    DbManager.Param("$no", values["傳票編號"]), DbManager.Param("$date", values["傳票日期"]),
                    DbManager.Param("$kind", values["傳票類別"]), DbManager.Param("$dept", values["部門編號"]),
                    DbManager.Param("$review", values["覆核"]), DbManager.Param("$maker", values["製單"]),
                    DbManager.Param("$key", key));
                DbManager.ExecuteNonQuery(
                    "UPDATE [傳票明細] SET [傳票編號]=$no,[傳票類別]=$kind WHERE [單據副碼]=$key",
                    DbManager.Param("$no", values["傳票編號"]), DbManager.Param("$kind", values["傳票類別"]),
                    DbManager.Param("$key", key));
            }
            LoadVouchers();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "傳票作業", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteVoucher()
    {
        var row = VoucherRow();
        if (row is null) return;
        if (MessageBox.Show(this, $"確定刪除傳票 {row["傳票編號"]}（含明細）？", "刪除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        DbManager.ExecuteTransaction(tx =>
        {
            DbManager.CreateCommand(tx, "DELETE FROM [傳票明細] WHERE [單據副碼]=$k",
                DbManager.Param("$k", _voucherKey)).ExecuteNonQuery();
            DbManager.CreateCommand(tx, "DELETE FROM [傳票主檔] WHERE [單據副碼]=$k",
                DbManager.Param("$k", _voucherKey)).ExecuteNonQuery();
        });
        LoadVouchers();
    }

    private void EditVoucherDetail(DataRow? row)
    {
        if (_voucherKey < 0) { MessageBox.Show(this, "請先選擇一張傳票。", "傳票作業", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var values = VoucherDialogs.ShowDetail(this, row);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                var seq = NextDetailSeq("傳票明細", _voucherKey);
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [傳票明細] ([單據副碼],[建檔序號],[傳票編號],[傳票類別],[借貸],[科目編號],[金額],[摘要],[部門編號],[專案編號],[借方金額],[貸方金額]) " +
                    "VALUES ($k,$seq,$no,$kind,$side,$title,$amt,$sum,$dept,$proj,$debit,$credit)",
                    DbManager.Param("$k", _voucherKey), DbManager.Param("$seq", seq),
                    DbManager.Param("$no", _voucherNo), DbManager.Param("$kind", _voucherKind),
                    DbManager.Param("$side", values["借貸"]), DbManager.Param("$title", values["科目編號"]),
                    DbManager.Param("$amt", values["金額"]),
                    DbManager.Param("$sum", values["摘要"]), DbManager.Param("$dept", values["部門編號"]),
                    DbManager.Param("$proj", values["專案編號"]), DbManager.Param("$debit", values["借方金額"]),
                    DbManager.Param("$credit", values["貸方金額"]));
            }
            else
            {
                var seq = Convert.ToInt64(row["__seq"]);
                DbManager.ExecuteNonQuery(
                    "UPDATE [傳票明細] SET [借貸]=$side,[科目編號]=$title,[金額]=$amt,[摘要]=$sum,[部門編號]=$dept," +
                    "[專案編號]=$proj,[借方金額]=$debit,[貸方金額]=$credit WHERE [單據副碼]=$k AND [建檔序號]=$seq",
                    DbManager.Param("$side", values["借貸"]), DbManager.Param("$title", values["科目編號"]),
                    DbManager.Param("$amt", values["金額"]),
                    DbManager.Param("$sum", values["摘要"]), DbManager.Param("$dept", values["部門編號"]),
                    DbManager.Param("$proj", values["專案編號"]), DbManager.Param("$debit", values["借方金額"]),
                    DbManager.Param("$credit", values["貸方金額"]),
                    DbManager.Param("$k", _voucherKey), DbManager.Param("$seq", seq));
            }
            RecalcVoucher();
            LoadVoucherDetail();
            LoadVouchers();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "傳票作業", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteVoucherDetail()
    {
        var row = VoucherDetailRow();
        if (row is null) return;
        var seq = Convert.ToInt64(row["__seq"]);
        DbManager.ExecuteNonQuery("DELETE FROM [傳票明細] WHERE [單據副碼]=$k AND [建檔序號]=$seq",
            DbManager.Param("$k", _voucherKey), DbManager.Param("$seq", seq));
        RecalcVoucher();
        LoadVoucherDetail();
        LoadVouchers();
    }

    private void RecalcVoucher()
    {
        var dt = DbManager.QueryTable(
            "SELECT COALESCE(SUM([借方金額]),0) AS [d], COALESCE(SUM([貸方金額]),0) AS [c] FROM [傳票明細] WHERE [單據副碼]=$k",
            DbManager.Param("$k", _voucherKey));
        if (dt.Rows.Count == 0) return;
        var debit = Convert.ToDecimal(dt.Rows[0]["d"]);
        var credit = Convert.ToDecimal(dt.Rows[0]["c"]);
        DbManager.ExecuteNonQuery(
            "UPDATE [傳票主檔] SET [借方合計]=$d,[貸方合計]=$c WHERE [單據副碼]=$k",
            DbManager.Param("$d", debit), DbManager.Param("$c", credit), DbManager.Param("$k", _voucherKey));
    }

    // ==================== 會計科目 ====================

    private TabPage BuildTitleTab()
    {
        var page = new TabPage("會計科目") { BackColor = UiTheme.Background };
        var bar = BuildBar(new (string Text, Action Action)[] {
            ("新增", () => EditTitle(null)),
            ("修改", () => EditTitle(TitleRow())),
            ("刪除", () => DeleteTitle()),
            ("重新整理", () => LoadTitles()),
        }, _lblTitle);
        bar.Dock = DockStyle.Top;
        page.Controls.Add(bar);
        page.Controls.Add(_gridTitle);
        _gridTitle.Dock = DockStyle.Fill;
        StyleGrid(_gridTitle);
        return page;
    }

    private DataRow? TitleRow()
    {
        if (_gridTitle.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private void LoadTitles()
    {
        var dt = DbManager.QueryTable(
            "SELECT [科目編號], [科目名稱], [英文名稱], [常用摘要], [類別編號], [期初借貸], [期初餘額], [沖銷科目], [統制科目], [隸屬科目], [說明] " +
            "FROM [會計科目] ORDER BY [科目編號]");
        _gridTitle.DataSource = dt;
        _lblTitle.Text = $"共 {dt.Rows.Count} 個科目";
    }

    private void EditTitle(DataRow? row)
    {
        var values = AccountTitleDialogs.ShowEdit(this, row);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [會計科目] ([科目編號],[科目名稱],[英文名稱],[常用摘要],[類別編號],[期初借貸],[期初餘額],[沖銷科目],[統制科目],[隸屬科目],[說明]) " +
                    "VALUES ($no,$name,$en,$memo,$cat,$side,$open,$off,$ctrl,$parent,$desc)",
                    DbManager.Param("$no", values["科目編號"]), DbManager.Param("$name", values["科目名稱"]),
                    DbManager.Param("$en", values["英文名稱"]), DbManager.Param("$memo", values["常用摘要"]),
                    DbManager.Param("$cat", values["類別編號"]), DbManager.Param("$side", values["期初借貸"]),
                    DbManager.Param("$open", values["期初餘額"]), DbManager.Param("$off", values["沖銷科目"]),
                    DbManager.Param("$ctrl", values["統制科目"]), DbManager.Param("$parent", values["隸屬科目"]),
                    DbManager.Param("$desc", values["說明"]));
            }
            else
            {
                DbManager.ExecuteNonQuery(
                    "UPDATE [會計科目] SET [科目名稱]=$name,[英文名稱]=$en,[常用摘要]=$memo,[類別編號]=$cat,[期初借貸]=$side," +
                    "[期初餘額]=$open,[沖銷科目]=$off,[統制科目]=$ctrl,[隸屬科目]=$parent,[說明]=$desc WHERE [科目編號]=$no",
                    DbManager.Param("$name", values["科目名稱"]), DbManager.Param("$en", values["英文名稱"]),
                    DbManager.Param("$memo", values["常用摘要"]), DbManager.Param("$cat", values["類別編號"]),
                    DbManager.Param("$side", values["期初借貸"]), DbManager.Param("$open", values["期初餘額"]),
                    DbManager.Param("$off", values["沖銷科目"]), DbManager.Param("$ctrl", values["統制科目"]),
                    DbManager.Param("$parent", values["隸屬科目"]), DbManager.Param("$desc", values["說明"]),
                    DbManager.Param("$no", row["科目編號"]));
            }
            LoadTitles();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "會計科目", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteTitle()
    {
        var row = TitleRow();
        if (row is null) return;
        if (MessageBox.Show(this, $"確定刪除科目 {row["科目編號"]}？", "刪除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        DbManager.ExecuteNonQuery("DELETE FROM [會計科目] WHERE [科目編號]=$no",
            DbManager.Param("$no", row["科目編號"]));
        LoadTitles();
    }

    // ==================== 常用分錄 ====================

    private TabPage BuildJournalTab()
    {
        var page = new TabPage("常用分錄") { BackColor = UiTheme.Background };
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 240 };

        var top = new Panel { Dock = DockStyle.Fill };
        var topBar = BuildBar(new (string Text, Action Action)[] {
            ("新增", () => EditJournal(null)),
            ("修改", () => EditJournal(JournalRow())),
            ("刪除", () => DeleteJournal()),
            ("重新整理", () => LoadJournals()),
        }, _lblJournal);
        top.Controls.Add(topBar);
        top.Controls.Add(_gridJournal);
        topBar.Dock = DockStyle.Top;
        _gridJournal.Dock = DockStyle.Fill;
        StyleGrid(_gridJournal);
        _gridJournal.SelectionChanged += (s, e) => LoadJournalDetail();

        var bottom = new Panel { Dock = DockStyle.Fill };
        var bottomBar = BuildBar(new (string Text, Action Action)[] {
            ("新增明細", () => EditJournalDetail(null)),
            ("修改明細", () => EditJournalDetail(JournalDetailRow())),
            ("刪除明細", () => DeleteJournalDetail()),
        }, null);
        bottom.Controls.Add(bottomBar);
        bottom.Controls.Add(_gridJournalDetail);
        bottomBar.Dock = DockStyle.Top;
        _gridJournalDetail.Dock = DockStyle.Fill;
        StyleGrid(_gridJournalDetail);

        split.Panel1.Controls.Add(top);
        split.Panel2.Controls.Add(bottom);
        page.Controls.Add(split);
        return page;
    }

    private DataRow? JournalRow()
    {
        if (_gridJournal.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private DataRow? JournalDetailRow()
    {
        if (_gridJournalDetail.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private void LoadJournals()
    {
        var dt = DbManager.QueryTable(
            "SELECT [分錄編號], [分錄類別], [分錄名稱] FROM [常用分錄] ORDER BY [分錄編號]");
        _gridJournal.DataSource = dt;
        _lblJournal.Text = $"共 {dt.Rows.Count} 組常用分錄";
        if (dt.Rows.Count > 0)
        {
            _gridJournal.Rows[0].Selected = true;
            _journalNo = Convert.ToString(dt.Rows[0]["分錄編號"]) ?? "";
            LoadJournalDetail();
        }
        else
        {
            _journalNo = "";
            _gridJournalDetail.DataSource = null;
        }
    }

    private void LoadJournalDetail()
    {
        var row = JournalRow();
        if (row is null) { _journalNo = ""; _gridJournalDetail.DataSource = null; return; }
        _journalNo = Convert.ToString(row["分錄編號"]) ?? "";
        if (_journalNo.Length == 0) { _gridJournalDetail.DataSource = null; return; }
        var dt = DbManager.QueryTable(
            "SELECT [分錄編號], [建檔時間], [借貸], [科目編號], [科目名稱], [摘要], [部門編號], [專案編號] " +
            "FROM [分錄明細] WHERE [分錄編號] = $no ORDER BY [建檔時間]",
            DbManager.Param("$no", _journalNo));
        _gridJournalDetail.DataSource = dt;
    }

    private void EditJournal(DataRow? row)
    {
        var values = JournalDialogs.ShowMain(this, row);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [常用分錄] ([分錄編號],[分錄類別],[分錄名稱]) VALUES ($no,$kind,$name)",
                    DbManager.Param("$no", values["分錄編號"]), DbManager.Param("$kind", values["分錄類別"]),
                    DbManager.Param("$name", values["分錄名稱"]));
            }
            else
            {
                var oldNo = Convert.ToString(row["分錄編號"]) ?? "";
                var newNo = Convert.ToString(values["分錄編號"]) ?? oldNo;
                DbManager.ExecuteNonQuery(
                    "UPDATE [常用分錄] SET [分錄編號]=$nno,[分錄類別]=$kind,[分錄名稱]=$name WHERE [分錄編號]=$ono",
                    DbManager.Param("$nno", newNo), DbManager.Param("$kind", values["分錄類別"]),
                    DbManager.Param("$name", values["分錄名稱"]), DbManager.Param("$ono", oldNo));
                if (newNo != oldNo)
                    DbManager.ExecuteNonQuery("UPDATE [分錄明細] SET [分錄編號]=$nno WHERE [分錄編號]=$ono",
                        DbManager.Param("$nno", newNo), DbManager.Param("$ono", oldNo));
            }
            LoadJournals();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "常用分錄", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteJournal()
    {
        var row = JournalRow();
        if (row is null) return;
        var no = Convert.ToString(row["分錄編號"]) ?? "";
        if (MessageBox.Show(this, $"確定刪除常用分錄 {no}（含明細）？", "刪除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        DbManager.ExecuteTransaction(tx =>
        {
            DbManager.CreateCommand(tx, "DELETE FROM [分錄明細] WHERE [分錄編號]=$no",
                DbManager.Param("$no", no)).ExecuteNonQuery();
            DbManager.CreateCommand(tx, "DELETE FROM [常用分錄] WHERE [分錄編號]=$no",
                DbManager.Param("$no", no)).ExecuteNonQuery();
        });
        LoadJournals();
    }

    private void EditJournalDetail(DataRow? row)
    {
        if (_journalNo.Length == 0) { MessageBox.Show(this, "請先選擇一組常用分錄。", "常用分錄", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var values = JournalDialogs.ShowDetail(this, row, _journalNo);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [分錄明細] ([分錄編號],[建檔時間],[借貸],[科目編號],[科目名稱],[摘要],[部門編號],[專案編號]) " +
                    "VALUES ($no,$time,$side,$title,$tname,$sum,$dept,$proj)",
                    DbManager.Param("$no", values["分錄編號"]), DbManager.Param("$time", values["建檔時間"]),
                    DbManager.Param("$side", values["借貸"]), DbManager.Param("$title", values["科目編號"]),
                    DbManager.Param("$tname", values["科目名稱"]), DbManager.Param("$sum", values["摘要"]),
                    DbManager.Param("$dept", values["部門編號"]), DbManager.Param("$proj", values["專案編號"]));
            }
            else
            {
                DbManager.ExecuteNonQuery(
                    "UPDATE [分錄明細] SET [借貸]=$side,[科目編號]=$title,[科目名稱]=$tname,[摘要]=$sum,[部門編號]=$dept," +
                    "[專案編號]=$proj WHERE [分錄編號]=$no AND [建檔時間]=$time",
                    DbManager.Param("$side", values["借貸"]), DbManager.Param("$title", values["科目編號"]),
                    DbManager.Param("$tname", values["科目名稱"]), DbManager.Param("$sum", values["摘要"]),
                    DbManager.Param("$dept", values["部門編號"]), DbManager.Param("$proj", values["專案編號"]),
                    DbManager.Param("$no", values["分錄編號"]), DbManager.Param("$time", values["建檔時間"]));
            }
            LoadJournalDetail();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "常用分錄", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteJournalDetail()
    {
        var row = JournalDetailRow();
        if (row is null) return;
        var no = Convert.ToString(row["分錄編號"]) ?? "";
        var time = Convert.ToString(row["建檔時間"]) ?? "";
        DbManager.ExecuteNonQuery("DELETE FROM [分錄明細] WHERE [分錄編號]=$no AND [建檔時間]=$time",
            DbManager.Param("$no", no), DbManager.Param("$time", time));
        LoadJournalDetail();
    }

    // ==================== 共用 ====================

    private static long NextSubKey(string table) =>
        Convert.ToInt64(DbManager.QueryScalar($"SELECT COALESCE(MAX([單據副碼]),0)+1 FROM [{table}]") ?? 1L);

    private static long NextDetailSeq(string table, long key) =>
        Convert.ToInt64(DbManager.QueryScalar(
            $"SELECT COALESCE(MAX([建檔序號]),0)+1 FROM [{table}] WHERE [單據副碼]=$k",
            DbManager.Param("$k", key)) ?? 1L);

    private Panel BuildBar((string Text, Action Action)[] buttons, Label? status)
    {
        var bar = new Panel { Height = 46, BackColor = Color.FromArgb(243, 245, 248), Padding = new Padding(10, 6, 10, 6) };
        int x = 12;
        foreach (var (text, action) in buttons)
        {
            var b = new ModernButton { Text = text, Width = 100, Height = 34, IsPrimary = x == 12, DrawShadow = false, CornerRadius = 6 };
            b.Location = new Point(x, 6);
            x += b.Width + 8;
            b.Click += (s, e) => action();
            bar.Controls.Add(b);
        }
        if (status is not null)
        {
            status.AutoSize = true;
            status.Font = UiTheme.Font(9.5F);
            status.ForeColor = UiTheme.TextSub;
            status.Location = new Point(x + 8, 14);
            bar.Controls.Add(status);
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
