// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（核准中心）
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 核准中心：檢視採購／訂貨／收款／付款等單據的核准流程，
/// 逐層核准或退回（附意見），並可調整各類別核准層數與啟用狀態。
/// </summary>
public sealed class ApprovalForm : Form
{
    private readonly ComboBox _cmbType = null!;
    private readonly ComboBox _cmbStatus = null!;
    private readonly TextBox _txtKeyword = null!;
    private readonly DataGridView _grid = null!;
    private readonly DataGridView _gridRecords = null!;
    private readonly TextBox _txtOpinion = null!;
    private readonly Label _lblStatus = null!;
    private readonly ModernButton _btnApprove = null!;
    private readonly ModernButton _btnReject = null!;

    public ApprovalForm()
    {
        Text = "核准中心";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = UiTheme.Background;

        Controls.Add(UiTheme.BuildHeader("核准中心", "採購／訂貨／收款／付款等單據多層核准、退回與進度追蹤"));

        // ── 工具列 ──
        var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.PrimaryDark };
        int x = UiTheme.SpacingMd;
        void Add(ModernButton b)
        {
            b.Location = new Point(x, 6);
            b.Height = 40;
            b.DrawShadow = false;
            bar.Controls.Add(b);
            x += b.Width + UiTheme.SpacingSm;
        }
        var btnReload = new ModernButton { Text = "重新整理", Width = 110 };
        btnReload.Click += (s, e) => LoadFlows();
        var btnSettings = new ModernButton { Text = "層數設定", Width = 110, IsPrimary = false };
        btnSettings.Click += (s, e) => ShowSettings();
        var btnExport = new ModernButton { Text = "匯出 CSV", Width = 110, IsPrimary = false };
        btnExport.Click += (s, e) => Export();
        var btnExit = new ModernButton { Text = "離開", Width = 90, IsPrimary = false };
        btnExit.Click += (s, e) => Close();
        Add(btnReload); Add(btnSettings); Add(btnExport); Add(btnExit);
        Controls.Add(bar);

        // ── 過濾列 ──
        var filter = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = UiTheme.Card };
        filter.Controls.Add(new Label { Text = "單據類別：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub, AutoSize = true, Location = new Point(12, 14) });
        _cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Location = new Point(88, 8) };
        UiTheme.StyleComboBox(_cmbType);
        _cmbType.Items.Add("全部");
        _cmbType.Items.AddRange(ApprovalService.預設類別);
        _cmbType.SelectedIndex = 0;
        _cmbType.SelectedIndexChanged += (s, e) => LoadFlows();
        filter.Controls.Add(_cmbType);

        filter.Controls.Add(new Label { Text = "狀態：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub, AutoSize = true, Location = new Point(238, 14) });
        _cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Location = new Point(286, 8) };
        UiTheme.StyleComboBox(_cmbStatus);
        _cmbStatus.Items.AddRange(new object[] { "全部", ApprovalService.待核准, ApprovalService.已核准, ApprovalService.已退回 });
        _cmbStatus.SelectedIndex = 0;
        _cmbStatus.SelectedIndexChanged += (s, e) => LoadFlows();
        filter.Controls.Add(_cmbStatus);

        filter.Controls.Add(new Label { Text = "單號／申請人：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub, AutoSize = true, Location = new Point(406, 14) });
        _txtKeyword = new TextBox { Location = new Point(498, 8), Width = 200 };
        UiTheme.StyleTextBox(_txtKeyword);
        _txtKeyword.TextChanged += (s, e) => LoadFlows();
        filter.Controls.Add(_txtKeyword);
        Controls.Add(filter);

        // ── 下方：核准紀錄 + 意見 ──
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 220, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingSm) };
        bottom.Controls.Add(new Label
        {
            Text = "核准紀錄（選取上表單據後顯示）", Font = UiTheme.Font(11F, FontStyle.Bold),
            ForeColor = UiTheme.Primary, AutoSize = true, Location = new Point(UiTheme.SpacingSm, 2),
        });
        _gridRecords = new DataGridView
        {
            Location = new Point(UiTheme.SpacingSm, 26), Size = new Size(680, 110),
            ReadOnly = true, AllowUserToAddRows = false, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        UiTheme.StyleDataGridView(_gridRecords);
        bottom.Controls.Add(_gridRecords);

        bottom.Controls.Add(new Label
        {
            Text = "核准意見：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub,
            AutoSize = true, Location = new Point(UiTheme.SpacingSm, 146),
        });
        _txtOpinion = new TextBox { Location = new Point(88, 144), Size = new Size(500, 26) };
        UiTheme.StyleTextBox(_txtOpinion);
        bottom.Controls.Add(_txtOpinion);

        _btnApprove = new ModernButton { Text = "核准（到下一層）", Size = new Size(160, 40), Location = new Point(UiTheme.SpacingSm, 178), IsPrimary = true };
        _btnApprove.Click += (s, e) => Decide(true);
        _btnReject = new ModernButton { Text = "退回", Size = new Size(100, 40), Location = new Point(180, 178), IsPrimary = false };
        _btnReject.Click += (s, e) => Decide(false);
        bottom.Controls.Add(_btnApprove);
        bottom.Controls.Add(_btnReject);
        Controls.Add(bottom);

        // ── 主清單 ──
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        UiTheme.StyleDataGridView(_grid);
        _grid.SelectionChanged += (s, e) => ShowRecords();
        Controls.Add(_grid);

        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom, Height = 26, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub,
            Padding = new Padding(UiTheme.SpacingMd, 5, 0, 0), BackColor = UiTheme.Card,
        };
        Controls.Add(_lblStatus);

        Load += (s, e) =>
        {
            try
            {
                ApprovalService.EnsureSchema();
                LoadFlows();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "狀態: 載入失敗 - " + ex.Message;
            }
        };
        ShortcutHelper.Enable(this, onSearch: () => _txtKeyword.Focus(), onDelete: () => { }, onAdd: () => ShowSettings());
    }

    private void LoadFlows()
    {
        var dt = ApprovalService.LoadFlows(
            _cmbType.SelectedItem?.ToString(),
            _cmbStatus.SelectedItem?.ToString(),
            _txtKeyword.Text.Trim());
        _grid.DataSource = dt;
        if (_grid.Columns.Contains("金額"))
            _grid.Columns["金額"].DefaultCellStyle.Format = "N2";
        if (_grid.Columns.Contains("序號"))
            _grid.Columns["序號"].Visible = false;
        _lblStatus.Text = $"狀態: 核准流程共 {dt.Rows.Count:N0} 筆";
        ShowRecords();
    }

    private long? SelectedFlow()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].DataBoundItem is not DataRowView drv)
            return null;
        return Convert.ToInt64(drv.Row["序號"]);
    }

    private void ShowRecords()
    {
        var seq = SelectedFlow();
        if (seq is null)
        {
            _gridRecords.DataSource = null;
            _btnApprove.Enabled = false;
            _btnReject.Enabled = false;
            return;
        }
        var dt = ApprovalService.LoadRecords(seq.Value);
        _gridRecords.DataSource = dt;
        bool pending = _grid.SelectedRows.Count > 0 &&
            (_grid.SelectedRows[0].DataBoundItem as DataRowView)?.Row?["狀態"]?.ToString() == ApprovalService.待核准;
        _btnApprove.Enabled = pending;
        _btnReject.Enabled = pending;
        if (dt.Columns.Contains("意見") && dt.Columns["意見"] != null)
            _gridRecords.Columns["意見"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void Decide(bool approve)
    {
        var seq = SelectedFlow();
        if (seq is null)
        {
            MessageBox.Show("請先選取一筆核准流程。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var row = (_grid.SelectedRows[0].DataBoundItem as DataRowView)?.Row;
        string 單據 = row is null ? seq.Value.ToString() : $"{row["單據類別"]} {row["單號"]}";
        string action = approve ? "核准" : "退回";
        if (MessageBox.Show($"確定{action}單據「{單據}」？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        string? error = approve
            ? ApprovalService.Approve(seq.Value, AuditService.CurrentUser, _txtOpinion.Text.Trim())
            : ApprovalService.Reject(seq.Value, AuditService.CurrentUser, _txtOpinion.Text.Trim());
        if (error is not null)
        {
            MessageBox.Show($"操作失敗：{error}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        AuditService.Log(AuditService.存檔, "核准中心", 單據, "成功", $"{action}（{AuditService.CurrentUser}）");
        _txtOpinion.Clear();
        LoadFlows();
    }

    private void ShowSettings()
    {
        using var dlg = new Form
        {
            Text = "核准層數設定",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(430, 360),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10.5F),
        };
        var grid = new DataGridView
        {
            Location = new Point(14, 14), Size = new Size(402, 210),
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            MultiSelect = false,
        };
        UiTheme.StyleDataGridView(grid);
        var settings = ApprovalService.LoadSettings();
        grid.DataSource = settings;
        if (grid.Columns.Contains("啟用"))
            grid.Columns["啟用"].ReadOnly = false;
        if (grid.Columns.Contains("層數"))
            grid.Columns["層數"].ReadOnly = false;
        dlg.Controls.Add(grid);

        var lblMsg = new Label { Text = "勾選「啟用」後，該類別單據存檔時會自動送審；層數為需核准的層級數。", Font = UiTheme.Font(8.5F), ForeColor = UiTheme.TextSub, AutoSize = true, Location = new Point(14, 232) };
        dlg.Controls.Add(lblMsg);

        var btnSave = new ModernButton { Text = "儲存設定", Size = new Size(100, 40), Location = new Point(120, 278), IsPrimary = true };
        btnSave.Click += (s, e) =>
        {
            try
            {
                using var tx = DbManager.OpenConnection();
                tx.Close();
                foreach (DataRowView? drv in grid.Rows.Cast<DataGridViewRow>()
                    .Select(r => r.DataBoundItem).OfType<DataRowView>())
                {
                    var row = drv!.Row;
                    string 類別 = Convert.ToString(row["單據類別"]) ?? "";
                    int 層數 = Convert.ToInt32(row["層數"]);
                    bool 啟用 = Convert.ToInt64(row["啟用"]) == 1;
                    ApprovalService.SaveSetting(類別, 層數, 啟用);
                }
                AuditService.Log(AuditService.存檔, "核准中心", "核准層數設定", "成功");
                dlg.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                lblMsg.Text = "儲存失敗：" + ex.Message;
                lblMsg.ForeColor = UiTheme.Danger;
            }
        };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(80, 40), Location = new Point(232, 278), IsPrimary = false, DrawShadow = false };
        btnCancel.Click += (s, e) => dlg.Close();
        dlg.Controls.Add(btnSave);
        dlg.Controls.Add(btnCancel);

        dlg.ShowDialog(this);
    }

    private void Export()
    {
        if (_grid.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("沒有可匯出的資料。", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ExportService.ExportAny(this, dt, $"核准流程_{DateTime.Now:yyyyMMdd}.xlsx", "匯出核准流程（Excel／CSV）");
    }
}
