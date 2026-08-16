-- 由 DbSchemaFix 產出：為缺少 PRIMARY KEY 的表重建（保留資料與索引）
-- 產生時間: 2026-08-12 02:02:53
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;

-- 表 客廠類別：原無主鍵，指定主鍵 客廠類別, 類別編號
CREATE TABLE "__fix_客廠類別" ("客廠類別" TEXT, "類別編號" TEXT, "類別名稱" TEXT, "備註" TEXT, PRIMARY KEY ("客廠類別", "類別編號"));
INSERT INTO "__fix_客廠類別" ("客廠類別", "類別編號", "類別名稱", "備註") SELECT "客廠類別", "類別編號", "類別名稱", "備註" FROM "客廠類別";
DROP TABLE "客廠類別";
ALTER TABLE "__fix_客廠類別" RENAME TO "客廠類別";
CREATE UNIQUE INDEX "ix_客廠類別_客廠類別_類別編號" ON "客廠類別" ("客廠類別", "類別編號");
COMMIT;
PRAGMA foreign_keys=ON;
