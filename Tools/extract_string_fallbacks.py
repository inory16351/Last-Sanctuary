# -*- coding: utf-8 -*-
"""★ <b>코드의 폴백 문구를 그대로 읽어낸다</b> — 표에 넣을 kr 을 손으로 옮겨 적지 않기 위해
(2026-08-27 신설).

■ 왜 필요한가
  이관 스크립트의 규약은 «kr 은 <b>코드의 지금 폴백과 한 글자도 다르지 않게</b> 적는다» 이다
  (`table_update_20260826_window_labels.py` 의 머리글). 그런데 그것을 <b>사람이 옮겨 적으면</b>
  반드시 한 글자가 어긋난다 — 그러면 표의 kr 과 화면의 글자가 갈라지고, 나중에 «표를 고쳤는데
  화면이 안 바뀐다» 로 나타난다. `table_update_20260826_scene_labels.py` 가 <b>씬에서</b> kr 을
  읽어 오는 것과 <b>같은 이유·같은 방식</b>이다. 이쪽은 <b>코드에서</b> 읽는다.

■ 무엇을 하나
  `Assets/_Project/Scripts` 전체에서 다음 꼴을 찾아 (키, 폴백) 을 뽑는다:
      HudTheme.T("키", "폴백")        ·  UI.HudTheme.T("키", "폴백")
      StringTable.Get("키", "폴백")   ·  Data.StringTable.Get("키", "폴백")
  이어 붙인 폴백(`"앞" + "뒤"`)과 줄바꿈으로 접힌 것도 이어서 읽는다.

■ 쓰는 법
    py -3 Tools/extract_string_fallbacks.py                # 전부
    py -3 Tools/extract_string_fallbacks.py --missing      # 표에 <b>없는</b> 키만
    py -3 Tools/extract_string_fallbacks.py --missing --py # 이관 스크립트에 붙일 파이썬 리터럴로

⚠ <b>같은 키에 폴백이 두 가지</b>로 적혀 있으면 그것부터 보고한다 — 그 자체가 버그다
  (한쪽을 고치고 다른 쪽을 잊은 자리).
"""
import os
import re
import sys
import glob

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS = os.path.join(PROJECT, 'Assets', '_Project', 'Scripts')
TSV = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'Data', 'StringTable.txt')

HANGUL = re.compile(r'[가-힣]')

#: `…T(` / `…Get(` 의 여는 괄호 자리를 잡는다. 인자는 아래에서 손으로 훑는다
#  (정규식 하나로 «이어 붙인 문자열» 까지 잡으려 들면 반드시 새는 자리가 생긴다).
OPEN = re.compile(r'(?:HudTheme\.T|StringTable\.Get)\s*\(')


def read_cs_string(src, i):
    """`src[i]` 가 여는 따옴표일 때 C# 문자열 하나를 읽어 (값, 다음 위치) 를 준다."""
    assert src[i] == '"'
    i += 1
    out = []
    while i < len(src):
        c = src[i]
        if c == '\\':
            nxt = src[i + 1] if i + 1 < len(src) else ''
            out.append({'n': '\n', 't': '\t', 'r': '\r',
                        '"': '"', '\\': '\\'}.get(nxt, '\\' + nxt))
            i += 2
            continue
        if c == '"':
            return ''.join(out), i + 1
        out.append(c)
        i += 1
    return ''.join(out), i


def read_concat(src, i):
    """`"앞" + "뒤"` 처럼 이어 붙인 문자열을 <b>하나로</b> 읽는다. 문자열이 아니면 None."""
    parts = []
    while True:
        while i < len(src) and src[i] in ' \t\r\n':
            i += 1
        if i >= len(src) or src[i] != '"':
            return (None, i) if not parts else (''.join(parts), i)
        s, i = read_cs_string(src, i)
        parts.append(s)
        j = i
        while j < len(src) and src[j] in ' \t\r\n':
            j += 1
        if j < len(src) and src[j] == '+':
            i = j + 1
            continue
        return ''.join(parts), i


def scan():
    """{키: [(폴백, 파일:줄), …]}"""
    found = {}
    for path in glob.glob(os.path.join(SCRIPTS, '**', '*.cs'), recursive=True):
        try:
            src = open(path, encoding='utf-8').read()
        except Exception:
            continue
        rel = os.path.relpath(path, PROJECT).replace('\\', '/')
        for m in OPEN.finditer(src):
            i = m.end()
            while i < len(src) and src[i] in ' \t\r\n':
                i += 1
            if i >= len(src) or src[i] != '"':
                continue                                   # 키가 변수다 — 못 푼다
            key, i = read_cs_string(src, i)
            while i < len(src) and src[i] in ' \t\r\n':
                i += 1
            if i >= len(src) or src[i] != ',':
                continue                                   # 폴백이 없다
            fb, _ = read_concat(src, i + 1)
            if fb is None:
                continue                                   # 폴백이 변수다
            line = src.count('\n', 0, m.start()) + 1
            found.setdefault(key, []).append((fb, '%s:%d' % (rel, line)))
    return found


def table_keys():
    keys = set()
    if os.path.isfile(TSV):
        with open(TSV, encoding='utf-8') as f:
            for line in f:
                if line.strip():
                    keys.add(line.split('\t')[0].strip())
    return keys


def main():
    only_missing = '--missing' in sys.argv
    as_py = '--py' in sys.argv

    found = scan()
    have = table_keys()

    # ⚠ 같은 키에 폴백이 여러 가지 — 그 자체가 버그다
    conflicts = {k: v for k, v in found.items()
                 if len({fb for fb, _ in v}) > 1}
    if conflicts:
        print('⚠⚠ 같은 키인데 폴백이 다릅니다 — 한쪽을 고치고 다른 쪽을 잊은 자리입니다:')
        for k, v in sorted(conflicts.items()):
            print('   %s' % k)
            for fb, where in v:
                print('      %-60r %s' % (fb, where))
        print()

    rows = []
    for key in sorted(found):
        if only_missing and key in have:
            continue
        fb, where = found[key][0]
        rows.append((key, fb, where))

    if as_py:
        print('#: 코드에서 그대로 읽어낸 폴백 — Tools/extract_string_fallbacks.py --missing --py')
        for key, fb, where in rows:
            mark = '' if HANGUL.search(fb) else '   # ⚠ 한글이 아니다 — 확인할 것'
            print('    (%r, %r, EN, %r),%s' % (key, fb, where, mark))
    else:
        print('코드가 부르는 키 %d개 · 표에 있는 키 %d개' % (len(found), len(have)))
        print('%s %d개' % ('표에 없는 키' if only_missing else '전체', len(rows)))
        print('-' * 78)
        for key, fb, where in rows:
            print('%-40s %-46r %s' % (key, fb[:44], where))

    if conflicts:
        sys.exit(1)


if __name__ == '__main__':
    main()
