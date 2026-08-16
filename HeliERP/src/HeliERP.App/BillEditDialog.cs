// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>票據新增／修改視窗：依回傳字典寫入票據收付表。</summary>
public sealed class BillEditDialog
{
    private const string 日期格式 = "yyyy-MM-dd HH:mm:ss";

    /// <summary>顯示編輯視窗；row 為 null 時為新增。回傳欄位值字典，取消回傳 null。</summary>
    public static Dictionary<string, object?>? Show(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增票據" : "修改票據",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(660, 720),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var partyDt = DbManager.QueryTable(
            "SELECT [客廠編號] AS [編號], COALESCE(NULLIF([公司簡稱],''),[客廠編號]) AS [顯示] FROM [客戶廠商] ORDER BY [客廠編號]");
        var bankDt = DbManager.QueryTable(
            "SELECT [帳戶編號] AS [編號], COALESCE(NULLIF([帳戶名稱],''),[帳戶編號]) AS [顯示] FROM [開戶銀行] ORDER BY [帳戶編號]");
        var deptDt = DbManager.QueryTable(
            "SELECT [部門編號] AS [編號], COALESCE(NULLIF([部門名稱],''),[部門編號]) AS [顯示] FROM [部門資料] ORDER BY [部門編號]");

        var cmbKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbKind.Items.AddRange(new object[] { "收票", "付票" });
        var txtNo = new TextBox();
        var txtHolder = new TextBox();
        var cmbParty = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = partyDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbParty);
        var txtAmt = new TextBox();
        var txtRate = new TextBox();
        var lblLcl = new Label { AutoSize = true, ForeColor = UiTheme.PrimaryDark, Font = UiTheme.Font(10.5F, FontStyle.Bold) };
        var txtBank = new TextBox();
        var txtAcct = new TextBox();
        var cmbBank = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = bankDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbBank);
        var cmbTrust = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = bankDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbTrust);
        var cmbDept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = deptDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbDept);
        var txtBType = new TextBox();
        var dtOd = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var dtDue = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var dtPre = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbStatus.Items.AddRange(new object[] { "尚未", "託收中", "已兌", "退票", "作廢" });
        var txtSubject = new TextBox();
        var txtSummary = new TextBox();
        var txtRemark = new TextBox();
        var chkCust = new CheckBox { Text = "客票", AutoSize = true };
        var chkDraw = new CheckBox { Text = "抬頭", AutoSize = true };
        var chkEndorse = new CheckBox { Text = "背書", AutoSize = true };
        var chkPar = new CheckBox { Text = "平行線", AutoSize = true };
        var lblUpper = new Label { AutoSize = true, ForeColor = UiTheme.TextSub, Font = UiTheme.Font(9.5F) };
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        void UpdateTotals()
        {
            if (!decimal.TryParse(txtAmt.Text.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out var amt))
                amt = 0m;
            if (!decimal.TryParse(txtRate.Text.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out var rate))
                rate = 1m;
            lblLcl.Text = $"本幣金額：{amt * rate:N2}";
            lblUpper.Text = $"中文大寫：{ChineseAmount.ToUpper(amt)}";
        }
        txtAmt.TextChanged += (s, e) => UpdateTotals();
        txtRate.TextChanged += (s, e) => UpdateTotals();

        // 版面
        int y = 16;
        void Row(Control label, Control field)
        {
            label.Location = new Point(24, y + 6);
            field.Location = new Point(170, y);
            field.Width = 300;
            dlg.Controls.Add(label);
            dlg.Controls.Add(field);
            y += 30;
        }
        Label Lbl(string text) => new Label { Text = text, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true };

        Row(Lbl("收付類別"), cmbKind);
        Row(Lbl("支票號碼"), txtNo);
        Row(Lbl("支票抬頭"), txtHolder);
        Row(Lbl("來往對象"), cmbParty);
        Row(Lbl("票面金額"), txtAmt);
        Row(Lbl("匯率"), txtRate);
        lblLcl.Location = new Point(24, y + 4);
        lblUpper.Location = new Point(170, y + 4);
        dlg.Controls.Add(lblLcl);
        dlg.Controls.Add(lblUpper);
        y += 30;
        Row(Lbl("票面銀行"), txtBank);
        Row(Lbl("票面帳號"), txtAcct);
        Row(Lbl("銀行帳戶"), cmbBank);
        Row(Lbl("託收帳戶"), cmbTrust);
        Row(Lbl("部門"), cmbDept);
        Row(Lbl("票據類別"), txtBType);
        Row(Lbl("收開票日"), dtOd);
        Row(Lbl("到期日"), dtDue);
        Row(Lbl("預兌日"), dtPre);
        Row(Lbl("票據現況"), cmbStatus);
        Row(Lbl("對方科目"), txtSubject);
        Row(Lbl("傳票摘要"), txtSummary);
        Row(Lbl("備註"), txtRemark);

        chkCust.Location = new Point(170, y + 2);
        chkDraw.Location = new Point(230, y + 2);
        chkEndorse.Location = new Point(290, y + 2);
        chkPar.Location = new Point(350, y + 2);
        dlg.Controls.AddRange(new Control[] { chkCust, chkDraw, chkEndorse, chkPar });
        y += 32;
        lblMsg.Location = new Point(24, y);
        dlg.Controls.Add(lblMsg);
        y += 28;

        var btnOk = new ModernButton { Text = "確定", Size = new Size(96, 40), Location = new Point(170, y), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(80, 40), Location = new Point(278, y), IsPrimary = false, DrawShadow = false };
        dlg.Controls.Add(btnOk);
        dlg.Controls.Add(btnCancel);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        // 帶入既有值
        if (row is not null)
        {
            cmbKind.SelectedItem = row["收付類別"] as string;
            txtNo.Text = Convert.ToString(row["支票號碼"]);
            txtHolder.Text = Convert.ToString(row["支票抬頭"]);
            cmbParty.SelectedValue = row["來往對象"];
            txtAmt.Text = Convert.ToString(row["票面金額"]);
            txtRate.Text = Convert.ToDecimal(row["匯率"]) == 0 ? "1" : Convert.ToString(row["匯率"]);
            txtBank.Text = Convert.ToString(row["票面銀行"]);
            txtAcct.Text = Convert.ToString(row["票面帳號"]);
            cmbBank.SelectedValue = row["銀行帳戶"];
            cmbTrust.SelectedValue = row["託收帳戶"];
            cmbDept.SelectedValue = row["部門編號"];
            txtBType.Text = Convert.ToString(row["票據類別"]);
            dtOd.Value = ParseDate(row["收開票日"]) ?? DateTime.Today;
            dtDue.Value = ParseDate(row["到期日"]) ?? DateTime.Today;
            dtPre.Value = ParseDate(row["預兌日"]) ?? DateTime.Today;
            cmbStatus.SelectedItem = row["票據現況"] as string;
            txtSubject.Text = Convert.ToString(row["對方科目"]);
            txtSummary.Text = Convert.ToString(row["傳票摘要"]);
            txtRemark.Text = Convert.ToString(row["備註"]);
            chkCust.Checked = ToBool(row["客票"]);
            chkDraw.Checked = ToBool(row["抬頭"]);
            chkEndorse.Checked = ToBool(row["背書"]);
            chkPar.Checked = ToBool(row["平行線"]);
            if (cmbKind.SelectedIndex < 0) cmbKind.SelectedIndex = 0;
            if (cmbStatus.SelectedIndex < 0) cmbStatus.SelectedIndex = 0;
        }
        else
        {
            cmbKind.SelectedIndex = 0;
            cmbStatus.SelectedIndex = 0;
            txtRate.Text = "1";
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            var no = txtNo.Text.Trim();
            if (no.Length == 0) { lblMsg.Text = "請輸入支票號碼"; return; }
            if (cmbKind.SelectedIndex < 0) { lblMsg.Text = "請選擇收付類別"; return; }
            if (!decimal.TryParse(txtAmt.Text.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out var amt))
            { lblMsg.Text = "票面金額格式錯誤"; return; }
            if (!decimal.TryParse(txtRate.Text.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out var rate) || rate <= 0)
                rate = 1m;

            result = new Dictionary<string, object?>
            {
                ["收付類別"] = cmbKind.SelectedItem?.ToString(),
                ["支票號碼"] = no,
                ["支票抬頭"] = NullIfEmpty(txtHolder.Text),
                ["票據現況"] = cmbStatus.SelectedItem?.ToString() ?? "尚未",
                ["票據類別"] = NullIfEmpty(txtBType.Text),
                ["部門編號"] = cmbDept.SelectedValue as string,
                ["來往對象"] = cmbParty.SelectedValue as string,
                ["銀行帳戶"] = cmbBank.SelectedValue as string,
                ["託收帳戶"] = cmbTrust.SelectedValue as string,
                ["票面帳號"] = NullIfEmpty(txtAcct.Text),
                ["票面銀行"] = NullIfEmpty(txtBank.Text),
                ["票面金額"] = amt,
                ["本幣金額"] = amt * rate,
                ["中文大寫"] = ChineseAmount.ToUpper(amt),
                ["匯率"] = rate,
                ["對方科目"] = NullIfEmpty(txtSubject.Text),
                ["傳票摘要"] = NullIfEmpty(txtSummary.Text),
                ["客票"] = chkCust.Checked ? 1 : 0,
                ["抬頭"] = chkDraw.Checked ? 1 : 0,
                ["背書"] = chkEndorse.Checked ? 1 : 0,
                ["平行線"] = chkPar.Checked ? 1 : 0,
                ["備註"] = NullIfEmpty(txtRemark.Text),
                ["收開票日"] = dtOd.Value.ToString(日期格式),
                ["到期日"] = dtDue.Value.ToString(日期格式),
                ["預兌日"] = dtPre.Value.ToString(日期格式),
                ["異動日"] = DateTime.Now.ToString(日期格式),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static DateTime? ParseDate(object v)
    {
        if (v is null || v == DBNull.Value) return null;
        return DateTime.TryParse(Convert.ToString(v), out var d) ? d : null;
    }

    private static bool ToBool(object v) =>
        v is null || v == DBNull.Value ? false : Convert.ToInt32(v) != 0;
}

/// <summary>金額中文大寫（銀行大寫：零壹貳參肆伍陸柒捌玖）</summary>
public static class ChineseAmount
{
    private static readonly string[] Digits = { "零", "壹", "貳", "參", "肆", "伍", "陸", "柒", "捌", "玖" };
    private static readonly string[] Units = { "", "拾", "佰", "仟" };
    private static readonly string[] Groups = { "", "萬", "億", "兆" };

    public static string ToUpper(decimal value)
    {
        var n = Math.Truncate(Math.Abs(value));
        var sign = value < 0 ? "負" : "";
        if (n == 0) return "零元整";
        var s = n.ToString(CultureInfo.InvariantCulture);
        var parts = new System.Text.StringBuilder();
        var digits = s;
        int len = digits.Length;
        for (int i = 0; i < len; i++)
        {
            int d = digits[i] - '0';
            int unitIdx = (len - 1 - i) % 4;
            int groupIdx = (len - 1 - i) / 4;
            if (d != 0)
            {
                if (parts.Length > 0 && parts[parts.Length - 1] == '零')
                    parts.Remove(parts.Length - 1, 1);
                parts.Append(Digits[d]).Append(Units[unitIdx]);
            }
            else
            {
                if (parts.Length > 0 && parts[parts.Length - 1] != '零')
                    parts.Append("零");
            }
            if (unitIdx == 0 && groupIdx > 0 && parts.Length > 0)
            {
                if (parts[parts.Length - 1] == '零')
                    parts.Remove(parts.Length - 1, 1);
                parts.Append(Groups[groupIdx]);
            }
        }
        var result = sign + parts.ToString().TrimEnd('零') + "元";
        var frac = Math.Round(Math.Abs(value) - n, 2);
        int jiao = (int)Math.Floor(frac * 10);
        int fen = (int)Math.Floor((frac * 100) % 10 + 0.5m);
        if (jiao == 0 && fen == 0)
            result += "整";
        else
        {
            if (jiao > 0) result += Digits[jiao] + "角";
            if (fen > 0) result += Digits[fen] + "分";
        }
        return result;
    }
}
