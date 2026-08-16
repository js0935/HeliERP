// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.IO;
using System.Linq;

namespace HeliERP.Data;

/// <summary>
/// 資料庫備份服務：一致性快照備份、還原與啟動自動備份（每天一份、保留 N 份）。
/// 備份檔為 VACUUM INTO 產生的可開機 SQLite 快照。
/// </summary>
public static class BackupService
{
    /// <summary>預設備份目錄：執行檔同目錄 Backups</summary>
    public static string DefaultBackupDir() => Path.Combine(AppContext.BaseDirectory, "Backups");

    /// <summary>依目前時間產生備份檔名</summary>
    public static string NewBackupName(DateTime now) => $"HeliERP_{now:yyyyMMdd_HHmmss}.bak";

    /// <summary>立即備份到指定檔案</summary>
    public static void BackupTo(string targetPath) => DbManager.BackupTo(targetPath);

    /// <summary>以備份檔覆蓋目前資料庫</summary>
    public static void RestoreFrom(string backupPath) => DbManager.RestoreFrom(backupPath);

    /// <summary>
    /// 啟動時自動備份：每天最多一份，超過保留份數自動清除最舊備份。
    /// 回傳本次建立的備份路徑；當天已備份或未啟用時回傳 null。
    /// </summary>
    public static string? AutoBackupIfDue(DbConfig config)
    {
        if (!config.AutoBackup)
            return null;

        var dir = DefaultBackupDir();
        var today = DateTime.Now.ToString("yyyyMMdd");
        if (Directory.Exists(dir) &&
            Directory.EnumerateFiles(dir, "HeliERP_*.bak")
                .Any(f => Path.GetFileName(f).StartsWith($"HeliERP_{today}", StringComparison.Ordinal)))
        {
            return null; // 今天已備份過
        }

        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, NewBackupName(DateTime.Now));
        DbManager.BackupTo(path);
        Prune(dir, config.BackupRetention);
        return path;
    }

    private static void Prune(string dir, int keep)
    {
        var files = Directory.EnumerateFiles(dir, "HeliERP_*.bak")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var f in files.Skip(Math.Max(keep, 1)))
        {
            try { File.Delete(f); }
            catch { /* 備份被占用時略過 */ }
        }
    }
}
