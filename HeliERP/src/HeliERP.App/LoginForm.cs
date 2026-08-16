// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing.Drawing2D;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>登入視窗：以「權限主檔」表的使用者編號/密碼驗證</summary>
public class LoginForm : Form
{
    private readonly DbConfig _config;
    private readonly float _scale;
    private readonly TextBox _txtUserId;
    private readonly TextBox _txtPassword;
    private readonly ComboBox _cmbDatabase;
    private readonly ModernButton _btnBrowse;
    private readonly ModernButton _btnLogin;
    private readonly ModernButton _btnCancel;
    private readonly Label _lblMessage;
    private readonly List<DbOption> _dbOptions = new();
    private bool _loadingDatabases;

    /// <summary>登入成功後的使用者資料</summary>
    public AppUser? LoggedInUser { get; private set; }

    /// <summary>資料庫下拉選項：顯示檔名、保留完整路徑</summary>
    private sealed class DbOption
    {
        public string Path { get; }
        public string Display { get; }
        public DbOption(string path)
        {
            Path = path;
            Display = System.IO.Path.GetFileName(path);
        }
    }

    public LoginForm(DbConfig config)
    {
        _config = config;
        SecurityService.EnsureLockTable();
        Text = "HeliERP - 登入";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(760, 600);
        BackColor = Color.White;
        Font = UiTheme.Font(11F);
        DoubleBuffered = true;

        const int cardX = 400;   // 登入卡片內容起點
        const int fieldW = 300;  // 輸入框寬度

        // 標題
        var lblWelcome = new Label
        {
            Text = "歡迎登入",
            Font = UiTheme.Font(20F, FontStyle.Bold),
            ForeColor = UiTheme.PrimaryDark,
            AutoSize = true,
            Location = new Point(cardX, 56),
        };
        var lblHint = new Label
        {
            Text = "請輸入您的使用者編號與密碼",
            Font = UiTheme.Font(10F),
            ForeColor = UiTheme.TextSub,
            AutoSize = true,
            Location = new Point(cardX, 100),
        };

        // 輸入欄位
        var lblUser = new Label { Text = "使用者編號", Font = UiTheme.Font(10.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(cardX, 150) };
        var lblPass = new Label { Text = "密　　碼", Font = UiTheme.Font(10.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(cardX, 232) };

        _txtUserId = MakeTextBox(_config.LastUserId, cardX, 180, fieldW);
        _txtPassword = MakeTextBox("", cardX, 262, fieldW);
        _txtPassword.UseSystemPasswordChar = true;
        _txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoLogin(); };

        // 資料庫選擇
        var lblDb = new Label { Text = "資料庫", Font = UiTheme.Font(10.5F), ForeColor = UiTheme.TextMain, AutoSize = true, Location = new Point(cardX, 302) };

        _cmbDatabase = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(cardX, 326),
            Size = new Size(208, 34),
            DropDownWidth = 480,
        };
        UiTheme.StyleComboBox(_cmbDatabase);
        _cmbDatabase.SelectedIndexChanged += OnDatabaseChanged;

        _btnBrowse = new ModernButton
        {
            Text = "瀏覽…",
            Size = new Size(86, 34),
            Location = new Point(cardX + 214, 326),
            IsPrimary = false,
            DrawShadow = false,
        };
        _btnBrowse.Click += (s, e) => BrowseDatabase();

        _lblMessage = new Label
        {
            Text = "",
            ForeColor = UiTheme.Danger,
            Font = UiTheme.Font(10F),
            AutoSize = true,
            Location = new Point(cardX, 372),
        };

        // 按鈕
        _btnLogin = new ModernButton
        {
            Text = "登　入",
            Size = new Size(170, 46),
            Location = new Point(cardX, 404),
            IsPrimary = true,
        };
        _btnLogin.Click += (s, e) => DoLogin();

        _btnCancel = new ModernButton
        {
            Text = "取　消",
            Size = new Size(90, 46),
            Location = new Point(cardX + 186, 404),
            IsPrimary = false,
            DrawShadow = false,
        };
        _btnCancel.Click += (s, e) => Close();

        var lblCredit1 = new Label
        {
            Text = "軟體屬名：禾秝軟體開發團隊",
            Font = UiTheme.Font(8.5F),
            ForeColor = UiTheme.TextFaint,
            AutoSize = true,
            Location = new Point(cardX, 488),
        };
        var lblCredit2 = new Label
        {
            Text = "代碼：洪俊士　版本：1.0.0",
            Font = UiTheme.Font(8.5F),
            ForeColor = UiTheme.TextFaint,
            AutoSize = true,
            Location = new Point(cardX, 512),
        };

        Controls.AddRange(new Control[] { lblWelcome, lblHint, lblUser, lblPass, _txtUserId, _txtPassword, lblDb, _cmbDatabase, _btnBrowse, _lblMessage, _btnLogin, _btnCancel, lblCredit1, lblCredit2 });
        AcceptButton = _btnLogin;
        CancelButton = _btnCancel;

        LoadDatabases();

        // 依螢幕工作區動態縮放：避免 200% 縮放的小邏輯螢幕（如 1128×704）下視窗超出螢幕被裁切
        var wa = Screen.PrimaryScreen!.WorkingArea;
        _scale = Math.Min(2f, Math.Min(wa.Width / 760f, wa.Height / 600f));
        Scale(new SizeF(_scale, _scale));
        ClientSize = new Size((int)(760 * _scale), (int)(600 * _scale));
    }

    /// <summary>建立扁平風輸入框</summary>
    private static TextBox MakeTextBox(string text, int x, int y, int w)
    {
        var tb = new TextBox
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, 34),
        };
        UiTheme.StyleTextBox(tb);
        return tb;
    }

    /// <summary>填滿資料庫下拉：掃描執行檔目錄、目前資料庫所在目錄與使用歷史</summary>
    private void LoadDatabases()
    {
        _loadingDatabases = true;
        try
        {
            var list = _config.FindDatabases().Select(p => new DbOption(p)).ToList();
            _dbOptions.Clear();
            _dbOptions.AddRange(list);
            _cmbDatabase.DataSource = null;
            _cmbDatabase.DataSource = _dbOptions;
            _cmbDatabase.DisplayMember = "Display";
            _cmbDatabase.ValueMember = "Path";

            var current = System.IO.Path.GetFullPath(_config.DatabasePath);
            var match = list.FirstOrDefault(o => string.Equals(o.Path, current, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                _cmbDatabase.SelectedItem = match;
        }
        finally
        {
            _loadingDatabases = false;
        }
    }

    /// <summary>使用者切換資料庫：驗證連線後立即切換 DbManager 目標</summary>
    private void OnDatabaseChanged(object? sender, EventArgs e)
    {
        if (_loadingDatabases || _cmbDatabase.SelectedItem is not DbOption opt)
            return;
        var path = opt.Path;
        var current = DbManager.DatabasePath;
        if (string.Equals(path, current, StringComparison.OrdinalIgnoreCase))
            return;

        if (!DbConfig.TestConnection(path))
        {
            _lblMessage.ForeColor = UiTheme.Danger;
            _lblMessage.Text = $"無法連線資料庫「{System.IO.Path.GetFileName(path)}」，已保留原設定";
            _loadingDatabases = true;
            try
            {
                _cmbDatabase.SelectedItem = _dbOptions
                    .FirstOrDefault(o => string.Equals(o.Path, current, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _loadingDatabases = false;
            }
            return;
        }

        DbManager.DatabasePath = path;
        _lblMessage.ForeColor = UiTheme.Ok;
        _lblMessage.Text = $"已切換資料庫：{System.IO.Path.GetFileName(path)}";
    }

    /// <summary>以檔案對話框挑選資料庫檔</summary>
    private void BrowseDatabase()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "選擇資料庫檔",
            Filter = "SQLite 資料庫 (*.db)|*.db|所有檔案 (*.*)|*.*",
            InitialDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(DbManager.DatabasePath)),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        var path = System.IO.Path.GetFullPath(dlg.FileName);
        if (!DbConfig.TestConnection(path))
        {
            _lblMessage.ForeColor = UiTheme.Danger;
            _lblMessage.Text = "無法連線此資料庫";
            return;
        }

        _loadingDatabases = true;
        try
        {
            var existing = _dbOptions
                .FirstOrDefault(o => string.Equals(o.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new DbOption(path);
                _dbOptions.Add(existing);
                _cmbDatabase.DataSource = null;
                _cmbDatabase.DataSource = _dbOptions;
                _cmbDatabase.DisplayMember = "Display";
                _cmbDatabase.ValueMember = "Path";
            }
            _cmbDatabase.SelectedItem = existing;
        }
        finally
        {
            _loadingDatabases = false;
        }

        if (!string.Equals(DbManager.DatabasePath, path, StringComparison.OrdinalIgnoreCase))
        {
            DbManager.DatabasePath = path;
            _lblMessage.ForeColor = UiTheme.Ok;
            _lblMessage.Text = $"已切換資料庫：{System.IO.Path.GetFileName(path)}";
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.ScaleTransform(_scale, _scale);
        const float designH = 600f;

        // 左側品牌區：深藍漸層
        using (var left = new LinearGradientBrush(
                   new RectangleF(0, 0, 340, designH),
                   UiTheme.PrimaryDark, UiTheme.PrimaryLight, LinearGradientMode.Vertical))
        {
            g.FillRectangle(left, 0, 0, 340, designH);
        }

        // 右側白底
        g.FillRectangle(Brushes.White, 340, 0, 420, designH);

        UiTheme.DrawCard(g, new Rectangle(360, 32, 380, 536), UiTheme.RadiusLg);

        // 金色分隔線
        using (var accent = new SolidBrush(UiTheme.Accent))
            g.FillRectangle(accent, 337, 0, 3, designH);

        // 品牌文字（DrawString 向量字形會隨 ScaleTransform 正確放大）
        var name = _config.Company.CompanyName;
        var brand = string.IsNullOrWhiteSpace(name) ? "HeliERP" : name;
        var sub = string.IsNullOrWhiteSpace(name) ? "企業資源規劃系統"
            : name.EndsWith("有限公司") ? "" : "有限公司";
        g.DrawString(brand, UiTheme.Font(20F, FontStyle.Bold),
            new SolidBrush(Color.White), new RectangleF(40, 150, 260, 62));
        if (sub.Length > 0)
            g.DrawString(sub, UiTheme.Font(12F),
                new SolidBrush(Color.FromArgb(200, 255, 255, 255)), new RectangleF(40, 212, 260, 26));
        using (var accent = new SolidBrush(UiTheme.Accent))
            g.FillRectangle(accent, 42, 254, 60, 3);
        g.DrawString("企業資源規劃系統 ERP", UiTheme.Font(13F),
            new SolidBrush(Color.FromArgb(220, 255, 255, 255)), new RectangleF(40, 274, 260, 30));
        g.DrawString("安全 ・ 效率 ・ 專業", UiTheme.Font(10.5F),
            new SolidBrush(Color.FromArgb(170, 255, 255, 255)), new RectangleF(40, 360, 260, 26));
    }

    private void DoLogin()
    {
        var userId = _txtUserId.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
        {
            _lblMessage.Text = "請輸入使用者編號與密碼";
            return;
        }

        // 鎖定檢查：連續失敗達上限後，於鎖定期間內禁止登入
        var (_, lockUntil) = SecurityService.GetLockState(userId);
        if (SecurityService.IsLocked(lockUntil, out var remaining))
        {
            AuditService.LogLogin(userId, false, "帳號鎖定期間拒絕登入");
            _lblMessage.Text = $"帳號暫時鎖定，請於 {remaining.Minutes + 1} 分鐘後再試";
            return;
        }

        // 資料庫防呆：確認目前的資料庫有權限主檔，避免選錯資料庫後無法運作
        if (!DbConfig.HasLoginTable(DbManager.DatabasePath))
        {
            _lblMessage.ForeColor = UiTheme.Danger;
            _lblMessage.Text = "此資料庫沒有權限主檔，無法登入，請選擇正確的資料庫";
            return;
        }

        DataTable dt;
        try
        {
            dt = DbManager.QueryTable(
                "SELECT [使用者編號], [使用者名稱], [使用者密碼], [員工編號], [成本權限], [售價權限] " +
                "FROM [權限主檔] WHERE [使用者編號] = $id",
                DbManager.Param("$id", userId));
        }
        catch (Exception ex)
        {
            _lblMessage.Text = "資料庫讀取失敗：" + ex.Message;
            return;
        }

        if (dt.Rows.Count == 0)
        {
            AuditService.LogLogin(userId, false, "使用者編號不存在");
            _lblMessage.Text = "使用者編號不存在";
            return;
        }

        var row = dt.Rows[0];
        var storedPwd = row["使用者密碼"] as string ?? "";
        if (!SecurityService.VerifyPassword(storedPwd, password))
        {
            var (failures, _) = SecurityService.GetLockState(userId);
            var next = failures + 1;
            SecurityService.RecordFailure(userId, next);
            AuditService.LogLogin(userId, false, $"密碼錯誤第 {next} 次" +
                (next >= SecurityService.MaxFailures ? "（已鎖定）" : ""));
            if (next >= SecurityService.MaxFailures)
                _lblMessage.Text = $"密碼錯誤已達 {SecurityService.MaxFailures} 次，帳號暫時鎖定 {SecurityService.LockDuration.TotalMinutes:0} 分鐘";
            else
                _lblMessage.Text = $"密碼錯誤（第 {next}/{SecurityService.MaxFailures} 次）";
            return;
        }

        SecurityService.ClearLock(userId);

        // 舊明文密碼登入成功後自動升級為雜湊儲存（2026 資安強化）
        if (!SecurityService.IsHashed(storedPwd))
        {
            try
            {
                DbManager.ExecuteNonQuery(
                    "UPDATE [權限主檔] SET [使用者密碼] = $p WHERE [使用者編號] = $id",
                    DbManager.Param("$p", SecurityService.HashPassword(password)),
                    DbManager.Param("$id", userId));
            }
            catch
            {
                // 遷移失敗不阻擋登入
            }
        }

        AuditService.LogLogin(userId, true);

        LoggedInUser = new AppUser
        {
            UserId = userId,
            DisplayName = row["使用者名稱"] as string ?? userId,
            EmployeeId = row["員工編號"] as string,
            CanViewCost = ConvertToBool(row["成本權限"]),
            CanViewPrice = ConvertToBool(row["售價權限"]),
            IsAdmin = userId.Equals("karahui", StringComparison.OrdinalIgnoreCase)
                      || userId.Equals("hr", StringComparison.OrdinalIgnoreCase),
        };

        _config.LastUserId = userId;
        _config.DatabasePath = DbManager.DatabasePath;
        var history = _config.DatabaseHistory ??= new List<string>();
        history.RemoveAll(p => string.Equals(p, DbManager.DatabasePath, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, DbManager.DatabasePath);
        if (history.Count > 10) history.RemoveRange(10, history.Count - 10);
        _config.Save();
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool ConvertToBool(object value)
    {
        if (value is bool b) return b;
        if (value is long l) return l != 0;
        if (value is double d) return d != 0;
        if (value is string s) return s == "1" || s.Equals("True", StringComparison.OrdinalIgnoreCase);
        return false;
    }
}
