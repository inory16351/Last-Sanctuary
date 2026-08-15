# -*- coding: utf-8 -*-
"""중립 몬스터 1002·1003 의 이름을 원화에 맞춰 확정한다 (2026-08-15).

유저 지시: *"중립 몬스터들 전부 테이블 기준으로 이름 변경"* + 이름 확정
(*"종양귀 / 종양 두더지 ㄱㄱ 그리고 테이블에도 넣어줘"*).

무엇을 고치나
-------------
`임시용 중립 몬스터.xlsx` 의 `mon_name` 칸은 **이미 스트링 키**(`mon_name_1002` 등)라
고칠 것이 없다. 자리표시자가 남아 있던 곳은 **스트링 키 테이블의 kr 칸**이다:

    mon_name_1002 = "역겨운 덩어리 2"   →  "종양귀"        (Tumorling · 원거리)
    mon_name_1003 = "역겨운 덩어리 3"   →  "종양 두더지"   (TumorMole · 근거리)

두 값은 1004 를 처음 채울 때와 같은 사정으로 생긴 임시 이름이다(`table_update_20260815_carcinos.py`
주석 참조) — 원화가 나중에 들어오면서 종이 확정됐는데 표가 따라오지 않았다.
1001 은 이미 "종양 거미"(TumorSpider) 로 확정돼 있어 **"종양 ○○" 계열로 통일**된다.

★ 어느 원화가 어느 id 인가 — **표의 `atk_type` 이 갈라준다**
------------------------------------------------------------
볼트 `리소스/` 의 시트를 열어보면

  · ``Tumorling_asset.png``   — 대기 · 이동 · **원거리 공격** · **투사체** 행이 있다 → 1002(ranged)
  · ``Tumor_mole_asset.png``  — 대기 · **근거리 공격** · 이동                        → 1003(melee)
  · ``Tumor spider_asset.png``— 대기 · 이동 · 근거리 공격                            → 1001(melee)

추측이 아니라 **표의 공격 유형과 시트의 모션 구성이 일치하는 쪽**으로 붙였다.

⚠ 편집은 **Excel COM** 이다 — openpyxl 로 저장하면 스트링 키 테이블의 "정의된 이름"
   (`key_<키>`)과 원본 표의 하이퍼링크가 날아간다(UI-17 절 실사고).

사용법:  py -3 Tools/table_update_20260815_neutral_names.py
"""

import os
import sys
import shutil
import datetime

from vault_path import TABLE_DIR as TABLES

STRING_XLSX = os.path.join(TABLES, "스트링 키 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

#: (스트링 키, 한국어, 영어) — 영어는 1001 의 ``TumorSpider`` 와 같은 붙여쓰기 규칙을 따른다.
NAMES = [
    ("mon_name_1002", "종양귀", "Tumorling"),
    ("mon_name_1003", "종양 두더지", "TumorMole"),
]

SOURCE = "neutrality_mon.mon_name"


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_중립이름")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(STRING_XLSX, os.path.join(dst, os.path.basename(STRING_XLSX)))
    print("백업:", dst)


def main():
    if not os.path.isfile(STRING_XLSX):
        print("⚠ 파일 없음:", STRING_XLSX)
        return 1

    import win32com.client as win32

    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    try:
        wb = excel.Workbooks.Open(STRING_XLSX)
        ws = wb.Worksheets("string")
        last = ws.UsedRange.Rows.Count

        # 키 → 행 번호를 한 번만 훑어서 만든다(행마다 다시 훑으면 O(n²)).
        rows = {}
        for r in range(4, last + 1):
            key = ws.Cells(r, 1).Value
            if key is not None:
                rows[str(key).strip()] = r

        for key, kr, en in NAMES:
            r = rows.get(key)
            if r is None:
                last += 1
                r = last
                ws.Cells(r, 1).Value = key
                ws.Cells(r, 4).Value = SOURCE
                print(f"  + {key} (행 신설)")

            before = str(ws.Cells(r, 2).Value or "").strip()
            if before != kr:
                ws.Cells(r, 2).Value = kr
                print(f"  {key} kr: '{before}' → '{kr}'")
            else:
                print(f"  (이미 맞음) {key} = {kr}")

            before_en = str(ws.Cells(r, 3).Value or "").strip()
            if before_en != en:
                ws.Cells(r, 3).Value = en
                print(f"  {key} en: '{before_en}' → '{en}'")

        wb.Save()
        wb.Close()
        print("스트링 키 테이블 저장")
    finally:
        excel.Quit()

    print("\n완료 — 다음을 순서대로 돌릴 것:")
    print("  py -3 Tools/gen_string_table.py")
    print("  py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
