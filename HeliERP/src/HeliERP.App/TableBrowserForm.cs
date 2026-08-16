// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 基本資料維護總覽：左側分類樹（頂層分類 → 子分類），右側列出該分類下全部資料表，
/// 點擊表名開啟泛型資料維護視窗。唯讀表（交易資料/彙總報表）以「〔唯讀〕」標示。
/// </summary>
public class TableBrowserForm : Form
{
    private readonly TreeView _tree;
    private readonly FlowLayoutPanel _flow;
    private readonly TextBox _txtSearch;
    private readonly ToolStripLabel _lblStatus;

    public TableBrowserForm()
    {
        Text = "基本資料維護總覽";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 600);
        Font = new Font("Microsoft JhengHei UI", 11F);
        UiTheme.Apply(this);

        // ── 標題列 ──
        Controls.Add(UiTheme.BuildHeader("基本資料維護總覽", "左側分類樹瀏覽，點擊資料表開啟維護視窗"));

        // ── 頂部工具列：搜尋 ──
        var toolbar = new ToolStrip();
        UiTheme.StyleToolStrip(toolbar);
        toolbar.Items.Add(new ToolStripLabel("搜尋資料表："));
        _txtSearch = new TextBox { Width = 320 };
        _txtSearch.TextChanged += (s, e) => ApplySearch();
        toolbar.Items.Add(new ToolStripControlHost(_txtSearch));
        toolbar.Items.Add(new ToolStripSeparator());
        _lblStatus = new ToolStripLabel("　") { ForeColor = UiTheme.TextSub };
        toolbar.Items.Add(_lblStatus);
        Controls.Add(toolbar);
        toolbar.Dock = DockStyle.Top;

        // ── 主版面：左樹 + 右按鈕區 ──
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(UiTheme.SpacingLg),
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _tree = new TreeView { Dock = DockStyle.Fill, Margin = new Padding(0, 0, UiTheme.SpacingMd, 0) };
        UiTheme.StyleTreeView(_tree);
        _tree.AfterSelect += (s, e) => OnNodeSelected(e.Node);
        layout.Controls.Add(_tree, 0, 0);

        var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        UiTheme.StyleCardPanel(card, UiTheme.SpacingLg);
        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = true,
            AutoScroll = true,
            BackColor = UiTheme.Card,
            Padding = new Padding(UiTheme.SpacingLg),
            Margin = new Padding(0),
        };
        card.Controls.Add(_flow);
        layout.Controls.Add(card, 1, 0);
        Controls.Add(layout);
        layout.BringToFront();

        BuildTree();
        _tree.SelectedNode = _tree.Nodes.Count > 0 ? _tree.Nodes[0] : null;
        if (_tree.SelectedNode?.Nodes.Count > 0)
            _tree.SelectedNode = _tree.SelectedNode.Nodes[0];

        ShortcutHelper.Enable(this, onSearch: () => _txtSearch.Focus());
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    /// <summary>建立分類樹（不含 Hidden 表）</summary>
    private void BuildTree()
    {
        foreach (var main in TableCatalog.GetMains())
        {
            var subs = TableCatalog.GetSubs(main);
            if (subs.Count == 0) continue;

            var node = new TreeNode(main)
            {
                NodeFont = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark,
            };
            foreach (var sub in subs)
            {
                var count = TableCatalog.GetTables(main, sub).Count;
                if (count == 0) continue;
                node.Nodes.Add(new TreeNode($"{sub}（{count} 表）"));
            }
            if (node.Nodes.Count == 0) continue;
            _tree.Nodes.Add(node);
        }
        _tree.ExpandAll();
    }

    /// <summary>樹節點選取：判斷是頂層分類或子分類，列出對應資料表按鈕</summary>
    private void OnNodeSelected(TreeNode? node)
    {
        if (node is null) return;
        if (node.Parent is null)
        {
            // 頂層分類 → 列出該分類下全部表
            var tables = TableCatalog.GetTables(node.Text);
            RenderButtons(tables);
            _lblStatus.Text = $"分類「{node.Text}」共 {tables.Count} 個資料表";
        }
        else
        {
            var sub = node.Text.Split('（')[0];     // 去掉「（N 表）」後綴
            var tables = TableCatalog.GetTables(node.Parent.Text, sub);
            RenderButtons(tables);
            _lblStatus.Text = $"「{node.Parent.Text}」-「{sub}」共 {tables.Count} 個資料表";
        }
    }

    /// <summary>右側列出資料表按鈕</summary>
    private void RenderButtons(IReadOnlyList<TableDef> tables)
    {
        _flow.SuspendLayout();
        _flow.Controls.Clear();
        foreach (var t in tables)
        {
            bool readOnly = t.Mode == TableMode.ReadOnly;
            var btn = new ModernButton
            {
                Text = readOnly ? $"{t.Name}〔唯讀〕" : t.Name,
                IsPrimary = false,
                DrawShadow = false,
                CornerRadius = UiTheme.RadiusSm,
                Size = new Size(238, 46),
                Margin = new Padding(0, 0, UiTheme.SpacingMd, UiTheme.SpacingMd),
                Font = UiTheme.Font(11F, readOnly ? FontStyle.Regular : FontStyle.Bold),
            };
            var tableName = t.Name;
            btn.Click += (s, e) => OpenTable(tableName);
            _flow.Controls.Add(btn);
        }
        if (tables.Count == 0)
        {
            _flow.Controls.Add(new Label
            {
                Text = "此分類沒有資料表。",
                Font = UiTheme.Font(11F),
                ForeColor = UiTheme.TextSub,
                AutoSize = true,
                Margin = new Padding(6, 20, 0, 0),
            });
        }
        _flow.ResumeLayout();
    }

    /// <summary>開啟資料維護視窗：表單式主檔（FormMasterCatalog）優先，其餘用泛型表格</summary>
    private void OpenTable(string tableName)
    {
        try
        {
            var cfg = FormMasterCatalog.Get(tableName);
            using var form = cfg is not null
                ? (Form)new FormMasterForm(tableName, cfg.Value.ListColumns, cfg.Value.Pages)
                : new GenericTableForm(tableName);
            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(@"D:\HeliAcc\HeliERP\ui-error.log",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 開啟「{tableName}」失敗\r\n{ex}\r\n\r\n");
            }
            catch { }
            MessageBox.Show($"開啟「{tableName}」失敗：{ex.Message}", "錯誤",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>搜尋：輸入即過濾；清空後回到目前樹選取分類</summary>
    private void ApplySearch()
    {
        var kw = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(kw))
        {
            OnNodeSelected(_tree.SelectedNode);
            return;
        }
        var found = TableCatalog.Find(kw);
        RenderButtons(found);
        _lblStatus.Text = $"搜尋「{kw}」：找到 {found.Count} 個資料表";
    }
}
