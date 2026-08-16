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
/// 生管系統：驗貨作業與託運作業（主從單據管理）。
/// 驗貨：驗貨主檔 + 驗貨明細；託運：託運主檔 + 託運明細。
/// </summary>
public sealed class ProductionModuleForm : Form
{
    // 驗貨
    private readonly DataGridView _gridInsp = new(), _gridInspDetail = new();
    private readonly Label _lblInsp = new();
    private long _inspKey = -1;

    // 託運
    private readonly DataGridView _gridShip = new(), _gridShipDetail = new();
    private readonly Label _lblShip = new();
    private long _shipKey = -1;

    private readonly TabControl _tabs = new();

    public ProductionModuleForm()
    {
        Text = "生管系統 - 驗貨 / 託運作業";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        MinimumSize = new Size(1100, 660);
        UiTheme.Apply(this);
        Controls.Add(UiTheme.BuildHeader("生管系統", "驗貨（品檢）與託運（出貨運輸）單據作業"));

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = UiTheme.Font(10.5F);
        _tabs.Controls.Add(BuildInspectionTab());
        _tabs.Controls.Add(BuildShippingTab());
        Controls.Add(_tabs);

        LoadInspection();
        LoadShipping();

        ShortcutHelper.Enable(this,
            () =>
            {
                if (_tabs.SelectedIndex == 1) EditShipping(null);
                else EditInspection(null);
            },
            () =>
            {
                if (_tabs.SelectedIndex == 1) EditShipping(ShipRow());
                else EditInspection(InspRow());
            },
            () =>
            {
                if (_tabs.SelectedIndex == 1) DeleteShipping();
                else DeleteInspection();
            },
            () =>
            {
                if (_tabs.SelectedIndex == 1) LoadShipping();
                else LoadInspection();
            });
    }

    // ==================== 驗貨 ====================

    private TabPage BuildInspectionTab()
    {
        var page = new TabPage("驗貨作業") { BackColor = UiTheme.Background };
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };

        var top = new Panel { Dock = DockStyle.Fill };
        var topBar = BuildBar(new (string Text, Action Action)[] {
            ("新增", () => EditInspection(null)),
            ("修改", () => EditInspection(InspRow())),
            ("刪除", () => DeleteInspection()),
            ("重新整理", () => LoadInspection()),
        }, _lblInsp);
        top.Controls.Add(topBar);
        top.Controls.Add(_gridInsp);
        topBar.Dock = DockStyle.Top;
        _gridInsp.Dock = DockStyle.Fill;
        UiTheme.StyleDataGridView(_gridInsp);
        _gridInsp.ReadOnly = true;
        _gridInsp.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridInsp.MultiSelect = false;
        _gridInsp.AllowUserToAddRows = false;
        _gridInsp.RowHeadersVisible = false;
        _gridInsp.SelectionChanged += (s, e) => LoadInspectionDetail();

        var bottom = new Panel { Dock = DockStyle.Fill };
        var bottomBar = BuildBar(new (string Text, Action Action)[] {
            ("新增明細", () => EditInspectionDetail(null)),
            ("修改明細", () => EditInspectionDetail(InspDetailRow())),
            ("刪除明細", () => DeleteInspectionDetail()),
        }, null);
        bottom.Controls.Add(bottomBar);
        bottom.Controls.Add(_gridInspDetail);
        bottomBar.Dock = DockStyle.Top;
        _gridInspDetail.Dock = DockStyle.Fill;
        UiTheme.StyleDataGridView(_gridInspDetail);
        _gridInspDetail.ReadOnly = true;
        _gridInspDetail.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridInspDetail.MultiSelect = false;
        _gridInspDetail.AllowUserToAddRows = false;
        _gridInspDetail.RowHeadersVisible = false;

        split.Panel1.Controls.Add(top);
        split.Panel2.Controls.Add(bottom);
        page.Controls.Add(split);
        return page;
    }

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

    private DataRow? InspRow()
    {
        if (_gridInsp.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private DataRow? InspDetailRow()
    {
        if (_gridInspDetail.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private void LoadInspection()
    {
        var dt = DbManager.QueryTable(
            "SELECT [單據副碼] AS __key, [驗貨單別], [驗貨單號], [驗貨日期], [送驗單號], [採購單號], [部門編號], " +
            "[業務員編號], [廠商編號], [廠商名稱], [檢驗狀況], [製單人員], [明細筆數], [備註] " +
            "FROM [驗貨主檔] ORDER BY [驗貨日期] DESC, [驗貨單號]");
        _gridInsp.DataSource = dt;
        _gridInsp.Columns["__key"].Visible = false;
        _lblInsp.Text = $"共 {dt.Rows.Count} 單";
        if (dt.Rows.Count > 0)
        {
            _gridInsp.Rows[0].Selected = true;
            _inspKey = Convert.ToInt64(dt.Rows[0]["__key"]);
            LoadInspectionDetail();
        }
        else
        {
            _inspKey = -1;
            _gridInspDetail.DataSource = null;
        }
    }

    private void LoadInspectionDetail()
    {
        var row = InspRow();
        _inspKey = row is null ? -1 : Convert.ToInt64(row["__key"]);
        if (_inspKey < 0)
        {
            _gridInspDetail.DataSource = null;
            return;
        }
        var dt = DbManager.QueryTable(
            "SELECT [建檔序號] AS __seq, [貨品編號], [品名], [單位], [送驗數量], [抽驗或已驗數量], [不良品數量], [合格註記], [備註] " +
            "FROM [驗貨明細] WHERE [單據副碼] = $k ORDER BY [建檔序號]",
            DbManager.Param("$k", _inspKey));
        _gridInspDetail.DataSource = dt;
        _gridInspDetail.Columns["__seq"].Visible = false;
    }

    private void EditInspection(DataRow? row)
    {
        var values = InspectionDialogs.ShowMain(this, row);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                var key = NextSubKey("驗貨主檔");
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [驗貨主檔] ([驗貨單別],[驗貨單號],[單據副碼],[驗貨日期],[送驗單號],[採購單號],[部門編號],[業務員編號]," +
                    "[廠商編號],[廠商名稱],[製單人員],[檢驗狀況],[備註],[明細筆數]) " +
                    "VALUES ($type,$no,$key,$date,$sent,$po,$dept,$staff,$vendor,$vname,$maker,$status,$remark,0)",
                    DbManager.Param("$type", values["驗貨單別"]), DbManager.Param("$no", values["驗貨單號"]), DbManager.Param("$key", key),
                    DbManager.Param("$date", values["驗貨日期"]), DbManager.Param("$sent", values["送驗單號"]), DbManager.Param("$po", values["採購單號"]),
                    DbManager.Param("$dept", values["部門編號"]), DbManager.Param("$staff", values["業務員編號"]),
                    DbManager.Param("$vendor", values["廠商編號"]), DbManager.Param("$vname", values["廠商名稱"]),
                    DbManager.Param("$maker", values["製單人員"]), DbManager.Param("$status", values["檢驗狀況"]), DbManager.Param("$remark", values["備註"]));
            }
            else
            {
                var key = Convert.ToInt64(row["__key"]);
                DbManager.ExecuteNonQuery(
                    "UPDATE [驗貨主檔] SET [驗貨單別]=$type,[驗貨單號]=$no,[驗貨日期]=$date,[送驗單號]=$sent,[採購單號]=$po,[部門編號]=$dept," +
                    "[業務員編號]=$staff,[廠商編號]=$vendor,[廠商名稱]=$vname,[製單人員]=$maker,[檢驗狀況]=$status,[備註]=$remark " +
                    "WHERE [單據副碼]=$key",
                    DbManager.Param("$type", values["驗貨單別"]), DbManager.Param("$no", values["驗貨單號"]), DbManager.Param("$date", values["驗貨日期"]),
                    DbManager.Param("$sent", values["送驗單號"]), DbManager.Param("$po", values["採購單號"]), DbManager.Param("$dept", values["部門編號"]),
                    DbManager.Param("$staff", values["業務員編號"]), DbManager.Param("$vendor", values["廠商編號"]), DbManager.Param("$vname", values["廠商名稱"]),
                    DbManager.Param("$maker", values["製單人員"]), DbManager.Param("$status", values["檢驗狀況"]), DbManager.Param("$remark", values["備註"]),
                    DbManager.Param("$key", key));
            }
            LoadInspection();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "驗貨作業", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteInspection()
    {
        var row = InspRow();
        if (row is null) return;
        var key = Convert.ToInt64(row["__key"]);
        if (MessageBox.Show(this, $"確定刪除驗貨單 {row["驗貨單號"]}（含明細）？", "刪除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        DbManager.ExecuteTransaction(tx =>
        {
            DbManager.CreateCommand(tx, "DELETE FROM [驗貨明細] WHERE [單據副碼]=$k", DbManager.Param("$k", key)).ExecuteNonQuery();
            DbManager.CreateCommand(tx, "DELETE FROM [驗貨主檔] WHERE [單據副碼]=$k", DbManager.Param("$k", key)).ExecuteNonQuery();
        });
        LoadInspection();
    }

    private void EditInspectionDetail(DataRow? row)
    {
        if (_inspKey < 0) { MessageBox.Show(this, "請先選擇一張驗貨單。", "驗貨作業", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var values = InspectionDialogs.ShowDetail(this, row);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                var seq = NextDetailSeq("驗貨明細", _inspKey);
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [驗貨明細] ([單據副碼],[建檔序號],[廠商編號],[貨品編號],[品名],[單位],[送驗數量],[抽驗或已驗數量],[不良品數量],[合格註記],[備註]) " +
                    "VALUES ($k,$seq,$vendor,$goods,$name,$unit,$qty,$checked,$bad,$pass,$remark)",
                    DbManager.Param("$k", _inspKey), DbManager.Param("$seq", seq),
                    DbManager.Param("$vendor", values["廠商編號"]), DbManager.Param("$goods", values["貨品編號"]),
                    DbManager.Param("$name", values["品名"]), DbManager.Param("$unit", values["單位"]),
                    DbManager.Param("$qty", values["送驗數量"]), DbManager.Param("$checked", values["抽驗或已驗數量"]),
                    DbManager.Param("$bad", values["不良品數量"]), DbManager.Param("$pass", values["合格註記"]),
                    DbManager.Param("$remark", values["備註"]));
            }
            else
            {
                var seq = Convert.ToInt64(row["__seq"]);
                DbManager.ExecuteNonQuery(
                    "UPDATE [驗貨明細] SET [廠商編號]=$vendor,[貨品編號]=$goods,[品名]=$name,[單位]=$unit,[送驗數量]=$qty," +
                    "[抽驗或已驗數量]=$checked,[不良品數量]=$bad,[合格註記]=$pass,[備註]=$remark WHERE [單據副碼]=$k AND [建檔序號]=$seq",
                    DbManager.Param("$vendor", values["廠商編號"]), DbManager.Param("$goods", values["貨品編號"]),
                    DbManager.Param("$name", values["品名"]), DbManager.Param("$unit", values["單位"]),
                    DbManager.Param("$qty", values["送驗數量"]), DbManager.Param("$checked", values["抽驗或已驗數量"]),
                    DbManager.Param("$bad", values["不良品數量"]), DbManager.Param("$pass", values["合格註記"]),
                    DbManager.Param("$remark", values["備註"]),
                    DbManager.Param("$k", _inspKey), DbManager.Param("$seq", seq));
            }
            UpdateDetailCount("驗貨主檔", "驗貨明細", "明細筆數", _inspKey);
            LoadInspectionDetail();
            LoadInspection();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "驗貨作業", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteInspectionDetail()
    {
        var row = InspDetailRow();
        if (row is null) return;
        var seq = Convert.ToInt64(row["__seq"]);
        DbManager.ExecuteNonQuery("DELETE FROM [驗貨明細] WHERE [單據副碼]=$k AND [建檔序號]=$seq",
            DbManager.Param("$k", _inspKey), DbManager.Param("$seq", seq));
        UpdateDetailCount("驗貨主檔", "驗貨明細", "明細筆數", _inspKey);
        LoadInspectionDetail();
        LoadInspection();
    }

    // ==================== 託運 ====================

    private TabPage BuildShippingTab()
    {
        var page = new TabPage("託運作業") { BackColor = UiTheme.Background };
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };

        var top = new Panel { Dock = DockStyle.Fill };
        var topBar = BuildBar(new (string Text, Action Action)[] {
            ("新增", () => EditShipping(null)),
            ("修改", () => EditShipping(ShipRow())),
            ("刪除", () => DeleteShipping()),
            ("重新整理", () => LoadShipping()),
        }, _lblShip);
        top.Controls.Add(topBar);
        top.Controls.Add(_gridShip);
        topBar.Dock = DockStyle.Top;
        _gridShip.Dock = DockStyle.Fill;
        UiTheme.StyleDataGridView(_gridShip);
        _gridShip.ReadOnly = true;
        _gridShip.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridShip.MultiSelect = false;
        _gridShip.AllowUserToAddRows = false;
        _gridShip.RowHeadersVisible = false;
        _gridShip.SelectionChanged += (s, e) => LoadShippingDetail();

        var bottom = new Panel { Dock = DockStyle.Fill };
        var bottomBar = BuildBar(new (string Text, Action Action)[] {
            ("新增明細", () => EditShippingDetail(null)),
            ("修改明細", () => EditShippingDetail(ShipDetailRow())),
            ("刪除明細", () => DeleteShippingDetail()),
        }, null);
        bottom.Controls.Add(bottomBar);
        bottom.Controls.Add(_gridShipDetail);
        bottomBar.Dock = DockStyle.Top;
        _gridShipDetail.Dock = DockStyle.Fill;
        UiTheme.StyleDataGridView(_gridShipDetail);
        _gridShipDetail.ReadOnly = true;
        _gridShipDetail.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridShipDetail.MultiSelect = false;
        _gridShipDetail.AllowUserToAddRows = false;
        _gridShipDetail.RowHeadersVisible = false;

        split.Panel1.Controls.Add(top);
        split.Panel2.Controls.Add(bottom);
        page.Controls.Add(split);
        return page;
    }

    private DataRow? ShipRow()
    {
        if (_gridShip.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private DataRow? ShipDetailRow()
    {
        if (_gridShipDetail.CurrentRow?.DataBoundItem is DataRowView drv) return drv.Row;
        return null;
    }

    private void LoadShipping()
    {
        var dt = DbManager.QueryTable(
            "SELECT [單據副碼] AS __key, [託運單號], [託運日期], [委託客戶], [聯絡電話], [收貨廠商], [司機編號], " +
            "[數量合計], [合計金額], [營業稅], [總計金額], [製單], [備註] " +
            "FROM [託運主檔] ORDER BY [託運日期] DESC, [託運單號]");
        _gridShip.DataSource = dt;
        _gridShip.Columns["__key"].Visible = false;
        _lblShip.Text = $"共 {dt.Rows.Count} 單";
        if (dt.Rows.Count > 0)
        {
            _gridShip.Rows[0].Selected = true;
            _shipKey = Convert.ToInt64(dt.Rows[0]["__key"]);
            LoadShippingDetail();
        }
        else
        {
            _shipKey = -1;
            _gridShipDetail.DataSource = null;
        }
    }

    private void LoadShippingDetail()
    {
        var row = ShipRow();
        _shipKey = row is null ? -1 : Convert.ToInt64(row["__key"]);
        if (_shipKey < 0)
        {
            _gridShipDetail.DataSource = null;
            return;
        }
        var dt = DbManager.QueryTable(
            "SELECT [建檔序號] AS __seq, [貨品編號], [品名], [規格], [數量], [單位], [單價], [金額], [起點], [訖點], [噸數], [板數], [備註說明] " +
            "FROM [託運明細] WHERE [單據副碼] = $k ORDER BY [建檔序號]",
            DbManager.Param("$k", _shipKey));
        _gridShipDetail.DataSource = dt;
        _gridShipDetail.Columns["__seq"].Visible = false;
    }

    private void EditShipping(DataRow? row)
    {
        var values = ShippingDialogs.ShowMain(this, row);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                var key = NextSubKey("託運主檔");
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [託運主檔] ([託運單號],[單據副碼],[託運日期],[委託客戶],[聯絡電話],[收貨廠商],[司機編號],[製單],[備註]) " +
                    "VALUES ($no,$key,$date,$client,$phone,$vendor,$driver,$maker,$remark)",
                    DbManager.Param("$no", values["託運單號"]), DbManager.Param("$key", key),
                    DbManager.Param("$date", values["託運日期"]), DbManager.Param("$client", values["委託客戶"]),
                    DbManager.Param("$phone", values["聯絡電話"]), DbManager.Param("$vendor", values["收貨廠商"]),
                    DbManager.Param("$driver", values["司機編號"]), DbManager.Param("$maker", values["製單"]),
                    DbManager.Param("$remark", values["備註"]));
            }
            else
            {
                var key = Convert.ToInt64(row["__key"]);
                DbManager.ExecuteNonQuery(
                    "UPDATE [託運主檔] SET [託運單號]=$no,[託運日期]=$date,[委託客戶]=$client,[聯絡電話]=$phone,[收貨廠商]=$vendor," +
                    "[司機編號]=$driver,[製單]=$maker,[備註]=$remark WHERE [單據副碼]=$key",
                    DbManager.Param("$no", values["託運單號"]), DbManager.Param("$date", values["託運日期"]),
                    DbManager.Param("$client", values["委託客戶"]), DbManager.Param("$phone", values["聯絡電話"]),
                    DbManager.Param("$vendor", values["收貨廠商"]), DbManager.Param("$driver", values["司機編號"]),
                    DbManager.Param("$maker", values["製單"]), DbManager.Param("$remark", values["備註"]),
                    DbManager.Param("$key", key));
            }
            LoadShipping();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "託運作業", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteShipping()
    {
        var row = ShipRow();
        if (row is null) return;
        var key = Convert.ToInt64(row["__key"]);
        if (MessageBox.Show(this, $"確定刪除託運單 {row["託運單號"]}（含明細）？", "刪除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        DbManager.ExecuteTransaction(tx =>
        {
            DbManager.CreateCommand(tx, "DELETE FROM [託運明細] WHERE [單據副碼]=$k", DbManager.Param("$k", key)).ExecuteNonQuery();
            DbManager.CreateCommand(tx, "DELETE FROM [託運主檔] WHERE [單據副碼]=$k", DbManager.Param("$k", key)).ExecuteNonQuery();
        });
        LoadShipping();
    }

    private void EditShippingDetail(DataRow? row)
    {
        if (_shipKey < 0) { MessageBox.Show(this, "請先選擇一張託運單。", "託運作業", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var values = ShippingDialogs.ShowDetail(this, row);
        if (values is null) return;
        try
        {
            if (row is null)
            {
                var seq = NextDetailSeq("託運明細", _shipKey);
                DbManager.ExecuteNonQuery(
                    "INSERT INTO [託運明細] ([單據副碼],[建檔序號],[貨品編號],[品名],[規格],[數量],[單位],[單價],[金額],[起點],[訖點],[噸數],[板數],[備註說明]) " +
                    "VALUES ($k,$seq,$goods,$name,$spec,$qty,$unit,$price,$amt,$from,$to,$ton,$plate,$remark)",
                    DbManager.Param("$k", _shipKey), DbManager.Param("$seq", seq),
                    DbManager.Param("$goods", values["貨品編號"]), DbManager.Param("$name", values["品名"]),
                    DbManager.Param("$spec", values["規格"]), DbManager.Param("$qty", values["數量"]),
                    DbManager.Param("$unit", values["單位"]), DbManager.Param("$price", values["單價"]),
                    DbManager.Param("$amt", values["金額"]), DbManager.Param("$from", values["起點"]),
                    DbManager.Param("$to", values["訖點"]), DbManager.Param("$ton", values["噸數"]),
                    DbManager.Param("$plate", values["板數"]), DbManager.Param("$remark", values["備註說明"]));
            }
            else
            {
                var seq = Convert.ToInt64(row["__seq"]);
                DbManager.ExecuteNonQuery(
                    "UPDATE [託運明細] SET [貨品編號]=$goods,[品名]=$name,[規格]=$spec,[數量]=$qty,[單位]=$unit,[單價]=$price,[金額]=$amt," +
                    "[起點]=$from,[訖點]=$to,[噸數]=$ton,[板數]=$plate,[備註說明]=$remark WHERE [單據副碼]=$k AND [建檔序號]=$seq",
                    DbManager.Param("$goods", values["貨品編號"]), DbManager.Param("$name", values["品名"]),
                    DbManager.Param("$spec", values["規格"]), DbManager.Param("$qty", values["數量"]),
                    DbManager.Param("$unit", values["單位"]), DbManager.Param("$price", values["單價"]),
                    DbManager.Param("$amt", values["金額"]), DbManager.Param("$from", values["起點"]),
                    DbManager.Param("$to", values["訖點"]), DbManager.Param("$ton", values["噸數"]),
                    DbManager.Param("$plate", values["板數"]), DbManager.Param("$remark", values["備註說明"]),
                    DbManager.Param("$k", _shipKey), DbManager.Param("$seq", seq));
            }
            RecalcShipping();
            LoadShippingDetail();
            LoadShipping();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "儲存失敗：" + ex.Message, "託運作業", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteShippingDetail()
    {
        var row = ShipDetailRow();
        if (row is null) return;
        var seq = Convert.ToInt64(row["__seq"]);
        DbManager.ExecuteNonQuery("DELETE FROM [託運明細] WHERE [單據副碼]=$k AND [建檔序號]=$seq",
            DbManager.Param("$k", _shipKey), DbManager.Param("$seq", seq));
        RecalcShipping();
        LoadShippingDetail();
        LoadShipping();
    }

    /// <summary>重新計算託運單彙總：數量合計、合計金額、營業稅、總計金額</summary>
    private void RecalcShipping()
    {
        var dt = DbManager.QueryTable(
            "SELECT COALESCE(SUM([數量]),0) AS [qty], COALESCE(SUM([金額]),0) AS [amt] FROM [託運明細] WHERE [單據副碼]=$k",
            DbManager.Param("$k", _shipKey));
        if (dt.Rows.Count == 0) return;
        decimal qty = Convert.ToDecimal(dt.Rows[0]["qty"]);
        decimal amt = Convert.ToDecimal(dt.Rows[0]["amt"]);
        decimal tax = Math.Round(amt * 0.05m, 0);
        decimal total = amt + tax;
        DbManager.ExecuteNonQuery(
            "UPDATE [託運主檔] SET [數量合計]=$qty,[合計金額]=$amt,[營業稅]=$tax,[總計金額]=$total WHERE [單據副碼]=$k",
            DbManager.Param("$qty", qty), DbManager.Param("$amt", amt),
            DbManager.Param("$tax", tax), DbManager.Param("$total", total),
            DbManager.Param("$k", _shipKey));
    }

    // ==================== 共用 ====================

    private static long NextSubKey(string table) =>
        Convert.ToInt64(DbManager.QueryScalar($"SELECT COALESCE(MAX([單據副碼]),0)+1 FROM [{table}]") ?? 1L);

    private static long NextDetailSeq(string table, long key) =>
        Convert.ToInt64(DbManager.QueryScalar(
            $"SELECT COALESCE(MAX([建檔序號]),0)+1 FROM [{table}] WHERE [單據副碼]=$k",
            DbManager.Param("$k", key)) ?? 1L);

    private static void UpdateDetailCount(string masterTable, string detailTable, string countColumn, long key)
    {
        var cnt = Convert.ToInt32(DbManager.QueryScalar(
            $"SELECT COUNT(*) FROM [{detailTable}] WHERE [單據副碼]=$k",
            DbManager.Param("$k", key)) ?? 0);
        DbManager.ExecuteNonQuery(
            $"UPDATE [{masterTable}] SET [{countColumn}]=$c WHERE [單據副碼]=$k",
            DbManager.Param("$c", cnt), DbManager.Param("$k", key));
    }
}
