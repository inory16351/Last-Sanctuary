# -*- coding: utf-8 -*-
"""웨이브 몬스터 테이블.xlsx / Skill 시트에 <b>침식 수치 컬럼</b>을 추가한다 (2026-08-13).

유저 지시: "시스템 로직 변경[을] 게임 시스템에 넣지말고 보스 스킬 테이블에 침식 수치 칼럼
추가하고 ... 테이블 칼럼 추가는 내가 만든 형식 그대로 쓰고 그냥 뒤에 붙이기만 해.
타락한 무덤은 5 / 공허의 광선은 10."

기존 값(value_01~03)·쿨타임과 성격이 같은 "표에 적힌 스킬 파라미터"라서, 이름도 그
형식 그대로 <b>밸류타입_04 / value_04 / float</b> 로 짓고, 기존 9칸(A~I) 뒤에
<b>10번째 칸(J)</b>으로 그냥 붙인다 — 끼워넣지 않는다(다른 표들이 컬럼을 추가할 때 항상
써온 방식과 같다, 예: first_Stat 의 collider_height/width_tiles).

⚠ ★ Excel COM 으로 고치는 이유 (openpyxl 을 쓰지 않는다) — 이 파일의 Skill 시트에는
  `skill_name`·`skill_explain` 칸에 스트링 키 테이블로 넘어가는 <b>하이퍼링크</b>가
  걸려 있다(`Tools/link_string_keys.py`). openpyxl 로 열어 저장하면 이 파일의
  하이퍼링크가 전부 날아간다 — 실제로 UI-17 절에서 12칸이 날아간 적이 있다.
  Excel 로 열어 셀 값만 바꾸고 저장하면 하이퍼링크·서식이 그대로 남는다
  (`convert_tables_to_string_keys.py` 가 같은 이유로 COM 을 쓴다).

사용법:  python Tools/add_boss_skill_erosion_column.py
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
XLSX = os.path.join(TABLE_DIR, '웨이브 몬스터 테이블.xlsx')
BACKUP_ROOT = os.path.join(TABLE_DIR, '_백업')

SHEET = 'Skill'
COL = 10   # J — 기존 9칸(A~I: id·이름·타입·value_01~03·쿨타임·아이콘·설명) 바로 뒤

HEADER_KR = '밸류타입_04'
HEADER_FIELD = 'value_04'
HEADER_TYPE = 'float'

# skill_id → 침식 수치 (유저 확정).
EROSION_BY_SKILL_ID = {
    130001: 5,    # 타락한 무덤
    130002: 10,   # 공허의 광선
}


def backup():
    stamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    folder = os.path.join(BACKUP_ROOT, stamp + '_보스스킬침식컬럼')
    os.makedirs(folder, exist_ok=True)
    shutil.copy2(XLSX, os.path.join(folder, os.path.basename(XLSX)))
    return folder


def main():
    if not os.path.exists(XLSX):
        sys.exit('파일을 찾지 못했습니다: ' + XLSX)

    folder = backup()
    print('백업:', folder)

    import win32com.client as win32
    # ⚠ DispatchEx — 유저가 엑셀을 켜 두면 EnsureDispatch 는 실패하고, 그냥 Dispatch 는
    #   유저 창에 붙어 아래 app.Quit() 이 그 창을 닫아 버린다. DispatchEx 만 새 인스턴스다.
    app = win32.DispatchEx('Excel.Application')
    app.Visible = False
    app.DisplayAlerts = False
    try:
        wb = app.Workbooks.Open(os.path.abspath(XLSX))
        try:
            ws = wb.Worksheets(SHEET)

            existing = ws.Cells(2, COL).Value
            if existing and str(existing).strip():
                sys.exit(f'{COL}번째 칸에 이미 값이 있습니다({existing!r}) — 다른 컬럼과 '
                         '겹치지 않는지 확인하세요.')

            ws.Cells(1, COL).Value = HEADER_KR
            ws.Cells(2, COL).Value = HEADER_FIELD
            ws.Cells(3, COL).Value = HEADER_TYPE

            written = 0
            r = 4
            while ws.Cells(r, 1).Value not in (None, ''):
                skill_id = int(ws.Cells(r, 1).Value)
                if skill_id in EROSION_BY_SKILL_ID:
                    ws.Cells(r, COL).Value = EROSION_BY_SKILL_ID[skill_id]
                    written += 1
                    print(f'  행 {r} (skill_id {skill_id}) -> value_04 = {EROSION_BY_SKILL_ID[skill_id]}')
                else:
                    print(f'  ! 행 {r} (skill_id {skill_id}) 는 침식 값이 지정되지 않아 비워둡니다')
                r += 1

            wb.Save()
            print(f'\n{SHEET} 시트에 {HEADER_FIELD} 컬럼(J) 신설 · 값 {written}개 기록')
        finally:
            wb.Close(SaveChanges=False)
    finally:
        app.DisplayAlerts = True
        app.Quit()


if __name__ == '__main__':
    main()
