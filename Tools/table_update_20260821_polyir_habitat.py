# -*- coding: utf-8 -*-
"""폴리르(1104) 서식지 줄을 `habitat_design` 에 추가한다 (2026-08-21).

유저 지시: *"폴리르 서식지 청크 이미지 넣었으니까 분석해서 타일맵 만들고 (스프라이트
폴더에 있음) 테이블에도 값 추가해줘"*

★★ **이 한 줄이 없으면 서식지가 통째로 안 그려진다** — 그리고 조용하다
=======================================================================
118절에서 아니사킬이 정확히 그랬다: 타일은 다 있는데 `habitat_design` 에 줄이 없어서
`habitatTileAsset` 이 빈 칸이었고, 그러면 `LoadHabitatTiles` 가 **첫 줄에서 null 로 빠져**
경고 코드에 도달조차 못 했다. 그때 «에픽인데 비어 있으면 경고» 를 넣어 두었으므로 이제는
콘솔에 뜨지만, 애초에 줄을 넣는 것이 정답이다.

지금 `habitat_design` 에는 1101(카르시노스) · 1102(아니사킬) 두 줄뿐이고 **1104 이 없다.**

값을 어떻게 정했나
==================
| 컬럼 | 값 | 근거 |
|---|---|---|
| `habitat_tile_asset` | `PolyirHabitat` | `gen_polyir_habitat_tiles.py` 가 만든 폴더 이름. 코드가 여기에 `Edge`·`Props` 를 붙여 찾는다(`NeutralMonsterDefinitionSO.HabitatPath`) |
| `habitat_radius_tiles` | **14** | 카르시노스·아니사킬과 같게 |
| `habitat_chase_tiles` | **8** | 〃 |
| `habitat_idle_slack_tiles` | **1** | 〃 |

★ 셋 다 앞의 두 에픽과 **같은 값**이다. 110-2절이 두 에픽을 «같은 보상·재생성·콜라이더의
동급» 으로 맞춰 놨고, 폴리르도 `neutrality_mon` 에서 완전히 같은 등급이다
(등장 범위 200~320 · 에너지 400~600 · 최대 1마리 · 재생성 600초 · 콜라이더 7.5x11).
따로 정할 근거가 없으면 같은 값이 맞다 — 다르게 하려면 그 이유가 표에 있어야 한다.

⚠ 「생성될 때마다 랜덤 서식지 배정 · 저장 시 위치 보존」은 **이미 되어 있다.**
  새로 만들 것이 없다 — `NeutralMonsterSpawner` 가 스폰 위치에 서식지를 칠하고
  (`PaintHabitat`), 99-9절이 «중립 몬스터 수·서식지 위치 유지» 를 저장에 넣었다.
  폴리르는 그 경로를 **그대로 탄다** — 빠져 있던 것은 타일과 이 표 한 줄뿐이다.

⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).

사용법:  py -3 Tools/table_update_20260820_variola_habitat.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

XLSX = "임시용 중립 몬스터.xlsx"
SHEET = "habitat_design"
BACKUP_ROOT = os.path.join(TABLES, "_백업")
FIRST_DATA_ROW = 4

#: mon_id → (타일 에셋, 반경, 추격거리, 유휴여유)
ROWS = {
    1104: ("PolyirHabitat", 14, 8, 1),
}

FIELDS = ("mon_id", "habitat_tile_asset", "habitat_radius_tiles",
          "habitat_chase_tiles", "habitat_idle_slack_tiles")


def check_lock():
    if os.path.isfile(os.path.join(TABLES, "~$" + XLSX)):
        raise SystemExit("⚠ 엑셀에서 열려 있습니다 — 닫고 다시 실행하세요: " + XLSX)


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_폴리르서식지")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(os.path.join(TABLES, XLSX), os.path.join(dst, XLSX))
    print("백업:", dst)


def find_col(ws, field, max_col=32):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def main():
    # 타일이 실제로 있는지 먼저 본다 — 표에만 적고 폴더가 없으면 게임이 경고만 남긴다.
    project = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    for asset, *_ in ROWS.values():
        folder = os.path.join(project, "Assets", "_Project", "Resources",
                              "HabitatTiles", asset)
        if not os.path.isdir(folder):
            raise SystemExit("⚠ 타일 폴더가 없습니다 (gen_polyir_habitat_tiles.py 를 "
                             "먼저 돌리세요): " + folder)

    check_lock()
    import win32com.client as win32

    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    try:
        wb = excel.Workbooks.Open(os.path.join(TABLES, XLSX))
        ws = wb.Worksheets(SHEET)

        cols = {k: find_col(ws, k) for k in FIELDS}
        missing = [k for k, v in cols.items() if not v]
        if missing:
            print("⚠ 컬럼을 못 찾음:", missing)
            wb.Close(False)
            return 1

        have = {}
        last = ws.UsedRange.Rows.Count
        for r in range(FIRST_DATA_ROW, last + 1):
            v = ws.Cells(r, cols["mon_id"]).Value
            if v is not None:
                have[int(v)] = r

        print("  기존 서식지 줄:", sorted(have))
        next_row = last + 1
        for mid, (asset, radius, chase, slack) in ROWS.items():
            if mid in have:
                r = have[mid]
                tag = "갱신"
            else:
                r = next_row
                next_row += 1
                tag = "추가"
            ws.Cells(r, cols["mon_id"]).Value = mid
            ws.Cells(r, cols["habitat_tile_asset"]).Value = asset
            ws.Cells(r, cols["habitat_radius_tiles"]).Value = radius
            ws.Cells(r, cols["habitat_chase_tiles"]).Value = chase
            ws.Cells(r, cols["habitat_idle_slack_tiles"]).Value = slack
            print("  %s %d행: %d · %s · 반경 %s · 추격 %s · 유휴 %s"
                  % (tag, r, mid, asset, radius, chase, slack))

        wb.Save()
        wb.Close()
        print("%s 저장" % XLSX)
    finally:
        excel.Quit()

    print()
    print("다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
