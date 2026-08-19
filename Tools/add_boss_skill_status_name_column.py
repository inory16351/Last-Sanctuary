# -*- coding: utf-8 -*-
"""보스 스킬이 거는 <b>「구속」의 화면 표시 이름</b> 컬럼을 추가한다 (2026-08-19).

유저 지시: *"보스가 지금 상태이상을 가지고 있는 보스들이 있어 예를 들어서 보스가 기절
스킬을 날렸을 때 캐릭터가 맞으면 기절상태이다 라고 명시를 캐릭터 상세 UI에 명시를 할거야
... 테이블에 칼럼도 추가해."*

■ 왜 새 컬럼인가 — 지금 「구속」(이동·공격 불가)을 거는 스킬이 둘 있다
  (`BossSkillSO.BindSeconds > 0`인 것): 말파스의 구속탄(130003) · 아니사킬의
  거대한 위협 포효(2004). 둘 다 <b>같은 게임 메커니즘</b>(`UnitCombat.ApplyBind`)을 쓰지만,
  화면에 뭐라고 부를지는 <b>스킬(보스)마다 다를 수 있다</b> — 실제로 거대한 위협 포효의
  정의문 자체가 *"...이동과 공격이 불가능해진다(<b>기절상태</b>)"* 라고 괄호로 못박아 뒀다.
  그래서 이 이름을 코드에 하드코딩하지 않고 <b>스킬 데이터 컬럼</b>으로 뺀다 — 68절의
  침식(value_04) 때와 같은 이유다.

■ 어느 표에 붙이는가 — 「구속」을 거는 두 스킬이 서로 다른 파일에 있다:
    130003 구속탄            → 웨이브 몬스터 테이블.xlsx / Skill  (기존 15칸 → 16번째 P열)
    2004   거대한 위협 포효   → 임시용 중립 몬스터.xlsx / Skill   (기존 13칸 → 14번째 N열)
  두 표 모두 기존 컬럼 <b>뒤에 그냥 붙인다.</b>

■ 값
    130003 구속탄            → <b>비워둔다</b>. 이 스킬 자신의 정의문이 이미 "구속"이라고
                                부르므로, 코드 기본값("구속")을 그대로 쓰면 된다.
    2004   거대한 위협 포효   → <b>"기절"</b>. 유저가 든 예시("기절 스킬")가 가리키는 것이
                                바로 이 스킬의 정의문에 적힌 그 표현이다.

⚠ ★ Excel COM(`DispatchEx`)으로 고친다 — 두 표 모두 `skill_name`·`skill_explain` 칸에
  하이퍼링크가 걸려 있다. `EnsureDispatch`는 유저가 엑셀을 켜 두면 실패하고, `Dispatch`는
  유저가 열어 둔 창에 붙어 `app.Quit()` 이 그 창을 닫아 버린다(2026-08-19 겪은 함정).

사용법:  python Tools/add_boss_skill_status_name_column.py
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
BACKUP_ROOT = os.path.join(TABLE_DIR, '_백업')

HEADER_KR = '부여 상태명'
HEADER_FIELD = 'status_name'
HEADER_TYPE = 'string'

# (파일, 컬럼 번호, {skill_id: 표시 이름})
TARGETS = [
    ('웨이브 몬스터 테이블.xlsx', 16, {}),                    # 130003 구속탄 — 기본값("구속") 유지
    ('임시용 중립 몬스터.xlsx', 14, {2004: '기절'}),           # 거대한 위협 포효
]


def backup(filenames):
    stamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    folder = os.path.join(BACKUP_ROOT, stamp + '_보스구속상태명컬럼')
    os.makedirs(folder, exist_ok=True)
    for fn in filenames:
        shutil.copy2(os.path.join(TABLE_DIR, fn), os.path.join(folder, fn))
    return folder


def main():
    for fn, _col, _vals in TARGETS:
        if not os.path.exists(os.path.join(TABLE_DIR, fn)):
            sys.exit('파일을 찾지 못했습니다: ' + fn)

    print('백업:', backup([fn for fn, _c, _v in TARGETS]))

    import win32com.client as win32
    # ⚠ DispatchEx — 유저가 엑셀을 켜 두면 EnsureDispatch 는 실패하고, 그냥 Dispatch 는
    #   유저 창에 붙어 아래 app.Quit() 이 그 창을 닫아 버린다. DispatchEx 만 새 인스턴스다.
    app = win32.DispatchEx('Excel.Application')
    app.Visible = False
    app.DisplayAlerts = False
    try:
        for fn, col, values in TARGETS:
            path = os.path.join(TABLE_DIR, fn)
            wb = app.Workbooks.Open(os.path.abspath(path))
            try:
                ws = wb.Worksheets('Skill')

                existing = ws.Cells(2, col).Value
                if existing and str(existing).strip():
                    sys.exit(f'{fn} 의 {col}번째 칸에 이미 값이 있습니다({existing!r}) — '
                             '위치를 확인하세요.')

                ws.Cells(1, col).Value = HEADER_KR
                ws.Cells(2, col).Value = HEADER_FIELD
                ws.Cells(3, col).Value = HEADER_TYPE

                written = 0
                r = 4
                while ws.Cells(r, 1).Value not in (None, ''):
                    sid = int(ws.Cells(r, 1).Value)
                    if sid in values:
                        ws.Cells(r, col).Value = values[sid]
                        written += 1
                        print(f'  {fn} 행{r} (skill_id {sid}) -> status_name = {values[sid]!r}')
                    r += 1

                wb.Save()
                print(f'{fn} / Skill 시트에 status_name 컬럼({col}열) 신설 · 값 {written}개 기록')
            finally:
                wb.Close(SaveChanges=False)
    finally:
        app.DisplayAlerts = True
        app.Quit()


if __name__ == '__main__':
    main()
