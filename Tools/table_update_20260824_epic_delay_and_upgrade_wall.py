# -*- coding: utf-8 -*-
"""에픽 첫 등장 지연 · Lv30 강화 벽 — <b>표에 기입</b>한다 (2026-08-24).

유저 지시: *"에픽 몬스터 너무 빨리 나옴 … 에픽 보스 몬스터의 생성 시간을 게임 시작 이후
300초 뒤로 수정"* · *"30lv 이상부터 강화 밸류 엄청 올리기 … 30LV 이상 부터는 강화에
소모되는 자원 소모량을 급진적으로 올려야 할듯"* · *"테이블 내에 변경한 데이터 반드시 기입"*.

무엇을 쓰나
-----------

**①** ``임시용 중립 몬스터.xlsx`` / ``neutrality_mon`` — **W열 `first_spawn_delay` 신설**

    잡몹 중립 1001~1004 = **0**(예전과 같이 시작과 함께) · 에픽 1101~1104 = **300**.

    ★ **맨 뒤에 붙인다** — 기존 열 위치를 밀지 않는다(54-5절 원칙 · 136-2절이 I열,
      136-3절이 V열을 같은 방식으로 붙였다).
    ★ 코드 쪽: ``NeutralMonsterDefinitionSO.firstSpawnDelaySeconds`` (0 이면 예전 동작) ·
      ``NeutralMonsterSpawner`` 의 ``_awaitingFirstSpawn`` · ``sync_tables_to_assets.py``.

**②** ``능력치 및 공식 정리.xlsx`` / ``계수`` — **Lv30 벽 계수 3줄 덧붙임**

    ⚠ **행을 끼워넣지 않는다** — 다른 시트가 절대 행 번호(``계수!$B$33``)로 참조한다
      (38-2절 · 136-4절이 같은 이유로 63행부터 붙였다). 그래서 **74행부터** 붙인다.

**③** ``능력치 및 공식 정리.xlsx`` / ``성장 시뮬레이션`` — Lv31~35·40 행을 실제 곡선으로

    Lv35 칸이 «비용 390 · 누적 7,740» 인데 그 값은 **선형 곡선의 값**이라 이제 틀렸다.
    벽이 보이도록 Lv31·32·33·34 를 채우고 Lv40 을 덧붙인다.

곡선 (코드 ``CharacterUpgradeService.CostForLevel`` 과 같은 식이다 — 아래 :func:`cost` 로
계산해 쓰므로 표와 코드가 어긋날 수 없다):

    n < 30 : 40 + 10n                        ← **예전 그대로**
    n ≥ 30 : (40 + 10×30) × 1.35^(n−30)      ← 10 단위 반올림

    Lv30 340 · Lv31 460 · Lv33 840 · Lv35 1,520 · Lv40 6,840

⚠ 편집은 **Excel COM · DispatchEx** — openpyxl 로 저장하면 서식·주석·수식 캐시가 상한다.

사용법:  py -3 Tools/table_update_20260824_epic_delay_and_upgrade_wall.py
다음:    py -3 Tools/sync_tables_to_assets.py   (표 → 중립 정의 에셋 8개)
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
STATS_XLSX = os.path.join(TABLES, "능력치 및 공식 정리.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

# ── ① 새 열 ───────────────────────────────────────────────────────────────
DELAY_COL = {
    1001: 0, 1002: 0, 1003: 0, 1004: 0,          # 잡몹 중립 — 예전과 같이 0초
    1101: 300, 1102: 300, 1103: 300, 1104: 300,  # 에픽 넷 — 유저 지시 300초
}
DELAY_HEAD = ("첫 등장 지연(초)", "first_spawn_delay", "float")

# ── ②·③ 강화 비용 곡선 (코드와 같은 식) ───────────────────────────────────
BASE_COST = 40
COST_PER = 10
STEEP_START = 30
STEEP_GROWTH = 1.35
ROUND_TO = 10


def cost(level):
    """``CharacterUpgradeService.CostForLevel`` 과 <b>같은 식</b>."""
    linear = BASE_COST + COST_PER * level
    if STEEP_START <= 0 or level < STEEP_START or STEEP_GROWTH <= 1.0:
        return linear
    at_start = BASE_COST + COST_PER * STEEP_START
    c = at_start * (STEEP_GROWTH ** (level - STEEP_START))
    if ROUND_TO > 1:
        c = round(c / ROUND_TO) * ROUND_TO
    return int(c)


def cumulative(level):
    """0 회부터 그 회차까지 누적 — 「성장 시뮬레이션」 시트의 C열 규칙과 같다."""
    return sum(cost(n) for n in range(0, level + 1))


# 계수 시트에 덧붙일 줄 (이름, 값, 필드명, 의미)
COEF_ROWS = [
    ("■ 강화 비용의 Lv30 벽 (2026-08-24 신설)", None, None, None),
    ("강화 급등 시작 레벨", STEEP_START, "CharacterUpgradeService.steepStartLevel",
     "★★ 이 강화 횟수(=Lv)부터 비용이 선형에서 등비로 꺾인다. Lv30 에 «도달하는» 비용은 "
     "한 톨도 안 변한다 — 136절 경제 모델(w30 = Lv30)이 그대로 유효하다. 0 이면 꺾이지 않는다"),
    ("강화 급등 배율/레벨", STEEP_GROWTH, "CharacterUpgradeService.steepGrowthPerLevel",
     "Lv30 이후 레벨 하나당 비용에 곱하는 값. 1.35 → Lv30 340 · Lv35 1,520(선형의 3.9배) · "
     "Lv40 6,840(15.5배). 밸런스 기획서의 «후반부는 성장/생성에 하드캡» 을 값으로 옮긴 자리"),
    ("강화 비용 반올림 단위", ROUND_TO, "CharacterUpgradeService.costRoundTo",
     "등비 구간의 비용을 이 단위로 반올림한다. 이 게임의 다른 비용이 전부 10 단위여서 자리를 맞췄다"),
]

# 성장 시뮬레이션 시트 — 22행(Lv30)까지는 그대로 두고 23행부터 다시 쓴다.
#   열: A 회차 · B 비용 · C 누적 · D 평균 상승폭(주력) · E 주력(시작 10) · F 일반(시작 5) · G 비고
FOCUS_PER = 2.62
PLAIN_PER = 1.62
SIM_ROWS = [
    (31, "★ 여기서부터 등비 — 비용이 선형의 1.3배"),
    (32, None),
    (33, None),
    (34, None),
    (35, "주력 상한 100 도달 = 「무난하게 승리」 · 비용은 선형의 3.9배"),
    (40, "무한 모드 — 비용이 선형의 15.5배. 사실상 여기가 천장이다"),
]


def backup(paths, tag):
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_" + tag)
    os.makedirs(dst, exist_ok=True)
    for p in paths:
        shutil.copy2(p, os.path.join(dst, os.path.basename(p)))
    print("backup:", dst)


def find_col(ws, field, max_col=40):
    """2행이 필드명 줄이다(1행 한글 · 2행 필드 · 3행 타입 · 4행부터 값)."""
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def last_used_col(ws, row=2, max_col=60):
    last = 0
    for c in range(1, max_col + 1):
        if ws.Cells(row, c).Value not in (None, ""):
            last = c
    return last


def add_delay_column(wb):
    print("[① 임시용 중립 몬스터 / neutrality_mon]")
    ws = wb.Worksheets("neutrality_mon")

    col = find_col(ws, DELAY_HEAD[1])
    if col:
        print("    이미 %s 열이 있습니다 (%d열) — 값만 맞춥니다" % (DELAY_HEAD[1], col))
    else:
        col = last_used_col(ws) + 1
        # ★ 서식은 <b>왼쪽 열에서 복사</b>한다 — 손으로 맞추면 반드시 어긋난다.
        ws.Columns(col - 1).Copy()
        ws.Columns(col).PasteSpecial(-4122)          # xlPasteFormats
        wb.Application.CutCopyMode = False
        for i, text in enumerate(DELAY_HEAD):
            ws.Cells(1 + i, col).Value = text
        print("    %d열에 %s 신설 (머리 3줄)" % (col, DELAY_HEAD[1]))

    c_id = find_col(ws, "mon_id") or 1
    last = ws.UsedRange.Rows.Count
    n, seen = 0, set()
    for r in range(4, last + 1):
        raw = ws.Cells(r, c_id).Value
        if raw is None:
            continue
        try:
            mid = int(raw)
        except (TypeError, ValueError):
            continue
        if mid not in DELAY_COL:
            continue
        seen.add(mid)
        want = DELAY_COL[mid]
        if ws.Cells(r, col).Value == want:
            continue
        ws.Cells(r, col).Value = want
        n += 1
        print("    %d행 %d → %s초" % (r, mid, want))
    missing = sorted(set(DELAY_COL) - seen)
    if missing:
        print("    ⚠ 표에서 못 찾은 mon_id: %s" % missing)
    return n


def add_coef_rows(wb):
    print("[② 능력치 및 공식 정리 / 계수]")
    ws = wb.Worksheets("계수")

    # 이미 붙였는지 — 필드명 칸(C열)으로 본다.
    last = ws.UsedRange.Rows.Count
    for r in range(1, last + 1):
        v = ws.Cells(r, 3).Value
        if v and "steepStartLevel" in str(v):
            print("    이미 %d행에 있습니다 — 값만 맞춥니다" % r)
            base = r - 1
            break
    else:
        base = last + 1                     # ⚠ 끼워넣지 않는다 — 맨 아래에 붙인다
        print("    %d행부터 덧붙입니다" % base)

    n = 0
    for i, row in enumerate(COEF_ROWS):
        r = base + i
        for c, val in enumerate(row, start=1):
            if val is None:
                continue
            if ws.Cells(r, c).Value != val:
                ws.Cells(r, c).Value = val
                n += 1
    print("    계수 %d칸" % n)
    return n


def rewrite_growth_sim(wb):
    print("[③ 능력치 및 공식 정리 / 성장 시뮬레이션]")
    ws = wb.Worksheets("성장 시뮬레이션")

    # Lv30 줄을 찾는다 — 그 아래부터 다시 쓴다(그 위는 선형 구간이라 값이 그대로 맞다).
    last = ws.UsedRange.Rows.Count
    row30 = 0
    for r in range(4, last + 1):
        if ws.Cells(r, 1).Value == 30:
            row30 = r
            break
    if row30 == 0:
        print("    ! Lv30 줄을 못 찾았습니다 — 건너뜁니다")
        return 0

    n = 0
    for i, (lv, note) in enumerate(SIM_ROWS):
        r = row30 + 1 + i
        focus = min(100.0, 10 + FOCUS_PER * lv)
        plain = 5 + PLAIN_PER * lv
        vals = [lv, cost(lv), cumulative(lv), FOCUS_PER,
                round(focus, 1), round(plain, 1), note]
        for c, val in enumerate(vals, start=1):
            if val is None:
                continue
            if ws.Cells(r, c).Value != val:
                ws.Cells(r, c).Value = val
                n += 1
        print("    %d행 Lv%-2d 비용 %6s · 누적 %7s" % (r, lv, f"{cost(lv):,}", f"{cumulative(lv):,}"))
    print("    성장 시뮬레이션 %d칸" % n)
    return n


def main():
    for p in (NEUTRAL_XLSX, STATS_XLSX):
        if not os.path.isfile(p):
            print("파일이 없습니다:", p)
            return 1

    import win32com.client as win32

    backup((NEUTRAL_XLSX, STATS_XLSX), "에픽지연_강화벽")

    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    try:
        wb = excel.Workbooks.Open(NEUTRAL_XLSX)
        n1 = add_delay_column(wb)
        if n1:
            wb.Save()
        wb.Close(SaveChanges=False)

        wb = excel.Workbooks.Open(STATS_XLSX)
        n2 = add_coef_rows(wb) + rewrite_growth_sim(wb)
        if n2:
            wb.Save()
        wb.Close(SaveChanges=False)
    finally:
        excel.Quit()

    print()
    print("Lv : 비용 (선형 대비)")
    for lv in (29, 30, 31, 32, 33, 34, 35, 40):
        lin = BASE_COST + COST_PER * lv
        print("  %2d : %8s  (선형 %s · x%.1f)" % (lv, f"{cost(lv):,}", f"{lin:,}", cost(lv) / lin))
    print()
    print("총 %d칸을 고쳤습니다." % (n1 + n2))
    print("다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
