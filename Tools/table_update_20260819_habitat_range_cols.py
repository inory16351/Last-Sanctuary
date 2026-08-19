# -*- coding: utf-8 -*-
"""서식지 범위 값(반경·추격거리·유휴여유)을 표에 컬럼으로 신설한다 (2026-08-19).

유저 지시: *"아니사킬 청크값"* — 118-1절이 아니사킬 서식지 타일을
`habitat_design` 시트에 넣으면서 함께 남겼던 미결 항목이다:

    범위 값(habitatRadiusTiles·habitatChaseTiles·habitatIdleSlackTiles)은
    <b>표에 없는 인스펙터 값</b>이다(sync 의 EPIC_HABITAT_SEED 하드코딩 상수) —
    카르시노스·아니사킬 둘 다 14 / 8 / 1 을 그대로 쓰고 있었다.

★ <b>「표가 정본」 원칙에 다시 맞춘다</b> — 매직 넘버가 파이썬 코드 안에 박혀 있으면
  "왜 14 인지" 를 표에서 확인할 길이 없다. 컬럼 셋을 신설해서 <b>지금 에셋에 실제로
  들어 있는 값 그대로</b>(14 / 8 / 1, 두 에픽 다 동일 — 확인함, 드리프트 없음)를 옮겨 적는다.

⚠ <b>인스펙터 조정 가능성은 그대로 유지한다.</b> 예전 결정(유저 지시 *"타일 계산 값들은
  에딧에서 수정할 수 있도록"*)을 뒤집지 않는다 — `sync_tables_to_assets.py` 는 여전히
  <b>에셋에 그 필드가 아직 없을 때(=새로 만들어질 때)만</b> 표 값을 심는다(seed-only).
  표는 이제 "처음에 어떤 값으로 태어나는지" 의 정본이 되고, 그 뒤에 에디터에서 바꾼 값은
  안 건드린다 — 하드코딩 상수가 표 컬럼으로 옮겨간 것뿐, 동작 규칙은 그대로다.

⚠ 편집은 <b>Excel COM · DispatchEx</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다
  (UI-17절 실사고). `DispatchEx` 를 쓰는 이유는 112-7절 함정 2 — `EnsureDispatch`/
  `Dispatch` 는 유저가 엑셀을 켜 두면 실패하거나 유저 창에 붙어서 Quit 으로 닫아 버린다.

사용법:  py -3 Tools/table_update_20260819_habitat_range_cols.py
다음:    py -3 Tools/sync_tables_to_assets.py  (기존 값과 같아서 diff 는 안 난다)
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")
SHEET = "habitat_design"

#: 새로 만들 컬럼 — (표시용 한글 헤더, 필드명, 타입 라벨).
NEW_COLUMNS = [
    ("서식지 반경(타일)", "habitat_radius_tiles", "float"),
    ("서식지 추격거리(타일)", "habitat_chase_tiles", "float"),
    ("서식지 유휴여유(타일)", "habitat_idle_slack_tiles", "float"),
]

#: mon_id → (radius, chase, idle_slack). 지금 에셋에 실제로 들어 있는 값 그대로
#: (NeutralMonster_4·5 둘 다 확인 — 드리프트 없음).
ROWS = {
    1101: (14, 8, 1),   # 카르시노스
    1102: (14, 8, 1),   # 아니사킬
}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_서식지범위컬럼")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(NEUTRAL_XLSX, os.path.join(dst, os.path.basename(NEUTRAL_XLSX)))
    print("백업:", dst)


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

    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    changed = 0
    try:
        wb = excel.Workbooks.Open(NEUTRAL_XLSX)
        ws = wb.Worksheets(SHEET)

        c_id = find_col(ws, "mon_id") or 1

        # 컬럼 확보 — 이미 있으면 그 자리를 쓰고, 없으면 맨 뒤에 신설한다.
        col_for = {}
        next_col = ws.UsedRange.Columns.Count + 1
        for header_kr, field, type_label in NEW_COLUMNS:
            existing = find_col(ws, field)
            if existing:
                col_for[field] = existing
                print(f"  (이미 있음) {field} → 열 {existing}")
                continue

            c = next_col
            next_col += 1
            ws.Cells(1, c).Value = header_kr
            ws.Cells(2, c).Value = field
            ws.Cells(3, c).Value = type_label
            col_for[field] = c
            print(f"  + {field} 컬럼 신설 (열 {c})")
            changed += 1

        # 행 찾기.
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

        fields = [f for _, f, _ in NEW_COLUMNS]
        for mid, values in sorted(ROWS.items()):
            r = rows.get(mid)
            if r is None:
                print(f"  ⚠ mon_id {mid} 행이 habitat_design 에 없다 — 118-1절에서 " +
                      "먼저 만들어야 한다. 건너뜀")
                continue

            for field, value in zip(fields, values):
                c = col_for[field]
                cur = ws.Cells(r, c).Value
                if cur is not None and float(cur) == float(value):
                    print(f"  (이미 맞음) {mid} {field} = {value}")
                    continue
                ws.Cells(r, c).Value = value
                print(f"  {mid} {field}: '{cur}' → {value}")
                changed += 1

        if changed:
            wb.Save()
            print("임시용 중립 몬스터.xlsx 저장 (%d 건 변경)" % changed)
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
