# -*- coding: utf-8 -*-
"""유저 결정 두 건을 표에 적는다 (2026-08-21).

① <b>폴리르(1104) 이름·칭호</b> — 스트링 키 테이블의 빈 칸을 채운다.
   유저 결정: 이름 <b>폴리르</b> · 칭호 <b>영원한 숙적</b>.
   (`gen_string_table.py` 가 키만 만들어 두고 값은 사람이 적게 비워 둔 상태였다.)

② <b>「도움의 손길」(80022) 쿨타임 0 → 30</b> — 유저 결정.
   122절이 «표 30» 으로 보고 코드를 쿨타임 스킬로 옮겼는데 표에는 0 이 들어 있었다.
   표를 30 으로 확정한다(코드는 이미 이 칸을 읽는다).

⚠ 편집은 Excel COM · DispatchEx (openpyxl 로 저장하면 하이퍼링크가 날아간다 · UI-17절).

사용법:  py -3 Tools/table_update_20260821_polyir_names_and_cooltime.py
다음:    py -3 Tools/gen_string_table.py  그리고  py -3 Tools/gen_character_assets.py
"""

import datetime
import os
import shutil

from vault_path import TABLE_DIR as TABLES

STRING_XLSX = os.path.join(TABLES, "스트링 키 테이블.xlsx")
CHAR_XLSX = os.path.join(TABLES, "캐릭터 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

#: 스트링 키 → 한국어 값
STRINGS = {
    "mon_name_1104": "폴리르",
    "epic_boss_title_1104": "영원한 숙적",
}

#: 캐릭터 표 Skill 시트 — skill_id → cool_time
COOLTIMES = {80022: 30}


def backup(paths, tag):
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_" + tag)
    os.makedirs(dst, exist_ok=True)
    for p in paths:
        shutil.copy2(p, os.path.join(dst, os.path.basename(p)))
    print("backup:", dst)


def find_col(ws, field, max_col=40):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def fill_strings(wb):
    ws = wb.Worksheets("string")
    c_key = find_col(ws, "string_key") or 1
    c_kr = find_col(ws, "kr") or 2

    last = ws.UsedRange.Rows.Count
    changed = 0
    for r in range(4, last + 1):
        key = ws.Cells(r, c_key).Value
        if key is None:
            continue
        key = str(key).strip()
        if key not in STRINGS:
            continue
        cur = ws.Cells(r, c_kr).Value
        cur = "" if cur is None else str(cur).strip()
        want = STRINGS[key]
        if cur == want:
            print("  already ok:", key, "=", want)
            continue
        ws.Cells(r, c_kr).Value = want
        print("  ", key, ":", repr(cur), "->", want)
        changed += 1
    return changed


def fill_cooltimes(wb):
    ws = wb.Worksheets("Skill")
    c_id = find_col(ws, "skill_id") or 1
    c_cool = find_col(ws, "cool_time")
    if c_cool == 0:
        print("  ! cool_time column not found")
        return 0

    last = ws.UsedRange.Rows.Count
    changed = 0
    for r in range(4, last + 1):
        sid = ws.Cells(r, c_id).Value
        if sid is None:
            continue
        try:
            sid = int(sid)
        except (TypeError, ValueError):
            continue
        if sid not in COOLTIMES:
            continue
        want = COOLTIMES[sid]
        cur = ws.Cells(r, c_cool).Value
        if cur is not None and float(cur) == float(want):
            print("  already ok:", sid, "cool", want)
            continue
        ws.Cells(r, c_cool).Value = want
        print("  ", sid, "cool_time:", cur, "->", want)
        changed += 1
    return changed


def main():
    for p in (STRING_XLSX, CHAR_XLSX):
        if not os.path.isfile(p):
            print("file missing:", p)
            return 1

    import win32com.client as win32

    backup((STRING_XLSX, CHAR_XLSX), "폴리르이름_도움의손길쿨")

    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    try:
        print("[스트링 키 테이블]")
        wb = excel.Workbooks.Open(STRING_XLSX)
        n = fill_strings(wb)
        if n:
            wb.Save()
        wb.Close(SaveChanges=False)
        print("  changed", n)

        print("[캐릭터 테이블]")
        wb = excel.Workbooks.Open(CHAR_XLSX)
        n = fill_cooltimes(wb)
        if n:
            wb.Save()
        wb.Close(SaveChanges=False)
        print("  changed", n)
    finally:
        excel.Quit()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
