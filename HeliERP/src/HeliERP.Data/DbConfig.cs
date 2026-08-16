// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.IO;
using System.Text.Json;
using HeliERP.Models;

namespace HeliERP.Data;

/// <summary>
/// 程式設定管理：資料庫路徑（支援區網 UNC 路徑）與公司資訊。
/// 設定檔存放於執行檔同目錄的 HeliERP.config.json，隨程式複製即可部署。
/// </summary>
public class DbConfig
{
    /// <summary>設定檔名稱</summary>
    public const string ConfigFileName = "HeliERP.config.json";

    /// <summary>資料庫檔名（預設）</summary>
    public const string DefaultDbFileName = "HeliERP.db";

    /// <summary>資料庫檔案路徑（可為本機路徑或 \\伺服器\共享\HeliERP.db）</summary>
    public string DatabasePath { get; set; } = "";

    /// <summary>公司基本資料</summary>
    public CompanyInfo Company { get; set; } = new();

    /// <summary>上次登入的使用者編號（登入畫面預填用）</summary>
    public string LastUserId { get; set; } = "";

    /// <summary>近期使用過的資料庫路徑（登入畫面提供選擇，依序為最近使用）</summary>
    public List<string> DatabaseHistory { get; set; } = new();

    /// <summary>啟動時自動備份資料庫</summary>
    public bool AutoBackup { get; set; } = true;

    /// <summary>自動備份保留份數</summary>
    public int BackupRetention { get; set; } = 10;

    private static string ConfigFilePath =>
        Path.Combine(AppContext.BaseDirectory, ConfigFileName);

    /// <summary>載入設定；無設定檔時回傳預設（資料庫路徑指向執行檔旁預設檔名）</summary>
    public static DbConfig Load()
    {
        var path = ConfigFilePath;
        if (File.Exists(path))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<DbConfig>(File.ReadAllText(path));
                if (cfg is not null)
                {
                    if (string.IsNullOrWhiteSpace(cfg.DatabasePath))
                        cfg.DatabasePath = DefaultDbPath();
                    cfg.DatabaseHistory ??= new List<string>();
                    return cfg;
                }
            }
            catch
            {
                // 設定檔損壞時回退預設
            }
        }
        return new DbConfig { DatabasePath = DefaultDbPath() };
    }

    /// <summary>儲存設定</summary>
    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        File.WriteAllText(ConfigFilePath, json);
    }

    /// <summary>預設資料庫路徑：執行檔同目錄下的 HeliERP.db</summary>
    public static string DefaultDbPath() =>
        Path.Combine(AppContext.BaseDirectory, DefaultDbFileName);

    /// <summary>測試指定路徑是否為可連線的 SQLite 資料庫</summary>
    public static bool TestConnection(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master";
            cmd.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>測試指定資料庫是否包含「權限主檔」表（可用來登入）</summary>
    public static bool HasLoginTable(string path)
    {
        if (!TestConnection(path))
            return false;
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '權限主檔'";
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>收集候選資料庫：執行檔目錄、目前資料庫所在目錄、使用歷史</summary>
    public List<string> FindDatabases()
    {
        var result = new List<string>();
        void Add(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            p = Path.GetFullPath(p);
            if (!File.Exists(p)) return;
            if (!p.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) return;
            if (!result.Contains(p, StringComparer.OrdinalIgnoreCase))
                result.Add(p);
        }

        foreach (var p in Directory.GetFiles(AppContext.BaseDirectory, "*.db", SearchOption.TopDirectoryOnly))
            Add(p);

        var dir = Path.GetDirectoryName(Path.GetFullPath(DatabasePath));
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            foreach (var p in Directory.GetFiles(dir, "*.db", SearchOption.TopDirectoryOnly))
                Add(p);

        foreach (var p in DatabaseHistory)
            Add(p);

        Add(DatabasePath);

        return result;
    }
}
