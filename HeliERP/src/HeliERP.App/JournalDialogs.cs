// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>常用分錄主檔／分錄明細編輯視窗。</summary>
public static class JournalDialogs
{
    private static readonly string[] 分錄類別集 = { "資產", "負債", "權益", "收入", "費用", "其他" };

    public static Dictionary<string, object?>? ShowMain(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增常用分錄" : "修改常用分錄",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(500, 260),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var txtNo = new TextBox();
        var cmbKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbKind.Items.AddRange(分錄類別集);
        var txtName = new TextBox();
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        int y = 16;
        void Row(string labelText, Control field, int width = 260)
        {
            dlg.Controls.Add(new Label { Text = labelText, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(24, y + 6) });
            field.Location = new Point(160, y);
            field.Width = width;
            dlg.Controls.Add(field);
            y += 38;
        }

        Row("分錄編號", txtNo);
        Row("分錄類別", cmbKind, 140);
        Row("分錄名稱", txtName);

        lblMsg.Location = new Point(24, y);
        dlg.Controls.Add(lblMsg);
        y += 28;
        var btnOk = new ModernButton { Text = "確定", Size = new Size(96, 40), Location = new Point(160, y), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(80, 40), Location = new Point(268, y), IsPrimary = false, DrawShadow = false };
        dlg.Controls.Add(btnOk);
        dlg.Controls.Add(btnCancel);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        if (row is not null)
        {
            txtNo.Text = Convert.ToString(row["分錄編號"]);
            cmbKind.SelectedItem = row["分錄類別"] as string;
            txtName.Text = Convert.ToString(row["分錄名稱"]);
        }
        else
        {
            cmbKind.SelectedIndex = 0;
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            var no = txtNo.Text.Trim();
            if (no.Length == 0) { lblMsg.Text = "請輸入分錄編號"; return; }
            if (txtName.Text.Trim().Length == 0) { lblMsg.Text = "請輸入分錄名稱"; return; }
            result = new Dictionary<string, object?>
            {
                ["分錄編號"] = no,
                ["分錄類別"] = cmbKind.SelectedItem?.ToString(),
                ["分錄名稱"] = txtName.Text.Trim(),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    /// <summary>分錄明細編輯；journalNo 為目前所屬常用分錄編號。</summary>
    public static Dictionary<string, object?>? ShowDetail(IWin32Window owner, DataRow? row, string journalNo)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增分錄明細" : "修改分錄明細",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(520, 340),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var titleDt = DbManager.QueryTable(
            "SELECT [科目編號] AS [編號], COALESCE(NULLIF([科目名稱],''),[科目編號]) AS [顯示] FROM [會計科目] ORDER BY [科目編號]");
        var deptDt = DbManager.QueryTable(
            "SELECT [部門編號] AS [編號], COALESCE(NULLIF([部門名稱],''),[部門編號]) AS [顯示] FROM [部門資料] ORDER BY [部門編號]");

        var cmbDebitCredit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbDebitCredit.Items.AddRange(new object[] { "借", "貸" });
        var cmbTitle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = titleDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbTitle);
        var txtName = new TextBox();
        var txtSummary = new TextBox();
        var cmbDept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = deptDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbDept);
        var txtProject = new TextBox();
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        cmbTitle.SelectedValueChanged += (s, e) =>
        {
            if (cmbTitle.SelectedValue is string id)
            {
                var rows = titleDt.Select($"[編號] = '{id.Replace("'", "''")}'");
                if (rows.Length > 0)
                    txtName.Text = rows[0]["顯示"]?.ToString() ?? "";
            }
        };

        int y = 16;
        void Row(string labelText, Control field, int width = 300)
        {
            dlg.Controls.Add(new Label { Text = labelText, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(24, y + 6) });
            field.Location = new Point(160, y);
            field.Width = width;
            dlg.Controls.Add(field);
            y += 38;
        }

        Row("借／貸", cmbDebitCredit, 100);
        Row("科目編號", cmbTitle);
        Row("科目名稱", txtName);
        Row("摘要", txtSummary);
        Row("部門", cmbDept);
        Row("專案編號", txtProject);

        lblMsg.Location = new Point(24, y);
        dlg.Controls.Add(lblMsg);
        y += 28;
        var btnOk = new ModernButton { Text = "確定", Size = new Size(96, 40), Location = new Point(160, y), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(80, 40), Location = new Point(268, y), IsPrimary = false, DrawShadow = false };
        dlg.Controls.Add(btnOk);
        dlg.Controls.Add(btnCancel);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        var 建檔時間 = row is null ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") : Convert.ToString(row["建檔時間"]);

        if (row is not null)
        {
            cmbDebitCredit.SelectedItem = row["借貸"] as string;
            cmbTitle.SelectedValue = row["科目編號"];
            txtName.Text = Convert.ToString(row["科目名稱"]);
            txtSummary.Text = Convert.ToString(row["摘要"]);
            cmbDept.SelectedValue = row["部門編號"];
            txtProject.Text = Convert.ToString(row["專案編號"]);
        }
        else
        {
            cmbDebitCredit.SelectedIndex = 0;
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            if (cmbTitle.SelectedValue is null) { lblMsg.Text = "請選擇會計科目"; return; }
            result = new Dictionary<string, object?>
            {
                ["分錄編號"] = journalNo,
                ["建檔時間"] = 建檔時間,
                ["借貸"] = cmbDebitCredit.SelectedItem?.ToString() ?? "借",
                ["科目編號"] = cmbTitle.SelectedValue as string,
                ["科目名稱"] = NullIfEmpty(txtName.Text),
                ["摘要"] = NullIfEmpty(txtSummary.Text),
                ["部門編號"] = cmbDept.SelectedValue as string,
                ["專案編號"] = NullIfEmpty(txtProject.Text),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
