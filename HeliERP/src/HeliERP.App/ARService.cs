// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;
using HeliERP.Models;
using Microsoft.Data.Sqlite;

namespace HeliERP.App;

/// <summary>
/// 應收帳款管理核心（含廠商應付）：對象餘額總覽、未收付明細、帳齡分析。
/// 資料來源：帳款主檔（對象彙總，含前期累計應收帳款）＋ 帳款簡要（單據層級未收付）。
/// 帳齡分桶（基準日依單據交易日期計算天數）：
///   未收付 &gt; 0 之單據依天數落入第一~第六期間桶（未滿第一期間→第一期間，依此類推；
///   超過第六期間→歸期初）；未收付 &lt; 0（出退/進退抵銷、貸項）彙總為「貸項」；
///   帳款主檔之「前期累計應收帳款」一律歸期初桶。
/// </summary>
public static class ARService
{
    public const string 應收類別 = "客戶";
    public const string 應付類別 = "廠商";

    /// <summary>畫面類別下拉（索引 0 = 應收帳款／客戶）</summary>
    public static readonly string[] Kinds = { "應收帳款", "應付帳款" };

    /// <summary>由畫面顯示名取得資料庫客廠類別值</summary>
    public static string 客廠類別For(string 顯示名) => 顯示名 == Kinds[1] ? 應付類別 : 應收類別;

    /// <summary>帳齡期間設定（天數；第一期間最小、依序遞增）</summary>
    public sealed class AgePeriods
    {
        public string 類別 = "一般";
        public int 第一期間 = 30;
        public int 第二期間 = 60;
        public int 第三期間 = 90;
        public int 第四期間 = 120;
        public int 第五期間 = 150;
        public int 第六期間 = 180;
    }

    public static readonly string[] 期間欄位 = { "第一期間", "第二期間", "第三期間", "第四期間", "第五期間", "第六期間" };

    // ==================== 期間設定 ====================

    /// <summary>讀取帳齡期間設定（類別 = 一般）；無資料回傳預設 30/60/90/120/150/180。</summary>
    public static AgePeriods LoadAgePeriods()
    {
        var p = new AgePeriods();
        var dt = DbManager.QueryTable("SELECT * FROM [帳龄期間] WHERE [類別] = $t",
            DbManager.Param("$t", p.類別));
        if (dt.Rows.Count == 0) return p;
        var r = dt.Rows[0];
        p.第一期間 = GetInt(r, "第一期間", p.第一期間);
        p.第二期間 = GetInt(r, "第二期間", p.第二期間);
        p.第三期間 = GetInt(r, "第三期間", p.第三期間);
        p.第四期間 = GetInt(r, "第四期間", p.第四期間);
        p.第五期間 = GetInt(r, "第五期間", p.第五期間);
        p.第六期間 = GetInt(r, "第六期間", p.第六期間);
        return p;
    }

    /// <summary>寫回帳齡期間設定（類別 = 一般，UPSERT）。天數必須為正且嚴格遞增，否則拋例外。</summary>
    public static void SaveAgePeriods(AgePeriods p)
    {
        int[] 值 = { p.第一期間, p.第二期間, p.第三期間, p.第四期間, p.第五期間, p.第六期間 };
        for (int i = 0; i < 值.Length; i++)
        {
            if (值[i] <= 0)
                throw new InvalidOperationException($"第 {期間欄位[i]} 天數必須大於 0。");
            if (i > 0 && 值[i] <= 值[i - 1])
                throw new InvalidOperationException("期間天數必須由小到大嚴格遞增。");
        }
        var exists = DbManager.QueryScalar("SELECT [類別] FROM [帳龄期間] WHERE [類別] = $t",
            DbManager.Param("$t", p.類別));
        if (exists is null)
        {
            DbManager.ExecuteNonQuery(
                "INSERT INTO [帳龄期間] ([類別],[第一期間],[第二期間],[第三期間],[第四期間],[第五期間],[第六期間]) " +
                "VALUES ($t,$1,$2,$3,$4,$5,$6)",
                DbManager.Param("$t", p.類別),
                DbManager.Param("$1", p.第一期間), DbManager.Param("$2", p.第二期間),
                DbManager.Param("$3", p.第三期間), DbManager.Param("$4", p.第四期間),
                DbManager.Param("$5", p.第五期間), DbManager.Param("$6", p.第六期間));
        }
        else
        {
            DbManager.ExecuteNonQuery(
                "UPDATE [帳龄期間] SET [第一期間]=$1,[第二期間]=$2,[第三期間]=$3,[第四期間]=$4," +
                "[第五期間]=$5,[第六期間]=$6 WHERE [類別]=$t",
                DbManager.Param("$1", p.第一期間), DbManager.Param("$2", p.第二期間),
                DbManager.Param("$3", p.第三期間), DbManager.Param("$4", p.第四期間),
                DbManager.Param("$5", p.第五期間), DbManager.Param("$6", p.第六期間),
                DbManager.Param("$t", p.類別));
        }
    }

    // ==================== 帶入查詢（畫面使用） ====================

    /// <summary>交易對象下拉（客廠類別 = 客戶／廠商）</summary>
    public static DataTable LoadObjectCombo(string 客廠類別) =>
        DbManager.QueryTable(
            "SELECT [客廠編號], [公司簡稱] FROM [客戶廠商] WHERE [客廠類別] = $t ORDER BY [客廠編號]",
            DbManager.Param("$t", 客廠類別));

    // ==================== 對象餘額總覽 ====================

    /// <summary>
    /// 對象餘額總覽（帳款主檔彙總；未收付合計 = 前期累計應收帳款 + 本期總計 − 已收付金額 − 折讓金額）。
    /// 回傳欄位：交易對象、公司簡稱、前期累計應收帳款、本期總計、折讓金額、已收付金額、累計預收貨款、未收付合計。
    /// </summary>
    public static DataTable LoadObjectSummary(string 客廠類別)
    {
        var dt = DbManager.QueryTable(
            "SELECT A.[交易對象], C.[公司簡稱], A.[前期累計應收帳款], A.[本期總計], A.[折讓金額], " +
            "A.[已收付金額], A.[累計預收貨款] " +
            "FROM [帳款主檔] A JOIN [客戶廠商] C ON A.[交易對象] = C.[客廠編號] AND C.[客廠類別] = $t " +
            "ORDER BY A.[交易對象]",
            DbManager.Param("$t", 客廠類別));
        dt.Columns.Add("未收付合計", typeof(decimal));
        foreach (DataRow r in dt.Rows)
        {
            decimal 前期 = Nz(r["前期累計應收帳款"]);
            decimal 本期 = Nz(r["本期總計"]);
            decimal 已收 = Nz(r["已收付金額"]);
            decimal 折讓 = Nz(r["折讓金額"]);
            r["未收付合計"] = 前期 + 本期 - 已收 - 折讓;
        }
        return dt;
    }

    // ==================== 未收付明細 ====================

    /// <summary>對象之未收付明細（未收付金額 ≠ 0 之單據，含貸項負數）。</summary>
    public static DataTable LoadOpenDetails(string 交易對象)
    {
        return DbManager.QueryTable(
            "SELECT [交易日期], [單據類別], [交易單號], [發票號碼], [總計金額], [折讓金額], [已收付金額], [未收付金額] " +
            "FROM [帳款簡要] WHERE [交易對象] = $o AND [未收付金額] <> 0 " +
            "ORDER BY [交易日期], [交易單號]",
            DbManager.Param("$o", 交易對象));
    }

    // ==================== 帳齡分析 ====================

    /// <summary>
    /// 帳齡分析：交易對象為 null 時彙總全部對象。
    /// 回傳欄位：交易對象、期初帳款、第一期間…第六期間、貸項、合計。
    /// 分桶規則：天數 = 基準日 − 交易日期；天數 &lt; 第一期間 → 第一期間桶，依此類推；
    /// 天數 ≥ 第六期間 → 期初桶；未收付 &lt; 0 之單據 → 貸項（保留負號）；前期累計應收帳款 → 期初桶。
    /// </summary>
    public static DataTable AgingAnalysis(string? 交易對象, AgePeriods 期間, DateTime 基準日)
    {
        int[] 天數界 = { 期間.第一期間, 期間.第二期間, 期間.第三期間, 期間.第四期間, 期間.第五期間, 期間.第六期間 };

        var 表 = new DataTable();
        表.Columns.Add("交易對象", typeof(string));
        表.Columns.Add("期初帳款", typeof(decimal));
        foreach (var f in 期間欄位)
            表.Columns.Add(f, typeof(decimal));
        表.Columns.Add("貸項", typeof(decimal));
        表.Columns.Add("合計", typeof(decimal));

        var 簡要 = DbManager.QueryTable(
            "SELECT [交易對象], [交易日期], [未收付金額] FROM [帳款簡要] WHERE [未收付金額] <> 0" +
            (交易對象 is null ? "" : " AND [交易對象] = $o") +
            " ORDER BY [交易日期]",
            交易對象 is null
                ? Array.Empty<SqliteParameter>()
                : new[] { DbManager.Param("$o", 交易對象) });
        var 前期 = DbManager.QueryTable(
            "SELECT [交易對象], [前期累計應收帳款] FROM [帳款主檔] WHERE [前期累計應收帳款] <> 0" +
            (交易對象 is null ? "" : " AND [交易對象] = $o"),
            交易對象 is null
                ? Array.Empty<SqliteParameter>()
                : new[] { DbManager.Param("$o", 交易對象) });

        var 對象們 = new SortedSet<string>();
        foreach (DataRow r in 簡要.Rows)
            對象們.Add(Str(r["交易對象"]));
        foreach (DataRow r in 前期.Rows)
            對象們.Add(Str(r["交易對象"]));

        var 基準日0 = 基準日.Date;
        foreach (var o in 對象們)
        {
            decimal[] 值 = new decimal[8];   // [0]期初 [1..6]期間 [7]貸項
            foreach (DataRow r in 前期.Rows)
            {
                if (Str(r["交易對象"]) == o)
                    值[0] += Nz(r["前期累計應收帳款"]);
            }
            foreach (DataRow r in 簡要.Rows)
            {
                if (Str(r["交易對象"]) != o) continue;
                decimal 未收 = Nz(r["未收付金額"]);
                if (未收 < 0m)
                {
                    值[7] += 未收;
                    continue;
                }
                var 日期 = DateTime.TryParse(Str(r["交易日期"]), out var d) ? d.Date : 基準日0;
                int 差 = (基準日0 - 日期).Days;
                int 桶 = 0;   // 0 = 期初
                for (int i = 0; i < 6; i++)
                {
                    if (差 < 天數界[i])
                    {
                        桶 = i + 1;
                        break;
                    }
                }
                if (桶 == 0) 值[0] += 未收;
                else 值[桶] += 未收;
            }

            var row = 表.NewRow();
            row["交易對象"] = o;
            row["期初帳款"] = 值[0];
            for (int i = 0; i < 6; i++)
                row[期間欄位[i]] = 值[i + 1];
            row["貸項"] = 值[7];
            row["合計"] = 值[0] + 值[1] + 值[2] + 值[3] + 值[4] + 值[5] + 值[6] + 值[7];
            表.Rows.Add(row);
        }
        return 表;
    }

    // ==================== 報表列印資料 ====================

    /// <summary>填公司基本資料（plCompany）進報表資料。</summary>
    public static void FillCompany(RtmData data)
    {
        var company = new CompanyInfo();
        data.Company["公司全名"] = company.CompanyName;
        data.Company["電話號碼"] = company.Phone;
        data.Company["登記地址"] = company.Address;
        data.Company["傳真號碼"] = LookupCompanyFax(company.CompanyName);
    }

    /// <summary>
    /// 應收帳款統計表報表資料（每列一個對象，限客廠類別）。
    /// 本期應收＝本期總計；本期累計應收＝前期累計應收帳款＋本期總計−已收付金額−折讓金額。
    /// </summary>
    public static RtmData BuildSummaryReportData(string 客廠類別)
    {
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        data.Master["編號區間"] = 客廠類別 == 應收類別 ? "全部客戶" : "全部廠商";

        var dt = DbManager.QueryTable(
            "SELECT A.[交易對象], COALESCE(A.[公司全名], C.[公司全名]) AS [公司全名], " +
            "A.[累計預收貨款], A.[前期累計應收帳款], A.[本期總計], A.[已收付金額], A.[折讓金額], A.[現金收付金額] " +
            "FROM [帳款主檔] A JOIN [客戶廠商] C ON A.[交易對象] = C.[客廠編號] AND C.[客廠類別] = $t " +
            "ORDER BY A.[交易對象]",
            DbManager.Param("$t", 客廠類別));

        foreach (DataRow r in dt.Rows)
        {
            decimal 前期 = Nz(r["前期累計應收帳款"]);
            decimal 本期 = Nz(r["本期總計"]);
            decimal 已收 = Nz(r["已收付金額"]);
            decimal 折讓 = Nz(r["折讓金額"]);
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            d["本期應收"] = 本期;
            d["本期累計應收"] = 前期 + 本期 - 已收 - 折讓;
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>
    /// 應收帳款帳齡分析報表資料（每列一個對象，限客廠類別，補公司全名）。
    /// </summary>
    public static RtmData BuildAgingReportData(string 客廠類別)
    {
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        var 基準日 = DateTime.Today;
        data.Master["日期區間"] = "基準日 " + 基準日.ToString("yyyy-MM-dd");
        data.Master["編號區間"] = 客廠類別 == 應收類別 ? "全部客戶" : "全部廠商";

        var 明細 = AgingAnalysis(null, LoadAgePeriods(), 基準日);

        var 名稱 = new Dictionary<string, string>();
        var cust = DbManager.QueryTable(
            "SELECT [客廠編號], [公司全名] FROM [客戶廠商] WHERE [客廠類別] = $t",
            DbManager.Param("$t", 客廠類別));
        foreach (DataRow r in cust.Rows)
            名稱[Str(r["客廠編號"])] = Str(r["公司全名"]);

        foreach (DataRow r in 明細.Rows)
        {
            var o = Str(r["交易對象"]);
            if (!名稱.ContainsKey(o)) continue;
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in 明細.Columns) d[col.ColumnName] = r[col];
            d["公司全名"] = 名稱[o];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>
    /// 應收帳款明細表報表資料（主檔＝帳款主檔，明細＝帳款明細）。找不到主檔回傳 null。
    /// </summary>
    public static RtmData? BuildDetailReportData(string 交易對象)
    {
        var master = LoadArMaster(交易對象);
        if (master is null) return null;
        var data = new RtmData { DetailPipeline = "ppDBPipeline2" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        foreach (var (k, v) in master) data.Master[k] = v;
        data.Master["本期累計應收"] = 累計應收(master);

        var dt = DbManager.QueryTable(
            "SELECT [交易日期],[單據類別],[交易單號],[發票號碼],[貨品編號],[品名],[數量],[單位],[單價],[折扣],[金額] " +
            "FROM [帳款明細] WHERE [交易對象] = $o ORDER BY [交易日期],[交易單號]",
            DbManager.Param("$o", 交易對象));
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>
    /// 應收帳款簡要表報表資料（主檔＝帳款主檔，明細＝帳款簡要未收付單據）。找不到主檔回傳 null。
    /// 明細管線依客廠類別：應付簡要表明細位於主檔管線（ppDBPipeline1），應收位於明細管線（ppDBPipeline2）。
    /// </summary>
    public static RtmData? BuildBriefReportData(string 交易對象, string 客廠類別)
    {
        var master = LoadArMaster(交易對象);
        if (master is null) return null;
        var data = new RtmData { DetailPipeline = 簡要表明細管線(客廠類別) };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        foreach (var (k, v) in master) data.Master[k] = v;
        data.Master["本期累計應收"] = 累計應收(master);

        var dt = DbManager.QueryTable(
            "SELECT [交易日期],[單據類別],[交易單號],[發票號碼],[合計金額],[營業稅],[總計金額],[已收付金額],[未收付金額] " +
            "FROM [帳款簡要] WHERE [交易對象] = $o AND [未收付金額] <> 0 ORDER BY [交易日期],[交易單號]",
            DbManager.Param("$o", 交易對象));
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>簡要表明細所在管線（應付簡要表明細在主檔管線，應收在明細管線）。</summary>
    public static string 簡要表明細管線(string 客廠類別) =>
        客廠類別 == 應付類別 ? "ppDBPipeline1" : "ppDBPipeline2";

    /// <summary>業務應收統計表報表資料（每列一個業務員，彙總帳款主檔）。</summary>
    public static RtmData BuildBizSummaryReportData()
    {
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        data.Master["編號區間"] = "全部業務員";
        var dt = DbManager.QueryTable(
            "SELECT E.[員工姓名], E.[員工姓名] AS [公司全名], " +
            "SUM(COALESCE(A.[前期累計應收帳款],0)) AS [前期累計應收帳款], " +
            "SUM(COALESCE(A.[本期合計],0)) AS [本期合計], " +
            "SUM(COALESCE(A.[本期總計],0)) AS [本期總計], " +
            "SUM(COALESCE(A.[已收付金額],0)) AS [已收付金額], " +
            "SUM(COALESCE(A.[前期累計應收帳款],0)+COALESCE(A.[本期總計],0)-COALESCE(A.[已收付金額],0)-COALESCE(A.[折讓金額],0)) AS [本期累計應收] " +
            "FROM [帳款主檔] A LEFT JOIN [員工資料] E ON E.[員工編號]=A.[員工編號] " +
            "WHERE E.[員工姓名] IS NOT NULL AND E.[員工姓名] <> '' " +
            "GROUP BY E.[員工姓名] ORDER BY E.[員工姓名]");
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>業務應收明細表報表資料（帳款簡要 join 員工與客戶，依員工分組）。</summary>
    public static RtmData BuildBizDetailReportData()
    {
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        data.Master["編號區間"] = "全部日期";
        var dt = DbManager.QueryTable(
            "SELECT E.[員工姓名], B.[交易日期], B.[交易單號], COALESCE(C.[公司全名],'') AS [公司全名], B.[單據類別], " +
            "COALESCE(B.[合計金額],0) AS [合計金額], COALESCE(B.[營業稅],0) AS [營業稅], " +
            "COALESCE(B.[總計金額],0) AS [總計金額], COALESCE(B.[已收付金額],0) AS [已收付金額], " +
            "COALESCE(B.[未收付金額],0) AS [未收付金額] " +
            "FROM [帳款簡要] B LEFT JOIN [員工資料] E ON E.[員工編號]=B.[員工編號] " +
            "LEFT JOIN [客戶廠商] C ON C.[客廠編號]=B.[交易對象] " +
            "ORDER BY E.[員工姓名], B.[交易日期], B.[交易單號]");
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>讀取帳款主檔單一對象（回傳欄位字典；找不到回傳 null）。</summary>
    private static Dictionary<string, object?>? LoadArMaster(string 交易對象)
    {
        var dt = DbManager.QueryTable(
            "SELECT * FROM [帳款主檔] WHERE [交易對象] = $o LIMIT 1",
            DbManager.Param("$o", 交易對象));
        if (dt.Rows.Count == 0) return null;
        var d = new Dictionary<string, object?>();
        foreach (DataColumn col in dt.Columns) d[col.ColumnName] = dt.Rows[0][col];
        return d;
    }

    /// <summary>本期累計應收＝前期累計應收帳款＋本期總計−已收付金額−折讓金額。</summary>
    private static decimal 累計應收(Dictionary<string, object?> master) =>
        Nz(master.TryGetValue("前期累計應收帳款", out var p) ? p : null)
        + Nz(master.TryGetValue("本期總計", out var m) ? m : null)
        - Nz(master.TryGetValue("已收付金額", out var y) ? y : null)
        - Nz(master.TryGetValue("折讓金額", out var z) ? z : null);

    private static string LookupCompanyFax(string companyName)
    {
        var v = DbManager.QueryScalar(
            "SELECT \"傳真號碼\" FROM \"客戶廠商\" WHERE \"公司全名\" = $name" +
            " AND \"傳真號碼\" IS NOT NULL AND \"傳真號碼\" != '' LIMIT 1",
            DbManager.Param("$name", companyName));
        return v?.ToString() ?? "";
    }

    // ==================== 工具 ====================

    private static int GetInt(DataRow r, string 欄, int 預設) =>
        r.IsNull(欄) ? 預設 : Convert.ToInt32(r[欄]);

    private static decimal Nz(object? v) =>
        v is null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);

    private static string Str(object? v) =>
        v is null || v == DBNull.Value ? "" : v.ToString()!;
}
