# -*- coding: utf-8 -*-
"""중립 표 「스킬 3 id」 칼럼의 <b>필드명 오타</b>를 고친다 (2026-08-21).

★★ <b>무엇이 문제였나</b> — `neutrality_mon` 시트의 세 번째 스킬 칼럼은 한글 헤더가
「스킬 3 id」 인데 <b>필드명(2행)이 `mon_skill_2` 로 중복</b>돼 있었다(폴리르 1104 를 추가할 때
복사한 흔적):

    한글 헤더 :  스킬 1 id   |  스킬 2 id   |  스킬 3 id
    필드명    :  mon_skill_1 |  mon_skill_2 |  mon_skill_2   ← 중복

`read_rows` 는 <b>필드명으로</b> 행을 딕셔너리로 만든다. 그래서 `mon_skill_2` 가
<b>뒤 칼럼 값으로 덮여</b> 버리고, 그 칼럼은 1101~1103 에서 비어 있으므로
<b>에픽 중립 셋의 두 번째 스킬이 조용히 사라졌다</b>
(콘솔: «카르시노스 스킬 2종 준비» → «1종 준비»).

→ 세 번째 칼럼의 필드명을 <b>`mon_skill_3`</b> 으로 고친다. 값(2009)은 손대지 않는다.
⚠ 게임 쪽도 같이 고쳐야 한다 — `sync_tables_to_assets.py` 가 지금 `mon_skill_1·2` 만 읽는다.

⚠ 편집은 Excel COM · DispatchEx (openpyxl 로 저장하면 하이퍼링크가 날아간다 · UI-17절).

사용법:  py -3 Tools/table_update_20260821_neutral_skill3_field.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil

from vault_path import TABLE_DIR as TABLES

NEUTRAL_XLSX = os.path.join(TABLES, "임시용 중립 몬스터.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")
SHEET = "neutrality_mon"

HEADER_KR = "스킬 3 id"        # 한글 헤더로 칼럼을 찾는다(필드명이 망가져 있으므로)
FIELD_WANT = "mon_skill_3"


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_중립스킬3필드명")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(NEUTRAL_XLSX, os.path.join(dst, os.path.basename(NEUTRAL_XLSX)))
    print("backup:", dst)


def main():
    if not os.path.isfile(NEUTRAL_XLSX):
        print("file missing:", NEUTRAL_XLSX)
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

        cols = ws.UsedRange.Columns.Count
        target = 0
        for c in range(1, cols + 1):
            kr = ws.Cells(1, c).Value
            if kr is not None and str(kr).strip() == HEADER_KR:
                target = c
                break

        if target == 0:
            print("column not found:", HEADER_KR)
        else:
            cur = ws.Cells(2, target).Value
            cur = "" if cur is None else str(cur).strip()
            if cur == FIELD_WANT:
                print("already ok:", FIELD_WANT)
            else:
                ws.Cells(2, target).Value = FIELD_WANT
                print("col", target, ":", cur, "->", FIELD_WANT)
                changed += 1

        if changed:
            wb.Save()
            print("saved -", changed)
        else:
            print("no change")
        wb.Close(SaveChanges=False)
    finally:
        excel.Quit()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
