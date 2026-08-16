// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════

namespace HeliERP.App;

/// <summary>欄位控制項型別；Auto = 依欄名/資料型別自動判斷</summary>
public enum FieldKind
{
    Auto,       // 依欄名與資料型別自動判斷
    Text,       // 單行文字
    Multiline,  // 多行文字
    Integer,    // 整數（數字上下）
    Decimal,    // 小數（右對齊文字框）
    Date,       // 日期選擇
    Bool,       // 勾選
    Lookup,     // 下拉選單（需指定 LookupTable）
    Hidden,     // 不顯示於輸入表單（系統維護欄位）
}

/// <summary>單一欄位的輸入定義</summary>
public sealed record FieldDef(
    string Name,                 // 資料庫欄名
    FieldKind Kind = FieldKind.Auto,
    string? Label = null,        // 顯示標籤（預設用欄名）
    string? LookupTable = null,  // Lookup：下拉來源資料表
    string LookupValue = "",     // Lookup：下拉值欄（預設本欄名）
    string LookupDisplay = "",   // Lookup：下拉顯示欄（預設值欄）
    bool Required = false,       // 必填
    int MaxLength = 0,           // 文字上限（0=不限制）
    IReadOnlyDictionary<string, string>? LookupCopy = null);  // 帶入：目標欄名 → 來源表欄名（選擇時自動填入）

/// <summary>
/// 主檔欄位定義目錄：集中定義各資料維護表的輸入欄位
/// （控制項型別、下拉來源、必填、標籤、隱藏系統欄）。
/// GenericEditorDialog 依此產生輸入畫面；未定義的表仍依欄名自動判斷。
/// </summary>
public static class TableFields
{
    private static FieldDef F(string n, FieldKind k = FieldKind.Auto, string? label = null,
        string? lookup = null, string disp = "", bool req = false, int max = 0,
        params (string Target, string Source)[] copy) =>
        new(n, k, label, lookup, n, disp, req, max,
            copy.Length > 0 ? copy.ToDictionary(c => c.Target, c => c.Source) : null);

    private static readonly Dictionary<string, FieldDef[]> Defs = new(StringComparer.OrdinalIgnoreCase)
    {
        // ═══ 貨品資料 ═══
        ["貨品主檔"] = new[]
        {
            F("貨品編號", req: true, max: 40),
            F("品名", max: 100),
            F("規格", max: 80),
            F("用途", max: 80),
            F("貨品型態", FieldKind.Text, "貨品型態", max: 20),
            F("條碼編號", max: 40),
            F("類別編號", FieldKind.Lookup, "貨品類別", "貨品類別", "類別名稱"),
            F("廠商編號", FieldKind.Lookup, "主要廠商", "客戶廠商", "公司簡稱"),
            F("原廠貨號", max: 40),
            F("屬性編號", max: 20),
            F("基本單位", FieldKind.Lookup, "基本單位", "貨品單位", "單位名稱"),
            F("標準售價", FieldKind.Decimal),
            F("牌價", FieldKind.Decimal),
            F("售價A", FieldKind.Decimal),
            F("售價B", FieldKind.Decimal),
            F("售價C", FieldKind.Decimal),
            F("售價D", FieldKind.Decimal),
            F("售價E", FieldKind.Decimal),
            F("標準成本", FieldKind.Decimal),
            F("現行成本", FieldKind.Decimal),
            F("內包裝數", FieldKind.Decimal),
            F("內包裝單位", max: 20),
            F("外包裝數量", FieldKind.Decimal),
            F("外包裝單位", max: 20),
            F("入庫方式", max: 20),
            F("儲放位置", max: 60),
            F("倉庫編號", FieldKind.Lookup, "存放倉庫", "倉庫資料", "倉庫名稱"),
            F("前置天數", FieldKind.Decimal),
            F("採購批量", FieldKind.Decimal),
            F("滯銷期間", FieldKind.Decimal),
            F("銷貨收入科目", FieldKind.Lookup, "銷貨收入科目", "會計科目", "科目名稱"),
            F("銷貨成本科目", FieldKind.Lookup, "銷貨成本科目", "會計科目", "科目名稱"),
            F("銷貨退回科目", FieldKind.Lookup, "銷貨退回科目", "會計科目", "科目名稱"),
            F("進貨科目", FieldKind.Lookup, "進貨科目", "會計科目", "科目名稱"),
            F("進貨退出科目", FieldKind.Lookup, "進貨退出科目", "會計科目", "科目名稱"),
            F("存貨科目", FieldKind.Lookup, "存貨科目", "會計科目", "科目名稱"),
            F("贈品費用科目", FieldKind.Lookup, "贈品費用科目", "會計科目", "科目名稱"),
            F("車種編號", FieldKind.Lookup, "車種", "車種資料", "車種名稱"),
            F("車廠編號", FieldKind.Lookup, "車廠", "車廠資料", "車廠名稱"),
            F("顏色", max: 20),
            F("重量單位", max: 20),
            F("考慮安全存量", FieldKind.Bool),
            F("安全存量", FieldKind.Decimal),
            F("計算庫存", FieldKind.Bool),
            F("包裝說明", FieldKind.Multiline),
            F("備註", FieldKind.Multiline),
            F("英文品名", max: 100),
            F("英文規格", max: 80),
            F("英文描述", FieldKind.Multiline),
            F("建檔日期", FieldKind.Date),
            F("最近進貨日", FieldKind.Date),
            F("最近出貨日", FieldKind.Date),
        },

        // ═══ 客戶廠商 ═══
        ["客戶廠商"] = new[]
        {
            F("客廠類別", req: true, max: 1),
            F("客廠編號", req: true, max: 30),
            F("同為廠商客戶", FieldKind.Bool),
            F("公司全名", max: 100),
            F("公司簡稱", max: 40),
            F("發票抬頭", max: 100),
            F("統一編號", max: 10),
            F("負責人", max: 40),
            F("聯絡人一", max: 40),
            F("聯絡人二", max: 40),
            F("聯絡電話一", max: 40),
            F("聯絡電話二", max: 40),
            F("行動電話", max: 40),
            F("傳真號碼", max: 40),
            F("電子郵件信箱", max: 80),
            F("網址", max: 80),
            F("登記地址", FieldKind.Multiline, "登記地址"),
            F("送貨地址", FieldKind.Multiline, "送貨地址"),
            F("帳單地址", FieldKind.Multiline, "帳單地址"),
            F("類別編號", FieldKind.Lookup, "客廠類別", "客廠類別", "類別名稱"),
            F("員工編號", FieldKind.Lookup, "業務員", "員工資料", "員工姓名"),
            F("部門編號", FieldKind.Lookup, "部門", "部門資料", "部門名稱"),
            F("貨運公司編號", FieldKind.Lookup, "貨運公司", "貨運公司", "貨運名稱"),
            F("銀行編號", FieldKind.Lookup, "往來銀行", "銀行資料", "銀行名稱"),
            F("區域編號", FieldKind.Lookup, "區域", "區域設定", "區域名稱"),
            F("適用售價", max: 20),
            F("課稅別", max: 20),
            F("售價稅別", max: 20),
            F("交易幣別", FieldKind.Lookup, "交易幣別", "幣別匯率", "幣別名稱"),
            F("銷售折扣", FieldKind.Decimal),
            F("發票聯式", max: 20),
            F("收款條件", max: 20),
            F("收款天數", FieldKind.Integer),
            F("月結帳日", FieldKind.Integer),
            F("請款對象", max: 40),
            F("請款日", FieldKind.Integer),
            F("收款日", FieldKind.Integer),
            F("帳款科目", FieldKind.Lookup, "帳款科目", "會計科目", "科目名稱"),
            F("預收科目", FieldKind.Lookup, "預收科目", "會計科目", "科目名稱"),
            F("票據科目", FieldKind.Lookup, "票據科目", "會計科目", "科目名稱"),
            F("佣金支出科目", FieldKind.Lookup, "佣金支出科目", "會計科目", "科目名稱"),
            F("應付佣金科目", FieldKind.Lookup, "應付佣金科目", "會計科目", "科目名稱"),
            F("佣金對象", max: 40),
            F("佣金率", FieldKind.Decimal),
            F("保險率", FieldKind.Decimal),
            F("交易條件", max: 40),
            F("交易方式", max: 40),
            F("付款方式", max: 40),
            F("出口港", max: 40),
            F("目的港", max: 40),
            F("轉口港", max: 40),
            F("國內外區分", max: 10),
            F("債權人", max: 40),
            F("運送方式", max: 40),
            F("員工數", FieldKind.Integer),
            F("資本額", FieldKind.Decimal),
            F("英文名稱", max: 100),
            F("英文地址", FieldKind.Multiline, "英文地址"),
            F("嘜頭編號", max: 60),
            F("建檔日期", FieldKind.Date),
            F("異動日期", FieldKind.Date),
            F("備註", FieldKind.Multiline),
            F("期前應收", FieldKind.Hidden),
            F("期前預收", FieldKind.Hidden),
            F("期前票據", FieldKind.Hidden),
            F("未收帳款", FieldKind.Hidden),
            F("預收貨款", FieldKind.Hidden),
            F("已收未兌", FieldKind.Hidden),
            F("額度類別", FieldKind.Hidden),
            F("帳款額度", FieldKind.Hidden),
            F("票據額度", FieldKind.Hidden),
            F("undevalue", FieldKind.Hidden),
            F("選取", FieldKind.Hidden),
            F("工廠登記證", max: 60),
            F("登記地郵遞區號", max: 10),
            F("送貨地郵遞區號", max: 10),
            F("帳單地郵遞區號", max: 10),
        },

        // ═══ 客廠類別 ═══
        ["客廠類別"] = new[]
        {
            F("客廠類別", req: true, max: 1),
            F("類別編號", req: true, max: 10),
            F("類別名稱", max: 40),
            F("備註", FieldKind.Multiline),
        },

        // ═══ 員工部門 ═══
        ["部門資料"] = new[]
        {
            F("部門編號", req: true, max: 20),
            F("部門名稱", max: 60),
            F("部門倉庫", FieldKind.Lookup, "部門倉庫", "倉庫資料", "倉庫名稱"),
            F("備註", FieldKind.Multiline),
            F("傳輸旗標", FieldKind.Hidden),
        },

        ["員工資料"] = new[]
        {
            F("員工編號", req: true, max: 20),
            F("員工姓名", max: 40),
            F("身份證編號", max: 20),
            F("性別", max: 4),
            F("出生日期", FieldKind.Date),
            F("聯絡電話", max: 40),
            F("行動電話", max: 40),
            F("聯絡地址", max: 100),
            F("戶籍地址", max: 100),
            F("部門編號", FieldKind.Lookup, "部門", "部門資料", "部門名稱"),
            F("工作職務", max: 40),
            F("到職日期", FieldKind.Date),
            F("離職日期", FieldKind.Date),
            F("工作時數", FieldKind.Decimal),
            F("婚姻狀況", max: 10),
            F("血型", max: 4),
            F("籍貫", max: 40),
            F("學歷", max: 60),
            F("經歷", FieldKind.Multiline),
            F("聯絡人", max: 40),
            F("特休天數", FieldKind.Integer),
            F("眷口數", FieldKind.Integer),
            F("扶養人數", FieldKind.Integer),
            F("有配偶", FieldKind.Bool),
            F("銀行帳號", max: 60),
            F("銀行代號", max: 20),
            F("本薪", FieldKind.Decimal),
            F("日薪", FieldKind.Decimal),
            F("時薪", FieldKind.Decimal),
            F("健保金額", FieldKind.Decimal),
            F("勞保金額", FieldKind.Decimal),
            F("全勤資格", FieldKind.Bool),
            F("常日上班", FieldKind.Text, "常日上班時間", max: 20),
            F("常日下班", FieldKind.Text, "常日下班時間", max: 20),
            F("晚班上班", FieldKind.Text, "晚班上班時間", max: 20),
            F("晚班下班", FieldKind.Text, "晚班下班時間", max: 20),
            F("小夜上班", FieldKind.Text, "小夜上班時間", max: 20),
            F("小夜下班", FieldKind.Text, "小夜下班時間", max: 20),
            F("大夜上班", FieldKind.Text, "大夜上班時間", max: 20),
            F("大夜下班", FieldKind.Text, "大夜下班時間", max: 20),
            F("備註", FieldKind.Multiline),
            F("傳輸旗標", FieldKind.Hidden),
            F("相片", FieldKind.Hidden),
            F("員工檔案照", FieldKind.Hidden),
            F("圖檔名稱", FieldKind.Hidden),
            F("圖檔類型", FieldKind.Hidden),
        },

        // ═══ 倉庫物流 ═══
        ["倉庫資料"] = new[]
        {
            F("倉庫編號", req: true, max: 20),
            F("倉庫名稱", max: 60),
            F("聯絡電話", max: 40),
            F("備註", FieldKind.Multiline),
        },

        ["貨運公司"] = new[]
        {
            F("貨運編號", req: true, max: 20),
            F("貨運名稱", max: 60),
            F("聯絡人", max: 40),
            F("聯絡電話", max: 40),
            F("傳真號碼", max: 40),
            F("備註", FieldKind.Multiline),
        },

        ["區域設定"] = new[]
        {
            F("區域編號", req: true, max: 20),
            F("區域名稱", max: 60),
            F("英文名稱", max: 80),
            F("說明", FieldKind.Multiline),
        },

        // ═══ 銀行幣別 ═══
        ["銀行資料"] = new[]
        {
            F("銀行編號", req: true, max: 20),
            F("銀行名稱", max: 60),
            F("聯絡電話", max: 40),
            F("備註", FieldKind.Multiline),
        },

        ["開戶銀行"] = new[]
        {
            F("帳戶編號", req: true, max: 20),
            F("帳戶名稱", max: 60),
            F("銀行編號", FieldKind.Lookup, "銀行", "銀行資料", "銀行名稱", copy: ("銀行名稱", "銀行名稱")),
            F("支票字軌", max: 20),
            F("開戶帳號", max: 60),
            F("開戶幣別", FieldKind.Lookup, "開戶幣別", "幣別匯率", "幣別名稱"),
            F("帳戶類別", max: 20),
            F("票貼額度", FieldKind.Decimal),
            F("票貼折數", FieldKind.Decimal),
            F("部門編號", FieldKind.Lookup, "部門", "部門資料", "部門名稱"),
            F("聯絡人", max: 40),
            F("聯絡電話", max: 40),
            F("傳真號碼", max: 40),
            F("安全餘額", FieldKind.Decimal),
            F("期初餘額", FieldKind.Decimal),
            F("存款科目", FieldKind.Lookup, "存款科目", "會計科目", "科目名稱"),
            F("票貼科目", FieldKind.Lookup, "票貼科目", "會計科目", "科目名稱"),
            F("借款科目", FieldKind.Lookup, "借款科目", "會計科目", "科目名稱"),
            F("備註", FieldKind.Multiline),
            F("現有餘額", FieldKind.Hidden),
        },

        ["幣別匯率"] = new[]
        {
            F("幣別代碼", req: true, max: 10),
            F("幣別名稱", max: 40),
            F("匯率", FieldKind.Decimal),
            F("異動日期", FieldKind.Date),
            F("備註", FieldKind.Multiline),
        },

        // ═══ 車籍資料 ═══
        ["車廠資料"] = new[]
        {
            F("車廠編號", req: true, max: 20),
            F("車廠名稱", max: 60),
            F("備註", FieldKind.Multiline),
        },

        ["車種資料"] = new[]
        {
            F("車種編號", req: true, max: 20),
            F("車種名稱", max: 60),
            F("備註", FieldKind.Multiline),
        },

        // ═══ 縣市鄉鎮 ═══
        ["縣市資料"] = new[]
        {
            F("縣市編號", req: true, max: 10),
            F("縣市名稱", max: 40),
        },

        ["鄉鎮資料"] = new[]
        {
            F("縣市編號", FieldKind.Lookup, "縣市", "縣市資料", "縣市名稱", req: true),
            F("鄉鎮編號", req: true, max: 10),
            F("鄉鎮名稱", max: 40),
            F("郵遞區號", max: 10),
            F("路段編號", max: 20),
        },

        ["路段資料"] = new[]
        {
            F("鄉鎮編號", FieldKind.Lookup, "鄉鎮", "鄉鎮資料", "鄉鎮名稱", req: true),
            F("路段編號", req: true, max: 20),
            F("路段名稱", max: 60),
        },

        // ═══ 會計科目 ═══
        ["會計科目"] = new[]
        {
            F("科目編號", req: true, max: 30),
            F("科目名稱", max: 60),
            F("英文名稱", max: 80),
            F("類別編號", FieldKind.Lookup, "科目類別", "會計類別", "類別名稱"),
            F("期初借貸", max: 4),
            F("期初餘額", FieldKind.Decimal),
            F("常用摘要", max: 60),
            F("沖銷科目", FieldKind.Lookup, "沖銷科目", "會計科目", "科目名稱"),
            F("統制科目", FieldKind.Lookup, "統制科目", "會計科目", "科目名稱"),
            F("隸屬科目", FieldKind.Lookup, "隸屬科目", "會計科目", "科目名稱"),
            F("說明", FieldKind.Multiline),
        },

        ["會計大類"] = new[]
        {
            F("大類編號", req: true, max: 20),
            F("大類名稱", max: 60),
            F("英文名稱", max: 80),
        },

        ["會計類別"] = new[]
        {
            F("大類編號", FieldKind.Lookup, "大類", "會計大類", "大類名稱", req: true),
            F("類別編號", req: true, max: 20),
            F("類別名稱", max: 60),
            F("英文名稱", max: 80),
        },

        // ═══ 貨品附屬主檔 ═══
        ["貨品類別"] = new[]
        {
            F("類別編號", req: true, max: 20),
            F("類別名稱", max: 60),
            F("折數", FieldKind.Decimal),
            F("計價方式", max: 20),
            F("備註", FieldKind.Multiline),
        },

        ["貨品單位"] = new[]
        {
            F("單位編號", req: true, max: 20),
            F("單位名稱", max: 40),
            F("英文名稱", max: 60),
            F("複數名稱", max: 60),
        },

        ["貨品客戶"] = new[]
        {
            F("貨品編號", FieldKind.Lookup, "貨品", "貨品主檔", "品名", req: true, copy: ("原廠編號", "原廠貨號")),
            F("客戶編號", FieldKind.Lookup, "客戶", "客戶廠商", "公司簡稱", req: true),
            F("原廠編號", max: 40),
            F("條碼編號", max: 40),
        },

        ["貨品廠商"] = new[]
        {
            F("貨品編號", FieldKind.Lookup, "貨品", "貨品主檔", "品名", req: true, copy: ("原廠編號", "原廠貨號")),
            F("廠商編號", FieldKind.Lookup, "廠商", "客戶廠商", "公司簡稱", req: true),
            F("原廠編號", max: 40),
            F("條碼編號", max: 40),
        },

        // ═══ 其他主檔 ═══
        ["常用分錄"] = new[]
        {
            F("分錄編號", req: true, max: 20),
            F("分錄類別", max: 20),
            F("分錄名稱", max: 60),
        },

        ["薪資項目"] = new[]
        {
            F("項目編號", req: true, max: 20),
            F("項目名稱", max: 60),
            F("薪資類別", max: 20),
            F("備註", FieldKind.Multiline),
        },

        ["期初餘額"] = new[]
        {
            F("科目編號", FieldKind.Lookup, "科目", "會計科目", "科目名稱", req: true),
            F("借方金額", FieldKind.Decimal),
            F("貸方金額", FieldKind.Decimal),
            F("餘額", FieldKind.Decimal),
            F("建檔序號", FieldKind.Hidden),
        },

        ["科目部門"] = new[]
        {
            F("科目編號", FieldKind.Lookup, "科目", "會計科目", "科目名稱", req: true),
            F("部門編號", FieldKind.Lookup, "部門", "部門資料", "部門名稱", req: true),
            F("期初借貸", max: 4),
            F("期初餘額", FieldKind.Decimal),
        },

        ["專案設定"] = new[]
        {
            F("專案編號", req: true, max: 20),
            F("專案名稱", max: 80),
            F("專案地址", FieldKind.Multiline),
            F("起始日期", FieldKind.Date),
            F("終止日期", FieldKind.Date),
            F("說明", FieldKind.Multiline),
        },

        ["帳龄期間"] = new[]
        {
            F("類別", req: true, max: 10),
            F("第一期間", FieldKind.Integer),
            F("第二期間", FieldKind.Integer),
            F("第三期間", FieldKind.Integer),
            F("第四期間", FieldKind.Integer),
            F("第五期間", FieldKind.Integer),
            F("第六期間", FieldKind.Integer),
        },

        ["年度月份"] = new[]
        {
            F("年月", req: true, max: 10),
            F("年度", FieldKind.Integer),
            F("月份", FieldKind.Integer),
            F("日期", FieldKind.Date),
        },

        ["客戶車歷"] = new[]
        {
            F("客廠編號", FieldKind.Lookup, "客戶/廠商", "客戶廠商", "公司簡稱", req: true),
            F("車牌號碼", req: true, max: 20),
            F("車主名稱", max: 40),
            F("車主電話", max: 40),
            F("行動電話", max: 40),
            F("車主地址", max: 100),
            F("駕駛姓名", max: 40),
            F("駕照號碼", max: 30),
            F("領照日期", FieldKind.Date),
            F("車型", max: 40),
            F("廠牌", max: 40),
            F("年份", FieldKind.Integer),
            F("排氣量", FieldKind.Decimal),
            F("顏色", max: 20),
            F("引擎號碼", max: 30),
            F("車輛類別", max: 20),
            F("里程", FieldKind.Decimal),
            F("英哩", FieldKind.Decimal),
            F("最近維修日", FieldKind.Date),
            F("驗車日期", FieldKind.Date),
        },

        ["財產資料"] = new[]
        {
            F("財產編號", req: true, max: 20),
            F("財產名稱", max: 80),
            F("規格", max: 60),
            F("所在位置", max: 60),
            F("數量", FieldKind.Decimal),
            F("單位", max: 20),
            F("取得日期", FieldKind.Date),
            F("取得原價", FieldKind.Decimal),
            F("現況", max: 20),
            F("耐用月數", FieldKind.Integer),
            F("預留殘值", FieldKind.Decimal),
            F("折舊方法", max: 20),
            F("最近折舊日期", FieldKind.Date),
            F("累計折舊金額", FieldKind.Decimal),
            F("累計折舊科目", FieldKind.Lookup, "累計折舊科目", "會計科目", "科目名稱"),
            F("所屬科目", FieldKind.Lookup, "所屬科目", "會計科目", "科目名稱"),
            F("費用科目", FieldKind.Lookup, "費用科目", "會計科目", "科目名稱"),
            F("改良日期", FieldKind.Date),
            F("改良金額", FieldKind.Decimal),
            F("部門代號", FieldKind.Lookup, "部門", "部門資料", "部門名稱"),
            F("備註", FieldKind.Multiline),
        },

        ["表尾條文"] = new[]
        {
            F("條文編號", req: true, max: 20),
            F("條文名稱", max: 60),
            F("備註", FieldKind.Multiline),
        },

        ["常用詞庫"] = new[]
        {
            F("片語編號", req: true, max: 20),
            F("片語名稱", max: 100),
        },
    };

    /// <summary>取得資料表的欄位定義（未定義回傳 null）</summary>
    public static IReadOnlyList<FieldDef>? Get(string tableName) =>
        Defs.TryGetValue(tableName, out var defs) ? defs : null;

    /// <summary>
    /// 系統維護欄位自動隱藏（僅對「已定義但未列出」的欄位生效）：
    /// 選取旗標、期初/現有/前期/本期/在建/期前餘額、圖檔、傳輸旗標、純英文代碼欄。
    /// </summary>
    public static bool IsAutoHidden(string columnName)
    {
        if (columnName == "選取")
            return true;
        if (columnName.StartsWith("期初", StringComparison.Ordinal) ||
            columnName.StartsWith("現有", StringComparison.Ordinal) ||
            columnName.StartsWith("前期", StringComparison.Ordinal) ||
            columnName.StartsWith("本期", StringComparison.Ordinal) ||
            columnName.StartsWith("在建", StringComparison.Ordinal) ||
            columnName.StartsWith("期前", StringComparison.Ordinal))
            return true;
        if (columnName == "傳輸旗標")
            return true;
        if (columnName == "貨品詳圖" || columnName.Contains("相片") ||
            columnName.Contains("圖檔") || columnName.Contains("圖檔名稱"))
            return true;
        if (columnName.All(ch => ch < 128))
            return true;   // 純英文欄名視為系統代碼欄
        return false;
    }
}
