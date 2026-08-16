// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0（表單式主檔維護：欄位定義目錄）
// ════════════════════════════════════════════════════════

namespace HeliERP.App;

/// <summary>
/// 表單式主檔維護的欄位定義目錄：為常用的基本資料表定義
/// 「分頁 × 欄位（型別/位置/下拉）」配置，開啟 FormMasterForm 時套用。
/// 未定義於分頁的其餘欄位會自動集中到「全部欄位」頁，確保資料不遺漏。
/// </summary>
public static class FormMasterCatalog
{
    /// <summary>已建置表單式介面的資料表（進入時改開 FormMasterForm）。</summary>
    private static readonly HashSet<string> MasterTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "客戶廠商", "員工資料", "倉庫資料", "會計科目",
        "銀行資料", "貨運公司", "車廠資料", "部門資料",
    };

    public static bool IsMasterTable(string tableName) => MasterTables.Contains(tableName);

    /// <summary>取得表單式介面配置；非表單式表回傳 null。</summary>
    public static (IReadOnlyList<string> ListColumns, IReadOnlyList<FormPage> Pages)? Get(string tableName)
    {
        return tableName switch
        {
            "客戶廠商" => (new[] { "客廠編號", "公司簡稱", "客廠類別", "統一編號" }, CustomerPages),
            "員工資料" => (new[] { "員工編號", "員工姓名", "部門編號", "工作職務" }, EmployeePages),
            "倉庫資料" => (new[] { "倉庫編號", "倉庫名稱" }, WarehousePages),
            "會計科目" => (new[] { "科目編號", "科目名稱", "類別編號", "期初借貸" }, AccountPages),
            "銀行資料" => (new[] { "銀行編號", "銀行名稱" }, BankPages),
            "貨運公司" => (new[] { "貨運編號", "貨運名稱", "聯絡電話" }, FreightPages),
            "車廠資料" => (new[] { "車廠編號", "車廠名稱" }, CarMakerPages),
            "部門資料" => (new[] { "部門編號", "部門名稱", "部門倉庫" }, DeptPages),
            _ => null,
        };
    }

    private static FormField F(string field, string label, FormFieldKind kind = FormFieldKind.Text,
        int row = 0, int col = 0, int span = 1, string[]? items = null, string? sql = null) =>
        new(field, label, kind, row, col, span, items, sql);

    // ═══════════ 客戶廠商 ═══════════

    private static readonly FormPage[] CustomerPages =
    {
        new("基本資料", new[]
        {
            F("客廠編號", "客廠編號"),
            F("客廠類別", "類別", FormFieldKind.Combo, 0, 1, 1, new[] { "客戶", "廠商" }),
            F("公司全名", "公司全名", row: 0, col: 2),
            F("公司簡稱", "公司簡稱", row: 0, col: 3),
            F("統一編號", "統一編號", row: 1),
            F("負責人", "負責人", row: 1, col: 1),
            F("聯絡人一", "聯絡人一", row: 1, col: 2),
            F("聯絡電話一", "聯絡電話一", row: 1, col: 3),
            F("行動電話", "行動電話", row: 2),
            F("聯絡電話二", "聯絡電話二", row: 2, col: 1),
            F("傳真號碼", "傳真號碼", row: 2, col: 2),
            F("電子郵件信箱", "電子郵件", row: 2, col: 3),
            F("網址", "網址", row: 3, span: 2),
            F("員工編號", "員工編號", row: 3, col: 2),
            F("建檔日期", "建檔日期", FormFieldKind.Date, 3, 3),
            F("類別編號", "類別編號", row: 4),
            F("部門編號", "部門編號", row: 4, col: 1),
            F("適用售價", "適用售價", row: 4, col: 2),
            F("發票聯式", "發票聯式", FormFieldKind.Combo, 4, 3, 1, new[] { "二聯式", "三聯式", "電子發票" }),
            F("課稅別", "課稅別", row: 5),
            F("售價稅別", "售價稅別", row: 5, col: 1),
            F("交易幣別", "交易幣別", row: 5, col: 2),
            F("備註", "備註", FormFieldKind.Memo, 6, 0, 4),
        }),
        new("地址", new[]
        {
            F("登記地址", "登記地址", row: 0, span: 3),
            F("登記地郵遞區號", "郵遞區號", row: 0, col: 3),
            F("送貨地址", "送貨地址", row: 1, span: 3),
            F("送貨地郵遞區號", "郵遞區號", row: 1, col: 3),
            F("帳單地址", "帳單地址", row: 2, span: 3),
            F("帳單地郵遞區號", "郵遞區號", row: 2, col: 3),
        }),
        new("帳款", new[]
        {
            F("收款條件", "收款條件", row: 0),
            F("收款天數", "收款天數", FormFieldKind.Number, 0, 1),
            F("月結帳日", "月結帳日", FormFieldKind.Number, 0, 2),
            F("請款日", "請款日", FormFieldKind.Number, 0, 3),
            F("銀行編號", "銀行編號", row: 1),
            F("請款對象", "請款對象", row: 1, col: 1),
            F("收款日", "收款日", FormFieldKind.Number, 1, 2),
            F("額度類別", "額度類別", row: 1, col: 3),
            F("帳款額度", "帳款額度", FormFieldKind.Number, 2),
            F("票據額度", "票據額度", FormFieldKind.Number, 2, 1),
            F("帳款科目", "帳款科目", row: 2, col: 2),
            F("預收科目", "預收科目", row: 2, col: 3),
            F("票據科目", "票據科目", row: 3),
            F("佣金支出科目", "佣金支出科目", row: 3, col: 1),
            F("應付佣金科目", "應付佣金科目", row: 3, col: 2),
            F("期前應收", "期前應收", FormFieldKind.ReadOnly, 4),
            F("未收帳款", "未收帳款", FormFieldKind.ReadOnly, 4, 1),
            F("預收貨款", "預收貨款", FormFieldKind.ReadOnly, 4, 2),
            F("已收未兌", "已收未兌", FormFieldKind.ReadOnly, 4, 3),
        }),
        new("貿易", new[]
        {
            F("交易條件", "交易條件", row: 0),
            F("交易方式", "交易方式", row: 0, col: 1),
            F("付款方式", "付款方式", row: 0, col: 2),
            F("運送方式", "運送方式", row: 0, col: 3),
            F("出口港", "出口港", row: 1),
            F("目的港", "目的港", row: 1, col: 1),
            F("轉口港", "轉口港", row: 1, col: 2),
            F("保險率", "保險率", FormFieldKind.Number, 1, 3),
            F("佣金率", "佣金率", FormFieldKind.Number, 2),
            F("佣金對象", "佣金對象", row: 2, col: 1),
            F("嘜頭編號", "嘜頭編號", row: 2, col: 2),
            F("區域編號", "區域編號", row: 2, col: 3),
            F("國內外區分", "國內外區分", row: 3),
            F("債權人", "債權人", row: 3, col: 1),
            F("貨運公司編號", "貨運公司", row: 3, col: 2),
            F("英文名稱", "英文名稱", row: 4, span: 2),
            F("英文地址", "英文地址", row: 5, span: 4),
            F("異動日期", "異動日期", FormFieldKind.Date, 6),
            F("工廠登記證", "工廠登記證", row: 6, col: 1),
            F("員工數", "員工數", FormFieldKind.Number, 6, 2),
            F("資本額", "資本額", FormFieldKind.Number, 6, 3),
        }),
        new("全部欄位", Array.Empty<FormField>()),
    };

    // ═══════════ 員工資料 ═══════════

    private static readonly FormPage[] EmployeePages =
    {
        new("基本資料", new[]
        {
            F("員工編號", "員工編號"),
            F("員工姓名", "員工姓名", row: 0, col: 1),
            F("性別", "性別", FormFieldKind.Combo, 0, 2, 1, new[] { "男", "女" }),
            F("身份證編號", "身份證編號", row: 0, col: 3),
            F("部門編號", "部門", FormFieldKind.Combo, 1, 0, 1, null, "SELECT [部門編號] FROM [部門資料] ORDER BY [部門編號]"),
            F("工作職務", "工作職務", row: 1, col: 1),
            F("婚姻狀況", "婚姻狀況", FormFieldKind.Combo, 1, 2, 1, new[] { "已婚", "未婚" }),
            F("血型", "血型", row: 1, col: 3),
            F("出生日期", "出生日期", FormFieldKind.Date, 2),
            F("籍貫", "籍貫", row: 2, col: 1),
            F("學歷", "學歷", row: 2, col: 2),
            F("聯絡人", "聯絡人", row: 2, col: 3),
            F("聯絡電話", "聯絡電話", row: 3),
            F("行動電話", "行動電話", row: 3, col: 1),
            F("聯絡地址", "聯絡地址", row: 4, span: 4),
            F("戶籍地址", "戶籍地址", row: 5, span: 4),
            F("到職日期", "到職日期", FormFieldKind.Date, 6),
            F("離職日期", "離職日期", FormFieldKind.Date, 6, 1),
            F("備註", "備註", FormFieldKind.Memo, 7, 0, 4),
        }),
        new("薪資", new[]
        {
            F("本薪", "本薪", FormFieldKind.Number, row: 0),
            F("日薪", "日薪", FormFieldKind.Number, 0, 1),
            F("時薪", "時薪", FormFieldKind.Number, 0, 2),
            F("健保金額", "健保金額", FormFieldKind.Number, 0, 3),
            F("勞保金額", "勞保金額", FormFieldKind.Number, 1),
            F("眷口數", "眷口數", FormFieldKind.Number, 1, 1),
            F("扶養人數", "扶養人數", FormFieldKind.Number, 1, 2),
            F("有配偶", "有配偶", FormFieldKind.Combo, 1, 3, 1, new[] { "是", "否" }),
            F("特休天數", "特休天數", FormFieldKind.Number, 2),
            F("全勤資格", "全勤資格", row: 2, col: 1),
            F("銀行代號", "銀行代號", row: 2, col: 2),
            F("銀行帳號", "銀行帳號", row: 2, col: 3),
            F("工作時數", "工作時數", FormFieldKind.Number, 3),
        }),
        new("全部欄位", Array.Empty<FormField>()),
    };

    // ═══════════ 倉庫資料 ═══════════

    private static readonly FormPage[] WarehousePages =
    {
        new("基本資料", new[]
        {
            F("倉庫編號", "倉庫編號"),
            F("倉庫名稱", "倉庫名稱", row: 0, col: 1),
            F("聯絡電話", "聯絡電話", row: 1),
            F("備註", "備註", FormFieldKind.Memo, 1, 1, 3),
        }),
    };

    // ═══════════ 會計科目 ═══════════

    private static readonly FormPage[] AccountPages =
    {
        new("基本資料", new[]
        {
            F("科目編號", "科目編號"),
            F("科目名稱", "科目名稱", row: 0, col: 1),
            F("類別編號", "類別編號", FormFieldKind.Combo, 0, 2, 1, null, "SELECT [類別編號] FROM [會計類別] ORDER BY [類別編號]"),
            F("期初借貸", "期初借貸", FormFieldKind.Combo, 0, 3, 1, new[] { "借", "貸" }),
            F("期初餘額", "期初餘額", FormFieldKind.Number, 1),
            F("沖銷科目", "沖銷科目", FormFieldKind.Combo, 1, 1, 1, new[] { "0", "1" }),
            F("統制科目", "統制科目", FormFieldKind.Combo, 1, 2, 1, new[] { "0", "1" }),
            F("隸屬科目", "隸屬科目", row: 1, col: 3),
            F("英文名稱", "英文名稱", row: 2, span: 2),
            F("常用摘要", "常用摘要", row: 3, span: 2),
            F("說明", "說明", FormFieldKind.Memo, 4, 0, 4),
        }),
    };

    // ═══════════ 銀行資料 ═══════════

    private static readonly FormPage[] BankPages =
    {
        new("基本資料", new[]
        {
            F("銀行編號", "銀行編號"),
            F("銀行名稱", "銀行名稱", row: 0, col: 1),
            F("聯絡電話", "聯絡電話", row: 1),
            F("備註", "備註", FormFieldKind.Memo, 1, 1, 3),
        }),
    };

    // ═══════════ 貨運公司 ═══════════

    private static readonly FormPage[] FreightPages =
    {
        new("基本資料", new[]
        {
            F("貨運編號", "貨運編號"),
            F("貨運名稱", "貨運名稱", row: 0, col: 1),
            F("聯絡人", "聯絡人", row: 0, col: 2),
            F("聯絡電話", "聯絡電話", row: 0, col: 3),
            F("傳真號碼", "傳真號碼", row: 1),
            F("備註", "備註", FormFieldKind.Memo, 1, 1, 3),
        }),
    };

    // ═══════════ 車廠資料 ═══════════

    private static readonly FormPage[] CarMakerPages =
    {
        new("基本資料", new[]
        {
            F("車廠編號", "車廠編號"),
            F("車廠名稱", "車廠名稱", row: 0, col: 1),
            F("備註", "備註", FormFieldKind.Memo, 1, 0, 3),
        }),
    };

    // ═══════════ 部門資料 ═══════════

    private static readonly FormPage[] DeptPages =
    {
        new("基本資料", new[]
        {
            F("部門編號", "部門編號"),
            F("部門名稱", "部門名稱", row: 0, col: 1),
            F("部門倉庫", "部門倉庫", row: 0, col: 2),
            F("傳輸旗標", "傳輸旗標", FormFieldKind.Number, 0, 3),
            F("備註", "備註", FormFieldKind.Memo, 1, 0, 3),
        }),
    };
}
