# -*- coding: utf-8 -*-
"""신규 캐릭터 2인(아르세니아 9010 · 불칸 9011) 표 정비 (2026-08-20, 2차).

유저 지시: *"불칸이랑 아르세니아 구현 … 캐릭터 테이블에 추가한 스트링 값들 너가 연동해서
스트링 키로 만들어줘"*

119절의 3인 정비와 **같은 종류의 작업**이고, 이번에도 문구를 키로 옮기기 **전에**
표 자체에서 고쳐야 하는 것이 있다.

★★ ① `Skill_Type` 시트의 **enum 칸에 한글 이름이 들어가 있었다**
----------------------------------------------------------------
아르세니아의 세 줄만 그렇다:

    Skill 시트        Skill_Type 시트
    Instability          →  「불안정성」          ← enum 이 아니라 한글 이름
    Sacred_blessing      →  「성스러운 축복」
    Unfinished_nobility  →  「완성되지 못한 고귀함」

`gen_character_assets.py` 는 **`Skill` 시트의 값으로** `Skill_Type` 을 찾으므로
(`skill_types.get(stype)`), 이대로면 세 스킬의 **효과 설명이 빈 문자열**로 나간다.
119-1절에서 카이론의 `Celestial_shield` 가 대소문자 때문에 겪은 것과 <b>같은 사고</b>다.
→ `Skill` 시트 쪽(enum)으로 맞춘다.

⚠ 불칸의 세 줄은 이미 enum 이다(`Blazing_anger`·`The_wisdom_of_a_sage`·`Flame_blast`).

★ ② 스킬 아이콘 6칸이 비어 있었다
---------------------------------
이미 쓰는 43개를 피해 골랐다 — 규칙은 늘 같다: **그림이 스킬을 설명해야 한다**.

그리고 스트링 키 테이블에 **직접 적어야만 하는 값**(119-2절과 같은 이유)
----------------------------------------------------------------------
`character_name_9010`·`character_name_9011` 의 **en** — ⚠ `gen_character_assets.py` 가
이 값으로 **에셋 파일 이름**(`Character_9010_Arsenia`)을 만든다. 비면 스크립트가 멈춘다.

⚠ 칭호는 이번엔 손댈 것이 없다 — 유저가 `character_title` 에 <b>한글 리터럴</b>을 적어 뒀고
   `character_title_EG` 에 영어가 있어서 수집기가 kr·en 을 <b>둘 다</b> 만든다.

⚠ 편집은 **Excel COM(`DispatchEx`)** — 119-2절 참조(`EnsureDispatch` 는 유저가 엑셀을
   열어 두면 `RPC_E_CALL_REJECTED` 로 죽는다).

실행 순서
---------
    py -3 Tools/table_update_20260820_arsenia_vulcan.py
    py -3 Tools/gen_string_table.py
    py -3 Tools/table_update_20260820_arsenia_vulcan.py --strings-only
    py -3 Tools/gen_string_table.py
    py -3 Tools/convert_tables_to_string_keys.py
    py -3 Tools/link_string_keys.py
    py -3 Tools/gen_character_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

BACKUP_ROOT = os.path.join(TABLES, "_백업")
CHAR_XLSX = "캐릭터 테이블.xlsx"
STRING_XLSX = "스트링 키 테이블.xlsx"
FIRST_DATA_ROW = 4

#: ① `Skill_Type` 시트의 enum 칸 교정 — (옛값, 새값). `Skill` 시트가 정본이다.
ENUM_FIXES = [
    ("불안정성", "Instability"),
    ("성스러운 축복", "Sacred_blessing"),
    ("완성되지 못한 고귀함", "Unfinished_nobility"),
]

#: ② 스킬 아이콘 (`icon_` 접두사 없이).
ICONS = {
    80028: "arcane_aura",   # Instability 불안정성 — 마법·회복에도 명중/크리가 걸린다(마법 아우라)
    80029: "holy_light",    # Sacred_blessing 성스러운 축복 — 바닥에 깔리는 성스러운 빛의 공간
    80030: "angel_ascend",  # Unfinished_nobility — 원화 그대로 «천사가 강림하며 레이저 낙하»
    80031: "flame_burst",   # Blazing_anger 타오르는 분노 — 적을 불태우는 지속 화상
    80032: "spell_tome",    # The_wisdom_of_a_sage 현자의 지혜 — 마법·공속 영구 상승
    80033: "comet_fall",    # Flame_blast 화염 세례 — 거대 화염구가 떨어진다
}

#: 영어 이름 — 에셋 파일 이름에 쓰인다.
NAMES_EN = {
    9010: "Arsenia",
    9011: "Vulcan",
}


def check_locks(files):
    locked = [f for f in files if os.path.isfile(os.path.join(TABLES, "~$" + f))]
    if locked:
        raise SystemExit("⚠ 엑셀에서 열려 있는 파일이 있습니다 — 닫고 다시 실행하세요:\n   "
                         + "\n   ".join(locked))


def backup(files, tag):
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_" + tag)
    os.makedirs(dst, exist_ok=True)
    for f in files:
        src = os.path.join(TABLES, f)
        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(dst, f))
    print("백업:", dst)


def find_col(ws, field, max_col=32):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def fix_character_table(excel):
    wb = excel.Workbooks.Open(os.path.join(TABLES, CHAR_XLSX))
    changed = 0
    try:
        # ── Skill_Type : enum 칸 교정 ─────────────────────────────────
        ws = wb.Worksheets("Skill_Type")
        last = ws.UsedRange.Rows.Count
        col = find_col(ws, "skill_type")
        if not col:
            raise SystemExit("⚠ Skill_Type 시트에서 skill_type 컬럼을 못 찾았습니다.")

        print("\n  [Skill_Type · enum 교정]")
        wanted = dict(ENUM_FIXES)
        for r in range(FIRST_DATA_ROW, last + 1):
            v = ws.Cells(r, col).Value
            key = str(v).strip() if v is not None else ""
            if key in wanted:
                ws.Cells(r, col).Value = wanted[key]
                print("    %d행  %s → %s" % (r, key, wanted[key]))
                changed += 1

        if changed == 0:
            print("    (이미 정리됨)")

        # ── Skill : 아이콘 ────────────────────────────────────────────
        ws = wb.Worksheets("Skill")
        last = ws.UsedRange.Rows.Count
        c_id = find_col(ws, "skill_id")
        c_icon = find_col(ws, "skill_icon")
        c_type = find_col(ws, "skill_type")
        if not (c_id and c_icon):
            raise SystemExit("⚠ Skill 시트에서 컬럼을 못 찾았습니다.")

        print("\n  [Skill · 아이콘]")
        for r in range(FIRST_DATA_ROW, last + 1):
            v = ws.Cells(r, c_id).Value
            if v is None:
                continue
            sid = int(float(v))
            if sid not in ICONS:
                continue
            old = ws.Cells(r, c_icon).Value
            new = "icon_" + ICONS[sid]
            if str(old or "").strip() == new:
                continue
            ws.Cells(r, c_icon).Value = new
            stype = ws.Cells(r, c_type).Value if c_type else ""
            print("    %-6d %-24s %-14s → %s"
                  % (sid, str(stype).strip(), str(old) if old else "(비어 있었다)", new))
            changed += 1

        wb.Save()
    finally:
        wb.Close()
    return changed


def fill_string_table(excel):
    """수집으로는 안 생기는 칸(영어 이름)을 채운다. ⚠ 이미 값이 있으면 건드리지 않는다."""
    wb = excel.Workbooks.Open(os.path.join(TABLES, STRING_XLSX))
    changed = 0
    try:
        ws = wb.Worksheets("string")
        last = ws.UsedRange.Rows.Count
        c_key = find_col(ws, "string_key")
        c_en = find_col(ws, "en")
        if not (c_key and c_en):
            raise SystemExit("⚠ string 시트에서 컬럼을 못 찾았습니다.")

        wanted = {"character_name_%d" % cid: en for cid, en in NAMES_EN.items()}
        print("\n  [스트링 키 테이블]")
        found = set()
        for r in range(FIRST_DATA_ROW, last + 1):
            v = ws.Cells(r, c_key).Value
            key = str(v).strip() if v is not None else ""
            if key not in wanted:
                continue
            found.add(key)
            cur = ws.Cells(r, c_en).Value
            cur = str(cur).strip() if cur is not None else ""
            if cur == wanted[key]:
                continue
            if cur:
                print("    · %-24s en 이미 \"%s\" — 건드리지 않습니다" % (key, cur))
                continue
            ws.Cells(r, c_en).Value = wanted[key]
            print("    %-24s en ← %s" % (key, wanted[key]))
            changed += 1

        missing = sorted(set(wanted) - found)
        if missing:
            print("    ⚠ 아직 표에 없는 키 — `gen_string_table.py` 를 먼저 돌리세요: "
                  + ", ".join(missing))

        wb.Save()
    finally:
        wb.Close()
    return changed


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    strings_only = "--strings-only" in sys.argv
    files = [STRING_XLSX] if strings_only else [CHAR_XLSX, STRING_XLSX]
    check_locks(files)

    if not strings_only:
        project = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        icon_dir = os.path.join(project, "Assets", "_Project", "Resources", "SkillIcons")
        missing = [n for n in sorted(set(ICONS.values()))
                   if not os.path.isfile(os.path.join(icon_dir, "icon_%s.png" % n))]
        if missing:
            raise SystemExit("⚠ 아이콘 파일이 없습니다: " + ", ".join(missing))

    import win32com.client as win32
    backup(files, "아르세니아불칸" + ("_문구만" if strings_only else ""))

    # ⚠ DispatchEx — 119-2절 참조.
    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    total = 0
    try:
        if not strings_only:
            total += fix_character_table(excel)
        total += fill_string_table(excel)
    finally:
        excel.Quit()

    print("\n== 고친 칸 %d개" % total)


if __name__ == "__main__":
    main()
