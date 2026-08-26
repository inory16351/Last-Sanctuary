# -*- coding: utf-8 -*-
"""「대체 이름 테이블.xlsx」를 만들고 스트링 키 테이블에 병합한다 (2026-08-26 · 유저 지시:
*"같은 캐릭터가 두번째로 등장할때는 랜덤한 다른 이름을 가지고 태어나게 해 … 랜덤하게 배정할
다른이름은 테이블 따로 만들어서 관리해 주고, 스트링 키에도 영어 / 한국어 이름 추가해줘"*).

■ 왜 표를 따로 두나
  대체 이름은 <b>인물 정의와 짝이 없는 «이름 주머니»</b> 다. 캐릭터 테이블에 칸을 만들면
  «누구의 두 번째 이름» 처럼 보이지만, 실제로는 <b>누구에게든 갈 수 있는 이름</b>이다.
  그래서 표를 따로 두고, 코드는 <b>스트링 키</b>(`character_altname_N`)만 본다.

■ 코드가 «몇 개인지» 를 모른다
  `CharacterAltNames` 는 `character_altname_1` 부터 <b>빈 번호가 나올 때까지</b> 훑는다.
  그러니 이 표에 줄을 더하고 이 스크립트를 다시 돌리면 <b>코드를 고치지 않아도</b> 늘어난다.
  ⚠ 그래서 <b>id 는 1 부터 «구멍 없이»</b> 이어야 한다 — 중간이 비면 거기서 목록이 끊긴다.
     이 스크립트가 그 검사를 한다.

■ 이 스크립트가 하는 일
  ① 표가 없으면 <b>새로 만든다</b>(아래 NAMES 로). 이미 있으면 <b>표를 정본으로 읽는다</b> —
     사람이 다듬은 이름을 덮지 않는다(`--force` 로만 다시 만든다 · .bak 을 남긴다).
  ② 표의 (id, 한국어, 영어)를 스트링 키 테이블에 <b>없는 키만</b> 덧붙인다.
  ③ id 가 1..N 연속인지 검사하고, 아니면 실패로 끝낸다.

■ 다음
    py -3 Tools/gen_string_table.py   →  py -3 Tools/link_string_keys.py
"""
import os
import shutil
import sys

import openpyxl
from openpyxl.styles import Alignment, Font, PatternFill

from vault_path import TABLE_DIR

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

XLSX = os.path.join(TABLE_DIR, '대체 이름 테이블.xlsx')
STRING_XLSX = os.path.join(TABLE_DIR, '스트링 키 테이블.xlsx')

SHEET = 'AltName'
INFO = '읽기'
KEY_PREFIX = 'character_altname_'
SOURCE = '대체 이름 표'

HEADER_KR = ['대체이름id', '한국어', '영어', '비고']
HEADER_FIELD = ['alt_name_id', 'kr', 'en', 'note']
HEADER_TYPE = ['int', 'string', 'string', '-']
DATA_ROW0 = 4          # 스트링 키 테이블과 같은 3행 헤더 규약

FONT = 'Arial'

#: 처음 표를 만들 때 넣는 이름들. 기존 인물 이름의 표기 규칙을 따랐다
#: (한국어는 소리대로 · 영어는 라틴 표기 — 엘린/Elin · 시카리아/Sicaria · 카이론/Chiron).
NAMES = [
    ('아드리엘', 'Adriel'), ('노아네', 'Noane'), ('레이린', 'Reilin'),
    ('미르카', 'Mirka'), ('세이하', 'Seiha'), ('오르넬', 'Ornel'),
    ('유리안', 'Yurian'), ('이레아', 'Irea'), ('카일런', 'Kailen'),
    ('타비아', 'Tavia'), ('페릴', 'Peril'), ('하르윈', 'Harwin'),
    ('가리엘', 'Gariel'), ('나비스', 'Navis'), ('데인', 'Dane'),
    ('라비니아', 'Lavinia'), ('마르첼', 'Marcel'), ('바이런', 'Byron'),
    ('사비네', 'Sabine'), ('아셀', 'Asel'), ('에리온', 'Erion'),
    ('오필리아', 'Ophelia'), ('율리아', 'Julia'), ('제피르', 'Zephyr'),
    ('카린', 'Karin'), ('테오도르', 'Theodor'), ('파비안', 'Fabian'),
    ('할리아', 'Halia'), ('그웬', 'Gwen'), ('니콜라', 'Nicola'),
    ('도리안', 'Dorian'), ('류시아', 'Lucia'), ('메이런', 'Meiren'),
    ('베르타', 'Berta'), ('솔레인', 'Solein'), ('아르덴', 'Arden'),
    ('엘로이', 'Eloi'), ('오데트', 'Odette'), ('이자벨', 'Isabel'),
    ('카시엘', 'Casiel'),
]

INFO_LINES = [
    ('■ 이 표는 무엇인가', True),
    ('같은 인물이 <이번 판에 두 번째로> 등장할 때 받는 «다른 이름» 주머니다.', False),
    ('죽은 인물이 다시 생성되면 다른 이름으로 태어나 «다른 사람» 처럼 보인다(엔딩 명단도 그 이름).', False),
    ('', False),
    ('■ 규칙', True),
    ('· id 는 1 부터 «구멍 없이» 이어야 한다 — 코드가 1 부터 훑다가 빈 번호에서 멈춘다.', False),
    ('· 한 판에서 같은 이름은 두 번 쓰이지 않는다. 이름이 다 떨어지면 원래 이름으로 태어난다.', False),
    ('· 화면에 나가는 문구는 <스트링 키 테이블>의 character_altname_<id> 다(한국어·영어).', False),
    ('', False),
    ('■ 이름을 더하거나 고치는 방법', True),
    ('1) 이 시트에 줄을 더한다(id 는 다음 번호).', False),
    ('2) py -3 Tools/gen_alt_name_table.py   → 스트링 키 테이블에 새 키가 붙는다', False),
    ('3) py -3 Tools/gen_string_table.py     → StringTable.txt 로 내보낸다', False),
    ('4) py -3 Tools/link_string_keys.py     → 하이퍼링크 재생성', False),
    ('★ 코드는 «몇 개인지» 를 모른다 — 표가 정본이라 코드를 고칠 필요가 없다.', False),
    ('', False),
    ('■ 표기 규칙', True),
    ('한국어는 소리대로, 영어는 라틴 표기. 기존 인물 이름과 같은 결로 짓는다', False),
    ('(엘린/Elin · 비기오르/Bigior · 시카리아/Sicaria · 카이론/Chiron · 아르세니아/Arsenia).', False),
]


def style_header(ws, headers):
    fill = PatternFill('solid', fgColor='DDEBF7')
    for row_idx, row in enumerate(headers, start=1):
        for col_idx, value in enumerate(row, start=1):
            c = ws.cell(row=row_idx, column=col_idx)
            c.value = value
            c.font = Font(name=FONT, bold=row_idx == 1)
            c.fill = fill
            c.alignment = Alignment(vertical='center')
    ws.column_dimensions['A'].width = 14
    ws.column_dimensions['B'].width = 16
    ws.column_dimensions['C'].width = 18
    ws.column_dimensions['D'].width = 40
    ws.freeze_panes = 'A4'


def create():
    wb = openpyxl.Workbook()
    info = wb.active
    info.title = INFO
    info.column_dimensions['A'].width = 104
    for i, (text, bold) in enumerate(INFO_LINES, start=1):
        c = info.cell(row=i, column=1)
        c.value = text
        c.font = Font(name=FONT, bold=bold)
        c.alignment = Alignment(vertical='center', wrap_text=True)

    ws = wb.create_sheet(SHEET)
    style_header(ws, [HEADER_KR, HEADER_FIELD, HEADER_TYPE])
    for i, (kr, en) in enumerate(NAMES, start=1):
        r = DATA_ROW0 + i - 1
        ws.cell(row=r, column=1).value = i
        ws.cell(row=r, column=2).value = kr
        ws.cell(row=r, column=3).value = en
        for col in range(1, 5):
            ws.cell(row=r, column=col).font = Font(name=FONT)
    wb.save(XLSX)
    print('  표 생성:', os.path.basename(XLSX), '· 이름 %d개' % len(NAMES))


def read():
    ws = openpyxl.load_workbook(XLSX, data_only=True)[SHEET]
    rows = []
    for r in range(DATA_ROW0, ws.max_row + 1):
        raw = ws.cell(row=r, column=1).value
        if raw is None or str(raw).strip() == '':
            continue
        try:
            alt_id = int(raw)
        except (TypeError, ValueError):
            sys.exit('id 가 숫자가 아닙니다: %r (행 %d)' % (raw, r))
        kr = (ws.cell(row=r, column=2).value or '')
        en = (ws.cell(row=r, column=3).value or '')
        rows.append((alt_id, str(kr).strip(), str(en).strip()))
    return rows


def merge_into_string_table(rows):
    wb = openpyxl.load_workbook(STRING_XLSX)
    ws = wb['string']

    keys = {}
    last_row = DATA_ROW0 - 1
    for r in range(DATA_ROW0, ws.max_row + 1):
        k = ws.cell(row=r, column=1).value
        if k is None or str(k).strip() == '':
            continue
        keys[str(k).strip()] = r
        last_row = r

    added = []
    for alt_id, kr, en in rows:
        key = KEY_PREFIX + str(alt_id)
        if key in keys:
            continue
        last_row += 1
        ws.cell(row=last_row, column=1).value = key
        ws.cell(row=last_row, column=2).value = kr
        ws.cell(row=last_row, column=3).value = en
        ws.cell(row=last_row, column=4).value = SOURCE
        ws.cell(row=last_row, column=5).value = '두 번째 등장 인물의 이름 주머니'
        keys[key] = last_row
        added.append((key, kr, en))

    if added:
        shutil.copyfile(STRING_XLSX, STRING_XLSX + '.bak')
        wb.save(STRING_XLSX)
    return added


def main():
    force = '--force' in sys.argv

    if not os.path.exists(XLSX):
        create()
    elif force:
        shutil.copyfile(XLSX, XLSX + '.bak')
        create()
        print('  ⚠ --force 로 다시 만들었습니다(.bak 남김)')
    else:
        print('  표가 이미 있습니다 — 표를 정본으로 읽습니다(다시 만들려면 --force).')

    rows = read()
    if not rows:
        sys.exit('표에 이름이 없습니다.')

    # ★ id 가 1..N 연속인가 — 구멍이 있으면 코드가 거기서 목록을 끊는다.
    ids = sorted(r[0] for r in rows)
    expected = list(range(1, len(ids) + 1))
    if ids != expected:
        missing = [i for i in expected if i not in ids]
        sys.exit('✗ id 가 1 부터 연속이 아닙니다 — 코드가 빈 번호에서 멈춥니다.\n'
                 '  빠진 번호: %s' % (missing[:20] or '(중복이 있습니다)'))

    empty = [r[0] for r in rows if not r[1] or not r[2]]
    if empty:
        sys.exit('✗ 한국어/영어가 빈 줄이 있습니다: id %s' % empty[:20])

    added = merge_into_string_table(rows)

    print('[대체 이름] 표 %d줄 · 스트링 표에 덧붙인 키 %d개' % (len(rows), len(added)))
    for k, kr, en in added[:60]:
        print('    +', k, '|', kr, '|', en)
    if not added:
        print('    (스트링 표가 이미 표와 같습니다)')
    print('  다음: py -3 Tools/gen_string_table.py  →  py -3 Tools/link_string_keys.py')


if __name__ == '__main__':
    main()
