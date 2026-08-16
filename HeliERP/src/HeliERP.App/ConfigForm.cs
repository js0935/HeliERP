// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（UI/UX 精緻化升級）
// ════════════════════════════════════════════════════════
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>資料庫設定視窗：選擇資料庫檔（支援區網 UNC）、顯示公司資訊、測試連線</summary>
public class ConfigForm : Form
{
    private readonly DbConfig _config;
    private readonly TextBox _txtDbPath;
    private readonly TextBox _txtCompany;
    private readonly ModernButton _btnBrowse;
    private readonly ModernButton _btnTest;
    private readonly ModernButton _btnOk;
    private readonly ModernButton _btnCancel;
    private readonly Label _lblStatus;

    public ConfigForm(DbConfig config)
    {
        _config = config;
        Text = "資料庫設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(680, 384);
        UiTheme.Apply(this);

        // 標題列
        Controls.Add(UiTheme.BuildHeader("資料庫設定", "設定 HeliERP 資料庫連線"));

        // ── 卡片 1：資料庫路徑 ──
        var cardDb = new Panel { Location = new Point(UiTheme.SpacingXl, 76), Size = new Size(632, 80) };
        UiTheme.StyleCardPanel(cardDb);
        var lblDb = new Label { Text = "資料庫路徑：", AutoSize = true, Location = new Point(UiTheme.SpacingLg, UiTheme.SpacingLg + 4) };
        UiTheme.StyleLabel(lblDb);
        _txtDbPath = new TextBox
        {
            Location = new Point(110, UiTheme.SpacingLg),
            Size = new Size(360, 30),
            Text = _config.DatabasePath,
        };
        UiTheme.StyleTextBox(_txtDbPath);
        _btnBrowse = new ModernButton
        {
            Text = "瀏覽…",
            Size = new Size(80, 32),
            Location = new Point(480, UiTheme.SpacingLg),
            IsPrimary = false,
            DrawShadow = false,
        };
        _btnBrowse.Click += (s, e) => BrowseDb();
        cardDb.Controls.AddRange(new Control[] { lblDb, _txtDbPath, _btnBrowse });

        // ── 卡片 2：公司資訊 ──
        var cardCompany = new Panel { Location = new Point(UiTheme.SpacingXl, 168), Size = new Size(632, 148) };
        UiTheme.StyleCardPanel(cardCompany);
        var lblCompany = new Label { Text = "公司資訊：", AutoSize = true, Location = new Point(UiTheme.SpacingLg, UiTheme.SpacingLg + 4) };
        UiTheme.StyleLabel(lblCompany);
        _txtCompany = new TextBox
        {
            Location = new Point(110, UiTheme.SpacingLg),
            Size = new Size(430, 100),
            Multiline = true,
            ReadOnly = true,
            Text = CompanyText(),
        };
        UiTheme.StyleTextBox(_txtCompany, readOnly: true);
        cardCompany.Controls.AddRange(new Control[] { lblCompany, _txtCompany });

        // ── 按鈕列 ──
        _lblStatus = new Label
        {
            Text = "",
            AutoSize = true,
            Location = new Point(360, 14),
        };
        UiTheme.StyleLabel(_lblStatus, sub: true);

        _btnTest = new ModernButton { Text = "測試連線", Size = new Size(110, 40), Location = new Point(0, 0), IsPrimary = true };
        _btnTest.Click += (s, e) => TestDb();
        _btnOk = new ModernButton { Text = "確　定", Size = new Size(100, 40), Location = new Point(122, 0), IsPrimary = false };
        _btnOk.Click += (s, e) => OkClick();
        _btnCancel = new ModernButton { Text = "取　消", Size = new Size(100, 40), Location = new Point(232, 0), IsPrimary = false };
        _btnCancel.Click += (s, e) => Close();

        var btnRow = new Panel { Location = new Point(UiTheme.SpacingXl, 328), Size = new Size(632, 40) };
        btnRow.Controls.AddRange(new Control[] { _btnTest, _btnOk, _btnCancel, _lblStatus });

        Controls.AddRange(new Control[] { cardDb, cardCompany, btnRow });
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    private string CompanyText()
    {
        var c = _config.Company;
        if (string.IsNullOrWhiteSpace(c.CompanyName))
            return "尚未設定公司資料（請填寫，報表、登入畫面與主視窗標題將使用此資料）";
        return $"{c.CompanyName}（統一編號 {c.TaxId}）\r\n負責人：{c.Owner}　電話：{c.Phone}\r\n地址：{c.Address}\r\nEmail：{c.Email}";
    }

    private void BrowseDb()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "選擇資料庫檔（HeliERP.db）",
            Filter = "SQLite 資料庫 (*.db)|*.db|所有檔案 (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtDbPath.Text = dlg.FileName;
    }

    private void TestDb()
    {
        var path = _txtDbPath.Text.Trim();
        if (DbConfig.TestConnection(path))
        {
            _lblStatus.ForeColor = UiTheme.Ok;
            _lblStatus.Text = "連線成功：資料庫正常";
        }
        else
        {
            _lblStatus.ForeColor = UiTheme.Danger;
            _lblStatus.Text = "連線失敗：請確認路徑與檔案";
        }
    }

    private void OkClick()
    {
        var path = _txtDbPath.Text.Trim();
        if (!DbConfig.TestConnection(path))
        {
            _lblStatus.ForeColor = UiTheme.Danger;
            _lblStatus.Text = "資料庫無法連線，請先測試成功";
            return;
        }
        _config.DatabasePath = path;
        _config.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}