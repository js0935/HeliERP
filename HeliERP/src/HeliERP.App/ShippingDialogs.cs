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

/// <summary>託運主檔／明細編輯視窗。</summary>
public static class ShippingDialogs
{
    private const string 日期格式 = "yyyy-MM-dd HH:mm:ss";

    public static Dictionary<string, object?>? ShowMain(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增託運單" : "修改託運單",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 480),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var customerDt = ComboData("SELECT [客廠編號] AS [編號], [公司簡稱] AS [顯示], [聯絡電話一] AS [電話] FROM [客戶廠商] ORDER BY [客廠編號]");
        var receiverDt = ComboData("SELECT [客廠編號] AS [編號], [公司簡稱] AS [顯示] FROM [客戶廠商] ORDER BY [客廠編號]");
        var driverDt = ComboData("SELECT [員工編號] AS [編號], COALESCE(NULLIF([員工姓名],''),[員工編號]) AS [顯示] FROM [員工資料]");

        var txtNo = new TextBox();
        var dtDate = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var txtPhone = new TextBox();
        var cmbCustomer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = customerDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbCustomer);
        cmbCustomer.SelectedValueChanged += (s, e) =>
        {
            if (cmbCustomer.SelectedItem is DataRowView drv)
                txtPhone.Text = drv["電話"] is DBNull ? "" : drv["電話"].ToString();
        };
        var cmbReceiver = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = receiverDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbReceiver);
        var cmbDriver = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = driverDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbDriver);
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

        Row("託運單號", txtNo);
        Row("託運日期", dtDate);
        Row("委託客戶", cmbCustomer);
        Row("聯絡電話", txtPhone);
        Row("收貨廠商", cmbReceiver);
        Row("司機", cmbDriver);
        Row("製單人員", txtMaker);
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
            txtNo.Text = Convert.ToString(row["託運單號"]);
            dtDate.Value = ParseDate(row["託運日期"]) ?? DateTime.Today;
            cmbCustomer.SelectedValue = row["委託客戶"];
            txtPhone.Text = Convert.ToString(row["聯絡電話"]);
            cmbReceiver.SelectedValue = row["收貨廠商"];
            cmbDriver.SelectedValue = row["司機編號"];
            txtMaker.Text = Convert.ToString(row["製單人員"]);
            txtRemark.Text = Convert.ToString(row["備註"]);
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            var no = txtNo.Text.Trim();
            if (no.Length == 0) { lblMsg.Text = "請輸入託運單號"; return; }
            result = new Dictionary<string, object?>
            {
                ["託運單號"] = no,
                ["託運日期"] = dtDate.Value.ToString(日期格式),
                ["委託客戶"] = cmbCustomer.SelectedValue as string,
                ["聯絡電話"] = NullIfEmpty(txtPhone.Text),
                ["收貨廠商"] = cmbReceiver.SelectedValue as string,
                ["司機編號"] = cmbDriver.SelectedValue as string,
                ["製單人員"] = NullIfEmpty(txtMaker.Text),
                ["備註"] = NullIfEmpty(txtRemark.Text),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        UiTheme.ScaleForDpi(dlg);
        UiTheme.ClampToScreen(dlg);
        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    public static Dictionary<string, object?>? ShowDetail(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增託運明細" : "修改託運明細",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(520, 560),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var goodsDt = ComboData("SELECT [貨品編號] AS [編號], [品名] AS [品名], [基本單位] AS [單位] FROM [貨品主檔] ORDER BY [貨品編號]");

        var cmbGoods = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = goodsDt, DisplayMember = "編號", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbGoods);
        var txtName = new TextBox();
        var txtSpec = new TextBox();
        var txtQty = new TextBox();
        var txtUnit = new TextBox();
        var txtPrice = new TextBox();
        var txtAmount = new TextBox { ReadOnly = true, BackColor = UiTheme.Card };
        var txtFrom = new TextBox();
        var txtTo = new TextBox();
        var txtTons = new TextBox();
        var txtBoards = new TextBox();
        var txtRemark = new TextBox();
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        void AutoAmount()
        {
            decimal.TryParse(txtQty.Text.Trim(), out var qty);
            decimal.TryParse(txtPrice.Text.Trim(), out var price);
            txtAmount.Text = (qty * price).ToString("0.##");
        }

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
        txtQty.TextChanged += (s, e) => AutoAmount();
        txtPrice.TextChanged += (s, e) => AutoAmount();

        int y = 16;
        void Row(string labelText, Control field, int width = 300)
        {
            dlg.Controls.Add(new Label { Text = labelText, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(24, y + 6) });
            field.Location = new Point(160, y);
            field.Width = width;
            dlg.Controls.Add(field);
            y += 38;
        }

        Row("貨品編號", cmbGoods);
        Row("品名", txtName);
        Row("規格", txtSpec);
        Row("數量", txtQty, 100);
        Row("單位", txtUnit, 100);
        Row("單價", txtPrice, 120);
        Row("金額", txtAmount, 140);
        Row("起點", txtFrom);
        Row("訖點", txtTo);
        Row("噸數", txtTons, 100);
        Row("板數", txtBoards, 100);
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
            cmbGoods.SelectedValue = row["貨品編號"];
            txtName.Text = Convert.ToString(row["品名"]);
            txtSpec.Text = Convert.ToString(row["規格"]);
            txtQty.Text = Convert.ToString(row["數量"]);
            txtUnit.Text = Convert.ToString(row["單位"]);
            txtPrice.Text = Convert.ToString(row["單價"]);
            txtFrom.Text = Convert.ToString(row["起點"]);
            txtTo.Text = Convert.ToString(row["訖點"]);
            txtTons.Text = Convert.ToString(row["噸數"]);
            txtBoards.Text = Convert.ToString(row["板數"]);
            txtRemark.Text = Convert.ToString(row["備註"]);
            AutoAmount();
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            if (!decimal.TryParse(txtQty.Text.Trim(), out var qty)) qty = 0;
            if (!decimal.TryParse(txtPrice.Text.Trim(), out var price)) price = 0;
            if (!decimal.TryParse(txtTons.Text.Trim(), out var tons)) tons = 0;
            if (!decimal.TryParse(txtBoards.Text.Trim(), out var boards)) boards = 0;
            result = new Dictionary<string, object?>
            {
                ["貨品編號"] = cmbGoods.SelectedValue as string,
                ["品名"] = NullIfEmpty(txtName.Text),
                ["規格"] = NullIfEmpty(txtSpec.Text),
                ["數量"] = qty,
                ["單位"] = NullIfEmpty(txtUnit.Text),
                ["單價"] = price,
                ["金額"] = qty * price,
                ["起點"] = NullIfEmpty(txtFrom.Text),
                ["訖點"] = NullIfEmpty(txtTo.Text),
                ["噸數"] = tons,
                ["板數"] = boards,
                ["備註"] = NullIfEmpty(txtRemark.Text),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        UiTheme.ScaleForDpi(dlg);
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
