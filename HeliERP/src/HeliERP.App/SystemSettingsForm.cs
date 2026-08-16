// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>系統設定中心：帳號權限、公司資料與密碼管理。</summary>
public sealed class SystemSettingsForm : Form
{
    private readonly DbConfig _config;
    private readonly AppUser _user;

    public SystemSettingsForm(DbConfig config, AppUser user)
    {
        _config = config;
        _user = user;
        UiTheme.Apply(this);
        Text = "系統設定";
        Size = new Size(760, 600);
        MinimumSize = new Size(600, 420);
        BackColor = UiTheme.Background;
        StartPosition = FormStartPosition.CenterScreen;

        var root = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
        root.Controls.Add(UiTheme.BuildHeader("系統設定", "帳號權限、公司資料與密碼管理"));

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(30, 12, 30, 20),
        };

        AddSection(flow, "帳號權限");
        AddItem(flow, "權限主檔", "使用者帳號 / 登入權限設定", () => new GenericTableForm("權限主檔").ShowDialog(this));
        AddItem(flow, "變更密碼", "目前登入使用者的密碼變更", () => ShowChangePassword(this, _user));

        AddSection(flow, "公司與資料庫");
        AddItem(flow, "公司資料與資料庫", "公司名稱 / 統一編號 / 資料庫位置", () =>
        {
            using var f = new ConfigForm(_config);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                DbManager.DatabasePath = _config.DatabasePath;
                SchemaReader.Reload();
            }
        });

        AddSection(flow, "資料備份");
        AddItem(flow, "立即備份", "建立目前資料庫的一致性快照（不中斷作業）", () =>
        {
            using var dlg = new SaveFileDialog
            {
                Title = "備份資料庫",
                FileName = BackupService.NewBackupName(DateTime.Now),
                InitialDirectory = BackupService.DefaultBackupDir(),
                Filter = "備份檔 (*.bak)|*.bak|SQLite 資料庫 (*.db)|*.db",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                BackupService.BackupTo(dlg.FileName);
                MessageBox.Show(this, $"備份完成：\n{dlg.FileName}", "資料備份",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "備份失敗：" + ex.Message, "資料備份",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        AddItem(flow, "還原備份", "以備份檔覆蓋目前資料庫（還原後重新啟動程式）", () =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "還原資料庫備份",
                InitialDirectory = BackupService.DefaultBackupDir(),
                Filter = "備份檔 (*.bak;*.db)|*.bak;*.db",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;
            var confirm = MessageBox.Show(this,
                "還原會以備份檔覆蓋目前的資料庫，尚未備份的資料將遺失。\n確定要繼續嗎？",
                "還原資料庫", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;
            try
            {
                BackupService.RestoreFrom(dlg.FileName);
                MessageBox.Show(this, "還原完成，程式即將重新啟動。", "還原資料庫",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Restart();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "還原失敗：" + ex.Message, "還原資料庫",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        var autoBox = new Panel { Size = new Size(660, 40), Margin = new Padding(2, 3, 2, 3), BackColor = Color.Transparent };
        var chkAuto = new CheckBox
        {
            Text = "啟動時自動備份（每天最多一份）",
            Font = UiTheme.Font(10.5F),
            ForeColor = UiTheme.TextMain,
            Checked = _config.AutoBackup,
            AutoSize = true,
            Location = new Point(2, 2),
        };
        var lblKeep = new Label
        {
            Text = "保留份數：",
            Font = UiTheme.Font(10.5F),
            ForeColor = UiTheme.TextMain,
            AutoSize = true,
            Location = new Point(270, 4),
        };
        var numKeep = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 99,
            Value = Math.Clamp(_config.BackupRetention, 1, 99),
            Width = 60,
            Location = new Point(350, 0),
        };
        chkAuto.CheckedChanged += (s, e) => { _config.AutoBackup = chkAuto.Checked; _config.Save(); };
        numKeep.ValueChanged += (s, e) => { _config.BackupRetention = (int)numKeep.Value; _config.Save(); };
        autoBox.Controls.AddRange(new Control[] { chkAuto, lblKeep, numKeep });
        flow.Controls.Add(autoBox);

        root.Controls.Add(flow);
        Controls.Add(root);
        UiTheme.ClampToScreen(this);
    }

    private static void AddSection(FlowLayoutPanel flow, string title)
    {
        flow.Controls.Add(new Label
        {
            Text = title,
            Font = UiTheme.Font(12.5F, FontStyle.Bold),
            ForeColor = UiTheme.AccentDark,
            AutoSize = true,
            Margin = new Padding(2, 16, 0, 6),
        });
    }

    private void AddItem(FlowLayoutPanel flow, string name, string desc, Action open)
    {
        var box = new Panel
        {
            Size = new Size(660, 64),
            Margin = new Padding(2, 3, 2, 3),
            BackColor = Color.Transparent,
        };
        var btn = new ModernButton
        {
            Text = name,
            IsPrimary = false,
            Font = UiTheme.Font(11F, FontStyle.Bold),
            Size = new Size(660, 44),
            Location = new Point(0, 0),
            CornerRadius = 7,
        };
        btn.Click += (s, e) => open();
        var lblDesc = new Label
        {
            Text = desc,
            Font = UiTheme.Font(9F),
            ForeColor = UiTheme.TextFaint,
            AutoSize = true,
            Location = new Point(4, 47),
        };
        box.Controls.Add(btn);
        box.Controls.Add(lblDesc);
        flow.Controls.Add(box);
    }

    /// <summary>變更目前登入使用者的密碼（系統選單與系統設定共用）</summary>
    public static void ShowChangePassword(IWin32Window owner, AppUser user)
    {
        using var dlg = new Form
        {
            Text = "變更密碼",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(380, 264),
            BackColor = UiTheme.Background,
            Font = UiTheme.Font(10.5F),
        };

        var lblOld = new Label { Text = "目前密碼", Font = UiTheme.Font(10F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(22, 14) };
        var txtOld = MakePwdBox(22, 36, 336);
        var lblNew = new Label { Text = "新密碼（至少 8 碼，含字母與數字）", Font = UiTheme.Font(10F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(22, 78) };
        var txtNew = MakePwdBox(22, 100, 336);
        var lblAgain = new Label { Text = "確認新密碼", Font = UiTheme.Font(10F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(22, 142) };
        var txtAgain = MakePwdBox(22, 164, 336);

        var lblMsg = new Label { Text = "", Font = UiTheme.Font(9.5F), ForeColor = UiTheme.Danger, AutoSize = true, Location = new Point(22, 202) };

        var btnOk = new ModernButton { Text = "確定", Size = new Size(96, 40), Location = new Point(154, 214), IsPrimary = true };
        var btnCancel = new ModernButton { Text = "取消", Size = new Size(80, 40), Location = new Point(260, 214), IsPrimary = false, DrawShadow = false };

        btnOk.Click += (s, e) =>
        {
            lblMsg.Text = "";
            if (!SecurityService.CheckPasswordStrength(txtNew.Text, out var pwdError)) { lblMsg.Text = pwdError; return; }
            if (txtNew.Text != txtAgain.Text) { lblMsg.Text = "兩次輸入的新密碼不一致"; return; }

            DataTable dt;
            try
            {
                dt = DbManager.QueryTable(
                    "SELECT [使用者密碼] FROM [權限主檔] WHERE [使用者編號] = $id",
                    DbManager.Param("$id", user.UserId));
            }
            catch (Exception ex)
            {
                lblMsg.Text = "資料庫讀取失敗：" + ex.Message;
                return;
            }
            if (dt.Rows.Count == 0) { lblMsg.Text = "找不到使用者資料"; return; }
            if (!SecurityService.VerifyPassword(dt.Rows[0]["使用者密碼"] as string ?? "", txtOld.Text))
            {
                lblMsg.Text = "目前密碼錯誤";
                return;
            }

            try
            {
                DbManager.ExecuteNonQuery(
                    "UPDATE [權限主檔] SET [使用者密碼] = $pwd WHERE [使用者編號] = $id",
                    DbManager.Param("$pwd", SecurityService.HashPassword(txtNew.Text)),
                    DbManager.Param("$id", user.UserId));
            }
            catch (Exception ex)
            {
                lblMsg.Text = "密碼更新失敗：" + ex.Message;
                return;
            }
            AuditService.Log(AuditService.變更密碼, "系統設定", user.UserId, "成功", $"使用者 {user.DisplayName} 變更密碼");
            dlg.DialogResult = DialogResult.OK;
        };
        btnCancel.Click += (s, e) => dlg.Close();
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        dlg.Controls.AddRange(new Control[] { lblOld, txtOld, lblNew, txtNew, lblAgain, txtAgain, lblMsg, btnOk, btnCancel });
        UiTheme.ClampToScreen(dlg);
        if (dlg.ShowDialog(owner) == DialogResult.OK)
            MessageBox.Show(owner, "密碼已變更，下次登入請使用新密碼。", "變更密碼", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static TextBox MakePwdBox(int x, int y, int w)
    {
        var tb = new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(w, 32),
            UseSystemPasswordChar = true,
        };
        UiTheme.StyleTextBox(tb);
        return tb;
    }
}
