# -*- coding: utf-8 -*-
"""중립 몬스터 표에 <b>체력 배율</b>(`hp_percent`) 컬럼을 신설한다 (2026-08-21).

유저 지시: *"중립 몬스터에게도 체력 배율 추가 해야될듯 특히 보스 몬스터 칼럼 추가하고
테이블에도 추가해줘"*.

★ <b>어느 시트인가</b> — `first_Stat` 이다. 체력(`hp`)이 그 시트에 있고, 배율은 그 값을
  읽는 사람 바로 옆에 있어야 «기본 체력 x 배율» 이 한눈에 보인다.
  (`neutrality_mon` 은 식별·등장범위·보상·개체수를 맡는 시트다.)

★ <b>씨앗값은 전부 100</b> = «배율 없음». 지금 게임 동작을 <b>한 톨도 바꾸지 않는다</b> —
  균형 수치는 기획이 정하는 값이라 코드가 지어내지 않는다. 보스 체력을 4배로 하고 싶으면
  1101~1104 행의 이 칸에 <b>400</b> 을 적으면 된다.

⚠ 편집은 <b>Excel COM · DispatchEx</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다
  (UI-17절 실사고). `DispatchEx` 는 유저가 엑셀을 켜 두었을 때도 안전하다(112-7절 함정 2).

사용법:  py -3 Tools/table_update_20260821_neutral_hp_percent.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")
SHEET = "first_Stat"

#: (표시용 한글 헤더, 필드명, 타입 라벨)
NEW_COLUMN = ("체력 배율(%)", "hp_percent", "int")

#: 모든 행의 씨앗값 — 100 = 배율 없음(위 ★).
SEED = 100


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_중립체력배율컬럼")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(NEUTRAL_XLSX, os.path.join(dst, os.path.basename(NEUTRAL_XLSX)))
    print("백업:", dst)


def find_col(ws, field, max_col=40):
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

    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    changed = 0
    try:
        wb = excel.Workbooks.Open(NEUTRAL_XLSX)
        ws = wb.Worksheets(SHEET)

        header_kr, field, type_label = NEW_COLUMN
        col = find_col(ws, field)
        if col:
            print(f"  (이미 있음) {field} → 열 {col}")
        else:
            col = ws.UsedRange.Columns.Count + 1
            ws.Cells(1, col).Value = header_kr
            ws.Cells(2, col).Value = field
            ws.Cells(3, col).Value = type_label
            print(f"  + {field} 컬럼 신설 (열 {col})")
            changed += 1

        c_id = find_col(ws, "mon_id") or 1
        last = ws.UsedRange.Rows.Count
        for r in range(4, last + 1):
            mid = ws.Cells(r, c_id).Value
            if mid is None:
                continue
            cur = ws.Cells(r, col).Value
            if cur is not None and str(cur).strip() != "":
                print(f"  (이미 있음) {int(mid)} hp_percent = {cur}")
                continue
            ws.Cells(r, col).Value = SEED
            print(f"  {int(mid)} hp_percent = {SEED}")
            changed += 1

        if changed:
            wb.Save()
            print("저장 완료 -", changed, "칸")
        else:
            print("변경 없음")
        wb.Close(SaveChanges=False)
    finally:
        excel.Quit()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
