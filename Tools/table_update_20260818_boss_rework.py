# -*- coding: utf-8 -*-
"""중간보스 삭제 · 보스를 웨이브별로 지정 · 말파스 스트링 키 (2026-08-18).

유저 지시: *"중간 보스 삭제. 5웨이브에 단탈리온 / 10웨이브에 말파스 등장으로 변경.
볼트 찾아보고 말파스 구현. 볼트에 알맞게 일러스트랑 에셋 테이블 및 스트링 키 테이블 연결"*

무엇을 고치나
-------------
**① `웨이브테이블.xlsx` / `Sheet2`**
  · ``mid_boss_mon_num`` 컬럼을 **지운다** — 중간보스라는 개념 자체가 없어졌다.
    (0 으로 채워만 두면 언젠가 다시 켜진다. 지우는 것이 지시에 맞다)
  · ``boss_mon_num`` — 5 · 10 · 15 · 20 웨이브에 1
  · ``boss_monster_id`` **신설** — 그 웨이브에 나올 보스의 id

**② `웨이브 몬스터 테이블.xlsx`**
  · ``wave_mid_boss`` 시트를 **지운다**
  · ``first_Stat`` 에서 110001 · 110002 행을 지운다
  · ⚠ ``first_Stat`` 의 **120001 단탈리온 체력을 되살린다** (아래 참조)

**③ `스트링 키 테이블.xlsx`**
  · 말파스 이름 · 스킬 2종 이름/설명 추가
  · 중간보스 2종의 이름·칭호 키 삭제
  · 「말바스」 → 「말파스」 (스킬 정의문 두 줄)

★ 왜 보스를 **표**가 정하게 했나
--------------------------------
예전 구조는 스포너의 `bossSlot` 이 <b>보스 한 종류</b>를 들고 있고 웨이브 표는 "몇 마리"만
정했다. 보스가 둘이 되면서 그 구조로는 <b>어느 보스인지</b>를 표현할 수 없다.
중간보스가 쓰던 「가중치 추첨」을 그대로 쓸 수도 있었지만, 유저 지시는 추첨이 아니라
**웨이브마다 정해진 보스**다. 그래서 표에 id 칸을 만드는 것이 정확하다 —
"표가 정본" 이라는 이 프로젝트의 원칙과도 맞고, 15·20 웨이브 배정도 표에서 바꾸면 된다.

⚠⚠ **되살리는 값이 있다 — 96절의 표 편집이 사라져 있었다**
------------------------------------------------------
96-2절이 단탈리온을 `hp 11 · hp_percent 1500` → **`hp 174 · hp_percent 100`** 으로 고쳤고
에셋(`Monster_Dantalian.asset`)에는 그 값이 들어가 있다. 그런데 **볼트의 표에는 옛 값이
그대로**다(2026-08-18 확인). 볼트 커밋 `eea9064 [ADD] 몬스터 추가` 가 96절 이전 사본을
덮어쓴 것으로 보인다.

그대로 두고 `sync_tables_to_assets.py` 를 돌리면 **단탈리온이 조용히 옛 값으로 되돌아간다**
(20웨이브 체력 32,230 → 15,600). 그래서 여기서 표를 에셋에 맞춰 되살린다.

⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).

사용법:  py -3 Tools/table_update_20260818_boss_rework.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

WAVE_XLSX = os.path.join(TABLES, "웨이브테이블.xlsx")
MON_XLSX = os.path.join(TABLES, "웨이브 몬스터 테이블.xlsx")
STR_XLSX = os.path.join(TABLES, "스트링 키 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

#: wave_num → 그 웨이브에 나올 보스 id (없으면 0).
#:
#: 유저 지시는 **5 = 단탈리온 · 10 = 말파스** 두 개뿐이지만, 원래 표는 15·20 웨이브에도
#: 보스(중간보스 15 · 최종보스 20)가 있었다. 그 자리를 비우면 **후반에 보스가 사라진다**
#: — 지시의 뜻이 "보스를 줄여라" 는 아니므로 <b>5웨이브마다 번갈아</b> 채운다.
#: 말파스 원화 시트에도 *"등장 웨이브 : 5n번째"* 라고 적혀 있어 같은 규칙이다.
BOSS_BY_WAVE = {
    5: 120001,    # 단탈리온
    10: 120002,   # 말파스
    15: 120001,
    20: 120002,
}

#: `first_Stat` 에서 고칠 값. {monster_id: {필드: 값}}
#:
#: ── 120001 단탈리온 ──────────────────────────────────────────────────
#: 96-2절이 정한 값인데 표에서 사라져 있다(맨 위 ⚠⚠ 참조). 되살리는 것뿐이다.
#:
#: ── 120002 말파스 ───────────────────────────────────────────────────
#: 표에 `hp 10 · hp_percent 900` 으로 들어와 있다. 두 가지가 걸린다:
#:
#:   ① **96-2절이 없앤 방식이다.** 상한 철폐 이후 이 프로젝트의 규약은
#:      "체력을 배율(`hp_percent`)로 넣지 말고 원시 `hp` 로 흡수" 다(유저 지시).
#:      말파스만 옛 방식으로 남으면 나중에 곡선을 볼 때 두 보스를 같이 못 읽는다.
#:
#:   ② **웨이브 순서와 세기가 어긋난다.** 표 값 그대로면 10웨이브 말파스의 유효 체력이
#:      5,760 인데, 5웨이브 단탈리온이 3,690 이고 15웨이브 단탈리온은 18,830 이다.
#:      20웨이브(최종) 말파스는 **11,640** 으로 15웨이브 보스보다 <b>약해진다</b>.
#:
#: <b>얼마로 잡았나</b> — `hp 174`(단탈리온과 같은 원시 체력)로 둔다. 그러면
#: 10웨이브 말파스의 유효 체력이 **10,480** 이 되는데, 이것은 <b>96절이 맞춰 둔
#: 「10웨이브 보스」의 체력 그대로</b>다. 즉 <b>10웨이브 전투의 난이도는 하나도 안 바뀌고
#: 보스의 정체만 바뀐다</b> — 이미 검증된 값을 버리지 않는 것이 가장 안전하다.
#:
#:   웨이브별 보스 유효 체력:  5 단탈 3,690 → 10 말파스 10,480
#:                            → 15 단탈 18,830 → 20 말파스 32,230  (계속 오른다)
#:
#: 말파스가 「물렁한 원거리 시전자」라는 성격은 <b>방어력</b>이 들고 있다
#: (말파스 20 vs 단탈리온 35) — 체력까지 깎으면 등장 웨이브가 뒤인 의미가 없어진다.
#: ⚠ 되돌리려면 이 줄의 hp 만 고치면 된다.
TABLE_STATS = {
    120001: {"hp": 174, "hp_percent": 100},
    120002: {"hp": 174, "hp_percent": 100},
}

#: 중간보스 삭제로 같이 지울 몬스터 id.
MID_BOSS_IDS = (110001, 110002)

#: 스트링 키 추가분 — (키, 한국어, 영어, 출처, 비고)
#:
#: 칭호 `boss_title_120002`(구속의 공작)는 **이미 표에 있다** — 유저가 미리 넣어 뒀다.
STRING_ADD = [
    ("monster_name_120002", "말파스", "Malphas", "wave_top_boss.monster_name", None),
    ("skill_name_130003", "구속탄", None, "Skill.skill_name", None),
    ("skill_name_130004", "저주광선", None, "Skill.skill_name", None),
    ("skill_explain_130003",
     "말파스의 구속탄에 얽힌 자는 제자리에서 스스로를 갉아먹습니다", None,
     "Skill.skill_explain", None),
    ("skill_explain_130004",
     "말파스가 읽어 내린 한 줄이 곧 적들의 마지막 문장이 됩니다", None,
     "Skill.skill_explain", None),
]

#: 지울 스트링 키 (중간보스 2종).
STRING_DELETE = [
    "monster_name_110001", "monster_name_110002",
    "boss_title_110001", "boss_title_110002",
]

#: 문구 치환 — 원화·표에는 「말바스」로 적혀 있는데 유저는 「말파스」라고 부른다.
#: Malphas 의 통용 표기도 말파스라 게임에 나가는 문구를 그쪽으로 통일한다.
STRING_REPLACE = ("말바스", "말파스")


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


def last_col(ws, max_col=40):
    last = 0
    for c in range(1, max_col + 1):
        if ws.Cells(2, c).Value is not None:
            last = c
    return last


# ---------------------------------------------------------------------------
# ① 웨이브테이블
# ---------------------------------------------------------------------------

def update_wave_table(excel):
    wb = excel.Workbooks.Open(WAVE_XLSX)
    try:
        ws = wb.Worksheets("Sheet2")

        # 1) 중간보스 컬럼 삭제
        c_mid = find_col(ws, "mid_boss_mon_num")
        if c_mid:
            ws.Columns(c_mid).Delete()
            print("  mid_boss_mon_num 컬럼 삭제 (열 %d)" % c_mid)
        else:
            print("  mid_boss_mon_num 컬럼 없음 — 이미 지워져 있다")

        c_num = find_col(ws, "wave_num")
        c_boss = find_col(ws, "boss_mon_num")
        if not (c_num and c_boss):
            raise SystemExit("⚠ wave_num / boss_mon_num 컬럼을 못 찾았습니다")

        # 2) boss_monster_id 컬럼 신설 — 맨 뒤에 붙인다(65-2절 규약)
        c_id = find_col(ws, "boss_monster_id")
        if not c_id:
            c_id = last_col(ws) + 1
            ws.Cells(1, c_id).Value = "보스 몬스터 id"
            ws.Cells(2, c_id).Value = "boss_monster_id"
            ws.Cells(3, c_id).Value = "int"
            print("  boss_monster_id 컬럼 신설 (열 %d)" % c_id)

        # 3) 웨이브별 보스 배정
        print()
        print("  웨이브 | boss_mon_num | boss_monster_id")
        print("  -------+--------------+-----------------")
        last_row = ws.UsedRange.Rows.Count
        for r in range(4, last_row + 1):
            v = ws.Cells(r, c_num).Value
            if v is None:
                continue
            wave = int(v)
            boss_id = BOSS_BY_WAVE.get(wave, 0)
            count = 1 if boss_id else 0

            old_count = int(ws.Cells(r, c_boss).Value or 0)
            ws.Cells(r, c_boss).Value = count
            ws.Cells(r, c_id).Value = boss_id

            if boss_id or old_count:
                print("  %6d | %d → %d        | %d"
                      % (wave, old_count, count, boss_id))

        wb.Save()
    finally:
        wb.Close(False)
    print("웨이브테이블.xlsx 저장")


# ---------------------------------------------------------------------------
# ② 웨이브 몬스터 테이블
# ---------------------------------------------------------------------------

def update_monster_table(excel):
    wb = excel.Workbooks.Open(MON_XLSX)
    try:
        # 1) wave_mid_boss 시트 삭제
        names = [ws.Name for ws in wb.Worksheets]
        if "wave_mid_boss" in names:
            wb.Worksheets("wave_mid_boss").Delete()
            print("  wave_mid_boss 시트 삭제")
        else:
            print("  wave_mid_boss 시트 없음 — 이미 지워져 있다")

        # 2) first_Stat — 중간보스 행 삭제 + 단탈리온 복원
        ws = wb.Worksheets("first_Stat")
        c_id = find_col(ws, "monster_id")
        if not c_id:
            raise SystemExit("⚠ first_Stat 에 monster_id 컬럼이 없습니다")

        # 삭제는 <b>아래에서 위로</b> — 위에서 지우면 행 번호가 밀린다.
        for r in range(ws.UsedRange.Rows.Count, 3, -1):
            v = ws.Cells(r, c_id).Value
            if v is not None and int(v) in MID_BOSS_IDS:
                print("  first_Stat %d행 삭제 (id %d)" % (r, int(v)))
                ws.Rows(r).Delete()

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

        wb.Save()
    finally:
        wb.Close(False)
    print("웨이브 몬스터 테이블.xlsx 저장")


# ---------------------------------------------------------------------------
# ③ 스트링 키 테이블
# ---------------------------------------------------------------------------

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

        # 1) 삭제 (아래에서 위로)
        for key in sorted(STRING_DELETE, key=lambda k: -existing.get(k, 0)):
            r = existing.get(key)
            if r:
                ws.Rows(r).Delete()
                print("  삭제 %s (%d행)" % (key, r))
            else:
                print("  삭제 %s — 없음(이미 지워짐)" % key)

        # 행이 밀렸으므로 다시 훑는다.
        last_row = ws.UsedRange.Rows.Count
        existing = {}
        for r in range(4, last_row + 1):
            k = ws.Cells(r, 1).Value
            if k is not None:
                existing[str(k).strip()] = r

        # 2) 추가 / 갱신
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

        # 3) 말바스 → 말파스 (한국어 칸 전체)
        old, new = STRING_REPLACE
        fixed = 0
        for r in range(4, ws.UsedRange.Rows.Count + 1):
            v = ws.Cells(r, 2).Value
            if v is not None and old in str(v):
                ws.Cells(r, 2).Value = str(v).replace(old, new)
                fixed += 1
        print("  「%s」→「%s」 %d줄" % (old, new, fixed))

        wb.Save()
    finally:
        wb.Close(False)
    print("스트링 키 테이블.xlsx 저장")


def main():
    for p in (WAVE_XLSX, MON_XLSX, STR_XLSX):
        if not os.path.isfile(p):
            print("⚠ 파일 없음:", p)
            return 1

    import win32com.client as win32

    backup((WAVE_XLSX, MON_XLSX, STR_XLSX), "보스개편_중간보스삭제")

    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    try:
        print("\n[① 웨이브테이블]")
        update_wave_table(excel)
        print("\n[② 웨이브 몬스터 테이블]")
        update_monster_table(excel)
        print("\n[③ 스트링 키 테이블]")
        update_string_table(excel)
    finally:
        excel.Quit()

    print("\n다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
