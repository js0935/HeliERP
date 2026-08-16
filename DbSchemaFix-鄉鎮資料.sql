-- 由 DbSchemaFix 產出：為缺少 PRIMARY KEY 的表重建（保留資料與索引）
-- 產生時間: 2026-08-12 02:02:57
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;

-- 表 鄉鎮資料：原無主鍵，指定主鍵 鄉鎮編號
CREATE TABLE "__fix_鄉鎮資料" ("縣市編號" TEXT, "鄉鎮編號" TEXT, "路段編號" TEXT, "鄉鎮名稱" TEXT, "郵遞區號" TEXT, PRIMARY KEY ("鄉鎮編號"));
INSERT INTO "__fix_鄉鎮資料" ("縣市編號", "鄉鎮編號", "路段編號", "鄉鎮名稱", "郵遞區號") SELECT "縣市編號", "鄉鎮編號", "路段編號", "鄉鎮名稱", "郵遞區號" FROM "鄉鎮資料";
DROP TABLE "鄉鎮資料";
ALTER TABLE "__fix_鄉鎮資料" RENAME TO "鄉鎮資料";
CREATE UNIQUE INDEX "ix_鄉鎮資料_鄉鎮編號" ON "鄉鎮資料" ("鄉鎮編號");
CREATE INDEX "ix_鄉鎮資料_路段編號" ON "鄉鎮資料" ("路段編號");
CREATE INDEX "ix_鄉鎮資料_縣市編號" ON "鄉鎮資料" ("縣市編號");
CREATE UNIQUE INDEX "ix_鄉鎮資料_縣市編號_鄉鎮編號_路段編號" ON "鄉鎮資料" ("縣市編號", "鄉鎮編號", "路段編號");
COMMIT;
PRAGMA foreign_keys=ON;
