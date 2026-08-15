# -*- coding: utf-8 -*-
"""웨이브 밸런스 재조정 — 후반까지 긴장이 유지되게 (2026-08-16).

유저 지시: *"지금 웨이브가 너무 쉽게 깨지니까 좀 더 밸런스 확실하게 잡아줘.
유저가 후반까지 긴장을 놓지 않고 세밀한 전술 수정을 통해 게임을 클리어할 수 있도록"*
유저 확정 방향: **① 몬스터 강화 + 수 증가** (+ 침식도 조금 강화 — 그쪽은 씬 값이라 별도).

무엇을 고치나 (`웨이브테이블.xlsx` / `Sheet2`)
---------------------------------------------
  · ``wave_mon_abil_per``  능력치 배율 — <b>곡선을 가파르게</b>
  · ``melee_mon_num`` · ``ranged_mon_num``  마리 수 — 중후반을 늘린다
  · ``spawn_group_size``   한 번에 쏟아지는 무리 크기 — 후반을 늘린다

★ <b>왜 배율만 올리지 않았나</b>
--------------------------------
배율만 올리면 <b>한 마리가 아주 센</b> 게임이 된다. 그러면 전방 한 명이 버티느냐 마느냐로
끝나고 <b>전술을 세밀하게 고칠 여지가 없다.</b> 마리 수와 무리 크기를 같이 올려야
"전열을 어디에 세우고 · 누구를 뒤로 빼고 · 언제 사냥을 멈추고 모을지" 가 실제 판단이 된다
(유저가 말한 "세밀한 전술 수정"이 이쪽이다).

★ <b>초반은 거의 그대로 둔다</b>
--------------------------------
1~4 웨이브는 캐릭터가 3명뿐이고 강화도 안 된 구간이다. 여기를 올리면 <b>어려운 게 아니라
그냥 진다</b>(23절 "3인 단독 한계선 = 6웨이브" 참조). 곡선은 <b>5웨이브(중간보스)부터</b>
벌어지기 시작해 후반에 크게 갈라진다.

  배율   기존 0.5 → 9.0   (후반 증가폭이 웨이브당 +0.4 로 <b>선형</b>)
  변경   0.55 → 18.5      (후반이 <b>웨이브당 약 1.12배</b>로 <b>기하급수</b>)

기하급수로 바꾼 것이 핵심이다 — 캐릭터 성장(강화)도 곱셈으로 늘기 때문에,
몬스터가 덧셈으로 세지면 <b>후반에 반드시 유저가 앞선다.</b> 그래서 지금 "너무 쉽게" 깨진다.

⚠ 편집은 <b>Excel COM</b> — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17 절 실사고).
⚠ 침식 강화는 이 표가 아니라 <b>씬</b>(`GameSystems > ErosionService`)에 있다 — MCP 로 따로 고친다.

사용법:  py -3 Tools/table_update_20260816_wave_balance.py
다음:    py -3 Tools/sync_tables_to_assets.py
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

WAVE_XLSX = os.path.join(TABLES, "웨이브테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")
SHEET = "Sheet2"

#: wave_num → (근거리 수, 원거리 수, 능력치 배율, 무리 크기)
#:
#: 보스 웨이브(10·20)는 잡몹을 줄이고 보스에 무게를 싣는 기존 구성을 유지한다.
#: 중간보스 웨이브(5·15)도 마찬가지로 잡몹을 크게 늘리지 않는다.
PLAN = {
    #      근접  원거리  배율   무리
    1:  (   4,     4,   0.55,   2),
    2:  (   5,     5,   0.75,   2),
    3:  (   7,     7,   1.05,   3),
    4:  (   9,     9,   1.45,   3),
    5:  (  11,    11,   2.10,   4),   # 중간보스
    6:  (  13,    13,   2.60,   4),
    7:  (  14,    14,   3.20,   5),
    8:  (  16,    16,   3.90,   5),
    9:  (  18,    18,   4.70,   6),
    10: (   9,     9,   6.00,   6),   # 최종보스급 1차
    11: (  17,    17,   6.60,   6),
    12: (  19,    19,   7.40,   7),
    13: (  20,    20,   8.30,   7),
    14: (  22,    22,   9.30,   7),
    15: (  20,    20,  10.80,   8),   # 중간보스
    16: (  22,    22,  11.80,   8),
    17: (  24,    24,  13.00,   8),
    18: (  26,    26,  14.40,   9),
    19: (  28,    28,  16.00,   9),
    20: (  18,    18,  18.50,   9),   # 최종보스
}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_웨이브밸런스")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(WAVE_XLSX, os.path.join(dst, os.path.basename(WAVE_XLSX)))
    print("백업:", dst)


def find_col(ws, field, max_col=32):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def main():
    if not os.path.isfile(WAVE_XLSX):
        print("⚠ 파일 없음:", WAVE_XLSX)
        return 1

    import win32com.client as win32

    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    try:
        wb = excel.Workbooks.Open(WAVE_XLSX)
        ws = wb.Worksheets(SHEET)

        cols = {k: find_col(ws, k) for k in
                ("wave_num", "melee_mon_num", "ranged_mon_num",
                 "wave_mon_abil_per", "spawn_group_size")}
        missing = [k for k, v in cols.items() if not v]
        if missing:
            print("⚠ 컬럼을 못 찾음:", missing)
            wb.Close(False)
            return 1

        last = ws.UsedRange.Rows.Count
        print()
        print("  웨이브 | 마리수(근/원)      | 배율            | 무리    | 위협도(수x배율)")
        print("  -------+--------------------+-----------------+---------+------------------")

        old_total = new_total = 0.0
        for r in range(4, last + 1):
            v = ws.Cells(r, cols["wave_num"]).Value
            if v is None:
                continue
            num = int(v)
            if num not in PLAN:
                continue

            melee, ranged, mult, group = PLAN[num]

            o_m = int(ws.Cells(r, cols["melee_mon_num"]).Value or 0)
            o_r = int(ws.Cells(r, cols["ranged_mon_num"]).Value or 0)
            o_a = float(ws.Cells(r, cols["wave_mon_abil_per"]).Value or 0)
            o_g = int(ws.Cells(r, cols["spawn_group_size"]).Value or 0)

            ws.Cells(r, cols["melee_mon_num"]).Value = melee
            ws.Cells(r, cols["ranged_mon_num"]).Value = ranged
            ws.Cells(r, cols["wave_mon_abil_per"]).Value = mult
            ws.Cells(r, cols["spawn_group_size"]).Value = group

            o_threat = (o_m + o_r) * o_a
            n_threat = (melee + ranged) * mult
            old_total += o_threat
            new_total += n_threat

            print("  %6d | %2d/%2d → %2d/%2d      | %5.2f → %5.2f   | %d → %d   | "
                  "%7.1f → %7.1f  (x%.2f)"
                  % (num, o_m, o_r, melee, ranged, o_a, mult, o_g, group,
                     o_threat, n_threat, n_threat / o_threat if o_threat else 0))

        wb.Save()
        wb.Close()
        print()
        print("  총 위협도 %.0f → %.0f  (x%.2f)" % (old_total, new_total, new_total / old_total))
        print("웨이브테이블.xlsx 저장")
    finally:
        excel.Quit()

    print("\n다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
