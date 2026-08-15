# -*- coding: utf-8 -*-
"""중립 몬스터 표에 <b>스킬 배정 칸</b>을 만든다 (2026-08-15).

유저 지시: *"에픽 몬스터 스킬 구현(카르시노스)"*

무엇이 없었나
-------------
`임시용 중립 몬스터.xlsx` 의 `Skill` 시트에는 <b>2001 할퀴기 · 2002 죽음의 포효</b>가
이미 적혀 있었지만, **어느 몬스터가 그 스킬을 쓰는지 적는 칸이 없었다.**
웨이브 보스는 `wave_top_boss` 에 `boss_skill_1~3` 칸이 있어 그 번호로 연결한다 —
중립도 <b>같은 방식·같은 이름 규칙</b>으로 맞춘다.

    neutrality_mon 에 `mon_skill_1` · `mon_skill_2` 를 <b>맨 뒤에</b> 붙인다
    1004 → 2001, 2002

⚠ <b>컬럼은 반드시 맨 뒤에 붙인다</b>(UI-18 규약). 중간에 끼우면 뒤 컬럼이 밀려
  위치로 읽던 코드가 조용히 엉뚱한 값을 읽는다(UI-23 에서 실제로 사고가 났다).
⚠ 편집은 <b>Excel COM</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다.

사용법:  py -3 Tools/table_update_20260815_neutral_skill_cols.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")
SHEET = "neutrality_mon"

#: (필드명, 1행 한글 라벨, 3행 자료형)
NEW_COLS = [
    ("mon_skill_1", "스킬 1 id", "int"),
    ("mon_skill_2", "스킬 2 id", "int"),
]

#: mon_id → (스킬1, 스킬2). 0 이면 없음.
ASSIGN = {1004: (2001, 2002)}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_중립스킬칸")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(NEUTRAL_XLSX, os.path.join(dst, os.path.basename(NEUTRAL_XLSX)))
    print("백업:", dst)


def main():
    if not os.path.isfile(NEUTRAL_XLSX):
        print("⚠ 파일 없음:", NEUTRAL_XLSX)
        return 1

    import win32com.client as win32

    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    try:
        wb = excel.Workbooks.Open(NEUTRAL_XLSX)
        ws = wb.Worksheets(SHEET)

        # 2행(필드명)에서 이미 있는 컬럼과 마지막 컬럼을 찾는다.
        existing = {}
        last_col = 0
        for c in range(1, 64):
            v = ws.Cells(2, c).Value
            if v is None or str(v).strip() == "":
                continue
            existing[str(v).strip()] = c
            last_col = max(last_col, c)

        cols = {}
        for field, label, kind in NEW_COLS:
            if field in existing:
                cols[field] = existing[field]
                print(f"  (이미 있음) {field} = {existing[field]}열")
                continue
            last_col += 1
            ws.Cells(1, last_col).Value = label
            ws.Cells(2, last_col).Value = field
            ws.Cells(3, last_col).Value = kind
            cols[field] = last_col
            print(f"  + {field} 컬럼 신설 ({last_col}열)")

        last_row = ws.UsedRange.Rows.Count
        for r in range(4, last_row + 1):
            v = ws.Cells(r, 1).Value
            if v is None:
                continue
            try:
                mid = int(v)
            except (TypeError, ValueError):
                continue
            if mid not in ASSIGN:
                continue

            for (field, _, _), want in zip(NEW_COLS, ASSIGN[mid]):
                col = cols[field]
                cur = ws.Cells(r, col).Value
                cur_i = int(cur) if isinstance(cur, (int, float)) else 0
                if cur_i == want:
                    print(f"  (이미 맞음) {mid} {field} = {want}")
                    continue
                ws.Cells(r, col).Value = want
                print(f"  {mid} {field}: {cur_i or '(빈칸)'} → {want}")

        wb.Save()
        wb.Close()
        print("임시용 중립 몬스터.xlsx 저장")
    finally:
        excel.Quit()

    print("\n다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
