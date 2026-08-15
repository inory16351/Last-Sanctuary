# -*- coding: utf-8 -*-
"""카르시노스(1004) 이름을 스트링 키로 정리한다 (2026-08-15).

유저 지시: *"테이블 확인해서 스트링 키 정리하고 카르시노스 만들어줘"*

무엇을 고치나
-------------
표 감사 결과 <b>리터럴로 남아 있는 문구는 딱 하나</b>였다:

    임시용 중립 몬스터.xlsx / neutrality_mon / 1004 / mon_name = "카르시노스"

(건물 시트의 `Information`·`Docs`·`DEF`·`Hp` 에도 한글이 있지만 그건 <b>기획 산문</b>이라
 `gen_string_table.py` 가 수집 대상에서 명시적으로 빼둔 것들이다 — 건드리지 않는다.)

⚠ <b>`convert_tables_to_string_keys.py` 로는 못 고친다.</b> 그 스크립트는 안전을 위해
   "칸의 리터럴이 스트링 테이블의 kr 과 <b>같을 때만</b>" 키로 바꾸는데, `mon_name_1004` 의
   kr 은 지금 <b>"역겨운 모체"</b> 다 — 1004 를 처음 채울 때 이름이 없어서 임시로 지어낸
   값이다. 유저가 표에 "카르시노스" 라고 적어 정본을 준 것이므로, <b>스트링 테이블 쪽을
   먼저 고치고</b> 표를 키로 바꾼다. 순서가 반대면 "역겨운 모체" 가 살아남는다.

⚠ `gen_string_table.py` 는 <b>기존 우선</b> 병합이라(사람이 다듬은 번역을 덮지 않는다)
   이 정정을 대신 해주지 않는다. 그래서 이 스크립트가 필요하다.

⚠ 편집은 <b>Excel COM</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17 절 실사고).

사용법:  python Tools/table_update_20260815_carcinos.py
"""

import os
import sys
import shutil
import datetime

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
STRING_XLSX = os.path.join(TABLES, "스트링 키 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

MON_ID = 1004
NAME_KEY = "mon_name_%d" % MON_ID
NAME_KR = "카르시노스"
NAME_EN = "Carcinos"


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_카르시노스스트링키")
    os.makedirs(dst, exist_ok=True)
    for src in (NEUTRAL_XLSX, STRING_XLSX):
        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(dst, os.path.basename(src)))
    print("백업:", dst)


def find_col(ws, field, max_col=64):
    """2행(필드명)에서 컬럼 번호. ⚠ 앞뒤 공백을 반드시 제거한다(표에 실제로 섞여 있다)."""
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def row_of(ws, id_value, first_row=4, max_row=400):
    for r in range(first_row, max_row + 1):
        v = ws.Cells(r, 1).Value
        if v is None:
            continue
        try:
            if int(v) == int(id_value):
                return r
        except (TypeError, ValueError):
            continue
    return 0


def main():
    for p in (NEUTRAL_XLSX, STRING_XLSX):
        if not os.path.isfile(p):
            print("⚠ 파일 없음:", p)
            return 1

    import win32com.client as win32

    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    try:
        # ── ① 스트링 키 테이블을 <b>먼저</b> 정본으로 만든다 ─────────────────
        wb = excel.Workbooks.Open(STRING_XLSX)
        ws = wb.Worksheets("string")
        last = ws.UsedRange.Rows.Count

        found = 0
        for r in range(4, last + 1):
            key = ws.Cells(r, 1).Value
            if key is None or str(key).strip() != NAME_KEY:
                continue
            found = r
            before = ws.Cells(r, 2).Value
            if str(before or "").strip() != NAME_KR:
                ws.Cells(r, 2).Value = NAME_KR
                print(f"  {NAME_KEY} kr: '{before}' → '{NAME_KR}'")
            else:
                print(f"  (이미 맞음) {NAME_KEY} = {NAME_KR}")
            # 영어 칸은 대부분 비어 있지만, 아는 값이 있으면 채워 둔다.
            if not str(ws.Cells(r, 3).Value or "").strip():
                ws.Cells(r, 3).Value = NAME_EN
                print(f"  {NAME_KEY} en → {NAME_EN}")
            break

        if not found:
            last += 1
            ws.Cells(last, 1).Value = NAME_KEY
            ws.Cells(last, 2).Value = NAME_KR
            ws.Cells(last, 3).Value = NAME_EN
            ws.Cells(last, 4).Value = "neutrality_mon.mon_name"
            print(f"  + {NAME_KEY} = {NAME_KR}")

        wb.Save()
        wb.Close()
        print("스트링 키 테이블 저장")

        # ── ② 원본 표의 리터럴을 키로 ────────────────────────────────────────
        wb = excel.Workbooks.Open(NEUTRAL_XLSX)
        wn = wb.Worksheets("neutrality_mon")
        c_name = find_col(wn, "mon_name")
        if not c_name:
            print("⚠ mon_name 컬럼을 못 찾음")
            wb.Close(False)
            return 1

        r = row_of(wn, MON_ID)
        if not r:
            print("⚠ neutrality_mon 에 1004 행이 없다")
            wb.Close(False)
            return 1

        cur = str(wn.Cells(r, c_name).Value or "").strip()
        if cur == NAME_KEY:
            print(f"  (이미 키) {MON_ID} mon_name = {NAME_KEY}")
        else:
            wn.Cells(r, c_name).Value = NAME_KEY
            print(f"  {MON_ID} mon_name: '{cur}' → {NAME_KEY}")

        wb.Save()
        wb.Close()
        print("임시용 중립 몬스터.xlsx 저장")

    finally:
        excel.Quit()

    print("\n완료 — 다음을 순서대로 돌릴 것:")
    print("  python Tools/gen_string_table.py")
    print("  python Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
