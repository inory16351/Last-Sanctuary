# -*- coding: utf-8 -*-
"""시그리드 칭호 신설 + 베일 스킬 설명 키 오류 수정 (2026-08-20).

① 시그리드(9006) 칭호 — **비어 있었다**
----------------------------------------
유저 지시: *"시그리드 타이틀 적당하게 정해서 넣어줘"*

표의 ``character_title`` · ``character_title_EG`` 가 9006 만 비어 있었다. 다른 다섯 명은
전부 채워져 있고, 상세 카드(112절)가 그 칸이 비면 **칭호 줄을 아예 안 띄운다** —
즉 시그리드만 칭호 없는 캐릭터가 된다.

  **환희에 젖은 순교자 / The Ecstatic Martyr**

<b>왜 이 문구인가</b> — 기존 칭호는 「수식어 + 명사」 한 덩어리다(눈먼 파수꾼 ·
The Unbreaking Bulwark · The Avenger Beyond Death). 시그리드의 패시브 셋이 전부
**고통을 기쁨으로 바꾸는** 한 가지 이야기다:

  · 가학증 — 자기 체력을 깎아 아군을 회복시킨다 (희생)
  · 고통의 기쁨 — 그 순간 공격 속도가 오른다 (도취)
  · 통제할 수 없는 쾌락 — 체력이 바닥나면 오히려 무적이 된다 (환희)

「순교자」가 그 희생을, 「환희에 젖은」이 그 도취를 담는다.
⚠ 이건 **내가 고른 문구**다 — 표의 두 칸(``character_title`` · ``character_title_EG``)만
  고치면 언제든 바꿀 수 있다. 코드에는 아무것도 박혀 있지 않다.

② ★★ 베일 스킬 설명이 **라린길의 것을 가리키고 있었다**
--------------------------------------------------------
``웨이브 몬스터 테이블.xlsx`` / ``Skill`` 의 ``skill_explain`` 칸:

    130009 Pipe_strike → ``skill_explain_130007``   ← 라린길 「아우성」의 키
    130010 Pipe_smoke  → ``skill_explain_130008``   ← 라린길 「타오르는 숨결」의 키

윗줄을 복사해 만든 흔적이다. ``Tools/convert_tables_to_string_keys.py`` 가 «이미 다른
키가 있다» 고 경고해서 찾았다 — **그 경고가 없었으면 못 찾았다**(게임은 조용히 엉뚱한
설명을 띄운다).

★ 정작 필요한 키는 **이미 스트링 키 테이블에 다 있다**:
    skill_explain_130009 = "베일은 담뱃대로 수 많은 천사들을 사냥했습니다"
    skill_explain_130010 = "베일의 숨결은 죽음 그 자체입니다"
    skill_type_desc_Pipe_strike · skill_type_desc_Pipe_smoke (정의문 전체)
    monster_name_120005 = 베일 / Bale · boss_title_120005 = 천사 사냥꾼 / Angel Hunter
그래서 이 스크립트는 **키 두 개만 바로잡는다.**

⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).

사용법:  py -3 Tools/table_update_20260820_sigrid_bale.py
다음:    py -3 Tools/gen_string_table.py            (칭호를 스트링 키 테이블로)
        py -3 Tools/convert_tables_to_string_keys.py  (칭호 리터럴 → 키)
        py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

BACKUP_ROOT = os.path.join(TABLES, "_백업")
FIRST_DATA_ROW = 4

CHAR_XLSX = "캐릭터 테이블.xlsx"
WAVE_MON_XLSX = "웨이브 몬스터 테이블.xlsx"

#: ① 시그리드 칭호 (맨 위 ①). 리터럴로 적으면 뒤이어 도는 파이프라인이 키로 바꿔준다.
SIGRID_ID = 9006
SIGRID_TITLE_KR = "환희에 젖은 순교자"
SIGRID_TITLE_EN = "The Ecstatic Martyr"

#: ② 베일 스킬 설명 키 (맨 위 ②) — skill_id → 올바른 skill_explain 키.
EXPLAIN_FIX = {
    130009: "skill_explain_130009",
    130010: "skill_explain_130010",
}


def check_locks():
    locked = [f for f in (CHAR_XLSX, WAVE_MON_XLSX)
              if os.path.isfile(os.path.join(TABLES, "~$" + f))]
    if locked:
        raise SystemExit("⚠ 엑셀에서 열려 있습니다 — 닫고 다시 실행하세요: " + ", ".join(locked))


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_시그리드칭호_베일설명키")
    os.makedirs(dst, exist_ok=True)
    for f in (CHAR_XLSX, WAVE_MON_XLSX):
        p = os.path.join(TABLES, f)
        if os.path.isfile(p):
            shutil.copy2(p, os.path.join(dst, f))
    print("백업:", dst)


def find_col(ws, field, max_col=32):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def row_of(ws, id_col, wanted):
    """
    id 컬럼에서 그 값을 가진 행. ⚠ 이 표의 id 칸에는 ``=A4+1`` 같은 **수식**이 들어 있다 —
    COM 의 ``.Value`` 는 계산된 값을 주므로 문제없다(openpyxl 은 수식 문자열을 준다).
    """
    last = ws.UsedRange.Rows.Count
    for r in range(FIRST_DATA_ROW, last + 1):
        v = ws.Cells(r, id_col).Value
        if v is not None and int(v) == wanted:
            return r
    return 0


def main():
    check_locks()
    import win32com.client as win32

    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    try:
        # ── ① 시그리드 칭호 ────────────────────────────────────────────────
        wb = excel.Workbooks.Open(os.path.join(TABLES, CHAR_XLSX))
        ws = wb.Worksheets("Character")
        c_id = find_col(ws, "character_id")
        c_kr = find_col(ws, "character_title")
        c_en = find_col(ws, "character_title_EG")
        if not (c_id and c_kr and c_en):
            print("⚠ Character 시트에서 칭호 컬럼을 못 찾음")
        else:
            r = row_of(ws, c_id, SIGRID_ID)
            if not r:
                print("⚠ Character 시트에 %d 이 없습니다" % SIGRID_ID)
            else:
                old_kr = ws.Cells(r, c_kr).Value
                old_en = ws.Cells(r, c_en).Value
                ws.Cells(r, c_kr).Value = SIGRID_TITLE_KR
                ws.Cells(r, c_en).Value = SIGRID_TITLE_EN
                print("  [칭호] %d행: %s / %s → %s / %s"
                      % (r, old_kr or "(비어 있었다)", old_en or "(비어 있었다)",
                         SIGRID_TITLE_KR, SIGRID_TITLE_EN))
        wb.Save()
        wb.Close()

        # ── ② 베일 스킬 설명 키 ────────────────────────────────────────────
        wb = excel.Workbooks.Open(os.path.join(TABLES, WAVE_MON_XLSX))
        ws = wb.Worksheets("Skill")
        c_id = find_col(ws, "skill_id")
        c_ex = find_col(ws, "skill_explain")
        if not (c_id and c_ex):
            print("⚠ Skill 시트에서 skill_id · skill_explain 컬럼을 못 찾음")
        else:
            for sid, want in EXPLAIN_FIX.items():
                r = row_of(ws, c_id, sid)
                if not r:
                    print("  ⚠ Skill 시트에 %d 이 없습니다" % sid)
                    continue
                old = ws.Cells(r, c_ex).Value
                if str(old) == want:
                    print("  [설명키] %d: 이미 %s (건드리지 않음)" % (sid, want))
                    continue
                ws.Cells(r, c_ex).Value = want
                print("  [설명키] %d: %s → %s  ★ 라린길의 키를 가리키고 있었다"
                      % (sid, old, want))
        wb.Save()
        wb.Close()
    finally:
        excel.Quit()

    print()
    print("다음: py -3 Tools/gen_string_table.py")
    print("      py -3 Tools/convert_tables_to_string_keys.py")
    print("      py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
