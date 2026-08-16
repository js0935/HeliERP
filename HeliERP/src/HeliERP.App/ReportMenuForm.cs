// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using HeliERP.Data;
using HeliERP.Models;

namespace HeliERP.App;

/// <summary>報表列印選單：依分類列出可列印報表，點選即產生資料並開啟列印預覽。</summary>
public sealed class ReportMenuForm : Form
{
    public ReportMenuForm()
    {
        UiTheme.Apply(this);
        Text = "報表列印";
        Size = new Size(820, 720);
        MinimumSize = new Size(640, 480);
        BackColor = UiTheme.Background;
        StartPosition = FormStartPosition.CenterScreen;

        var root = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
        root.Controls.Add(UiTheme.BuildHeader("報表列印", "選擇報表進行預覽／列印，紙張與欄位依報表定義"));

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(30, 12, 30, 20),
        };

        AddSection(flow, "業務");
        AddReport(flow, "業務應收統計表", () => ARService.BuildBizSummaryReportData(), "業務應收統計表.rtm");
        AddReport(flow, "業務應收明細表", () => ARService.BuildBizDetailReportData(), "業務應收明細表.rtm");

        AddSection(flow, "應收帳款（客戶）");
        AddReport(flow, "應收帳款統計表", () => ARService.BuildSummaryReportData(ARService.應收類別), "應收帳款統計表.rtm");
        AddReport(flow, "應收帳款帳齡分析", () => ARService.BuildAgingReportData(ARService.應收類別), "應收帳款帳齡分析.rtm");
        AddReport(flow, "應收帳款明細表（選對象）", () => PickObjectData(ARService.應收類別, ARService.BuildDetailReportData), "應收帳款明細表.rtm");
        AddReport(flow, "應收帳款明細表(含折扣)（選對象）", () => PickObjectData(ARService.應收類別, ARService.BuildDetailReportData), "應收帳款明細表(含折扣).rtm");
        AddReport(flow, "應收帳款簡要表（選對象）", () => PickObjectData(ARService.應收類別, obj => ARService.BuildBriefReportData(obj, ARService.應收類別)), "應收帳款簡要表.rtm");

        AddSection(flow, "應付帳款（廠商）");
        AddReport(flow, "應付帳款統計表", () => ARService.BuildSummaryReportData(ARService.應付類別), "應付帳款統計表.rtm");
        AddReport(flow, "應付帳款明細表（選對象）", () => PickObjectData(ARService.應付類別, ARService.BuildDetailReportData), "應付帳款明細表.rtm");
        AddReport(flow, "應付帳款明細表(含折扣)（選對象）", () => PickObjectData(ARService.應付類別, ARService.BuildDetailReportData), "應付帳款明細表(含折扣).rtm");
        AddReport(flow, "應付帳款簡要表（選對象）", () => PickObjectData(ARService.應付類別, obj => ARService.BuildBriefReportData(obj, ARService.應付類別)), "應付帳款簡要表.rtm");

        AddSection(flow, "票據（應收）");
        AddReport(flow, "應收票據明細表（收票日）", () => BillService.BuildBillDetailReportData(BillService.收票類別, "收票日"), "應收票據明細表(收票日).rtm");
        AddReport(flow, "應收票據明細表（託收銀行）", () => BillService.BuildBillDetailReportData(BillService.收票類別, "託收銀行"), "應收票據明細表(託收銀行).rtm");
        AddReport(flow, "未兌現應收票據", () => BillService.BuildUnclearedBillData(BillService.收票類別), "未兌現應收票據.rtm");

        AddSection(flow, "票據（應付）");
        AddReport(flow, "應付票據明細表（開票日）", () => BillService.BuildBillDetailReportData(BillService.付票類別, "開票日"), "應付票據明細表(開票日).rtm");
        AddReport(flow, "應付票據明細表（開票銀行）", () => BillService.BuildBillDetailReportData(BillService.付票類別, "開票銀行"), "應付票據明細表(開票銀行).rtm");
        AddReport(flow, "未兌現應付票據", () => BillService.BuildUnclearedBillData(BillService.付票類別), "未兌現應付票據.rtm");

        AddSection(flow, "沖銷日報表");
        AddReport(flow, "收款沖銷日報表", () => WriteOffService.BuildWriteOffReportData(WriteOffService.收款類別), "收款沖銷日報表.rtm");
        AddReport(flow, "付款沖銷日報表", () => WriteOffService.BuildWriteOffReportData(WriteOffService.付款類別), "付款沖銷日報表.rtm");

        AddSection(flow, "票據列印");
        AddReport(flow, "支票列印", BuildCheckPrintData, "支票列印.rtm");
        AddReport(flow, "票據簽收回條", BuildBillReceiptData, "票據簽收回條.rtm");
        AddReport(flow, "票貼剩餘額度表", BuildBillDiscountBalanceData, "票貼剩餘額度表.rtm");

        AddSection(flow, "會計");
        AddReport(flow, "會計傳票", AccountingService.BuildVoucherReportData, "會計傳票.rtm");
        AddReport(flow, "總分類帳明細表", AccountingService.BuildLedgerDetailReportData, "總分類帳明細表.rtm");
        AddReport(flow, "總分類帳簡要表", AccountingService.BuildLedgerBriefReportData, "總分類帳簡要表.rtm");
        AddReport(flow, "明細分類帳", AccountingService.BuildDetailLedgerReportData, "明細分類帳.rtm");
        AddReport(flow, "日記帳（含現）", AccountingService.BuildJournalReportData, "日記帳(含現).rtm");
        AddReport(flow, "日記帳（不含現）", AccountingService.BuildJournalNoCashReportData, "日記帳(不含現).rtm");
        AddReport(flow, "現金帳", AccountingService.BuildCashBookReportData, "現金帳.rtm");
        AddReport(flow, "試算表", AccountingService.BuildTrialBalanceReportData, "試算表.rtm");
        AddReport(flow, "期間試算表", AccountingService.BuildPeriodTrialBalanceReportData, "期間試算表.rtm");
        AddReport(flow, "損益表", AccountingService.BuildIncomeStatementReportData, "損益表.rtm");
        AddReport(flow, "報告式資產負債表", AccountingService.BuildBalanceSheetReportData, "報告式資產負債表.rtm");
        AddReport(flow, "帳戶式資產負債表", AccountingService.BuildAccountBalanceSheetReportData, "帳戶式資產負債表.rtm");

        AddSection(flow, "庫存");
        AddReport(flow, "現有庫存明細表", BuildStockReport, "現有庫存明細表.rtm");
        AddReport(flow, "各倉庫存明細表", BuildWarehouseStockReport, "各倉庫存明細表.rtm");
        AddReport(flow, "歷史庫存明細表", BuildHistoryStockReport, "歷史庫存明細表.rtm");
        AddReport(flow, "庫存呆滯報表", BuildDullStockReport, "庫存呆滯報表.rtm");
        AddReport(flow, "庫存盤點明細表", BuildAdjustmentDetailReport, "庫存盤點明細表.rtm");
        AddReport(flow, "貨品盤點明細表", BuildAdjustmentDetailReport, "貨品盤點明細表.rtm");
        AddReport(flow, "盤點單據", BuildAdjustmentBillReport, "盤點單據.rtm");
        AddReport(flow, "貨品存貨異動明細表", BuildMovementReport, "貨品存貨異動明細表.rtm");
        AddReport(flow, "庫存調整明細表", BuildAdjustmentReport, "庫存調整明細表.rtm");
        AddReport(flow, "類別庫存明細表", BuildCategoryStockReport, "類別庫存明細表.rtm");
        AddReport(flow, "貨品低於安存表", BuildGoodsBelowSafetyData, "貨品低於安存表.rtm");
        AddReport(flow, "倉庫低於安存表", BuildWarehouseBelowSafetyData, "倉庫低於安存表.rtm");
        AddReport(flow, "入出庫明細表", BuildStockIoDetailData, "入出庫明細表.rtm");
        AddReport(flow, "貨品入出庫明細表", BuildGoodsStockIoData, "貨品入出庫明細表.rtm");
        AddReport(flow, "貨品調整明細表", BuildGoodsAdjustmentData, "貨品調整明細表.rtm");
        AddReport(flow, "出貨利潤明細表", BuildShipProfitData, "出貨利潤明細表.rtm");
        AddReport(flow, "貨品利潤明細表", BuildGoodsProfitData, "貨品利潤明細表.rtm");

        AddSection(flow, "基本資料");
        AddReport(flow, "客戶資料", BuildCustomerData, "客戶資料.rtm");
        AddReport(flow, "廠商資料", BuildVendorData, "廠商資料.rtm");
        AddReport(flow, "員工資料", BuildEmployeeData, "員工資料.rtm");
        AddReport(flow, "貨品報表", BuildGoodsReport, "貨品報表.rtm");
        AddReport(flow, "會計科目", BuildAccountSubjectReport, "會計科目.rtm");
        AddReport(flow, "財產基本資料", BuildPropertyReport, "財產基本資料.rtm");

        AddSection(flow, "交易明細");
        AddReport(flow, "出貨明細表", () => BuildTxDetailData("出貨", ARService.應收類別), "出貨明細表.rtm");
        AddReport(flow, "進貨明細表", () => BuildTxDetailData("進貨", ARService.應付類別), "進貨明細表.rtm");
        AddReport(flow, "客戶交易明細表", BuildCustomerTxReportData, "客戶交易明細表.rtm");
        AddReport(flow, "廠商交易明細表", BuildVendorTxReportData, "廠商交易明細表.rtm");

        AddSection(flow, "分析統計");
        AddReport(flow, "客戶交易排行", MissingReportService.Build客戶交易排行, "客戶交易排行.rtm");
        AddReport(flow, "客戶交易類別", MissingReportService.Build客戶交易類別, "客戶交易類別.rtm");
        AddReport(flow, "客戶別報價明細", MissingReportService.Build客戶別報價明細, "客戶別報價明細.rtm");
        AddReport(flow, "客戶歷次售價", MissingReportService.Build客戶歷次售價, "客戶歷次售價.rtm");
        AddReport(flow, "廠商交易排行", MissingReportService.Build廠商交易排行, "廠商交易排行.rtm");
        AddReport(flow, "廠商歷次售價", MissingReportService.Build廠商歷次售價, "廠商歷次售價.rtm");
        AddReport(flow, "業務銷售排行", MissingReportService.Build業務銷售排行, "業務銷售排行.rtm");
        AddReport(flow, "業務銷售明細表", MissingReportService.Build業務銷售明細表, "業務銷售明細表.rtm");
        AddReport(flow, "業務利潤分析表", MissingReportService.Build業務利潤分析表, "業務利潤分析表.rtm");
        AddReport(flow, "貨品交易排行", MissingReportService.Build貨品交易排行, "貨品交易排行.rtm");
        AddReport(flow, "貨品交易明細表", MissingReportService.Build貨品交易明細表, "貨品交易明細表.rtm");
        AddReport(flow, "貨品類別排行", MissingReportService.Build貨品類別排行, "貨品類別排行.rtm");
        AddReport(flow, "貨品別報價明細", MissingReportService.Build貨品別報價明細, "貨品別報價明細.rtm");

        AddSection(flow, "採購訂貨");
        AddReport(flow, "報價單據", () => BuildPoBillData("報價"), "報價單據.rtm");
        AddReport(flow, "訂貨單據", () => BuildPoBillData("訂貨"), "訂貨單據.rtm");
        AddReport(flow, "採購單據", () => BuildPoBillData("採購"), "採購單據.rtm");
        AddReport(flow, "詢價單據", () => BuildPoBillData("詢價"), "詢價單據.rtm");
        AddReport(flow, "已訂未交反應表", () => BuildPoReactionData("訂貨", "交易數量"), "已訂未交反應表.rtm");
        AddReport(flow, "訂貨已交反應表", () => BuildPoReactionData("訂貨", "已交數量"), "訂貨已交反應表.rtm");
        AddReport(flow, "已購未進反應表", () => BuildPoReactionData("採購", "交易數量"), "已購未進反應表.rtm");

        AddSection(flow, "出退貨");
        AddReport(flow, "出退貨明細表", BuildShipReturnDetailData, "出退貨明細表.rtm");
        AddReport(flow, "出退貨簡要表", BuildShipReturnBriefData, "出退貨簡要表.rtm");
        AddReport(flow, "出貨退回明細表", BuildShipReturnDetailReport, "出貨退回明細表.rtm");

        AddSection(flow, "借出借入");
        AddReport(flow, "借出單據", () => PickTradeBill(MissingReportService.借出, "借出單據"), "借出單據.rtm");
        AddReport(flow, "借出還入單", () => PickTradeBill(MissingReportService.借出還入, "借出還入單"), "借出還入單.rtm");
        AddReport(flow, "借入單據", () => PickTradeBill(MissingReportService.借入, "借入單據"), "借入單據.rtm");
        AddReport(flow, "借入還出單", () => PickTradeBill(MissingReportService.借入還出, "借入還出單"), "借入還出單.rtm");
        AddReport(flow, "借出明細表", MissingReportService.Build借出明細表, "借出明細表.rtm");
        AddReport(flow, "借出還入明細表", MissingReportService.Build借出還入明細表, "借出還入明細表.rtm");
        AddReport(flow, "借入還出明細表", MissingReportService.Build借入還出明細表, "借入還出明細表.rtm");
        AddReport(flow, "客戶借出明細表", MissingReportService.Build客戶借出明細表, "客戶借出明細表.rtm");
        AddReport(flow, "客戶借出還入明細表", MissingReportService.Build客戶借出還入明細表, "客戶借出還入明細表.rtm");
        AddReport(flow, "貨品借出明細表", MissingReportService.Build貨品借出明細表, "貨品借出明細表.rtm");
        AddReport(flow, "貨品借出還入明細表", MissingReportService.Build貨品借出還入明細表, "貨品借出還入明細表.rtm");
        AddReport(flow, "貨品借入還出明細表", MissingReportService.Build貨品借入還出明細表, "貨品借入還出明細表.rtm");
        AddReport(flow, "廠商借入還出明細表", MissingReportService.Build廠商借入還出明細表, "廠商借入還出明細表.rtm");

        AddSection(flow, "託售託工");
        AddReport(flow, "託售單據", () => PickTradeBill(MissingReportService.託售, "託售單據"), "託售單據.rtm");
        AddReport(flow, "託售回貨單", () => PickTradeBill(MissingReportService.託售回貨, "託售回貨單"), "託售回貨單.rtm");
        AddReport(flow, "託工出庫", () => PickTradeBill(MissingReportService.託工出庫, "託工出庫"), "託工出庫.rtm");
        AddReport(flow, "託工入庫", () => PickTradeBill(MissingReportService.託工入庫, "託工入庫"), "託工入庫.rtm");
        AddReport(flow, "託售回貨明細表", MissingReportService.Build託售回貨明細表, "託售回貨明細表.rtm");
        AddReport(flow, "客戶託售回貨明細表", MissingReportService.Build客戶託售回貨明細表, "客戶託售回貨明細表.rtm");
        AddReport(flow, "貨品託售明細表", MissingReportService.Build貨品託售明細表, "貨品託售明細表.rtm");

        AddSection(flow, "調撥領料");
        AddReport(flow, "調撥單據", () => PickTradeBill(MissingReportService.調撥, "調撥單據"), "調撥單據.rtm");
        AddReport(flow, "領料單據", () => PickTradeBill(MissingReportService.領料, "領料單據"), "領料單據.rtm");
        AddReport(flow, "倉庫調撥明細表", MissingReportService.Build倉庫調撥明細表, "倉庫調撥明細表.rtm");
        AddReport(flow, "貨品調撥明細表", MissingReportService.Build貨品調撥明細表, "貨品調撥明細表.rtm");

        AddSection(flow, "進退貨");
        AddReport(flow, "進貨退出明細表", MissingReportService.Build進貨退出明細表, "進貨退出明細表.rtm");
        AddReport(flow, "進退貨簡要表", MissingReportService.Build進退貨簡要表, "進退貨簡要表.rtm");
        AddReport(flow, "貨品進貨及退出明細表", MissingReportService.Build貨品進貨及退出明細表, "貨品進貨及退出明細表.rtm");
        AddReport(flow, "廠商入出庫明細表", MissingReportService.Build廠商入出庫明細表, "廠商入出庫明細表.rtm");

        AddSection(flow, "折讓");
        AddReport(flow, "出貨折讓單", () => PickDiscountBill(MissingReportService.出貨折讓, "出貨折讓單"), "出貨折讓單.rtm");
        AddReport(flow, "進貨折讓單", () => PickDiscountBill(MissingReportService.進貨折讓, "進貨折讓單"), "進貨折讓單.rtm");
        AddReport(flow, "出貨折讓明細表", MissingReportService.Build出貨折讓明細表, "出貨折讓明細表.rtm");
        AddReport(flow, "進貨折讓明細表", MissingReportService.Build進貨折讓明細表, "進貨折讓明細表.rtm");
        AddReport(flow, "客戶折讓明細表", MissingReportService.Build客戶折讓明細表, "客戶折讓明細表.rtm");
        AddReport(flow, "廠商折讓明細表", MissingReportService.Build廠商折讓明細表, "廠商折讓明細表.rtm");
        AddReport(flow, "採購折讓明細表", MissingReportService.Build採購折讓明細表, "採購折讓明細表.rtm");
        AddReport(flow, "業務折讓明細表", MissingReportService.Build業務折讓明細表, "業務折讓明細表.rtm");

        AddSection(flow, "專案");
        AddReport(flow, "專案出退貨明細表", MissingReportService.Build專案出退貨明細表, "專案出退貨明細表.rtm");
        AddReport(flow, "專案進退貨明細表", MissingReportService.Build專案進退貨明細表, "專案進退貨明細表.rtm");
        AddReport(flow, "專案收款沖銷日報表", MissingReportService.Build專案收款沖銷日報表, "專案收款沖銷日報表.rtm");

        AddSection(flow, "銀行");
        AddReport(flow, "銀行存款對帳單", MissingReportService.Build銀行存款對帳單, "銀行存款對帳單.rtm");
        AddReport(flow, "銀行資金預估明細表", MissingReportService.Build銀行資金預估明細表, "銀行資金預估明細表.rtm");

        AddSection(flow, "郵寄標籤");
        AddReport(flow, "客戶標籤", BuildCustomerLabel, "客戶標籤.rtm");
        AddReport(flow, "廠商標籤", BuildVendorLabel, "廠商標籤.rtm");
        AddReport(flow, "標準信封", BuildEnvelope, "標準信封.rtm");

        AddSection(flow, "折舊");
        AddReport(flow, "日期別折舊表", () => BuildDepreciationData("日期"), "日期別折舊表.rtm");
        AddReport(flow, "科目別折舊表", () => BuildDepreciationData("科目"), "科目別折舊表.rtm");
        AddReport(flow, "財產別折舊表", () => BuildDepreciationData("財產"), "財產別折舊表.rtm");
        AddReport(flow, "應收帳款郵寄標籤", BuildARLabel, "應收帳款郵寄標籤.rtm");
        AddReport(flow, "應收帳款標準信封", BuildAREnvelope, "應收帳款標準信封.rtm");

        root.Controls.Add(flow);
        Controls.Add(root);
        UiTheme.ScaleForDpi(this);

        UiTheme.ClampToScreen(this);
    }

    private static void AddSection(FlowLayoutPanel flow, string title)
    {
        flow.Controls.Add(new Label
        {
            Text = title,
            Font = UiTheme.Font(12.5F, FontStyle.Bold),
            ForeColor = UiTheme.AccentDark,
            AutoSize = true,
            Margin = new Padding(2, 16, 0, 6),
        });
    }

    private void AddReport(FlowLayoutPanel flow, string label, Func<RtmData?> build, string rtmFile)
    {
        var btn = new ModernButton
        {
            Text = label,
            IsPrimary = false,
            Font = UiTheme.Font(10.5F),
            Size = new Size(720, 42),
            Margin = new Padding(2, 3, 2, 3),
            CornerRadius = 7,
        };
        btn.Click += (s, e) =>
        {
            try
            {
                var data = build();
                if (data is null || data.Detail.Count == 0)
                {
                    MessageBox.Show(this, "查無可列印資料（無對象或明細 0 筆）。", "報表列印",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                PreviewReport(rtmFile, data);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"{label}：{ex.Message}", "報表列印",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        flow.Controls.Add(btn);
    }

    private static void PreviewReport(string rtmFile, RtmData data)
    {
        var path = Path.Combine(ReportPrintService.RepDirectory, rtmFile);
        if (!File.Exists(path))
        {
            MessageBox.Show($"找不到報表檔：{path}", "報表列印", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ReportPrintService.Preview(ReportPrintService.Load(rtmFile), data);
    }

    // ==================== 庫存報表 ====================

    /// <summary>現有庫存明細表：全部庫存資料（每列一個貨品倉庫）。</summary>
    private RtmData? BuildStockReport()
    {
        var dt = InventoryService.LoadStock();
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = "全部";
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>庫存調整明細表：全部庫存調整明細（每列一筆調整）。</summary>
    private RtmData? BuildAdjustmentReport()
    {
        var dt = InventoryService.LoadAdjustmentDetails();
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>各倉庫存明細表：全部庫存現量（同現有庫存明細表資料，欄位視報表定義）。</summary>
    private RtmData? BuildWarehouseStockReport() => BuildStockData(InventoryService.LoadStock(), "全部");

    /// <summary>歷史庫存明細表：庫存現量（標示日期區間）。</summary>
    private RtmData? BuildHistoryStockReport()
    {
        var data = BuildStockData(InventoryService.LoadStock(), "全部");
        if (data is null) return null;
        data.Master["日期區間"] = "全部日期";
        return data;
    }

    /// <summary>庫存呆滯報表：庫存現量 + 呆滯天數（由最近出貨日推算，無出貨記錄視為 9999 天）。</summary>
    private RtmData? BuildDullStockReport()
    {
        var dt = InventoryService.LoadStock();
        if (dt.Rows.Count == 0) return null;
        dt.Columns.Add("呆滯天數", typeof(int));
        foreach (DataRow r in dt.Rows)
        {
            int days = 9999;
            if (DateTime.TryParse(Convert.ToString(r["最近出貨日"]), out var 出貨日))
                days = Math.Max(0, (int)(DateTime.Today - 出貨日).TotalDays);
            r["呆滯天數"] = days;
        }
        return BuildStockData(dt, "全部");
    }

    /// <summary>庫存盤點明細表／貨品盤點明細表：庫存調整明細（含單價／折扣／金額）。</summary>
    private RtmData? BuildAdjustmentDetailReport()
    {
        var dt = InventoryService.LoadAdjustmentDetails();
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>盤點單據：挑選一張庫存調整單，組主檔＋明細資料。</summary>
    private RtmData? BuildAdjustmentBillReport()
    {
        var list = DbManager.QueryTable(
            "SELECT [單據副碼], [交易單號], [交易日期] FROM [交易主檔] WHERE [單據類別] = '庫存調整' " +
            "ORDER BY [交易日期], [交易單號]");
        if (list.Rows.Count == 0) return null;
        var 副碼 = list.Rows.Count == 1 ? list.Rows[0]["單據副碼"] : PickAdjustmentBill(list);
        if (副碼 is null) return null;

        var dt = DbManager.QueryTable("SELECT * FROM [交易主檔] WHERE [單據副碼] = $c",
            DbManager.Param("$c", 副碼));
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData();
        var row = dt.Rows[0];
        foreach (DataColumn col in dt.Columns) data.Master[col.ColumnName] = row[col];
        data.Master["員工名稱"] = Convert.ToString(data.Master["製單"]);
        FillCompany(data);

        var detailDt = DbManager.QueryTable(
            "SELECT * FROM [交易明細] WHERE [單據副碼] = $c ORDER BY [建檔序號]",
            DbManager.Param("$c", 副碼));
        foreach (DataRow dr in detailDt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in detailDt.Columns) d[col.ColumnName] = dr[col];
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>貨品存貨異動明細表：全部貨品異動（含公司簡稱與累計）。</summary>
    private RtmData? BuildMovementReport()
    {
        var dt = InventoryService.LoadMovements();
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>類別庫存明細表：類別 × 倉庫彙總。</summary>
    private RtmData? BuildCategoryStockReport()
    {
        var dt = InventoryService.LoadCategorySummary();
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = "全部";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>列表式報表共用：填公司資料、編號區間與明細。</summary>
    private RtmData? BuildStockData(DataTable dt, string scope)
    {
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = scope;
        FillDetail(data, dt);
        return data;
    }

    private static void FillDetail(RtmData data, DataTable dt)
    {
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            foreach (DataColumn col in dt.Columns) d[col.ColumnName] = r[col];
            data.Detail.Add(d);
        }
    }

    /// <summary>共用挑選視窗：列出 items 供下拉選擇，取消或無項回傳 null。</summary>
    private int? PickRowDialog(string title, string hint, IList<string> items)
    {
        if (items.Count == 0) return null;
        using var dlg = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(360, 152),
            BackColor = UiTheme.Background,
        };
        dlg.Controls.Add(new Label
        {
            Text = hint,
            Font = UiTheme.Font(10F),
            Location = new Point(16, 14),
            AutoSize = true,
        });
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(16, 42),
            Width = 328,
            Font = UiTheme.Font(10F),
        };
        combo.Items.AddRange(items.ToArray());
        combo.SelectedIndex = 0;
        dlg.Controls.Add(combo);
        var ok = new ModernButton { Text = "確定", DialogResult = DialogResult.OK, IsPrimary = true, Location = new Point(146, 92), Size = new Size(92, 38), CornerRadius = 6 };
        var cancel = new ModernButton { Text = "取消", DialogResult = DialogResult.Cancel, IsPrimary = false, Location = new Point(246, 92), Size = new Size(92, 38), CornerRadius = 6 };
        dlg.Controls.Add(ok);
        dlg.Controls.Add(cancel);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        UiTheme.ScaleForDpi(dlg);
        UiTheme.ClampToScreen(dlg);

        if (dlg.ShowDialog(this) != DialogResult.OK || combo.SelectedIndex < 0)
            return null;
        return combo.SelectedIndex;
    }

    /// <summary>挑選庫存調整單（多筆時開選擇視窗），取消時回傳 null。</summary>
    private object? PickAdjustmentBill(DataTable list)
    {
        var items = list.Rows.Cast<DataRow>().Select(r =>
        {
            var no = Convert.ToString(r["交易單號"]) ?? "";
            var date = Convert.ToString(r["交易日期"]) ?? "";
            return date.Length > 0 ? $"{no}　{date}" : no;
        }).ToList();
        var idx = PickRowDialog("選擇盤點單據", "請選擇庫存調整單：", items);
        return idx is null ? null : list.Rows[idx.Value]["單據副碼"];
    }

    /// <summary>挑選交易單據（借出/借入/託售/託工/調撥/領料），取消或無單時回傳 null。</summary>
    private RtmData? PickTradeBill(string 單據類別, string label)
    {
        var key = PickBillRow(MissingReportService.LoadBillList(單據類別),
            $"選擇{label}", $"請選擇{label}：", "交易單號", "交易日期");
        return key is null ? null : MissingReportService.BuildBill(單據類別, key.Value);
    }

    /// <summary>挑選折讓單（出貨/進貨折讓單），取消或無單時回傳 null。</summary>
    private RtmData? PickDiscountBill(string 折讓類別, string label)
    {
        var key = PickBillRow(MissingReportService.LoadDiscountList(折讓類別),
            $"選擇{label}", $"請選擇{label}：", "折讓單號", "折讓日期");
        return key is null ? null : MissingReportService.BuildDiscountBill(折讓類別, key.Value);
    }

    /// <summary>通用單據挑選視窗，回傳所選單據副碼，取消時回傳 null。</summary>
    private long? PickBillRow(DataTable list, string title, string hint, string 單號欄位, string 日期欄位)
    {
        if (list.Rows.Count == 0) return null;
        var items = list.Rows.Cast<DataRow>().Select(r =>
        {
            var no = Convert.ToString(r[單號欄位]) ?? "";
            var date = Convert.ToString(r[日期欄位]) ?? "";
            return date.Length > 0 ? $"{no}　{date}" : no;
        }).ToList();
        var idx = PickRowDialog(title, hint, items);
        return idx is null ? null : Convert.ToInt64(list.Rows[idx.Value]["單據副碼"]);
    }

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

    // ==================== 基本資料列印 ====================

    /// <summary>客戶資料：客廠類別＝客戶的全部客戶。</summary>
    private RtmData? BuildCustomerData() => BuildObjectData(ARService.應收類別, "全部客戶");

    /// <summary>廠商資料：客廠類別＝廠商的全部廠商。</summary>
    private RtmData? BuildVendorData() => BuildObjectData(ARService.應付類別, "全部廠商");

    private RtmData? BuildObjectData(string 客廠類別, string scope)
    {
        var dt = DbManager.QueryTable(
            "SELECT [客廠編號], [公司全名], [聯絡電話一], [聯絡人一], [傳真號碼], [送貨地址], [送貨地郵遞區號] " +
            "FROM [客戶廠商] WHERE [客廠類別] = $t ORDER BY [客廠編號]",
            DbManager.Param("$t", 客廠類別));
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = scope;
        FillDetail(data, dt);
        return data;
    }

    /// <summary>員工資料：全部員工。</summary>
    private RtmData? BuildEmployeeData()
    {
        var dt = DbManager.QueryTable(
            "SELECT [員工編號], [員工姓名], [聯絡電話], [聯絡人], [出生日期], [聯絡地址], [性別], [血型], [到職日期] " +
            "FROM [員工資料] ORDER BY [員工編號]");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = "全部員工";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>貨品報表：全部貨品。</summary>
    private RtmData? BuildGoodsReport()
    {
        var dt = DbManager.QueryTable(
            "SELECT [貨品編號], [品名], [基本單位], [標準售價], [標準成本] " +
            "FROM [貨品主檔] ORDER BY [貨品編號]");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = "全部貨品";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>會計科目：全部科目（join 類別名稱）。</summary>
    private RtmData? BuildAccountSubjectReport()
    {
        var dt = DbManager.QueryTable(
            "SELECT a.[科目編號], a.[科目名稱], COALESCE(c.[類別名稱],'') AS [類別名稱], " +
            "a.[期初借貸], a.[期初餘額] " +
            "FROM [會計科目] a LEFT JOIN [會計類別] c ON a.[類別編號] = c.[類別編號] " +
            "ORDER BY a.[科目編號]");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = "全部科目";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>財產基本資料：全部財產（彙總 DBCalc 由渲染器自動加總）。</summary>
    private RtmData? BuildPropertyReport()
    {
        var dt = DbManager.QueryTable(
            "SELECT [財產編號], [財產名稱], [數量], [取得日期], [取得原價], [預留殘值], [累計折舊金額], [單位] " +
            "FROM [財產資料] ORDER BY [財產編號]");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>郵寄標籤／信封：客廠基本資料的收件欄位。</summary>
    private RtmData? BuildLabelData(string 客廠類別, string scope)
    {
        var dt = DbManager.QueryTable(
            "SELECT [公司全名], [帳單地址], [帳單地郵遞區號] " +
            "FROM [客戶廠商] WHERE [客廠類別] = $t ORDER BY [客廠編號]",
            DbManager.Param("$t", 客廠類別));
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = scope;
        FillDetail(data, dt);
        return data;
    }

    private RtmData? BuildCustomerLabel() => BuildLabelData(ARService.應收類別, "全部客戶");

    private RtmData? BuildVendorLabel() => BuildLabelData(ARService.應付類別, "全部廠商");

    private RtmData? BuildEnvelope() => BuildLabelData(ARService.應收類別, "全部客戶");

    /// <summary>應收帳款郵寄標籤／標準信封：帳款主檔 join 客廠收件欄位。</summary>
    private RtmData? BuildARLabelData()
    {
        var dt = DbManager.QueryTable(
            "SELECT COALESCE(C.[公司全名],'') AS [公司全名], COALESCE(C.[帳單地址],'') AS [帳單地址], " +
            "COALESCE(C.[帳單地郵遞區號],'') AS [帳單地郵遞區號] " +
            "FROM [帳款主檔] B JOIN [客戶廠商] C ON B.[交易對象] = C.[客廠編號] " +
            "GROUP BY C.[客廠編號] ORDER BY C.[客廠編號]");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Company["聯絡地址"] = new CompanyInfo().Address;
        data.Company["聯絡地郵遞區號"] = "";
        data.Master["編號區間"] = "全部應收對象";
        FillDetail(data, dt);
        return data;
    }

    private RtmData? BuildARLabel() => BuildARLabelData();

    private RtmData? BuildAREnvelope() => BuildARLabelData();

    // ==================== 交易明細報表 ====================

    /// <summary>交易明細查詢：交易主檔 join 交易明細 join 客廠 join 貨品主檔。</summary>
    private static DataTable LoadTxDetailRows(string 單據類別, string 客廠類別)
    {
        return DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易單號], COALESCE(c.[公司簡稱],'') AS [對象名稱], " +
            "COALESCE(c.[公司簡稱],'') AS [公司簡稱], m.[合計金額], m.[營業稅], m.[總計金額], " +
            "d.[貨品編號], COALESCE(p.[品名],'') AS [品名], d.[數量], d.[單位], d.[單價], d.[金額], m.[單據類別] " +
            "FROM [交易主檔] m " +
            "JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] AND c.[客廠類別] = $t " +
            "LEFT JOIN [貨品主檔] p ON d.[貨品編號] = p.[貨品編號] " +
            "WHERE m.[單據類別] = $k ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]",
            DbManager.Param("$k", 單據類別), DbManager.Param("$t", 客廠類別));
    }

    /// <summary>出貨／進貨明細表：主檔（ppDBPipeline1）＋明細（ppDBPipeline2）主從報表資料。</summary>
    private RtmData? BuildTxDetailData(string 單據類別, string 客廠類別)
    {
        var dt = LoadTxDetailRows(單據類別, 客廠類別);
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline2" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";

        string 前一單號 = "";
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            var 單號 = Convert.ToString(r["交易單號"]) ?? "";
            bool 新單 = 單號 != 前一單號;
        foreach (DataColumn col in dt.Columns)
        {
            var name = col.ColumnName;
            if (name is "貨品編號" or "品名" or "數量" or "單位" or "單價" or "金額")
                d[name] = r[col];                       // 明細 pipeline 欄位（無前綴）
            else if (新單 || name is "交易日期" or "交易單號" or "對象名稱")
                d[$"ppDBPipeline1|{name}"] = r[col];     // 主檔欄位：分組欄位每列填，彙總金額欄位僅單首列
        }
            if (新單) 前一單號 = 單號;
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>客戶交易明細表：客戶出貨明細（每明細一列，單 pipeline）。</summary>
    private RtmData? BuildCustomerTxReportData()
    {
        var dt = LoadTxDetailRows("出貨", ARService.應收類別);
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>出退貨明細表：出貨與出退單據之交易明細（join 客廠簡稱與貨品）。</summary>
    private RtmData? BuildShipReturnDetailData()
    {
        var dt = DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易單號], m.[單據類別], COALESCE(c.[公司簡稱],'') AS [公司簡稱], " +
            "d.[貨品編號], COALESCE(p.[品名],'') AS [品名], d.[數量], COALESCE(d.[單位],'') AS [單位], " +
            "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額] " +
            "FROM [交易主檔] m " +
            "JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "LEFT JOIN [貨品主檔] p ON d.[貨品編號] = p.[貨品編號] " +
            "WHERE m.[單據類別] IN ('出貨','出退') " +
            "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
        return BuildTxListData(dt);
    }

    /// <summary>出貨退回明細表：僅出退單據之交易明細（同出退貨明細表資料結構）。</summary>
    private RtmData? BuildShipReturnDetailReport()
    {
        var dt = DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易單號], m.[單據類別], COALESCE(c.[公司簡稱],'') AS [公司簡稱], " +
            "d.[貨品編號], COALESCE(p.[品名],'') AS [品名], d.[數量], COALESCE(d.[單位],'') AS [單位], " +
            "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額] " +
            "FROM [交易主檔] m " +
            "JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "LEFT JOIN (SELECT [貨品編號], MAX([品名]) AS [品名] FROM [貨品主檔] GROUP BY [貨品編號]) p ON d.[貨品編號] = p.[貨品編號] " +
            "WHERE m.[單據類別] = '出退' ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
        return BuildTxListData(dt);
    }

    /// <summary>廠商交易明細表：廠商帳款明細（join 客廠簡稱與貨品主檔）。</summary>
    private RtmData? BuildVendorTxReportData()
    {
        var dt = DbManager.QueryTable(
            "SELECT B.[交易日期], B.[交易單號], B.[單據類別], COALESCE(C.[公司簡稱],'') AS [公司簡稱], " +
            "B.[貨品編號], COALESCE(P.[品名],'') AS [品名], B.[數量], COALESCE(B.[單位],'') AS [單位], " +
            "COALESCE(B.[單價],0) AS [單價], COALESCE(B.[金額],0) AS [金額] " +
            "FROM [帳款明細] B " +
            "JOIN [客戶廠商] C ON B.[交易對象] = C.[客廠編號] AND C.[客廠類別] = '廠商' " +
            "LEFT JOIN (SELECT [貨品編號], MAX([品名]) AS [品名] FROM [貨品主檔] GROUP BY [貨品編號]) P ON B.[貨品編號] = P.[貨品編號] " +
            "ORDER BY B.[交易日期], B.[交易單號]");
        return BuildTxListData(dt);
    }

    /// <summary>採購訂貨單據報表（報價／訂貨／採購／詢價）：主檔（ppDBPipeline1）＋明細（ppDBPipeline2）。</summary>
    private RtmData? BuildPoBillQuoteData() => BuildPoBillData("報價");
    private RtmData? BuildPoBillOrderData() => BuildPoBillData("訂貨");
    private RtmData? BuildPoBillPurchaseData() => BuildPoBillData("採購");
    private RtmData? BuildPoBillInquiryData() => BuildPoBillData("詢價");
    private RtmData? BuildPoReactionOpenData() => BuildPoReactionData("訂貨", "交易數量");
    private RtmData? BuildPoReactionShippedData() => BuildPoReactionData("訂貨", "已交數量");
    private RtmData? BuildPoReactionNotInData() => BuildPoReactionData("採購", "交易數量");
    private RtmData? BuildDepreciationDateData() => BuildDepreciationData("日期");
    private RtmData? BuildDepreciationSubjectData() => BuildDepreciationData("科目");
    private RtmData? BuildDepreciationPropertyData() => BuildDepreciationData("財產");

    private RtmData? BuildPoBillData(string 單據類別)
    {
        var dt = DbManager.QueryTable(
            "SELECT m.[交易單號], m.[交易日期], m.[交易對象], COALESCE(m.[交貨日期],'') AS [交貨日期], " +
            "COALESCE(m.[送貨地址],'') AS [送貨地址], COALESCE(m.[合計金額],0) AS [合計金額], " +
            "COALESCE(m.[營業稅],0) AS [營業稅], COALESCE(m.[總計金額],0) AS [總計金額], " +
            "COALESCE(m.[備註],'') AS [備註], " +
            "COALESCE(c.[公司全名],'') AS [對象名稱], COALESCE(c.[聯絡人一],'') AS [聯絡人一], " +
            "COALESCE(c.[聯絡電話一],'') AS [聯絡電話一], COALESCE(c.[統一編號],'') AS [統一編號], " +
            "COALESCE(c.[傳真號碼],'') AS [傳真號碼], COALESCE(e.[員工姓名],'') AS [員工名稱], " +
            "d.[貨品編號], COALESCE(d.[品名],'') AS [品名], d.[單位], COALESCE(d.[單價],0) AS [單價], " +
            "COALESCE(d.[金額],0) AS [金額], d.[數量], COALESCE(d.[附註說明],'') AS [附註說明] " +
            "FROM [採訂主檔] m " +
            "JOIN [採訂明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "LEFT JOIN [員工資料] e ON m.[員工編號] = e.[員工編號] " +
            "WHERE m.[單據類別] = $k ORDER BY m.[交易單號], d.[建檔序號]",
            DbManager.Param("$k", 單據類別));
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline2" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";

        string 前一單號 = "";
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            var 單號 = Convert.ToString(r["交易單號"]) ?? "";
            bool 新單 = 單號 != 前一單號;
            foreach (DataColumn col in dt.Columns)
            {
                var name = col.ColumnName;
                if (name is "貨品編號" or "品名" or "單位" or "單價" or "金額" or "數量" or "附註說明")
                    d[name] = r[col];
                else if (新單 || name is "交易單號")
                    d[$"ppDBPipeline1|{name}"] = r[col];
            }
            if (新單) 前一單號 = 單號;
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>採購訂貨反應表：依單據類別列示訂購明細與已交（交易）數量。</summary>
    private RtmData? BuildPoReactionData(string 單據類別, string 已交欄位)
    {
        var dt = DbManager.QueryTable(
            "SELECT COALESCE(c.[公司全名],'') AS [公司全名], m.[交易單號], d.[貨品編號], " +
            "COALESCE(d.[品名],'') AS [品名], m.[交易日期], COALESCE(d.[數量],0) AS [數量], " +
            $"COALESCE(d.[交易數量],0) AS [{已交欄位}], COALESCE(d.[相關單號],'') AS [相關單號] " +
            "FROM [採訂主檔] m JOIN [採訂明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "WHERE m.[單據類別] = $k ORDER BY m.[交易日期], d.[建檔序號]",
            DbManager.Param("$k", 單據類別));
        return BuildTxListData(dt);
    }

    /// <summary>折舊報表（日期別／科目別／財產別）：折舊明細 join 折舊提列。</summary>
    private RtmData? BuildDepreciationData(string 樣式)
    {
        var dt = DbManager.QueryTable(
            "SELECT D.[折舊日期], D.[財產編號], COALESCE(M.[財產名稱],'') AS [財產名稱], " +
            "COALESCE(D.[折舊金額],0) AS [折舊金額], COALESCE(D.[折舊科目],'') AS [折舊科目], " +
            "COALESCE(D.[費用科目],'') AS [費用科目], COALESCE(D.[備註],'') AS [備註], " +
            "COALESCE(D.[傳票編號],'') AS [傳票編號], COALESCE(M.[取得日期],'') AS [取得日期], " +
            "COALESCE(M.[取得原價],0) AS [取得原價], COALESCE(M.[耐用月數],0) AS [耐用月數] " +
            "FROM [折舊明細] D LEFT JOIN [折舊提列] M ON D.[財產編號] = M.[財產編號] " +
            "ORDER BY D.[折舊日期], D.[財產編號]");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        // 科目別折舊表以「所屬科目」欄顯示費用科目；日期別折舊表以「科目名稱」顯示折舊科目
        foreach (DataRow r in dt.Rows)
        {
            var d = new Dictionary<string, object?>();
            d["折舊日期"] = r["折舊日期"];
            d["財產編號"] = r["財產編號"];
            d["財產名稱"] = r["財產名稱"];
            d["折舊金額"] = r["折舊金額"];
            d["折舊科目"] = r["折舊科目"];
            d["費用科目"] = r["費用科目"];
            d["備註"] = r["備註"];
            d["傳票編號"] = r["傳票編號"];
            d["取得日期"] = r["取得日期"];
            d["取得原價"] = r["取得原價"];
            d["耐用月數"] = r["耐用月數"];
            d["所屬科目"] = 樣式 == "科目" ? (r["費用科目"] ?? "") : "";
            d["科目名稱"] = 樣式 == "日期" ? (r["折舊科目"] ?? "") : "";
            d["公司簡稱"] = new CompanyInfo().CompanyName;
            data.Detail.Add(d);
        }
        return data;
    }

    /// <summary>出退貨簡要表：出貨與出退單據之交易主檔（join 客廠全名）。</summary>
    private RtmData? BuildShipReturnBriefData()
    {
        var dt = DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易單號], m.[單據類別], COALESCE(m.[發票號碼],'') AS [發票號碼], " +
            "COALESCE(m.[合計金額],0) AS [合計金額], COALESCE(m.[營業稅],0) AS [營業稅], " +
            "COALESCE(m.[總計金額],0) AS [總計金額], COALESCE(c.[公司全名],'') AS [公司全名] " +
            "FROM [交易主檔] m LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "WHERE m.[單據類別] IN ('出貨','出退') ORDER BY m.[交易日期], m.[交易單號]");
        return BuildTxListData(dt);
    }

    /// <summary>出退貨列表式報表共用（單 pipeline，日期區間）。</summary>
    private RtmData? BuildTxListData(DataTable dt)
    {
        if (dt.Rows.Count == 0) return null;
        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>庫存異動明細（入出庫）：全部計算庫存之交易明細（join 客廠簡稱與貨品）。</summary>
    private DataTable LoadStockIoRows()
    {
        return DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易單號], m.[單據類別], COALESCE(c.[公司簡稱],'') AS [公司簡稱], " +
            "d.[貨品編號], COALESCE(p.[品名],'') AS [品名], d.[數量], COALESCE(d.[單位],'') AS [單位], " +
            "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額] " +
            "FROM [交易主檔] m " +
            "JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "LEFT JOIN [貨品主檔] p ON d.[貨品編號] = p.[貨品編號] " +
            "WHERE m.[單據類別] IN ('出貨','出退','進貨','進退','庫存調整') " +
            "ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
    }

    /// <summary>入出庫明細表：全部庫存異動明細。</summary>
    private RtmData? BuildStockIoDetailData() => BuildTxListData(LoadStockIoRows());

    /// <summary>貨品入出庫明細表：同入出庫明細表（貨品編號欄位於前）。</summary>
    private RtmData? BuildGoodsStockIoData() => BuildStockIoDetailData();

    /// <summary>貨品調整明細表：庫存調整明細含倉庫編號。</summary>
    private RtmData? BuildGoodsAdjustmentData()
    {
        var dt = DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易單號], d.[貨品編號], COALESCE(p.[品名],'') AS [品名], " +
            "COALESCE(d.[倉庫編號],'') AS [倉庫編號], COALESCE(d.[單位],'') AS [單位], " +
            "COALESCE(d.[數量],0) AS [數量], COALESCE(d.[金額],0) AS [金額] " +
            "FROM [交易主檔] m JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN (SELECT [貨品編號], MAX([品名]) AS [品名] FROM [貨品主檔] GROUP BY [貨品編號]) p ON d.[貨品編號] = p.[貨品編號] " +
            "WHERE m.[單據類別] = '庫存調整' ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
        return BuildTxListData(dt);
    }

    /// <summary>應收帳款明細表(含折扣)：取第一個有未收付單據的應收對象（供批次渲染驗證）。</summary>
    private RtmData? BuildArDetailDiscountData() =>
        BuildDetailDiscountData(ARService.應收類別);

    /// <summary>應付帳款明細表(含折扣)：取第一個有未收付單據的應付對象（供批次渲染驗證）。</summary>
    private RtmData? BuildApDetailDiscountData() =>
        BuildDetailDiscountData(ARService.應付類別);

    /// <summary>含折扣明細表資料：取客廠類別第一個有未收付單據之對象。</summary>
    private RtmData? BuildDetailDiscountData(string 客廠類別)
    {
        var obj = DbManager.QueryScalar(
            "SELECT B.[交易對象] FROM [帳款簡要] B " +
            "JOIN [客戶廠商] C ON B.[交易對象] = C.[客廠編號] AND C.[客廠類別] = $t " +
            "WHERE B.[未收付金額] <> 0 GROUP BY B.[交易對象] ORDER BY B.[交易對象] LIMIT 1",
            DbManager.Param("$t", 客廠類別));
        return obj is null ? null : ARService.BuildDetailReportData(obj.ToString()!);
    }

    /// <summary>出貨利潤明細表：出貨明細 join 成本，逐列計算毛利與毛利率。</summary>
    private RtmData? BuildShipProfitData()
    {
        var dt = DbManager.QueryTable(
            "SELECT m.[交易日期], m.[交易單號], COALESCE(c.[公司簡稱],'') AS [公司簡稱], " +
            "d.[貨品編號], COALESCE(p.[品名],'') AS [品名], d.[數量], COALESCE(d.[單位],'') AS [單位], " +
            "COALESCE(d.[單價],0) AS [單價], COALESCE(d.[金額],0) AS [金額], " +
            "ROUND(COALESCE(d.[金額],0) - COALESCE(d.[數量],0) * COALESCE(p.[標準成本],0), 2) AS [毛利], " +
            "CASE WHEN COALESCE(d.[金額],0) <> 0 " +
            "THEN ROUND((COALESCE(d.[金額],0) - COALESCE(d.[數量],0) * COALESCE(p.[標準成本],0)) / COALESCE(d.[金額],0) * 100, 2) " +
            "ELSE 0 END AS [毛利率] " +
            "FROM [交易主檔] m JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [客戶廠商] c ON m.[交易對象] = c.[客廠編號] " +
            "LEFT JOIN [貨品主檔] p ON d.[貨品編號] = p.[貨品編號] " +
            "WHERE m.[單據類別] = '出貨' ORDER BY m.[交易日期], m.[交易單號], d.[建檔序號]");
        return BuildTxListData(dt);
    }

    /// <summary>貨品利潤明細表：出貨明細依貨品列示成本與毛利。</summary>
    private RtmData? BuildGoodsProfitData()
    {
        var dt = DbManager.QueryTable(
            "SELECT d.[貨品編號], COALESCE(p.[品名],'') AS [品名], m.[交易日期], d.[數量], " +
            "COALESCE(d.[單位],'') AS [單位], COALESCE(d.[單價],0) AS [單價], " +
            "COALESCE(d.[金額],0) AS [金額], COALESCE(p.[標準成本],0) AS [成本], " +
            "ROUND(COALESCE(d.[金額],0) - COALESCE(d.[數量],0) * COALESCE(p.[標準成本],0), 2) AS [毛利], " +
            "CASE WHEN COALESCE(d.[金額],0) <> 0 " +
            "THEN ROUND((COALESCE(d.[金額],0) - COALESCE(d.[數量],0) * COALESCE(p.[標準成本],0)) / COALESCE(d.[金額],0) * 100, 2) " +
            "ELSE 0 END AS [毛利率] " +
            "FROM [交易主檔] m JOIN [交易明細] d ON m.[單據副碼] = d.[單據副碼] " +
            "LEFT JOIN [貨品主檔] p ON d.[貨品編號] = p.[貨品編號] " +
            "WHERE m.[單據類別] = '出貨' ORDER BY d.[貨品編號], m.[交易日期], d.[建檔序號]");
        return BuildTxListData(dt);
    }

    /// <summary>貨品低於安存表：現有數量低於安全存量之貨品庫存（含低於安存量）。</summary>
    private RtmData? BuildGoodsBelowSafetyData()
    {
        var dt = InventoryService.LoadStock(僅不足: true);
        if (dt.Rows.Count == 0) return null;
        dt.Columns.Add("低於安存", typeof(decimal));
        foreach (DataRow r in dt.Rows)
            r["低於安存"] = Convert.ToDecimal(r["現有數量"]) - Convert.ToDecimal(r["安全存量"]);

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["編號區間"] = "全部貨品";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>倉庫低於安存表：同貨品低於安存表（倉庫編號欄位於前）。</summary>
    private RtmData? BuildWarehouseBelowSafetyData() => BuildGoodsBelowSafetyData();

    // ==================== 票據列印報表 ====================

    /// <summary>支票列印：挑選一張未兌現付票，拆分到期日為年／月／日並填支票抬頭。</summary>
    private RtmData? BuildCheckPrintData()
    {
        var dt = DbManager.QueryTable(
            "SELECT [支票號碼], [票面金額], [到期日], COALESCE([支票抬頭],'') AS [公司全名] " +
            "FROM [票據收付] WHERE [收付類別] = '付票' AND COALESCE([票面金額],0) <> 0 " +
            "AND [到期日] IS NOT NULL ORDER BY [支票號碼] LIMIT 1");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        var d = new Dictionary<string, object?>();
        var row = dt.Rows[0];
        foreach (DataColumn col in dt.Columns) d[col.ColumnName] = row[col];
        if (DateTime.TryParse(Convert.ToString(row["到期日"]), out var 到期))
        {
            d["年"] = 到期.Year;
            d["月"] = 到期.Month;
            d["日"] = 到期.Day;
        }
        data.Detail.Add(d);
        return data;
    }

    /// <summary>票據簽收回條：挑選一張收票（到期日與票面金額填齊）。</summary>
    private RtmData? BuildBillReceiptData()
    {
        var dt = DbManager.QueryTable(
            "SELECT [支票號碼], [到期日], [票面金額] FROM [票據收付] " +
            "WHERE [收付類別] = '收票' AND COALESCE([票面金額],0) <> 0 " +
            "AND [到期日] IS NOT NULL ORDER BY [支票號碼] LIMIT 1");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        data.Company["公司全名"] = new CompanyInfo().CompanyName;
        var d = new Dictionary<string, object?>();
        var row = dt.Rows[0];
        foreach (DataColumn col in dt.Columns) d[col.ColumnName] = row[col];
        d["公司全名"] = new CompanyInfo().CompanyName;
        data.Detail.Add(d);
        return data;
    }

    /// <summary>票貼剩餘額度表：依票面銀行彙總票貼金額。</summary>
    private RtmData? BuildBillDiscountBalanceData()
    {
        var dt = DbManager.QueryTable(
            "SELECT COALESCE([票面銀行],'') AS [銀行名稱], COALESCE([銀行帳戶],'') AS [銀行帳戶], " +
            "COALESCE([票面銀行],'') AS [帳戶名稱], 0 AS [票貼額度], 0 AS [票貼折數], " +
            "SUM(COALESCE([票面金額],0)) AS [票貼金額總計] " +
            "FROM [票據收付] GROUP BY [票面銀行], [銀行帳戶] ORDER BY [票面銀行]");
        if (dt.Rows.Count == 0) return null;

        var data = new RtmData { DetailPipeline = "ppDBPipeline1" };
        FillCompany(data);
        data.Master["日期區間"] = "全部日期";
        FillDetail(data, dt);
        return data;
    }

    /// <summary>挑選有未收付單據的對象並產生明細／簡要表資料；無對象時回傳 null。</summary>
    private RtmData? PickObjectData(string 客廠類別, Func<string, RtmData?> build)
    {
        var 對象 = 挑選對象(客廠類別);
        if (string.IsNullOrEmpty(對象))
            return null;
        return build(對象);
    }

    /// <summary>挑選有未收付單據的對象；多筆時開選擇視窗。</summary>
    private string 挑選對象(string 客廠類別)
    {
        var dt = DbManager.QueryTable(
            "SELECT B.[交易對象] AS [對象], MAX(C.[公司簡稱]) AS [公司] " +
            "FROM [帳款簡要] B " +
            "JOIN [客戶廠商] C ON B.[交易對象] = C.[客廠編號] AND C.[客廠類別] = $t " +
            "WHERE B.[未收付金額] <> 0 GROUP BY B.[交易對象] ORDER BY B.[交易對象]",
            DbManager.Param("$t", 客廠類別));
        if (dt.Rows.Count == 0)
            return "";
        if (dt.Rows.Count == 1)
            return Convert.ToString(dt.Rows[0]["對象"]) ?? "";

        var items = dt.Rows.Cast<DataRow>().Select(r =>
        {
            var 對象 = Convert.ToString(r["對象"]) ?? "";
            var 公司 = Convert.ToString(r["公司"]) ?? "";
            return 公司.Length > 0 ? $"{對象}　{公司}" : 對象;
        }).ToList();
        var idx = PickRowDialog("選擇報表對象", $"請選擇{客廠類別}：", items);
        if (idx is null)
            return "";
        return (items[idx.Value] ?? "").Split('　')[0].Trim();
    }
}
