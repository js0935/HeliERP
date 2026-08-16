# -*- coding: utf-8 -*-
"""查看帳款與收付現況：未沖帳單據、帳款主檔狀態"""
import sqlite3

conn = sqlite3.connect(r"D:\HeliAcc\HeliERP.db")
conn.row_factory = sqlite3.Row
out = []

out.append("== 帳款簡要（未沖帳 = 未收付金額 != 0）==")
rows = conn.execute("SELECT 交易單號, 單據類別, 交易對象, 總計金額, 已收付金額, 未收付金額, 應收付金額 FROM 帳款簡要 LIMIT 15").fetchall()
for r in rows:
    out.append(f"{r['交易單號']} | {r['單據類別']} | {r['交易對象']} | 總計={r['總計金額']} 已收={r['已收付金額']} 未收={r['未收付金額']} 應收={r['應收付金額']}")
out.append(f"未沖帳(未收付金額>0)筆數: {conn.execute('SELECT COUNT(*) FROM 帳款簡要 WHERE 未收付金額 != 0').fetchone()[0]}")
out.append(f"帳款簡要總筆數: {conn.execute('SELECT COUNT(*) FROM 帳款簡要').fetchone()[0]}")

out.append("")
out.append("== 帳款主檔 ==")
rows = conn.execute("SELECT 交易對象, 本期合計, 營業稅, 本期總計, 已收付金額 FROM 帳款主檔 LIMIT 10").fetchall()
for r in rows:
    out.append(f"{r['交易對象']} | 本期合計={r['本期合計']} 稅={r['營業稅']} 總計={r['本期總計']} 已收={r['已收付金額']}")

out.append("")
out.append("== 收付主檔 / 收付明細（現有資料）==")
out.append(f"收付主檔: {conn.execute('SELECT COUNT(*) FROM 收付主檔').fetchone()[0]} 筆")
out.append(f"收付明細: {conn.execute('SELECT COUNT(*) FROM 收付明細').fetchone()[0]} 筆")

out.append("")
out.append("== 客戶廠商（客戶類別）==")
rows = conn.execute("SELECT 客廠編號, 公司簡稱, 客廠類別 FROM 客戶廠商 WHERE 客廠類別='客戶' LIMIT 8").fetchall()
for r in rows:
    out.append(f"{r['客廠編號']} | {r['公司簡稱']}")
cust_q = "SELECT COUNT(*) FROM 客戶廠商 WHERE 客廠類別='客戶'"
out.append(f"客戶總數: {conn.execute(cust_q).fetchone()[0]}")
conn.close()
with open(r"D:\HeliAcc\payment_data.txt", "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("done")
