# -*- coding: utf-8 -*-
"""카르시노스(1004)의 서식지 타일을 표에 적는다 (2026-08-15).

유저 지시: *"서식지 타일 에셋 만들어서 넣고 해당 에셋 적용한 다음 테이블에 넣어줘."*

`임시용 중립 몬스터.xlsx` 의 **`habitat_design` 시트**가 그 자리다 — 컬럼
(`mon_id` · `habitat_tile_asset`)은 처음부터 있었는데 <b>한 줄도 채워져 있지 않았다</b>.

    1004 → CarcinosHabitat      → Resources/HabitatTiles/CarcinosHabitat/ 의 타일 32종

⚠ 편집은 <b>Excel COM</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17 절 실사고).

사용법:  py -3 Tools/table_update_20260815_habitat.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")
SHEET = "habitat_design"

#: mon_id → habitat_tile_asset
ROWS = {1004: "CarcinosHabitat"}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_서식지타일")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(NEUTRAL_XLSX, os.path.join(dst, os.path.basename(NEUTRAL_XLSX)))
    print("백업:", dst)


def find_col(ws, field, max_col=32):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


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

        c_id = find_col(ws, "mon_id") or 1
        c_tile = find_col(ws, "habitat_tile_asset")
        if not c_tile:
            print("⚠ habitat_tile_asset 컬럼을 못 찾음")
            wb.Close(False)
            return 1

        # 이미 있는 행을 찾고, 없으면 뒤에 붙인다.
        last = ws.UsedRange.Rows.Count
        rows = {}
        for r in range(4, last + 1):
            v = ws.Cells(r, c_id).Value
            if v is None:
                continue
            try:
                rows[int(v)] = r
            except (TypeError, ValueError):
                continue

        for mid, tile in ROWS.items():
            r = rows.get(mid)
            if r is None:
                last += 1
                r = last
                ws.Cells(r, c_id).Value = mid
                print(f"  + {mid} 행 신설 (행 {r})")

            cur = str(ws.Cells(r, c_tile).Value or "").strip()
            if cur == tile:
                print(f"  (이미 맞음) {mid} habitat_tile_asset = {tile}")
            else:
                ws.Cells(r, c_tile).Value = tile
                print(f"  {mid} habitat_tile_asset: '{cur}' → '{tile}'")

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
