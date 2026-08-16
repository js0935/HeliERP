-- 由 DbSchemaFix 產出：為缺少 PRIMARY KEY 的表重建（保留資料與索引）
-- 產生時間: 2026-08-12 02:02:43
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;

-- 表 收付主檔：原無主鍵，指定主鍵 單據副碼
CREATE TABLE "__fix_收付主檔" ("收付類別" TEXT, "收付單號" TEXT, "單據副碼" INTEGER, "沖帳日期" TEXT, "沖帳對象" TEXT, "員工編號" TEXT, "部門編號" TEXT, "現金金額" REAL, "票據金額" REAL, "取用預收" REAL, "應收餘額" REAL, "預收餘額" REAL, "累入預收" REAL, "銷貨折讓" REAL, "現金折讓" REAL, "沖帳合計" REAL, "可沖餘額" REAL, "傳票編號" TEXT, "經辦人員" TEXT, "專案編號" TEXT, PRIMARY KEY ("單據副碼"));
INSERT INTO "__fix_收付主檔" ("收付類別", "收付單號", "單據副碼", "沖帳日期", "沖帳對象", "員工編號", "部門編號", "現金金額", "票據金額", "取用預收", "應收餘額", "預收餘額", "累入預收", "銷貨折讓", "現金折讓", "沖帳合計", "可沖餘額", "傳票編號", "經辦人員", "專案編號") SELECT "收付類別", "收付單號", "單據副碼", "沖帳日期", "沖帳對象", "員工編號", "部門編號", "現金金額", "票據金額", "取用預收", "應收餘額", "預收餘額", "累入預收", "銷貨折讓", "現金折讓", "沖帳合計", "可沖餘額", "傳票編號", "經辦人員", "專案編號" FROM "收付主檔";
DROP TABLE "收付主檔";
ALTER TABLE "__fix_收付主檔" RENAME TO "收付主檔";
CREATE INDEX "ix_收付主檔_收付類別" ON "收付主檔" ("收付類別");
CREATE INDEX "ix_收付主檔_收付單號" ON "收付主檔" ("收付單號");
CREATE UNIQUE INDEX "ix_收付主檔_單據副碼" ON "收付主檔" ("單據副碼");
CREATE INDEX "ix_收付主檔_傳票編號" ON "收付主檔" ("傳票編號");
CREATE UNIQUE INDEX "ix_收付主檔_收付類別_收付單號" ON "收付主檔" ("收付類別", "收付單號");
CREATE INDEX "ix_收付主檔_沖帳對象" ON "收付主檔" ("沖帳對象");
CREATE INDEX "ix_收付主檔_部門編號" ON "收付主檔" ("部門編號");
COMMIT;
PRAGMA foreign_keys=ON;
