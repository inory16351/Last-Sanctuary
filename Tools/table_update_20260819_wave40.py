# -*- coding: utf-8 -*-
"""웨이브 표를 **40웨이브**로 늘린다 (2026-08-19).

유저 지시
---------
*"웨이브 테이블 40 웨이브 까지 확장해. 보스는 5라운드마다 나오는 걸로 보스의 숫자는 하나로
고정. 웨이브 몬스터 테이블에서 위에서 부터 차례로 등장하는거야. (…) 웨이브 테이블은
재밸런싱을 해도 되고, 웬만하면 웨이브가 올라갈때마다 등장하는 웨이브 몬스터의 수를 늘리는
식으로 밸런스를 잡아봐. 기존은 스탯을 올렸었는데 스탯을 무한정으로 올리니까 스탯의 제한이
있는 캐릭터로는 해당 웨이브를 막을 수가 없는 현상이 발생하니까."*

★ 1~20웨이브는 **한 칸도 건드리지 않는다**
------------------------------------------
88-7절이 이미 그 구간을 재밸런싱했고(위협도 곡선을 기하급수로 바꿨다) 유저가 그 구간에
불만을 말한 적이 없다. 지시의 "재밸런싱을 해도 되고" 는 **늘어나는 구간**의 이야기로 읽는다 —
검증된 구간을 같이 흔들면 무엇이 바뀌어서 어려워졌는지 알 수 없게 된다.
보스 배치도 이미 **5·10·15·20 에 한 마리씩 · 표 순서대로**(120001→120004) 라
지시와 정확히 같다 — 즉 지시는 «지금 규칙을 21웨이브 이후로 이어라» 다.

★★ 21~40웨이브는 **마리 수로** 어렵게 만든다
---------------------------------------------
지시의 핵심이 여기다. 예전 곡선은 배율이 웨이브당 약 **1.12배**(기하급수)로 올랐다.
그대로 20웨이브를 더 이으면 40웨이브 배율이 **1850% × 1.12²⁰ ≈ 17,800%** 가 된다.
캐릭터 능력치는 **100 에서 멈추므로**(`BalanceConfigSO.statMax`) 그 지점에서는 어떤 조합도
한 대를 못 버틴다 — 유저가 말한 "막을 수가 없는 현상" 이 그것이다.

그래서 **두 축을 갈랐다**:

| 축 | 1~20 (기존) | 21~40 (여기) |
|---|---|---|
| 능력치 배율 | 웨이브당 **×1.12** | 웨이브당 **×1.01~1.05** (1850% → **3000%**) |
| 마리 수 | 8 → 36 | 36 → **98** (보스 웨이브는 줄인다) |
| 무리 크기 | 2 → 9 | 9 → **14** |

40웨이브 총 위협도는 20웨이브의 **약 3.2배**다(마리 수 ×2.7 · 배율 ×1.62).
같은 20웨이브 동안 배율만으로 올렸다면 ×9.6 이 됐다.

⚠ **이 표만으로는 부족하다** — 배율은 체력에도 공격력에도 곱해진다. 공격력이
1850% → 3000% 로 오르는 것만으로도 캐릭터가 녹으므로, **공격 계열 능력치 상한**을
`BalanceConfigSO`(`monsterAttackStatMax` 60 · `bossAttackStatMax` 120)에 같이 넣었다.
그쪽 주석에 계산 근거가 있다. 이 표의 배율은 그 상한 뒤로는 **체력만** 올린다.

보스 — **표 순서대로 돌린다**
-----------------------------
`웨이브 몬스터 테이블.xlsx` / `wave_top_boss` 위에서부터:

    5→120001 단탈리온 · 10→120002 말파스 · 15→120003 카시노마 · 20→120004 라린길
    25→120001 · 30→120002 · 35→120003 · 40→120004   ← 한 바퀴 돌아 다시 위에서부터

⚠⚠ **표의 다섯째 줄(120005 베일)은 건너뛴다.** 그 줄에는 게임 쪽 실물이 하나도 없다:
`Char_Asset_Bale` 원화 없음 · `Monster_Bale.asset` 없음 · `Skins`/`MonsterSkins` 에 스킨
없음 · 씬의 `MonsterSpawner.bossSlots` 에도 없음(확인). 25웨이브에 적으면
`ResolveBossSlot` 이 경고를 남기고 **기본 보스로 대신 내보낸다** — 즉 «표에 적힌 보스와
화면에 나오는 보스가 다른» 상태가 된다. 원화가 들어오면 이 표의 25·30·35·40 을
120005 부터 이어 적으면 되고, 코드는 안 고쳐도 된다.

⚠ 보스 마리 수는 **전부 1** 이다(지시: "보스의 숫자는 하나로 고정").
⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).
⚠ 증원(`reinforce*`)은 이 표에 없는 값이다 — `sync_tables_to_assets.py` 가 계산한다.

사용법:  py -3 Tools/table_update_20260819_wave40.py
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

#: 데이터가 시작하는 행 (1=한글 헤더 · 2=필드명 · 3=타입).
FIRST_DATA_ROW = 4

#: wave_id 는 100000 + 웨이브 번호 규칙이다(기존 1~20 이 그렇게 적혀 있다).
WAVE_ID_BASE = 100000

#: ★ 보스가 나오는 주기와 **표 순서대로 도는** 보스 id (맨 위 「보스」 절).
#:   ⚠ 120005 베일은 게임 쪽 실물이 없어 넣지 않았다 — 넣으면 다른 보스가 나온다.
BOSS_EVERY = 5
BOSS_ROTATION = [120001, 120002, 120003, 120004]

#: 21~40웨이브 계획 — wave_num → (근접 수, 원거리 수, 능력치 배율, 무리 크기)
#:
#: 보스 웨이브는 잡몹을 **약 70%로 줄인다** — 1~20 구간이 쓰던 규칙 그대로다
#: (보스에 무게를 싣고 동시 교전 수를 낮춘다).
PLAN = {
    #      근접  원거리  배율    무리
    21: (  30,    30,   19.40,   10),
    22: (  31,    31,   20.20,   10),
    23: (  32,    32,   21.00,   10),
    24: (  34,    34,   21.80,   10),
    25: (  24,    24,   22.60,   11),   # 보스
    26: (  35,    35,   23.20,   11),
    27: (  36,    36,   23.80,   11),
    28: (  37,    37,   24.40,   11),
    29: (  39,    39,   25.00,   11),
    30: (  27,    27,   25.60,   12),   # 보스
    31: (  40,    40,   26.10,   12),
    32: (  41,    41,   26.60,   12),
    33: (  42,    42,   27.10,   12),
    34: (  44,    44,   27.60,   12),
    35: (  30,    30,   28.10,   13),   # 보스
    36: (  45,    45,   28.50,   13),
    37: (  46,    46,   28.90,   13),
    38: (  47,    47,   29.30,   13),
    39: (  49,    49,   29.70,   13),
    40: (  33,    33,   30.00,   14),   # 보스 (마지막)
}

FIELDS = ("wave_id", "wave_num", "melee_mon_num", "ranged_mon_num", "boss_mon_num",
          "wave_mon_abil_per", "spawn_group_size", "boss_monster_id")


def boss_for(wave):
    """이 웨이브의 보스 id. 보스가 없으면 0."""
    if wave % BOSS_EVERY != 0:
        return 0
    nth = wave // BOSS_EVERY - 1          # 5웨이브가 0번째
    return BOSS_ROTATION[nth % len(BOSS_ROTATION)]


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_웨이브40확장")
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

        cols = {k: find_col(ws, k) for k in FIELDS}
        missing = [k for k, v in cols.items() if not v]
        if missing:
            print("⚠ 컬럼을 못 찾음:", missing)
            wb.Close(False)
            return 1

        # 이미 있는 웨이브 번호 → 그 행. 다시 돌려도 행이 늘어나지 않게(멱등) 필요하다.
        rows = {}
        last = ws.UsedRange.Rows.Count
        for r in range(FIRST_DATA_ROW, last + 1):
            v = ws.Cells(r, cols["wave_num"]).Value
            if v is not None:
                rows[int(v)] = r

        print("  기존 웨이브 %d개 (최대 %d)" % (len(rows), max(rows) if rows else 0))
        print()
        print("  웨이브 | 근/원   | 보스 | 배율   | 무리 | 위협도(수x배율)")
        print("  -------+---------+------+--------+------+-----------------")

        next_row = last + 1
        added = updated = 0
        for wave in sorted(PLAN):
            melee, ranged, mult, group = PLAN[wave]
            boss_id = boss_for(wave)
            boss_num = 1 if boss_id else 0

            if wave in rows:
                r = rows[wave]
                updated += 1
                tag = "갱신"
            else:
                r = next_row
                next_row += 1
                added += 1
                tag = "추가"

            ws.Cells(r, cols["wave_id"]).Value = WAVE_ID_BASE + wave
            ws.Cells(r, cols["wave_num"]).Value = wave
            ws.Cells(r, cols["melee_mon_num"]).Value = melee
            ws.Cells(r, cols["ranged_mon_num"]).Value = ranged
            ws.Cells(r, cols["boss_mon_num"]).Value = boss_num
            ws.Cells(r, cols["wave_mon_abil_per"]).Value = mult
            ws.Cells(r, cols["spawn_group_size"]).Value = group
            ws.Cells(r, cols["boss_monster_id"]).Value = boss_id

            print("  %6d | %2d/%2d   | %6s | %6.2f | %4d | %8.1f  (%s)"
                  % (wave, melee, ranged, boss_id or "-", mult, group,
                     (melee + ranged) * mult, tag))

        wb.Save()
        wb.Close()
        print()
        print("  추가 %d행 · 갱신 %d행" % (added, updated))

        # 20웨이브와 비교 — 「마리 수로 어렵게」 가 실제로 그렇게 됐는지 눈으로 본다.
        w20 = (18 + 18) * 18.5
        w40 = sum(PLAN[40][:2]) * PLAN[40][2]
        print("  위협도 20웨이브 %.0f → 40웨이브 %.0f  (x%.2f)" % (w20, w40, w40 / w20))
        print("    · 마리 수 36 → %d (x%.2f)" % (sum(PLAN[40][:2]),
                                                sum(PLAN[40][:2]) / 36.0))
        print("    · 배율  1850%% → %.0f%% (x%.2f)" % (PLAN[40][2] * 100,
                                                      PLAN[40][2] / 18.5))
        print("웨이브테이블.xlsx 저장")
    finally:
        excel.Quit()

    print()
    print("다음: py -3 Tools/sync_tables_to_assets.py")
    print("그리고 씬: WaveManager.victoryWave 20 → 40 (MCP)")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
