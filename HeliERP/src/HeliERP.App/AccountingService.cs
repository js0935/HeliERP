// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>
/// 會計報表核心：由會計快照表（日記帳簿／總分類帳／現金帳簿／期初餘額／損益報表／資產負債）
/// join 會計科目建構報表列印資料。全部報表採 ppDBPipeline1 明細管線；
/// 帳戶式資產負債表以「ppDBPipeline2|欄位」前綴鍵提供右側（負債＋權益）欄位，
/// 會計傳票採主檔（ppDBPipeline1）＋明細（ppDBPipeline2）主從結構。
/// </summary>
public static class AccountingService
{
    /// <summary>列表式報表共用：新 RtmData（ppDBPipeline1）＋公司資料＋日期區間。</summary>
    private static RtmData NewData()
    {
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        ARService.FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        return data;
    }

    private static RtmData? Finish(DataTable dt)
    {
        if (dt.Rows.Count == 0) return null;
        var data = NewData();
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>總分類帳明細表：日記帳簿逐筆（join 科目名稱），依科目分組印小計。</summary>
    public static RtmData? BuildLedgerDetailReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT d.[傳票日期], d.[摘要], d.[借方金額], d.[貸方金額], d.[傳票編號], d.[餘額], " +
            "d.[科目編號], COALESCE(s.[科目名稱],'') AS [科目名稱] " +
            "FROM [日記帳簿] d LEFT JOIN [會計科目] s ON d.[科目編號] = s.[科目編號] " +
            "ORDER BY d.[科目編號], d.[傳票日期], d.[傳票編號]");
        return Finish(dt);
    }

    /// <summary>總分類帳簡要表：總分類帳逐科目（join 科目名稱）。</summary>
    public static RtmData? BuildLedgerBriefReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT g.[科目編號], g.[期初餘額], g.[借方金額], COALESCE(s.[科目名稱],'') AS [科目名稱], " +
            "g.[餘額], g.[貸方金額] " +
            "FROM [總分類帳] g LEFT JOIN [會計科目] s ON g.[科目編號] = s.[科目編號] " +
            "ORDER BY g.[科目編號]");
        return Finish(dt);
    }

    /// <summary>明細分類帳：明細分類帳簿逐筆（同總分類帳明細表資料，依科目分組）。</summary>
    public static RtmData? BuildDetailLedgerReportData() => BuildLedgerDetailReportData();

    /// <summary>日記帳（含現）：日記帳簿全部逐筆（join 科目名稱）。</summary>
    public static RtmData? BuildJournalReportData() => BuildJournal(excludeCash: false);

    /// <summary>日記帳（不含現）：排除現金科目（1101000）。</summary>
    public static RtmData? BuildJournalNoCashReportData() => BuildJournal(excludeCash: true);

    private static RtmData? BuildJournal(bool excludeCash)
    {
        var dt = DbManager.QueryTable(
            "SELECT d.[傳票日期], COALESCE(s.[科目名稱],'') AS [科目名稱], d.[摘要], " +
            "d.[借方金額], d.[貸方金額], d.[傳票編號], d.[科目編號] " +
            "FROM [日記帳簿] d LEFT JOIN [會計科目] s ON d.[科目編號] = s.[科目編號] " +
            (excludeCash ? "WHERE d.[科目編號] <> '1101000' " : "") +
            "ORDER BY d.[傳票日期], d.[傳票編號]");
        return Finish(dt);
    }

    /// <summary>現金帳：現金帳簿逐筆（限現金科目，join 科目名稱）。</summary>
    public static RtmData? BuildCashBookReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT c.[傳票日期], c.[科目編號], c.[摘要], c.[收入], c.[支出], c.[傳票編號], " +
            "COALESCE(s.[科目名稱],'') AS [科目名稱], c.[餘額] " +
            "FROM [現金帳簿] c LEFT JOIN [會計科目] s ON c.[科目編號] = s.[科目編號] " +
            "WHERE c.[科目編號] = '1101000' ORDER BY c.[傳票日期]");
        return Finish(dt);
    }

    /// <summary>試算表：期初餘額逐科目（借方餘額／貸方餘額）。</summary>
    public static RtmData? BuildTrialBalanceReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT p.[科目編號], p.[借方金額] AS [借方餘額], COALESCE(s.[科目名稱],'') AS [科目名稱], " +
            "p.[貸方金額] AS [貸方餘額] " +
            "FROM [期初餘額] p LEFT JOIN [會計科目] s ON p.[科目編號] = s.[科目編號] " +
            "ORDER BY p.[科目編號]");
        return Finish(dt);
    }

    /// <summary>期間試算表：總分類帳逐科目（join 借方／貸方筆數與科目名稱）。</summary>
    public static RtmData? BuildPeriodTrialBalanceReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT g.[科目編號], g.[借方金額], COALESCE(s.[科目名稱],'') AS [科目名稱], g.[餘額], g.[貸方金額], " +
            "COALESCE(db.[借方筆數],0) AS [借方筆數], COALESCE(cr.[貸方筆數],0) AS [貸方筆數] " +
            "FROM [總分類帳] g " +
            "LEFT JOIN [會計科目] s ON g.[科目編號] = s.[科目編號] " +
            "LEFT JOIN [借方筆數] db ON g.[科目編號] = db.[科目編號] " +
            "LEFT JOIN [貸方筆數] cr ON g.[科目編號] = cr.[科目編號] " +
            "ORDER BY g.[科目編號]");
        return Finish(dt);
    }

    /// <summary>損益表：損益報表快照（依大類分組，小計金額由渲染器彙總）。</summary>
    public static RtmData? BuildIncomeStatementReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT [損益類別], [大類名稱], [科目編號], [科目名稱], [本期金額] " +
            "FROM [損益報表] ORDER BY [建檔序號]");
        return Finish(dt);
    }

    /// <summary>報告式資產負債表：資產負債快照逐科目（大類／類別／科目）。</summary>
    public static RtmData? BuildBalanceSheetReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT [大類名稱], [類別名稱], [科目編號], [科目名稱], [金額小計] " +
            "FROM [資產負債] ORDER BY [建檔序號]");
        return Finish(dt);
    }

    /// <summary>
    /// 帳戶式資產負債表：左側（ppDBPipeline1）資產、右側（ppDBPipeline2）負債＋業主權益。
    /// 右側欄位以「ppDBPipeline2|欄位」前綴鍵提供，左右兩側依列對齊。
    /// </summary>
    public static RtmData? BuildAccountBalanceSheetReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT [大類名稱], [類別名稱], [科目編號], [科目名稱], [金額小計] " +
            "FROM [資產負債] ORDER BY [建檔序號]");
        if (dt.Rows.Count == 0) return null;

        var 資產 = new List<DataRow>();
        var 負債權益 = new List<DataRow>();
        foreach (DataRow r in dt.Rows)
        {
            if (Convert.ToString(r["大類名稱"]) == "資產") 資產.Add(r);
            else 負債權益.Add(r);
        }

        var data = NewData();
        int n = Math.Max(資產.Count, 負債權益.Count);
        for (int i = 0; i < n; i++)
        {
            var d = new Dictionary<string, object?>();
            if (i < 資產.Count) CopyRow(d, 資產[i], prefix: null);
            if (i < 負債權益.Count) CopyRow(d, 負債權益[i], prefix: "ppDBPipeline2");
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>
    /// 會計傳票：挑選一筆傳票，主檔欄位（ppDBPipeline1）放 Master、
    /// 明細（ppDBPipeline2）放 Detail（join 科目名稱）。
    /// </summary>
    public static RtmData? BuildVoucherReportData()
    {
        var list = DbManager.QueryTable(
            "SELECT [單據副碼] FROM [傳票主檔] WHERE [傳票類別] IS NOT NULL " +
            "AND [單據副碼] IN (SELECT [單據副碼] FROM [傳票明細]) " +
            "ORDER BY [傳票日期] LIMIT 1");
        if (list.Rows.Count == 0) return null;
        var 副碼 = Convert.ToInt64(list.Rows[0]["單據副碼"]);

        var m = DbManager.QueryTable(
            "SELECT [傳票日期], [傳票類別], [傳票編號], [借方合計], [貸方合計] " +
            "FROM [傳票主檔] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
        if (m.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline2" };
        ARService.FillCompany(data);
        foreach (DataColumn col in m.Columns) data.Master[col.ColumnName] = m.Rows[0][col];

        var dt = DbManager.QueryTable(
            "SELECT d.[借貸], d.[科目編號], d.[摘要], d.[貸方金額], " +
            "COALESCE(s.[科目名稱],'') AS [科目名稱], d.[借方金額] " +
            "FROM [傳票明細] d LEFT JOIN [會計科目] s ON d.[科目編號] = s.[科目編號] " +
            "WHERE d.[單據副碼] = $c ORDER BY d.[建檔序號]", DbManager.Param("$c", 副碼));
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>把資料列欄位複製進字典；prefix 非 null 時加「{prefix}|」前綴。</summary>
    private static void CopyRow(Dictionary<string, object?> d, DataRow r, string? prefix)
    {
        foreach (DataColumn col in r.Table.Columns)
            d[prefix is null ? col.ColumnName : $"{prefix}|{col.ColumnName}"] = r[col];
    }
}
