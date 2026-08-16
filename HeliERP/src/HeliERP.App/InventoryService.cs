// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 庫存查詢核心：庫存現量、異動歷史、類別彙總。
/// 現量以「貨品庫存」為準（TradeService 出貨扣/退回加皆更新此表），
/// 成本取自「貨品主檔」現行平均成本；異動歷史由交易明細反查。
/// </summary>
public static class InventoryService
{
    /// <summary>庫存現量（貨品庫存 × 貨品主檔 × 倉庫資料）</summary>
    public static DataTable LoadStock(string? 貨品編號 = null, string? 品名 = null,
        string? 倉庫編號 = null, string? 類別編號 = null, bool 僅不足 = false)
    {
        var cond = new List<string>();
        var pars = new List<SqliteParameter>();
        if (!string.IsNullOrWhiteSpace(貨品編號))
        {
            cond.Add("k.[貨品編號] LIKE $g");
            pars.Add(DbManager.Param("$g", 貨品編號.Trim() + "%"));
        }
        if (!string.IsNullOrWhiteSpace(品名))
        {
            cond.Add("p.[品名] LIKE $n");
            pars.Add(DbManager.Param("$n", "%" + 品名.Trim() + "%"));
        }
        if (!string.IsNullOrWhiteSpace(倉庫編號))
        {
            cond.Add("k.[倉庫編號] = $w");
            pars.Add(DbManager.Param("$w", 倉庫編號));
        }
        if (!string.IsNullOrWhiteSpace(類別編號))
        {
            cond.Add("p.[類別編號] = $c");
            pars.Add(DbManager.Param("$c", 類別編號));
        }
        if (僅不足)
            cond.Add("COALESCE(k.[現有數量],0) < COALESCE(k.[安全存量],0)");

        var where = cond.Count == 0 ? "" : " WHERE " + string.Join(" AND ", cond);
        return DbManager.QueryTable(
            "SELECT COALESCE(k.[貨品編號],'') AS [貨品編號], COALESCE(MAX(p.[品名]),'') AS [品名], " +
            "COALESCE(MAX(p.[規格]),'') AS [規格], COALESCE(MAX(p.[類別編號]),'') AS [類別編號], " +
            "COALESCE(k.[倉庫編號],'') AS [倉庫編號], COALESCE(MAX(w.[倉庫名稱]),'') AS [倉庫名稱], " +
            "MAX(COALESCE(p.[基本單位],'')) AS [基本單位], " +
            "SUM(COALESCE(k.[期初數量],0)) AS [期初數量], SUM(COALESCE(k.[現有數量],0)) AS [現有數量], " +
            "MAX(COALESCE(k.[安全存量],0)) AS [安全存量], MAX(COALESCE(p.[現行平均成本],0)) AS [平均成本], " +
            "MAX(COALESCE(p.[標準成本],0)) AS [標準成本], " +
            "ROUND(SUM(COALESCE(k.[現有數量],0)) * MAX(COALESCE(p.[現行平均成本],0)), 2) AS [庫存總值], " +
            "COALESCE(MAX(p.[最近進貨日]),'') AS [最近進貨日], COALESCE(MAX(p.[最近出貨日]),'') AS [最近出貨日] " +
            "FROM [貨品庫存] k " +
            "LEFT JOIN [貨品主檔] p ON p.[貨品編號] = k.[貨品編號] " +
            "LEFT JOIN [倉庫資料] w ON w.[倉庫編號] = k.[倉庫編號]" + where +
            " GROUP BY k.[貨品編號], k.[倉庫編號]" +
            " ORDER BY k.[貨品編號], k.[倉庫編號]", pars.ToArray());
    }

    /// <summary>
    /// 異動歷史（交易明細 × 交易主檔 × 貨品主檔）。
    /// 異動方向依單據類別：出貨/進退 = 庫存減（負）、出退/進貨 = 庫存增（正）、
    /// 庫存調整 = 明細數量即為帶方向之調整量（盤盈正 / 盤虧負）。
    /// </summary>
    public static DataTable LoadMovements(string? 貨品編號 = null)
    {
        var cond = new List<string>
        {
            "COALESCE(d.[計算庫存],0) = 1",
            "COALESCE(d.[贈品],0) = 0",
            "COALESCE(d.[服務項目],0) = 0",
        };
        var pars = new List<SqliteParameter>();
        if (!string.IsNullOrWhiteSpace(貨品編號))
        {
            cond.Add("d.[貨品編號] = $g");
            pars.Add(DbManager.Param("$g", 貨品編號.Trim()));
        }
        var where = " WHERE " + string.Join(" AND ", cond);
        return DbManager.QueryTable(
            "SELECT m.[交易日期], m.[單據類別], m.[交易單號], m.[交易對象], d.[貨品編號], " +
            "COALESCE(p.[品名],'') AS [品名], COALESCE(d.[倉庫編號],'') AS [倉庫編號], " +
            "COALESCE(v.[公司簡稱],'') AS [公司簡稱], d.[數量], COALESCE(d.[單位],'') AS [單位], " +
            "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額], " +
            "CASE m.[單據類別] WHEN '出貨' THEN -d.[數量] WHEN '進退' THEN -d.[數量] " +
            "WHEN '出退' THEN d.[數量] WHEN '進貨' THEN d.[數量] " +
            "WHEN '庫存調整' THEN d.[數量] ELSE 0 END AS [異動數量], " +
            "SUM(CASE m.[單據類別] WHEN '出貨' THEN -d.[數量] WHEN '進退' THEN -d.[數量] " +
            "WHEN '出退' THEN d.[數量] WHEN '進貨' THEN d.[數量] " +
            "WHEN '庫存調整' THEN d.[數量] ELSE 0 END) " +
            "OVER (PARTITION BY d.[貨品編號] ORDER BY m.[交易日期], d.[建檔序號]) AS [累計], " +
            "COALESCE(m.[製單],'') AS [製單] " +
            "FROM [交易明細] d " +
            "JOIN [交易主檔] m ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [貨品主檔] p ON p.[貨品編號] = d.[貨品編號] " +
            "LEFT JOIN [客戶廠商] v ON v.[客廠編號] = m.[交易對象]" + where +
            " ORDER BY m.[交易日期] DESC, d.[建檔序號] DESC", pars.ToArray());
    }

    /// <summary>庫存調整明細（庫存調整單之明細，供「庫存調整明細表」報表使用）。</summary>
    public static DataTable LoadAdjustmentDetails()
    {
        return DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易單號], d.[貨品編號], COALESCE(p.[品名],'') AS [品名], " +
            "COALESCE(d.[倉庫編號],'') AS [倉庫編號], COALESCE(d.[單位],'') AS [單位], " +
            "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單價],0) AS [單價], " +
            "COALESCE(d.[折扣],100) AS [折扣], COALESCE(d.[金額],0) AS [金額], " +
            "COALESCE(d.[附註說明],'') AS [附註說明] " +
            "FROM [交易明細] d " +
            "JOIN [交易主檔] m ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [貨品主檔] p ON p.[貨品編號] = d.[貨品編號] " +
            "WHERE m.[單據類別] = '庫存調整' AND COALESCE(d.[計算庫存],0) = 1 " +
            "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
    }

    /// <summary>類別彙總（貨品數 / 期初 / 現量 / 庫存總值）</summary>
    public static DataTable LoadCategorySummary()
    {
        return DbManager.QueryTable(
            "SELECT COALESCE(c.[類別編號],'') AS [類別編號], COALESCE(c.[類別名稱],'未分類') AS [類別名稱], " +
            "COALESCE(k.[倉庫編號],'') AS [倉庫編號], COUNT(DISTINCT k.[貨品編號]) AS [貨品數], " +
            "COALESCE(SUM(k.[期初數量]),0) AS [期初數量合計], " +
            "COALESCE(SUM(k.[現有數量]),0) AS [現有數量合計], " +
            "ROUND(COALESCE(SUM(k.[現有數量] * p.[現行平均成本]),0), 2) AS [庫存總值合計] " +
            "FROM [貨品庫存] k " +
            "LEFT JOIN [貨品主檔] p ON p.[貨品編號] = k.[貨品編號] " +
            "LEFT JOIN [貨品類別] c ON c.[類別編號] = p.[類別編號] " +
            "GROUP BY c.[類別編號], c.[類別名稱], k.[倉庫編號] " +
            "ORDER BY c.[類別編號], k.[倉庫編號]");
    }

    /// <summary>倉庫下拉資料</summary>
    public static DataTable LoadWarehouses() =>
        DbManager.QueryTable("SELECT [倉庫編號], [倉庫名稱] FROM [倉庫資料] ORDER BY [倉庫編號]");

    /// <summary>貨品類別下拉資料</summary>
    public static DataTable LoadCategories() =>
        DbManager.QueryTable("SELECT [類別編號], [類別名稱] FROM [貨品類別] ORDER BY [類別編號]");
}
