// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（改為查詢式版面：全螢幕清單＋彈出編輯框）
// ════════════════════════════════════════════════════════
using System.Data;
using System.Globalization;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 貨品主檔維護：查詢式版面（全螢幕清單＋彈出式編輯框）。
/// 新增／修改／刪除透過 <see cref="GenericEditorDialog"/> 編輯，儲存自動判斷新增或更新。
/// </summary>
public sealed class ProductMaintenanceForm : Form
{
    private const string TableName = "貨品主檔";
    private readonly DataTable _dt;
    private readonly DataGridView _grid = new();
    private readonly TextBox _txtKeyword = new();
    private readonly ToolStripStatusLabel _lblCount = new();
    private readonly List<string> _columns;
    private readonly List<string> _pkColumns;
    private readonly HashSet<string> _existingKeys = new();

    /// <summary>清單顯示欄位（依資料表實際欄位過濾）。</summary>
    private static readonly string[] ListColumns =
    {
        "貨品編號", "品名", "規格", "基本單位", "標準售價", "牌價",
        "售價A", "售價B", "售價C", "現行成本", "安全存量", "倉庫編號",
        "儲放位置", "備註",
    };

    public ProductMaintenanceForm()
    {
        var table = SchemaReader.GetTable(TableName)
            ?? throw new InvalidOperationException("找不到貨品主檔資料表");
        _columns = table.Columns.Select(c => c.Name).ToList();
        _pkColumns = table.PrimaryKey;

        Text = "貨品主檔維護";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 660);
        UiTheme.Apply(this);

        Controls.Add(UiTheme.BuildHeader("貨品主檔維護", "貨品資料的新增／修改／刪除／儲存"));

        // 載入資料（大型表提示）
        using (new CursorScope(Cursors.WaitCursor))
        {
            _dt = DbManager.QueryTable($"SELECT * FROM \"{TableName}\"");
        }
        _dt.AcceptChanges();
        foreach (DataRow row in _dt.Rows)
            _existingKeys.Add(KeyOf(row));
        // 放寬主鍵唯一約束：唯一性改由儲存時 _existingKeys 檢查。
        _dt.PrimaryKey = Array.Empty<DataColumn>();
        foreach (var name in _pkColumns)
            if (_dt.Columns[name] is { } col)
                col.Unique = false;

        BuildToolbar();
        BuildFilterBar();
        BuildGrid();
        BuildStatusBar();

        ShortcutHelper.Enable(this,
            () => EditRow(null),
            () => EditRow(GetSelectedRow()),
            DeleteSelected,
            () => _txtKeyword.Focus(),
            ApplyFilter);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    // ==================== UI ====================

    private void BuildToolbar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 52 };
        UiTheme.StyleTopBar(bar);
        int x = UiTheme.SpacingMd;
        void Add(ModernButton b) { b.Location = new Point(x, 6); b.Height = 40; b.DrawShadow = false; bar.Controls.Add(b); x += b.Width + UiTheme.SpacingSm; }
        void Sep() { bar.Controls.Add(new Panel { Location = new Point(x, 10), Size = new Size(2, 32), BackColor = UiTheme.Border }); x += UiTheme.SpacingSm + 2; }

        var btnSearch = new ModernButton { Text = "搜尋", Width = 110 };
        btnSearch.Click += (s, e) => { ApplyFilter(); _txtKeyword.Focus(); };
        var btnNew = new ModernButton { Text = "新增貨品", Width = 130 };
        btnNew.Click += (s, e) => EditRow(null);
        var btnEdit = new ModernButton { Text = "修改", Width = 100, IsPrimary = false };
        btnEdit.Click += (s, e) => EditRow(GetSelectedRow());
        var btnDel = new ModernButton { Text = "刪除", Width = 100, IsPrimary = false };
        btnDel.Click += (s, e) => DeleteSelected();
        var btnHelp = new ModernButton { Text = "說明", Width = 100, IsPrimary = false };
        btnHelp.Click += (s, e) =>
            MessageBox.Show(
                "貨品主檔維護功能說明：\n" +
                "1. 全螢幕清單顯示貨品資料，可依貨品編號／品名／規格搜尋。\n" +
                "2. 新增或修改以彈出式編輯框輸入，下拉欄位（類別、單位、倉庫、科目…）自動帶入。\n" +
                "3. 新增時提供「新增並繼續」連續建檔；主鍵「貨品編號」不可重複。\n" +
                "4. 刪除為整列刪除；修改或刪除後立即寫入資料庫。",
                "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var btnExit = new ModernButton { Text = "離開", Width = 100, IsPrimary = false };
        btnExit.Click += (s, e) => Close();

        Add(btnSearch); Add(btnNew); Add(btnEdit); Add(btnDel);
        Sep();
        Add(btnHelp); Add(btnExit);
        Controls.Add(bar);
    }

    private void BuildFilterBar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.Background, Padding = new Padding(UiTheme.SpacingMd, 10, UiTheme.SpacingMd, 8) };
        _txtKeyword.PlaceholderText = "貨品編號 / 品名 / 規格";
        _txtKeyword.Location = new Point(UiTheme.SpacingMd, 12);
        _txtKeyword.Width = 280;
        _txtKeyword.TextChanged += (s, e) => ApplyFilter();
        bar.Controls.Add(_txtKeyword);
        bar.Controls.Add(new Label
        {
            Text = "（輸入即時篩選；留空 = 全部）",
            Font = UiTheme.Font(9F),
            ForeColor = UiTheme.TextFaint,
            AutoSize = true,
            Location = new Point(UiTheme.SpacingMd + 292, 18),
        });
        Controls.Add(bar);
    }

    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        UiTheme.StyleDataGridView(_grid);
        _grid.DataSource = _dt;
        _grid.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (!ListColumns.Contains(col.Name))
                {
                    col.Visible = false;
                    continue;
                }
                col.SortMode = DataGridViewColumnSortMode.Automatic;
                if (_pkColumns.Contains(col.Name))
                {
                    UiTheme.StyleHeaderBold(col);
                    col.DefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
                }
                if (col.Name is "標準售價" or "牌價" or "售價A" or "售價B" or "售價C" or "現行成本" or "安全存量")
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        };
        _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditRow(GetSelectedRow()); };
        Controls.Add(_grid);
    }

    private void BuildStatusBar()
    {
        var bar = new StatusStrip { SizingGrip = false, BackColor = UiTheme.Card, Padding = new Padding(12, 2, 8, 2) };
        bar.Items.Add(_lblCount);
        Controls.Add(bar);
        UpdateCount();
    }

    // ==================== 資料 ====================

    private DataRow? GetSelectedRow()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.DataBoundItem is not DataRowView drv)
            return null;
        return drv.Row;
    }

    private void EditRow(DataRow? row)
    {
        if (row is null)
        {
            // 新增
            var n = _dt.NewRow();
            var (ok, cont) = GenericEditorDialog.ShowDialog(this, TableName, n);
            if (ok)
            {
                _dt.Rows.Add(n);
                SaveChanges();
            }
            if (cont)
                EditRow(null);
            return;
        }
        if (GenericEditorDialog.ShowDialog(this, TableName, row).Ok)
            SaveChanges();
    }

    private void DeleteSelected()
    {
        var row = GetSelectedRow();
        if (row is null)
        {
            MessageBox.Show("請先於清單選取一筆貨品。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string 編號 = Convert.ToString(row["貨品編號"]) ?? "";
        if (MessageBox.Show($"確定刪除貨品「{編號}」？", "刪除確認",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        row.Delete();
        SaveChanges();
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
                            duplicateRows.Add(row);
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
            ApplyFilter();
            if (errors.Count > 0)
                MessageBox.Show(string.Join("\n", errors), "部分資料未儲存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"儲存失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        var kw = _txtKeyword.Text.Trim();
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
        if (_lblCount.Text is null) return;
        _lblCount.Text = $"共 {_dt.DefaultView.Count} 筆（總共 {_dt.Rows.Count} 筆）";
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
