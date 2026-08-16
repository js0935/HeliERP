// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════

namespace HeliERP.App;

/// <summary>資料表維護模式</summary>
public enum TableMode
{
    /// <summary>可編輯（基本資料、系統設定）</summary>
    Editable,

    /// <summary>唯讀檢視（交易資料、彙總報表——正式交易畫面於後續階段提供）</summary>
    ReadOnly,

    /// <summary>系統內部衍生表，不出現在瀏覽器與選單（暫存/流水/衍生統計）</summary>
    Hidden,
}

/// <summary>資料表分類定義；KeyFields 為邏輯主鍵（SQLite 未宣告 PRIMARY KEY，依業務慣例指定）</summary>
public sealed record TableDef(string Name, string Main, string Sub, TableMode Mode, string[] KeyFields);

/// <summary>
/// 資料表分類目錄：將資料庫全部資料表依業務功能分門別類，
/// 作為「依資料庫恢復功能」的地圖。分類對應舊系統（Pili6）的模組結構。
/// </summary>
public static class TableCatalog
{
    private static TableDef E(string n, string m, string s, params string[] k) => new(n, m, s, TableMode.Editable, k);
    private static TableDef R(string n, string m, string s) => new(n, m, s, TableMode.ReadOnly, Array.Empty<string>());
    private static TableDef H(string n, string m, string s) => new(n, m, s, TableMode.Hidden, Array.Empty<string>());

    /// <summary>全部資料表分類定義（135 表）</summary>
    private static readonly TableDef[] Tables =
    {
        // ═══ 基本資料 ═══
        E("貨品主檔", "基本資料", "貨品資料", "貨品編號"),
        E("貨品類別", "基本資料", "貨品資料", "類別編號"),
        E("貨品單位", "基本資料", "貨品資料", "單位編號"),
        E("貨品客戶", "基本資料", "貨品資料", "貨品編號", "客戶編號"),
        E("貨品廠商", "基本資料", "貨品資料", "貨品編號", "廠商編號"),
        E("貨品成本", "基本資料", "貨品資料", "貨品編號", "年度"),
        E("貨品替代", "基本資料", "貨品資料", "建檔序號"),
        E("貨品組合", "基本資料", "貨品資料", "建檔序號"),

        E("客戶廠商", "基本資料", "客戶廠商", "客廠編號"),
        E("客廠類別", "基本資料", "客戶廠商", "客廠類別", "類別編號"),
        E("客戶車歷", "基本資料", "客戶廠商", "客廠編號", "車牌號碼"),
        E("縣市資料", "基本資料", "客戶廠商", "縣市編號"),
        E("鄉鎮資料", "基本資料", "客戶廠商", "縣市編號", "鄉鎮編號"),
        E("路段資料", "基本資料", "客戶廠商", "鄉鎮編號", "路段編號"),

        E("員工資料", "基本資料", "員工部門", "員工編號"),
        E("部門資料", "基本資料", "員工部門", "部門編號"),

        E("倉庫資料", "基本資料", "倉庫物流", "倉庫編號"),
        E("貨運公司", "基本資料", "倉庫物流", "貨運編號"),
        E("區域設定", "基本資料", "倉庫物流", "區域編號"),
        E("專案設定", "基本資料", "倉庫物流", "專案編號"),

        E("銀行資料", "基本資料", "銀行幣別", "銀行編號"),
        E("開戶銀行", "基本資料", "銀行幣別", "帳戶編號"),
        E("幣別匯率", "基本資料", "銀行幣別", "幣別代碼"),

        E("車廠資料", "基本資料", "車籍資料", "車廠編號"),
        E("車種資料", "基本資料", "車籍資料", "車種編號"),

        E("健保對照", "基本資料", "薪資基礎", "健保等級"),
        E("勞保對照", "基本資料", "薪資基礎", "級數"),
        E("所得稅表", "基本資料", "薪資基礎", "稅額編號"),
        E("薪資項目", "基本資料", "薪資基礎", "項目編號"),

        E("財產資料", "基本資料", "資產", "財產編號"),

        E("會計科目", "基本資料", "會計科目", "科目編號"),
        E("會計大類", "基本資料", "會計科目", "大類編號"),
        E("會計類別", "基本資料", "會計科目", "大類編號", "類別編號"),
        E("科目部門", "基本資料", "會計科目", "科目編號", "部門編號"),
        E("常用分錄", "基本資料", "會計科目", "分錄編號"),
        E("預估科目", "基本資料", "會計科目", "科目編號", "類別"),
        E("預估項目", "基本資料", "會計科目", "項目編號", "類別"),
        E("傳輸科目", "基本資料", "會計科目", "Id"),
        E("加減項目", "基本資料", "會計科目", "項目編號"),
        E("科目預算", "基本資料", "會計科目", "科目編號", "部門編號", "預算年度"),
        E("帳龄期間", "基本資料", "會計科目", "類別"),
        E("期初餘額", "基本資料", "會計科目", "科目編號"),

        // ═══ 系統設定 ═══
        E("系統參數", "系統設定", "參數設定", "編號"),
        E("系統項目", "系統設定", "參數設定", "作業編號"),
        E("庫存參數", "系統設定", "參數設定", "參數編號"),
        E("會計參數", "系統設定", "參數設定", "系統編號"),
        E("薪資設定", "系統設定", "參數設定", "員工編號", "計薪編號"),
        E("年度月份", "系統設定", "參數設定", "年月"),
        E("版本資訊", "系統設定", "參數設定", "版本編號"),

        E("單據設定", "系統設定", "單據報表", "no"),
        E("發票設定", "系統設定", "單據報表", "序號"),
        E("列印區間", "系統設定", "單據報表", "日期區間", "編號區間"),
        E("報表格式", "系統設定", "單據報表", "報表編號"),
        E("表尾條文", "系統設定", "單據報表", "條文編號"),
        E("常用詞庫", "系統設定", "單據報表", "片語編號"),

        E("權限主檔", "系統設定", "權限管理", "使用者編號"),
        E("權限明細", "系統設定", "權限管理", "使用者編號", "序號"),
        E("登入記錄", "系統設定", "權限管理", "序號"),
        E("郵寄檔案", "系統設定", "權限管理", "建檔序號"),

        // ═══ 交易資料（唯讀，正式交易畫面於後續階段提供） ═══
        R("交易主檔", "交易資料", "進銷存"),
        R("交易明細", "交易資料", "進銷存"),
        R("採訂主檔", "交易資料", "進銷存"),
        R("採訂明細", "交易資料", "進銷存"),
        R("折讓主檔", "交易資料", "進銷存"),
        R("折讓明細", "交易資料", "進銷存"),

        R("驗貨主檔", "交易資料", "生管"),
        R("驗貨明細", "交易資料", "生管"),
        R("託運主檔", "交易資料", "生管"),
        R("託運明細", "交易資料", "生管"),
        R("託運對帳", "交易資料", "生管"),

        R("發票主檔", "交易資料", "發票"),
        R("發票明細", "交易資料", "發票"),

        R("維修主檔", "交易資料", "維修"),
        R("維修明細", "交易資料", "維修"),

        R("調整數量", "交易資料", "庫存調整"),
        R("調整金額", "交易資料", "庫存調整"),
        R("進出明細", "交易資料", "庫存調整"),

        R("帳款主檔", "交易資料", "帳款收付"),
        R("帳款明細", "交易資料", "帳款收付"),
        R("收付主檔", "交易資料", "帳款收付"),
        R("收付明細", "交易資料", "帳款收付"),
        R("銀行存提", "交易資料", "帳款收付"),
        R("銀行存款", "交易資料", "帳款收付"),
        R("銀行轉帳", "交易資料", "帳款收付"),
        R("票據收付", "交易資料", "帳款收付"),
        R("批次票據", "交易資料", "帳款收付"),

        R("薪資主檔", "交易資料", "薪資"),
        R("薪資明細", "交易資料", "薪資"),

        R("出缺主檔", "交易資料", "出缺"),
        R("出缺明細", "交易資料", "出缺"),

        R("傳票主檔", "交易資料", "會計"),
        R("傳票明細", "交易資料", "會計"),
        R("分攤主檔", "交易資料", "會計"),
        R("分攤明細", "交易資料", "會計"),

        // ═══ 彙總報表（唯讀） ═══
        R("庫存數量", "彙總報表", "庫存"),
        R("貨品庫存", "彙總報表", "庫存"),
        R("帳款簡要", "彙總報表", "帳款"),
        R("帳齡分析", "彙總報表", "帳款"),
        R("各月帳款", "彙總報表", "帳款"),
        R("總分類帳", "彙總報表", "會計帳簿"),
        R("日記帳簿", "彙總報表", "會計帳簿"),
        R("現金帳簿", "彙總報表", "會計帳簿"),
        R("明細類帳", "彙總報表", "會計帳簿"),
        R("損益報表", "彙總報表", "會計帳簿"),
        R("資產負債", "彙總報表", "會計帳簿"),
        R("出退統計", "彙總報表", "統計"),
        R("員工績效", "彙總報表", "統計"),
        R("部門績效", "彙總報表", "統計"),

        // ═══ 系統內部（隱藏：暫存/流水/衍生統計，由系統自動產生） ═══
        H("交易暫存", "系統內部", "暫存"),
        H("進出暫存", "系統內部", "暫存"),
        H("交易異動", "系統內部", "流水"),
        H("異動明細", "系統內部", "流水"),
        H("分錄明細", "系統內部", "流水"),
        H("總帳金額", "系統內部", "流水"),
        H("借方筆數", "系統內部", "衍生"),
        H("貸方筆數", "系統內部", "衍生"),
        H("前期成本", "系統內部", "衍生"),
        H("前期數量", "系統內部", "衍生"),
        H("前期金額", "系統內部", "衍生"),
        H("期初數量", "系統內部", "衍生"),
        H("期初金額", "系統內部", "衍生"),
        H("本期數量", "系統內部", "衍生"),
        H("本期金額", "系統內部", "衍生"),
        H("加權平均", "系統內部", "衍生"),
        H("進貨數量", "系統內部", "衍生"),
        H("進貨金額", "系統內部", "衍生"),
        H("銷貨數量", "系統內部", "衍生"),
        H("銷貨金額", "系統內部", "衍生"),
        H("銷售金額", "系統內部", "衍生"),
        H("利潤分析", "系統內部", "衍生"),
        H("折舊提列", "系統內部", "衍生"),
        H("折舊明細", "系統內部", "衍生"),
        H("封存主檔", "系統內部", "封存"),
        H("封存明細", "系統內部", "封存"),
        H("存款明細", "系統內部", "流水"),
    };

    private static readonly Dictionary<string, TableDef> ByName =
        Tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>全部定義（不含 Hidden）</summary>
    public static IReadOnlyList<TableDef> GetVisible() => Tables.Where(t => t.Mode != TableMode.Hidden).ToArray();

    /// <summary>全部定義（含 Hidden，供系統內部使用）</summary>
    public static IReadOnlyList<TableDef> GetAll() => Tables;

    /// <summary>依表名取得模式（不存在回傳 ReadOnly）</summary>
    public static TableMode GetMode(string tableName) =>
        ByName.TryGetValue(tableName, out var def) ? def.Mode : TableMode.ReadOnly;

    /// <summary>依表名取得定義（不存在回傳 null）</summary>
    public static TableDef? Get(string tableName) =>
        ByName.TryGetValue(tableName, out var def) ? def : null;

    /// <summary>取得邏輯主鍵欄位（無定義回傳空清單）</summary>
    public static IReadOnlyList<string> GetKeyFields(string tableName) =>
        ByName.TryGetValue(tableName, out var def) ? def.KeyFields : Array.Empty<string>();

    /// <summary>依關鍵字搜尋表名（不含 Hidden），排序：名稱開頭優先</summary>
    public static IReadOnlyList<TableDef> Find(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return GetVisible();
        var kw = keyword.Trim();
        return Tables
            .Where(t => t.Mode != TableMode.Hidden && t.Name.Contains(kw, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name.StartsWith(kw, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>頂層分類清單（依定義順序）</summary>
    public static IReadOnlyList<string> GetMains() =>
        Tables.Select(t => t.Main).Distinct().ToArray();

    /// <summary>頂層分類下的子分類清單</summary>
    public static IReadOnlyList<string> GetSubs(string main) =>
        Tables.Where(t => t.Main == main).Select(t => t.Sub).Distinct().ToArray();

    /// <summary>特定分類下的表（不含 Hidden）</summary>
    public static IReadOnlyList<TableDef> GetTables(string main, string sub) =>
        Tables.Where(t => t.Main == main && t.Sub == sub && t.Mode != TableMode.Hidden).ToArray();

    /// <summary>頂層分類下的全部表（不含 Hidden）</summary>
    public static IReadOnlyList<TableDef> GetTables(string main) =>
        Tables.Where(t => t.Main == main && t.Mode != TableMode.Hidden).ToArray();
}
