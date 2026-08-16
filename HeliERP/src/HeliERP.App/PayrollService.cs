// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Text;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>
/// 薪資計算服務：依出缺勤統計 + 薪資設定 + 員工基本薪資，
/// 產生指定年月的薪資主檔與薪資明細。
/// </summary>
public static class PayrollService
{
    /// <summary>計算指定年月薪資，回傳處理摘要文字。</summary>
    public static string Calculate(int year, int month)
    {
        var prefix = $"{year:0000}-{month:00}";
        var att = DbManager.QueryTable(
            "SELECT [員工編號], [所屬部門], [出勤部門] FROM [出缺主檔] WHERE [出勤年度]=$y AND [出勤月份]=$m ORDER BY [員工編號]",
            DbManager.Param("$y", year), DbManager.Param("$m", month));

        var sb = new StringBuilder();
        if (att.Rows.Count == 0) return $"無 {year} 年 {month} 月出缺勤資料，請先建立出缺勤。";

        int okCount = 0;
        foreach (DataRow row in att.Rows)
        {
            var empNo = Convert.ToString(row["員工編號"]) ?? "";
            if (empNo.Length == 0) continue;

            var emp = DbManager.QueryTable(
                "SELECT [本薪],[日薪],[時薪],[健保金額],[勞保金額] FROM [員工資料] WHERE [員工編號]=$e",
                DbManager.Param("$e", empNo));
            decimal 本薪 = emp.Rows.Count > 0 ? Convert.ToDecimal(emp.Rows[0]["本薪"]) : 0;
            decimal 日薪 = emp.Rows.Count > 0 ? Convert.ToDecimal(emp.Rows[0]["日薪"]) : 0;
            decimal 時薪 = emp.Rows.Count > 0 ? Convert.ToDecimal(emp.Rows[0]["時薪"]) : 0;
            decimal 健保 = emp.Rows.Count > 0 ? Convert.ToDecimal(emp.Rows[0]["健保金額"]) : 0;
            decimal 勞保 = emp.Rows.Count > 0 ? Convert.ToDecimal(emp.Rows[0]["勞保金額"]) : 0;

            var stats = QueryAttendanceStats(empNo, prefix);
            int 出勤天數 = stats["出勤"];
            int 加班天數 = stats["加班"];
            int 特休天數 = stats["特休"];
            int 請假天數 = stats["事假"] + stats["病假"] + stats["公假"] + stats["喪假"] + stats["婚假"] + stats["產假"];
            int 曠職天數 = stats["曠職"];

            // ── 建立計薪項目明細 ──
            var items = new List<(string 薪資編號, string 計薪編號, string 計薪名稱, decimal 金額, string 加減, string 計稅別)>();

            // 本薪（或日薪×出勤、時薪×工時）
            decimal basePay = 本薪 > 0 ? 本薪 : 0;
            if (basePay <= 0 && 日薪 > 0) basePay = 日薪 * 出勤天數;
            if (basePay <= 0 && 時薪 > 0) basePay = 時薪 * (出勤天數 * 8);
            if (basePay > 0)
                items.Add((Key(empNo, prefix, "BASE"), "BASE", "本薪", basePay, "加", "應稅"));

            // 薪資設定項目
            var cfg = DbManager.QueryTable(
                "SELECT [計薪編號],[計薪名稱],[單位],[加減],[計稅別],[單位金額],[金額公式編號],[數量公式編號],[轉帳科目] " +
                "FROM [薪資設定] WHERE [員工編號]=$e ORDER BY [計薪編號]",
                DbManager.Param("$e", empNo));
            foreach (DataRow c in cfg.Rows)
            {
                var 編號 = Convert.ToString(c["計薪編號"]) ?? "ITEM";
                var 名稱 = Convert.ToString(c["計薪名稱"]) ?? 編號;
                var 單位 = Convert.ToString(c["單位"]);
                var 加減 = Convert.ToString(c["加減"]) ?? "加";
                var 計稅別 = Convert.ToString(c["計稅別"]) ?? "應稅";
                var 單位金額 = Convert.ToDecimal(c["單位金額"]);
                var qtyFormula = Convert.ToString(c["數量公式編號"]) ?? "";
                var qty = ResolveQuantity(qtyFormula, 出勤天數, 加班天數);
                if (qty <= 0 && 單位 == "件") qty = 1;
                var 金額 = 單位金額 * qty;
                if (金額 != 0)
                    items.Add((Key(empNo, prefix, 編號), 編號, 名稱, 金額, 加減, 計稅別));
            }

            // 健保／勞保自付額（扣項）
            if (健保 > 0) items.Add((Key(empNo, prefix, "NHI"), "NHI", "健保自付額", 健保, "減", "免稅"));
            if (勞保 > 0) items.Add((Key(empNo, prefix, "LBI"), "LBI", "勞保自付額", 勞保, "減", "免稅"));

            decimal 應領 = 0, 扣領 = 0, 稅項 = 0;
            foreach (var (_, _, _, 金額, 加減, 計稅別) in items)
            {
                if (加減 == "減") 扣領 += 金額;
                else 應領 += 金額;
                if (計稅別 == "應稅" && 加減 != "減") 稅項 += 金額;
            }
            decimal 實領 = 應領 - 扣領;
            decimal 給付 = 實領;

            // ── 寫入主檔與明細 ──
            DbManager.ExecuteTransaction(tx =>
            {
                DbManager.CreateCommand(tx,
                    "INSERT OR REPLACE INTO [薪資主檔] ([員工編號],[薪資年度],[薪資月份],[所屬部門],[出勤部門],[應領金額],[扣領金額],[實領金額],[給付金額],[稅項加總]) " +
                    "VALUES ($e,$y,$m,$od,$wd,$earn,$deduct,$net,$pay,$tax)",
                    DbManager.Param("$e", empNo), DbManager.Param("$y", year), DbManager.Param("$m", month),
                    DbManager.Param("$od", row["所屬部門"]), DbManager.Param("$wd", row["出勤部門"]),
                    DbManager.Param("$earn", 應領), DbManager.Param("$deduct", 扣領),
                    DbManager.Param("$net", 實領), DbManager.Param("$pay", 給付), DbManager.Param("$tax", 稅項))
                    .ExecuteNonQuery();

                DbManager.CreateCommand(tx, "DELETE FROM [薪資明細] WHERE [薪資編號] LIKE $p",
                    DbManager.Param("$p", $"{empNo}|{prefix}|%")).ExecuteNonQuery();
                foreach (var (薪資編號, 計薪編號, 計薪名稱, 金額, 加減, 計稅別) in items)
                {
                    DbManager.CreateCommand(tx,
                        "INSERT INTO [薪資明細] ([薪資編號],[計薪編號],[計薪名稱],[計薪別],[單位金額],[基本值],[單位別],[加減],[轉帳科目],[金額]) " +
                        "VALUES ($id,$no,$name,$tax,$unit,$base,$unittype,$addsub,$acct,$amt)",
                        DbManager.Param("$id", 薪資編號), DbManager.Param("$no", 計薪編號), DbManager.Param("$name", 計薪名稱),
                        DbManager.Param("$tax", 計稅別), DbManager.Param("$unit", 0m), DbManager.Param("$base", 0m),
                        DbManager.Param("$unittype", ""), DbManager.Param("$addsub", 加減),
                        DbManager.Param("$acct", (object?)null), DbManager.Param("$amt", 金額))
                        .ExecuteNonQuery();
                }
            });

            okCount++;
            sb.AppendLine($"{empNo}：出勤 {出勤天數} 天／加班 {加班天數} 天／請假 {請假天數} 天／曠職 {曠職天數} 天，應領 {應領:N0}、扣領 {扣領:N0}、實領 {實領:N0}");
        }

        return $"計算完成，共 {okCount} 位員工。\n" + sb;
    }

    private static Dictionary<string, int> QueryAttendanceStats(string empNo, string prefix)
    {
        var dt = DbManager.QueryTable(
            "SELECT [出缺類別], COUNT(*) AS [c] FROM [出缺明細] WHERE [出缺編號] LIKE $p GROUP BY [出缺類別]",
            DbManager.Param("$p", $"{empNo}|{prefix}|%"));
        var stats = new Dictionary<string, int>
        {
            ["出勤"] = 0, ["加班"] = 0, ["特休"] = 0,
            ["事假"] = 0, ["病假"] = 0, ["公假"] = 0, ["喪假"] = 0, ["婚假"] = 0, ["產假"] = 0, ["曠職"] = 0,
        };
        foreach (DataRow row in dt.Rows)
        {
            var kind = Convert.ToString(row["出缺類別"]) ?? "";
            if (stats.ContainsKey(kind))
                stats[kind] = Convert.ToInt32(row["c"]);
        }
        return stats;
    }

    private static decimal ResolveQuantity(string formula, int 出勤天數, int 加班天數)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 1;
        if (formula.Contains("出勤") || formula.Contains("天")) return 出勤天數;
        if (formula.Contains("加班")) return 加班天數;
        return 1;
    }

    private static string Key(string empNo, string prefix, string itemNo) => $"{empNo}|{prefix}|{itemNo}";
}
