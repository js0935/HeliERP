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

/// <summary>會計傳票主檔／明細編輯視窗。</summary>
public static class VoucherDialogs
{
    private const string 日期格式 = "yyyy-MM-dd HH:mm:ss";
    private static readonly string[] 傳票類別集 = { "現金收入", "現金支出", "轉帳傳票", "其他" };

    public static Dictionary<string, object?>? ShowMain(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增傳票" : "修改傳票",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(540, 420),
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoScaleDimensions = new SizeF(96F, 96F),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var deptDt = DbManager.QueryTable(
            "SELECT [部門編號] AS [編號], COALESCE(NULLIF([部門名稱],''),[部門編號]) AS [顯示] FROM [部門資料] ORDER BY [部門編號]");

        var txtNo = new TextBox();
        var dtDate = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var cmbKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbKind.Items.AddRange(傳票類別集);
        var cmbDept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = deptDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbDept);
        var txtReview = new TextBox();
        var txtMaker = new TextBox();
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

        Row("傳票編號", txtNo);
        Row("傳票日期", dtDate);
        Row("傳票類別", cmbKind, 160);
        Row("部門", cmbDept);
        Row("覆核", txtReview);
        Row("製單", txtMaker);

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
            txtNo.Text = Convert.ToString(row["傳票編號"]);
            dtDate.Value = ParseDate(row["傳票日期"]) ?? DateTime.Today;
            cmbKind.SelectedItem = row["傳票類別"] as string;
            cmbDept.SelectedValue = row["部門編號"];
            txtReview.Text = Convert.ToString(row["覆核"]);
            txtMaker.Text = Convert.ToString(row["製單"]);
        }
        else
        {
            cmbKind.SelectedIndex = 0;
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            var no = txtNo.Text.Trim();
            if (no.Length == 0) { lblMsg.Text = "請輸入傳票編號"; return; }
            result = new Dictionary<string, object?>
            {
                ["傳票編號"] = no,
                ["傳票日期"] = dtDate.Value.ToString(日期格式),
                ["傳票類別"] = cmbKind.SelectedItem?.ToString() ?? "轉帳傳票",
                ["部門編號"] = cmbDept.SelectedValue as string,
                ["覆核"] = NullIfEmpty(txtReview.Text),
                ["製單"] = NullIfEmpty(txtMaker.Text),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        UiTheme.ClampToScreen(dlg);
        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    public static Dictionary<string, object?>? ShowDetail(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增傳票明細" : "修改傳票明細",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(520, 480),
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoScaleDimensions = new SizeF(96F, 96F),
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
        var txtAmount = new TextBox();
        var txtDebit = new TextBox { ReadOnly = true, BackColor = UiTheme.Card };
        var txtCredit = new TextBox { ReadOnly = true, BackColor = UiTheme.Card };
        var txtSummary = new TextBox();
        var cmbDept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = deptDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbDept);
        var txtProject = new TextBox();
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        void RefreshAmount()
        {
            decimal.TryParse(txtAmount.Text.Trim(), out var amt);
            var side = cmbDebitCredit.SelectedItem?.ToString();
            txtDebit.Text = side == "借" ? amt.ToString("0.##") : "";
            txtCredit.Text = side == "貸" ? amt.ToString("0.##") : "";
        }

        cmbTitle.SelectedValueChanged += (s, e) =>
        {
            if (cmbTitle.SelectedValue is string id)
            {
                var rows = titleDt.Select($"[編號] = '{id.Replace("'", "''")}'");
                if (rows.Length > 0)
                    txtName.Text = rows[0]["顯示"]?.ToString() ?? "";
            }
        };
        cmbDebitCredit.SelectedIndexChanged += (s, e) => RefreshAmount();
        txtAmount.TextChanged += (s, e) => RefreshAmount();

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
        Row("金額", txtAmount, 140);
        Row("借方金額", txtDebit, 140);
        Row("貸方金額", txtCredit, 140);
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

        if (row is not null)
        {
            cmbDebitCredit.SelectedItem = row["借貸"] as string;
            cmbTitle.SelectedValue = row["科目編號"];
            txtName.Text = Convert.ToString(row["科目名稱"]);
            txtAmount.Text = Convert.ToString(row["金額"]);
            txtSummary.Text = Convert.ToString(row["摘要"]);
            cmbDept.SelectedValue = row["部門編號"];
            txtProject.Text = Convert.ToString(row["專案編號"]);
            RefreshAmount();
        }
        else
        {
            cmbDebitCredit.SelectedIndex = 0;
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            if (cmbTitle.SelectedValue is null) { lblMsg.Text = "請選擇會計科目"; return; }
            if (!decimal.TryParse(txtAmount.Text.Trim(), out var amt)) amt = 0;
            var side = cmbDebitCredit.SelectedItem?.ToString() ?? "借";
            result = new Dictionary<string, object?>
            {
                ["借貸"] = side,
                ["科目編號"] = cmbTitle.SelectedValue as string,
                ["科目名稱"] = NullIfEmpty(txtName.Text),
                ["金額"] = amt,
                ["借方金額"] = side == "借" ? amt : 0m,
                ["貸方金額"] = side == "貸" ? amt : 0m,
                ["摘要"] = NullIfEmpty(txtSummary.Text),
                ["部門編號"] = cmbDept.SelectedValue as string,
                ["專案編號"] = NullIfEmpty(txtProject.Text),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        UiTheme.ClampToScreen(dlg);
        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static DateTime? ParseDate(object v)
    {
        if (v is null || v == DBNull.Value) return null;
        return DateTime.TryParse(Convert.ToString(v), out var d) ? d : null;
    }
}
