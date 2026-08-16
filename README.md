# HeliERP

臺灣中小企業用桌面型 ERP。以 Windows Forms（.NET 8）為介面，SQLite 單機資料庫，
涵蓋貿易、庫存、會計、票據、薪資、生管、維修等營運模組與報表列印。

## 功能模組

| 模組 | 說明 | 主要畫面 |
|---|---|---|
| 貿易 | 出貨／出貨退回／進貨／進貨退出單據作業，貨品明細下拉選單、稅額自動計算 | `TransactionForm` |
| 庫存 | 進出貨、庫存調整、現有庫存查詢 | `InventoryForm`、`AdjustmentForm` |
| 採購訂單 | 採訂單據、收貨明細 | `PoOrderForm` |
| 帳款收付 | 收付款登錄、帳齡分析、期間設定 | `PaymentForm`、`AccountReceivableForm` |
| 票據 | 支票／票據收付、託收、存提轉帳 | `BillModuleForm`、`BillEditDialog` |
| 薪資出勤 | 員工薪資結算、出缺勤、健勞保對照、薪資組設 | `PayrollModuleForm`、`PayrollConfigDialogs`、`AttendanceDialogs` |
| 會計 | 傳票輸入（科目/部門下拉）、日記帳、總分類帳、損益表 | `AccountingModuleForm`、`VoucherDialogs`、`JournalDialogs` |
| 生管 | 驗貨、託運單據與明細 | `ProductionModuleForm`、`ShippingDialogs`、`InspectionDialogs` |
| 維修 | 維修單據作業 | `RepairModuleForm` |
| 主檔維護 | 客戶廠商、貨品、員工、科目等基本資料；分頁表單式編輯或欄位定義式編輯 | `FormMasterForm`、`GenericTableForm`、`GenericEditorDialog` |
| 報表 | 141 種單據／報表，Rtm 樣版渲染、重疊檢查 | `ReportMenuForm`、`ReportPrintService` |
| 系統設定 | 參數、權限（角色/畫面）、核准層數、審計日誌、發票字軌、健康檢查 | `SystemSettingsForm`、`ApprovalForm`、`AuditLogForm`、`InvoiceTrackForm`、`HealthCheckForm` |
| 其他 | 全域搜尋、折扣作業、資料備份還原、匯出共用（Excel/Word/HTML 等） | `GlobalSearchForm`、`DiscountForm`、`BackupService`、`ExportService` |

## 技術架構

- **介面**：Windows Forms，自訂 `UiTheme`（色彩/字型/元件樣式）、`ModernButton`、`ChartControl`
- **執行層**：.NET 8 / C#，`HeliERP.App`
- **資料層**：`HeliERP.Data`（`DbManager`、`SchemaReader`、`DbConfig`、`BackupService`）
- **資料庫**：SQLite，142 張業務資料表；主鍵／欄位定義集中在 `TableCatalog.cs`、`TableFields.cs`

### 欄位定義機制

主檔維護的輸入畫面由 `TableFields.cs` 集中定義（控制項型別、下拉來源、必填、標籤、
帶入規則、隱藏系統欄），`GenericEditorDialog` 依定義自動產生欄位式輸入視窗；
未定義的表則依欄名與資料型別自動判斷。下拉選單支援「帶入」：選擇後自動填寫來源表
的其他欄位（例：開戶銀行選銀行自動帶出銀行名稱）。

## 專案結構

```
HeliERP/
├── src/
│   ├── HeliERP.App/      # 主程式（WinForms）
│   ├── HeliERP.Data/     # 資料存取、結構讀取、備份
│   └── HeliERP.Models/   # 資料模型
├── verify-all.ps1        # 模組檢查腳本（副本庫上執行）
└── src/ModuleCheck/ 等   # 檢查工具（CrudCheck/UiLayoutCheck/UiShot/...）
```

## 建置與執行

需求：Windows、.NET 8 SDK。

```
dotnet build src/HeliERP.App/HeliERP.App.csproj -c Release
dotnet run --project src/HeliERP.App -c Release
```

## 資料庫

- 系統使用既有結構的 `HeliERP.db`（SQLite，142 張業務表）；程式不會自動建立空庫，
  需先準備含業務資料結構的資料庫檔，再於登入畫面選擇。
- 公開來源庫**不包含含資料的資料庫檔**（`*.db` 已列入 `.gitignore`），
  主鍵重建的 DDL 樣版存放於根目錄 `DbSchemaFix*.sql` 供參考。
- **結構樣板庫**：`database/HeliERP.structure.db` 為僅含完整欄位結構（142 張表、334 個索引）
  與預設使用者 `heli / heli`（成本權限、售價權限已開啟）的空資料庫。首次建庫方式：

  ```
  copy database\HeliERP.structure.db HeliERP.db
  ```

  再把正式資料庫放至登入畫面指定的路徑即可。首次登入後建議於「作業管理 → 帳號權限」變更密碼。

## 開發檢查工具

`verify-all.ps1` 於複製的測試資料庫上執行各模組端到端驗證（貿易、票據、薪資、會計、
庫存調整、帳齡、採訂、儀表板、報表重疊渲染等），輸出紀錄於 `verify-logs/`。
