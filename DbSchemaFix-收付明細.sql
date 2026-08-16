-- 由 DbSchemaFix 產出：為缺少 PRIMARY KEY 的表重建（保留資料與索引）
-- 產生時間: 2026-08-12 02:02:47
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;

-- 表 收付明細：原無主鍵，指定主鍵 單據副碼, 建檔序號
CREATE TABLE "__fix_收付明細" ("單據副碼" INTEGER, "建檔序號" INTEGER, "單據號碼" TEXT, "單別" TEXT, "單據日期" TEXT, "發票編號" TEXT, "現行餘額" REAL, "折讓金額" REAL, "沖帳金額" REAL, PRIMARY KEY ("單據副碼", "建檔序號"));
INSERT INTO "__fix_收付明細" ("單據副碼", "建檔序號", "單據號碼", "單別", "單據日期", "發票編號", "現行餘額", "折讓金額", "沖帳金額") SELECT "單據副碼", "建檔序號", "單據號碼", "單別", "單據日期", "發票編號", "現行餘額", "折讓金額", "沖帳金額" FROM "收付明細";
DROP TABLE "收付明細";
ALTER TABLE "__fix_收付明細" RENAME TO "收付明細";
CREATE INDEX "ix_收付明細_單據副碼" ON "收付明細" ("單據副碼");
CREATE INDEX "ix_收付明細_建檔序號" ON "收付明細" ("建檔序號");
CREATE UNIQUE INDEX "ix_收付明細_單據副碼_建檔序號" ON "收付明細" ("單據副碼", "建檔序號");
CREATE INDEX "ix_收付明細_單據號碼" ON "收付明細" ("單據號碼");
COMMIT;
PRAGMA foreign_keys=ON;
