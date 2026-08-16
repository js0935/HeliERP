// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Drawing.Printing;
using System.Windows.Forms;
using HeliERP.Data;

namespace HeliERP.App;

/// <summary>報表列印／預覽服務：把 RtmReportModel 以 PrintDocument 輸出到列印預覽視窗。</summary>
public static class ReportPrintService
{
    /// <summary>報表檔目錄（與資料庫同層的 Rep）。</summary>
    public static string RepDirectory =>
        Path.Combine(Path.GetDirectoryName(DbManager.DatabasePath) ?? AppContext.BaseDirectory, "Rep");

    /// <summary>從 .rtm 檔載入報表模型。</summary>
    public static RtmReportModel Load(string rtmFile)
    {
        var root = Tpf0Reader.Parse(File.ReadAllBytes(Path.Combine(RepDirectory, rtmFile)));
        return RtmLoader.Load(root);
    }

    /// <summary>開啟列印預覽（含列印按鈕）。紙張依報表定義。</summary>
    public static void Preview(RtmReportModel report, RtmData data)
    {
        var renderer = new RtmRenderer(report, data);
        var state = new RtmRenderState();

        using var doc = new PrintDocument();
        var ps = doc.DefaultPageSettings!;
        ps.PaperSize = new PaperSize("報表紙", To100thInch(report.MmPaperWidth), To100thInch(report.MmPaperHeight));
        ps.Margins = new Margins(0, 0, 0, 0);

        doc.PrintPage += (s, e) =>
        {
            e.Graphics!.Clear(Color.White);
            e.HasMorePages = renderer.RenderPage(e.Graphics, e.PageBounds, state);
        };

        using var dlg = new PrintPreviewDialog
        {
            Document = doc,
            Width = 1100,
            Height = 780,
            StartPosition = FormStartPosition.CenterScreen,
            UseAntiAlias = true,
        };
        dlg.ShowDialog();
    }

    /// <summary>1/1000 mm → 1/100 英吋（PaperSize 單位）。</summary>
    private static int To100thInch(float thousandthMm) =>
        Math.Max(1, (int)Math.Round(thousandthMm / 254.0));
}
