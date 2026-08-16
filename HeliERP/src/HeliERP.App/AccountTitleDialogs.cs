// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace HeliERP.App;

/// <summary>會計科目編輯視窗。</summary>
public static class AccountTitleDialogs
{
    public static Dictionary<string, object?>? ShowEdit(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增會計科目" : "修改會計科目",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 520),
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoScaleDimensions = new SizeF(96F, 96F),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var txtNo = new TextBox();
        var txtName = new TextBox();
        var txtEn = new TextBox();
        var txtMemo = new TextBox();
        var txtCategory = new TextBox();
        var cmbSide = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbSide.Items.AddRange(new object[] { "借", "貸" });
        var txtOpen = new TextBox();
        var txtOffset = new TextBox();
        var txtControl = new TextBox();
        var txtParent = new TextBox();
        var txtDesc = new TextBox();
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        int y = 16;
        void Row(string labelText, Control field, int width = 300)
        {
            dlg.Controls.Add(new Label { Text = labelText, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(24, y + 6) });
            field.Location = new Point(160, y);
            field.Width = width;
            dlg.Controls.Add(field);
            y += 38;
        }

        Row("科目編號", txtNo);
        Row("科目名稱", txtName);
        Row("英文名稱", txtEn);
        Row("常用摘要", txtMemo);
        Row("類別編號", txtCategory, 160);
        Row("期初借貸", cmbSide, 100);
        Row("期初餘額", txtOpen, 140);
        Row("沖銷科目", txtOffset, 100);
        Row("統制科目", txtControl, 100);
        Row("隸屬科目", txtParent);
        Row("說明", txtDesc);

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
            txtNo.Text = Convert.ToString(row["科目編號"]);
            txtName.Text = Convert.ToString(row["科目名稱"]);
            txtEn.Text = Convert.ToString(row["英文名稱"]);
            txtMemo.Text = Convert.ToString(row["常用摘要"]);
            txtCategory.Text = Convert.ToString(row["類別編號"]);
            cmbSide.SelectedItem = row["期初借貸"] as string;
            txtOpen.Text = Convert.ToString(row["期初餘額"]);
            txtOffset.Text = Convert.ToString(row["沖銷科目"]);
            txtControl.Text = Convert.ToString(row["統制科目"]);
            txtParent.Text = Convert.ToString(row["隸屬科目"]);
            txtDesc.Text = Convert.ToString(row["說明"]);
        }
        else
        {
            cmbSide.SelectedIndex = 0;
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            var no = txtNo.Text.Trim();
            if (no.Length == 0) { lblMsg.Text = "請輸入科目編號"; return; }
            if (txtName.Text.Trim().Length == 0) { lblMsg.Text = "請輸入科目名稱"; return; }
            if (!decimal.TryParse(txtOpen.Text.Trim(), out var open)) open = 0;
            result = new Dictionary<string, object?>
            {
                ["科目編號"] = no,
                ["科目名稱"] = txtName.Text.Trim(),
                ["英文名稱"] = NullIfEmpty(txtEn.Text),
                ["常用摘要"] = NullIfEmpty(txtMemo.Text),
                ["類別編號"] = NullIfEmpty(txtCategory.Text),
                ["期初借貸"] = cmbSide.SelectedItem?.ToString(),
                ["期初餘額"] = open,
                ["沖銷科目"] = ParseInt(txtOffset.Text),
                ["統制科目"] = ParseInt(txtControl.Text),
                ["隸屬科目"] = NullIfEmpty(txtParent.Text),
                ["說明"] = NullIfEmpty(txtDesc.Text),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        UiTheme.ClampToScreen(dlg);
        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    private static int ParseInt(string s) => int.TryParse(s.Trim(), out var n) ? n : 0;

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
