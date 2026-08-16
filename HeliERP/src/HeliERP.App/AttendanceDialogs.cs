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

/// <summary>出缺勤主檔／明細編輯視窗。</summary>
public static class AttendanceDialogs
{
    private static readonly string[] 班別 = { "", "常日", "晚班", "小夜", "大夜" };
    private static readonly string[] 星期 = { "一", "二", "三", "四", "五", "六", "日" };

    public static Dictionary<string, object?>? ShowMain(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增出缺勤" : "修改出缺勤",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(520, 360),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var staffDt = DbManager.QueryTable(
            "SELECT [員工編號] AS [編號], COALESCE(NULLIF([員工姓名],''),[員工編號]) AS [顯示], [部門編號] AS [部門] FROM [員工資料] ORDER BY [員工編號]");
        var deptDt = DbManager.QueryTable(
            "SELECT [部門編號] AS [編號], COALESCE(NULLIF([部門名稱],''),[部門編號]) AS [顯示] FROM [部門資料] ORDER BY [部門編號]");

        var cmbStaff = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = staffDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbStaff);
        var numYear = new NumericUpDown { Minimum = 2000, Maximum = 2100, Value = DateTime.Today.Year };
        var numMonth = new NumericUpDown { Minimum = 1, Maximum = 12, Value = DateTime.Today.Month };
        var cmbOwnDept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = deptDt.Clone(), DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbOwnDept);
        var cmbWorkDept = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = deptDt, DisplayMember = "顯示", ValueMember = "編號" };
        UiTheme.AutoWiden(cmbWorkDept);
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        cmbStaff.SelectedValueChanged += (s, e) =>
        {
            if (cmbStaff.SelectedValue is string id)
            {
                var rows = staffDt.Select($"[編號] = '{id.Replace("'", "''")}'");
                if (rows.Length > 0 && rows[0]["部門"] is string dept && dept.Length > 0)
                {
                    if (cmbOwnDept.SelectedValue is null) cmbOwnDept.SelectedValue = dept;
                    if (cmbWorkDept.SelectedValue is null) cmbWorkDept.SelectedValue = dept;
                }
            }
        };

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
        Row("出勤年度", numYear, 120);
        Row("出勤月份", numMonth, 120);
        Row("所屬部門", cmbOwnDept);
        Row("出勤部門", cmbWorkDept);

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
            numYear.Value = Convert.ToInt32(row["出勤年度"]);
            numMonth.Value = Convert.ToInt32(row["出勤月份"]);
            cmbOwnDept.SelectedValue = row["所屬部門"];
            cmbWorkDept.SelectedValue = row["出勤部門"];
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            if (cmbStaff.SelectedValue is null) { lblMsg.Text = "請選擇員工"; return; }
            result = new Dictionary<string, object?>
            {
                ["員工編號"] = cmbStaff.SelectedValue as string,
                ["出勤年度"] = (int)numYear.Value,
                ["出勤月份"] = (int)numMonth.Value,
                ["所屬部門"] = cmbOwnDept.SelectedValue as string,
                ["出勤部門"] = cmbWorkDept.SelectedValue as string,
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    public static Dictionary<string, object?>? ShowDetail(IWin32Window owner, DataRow? row)
    {
        using var dlg = new Form
        {
            Text = row is null ? "新增出缺明細" : "修改出缺明細",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(520, 660),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10F),
        };

        var cmbKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbKind.Items.AddRange(new object[] { "出勤", "加班", "特休", "事假", "病假", "公假", "喪假", "婚假", "產假", "曠職" });
        var numDay = new NumericUpDown { Minimum = 1, Maximum = 31, Value = 1 };
        var cmbWeek = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbWeek.Items.AddRange(星期);

        TextBox TimeBox(string val = "") => new() { Text = val, MaxLength = 4 };
        var txtOdIn = TimeBox("0800");
        var txtOdOut = TimeBox("1700");
        var cmbOdShift = ShiftBox();
        var txtEvIn = TimeBox();
        var txtEvOut = TimeBox();
        var cmbEvShift = ShiftBox();
        var txtNiIn = TimeBox();
        var txtNiOut = TimeBox();
        var cmbNiShift = ShiftBox();
        var txtDnIn = TimeBox();
        var txtDnOut = TimeBox();
        var cmbDnShift = ShiftBox();
        var lblMsg = new Label { Text = "", ForeColor = UiTheme.Danger, AutoSize = true };

        static ComboBox ShiftBox()
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(班別);
            return c;
        }

        int y = 16;
        void Row(string labelText, Control field, int width = 220)
        {
            dlg.Controls.Add(new Label { Text = labelText, Font = UiTheme.Font(9.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(24, y + 6) });
            field.Location = new Point(160, y);
            field.Width = width;
            dlg.Controls.Add(field);
            y += 38;
        }

        Row("出缺類別", cmbKind, 160);
        Row("日", numDay, 100);
        Row("星期", cmbWeek, 100);
        Row("常日上班(HHmm)", txtOdIn, 120);
        Row("常日下班(HHmm)", txtOdOut, 120);
        Row("常日班別", cmbOdShift, 120);
        Row("晚班上班(HHmm)", txtEvIn, 120);
        Row("晚班下班(HHmm)", txtEvOut, 120);
        Row("晚班別", cmbEvShift, 120);
        Row("小夜上班(HHmm)", txtNiIn, 120);
        Row("小夜下班(HHmm)", txtNiOut, 120);
        Row("小夜班別", cmbNiShift, 120);
        Row("大夜上班(HHmm)", txtDnIn, 120);
        Row("大夜下班(HHmm)", txtDnOut, 120);
        Row("大夜班別", cmbDnShift, 120);

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
            cmbKind.SelectedItem = row["出缺類別"] as string;
            numDay.Value = ParseDay(row["日"]);
            cmbWeek.SelectedItem = row["星期"] as string;
            txtOdIn.Text = Convert.ToString(row["常日上班"]);
            txtOdOut.Text = Convert.ToString(row["常日下班"]);
            cmbOdShift.SelectedItem = row["常日班別"] as string;
            txtEvIn.Text = Convert.ToString(row["晚班上班"]);
            txtEvOut.Text = Convert.ToString(row["晚班下班"]);
            cmbEvShift.SelectedItem = row["晚班別"] as string;
            txtNiIn.Text = Convert.ToString(row["小夜上班"]);
            txtNiOut.Text = Convert.ToString(row["小夜下班"]);
            cmbNiShift.SelectedItem = row["小夜班別"] as string;
            txtDnIn.Text = Convert.ToString(row["大夜上班"]);
            txtDnOut.Text = Convert.ToString(row["大夜下班"]);
            cmbDnShift.SelectedItem = row["大夜班別"] as string;
        }
        else
        {
            cmbKind.SelectedIndex = 0;
            cmbWeek.SelectedIndex = 0;
            cmbOdShift.SelectedIndex = 1;
            cmbEvShift.SelectedIndex = 0;
            cmbNiShift.SelectedIndex = 0;
            cmbDnShift.SelectedIndex = 0;
        }

        Dictionary<string, object?>? result = null;
        btnOk.Click += (s, e) =>
        {
            result = new Dictionary<string, object?>
            {
                ["出缺類別"] = cmbKind.SelectedItem?.ToString() ?? "出勤",
                ["日"] = ((int)numDay.Value).ToString("00"),
                ["星期"] = cmbWeek.SelectedItem?.ToString(),
                ["常日上班"] = ToClock(txtOdIn.Text),
                ["常日下班"] = ToClock(txtOdOut.Text),
                ["常日班別"] = cmbOdShift.SelectedItem?.ToString(),
                ["晚班上班"] = ToClock(txtEvIn.Text),
                ["晚班下班"] = ToClock(txtEvOut.Text),
                ["晚班別"] = cmbEvShift.SelectedItem?.ToString(),
                ["小夜上班"] = ToClock(txtNiIn.Text),
                ["小夜下班"] = ToClock(txtNiOut.Text),
                ["小夜班別"] = cmbNiShift.SelectedItem?.ToString(),
                ["大夜上班"] = ToClock(txtDnIn.Text),
                ["大夜下班"] = ToClock(txtDnOut.Text),
                ["大夜班別"] = cmbDnShift.SelectedItem?.ToString(),
            };
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();

        return dlg.ShowDialog(owner) == DialogResult.OK ? result : null;
    }

    private static int ToClock(string s)
    {
        s = s.Trim();
        if (s.Length == 0) return 0;
        if (int.TryParse(s, out var n)) return Math.Clamp(n, 0, 2359);
        return 0;
    }

    private static int ParseDay(object v)
    {
        if (int.TryParse(Convert.ToString(v), out var d) && d >= 1 && d <= 31) return d;
        return 1;
    }
}
