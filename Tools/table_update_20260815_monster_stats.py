# -*- coding: utf-8 -*-
"""몬스터 표 정비 — 중립 스탯 전체 · 1004 에픽 보스 · 무리 재정의 · 스트링 키 정리 (2026-08-15).

유저 지시:
  *"중립 / 웨이브 몬스터도 일단 모든 스탯 임시 값 넣어서 구현하고 테이블 구조 확인 후
    알맞게 조정하여 테이블에 데이터 채워주고 파싱해 스트링키 테이블 구조도 다시 다듬어서
    다 맞춰서 넣고. 1004 번 중립 몬스터 보스몹으로 구현"*

이 스크립트가 표에 하는 일
--------------------------
① `임시용 중립 몬스터.xlsx` / `first_Stat`
   · **컬럼 3개 신설** — `ranged_atk` · `magic` · `cure` (맨 뒤 K·L·M).
     웨이브 몬스터 `first_Stat` 과 형식을 맞춘다. 지금까지 중립에는 이 칸이 아예 없어서
     **`atk_type` 이 `ranged` 인 1002 도 근거리 공격력 칸을 쓰고 있었다.**
   · **1002 의 공격력을 원거리 칸으로 옮긴다** (melee 6 → ranged 6, melee 0).
     웨이브의 100002(영혼 사수)가 `melee 0 / ranged 4` 인 것과 같은 형식이다.
   · **1004 행을 채운다** (에픽 보스, 전부 임시값).

② `임시용 중립 몬스터.xlsx` / `neutrality_mon`
   · **1004 행 마무리** — `mon_name` 스트링 키 · 처치 보상 에너지.
   · 나머지 칸(mon_type=epic · max_alive 1 · respawn 600)은 유저가 이미 적어둔 값을 그대로 둔다.

   ⚠ **`atk_take` 는 건드리지 않는다.** 이 칸의 한글 헤더는 처음부터 **"동료 협공 여부"**
      였는데 71절이 "선공 여부" 로 잘못 읽어 코드에 넣고 있었다. 유저 확정(2026-08-15)으로
      **선공 여부는 표에 없다** — 중립은 전부 비선공, 웨이브는 전부 선공으로 <b>종류가 정한다</b>.
      `atk_take` 는 원래 뜻대로 **무리 반격 여부**로 쓴다. 표의 현재 값
      (1001=1·1002=1·1003=0·1004=0)이 `group_making` 과 이미 일관되므로 고칠 것이 없다.

③ `웨이브 몬스터 테이블.xlsx`
   · `wave_mid_boss.boss_title` 의 **한글 리터럴 → 스트링 키**
     (`boss_title_110001` · `_110002`). 두 키는 스트링 키 테이블에 **이미 있다** —
     표만 51절 규칙에서 벗어나 있었다.
   · `first_Stat` 2행의 **` resistance` 앞 공백 제거.** `' Rho_aias'` 와 같은 함정이다.

④ `스트링 키 테이블.xlsx`
   · `mon_name_1004` 추가.

⚠ 편집은 **Excel COM** 으로 한다 — openpyxl 로 저장하면 51-11절이 넣은 **하이퍼링크**가
   날아간다(UI-17 절에서 실제로 12칸이 날아갔다). 64-2·69-10·84-3절과 같은 이유.

⚠ **컬럼은 맨 뒤에만 붙인다**(65-2절). 끼워넣으면 위치로 읽는 코드가 조용히 어긋난다.
   실제로 2026-08-13 에 `*_EG` 컬럼을 지웠다가 그 뒤 컬럼이 한 칸씩 밀린 사고가 있었다.

사용법:  python Tools/table_update_20260815_monster_stats.py
"""

import os
import sys
import shutil
import datetime

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
WAVE_XLSX = os.path.join(TABLES, "웨이브 몬스터 테이블.xlsx")
STRING_XLSX = os.path.join(TABLES, "스트링 키 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

# ── 중립 first_Stat 신설 컬럼 (맨 뒤에 붙인다) ──────────────────────────
#    (한글 라벨, 필드명, 자료형)
NEW_STAT_COLUMNS = [
    ("원거리 공격력", "ranged_atk", "int"),
    ("마법", "magic", "int"),
    ("회복력", "cure", "int"),
]

# ── 1004 에픽 보스 능력치 (전부 임시값) ─────────────────────────────────
#
# 근거로 삼은 것 — 지금 가장 강한 중립 1003 과 실제 체감이 얼마나 벌어지는지:
#   체력   = 40 + hp×10       1003(14) → 180  ·  1004(40) → 440   (약 2.4배)
#   타격력 = 2 + 공격력×2      1003(11) →  24  ·  1004(24) →  50   (약 2.1배)
# "보스몹" 이라는 지시에 맞게 잡몹 여럿을 상대할 만큼은 되되, 최종보스(2250)와는
# 자릿수를 다르게 뒀다. 밸런싱은 표에서 바로 고칠 수 있다.
EPIC_1004_STATS = {
    "hp": 40,
    "melee_atk": 24,
    "accuracy": 50,       # ⚠ 근거리라 실제로는 쓰이지 않는다(명중은 원거리 전용)
    "critical": 0,        # ⚠ 같은 이유로 쓰이지 않는다
    "def": 12,
    "hp_recovery": 6,
    "atk_speed": 5,       # 크고 느리게 — 1003(7)보다 느리다
    "movement_speed": 3,  # 1003(5)보다 느리다
    "resistance": 50,
    "ranged_atk": 0,
    "magic": 0,
    "cure": 0,
}

# 처치 보상 — 1003 이 66~108 이므로 보스답게 한 자릿수 위로.
EPIC_1004_ENERGY = (400, 600)

EPIC_1004_NAME_KEY = "mon_name_1004"
EPIC_1004_NAME_KR = "역겨운 모체"

# ── 웨이브 중간보스 칭호 → 스트링 키 ────────────────────────────────────
#    스트링 키 테이블에 이미 있는 키다(86·87행). 표 쪽만 리터럴로 남아 있었다.
MID_BOSS_TITLE_KEYS = {
    110001: "boss_title_110001",   # 피에 새겨진 낙인
    110002: "boss_title_110002",   # 허공을 삼킨 목소리
}

NEW_STRINGS = [
    (EPIC_1004_NAME_KEY, EPIC_1004_NAME_KR, "neutrality_mon.mon_name"),
]


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_몬스터스탯정비")
    os.makedirs(dst, exist_ok=True)
    for src in (NEUTRAL_XLSX, WAVE_XLSX, STRING_XLSX):
        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(dst, os.path.basename(src)))
    print("백업:", dst)


def find_col(ws, field, max_col=64):
    """2행(필드명)에서 컬럼 번호를 찾는다. 없으면 0. ⚠ 앞뒤 공백을 반드시 제거한다."""
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def row_of(ws, id_value, first_row=4, max_row=400):
    """A열이 id_value 인 행 번호. 없으면 0."""
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


def last_used_col(ws, max_col=64):
    last = 0
    for c in range(1, max_col + 1):
        if ws.Cells(2, c).Value is not None:
            last = c
    return last


def main():
    import win32com.client as win32

    for p in (NEUTRAL_XLSX, WAVE_XLSX, STRING_XLSX):
        if not os.path.isfile(p):
            print("⚠ 파일 없음:", p)
            return 1

    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    try:
        # ══════════════════════════════════════════════════════════════
        # ① + ②  임시용 중립 몬스터.xlsx
        # ══════════════════════════════════════════════════════════════
        wb = excel.Workbooks.Open(NEUTRAL_XLSX)

        # ── first_Stat: 컬럼 신설 ──────────────────────────────────────
        ws = wb.Worksheets("first_Stat")
        col = last_used_col(ws)
        for label, field, typ in NEW_STAT_COLUMNS:
            if find_col(ws, field):
                print(f"  (이미 있음) first_Stat.{field}")
                continue
            col += 1
            ws.Cells(1, col).Value = label
            ws.Cells(2, col).Value = field
            ws.Cells(3, col).Value = typ
            print(f"  + first_Stat 컬럼 {field} (열 {col})")

        cols = {f: find_col(ws, f) for _, f, _ in NEW_STAT_COLUMNS}
        for f in ("hp", "melee_atk", "accuracy", "critical", "def",
                  "hp_recovery", "atk_speed", "movement_speed", "resistance"):
            cols[f] = find_col(ws, f)

        missing = [f for f, c in cols.items() if not c]
        if missing:
            print("⚠ first_Stat 에서 못 찾은 컬럼:", missing)
            wb.Close(False)
            return 1

        # 기존 행의 빈 신설 칸을 0 으로 채운다 — 비어 있으면 파싱이 기본값으로 떨어진다.
        for mid in (1001, 1002, 1003):
            r = row_of(ws, mid)
            if not r:
                continue
            for _, f, _ in NEW_STAT_COLUMNS:
                if ws.Cells(r, cols[f]).Value is None:
                    ws.Cells(r, cols[f]).Value = 0

        # ── 1002 를 원거리 형식으로 (melee → ranged) ────────────────────
        r = row_of(ws, 1002)
        if r:
            melee = ws.Cells(r, cols["melee_atk"]).Value or 0
            ranged = ws.Cells(r, cols["ranged_atk"]).Value or 0
            if melee and not ranged:
                ws.Cells(r, cols["ranged_atk"]).Value = int(melee)
                ws.Cells(r, cols["melee_atk"]).Value = 0
                print(f"  1002 원거리 이관: melee {int(melee)} → ranged {int(melee)}")
            else:
                print(f"  (이미 이관됨) 1002 melee={melee} ranged={ranged}")

        # ── 1004 능력치 ────────────────────────────────────────────────
        r = row_of(ws, 1004)
        if not r:
            # 행이 없으면 맨 뒤에 새로 만든다.
            r = 4
            while ws.Cells(r, 1).Value is not None:
                r += 1
            ws.Cells(r, 1).Value = 1004
            print(f"  + first_Stat 1004 행 신설 (행 {r})")
        for f, v in EPIC_1004_STATS.items():
            ws.Cells(r, cols[f]).Value = v
        print(f"  1004 능력치 {len(EPIC_1004_STATS)}칸 기입 (체력 {40 + EPIC_1004_STATS['hp'] * 10} · "
              f"타격 {2 + EPIC_1004_STATS['melee_atk'] * 2})")

        # ── neutrality_mon: 1004 이름·에너지 ───────────────────────────
        wn = wb.Worksheets("neutrality_mon")
        c_name = find_col(wn, "mon_name")
        c_min = find_col(wn, "min_energy")
        c_max = find_col(wn, "max_energy")
        if not (c_name and c_min and c_max):
            print("⚠ neutrality_mon 컬럼을 못 찾음")
            wb.Close(False)
            return 1

        r = row_of(wn, 1004)
        if r:
            if not wn.Cells(r, c_name).Value:
                wn.Cells(r, c_name).Value = EPIC_1004_NAME_KEY
                print(f"  1004 mon_name → {EPIC_1004_NAME_KEY}")
            if wn.Cells(r, c_min).Value is None:
                wn.Cells(r, c_min).Value = EPIC_1004_ENERGY[0]
            if wn.Cells(r, c_max).Value is None:
                wn.Cells(r, c_max).Value = EPIC_1004_ENERGY[1]
            print(f"  1004 에너지 {EPIC_1004_ENERGY[0]}~{EPIC_1004_ENERGY[1]}")
        else:
            print("⚠ neutrality_mon 에 1004 행이 없다")

        wb.Save()
        wb.Close()
        print("임시용 중립 몬스터.xlsx 저장")

        # ══════════════════════════════════════════════════════════════
        # ③  웨이브 몬스터 테이블.xlsx
        # ══════════════════════════════════════════════════════════════
        wb = excel.Workbooks.Open(WAVE_XLSX)

        ws = wb.Worksheets("wave_mid_boss")
        c_title = find_col(ws, "boss_title")
        if c_title:
            for mid, key in MID_BOSS_TITLE_KEYS.items():
                r = row_of(ws, mid)
                if not r:
                    continue
                cur = ws.Cells(r, c_title).Value
                if cur and str(cur).strip() == key:
                    print(f"  (이미 키) {mid} boss_title")
                    continue
                ws.Cells(r, c_title).Value = key
                print(f"  {mid} boss_title '{cur}' → {key}")
        else:
            print("⚠ wave_mid_boss.boss_title 컬럼 없음")

        # ` resistance` 앞 공백 제거
        wf = wb.Worksheets("first_Stat")
        fixed = 0
        for c in range(1, last_used_col(wf) + 1):
            v = wf.Cells(2, c).Value
            if v is None:
                continue
            s = str(v)
            if s != s.strip():
                wf.Cells(2, c).Value = s.strip()
                fixed += 1
                print(f"  first_Stat 헤더 공백 제거: '{s}' → '{s.strip()}'")
        if not fixed:
            print("  (헤더 공백 없음)")

        wb.Save()
        wb.Close()
        print("웨이브 몬스터 테이블.xlsx 저장")

        # ══════════════════════════════════════════════════════════════
        # ④  스트링 키 테이블.xlsx
        # ══════════════════════════════════════════════════════════════
        wb = excel.Workbooks.Open(STRING_XLSX)
        ws = wb.Worksheets("string")
        last = ws.UsedRange.Rows.Count
        existing = {str(ws.Cells(r, 1).Value).strip()
                    for r in range(4, last + 1) if ws.Cells(r, 1).Value}

        added = 0
        for key, kr, source in NEW_STRINGS:
            if key in existing:
                print(f"  (이미 있음) {key}")
                continue
            last += 1
            ws.Cells(last, 1).Value = key
            ws.Cells(last, 2).Value = kr
            ws.Cells(last, 4).Value = source
            added += 1
            print(f"  + {key} = {kr}")

        wb.Save()
        wb.Close()
        print(f"스트링 키 {added}개 추가 (총 {last - 3}개)")

    finally:
        excel.Quit()

    print("\n완료 — 다음으로 아래를 순서대로 돌릴 것:")
    print("  python Tools/gen_string_table.py")
    print("  python Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
