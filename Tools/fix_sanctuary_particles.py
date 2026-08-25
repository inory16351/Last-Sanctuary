# -*- coding: utf-8 -*-
"""「넥서스」→「성역」 치환이 깨뜨린 <b>조사</b>를 바로잡는다 (2026-08-25).

★★★ <b>왜 이 스크립트가 따로 필요한가</b>
──────────────────────────────────
한국어 조사는 <b>앞 낱말의 받침</b>을 따라 달라진다.

  · <b>넥서스</b> — 「스」에 받침이 없다  →  <b>가 · 를 · 는 · 와 · 로</b>
  · <b>성역</b>   — 「역」에 받침(ㄱ)이 있다 →  <b>이 · 을 · 은 · 과 · 으로</b>

그래서 낱말만 갈아 끼우면 <b>「성역가 파괴되면」</b> 같은 문장이 남는다. 실제로 그렇게 됐다.
(`rename_nexus_to_sanctuary.py` 를 돌린 <b>직후에</b> 이것을 돌려야 한다.)

⚠ <b>«성역으로» 를 두 번 고치지 않는다</b> — 찾는 것이 «성역로» 이고, 이미 올바른
  «성역으로» 안에는 그 글자가 <b>없다</b>(으가 사이에 있다). 그래서 여러 번 돌려도 안전하다.

사용법:  py -3 Tools/fix_sanctuary_particles.py           (무엇이 바뀔지만 본다)
         py -3 Tools/fix_sanctuary_particles.py --write   (실제로 쓴다)
"""

import io
import os
import re
import shutil
import sys

import openpyxl

from vault_path import TABLE_DIR, PROJECT

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

WRITE = "--write" in sys.argv
SCRIPTS = os.path.join(PROJECT, "Assets", "_Project", "Scripts")

#: 받침 없는 말 뒤의 조사 → 받침 있는 말 뒤의 조사.
#: ⚠ 순서가 중요하다 — «로» 를 «으로» 로 고치기 <b>전에</b> 다른 것을 먼저 본다.
FIX = [
    ("성역가", "성역이"),
    ("성역를", "성역을"),
    ("성역는", "성역은"),
    ("성역와", "성역과"),
    ("성역라", "성역이라"),
    ("성역로", "성역으로"),
]


def convert(text):
    for a, b in FIX:
        text = text.replace(a, b)
    return text


def report(text, where, out):
    """무엇이 걸렸는지 한 줄씩 모은다 — 조용히 고치지 않는다."""
    for a, b in FIX:
        for m in re.finditer(re.escape(a), text):
            s = max(0, m.start() - 24)
            e = min(len(text), m.end() + 24)
            snippet = text[s:e].replace("\n", " ⏎ ")
            out.append((where, a, b, snippet))


def do_scripts():
    hits, files = [], 0
    for base, _, names in os.walk(SCRIPTS):
        for fn in names:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(base, fn)
            src = io.open(p, encoding="utf-8").read()
            out = convert(src)
            if out == src:
                continue
            files += 1
            report(src, os.path.relpath(p, PROJECT).replace("\\", "/"), hits)
            if WRITE:
                io.open(p, "w", encoding="utf-8", newline="\n").write(out)
    return files, hits


def do_tables():
    hits, total = [], 0
    for fn in sorted(os.listdir(TABLE_DIR)):
        if not fn.endswith(".xlsx") or fn.startswith("~$"):
            continue
        path = os.path.join(TABLE_DIR, fn)
        try:
            wb = openpyxl.load_workbook(path)
        except Exception:
            continue

        changed = 0
        for ws in wb.worksheets:
            for row in ws.iter_rows():
                for cell in row:
                    v = cell.value
                    if not isinstance(v, str):
                        continue
                    new = convert(v)
                    if new == v:
                        continue
                    report(v, f"{fn}[{ws.title}]{cell.coordinate}", hits)
                    changed += 1
                    if WRITE:
                        cell.value = new

        if changed:
            total += changed
            if WRITE:
                shutil.copy2(path, path + ".bak")
                try:
                    wb.save(path)
                except PermissionError:
                    raise SystemExit(f"[실패] 엑셀에서 열려 있습니다:\n  {path}")
    return total, hits


def main():
    print("조사 바로잡기" + ("" if WRITE else "   (미리보기 — 쓰려면 --write)") + "\n")

    files, h1 = do_scripts()
    cells, h2 = do_tables()
    hits = h1 + h2

    for where, a, b, snippet in hits:
        print(f"  «{a}» → «{b}»   {where}")
        print(f"      …{snippet}…")

    print(f"\n코드 {files}개 파일 · 표 {cells}칸 · 고친 조사 {len(hits)}군데")
    if not WRITE:
        print("\n⚠ 아직 아무것도 쓰지 않았습니다. --write 를 붙여 다시 돌리십시오.")


if __name__ == "__main__":
    main()
