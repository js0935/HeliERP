-- 由 DbSchemaFix 產出：為缺少 PRIMARY KEY 的表重建（保留資料與索引）
-- 產生時間: 2026-08-12 02:03:01
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;

-- 表 會計類別：原無主鍵，指定主鍵 大類編號, 類別編號
CREATE TABLE "__fix_會計類別" ("大類編號" TEXT, "類別編號" TEXT, "類別名稱" TEXT, "英文名稱" TEXT, PRIMARY KEY ("大類編號", "類別編號"));
INSERT INTO "__fix_會計類別" ("大類編號", "類別編號", "類別名稱", "英文名稱") SELECT "大類編號", "類別編號", "類別名稱", "英文名稱" FROM "會計類別";
DROP TABLE "會計類別";
ALTER TABLE "__fix_會計類別" RENAME TO "會計類別";
CREATE UNIQUE INDEX "ix_會計類別_大類編號_類別編號" ON "會計類別" ("大類編號", "類別編號");
COMMIT;
PRAGMA foreign_keys=ON;
