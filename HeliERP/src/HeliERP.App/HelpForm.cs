// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;

namespace HeliERP.App;

/// <summary>快捷鍵與操作說明（由「說明」選單開啟）</summary>
public sealed class HelpForm : Form
{
    public HelpForm()
    {
        Text = "快捷鍵與操作說明";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 460);
        BackColor = UiTheme.Background;
        Font = UiTheme.Font(10F);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        Controls.Add(UiTheme.BuildHeader("快捷鍵與操作說明",
            "快速操作捷徑，與各畫面工具列按鈕等效", 56));

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = UiTheme.Card,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        };
        UiTheme.StyleDataGridView(grid);

        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "快捷鍵", DataPropertyName = "快捷鍵", Width = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "功能", DataPropertyName = "功能", Width = 230 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "適用範圍", DataPropertyName = "適用範圍", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        var dt = new DataTable();
        dt.Columns.Add("快捷鍵", typeof(string));
        dt.Columns.Add("功能", typeof(string));
        dt.Columns.Add("適用範圍", typeof(string));
        dt.Rows.Add("F2", "新增資料", "各資料維護畫面");
        dt.Rows.Add("F3", "修改／編輯資料", "各資料維護畫面");
        dt.Rows.Add("F4", "刪除資料", "各資料維護畫面");
        dt.Rows.Add("Ctrl + F", "搜尋／查詢", "各資料維護畫面");
        dt.Rows.Add("Ctrl + K", "全域快速搜尋", "主畫面");
        dt.Rows.Add("Esc", "關閉視窗／取消", "對話框");
        dt.Rows.Add("雙擊資料列", "開啟編輯", "表格型維護畫面");
        grid.DataSource = dt;

        var tip = new Label
        {
            Text = "提示：各畫面頂端工具列均提供對應按鈕，滑鼠操作與快捷鍵等效。",
            Dock = DockStyle.Bottom,
            Height = 34,
            ForeColor = UiTheme.TextSub,
            BackColor = UiTheme.Card,
            Padding = new Padding(UiTheme.SpacingLg, 8, UiTheme.SpacingLg, 0),
            Font = UiTheme.Font(9F),
        };

        var btnClose = new ModernButton
        {
            Text = "關　閉",
            Width = 110,
            Height = 40,
            IsPrimary = false,
            DrawShadow = false,
            Location = new Point(ClientSize.Width - 130, ClientSize.Height - 54),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnClose.Click += (s, e) => Close();

        Controls.Add(grid);
        Controls.Add(tip);
        Controls.Add(btnClose);
        AcceptButton = btnClose;
        UiTheme.ScaleForDpi(this);
        UiTheme.ClampToScreen(this);
    }
}
