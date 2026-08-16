// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>主畫面儀表板統計資料</summary>
public sealed class DashboardData
{
    public int 庫存不足筆數;
    public decimal 應收餘額;
    public int 未收單據筆數;
    public decimal 應付餘額;
    public int 未付單據筆數;
    public decimal 今日出貨金額;
    public int 今日出貨筆數;
    public decimal 本月進貨金額;
    public int 本月進貨筆數;
    public DataTable 庫存不足清單 = new();

    // ── 商業智慧（2026 儀表板）──
    public string[] 月份標籤 = Array.Empty<string>();
    public decimal[] 近12月營收 = Array.Empty<decimal>();
    public decimal[] 近12月進貨 = Array.Empty<decimal>();
    public decimal[] 近12月折讓 = Array.Empty<decimal>();
    /// <summary>應收帳款帳齡：未逾期 / 1-30 / 31-60 / 61-90 / 90天以上。</summary>
    public decimal[] 應收帳齡 = new decimal[5];
    public decimal 逾期未收金額;
    /// <summary>應付帳款帳齡：未逾期 / 1-30 / 31-60 / 61-90 / 90天以上。</summary>
    public decimal[] 應付帳齡 = new decimal[5];
    public decimal 逾期未付金額;
    public DataTable 客戶業績TOP = new();
    public int 客戶數;
    public int 廠商數;
    public int 貨品數;
    public decimal 庫存總額;
    public int 本月出貨筆數;
    public decimal 本月折讓金額;
    public int 今日折讓筆數;
    public int 本月折讓筆數;
    public int 待核准筆數;
}

/// <summary>
/// 主畫面儀表板：庫存不足警示、應收／應付餘額、今日出貨、本月進貨。
/// 資料來源：貨品庫存（不足清單）、帳款主檔／簡要（餘額與未收付筆數）、交易主檔（出貨／進貨統計）。
/// </summary>
public static class DashboardService
{
    public static DashboardData Load()
    {
        var d = new DashboardData();

        // 1. 庫存不足（現有 < 安全存量）
        var shortDt = InventoryService.LoadStock(僅不足: true);
        d.庫存不足筆數 = shortDt.Rows.Count;
        d.庫存不足清單 = shortDt.Clone();
        foreach (DataRow r in shortDt.Rows)
        {
            if (d.庫存不足清單.Rows.Count >= 8) break;
            d.庫存不足清單.ImportRow(r);
        }

        // 2. 應收／應付餘額（帳款主檔未收付合計加總）
        d.應收餘額 = SumColumn(ARService.LoadObjectSummary(ARService.應收類別), "未收付合計");
        d.應付餘額 = SumColumn(ARService.LoadObjectSummary(ARService.應付類別), "未收付合計");

        // 3. 未收／未付單據筆數（帳款簡要依客廠類別，僅計待收付正數）
        d.未收單據筆數 = CountOpenBills(ARService.應收類別);
        d.未付單據筆數 = CountOpenBills(ARService.應付類別);

        // 4. 今日出貨／本月進貨（交易主檔）
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        string month = DateTime.Now.ToString("yyyy-MM");
        d.今日出貨金額 = ScalarDec(
            "SELECT COALESCE(SUM([總計金額]),0) FROM [交易主檔] WHERE [單據類別] = '出貨' AND [交易日期] LIKE $p",
            DbManager.Param("$p", today + "%"));
        d.今日出貨筆數 = ScalarInt(
            "SELECT COUNT(*) FROM [交易主檔] WHERE [單據類別] = '出貨' AND [交易日期] LIKE $p",
            DbManager.Param("$p", today + "%"));
        d.本月進貨金額 = ScalarDec(
            "SELECT COALESCE(SUM([總計金額]),0) FROM [交易主檔] WHERE [單據類別] = '進貨' AND [交易日期] LIKE $p",
            DbManager.Param("$p", month + "%"));
        d.本月進貨筆數 = ScalarInt(
            "SELECT COUNT(*) FROM [交易主檔] WHERE [單據類別] = '進貨' AND [交易日期] LIKE $p",
            DbManager.Param("$p", month + "%"));

        // ── 商業智慧 ──
        LoadTrend(d);
        LoadAging(d);
        LoadTopCustomers(d);
        LoadKpi(d);

        // 5. 待核准單據（多層核准流程尚未核准者）
        d.待核准筆數 = ApprovalService.LoadFlows(null, ApprovalService.待核准, "").Rows.Count;
        return d;
    }

    /// <summary>近 12 個月出貨／進貨／折讓金額趨勢（無資料月份補 0）。</summary>
    private static void LoadTrend(DashboardData d)
    {
        var labels = new List<string>();
        var start = DateTime.Now.AddMonths(-11);
        var period = new List<(string Ym, DateTime First)>();
        for (int i = 0; i < 12; i++)
        {
            var first = start.AddMonths(i);
            var ym = first.ToString("yyyy-MM");
            labels.Add(first.ToString("MM月"));
            period.Add((ym, first));
        }
        d.月份標籤 = labels.ToArray();
        var 出貨 = new decimal[12];
        var 進貨 = new decimal[12];
        var 折讓 = new decimal[12];

        var rows = DbManager.QueryTable(
            "SELECT substr([交易日期],1,7) AS ym, [單據類別], COALESCE(SUM([總計金額]),0) AS amt " +
            "FROM [交易主檔] WHERE [交易日期] >= $start GROUP BY ym, [單據類別]",
            DbManager.Param("$start", start.ToString("yyyy-MM-01")));
        foreach (DataRow r in rows.Rows)
        {
            string ym = r["ym"]?.ToString() ?? "";
            int idx = period.FindIndex(p => p.Ym == ym);
            if (idx < 0) continue;
            decimal amt = r["amt"] is DBNull or null ? 0m : Convert.ToDecimal(r["amt"]);
            switch (r["單據類別"]?.ToString())
            {
                case "出貨": 出貨[idx] = amt; break;
                case "進貨": 進貨[idx] = amt; break;
            }
        }
        var discRows = DbManager.QueryTable(
            "SELECT substr([折讓日期],1,7) AS ym, COALESCE(SUM([總計金額]),0) AS amt " +
            "FROM [折讓主檔] WHERE [折讓日期] >= $start GROUP BY ym",
            DbManager.Param("$start", start.ToString("yyyy-MM-01")));
        foreach (DataRow r in discRows.Rows)
        {
            int idx = period.FindIndex(p => p.Ym == (r["ym"]?.ToString() ?? ""));
            if (idx >= 0)
                折讓[idx] = r["amt"] is DBNull or null ? 0m : Convert.ToDecimal(r["amt"]);
        }
        d.近12月營收 = 出貨;
        d.近12月進貨 = 進貨;
        d.近12月折讓 = 折讓;
    }

    /// <summary>應收／應付帳款帳齡分佈（帳款簡要未收付依交易日期推算天數）。</summary>
    private static void LoadAging(DashboardData d)
    {
        d.應收帳齡 = LoadAgingKind(ARService.應收類別);
        d.應付帳齡 = LoadAgingKind(ARService.應付類別);
        d.逾期未收金額 = d.應收帳齡[1] + d.應收帳齡[2] + d.應收帳齡[3] + d.應收帳齡[4];
        d.逾期未付金額 = d.應付帳齡[1] + d.應付帳齡[2] + d.應付帳齡[3] + d.應付帳齡[4];
    }

    private static decimal[] LoadAgingKind(string 客廠類別)
    {
        var result = new decimal[5];
        var row = DbManager.QueryTable(
            "SELECT " +
            "COALESCE(SUM(CASE WHEN age <= 0 THEN v END),0) AS c0, " +
            "COALESCE(SUM(CASE WHEN age BETWEEN 1 AND 30 THEN v END),0) AS c30, " +
            "COALESCE(SUM(CASE WHEN age BETWEEN 31 AND 60 THEN v END),0) AS c60, " +
            "COALESCE(SUM(CASE WHEN age BETWEEN 61 AND 90 THEN v END),0) AS c90, " +
            "COALESCE(SUM(CASE WHEN age > 90 THEN v END),0) AS c999 " +
            "FROM (SELECT julianday('now') - julianday([交易日期]) AS age, [未收付金額] AS v " +
            "FROM [帳款簡要] A JOIN [客戶廠商] C ON A.[交易對象] = C.[客廠編號] " +
            "AND C.[客廠類別] = $t WHERE A.[未收付金額] > 0)",
            DbManager.Param("$t", 客廠類別));
        if (row.Rows.Count > 0)
        {
            result = new[]
            {
                GetDec(row.Rows[0]["c0"]), GetDec(row.Rows[0]["c30"]),
                GetDec(row.Rows[0]["c60"]), GetDec(row.Rows[0]["c90"]),
                GetDec(row.Rows[0]["c999"]),
            };
        }
        return result;
    }

    /// <summary>近 6 個月客戶業績 TOP（出貨金額）。</summary>
    private static void LoadTopCustomers(DashboardData d)
    {
        var start = DateTime.Now.AddMonths(-6).ToString("yyyy-MM-01");
        d.客戶業績TOP = DbManager.QueryTable(
            "SELECT COALESCE(C.[公司簡稱],'') AS [客戶], COALESCE(SUM(T.[總計金額]),0) AS [業績] " +
            "FROM [交易主檔] T LEFT JOIN [客戶廠商] C ON T.[交易對象] = C.[客廠編號] " +
            "WHERE T.[單據類別] = '出貨' AND T.[交易日期] >= $start " +
            "GROUP BY C.[公司簡稱] ORDER BY [業績] DESC LIMIT 8",
            DbManager.Param("$start", start));
    }

    private static void LoadKpi(DashboardData d)
    {
        d.客戶數 = ScalarInt("SELECT COUNT(*) FROM [客戶廠商] WHERE [客廠類別] = $t", DbManager.Param("$t", "客戶"));
        d.廠商數 = ScalarInt("SELECT COUNT(*) FROM [客戶廠商] WHERE [客廠類別] = $t", DbManager.Param("$t", "廠商"));
        d.貨品數 = ScalarInt("SELECT COUNT(*) FROM [貨品主檔]");
        d.庫存總額 = ScalarDec(
            "SELECT COALESCE(SUM(COALESCE(k.[現有數量],0) * COALESCE(p.[現行平均成本],0)),0) " +
            "FROM [貨品庫存] k LEFT JOIN [貨品主檔] p ON k.[貨品編號] = p.[貨品編號]");
        string month = DateTime.Now.ToString("yyyy-MM");
        d.本月出貨筆數 = ScalarInt(
            "SELECT COUNT(*) FROM [交易主檔] WHERE [單據類別] = '出貨' AND [交易日期] LIKE $p",
            DbManager.Param("$p", month + "%"));
        d.本月折讓金額 = ScalarDec(
            "SELECT COALESCE(SUM([總計金額]),0) FROM [折讓主檔] WHERE [折讓日期] LIKE $p",
            DbManager.Param("$p", month + "%"));
        string todayYm = DateTime.Now.ToString("yyyy-MM-dd");
        d.今日折讓筆數 = ScalarInt(
            "SELECT COUNT(*) FROM [折讓主檔] WHERE [折讓日期] LIKE $p",
            DbManager.Param("$p", todayYm + "%"));
        d.本月折讓筆數 = ScalarInt(
            "SELECT COUNT(*) FROM [折讓主檔] WHERE [折讓日期] LIKE $p",
            DbManager.Param("$p", month + "%"));
    }

    private static decimal GetDec(object v) =>
        v is null or DBNull ? 0m : Convert.ToDecimal(v);

    private static decimal SumColumn(DataTable dt, string column)
    {
        decimal sum = 0m;
        foreach (DataRow r in dt.Rows)
            sum += r.IsNull(column) ? 0m : Convert.ToDecimal(r[column]);
        return sum;
    }

    private static int CountOpenBills(string 客廠類別) => ScalarInt(
        "SELECT COUNT(*) FROM [帳款簡要] A " +
        "JOIN [客戶廠商] C ON A.[交易對象] = C.[客廠編號] AND C.[客廠類別] = $t " +
        "WHERE A.[未收付金額] > 0",
        DbManager.Param("$t", 客廠類別));

    private static decimal ScalarDec(string sql, params SqliteParameter[] pars)
    {
        var v = DbManager.QueryScalar(sql, pars);
        return v is null || v is DBNull ? 0m : Convert.ToDecimal(v);
    }

    private static int ScalarInt(string sql, params SqliteParameter[] pars)
    {
        var v = DbManager.QueryScalar(sql, pars);
        return v is null || v is DBNull ? 0 : Convert.ToInt32(v);
    }
}
