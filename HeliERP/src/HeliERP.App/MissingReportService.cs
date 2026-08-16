using System.Data;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App
{
    /// <summary>
    /// 未應用報表補登：借出/借入/託售/託工/調撥/領料/折讓/專案/銀行等 41 份報表的資料建構。
    /// 單據類報表：交易主檔（ppDBPipeline1）＋交易明細（ppDBPipeline2），主檔欄位使用前綴鍵。
    /// 明細表類報表：交易明細（ppDBPipeline1）每列一筆。
    /// </summary>
    public static class MissingReportService
    {
        // 既有交易類別
        private const string 出貨類 = "出貨", 出退類 = "出退", 進貨類 = "進貨", 進退類 = "進退";

        // 新增交易類別（後續由 TradeService.Kinds 擴充對應）
        public const string 借出 = "借出", 借出還入 = "借出還入", 借入 = "借入", 借入還出 = "借入還出",
            託售 = "託售", 託售回貨 = "託售回貨", 託工出庫 = "託工出庫", 託工入庫 = "託工入庫",
            調撥 = "調撥", 領料 = "領料";

        // 折讓類別
        public const string 出貨折讓 = "出貨折讓", 進貨折讓 = "進貨折讓";

        private static readonly HashSet<string> 交易明細欄位 =
            new() { "貨品編號", "品名", "單位", "單價", "金額", "數量", "附註說明", "倉庫編號", "調入倉庫" };

        private static readonly HashSet<string> 折讓明細欄位 =
            new() { "貨單編號", "發票編號", "發票日期", "單據金額", "單據稅金", "單據折讓", "折扣稅額", "附註" };

        private static void FillCompany(RtmData data)
        {
            var company = new CompanyInfo();
            data.Company["公司全名"] = company.CompanyName;
            data.Company["電話號碼"] = company.Phone;
            data.Company["登記地址"] = company.Address;
            data.Company["傳真號碼"] = LookupCompanyFax(company.CompanyName);
        }

        private static string LookupCompanyFax(string companyName)
        {
            var v = DbManager.QueryScalar(
                "SELECT \"傳真號碼\" FROM \"客戶廠商\" WHERE \"公司全名\" = $name" +
                " AND \"傳真號碼\" IS NOT NULL AND \"傳真號碼\" != '' LIMIT 1",
                DbManager.Param("$name", companyName));
            return v?.ToString() ?? "";
        }

        private static string InList(string[] 類別) =>
            string.Join(",", 類別.Select(k => $"'{k.Replace("'", "''")}'"));

        // ==================== 明細表資料（每列一筆） ====================

        private static DataTable LoadTxDetail(string[] 類別, bool 限專案 = false)
        {
            var 專案 = 限專案 ? " AND COALESCE(NULLIF(m.[專案編號],''),'') <> '' " : " ";
            return DbManager.QueryTable(
                "SELECT m.[交易日期], m.[交易單號], m.[單據類別], " +
                "COALESCE(c.[公司簡稱],'') AS [公司簡稱], COALESCE(j.[專案名稱],'') AS [專案名稱], " +
                "d.[貨品編號], COALESCE(p.[品名],'') AS [品名], d.[數量], COALESCE(d.[單位],'') AS [單位], " +
                "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額], " +
                "COALESCE(d.[倉庫編號],'') AS [倉庫編號], COALESCE(d.[調入倉庫],'') AS [調入倉庫] " +
                "FROM [交易主檔] m " +
                "JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
                "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
                "LEFT JOIN [專案設定] j ON m.[專案編號] = j.[專案編號] " +
                "LEFT JOIN (SELECT [貨品編號], MAX([品名]) AS [品名] FROM [貨品主檔] GROUP BY [貨品編號]) p ON d.[貨品編號] = p.[貨品編號] " +
                $"WHERE m.[單據類別] IN ({InList(類別)}){專案} " +
                "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
        }

        private static DataTable LoadTxMaster(string[] 類別)
        {
            return DbManager.QueryTable(
                "SELECT m.[交易日期], m.[交易單號], m.[單據類別], COALESCE(m.[發票號碼],'') AS [發票號碼], " +
                "COALESCE(m.[合計金額],0) AS [合計金額], COALESCE(m.[營業稅],0) AS [營業稅], " +
                "COALESCE(m.[總計金額],0) AS [總計金額], COALESCE(c.[公司全名],'') AS [公司全名] " +
                "FROM [交易主檔] m LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
                $"WHERE m.[單據類別] IN ({InList(類別)}) ORDER BY m.[交易日期], m.[交易單號]");
        }

        private static RtmData? ToListData(DataTable dt, string 編號區間 = "全部帳戶")
        {
            if (dt.Rows.Count == 0) return null;
            var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
            FillCompany(data);
            data.Master["日期區間"] = "全部日期";
            data.Master["編號區間"] = 編號區間;
            foreach (DataRow r in dt.Rows)
            {
                var d = new Dictionary<string, object?>();
                foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
                data.Detail.Add(d);
            }
            return data;
        }

        // ==================== 單據主從資料（ppDBPipeline1 主檔 + ppDBPipeline2 明細） ====================

        private static RtmData? BuildMasterDetail(DataTable dt, HashSet<string> 明細欄位, string 單號欄位)
        {
            if (dt.Rows.Count == 0) return null;
            var data = new RtmData { DetailPipeline = "ppDBPipeline2" };
            FillCompany(data);
            string 前一單號 = "";
            foreach (DataRow r in dt.Rows)
            {
                var d = new Dictionary<string, object?>();
                var 單號 = Convert.ToString(r[單號欄位]) ?? "";
                bool 新單 = 單號 != 前一單號;
                foreach (DataColumn col in dt.Columns)
                {
                    var name = col.ColumnName;
                    if (明細欄位.Contains(name))
                        d[name] = r[col];
                    else if (name == "主倉庫")
                    {
                        if (新單) data.Master["倉庫編號"] = r[col];
                    }
                    else if (新單)
                    {
                        d[$"ppDBPipeline1|{name}"] = r[col];
                    }
                }
                if (新單) 前一單號 = 單號;
                data.Detail.Add(d);
            }
            return data;
        }

        /// <summary>挑選單據清單：交易單號/交易日期/單據副碼。</summary>
        public static DataTable LoadBillList(string 單據類別) =>
            DbManager.QueryTable(
                "SELECT [單據副碼], [交易單號], [交易日期] FROM [交易主檔] " +
                "WHERE [單據類別] = $k ORDER BY [交易日期] DESC, [交易單號] DESC",
                DbManager.Param("$k", 單據類別));

        /// <summary>單據類報表資料（借出/借入/託售/託工/調撥/領料共用）。</summary>
        public static RtmData? BuildBill(string 單據類別, long 單據副碼)
        {
            var dt = DbManager.QueryTable(
                "SELECT m.[交易單號], m.[交易日期], COALESCE(m.[發票號碼],'') AS [發票號碼], " +
                "COALESCE(m.[備註],'') AS [備註], " +
                "COALESCE(m.[合計金額],0) AS [合計金額], COALESCE(m.[營業稅],0) AS [營業稅], " +
                "COALESCE(m.[總計金額],0) AS [總計金額], COALESCE(m.[應收付金額],0) AS [應收付金額], " +
                "COALESCE(m.[已收付金額],0) AS [已收付金額], COALESCE(m.[折讓金額],0) AS [折讓金額], " +
                "COALESCE(NULLIF(m.[送貨地址],''), c.[送貨地址]) AS [送貨地址], " +
                "COALESCE(m.[倉庫編號],'') AS [主倉庫], " +
                "COALESCE(c.[公司全名],'') AS [對象名稱], COALESCE(c.[聯絡人一],'') AS [聯絡人一], " +
                "COALESCE(c.[聯絡電話一],'') AS [聯絡電話一], COALESCE(c.[統一編號],'') AS [統一編號], " +
                "COALESCE(c.[傳真號碼],'') AS [傳真號碼], COALESCE(e.[員工姓名],'') AS [員工名稱], " +
                "d.[貨品編號], COALESCE(p.[品名],'') AS [品名], d.[單位], COALESCE(d.[單價],0) AS [單價], " +
                "COALESCE(d.[金額],0) AS [金額], d.[數量], COALESCE(d.[附註說明],'') AS [附註說明], " +
                "COALESCE(d.[倉庫編號],'') AS [倉庫編號], COALESCE(d.[調入倉庫],'') AS [調入倉庫] " +
                "FROM [交易主檔] m " +
                "JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
                "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
                "LEFT JOIN [員工資料] e ON m.[員工編號] = e.[員工編號] " +
                "LEFT JOIN (SELECT [貨品編號], MAX([品名]) AS [品名] FROM [貨品主檔] GROUP BY [貨品編號]) p ON d.[貨品編號] = p.[貨品編號] " +
                "WHERE m.[單據類別] = $k AND m.[單據副碼] = $c ORDER BY d.[建檔序號]",
                DbManager.Param("$k", 單據類別), DbManager.Param("$c", 單據副碼));
            return BuildMasterDetail(dt, 交易明細欄位, "交易單號");
        }

        // ==================== 折讓報表 ====================

        /// <summary>挑選折讓單清單：單據副碼/折讓單號/折讓日期。</summary>
        public static DataTable LoadDiscountList(string 折讓類別) =>
            DbManager.QueryTable(
                "SELECT [單據副碼], [折讓單號], [折讓日期] FROM [折讓主檔] " +
                "WHERE [單據類別] = $k ORDER BY [折讓日期] DESC, [折讓單號] DESC",
                DbManager.Param("$k", 折讓類別));

        /// <summary>折讓單據資料（出貨折讓單/進貨折讓單共用）。</summary>
        public static RtmData? BuildDiscountBill(string 折讓類別, long 單據副碼)
        {
            DiscountService.EnsureDiscountSchema();
            var dt = DbManager.QueryTable(
                "SELECT m.[折讓單號], m.[折讓日期], COALESCE(m.[備註],'') AS [備註], " +
                "COALESCE(m.[淨計金額],0) AS [合計金額], COALESCE(m.[稅額合計],0) AS [稅金合計], " +
                "COALESCE(m.[折讓金額],0) AS [折讓金額], COALESCE(m.[退稅],0) AS [扣抵稅額], " +
                "COALESCE(m.[總計金額],0) AS [總計金額], '' AS [製單], '' AS [覆核], " +
                "COALESCE(c.[公司全名],'') AS [對象名稱], COALESCE(c.[送貨地址],'') AS [送貨地址], " +
                "COALESCE(c.[聯絡人一],'') AS [聯絡人一], COALESCE(c.[聯絡電話一],'') AS [聯絡電話一], " +
                "COALESCE(c.[統一編號],'') AS [統一編號], COALESCE(c.[傳真號碼],'') AS [傳真號碼], " +
                "COALESCE(e.[員工姓名],'') AS [員工姓名], " +
                "d.[貨單編號], d.[發票編號], d.[發票日期], COALESCE(d.[單據金額],0) AS [單據金額], " +
                "COALESCE(d.[單據稅金],0) AS [單據稅金], COALESCE(d.[單據折讓],0) AS [單據折讓], " +
                "COALESCE(d.[折扣稅額],0) AS [折扣稅額], COALESCE(d.[附註],'') AS [附註] " +
                "FROM [折讓主檔] m " +
                "JOIN [折讓明細] d ON m.[單據副碼] = d.[單據副碼] " +
                "LEFT JOIN [客戶廠商] c ON m.[對象編號] = c.[客廠編號] " +
                "LEFT JOIN [員工資料] e ON m.[員編編號] = e.[員工編號] " +
                "WHERE m.[單據類別] = $k AND m.[單據副碼] = $c ORDER BY d.[建檔序號]",
                DbManager.Param("$k", 折讓類別), DbManager.Param("$c", 單據副碼));
            return BuildMasterDetail(dt, 折讓明細欄位, "折讓單號");
        }

        /// <summary>折讓明細表資料（出貨/進貨/客戶/廠商/採購/業務折讓明細表共用）。</summary>
        public static RtmData? BuildDiscountListData(string 折讓類別)
        {
            DiscountService.EnsureDiscountSchema();
            var dt = DbManager.QueryTable(
                "SELECT m.[折讓單號], m.[折讓日期], COALESCE(c.[公司簡稱],'') AS [公司簡稱], " +
                "COALESCE(m.[員編編號],'') AS [員工編號], " +
                "d.[貨單編號], d.[發票編號], COALESCE(d.[單據金額],0) AS [單據金額], " +
                "COALESCE(d.[單據稅金],0) AS [單據稅金], COALESCE(d.[單據折讓],0) AS [單據折讓], " +
                "COALESCE(d.[折扣稅額],0) AS [折扣稅額], COALESCE(d.[附註],'') AS [附註] " +
                "FROM [折讓主檔] m " +
                "JOIN [折讓明細] d ON m.[單據副碼] = d.[單據副碼] " +
                "LEFT JOIN [客戶廠商] c ON m.[對象編號] = c.[客廠編號] " +
                "WHERE m.[單據類別] = $k ORDER BY m.[折讓日期], m.[折讓單號], d.[建檔序號]",
                DbManager.Param("$k", 折讓類別));
            var data = ToListData(dt);
            if (data is null) return null;
            foreach (var row in data.Detail)
                if (row.TryGetValue("折讓日期", out var v)) row["折讓主檔.折讓日期"] = v;
            return data;
        }

        // ==================== 專案 / 銀行報表 ====================

        public static RtmData? Build專案收款沖銷日報表()
        {
            var dt = DbManager.QueryTable(
                "SELECT p.[沖帳日期], COALESCE(p.[沖帳合計],0) AS [沖帳合計], " +
                "COALESCE(c.[公司全名],'') AS [公司全名], COALESCE(p.[現金金額],0) AS [現金金額], " +
                "COALESCE(p.[票據金額],0) AS [票據金額], COALESCE(p.[取用預收],0) AS [取用預收], " +
                "COALESCE(p.[累入預收],0) AS [累入預收], COALESCE(j.[專案名稱],'') AS [專案名稱] " +
                "FROM [收付主檔] p " +
                "LEFT JOIN [客戶廠商] c ON p.[沖帳對象] = c.[客廠編號] " +
                "LEFT JOIN [專案設定] j ON p.[專案編號] = j.[專案編號] " +
                "WHERE COALESCE(NULLIF(p.[專案編號],''),'') <> '' ORDER BY p.[沖帳日期]");
            return ToListData(dt, "全部專案");
        }

        public static RtmData? Build銀行存款對帳單()
        {
            var dt = DbManager.QueryTable(
                "SELECT [日期], [對象名稱], [類別], COALESCE([支票號碼],'') AS [支票號碼], " +
                "COALESCE([存入金額],0) AS [存入金額], COALESCE([提出金額],0) AS [提出金額], " +
                "COALESCE([結餘金額],0) AS [結餘金額], COALESCE([備註],'') AS [備註] " +
                "FROM [銀行存款] ORDER BY [日期]");
            return ToListData(dt);
        }

        public static RtmData? Build銀行資金預估明細表()
        {
            var dt = DbManager.QueryTable(
                "SELECT [日期], [對象名稱], [收付類別], [類別], COALESCE([支票號碼],'') AS [支票號碼], " +
                "COALESCE([存入金額],0) AS [存入金額], COALESCE([提出金額],0) AS [提出金額], " +
                "COALESCE([結餘金額],0) AS [結餘金額], COALESCE([備註],'') AS [備註] " +
                "FROM [銀行存款] ORDER BY [日期]");
            return ToListData(dt);
        }

        // ==================== 41 份報表公開建構函式 ====================

        // 借出 / 借入（13）
        public static RtmData? Build借出明細表() => ToListData(LoadTxDetail(new[] { 借出 }));
        public static RtmData? Build借出還入明細表() => ToListData(LoadTxDetail(new[] { 借出還入 }));
        public static RtmData? Build借入還出明細表() => ToListData(LoadTxDetail(new[] { 借入還出 }));
        public static RtmData? Build客戶借出明細表() => ToListData(LoadTxDetail(new[] { 借出 }));
        public static RtmData? Build客戶借出還入明細表() => ToListData(LoadTxDetail(new[] { 借出還入 }));
        public static RtmData? Build貨品借出明細表() => ToListData(LoadTxDetail(new[] { 借出 }));
        public static RtmData? Build貨品借出還入明細表() => ToListData(LoadTxDetail(new[] { 借出還入 }));
        public static RtmData? Build貨品借入還出明細表() => ToListData(LoadTxDetail(new[] { 借入還出 }));
        public static RtmData? Build廠商借入還出明細表() => ToListData(LoadTxDetail(new[] { 借入還出 }));

        // 託售 / 託工（7）
        public static RtmData? Build託售回貨明細表() => ToListData(LoadTxDetail(new[] { 託售回貨 }));
        public static RtmData? Build客戶託售回貨明細表() => ToListData(LoadTxDetail(new[] { 託售回貨 }));
        public static RtmData? Build貨品託售明細表() => ToListData(LoadTxDetail(new[] { 託售 }));

        // 調撥（2）
        public static RtmData? Build倉庫調撥明細表() => ToListData(LoadTxDetail(new[] { 調撥 }));
        public static RtmData? Build貨品調撥明細表() => ToListData(LoadTxDetail(new[] { 調撥 }));

        // 進退貨（4）
        public static RtmData? Build進貨退出明細表() => ToListData(LoadTxDetail(new[] { 進退類 }));
        public static RtmData? Build進退貨簡要表() => ToListData(LoadTxMaster(new[] { 出貨類, 出退類, 進貨類, 進退類 }));
        public static RtmData? Build貨品進貨及退出明細表() => ToListData(LoadTxDetail(new[] { 進貨類, 進退類 }));
        public static RtmData? Build廠商入出庫明細表() => ToListData(LoadTxDetail(new[] { 進貨類, 進退類 }));

        // 專案（3）
        public static RtmData? Build專案出退貨明細表() => ToListData(LoadTxDetail(new[] { 出貨類, 出退類 }, 限專案: true));
        public static RtmData? Build專案進退貨明細表() => ToListData(LoadTxDetail(new[] { 進貨類, 進退類 }, 限專案: true));

        // 折讓明細表（6）
        public static RtmData? Build出貨折讓明細表() => BuildDiscountListData(出貨折讓);
        public static RtmData? Build進貨折讓明細表() => BuildDiscountListData(進貨折讓);
        public static RtmData? Build客戶折讓明細表() => BuildDiscountListData(出貨折讓);
        public static RtmData? Build廠商折讓明細表() => BuildDiscountListData(進貨折讓);
        public static RtmData? Build採購折讓明細表() => BuildDiscountListData(進貨折讓);
        public static RtmData? Build業務折讓明細表() => BuildDiscountListData(出貨折讓);

        // ==================== 分析統計報表（13） ====================

        private const string 交易主明細Join =
            "FROM [交易主檔] m JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "LEFT JOIN (SELECT [貨品編號], MAX([品名]) AS [品名], MAX([類別編號]) AS [類別編號], " +
            "MAX([基本單位]) AS [基本單位], MAX([現行平均成本]) AS [現行平均成本] FROM [貨品主檔] GROUP BY [貨品編號]) p ON d.[貨品編號] = p.[貨品編號] " +
            "LEFT JOIN [貨品類別] g ON p.[類別編號] = g.[類別編號] ";

        private static string 貨品名稱(string 前綴) =>
            $"COALESCE(NULLIF(p.[品名],''), NULLIF(d.[品名],''), '') AS [{前綴}]";

        private static string 交易單位(string 前綴) =>
            $"COALESCE(NULLIF(d.[單位],''), p.[基本單位], '') AS [{前綴}]";

        /// <summary>客戶交易排行：按客戶彙總出貨/折讓/退回/實銷金額。</summary>
        public static RtmData? Build客戶交易排行()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(m.[交易對象],'') AS [編號], COALESCE(c.[公司全名],'') AS [公司全名], " +
                "SUM(CASE WHEN m.[單據類別] = '出貨' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [出貨金額], " +
                "0 AS [折讓金額], " +
                "SUM(CASE WHEN m.[單據類別] = '出退' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [退回金額], " +
                "SUM(COALESCE(d.[金額],0) * CASE WHEN m.[單據類別] = '出貨' THEN 1 ELSE -1 END) AS [實銷金額] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "GROUP BY m.[交易對象], c.[公司全名] ORDER BY [實銷金額] DESC");
            return ToListData(dt, "全部客戶");
        }

        /// <summary>廠商交易排行：按廠商彙總進貨/折讓/退回/實銷金額。</summary>
        public static RtmData? Build廠商交易排行()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(m.[交易對象],'') AS [編號], COALESCE(c.[公司全名],'') AS [公司全名], " +
                "SUM(CASE WHEN m.[單據類別] = '進貨' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [出貨金額], " +
                "0 AS [折讓金額], " +
                "SUM(CASE WHEN m.[單據類別] = '進退' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [退回金額], " +
                "SUM(COALESCE(d.[金額],0) * CASE WHEN m.[單據類別] = '進貨' THEN 1 ELSE -1 END) AS [實銷金額] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('進貨','進退') " +
                "GROUP BY m.[交易對象], c.[公司全名] ORDER BY [實銷金額] DESC");
            return ToListData(dt, "全部廠商");
        }

        /// <summary>客戶交易類別：按客戶＋貨品類別彙總數量與金額。</summary>
        public static RtmData? Build客戶交易類別()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(m.[交易對象],'') AS [客廠編號], COALESCE(c.[公司全名],'') AS [公司全名], " +
                "COALESCE(p.[類別編號],'') AS [類別編號], COALESCE(g.[類別名稱],'') AS [類別名稱], " +
                "SUM(COALESCE(d.[數量],0)) AS [數量之總計], SUM(COALESCE(d.[金額],0)) AS [金額之總計] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "GROUP BY m.[交易對象], c.[公司全名], p.[類別編號], g.[類別名稱] " +
                "ORDER BY m.[交易對象], p.[類別編號]");
            return ToListData(dt, "全部客戶");
        }

        /// <summary>客戶歷次售價：出貨明細按客戶＋貨品列示歷史售價。</summary>
        public static RtmData? Build客戶歷次售價()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(m.[交易對象],'') AS [交易對象], COALESCE(c.[公司全名],'') AS [公司全名], " +
                "m.[交易日期], " + 貨品名稱("品名") + ", " + 交易單位("單位") + ", " +
                "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額], " +
                "d.[貨品編號], COALESCE(d.[數量],0) AS [數量] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "ORDER BY m.[交易對象], d.[貨品編號], m.[交易日期], d.[建檔序號]");
            return ToListData(dt, "全部客戶");
        }

        /// <summary>廠商歷次售價：進貨明細按廠商＋貨品列示歷史進價。</summary>
        public static RtmData? Build廠商歷次售價()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(m.[交易對象],'') AS [交易對象], COALESCE(c.[公司全名],'') AS [公司全名], " +
                "m.[交易日期], " + 貨品名稱("品名") + ", " + 交易單位("單位") + ", " +
                "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額], " +
                "d.[貨品編號], COALESCE(d.[數量],0) AS [數量] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('進貨','進退') " +
                "ORDER BY m.[交易對象], d.[貨品編號], m.[交易日期], d.[建檔序號]");
            return ToListData(dt, "全部廠商");
        }

        /// <summary>業務利潤分析表：出貨明細之毛利與毛利率。</summary>
        public static RtmData? Build業務利潤分析表()
        {
            var dt = DbManager.QueryTable(
                "SELECT m.[交易日期], m.[交易單號], " +
                "COALESCE(NULLIF(e.[員工姓名],''), m.[員工編號], '') AS [員工姓名], " +
                "d.[貨品編號], " + 貨品名稱("品名") + ", COALESCE(d.[數量],0) AS [數量], " +
                交易單位("單位") + ", COALESCE(d.[單價],0) AS [單價], " +
                "COALESCE(d.[金額],0) AS [金額], " +
                "(COALESCE(d.[金額],0) - COALESCE(d.[數量],0) * COALESCE(p.[現行平均成本],0)) AS [毛利], " +
                "ROUND((COALESCE(d.[金額],0) - COALESCE(d.[數量],0) * COALESCE(p.[現行平均成本],0)) " +
                "* 100.0 / NULLIF(COALESCE(d.[金額],0),0), 1) AS [毛利率%] " +
                "FROM [交易主檔] m JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
                "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
                "LEFT JOIN (SELECT [貨品編號], MAX([品名]) AS [品名], MAX([基本單位]) AS [基本單位], " +
                "MAX([現行平均成本]) AS [現行平均成本] FROM [貨品主檔] GROUP BY [貨品編號]) p ON d.[貨品編號] = p.[貨品編號] " +
                "LEFT JOIN [員工資料] e ON m.[員工編號] = e.[員工編號] " +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "ORDER BY m.[員工編號], m.[交易日期], m.[交易單號], d.[建檔序號]");
            return ToListData(dt, "全部業務");
        }

        /// <summary>業務銷售排行：按業務彙總出貨/折讓/退回/實銷金額。</summary>
        public static RtmData? Build業務銷售排行()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(m.[員工編號],'') AS [編號], " +
                "COALESCE(NULLIF(e.[員工姓名],''), m.[員工編號], '') AS [員工姓名], " +
                "SUM(CASE WHEN m.[單據類別] = '出貨' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [出貨金額], " +
                "0 AS [折讓金額], " +
                "SUM(CASE WHEN m.[單據類別] = '出退' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [退回金額], " +
                "SUM(COALESCE(d.[金額],0) * CASE WHEN m.[單據類別] = '出貨' THEN 1 ELSE -1 END) AS [實銷金額] " +
                "FROM [交易主檔] m JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
                "LEFT JOIN [員工資料] e ON m.[員工編號] = e.[員工編號] " +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "GROUP BY m.[員工編號], e.[員工姓名] ORDER BY [實銷金額] DESC");
            return ToListData(dt, "全部業務");
        }

        /// <summary>業務銷售明細表：出貨/出退明細逐列。</summary>
        public static RtmData? Build業務銷售明細表()
        {
            var dt = DbManager.QueryTable(
                "SELECT m.[交易日期], m.[交易單號], COALESCE(c.[公司簡稱],'') AS [公司簡稱], " +
                "d.[貨品編號], " + 貨品名稱("品名") + ", COALESCE(d.[數量],0) AS [數量], " +
                交易單位("單位") + ", COALESCE(d.[單價],0) AS [單價], " +
                "COALESCE(d.[金額],0) AS [金額], m.[單據類別] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
            return ToListData(dt, "全部業務");
        }

        /// <summary>貨品交易排行：按貨品彙總出貨/退回數量與金額。</summary>
        public static RtmData? Build貨品交易排行()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(d.[貨品編號],'') AS [編號], " + 貨品名稱("品名") + ", " +
                "SUM(CASE WHEN m.[單據類別] = '出貨' THEN COALESCE(d.[數量],0) ELSE 0 END) AS [出貨數量], " +
                "COALESCE(p.[基本單位],'') AS [基本單位], " +
                "SUM(CASE WHEN m.[單據類別] = '出貨' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [出貨金額], " +
                "SUM(CASE WHEN m.[單據類別] = '出退' THEN COALESCE(d.[數量],0) ELSE 0 END) AS [退回數量], " +
                "SUM(CASE WHEN m.[單據類別] = '出退' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [退回金額], " +
                "SUM(COALESCE(d.[數量],0) * CASE WHEN m.[單據類別] = '出貨' THEN 1 ELSE -1 END) AS [合計數量], " +
                "SUM(COALESCE(d.[金額],0) * CASE WHEN m.[單據類別] = '出貨' THEN 1 ELSE -1 END) AS [合計金額] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "GROUP BY d.[貨品編號], p.[品名], p.[基本單位] ORDER BY [合計金額] DESC");
            return ToListData(dt, "全部貨品");
        }

        /// <summary>貨品交易明細表：出貨/出退明細按貨品群組逐列。</summary>
        public static RtmData? Build貨品交易明細表()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(d.[貨品編號],'') AS [貨品編號], m.[交易日期], m.[交易單號], COALESCE(c.[公司簡稱],'') AS [公司簡稱], " +
                貨品名稱("品名") + ", COALESCE(d.[數量],0) AS [數量], " +
                交易單位("單位") + ", COALESCE(d.[單價],0) AS [單價], " +
                "COALESCE(d.[金額],0) AS [金額], m.[單據類別] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "ORDER BY d.[貨品編號], m.[交易日期], m.[交易單號], d.[建檔序號]");
            return ToListData(dt, "全部貨品");
        }

        /// <summary>客戶別報價明細：採訂（報價/訂貨）明細按客戶列示。</summary>
        public static RtmData? Build客戶別報價明細()
        {
            var dt = DbManager.QueryTable(
                "SELECT m.[交易單號], m.[交易日期], COALESCE(c.[公司全名],'') AS [公司全名], " +
                "COALESCE(d.[貨品編號],'') AS [貨品編號], COALESCE(d.[品名],'') AS [品名], " +
                "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單位],'') AS [單位], " +
                "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額] " +
                "FROM [採訂主檔] m JOIN [採訂明細] d ON m.[單據副碼] = d.[單據副碼] " +
                "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
                "WHERE m.[單據類別] IN ('報價','訂貨') " +
                "ORDER BY m.[交易對象], m.[交易日期], d.[建檔序號]");
            return ToListData(dt, "全部對象");
        }

        /// <summary>貨品別報價明細：採訂（報價/訂貨）明細按貨品列示。</summary>
        public static RtmData? Build貨品別報價明細()
        {
            var dt = DbManager.QueryTable(
                "SELECT m.[交易單號], m.[交易日期], COALESCE(c.[公司全名],'') AS [公司全名], " +
                "COALESCE(d.[貨品編號],'') AS [貨品編號], COALESCE(d.[品名],'') AS [品名], " +
                "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[單位],'') AS [單位], " +
                "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額] " +
                "FROM [採訂主檔] m JOIN [採訂明細] d ON m.[單據副碼] = d.[單據副碼] " +
                "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
                "WHERE m.[單據類別] IN ('報價','訂貨') " +
                "ORDER BY d.[貨品編號], m.[交易日期], d.[建檔序號]");
            return ToListData(dt, "全部貨品");
        }

        /// <summary>貨品類別排行：按貨品類別彙總出貨/退回數量與金額。</summary>
        public static RtmData? Build貨品類別排行()
        {
            var dt = DbManager.QueryTable(
                "SELECT COALESCE(p.[類別編號],'') AS [編號], COALESCE(g.[類別名稱],'') AS [類別名稱], " +
                "SUM(CASE WHEN m.[單據類別] = '出貨' THEN COALESCE(d.[數量],0) ELSE 0 END) AS [出貨數量], " +
                "SUM(CASE WHEN m.[單據類別] = '出貨' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [出貨金額], " +
                "SUM(CASE WHEN m.[單據類別] = '出退' THEN COALESCE(d.[數量],0) ELSE 0 END) AS [退回數量], " +
                "SUM(CASE WHEN m.[單據類別] = '出退' THEN COALESCE(d.[金額],0) ELSE 0 END) AS [退回金額], " +
                "SUM(COALESCE(d.[數量],0) * CASE WHEN m.[單據類別] = '出貨' THEN 1 ELSE -1 END) AS [合計數量], " +
                "SUM(COALESCE(d.[金額],0) * CASE WHEN m.[單據類別] = '出貨' THEN 1 ELSE -1 END) AS [合計金額] " +
                交易主明細Join +
                "WHERE m.[單據類別] IN ('出貨','出退') " +
                "GROUP BY p.[類別編號], g.[類別名稱] ORDER BY [合計金額] DESC");
            return ToListData(dt, "全部類別");
        }
    }
}
