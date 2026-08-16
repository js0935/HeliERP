// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.IO;
using System.Text.Json;

namespace HeliERP.App;

/// <summary>
/// 「最近使用」模組記錄：依最後開啟時間排序，最多保留 8 筆，
/// 存於本機使用者設定目錄（%LocalAppData%\HeliERP\recent-modules.json）。
/// </summary>
public static class RecentModules
{
    private const int MaxItems = 8;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HeliERP", "recent-modules.json");

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<string>();
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath))
                   ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void Record(string name)
    {
        try
        {
            var list = Load();
            list.RemoveAll(n => n == name);
            list.Insert(0, name);
            if (list.Count > MaxItems) list.RemoveRange(MaxItems, list.Count - MaxItems);
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch
        {
        }
    }
}
