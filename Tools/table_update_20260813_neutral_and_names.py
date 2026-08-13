# -*- coding: utf-8 -*-
"""2026-08-13(2차) 유저 지시 — 중립 몬스터 표 확장 + 영어 이름 컬럼 정리.

유저 지시(요약):
  3번 "중립 몬스터에도 퍼스트 스탯 시트 추가해서 넣어줘 안에 채울 값은 적절히 너가 계산해서
       넣고 ... 2, 3번째 중립 몬스터 개체량을 조금 늘려 스폰 주기도 조절하고 지금 후반 가면
       멀리 나갈 이유가 없음 ... 후반에 멀리까지 가서 사냥할 당위성이 생기게 조절해서 테이블에
       기입하고 게임내에 넣어"
  4번 "그냥 첫번째 몬스터에도 공격력 넣고 비선공/선공 판정만 넣어줘"
  추가 "스트링 테이블로 영어 이름 배정해야 하는데 지금 영어 이름 칼럼이 삭제되지 않고
       남아있는 것들 확인해서 없애줘"

■ 이 스크립트가 하는 일
  ┌ 임시용 중립 몬스터.xlsx
  │  · first_Stat  시트 <b>신설</b> — 웨이브 몬스터 테이블의 first_Stat 과 같은 형식
  │  · neutrality_mon  K = max_alive(int) · L = respawn_seconds(float)   [컬럼 신설]
  │  · neutrality_mon  atk / 에너지 / spawn_range 값 재조정
  └ 영어 이름 컬럼 삭제 (스트링 키 테이블의 en 칸이 정본이 됐다)
     · 웨이브 몬스터 테이블 / wave_nom       C  character_name_EG
     · 웨이브 몬스터 테이블 / wave_mid_boss  C  character_name_EG
     · 웨이브 몬스터 테이블 / wave_top_boss  C  character_name_EG · H(삭제 후 G) boss_title_EG
     · 캐릭터 테이블      / Character      C  character_name_EG

■ ⚠ 컬럼을 지우면 <b>위치로 읽는 코드가 전부 밀린다</b>
  65-2절이 "컬럼은 항상 맨 뒤에 붙인다"고 정한 것과 같은 이유다. 지우는 건 그보다 더
  위험해서, 이 스크립트를 돌린 뒤 아래 두 곳을 <b>같이</b> 고쳐야 한다(이미 고쳐뒀다):
    · Tools/sync_tables_to_assets.py  — boss_title / boss_skill_1~3 의 컬럼 인덱스
    · Tools/gen_character_assets.py   — 에셋 파일명에 쓰던 영어 이름을 스트링 테이블에서 읽는다
  gen_string_table.py 의 `character_name_EG`·`boss_title_EG` 수집 규칙도 지웠다 —
  이미 스트링 키 테이블에 들어간 en 값은 merge 규칙상 그대로 남는다(덮어쓰지 않는다).

■ ⚠ Excel COM 으로만 만진다 — 웨이브 몬스터 테이블에는 하이퍼링크 12칸이 있다(51-3절).

사용법:  python Tools/table_update_20260813_neutral_and_names.py
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
XLSX_WAVE_MON = os.path.join(TABLE_DIR, '웨이브 몬스터 테이블.xlsx')
XLSX_CHAR = os.path.join(TABLE_DIR, '캐릭터 테이블.xlsx')
BACKUP_ROOT = os.path.join(TABLE_DIR, '_백업')

DATA_ROW0 = 4


# ---------------------------------------------------------------------------
# 1) 중립 몬스터 first_Stat 시트 (신설)
#
# 웨이브 몬스터 테이블의 first_Stat 과 <b>같은 컬럼 형식</b>을 그대로 따른다 — 다른 표를
# 읽던 눈과 코드가 그대로 통한다. 다만 중립 몬스터에는 콜라이더 칸이 필요 없다(전용 스킨이
# 없어 크기를 조정할 일이 없다) — 필요해지면 뒤에 붙이면 된다.
#
# ■ 값을 어떻게 정했나 (유저: "적절히 너가 계산해서 넣고")
#   기준은 웨이브 잡몹이다(지옥 송곳니 hp 7 / atk 5 / def 2, 영혼 사수 hp 6 / atk 4 / def 1).
#   중립 몬스터는 <b>거리에 따라 강해지고 보상도 커지는</b> 사냥감이라, 가까운 종은 잡몹보다
#   약하게 · 먼 종은 확실히 세게 잡았다.
#
#   ★ 1001 에 공격력 2 를 넣는다 (유저 지시 4번 "첫번째 몬스터에도 공격력 넣고").
#     예전에는 atk 0 이라 비선공인데 반격도 무의미한 허수아비였다 — 이제 맞으면 문다.
#
#   이동속도·공격속도는 38-1절 공식으로 풀리는 스탯값이다:
#     공속 = 0.6 + 3.0·s/(s+50)   ·   이속 = 2.1 + 3.9·s/(s+50)
#   중립은 캐릭터보다 느려야 도망칠 여지가 생기므로 낮게 잡았다.
# ---------------------------------------------------------------------------
NEUTRAL_STAT_HEADER = [
    ('몬스터 id', 'mon_id', 'int'),
    ('체력', 'hp', 'int'),
    ('근거리 공격력', 'melee_atk', 'int'),
    ('명중률', 'accuracy', 'int'),
    ('크리티컬 확률', 'critical', 'int'),
    ('방어력', 'def', 'int'),
    ('체력 회복', 'hp_recovery', 'int'),
    ('공격 속도', 'atk_speed', 'int'),
    ('이동속도', 'movement_speed', 'int'),
    ('저항력', 'resistance', 'int'),
]

# mon_id: (hp, melee_atk, accuracy, critical, def, hp_recovery, atk_speed, movement_speed, resistance)
NEUTRAL_STATS = {
    #        hp  atk  acc  crit def  regen aspd mspd  res
    1001: (   4,   2,  40,   0,   0,    0,   3,    1,  50),   # 근처 · 약하고 느리다. 공격력 신설
    1002: (   8,   6,  50,   3,   2,    1,   5,    3,  50),   # 중간 · 잡몹과 비슷
    1003: (  14,  11,  55,   8,   5,    3,   7,    5,  50),   # 원거리 · 잡몹보다 확실히 세다
}


# ---------------------------------------------------------------------------
# 2) neutrality_mon — 개체수 · 재생성 주기 컬럼 신설 + 값 재조정
#
# ■ 유저가 지적한 문제: "지금 후반 가면 멀리 나갈 이유가 없음. 계속 중앙으로 중립몹이
#   모이니까." 원인이 두 가지였다.
#     ① 배회·스폰 위치를 <b>유클리드 반지름</b>으로 뽑고 <b>체비셰프</b>로 검사해서 개체가
#        고리 안쪽(넥서스 쪽)으로 쏠렸다 → 코드에서 고쳤다(SampleRingCell).
#     ② <b>개체수 분포가 거꾸로였다</b> — 가까운 종 15마리 / 중간 8 / 먼 종 4.
#        중앙에 사냥감이 제일 많으니 멀리 갈 이유가 없다.
#
# ■ 이번 조정
#     · 가까운 종(1001)은 <b>줄이고 느리게</b> 차오르게 — 초반용 자원이지 후반 밥줄이 아니다.
#     · 중간·먼 종(1002·1003)은 <b>늘리고 빠르게</b> — 유저 지시 "2, 3번째 개체량을 조금 늘려".
#     · 에너지 보상 격차를 벌린다 — 멀리 갈수록 <b>시간당 수익</b>이 확실히 커야 당위성이 생긴다.
#       (1001 5~10 → 그대로 · 1002 15~25 → 22~38 · 1003 30~50 → 55~90)
#     · 등장 범위는 그대로 둔다(15 / 100 / 200 → 고리 7.5~50 · 50~100 · 100~맵끝).
# ---------------------------------------------------------------------------
NEUTRAL_NEW_COLS = [
    (11, '동시 최대 개체수', 'max_alive', 'int'),
    (12, '재생성 주기(초)', 'respawn_seconds', 'float'),
]

# mon_id: (max_alive, respawn_seconds, min_energy, max_energy, atk_take)
NEUTRAL_ROWS = {
    1001: (8,  45, 5,  10, 0),    # 비선공 — 맞기 전엔 안 문다
    1002: (14, 22, 22, 38, 1),    # 선공
    1003: (18, 16, 55, 90, 1),    # 선공
}


# ---------------------------------------------------------------------------
# 3) 영어 이름 컬럼 삭제
#
# 51절에서 모든 표시 문구를 스트링 키 테이블로 모았고, 영어는 그 표의 `en` 칸이 정본이다.
# 원본 표에 남아 있던 `*_EG` 칸은 <b>같은 값을 두 곳에 적어두는 것</b>이라 어긋날 수밖에 없다.
#
# ⚠ 오른쪽부터 지운다 — 왼쪽부터 지우면 뒤 컬럼 번호가 밀려 엉뚱한 칸을 지운다.
# ---------------------------------------------------------------------------
EG_COLUMNS = [
    (XLSX_WAVE_MON, 'wave_nom',      ['character_name_EG']),
    (XLSX_WAVE_MON, 'wave_mid_boss', ['character_name_EG']),
    (XLSX_WAVE_MON, 'wave_top_boss', ['character_name_EG', 'boss_title_EG']),
    (XLSX_CHAR,     'Character',     ['character_name_EG']),
]


# ---------------------------------------------------------------------------

def backup():
    stamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    folder = os.path.join(BACKUP_ROOT, stamp + '_중립스탯_영어컬럼정리')
    os.makedirs(folder, exist_ok=True)
    for p in (XLSX_NEUTRAL, XLSX_WAVE_MON, XLSX_CHAR):
        if os.path.exists(p):
            shutil.copy2(p, os.path.join(folder, os.path.basename(p)))
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


def build_first_stat(wb):
    """중립 몬스터 first_Stat 시트를 만들거나 갱신한다."""
    names = [ws.Name for ws in wb.Worksheets]
    if 'first_Stat' in names:
        ws = wb.Worksheets('first_Stat')
        print('[중립/first_Stat] 기존 시트 갱신')
    else:
        ws = wb.Worksheets.Add(After=wb.Worksheets(wb.Worksheets.Count))
        ws.Name = 'first_Stat'
        print('[중립/first_Stat] 시트 신설')

    for c, (kr, field, typ) in enumerate(NEUTRAL_STAT_HEADER, start=1):
        ws.Cells(1, c).Value = kr
        ws.Cells(2, c).Value = field
        ws.Cells(3, c).Value = typ

    for i, (mid, vals) in enumerate(sorted(NEUTRAL_STATS.items())):
        r = DATA_ROW0 + i
        ws.Cells(r, 1).Value = mid
        for c, v in enumerate(vals, start=2):
            ws.Cells(r, c).Value = v
        print('  %d -> hp %s · atk %s · def %s · aspd %s · mspd %s'
              % (mid, vals[0], vals[1], vals[4], vals[6], vals[7]))


def update_neutrality_mon(wb):
    ws = wb.Worksheets('neutrality_mon')

    for col, kr, field, typ in NEUTRAL_NEW_COLS:
        existing = ws.Cells(2, col).Value
        if existing and str(existing).strip() and str(existing).strip() != field:
            sys.exit('  ! neutrality_mon %d번째 칸에 이미 다른 필드가 있습니다: %r' % (col, existing))
        ws.Cells(1, col).Value = kr
        ws.Cells(2, col).Value = field
        ws.Cells(3, col).Value = typ
    print('[중립/neutrality_mon] max_alive(K) · respawn_seconds(L) 준비')

    cols = {f: field_col(ws, f) for f in
            ('min_energy', 'max_energy', 'atk', 'atk_take', 'max_alive', 'respawn_seconds')}
    missing = [k for k, v in cols.items() if v is None]
    if missing:
        sys.exit('  ! neutrality_mon 에 없는 컬럼: %s' % missing)

    for r, mid in row_ids(ws):
        if mid not in NEUTRAL_ROWS:
            print('  ! 행 %d (mon_id %d) 는 값이 지정되지 않아 건너뜁니다' % (r, mid))
            continue
        cap, respawn, emin, emax, take = NEUTRAL_ROWS[mid]
        ws.Cells(r, cols['max_alive']).Value = cap
        ws.Cells(r, cols['respawn_seconds']).Value = respawn
        ws.Cells(r, cols['min_energy']).Value = emin
        ws.Cells(r, cols['max_energy']).Value = emax
        ws.Cells(r, cols['atk_take']).Value = take
        # 공격력은 first_Stat 이 정본이지만, 예전 칸도 같은 값으로 맞춰 둔다(눈으로 볼 때 헷갈리지 않게).
        ws.Cells(r, cols['atk']).Value = NEUTRAL_STATS[mid][1]
        print('  %d -> 개체 %d마리 · 재생성 %d초 · 에너지 %d~%d · 선공 %d · 공격력 %d'
              % (mid, cap, respawn, emin, emax, take, NEUTRAL_STATS[mid][1]))


def drop_eg_columns(app):
    for path, sheet, fields in EG_COLUMNS:
        if not os.path.exists(path):
            print('  ! 파일 없음:', path)
            continue

        wb = app.Workbooks.Open(os.path.abspath(path))
        try:
            ws = wb.Worksheets(sheet)
            # 오른쪽부터 지운다 — 왼쪽부터 지우면 뒤 컬럼 번호가 밀린다.
            targets = sorted(
                [(field_col(ws, f), f) for f in fields if field_col(ws, f) is not None],
                reverse=True)
            if not targets:
                print('  %s / %s: 이미 없음 (건너뜀)' % (os.path.basename(path), sheet))
                continue

            for col, field in targets:
                ws.Columns(col).Delete()
                print('  %s / %s: %s (%d열) 삭제'
                      % (os.path.basename(path), sheet, field, col))
            wb.Save()
        finally:
            wb.Close(SaveChanges=False)


def main():
    for p in (XLSX_NEUTRAL, XLSX_WAVE_MON, XLSX_CHAR):
        if not os.path.exists(p):
            sys.exit('파일을 찾지 못했습니다: ' + p)

    folder = backup()
    print('백업:', folder, '\n')

    import win32com.client as win32
    app = win32.gencache.EnsureDispatch('Excel.Application')
    app.Visible = False
    app.DisplayAlerts = False
    try:
        wb = app.Workbooks.Open(os.path.abspath(XLSX_NEUTRAL))
        try:
            build_first_stat(wb)
            update_neutrality_mon(wb)
            wb.Save()
        finally:
            wb.Close(SaveChanges=False)

        print('\n[영어 이름 컬럼 삭제]')
        drop_eg_columns(app)
    finally:
        app.DisplayAlerts = True
        app.Quit()

    print('\n완료 — 이어서 아래를 돌릴 것:')
    print('  python Tools/gen_string_table.py       (영어 이름은 이미 en 칸에 있다 — 확인용)')
    print('  python Tools/sync_tables_to_assets.py  (표 → Unity 에셋)')
    print('  python Tools/gen_character_assets.py   (캐릭터 에셋 이름을 스트링 테이블에서 읽는다)')


if __name__ == '__main__':
    main()
