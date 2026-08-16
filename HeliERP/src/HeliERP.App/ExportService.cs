// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.1.0（原生 .xlsx 匯出）
// ════════════════════════════════════════════════════════
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace HeliERP.App;

/// <summary>匯出共用服務：CSV 與原生 .xlsx（OpenXML／Zip）雙格式匯出，不需第三方套件。</summary>
public static class ExportService
{
    /// <summary>以存檔對話框匯出 DataTable 為 CSV，回傳是否成功。</summary>
    public static bool ExportCsv(IWin32Window? owner, DataTable dt, string defaultName, string title, Func<string, bool>? columnFilter = null)
    {
        if (dt is null || dt.Rows.Count == 0)
        {
            MessageBox.Show(owner, "目前沒有可匯出的資料。", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        using var dlg = new SaveFileDialog
        {
            Title = title,
            FileName = defaultName,
            Filter = "CSV 檔案 (*.csv)|*.csv",
        };
        if (dlg.ShowDialog(owner) != DialogResult.OK)
            return false;
        try
        {
            WriteDataTable(dt, dlg.FileName, columnFilter);
            MessageBox.Show(owner, $"已匯出 {dt.Rows.Count} 筆到：\n{dlg.FileName}", title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"匯出失敗：{ex.Message}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>以存檔對話框匯出 DataTable 為原生 .xlsx，回傳是否成功。</summary>
    public static bool ExportXlsx(IWin32Window? owner, DataTable dt, string defaultName, string title, Func<string, bool>? columnFilter = null)
    {
        if (dt is null || dt.Rows.Count == 0)
        {
            MessageBox.Show(owner, "目前沒有可匯出的資料。", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        using var dlg = new SaveFileDialog
        {
            Title = title,
            FileName = defaultName,
            Filter = "Excel 活頁簿 (*.xlsx)|*.xlsx",
        };
        if (dlg.ShowDialog(owner) != DialogResult.OK)
            return false;
        try
        {
            WriteXlsx(dt, dlg.FileName, columnFilter);
            MessageBox.Show(owner, $"已匯出 {dt.Rows.Count} 筆到：\n{dlg.FileName}", title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"匯出失敗：{ex.Message}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>以存檔對話框匯出 DataTable，由使用者於存檔時選擇 .xlsx 或 .csv。</summary>
    public static bool ExportAny(IWin32Window? owner, DataTable dt, string defaultName, string title, Func<string, bool>? columnFilter = null)
    {
        if (dt is null || dt.Rows.Count == 0)
        {
            MessageBox.Show(owner, "目前沒有可匯出的資料。", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        using var dlg = new SaveFileDialog
        {
            Title = title,
            FileName = defaultName,
            Filter = "Excel 活頁簿 (*.xlsx)|*.xlsx|CSV 檔案 (*.csv)|*.csv",
        };
        if (dlg.ShowDialog(owner) != DialogResult.OK)
            return false;
        try
        {
            bool isXlsx = string.Equals(Path.GetExtension(dlg.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase);
            if (isXlsx)
                WriteXlsx(dt, dlg.FileName, columnFilter);
            else
                WriteDataTable(dt, dlg.FileName, columnFilter);
            MessageBox.Show(owner, $"已匯出 {dt.Rows.Count} 筆到：\n{dlg.FileName}", title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"匯出失敗：{ex.Message}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>以存檔對話框匯出 DataGridView 可見欄位（顯示值）為 CSV，回傳是否成功。</summary>
    public static bool ExportGrid(IWin32Window? owner, DataGridView grid, string defaultName, string title)
    {
        if (grid is null || grid.Rows.Count == 0)
        {
            MessageBox.Show(owner, "目前沒有可匯出的資料。", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        using var dlg = new SaveFileDialog
        {
            Title = title,
            FileName = defaultName,
            Filter = "CSV 檔案 (*.csv)|*.csv",
        };
        if (dlg.ShowDialog(owner) != DialogResult.OK)
            return false;
        try
        {
            var cols = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).ToList();
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", cols.Select(c => QuoteCsv(c.HeaderText))));
            int rows = 0;
            foreach (DataGridViewRow r in grid.Rows)
            {
                if (r.IsNewRow) continue;
                sb.AppendLine(string.Join(",", cols.Select(c => QuoteCsv(Convert.ToString(r.Cells[c.Index].FormattedValue) ?? ""))));
                rows++;
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            MessageBox.Show(owner, $"已匯出 {rows} 筆到：\n{dlg.FileName}", title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"匯出失敗：{ex.Message}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>將 DataTable 寫成 CSV（可排除指定欄位）。</summary>
    public static void WriteDataTable(DataTable dt, string path, Func<string, bool>? columnFilter = null)
    {
        var cols = dt.Columns.Cast<DataColumn>()
            .Where(c => columnFilter is null || columnFilter(c.ColumnName))
            .ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(c => QuoteCsv(c.ColumnName))));
        foreach (DataRow r in dt.Rows)
            sb.AppendLine(string.Join(",", cols.Select(c => QuoteCsv(r[c].ToString() ?? ""))));
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>CSV 欄位跳脫（含逗號／雙引號／換行時加引號）。</summary>
    public static string QuoteCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    // ==================== 原生 .xlsx（OpenXML Zip） ====================

    /// <summary>將 DataTable 寫成 .xlsx（SpreadsheetML 2007，Zip 封裝，不需第三方套件）。</summary>
    public static void WriteXlsx(DataTable dt, string path, Func<string, bool>? columnFilter = null)
    {
        var cols = dt.Columns.Cast<DataColumn>()
            .Where(c => columnFilter is null || columnFilter(c.ColumnName))
            .ToList();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        AddEntry(zip, "[Content_Types].xml", ContentTypesXml());
        AddEntry(zip, "_rels/.rels", RelsRootXml());
        AddEntry(zip, "xl/workbook.xml", WorkbookXml());
        AddEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
        AddEntry(zip, "xl/styles.xml", StylesXml());
        AddEntry(zip, "xl/worksheets/sheet1.xml", SheetXml(cols, dt));
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var sw = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        sw.Write(content);
    }

    private static string ContentTypesXml() => """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
      <Default Extension="xml" ContentType="application/xml"/>
      <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
      <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
      <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
    </Types>
""";

    private static string RelsRootXml() => """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
    </Relationships>
""";

    private static string WorkbookXml() => """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
      <sheets><sheet name="資料" sheetId="1" r:id="rId1"/></sheets>
    </workbook>
""";

    private static string WorkbookRelsXml() => """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
      <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
    </Relationships>
""";

    private static string StylesXml() => """
    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
      <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
      <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
      <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
      <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
      <cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>
      <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
    </styleSheet>
""";

    private static string SheetXml(List<DataColumn> cols, DataTable dt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        sb.AppendLine("<sheetData>");
        // 表頭列
        sb.AppendLine("<row r=\"1\">");
        for (int i = 0; i < cols.Count; i++)
            sb.Append($"<c r=\"{ColumnRef(i + 1)}1\" t=\"inlineStr\"><is><t>{XmlEscape(cols[i].ColumnName)}</t></is></c>");
        sb.AppendLine();
        sb.AppendLine("</row>");

        // 資料列
        int row = 2;
        foreach (DataRow r in dt.Rows)
        {
            sb.Append($"<row r=\"{row}\">");
            for (int i = 0; i < cols.Count; i++)
            {
                var v = r[cols[i]];
                if (v is null || v is DBNull) continue;
                var cell = ColumnRef(i + 1) + row;
                if (IsNumeric(cols[i].DataType, v))
                {
                    sb.Append($"<c r=\"{cell}\"><v>{ToNumber(v)}</v></c>");
                }
                else
                {
                    sb.Append($"<c r=\"{cell}\" t=\"inlineStr\"><is><t>{XmlEscape(Convert.ToString(v, CultureInfo.InvariantCulture))}</t></is></c>");
                }
            }
            sb.AppendLine("</row>");
            row++;
        }
        sb.AppendLine("</sheetData>");
        sb.AppendLine("</worksheet>");
        return sb.ToString();
    }

    private static bool IsNumeric(Type type, object value)
        => type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    private static string ToNumber(object v) =>
        Convert.ToString(v, CultureInfo.InvariantCulture) ?? "0";

    private static string ColumnRef(int index)
    {
        string s = "";
        while (index > 0)
        {
            int m = (index - 1) % 26;
            s = (char)('A' + m) + s;
            index = (index - 1) / 26;
        }
        return s;
    }

    private static string XmlEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
