// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（全域快速搜尋）
// ════════════════════════════════════════════════════════
using System.Data;

namespace HeliERP.App;

/// <summary>
/// 全域快速搜尋視窗（Ctrl+K）：輸入關鍵字即時跨表檢索，
/// 方向鍵選取、Enter／雙擊開啟對應資料檢視。
/// </summary>
public sealed class GlobalSearchForm : Form
{
    private readonly TextBox _txtSearch;
    private readonly DataGridView _grid;
    private readonly Label _lblStatus;
    private readonly System.Windows.Forms.Timer _debounce;
    private List<GlobalSearchService.SearchHit> _hits = new();

    public GlobalSearchForm()
    {
        Text = "全域快速搜尋";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(680, 460);
        MinimumSize = new Size(680, 400);
        BackColor = UiTheme.Background;
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };

        var header = UiTheme.BuildHeader("全域快速搜尋", "輸入客戶／廠商、貨品、員工或單據號碼，Enter 開啟（Esc 關閉）", 58);
        header.Dock = DockStyle.Top;
        Controls.Add(header);

        var searchBar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = UiTheme.Card, Padding = new Padding(UiTheme.SpacingLg) };
        _txtSearch = new TextBox { Location = new Point(UiTheme.SpacingLg, 12), Width = 520, Font = UiTheme.Font(13F) };
        UiTheme.StyleTextBox(_txtSearch);
        var btnClear = new ModernButton
        {
            Text = "清除",
            Width = 70,
            Height = 34,
            Location = new Point(548, 14),
            IsPrimary = false,
            DrawShadow = false,
        };
        btnClear.Click += (s, e) => { _txtSearch.Clear(); _txtSearch.Focus(); };
        searchBar.Controls.Add(_txtSearch);
        searchBar.Controls.Add(btnClear);
        Controls.Add(searchBar);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        };
        UiTheme.StyleDataGridView(_grid);
        _grid.ColumnHeadersHeight = 34;
        var colCat = new DataGridViewTextBoxColumn { HeaderText = "類別", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true };
        var colText = new DataGridViewTextBoxColumn { HeaderText = "結果", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true };
        _grid.Columns.AddRange(colCat, colText);
        _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenSelected(); };
        _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; e.SuppressKeyPress = true; } };
        _grid.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var cat = e.RowIndex < _hits.Count ? _hits[e.RowIndex].類別 : "";
            e.CellStyle!.BackColor = cat switch
            {
                "客戶/廠商" => Color.FromArgb(240, 245, 252),
                "貨品" => Color.FromArgb(244, 250, 244),
                "員工" => Color.FromArgb(252, 248, 240),
                "交易單據" => Color.FromArgb(248, 243, 251),
                _ => Color.FromArgb(251, 244, 244),
            };
        };
        Controls.Add(_grid);

        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            Font = UiTheme.Font(9.5F),
            ForeColor = UiTheme.TextSub,
            Text = "輸入關鍵字開始搜尋…",
            Padding = new Padding(UiTheme.SpacingMd, 5, 0, 0),
            BackColor = UiTheme.Card,
        };
        Controls.Add(_lblStatus);

        _debounce = new System.Windows.Forms.Timer { Interval = 250 };
        _debounce.Tick += (s, e) => { _debounce.Stop(); RunSearch(); };
        _txtSearch.TextChanged += (s, e) => { _debounce.Stop(); _debounce.Start(); };
        _txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                _debounce.Stop();
                RunSearch();
                if (_grid.Rows.Count > 0) OpenSelected();
            }
            else if (e.KeyCode == Keys.Down && _grid.Rows.Count > 0)
            {
                _grid.ClearSelection();
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
                _grid.Focus();
                e.Handled = true;
            }
        };

        Shown += (s, e) => _txtSearch.Focus();
    }

    private void RunSearch()
    {
        var kw = _txtSearch.Text.Trim();
        if (kw.Length == 0)
        {
            _hits = new();
            _grid.DataSource = null;
            _lblStatus.Text = "輸入關鍵字開始搜尋…";
            return;
        }
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            _hits = GlobalSearchService.Search(kw);
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }
        var dt = new DataTable();
        dt.Columns.Add("類別");
        dt.Columns.Add("結果");
        foreach (var hit in _hits)
            dt.Rows.Add(hit.類別, hit.顯示);
        _grid.DataSource = dt;
        if (_grid.Columns.Count > 0)
        {
            _grid.Columns["類別"]!.Width = 110;
            _grid.Columns["結果"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }
        _lblStatus.Text = _hits.Count == 0
            ? "沒有符合的資料。"
            : $"找到 {_hits.Count} 筆（Enter 開啟選取項目）";
    }

    private void OpenSelected()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Index >= _hits.Count) return;
        var hit = _hits[_grid.SelectedRows[0].Index];
        Close();
        using var form = new GenericTableForm(hit.表名, initialFilter: hit.過濾);
        form.ShowDialog(Owner);
    }
}
