// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 收款／付款沖銷日報表報表資料。
/// 資料來源：收付主檔（收付類別 = 收款 / 付款），沖帳對象 JOIN 客戶廠商取公司全名。
/// </summary>
public static class WriteOffService
{
    public const string 收款類別 = "收款";
    public const string 付款類別 = "付款";

    /// <summary>收款沖銷日報表（收付類別 = 收款）／付款沖銷日報表（收付類別 = 付款）。</summary>
    public static RtmData BuildWriteOffReportData(string 收付類別)
    {
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        ARService.FillCompany(data);
        data.Master["日期區間"] = "全部日期";

        var dt = DbManager.QueryTable(
            "SELECT P.[沖帳日期], COALESCE(C.[公司全名],'') AS [公司全名], P.[現金金額], P.[票據金額], " +
            "P.[取用預收], P.[累入預收], P.[沖帳合計] " +
            "FROM [收付主檔] P LEFT JOIN [客戶廠商] C ON P.[沖帳對象] = C.[客廠編號] " +
            "WHERE P.[收付類別] = $k ORDER BY P.[沖帳日期], P.[收付單號]",
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
