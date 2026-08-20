# -*- coding: utf-8 -*-
"""웨이브 표 — **1웨이브부터 40웨이브까지 마리 수를 단조 증가로** (2026-08-20).

유저 지시 (앞 지시의 **정정**)
------------------------------
*"웨이브 처음부터 계속 동일한 숫자가 나오지 말고 점진적으로 나오는 몬스터 수를 늘리는
방식으로 하라는 거였어"*

앞 작업(``table_update_20260819_wave40.py``)은 **21~40웨이브만** 늘리고 1~20은 검증된
구간이라 그대로 뒀다. 유저 정정의 뜻은 **처음부터** 점진적으로 늘리라는 것이다.

⚠ 그 스크립트를 지우지 않는다 — 21~40 행을 **처음 만든** 기록이고, 배율 곡선은
  그때 정한 값을 여기서도 그대로 쓴다. 이 스크립트는 **마리 수만** 다시 쓴다.

★★ 무엇이 실제로 바뀌나 — **보스 웨이브의 「움푹 파임」이 없어진다**
--------------------------------------------------------------------
지금 곡선은 이미 오르고 있었다. 문제는 **보스 웨이브마다 잡몹이 뚝 떨어지는** 것이었다:

    9웨이브 18 → **10웨이브 9** → 11웨이브 17     (절반으로 떨어졌다 다시 오른다)
    19웨이브 28 → **20웨이브 18** → (끝)

그 설계 의도는 "보스에 무게를 싣고 동시 교전 수를 낮춘다" 였는데, 화면에서는
**웨이브가 거꾸로 쉬워지는 구간**으로 읽힌다. 이제 한 번도 안 줄어든다:

    한쪽(근접·원거리 각각) = round(4 + (웨이브-1) × 1.42)   … 1~20웨이브
                           = 31 + (웨이브 - 20)             … 21~40웨이브
    총 마리 수 = 그 두 배 → **8마리(1웨이브) … 62(20) … 102(40)**

1~20 구간의 값이 기존과 거의 같게 나오도록 기울기 1.42 를 골랐다 — 비보스 웨이브는
±2마리 안쪽이고(9웨이브 18→15 · 13웨이브 20→21 · 19웨이브 28→30), **바뀌는 것은
보스 웨이브뿐**이다.

⚠⚠ **보스 웨이브가 그만큼 어려워진다** — 파임을 없앤 결과다:

    | 웨이브 | 위협도(마리수 × 배율) 전 → 후 |
    |---|---|
    | 10 | 108 → **204** (1.89배) |
    | 15 | 432 → 518 (1.20배) |
    | 20 | 666 → **1147** (1.72배) |
    | 1~20 합계 | 5791 → 6702 (1.16배) |

이제 **보스 웨이브가 그 구간에서 가장 어렵다**(예전에는 가장 쉬웠다). 유저 지시가
「점진적 증가」이므로 이게 맞는 방향이지만, 실플레이로 10·20웨이브를 확인할 것.
되돌리는 손잡이는 :func:`per_side` 한 줄이다 — 보스 웨이브만 예전처럼 줄이려면
그 함수에 `if wave % 5 == 0: n = round(n * 0.6)` 를 넣으면 된다.

배율은 안 건드린다 — 이미 단조 증가다
-------------------------------------
``wave_mon_abil_per`` 는 55% → 1850%(20웨이브) → 3000%(40웨이브) 로 이미 한 번도 안
줄어든다. 그리고 **잡몹도 체력 배율을 계속 받는다**(유저 확정 2026-08-20:
*"잡몹도 체력 배율 줘도 됨 40라운드 까지 가면 한방에 잡몹이 녹을 수도 있으니까"*) —
그래서 ``BalanceConfigSO.monsterHpStatMax`` 는 **0(무제한)** 이 맞다. 상한이 걸린 것은
공격 계열뿐이다(그쪽 주석 참조).

보스 — **표 순서대로 다섯 마리를 돈다**
----------------------------------------
``wave_top_boss`` 위에서부터:

    5→120001 단탈리온 · 10→120002 말파스 · 15→120003 카시노마 · 20→120004 라린길
    25→120005 **베일** · 30→120001 · 35→120002 · 40→120003   ← 한 바퀴 돌아 위에서부터

⚠ 그래서 **마지막 웨이브의 보스가 베일이 아니다.** 표에서 가장 센 것은 베일인데
  (근접 18 · 원거리 15 · 방어 35 · 콜라이더 10x15) 25웨이브에 나온다 — 「위에서부터
  차례로」 라는 지시를 글자대로 따른 결과다. 베일을 40웨이브 최종보스로 두고 싶으면
  :data:`BOSS_ROTATION` 순서만 바꾸면 된다(코드는 안 고쳐도 된다).

⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).

사용법:  py -3 Tools/table_update_20260820_wave40_counts.py
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
FIRST_DATA_ROW = 4
WAVE_ID_BASE = 100000
LAST_WAVE = 40

BOSS_EVERY = 5

#: ★ 표 ``wave_top_boss`` 의 줄 순서 그대로. 순서를 바꾸면 보스 배치가 바뀐다(맨 위 ⚠).
#:
#: ★ 베일(120005)이 2026-08-20 에 합류했다 — 스킨(`Tools/bale_skin_build.py`)·정의
#:   에셋·일러스트가 다 들어와서 25웨이브에 실물로 나온다.
BOSS_ROTATION = [120001, 120002, 120003, 120004, 120005]

#: 능력치 배율 — **안 건드린다**. 1~20 은 88-7절, 21~40 은 앞 스크립트가 정한 값이다.
#:   여기 적어두는 이유는 위협도 비교를 출력하려면 값을 알아야 하기 때문이다.
MULT = {
    1: 0.55,  2: 0.75,  3: 1.05,  4: 1.45,  5: 2.10,  6: 2.60,  7: 3.20,  8: 3.90,
    9: 4.70, 10: 6.00, 11: 6.60, 12: 7.40, 13: 8.30, 14: 9.30, 15: 10.80, 16: 11.80,
    17: 13.00, 18: 14.40, 19: 16.00, 20: 18.50,
    21: 19.40, 22: 20.20, 23: 21.00, 24: 21.80, 25: 22.60, 26: 23.20, 27: 23.80,
    28: 24.40, 29: 25.00, 30: 25.60, 31: 26.10, 32: 26.60, 33: 27.10, 34: 27.60,
    35: 28.10, 36: 28.50, 37: 28.90, 38: 29.30, 39: 29.70, 40: 30.00,
}

#: 포탈 한 곳에서 한 번에 나오는 마리 수 — 이것도 단조 증가로 둔다.
GROUP = {**{w: g for w, g in zip(range(1, 21),
            [2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 6, 7, 7, 7, 8, 8, 8, 9, 9, 9])},
         **{w: 10 + (w - 21) // 4 for w in range(21, 41)}}


def per_side(wave):
    """
    ★ 한쪽(근접·원거리 각각) 마리 수 — **한 번도 줄지 않는다** (맨 위 ★★).

    기울기 1.42 는 1~20 구간의 기존 값(비보스 웨이브)에 맞춘 것이다 — 그래서 실제로
    바뀌는 것은 보스 웨이브뿐이다. 21웨이브부터는 배율을 눕혀 놨으므로(앞 스크립트)
    난이도를 마리 수가 만든다: 웨이브당 정확히 +1.
    """
    if wave <= 20:
        return round(4 + (wave - 1) * 1.42)
    return 31 + (wave - 20)


def boss_for(wave):
    if wave % BOSS_EVERY != 0:
        return 0
    return BOSS_ROTATION[(wave // BOSS_EVERY - 1) % len(BOSS_ROTATION)]


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_웨이브마리수단조증가")
    os.makedirs(dst, exist_ok=True)
    shutil.copy2(WAVE_XLSX, os.path.join(dst, os.path.basename(WAVE_XLSX)))
    print("백업:", dst)


def find_col(ws, field, max_col=32):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


FIELDS = ("wave_id", "wave_num", "melee_mon_num", "ranged_mon_num", "boss_mon_num",
          "wave_mon_abil_per", "spawn_group_size", "boss_monster_id")


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

        cols = {k: find_col(ws, k) for k in FIELDS}
        missing = [k for k, v in cols.items() if not v]
        if missing:
            print("⚠ 컬럼을 못 찾음:", missing)
            wb.Close(False)
            return 1

        rows = {}
        last = ws.UsedRange.Rows.Count
        for r in range(FIRST_DATA_ROW, last + 1):
            v = ws.Cells(r, cols["wave_num"]).Value
            if v is not None:
                rows[int(v)] = r

        print()
        print("  웨 | 근/원      | 보스   | 배율   | 무리 | 위협도 전 → 후")
        print("  ---+------------+--------+--------+------+--------------------")

        next_row = last + 1
        old_tot = new_tot = 0.0
        for wave in range(1, LAST_WAVE + 1):
            n = per_side(wave)
            mult = MULT[wave]
            group = GROUP[wave]
            boss_id = boss_for(wave)

            if wave in rows:
                r = rows[wave]
                o_m = int(ws.Cells(r, cols["melee_mon_num"]).Value or 0)
                o_r = int(ws.Cells(r, cols["ranged_mon_num"]).Value or 0)
            else:
                r = next_row
                next_row += 1
                o_m = o_r = 0

            ws.Cells(r, cols["wave_id"]).Value = WAVE_ID_BASE + wave
            ws.Cells(r, cols["wave_num"]).Value = wave
            ws.Cells(r, cols["melee_mon_num"]).Value = n
            ws.Cells(r, cols["ranged_mon_num"]).Value = n
            ws.Cells(r, cols["boss_mon_num"]).Value = 1 if boss_id else 0
            ws.Cells(r, cols["wave_mon_abil_per"]).Value = mult
            ws.Cells(r, cols["spawn_group_size"]).Value = group
            ws.Cells(r, cols["boss_monster_id"]).Value = boss_id

            o_threat = (o_m + o_r) * mult
            n_threat = 2 * n * mult
            old_tot += o_threat
            new_tot += n_threat
            mark = " ←보스" if boss_id else ""
            print("  %2d | %2d/%2d → %2d/%2d | %6s | %6.2f | %4d | %7.0f → %7.0f%s"
                  % (wave, o_m, o_r, n, n, boss_id or "-", mult, group,
                     o_threat, n_threat, mark))

        wb.Save()
        wb.Close()

        counts = [2 * per_side(w) for w in range(1, LAST_WAVE + 1)]
        mono = all(counts[i] < counts[i + 1] for i in range(len(counts) - 1))
        print()
        print("  총 마리 수: %s" % counts)
        print("  단조 증가: %s" % ("예 (한 번도 안 줄어든다)" if mono else "⚠ 아니다"))
        print("  총 위협도 %.0f → %.0f (x%.2f)" % (old_tot, new_tot, new_tot / old_tot))
        print("웨이브테이블.xlsx 저장")
    finally:
        excel.Quit()

    print()
    print("다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
