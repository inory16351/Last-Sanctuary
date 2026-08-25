# -*- coding: utf-8 -*-
"""유물 아이콘이 <b>겹치지 않았는지</b> 검사한다 (2026-08-25 신설).

유저 지시: *"유물 아이콘이 중복으로 적용된건 없는건지 확인"*

★ <b>표가 아니라 «만들어진 에셋» 을 본다.</b> `assign_relic_icons.py` 도 중복을 검사하지만
  그것은 «내가 적으려는 값» 을 보는 것이고, 실제로 게임이 읽는 것은 에셋의
  `iconKey` / `icon`(guid) 다. 중간에 `gen_relic_assets.py` 가 무엇을 했는지까지 포함해
  <b>결과</b>를 확인해야 «확인했다» 가 된다.

검사하는 것 넷 —
  ① 같은 `iconKey` 를 두 유물이 쓰지 않는가
  ② 같은 `icon`(guid) 을 두 유물이 쓰지 않는가   ← 키가 달라도 파일이 같을 수 있다
  ③ `iconKey` 가 빈 유물이 없는가
  ④ 그 키의 PNG 가 실제로 있는가

사용법:  python Tools/check_relic_icons.py
"""
import glob
import io
import os
import re
import sys
from collections import defaultdict

from vault_path import PROJECT

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

RELIC_DIR = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'Relics')
ICON_DIR = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'RelicIcons')


def field(text, key):
    m = re.search(r'^\s*%s: (.*)$' % re.escape(key), text, re.M)
    return m.group(1).strip() if m else ''


def main():
    by_key = defaultdict(list)
    by_guid = defaultdict(list)
    blank = []
    no_file = []
    rows = []

    for path in sorted(glob.glob(os.path.join(RELIC_DIR, 'Relic_*.asset'))):
        text = io.open(path, encoding='utf-8', errors='ignore').read()
        rid = field(text, 'relicId')
        name = field(text, 'relicName').strip('"')
        key = field(text, 'iconKey').strip('"')
        icon = field(text, 'icon')

        guid = ''
        m = re.search(r'guid: ([0-9a-f]{32})', icon)
        if m:
            guid = m.group(1)

        rows.append((rid, name, key, guid))

        if not key:
            blank.append('%s %s' % (rid, name))
            continue

        by_key[key].append('%s %s' % (rid, name))
        if guid:
            by_guid[guid].append('%s %s' % (rid, name))

        if not os.path.exists(os.path.join(ICON_DIR, key + '.png')):
            no_file.append('%s %s → %s.png' % (rid, name, key))

    print('유물 %d종' % len(rows))

    bad = False

    dup_key = {k: v for k, v in by_key.items() if len(v) > 1}
    if dup_key:
        bad = True
        print('\n★ 같은 iconKey 를 쓰는 유물 %d쌍:' % len(dup_key))
        for k, v in sorted(dup_key.items()):
            print('  %-16s ← %s' % (k, ' / '.join(v)))
    else:
        print('  ① iconKey 중복 없음')

    dup_guid = {k: v for k, v in by_guid.items() if len(v) > 1}
    if dup_guid:
        bad = True
        print('\n★ 같은 그림 파일(guid)을 쓰는 유물 %d쌍:' % len(dup_guid))
        for k, v in sorted(dup_guid.items()):
            print('  %s ← %s' % (k[:12], ' / '.join(v)))
    else:
        print('  ② 그림 파일 중복 없음')

    if blank:
        bad = True
        print('\n★ iconKey 가 빈 유물 %d종:\n  %s' % (len(blank), '\n  '.join(blank)))
    else:
        print('  ③ 빈 iconKey 없음')

    if no_file:
        bad = True
        print('\n★ 그림 파일이 없는 유물 %d종:\n  %s' % (len(no_file), '\n  '.join(no_file)))
    else:
        print('  ④ 모든 키에 PNG 가 있음')

    print('\n%s' % ('⚠ 문제가 있습니다 — 위를 볼 것' if bad else '✅ 이상 없음'))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
