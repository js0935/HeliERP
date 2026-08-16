// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（稽核日誌與資安）
// ════════════════════════════════════════════════════════
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 稽核日誌：記錄登入／登出／失敗嘗試與關鍵單據異動軌跡，供管理員檢視與查核。
/// 寫入採獨立連線且失敗不影響主流程；僅記帳號、事件、模組、對象、結果與詳細。
/// 2026 資安強化：登入失敗、帳號鎖定、密碼變更、單據存刪皆留下可稽核紀錄。
/// </summary>
public static class AuditService
{
    /// <summary>目前登入帳號（由程式進入點設定）。</summary>
    public static string CurrentAccount { get; set; } = "";

    /// <summary>目前登入使用者顯示名。</summary>
    public static string CurrentUser { get; set; } = "";

    public static string MachineName => Environment.MachineName;

    // ── 事件類型 ──
    public const string 登入成功 = "登入成功";
    public const string 登入失敗 = "登入失敗";
    public const string 登出 = "登出";
    public const string 存檔 = "存檔";
    public const string 刪除 = "刪除";
    public const string 變更密碼 = "變更密碼";
    public const string 系統 = "系統";

    /// <summary>確認稽核日誌表存在（缺則建立）。</summary>
    public static void EnsureSchema()
    {
        using var conn = DbManager.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE IF NOT EXISTS [稽核日誌] (" +
            "[序號] INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "[時間] TEXT NOT NULL, [帳號] TEXT, [使用者] TEXT, [機器] TEXT, " +
            "[事件] TEXT NOT NULL, [模組] TEXT, [對象] TEXT, [結果] TEXT, [詳細] TEXT)";
        cmd.ExecuteNonQuery();
        using (var idx = conn.CreateCommand())
        {
            idx.CommandText = "CREATE INDEX IF NOT EXISTS [IX_稽核日誌_時間] ON [稽核日誌]([時間])";
            idx.ExecuteNonQuery();
        }
    }

    /// <summary>寫入一筆稽核紀錄（失敗不拋出，避免影響主流程）。</summary>
    public static void Log(string 事件, string 模組, string 對象, string 結果 = "成功", string? 詳細 = null)
    {
        try
        {
            EnsureSchema();
            using var conn = DbManager.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO [稽核日誌] ([時間],[帳號],[使用者],[機器],[事件],[模組],[對象],[結果],[詳細]) " +
                "VALUES ($t,$a,$u,$m,$e,$mo,$o,$r,$d)";
            cmd.Parameters.Add(DbManager.Param("$t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            cmd.Parameters.Add(DbManager.Param("$a", CurrentAccount));
            cmd.Parameters.Add(DbManager.Param("$u", CurrentUser));
            cmd.Parameters.Add(DbManager.Param("$m", MachineName));
            cmd.Parameters.Add(DbManager.Param("$e", 事件));
            cmd.Parameters.Add(DbManager.Param("$mo", 模組));
            cmd.Parameters.Add(DbManager.Param("$o", Nz(對象)));
            cmd.Parameters.Add(DbManager.Param("$r", 結果));
            cmd.Parameters.Add(DbManager.Param("$d", Nz(詳細)));
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // 稽核寫入失敗不阻擋業務流程
        }
    }

    /// <summary>登入相關記錄（登入畫面使用者尚未登入，帳號由參數帶入）。</summary>
    public static void LogLogin(string 帳號, bool ok, string? 詳細 = null)
    {
        var backup = CurrentAccount;
        CurrentAccount = 帳號;
        CurrentUser = 帳號;
        Log(ok ? 登入成功 : 登入失敗, "登入", 帳號, ok ? "成功" : "失敗", 詳細);
        CurrentAccount = backup;
    }

    private static string? Nz(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
