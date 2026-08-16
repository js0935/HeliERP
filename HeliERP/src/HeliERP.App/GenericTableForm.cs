// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Globalization;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 泛型資料維護視窗：依資料表結構動態產生欄位，提供搜尋/新增/刪除/儲存。
/// Editable 模式可編輯（基本資料、系統設定）；ReadOnly 模式僅供檢視（交易資料、彙總報表）。
/// 無主鍵的資料表一律強制唯讀，避免刪除/更新時無 WHERE 條件造成全表異動。
/// </summary>
public class GenericTableForm : Form
{
    private readonly string _tableName;
    private readonly TableMode _mode;
    private readonly bool _canEdit;
    private readonly DataTable _dt;
    private readonly DataGridView _grid;
    private readonly TextBox _txtSearch;
    private readonly ToolStripLabel _lblCount;
    private readonly List<string> _columns;
    private readonly List<string> _pkColumns;
    private readonly HashSet<string> _existingKeys = new();

    public GenericTableForm(string tableName, TableMode? mode = null, string? initialFilter = null)
    {
        _tableName = tableName;
        _mode = mode ?? TableCatalog.GetMode(tableName);
        _canEdit = _mode == TableMode.Editable;

        var table = SchemaReader.GetTable(tableName)
            ?? throw new InvalidOperationException($"找不到資料表「{tableName}」");
        _columns = table.Columns.Select(c => c.Name).ToList();
        var declaredPk = table.PrimaryKey;
        _pkColumns = declaredPk.Count > 0 ? declaredPk : TableCatalog.GetKeyFields(tableName).ToList();
        if (!_canEdit || _pkColumns.Count == 0)
            _canEdit = false;   // 唯讀或無主鍵 → 強制唯讀（欄位已鎖定）

        Text = _canEdit ? $"{tableName} - 資料維護" : $"{tableName} - 資料檢視〔唯讀〕";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Font = new Font("Microsoft JhengHei UI", 11F);
        UiTheme.Apply(this);

        // 載入資料（大型表提示）
        using (new CursorScope(Cursors.WaitCursor))
        {
            _dt = DbManager.QueryTable($"SELECT * FROM \"{tableName}\"");
        }
        _dt.AcceptChanges();
        foreach (DataRow row in _dt.Rows)
            _existingKeys.Add(KeyOf(row));
        // 編輯期放寬主鍵唯一約束：DataTable 依資料庫主鍵建立 PrimaryKey/UniqueConstraint，
        // 會阻擋「多列未填主鍵」與「編輯中重複主鍵」造成 Rows.Add 拋例外。
        // 唯一性改由儲存時 _existingKeys 檢查（見 SaveChanges）。
        _dt.PrimaryKey = Array.Empty<DataColumn>();
        foreach (var name in _pkColumns)
            if (_dt.Columns[name] is { } col)
                col.Unique = false;

        // 工具列
        var toolbar = new ToolStrip();
        UiTheme.StyleToolStrip(toolbar);
        if (_canEdit)
        {
            toolbar.Items.Add(new ToolStripButton("新增", null, (s, e) => AddRowDialog()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
            toolbar.Items.Add(new ToolStripButton("編輯", null, (s, e) => EditRow()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
            toolbar.Items.Add(new ToolStripButton("刪除", null, (s, e) => DeleteRows()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
            toolbar.Items.Add(new ToolStripButton("儲存", null, (s, e) => SaveChanges()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
            toolbar.Items.Add(new ToolStripSeparator());
        }
        toolbar.Items.Add(new ToolStripButton("匯出", null, (s, e) => ExportTable()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        _txtSearch = new TextBox { Width = 280 };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();
        toolbar.Items.Add(new ToolStripControlHost(_txtSearch));
        toolbar.Items.Add(new ToolStripSeparator());
        _lblCount = new ToolStripLabel("共 0 筆") { ForeColor = UiTheme.TextSub };
        toolbar.Items.Add(_lblCount);
        if (_canEdit && _pkColumns.Count > 0)
            toolbar.Items.Add(new ToolStripLabel($"　（主鍵：{string.Join(" + ", _pkColumns)}）") { ForeColor = UiTheme.TextSub });
        else
            toolbar.Items.Add(new ToolStripLabel("　（唯讀檢視，不開放編輯）") { ForeColor = UiTheme.TextSub });

        // 資料表格
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = _dt,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            AllowUserToAddRows = _canEdit,
            AllowUserToDeleteRows = _canEdit,
            ReadOnly = !_canEdit,
            RowHeadersWidth = 48,
            EditMode = _canEdit ? DataGridViewEditMode.EditOnKeystrokeOrF2 : DataGridViewEditMode.EditProgrammatically,
        };
        UiTheme.StyleDataGridView(_grid);
        _grid.DataError += (s, e) => { e.ThrowException = false; };

        Controls.Add(UiTheme.BuildHeader(_tableName, _canEdit ? "可編輯：可新增／編輯／刪除／儲存" : "唯讀檢視，不開放編輯"));
        Controls.Add(_grid);
        Controls.Add(toolbar);
        toolbar.Dock = DockStyle.Top;
        _grid.BringToFront();

        // 雙擊列 → 開啟編輯對話框
        _grid.CellDoubleClick += (s, e) =>
        {
            if (_canEdit && e.RowIndex >= 0)
                EditRow();
        };

        // 主鍵欄位以粗體標示；於 DataBindingComplete 設定——
        // DataGridView 加入表單時因 BindingContext 變更會重新產生欄位，
        // 若在構造函式直接設定，樣式會被重新產生的欄位覆蓋。
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

        UpdateCount();

        if (!string.IsNullOrEmpty(initialFilter))
            _txtSearch.Text = initialFilter;

        ShortcutHelper.Enable(this, onDelete: _canEdit ? DeleteRows : null, onSearch: () => _txtSearch.Focus(), onReload: ApplyFilter);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    /// <summary>匯出目前資料表內容為 CSV / Excel（含唯讀檢視）</summary>
    private void ExportTable()
    {
        if (_dt.Rows.Count == 0)
        {
            ShowWarning("目前沒有資料可匯出。", "提示");
            return;
        }
        ExportService.ExportAny(this, _dt, _tableName, $"匯出「{_tableName}」");
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

    private void AddRowDialog()
    {
        var row = _dt.NewRow();
        var (ok, cont) = GenericEditorDialog.ShowDialog(this, _tableName, row);
        if (ok)
        {
            _dt.Rows.Add(row);
            _grid.CurrentCell = _grid.Rows[_dt.Rows.Count - 1].Cells[0];
            UpdateCount();
            if (cont)
                AddRowDialog();
        }
    }

    private void EditRow()
    {
        if (_grid.CurrentRow is not { } gridRow || gridRow.IsNewRow)
        {
            ShowWarning("請先選取要編輯的列。", "提示");
            return;
        }
        if (gridRow.DataBoundItem is not DataRowView drv)
            return;
        if (GenericEditorDialog.ShowDialog(this, _tableName, drv.Row).Ok)
            UpdateCount();
    }

    private void DeleteRows()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            ShowWarning("請先選取要刪除的列。", "提示");
            return;
        }
        var n = _grid.SelectedRows.Count;
        if (Confirm($"確定要刪除選取的 {n} 筆資料嗎？", "刪除確認") != DialogResult.Yes)
            return;
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            if (!row.IsNewRow)
            {
                // 用 Delete() 標記而非 Rows.Remove()：被刪除列需留在集合中，
                // SaveChanges 才能迭代到並執行 DELETE（Remove 會直接移出集合）。
                if (row.DataBoundItem is DataRowView drv)
                    drv.Row.Delete();
                else if (row.DataBoundItem is DataRow dr)
                    dr.Delete();
            }
        }
        ApplyFilter();
        UpdateCount();
    }

    private void SaveChanges()
    {
        try
        {
            var errors = new List<string>();
            var duplicateRows = new List<DataRow>();
            DbManager.ExecuteTransaction(tx =>
            {
                foreach (DataRow row in _dt.Rows)
                {
                    var state = row.RowState;
                    if (state == DataRowState.Deleted)
                    {
                        // 已刪除列（DataGridView 刪除時 DataTable 標記 Deleted）
                        var origKey = KeyOfDeleted(row);
                        if (string.IsNullOrEmpty(origKey)) continue;
                        DbManager.CreateCommand(tx,
                            $"DELETE FROM \"{_tableName}\" WHERE {WhereClause(_pkColumns)}",
                            WhereParams(_pkColumns, origKey)).ExecuteNonQuery();
                        _existingKeys.Remove(origKey);
                        continue;
                    }
                    if (state is not (DataRowState.Added or DataRowState.Modified))
                        continue;

                    var key = KeyOf(row);
                    var values = new List<object?>();
                    foreach (var col in _columns)
                        values.Add(row[col] == DBNull.Value ? null : row[col]);

                    if (state == DataRowState.Added)
                    {
                        if (_existingKeys.Contains(key))
                        {
                            errors.Add($"主鍵「{key}」已存在，已略過新增。");
                            duplicateRows.Add(row);   // 延後 RejectChanges，避免迭代中修改集合
                            continue;
                        }
                        var cols = string.Join(",", _columns.Select(c => $"\"{c}\""));
                        var pars = string.Join(",", _columns.Select(c => $"${c}"));
                        DbManager.CreateCommand(tx,
                            $"INSERT INTO \"{_tableName}\" ({cols}) VALUES ({pars})",
                            BuildParams(_columns, values)).ExecuteNonQuery();
                        _existingKeys.Add(key);
                    }
                    else
                    {
                        var sets = string.Join(",", _columns.Where(c => !_pkColumns.Contains(c))
                            .Select(c => $"\"{c}\" = ${c}"));
                        var cmd = DbManager.CreateCommand(tx,
                            $"UPDATE \"{_tableName}\" SET {sets} WHERE {WhereClause(_pkColumns)}",
                            BuildParams(_columns, values).Concat(WhereParams(_pkColumns, key)).ToArray());
                        cmd.ExecuteNonQuery();
                    }
                }
            });
            foreach (var dup in duplicateRows)
                dup.RejectChanges();
            _dt.AcceptChanges();
            UpdateCount();
            if (errors.Count > 0)
                ShowWarning(string.Join("\n", errors), "部分資料未儲存");
            else
                ShowInfo("儲存完成。", "成功");
        }
        catch (Exception ex)
        {
            ShowError($"儲存失敗：{ex.Message}", "錯誤");
        }
    }

    protected virtual DialogResult Confirm(string message, string caption) =>
        MessageBox.Show(message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

    protected virtual void ShowInfo(string message, string caption) =>
        MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

    protected virtual void ShowWarning(string message, string caption) =>
        MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected virtual void ShowError(string message, string caption) =>
        MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private string KeyOfDeleted(DataRow row)
    {
        var parts = new List<string>();
        foreach (var col in _pkColumns)
        {
            var v = row[col, DataRowVersion.Original];
            parts.Add(v is DBNull or null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture)!.Trim());
        }
        return string.Join("|", parts);
    }

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

    private void ApplyFilter()
    {
        var kw = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(kw))
        {
            _dt.DefaultView.RowFilter = "";
        }
        else
        {
            // 對所有字串欄位做模糊搜尋
            var esc = kw.Replace("'", "''").Replace("*", "%").Replace("?", "_");
            var likes = _dt.Columns.Cast<DataColumn>()
                .Where(c => c.DataType == typeof(string))
                .Select(c => $"CONVERT([{c.ColumnName}],'System.String') LIKE '%{esc}%'");
            _dt.DefaultView.RowFilter = string.Join(" OR ", likes);
        }
        UpdateCount();
    }

    private void UpdateCount()
    {
        _lblCount.Text = $"共 {_dt.DefaultView.Count} 筆（總共 {_dt.Rows.Count} 筆）";
    }

    /// <summary>載入/儲存期間的等待游標助手</summary>
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
