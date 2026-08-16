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

/// <summary>驗貨主檔／明細編輯視窗。</summary>
public static class InspectionDialogs
{
    private const string 日期格式 = "yyyy-MM-dd HH:mm:ss";

    public static Dictionary<string, object?>? ShowMain(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增驗貨單" : "修改驗貨單",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 560),
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoScaleDimensions = new SizeF(96F, 96F),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var deptDt = ComboData("SELECT [部門編號] AS [編號], COALESCE(NULLIF([部門名稱],''),[部門編號]) AS [顯示] FROM [部門資料]");
        var staffDt = ComboData("SELECT [員工編號] AS [編號], COALESCE(NULLIF([員工姓名],''),[員工編號]) AS [顯示] FROM [員工資料]");
        var vendorDt = ComboData("SELECT [客廠編號] AS [編號], [公司簡稱] AS [顯示] FROM [客戶廠商] ORDER BY [客廠編號]");

        var cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbType.Items.AddRange(new object[] { "進貨", "出貨" });
        var txtNo = new TextBox();
        var dtDate = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var txtSent = new TextBox();
        var txtPo = new TextBox();
        var cmbDept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = deptDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbDept);
        var cmbStaff = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = staffDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbStaff);
        var cmbVendor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = vendorDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbVendor);
        var txtVendorName = new TextBox();
        var cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbStatus.Items.AddRange(new object[] { "未檢", "檢驗中", "已驗畢" });
        var txtMaker = new TextBox();
        var txtRemark = new TextBox();
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

        Row("驗貨單別", cmbType, 120);
        Row("驗貨單號", txtNo);
        Row("驗貨日期", dtDate);
        Row("送驗單號", txtSent);
        Row("採購單號", txtPo);
        Row("部門", cmbDept);
        Row("業務員", cmbStaff);
        Row("廠商", cmbVendor);
        Row("廠商名稱", txtVendorName);
        Row("檢驗狀況", cmbStatus, 120);
        Row("製單人員", txtMaker);
        Row("備註", txtRemark);

        cmbVendor.SelectedValueChanged += (s, e) =>
        {
            if (cmbVendor.SelectedValue is string id && vendorDt.Select($"[編號] = '{id.Replace("'", "''")}'").Length > 0)
                txtVendorName.Text = vendorDt.Select($"[編號] = '{id.Replace("'", "''")}'")[0]["顯示"]?.ToString() ?? "";
        };

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
            cmbType.SelectedItem = row["驗貨單別"] as string;
            txtNo.Text = Convert.ToString(row["驗貨單號"]);
            dtDate.Value = ParseDate(row["驗貨日期"]) ?? DateTime.Today;
            txtSent.Text = Convert.ToString(row["送驗單號"]);
            txtPo.Text = Convert.ToString(row["採購單號"]);
            cmbDept.SelectedValue = row["部門編號"];
            cmbStaff.SelectedValue = row["業務員編號"];
            cmbVendor.SelectedValue = row["廠商編號"];
            txtVendorName.Text = Convert.ToString(row["廠商名稱"]);
            cmbStatus.SelectedItem = row["檢驗狀況"] as string;
            txtMaker.Text = Convert.ToString(row["製單人員"]);
            txtRemark.Text = Convert.ToString(row["備註"]);
            if (cmbType.SelectedIndex < 0) cmbType.SelectedIndex = 0;
            if (cmbStatus.SelectedIndex < 0) cmbStatus.SelectedIndex = 0;
        }
        else
        {
            cmbType.SelectedIndex = 0;
            cmbStatus.SelectedIndex = 0;
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            var no = txtNo.Text.Trim();
            if (no.Length == 0) { lblMsg.Text = "請輸入驗貨單號"; return; }
            result = new Dictionary<string, object?>
            {
                ["驗貨單別"] = cmbType.SelectedItem?.ToString(),
                ["驗貨單號"] = no,
                ["驗貨日期"] = dtDate.Value.ToString(日期格式),
                ["送驗單號"] = NullIfEmpty(txtSent.Text),
                ["採購單號"] = NullIfEmpty(txtPo.Text),
                ["部門編號"] = cmbDept.SelectedValue as string,
                ["業務員編號"] = cmbStaff.SelectedValue as string,
                ["廠商編號"] = cmbVendor.SelectedValue as string,
                ["廠商名稱"] = NullIfEmpty(txtVendorName.Text),
                ["檢驗狀況"] = cmbStatus.SelectedItem?.ToString() ?? "未檢",
                ["製單人員"] = NullIfEmpty(txtMaker.Text),
                ["備註"] = NullIfEmpty(txtRemark.Text),
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
            Text = row is null ? "新增驗貨明細" : "修改驗貨明細",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(480, 440),
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoScaleDimensions = new SizeF(96F, 96F),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var vendorDt = ComboData("SELECT [客廠編號] AS [編號], [公司簡稱] AS [顯示] FROM [客戶廠商] ORDER BY [客廠編號]");
        var goodsDt = ComboData("SELECT [貨品編號] AS [編號], [品名] AS [品名], [基本單位] AS [單位] FROM [貨品主檔] ORDER BY [貨品編號]");

        var cmbVendor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = vendorDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbVendor);
        var cmbGoods = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = goodsDt, DisplayMember = "編號", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbGoods);
        var txtName = new TextBox();
        var txtUnit = new TextBox();
        var txtQty = new TextBox();
        var txtChecked = new TextBox();
        var txtBad = new TextBox();
        var cmbPass = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbPass.Items.AddRange(new object[] { "合格", "不合格", "" });
        var txtRemark = new TextBox();
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        cmbGoods.SelectedValueChanged += (s, e) =>
        {
            if (cmbGoods.SelectedValue is string id)
            {
                var rows = goodsDt.Select($"[編號] = '{id.Replace("'", "''")}'");
                if (rows.Length > 0)
                {
                    txtName.Text = rows[0]["品名"]?.ToString() ?? "";
                    txtUnit.Text = rows[0]["單位"]?.ToString() ?? "";
                }
            }
        };

        int y = 16;
        void Row(string labelText, Control field, int width = 280)
        {
            dlg.Controls.Add(new Label { Text = labelText, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(24, y + 6) });
            field.Location = new Point(160, y);
            field.Width = width;
            dlg.Controls.Add(field);
            y += 38;
        }

        Row("廠商", cmbVendor);
        Row("貨品編號", cmbGoods);
        Row("品名", txtName);
        Row("單位", txtUnit, 100);
        Row("送驗數量", txtQty, 100);
        Row("抽驗／已驗數量", txtChecked, 100);
        Row("不良品數量", txtBad, 100);
        Row("合格註記", cmbPass, 120);
        Row("備註", txtRemark);

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
            cmbVendor.SelectedValue = row["廠商編號"];
            cmbGoods.SelectedValue = row["貨品編號"];
            txtName.Text = Convert.ToString(row["品名"]);
            txtUnit.Text = Convert.ToString(row["單位"]);
            txtQty.Text = Convert.ToString(row["送驗數量"]);
            txtChecked.Text = Convert.ToString(row["抽驗或已驗數量"]);
            txtBad.Text = Convert.ToString(row["不良品數量"]);
            cmbPass.SelectedItem = row["合格註記"] as string;
            txtRemark.Text = Convert.ToString(row["備註"]);
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            if (!decimal.TryParse(txtQty.Text.Trim(), out var qty)) qty = 0;
            if (!decimal.TryParse(txtChecked.Text.Trim(), out var checkedQty)) checkedQty = 0;
            if (!decimal.TryParse(txtBad.Text.Trim(), out var bad)) bad = 0;
            result = new Dictionary<string, object?>
            {
                ["廠商編號"] = cmbVendor.SelectedValue as string,
                ["貨品編號"] = cmbGoods.SelectedValue as string,
                ["品名"] = NullIfEmpty(txtName.Text),
                ["單位"] = NullIfEmpty(txtUnit.Text),
                ["送驗數量"] = qty,
                ["抽驗或已驗數量"] = checkedQty,
                ["不良品數量"] = bad,
                ["合格註記"] = string.IsNullOrEmpty(cmbPass.SelectedItem?.ToString()) ? null : cmbPass.SelectedItem?.ToString(),
                ["備註"] = NullIfEmpty(txtRemark.Text),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        UiTheme.ClampToScreen(dlg);
        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    private static DataTable ComboData(string sql) => DbManager.QueryTable(sql);

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static DateTime? ParseDate(object v)
    {
        if (v is null || v == DBNull.Value) return null;
        return DateTime.TryParse(Convert.ToString(v), out var d) ? d : null;
    }
}
