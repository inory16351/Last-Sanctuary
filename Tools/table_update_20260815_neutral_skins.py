# -*- coding: utf-8 -*-
"""중립 몬스터 1001~1003 의 `mon_skin` · `mon_illust` 칸을 채운다 (2026-08-15).

유저 지시: *"몬스터들 스킨 에셋 리소스 폴더 찾아보고 만들어."*

에셋을 만들어 놓기만 하면 게임에 안 붙는다 — <b>표가 정본</b>이라, 표의 두 칸을 채워야
`sync_tables_to_assets.py` 가 정의 에셋에 옮기고 스포너가 스킨을 붙인다.
1004(카르시노스)는 직전 세션이 이미 채웠으므로 건드리지 않는다.

    mon_skin   Tumor spider_asset   → 게임은 꼬리표(`_asset`)를 떼고 <b>종 이름</b>만 쓴다
                                      (`NeutralMonsterDefinitionSO.SkinResourcePath`)
    mon_illust TumorSpider_illust   → `Resources/Illust/` 아래의 파일 이름

⚠ <b>`mon_skin` 은 원본 시트 파일 이름이 아니라 종 이름 기준으로 적는다.</b>
  볼트의 시트 파일은 `Tumor spider_asset.png`(공백) · `Tumor_mole_asset.png`(밑줄)로
  표기가 제각각인데, 게임이 찾는 폴더는 `Resources/MonsterSkins/TumorSpider` 다.
  표에는 <b>게임이 찾을 이름</b>을 적어야 한다 — 원본 파일 이름을 그대로 옮기면
  `Tumor spider` 라는 없는 폴더를 찾는다. (웨이브 몬스터의 `ingame_asset` 과 같은 규칙.)

⚠ 편집은 <b>Excel COM</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17 절 실사고).

사용법:  py -3 Tools/table_update_20260815_neutral_skins.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

#: mon_id → (mon_skin, mon_illust)
ROWS = {
    1001: ("TumorSpider_asset", "TumorSpider_illust"),
    1002: ("Tumorling_asset",   "Tumorling_illust"),
    1003: ("TumorMole_asset",   "TumorMole_illust"),
}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_중립스킨칸")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(NEUTRAL_XLSX, os.path.join(dst, os.path.basename(NEUTRAL_XLSX)))
    print("백업:", dst)


def find_col(ws, field, max_col=64):
    """2행(필드명)에서 컬럼 번호. ⚠ 앞뒤 공백을 반드시 제거한다(표에 실제로 섞여 있다)."""
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
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    try:
        wb = excel.Workbooks.Open(NEUTRAL_XLSX)
        ws = wb.Worksheets("neutrality_mon")

        c_skin = find_col(ws, "mon_skin")
        c_illust = find_col(ws, "mon_illust")
        if not c_skin or not c_illust:
            print(f"⚠ 컬럼을 못 찾음 (mon_skin={c_skin} · mon_illust={c_illust})")
            wb.Close(False)
            return 1

        last = ws.UsedRange.Rows.Count
        touched = 0
        for r in range(4, last + 1):
            v = ws.Cells(r, 1).Value
            if v is None:
                continue
            try:
                mid = int(v)
            except (TypeError, ValueError):
                continue
            if mid not in ROWS:
                continue

            skin, illust = ROWS[mid]
            for col, want, label in ((c_skin, skin, "mon_skin"),
                                     (c_illust, illust, "mon_illust")):
                cur = str(ws.Cells(r, col).Value or "").strip()
                if cur == want:
                    print(f"  (이미 맞음) {mid} {label} = {want}")
                    continue
                ws.Cells(r, col).Value = want
                print(f"  {mid} {label}: '{cur}' → '{want}'")
                touched += 1

        wb.Save()
        wb.Close()
        print(f"임시용 중립 몬스터.xlsx 저장 ({touched}칸 변경)")
    finally:
        excel.Quit()

    print("\n다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
