// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0（表單式主檔維護框架）
// ════════════════════════════════════════════════════════
using System.Data;
using System.Globalization;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>表單欄位類型</summary>
public enum FormFieldKind
{
    /// <summary>單行文字</summary>
    Text,

    /// <summary>數字（右對齊）</summary>
    Number,

    /// <summary>日期（可勾選空白）</summary>
    Date,

    /// <summary>下拉（可輸入）</summary>
    Combo,

    /// <summary>多行文字</summary>
    Memo,

    /// <summary>唯讀文字</summary>
    ReadOnly,
}

/// <summary>表單欄位定義：指定資料庫欄位、顯示標籤與控制項型別。</summary>
public sealed record FormField(
    string Field,
    string Label,
    FormFieldKind Kind = FormFieldKind.Text,
    int Row = 0,
    int Col = 0,
    int Span = 1,
    string[]? Items = null,
    string? ComboSql = null);

/// <summary>表單分頁定義。標題為「全部欄位」且欄位為空時，自動補上未定義欄位。</summary>
public sealed record FormPage(string Title, IReadOnlyList<FormField> Fields);

/// <summary>
/// 表單式主檔維護視窗：以「欄位定義」驅動，左側清單＋右側分頁表單，
/// 提供新增／儲存／刪除／搜尋。欄位定義見 FormMasterCatalog。
/// 可彈性定義分頁、下拉、日期、多行等控制項，輸入比通用表格直覺。
/// </summary>
public sealed class FormMasterForm : Form
{
    private readonly string _tableName;
    private readonly IReadOnlyList<string> _pkColumns;
    private readonly IReadOnlyList<string> _listColumns;
    private readonly IReadOnlyList<FormPage> _pages;

    private DataTable _dt = new();
    private readonly HashSet<string> _existingKeys = new();
    private readonly Dictionary<string, Control> _editors = new();
    private readonly Dictionary<string, Label> _labels = new();

    private DataTable _listDt = new();
    private DataGridView _grid = new();
    private TextBox _txtSearch = new();
    private ToolStripLabel _lblStatus = new();
    private SplitContainer _split = new();
    private ToolStripButton _btnToggleList = new();
    private DataRow? _currentRow;          // null = 新增模式
    private bool _loading;                 // 同步期間抑制事件
    private bool _splitterSet;             // 左清單寬度僅於首次 Shown 設定

    public FormMasterForm(string tableName, IReadOnlyList<string> listColumns, IReadOnlyList<FormPage> pages)
    {
        _tableName = tableName;
        _listColumns = listColumns;
        _pages = pages;
        _pkColumns = TableCatalog.GetKeyFields(tableName);

        Text = $"{tableName} - 資料維護";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Font = new Font("Microsoft JhengHei UI", 11F);
        UiTheme.Apply(this);

        using (new CursorScope(Cursors.WaitCursor))
        {
            ReloadData();
        }
        _dt.PrimaryKey = Array.Empty<DataColumn>();
        foreach (var name in _pkColumns)
            if (_dt.Columns[name] is { } col)
                col.Unique = false;

        var pagesFinal = new List<FormPage>(_pages);
        var last = pagesFinal.LastOrDefault();
        if (last is { Title: "全部欄位" } && last.Fields.Count == 0)
            pagesFinal[pagesFinal.Count - 1] = BuildAllFieldsPage(last);

        BuildUi(pagesFinal);
        ReloadList();
        UpdateStatus();
        UiTheme.ClampToScreen(this);
    }

    // ══════════════════════════ 資料載入 ══════════════════════════

    private void ReloadData()
    {
        _dt = DbManager.QueryTable($"SELECT * FROM \"{_tableName}\"");
        _dt.AcceptChanges();
        _existingKeys.Clear();
        foreach (DataRow row in _dt.Rows)
            _existingKeys.Add(KeyOf(row));
    }

    private string KeyOf(DataRow row)
    {
        var parts = _pkColumns.Select(c =>
        {
            var v = row[c];
            return v is DBNull or null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture)!.Trim();
        });
        return string.Join("|", parts);
    }

    private FormPage BuildAllFieldsPage(FormPage last)
    {
        var defined = _pages.SelectMany(p => p.Fields).Select(f => f.Field).ToHashSet();
        var fields = new List<FormField>();
        int row = 0, col = 0;
        foreach (DataColumn dc in _dt.Columns)
        {
            if (defined.Contains(dc.ColumnName)) continue;
            var kind = IsNumeric(dc.DataType) ? FormFieldKind.Number : FormFieldKind.Text;
            fields.Add(new FormField(dc.ColumnName, dc.ColumnName, kind, row, col));
            if (++col >= 4) { col = 0; row++; }
        }
        return last with { Fields = fields };
    }

    private static bool IsNumeric(Type t) =>
        t == typeof(double) || t == typeof(float) || t == typeof(decimal) ||
        t == typeof(long) || t == typeof(int) || t == typeof(short);

    // ══════════════════════════ 介面建構 ══════════════════════════

    private void BuildUi(IReadOnlyList<FormPage> pages)
    {
        Controls.Add(UiTheme.BuildHeader($"{_tableName} 維護", "左側清單選取 → 右側表單編輯 → 儲存"));
        BuildToolbar();
        BuildStatusBar();

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
        };
        _split.Panel1.BackColor = UiTheme.Background;
        _split.Panel2.BackColor = UiTheme.Background;
        Controls.Add(_split);
        _split.BringToFront();
        Shown += (s, e) =>
        {
            try { SetSplitterWidth(); }
            catch { _splitterSet = true; } // 窄視窗等異常情況：維持預設各半，不阻擋開啟
        };

        BuildListGrid(_split.Panel1);
        BuildFormTabs(_split.Panel2, pages);
    }

    private void BuildToolbar()
    {
        var toolbar = new ToolStrip();
        UiTheme.StyleToolStrip(toolbar);
        var navGroup = new ToolStripLabel("  資料：");
        toolbar.Items.Add(new ToolStripButton("新增", null, (s, e) => StartNew()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("複製", null, (s, e) => DuplicateCurrent()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("存檔", null, (s, e) => SaveCurrent()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("刪除", null, (s, e) => DeleteCurrent()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("取消", null, (s, e) => CancelEdit()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("重新整理", null, (s, e) => RefreshAll()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("<<", null, (s, e) => MoveSelection(0, true)) { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = "第一筆" });
        toolbar.Items.Add(new ToolStripButton("<", null, (s, e) => MoveSelection(-1, false)) { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = "上一筆" });
        toolbar.Items.Add(new ToolStripButton(">", null, (s, e) => MoveSelection(1, false)) { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = "下一筆" });
        toolbar.Items.Add(new ToolStripButton(">>", null, (s, e) => MoveSelection(0, true)) { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = "最後一筆" });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripLabel("搜尋："));
        _txtSearch = new TextBox { Width = 240 };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();
        toolbar.Items.Add(new ToolStripControlHost(_txtSearch));
        toolbar.Items.Add(new ToolStripSeparator());
        _btnToggleList = new ToolStripButton("隱藏清單", null, (s, e) => ToggleList()) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        toolbar.Items.Add(_btnToggleList);
        toolbar.Items.Add(new ToolStripSeparator());
        _lblStatus = new ToolStripLabel("") { ForeColor = UiTheme.TextSub };
        toolbar.Items.Add(_lblStatus);
        Controls.Add(toolbar);
        toolbar.Dock = DockStyle.Top;
    }

    private void BuildStatusBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = UiTheme.BorderLight };
        Controls.Add(bar);
    }

    private void BuildListGrid(Panel host)
    {
        _listDt = CreateListTable();
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = _listDt,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersWidth = 40,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        UiTheme.StyleDataGridView(_grid);
        _grid.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (_pkColumns.Contains(col.Name))
                {
                    UiTheme.StyleHeaderBold(col);
                    col.DefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
                }
                col.SortMode = DataGridViewColumnSortMode.Automatic;
            }
        };
        _grid.SelectionChanged += (s, e) =>
        {
            if (_loading) return;
            if (_grid.SelectedRows.Count == 0) return;
            var row = _grid.SelectedRows[0];
            if (row.DataBoundItem is DataRowView drv)
            {
                var key = KeyOfView(drv);
                SelectByKey(key);
            }
        };
        host.Controls.Add(_grid);
    }

    private DataTable CreateListTable()
    {
        var dt = new DataTable();
        foreach (var name in _listColumns)
            if (_dt.Columns[name] is { } src)
                dt.Columns.Add(name, src.DataType);
        return dt;
    }

    private void ReloadList()
    {
        _loading = true;
        try
        {
            var newDt = CreateListTable();
            foreach (DataRow src in _dt.Rows)
            {
                var nr = newDt.NewRow();
                foreach (var name in _listColumns)
                    nr[name] = src[name];
                newDt.Rows.Add(nr);
            }
            _listDt = newDt;
            _grid.DataSource = _listDt;
        }
        finally
        {
            _loading = false;
        }
    }

    private void BuildFormTabs(Panel host, IReadOnlyList<FormPage> pages)
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        UiTheme.StyleTabControl(tabs);
        foreach (var page in pages)
        {
            var tab = new TabPage(page.Title);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UiTheme.Background };
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 8,
                RowCount = MaxRow(page.Fields) + 1,
                Padding = new Padding(UiTheme.SpacingXl, UiTheme.SpacingMd, UiTheme.SpacingXl, UiTheme.SpacingSm),
            };
            for (int i = 0; i < 8; i++)
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            for (int i = 0; i < panel.RowCount; i++)
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            foreach (var f in page.Fields)
                AddEditor(panel, f);

            scroll.Controls.Add(panel);
            tab.Controls.Add(scroll);
            tabs.TabPages.Add(tab);
        }
        host.Controls.Add(tabs);
    }

    private static int MaxRow(IReadOnlyList<FormField> fields) =>
        fields.Count == 0 ? 1 : fields.Max(f => f.Row) + 1;

    private void AddEditor(TableLayoutPanel panel, FormField f)
    {
        var lbl = new Label { Text = f.Label + "：", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingXs, 0) };
        UiTheme.StyleLabel(lbl);
        var ctrl = MakeEditor(f);
        ctrl.Dock = DockStyle.Fill;
        ctrl.Margin = new Padding(UiTheme.SpacingXs, UiTheme.SpacingSm, UiTheme.SpacingLg, UiTheme.SpacingXs);
        panel.Controls.Add(lbl, f.Col * 2, f.Row);
        panel.Controls.Add(ctrl, f.Col * 2 + 1, f.Row);
        if (f.Span > 1)
            panel.SetColumnSpan(ctrl, f.Span * 2 - 1);
        _editors[f.Field] = ctrl;
        _labels[f.Field] = lbl;
    }

    private Control MakeEditor(FormField f)
    {
        bool isPk = _pkColumns.Contains(f.Field);
        Control ctrl;
        switch (f.Kind)
        {
            case FormFieldKind.Date:
                var dtp = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
                UiTheme.StyleDateTimePicker(dtp);
                ctrl = dtp;
                break;
            case FormFieldKind.Combo:
                var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
                UiTheme.StyleComboBox(cmb);
                var items = new List<string>();
                if (f.Items is not null) items.AddRange(f.Items);
                if (!string.IsNullOrWhiteSpace(f.ComboSql))
                    foreach (DataRow r in DbManager.QueryTable(f.ComboSql).Rows)
                        items.Add(Convert.ToString(r[0]) ?? "");
                cmb.Items.AddRange(items.Distinct().ToArray());
                ctrl = cmb;
                break;
            case FormFieldKind.Memo:
                var memo = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
                UiTheme.StyleTextBox(memo);
                memo.Height = 76;
                ctrl = memo;
                break;
            case FormFieldKind.ReadOnly:
                var ro = new TextBox();
                UiTheme.StyleTextBox(ro, readOnly: true);
                ctrl = ro;
                break;
            default:
                var tb = new TextBox();
                UiTheme.StyleTextBox(tb);
                if (f.Kind == FormFieldKind.Number)
                    tb.TextAlign = HorizontalAlignment.Right;
                ctrl = tb;
                break;
        }
        if (f.Kind == FormFieldKind.ReadOnly || isPk)
            ctrl.Enabled = false;
        return ctrl;
    }

    // ══════════════════════════ 資料同步 ══════════════════════════

    private void SelectByKey(string key)
    {
        DataRow? target = null;
        foreach (DataRow row in _dt.Rows)
        {
            if (KeyOf(row) == key) { target = row; break; }
        }
        if (target is null) return;
        _currentRow = target;
        LoadRowToForm(target);
        UpdateStatus();
    }

    private string KeyOfView(DataRowView drv)
    {
        var parts = _pkColumns.Select(c => Convert.ToString(drv[c])?.Trim() ?? "");
        return string.Join("|", parts);
    }

    private void LoadRowToForm(DataRow row)
    {
        _loading = true;
        try
        {
            foreach (var (field, ctrl) in _editors)
            {
                switch (ctrl)
                {
                    case DateTimePicker dtp:
                        var raw = Str(row[field]);
                        if (DateTime.TryParse(raw, out var val))
                        {
                            dtp.Checked = true;
                            dtp.Value = val;
                        }
                        else
                        {
                            dtp.Checked = false;
                            dtp.Value = DateTime.Now;
                        }
                        break;
                    case TextBox tb:
                        tb.Text = Str(row[field]);
                        break;
                    case ComboBox cmb:
                        cmb.Text = Str(row[field]);
                        break;
                }
            }
            foreach (var pk in _pkColumns)
                if (_editors.TryGetValue(pk, out var pkCtrl))
                    pkCtrl.Enabled = false;
        }
        finally
        {
            _loading = false;
        }
    }

    private void StartNew()
    {
        _currentRow = null;
        _loading = true;
        try
        {
            foreach (var (field, ctrl) in _editors)
            {
                switch (ctrl)
                {
                    case DateTimePicker dtp:
                        dtp.Checked = false;
                        dtp.Value = DateTime.Now;
                        break;
                    case TextBox tb:
                        tb.Text = "";
                        break;
                    case ComboBox cmb:
                        cmb.Text = "";
                        break;
                }
            }
            foreach (var pk in _pkColumns)
                if (_editors.TryGetValue(pk, out var pkCtrl))
                    pkCtrl.Enabled = true;
        }
        finally
        {
            _loading = false;
        }
        UpdateStatus();
    }

    private void CollectFormToRow(DataRow row)
    {
        foreach (var (field, ctrl) in _editors)
        {
            switch (ctrl)
            {
                case DateTimePicker dtp:
                    row[field] = dtp.Checked ? dtp.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value;
                    break;
                case ComboBox cmb:
                    row[field] = string.IsNullOrWhiteSpace(cmb.Text) ? DBNull.Value : (object)cmb.Text.Trim();
                    break;
                case TextBox tb:
                    row[field] = string.IsNullOrWhiteSpace(tb.Text) ? DBNull.Value : (object)tb.Text.Trim();
                    break;
            }
        }
    }

    // ══════════════════════════ 資料操作 ══════════════════════════

    private void SaveCurrent()
    {
        try
        {
            if (_currentRow is null)
            {
                var key = string.Join("|", _pkColumns.Select(c => _editors.TryGetValue(c, out var e) ? e.Text.Trim() : ""));
                if (string.IsNullOrEmpty(key))
                {
                    MessageBox.Show("請輸入主鍵欄位後再儲存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (_existingKeys.Contains(key))
                {
                    MessageBox.Show($"主鍵「{key}」已存在，請改用其他主鍵。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var row = _dt.NewRow();
                CollectFormToRow(row);
                var cols = _columns;
                var values = cols.Select(c => row[c] == DBNull.Value ? null : row[c]).ToList();
                DbManager.ExecuteNonQuery(
                    $"INSERT INTO \"{_tableName}\" ({JoinNames(cols)}) VALUES ({JoinParams(cols)})",
                    BuildParams(cols, values));
                _existingKeys.Add(key);
            }
            else
            {
                CollectFormToRow(_currentRow);
                var key = KeyOf(_currentRow);
                var sets = _columns.Where(c => !_pkColumns.Contains(c)).ToList();
                DbManager.ExecuteNonQuery(
                    $"UPDATE \"{_tableName}\" SET {JoinSets(sets)} WHERE {WhereClause(_pkColumns)}",
                    BuildParams(sets, sets.Select(c => _currentRow[c] == DBNull.Value ? null : _currentRow[c]).ToList())
                        .Concat(WhereParams(_pkColumns, key)).ToArray());
            }
            ReloadData();
            ReloadList();
            if (_currentRow is not null)
                SelectByKey(KeyOf(_currentRow));
            else
                UpdateStatus();
            MessageBox.Show("儲存完成。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteCurrent()
    {
        if (_currentRow is null)
        {
            MessageBox.Show("請先選取一筆資料再刪除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var key = KeyOf(_currentRow);
        if (MessageBox.Show($"確定要刪除「{key}」嗎？此動作無法復原。", "刪除確認",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try
        {
            DbManager.ExecuteNonQuery(
                $"DELETE FROM \"{_tableName}\" WHERE {WhereClause(_pkColumns)}",
                WhereParams(_pkColumns, key));
            _existingKeys.Remove(key);
            _currentRow = null;
            ReloadData();
            ReloadList();
            UpdateStatus();
            MessageBox.Show("已刪除。", "刪除", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刪除失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshAll()
    {
        ReloadData();
        ReloadList();
        _currentRow = null;
        UpdateStatus();
    }

    /// <summary>原系統風導覽：extreme 為 true 時 offset 0=第一筆、非 0=最後一筆；否則為上/下一筆。</summary>
    private void MoveSelection(int offset, bool extreme)
    {
        if (_grid.SelectedRows.Count == 0) return;
        int idx = _grid.SelectedRows[0].Index;
        int target = extreme
            ? (offset == 0 ? 0 : _grid.Rows.Count - 1)
            : Math.Clamp(idx + offset, 0, _grid.Rows.Count - 1);
        if (target < 0 || target >= _grid.Rows.Count || target == idx) return;
        _grid.ClearSelection();
        _grid.Rows[target].Selected = true;
        _grid.CurrentCell = _grid.Rows[target].Cells[0];
        _grid.FirstDisplayedScrollingRowIndex = target;
    }

    /// <summary>以目前欄位內容為底開啟新一筆（主鍵清空、可輸入）。</summary>
    private void DuplicateCurrent()
    {
        if (_currentRow is null)
        {
            MessageBox.Show("目前為新增模式，無可複製的資料。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _currentRow = null;
        _loading = true;
        try
        {
            foreach (var pk in _pkColumns)
            {
                if (!_editors.TryGetValue(pk, out var pkCtrl)) continue;
                pkCtrl.Enabled = true;
                switch (pkCtrl)
                {
                    case TextBox tb: tb.Text = ""; break;
                    case ComboBox cmb: cmb.Text = ""; break;
                }
            }
        }
        finally
        {
            _loading = false;
        }
        UpdateStatus();
        var first = _pkColumns.Select(c => _editors.TryGetValue(c, out var e) ? e : null)
            .FirstOrDefault(e => e is TextBox or ComboBox);
        first?.Focus();
    }

    /// <summary>取消未存變更：修改模式重新載入，新增模式清空。</summary>
    private void CancelEdit()
    {
        if (_currentRow is null)
        {
            StartNew();
            return;
        }
        SelectByKey(KeyOf(_currentRow));
    }

    /// <summary>視窗顯示後才設定左清單寬度，避免建構時視窗尚未有寬度而溢位。</summary>
    private void SetSplitterWidth()
    {
        if (_splitterSet) return;
        if (_split.Width <= 0) return;
        _split.Panel1MinSize = 280;
        _split.Panel2MinSize = 520;
        int max = Math.Max(0, _split.Width - _split.Panel2MinSize);
        if (max < _split.Panel1MinSize) { _splitterSet = true; return; }
        _split.SplitterDistance = Math.Min(340, max);
        _splitterSet = true;
    }

    /// <summary>收合／展開左側清單。</summary>
    private void ToggleList()
    {
        _split.Panel1Collapsed = !_split.Panel1Collapsed;
        _btnToggleList.Text = _split.Panel1Collapsed ? "顯示清單" : "隱藏清單";
    }

    private void ApplyFilter()
    {
        var kw = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(kw))
        {
            _listDt.DefaultView.RowFilter = "";
        }
        else
        {
            var esc = kw.Replace("'", "''").Replace("*", "%").Replace("?", "_");
            var likes = _listColumns.Select(c => $"CONVERT([{c}],'System.String') LIKE '%{esc}%'");
            _listDt.DefaultView.RowFilter = string.Join(" OR ", likes);
        }
    }

    private void UpdateStatus()
    {
        _lblStatus.Text = _currentRow is null
            ? $"新增模式　（共 {_dt.Rows.Count} 筆）"
            : $"修改：{KeyOf(_currentRow)}　（共 {_dt.Rows.Count} 筆）";
    }

    // ══════════════════════════ SQL 輔助 ══════════════════════════

    private List<string> _columns => _dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

    private static string JoinNames(IEnumerable<string> cols) =>
        string.Join(",", cols.Select(c => $"\"{c}\""));

    private static string JoinParams(IEnumerable<string> cols) =>
        string.Join(",", cols.Select(c => $"${c}"));

    private static string JoinSets(IEnumerable<string> cols) =>
        string.Join(",", cols.Select(c => $"\"{c}\" = ${c}"));

    private static string WhereClause(IEnumerable<string> cols) =>
        string.Join(" AND ", cols.Select(c => $"\"{c}\" = $p_{c}"));

    private static SqliteParameter[] WhereParams(IEnumerable<string> cols, string key)
    {
        var parts = key.Split('|');
        return cols.Select((c, i) => DbManager.Param($"$p_{c}", parts[i])).ToArray();
    }

    private static SqliteParameter[] BuildParams(List<string> cols, List<object?> values)
    {
        var list = new List<Microsoft.Data.Sqlite.SqliteParameter>();
        for (int i = 0; i < cols.Count; i++)
            list.Add(DbManager.Param($"${cols[i]}", values[i]));
        return list.ToArray();
    }

    private static string Str(object? v) => v is DBNull or null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture)!;

    private sealed class CursorScope : IDisposable
    {
        private readonly Cursor? _previous;
        public CursorScope(Cursor cursor) { _previous = Cursor.Current; Cursor.Current = cursor; }
        public void Dispose()
        {
            if (_previous is not null) Cursor.Current = _previous;
        }
    }
}
