// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>程式進入點：載入設定 → 確認資料庫 → 登入 → 主視窗</summary>
static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
            MessageBox.Show($"發生未預期錯誤：{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);

        var config = DbConfig.Load();
        DbManager.DatabasePath = config.DatabasePath;

        // 資料庫無法連線時不中斷啟動：先嘗試自動改用候選資料庫，
        // 若無候選則直接進入登入畫面，由使用者從下拉清單或瀏覽選擇資料庫
        if (!DbConfig.TestConnection(DbManager.DatabasePath))
        {
            var alt = config.FindDatabases().FirstOrDefault(DbConfig.HasLoginTable);
            if (alt is not null)
            {
                DbManager.DatabasePath = alt;
                config.DatabasePath = alt;
            }
        }

        // 登入（登入畫面可選擇資料庫；登入成功會回寫 config.DatabasePath）
        using var login = new LoginForm(config);
        if (login.ShowDialog() != DialogResult.OK)
            return;
        SchemaReader.Reload();

        var user = login.LoggedInUser!;

        // 第一次使用：公司基本資料未設定時強制填寫，未完成即結束程式
        if (string.IsNullOrWhiteSpace(config.Company.CompanyName))
        {
            using var setup = new FirstRunSetupForm(config);
            if (setup.ShowDialog() != DialogResult.OK)
                return;
        }

        // 主視窗
        Application.Run(new MainForm(config, user));
    }
}
