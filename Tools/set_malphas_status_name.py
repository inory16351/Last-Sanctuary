# -*- coding: utf-8 -*-
"""말파스 구속탄(130003)의 부여 상태명도 "기절"로 맞춘다 (2026-08-19).

유저 지시: "그냥 말파스 스킬이랑 아니시킬 둘다 기절로 해줘 똑같은 효과인 대신
발동조건이 다름 말파스 스킬은 15초 내에 두번 맞아야 하고 아니시킬은 바로 발동"

발동 조건(15초 내 2회 피격 vs 즉발)은 각 스킬 자신의 밸류(웨이브 몬스터
테이블.xlsx/Skill 130003의 밸류타입_05=15초 창, BossSkillCaster 의 약화→구속 전이
로직)로 이미 갈려 있어 코드 변경이 필요 없다 — 화면에 뜨는 이름만 "구속"에서
"기절"로 바꾼다.

⚠ DispatchEx — 유저가 엑셀을 켜 두고 있을 수 있다(2026-08-19 겪은 함정 참고).

사용법:  python Tools/set_malphas_status_name.py
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

FILE_NAME = '웨이브 몬스터 테이블.xlsx'
STATUS_COL = 16
SKILL_ID = 130003
NEW_VALUE = '기절'


def backup():
    stamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    folder = os.path.join(BACKUP_ROOT, stamp + '_말파스기절통일')
    os.makedirs(folder, exist_ok=True)
    shutil.copy2(os.path.join(TABLE_DIR, FILE_NAME), os.path.join(folder, FILE_NAME))
    return folder


def main():
    path = os.path.join(TABLE_DIR, FILE_NAME)
    if not os.path.exists(path):
        sys.exit('파일을 찾지 못했습니다: ' + FILE_NAME)

    print('백업:', backup())

    import win32com.client as win32
    app = win32.DispatchEx('Excel.Application')
    app.Visible = False
    app.DisplayAlerts = False
    try:
        wb = app.Workbooks.Open(os.path.abspath(path))
        try:
            ws = wb.Worksheets('Skill')

            r = 4
            found = False
            while ws.Cells(r, 1).Value not in (None, ''):
                sid = int(ws.Cells(r, 1).Value)
                if sid == SKILL_ID:
                    before = ws.Cells(r, STATUS_COL).Value
                    ws.Cells(r, STATUS_COL).Value = NEW_VALUE
                    print(f'행{r} (skill_id {sid}): status_name {before!r} -> {NEW_VALUE!r}')
                    found = True
                    break
                r += 1

            if not found:
                sys.exit(f'skill_id {SKILL_ID} 을 찾지 못했습니다.')

            wb.Save()
            print(f'{FILE_NAME} / Skill 시트 저장 완료')
        finally:
            wb.Close(SaveChanges=False)
    finally:
        app.DisplayAlerts = True
        app.Quit()


if __name__ == '__main__':
    main()
