# -*- coding: utf-8 -*-
"""重建採訂主檔/明細主鍵（dump 併表錯誤）：
採訂主檔: 單據類別 -> 單據副碼
採訂明細: 單據副碼 -> (單據副碼, 建檔序號)
用法: python fix_pobill_pk.py [--db 路徑] [--no-backup]
預設 db: D:/HeliAcc/HeliERP.db（先備份 .bak-時間戳）
"""
import sqlite3, sys, shutil, datetime

DB = r'D:/HeliAcc/HeliERP.db'
args = sys.argv[1:]
if '--db' in args:
    DB = args[args.index('--db') + 1]
backup = '--no-backup' not in args

FIXES = [
    ('採訂主檔', ['單據副碼'], [
        'CREATE INDEX "ix_採訂主檔_單據類別" ON "採訂主檔" ("單據類別")',
        'CREATE INDEX "ix_採訂主檔_交易單號" ON "採訂主檔" ("交易單號")',
        'CREATE UNIQUE INDEX "ix_採訂主檔_單據類別_交易單號" ON "採訂主檔" ("單據類別", "交易單號")',
    ]),
    ('採訂明細', ['單據副碼', '建檔序號'], [
        'CREATE INDEX "ix_採訂明細_貨品編號" ON "採訂明細" ("貨品編號")',
        'CREATE INDEX "ix_採訂明細_單據副碼" ON "採訂明細" ("單據副碼")',
        'CREATE UNIQUE INDEX "ix_採訂明細_單據副碼_建檔序號" ON "採訂明細" ("單據副碼", "建檔序號")',
    ]),
]

def q(conn, sql, *p):
    return conn.execute(sql, p).fetchall()

conn = sqlite3.connect(DB)
ik = conn.execute('PRAGMA integrity_check').fetchone()[0]
assert ik == 'ok', f'integrity_check 失敗: {ik}'
print(f'資料庫: {DB}')
need_fix = False
for (table, new_pk, _) in FIXES:
    info = q(conn, f'PRAGMA table_info("{table}")')
    cur_pk = [c[1] for c in info if c[5] > 0]
    cnt = conn.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0]
    print(f'{table}: {cnt} 筆, 目前主鍵 {cur_pk}')
    if cur_pk != new_pk:
        need_fix = True
if not need_fix:
    print('主鍵均已正確，無需重建。')
    conn.close()
    sys.exit(0)

if backup:
    bak = f'{DB}.bak-{datetime.datetime.now():%Y%m%d-%H%M%S}'
    shutil.copy2(DB, bak)
    print(f'已備份: {bak}')

conn.execute('PRAGMA foreign_keys=OFF')
conn.execute('BEGIN')
for (table, new_pk, keep_idx) in FIXES:
    info = q(conn, f'PRAGMA table_info("{table}")')
    cols = [r[1] for r in info]
    cur_pk = [c[1] for c in info if c[5] > 0]
    if cur_pk == new_pk:
        print(f'{table}: 主鍵已正確，略過')
        continue
    coldefs = ', '.join(f'"{c}" {next((r[2] or "TEXT" for r in info if r[1] == c), "TEXT")}' for c in cols)
    colsql = ', '.join(f'"{c}"' for c in cols)
    pkdef = ', '.join(f'"{p}"' for p in new_pk)
    tmp = f'__fix_{table}'
    conn.execute(f'CREATE TABLE "{tmp}" ({coldefs}, PRIMARY KEY ({pkdef}))')
    conn.execute(f'INSERT INTO "{tmp}" ({colsql}) SELECT {colsql} FROM "{table}"')
    conn.execute(f'DROP TABLE "{table}"')
    conn.execute(f'ALTER TABLE "{tmp}" RENAME TO "{table}"')
    for idx in keep_idx:
        conn.execute(idx)
    cnt = conn.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0]
    print(f'{table}: 主鍵 -> {new_pk}, 筆數 {cnt}')
conn.execute('COMMIT')
conn.execute('PRAGMA foreign_keys=ON')

# 事後驗證
for (table, new_pk, _) in FIXES:
    cnt = conn.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0]
    info2 = q(conn, f'PRAGMA table_info("{table}")')
    pk2 = [c[1] for c in info2 if c[5] > 0]
    assert pk2 == new_pk, f'{table} 主鍵驗證失敗: {pk2}'
    print(f'{table}: 主鍵={pk2}, 筆數={cnt}')
ik2 = conn.execute('PRAGMA integrity_check').fetchone()[0]
assert ik2 == 'ok', f'integrity_check 失敗: {ik2}'
print(f'integrity={ik2}')
conn.close()
print('OK')
