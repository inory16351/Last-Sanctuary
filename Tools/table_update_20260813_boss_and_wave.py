# -*- coding: utf-8 -*-
"""2026-08-13 유저 지시분 — 표 갱신 4건 (컬럼 3개 신설 + 값 1개 수정).

유저 지시(요약):
  2번 "보스 이동 속도 수정 (증가) -> 너무 느림"
  3번 "포탈에서 몬스터 등장 시 여러 마리 나오게 -> 각개 격파가 너무 잘돼서 디펜스 느낌이
       안 남. 웨이브 테이블 데이터랑 로직 좀 변경해서 개선해 줘"
  5번 "에딧 모드에서 Cast Seconds 를 각 스킬마다 설정할 수 있게 -> 공허의 광선 Cast
       Seconds 초 증가 (가시성을 증가하기 위함)"
  4번 "…360도 범위 값으로 적용 가능하게 해줘 원형으로"  → range_type 칸
 11번 "단탈리온처럼 보스 몬스터는 소환되면 체력바에 타이틀을 붙여서 표기"

■ 이 스크립트가 하는 일
  ┌ 웨이브 몬스터 테이블.xlsx
  │  · Skill          K = cast_time(float)   · L = range_type(enum)     [컬럼 신설]
  │  · first_Stat     120001(단탈리온) movement_speed 1 → 4             [값 수정]
  │  · wave_mid_boss  H = boss_title(string)                            [컬럼 신설]
  └ 웨이브테이블.xlsx
     · Sheet2         H = spawn_group_size(int)                         [컬럼 신설]

■ 규칙 — 기존 표 형식을 건드리지 않는다
  컬럼은 <b>항상 맨 뒤에 붙인다</b>. 중간에 끼우면 `read_sheet()` 가 위치로 읽는 기존
  코드(`r[10]` = 방어력 등)가 통째로 깨진다 — 65-2절이 같은 이유로 정한 규약이다.

■ ⚠ Excel COM 을 쓰는 이유 (openpyxl 을 쓰지 않는다)
  `웨이브 몬스터 테이블.xlsx` 의 Skill·wave_* 시트에는 스트링 키 테이블로 넘어가는
  <b>하이퍼링크</b>가 걸려 있다(`Tools/link_string_keys.py`). openpyxl 로 열어 저장하면
  파일 전체의 하이퍼링크가 날아간다 — 51-3절·64-2절·68-1절에서 세 번 확인한 함정이다.
  Excel 로 열어 셀 값만 바꾸면 하이퍼링크·서식이 그대로 남는다.

사용법:  python Tools/table_update_20260813_boss_and_wave.py
        (재실행 안전 — 이미 같은 값이면 건너뛴다)
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
XLSX_WAVE_MON = os.path.join(TABLE_DIR, '웨이브 몬스터 테이블.xlsx')
XLSX_WAVE = os.path.join(TABLE_DIR, '웨이브테이블.xlsx')
BACKUP_ROOT = os.path.join(TABLE_DIR, '_백업')

DATA_ROW0 = 4      # 1행 한글 라벨 / 2행 필드명 / 3행 자료형 / 4행부터 데이터


# ---------------------------------------------------------------------------
# 1) Skill 시트 — cast_time · range_type
#
# cast_time: 연출(시전 모션 + 지면 범위 표시)이 화면에 머무는 시간(초).
#   예전에는 BossSkillCaster 인스펙터의 castSeconds 0.55 하나로 <b>모든 스킬이 같았다.</b>
#   유저가 "공허의 광선은 가시성을 위해 더 길게" 를 요구해 스킬 데이터로 내렸다.
#   ⚠ 피해는 시전과 <b>동시에</b> 들어간다 — 이 값은 판정 시점이 아니라 연출 길이다.
#
# range_type: 범위 모양. 'Line'(기본) = 조준 방향으로 뻗는 직사각형, 'Circle' = 보스 중심 원형.
#   ★ 두 스킬 모두 <b>Line</b> 으로 둔다. 유저가 "원형으로" 라고 했지만 단탈리온의
#     두 원화는 가로로 긴 <b>파동·광선</b>이라(ShockwaveFx 3.06:1 · BeamFx 9.42:1)
#     원형으로 만들면 그림과 판정이 어긋난다. 유저가 실제로 지적한 문제
#     ("4방향에 적이 없으면 대각선 적을 못 때린다")는 <b>조준을 360도로 푸는 것</b>으로
#     해소했다(BossSkillCaster). 진짜 원형이 필요해지면 이 칸만 Circle 로 바꾸면 된다.
# ---------------------------------------------------------------------------
SKILL_COLS = [
    # (컬럼번호, 1행 한글라벨, 2행 필드명, 3행 자료형, {skill_id: 값})
    (11, '시전 시간', 'cast_time', 'float', {
        130001: 1.2,    # 타락한 무덤 — 근접 광역. 예전 0.55 보다 두 배쯤 보이게
        130002: 2.5,    # 공허의 광선 — 유저 지시로 확실히 길게(가시성)
    }),
    (12, '범위 타입', 'range_type', 'enum', {
        130001: 'Line',
        130002: 'Line',
    }),
]


# ---------------------------------------------------------------------------
# 2) first_Stat — 최종보스 이동속도
#
# 유저: "보스 이동 속도 수정 (증가) -> 너무 느림".
# 게임의 실제 이동속도는 38-1절 공식 `2.1 + 3.9·s/(s+50)` 으로 풀린다:
#     s=1 → 2.18   s=4 → 2.39   s=8 → 2.64
# 그런데 <b>보스 에셋에는 공식과 무관한 1.4 가 손으로 적혀 있었다</b>(잡몹 2.2 · 중간보스 2.25).
# 그래서 보스가 자기 호위대보다 <b>36% 느려</b> 항상 한참 뒤에 도착했다.
# 스탯을 4 로 올려 2.39 가 되게 한다 — 잡몹보다 아주 조금 빠른 정도이고 현재의 1.7배다.
# ⚠ 이 값이 실제로 에셋에 들어가려면 `sync_tables_to_assets.py` 가 moveSpeedTiles 를
#   옮겨야 한다. 예전에는 중간보스만 옮기고 있었다 — 같이 고쳤다.
# ---------------------------------------------------------------------------
FIRST_STAT_FIELD = 'movement_speed'
FIRST_STAT_EDITS = {120001: 4}


# ---------------------------------------------------------------------------
# 3) wave_mid_boss — boss_title
#
# 유저: "단탈리온처럼 보스 몬스터는 소환되면 체력바에 타이틀을 붙여서 표기".
# 최종보스에만 `boss_title` 칸이 있었다(wave_top_boss G열). 중간보스에는 칸 자체가 없어서
# 체력바에 띄울 칭호가 없었다 — 64-1절에서 영어 이름 칸이 없던 것과 똑같은 상황이다.
#
# ⚠ <b>한국어 문구를 그대로 적는다</b>(키가 아니라). `gen_string_table.py` 가 이 칸을
#   읽어 `boss_title_110001` 키를 만들고 kr 칸을 채운다 — 그 파일 Info 시트가 안내하는
#   "① 원본 테이블에 한국어를 적고 gen 을 돌린다" 경로 그대로다.
#   게임 쪽은 id 로 키를 조립하므로(`titleKey = boss_title_<id>`) 셀을 키로 바꿀 필요가 없다.
#
# 칭호는 64-1절과 같이 <b>대충 정해서 넣는 위임</b> 범위다 — 표가 정본이므로 마음에 안 들면
# 이 칸만 고치고 gen_string_table.py 를 다시 돌리면 된다.
# ---------------------------------------------------------------------------
MID_BOSS_TITLE_COL = 8      # H — 기존 7칸(A~G) 바로 뒤
MID_BOSS_TITLES = {
    110001: '피에 새겨진 낙인',      # 혈인 / BloodMark — 근접
    110002: '허공을 삼킨 목소리',    # 공허의 속삭임 / VoidWhisper — 원거리
}


# ---------------------------------------------------------------------------
# 4) 웨이브테이블 Sheet2 — spawn_group_size
#
# 유저: "포탈에서 몬스터 등장 시 여러 마리 나오게 -> 각개 격파가 너무 잘돼서 디펜스 느낌이
#        안 남."
#
# 지금까지는 <b>한 마리씩</b>, 그것도 포탈을 <b>돌아가며</b> 내보냈다(MonsterSpawner.PortalAt).
# 그래서 어느 순간에도 화면에는 서로 다른 방향에서 온 한 마리씩만 있었고, 캐릭터 4명이
# 차례로 하나씩 처리하면 끝났다 — "각개 격파" 의 실체가 이 두 가지다.
#
# 이 칸은 <b>한 포탈에서 한 번에 튀어나오는 마리 수</b>다. 무리 하나가 통째로 같은 자리에서
# 나와 같이 걸어오므로 "떼로 밀려온다" 가 된다. 총 마리 수·능력치는 그대로다 —
# 나오는 <b>방식</b>만 바뀐다.
#
# 값: 웨이브가 오를수록 무리가 커진다(2 → 7). 1 이면 예전과 완전히 같은 동작이다.
# ---------------------------------------------------------------------------
GROUP_COL = 8       # H — 기존 7칸(A~G) 바로 뒤
GROUP_BY_WAVE = {
    1: 2,  2: 2,  3: 3,  4: 3,  5: 4,
    6: 4,  7: 4,  8: 5,  9: 5,  10: 5,
    11: 5, 12: 5, 13: 6, 14: 6, 15: 6,
    16: 6, 17: 6, 18: 7, 19: 7, 20: 7,
}


# ---------------------------------------------------------------------------

def backup():
    stamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    folder = os.path.join(BACKUP_ROOT, stamp + '_보스속도_무리소환_시전시간_칭호')
    os.makedirs(folder, exist_ok=True)
    for p in (XLSX_WAVE_MON, XLSX_WAVE):
        shutil.copy2(p, os.path.join(folder, os.path.basename(p)))
    return folder


def field_col(ws, field):
    """2행(필드명)에서 컬럼 번호를 찾는다. 없으면 None.

    ⚠ 필드명 앞뒤 공백을 없애고 비교한다 — 이 표에는 ' resistance' 처럼 앞 공백이 붙은
      헤더가 실제로 있다(gen_string_table.field_index 와 같은 이유).
    """
    c = 1
    while c <= 64:
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
        c += 1
    return None


def row_ids(ws, id_col=1):
    """(행번호, id) 목록. 4행부터 id 칸이 빌 때까지."""
    out = []
    r = DATA_ROW0
    while ws.Cells(r, id_col).Value not in (None, ''):
        out.append((r, int(ws.Cells(r, id_col).Value)))
        r += 1
    return out


def ensure_column(ws, col, label_kr, field, type_name):
    """헤더 3줄을 채운다. 이미 같은 필드면 그대로 두고, 다른 필드가 있으면 멈춘다."""
    existing = ws.Cells(2, col).Value
    if existing and str(existing).strip():
        if str(existing).strip() != field:
            sys.exit(f'  ! {ws.Name} {col}번째 칸에 이미 다른 필드가 있습니다: {existing!r}')
        return False           # 이미 있다 — 값만 갱신한다

    ws.Cells(1, col).Value = label_kr
    ws.Cells(2, col).Value = field
    ws.Cells(3, col).Value = type_name
    return True


def main():
    for p in (XLSX_WAVE_MON, XLSX_WAVE):
        if not os.path.exists(p):
            sys.exit('파일을 찾지 못했습니다: ' + p)

    folder = backup()
    print('백업:', folder)

    import win32com.client as win32
    app = win32.gencache.EnsureDispatch('Excel.Application')
    app.Visible = False
    app.DisplayAlerts = False
    try:
        # ── 웨이브 몬스터 테이블 ────────────────────────────────────────────
        wb = app.Workbooks.Open(os.path.abspath(XLSX_WAVE_MON))
        try:
            # 1) Skill — cast_time · range_type
            ws = wb.Worksheets('Skill')
            for col, label, field, typ, values in SKILL_COLS:
                made = ensure_column(ws, col, label, field, typ)
                print(f'[Skill] {field}({chr(64 + col)}열) {"신설" if made else "기존"}')
                for r, sid in row_ids(ws):
                    if sid not in values:
                        print(f'  ! 행 {r} (skill_id {sid}) 는 값이 지정되지 않아 비워둡니다')
                        continue
                    ws.Cells(r, col).Value = values[sid]
                    print(f'  행 {r} (skill_id {sid}) -> {field} = {values[sid]}')

            # 2) first_Stat — 보스 이동속도
            ws = wb.Worksheets('first_Stat')
            col = field_col(ws, FIRST_STAT_FIELD)
            if col is None:
                sys.exit(f'  ! first_Stat 에 {FIRST_STAT_FIELD} 컬럼이 없습니다')
            print(f'[first_Stat] {FIRST_STAT_FIELD}({chr(64 + col)}열) 값 수정')
            for r, mid in row_ids(ws):
                if mid not in FIRST_STAT_EDITS:
                    continue
                before = ws.Cells(r, col).Value
                ws.Cells(r, col).Value = FIRST_STAT_EDITS[mid]
                print(f'  행 {r} (monster_id {mid}) -> {FIRST_STAT_FIELD} {before} → '
                      f'{FIRST_STAT_EDITS[mid]}')

            # 3) wave_mid_boss — boss_title
            ws = wb.Worksheets('wave_mid_boss')
            made = ensure_column(ws, MID_BOSS_TITLE_COL, '칭호', 'boss_title', 'string')
            print(f'[wave_mid_boss] boss_title({chr(64 + MID_BOSS_TITLE_COL)}열) '
                  f'{"신설" if made else "기존"}')
            for r, mid in row_ids(ws):
                if mid not in MID_BOSS_TITLES:
                    continue
                cur = ws.Cells(r, MID_BOSS_TITLE_COL).Value
                # 이미 스트링 키로 바뀐 칸이면 덮지 않는다 — 사람이 정리한 결과다.
                if cur and str(cur).strip().startswith('boss_title_'):
                    print(f'  행 {r} (monster_id {mid}) 는 이미 키({cur})라 건드리지 않습니다')
                    continue
                ws.Cells(r, MID_BOSS_TITLE_COL).Value = MID_BOSS_TITLES[mid]
                print(f'  행 {r} (monster_id {mid}) -> boss_title = {MID_BOSS_TITLES[mid]}')

            wb.Save()
        finally:
            wb.Close(SaveChanges=False)

        # ── 웨이브테이블 ────────────────────────────────────────────────────
        wb = app.Workbooks.Open(os.path.abspath(XLSX_WAVE))
        try:
            ws = wb.Worksheets('Sheet2')
            made = ensure_column(ws, GROUP_COL, '포탈 동시 등장 마리 수',
                                 'spawn_group_size', 'int')
            print(f'[웨이브테이블/Sheet2] spawn_group_size({chr(64 + GROUP_COL)}열) '
                  f'{"신설" if made else "기존"}')

            # 이 시트는 id 가 wave_id(A) 이고 웨이브 번호는 B 열이다.
            wave_col = field_col(ws, 'wave_num')
            if wave_col is None:
                sys.exit('  ! Sheet2 에 wave_num 컬럼이 없습니다')

            for r, _ in row_ids(ws):
                wn = int(ws.Cells(r, wave_col).Value)
                size = GROUP_BY_WAVE.get(wn)
                if size is None:
                    print(f'  ! 행 {r} (웨이브 {wn}) 는 값이 지정되지 않아 비워둡니다')
                    continue
                ws.Cells(r, GROUP_COL).Value = size
                print(f'  행 {r} (웨이브 {wn:>2}) -> spawn_group_size = {size}')

            wb.Save()
        finally:
            wb.Close(SaveChanges=False)
    finally:
        app.DisplayAlerts = True
        app.Quit()

    print('\n완료 — 이어서 아래를 순서대로 돌릴 것:')
    print('  python Tools/gen_string_table.py       (중간보스 칭호를 스트링 테이블로)')
    print('  python Tools/sync_tables_to_assets.py  (표 → Unity 에셋)')


if __name__ == '__main__':
    main()
