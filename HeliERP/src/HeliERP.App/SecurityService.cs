// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Security.Cryptography;
using System.Text;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 登入安全服務：連續失敗鎖定（持久化於「登入鎖定」表）、密碼強度規則與
/// SHA-256 加鹽雜湊（2026 資安強化；既有明文密碼於登入成功時自動遷移）。
/// </summary>
public static class SecurityService
{
    /// <summary>允許的最大連續失敗次數。</summary>
    public const int MaxFailures = 5;

    /// <summary>鎖定期間。</summary>
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

    private const string HashPrefix = "$sha256$";

    // ── 密碼雜湊（遷移式：舊明文登入成功後自動升級）──

    /// <summary>以隨機鹽雜湊密碼，格式：$sha256$鹽$雜湊。</summary>
    public static string HashPassword(string pwd)
    {
        var salt = Guid.NewGuid().ToString("N")[..16];
        return HashPrefix + salt + "$" + ComputeHash(salt, pwd);
    }

    /// <summary>判斷儲存值是否已為雜湊格式（未雜湊 = 舊明文）。</summary>
    public static bool IsHashed(string stored) => stored.StartsWith(HashPrefix);

    /// <summary>驗證密碼：雜湊格式用 SHA-256 比對；舊明文格式直接比對（相容遷移）。</summary>
    public static bool VerifyPassword(string stored, string pwd)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        if (!IsHashed(stored)) return string.Equals(stored, pwd, StringComparison.Ordinal);
        var parts = stored[HashPrefix.Length..].Split('$');
        if (parts.Length != 2) return false;
        var computed = ComputeHash(parts[0], pwd);
        return FixedTimeEquals(computed, parts[1]);
    }

    private static string ComputeHash(string salt, string pwd)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(salt + ":" + pwd);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }

    /// <summary>確保「登入鎖定」表存在（失敗不拋例外，僅跳過）。</summary>
    public static void EnsureLockTable()
    {
        try
        {
            DbManager.ExecuteNonQuery(
                "CREATE TABLE IF NOT EXISTS [登入鎖定] (" +
                "[使用者編號] TEXT PRIMARY KEY, [失敗次數] INTEGER NOT NULL DEFAULT 0, [鎖定至] TEXT)");
        }
        catch
        {
            // 無權限建表時不影響登入主流程
        }
    }

    /// <summary>取得目前失敗次數與鎖定截止時間。</summary>
    public static (int Failures, DateTime? LockUntil) GetLockState(string userId)
    {
        try
        {
            var dt = DbManager.QueryTable(
                "SELECT [失敗次數], [鎖定至] FROM [登入鎖定] WHERE [使用者編號] = $id",
                DbManager.Param("$id", userId));
            if (dt.Rows.Count == 0) return (0, null);
            var row = dt.Rows[0];
            var failures = Convert.ToInt32(row["失敗次數"]);
            var raw = Convert.ToString(row["鎖定至"]);
            DateTime? lockUntil = DateTime.TryParse(raw, out var lu) ? lu : null;
            return (failures, lockUntil);
        }
        catch
        {
            return (0, null);
        }
    }

    /// <summary>記錄一次失敗；累計達上限時設定鎖定截止時間。</summary>
    public static void RecordFailure(string userId, int failures)
    {
        try
        {
            if (failures >= MaxFailures)
            {
                var until = DateTime.Now.Add(LockDuration).ToString("yyyy-MM-dd HH:mm:ss");
                DbManager.ExecuteNonQuery(
                    "INSERT OR REPLACE INTO [登入鎖定] ([使用者編號],[失敗次數],[鎖定至]) VALUES ($id,$n,$until)",
                    DbManager.Param("$id", userId), DbManager.Param("$n", failures), DbManager.Param("$until", until));
            }
            else
            {
                DbManager.ExecuteNonQuery(
                    "INSERT OR REPLACE INTO [登入鎖定] ([使用者編號],[失敗次數]) VALUES ($id,$n)",
                    DbManager.Param("$id", userId), DbManager.Param("$n", failures));
            }
        }
        catch
        {
            // 記錄失敗不應阻斷登入流程
        }
    }

    /// <summary>登入成功時清除鎖定記錄。</summary>
    public static void ClearLock(string userId)
    {
        try
        {
            DbManager.ExecuteNonQuery("DELETE FROM [登入鎖定] WHERE [使用者編號] = $id",
                DbManager.Param("$id", userId));
        }
        catch
        {
            // 忽略清除失敗
        }
    }

    /// <summary>判斷是否仍在鎖定期間內。</summary>
    public static bool IsLocked(DateTime? lockUntil, out TimeSpan remaining)
    {
        if (lockUntil is DateTime lu && lu > DateTime.Now)
        {
            remaining = lu - DateTime.Now;
            return true;
        }
        remaining = TimeSpan.Zero;
        return false;
    }

    /// <summary>檢查密碼強度：至少 8 碼且包含英文字母與數字。</summary>
    public static bool CheckPasswordStrength(string pwd, out string? error)
    {
        if (pwd.Length < 8) { error = "密碼至少需 8 碼"; return false; }
        if (!pwd.Any(char.IsLetter) || !pwd.Any(char.IsDigit))
        {
            error = "密碼需同時包含英文字母與數字";
            return false;
        }
        error = null;
        return true;
    }
}
