# -*- coding: utf-8 -*-
"""아니사킬(1005) · 고르도네(1006) 표 정리 (2026-08-19).

유저 지시: *"지금 볼트에 테이블 새로 넣었거든? … 볼트에 있는 데이터 값 기준으로 보스 몹
아나사킬이랑 일반 몹 고르도네 구현해줘 에셋 이름이나 일러스트 이름 잘못 된거 있으면 바꿔주고
스탯 같은건 적절하지 못하다 생각하면 바꿔도 됨 이유 설명해주고, 칼럼에 존재해야 하는데 존재
하지 않거나 하는 것들은 다 데이터 값 일단 테이블에 넣어놓고 그 담에 게임에 적용해줘"*

무엇을 고치나 — 표를 읽고 대조해서 찾은 것만
============================================

**① 이름 표기가 세 갈래였다 → `Anisakil` 로 통일**

    표(neutrality_mon)     Anisakil_illust · Anisakil_asset
    원화 파일              anisakil_asset.png
    일러스트 파일          Anasakil_illust.webp        ← 이것만 다르다
    스트링 설명문          "아니사킬을 중심으로 …"
    원화 시트 제목         "ANISAKIL — LEFT / RIGHT MOTION GUIDE"

  <b>다수도 어원도 `Anisakil` 이다</b>(아니사키스 = 기생 선충 Anisakis → 아니사킬, 칭호가
  "끈적이는 보랏빛 포식자" 인 것과 맞는다). 그래서 <b>표는 그대로 두고 일러스트 파일 이름을
  고친다</b>(파일 복사는 `import_monster_illust.py` 가 한다). 유저가 지시에서 쓴 "아나사킬" ·
  "아나시킬" 도 같은 대상이지만, 게임에 뜨는 이름은 스트링 테이블이 정본이라 그쪽을 따른다.

**② 고르도네는 표가 `Gordonae` 인데 파일이 `Gordone` 이다 → ★ <b>표를 따른다</b>**

    표(neutrality_mon)     Gordonae_illust · Gordonae_asset     ← <b>정본</b>
    볼트 원화 파일          Gordone_illust.png · Gordone_asset.png

  ⚠⚠ <b>처음엔 표를 고쳤다가 되돌렸다.</b> 파일 2개와 유저가 부른 이름("고르도네")이
    `Gordone` 이라 표가 소수라고 봤는데, 유저가 <b>"표 기준이 맞으니까 표 기준으로 일러스트나
    스킨 에셋 이름을 바꿔"</b> 로 정리했다. 그래서 <b>표는 손대지 않고</b> 프로젝트 쪽 에셋
    이름을 `Gordonae_*` 로 맞춘다:

      Resources/MonsterSkins/<b>Gordonae</b>/Skin_<b>Gordonae</b>.asset
      Resources/Illust/<b>Gordonae_illust</b>.png
      씬 템플릿 <b>Gordonae_Template</b>

  ★ <b>볼트 원화 파일 이름은 안 바꾼다</b> — 그건 입력이고, 빌더 스크립트가 경로를 직접
    가리킨다. 바꿔야 하는 것은 <b>게임이 표를 보고 찾아가는 이름</b>뿐이다.
  ⚠ 이 칸이 어긋나면 게임은 <b>조용히 기본 스킨</b>으로 나온다 — 스킨 폴더를 못 찾으면
    `CharacterAnimator` 가 경고 한 줄만 남기고 넘어가기 때문이다.

**③ `first_Stat` 에 1006 행이 아예 없다 → 새로 만든다**

  능력치가 없으면 `sync_tables_to_assets.py` 가 <b>기존 값을 유지</b>하는데, 새 종은 기존
  값도 없어서 코드 기본값(hp 3 · 공격 0)으로 나온다 — 공격력 0 인 원거리 몹이 된다.

  넣는 값과 <b>근거</b> — 같은 띠(spawn 100~199)·같은 보상(26~44)·같은 재생성(13초)인
  <b>1002 종양귀</b>를 기준으로 잡았다:

      항목        1002 종양귀   1006 고르도네   왜
      hp                8          10         혼자 나온다(group_making=0) — 종양귀는 3마리
      ranged_atk        6           7            떼로 오는 쪽과 총 화력을 맞추려면 개체가 세야 한다
      def               2           3
      atk_speed         5           4         무거운 돔 형태 — 느린 대신 한 방이 세다
      movement_speed    3           2
      accuracy         50          55         원거리는 명중이 실제로 쓰인다(명중 판정 대상)
      critical          3           3         그대로
      hp_recovery       1           1         그대로
      resistance       50          50         그대로
      melee_atk         0           0         원거리 종

  ★ <b>보상이 같으면 개체 전력도 같아야 한다</b>가 이 표의 기준선이다(79절 이후). 혼자
    나오는 종이 떼로 오는 종과 같은 보상을 주면 <b>혼자 나오는 쪽이 사냥하기 쉬운 만큼
    이득</b>이 되므로, 그 차이를 개체 능력치로 메운다.

**④ 1005 의 체력이 카르시노스의 <b>절반</b>이다 → 20 → 40**

      항목       1004 카르시노스   1005 아니사킬(표)   → 고친 값
      hp               40                20              <b>40</b>
      melee_atk        24                28              28 (그대로)
      def              12                10              10 (그대로)
      hp_recovery       6                 8               8 (그대로)
      보상          400~600           400~600          같다
      재생성           600초            600초           같다
      등장 범위      200~320          200~320          같다
      콜라이더       11 x 7.5         11 x 7.5         같다

  보상·재생성·등장범위·콜라이더가 <b>전부 같은데</b> 체력만 절반이다. 그러면 아니사킬은
  <b>같은 값을 주면서 절반의 시간에 죽는</b> 상위 사냥감이 되어, 두 에픽 중 한쪽만 잡는 것이
  언제나 이득이 된다. 20 은 <b>40 의 오타로 보인다</b>(다른 칸은 전부 카르시노스 대비 ±2 로
  붙어 있다).

  ★ <b>성격 차이는 남긴다</b> — 공격 +4 · 방어 −2 · 재생 +2 는 그대로 뒀다. "더 세게 때리고
    덜 버티는 에픽" 이라는 결이 살아 있고, 체력만 같은 급으로 올린 것이다.

  ⚠ 체력 40 은 실제 체력 440 이다(`hpBase 40 + hp x hpPerStat 10`).

**⑤ `habitat_design` 에 1005 행이 없다 → 미리 넣는다**

  1005 는 `mon_type=epic` 이라 <b>서식지(청크)를 갖는다</b>. 그 칸이 비면
  `NeutralMonsterSpawner` 가 서식지를 아예 안 그린다. 유저가 *"아나시킬 청크 에셋은 잠시
  기다려"* 라고 했으므로 <b>이름만 먼저 넣고</b>(`AnisakilHabitat`) 타일은 나중에 굽는다.

  ⚠ 폴더가 없는 동안은 `Resources/HabitatTiles/AnisakilHabitat` 조회가 빈 배열을 돌려주고,
    `NeutralHabitat.Paint` 는 <b>바닥 후보가 비면 아무것도 하지 않는다</b> — 경고 한 줄만
    나고 게임은 정상으로 돈다. 그래서 지금 넣어도 안전하다.

**⑥ 중립 `Skill` 시트에 `mentalerror_damage` 칸이 없다 → 만든다 (값 0)**

  같은 `BossSkillSO` 에셋을 <b>웨이브 보스와 중립 에픽이 공유</b>하는데, 웨이브 쪽 `Skill`
  시트에만 이 칸이 있다. 그래서 중립 스킬은 `erosionValue` 가 <b>표에 없는 값</b>이 되어
  코드 기본값 0 으로 나갔다 — 지금까지 조용히 0 이었다.

  ★ <b>값은 0 을 그대로 넣는다.</b> 29절이 정한 규칙이 *"중립 몬스터 사냥은 침식을 올리지
    않는다"* 이므로 0 이 <b>설계상 맞는 값</b>이다. 바뀐 것은 그 0 이 이제 <b>표에 적혀 있다</b>는
    것이다 — 올리려면 코드가 아니라 표를 고치면 된다.

**⑦ 스트링 키 6줄이 없고, 설명문 하나에 오타가 있다**

  없는 키: `mon_name_1005` · `mon_name_1006` · `skill_name_2003` · `skill_name_2004` ·
  `skill_explain_2001~2004`(2001·2002 는 <b>전부터</b> 없었다 — 같이 채운다).
  키가 없으면 화면에 <b>키 문자열이 그대로 뜬다</b>(51-4절).

  오타: `skill_type_desc_Huge_threat` 의 "부정<b>저</b>인" → "부정<b>적</b>인".
  스킬 상세창에 그대로 뜨는 문장이라 고친다.

  ⚠ `skill_name_2004` 는 원화 시트가 "거대한 위협 <b>표효</b>" 인데 <b>포효</b>가 맞다
    (영문이 GREAT THREAT ROAR 다). 고쳐 넣는다.

⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).

사용법:  python Tools/table_update_20260819_anisakil_gordone.py
다음:    python Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
STR_XLSX = os.path.join(TABLES, "스트링 키 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

# ── ① 이름 오기 정정 (neutrality_mon) ─────────────────────────────────────
#: mon_id → {필드: 새 값}
# ★ 표를 정본으로 확정했으므로(위 ②) <b>고칠 것이 없다.</b> 되돌린 흔적을 남겨 둔다 —
#   다음 세션이 "파일 이름과 다른데?" 를 다시 발견하고 또 표를 고치지 않게.
NAME_FIX = {
    # 1006: 표의 Gordonae_* 가 정본이다. 프로젝트 에셋 이름을 그쪽에 맞췄다.
}

# ── ③④ first_Stat ────────────────────────────────────────────────────────
#: mon_id → {필드: 값}. 있으면 갱신, 없으면 새 행을 만든다.
STAT_SET = {
    # ④ 체력만 카르시노스와 같은 급으로 (맨 위 설명 참조)
    1005: {"hp": 40},
    # ③ 행 자체가 없었다
    1006: {
        "hp": 10, "melee_atk": 0, "accuracy": 55, "critical": 3, "def": 3,
        "hp_recovery": 1, "atk_speed": 4, "movement_speed": 2, "resistance": 50,
        "ranged_atk": 7, "magic": 0, "cure": 0,
    },
}

# ── ⑤ habitat_design ─────────────────────────────────────────────────────
HABITAT_SET = {
    1005: "AnisakilHabitat",     # ⚠ 타일은 유저 대기 중 — 이름만 먼저
}

# ── ⑥ 중립 Skill 시트에 없는 칸 ──────────────────────────────────────────
#: (필드명, 한글 헤더, 형, skill_id → 값)
SKILL_NEW_COLUMN = ("mentalerror_damage", "침식 수치 상승", "float",
                    {2001: 0, 2002: 0, 2003: 0, 2004: 0})

# ── ⑦ 스트링 키 ──────────────────────────────────────────────────────────
#: (키, 한국어, 영어, 출처, 비고)
STRING_ADD = [
    ("mon_name_1005", "아니사킬", "Anisakil", "neutrality_mon.mon_name",
     "원화 시트 제목 ANISAKIL · 설명문 '아니사킬' 과 통일"),
    ("mon_name_1006", "고르도네", "Gordone", "neutrality_mon.mon_name", None),

    ("skill_name_2003", "치명적 꼬리 타격", "Deadly Tail Strike", "Skill.skill_name", None),
    ("skill_name_2004", "거대한 위협 포효", "Great Threat Roar", "Skill.skill_name",
     "원화 시트의 '표효' 는 오타 — 영문이 ROAR 다"),

    # 스킬 설명은 <b>수치 설명이 아니라 한 줄 분위기</b>다(수치는 skill_type_desc_*).
    ("skill_explain_2001", "스친 자리마다 껍질이 한 겹씩 벗겨져 나갑니다", None,
     "Skill.skill_explain", "2001·2002 는 전부터 비어 있었다"),
    ("skill_explain_2002", "그 울음을 들은 다리는 저절로 뒤로 물러섭니다", None,
     "Skill.skill_explain", "2001·2002 는 전부터 비어 있었다"),
    ("skill_explain_2003", "지나간 뒤에야 무엇에 베였는지 알게 됩니다", None,
     "Skill.skill_explain", None),
    ("skill_explain_2004", "거대한 것이 몸을 세우면 다리가 먼저 굳습니다", None,
     "Skill.skill_explain", None),
]

#: (키, 찾을 문자열, 바꿀 문자열) — 이미 있는 줄의 오타만 고친다.
STRING_FIX = [
    ("skill_type_desc_Huge_threat", "부정저인", "부정적인"),
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


def find_row_by_id(ws, id_col, wanted, first_row=4):
    """id 컬럼에서 값이 같은 행 번호. 없으면 0. 같이 「마지막 데이터 행」도 돌려준다."""
    last = first_row - 1
    for r in range(first_row, ws.UsedRange.Rows.Count + 1):
        v = ws.Cells(r, id_col).Value
        if v in (None, ""):
            continue
        last = r
        if int(v) == wanted:
            return r, last
    return 0, last


# ---------------------------------------------------------------------------

def update_neutral_names(ws):
    """① 이름 오기 정정."""
    c_id = find_col(ws, "mon_id")
    if not c_id:
        raise SystemExit("⚠ neutrality_mon 에 mon_id 컬럼이 없습니다")

    for mid, fields in NAME_FIX.items():
        r, _ = find_row_by_id(ws, c_id, mid)
        if not r:
            print("  ! neutrality_mon 에 %d 행이 없습니다" % mid)
            continue
        for field, value in fields.items():
            c = find_col(ws, field)
            if not c:
                print("  ! %s 컬럼이 없습니다" % field)
                continue
            old = ws.Cells(r, c).Value
            if str(old or "").strip() == value:
                continue
            ws.Cells(r, c).Value = value
            print("  ★ %d %s : %s → %s" % (mid, field, old, value))


def update_neutral_stats(ws):
    """③④ first_Stat — 없는 행은 만들고, 있는 행은 지정한 칸만 고친다."""
    c_id = find_col(ws, "mon_id")
    if not c_id:
        raise SystemExit("⚠ first_Stat 에 mon_id 컬럼이 없습니다")

    for mid, fields in STAT_SET.items():
        r, last = find_row_by_id(ws, c_id, mid)
        if not r:
            r = last + 1
            ws.Cells(r, c_id).Value = mid
            print("  + first_Stat %d 행 신설 (%d행)" % (mid, r))

        for field, value in fields.items():
            c = find_col(ws, field)
            if not c:
                print("  ! first_Stat 에 %s 컬럼이 없습니다" % field)
                continue
            old = ws.Cells(r, c).Value
            if old is not None and float(old) == float(value):
                continue
            ws.Cells(r, c).Value = value
            print("  ★ %d %s : %s → %s" % (mid, field, old, value))


def update_habitat(ws):
    """⑤ habitat_design — 에픽에 서식지 타일 이름을 미리 넣는다."""
    c_id = find_col(ws, "mon_id")
    c_tile = find_col(ws, "habitat_tile_asset")
    if not (c_id and c_tile):
        raise SystemExit("⚠ habitat_design 컬럼을 못 찾았습니다")

    for mid, name in HABITAT_SET.items():
        r, last = find_row_by_id(ws, c_id, mid)
        if not r:
            r = last + 1
            ws.Cells(r, c_id).Value = mid
            print("  + habitat_design %d 행 신설 (%d행)" % (mid, r))
        old = ws.Cells(r, c_tile).Value
        if str(old or "").strip() == name:
            continue
        ws.Cells(r, c_tile).Value = name
        print("  ★ %d habitat_tile_asset : %s → %s" % (mid, old, name))


def update_skill_column(ws):
    """
    ⑥ 없는 칸을 <b>맨 끝에</b> 만든다.

    ⚠ <b>가운데에 끼워 넣지 않는다.</b> 웨이브 쪽 시트는 `skill_explain` 다음에 이 칸이
      있지만, 표에는 하이퍼링크가 걸려 있어 컬럼을 삽입하면 참조가 밀린다(UI-17절).
      읽는 쪽(`sync_tables_to_assets.read_rows`)은 <b>필드명으로</b> 찾으므로 위치는
      아무 상관이 없다.
    """
    field, header_kr, kind, values = SKILL_NEW_COLUMN
    c_id = find_col(ws, "skill_id")
    if not c_id:
        raise SystemExit("⚠ Skill 에 skill_id 컬럼이 없습니다")

    c = find_col(ws, field)
    if not c:
        c = ws.UsedRange.Columns.Count + 1
        ws.Cells(1, c).Value = header_kr
        ws.Cells(2, c).Value = field
        ws.Cells(3, c).Value = kind
        print("  + Skill 에 %s 컬럼 신설 (%d열)" % (field, c))

    for r in range(4, ws.UsedRange.Rows.Count + 1):
        v = ws.Cells(r, c_id).Value
        if v in (None, ""):
            continue
        sid = int(v)
        if sid not in values:
            continue
        old = ws.Cells(r, c).Value
        if old is not None and float(old) == float(values[sid]):
            continue
        ws.Cells(r, c).Value = values[sid]
        print("  ★ Skill %d %s : %s → %s" % (sid, field, old, values[sid]))


def update_neutral(excel):
    wb = excel.Workbooks.Open(NEUTRAL_XLSX)
    try:
        print("  neutrality_mon")
        update_neutral_names(wb.Worksheets("neutrality_mon"))
        print("  first_Stat")
        update_neutral_stats(wb.Worksheets("first_Stat"))
        print("  habitat_design")
        update_habitat(wb.Worksheets("habitat_design"))
        print("  Skill")
        update_skill_column(wb.Worksheets("Skill"))
        wb.Save()
    finally:
        wb.Close(False)
    print("임시용 중립 몬스터.xlsx 저장")


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
                print("  + %s = %s" % (key, kr))
            else:
                print("  ~ %s = %s" % (key, kr))
            ws.Cells(r, 2).Value = kr
            if en is not None:
                ws.Cells(r, 3).Value = en
            if src is not None:
                ws.Cells(r, 4).Value = src
            if note is not None:
                ws.Cells(r, 5).Value = note

        for key, bad, good in STRING_FIX:
            r = existing.get(key)
            if r is None:
                print("  ! 오타를 고칠 %s 가 없습니다" % key)
                continue
            cur = str(ws.Cells(r, 2).Value or "")
            if bad not in cur:
                continue
            ws.Cells(r, 2).Value = cur.replace(bad, good)
            print("  ★ %s 오타 '%s' → '%s'" % (key, bad, good))

        wb.Save()
    finally:
        wb.Close(False)
    print("스트링 키 테이블.xlsx 저장")


def main():
    import win32com.client

    for p in (NEUTRAL_XLSX, STR_XLSX):
        if not os.path.isfile(p):
            raise SystemExit("⚠ 표가 없습니다: " + p)

    backup([NEUTRAL_XLSX, STR_XLSX], "아니사킬_고르도네")

    excel = win32com.client.Dispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    try:
        print("\n① 임시용 중립 몬스터")
        update_neutral(excel)
        print("\n② 스트링 키 테이블")
        update_string_table(excel)
    finally:
        excel.Quit()

    print("\n끝. 다음: python Tools/sync_tables_to_assets.py")


if __name__ == "__main__":
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    main()
