# -*- coding: utf-8 -*-
"""
verify_trade.py — 交易作業功能驗證（在真實 DB 副本上執行）
============================================================
驗證目標：TradeService 的資料流假設與 DB schema 的一致性，以及
「新增出貨單 → 庫存扣減/帳款三層/異動快照 → 刪除 → 全部回復」的資料完整性。

範圍：
  S1 schema 一致性：TradeService.cs 引用的每張表、每個欄位都存在於 DB。
  T1 新增出貨單：模擬 SaveBill 的 SQL 資料流，驗證庫存扣減、帳款主檔/簡要/明細、
     交易異動/異動明細 全部寫入且數值正確。
  T2 刪除出貨單：模擬 DeleteBill/ReverseEffects 的 SQL 資料流，
     驗證庫存回復、帳款沖銷、快照/明細/主檔清除，最終狀態與操作前一致。

結果輸出至 verify_trade_result.txt（UTF-8），避免終端編碼問題。
"""
import shutil
import sqlite3
import sys
import os
from datetime import datetime

SRC_DB = r"D:\HeliAcc\HeliERP.db"
TMP_DIR = r"C:\Users\JS\AppData\Local\Temp\opencode"
WORK_DB = os.path.join(TMP_DIR, "trade_verify.db")
RESULT = r"D:\HeliAcc\verify_trade_result.txt"

TAX_RATE = 5  # 銷項稅率（系統參數預設）

# ── TradeService.cs 引用的表與欄位（從原始碼逐一提取）──
EXPECTED = {
    "交易主檔": ["單據類別", "交易單號", "單據副碼", "交易日期", "交易對象", "倉庫編號",
                 "員工編號", "發票號碼", "帳款日期", "備註", "課稅類別", "售價稅別",
                 "計算庫存", "數量合計", "合計金額", "營業稅", "總計金額", "加項金額",
                 "減項金額", "折讓金額", "已收付金額", "未收付金額", "應收付金額",
                 "現金收付金額", "明細總筆數", "本張成本", "原幣合計金額", "原幣營業稅",
                 "原幣總計金額", "製單"],
    "交易明細": ["單據副碼", "建檔序號", "貨品編號", "倉庫編號", "數量", "單位", "單價",
                 "成本", "折扣", "金額", "附註說明", "贈品", "服務項目", "計算庫存",
                 "異動數量", "異動金額"],
    "貨品庫存": ["貨品編號", "倉庫編號", "建檔序號", "現有數量"],
    "貨品主檔": ["貨品編號", "品名", "規格", "基本單位", "標準售價", "最近售價", "售價A",
                 "標準成本", "現行平均成本", "現行成本", "倉庫編號"],
    "客戶廠商": ["客廠編號", "客廠類別", "公司簡稱", "公司全名", "統一編號", "聯絡人一",
                 "聯絡電話一", "傳真號碼", "課稅別", "售價稅別"],
    "員工資料": ["員工編號", "員工姓名"],
    "倉庫資料": ["倉庫編號", "倉庫名稱"],
    "帳款主檔": ["建檔序號", "交易對象", "公司全名", "員工編號", "員工姓名", "統一編號",
                 "聯絡人一", "聯絡電話一", "傳真號碼", "累計預收貨款", "前期累計應收帳款",
                 "本期合計", "營業稅", "折讓金額", "已收付金額", "現金收付金額", "本期總計"],
    "帳款簡要": ["建檔序號", "單據類別", "交易對象", "員工編號", "交易日期", "交易單號",
                 "發票號碼", "合計金額", "營業稅", "總計金額", "折讓金額", "現金收付金額",
                 "已收付金額", "未收付金額", "應收付金額"],
    "帳款明細": ["建檔序號", "單據類別", "交易對象", "員工編號", "交易日期", "交易單號",
                 "發票號碼", "貨品編號", "品名", "數量", "單位", "單價", "折扣", "金額",
                 "附註說明", "贈品", "服務項目"],
    "交易異動": ["建檔序號", "單據類別", "交易單號", "單據副碼", "來源副碼", "交易日期",
                 "交易對象", "公司簡稱", "倉庫編號", "員工編號", "發票號碼", "帳款日期",
                 "合計金額", "營業稅", "總計金額", "明細總筆數", "貨品編號", "批號", "品名",
                 "數量", "單位", "單價", "成本", "折扣", "金額", "附註說明", "贈品",
                 "服務項目", "計算庫存", "異動數量", "異動金額"],
    "異動明細": ["建檔序號", "單據類別", "交易單號", "單據副碼", "來源副碼", "交易日期",
                 "交易對象", "公司簡稱", "倉庫編號", "員工編號", "發票號碼", "帳款日期",
                 "合計金額", "營業稅", "總計金額", "明細總筆數", "貨品編號", "批號", "品名",
                 "數量", "單位", "單價", "成本", "折扣", "金額", "附註說明", "贈品",
                 "服務項目", "計算庫存", "異動數量", "異動金額", "交易數量"],
    "庫存參數": ["參數編號", "使用多倉管理", "使用貨品批號", "使用貨品顏色", "檢查庫存量"],
    "系統參數": ["編號", "銷項稅率", "進項稅率", "常用倉庫"],
    "收付明細": ["單據號碼", "單別", "單據副碼", "建檔序號", "單據日期", "發票編號",
                 "現行餘額", "折讓金額", "沖帳金額"],
    "收付主檔": ["收付類別", "收付單號", "單據副碼", "沖帳日期", "沖帳對象", "員工編號",
                 "部門編號", "現金金額", "票據金額", "取用預收", "應收餘額", "預收餘額",
                 "累入預收", "銷貨折讓", "現金折讓", "沖帳合計", "可沖餘額", "傳票編號",
                 "經辦人員", "專案編號"],
}


def quote(t):
    return f'"{t}"'


def q(t, c):
    return f'"{c}"'


class Verifier:
    def __init__(self):
        self.lines = []
        self.pass_n = 0
        self.fail_n = 0
        self.conn = None

    def ok(self, name, detail):
        self.pass_n += 1
        self.lines.append(f"PASS  {name}  {detail}")

    def fail(self, name, detail):
        self.fail_n += 1
        self.lines.append(f"FAIL  {name}  {detail}")

    def check(self, cond, name, detail):
        (self.ok if cond else self.fail)(name, detail)
        return cond

    def q1(self, sql, args=()):
        cur = self.conn.execute(sql, args)
        row = cur.fetchone()
        return row[0] if row else None

    def qall(self, sql, args=()):
        return self.conn.execute(sql, args).fetchall()


def main():
    v = Verifier()

    # ── 準備：副本 DB ──
    if os.path.exists(WORK_DB):
        os.remove(WORK_DB)
    shutil.copy2(SRC_DB, WORK_DB)
    v.conn = sqlite3.connect(WORK_DB)
    v.conn.row_factory = sqlite3.Row

    # ════════════ S1：schema 一致性 ════════════
    v.lines.append("== S1 schema 一致性（TradeService 引用 vs DB 實際結構）==")
    missing_tables = []
    for t in EXPECTED:
        row = v.q1("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", (t,))
        if not v.check(row == 1, f"S1 表存在 [{t}]", "存在" if row else "❌ 缺表"):
            missing_tables.append(t)
    for t, cols in EXPECTED.items():
        if t in missing_tables:
            continue
        actual = {r[1] for r in v.qall(f"PRAGMA table_info('{t}')")}
        miss = [c for c in cols if c not in actual]
        v.check(not miss, f"S1 欄位 [{t}]", "全部存在" if not miss else f"❌ 缺欄位: {miss}")

    # ════════════ 測試資料選取 ════════════
    cust = v.qall("SELECT 客廠編號, 公司簡稱, 課稅別, 售價稅別 FROM 客戶廠商 WHERE 客廠類別='客戶' LIMIT 1")
    goods = v.qall("SELECT 貨品編號, 品名, 基本單位, 標準售價 FROM 貨品主檔 LIMIT 2")
    staff = v.qall("SELECT 員工編號, 員工姓名 FROM 員工資料 LIMIT 1")
    wh = v.qall("SELECT 倉庫編號, 倉庫名稱 FROM 倉庫資料 LIMIT 1")
    if not (cust and goods and staff and wh):
        v.fail("T0 測試資料", f"缺資料 客戶={bool(cust)} 貨品={len(goods)} 員工={bool(staff)} 倉庫={bool(wh)}")
        return v

    c = cust[0]
    g1, g2 = goods[0], goods[1] if len(goods) > 1 else goods[0]
    s = staff[0]
    w = wh[0]
    貨品編號1, 貨品編號2 = g1["貨品編號"], g2["貨品編號"]
    倉庫編號 = w["倉庫編號"]

    # 庫存檢查（多倉管理=1 → 需該貨品有該倉庫庫存列）
    stk1 = v.qall("SELECT 建檔序號, 現有數量 FROM 貨品庫存 WHERE 貨品編號=? AND 倉庫編號=?", (貨品編號1, 倉庫編號))
    stk2 = v.qall("SELECT 建檔序號, 現有數量 FROM 貨品庫存 WHERE 貨品編號=? AND 倉庫編號=?", (貨品編號2, 倉庫編號))
    if not (stk1 and stk2):
        # 退而求其次：任一有庫存的貨品
        stk1 = v.qall("SELECT 建檔序號, 現有數量 FROM 貨品庫存 WHERE 倉庫編號=? LIMIT 1", (倉庫編號,))
        if not stk1:
            v.fail("T0 庫存資料", "找不到有庫存列的貨品/倉庫組合")
            return v
        貨品編號1 = v.q1("SELECT 貨品編號 FROM 貨品庫存 WHERE 倉庫編號=?", (倉庫編號,))
        g1 = v.qall("SELECT * FROM 貨品主檔 WHERE 貨品編號=?", (貨品編號1,))[0]
        stk2 = stk1
        貨品編號2 = 貨品編號1

    inv1 = stk1[0]["現有數量"] if stk1[0]["現有數量"] is not None else 0.0
    inv2 = stk2[0]["現有數量"] if stk2[0]["現有數量"] is not None else 0.0
    inv1_idx, inv2_idx = stk1[0]["建檔序號"], stk2[0]["建檔序號"]

    # ════════════ T1：新增出貨單 ════════════
    v.lines.append("")
    v.lines.append(f"== T1 新增出貨單（客戶 {c['客廠編號']}，貨品 {貨品編號1}/{貨品編號2}，倉庫 {倉庫編號}）==")
    now = datetime.now()
    單號日期 = now.strftime("%y%m%d")
    # 取號規則（同 NextBillNo）
    max_no = v.q1("SELECT MAX(交易單號) FROM 交易主檔 WHERE 單據類別='出貨' AND 交易單號 LIKE ?", (單號日期 + "%",))
    seq = 1
    if max_no and len(max_no) >= 10 and max_no[6:10].isdigit():
        seq = int(max_no[6:10]) + 1
    交易單號 = 單號日期 + f"{seq:04d}"
    交易日期 = now.strftime("%Y-%m-%d %H:%M:%S")

    # 明細（2 筆，含折扣與贈品驗證）
    明細 = [
        {"貨品編號": 貨品編號1, "倉庫編號": 倉庫編號, "數量": 10, "單位": g1["基本單位"] or "", "單價": 100.0, "成本": 60.0, "折扣": 100, "贈品": 0, "服務項目": 0},
        {"貨品編號": 貨品編號2, "倉庫編號": 倉庫編號, "數量": 5, "單位": g2["基本單位"] or "", "單價": 80.0, "成本": 50.0, "折扣": 90, "贈品": 0, "服務項目": 0},
    ]
    金額1 = round(10 * 100.0 * 100 / 100, 2)   # 1000.00
    金額2 = round(5 * 80.0 * 90 / 100, 2)      # 360.00
    合計 = round(金額1 + 金額2, 2)              # 1360.00
    稅 = round(合計 * TAX_RATE / 100, 0)        # 68
    總計 = 合計 + 稅                            # 1428.00
    數量合計 = 15
    本張成本 = 60.0 * 10 + 50.0 * 5             # 850.0

    # 操作前基準
    acc_before = v.q1("SELECT 本期總計 FROM 帳款主檔 WHERE 交易對象=?", (c["客廠編號"],))
    acc_before = acc_before if acc_before is not None else 0.0
    交易主檔_before = v.q1("SELECT COUNT(*) FROM 交易主檔") or 0
    交易異動_before = v.q1("SELECT COUNT(*) FROM 交易異動") or 0

    with v.conn:  # 單一交易（模擬 BEGIN IMMEDIATE 語意）
        # 1. 主檔
        單據副碼 = (v.q1("SELECT COALESCE(MAX(單據副碼),0) FROM 交易主檔") or 0) + 1
        課稅別 = c["課稅別"] or ""
        售價稅別 = c["售價稅別"] or ""
        v.conn.execute(f"INSERT INTO {quote('交易主檔')} ({','.join(q('交易主檔', x) for x in EXPECTED['交易主檔'])}) VALUES ({','.join('?' for _ in EXPECTED['交易主檔'])})", (
            交易單號, 交易單號, 單據副碼, 交易日期, c["客廠編號"], 倉庫編號, s["員工編號"],
            "", 交易日期, "", 課稅別, 售價稅別, 1, 數量合計, 合計, 稅, 總計,
            0.0, 0.0, 0.0, 0.0, 總計, 總計, 0.0, len(明細), 本張成本, 合計, 稅, 總計, os.environ.get("USERNAME", "test"),
        ))
        # 2. 明細
        建檔序號 = (v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 交易明細") or 0) + 1
        for d, amt in zip(明細, (金額1, 金額2)):
            v.conn.execute(f"INSERT INTO {quote('交易明細')} ({','.join(q('交易明細', x) for x in EXPECTED['交易明細'])}) VALUES ({','.join('?' for _ in EXPECTED['交易明細'])})", (
                單據副碼, 建檔序號, d["貨品編號"], d["倉庫編號"], d["數量"], d["單位"], d["單價"],
                d["成本"], d["折扣"], amt, "", d["贈品"], d["服務項目"], 1, d["數量"], amt,
            ))
            建檔序號 += 1
        # 3. 庫存扣減（StockDirection = -1）
        v.conn.execute("UPDATE 貨品庫存 SET 現有數量 = 現有數量 + ? WHERE 貨品編號=? AND 建檔序號=?",
                       (-10, 貨品編號1, inv1_idx))
        v.conn.execute("UPDATE 貨品庫存 SET 現有數量 = 現有數量 + ? WHERE 貨品編號=? AND 建檔序號=?",
                       (-5, 貨品編號2, inv2_idx))
        # 4. 帳款主檔（PayDirection = +1，累加本期欄位）
        acc = v.q1("SELECT 建檔序號 FROM 帳款主檔 WHERE 交易對象=?", (c["客廠編號"],))
        if acc is None:
            v.conn.execute(f"INSERT INTO {quote('帳款主檔')} ({','.join(q('帳款主檔', x) for x in EXPECTED['帳款主檔'])}) VALUES ({','.join('?' for _ in EXPECTED['帳款主檔'])})", (
                (v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 帳款主檔") or 0) + 1, c["客廠編號"],
                c["公司簡稱"], s["員工編號"], s["員工姓名"], "", "", "", "", 0.0, 0.0, 合計, 稅, 0.0, 0.0, 0.0, 總計,
            ))
        else:
            v.conn.execute("UPDATE 帳款主檔 SET 本期合計=本期合計+?, 營業稅=營業稅+?, 本期總計=本期總計+? WHERE 建檔序號=?",
                           (合計, 稅, 總計, acc))
        # 5. 帳款簡要
        v.conn.execute(f"INSERT INTO {quote('帳款簡要')} ({','.join(q('帳款簡要', x) for x in EXPECTED['帳款簡要'])}) VALUES ({','.join('?' for _ in EXPECTED['帳款簡要'])})", (
            (v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 帳款簡要") or 0) + 1, "出貨", c["客廠編號"],
            s["員工編號"], 交易日期, 交易單號, "", 合計, 稅, 總計, 0.0, 0.0, 0.0, 總計, 總計,
        ))
        # 6. 帳款明細
        for d, amt in zip(明細, (金額1, 金額2)):
            v.conn.execute(f"INSERT INTO {quote('帳款明細')} ({','.join(q('帳款明細', x) for x in EXPECTED['帳款明細'])}) VALUES ({','.join('?' for _ in EXPECTED['帳款明細'])})", (
                (v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 帳款明細") or 0) + 1, "出貨", c["客廠編號"],
                s["員工編號"], 交易日期, 交易單號, "", d["貨品編號"], g1["品名"] if d["貨品編號"] == 貨品編號1 else g2["品名"],
                d["數量"], d["單位"], d["單價"], d["折扣"], amt, "", d["贈品"], d["服務項目"],
            ))
        # 7. 交易異動 + 異動明細
        建檔序號 = (v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 交易異動") or 0) + 1
        for d, amt in zip(明細, (金額1, 金額2)):
            公司簡稱 = c["公司簡稱"] or ""
            snap = (
                建檔序號, "出貨", 交易單號, 單據副碼, 單據副碼, 交易日期, c["客廠編號"], 公司簡稱, 倉庫編號,
                s["員工編號"], "", 交易日期, 合計, 稅, 總計, len(明細), d["貨品編號"], None,
                g1["品名"] if d["貨品編號"] == 貨品編號1 else g2["品名"], d["數量"], d["單位"], d["單價"],
                d["成本"], d["折扣"], amt, "", d["贈品"], d["服務項目"], 1, d["數量"], amt,
            )
            v.conn.execute(f"INSERT INTO {quote('交易異動')} ({','.join(q('交易異動', x) for x in EXPECTED['交易異動'])}) VALUES ({','.join('?' for _ in EXPECTED['交易異動'])})", snap)
            snap_detail = snap + (d["數量"],)
            v.conn.execute(f"INSERT INTO {quote('異動明細')} ({','.join(q('異動明細', x) for x in EXPECTED['異動明細'])}) VALUES ({','.join('?' for _ in EXPECTED['異動明細'])})", snap_detail)
            建檔序號 += 1

    # ── T1 驗證 ──
    v.check(v.q1("SELECT COUNT(*) FROM 交易主檔 WHERE 單據副碼=? AND 交易單號=?", (單據副碼, 交易單號)) == 1,
            "T1 主檔寫入", f"{交易單號} 已建立")
    v.check(v.q1("SELECT COUNT(*) FROM 交易明細 WHERE 單據副碼=?", (單據副碼,)) == 2,
            "T1 明細寫入", "2 筆")
    合計_got = v.q1("SELECT 合計金額 FROM 交易主檔 WHERE 單據副碼=?", (單據副碼,))
    稅_got = v.q1("SELECT 營業稅 FROM 交易主檔 WHERE 單據副碼=?", (單據副碼,))
    總計_got = v.q1("SELECT 總計金額 FROM 交易主檔 WHERE 單據副碼=?", (單據副碼,))
    v.check(合計_got == 合計, "T1 合計金額", f"{合計_got} == {合計}")
    v.check(稅_got == 稅, "T1 營業稅", f"{稅_got} == {稅}")
    v.check(總計_got == 總計, "T1 總計金額", f"{總計_got} == {總計}")
    v.check(v.q1("SELECT 未收付金額 FROM 交易主檔 WHERE 單據副碼=?", (單據副碼,)) == 總計,
            "T1 未收付金額", f"= 總計 {總計}")
    # 庫存
    現1 = v.q1("SELECT 現有數量 FROM 貨品庫存 WHERE 貨品編號=? AND 建檔序號=?", (貨品編號1, inv1_idx))
    現2 = v.q1("SELECT 現有數量 FROM 貨品庫存 WHERE 貨品編號=? AND 建檔序號=?", (貨品編號2, inv2_idx))
    v.check(abs(現1 - (inv1 - 10)) < 1e-9, "T1 庫存扣減(1)", f"{inv1} → {現1}（-10）")
    v.check(abs(現2 - (inv2 - 5)) < 1e-9, "T1 庫存扣減(2)", f"{inv2} → {現2}（-5）")
    # 帳款
    本期總計 = v.q1("SELECT 本期總計 FROM 帳款主檔 WHERE 交易對象=?", (c["客廠編號"],))
    v.check(abs(本期總計 - (acc_before + 總計)) < 1e-9, "T1 帳款主檔累加", f"{acc_before} → {本期總計}（+{總計}）")
    v.check(v.q1("SELECT COUNT(*) FROM 帳款簡要 WHERE 交易單號=?", (交易單號,)) == 1, "T1 帳款簡要", "1 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 帳款明細 WHERE 交易單號=?", (交易單號,)) == 2, "T1 帳款明細", "2 筆")
    # 異動快照
    v.check(v.q1("SELECT COUNT(*) FROM 交易異動 WHERE 單據副碼=?", (單據副碼,)) == 2, "T1 交易異動", "2 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 異動明細 WHERE 單據副碼=?", (單據副碼,)) == 2, "T1 異動明細", "2 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 交易異動 WHERE 單據副碼=? AND 異動數量=10 AND 異動金額=1000.0", (單據副碼,)) == 1,
            "T1 快照異動數/金額", "明細1 數量10 金額1000")
    數量合計_got = v.q1("SELECT 數量合計 FROM 交易主檔 WHERE 單據副碼=?", (單據副碼,))
    v.check(數量合計_got == 15, "T1 主檔數量合計", f"{數量合計_got} == 15")

    # ════════════ T2：刪除出貨單 ════════════
    v.lines.append("")
    v.lines.append("== T2 刪除出貨單（ReverseEffects：庫存回復 / 帳款沖銷 / 快照清除）==")
    with v.conn:
        # 1. 回復庫存（反向加回，StockDirection 反號）
        v.conn.execute("UPDATE 貨品庫存 SET 現有數量=現有數量+? WHERE 貨品編號=? AND 建檔序號=?", (10, 貨品編號1, inv1_idx))
        v.conn.execute("UPDATE 貨品庫存 SET 現有數量=現有數量+? WHERE 貨品編號=? AND 建檔序號=?", (5, 貨品編號2, inv2_idx))
        # 2. 沖銷帳款主檔
        v.conn.execute("UPDATE 帳款主檔 SET 本期合計=本期合計-?, 營業稅=營業稅-?, 本期總計=本期總計-? WHERE 交易對象=?",
                       (合計, 稅, 總計, c["客廠編號"]))
        # 3. 刪帳款簡要/明細
        v.conn.execute("DELETE FROM 帳款簡要 WHERE 交易單號=?", (交易單號,))
        v.conn.execute("DELETE FROM 帳款明細 WHERE 交易單號=?", (交易單號,))
        # 4. 刪異動快照
        v.conn.execute("DELETE FROM 交易異動 WHERE 單據副碼=?", (單據副碼,))
        v.conn.execute("DELETE FROM 異動明細 WHERE 單據副碼=?", (單據副碼,))
        # 5. 刪交易明細 + 主檔
        v.conn.execute("DELETE FROM 交易明細 WHERE 單據副碼=?", (單據副碼,))
        v.conn.execute("DELETE FROM 交易主檔 WHERE 單據副碼=?", (單據副碼,))

    # ── T2 驗證 ──
    現1b = v.q1("SELECT 現有數量 FROM 貨品庫存 WHERE 貨品編號=? AND 建檔序號=?", (貨品編號1, inv1_idx))
    現2b = v.q1("SELECT 現有數量 FROM 貨品庫存 WHERE 貨品編號=? AND 建檔序號=?", (貨品編號2, inv2_idx))
    v.check(abs(現1b - inv1) < 1e-9, "T2 庫存回復(1)", f"{現1} → {現1b}（回到 {inv1}）")
    v.check(abs(現2b - inv2) < 1e-9, "T2 庫存回復(2)", f"{現2} → {現2b}（回到 {inv2}）")
    本期總計b = v.q1("SELECT 本期總計 FROM 帳款主檔 WHERE 交易對象=?", (c["客廠編號"],))
    v.check(abs(本期總計b - acc_before) < 1e-9, "T2 帳款沖銷", f"{本期總計} → {本期總計b}（回到 {acc_before}）")
    v.check(v.q1("SELECT COUNT(*) FROM 帳款簡要 WHERE 交易單號=?", (交易單號,)) == 0, "T2 帳款簡要清除", "0 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 帳款明細 WHERE 交易單號=?", (交易單號,)) == 0, "T2 帳款明細清除", "0 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 交易異動 WHERE 單據副碼=?", (單據副碼,)) == 0, "T2 交易異動清除", "0 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 異動明細 WHERE 單據副碼=?", (單據副碼,)) == 0, "T2 異動明細清除", "0 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 交易明細 WHERE 單據副碼=?", (單據副碼,)) == 0, "T2 交易明細清除", "0 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 交易主檔 WHERE 單據副碼=?", (單據副碼,)) == 0, "T2 交易主檔清除", "0 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 交易主檔") == 交易主檔_before, "T2 主檔總數回復", f"回到 {交易主檔_before}")

    # ════════════ T3：收付沖帳（SavePayment / DeletePayment 資料流） ════════════
    v.lines.append("")
    v.lines.append(f"== T3 收付沖帳（收款部分沖帳；客戶 {c['客廠編號']}）==")
    出貨總計 = 1000.0
    收款金額 = 600.0
    單號日期2 = datetime.now().strftime("%y%m%d")
    max2 = v.q1("SELECT MAX(交易單號) FROM 交易主檔 WHERE 單據類別='出貨' AND 交易單號 LIKE ?", (單號日期2 + "%",))
    seq2 = 1
    if max2 and len(max2) >= 10 and max2[6:10].isdigit():
        seq2 = int(max2[6:10]) + 1
    出貨單號 = 單號日期2 + f"{seq2:04d}"
    交易日期2 = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    沖帳前餘額 = v.q1("SELECT COALESCE(SUM(ABS(未收付金額)),0) FROM 帳款簡要 WHERE 交易對象=?", (c["客廠編號"],)) or 0.0

    # ── 準備一張未收付出貨單（帳款輸入條件：交易主檔 + 帳款簡要 + 帳款主檔）──
    with v.conn:
        出貨副碼 = (v.q1("SELECT COALESCE(MAX(單據副碼),0) FROM 交易主檔") or 0) + 1
        v.conn.execute("INSERT INTO 交易主檔 (單據類別,交易單號,單據副碼,交易日期,交易對象,倉庫編號,員工編號,發票號碼,帳款日期,備註,課稅類別,售價稅別,計算庫存,數量合計,合計金額,營業稅,總計金額,加項金額,減項金額,折讓金額,已收付金額,未收付金額,應收付金額,現金收付金額,明細總筆數,本張成本,原幣合計金額,原幣營業稅,原幣總計金額,製單) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                       ("出貨", 出貨單號, 出貨副碼, 交易日期2, c["客廠編號"], 倉庫編號, s["員工編號"], "", 交易日期2, "", "", "", 0, 0, 0.0, 0.0, 出貨總計, 0.0, 0.0, 0.0, 0.0, 出貨總計, 出貨總計, 0.0, 1, 0.0, 0.0, 0.0, 出貨總計, "test"))
        v.conn.execute("INSERT INTO 帳款簡要 (建檔序號,單據類別,交易對象,員工編號,交易日期,交易單號,發票號碼,合計金額,營業稅,總計金額,折讓金額,現金收付金額,已收付金額,未收付金額,應收付金額) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                       ((v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 帳款簡要") or 0) + 1, "出貨", c["客廠編號"], s["員工編號"], 交易日期2, 出貨單號, "", 0.0, 0.0, 出貨總計, 0.0, 0.0, 0.0, 出貨總計, 出貨總計))
        v.conn.execute("UPDATE 帳款主檔 SET 本期合計=本期合計+?, 本期總計=本期總計+? WHERE 交易對象=?",
                       (0.0, 出貨總計, c["客廠編號"]))
    v.check(v.q1("SELECT COUNT(*) FROM 交易主檔 WHERE 單據副碼=?", (出貨副碼,)) == 1,
            "T3 出貨單建立", f"{出貨單號} 未收付 {出貨總計}")

    # ── 沖帳（SavePayment 資料流：收付主檔→明細→帳款簡要→交易主檔→帳款主檔）──
    with v.conn:
        收付副碼 = (v.q1("SELECT COALESCE(MAX(單據副碼),0) FROM 收付主檔") or 0) + 1
        maxp = v.q1("SELECT MAX(收付單號) FROM 收付主檔 WHERE 收付單號 LIKE ?", (單號日期2 + "%",))
        pseq = 1
        if maxp and len(maxp) >= 10 and maxp[6:10].isdigit():
            pseq = int(maxp[6:10]) + 1
        收付單號 = 單號日期2 + f"{pseq:04d}"
        應收餘額 = round((沖帳前餘額 + 出貨總計) - 收款金額, 2)
        v.conn.execute("INSERT INTO 收付主檔 (收付類別,收付單號,單據副碼,沖帳日期,沖帳對象,員工編號,部門編號,現金金額,票據金額,取用預收,應收餘額,預收餘額,累入預收,銷貨折讓,現金折讓,沖帳合計,可沖餘額,傳票編號,經辦人員,專案編號) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                       ("收款", 收付單號, 收付副碼, datetime.now().strftime("%Y-%m-%d %H:%M:%S"), c["客廠編號"], s["員工編號"], "", 收款金額, 0.0, 0.0, 應收餘額, 0.0, 0.0, 0.0, 0.0, 收款金額, 0.0, "", "test", ""))
        v.conn.execute("INSERT INTO 收付明細 (單據副碼,建檔序號,單據號碼,單別,單據日期,發票編號,現行餘額,折讓金額,沖帳金額) VALUES (?,?,?,?,?,?,?,?,?)",
                       (收付副碼, (v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 收付明細") or 0) + 1, 出貨單號, "出貨", 交易日期2, "", 出貨總計, 0.0, 收款金額))
        v.conn.execute("UPDATE 帳款簡要 SET 已收付金額=已收付金額+?, 未收付金額=未收付金額-? WHERE 交易單號=? AND 單據類別=?",
                       (收款金額, 收款金額, 出貨單號, "出貨"))
        v.conn.execute("UPDATE 交易主檔 SET 已收付金額=已收付金額+?, 未收付金額=未收付金額-? WHERE 交易單號=? AND 單據類別=?",
                       (收款金額, 收款金額, 出貨單號, "出貨"))
        v.conn.execute("UPDATE 帳款主檔 SET 已收付金額=已收付金額+? WHERE 交易對象=?",
                       (收款金額, c["客廠編號"]))

    # ── T3 沖帳驗證 ──
    v.check(v.q1("SELECT COUNT(*) FROM 收付主檔 WHERE 單據副碼=?", (收付副碼,)) == 1,
            "T3 收付主檔", f"{收付單號} 已建立")
    沖帳合計_got = v.q1("SELECT 沖帳合計 FROM 收付主檔 WHERE 單據副碼=?", (收付副碼,))
    v.check(abs(沖帳合計_got - 收款金額) < 1e-9, "T3 沖帳合計", f"{沖帳合計_got} == {收款金額}")
    應收餘額_got = v.q1("SELECT 應收餘額 FROM 收付主檔 WHERE 單據副碼=?", (收付副碼,))
    v.check(abs(應收餘額_got - 應收餘額) < 1e-9, "T3 應收餘額", f"{應收餘額_got} == {應收餘額}")
    v.check(v.q1("SELECT COUNT(*) FROM 收付明細 WHERE 單據副碼=? AND 單據號碼=? AND 單別='出貨'", (收付副碼, 出貨單號)) == 1,
            "T3 收付明細關聯", "單據號碼+單別 正確")
    沖帳金額_got = v.q1("SELECT 沖帳金額 FROM 收付明細 WHERE 單據副碼=?", (收付副碼,))
    v.check(abs(沖帳金額_got - 收款金額) < 1e-9, "T3 明細沖帳金額", f"{沖帳金額_got} == {收款金額}")
    未收付_got = v.q1("SELECT 未收付金額 FROM 帳款簡要 WHERE 交易單號=?", (出貨單號,))
    v.check(abs(未收付_got - (出貨總計 - 收款金額)) < 1e-9, "T3 帳款簡要未收付", f"{未收付_got} == {出貨總計 - 收款金額}（部分沖帳）")
    已收付_got = v.q1("SELECT 已收付金額 FROM 帳款簡要 WHERE 交易單號=?", (出貨單號,))
    v.check(abs(已收付_got - 收款金額) < 1e-9, "T3 帳款簡要已收付", f"{已收付_got} == {收款金額}")
    主未收付 = v.q1("SELECT 未收付金額 FROM 交易主檔 WHERE 單據副碼=?", (出貨副碼,))
    v.check(abs(主未收付 - (出貨總計 - 收款金額)) < 1e-9, "T3 交易主檔未收付同步", f"{主未收付} == {出貨總計 - 收款金額}")
    主已收付 = v.q1("SELECT 已收付金額 FROM 交易主檔 WHERE 單據副碼=?", (出貨副碼,))
    v.check(abs(主已收付 - 收款金額) < 1e-9, "T3 交易主檔已收付同步", f"{主已收付} == {收款金額}")
    帳款主已收付 = v.q1("SELECT 已收付金額 FROM 帳款主檔 WHERE 交易對象=?", (c["客廠編號"],))
    v.check(abs(帳款主已收付 - 收款金額) < 1e-9, "T3 帳款主檔已收付", f"{帳款主已收付} == {收款金額}")

    # ── 撤銷沖帳（DeletePayment 資料流：逐筆反向 + 清除）──
    with v.conn:
        v.conn.execute("UPDATE 帳款簡要 SET 已收付金額=已收付金額-?, 未收付金額=未收付金額+? WHERE 交易單號=? AND 單據類別=?",
                       (收款金額, 收款金額, 出貨單號, "出貨"))
        v.conn.execute("UPDATE 交易主檔 SET 已收付金額=已收付金額-?, 未收付金額=未收付金額+? WHERE 交易單號=? AND 單據類別=?",
                       (收款金額, 收款金額, 出貨單號, "出貨"))
        v.conn.execute("UPDATE 帳款主檔 SET 已收付金額=已收付金額-? WHERE 交易對象=?",
                       (收款金額, c["客廠編號"]))
        v.conn.execute("DELETE FROM 收付明細 WHERE 單據副碼=?", (收付副碼,))
        v.conn.execute("DELETE FROM 收付主檔 WHERE 單據副碼=?", (收付副碼,))

    # ── T3 撤銷驗證 ──
    v.check(v.q1("SELECT COUNT(*) FROM 收付主檔 WHERE 單據副碼=?", (收付副碼,)) == 0,
            "T3 撤銷-收付主檔清除", "0 筆")
    v.check(v.q1("SELECT COUNT(*) FROM 收付明細 WHERE 單據副碼=?", (收付副碼,)) == 0,
            "T3 撤銷-收付明細清除", "0 筆")
    未收付_r = v.q1("SELECT 未收付金額 FROM 帳款簡要 WHERE 交易單號=?", (出貨單號,))
    v.check(abs(未收付_r - 出貨總計) < 1e-9, "T3 撤銷-未收付回復", f"{未收付_r} == {出貨總計}")
    已收付_r = v.q1("SELECT 已收付金額 FROM 帳款簡要 WHERE 交易單號=?", (出貨單號,))
    v.check(abs(已收付_r - 0.0) < 1e-9, "T3 撤銷-已收付歸零", f"{已收付_r} == 0")
    帳款主已收付_r = v.q1("SELECT 已收付金額 FROM 帳款主檔 WHERE 交易對象=?", (c["客廠編號"],))
    v.check(abs(帳款主已收付_r - 0.0) < 1e-9, "T3 撤銷-帳款主檔回復", f"{帳款主已收付_r} == 0")

    # ── 清理 T3 出貨單（副本回到操作前狀態）──
    with v.conn:
        v.conn.execute("UPDATE 帳款主檔 SET 本期合計=本期合計-?, 本期總計=本期總計-? WHERE 交易對象=?",
                       (0.0, 出貨總計, c["客廠編號"]))
        v.conn.execute("DELETE FROM 帳款簡要 WHERE 交易單號=?", (出貨單號,))
        v.conn.execute("DELETE FROM 交易主檔 WHERE 單據副碼=?", (出貨副碼,))
    v.check(v.q1("SELECT COUNT(*) FROM 交易主檔") == 交易主檔_before, "T3 清理-主檔總數回復", f"回到 {交易主檔_before}")

    # ════════════ T4：付款閉環（進貨應付 → 付款沖帳 → 撤銷） ════════════
    v.lines.append("")
    v.lines.append("== T4 付款閉環（進貨應付 → 付款部分沖帳 → 撤銷回復）==")
    sup = v.qall("SELECT 客廠編號, 公司簡稱 FROM 客戶廠商 WHERE 客廠類別='廠商' LIMIT 1")
    if not sup:
        v.fail("T4 廠商資料", "找不到廠商")
    else:
        s4 = sup[0]
        進貨總計 = 800.0
        付款金額 = 500.0
        單號日期4 = datetime.now().strftime("%y%m%d")
        max4 = v.q1("SELECT MAX(交易單號) FROM 交易主檔 WHERE 單據類別='進貨' AND 交易單號 LIKE ?", (單號日期4 + "%",))
        seq4 = 1
        if max4 and len(max4) >= 10 and max4[6:10].isdigit():
            seq4 = int(max4[6:10]) + 1
        進貨單號 = 單號日期4 + f"{seq4:04d}"
        交易日期4 = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        沖帳前餘額4 = v.q1("SELECT COALESCE(SUM(ABS(未收付金額)),0) FROM 帳款簡要 WHERE 交易對象=?", (s4["客廠編號"],)) or 0.0
        acc4 = v.q1("SELECT 建檔序號 FROM 帳款主檔 WHERE 交易對象=?", (s4["客廠編號"],))
        acc4_existed = acc4 is not None

        # ── 準備一張未付進貨單（應付：未收付為正值）──
        with v.conn:
            進貨副碼 = (v.q1("SELECT COALESCE(MAX(單據副碼),0) FROM 交易主檔") or 0) + 1
            v.conn.execute("INSERT INTO 交易主檔 (單據類別,交易單號,單據副碼,交易日期,交易對象,倉庫編號,員工編號,發票號碼,帳款日期,備註,課稅類別,售價稅別,計算庫存,數量合計,合計金額,營業稅,總計金額,加項金額,減項金額,折讓金額,已收付金額,未收付金額,應收付金額,現金收付金額,明細總筆數,本張成本,原幣合計金額,原幣營業稅,原幣總計金額,製單) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                           ("進貨", 進貨單號, 進貨副碼, 交易日期4, s4["客廠編號"], 倉庫編號, s["員工編號"], "", 交易日期4, "", "", "", 0, 0, 0.0, 0.0, 進貨總計, 0.0, 0.0, 0.0, 0.0, 進貨總計, 進貨總計, 0.0, 1, 0.0, 0.0, 0.0, 進貨總計, "test"))
            v.conn.execute("INSERT INTO 帳款簡要 (建檔序號,單據類別,交易對象,員工編號,交易日期,交易單號,發票號碼,合計金額,營業稅,總計金額,折讓金額,現金收付金額,已收付金額,未收付金額,應收付金額) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                           ((v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 帳款簡要") or 0) + 1, "進貨", s4["客廠編號"], s["員工編號"], 交易日期4, 進貨單號, "", 0.0, 0.0, 進貨總計, 0.0, 0.0, 0.0, 進貨總計, 進貨總計))
            if acc4_existed:
                v.conn.execute("UPDATE 帳款主檔 SET 本期合計=本期合計+?, 本期總計=本期總計+? WHERE 交易對象=?",
                               (0.0, 進貨總計, s4["客廠編號"]))
            else:
                v.conn.execute("INSERT INTO 帳款主檔 (建檔序號,交易對象,公司全名,員工編號,員工姓名,統一編號,聯絡人一,聯絡電話一,傳真號碼,累計預收貨款,前期累計應收帳款,本期合計,營業稅,折讓金額,已收付金額,現金收付金額,本期總計) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                               ((v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 帳款主檔") or 0) + 1, s4["客廠編號"], s4["公司簡稱"] or "", "", "", "", "", "", "", 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 進貨總計))
        v.check(v.q1("SELECT COUNT(*) FROM 交易主檔 WHERE 單據副碼=?", (進貨副碼,)) == 1,
                "T4 進貨單建立", f"{進貨單號} 應付 {進貨總計}")

        # ── 付款沖帳（收付類別=付款，資料流同收款）──
        with v.conn:
            付副碼 = (v.q1("SELECT COALESCE(MAX(單據副碼),0) FROM 收付主檔") or 0) + 1
            maxp4 = v.q1("SELECT MAX(收付單號) FROM 收付主檔 WHERE 收付單號 LIKE ?", (單號日期4 + "%",))
            pseq4 = 1
            if maxp4 and len(maxp4) >= 10 and maxp4[6:10].isdigit():
                pseq4 = int(maxp4[6:10]) + 1
            付款單號 = 單號日期4 + f"{pseq4:04d}"
            應付餘額 = round((沖帳前餘額4 + 進貨總計) - 付款金額, 2)
            v.conn.execute("INSERT INTO 收付主檔 (收付類別,收付單號,單據副碼,沖帳日期,沖帳對象,員工編號,部門編號,現金金額,票據金額,取用預收,應收餘額,預收餘額,累入預收,銷貨折讓,現金折讓,沖帳合計,可沖餘額,傳票編號,經辦人員,專案編號) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                           ("付款", 付款單號, 付副碼, datetime.now().strftime("%Y-%m-%d %H:%M:%S"), s4["客廠編號"], s["員工編號"], "", 付款金額, 0.0, 0.0, 應付餘額, 0.0, 0.0, 0.0, 0.0, 付款金額, 0.0, "", "test", ""))
            v.conn.execute("INSERT INTO 收付明細 (單據副碼,建檔序號,單據號碼,單別,單據日期,發票編號,現行餘額,折讓金額,沖帳金額) VALUES (?,?,?,?,?,?,?,?,?)",
                           (付副碼, (v.q1("SELECT COALESCE(MAX(建檔序號),0) FROM 收付明細") or 0) + 1, 進貨單號, "進貨", 交易日期4, "", 進貨總計, 0.0, 付款金額))
            v.conn.execute("UPDATE 帳款簡要 SET 已收付金額=已收付金額+?, 未收付金額=未收付金額-? WHERE 交易單號=? AND 單據類別=?",
                           (付款金額, 付款金額, 進貨單號, "進貨"))
            v.conn.execute("UPDATE 交易主檔 SET 已收付金額=已收付金額+?, 未收付金額=未收付金額-? WHERE 交易單號=? AND 單據類別=?",
                           (付款金額, 付款金額, 進貨單號, "進貨"))
            v.conn.execute("UPDATE 帳款主檔 SET 已收付金額=已收付金額+? WHERE 交易對象=?",
                           (付款金額, s4["客廠編號"]))

        # ── T4 付款驗證 ──
        v.check(v.q1("SELECT COUNT(*) FROM 收付主檔 WHERE 單據副碼=? AND 收付類別='付款'", (付副碼,)) == 1,
                "T4 付款主檔", f"{付款單號} 已建立")
        v.check(v.q1("SELECT COUNT(*) FROM 收付明細 WHERE 單據副碼=? AND 單據號碼=? AND 單別='進貨'", (付副碼, 進貨單號)) == 1,
                "T4 收付明細關聯", "單據號碼+單別（進貨）正確")
        未付4 = v.q1("SELECT 未收付金額 FROM 帳款簡要 WHERE 交易單號=?", (進貨單號,))
        v.check(abs(未付4 - (進貨總計 - 付款金額)) < 1e-9, "T4 應付未付遞減", f"{未付4} == {進貨總計 - 付款金額}（部分付款）")
        主未付4 = v.q1("SELECT 未收付金額 FROM 交易主檔 WHERE 單據副碼=?", (進貨副碼,))
        v.check(abs(主未付4 - (進貨總計 - 付款金額)) < 1e-9, "T4 交易主檔同步", f"{主未付4} == {進貨總計 - 付款金額}")
        帳款主已付4 = v.q1("SELECT 已收付金額 FROM 帳款主檔 WHERE 交易對象=?", (s4["客廠編號"],))
        v.check(abs(帳款主已付4 - 付款金額) < 1e-9, "T4 帳款主檔已收付", f"{帳款主已付4} == {付款金額}")

        # ── 撤銷付款（反向回復）──
        with v.conn:
            v.conn.execute("UPDATE 帳款簡要 SET 已收付金額=已收付金額-?, 未收付金額=未收付金額+? WHERE 交易單號=? AND 單據類別=?",
                           (付款金額, 付款金額, 進貨單號, "進貨"))
            v.conn.execute("UPDATE 交易主檔 SET 已收付金額=已收付金額-?, 未收付金額=未收付金額+? WHERE 交易單號=? AND 單據類別=?",
                           (付款金額, 付款金額, 進貨單號, "進貨"))
            v.conn.execute("UPDATE 帳款主檔 SET 已收付金額=已收付金額-? WHERE 交易對象=?",
                           (付款金額, s4["客廠編號"]))
            v.conn.execute("DELETE FROM 收付明細 WHERE 單據副碼=?", (付副碼,))
            v.conn.execute("DELETE FROM 收付主檔 WHERE 單據副碼=?", (付副碼,))

        # ── T4 撤銷驗證 ──
        v.check(v.q1("SELECT COUNT(*) FROM 收付主檔 WHERE 單據副碼=?", (付副碼,)) == 0,
                "T4 撤銷-收付清除", "0 筆")
        未付4r = v.q1("SELECT 未收付金額 FROM 帳款簡要 WHERE 交易單號=?", (進貨單號,))
        v.check(abs(未付4r - 進貨總計) < 1e-9, "T4 撤銷-應付回復", f"{未付4r} == {進貨總計}")

        # ── 清理 T4 進貨單 ──
        with v.conn:
            if acc4_existed:
                v.conn.execute("UPDATE 帳款主檔 SET 本期合計=本期合計-?, 本期總計=本期總計-? WHERE 交易對象=?",
                               (0.0, 進貨總計, s4["客廠編號"]))
            else:
                v.conn.execute("DELETE FROM 帳款主檔 WHERE 交易對象=?", (s4["客廠編號"],))
            v.conn.execute("DELETE FROM 帳款簡要 WHERE 交易單號=?", (進貨單號,))
            v.conn.execute("DELETE FROM 交易主檔 WHERE 單據副碼=?", (進貨副碼,))
        v.check(v.q1("SELECT COUNT(*) FROM 交易主檔") == 交易主檔_before, "T4 清理-主檔總數回復", f"回到 {交易主檔_before}")

    # ════════════ 收尾 ════════════
    v.conn.close()
    os.remove(WORK_DB)
    v.lines.append("")
    v.lines.append(f"=== 總計: {v.pass_n} PASS / {v.fail_n} FAIL ===")
    with open(RESULT, "w", encoding="utf-8") as f:
        f.write("\n".join(v.lines))
    print(f"done: {v.pass_n} PASS / {v.fail_n} FAIL → {RESULT}")
    return v.fail_n


if __name__ == "__main__":
    sys.exit(main())
