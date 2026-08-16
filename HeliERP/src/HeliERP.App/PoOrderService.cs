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
/// 採購訂貨作業核心：報價／訂貨／採購／詢價四種單據。
/// 資料流（單一 BEGIN IMMEDIATE 交易，失敗全數回滾）：
/// 採訂主檔 → 採訂明細。
/// 採訂單據為契約／詢報價性質，不影響貨品庫存，亦不產生帳款。
/// 訂貨／採購單以「交易數量」累計已交數量，主檔「未交完數」= 數量 − 交易數量。
/// </summary>
public static class PoOrderService
{
    /// <summary>採訂單據類別定義</summary>
    public sealed class PoKind
    {
        /// <summary>單據類別（資料庫值）</summary>
        public required string Name { get; init; }
        /// <summary>交易對象類型：客戶 / 廠商</summary>
        public required string ObjectType { get; init; }
        /// <summary>稅率來源：銷項 / 進項 / 免稅</summary>
        public required string TaxSource { get; init; }
        /// <summary>報表檔名</summary>
        public required string ReportFile { get; init; }
    }

    public static readonly PoKind[] Kinds =
    {
        new() { Name = "報價", ObjectType = "客戶", TaxSource = "銷項", ReportFile = "報價單據.rtm" },
        new() { Name = "訂貨", ObjectType = "客戶", TaxSource = "銷項", ReportFile = "訂貨單據.rtm" },
        new() { Name = "採購", ObjectType = "廠商", TaxSource = "進項", ReportFile = "採購單據.rtm" },
        new() { Name = "詢價", ObjectType = "廠商", TaxSource = "進項", ReportFile = "詢價單據.rtm" },
    };

    public static PoKind GetKind(string name) => Kinds.FirstOrDefault(k => k.Name == name) ?? Kinds[0];

    public sealed class PoLine
    {
        public string 貨品編號 = "";
        public string 倉庫編號 = "";
        public decimal 數量;
        public string 單位 = "";
        public decimal 單價;
        public decimal 成本;
        public decimal 折扣 = 100m;
        public string 附註說明 = "";
    }

    public sealed class PoBillRequest
    {
        public string 單據類別 = "報價";
        /// <summary>null = 新增（自動取號）；非 null = 修改（明細重寫）</summary>
        public long? 單據副碼;
        public DateTime 交易日期 = DateTime.Now;
        public DateTime 交貨日期 = DateTime.Now;
        public string 交易對象 = "";
        public string 部門編號 = "";
        public string 員工編號 = "";
        public string 送貨地址 = "";
        public string 課稅類別 = "外加";
        public string 備註 = "";
        public List<PoLine> 明細 = new();
    }

    public sealed record PoSaveResult(string 交易單號, long 單據副碼);

    // ==================== 存檔（新增 / 修改全單重寫明細） ====================

    public static PoSaveResult SavePoBill(PoBillRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.交易對象))
            throw new InvalidOperationException("請輸入交易對象。");
        if (req.明細.Count == 0)
            throw new InvalidOperationException("請至少輸入一筆明細。");
        if (req.明細.Any(d => string.IsNullOrWhiteSpace(d.貨品編號) || d.數量 <= 0))
            throw new InvalidOperationException("明細的貨品編號不可空白，且數量必須大於 0。");

        var kind = GetKind(req.單據類別);
        bool 免稅 = req.課稅類別.Contains("免");
        decimal 稅率 = TradeService.LoadParams().銷項稅率;
        if (kind.TaxSource == "進項")
            稅率 = TradeService.LoadParams().進項稅率;

        PoSaveResult? result = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            long 副碼 = req.單據副碼 ?? NextSeq(conn, "採訂主檔", "單據副碼");
            string 單號 = req.單據副碼 is not null
                ? Str(SelectOne(conn, "SELECT [交易單號] FROM [採訂主檔] WHERE [單據副碼] = $c",
                    DbManager.Param("$c", 副碼))?["交易單號"] ?? "")
                : NextPoNo(conn, kind.Name);
            if (單號.Length == 0)
                單號 = NextPoNo(conn, kind.Name);
            var dup = ExecScalar(conn,
                "SELECT COUNT(*) FROM [採訂主檔] WHERE [交易單號] = $n AND [單據類別] = $k AND [單據副碼] <> $c",
                DbManager.Param("$n", 單號), DbManager.Param("$k", kind.Name), DbManager.Param("$c", 副碼));
            if (Convert.ToInt64(dup) > 0)
                throw new InvalidOperationException($"交易單號「{單號}」已存在，請改用其他單號。");

            decimal 合計 = 0m;
            long seq = NextSeq(conn, "採訂明細", "建檔序號");
            var 已交 = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (req.單據副碼 is not null)
            {
                var 舊明細 = SelectAll(conn,
                    "SELECT [貨品編號], [交易數量] FROM [採訂明細] WHERE [單據副碼] = $c",
                    DbManager.Param("$c", 副碼));
                foreach (var o in 舊明細)
                {
                    var g = Str(o["貨品編號"]);
                    if (g.Length > 0)
                        已交[g] = Math.Max(已交.GetValueOrDefault(g), GetDec(o, "交易數量", 0m));
                }
            }
            var 明細資料 = new List<(Dictionary<string, object?> Row, decimal 金額, decimal 未交)>();
            foreach (var d in req.明細)
            {
                decimal 金額 = CalcDetailAmount(d);
                string 品名 = LookupStr(conn, "SELECT [品名] FROM [貨品主檔] WHERE [貨品編號] = $g", "$g", d.貨品編號);
                decimal 成本 = d.成本 == 0
                    ? LookupDec(conn, "SELECT COALESCE([現行平均成本],0) FROM [貨品主檔] WHERE [貨品編號] = $g", "$g", d.貨品編號)
                    : d.成本;
                decimal 交易數量 = 已交.GetValueOrDefault(d.貨品編號);
                decimal 未交 = Math.Max(0m, d.數量 - 交易數量);
                合計 += 金額;
                明細資料.Add((new Dictionary<string, object?>
                {
                    ["單據副碼"] = 副碼, ["建檔序號"] = seq++, ["貨品編號"] = d.貨品編號,
                    ["倉庫編號"] = Nz(d.倉庫編號), ["調入倉庫"] = null,
                    ["數量"] = d.數量, ["交易數量"] = 交易數量, ["單位"] = Nz(d.單位),
                    ["單價"] = d.單價, ["成本"] = 成本, ["折扣"] = d.折扣, ["金額"] = 金額,
                    ["附註說明"] = Nz(d.附註說明), ["贈品"] = 0, ["服務項目"] = 0,
                    ["計算庫存"] = 0,
                }, 金額, 未交));
            }

            decimal 稅 = 免稅 ? 0m : Math.Round(合計 * 稅率 / 100m, 0, MidpointRounding.AwayFromZero);
            decimal 總計 = 合計 + 稅;
            decimal 未交完數 = 明細資料.Sum(x => x.未交);

            var master = new Dictionary<string, object?>
            {
                ["單據類別"] = kind.Name, ["交易單號"] = 單號, ["單據副碼"] = 副碼,
                ["交易日期"] = req.交易日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["交易對象"] = req.交易對象,
                ["交貨日期"] = req.交貨日期 == default ? null : req.交貨日期.ToString("yyyy-MM-dd HH:mm:ss"),
                ["部門編號"] = Nz(req.部門編號), ["員工編號"] = Nz(req.員工編號),
                ["幣別編號"] = "NT", ["匯率"] = 1m, ["明細筆數"] = req.明細.Count,
                ["來源單據"] = null, ["來源單號"] = 單號,
                ["合計金額"] = 合計, ["營業稅"] = 稅, ["總計金額"] = 總計,
                ["折讓金額"] = 0m, ["未交完數"] = 未交完數, ["課稅類別"] = req.課稅類別,
                ["製單"] = CurrentUser, ["覆核"] = null, ["備註"] = Nz(req.備註),
                ["送貨地址"] = Nz(req.送貨地址),
            };

            if (req.單據副碼 is null)
            {
                InsertRow(conn, "採訂主檔", master);
                foreach (var (row, _, _) in 明細資料)
                    InsertRow(conn, "採訂明細", row);
            }
            else
            {
                var sets = string.Join(", ", master.Keys.Select(k => $"[{k}] = ${k}"));
                var pars = master.Select(kv => DbManager.Param($"${kv.Key}", kv.Value)).ToList();
                pars.Add(DbManager.Param("$c", 副碼));
                Execute(conn, $"UPDATE [採訂主檔] SET {sets} WHERE [單據副碼] = $c", pars.ToArray());
                Execute(conn, "DELETE FROM [採訂明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
                foreach (var (row, _, _) in 明細資料)
                    InsertRow(conn, "採訂明細", row);
            }

            result = new PoSaveResult(單號, 副碼);
        });
        if (result is not null)
            AuditService.Log(AuditService.存檔, "採訂", result.交易單號, "成功",
                $"{kind.Name}，明細 {req.明細.Count} 筆");
        return result!;
    }

    // ==================== 刪除 ====================

    public static void DeletePoBill(long 副碼)
    {
        string? auditNo = null;
        DbManager.ExecuteImmediateTransaction(conn =>
        {
            var m = SelectOne(conn, "SELECT [交易單號],[單據類別] FROM [採訂主檔] WHERE [單據副碼] = $c",
                DbManager.Param("$c", 副碼))
                ?? throw new InvalidOperationException("找不到該單據，可能已被刪除。");
            if (!Kinds.Any(k => k.Name == Str(m["單據類別"])))
                throw new InvalidOperationException("該單據不是報價／訂貨／採購／詢價單，無法在此刪除。");
            Execute(conn, "DELETE FROM [採訂明細] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            Execute(conn, "DELETE FROM [採訂主檔] WHERE [單據副碼] = $c", DbManager.Param("$c", 副碼));
            auditNo = Str(m["交易單號"]);
        });
        if (auditNo is not null)
            AuditService.Log(AuditService.刪除, "採訂", auditNo, "成功");
    }

    // ==================== 帶入查詢（畫面使用） ====================

    /// <summary>採訂單清單（依類別、單號倒序）</summary>
    public static DataTable LoadPoList(string 單據類別, string? 單號 = null)
    {
        var where = new List<string> { "m.[單據類別] = $k" };
        var pars = new List<SqliteParameter> { DbManager.Param("$k", 單據類別) };
        if (!string.IsNullOrWhiteSpace(單號))
        {
            where.Add("m.[交易單號] LIKE $n");
            pars.Add(DbManager.Param("$n", 單號.Trim() + "%"));
        }
        return DbManager.QueryTable(
            "SELECT m.[單據副碼], m.[交易單號], m.[交易日期], m.[交貨日期], " +
            "COALESCE(c.[公司簡稱],'') AS [對象名稱], m.[交易對象], " +
            "COALESCE(m.[合計金額],0) AS [合計金額], COALESCE(m.[營業稅],0) AS [營業稅], " +
            "COALESCE(m.[總計金額],0) AS [總計金額], COALESCE(m.[明細筆數],0) AS [明細筆數], " +
            "COALESCE(m.[未交完數],0) AS [未交完數], COALESCE(m.[製單],'') AS [製單] " +
            "FROM [採訂主檔] m " +
            "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "WHERE " + string.Join(" AND ", where) +
            " ORDER BY m.[交易單號] DESC", pars.ToArray());
    }

    /// <summary>單一採訂單主檔（檢視／列印用，全欄位）</summary>
    public static DataTable LoadPoMaster(long 副碼) =>
        DbManager.QueryTable("SELECT * FROM [採訂主檔] WHERE [單據副碼] = $c",
            DbManager.Param("$c", 副碼));

    /// <summary>單一採訂單明細（檢視用）</summary>
    public static DataTable LoadPoDetails(long 副碼) =>
        DbManager.QueryTable(
            "SELECT d.[貨品編號], COALESCE(p.[品名],'') AS [品名], COALESCE(d.[倉庫編號],'') AS [倉庫編號], " +
            "d.[數量], d.[交易數量], COALESCE(d.[單位],'') AS [單位], COALESCE(d.[單價],0) AS [單價], " +
            "COALESCE(d.[折扣],100) AS [折扣], COALESCE(d.[金額],0) AS [金額], " +
            "COALESCE(d.[附註說明],'') AS [附註說明] " +
            "FROM [採訂明細] d " +
            "LEFT JOIN [貨品主檔] p ON p.[貨品編號] = d.[貨品編號] " +
            "WHERE d.[單據副碼] = $c ORDER BY d.[建檔序號]",
            DbManager.Param("$c", 副碼));

    /// <summary>採訂單明細（列印用，全欄位）</summary>
    public static DataTable LoadPoPrintDetails(long 副碼) =>
        DbManager.QueryTable(
            "SELECT * FROM [採訂明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
            DbManager.Param("$c", 副碼));

    /// <summary>對象下拉（依類別決定客戶或廠商）</summary>
    public static DataTable LoadObjectCombo(string 單據類別) =>
        TradeService.LoadCustomerCombo(GetKind(單據類別).ObjectType);

    /// <summary>部門下拉</summary>
    public static DataTable LoadDepartmentCombo() =>
        DbManager.QueryTable("SELECT [部門編號], [部門名稱] FROM [部門資料] ORDER BY [部門編號]");

    /// <summary>預估下一筆單號（僅供畫面顯示；正式取號以 SavePoBill 交易內為準）</summary>
    public static string PreviewPoNo(string 單據類別)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = DbManager.QueryScalar(
            "SELECT MAX([交易單號]) FROM [採訂主檔] WHERE [單據類別] = $k AND [交易單號] LIKE $p",
            DbManager.Param("$k", 單據類別), DbManager.Param("$p", today + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= 10 && int.TryParse(max.AsSpan(6, 4), out var last))
            seq = last + 1;
        return today + seq.ToString("0000");
    }

    /// <summary>明細金額：數量 × 單價 × 折扣 ÷ 100</summary>
    public static decimal CalcDetailAmount(PoLine d) =>
        Math.Round(d.數量 * d.單價 * d.折扣 / 100m, 2, MidpointRounding.AwayFromZero);

    // ==================== 交易內輔助 ====================

    private static string CurrentUser => Environment.UserName;

    private static long NextSeq(SqliteConnection conn, string table, string column) =>
        Convert.ToInt64(ExecScalar(conn, $"SELECT COALESCE(MAX([{column}]), 0) FROM [{table}]")) + 1;

    /// <summary>採訂單號：YYMMDD + 當日 4 位流水（依單據類別）</summary>
    private static string NextPoNo(SqliteConnection conn, string kind)
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var max = ExecScalar(conn,
            "SELECT MAX([交易單號]) FROM [採訂主檔] WHERE [單據類別] = $k AND [交易單號] LIKE $p",
            DbManager.Param("$k", kind), DbManager.Param("$p", today + "%")) as string;
        int seq = 1;
        if (!string.IsNullOrEmpty(max) && max.Length >= 10 && int.TryParse(max.AsSpan(6, 4), out var last))
            seq = last + 1;
        if (seq > 9999)
            throw new InvalidOperationException("當日單號流水已滿（超過 9999 張），請聯絡系統管理員。");
        return today + seq.ToString("0000");
    }

    private static void InsertRow(SqliteConnection conn, string table, Dictionary<string, object?> vals)
    {
        var cols = string.Join(", ", vals.Keys.Select(k => $"[{k}]"));
        var marks = string.Join(", ", vals.Keys.Select(k => $"${k}"));
        var pars = vals.Select(kv => DbManager.Param($"${kv.Key}", kv.Value)).ToArray();
        Execute(conn, $"INSERT INTO [{table}] ({cols}) VALUES ({marks})", pars);
    }

    private static void Execute(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        cmd.ExecuteNonQuery();
    }

    private static object? ExecScalar(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    private static Dictionary<string, object?>? SelectOne(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return null;
        var dict = new Dictionary<string, object?>();
        for (int i = 0; i < r.FieldCount; i++)
            dict[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return dict;
    }

    private static List<Dictionary<string, object?>> SelectAll(SqliteConnection conn, string sql, params SqliteParameter[] pars)
    {
        var list = new List<Dictionary<string, object?>>();
        using var cmd = DbManager.CreateCommand(conn, sql, pars);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                dict[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(dict);
        }
        return list;
    }

    private static string LookupStr(SqliteConnection conn, string sql, string paramName, string value)
    {
        var v = ExecScalar(conn, sql, DbManager.Param(paramName, value));
        return v is null ? "" : v.ToString() ?? "";
    }

    private static decimal LookupDec(SqliteConnection conn, string sql, string paramName, string value)
    {
        var v = ExecScalar(conn, sql, DbManager.Param(paramName, value));
        return v is null || !decimal.TryParse(v.ToString(), out var m) ? 0m : m;
    }

    private static string Str(object? v) => v is null or DBNull ? "" : v.ToString() ?? "";

    private static decimal GetDec(Dictionary<string, object?> d, string col, decimal def) =>
        d.TryGetValue(col, out var v) && v is not null && decimal.TryParse(v.ToString(), out var m) ? m : def;

    private static object? Nz(string s) => string.IsNullOrEmpty(s) ? null : s;
}
