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
/// 貨品主檔維護視窗：動態依資料表結構產生欄位，提供搜尋/新增/修改/刪除/儲存。
/// 主鍵為「貨品編號」，儲存時自動判斷新增或更新。
/// </summary>
public class ProductMaintenanceForm : Form
{
    private const string TableName = "貨品主檔";
    private readonly DataTable _dt;
    private readonly DataGridView _grid;
    private readonly TextBox _txtSearch;
    private readonly ToolStripLabel _lblCount;
    private readonly List<string> _columns;
    private readonly List<string> _pkColumns;
    private readonly HashSet<string> _existingKeys = new();

    public ProductMaintenanceForm()
    {
        var table = SchemaReader.GetTable(TableName)
            ?? throw new InvalidOperationException("找不到貨品主檔資料表");
        _columns = table.Columns.Select(c => c.Name).ToList();
        _pkColumns = table.PrimaryKey;

        Text = "貨品主檔維護";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Font = new Font("Microsoft JhengHei UI", 11F);
        UiTheme.Apply(this);

        // 載入資料（大型表提示）
        using (new CursorScope(Cursors.WaitCursor))
        {
            _dt = DbManager.QueryTable($"SELECT * FROM \"{TableName}\"");
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
        toolbar.Items.Add(new ToolStripButton("新增", null, (s, e) => AddRow()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("刪除", null, (s, e) => DeleteRows()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("儲存", null, (s, e) => SaveChanges()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripLabel("搜尋："));
        _txtSearch = new TextBox { Width = 280 };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();
        toolbar.Items.Add(new ToolStripControlHost(_txtSearch));
        toolbar.Items.Add(new ToolStripSeparator());
        _lblCount = new ToolStripLabel("共 0 筆") { ForeColor = UiTheme.TextSub };
        toolbar.Items.Add(_lblCount);
        toolbar.Items.Add(new ToolStripLabel("　（主鍵：貨品編號）") { ForeColor = UiTheme.TextSub });

        // 資料表格
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = _dt,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            RowHeadersWidth = 48,
            ReadOnly = false,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
        };
        UiTheme.StyleDataGridView(_grid);
        _grid.DataError += (s, e) => { e.ThrowException = false; };

        Controls.Add(UiTheme.BuildHeader("貨品主檔維護", "貨品資料的新增／修改／刪除／儲存"));
        Controls.Add(_grid);
        Controls.Add(toolbar);
        toolbar.Dock = DockStyle.Top;
        _grid.BringToFront();

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

        ShortcutHelper.Enable(this, onDelete: DeleteRows, onSearch: () => _txtSearch.Focus());
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
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

    private void AddRow()
    {
        var row = _dt.NewRow();
        _dt.Rows.Add(row);
        _grid.CurrentCell = _grid.Rows[_dt.Rows.Count - 1].Cells[0];
        _grid.BeginEdit(true);
    }

    private void DeleteRows()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            MessageBox.Show("請先選取要刪除的列。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var n = _grid.SelectedRows.Count;
        if (MessageBox.Show($"確定要刪除選取的 {n} 筆貨品嗎？", "刪除確認",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
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
                            $"DELETE FROM \"{TableName}\" WHERE {WhereClause(_pkColumns)}",
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
                            errors.Add($"貨品編號「{key}」已存在，已略過新增。");
                            duplicateRows.Add(row);   // 延後 RejectChanges，避免迭代中修改集合
                            continue;
                        }
                        var cols = string.Join(",", _columns.Select(c => $"\"{c}\""));
                        var pars = string.Join(",", _columns.Select(c => $"${c}"));
                        DbManager.CreateCommand(tx,
                            $"INSERT INTO \"{TableName}\" ({cols}) VALUES ({pars})",
                            BuildParams(_columns, values)).ExecuteNonQuery();
                        _existingKeys.Add(key);
                    }
                    else
                    {
                        var sets = string.Join(",", _columns.Where(c => !_pkColumns.Contains(c))
                            .Select(c => $"\"{c}\" = ${c}"));
                        var cmd = DbManager.CreateCommand(tx,
                            $"UPDATE \"{TableName}\" SET {sets} WHERE {WhereClause(_pkColumns)}",
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
                MessageBox.Show(string.Join("\n", errors), "部分資料未儲存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show("儲存完成。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

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
            var esc = kw.Replace("'", "''").Replace("*", "%").Replace("?", "_");
            var likes = new List<string> { $"CONVERT([貨品編號],'System.String') LIKE '%{esc}%'" };
            if (_columns.Contains("品名"))
                likes.Add($"CONVERT([品名],'System.String') LIKE '%{esc}%'");
            if (_columns.Contains("規格"))
                likes.Add($"CONVERT([規格],'System.String') LIKE '%{esc}%'");
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
