// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（系統健康監控）
// ════════════════════════════════════════════════════════
using System.Data;
using System.IO;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 系統健康監控：啟動或手動時檢查資料庫完整性、WAL 大小、
/// 備份狀態與磁碟空間，並回傳建議事項，讓使用者及早處理異常。
/// </summary>
public static class HealthCheckService
{
    public enum 狀態 { 正常, 注意, 異常 }

    public sealed record Item(string 項目, 狀態 Status, string 說明, string 建議 = "");

    public static List<Item> RunAll(DbConfig config)
    {
        var items = new List<Item>();
        string dbPath = DbManager.DatabasePath;
        string? walPath = null;

        items.Add(CheckDbFile(dbPath, out walPath));
        items.Add(CheckIntegrity());
        items.Add(CheckWal(walPath));
        items.Add(CheckBackup(config));
        items.Add(CheckDisk(dbPath));
        items.Add(CheckAutoBackup(config));
        return items;
    }

    private static Item CheckDbFile(string dbPath, out string? walPath)
    {
        walPath = null;
        if (!File.Exists(dbPath))
        {
            return new Item("資料庫檔案", 狀態.異常, "找不到資料庫檔案：" + dbPath,
                "請檢查路徑或從備份還原。");
        }
        var info = new FileInfo(dbPath);
        walPath = dbPath + "-wal";
        return new Item("資料庫檔案", 狀態.正常,
            $"{Path.GetFileName(dbPath)}　{FormatSize(info.Length)}");
    }

    private static Item CheckIntegrity()
    {
        try
        {
            using var conn = DbManager.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check";
            var result = cmd.ExecuteScalar();
            if (result is null || Convert.ToString(result) == "ok")
                return new Item("資料庫完整性", 狀態.正常, "PRAGMA quick_check 通過");
            return new Item("資料庫完整性", 狀態.異常, "完整性檢查發現問題：" + result,
                "請立即以最近備份還原或聯絡技術支援。");
        }
        catch (Exception ex)
        {
            return new Item("資料庫完整性", 狀態.異常, "無法執行完整性檢查：" + ex.Message,
                "請確認資料庫路徑與連線。");
        }
    }

    private static Item CheckWal(string? walPath)
    {
        if (walPath is null || !File.Exists(walPath))
            return new Item("WAL 日誌", 狀態.正常, "無未結 WAL 日誌（模式為 DELETE 或已 checkpoint）");
        var len = new FileInfo(walPath).Length;
        if (len > 256L * 1024 * 1024)
            return new Item("WAL 日誌", 狀態.注意, $"WAL 日誌已達 {FormatSize(len)}（偏大）",
                "於無其他使用者連線時執行 checkpoint，可合併回主資料庫並縮小檔案。");
        return new Item("WAL 日誌", 狀態.正常, $"WAL 日誌 {FormatSize(len)}");
    }

    private static Item CheckBackup(DbConfig config)
    {
        var dir = BackupService.DefaultBackupDir();
        if (!Directory.Exists(dir))
            return new Item("備份狀態", 狀態.注意, "尚未建立任何備份", "請立即手動備份一次。");
        var latest = Directory.EnumerateFiles(dir, "HeliERP_*.bak")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new FileInfo(f))
            .FirstOrDefault();
        if (latest is null)
            return new Item("備份狀態", 狀態.注意, "尚未建立任何備份", "請立即手動備份一次。");
        var age = DateTime.Now - latest.LastWriteTime;
        var names = Directory.EnumerateFiles(dir, "HeliERP_*.bak").Count();
        if (age.TotalDays > 7)
            return new Item("備份狀態", 狀態.注意,
                $"最近備份 {latest.LastWriteTime:yyyy-MM-dd HH:mm}（距今 {age.TotalDays:N0} 天），共 {names} 份",
                "備份逾一週，請確認自動備份設定或手動備份。");
        return new Item("備份狀態", 狀態.正常,
            $"最近備份 {latest.LastWriteTime:yyyy-MM-dd HH:mm}，共 {names} 份　{FormatSize(latest.Length)}");
    }

    private static Item CheckDisk(string dbPath)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(dbPath)) ?? "C:\\";
            var drive = new DriveInfo(root);
            if (!drive.IsReady)
                return new Item("磁碟空間", 狀態.注意, $"無法讀取磁碟 {root} 空間", "請檢查磁碟狀態。");
            decimal freeGb = drive.AvailableFreeSpace / 1024m / 1024m / 1024m;
            if (freeGb < 1m)
                return new Item("磁碟空間", 狀態.異常, $"磁碟 {root} 剩餘空間不足（{freeGb:N2} GB）",
                    "請清理磁碟，避免資料庫無法寫入。");
            if (freeGb < 5m)
                return new Item("磁碟空間", 狀態.注意, $"磁碟 {root} 剩餘空間偏低（{freeGb:N2} GB）",
                    "請盡快清理磁碟。");
            return new Item("磁碟空間", 狀態.正常, $"磁碟 {root} 剩餘空間 {freeGb:N1} GB");
        }
        catch
        {
            return new Item("磁碟空間", 狀態.正常, "無法取得磁碟空間資訊");
        }
    }

    private static Item CheckAutoBackup(DbConfig config)
    {
        if (config.AutoBackup)
            return new Item("自動備份", 狀態.正常,
                $"已啟用，每天一份，保留 {config.BackupRetention} 份");
        return new Item("自動備份", 狀態.注意, "自動備份未啟用",
            "建議於「系統設定」開啟自動備份，或定期手動備份。");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024m * 1024 * 1024)
            return $"{bytes / 1024m / 1024m / 1024m:N2} GB";
        if (bytes >= 1024 * 1024)
            return $"{bytes / 1024m / 1024m:N1} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024m:N1} KB";
        return $"{bytes} B";
    }
}
