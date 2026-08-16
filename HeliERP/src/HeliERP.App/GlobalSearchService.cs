// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（全域快速搜尋）
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 全域快速搜尋（Ctrl+K）：跨客戶／廠商、貨品、員工、交易單據、折讓單據
/// 即時檢索，選取後可直接開啟對應資料檢視（GenericTableForm 過濾）。
/// 仿 2026 主流 ERP 的 Global Search 設計。
/// </summary>
public static class GlobalSearchService
{
    /// <summary>搜尋結果一筆：類別（分組）、顯示文字、跳轉目標表與過濾條件。</summary>
    public sealed record SearchHit(string 類別, string 顯示, string 表名, string 過濾);

    public static List<SearchHit> Search(string keyword)
    {
        var kw = keyword.Trim();
        var hits = new List<SearchHit>();
        if (kw.Length == 0) return hits;
        string like = "%" + kw + "%";

        SearchParties(hits, like);
        SearchProducts(hits, like);
        SearchStaff(hits, like);
        SearchTradeBills(hits, like);
        SearchDiscountBills(hits, like);
        return hits;
    }

    private static void SearchParties(List<SearchHit> hits, string like)
    {
        var dt = DbManager.QueryTable(
            "SELECT [客廠編號],[客廠類別],[公司簡稱],[統一編號] FROM [客戶廠商] " +
            "WHERE [客廠編號] LIKE $k OR [公司簡稱] LIKE $k OR [公司全名] LIKE $k OR [統一編號] LIKE $k " +
            "ORDER BY [客廠類別], [客廠編號] LIMIT 8",
            DbManager.Param("$k", like));
        foreach (DataRow r in dt.Rows)
        {
            hits.Add(new SearchHit(
                "客戶/廠商",
                $"{Str(r["客廠編號"])}　{Str(r["公司簡稱"])}　〔{Str(r["客廠類別"])}〕" +
                (Str(r["統一編號"]).Length > 0 ? $"　統編：{Str(r["統一編號"])}" : ""),
                "客戶廠商", Str(r["客廠編號"])));
        }
    }

    private static void SearchProducts(List<SearchHit> hits, string like)
    {
        var dt = DbManager.QueryTable(
            "SELECT [貨品編號],[品名],[包裝] FROM [貨品主檔] " +
            "WHERE [貨品編號] LIKE $k OR [品名] LIKE $k OR [包裝] LIKE $k " +
            "ORDER BY [貨品編號] LIMIT 8",
            DbManager.Param("$k", like));
        foreach (DataRow r in dt.Rows)
        {
            hits.Add(new SearchHit(
                "貨品",
                $"{Str(r["貨品編號"])}　{Str(r["品名"])}" +
                (Str(r["包裝"]).Length > 0 ? $"　包裝：{Str(r["包裝"])}" : ""),
                "貨品主檔", Str(r["貨品編號"])));
        }
    }

    private static void SearchStaff(List<SearchHit> hits, string like)
    {
        var dt = DbManager.QueryTable(
            "SELECT [員工編號],[員工姓名] FROM [員工資料] " +
            "WHERE [員工編號] LIKE $k OR [員工姓名] LIKE $k ORDER BY [員工編號] LIMIT 6",
            DbManager.Param("$k", like));
        foreach (DataRow r in dt.Rows)
        {
            hits.Add(new SearchHit(
                "員工",
                $"{Str(r["員工編號"])}　{Str(r["員工姓名"])}",
                "員工資料", Str(r["員工編號"])));
        }
    }

    private static void SearchTradeBills(List<SearchHit> hits, string like)
    {
        var dt = DbManager.QueryTable(
            "SELECT T.[交易單號], T.[單據類別], COALESCE(T.[交易對象],'') AS [對象編號], " +
            "COALESCE(C.[公司簡稱],'') AS [公司簡稱], COALESCE(T.[總計金額],0) AS [總計金額], " +
            "T.[交易日期] FROM [交易主檔] T " +
            "LEFT JOIN [客戶廠商] C ON T.[交易對象] = C.[客廠編號] " +
            "WHERE T.[交易單號] LIKE $k OR COALESCE(C.[公司簡稱],'') LIKE $k " +
            "ORDER BY T.[交易日期] DESC LIMIT 8",
            DbManager.Param("$k", like));
        foreach (DataRow r in dt.Rows)
        {
            hits.Add(new SearchHit(
                "交易單據",
                $"{Str(r["交易單號"])}　〔{Str(r["單據類別"])}〕　{Str(r["公司簡稱"])}　" +
                $"金額 {Dec(r["總計金額"]):N0}　{Str(r["交易日期"])}",
                "交易主檔", Str(r["交易單號"])));
        }
    }

    private static void SearchDiscountBills(List<SearchHit> hits, string like)
    {
        var dt = DbManager.QueryTable(
            "SELECT D.[折讓單號], D.[單據類別], COALESCE(D.[對象編號],'') AS [對象編號], " +
            "COALESCE(C.[公司簡稱],'') AS [公司簡稱], COALESCE(D.[總計金額],0) AS [總計金額], " +
            "D.[折讓日期] FROM [折讓主檔] D " +
            "LEFT JOIN [客戶廠商] C ON D.[對象編號] = C.[客廠編號] " +
            "WHERE D.[折讓單號] LIKE $k OR COALESCE(C.[公司簡稱],'') LIKE $k " +
            "ORDER BY D.[折讓日期] DESC LIMIT 8",
            DbManager.Param("$k", like));
        foreach (DataRow r in dt.Rows)
        {
            hits.Add(new SearchHit(
                "折讓單據",
                $"{Str(r["折讓單號"])}　〔{Str(r["單據類別"])}〕　{Str(r["公司簡稱"])}　" +
                $"金額 {Dec(r["總計金額"]):N0}　{Str(r["折讓日期"])}",
                "折讓主檔", Str(r["折讓單號"])));
        }
    }

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";
    private static decimal Dec(object? v) =>
        v is null or DBNull ? 0m : (decimal.TryParse(v.ToString(), out var m) ? m : 0m);
}
