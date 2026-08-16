// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（系統健康監控）
// ════════════════════════════════════════════════════════
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 系統健康檢查表單：顯示資料庫完整性、WAL、備份、磁碟等檢查結果，
/// 並提供立即重新檢查與立即備份。
/// </summary>
public sealed class HealthCheckForm : Form
{
    private readonly DbConfig _config;
    private readonly DataGridView _grid;
    private readonly Label _lblStatus;

    public HealthCheckForm(DbConfig config)
    {
        _config = config;
        Text = "系統健康檢查";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = UiTheme.Background;

        Controls.Add(UiTheme.BuildHeader("系統健康檢查",
            "資料庫完整性、WAL 日誌、備份狀態與磁碟空間檢查"));

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
        var btnRun = new ModernButton { Text = "立即檢查", Width = 110 };
        btnRun.Click += (s, e) => RunChecks();
        var btnBackup = new ModernButton { Text = "立即備份", Width = 110, IsPrimary = false };
        btnBackup.Click += (s, e) => BackupNow();
        var btnExit = new ModernButton { Text = "離開", Width = 90, IsPrimary = false };
        btnExit.Click += (s, e) => Close();
        Add(btnRun); Add(btnBackup); Add(btnExit);
        Controls.Add(bar);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        UiTheme.StyleDataGridView(_grid);
        Controls.Add(_grid);

        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom, Height = 26, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub,
            Padding = new Padding(UiTheme.SpacingMd, 5, 0, 0), BackColor = UiTheme.Card,
        };
        Controls.Add(_lblStatus);

        Load += (s, e) => RunChecks();
        UiTheme.ClampToScreen(this);
    }

    private void RunChecks()
    {
        var items = HealthCheckService.RunAll(_config);
        _grid.DataSource = null;
        _grid.Rows.Clear();
        _grid.Columns.Clear();
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "項目", HeaderText = "檢查項目", Width = 130, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "狀態", HeaderText = "狀態", Width = 70, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "說明", HeaderText = "說明", ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "建議", HeaderText = "建議", Width = 260, ReadOnly = true });
        foreach (var it in items)
        {
            int idx = _grid.Rows.Add(it.項目, it.Status.ToString(), it.說明, it.建議);
            var color = it.Status switch
            {
                HealthCheckService.狀態.正常 => UiTheme.Ok,
                HealthCheckService.狀態.注意 => UiTheme.Warn,
                _ => UiTheme.Danger,
            };
            _grid.Rows[idx].Cells["狀態"].Style.ForeColor = color;
            _grid.Rows[idx].Cells["狀態"].Style.Font = UiTheme.Font(9.5F, FontStyle.Bold);
        }
        int 正常 = items.Count(i => i.Status == HealthCheckService.狀態.正常);
        int 注意 = items.Count(i => i.Status == HealthCheckService.狀態.注意);
        int 異常 = items.Count(i => i.Status == HealthCheckService.狀態.異常);
        _lblStatus.Text = $"狀態: 共 {items.Count} 項｜正常 {正常}｜注意 {注意}｜異常 {異常}";
    }

    private void BackupNow()
    {
        if (MessageBox.Show("立即建立目前資料庫的備份？", "備份確認",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            var dir = BackupService.DefaultBackupDir();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, BackupService.NewBackupName(DateTime.Now));
            BackupService.BackupTo(path);
            AuditService.Log(AuditService.存檔, "系統健康", "手動備份", "成功");
            MessageBox.Show($"備份完成：\n{path}", "備份", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RunChecks();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"備份失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
