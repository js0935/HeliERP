// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
namespace HeliERP.Models;

/// <summary>公司基本資料（原 DataLinks.mdb 對應）；預設為空，由使用者於系統設定填寫</summary>
public class CompanyInfo
{
    /// <summary>公司名稱</summary>
    public string CompanyName { get; set; } = "";

    /// <summary>統一編號</summary>
    public string TaxId { get; set; } = "";

    /// <summary>負責人</summary>
    public string Owner { get; set; } = "";

    /// <summary>聯絡電話</summary>
    public string Phone { get; set; } = "";

    /// <summary>登記地址</summary>
    public string Address { get; set; } = "";

    /// <summary>電子郵件</summary>
    public string Email { get; set; } = "";

    /// <summary>網址</summary>
    public string Website { get; set; } = "";
}
