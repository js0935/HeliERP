# -*- coding: utf-8 -*-
"""查看收付主檔/明細實際資料樣本與帳款主檔完整欄位"""
import sqlite3

conn = sqlite3.connect(r"D:\HeliAcc\HeliERP.db")
conn.row_factory = sqlite3.Row
out = []

out.append("== 收付主檔 樣本 10 筆 ==")
for r in conn.execute("SELECT * FROM 收付主檔 LIMIT 10").fetchall():
    out.append(" | ".join(f"{k}={r[k]}" for k in r.keys()))

out.append("")
out.append("== 收付主檔 收付類別 分佈 ==")
for r in conn.execute("SELECT 收付類別, COUNT(*) c FROM 收付主檔 GROUP BY 收付類別").fetchall():
    out.append(f"{r['收付類別']}: {r['c']}")

out.append("")
out.append("== 收付明細 樣本 10 筆 ==")
for r in conn.execute("SELECT * FROM 收付明細 LIMIT 10").fetchall():
    out.append(" | ".join(f"{k}={r[k]}" for k in r.keys()))

out.append("")
out.append("== 收付明細 單別 分佈 ==")
for r in conn.execute("SELECT 單別, COUNT(*) c FROM 收付明細 GROUP BY 單別").fetchall():
    out.append(f"{r['單別']}: {r['c']}")

out.append("")
out.append("== 帳款主檔 樣本 3 筆（全部欄位）==")
for r in conn.execute("SELECT * FROM 帳款主檔 LIMIT 3").fetchall():
    out.append(" | ".join(f"{k}={r[k]}" for k in r.keys()))

conn.close()
with open(r"D:\HeliAcc\payment_sample.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("done")
