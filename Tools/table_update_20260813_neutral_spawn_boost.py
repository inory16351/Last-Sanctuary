# -*- coding: utf-8 -*-
"""2026-08-13(3차) 유저 지시 — 중립 몬스터가 너무 안 나와서 자원(에너지) 수급이 막힘.

유저 지시(요약): "중립 몬스터가 너무 안나오는 문제가 발생해 자원 수급이 원활하게 되지
않아 너가 임의로 테이블 값을 수정해서 중립 몬스터의 스폰량을 늘리고 자원수급이 원활하게
진행될 수 있게 적용해."

■ 왜 안 나왔나 (71-2·71-3절 참조)
  맵 320×320 전체에 동시 존재 개체수 상한이 8+14+18 = 40마리뿐이고, 재생성 주기도
  16~45초로 느리다. 캐릭터의 사냥 감지 범위(huntDetectRange 10타일)에 비해 맵이 워낙
  넓어 실제 조우 빈도가 낮다 — 미결 16번이 이미 "조우 빈도가 가정보다 낮을 수 있다"고
  경고해뒀던 부분이 실전에서 드러난 것.

■ 무엇을 올렸나 — `neutrality_mon` 시트의 max_alive / respawn_seconds / min_energy /
  max_energy 네 컬럼만 조정한다(스탯·등장범위·선공여부는 그대로 — 요청 범위 밖).
  71-3절이 잡은 "가까운 종은 적고 느리게, 먼 종은 많고 빠르게" 라는 상대적 배분(멀리
  나갈수록 사냥터가 풍부해야 원정의 당위성이 생긴다)은 그대로 지키고, 세 종 모두
  개체수는 약 1.9배, 재생성은 약 0.56배(마리당 대기 시간이 절반 가까이 줄어듦)로
  일괄 확대했다. 에너지 보상도 약 20% 올려 "많이 나오는데 보상은 그대로"라 체감이
  안 되는 상황을 피한다.

  | mon_id | max_alive | respawn_seconds | min_energy | max_energy |
  |-------:|----------:|----------------:|-----------:|-----------:|
  |   1001 |   8 -> 15 |       45 -> 25  |    5 ->  6 |   10 -> 12 |
  |   1002 |  14 -> 26 |       22 -> 13  |   22 -> 26 |   38 -> 44 |
  |   1003 |  18 -> 34 |       16 ->  9  |   55 -> 66 |   90 -> 108|

  총 동시 개체수 40 -> 75마리. 근/중/원 세 종의 비율(대략 1 : 1.7 : 2.3)은 그대로라
  "멀리 갈수록 사냥감이 많다"는 기존 밸런스 논리는 안 깨진다.

■ ⚠ 임의값이다 — 실제 조우 빈도·자원 수급 체감은 플레이해서 다시 볼 것(진행상황.md
  미결 항목에 기록해뒀다). 부족하면 이 스크립트의 NEUTRAL_ROWS 값만 다시 고치면 된다.

■ Excel COM 으로만 만진다(다른 table_update_*.py 와 같은 이유 — 서식·시트 순서 보존).

사용법:
  python Tools/table_update_20260813_neutral_spawn_boost.py
  이어서 python Tools/sync_tables_to_assets.py   (표 -> Unity 에셋 반영, 필수)
"""
import os
import sys
import shutil
import datetime

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

TABLE_DIR = r'C:\Project\Last-Sanctuary-Vault\데이터 테이블'
XLSX_NEUTRAL = os.path.join(TABLE_DIR, '임시용 중립 몬스터.xlsx')
BACKUP_ROOT = os.path.join(TABLE_DIR, '_백업')

DATA_ROW0 = 4

# mon_id: (max_alive, respawn_seconds, min_energy, max_energy)
NEUTRAL_ROWS = {
    1001: (15, 25, 6, 12),
    1002: (26, 13, 26, 44),
    1003: (34, 9, 66, 108),
}


def backup():
    stamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    folder = os.path.join(BACKUP_ROOT, stamp + '_중립스폰증량')
    os.makedirs(folder, exist_ok=True)
    shutil.copy2(XLSX_NEUTRAL, os.path.join(folder, os.path.basename(XLSX_NEUTRAL)))
    return folder


def field_col(ws, field, limit=64):
    """2행(필드명)에서 컬럼 번호를 찾는다. 없으면 None. 앞뒤 공백을 무시한다."""
    for c in range(1, limit + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return None


def row_ids(ws, id_col=1):
    out = []
    r = DATA_ROW0
    while ws.Cells(r, id_col).Value not in (None, ''):
        out.append((r, int(ws.Cells(r, id_col).Value)))
        r += 1
    return out


def update_neutrality_mon(wb):
    ws = wb.Worksheets('neutrality_mon')

    cols = {f: field_col(ws, f) for f in
            ('max_alive', 'respawn_seconds', 'min_energy', 'max_energy')}
    missing = [k for k, v in cols.items() if v is None]
    if missing:
        sys.exit('  ! neutrality_mon 에 없는 컬럼: %s' % missing)

    for r, mid in row_ids(ws):
        if mid not in NEUTRAL_ROWS:
            print('  ! 행 %d (mon_id %d) 는 값이 지정되지 않아 건너뜁니다' % (r, mid))
            continue
        cap, respawn, emin, emax = NEUTRAL_ROWS[mid]
        old_cap = ws.Cells(r, cols['max_alive']).Value
        old_respawn = ws.Cells(r, cols['respawn_seconds']).Value
        old_emin = ws.Cells(r, cols['min_energy']).Value
        old_emax = ws.Cells(r, cols['max_energy']).Value

        ws.Cells(r, cols['max_alive']).Value = cap
        ws.Cells(r, cols['respawn_seconds']).Value = respawn
        ws.Cells(r, cols['min_energy']).Value = emin
        ws.Cells(r, cols['max_energy']).Value = emax

        print('  %d -> 개체 %s->%d마리 · 재생성 %s->%d초 · 에너지 %s~%s -> %d~%d'
              % (mid, old_cap, cap, old_respawn, respawn, old_emin, old_emax, emin, emax))


def main():
    if not os.path.exists(XLSX_NEUTRAL):
        sys.exit('파일을 찾지 못했습니다: ' + XLSX_NEUTRAL)

    folder = backup()
    print('백업:', folder, '\n')

    import win32com.client as win32
    app = win32.gencache.EnsureDispatch('Excel.Application')
    app.Visible = False
    app.DisplayAlerts = False
    try:
        wb = app.Workbooks.Open(os.path.abspath(XLSX_NEUTRAL))
        try:
            update_neutrality_mon(wb)
            wb.Save()
        finally:
            wb.Close(SaveChanges=False)
    finally:
        app.DisplayAlerts = True
        app.Quit()

    print('\n완료 — 이어서 아래를 돌릴 것:')
    print('  python Tools/sync_tables_to_assets.py   (표 -> Unity 에셋 반영, 필수)')


if __name__ == '__main__':
    main()
