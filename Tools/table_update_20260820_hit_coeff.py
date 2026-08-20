# -*- coding: utf-8 -*-
"""「능력치 및 공식 정리.xlsx」의 **「계수」 시트를 「공식」 시트에 맞춘다** (2026-08-20, 3차).

★★ 무엇이 어긋나 있었나
------------------------
120절에서 명중·치명 공식을 코드에 반영했다(유저 지시: *"공식 바꼈으니까 확인 … 그리고 적용"*).
그때 근거로 삼은 것은 <b>「공식」 시트</b>였는데, 같은 파일의 <b>「계수」 시트</b>는
<b>옛 값 그대로</b>였다. 즉 <b>한 파일 안에서 두 시트가 서로 다른 말을 하고</b> 있었다:

| 항목 | 「공식」 시트 (정본) | 「계수」 시트 (옛 값) | 코드 |
|---|---|---|---|
| 적중 확률(%) | 40 + (명중률 × **0.6**) | 85 + (명중률 × **0.3**) | 40 · 0.6 |
| 치명타 상한(%) | **없음**("그냥 상한 없이") | **60** | 100 |

「계수」 시트는 <b>「코드 상 필드명」 칸을 갖고 있어서</b>(`accuracyBasePercent` 등)
사람이 «코드가 이 값이겠구나» 라고 읽는 표다. 그대로 두면 <b>다음에 표를 보는 사람이
코드를 옛 값으로 되돌린다.</b> 그것이 이 스크립트가 있는 이유다.

⚠ 이번 갱신은 **표를 코드에 맞추는 것이 아니다** — 둘 다 <b>「공식」 시트</b>에 맞춘다.
  「공식」 시트가 사람이 손으로 고쳐 둔 정본이고(120-12절 ③), 코드는 이미 그쪽을 따랐다.

⚠ 「의미」 칸도 같이 고친다 — «50에서 100% 도달» 은 옛 계수(0.3)에서 나온 문장이라
  값만 바꾸면 <b>설명이 거짓말을 시작한다</b>. 새 값에서는 100 에서 도달한다.

실행:  py -3 Tools/table_update_20260820_hit_coeff.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

BACKUP_ROOT = os.path.join(TABLES, "_백업")
XLSX = "능력치 및 공식 정리.xlsx"
SHEET = "계수"

#: (코드 상 필드명, 새 값, 새 「의미」 문구 or None=그대로)
#:   ★ <b>행 번호로 찾지 않는다</b> — 표에 줄이 끼면 밀린다. 「코드 상 필드명」 칸으로 찾는다
#:     (`gen_character_assets.py` 가 컬럼을 이름으로 찾는 것과 같은 이유 · 그쪽 ⚠⚠ 주석).
FIXES = [
    ("accuracyBasePercent", 40,
     "명중률이 0일 때의 적중 확률 (2026-08-20 개정 — 옛 값 85)"),
    ("accuracyPerStat", 0.6,
     "명중률 1당 적중 확률 증가. 100에서 100% 도달 (2026-08-20 개정 — 옛 값 0.3)"),
    ("criticalMaxPercent", 100,
     "치명타 확률 상한. 「공식」 시트가 \"그냥 상한 없이\" 라고 해 100 으로 열었다 "
     "(2026-08-20 개정 — 옛 값 60)"),
]


def check_locks(files):
    locked = [f for f in files if os.path.isfile(os.path.join(TABLES, "~$" + f))]
    if locked:
        raise SystemExit("⚠ 엑셀에서 열려 있는 파일이 있습니다 — 닫고 다시 실행하세요:\n   "
                         + "\n   ".join(locked))


def backup(files, tag):
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_" + tag)
    os.makedirs(dst, exist_ok=True)
    for f in files:
        src = os.path.join(TABLES, f)
        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(dst, f))
    print("백업:", dst)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    check_locks([XLSX])
    import win32com.client as win32
    backup([XLSX], "명중계수")

    # ⚠ DispatchEx — EnsureDispatch 는 유저가 엑셀을 열어 두면 죽는다(119-2절).
    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    changed = 0
    try:
        wb = excel.Workbooks.Open(os.path.join(TABLES, XLSX))
        try:
            ws = wb.Worksheets(SHEET)
            last = ws.UsedRange.Rows.Count
            wanted = {f: (v, d) for f, v, d in FIXES}
            found = set()

            print("\n  [계수 시트]")
            for r in range(1, last + 1):
                field = ws.Cells(r, 3).Value          # 「코드 상 필드명」
                key = str(field).strip() if field is not None else ""
                if key not in wanted:
                    continue
                found.add(key)
                new_v, new_d = wanted[key]
                old_v = ws.Cells(r, 2).Value
                if old_v is not None and abs(float(old_v) - float(new_v)) < 1e-9:
                    print("    · %-24s 이미 %s" % (key, new_v))
                else:
                    ws.Cells(r, 2).Value = new_v
                    print("    %-24s %s → %s" % (key, old_v, new_v))
                    changed += 1
                if new_d:
                    ws.Cells(r, 4).Value = new_d

            missing = sorted(set(wanted) - found)
            if missing:
                raise SystemExit("⚠ 「계수」 시트에서 못 찾은 필드가 있습니다: "
                                 + ", ".join(missing))
            wb.Save()
        finally:
            wb.Close()
    finally:
        excel.Quit()

    print("\n고친 칸 %d개" % changed)
    print("★ 이제 「공식」·「계수」 시트와 BalanceConfigSO 가 같은 값을 말한다.")


if __name__ == "__main__":
    main()
