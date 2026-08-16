// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
namespace HeliERP.Models;

/// <summary>登入使用者（對應 權限主檔 表）</summary>
public class AppUser
{
    /// <summary>使用者編號（登入帳號）</summary>
    public string UserId { get; set; } = "";

    /// <summary>使用者名稱</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>關聯的員工編號（可為空）</summary>
    public string? EmployeeId { get; set; }

    /// <summary>可否檢視成本</summary>
    public bool CanViewCost { get; set; }

    /// <summary>可否檢視售價</summary>
    public bool CanViewPrice { get; set; }

    /// <summary>是否為系統管理員（第一個內建帳號）</summary>
    public bool IsAdmin { get; set; }
}
