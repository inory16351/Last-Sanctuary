# -*- coding: utf-8 -*-
"""30웨이브 보스를 <b>레기미아(120006)</b>로 (2026-08-21).

유저 지시: *"레기미아 인게임에 구현해줘 30웨이브에 웨이브보스로 나오면돼"*.

무엇을 고치나 — <b>한 칸이다</b>
--------------------------------
``웨이브테이블.xlsx / Sheet2`` 의 30웨이브 줄, ``boss_monster_id`` 를
**120001(단탈리온) → 120006(레기미아)**.

그게 전부다. 102-1절이 *"보스가 누구인지를 표가 정한다"* 로 구조를 바꿔 뒀기 때문에
<b>코드에는 보스 id 가 한 군데도 없다</b> — 이 한 칸과 파이프라인 재실행이면 배치가 바뀐다
(라린길 20웨이브 배정과 <b>완전히 같은 작업</b>이다 · `table_update_20260819_laryngeal_wave20.py`).

왜 30웨이브가 단탈리온이었나 — 40웨이브로 늘리면서(`table_update_20260819_wave40.py`)
보스 자리가 5개(120001~120005)뿐이라 <b>30부터 처음으로 되돌려 채운</b> 임시 배정이다.
레기미아(120006)는 114절 이후 표·정의·스킬·스트링키가 다 있는데 <b>어느 웨이브에도
배정돼 있지 않았다</b>(라린길이 겪은 것과 같은 상태). 이번 지시가 그 자리를 정해 준 것이다.

바뀐 뒤 배치:
  5 단탈리온 · 10 말파스 · 15 카시노마 · 20 라린길 · 25 베일 ·
  **30 레기미아** · 35 말파스 · 40 카시노마

★ 30웨이브가 <b>새 보스의 첫 등장</b>이 된다 — 예전에는 25(베일)를 넘기면 이미 본
  보스만 다시 나왔다. 이제 6종이 모두 «처음 보는 웨이브» 를 하나씩 갖는다.

⚠ 편집은 <b>Excel COM</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).
⚠ <b>`DispatchEx`</b> 를 쓴다 — `EnsureDispatch` 는 유저가 엑셀을 켜 두면 실패하고,
   `Dispatch` 는 <b>유저가 열어 둔 창에 붙어</b> 스크립트 끝의 `Quit()` 이 그 창을
   닫아 버린다(112-7절 함정 2번).

사용법:  py -3 Tools/table_update_20260821_legimia_wave30.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

WAVE_XLSX = os.path.join(TABLES, "웨이브테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

#: 고칠 웨이브 → 보스 id. <b>이 표에 적힌 웨이브만</b> 건드린다 — 다른 줄은 읽지도 않는다.
BOSS_BY_WAVE = {
    30: 120006,   # 레기미아 「부패의 마왕」
}


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


def update_wave_table(excel):
    wb = excel.Workbooks.Open(WAVE_XLSX)
    changed = 0
    try:
        ws = wb.Worksheets("Sheet2")
        c_num = find_col(ws, "wave_num")
        c_id = find_col(ws, "boss_monster_id")
        c_cnt = find_col(ws, "boss_mon_num")
        if not (c_num and c_id):
            raise SystemExit("⚠ wave_num / boss_monster_id 컬럼을 못 찾았습니다")

        last_row = ws.UsedRange.Rows.Count
        for r in range(4, last_row + 1):
            v = ws.Cells(r, c_num).Value
            if v is None:
                continue
            wave = int(v)
            if wave not in BOSS_BY_WAVE:
                continue

            want = BOSS_BY_WAVE[wave]
            old = int(ws.Cells(r, c_id).Value or 0)
            count = int(ws.Cells(r, c_cnt).Value or 0) if c_cnt else 1

            if old == want:
                print("  %2d웨이브 — 이미 %d (그대로)" % (wave, want))
                continue

            ws.Cells(r, c_id).Value = want
            # 보스를 지정했는데 마릿수가 0 이면 아무도 안 나온다 — 같이 맞춘다.
            if c_cnt and count < 1:
                ws.Cells(r, c_cnt).Value = 1
                print("  %2d웨이브 — boss_mon_num 0 → 1 (보스가 지정됐는데 마릿수가 없었다)"
                      % wave)
            print("  %2d웨이브 — boss_monster_id %d → %d" % (wave, old, want))
            changed += 1

        if changed:
            wb.Save()
    finally:
        wb.Close(False)
    return changed


def main():
    if not os.path.isfile(WAVE_XLSX):
        print("⚠ 파일 없음:", WAVE_XLSX)
        return 1

    import win32com.client as win32

    backup([WAVE_XLSX], "legimia_wave30")

    # ⚠ DispatchEx — 맨 위 주석 참조. 유저가 켜 둔 엑셀과 완전히 분리된 인스턴스다.
    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    try:
        print("\n[웨이브테이블 / Sheet2]")
        changed = update_wave_table(excel)
    finally:
        excel.Quit()

    print("\n%d칸 변경" % changed)
    print("다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.exit(main())
