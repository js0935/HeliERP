// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>第一次使用強制設定的公司基本資料視窗（登入後公司資料未填寫時顯示）</summary>
public class FirstRunSetupForm : Form
{
    private readonly DbConfig _config;
    private readonly TextBox _txtName;
    private readonly TextBox _txtTaxId;
    private readonly TextBox _txtOwner;
    private readonly TextBox _txtPhone;
    private readonly TextBox _txtEmail;
    private readonly TextBox _txtAddress;
    private readonly TextBox _txtWebsite;
    private readonly Label _lblMsg;

    public FirstRunSetupForm(DbConfig config)
    {
        _config = config;
        Text = "基本資料設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(680, 392);
        UiTheme.Apply(this);

        Controls.Add(UiTheme.BuildHeader("基本資料設定", "首次使用請填寫公司基本資料（公司名稱必填）"));

        // ── 基本資料卡片 ──
        var card = new Panel { Location = new Point(UiTheme.SpacingXl, UiTheme.SpacingXl + 4), Size = new Size(632, 306) };
        UiTheme.StyleCardPanel(card);

        var c = _config.Company;
        int y = UiTheme.SpacingLg;
        _txtName = new TextBox { Text = c.CompanyName };
        _txtTaxId = new TextBox { Text = c.TaxId };
        _txtOwner = new TextBox { Text = c.Owner };
        _txtPhone = new TextBox { Text = c.Phone };
        _txtEmail = new TextBox { Text = c.Email };
        _txtAddress = new TextBox { Text = c.Address };
        _txtWebsite = new TextBox { Text = c.Website };

        foreach (var (label, tb) in new (string, TextBox)[]
        {
            ("公司名稱 *", _txtName),
            ("統一編號", _txtTaxId),
            ("負責人", _txtOwner),
            ("聯絡電話", _txtPhone),
            ("電子郵件", _txtEmail),
            ("登記地址", _txtAddress),
            ("網址", _txtWebsite),
        })
        {
            var lbl = new Label { Text = label, AutoSize = true, Location = new Point(UiTheme.SpacingLg, y + 6) };
            UiTheme.StyleLabel(lbl);
            tb.Location = new Point(120, y);
            tb.Size = new Size(440, 30);
            UiTheme.StyleTextBox(tb);
            card.Controls.AddRange(new Control[] { lbl, tb });
            y += 38;
        }
        card.Controls.Add(new Label
        {
            Text = "* 為必填欄位；設定後可隨時於「系統設定」修改。",
            AutoSize = true,
            Location = new Point(UiTheme.SpacingLg, y + 2),
            ForeColor = UiTheme.TextSub,
        });

        // ── 按鈕列 ──
        _lblMsg = new Label { Text = "", AutoSize = true, Location = new Point(360, 14) };
        UiTheme.StyleLabel(_lblMsg, sub: true);

        var btnOk = new ModernButton { Text = "確　定", Size = new Size(100, 40), Location = new Point(0, 0), IsPrimary = true };
        btnOk.Click += (s, e) => OkClick();
        var btnCancel = new ModernButton { Text = "取　消", Size = new Size(100, 40), Location = new Point(112, 0), IsPrimary = false };
        btnCancel.Click += (s, e) => Close();

        var btnRow = new Panel { Location = new Point(UiTheme.SpacingXl, 346), Size = new Size(632, 40) };
        btnRow.Controls.AddRange(new Control[] { btnOk, btnCancel, _lblMsg });

        Controls.AddRange(new Control[] { card, btnRow });
    }

    private void OkClick()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            _lblMsg.ForeColor = UiTheme.Danger;
            _lblMsg.Text = "請輸入公司名稱";
            _txtName.Focus();
            return;
        }

        var c = _config.Company;
        c.CompanyName = _txtName.Text.Trim();
        c.TaxId = _txtTaxId.Text.Trim();
        c.Owner = _txtOwner.Text.Trim();
        c.Phone = _txtPhone.Text.Trim();
        c.Email = _txtEmail.Text.Trim();
        c.Address = _txtAddress.Text.Trim();
        c.Website = _txtWebsite.Text.Trim();
        _config.Save();

        DialogResult = DialogResult.OK;
        Close();
    }
}
