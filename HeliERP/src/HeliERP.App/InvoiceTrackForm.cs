// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（電子發票字軌管理）
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 電子發票字軌管理：建置各期字軌（年度／月期／字軌／起迄號）、
/// 啟停用、自動配號切換，並檢視每張發票的開立與作廢紀錄（含使用進度）。
/// </summary>
public sealed class InvoiceTrackForm : Form
{
    private DataGridView _gridTracks = null!;
    private DataGridView _gridLog = null!;
    private ComboBox _cmbTrackFilter = null!;
    private ComboBox _cmbStatusFilter = null!;
    private TextBox _txtInvoiceNo = null!;
    private Label _lblStatus = null!;

    public InvoiceTrackForm()
    {
        Text = "電子發票字軌管理";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = UiTheme.Background;

        Controls.Add(UiTheme.BuildHeader("電子發票字軌管理",
            "建置國稅局配發之字軌；出貨／進貨存檔時可自動配號並登記開立紀錄"));

        BuildToolbar();
        BuildTrackGrid();
        BuildLogSection();
        BuildStatusBar();

        Load += (s, e) =>
        {
            try
            {
                InvoiceTrackService.EnsureSchema();
                LoadTracks();
                _lblStatus.Text = "狀態: 就緒";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "狀態: 載入失敗 - " + ex.Message;
            }
        };
        ShortcutHelper.Enable(this, onSearch: LoadLog);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    // ==================== 版面 ====================

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

        var btnAdd = new ModernButton { Text = "新增字軌", Width = 120 };
        btnAdd.Click += (s, e) => ShowTrackEditor(null);
        var btnEdit = new ModernButton { Text = "修改字軌", Width = 120, IsPrimary = false };
        btnEdit.Click += (s, e) => ShowTrackEditor(SelectedTrack());
        var btnToggle = new ModernButton { Text = "停用／啟用", Width = 130, IsPrimary = false };
        btnToggle.Click += (s, e) => ToggleStatus();
        var btnDelete = new ModernButton { Text = "刪除字軌", Width = 120, IsPrimary = false };
        btnDelete.Click += (s, e) => DeleteTrack();
        var btnExport = new ModernButton { Text = "匯出字軌", Width = 120, IsPrimary = false };
        btnExport.Click += (s, e) => ExportTracks();
        var btnExit = new ModernButton { Text = "離開", Width = 100, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnAdd); Add(btnEdit); Add(btnToggle); Add(btnDelete); Add(btnExport); Add(btnExit);
        Controls.Add(bar);
    }

    private void BuildTrackGrid()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 300, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingSm) };
        var lbl = new Label
        {
            Text = "字軌使用進度（已用迄號為 0 表示尚未開立）",
            Font = UiTheme.Font(12F, FontStyle.Bold),
            ForeColor = UiTheme.Primary,
            AutoSize = true,
            Location = new Point(UiTheme.SpacingSm, 2),
        };
        panel.Controls.Add(lbl);

        _gridTracks = new DataGridView
        {
            Location = new Point(UiTheme.SpacingSm, 30),
            Size = new Size(1180, 256),
            ReadOnly = true,
            AllowUserToAddRows = false,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        UiTheme.StyleDataGridView(_gridTracks);
        _gridTracks.SelectionChanged += (s, e) => LoadLog();
        _gridTracks.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var status = _gridTracks.Rows[e.RowIndex].Cells["狀態"]?.Value as string;
            if (status == InvoiceTrackService.停用)
                e.CellStyle!.ForeColor = UiTheme.TextFaint;
            else if (e.ColumnIndex == _gridTracks.Columns["剩餘張數"]?.Index && e.RowIndex < _gridTracks.Rows.Count)
            {
                var remaining = _gridTracks.Rows[e.RowIndex].Cells["剩餘張數"]?.Value;
                if (remaining is long or decimal or int && Convert.ToInt64(remaining) <= 0)
                    e.CellStyle!.ForeColor = UiTheme.Danger;
            }
        };
        panel.Controls.Add(_gridTracks);
        Controls.Add(panel);
    }

    private void BuildLogSection()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingSm) };
        var filterRow = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = UiTheme.Card };

        _cmbTrackFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Location = new Point(76, 7) };
        UiTheme.StyleComboBox(_cmbTrackFilter);
        _cmbTrackFilter.SelectedIndexChanged += (s, e) => LoadLog();
        _cmbStatusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90, Location = new Point(320, 7) };
        UiTheme.StyleComboBox(_cmbStatusFilter);
        _cmbStatusFilter.Items.Add("全部");
        _cmbStatusFilter.Items.Add(InvoiceTrackService.開立);
        _cmbStatusFilter.Items.Add(InvoiceTrackService.作廢);
        _cmbStatusFilter.SelectedIndex = 0;
        _cmbStatusFilter.SelectedIndexChanged += (s, e) => LoadLog();
        UiTheme.StyleComboBox(_cmbStatusFilter);
        _txtInvoiceNo = new TextBox { Location = new Point(434, 7), Width = 150 };
        UiTheme.StyleTextBox(_txtInvoiceNo);
        _txtInvoiceNo.TextChanged += (s, e) => LoadLog();

        var lblTrack = new Label { Text = "字軌：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub, AutoSize = true, Location = new Point(10, 12) };
        var lblStatus = new Label { Text = "狀態：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub, AutoSize = true, Location = new Point(262, 12) };
        var lblNo = new Label { Text = "發票號碼：", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextSub, AutoSize = true, Location = new Point(378, 12) };

        var btnExportLog = new ModernButton { Text = "匯出紀錄", Width = 100, Height = 34, Location = new Point(600, 5), IsPrimary = false, DrawShadow = false };
        btnExportLog.Click += (s, e) => ExportLog();

        filterRow.Controls.Add(lblTrack);
        filterRow.Controls.Add(lblStatus);
        filterRow.Controls.Add(lblNo);
        filterRow.Controls.Add(_cmbTrackFilter);
        filterRow.Controls.Add(_cmbStatusFilter);
        filterRow.Controls.Add(_txtInvoiceNo);
        filterRow.Controls.Add(btnExportLog);
        panel.Controls.Add(filterRow);

        _gridLog = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        UiTheme.StyleDataGridView(_gridLog);
        _gridLog.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            if (_gridLog.Columns.Contains("狀態") && e.ColumnIndex == _gridLog.Columns["狀態"].Index)
            {
                if ((_gridLog.Rows[e.RowIndex].Cells["狀態"]?.Value as string) == InvoiceTrackService.作廢)
                {
                    e.CellStyle!.ForeColor = UiTheme.Danger;
                    e.CellStyle.Font = UiTheme.Font(9.5F, FontStyle.Bold);
                }
            }
        };
        panel.Controls.Add(_gridLog);
        Controls.Add(panel);
    }

    private void BuildStatusBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 28 };
        bar.BackColor = Color.White;
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(UiTheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };
        _lblStatus = new Label
        {
            Text = "狀態: 就緒",
            Font = UiTheme.Font(9.5F),
            ForeColor = UiTheme.TextSub,
            AutoSize = true,
            Location = new Point(UiTheme.SpacingMd, 6),
        };
        bar.Controls.Add(_lblStatus);
        Controls.Add(bar);
    }

    // ==================== 資料載入 ====================

    private DataRow? SelectedTrack()
    {
        if (_gridTracks.SelectedRows.Count == 0 || _gridTracks.SelectedRows[0].IsNewRow)
        {
            MessageBox.Show("請先於字軌清單選取一筆。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        var row = _gridTracks.SelectedRows[0];
        return (row.DataBoundItem as DataRowView)?.Row ?? row.DataBoundItem as DataRow;
    }

    private void LoadTracks()
    {
        var dt = InvoiceTrackService.LoadTracks();
        _gridTracks.DataSource = dt;
        if (_gridTracks.Columns.Contains("剩餘張數"))
            _gridTracks.Columns["剩餘張數"].DefaultCellStyle.Format = "N0";

        // 過濾下拉重建（保留原選取）
        string? prev = _cmbTrackFilter.SelectedIndex > 0 ? _cmbTrackFilter.SelectedItem?.ToString() : null;
        _cmbTrackFilter.Items.Clear();
        _cmbTrackFilter.Items.Add("全部字軌");
        foreach (DataRow r in dt.Rows)
        {
            string label = $"{r["年度"]}-{r["月期"]}　{r["字軌"]}";
            _cmbTrackFilter.Items.Add(label);
        }
        _cmbTrackFilter.SelectedIndex = prev is null ? 0 : Math.Max(0, _cmbTrackFilter.Items.IndexOf(prev));
    }

    private void LoadLog()
    {
        long? seq = null;
        if (_cmbTrackFilter.SelectedIndex > 0 && _gridTracks.SelectedRows.Count > 0)
        {
            var row = _gridTracks.SelectedRows[0];
            if (row.DataBoundItem is DataRowView drv && drv.Row.Table.Columns.Contains("序號"))
                seq = Convert.ToInt64(drv.Row["序號"]);
            else if (row.DataBoundItem is DataRow dr && dr.Table.Columns.Contains("序號"))
                seq = Convert.ToInt64(dr["序號"]);
        }
        var dt = InvoiceTrackService.LoadIssueLog(seq,
            _cmbStatusFilter.SelectedItem?.ToString() ?? "全部",
            _txtInvoiceNo.Text.Trim());
        _gridLog.DataSource = dt;
    }

    // ==================== 動作 ====================

    private void ShowTrackEditor(DataRow? track)
    {
        bool isNew = track is null;
        using var dlg = new Form
        {
            Text = isNew ? "新增字軌" : "修改字軌",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 340),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10.5F),
        };

        var txtYear = MakeInput(dlg, 22, 36, "年度（民國）：", track?["年度"]?.ToString() ?? "");
        var txtMonth = MakeInput(dlg, 22, 80, "月期（如 01-12）：", track?["月期"]?.ToString() ?? "");
        var txtTrack = MakeInput(dlg, 22, 124, "字軌（英文）：", track?["字軌"]?.ToString() ?? "");
        var txtStart = MakeInput(dlg, 22, 168, "起號（8碼）：", track is null ? "" : $"{Convert.ToInt64(track["起號"]):D8}");
        var txtEnd = MakeInput(dlg, 22, 212, "迄號（8碼）：", track is null ? "" : $"{Convert.ToInt64(track["迄號"]):D8}");
        var chkAuto = new CheckBox
        {
            Text = "自動配號（出貨／進貨存檔時自動取號）",
            Font = UiTheme.Font(9.5F),
            ForeColor = UiTheme.TextMain,
            AutoSize = true,
            Location = new Point(22, 242),
            Checked = track is not null && Convert.ToInt64(track["自動配號"]) == 1,
        };

        var lblMsg = new Label { Text = "", Font = UiTheme.Font(9F), ForeColor = UiTheme.Danger, AutoSize = true, Location = new Point(22, 268) };
        var btnOk = new ModernButton { Text = "確定", Size = new Size(90, 36), Location = new Point(150, 292), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(80, 36), Location = new Point(252, 292), IsPrimary = false, DrawShadow = false };

        btnOk.Click += (s, e) =>
        {
            lblMsg.Text = "";
            if (!long.TryParse(txtStart.Text.Trim(), out var start) ||
                !long.TryParse(txtEnd.Text.Trim(), out var end))
            {
                lblMsg.Text = "起號／迄號必須是數字";
                return;
            }
            var req = new InvoiceTrackService.TrackSaveRequest(
                txtYear.Text.Trim(), txtMonth.Text.Trim(), txtTrack.Text.Trim(), start, end, chkAuto.Checked, "");
            try
            {
                if (isNew)
                    InvoiceTrackService.SaveTrack(null, req);
                else
                    InvoiceTrackService.SaveTrack(Convert.ToInt64(track!["序號"]), req);
                AuditService.Log(AuditService.存檔, "電子發票", $"字軌 {txtTrack.Text.Trim().ToUpperInvariant()}",
                    "成功", isNew ? "新增字軌" : "修改字軌");
            }
            catch (Exception ex)
            {
                lblMsg.Text = ex.Message;
                return;
            }
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;
        dlg.Controls.AddRange(new Control[] { txtYear, txtMonth, txtTrack, txtStart, txtEnd, chkAuto, lblMsg, btnOk, btnCancel });
        UiTheme.ScaleForDpi(dlg);
        UiTheme.ClampToScreen(dlg);

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            LoadTracks();
            _lblStatus.Text = "狀態: 字軌已儲存";
        }
    }

    private TextBox MakeInput(Form owner, int x, int y, string label, string value)
    {
        owner.Controls.Add(new Label
        {
            Text = label,
            Font = UiTheme.Font(9.5F),
            ForeColor = UiTheme.TextSub,
            AutoSize = true,
            Location = new Point(x, y),
        });
        var box = new TextBox { Location = new Point(150, y - 3), Width = 230, Text = value };
        UiTheme.StyleTextBox(box);
        owner.Controls.Add(box);
        return box;
    }

    private void ToggleStatus()
    {
        var row = SelectedTrack();
        if (row is null) return;
        bool isActive = Convert.ToString(row["狀態"]) == InvoiceTrackService.啟用;
        string next = isActive ? InvoiceTrackService.停用 : InvoiceTrackService.啟用;
        var confirm = MessageBox.Show($"確定將字軌「{row["年度"]}-{row["月期"]}　{row["字軌"]}」改為「{next}」嗎？",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        InvoiceTrackService.SetTrackStatus(Convert.ToInt64(row["序號"]), next);
        AuditService.Log(AuditService.存檔, "電子發票", $"字軌 {row["字軌"]}", "成功", $"狀態改為 {next}");
        LoadTracks();
        _lblStatus.Text = $"狀態: 字軌已{next}";
    }

    private void DeleteTrack()
    {
        var row = SelectedTrack();
        if (row is null) return;
        long 序號 = Convert.ToInt64(row["序號"]);
        var confirm = MessageBox.Show($"確定刪除字軌「{row["年度"]}-{row["月期"]}　{row["字軌"]}」？（已有開立紀錄者不可刪除）",
            "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        try
        {
            InvoiceTrackService.DeleteTrack(序號);
            AuditService.Log(AuditService.刪除, "電子發票", $"字軌 {row["字軌"]}", "成功");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刪除失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        LoadTracks();
        _lblStatus.Text = "狀態: 字軌已刪除";
    }

    private void ExportTracks()
    {
        if (_gridTracks.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("沒有可匯出的資料。", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ExportService.ExportAny(this, dt, $"發票字軌_{DateTime.Now:yyyyMMdd}.xlsx", "匯出發票字軌（Excel／CSV）");
    }

    private void ExportLog()
    {
        if (_gridLog.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("沒有可匯出的資料。", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ExportService.ExportAny(this, dt, $"發票開立紀錄_{DateTime.Now:yyyyMMdd}.xlsx", "匯出發票開立紀錄（Excel／CSV）");
    }
}
