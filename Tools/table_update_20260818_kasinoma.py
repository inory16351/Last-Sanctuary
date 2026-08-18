# -*- coding: utf-8 -*-
"""카시노마(120003) 표 정리 — 스트링 키 · 스탯 정규화 · 스킬 값 (2026-08-18).

유저 지시: *"카시노마도 구현하고 … 카시노마 / 말파스 테이블 연동 전부 맞추고"*,
*"데이터 값 변경 필요하다 판단 할경우 테이블에 값 바꿔서 넣고 게임에 적용해줘"*.

무엇을 고치나
-------------
**① `스트링 키 테이블.xlsx`**
  유저가 `웨이브 몬스터 테이블` 에는 120003 행을 넣어 뒀지만 **스트링 키 쪽은
  `skill_type_desc_*` 두 줄만** 들어와 있었다. 이름·칭호·스킬 이름·설명이 없으면
  게임에서 `monster_name_120003` 같은 **키가 그대로 화면에 뜬다**(51-4절).

**② `웨이브 몬스터 테이블.xlsx` / `first_Stat`**
  · 120003 이 `hp 12 · hp_percent 1200` 으로 **옛 방식**이다.
    96-2절 이후 이 프로젝트의 규약은 "체력을 배율이 아니라 원시 `hp` 로 흡수" 다.

**③ `웨이브 몬스터 테이블.xlsx` / `Skill`**
  · 130005·130006 의 값이 **원화 시트와 어긋난다**(아래 참조).

**④ `웨이브테이블.xlsx`**
  · 15웨이브 보스를 단탈리온 → **카시노마**로.

⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).

사용법:  python Tools/table_update_20260818_kasinoma.py
다음:    python Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil

from vault_path import TABLE_DIR as TABLES

WAVE_XLSX = os.path.join(TABLES, "웨이브테이블.xlsx")
MON_XLSX = os.path.join(TABLES, "웨이브 몬스터 테이블.xlsx")
STR_XLSX = os.path.join(TABLES, "스트링 키 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

# ---------------------------------------------------------------------------
# ★ 15웨이브를 카시노마로 — 왜 이 자리인가
#
# 유저 지시는 **5 단탈리온 · 10 말파스** 두 개뿐이고 카시노마의 등장 웨이브는 안 정해졌다
# (말파스 원화에는 *"등장 웨이브 : 5n번째"* 가 적혀 있지만 카시노마 원화에는 없다).
# 그런데 카시노마는 `wave_top_boss` 시트에 있는 **웨이브 보스**라 어딘가에는 나와야 한다.
#
# 15 를 고른 이유:
#   ① 5·10·15·20 네 자리 중 **아직 두 번째로 도는 자리**(15·20)만 비어 있다.
#   ② 20 은 <b>최종 보스</b> 자리다 — 원화 시트가 「보스 몬스터 : 말파스」라고 못 박은 쪽을
#      남겨 두는 것이 안전하다.
#   ③ 세 보스가 5 → 10 → 15 로 <b>한 번씩</b> 나오고 20 에서 최종 보스가 다시 온다.
#
# ⚠ 유저가 다른 배치를 원하면 <b>이 표만 고치면 된다</b> — 코드에는 보스 id 가 없다
#   (`MonsterSpawner.bossSlots` 가 표의 `boss_monster_id` 로 찾는다).
# ---------------------------------------------------------------------------
BOSS_BY_WAVE = {
    5: 120001,    # 단탈리온
    10: 120002,   # 말파스
    15: 120003,   # 카시노마  ← 이번에 바뀐 자리 (전에는 120001)
    20: 120002,   # 말파스 (최종)
}

# ---------------------------------------------------------------------------
# ★ 체력 — 96-2절 규약대로 원시 `hp` 로 흡수한다
#
# 표에 `hp 12 · hp_percent 1200` 으로 들어와 있다(유효 144). 두 가지가 걸린다:
#
#   ① **96-2절이 없앤 방식이다.** 상한 철폐 이후 규약은 "배율이 아니라 원시 hp".
#      말파스는 이미 `hp 174 · hp_percent 100` 으로 옮겨 놨다 — 카시노마만 옛 방식으로
#      남으면 세 보스의 체력 곡선을 같이 못 읽는다.
#
#   ② **15웨이브의 난이도가 바뀐다.** 표 값 그대로면 15웨이브 카시노마의 유효 체력이
#      144 x 10.8 = **1,555** 인데, 지금 그 자리에 있던 단탈리온은 **18,830** 이다.
#      12배 약한 보스가 나오면 15웨이브가 보스전이 아니게 된다.
#
# <b>얼마로 잡았나</b> — `hp 174`. 단탈리온·말파스와 **같은 원시 체력**이다. 그러면
# 15웨이브 보스의 유효 체력이 96절이 맞춰 둔 **18,830 그대로**가 되어,
# <b>난이도는 하나도 안 바뀌고 보스의 정체만 바뀐다</b>. 말파스를 10웨이브에 넣을 때
# 쓴 것과 <b>같은 판단</b>이다(table_update_20260818_boss_rework.py 참조).
#
# 카시노마가 「빠르고 아픈 근접 암살자」라는 성격은 <b>공격력·이동속도</b>가 들고 있다
# (근거리 9 · 이속 4 — 단탈리온 8/4 보다 세고, 방어력은 25 로 단탈리온 35 보다 무르다).
# ---------------------------------------------------------------------------
TABLE_STATS = {
    120003: {"hp": 174, "hp_percent": 100},
}

# ---------------------------------------------------------------------------
# ★ 스킬 값 — <b>원화 시트가 근거다</b>
#
# 표에 들어와 있던 값이 `리소스/Kasinoma_asset_01.png` 의 설명과 어긋난다.
# 원화 시트에 적힌 것:
#
#   스킬 1 (이끌리는 혈취) : "20x20 타일 범위 내 타겟 1명에게 돌진 후 1회 피해
#                            (근거리 공격의 3배)"
#   스킬 2 (죽음의 노래)   : "4x4 타일 범위 내 타겟 1명에게 6번의 근접 피해"
#
# 스트링 테이블의 정의문이 각 칸의 뜻을 정한다(67-1절 — 정의문이 규칙이다):
#
#   Lure_blood : "{value_01} 지름 타일 범위 안에 적 1명에게 돌진하여 {value_02}% 데미지"
#                → value_01 = 탐색 <b>지름</b> · value_02 = <b>피해 %</b>
#                  ⚠ 다른 스킬은 value_03 이 피해다 — 이 스킬만 한 칸 앞이다.
#   Death_song : "{value_01}(가로) x {value_02}(세로) 범위의 적을 {value_04}번 만큼
#                 카시노마의 근거리 공격력 {value_03}%로 공격한다"
#
# 무엇을 바꿨나:
#   130005  value_01  5 → <b>20</b>   (원화의 "20x20 타일 범위")
#           value_02 200 → <b>300</b> (원화의 "근거리 공격의 3배")
#   130006  value_01  5 → <b>4</b>    (원화의 "4x4 타일 범위")
#           value_02  3 → <b>4</b>
#           range_type Circle → <b>Line</b>
#              정의문이 「가로 x 세로」라 방향이 있는 상자다. Circle 로 두면 코드가
#              value_01 을 <b>지름</b>으로 읽어 방향 없는 원이 되어 정의문과 어긋난다.
#
#   130006  value_03 35 → <b>100</b>  ★ <b>유저 확정 2026-08-18</b>
#              원화의 "6번의 근접 피해" 를 글자 그대로 <b>근거리 공격력 100% x 6타</b> 로 읽는다.
#              ⚠ <b>총 600% 다</b> — 단탈리온(150·200%) · 말파스(130·220%) 와 견줘 3배다.
#                처음에는 표에 있던 35%(총 210%)를 다른 보스와 같은 눈금이라 보고 남겨 뒀는데,
#                유저가 <b>원화 문구 그대로</b>를 골랐다. 15웨이브 보스가 전열을 한 번에
#                녹일 수 있다는 뜻이고, 그것이 「죽음의 노래」의 의도다.
#                되돌리려면 이 줄의 value_03 만 고치면 된다.
# ---------------------------------------------------------------------------
SKILL_VALUES = {
    130005: {"value_01": 20, "value_02": 300, "range_type": "Line"},
    130006: {"value_01": 4, "value_02": 4, "value_03": 100, "range_type": "Line"},
}

# ---------------------------------------------------------------------------
# ★ 보스 일러스트 — `wave_top_boss.illust` 를 채운다
#
# 칸은 원래 있었는데 <b>세 보스 중 카시노마만</b> 채워져 있었다(유저가 넣어 뒀다).
# 볼트 `리소스/illust/monster_cancer/` 에는 세 장이 다 있다.
#
# ⚠ 이 칸은 그동안 <b>읽는 코드가 아예 없었다</b> — 클릭 초상화(UnitPortraitPanel)는
#   2026-08-15 에 중립 몬스터에만 붙었다(86-4·5절). 이번에 MonsterDefinitionSO.illustName
#   을 신설해 웨이브 보스도 같은 경로를 타게 했다.
# ---------------------------------------------------------------------------
BOSS_ILLUST = {
    120001: "Dantalian_illust",
    120002: "Malphas_illust",
    120003: "Kasinoma_illust",
}

#: 스트링 키 추가분 — (키, 한국어, 영어, 출처, 비고)
#:
#: `skill_type_desc_Lure_blood` · `skill_type_desc_Death_song` 는 **이미 표에 있다**
#: (유저가 미리 넣어 뒀다) — 건드리지 않는다.
STRING_ADD = [
    ("monster_name_120003", "카시노마", "Kasinoma", "wave_top_boss.monster_name", None),
    ("boss_title_120003", "검붉은 응시자", "The Crimson Gazer",
     "wave_top_boss.boss_title", "원화 시트의 「칭호」 그대로"),
    ("skill_name_130005", "이끌리는 혈취", None, "Skill.skill_name", None),
    ("skill_name_130006", "죽음의 노래", None, "Skill.skill_name", None),
    ("skill_explain_130005",
     "피 냄새를 맡은 카시노마는 거리를 두는 법을 잊습니다", None,
     "Skill.skill_explain", None),
    ("skill_explain_130006",
     "여섯 번의 팔놀림이 끝나기 전에 노래는 멈추지 않습니다", None,
     "Skill.skill_explain", None),
]


def backup(paths, tag):
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_" + tag)
    os.makedirs(dst, exist_ok=True)
    for p in paths:
        shutil.copy2(p, os.path.join(dst, os.path.basename(p)))
    print("백업:", dst)


def find_col(ws, field, max_col=40):
    """2행(필드명)에서 컬럼 번호를 찾는다. 없으면 0."""
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


# ---------------------------------------------------------------------------

def update_wave_table(excel):
    wb = excel.Workbooks.Open(WAVE_XLSX)
    try:
        ws = wb.Worksheets("Sheet2")
        c_num = find_col(ws, "wave_num")
        c_boss = find_col(ws, "boss_mon_num")
        c_id = find_col(ws, "boss_monster_id")
        if not (c_num and c_boss and c_id):
            raise SystemExit("⚠ wave_num / boss_mon_num / boss_monster_id 컬럼을 못 찾았습니다")

        for r in range(4, ws.UsedRange.Rows.Count + 1):
            v = ws.Cells(r, c_num).Value
            if v is None:
                continue
            wave = int(v)
            boss_id = BOSS_BY_WAVE.get(wave, 0)
            old = int(ws.Cells(r, c_id).Value or 0)
            if old == boss_id:
                continue
            ws.Cells(r, c_id).Value = boss_id
            ws.Cells(r, c_boss).Value = 1 if boss_id else 0
            print("  ★ %d웨이브 보스 : %d → %d" % (wave, old, boss_id))

        wb.Save()
    finally:
        wb.Close(False)
    print("웨이브테이블.xlsx 저장")


def update_monster_table(excel):
    wb = excel.Workbooks.Open(MON_XLSX)
    try:
        # ── first_Stat ────────────────────────────────────────────────
        ws = wb.Worksheets("first_Stat")
        c_id = find_col(ws, "monster_id")
        if not c_id:
            raise SystemExit("⚠ first_Stat 에 monster_id 컬럼이 없습니다")

        for r in range(4, ws.UsedRange.Rows.Count + 1):
            v = ws.Cells(r, c_id).Value
            if v is None:
                continue
            want = TABLE_STATS.get(int(v))
            if not want:
                continue
            for field, value in want.items():
                c = find_col(ws, field)
                if not c:
                    print("  ⚠ first_Stat 에 %s 컬럼이 없습니다" % field)
                    continue
                old = ws.Cells(r, c).Value
                if old is not None and int(old) == value:
                    print("  first_Stat %d %s = %s (이미 맞음)" % (int(v), field, value))
                    continue
                ws.Cells(r, c).Value = value
                print("  ★ first_Stat %d %s : %s → %s" % (int(v), field, old, value))

        # ── wave_top_boss (일러스트) ──────────────────────────────────
        ws = wb.Worksheets("wave_top_boss")
        c_id = find_col(ws, "monster_id")
        c_il = find_col(ws, "illust")
        if not (c_id and c_il):
            raise SystemExit("⚠ wave_top_boss 에 monster_id / illust 컬럼이 없습니다")

        for r in range(4, ws.UsedRange.Rows.Count + 1):
            v = ws.Cells(r, c_id).Value
            if v is None:
                continue
            want = BOSS_ILLUST.get(int(v))
            if not want:
                continue
            old = ws.Cells(r, c_il).Value
            if old is not None and str(old).strip() == want:
                print("  wave_top_boss %d illust = %s (이미 맞음)" % (int(v), want))
                continue
            ws.Cells(r, c_il).Value = want
            print("  ★ wave_top_boss %d illust : %s → %s" % (int(v), old, want))

        # ── Skill ─────────────────────────────────────────────────────
        ws = wb.Worksheets("Skill")
        c_id = find_col(ws, "skill_id")
        if not c_id:
            raise SystemExit("⚠ Skill 에 skill_id 컬럼이 없습니다")

        for r in range(4, ws.UsedRange.Rows.Count + 1):
            v = ws.Cells(r, c_id).Value
            if v is None:
                continue
            want = SKILL_VALUES.get(int(v))
            if not want:
                continue
            for field, value in want.items():
                c = find_col(ws, field)
                if not c:
                    print("  ⚠ Skill 에 %s 컬럼이 없습니다" % field)
                    continue
                old = ws.Cells(r, c).Value
                if old is not None and str(old).strip() == str(value):
                    print("  Skill %d %s = %s (이미 맞음)" % (int(v), field, value))
                    continue
                ws.Cells(r, c).Value = value
                print("  ★ Skill %d %s : %s → %s" % (int(v), field, old, value))

        wb.Save()
    finally:
        wb.Close(False)
    print("웨이브 몬스터 테이블.xlsx 저장")


def update_string_table(excel):
    wb = excel.Workbooks.Open(STR_XLSX)
    try:
        ws = wb.Worksheets("string")
        last_row = ws.UsedRange.Rows.Count

        existing = {}
        for r in range(4, last_row + 1):
            k = ws.Cells(r, 1).Value
            if k is not None:
                existing[str(k).strip()] = r

        write_row = last_row + 1
        for key, kr, en, src, note in STRING_ADD:
            r = existing.get(key)
            if r is None:
                r = write_row
                write_row += 1
                ws.Cells(r, 1).Value = key
                print("  추가 %s = %s" % (key, kr))
            else:
                print("  갱신 %s = %s" % (key, kr))
            ws.Cells(r, 2).Value = kr
            if en is not None:
                ws.Cells(r, 3).Value = en
            if src is not None:
                ws.Cells(r, 4).Value = src
            if note is not None:
                ws.Cells(r, 5).Value = note

        wb.Save()
    finally:
        wb.Close(False)
    print("스트링 키 테이블.xlsx 저장")


def main():
    import win32com.client

    backup([WAVE_XLSX, MON_XLSX, STR_XLSX], "카시노마")

    excel = win32com.client.Dispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    try:
        print("\n① 웨이브테이블")
        update_wave_table(excel)
        print("\n② 웨이브 몬스터 테이블")
        update_monster_table(excel)
        print("\n③ 스트링 키 테이블")
        update_string_table(excel)
    finally:
        excel.Quit()

    print("\n끝. 다음: python Tools/sync_tables_to_assets.py")


if __name__ == "__main__":
    main()
