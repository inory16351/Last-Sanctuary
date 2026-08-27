# -*- coding: utf-8 -*-
"""★ <b>코드가 부르는 스트링 키가 표에 다 있는가</b> — 한 줄로 세는 검산 (2026-08-27 신설).

■ 왜 스크립트가 됐나
  이 검산은 176절부터 <b>절마다 손으로</b> 돌리고 있었다(«코드가 부르는 키 231개 전부 표에
  있고 영어가 비어 있지 않다»). 매번 다시 짜면 <b>기준이 조금씩 달라진다</b> — 실제로
  178절의 검산 도구가 런타임과 기준이 달라 <b>죽은 칸 일곱을 못 잡은</b> 일이 있다(182-2절).
  그래서 «세는 법» 을 한 곳에 둔다(`vault_path.py` 가 경로 규칙을 한 곳에 둔 것과 같은 이유).

■ 무엇을 세나
  ① `Assets/_Project/Scripts` 전체에서 <b>코드가 부르는 키</b>를 뽑는다
     (`HudTheme.T("…")` · `StringTable.Get("…")` · `StringTable.Has("…")` · `Format`/`Replace`).
  ② 그 키가 <b>내보낸 표</b>(`Resources/Data/StringTable.txt`)에 있는지 본다.
  ③ 있으면 <b>영어 칸이 비어 있지 않은지</b> 본다.
  ④ 표 전체의 <b>영어 빈칸</b>도 함께 센다(한국어도 빈 «자리표» 는 갈라서 보고한다).

■ ⚠ 왜 이 검산이 중요한가 — `HudTheme.T` 의 ⚠⚠ 절과 짝이다
  창 열넷이 언어 전환 때 `LocalizeLabels()` 를 <b>다시 부른다</b>(2026-08-27). 그 관용구
  (`field = T(key, field)`)는 <b>키가 표에 있을 때만</b> 여러 번 불려도 안전하다. 즉
  «키가 표에 있는가» 가 단순한 위생이 아니라 <b>동작의 전제</b>가 됐다.

■ 쓰는 법
    py -3 Tools/check_string_keys.py          # 사람이 읽는 보고
    py -3 Tools/check_string_keys.py --strict # 빠진 키가 하나라도 있으면 exit 1

⚠ 이 스크립트는 <b>내보낸 TSV</b>를 본다 — 즉 `gen_string_table.py` 를 <b>돌린 뒤에</b> 세야
  한다. xlsx 만 고치고 이걸 돌리면 «아직 안 구운» 키가 «없는 키» 로 잡힌다.
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

#: 키를 <b>문자열 리터럴로</b> 넘기는 자리만 잡는다.
#  ⚠ 변수로 넘기는 자리(`HudTheme.T(hintKey, …)`)는 <b>정적으로는 알 수 없다</b> —
#    아래 `INDIRECT` 가 그런 자리를 따로 세어 보고한다(놓친 것을 «없다» 고 말하지 않기 위해).
CALL = re.compile(
    r'(?:HudTheme\.T|StringTable\.Get|StringTable\.Has|StringTable\.Format|StringTable\.Replace)'
    r'\s*\(\s*"([a-zA-Z0-9_]+)"')

#: 같은 함수인데 <b>첫 인자가 리터럴이 아닌</b> 자리.
INDIRECT = re.compile(
    r'(?:HudTheme\.T|StringTable\.Get|StringTable\.Has|StringTable\.Format|StringTable\.Replace)'
    r'\s*\(\s*(?!")')

#: 키 «접두사» 를 코드가 이어 붙여 쓰는 자리는 세지 않는다(정적으로 못 푼다).
KNOWN_PREFIX_ONLY = {'character_altname_'}


def load_table():
    """내보낸 TSV → {key: (kr, en)}."""
    table = {}
    if not os.path.isfile(TSV):
        sys.exit('✗ 표를 찾지 못했습니다: %s\n  먼저 py -3 Tools/gen_string_table.py 를 돌리세요.' % TSV)
    with open(TSV, encoding='utf-8') as f:
        for line in f:
            line = line.rstrip('\r\n')
            if not line:
                continue
            parts = line.split('\t')
            if len(parts) < 2:
                continue
            key = parts[0].strip()
            kr = parts[1] if len(parts) > 1 else ''
            en = parts[2] if len(parts) > 2 else ''
            table[key] = (kr, en)
    return table


def strip_comments(src):
    """주석을 <b>같은 길이의 공백</b>으로 지운다(줄 번호가 안 밀리게).

    ⚠ <b>왜 필요한가</b> — 2026-08-27 에 이 검산이 `ui_x_title` 을 «표에 없는 키» 로 잡았다.
      그것은 <c>HudTheme.T</c> 의 <b>설명 주석에 적어 둔 예시 코드</b>였다. 주석 속 예시가
      «코드가 부르는 키» 로 세어지면 <b>고칠 수 없는 실패</b>가 영영 남는다 —
      그 자리를 고치려면 설명을 지워야 하기 때문이다.
    """
    out = list(src)
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        # 문자열은 건너뛴다(그 안의 «//» 는 주석이 아니다)
        if c == '"':
            if i and src[i - 1] == '@':
                i += 1
                while i < n:
                    if src[i] == '"':
                        if i + 1 < n and src[i + 1] == '"':
                            i += 2
                            continue
                        break
                    i += 1
                i += 1
                continue
            i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == '"':
                    break
                i += 1
            i += 1
            continue
        if c == '/' and i + 1 < n and src[i + 1] == '/':
            while i < n and src[i] != '\n':
                out[i] = ' '
                i += 1
            continue
        if c == '/' and i + 1 < n and src[i + 1] == '*':
            while i < n and not (src[i] == '*' and i + 1 < n and src[i + 1] == '/'):
                if src[i] != '\n':
                    out[i] = ' '
                i += 1
            for j in range(i, min(i + 2, n)):
                out[j] = ' '
            i += 2
            continue
        i += 1
    return ''.join(out)


def scan_code():
    """코드가 부르는 키 → {키: {파일…}} · 간접 호출 수."""
    used, indirect = {}, []
    for path in glob.glob(os.path.join(SCRIPTS, '**', '*.cs'), recursive=True):
        try:
            src = strip_comments(open(path, encoding='utf-8').read())
        except Exception:
            continue
        rel = os.path.relpath(path, PROJECT)
        for m in CALL.finditer(src):
            used.setdefault(m.group(1), set()).add(rel)
        n = len(INDIRECT.findall(src))
        if n:
            indirect.append((rel, n))
    return used, indirect


def main():
    strict = '--strict' in sys.argv

    table = load_table()
    used, indirect = scan_code()

    missing, no_en = [], []
    for key in sorted(used):
        if key in KNOWN_PREFIX_ONLY:
            continue
        if key not in table:
            missing.append(key)
        elif not table[key][1].strip():
            no_en.append(key)

    # 표 전체의 영어 빈칸 — «한국어도 빈 자리표» 와 «진짜 미번역» 을 가른다
    blank_both = [k for k, (kr, en) in table.items() if not en.strip() and not kr.strip()]
    blank_en_only = [k for k, (kr, en) in table.items() if not en.strip() and kr.strip()]

    print('표 %d키 · 코드가 부르는 키 %d개' % (len(table), len(used)))
    print('-' * 68)

    print('표에 없는 키 : %d개' % len(missing))
    for k in missing:
        print('   ✗ %-42s %s' % (k, ', '.join(sorted(used[k]))))

    print('영어가 빈 키 : %d개  (코드가 실제로 부르는 것 중)' % len(no_en))
    for k in no_en:
        print('   ✗ %-42s kr=%r' % (k, table[k][0][:40]))

    print('-' * 68)
    print('표 전체 영어 빈칸 : %d개' % (len(blank_both) + len(blank_en_only)))
    if blank_en_only:
        print('   ⚠ 한국어는 있는데 영어가 없다 — <b>진짜 미번역</b> %d개' % len(blank_en_only))
        for k in blank_en_only:
            print('      %-42s kr=%r' % (k, table[k][0][:40]))
    if blank_both:
        print('   · 한국어도 비어 있다 — 내용 자체가 없는 <b>자리표</b> %d개 (번역 문제가 아니다)'
              % len(blank_both))
        for k in sorted(blank_both):
            print('      %s' % k)

    if indirect:
        total = sum(n for _, n in indirect)
        print('-' * 68)
        print('⚠ 키를 <변수>로 넘기는 자리 %d곳 — 이 검산이 못 보는 자리다:' % total)
        for rel, n in sorted(indirect, key=lambda x: -x[1])[:10]:
            print('   %3d  %s' % (n, rel))

    print('-' * 68)
    ok = not missing and not no_en
    print('✓ 통과 — 코드가 부르는 키가 전부 표에 있고 영어가 채워져 있습니다.' if ok
          else '✗ 실패 — 위의 키를 표에 넣고 gen_string_table.py 를 다시 돌리세요.')

    if strict and not ok:
        sys.exit(1)


if __name__ == '__main__':
    main()
