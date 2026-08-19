# -*- coding: utf-8 -*-
"""캐릭터 테이블.xlsx / Character 시트에 <b>칭호 컬럼 2개</b>를 추가한다 (2026-08-19).

유저 지시: *"타이틀 칼럼 만들어서 적당한 이름 정해서 넣어줘 내가 별로면 다시 만들게"*.

상세 카드(112절)에 칭호 칸을 만들어 두었는데 <b>캐릭터에는 칭호 데이터가 아예 없어서</b>
항상 빈칸이었다(몬스터·넥서스만 표에 칭호가 있었다). 그 칸을 채운다.

■ 형식은 <b>`wave_top_boss` 의 칭호 2컬럼을 그대로</b> 따른다
    boss_title      → 스트링 키가 들어간다 (예: boss_title_120001)
    boss_title_EG   → 영어 문구가 그대로 들어간다 (예: The Lord Of Endless Forms)
  그래서 여기서도 `character_title` / `character_title_EG` 두 칸을 만든다.
  기존 7컬럼(A~G) <b>뒤에 그냥 붙인다</b> — 끼워넣지 않는다.
  (`gen_character_assets.py` 는 Character 시트를 <b>필드명으로</b> 읽으므로 안전하다.)

■ 이 스크립트는 <b>한글 문구를 그대로</b> 넣는다. 그다음 파이프라인이 키로 바꾼다:
    1) python Tools/add_character_title_column.py     ← 여기 (문구를 표에 적는다)
    2) python Tools/gen_string_table.py               ← 문구를 스트링 키 테이블로 수집
    3) python Tools/convert_tables_to_string_keys.py  ← 표의 문구를 키로 치환
  51절이 정한 그 순서 그대로다.

⚠ ★ Excel COM 으로 고친다 (openpyxl 금지) — 이 파일의 `character_name` 칸에는 스트링 키
  테이블로 넘어가는 <b>하이퍼링크</b>가 걸려 있다(`link_string_keys.py`). openpyxl 로 저장하면
  파일의 하이퍼링크가 전부 날아간다(UI-17 에서 실제로 겪었다).

사용법:  python Tools/add_character_title_column.py
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
XLSX = os.path.join(TABLE_DIR, '캐릭터 테이블.xlsx')
BACKUP_ROOT = os.path.join(TABLE_DIR, '_백업')

SHEET = 'Character'
COL_KR = 8    # H — 기존 7칸(A~G: id·이름·스킬3·일러스트·인게임에셋) 바로 뒤
COL_EN = 9    # I

HEADERS = [
    (COL_KR, '칭호', 'character_title', 'string'),
    (COL_EN, '영어 칭호', 'character_title_EG', 'string'),
]

# character_id → (한글 칭호, 영어 칭호)
#
# 서사·스킬에서 뽑았다. 보스 칭호(「끝없는 형상의 군주」·「구속의 공작」)와 같은
# <수식어> + <명사> 짜임으로 맞췄고, 카드의 칭호 칸(208px)에 들어가게 짧게 줄였다.
TITLES = {
    # "눈을 가린 채 ... 보지 못하지만 누구보다 먼저 침입자를 알아챈다" — 시야 0 패시브
    9001: ('눈먼 파수꾼', 'The Blind Sentinel'),
    # "전열을 지탱하는 대식세포 ... 무너지지 않는 것이 그의 역할이다" — 로 아이아스·강철의 의지
    9002: ('무너지지 않는 방벽', 'The Unbreaking Bulwark'),
    # "굶주린 자연살해세포 ... 먹어치울수록 빨라진다" — 포식·희열·광란
    9003: ('굶주린 사냥꾼', 'The Starving Hunter'),
    # 역병의사 차림 + 부식·정신 안정·정화의 손길
    9004: ('역병을 걷는 의사', 'The Plague Walking Doctor'),
    # "죽음조차 히스톤의 복수를 막을 수는 없습니다" — 선봉장·분노·복수자
    9005: ('죽음을 딛는 복수자', 'The Avenger Beyond Death'),
}


def backup():
    stamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    folder = os.path.join(BACKUP_ROOT, stamp + '_캐릭터칭호컬럼')
    os.makedirs(folder, exist_ok=True)
    shutil.copy2(XLSX, os.path.join(folder, os.path.basename(XLSX)))
    return folder


def main():
    if not os.path.exists(XLSX):
        sys.exit('파일을 찾지 못했습니다: ' + XLSX)

    print('백업:', backup())

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

            for col, kr, field, typ in HEADERS:
                existing = ws.Cells(2, col).Value
                if existing and str(existing).strip() not in ('', field):
                    sys.exit('%d번째 칸에 이미 다른 컬럼(%r)이 있습니다 — 위치를 확인하세요.'
                             % (col, existing))
                ws.Cells(1, col).Value = kr
                ws.Cells(2, col).Value = field
                ws.Cells(3, col).Value = typ

            written = 0
            r = 4
            while ws.Cells(r, 1).Value not in (None, ''):
                cid = int(ws.Cells(r, 1).Value)
                if cid in TITLES:
                    kr, en = TITLES[cid]
                    # 이미 키로 바뀐 뒤 다시 돌려도 문구로 되돌리지 않는다(멱등).
                    cur = ws.Cells(r, COL_KR).Value
                    if not (cur and str(cur).strip().startswith('character_title_')):
                        ws.Cells(r, COL_KR).Value = kr
                    ws.Cells(r, COL_EN).Value = en
                    written += 1
                    print('  %d → %s / %s' % (cid, kr, en))
                else:
                    print('  ! %d 는 칭호가 지정되지 않아 비워둡니다' % cid)
                r += 1

            wb.Save()
            print('\n%s 시트에 character_title·character_title_EG 신설 · %d명 기록'
                  % (SHEET, written))
            print('이어서 gen_string_table.py → convert_tables_to_string_keys.py 를 돌릴 것.')
        finally:
            wb.Close(SaveChanges=False)
    finally:
        app.DisplayAlerts = True
        app.Quit()


if __name__ == '__main__':
    main()
