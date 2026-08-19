# -*- coding: utf-8 -*-
"""아니사킬(1102)의 서식지 타일을 표에 적는다 (2026-08-19).

유저 리포트: *"게임 시작 시 에픽 중립 몬스터인 아니사킬의 서식지(청크) 부분이 생성되지 않는
버그 발견. 원래 넣어놨는데 지금 인게임에서 구현이 안됐어."*

★ <b>타일 에셋은 이미 다 있다.</b> 110-6절이 만든 것이 그대로 살아 있다 —
  ``Resources/HabitatTiles/AnisakilHabitat``(바닥 16종) · ``…Edge``(16종) · ``…Props``(32종).
  전부 불투명하고 색조도 측정값(H 342°) 그대로다. <b>다시 만들 필요가 없다.</b>

⚠ <b>빠진 것은 표의 한 줄뿐이었다.</b> `habitat_design` 시트에는 카르시노스 한 줄만 있고
  아니사킬 줄이 <b>처음부터 없었다</b>(백업 15개를 2026-08-15 부터 전부 훑어 확인 —
  단 한 스냅샷에도 1102/1005 줄이 없다). 110-2절은 이 값을 넣었다고 적어놨지만
  <b>실제로 표에 저장되지 않았다.</b>

<b>왜 조용히 실패했나</b> — 그래서 발견이 늦었다:
    habitatTileAsset 빈칸
      → NeutralMonsterDefinitionSO.HabitatTileResourcePath 가 "" 를 돌려준다
      → NeutralMonsterSpawner.LoadHabitatTiles 가 <b>첫 줄에서</b> null 로 빠진다
        (경고를 찍는 코드는 그 아래에 있어서 <b>한 줄도 안 남는다</b>)
      → PaintHabitat 이 `ground == null` 로 조용히 return
    콘솔에 아무 흔적이 없고 <b>눈으로만</b> 발견된다. 그 구멍은 이 커밋에서 같이 막았다
    (에픽인데 타일 이름이 비면 경고 — NeutralMonsterSpawner.PaintHabitat).

<b>범위 값은 카르시노스와 같게 뒀다</b>(유저 지시: *"카르시노스처럼 너가 임의로 범위 값 잡아서"*).
`habitatRadiusTiles` 14 · `habitatChaseTiles` 8 · `habitatIdleSlackTiles` 1 —
이 셋은 <b>표에 없는 인스펙터 값</b>이고(sync 의 EPIC_HABITAT_SEED) 이미 그 값이 들어 있다.
110-2절이 두 에픽을 <b>같은 보상·같은 재생성·같은 콜라이더의 동급</b>으로 맞춰놨으므로
(성격 차이는 공격 +4 · 방어 −2 뿐) 서식지 크기까지 다르게 할 근거가 없다.
14 는 카르시노스로 실제 검증된 값이다 — 키우면 칠하는 칸이 제곱으로 늘어난다(510칸 → …).

⚠ 편집은 <b>Excel COM</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).
⚠ <b>`DispatchEx` 를 쓴다</b> — `EnsureDispatch`/`Dispatch` 는 유저가 엑셀을 켜 두면
  실패하거나 <b>유저 창에 붙어서 Quit 으로 닫아 버린다</b>(112-7절 함정 2).

사용법:  py -3 Tools/table_update_20260819_anisakil_habitat.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")
SHEET = "habitat_design"

#: mon_id → habitat_tile_asset
#: ⚠ 1101(카르시노스)도 같이 적는다 — 이미 맞는 값이면 건드리지 않고 확인만 한다(멱등).
ROWS = {
    1101: "CarcinosHabitat",
    1102: "AnisakilHabitat",   # ★ 이번에 신설 — 타일 에셋은 110-6절에 이미 있다
}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_아니사킬서식지")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(NEUTRAL_XLSX, os.path.join(dst, os.path.basename(NEUTRAL_XLSX)))
    print("백업:", dst)
    return dst


def find_col(ws, field, max_col=32):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def main():
    if not os.path.isfile(NEUTRAL_XLSX):
        print("⚠ 파일 없음:", NEUTRAL_XLSX)
        return 1

    import win32com.client as win32

    backup()

    # ⚠ DispatchEx — 항상 새 인스턴스. 유저가 열어 둔 엑셀 창과 완전히 분리된다(112-7절).
    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    changed = 0
    try:
        wb = excel.Workbooks.Open(NEUTRAL_XLSX)
        ws = wb.Worksheets(SHEET)

        c_id = find_col(ws, "mon_id") or 1
        c_tile = find_col(ws, "habitat_tile_asset")
        if not c_tile:
            print("⚠ habitat_tile_asset 컬럼을 못 찾음 — 아무것도 고치지 않았다")
            wb.Close(False)
            return 1

        last = ws.UsedRange.Rows.Count
        rows = {}
        for r in range(4, last + 1):
            v = ws.Cells(r, c_id).Value
            if v is None:
                continue
            try:
                rows[int(v)] = r
            except (TypeError, ValueError):
                continue

        for mid in sorted(ROWS):
            tile = ROWS[mid]
            r = rows.get(mid)
            if r is None:
                last += 1
                r = last
                ws.Cells(r, c_id).Value = mid
                print(f"  + mon_id {mid} 행 신설 (행 {r})")
                changed += 1

            cur = str(ws.Cells(r, c_tile).Value or "").strip()
            if cur == tile:
                print(f"  (이미 맞음) {mid} habitat_tile_asset = {tile}")
            else:
                ws.Cells(r, c_tile).Value = tile
                print(f"  {mid} habitat_tile_asset: '{cur}' → '{tile}'")
                changed += 1

        if changed:
            wb.Save()
            print("임시용 중립 몬스터.xlsx 저장 (%d 칸 변경)" % changed)
        else:
            print("바뀐 것이 없다 — 저장하지 않았다")
        wb.Close(False)
    finally:
        excel.Quit()

    print("\n다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
