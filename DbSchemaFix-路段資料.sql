-- 由 DbSchemaFix 產出：為缺少 PRIMARY KEY 的表重建（保留資料與索引）
-- 產生時間: 2026-08-12 02:03:03
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;

-- 表 路段資料：原無主鍵，指定主鍵 路段編號
CREATE TABLE "__fix_路段資料" ("鄉鎮編號" TEXT, "路段編號" TEXT, "路段名稱" TEXT, PRIMARY KEY ("路段編號"));
INSERT INTO "__fix_路段資料" ("鄉鎮編號", "路段編號", "路段名稱") SELECT "鄉鎮編號", "路段編號", "路段名稱" FROM "路段資料";
DROP TABLE "路段資料";
ALTER TABLE "__fix_路段資料" RENAME TO "路段資料";
CREATE INDEX "ix_路段資料_鄉鎮編號" ON "路段資料" ("鄉鎮編號");
CREATE UNIQUE INDEX "ix_路段資料_路段編號" ON "路段資料" ("路段編號");
CREATE UNIQUE INDEX "ix_路段資料_鄉鎮編號_路段編號" ON "路段資料" ("鄉鎮編號", "路段編號");
COMMIT;
PRAGMA foreign_keys=ON;
