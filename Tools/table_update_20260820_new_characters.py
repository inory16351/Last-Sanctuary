# -*- coding: utf-8 -*-
"""신규 캐릭터 3인(시카리아 9007 · 아루 9008 · 카이론 9009) 표 정비 (2026-08-20).

유저 지시
---------
*"캐릭터 세명 구현 해줘 카이론, 아루, 시카리아. 그리고 테이블에 스트링 으로 적은 값들
스트링 키 테이블로 연동해서 옮겨줘."*

유저가 표에 세 인물을 손으로 적어 넣은 상태였다. 문구를 스트링 키로 옮기기 **전에**
표 자체에서 고쳐야 하는 것이 넷 있다 — 그것만 이 스크립트가 한다.
(문구 → 키 이관은 기존 파이프라인이 한다: `gen_string_table.py` → `convert_tables_to_string_keys.py`)

★★ ① 스킬 배정이 **한 칸씩 밀려 있었다**
------------------------------------------
표에 적힌 대로면 이렇다:

    9007 시카리아  80019 · 80020 · 80021
    9008 아루      80021 · 80022 · 80023      ← 80021 은 시카리아 것이다
    9009 카이론    80024 · 80025 · 80026      ← 80024 는 아루 것이다

그런데 **설명문이 주인을 직접 부른다** — 의심할 여지가 없다:

    80021 Arrow_rain      "**시카리아**의 궁술이 천상에 닿습니다"        → 시카리아
    80024 Dawn            "**아루**의 선택 된 공격 유형이 …골렘을 소환"  → 아루
    80025 Fallen_body     "**카이론**이 …보호막을 생성합니다"            → 카이론
    80026 Celestial_shield"**카이론**이 …정신집중을 합니다"              → 카이론
    80027 Divine_wrath    "**카이론**이 …정신집중을 합니다"              → 카이론  ← 아무도 안 쓰고 있었다

즉 아루는 80022~80024, 카이론은 80025~80027 이다. 고치지 않으면
① 시카리아의 「애로우 레인」이 아루에게도 붙고 ② 아루의 「강림」(골렘)이 카이론에게 가고
③ 카이론의 「천벌」(80027)은 **아무 데도 안 붙어 게임에 안 나온다.**

★ ② `Celestial_Shield` / `Celestial_shield` — 대소문자가 갈려 있었다
--------------------------------------------------------------------
`Skill` 시트는 `Celestial_shield`(소문자 s), `Skill_Type` 시트는 `Celestial_Shield`.
`gen_character_assets.py` 는 **`Skill` 시트의 값으로** `Skill_Type` 을 찾으므로
(`skill_types.get(stype)`), 이대로면 그 스킬의 **효과 설명이 빈 문자열**로 나간다.
`Skill` 시트 쪽(소문자)으로 통일한다 — 같은 줄의 `Fallen_body`·`Divine_wrath` 와도 맞는다.

★ ③ 이름 칸에 **영어**가 적혀 있었다
------------------------------------
`character_name` 이 `Sicaria`/`Aru`/`Chiron` 이다. 이 칸은 **한국어 문구**가 들어가는
자리고(51절), 영어는 스트링 키 테이블의 `en` 칸이 정본이다. 그대로 두면
`gen_string_table.py` 가 **`kr` 칸에 영어를 넣는다** — 게임에 이름이 영어로 뜬다.
한국어로 바꾼다(영어는 이 스크립트가 스트링 키 테이블 쪽에 `en` 으로 넣는다).

★ ④ 스킬 아이콘 9칸이 비어 있었다
---------------------------------
`table_update_20260820_skill_icons.py` 와 같은 규칙 — **그림이 스킬을 설명해야 한다**.
이미 쓰고 있는 34개를 피해 골랐다(아래 `ICONS` 주석).

그리고 스트링 키 테이블에 **직접 적어야만 하는 값** 두 종류를 채운다
--------------------------------------------------------------------
수집으로는 절대 안 생기는 칸이다:

  · `character_name_9007~9009` 의 **en** — ⚠ `gen_character_assets.py` 가 이 값으로
    **에셋 파일 이름**(`Character_9007_Sicaria`)을 만든다. 비면 스크립트가 멈춘다.
  · `character_title_9007~9009` 의 **kr** — 표의 `character_title` 칸은 이미 키
    (`character_title_9007`)라 수집기는 **빈 행만** 만든다. 영어는 `character_title_EG`
    컬럼에서 수집되지만 한국어는 어디에도 없다. 기존 6명과 같은 결(짧은 명사구)로 적는다.

⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 51-11절의 하이퍼링크가 날아간다.
⚠ 파일이 엑셀에서 **열려 있으면 안 된다** (`~$` 잠금 파일이 있으면 멈춘다).

실행 순서
---------
    py -3 Tools/table_update_20260820_new_characters.py   # ← 이 스크립트
    py -3 Tools/gen_string_table.py                       # 문구 수집 + TSV 내보내기
    py -3 Tools/table_update_20260820_new_characters.py --strings-only   # en/칭호 채우기
    py -3 Tools/gen_string_table.py                       # TSV 다시 굽기
    py -3 Tools/convert_tables_to_string_keys.py          # 표의 리터럴 → 키
    py -3 Tools/link_string_keys.py                       # 새 키 칸에 하이퍼링크
    py -3 Tools/gen_character_assets.py                   # 캐릭터·패시브 에셋
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

#: ① 스킬 재배정 — 캐릭터 id → (skill_01, skill_02, skill_03). 근거는 맨 위 ★★.
SKILLS = {
    9007: (80019, 80020, 80021),   # 고조된 감각 · 한발에 두마리 · 애로우 레인 (바뀌지 않는다)
    9008: (80022, 80023, 80024),   # 도움의 손길 · 구원 · 강림
    9009: (80025, 80026, 80027),   # 타락한 육체 · 천상의 방패 · 천벌
}

#: ③ 이름 — 한국어는 표의 `character_name`, 영어는 스트링 키 테이블의 `en`.
NAMES = {
    9007: ("시카리아", "Sicaria"),
    9008: ("아루", "Aru"),
    9009: ("카이론", "Chiron"),
}

#: 칭호의 한국어. 영어(`character_title_EG`)를 그대로 옮긴 것이고,
#: 기존 여섯 명과 같은 결이다(눈먼 파수꾼 · 무너지지 않는 방벽 · 환희에 젖은 순교자…).
TITLES_KR = {
    9007: "빛의 궁수",        # The Light Archer
    9008: "종말의 사신",      # The Reaper of Armageddon
    9009: "타락한 투사",      # The Fallen Fighter
}

#: ★ 표에 «행» 이 없어서 수집으로는 절대 안 생기는 키 — 사람이 직접 짓는다(51-1절 규칙).
#:
#:   `unit_name_aru_golem` — 아루의 「강림」이 부르는 골렘의 이름이다.
#:   골렘은 캐릭터 테이블에 행이 없는 «소환수» 라 어느 시트에서도 수집되지 않는다.
NEW_KEYS = {
    "unit_name_aru_golem": ("강림한 골렘", "Descended Golem",
                            "AruGolem.cs", "아루 「강림」(80024) 소환수"),
}

#: ④ 스킬 아이콘. `icon_` 접두사 없이 적는다(표에는 붙여서 들어간다).
ICONS = {
    80019: "spirit_sense",      # Heightened_senses 고조된 감각 — 감각이 퍼지는 그림
    80020: "arrow_volley",      # Two_on_one_leg 한발에 두마리 — 화살 여러 대가 동시에
    80021: "arrow_volley_gold", # Arrow_rain 애로우 레인 — 금빛 화살비
    80022: "teal_portal",       # A_Helping_Hand 도움의 손길 — 아군을 끌어오는 문
    80023: "blessing",          # Salvation 구원 — 축복의 손
    80024: "stone_golem",       # Dawn 강림 — 골렘 그 자체
    80025: "shield_aura",       # Fallen_body 타락한 육체 — 몸을 감싸는 보호막
    80026: "taunt_guard",       # Celestial_shield 천상의 방패 — 도발하며 막아서는 그림
    80027: "lightning_strike",  # Divine_wrath 천벌 — 내리꽂히는 번개
}

#: ② 대소문자 통일 — (시트, 컬럼, 옛값, 새값)
ENUM_FIXES = [
    ("Skill_Type", "skill_type", "Celestial_Shield", "Celestial_shield"),
]


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
    """2행(필드명)에서 컬럼 번호를 찾는다. 없으면 0."""
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def row_of(ws, col, value, last):
    """`col` 컬럼이 `value` 인 행 번호. 없으면 0."""
    for r in range(FIRST_DATA_ROW, last + 1):
        v = ws.Cells(r, col).Value
        if v is None:
            continue
        try:
            same = int(float(v)) == int(value)
        except (TypeError, ValueError):
            same = str(v).strip() == str(value)
        if same:
            return r
    return 0


# ---------------------------------------------------------------------------
def fix_character_table(excel):
    """캐릭터 테이블 — 이름 · 스킬 배정 · 아이콘 · enum 대소문자."""
    wb = excel.Workbooks.Open(os.path.join(TABLES, CHAR_XLSX))
    changed = 0
    try:
        # ── Character 시트 ────────────────────────────────────────────
        ws = wb.Worksheets("Character")
        last = ws.UsedRange.Rows.Count
        c_id = find_col(ws, "character_id")
        c_name = find_col(ws, "character_name")
        c_sk = [find_col(ws, "skill_0%d" % i) for i in (1, 2, 3)]
        if not (c_id and c_name and all(c_sk)):
            raise SystemExit("⚠ Character 시트에서 컬럼을 못 찾았습니다.")

        print("\n  [Character]")
        for cid, (kr, _en) in NAMES.items():
            r = row_of(ws, c_id, cid, last)
            if not r:
                print("    ⚠ %d 행이 없습니다 — 건너뜁니다" % cid)
                continue
            old = ws.Cells(r, c_name).Value
            if str(old or "").strip() != kr:
                ws.Cells(r, c_name).Value = kr
                print("    %d character_name  %s → %s" % (cid, old, kr))
                changed += 1

            for i, sid in enumerate(SKILLS[cid]):
                cur = ws.Cells(r, c_sk[i]).Value
                cur = int(float(cur)) if cur is not None else 0
                if cur != sid:
                    ws.Cells(r, c_sk[i]).Value = sid
                    print("    %d skill_0%d       %d → %d" % (cid, i + 1, cur, sid))
                    changed += 1

        # ── Skill 시트 (아이콘) ───────────────────────────────────────
        ws = wb.Worksheets("Skill")
        last = ws.UsedRange.Rows.Count
        s_id = find_col(ws, "skill_id")
        s_icon = find_col(ws, "skill_icon")
        s_type = find_col(ws, "skill_type")
        if not (s_id and s_icon):
            raise SystemExit("⚠ Skill 시트에서 컬럼을 못 찾았습니다.")

        print("\n  [Skill · 아이콘]")
        for sid, icon in ICONS.items():
            r = row_of(ws, s_id, sid, last)
            if not r:
                print("    ⚠ %d 행이 없습니다 — 건너뜁니다" % sid)
                continue
            old = ws.Cells(r, s_icon).Value
            new = "icon_" + icon
            if str(old or "").strip() != new:
                ws.Cells(r, s_icon).Value = new
                stype = ws.Cells(r, s_type).Value if s_type else ""
                print("    %-6d %-24s %-14s → %s"
                      % (sid, str(stype).strip(), str(old) if old else "(비어 있었다)", new))
                changed += 1

        # ── enum 대소문자 ─────────────────────────────────────────────
        print("\n  [enum 통일]")
        for sheet, field, old_v, new_v in ENUM_FIXES:
            ws = wb.Worksheets(sheet)
            last = ws.UsedRange.Rows.Count
            col = find_col(ws, field)
            if not col:
                print("    ⚠ %s/%s 컬럼 없음" % (sheet, field))
                continue
            r = row_of(ws, col, old_v, last)
            if r:
                ws.Cells(r, col).Value = new_v
                print("    %s.%s  %s → %s" % (sheet, field, old_v, new_v))
                changed += 1
            else:
                print("    %s.%s  %s — 이미 정리됨" % (sheet, field, old_v))

        wb.Save()
    finally:
        wb.Close()
    return changed


def fill_string_table(excel):
    """스트링 키 테이블 — 수집으로는 안 생기는 칸(영어 이름 · 한국어 칭호)을 채운다.

    ⚠ **이미 값이 있으면 건드리지 않는다** — `gen_string_table.py` 의 merge 규칙과 같은
    이유다(사람이 다듬은 번역을 덮으면 안 된다).
    """
    path = os.path.join(TABLES, STRING_XLSX)
    if not os.path.isfile(path):
        raise SystemExit("⚠ 스트링 키 테이블이 없습니다: " + path)

    wb = excel.Workbooks.Open(path)
    changed = 0
    try:
        ws = wb.Worksheets("string")
        last = ws.UsedRange.Rows.Count
        c_key = find_col(ws, "string_key")
        c_kr = find_col(ws, "kr")
        c_en = find_col(ws, "en")
        if not (c_key and c_kr and c_en):
            raise SystemExit("⚠ string 시트에서 컬럼을 못 찾았습니다.")

        wanted = {}
        for cid, (kr, en) in NAMES.items():
            wanted["character_name_%d" % cid] = (None, en)     # kr 은 수집기가 넣는다
        for cid, kr in TITLES_KR.items():
            wanted["character_title_%d" % cid] = (kr, None)    # en 은 수집기가 넣는다

        print("\n  [스트링 키 테이블]")
        found = set()
        for r in range(FIRST_DATA_ROW, last + 1):
            key = ws.Cells(r, c_key).Value
            key = str(key).strip() if key is not None else ""
            if key not in wanted:
                continue
            found.add(key)
            kr, en = wanted[key]
            for col, val, label in ((c_kr, kr, "kr"), (c_en, en, "en")):
                if val is None:
                    continue
                cur = ws.Cells(r, col).Value
                cur = str(cur).strip() if cur is not None else ""
                if cur == val:
                    continue
                if cur:
                    print("    · %-24s %s 이미 \"%s\" — 건드리지 않습니다" % (key, label, cur))
                    continue
                ws.Cells(r, col).Value = val
                print("    %-24s %s ← %s" % (key, label, val))
                changed += 1

        # ★ 없는 키를 맨 아래에 만든다 (NEW_KEYS 주석).
        existing = set()
        for r in range(FIRST_DATA_ROW, last + 1):
            v = ws.Cells(r, c_key).Value
            if v is not None and str(v).strip():
                existing.add(str(v).strip())

        c_src = find_col(ws, "source")
        c_note = find_col(ws, "note")
        row = last + 1
        for key, (kr, en, src, note) in NEW_KEYS.items():
            if key in existing:
                print("    · %-24s 이미 있음" % key)
                continue
            ws.Cells(row, c_key).Value = key
            ws.Cells(row, c_kr).Value = kr
            ws.Cells(row, c_en).Value = en
            if c_src:
                ws.Cells(row, c_src).Value = src
            if c_note:
                ws.Cells(row, c_note).Value = note
            print("    %-24s 신규 ← %s / %s" % (key, kr, en))
            row += 1
            changed += 1

        missing = sorted(set(wanted) - found)
        if missing:
            print("    ⚠ 아직 표에 없는 키 %d개 — `gen_string_table.py` 를 먼저 돌리세요:"
                  % len(missing))
            for k in missing:
                print("       ", k)

        wb.Save()
    finally:
        wb.Close()
    return changed


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    strings_only = "--strings-only" in sys.argv

    files = [STRING_XLSX] if strings_only else [CHAR_XLSX, STRING_XLSX]
    check_locks(files)

    # 아이콘 파일이 실제로 있는지 먼저 본다 — 표에만 적고 파일이 없으면 게임이
    # 조용히 빈 아이콘을 띄운다(`PassiveSkillSO.Icon`).
    if not strings_only:
        project = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        icon_dir = os.path.join(project, "Assets", "_Project", "Resources", "SkillIcons")
        missing = [n for n in sorted(set(ICONS.values()))
                   if not os.path.isfile(os.path.join(icon_dir, "icon_%s.png" % n))]
        if missing:
            raise SystemExit("⚠ 아이콘 파일이 없습니다: " + ", ".join(missing))

    import win32com.client as win32
    backup(files, "신규캐릭터3인" + ("_문구만" if strings_only else ""))

    # ★★ <b>DispatchEx 를 쓴다 — gencache.EnsureDispatch 가 아니다</b> (2026-08-20).
    #
    #   EnsureDispatch 는 ① 이미 돌고 있는 Excel 에 <b>붙고</b> ② 그 타입라이브러리를
    #   캡쳐둔다(makepy). 그러다 보니 다른 엑셀 창이 <b>모달 대화상자를 띄우고 있으면</b>
    #   RPC_E_CALL_REJECTED("피호출자가 호출을 거부했습니다")로 죽고,
    #   그 상태에서는 캐시를 지워도 살아나지 않는다 — 실제로 같힐려 봤다.
    #
    #   DispatchEx 는 <b>새 프로세스를 띄운다.</b> 유저가 열어둔 엑셀과 섞이지 않으므로
    #   ① 그쪽 상태에 영향을 안 받고 ② 유저의 문서를 건드릴 위험도 없다.
    #   늦은 바인딩(타입라이브러리 없이 이름으로 호출)이라 이 스크립트가 쓰는
    #   Workbooks.Open / Cells / Save 에는 아무 제약이 없다.
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
    if not strings_only:
        print("다음: py -3 Tools/gen_string_table.py "
              "→ py -3 Tools/table_update_20260820_new_characters.py --strings-only "
              "→ py -3 Tools/gen_string_table.py "
              "→ py -3 Tools/convert_tables_to_string_keys.py")


if __name__ == "__main__":
    main()
