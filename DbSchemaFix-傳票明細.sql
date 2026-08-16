-- 由 DbSchemaFix 產出：為缺少 PRIMARY KEY 的表重建（保留資料與索引）
-- 產生時間: 2026-08-12 02:02:59
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;

-- 表 傳票明細：原無主鍵，指定主鍵 單據副碼, 建檔序號
CREATE TABLE "__fix_傳票明細" ("單據副碼" INTEGER, "建檔序號" INTEGER, "傳票編號" TEXT, "傳票類別" TEXT, "借貸" TEXT, "科目編號" TEXT, "金額" REAL, "摘要" TEXT, "部門編號" TEXT, "專案編號" TEXT, "沖消傳票" TEXT, "借方金額" REAL, "貸方金額" REAL, PRIMARY KEY ("單據副碼", "建檔序號"));
INSERT INTO "__fix_傳票明細" ("單據副碼", "建檔序號", "傳票編號", "傳票類別", "借貸", "科目編號", "金額", "摘要", "部門編號", "專案編號", "沖消傳票", "借方金額", "貸方金額") SELECT "單據副碼", "建檔序號", "傳票編號", "傳票類別", "借貸", "科目編號", "金額", "摘要", "部門編號", "專案編號", "沖消傳票", "借方金額", "貸方金額" FROM "傳票明細";
DROP TABLE "傳票明細";
ALTER TABLE "__fix_傳票明細" RENAME TO "傳票明細";
CREATE INDEX "ix_傳票明細_科目編號" ON "傳票明細" ("科目編號");
CREATE INDEX "ix_傳票明細_專案編號" ON "傳票明細" ("專案編號");
CREATE INDEX "ix_傳票明細_單據副碼" ON "傳票明細" ("單據副碼");
CREATE INDEX "ix_傳票明細_傳票編號" ON "傳票明細" ("傳票編號");
CREATE UNIQUE INDEX "ix_傳票明細_單據副碼_建檔序號" ON "傳票明細" ("單據副碼", "建檔序號");
CREATE INDEX "ix_傳票明細_部門編號" ON "傳票明細" ("部門編號");
COMMIT;
PRAGMA foreign_keys=ON;
