// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（稽核日誌）
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 稽核日誌檢視：管理員查核登入／登出／失敗嘗試與關鍵單據異動軌跡。
/// 可依事件、帳號、日期區間過濾，並匯出 CSV 留檔。
/// </summary>
public sealed class AuditLogForm : Form
{
    private readonly ComboBox _cmbEvent;
    private readonly TextBox _txtAccount;
    private readonly DateTimePicker _dtpFrom;
    private readonly DateTimePicker _dtpTo;
    private readonly DataGridView _grid;
    private readonly Label _lblStatus;

    public AuditLogForm()
    {
        Text = "稽核日誌";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        BackColor = UiTheme.Background;

        Controls.Add(UiTheme.BuildHeader("稽核日誌", "登入／登出／失敗嘗試與關鍵單據異動軌跡（僅系統管理員可見）"));

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingMd) };

        _cmbEvent = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Location = new Point(96, 10) };
        _cmbEvent.Items.Add("全部事件");
        _cmbEvent.Items.Add(AuditService.登入成功);
        _cmbEvent.Items.Add(AuditService.登入失敗);
        _cmbEvent.Items.Add(AuditService.登出);
        _cmbEvent.Items.Add(AuditService.存檔);
        _cmbEvent.Items.Add(AuditService.刪除);
        _cmbEvent.Items.Add(AuditService.變更密碼);
        _cmbEvent.Items.Add(AuditService.系統);
        _cmbEvent.SelectedIndex = 0;
        UiTheme.StyleComboBox(_cmbEvent);

        _txtAccount = new TextBox { Location = new Point(280, 10), Width = 130 };
        UiTheme.StyleTextBox(_txtAccount);

        _dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Location = new Point(470, 10), Width = 100 };
        UiTheme.StyleDateTimePicker(_dtpFrom);
        _dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Location = new Point(590, 10), Width = 100 };
        UiTheme.StyleDateTimePicker(_dtpTo);

        var btnQuery = new ModernButton { Text = "查詢", Width = 80, Height = 34, Location = new Point(710, 9), IsPrimary = true, DrawShadow = false };
        btnQuery.Click += (s, e) => LoadLog();
        var btnExport = new ModernButton { Text = "匯出 CSV", Width = 100, Height = 34, Location = new Point(800, 9), IsPrimary = false, DrawShadow = false };
        btnExport.Click += (s, e) => ExportCsv();
        var btnClear = new ModernButton { Text = "清除條件", Width = 90, Height = 34, Location = new Point(910, 9), IsPrimary = false, DrawShadow = false };
        btnClear.Click += (s, e) => { _cmbEvent.SelectedIndex = 0; _txtAccount.Clear(); _dtpFrom.Value = DateTime.Now.AddMonths(-1); _dtpTo.Value = DateTime.Now; LoadLog(); };

        toolbar.Controls.Add(MakeFieldLabel("事件：", 6, _cmbEvent.Top));
        toolbar.Controls.Add(MakeFieldLabel("帳號：", 230, _txtAccount.Top));
        toolbar.Controls.Add(MakeFieldLabel("期間：", 420, _dtpFrom.Top));
        toolbar.Controls.Add(_cmbEvent);
        toolbar.Controls.Add(_txtAccount);
        toolbar.Controls.Add(_dtpFrom);
        toolbar.Controls.Add(_dtpTo);
        toolbar.Controls.Add(btnQuery);
        toolbar.Controls.Add(btnExport);
        toolbar.Controls.Add(btnClear);
        Controls.Add(toolbar);

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
            Dock = DockStyle.Bottom,
            Height = 26,
            Font = UiTheme.Font(9.5F),
            ForeColor = UiTheme.TextSub,
            Padding = new Padding(UiTheme.SpacingMd, 5, 0, 0),
            BackColor = UiTheme.Card,
        };
        Controls.Add(_lblStatus);

        _dtpFrom.Value = DateTime.Now.AddMonths(-1);
        _dtpTo.Value = DateTime.Now;
        ShortcutHelper.Enable(this, onSearch: LoadLog);
        Load += (s, e) => LoadLog();
        UiTheme.ClampToScreen(this);
    }

    private Label MakeFieldLabel(string text, int x, int top)
    {
        var lbl = new Label
        {
            Text = text,
            Font = UiTheme.Font(9.5F),
            ForeColor = UiTheme.TextSub,
            AutoSize = true,
            Location = new Point(x, top + 5),
        };
        return lbl;
    }

    private void LoadLog()
    {
        try
        {
            AuditService.EnsureSchema();
        }
        catch
        {
            // 無表時查詢自然為空
        }
        string where = "1=1";
        var pars = new List<Microsoft.Data.Sqlite.SqliteParameter>();
        if (_cmbEvent.SelectedIndex > 0)
        {
            where += " AND [事件] = $e";
            pars.Add(DbManager.Param("$e", _cmbEvent.SelectedItem?.ToString()));
        }
        if (_txtAccount.Text.Trim().Length > 0)
        {
            where += " AND ([帳號] LIKE $a OR [使用者] LIKE $a)";
            pars.Add(DbManager.Param("$a", "%" + _txtAccount.Text.Trim() + "%"));
        }
        where += " AND [時間] >= $f AND [時間] <= $t";
        pars.Add(DbManager.Param("$f", _dtpFrom.Value.Date.ToString("yyyy-MM-dd 00:00:00")));
        pars.Add(DbManager.Param("$t", _dtpTo.Value.Date.ToString("yyyy-MM-dd 23:59:59")));

        var dt = DbManager.QueryTable(
            "SELECT [時間], [帳號], [使用者], [機器], [事件], [模組], [對象], [結果], [詳細] " +
            "FROM [稽核日誌] WHERE " + where + " ORDER BY [序號] DESC LIMIT 2000",
            pars.ToArray());
        _grid.DataSource = dt;
        _lblStatus.Text = $"共 {dt.Rows.Count:N0} 筆紀錄（依時間倒序，最多顯示 2000 筆）";
    }

    private void ExportCsv()
    {
        if (_grid.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("沒有可匯出的資料。", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ExportService.ExportAny(this, dt, $"稽核日誌_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx", "匯出稽核日誌（Excel／CSV）");
    }
}
