// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
namespace HeliERP.Models;

/// <summary>公司基本資料（原 DataLinks.mdb 對應）</summary>
public class CompanyInfo
{
    /// <summary>公司名稱</summary>
    public string CompanyName { get; set; } = "禾秝安全系統工程有限公司";

    /// <summary>統一編號</summary>
    public string TaxId { get; set; } = "22619219";

    /// <summary>負責人</summary>
    public string Owner { get; set; } = "何正國";

    /// <summary>聯絡電話</summary>
    public string Phone { get; set; } = "(02)2593-2101";

    /// <summary>登記地址</summary>
    public string Address { get; set; } = "臺北市新生北路3段79-2號3F";

    /// <summary>電子郵件</summary>
    public string Email { get; set; } = "karahui@ms95.url.com.tw";

    /// <summary>網址</summary>
    public string Website { get; set; } = "";
}
