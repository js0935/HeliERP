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

/// <summary>薪資設定（員工計薪項目）編輯視窗。</summary>
public static class PayrollConfigDialogs
{
    public static Dictionary<string, object?>? ShowEdit(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增計薪項目" : "修改計薪項目",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(520, 480),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var staffDt = DbManager.QueryTable(
            "SELECT [員工編號] AS [編號], COALESCE(NULLIF([員工姓名],''),[員工編號]) AS [顯示] FROM [員工資料] ORDER BY [員工編號]");

        var cmbStaff = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = staffDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbStaff);
        var txtNo = new TextBox();
        var txtName = new TextBox();
        var cmbUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbUnit.Items.AddRange(new object[] { "月", "日", "時", "件", "次" });
        var cmbAddSub = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbAddSub.Items.AddRange(new object[] { "加", "減" });
        var cmbTax = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbTax.Items.AddRange(new object[] { "應稅", "免稅" });
        var txtUnitAmt = new TextBox();
        var txtAmtFormula = new TextBox();
        var txtQtyFormula = new TextBox();
        var txtAccount = new TextBox();
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

        Row("員工", cmbStaff);
        Row("計薪編號", txtNo);
        Row("計薪名稱", txtName);
        Row("單位", cmbUnit, 100);
        Row("加／減", cmbAddSub, 100);
        Row("計稅別", cmbTax, 100);
        Row("單位金額", txtUnitAmt, 140);
        Row("金額公式編號", txtAmtFormula);
        Row("數量公式編號", txtQtyFormula);
        Row("轉帳科目", txtAccount);

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
            cmbStaff.SelectedValue = row["員工編號"];
            txtNo.Text = Convert.ToString(row["計薪編號"]);
            txtName.Text = Convert.ToString(row["計薪名稱"]);
            cmbUnit.SelectedItem = row["單位"] as string;
            cmbAddSub.SelectedItem = row["加減"] as string;
            cmbTax.SelectedItem = row["計稅別"] as string;
            txtUnitAmt.Text = Convert.ToString(row["單位金額"]);
            txtAmtFormula.Text = Convert.ToString(row["金額公式編號"]);
            txtQtyFormula.Text = Convert.ToString(row["數量公式編號"]);
            txtAccount.Text = Convert.ToString(row["轉帳科目"]);
        }
        else
        {
            cmbUnit.SelectedIndex = 0;
            cmbAddSub.SelectedIndex = 0;
            cmbTax.SelectedIndex = 0;
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            var no = txtNo.Text.Trim();
            if (cmbStaff.SelectedValue is null) { lblMsg.Text = "請選擇員工"; return; }
            if (no.Length == 0) { lblMsg.Text = "請輸入計薪編號"; return; }
            if (!decimal.TryParse(txtUnitAmt.Text.Trim(), out var amt)) amt = 0;
            result = new Dictionary<string, object?>
            {
                ["員工編號"] = cmbStaff.SelectedValue as string,
                ["計薪編號"] = no,
                ["計薪名稱"] = NullIfEmpty(txtName.Text),
                ["單位"] = cmbUnit.SelectedItem?.ToString(),
                ["加減"] = cmbAddSub.SelectedItem?.ToString() ?? "加",
                ["計稅別"] = cmbTax.SelectedItem?.ToString() ?? "應稅",
                ["單位金額"] = amt,
                ["金額公式編號"] = NullIfEmpty(txtAmtFormula.Text),
                ["數量公式編號"] = NullIfEmpty(txtQtyFormula.Text),
                ["轉帳科目"] = NullIfEmpty(txtAccount.Text),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
