// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 票據系統報表資料：應收票據（收票）／應付票據（付票）之明細表與未兌現表。
/// 資料來源：票據收付（收付類別 = 收票 / 付票），來往對象 JOIN 客戶廠商取公司簡稱。
/// </summary>
public static class BillService
{
    public const string 收票類別 = "收票";
    public const string 付票類別 = "付票";

    /// <summary>
    /// 票據明細表報表資料（應收票據明細表／應付票據明細表）。
    /// 排序鍵：收票日／開票日 → 依收開票日；託收銀行／開票銀行 → 依票面銀行。
    /// </summary>
    public static RtmData BuildBillDetailReportData(string 收付類別, string 排序)
    {
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        ARService.FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        data.Master["編號區間"] = 收付類別 == 付票類別 ? "全部廠商" : "全部客戶";

        string orderBy = 排序 switch
        {
            "收票日" or "開票日" => "B.[收開票日], B.[支票號碼]",
            _ => "COALESCE(B.[票面銀行],''), B.[收開票日], B.[支票號碼]",
        };
        var dt = DbManager.QueryTable(
            "SELECT B.[收開票日], B.[到期日], B.[票面金額], B.[支票號碼], B.[預兌日], " +
            "COALESCE(B.[票面銀行],'') AS [銀行名稱], COALESCE(B.[銀行帳戶], B.[票面銀行]) AS [銀行帳戶], B.[票據現況], " +
            "COALESCE(C.[公司簡稱],'') AS [公司簡稱] " +
            "FROM [票據收付] B LEFT JOIN [客戶廠商] C ON B.[來往對象] = C.[客廠編號] " +
            "WHERE B.[收付類別] = $k ORDER BY " + orderBy,
            DbManager.Param("$k", 收付類別));

        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>
    /// 未兌現票據報表資料（未兌現應收票據／未兌現應付票據）。
    /// 條件：票據現況 = 尚未。應收顯示到期日、應付顯示預兌日（報表欄位名不同）。
    /// </summary>
    public static RtmData BuildUnclearedBillData(string 收付類別)
    {
        bool 應收 = 收付類別 == 收票類別;
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        ARService.FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        data.Master["編號區間"] = 應收 ? "全部客戶" : "全部廠商";

        string 日期欄 = 應收 ? "到期日" : "預兌日";
        var dt = DbManager.QueryTable(
            $"SELECT B.[票面金額], B.[支票號碼], B.[{日期欄}] AS [{日期欄}], " +
            "COALESCE(B.[票面銀行],'') AS [銀行名稱], B.[票據現況], " +
            "COALESCE(C.[公司簡稱],'') AS [公司簡稱] " +
            "FROM [票據收付] B LEFT JOIN [客戶廠商] C ON B.[來往對象] = C.[客廠編號] " +
            $"WHERE B.[收付類別] = $k AND B.[票據現況] = '尚未' ORDER BY B.[{日期欄}], B.[支票號碼]",
            DbManager.Param("$k", 收付類別));

        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }
}
