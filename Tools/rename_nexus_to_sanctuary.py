# -*- coding: utf-8 -*-
"""「넥서스」라는 낱말을 <b>「성역」</b>으로 바꾼다 (2026-08-25).

> 유저 지시: *"모든 넥서스 라는 말을 성역으로 바꿔줘"*

★★★ <b>«말» 만 바꾼다 — 이름(식별자)은 건드리지 않는다</b>
────────────────────────────────────────────────
`Nexus` · `NexusAnimator` · `UnitKind.Nexus` · `DefeatReason.NexusDestroyed` ·
씬 오브젝트 이름 · 파일 이름은 <b>그대로 둔다</b>. 유저가 말한 것은 <b>글자로 읽히는 낱말</b>이고,
식별자를 바꾸는 것은 씬 참조와 .meta guid 가 걸린 <b>다른 종류의 일</b>이다.
  ★ 다행히 <b>저절로 안전하다</b> — 식별자는 전부 ASCII 라 «넥서스» 를 찾는 치환에 걸릴 수 없다.

⚠⚠ <b>그대로 바꾸면 깨지는 자리가 셋 있다</b> — 아래 EXCLUDE·SPECIAL 이 그것이다.
  ① 도움말 표 「읽기」 시트 — 「넥서스」를 <b>«쓰지 말라»는 예시</b>로 인용하고 있다.
     바꾸면 규칙이 「성역」→「중앙」이 되어 <b>스스로를 부정한다</b>.
  ② 유물·이벤트 표 「Info」 — 은유 축이 <b>«성역 = 신체 · 넥서스 = 심장»</b> 이다.
     그대로 바꾸면 «성역 = 신체 · 성역 = 심장» 이 되어 뜻이 무너진다.
  ③ `NexusSanctuary` 의 로그 — 이미 «[성역]» 이 머리표라 «[성역] 성역 둘레» 가 된다.

사용법:  py -3 Tools/rename_nexus_to_sanctuary.py          (무엇이 바뀔지만 본다)
         py -3 Tools/rename_nexus_to_sanctuary.py --write  (실제로 쓴다)
다음:    py -3 Tools/gen_string_table.py
         py -3 Tools/link_string_keys.py
         py -3 Tools/gen_event_assets.py
         py -3 Tools/gen_relic_assets.py  →  py -3 Tools/relic_icon_build.py
"""

import io
import os
import shutil
import sys

import openpyxl

from vault_path import TABLE_DIR, PROJECT

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

OLD, NEW = "넥서스", "성역"
WRITE = "--write" in sys.argv

SCRIPTS = os.path.join(PROJECT, "Assets", "_Project", "Scripts")

#: ⚠ 위 ③ — 먼저 갈아 끼우고 나서 일반 치환을 돌린다(그러면 남는 «넥서스» 가 없다).
SPECIAL = [
    ('Debug.Log($"[성역] 넥서스 둘레 ', 'Debug.Log($"[성역] 둘레 '),
]


def convert(text):
    for a, b in SPECIAL:
        text = text.replace(a, b)
    return text.replace(OLD, NEW)


# ══════════════════════════════════════════════════════════════════════
#  ① 코드 (.cs) — 리터럴 · Tooltip · 주석 전부
# ══════════════════════════════════════════════════════════════════════
def do_scripts():
    hit = 0
    files = 0
    for base, _, names in os.walk(SCRIPTS):
        for fn in names:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(base, fn)
            src = io.open(p, encoding="utf-8").read()
            if OLD not in src:
                continue
            out = convert(src)
            n = src.count(OLD)
            hit += n
            files += 1
            if WRITE:
                io.open(p, "w", encoding="utf-8", newline="\n").write(out)
    print(f"① 코드  — {files}개 파일 · {hit}군데")
    return hit


# ══════════════════════════════════════════════════════════════════════
#  ② 볼트의 표 — 시트·셀 단위로 <b>예외를 두고</b> 바꾼다
# ══════════════════════════════════════════════════════════════════════
#: (파일, 시트) — 이 시트는 통째로 건드리지 않는다. ⚠ 위 ①의 이유.
EXCLUDE_SHEETS = {
    ("Last_Sanctuary_도움말테이블_Ver01.xlsx", "읽기"),
}

#: 셀 하나를 <b>손으로 다시 쓴다</b>. ⚠ 위 ②의 이유 — 은유 축이 겹친다.
REWRITE_CELLS = {
    ("Last_Sanctuary_유물테이블_Ver02.xlsx", "Info", "B7"):
        ("성역 = 신체 · 넥서스 = 심장", "성역 = 신체 · 심장부 = 심장"),
    ("Last_Sanctuary_이벤트테이블_Ver013.xlsx", "Info", "B57"):
        ("성역 = 신체 / 넥서스 = 심장", "성역 = 신체 / 심장부 = 심장"),
    # 도움말 Help 시트의 «비고(제목)» 메모 — 제목이 이미 「성역 수호」로 바뀌었다(155절).
    ("Last_Sanctuary_도움말테이블_Ver01.xlsx", "Help", "L2"):
        ("성역과 넥서스", "성역 수호"),
}


def do_tables():
    total = 0
    for fn in sorted(os.listdir(TABLE_DIR)):
        if not fn.endswith(".xlsx") or fn.startswith("~$"):
            continue
        path = os.path.join(TABLE_DIR, fn)
        try:
            wb = openpyxl.load_workbook(path)
        except Exception as e:
            print(f"   ! {fn}: {e}")
            continue

        changed = 0
        for ws in wb.worksheets:
            if (fn, ws.title) in EXCLUDE_SHEETS:
                continue
            for row in ws.iter_rows():
                for cell in row:
                    v = cell.value
                    if not isinstance(v, str) or OLD not in v:
                        continue

                    key = (fn, ws.title, cell.coordinate)
                    if key in REWRITE_CELLS:
                        a, b = REWRITE_CELLS[key]
                        if a not in v:
                            print(f"   ⚠ {fn}[{ws.title}]{cell.coordinate} — 손으로 쓸 조각을 "
                                  f"찾지 못했습니다: «{a}»")
                            continue
                        new = v.replace(a, b)
                        # 그 조각을 갈아 낀 뒤에도 남은 «넥서스» 는 일반 치환으로 처리한다
                        new = convert(new)
                    else:
                        new = convert(v)

                    if new != v:
                        if WRITE:
                            cell.value = new
                        changed += 1

        if changed:
            total += changed
            print(f"   {fn}: {changed}칸")
            if WRITE:
                shutil.copy2(path, path + ".bak")
                try:
                    wb.save(path)
                except PermissionError:
                    raise SystemExit(f"[실패] 엑셀에서 열려 있습니다 — 닫고 다시 돌리십시오:\n  {path}")
    print(f"② 표    — {total}칸")
    return total


def main():
    print(f"「{OLD}」 → 「{NEW}」" + ("" if WRITE else "   (미리보기 — 쓰려면 --write)"))
    print()
    a = do_scripts()
    b = do_tables()
    print(f"\n합계 {a + b}군데")
    if not WRITE:
        print("\n⚠ 아직 아무것도 쓰지 않았습니다. --write 를 붙여 다시 돌리십시오.")
    else:
        print("\n다음: gen_string_table → link_string_keys → gen_event_assets → "
              "gen_relic_assets → relic_icon_build → 유니티 Assets/Refresh")


if __name__ == "__main__":
    main()
