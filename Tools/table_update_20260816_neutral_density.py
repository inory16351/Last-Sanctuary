# -*- coding: utf-8 -*-
"""중립 몬스터 개체수를 <b>밀도 유지</b> 기준으로 다시 잡는다 (2026-08-16).

왜 필요한가
-----------
같은 날 등장 범위를 <b>원형 → 정사각형</b>으로 바꿨다(유저 확정, 진행상황 88절).
그 결과 각 종의 서식 면적이 <b>약 25% 넓어졌다</b>:

    종      기존(원)    지금(정사각)   배수
    1001      7,528  →   9,576       1.27
    1002     23,292  →  29,800       1.28
    1003     48,982  →  60,888       1.24

⚠ <b>개체수를 그대로 두면 밀도가 조용히 20% 떨어진다.</b> 같은 범위를 탐험해도 마주치는
중립이 줄고, 그만큼 <b>에너지 수입이 줄어든다</b> — 웨이브를 어렵게 만든 것과 별개로
의도하지 않은 하향이 겹치는 셈이다. 면적이 늘어난 만큼 개체수를 올려 <b>밀도를 그대로</b> 둔다.

★ 이것은 <b>난이도 조정이 아니다.</b> 웨이브 난이도는 `table_update_20260816_wave_balance.py`
  가 따로 올렸고, 여기는 그 조정이 <b>깨끗하게 측정되도록</b> 다른 변수를 고정하는 것이다.

⚠ 에픽(1004)은 <b>1마리 그대로</b> — 서식지를 가진 유일 개체라 밀도 개념이 없다.
⚠ 편집은 <b>Excel COM</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다.

사용법:  py -3 Tools/table_update_20260816_neutral_density.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")
SHEET = "neutrality_mon"

#: mon_id → 새 max_alive (면적 배수를 곱해 반올림한 값)
PLAN = {
    1001: 43,      # 34 x 1.27
    1002: 33,      # 26 x 1.28
    1003: 19,      # 15 x 1.24
}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_중립밀도")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(NEUTRAL_XLSX, os.path.join(dst, os.path.basename(NEUTRAL_XLSX)))
    print("백업:", dst)


def find_col(ws, field, max_col=64):
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
        ws = wb.Worksheets(SHEET)

        col = find_col(ws, "max_alive")
        if not col:
            print("⚠ max_alive 컬럼을 못 찾음")
            wb.Close(False)
            return 1

        last = ws.UsedRange.Rows.Count
        for r in range(4, last + 1):
            v = ws.Cells(r, 1).Value
            if v is None:
                continue
            try:
                mid = int(v)
            except (TypeError, ValueError):
                continue
            if mid not in PLAN:
                continue

            cur = int(ws.Cells(r, col).Value or 0)
            want = PLAN[mid]
            if cur == want:
                print(f"  (이미 맞음) {mid} max_alive = {want}")
                continue
            ws.Cells(r, col).Value = want
            print(f"  {mid} max_alive: {cur} → {want}")

        wb.Save()
        wb.Close()
        print("임시용 중립 몬스터.xlsx 저장")
    finally:
        excel.Quit()

    print("\n다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
