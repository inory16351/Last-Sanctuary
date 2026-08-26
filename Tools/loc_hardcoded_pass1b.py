# -*- coding: utf-8 -*-
"""하드코딩 한글 이관 1차-b — <b>직렬화 문구 칸</b>을 쓰는 자리를 키로 잇는다.

1차(`loc_hardcoded_pass1.py`)는 코드 안의 <b>리터럴</b>을 바꿨다. 그런데 창 여럿은
문구를 <c>[SerializeField] string</c> 칸에 두고 쓴다(유물 창·초상화 창·성장 창).
그 칸은 <b>씬에 값이 저장돼</b> 있어서, 리터럴만 바꿔도 씬의 한글이 이긴다.

★ 그래서 <b>쓰는 자리</b>를 고친다 — `Data.StringTable.Get(키, 그 칸)`.
  칸은 그대로 «폴백» 이 된다. 씬에서 문구를 고쳐 둔 사람의 값도 살아남는다.

다음: python Tools/gen_string_table.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

SCRIPTS = os.path.join("Assets", "_Project", "Scripts")
G = "Data.StringTable.Get"

EDITS = {
    "UI/RelicPanel.cs": [
        # ── 안내 줄 세 가지 ──
        ('                _hint.text = _sorted.Count == 0 ? hintEmpty\n'
         '                           : r == null ? hintPick\n'
         '                           : target == null ? hintNoCharacter\n'
         '                           : string.IsNullOrEmpty(slotNote) ? hintPick\n'
         '                           : $"{hintPick}  {slotNote}";',
         f'                string pick = {G}(hintPickKey, hintPick);\n'
         '                _hint.text =\n'
         f'                      _sorted.Count == 0 ? {G}(hintEmptyKey, hintEmpty)\n'
         '                    : r == null ? pick\n'
         f'                    : target == null ? {G}(hintNoCharacterKey, hintNoCharacter)\n'
         '                    : string.IsNullOrEmpty(slotNote) ? pick\n'
         '                    : $"{pick}  {slotNote}";'),

        ('                string slotNote = inv != null && target != null\n'
         '                    ? string.Format(slotFormat, inv.UsedSlots(target), inv.EquipSlots)\n'
         '                    : string.Empty;',
         '                string slotNote = inv != null && target != null\n'
         f'                    ? string.Format({G}("ui_relic_slot_format", slotFormat),\n'
         '                                    inv.UsedSlots(target), inv.EquipSlots)\n'
         '                    : string.Empty;'),

        # ── 개수 · 착용자 ──
        ('                    row.Count.text = n > 1 ? string.Format(countFormat, n) : "";',
         '                    row.Count.text = n > 1\n'
         f'                        ? string.Format({G}("ui_relic_count", countFormat), n) : "";'),
        ('                if (key > 0) wearer = string.Format(wearerFormat, NameOfCharacter(key));',
         '                if (key > 0)\n'
         f'                    wearer = string.Format({G}("ui_relic_wearer", wearerFormat),\n'
         '                                           NameOfCharacter(key));'),
        ('            if (_wearerKeys.Count == 1) return string.Format(rowWearerFormat, first);',
         '            if (_wearerKeys.Count == 1)\n'
         f'                return string.Format({G}("ui_relic_row_wearer", rowWearerFormat), first);'),
        ('            return string.Format(rowWearerMoreFormat, first, _wearerKeys.Count - 1);',
         f'            return string.Format({G}("ui_relic_wearer_more", rowWearerMoreFormat),\n'
         '                                 first, _wearerKeys.Count - 1);'),

        # ── 버튼 문구 ──
        ('                if (_unequipLabelText != null)\n'
         '                    _unequipLabelText.text = alreadyOn ? unequipLabel : unequipOtherLabel;',
         '                if (_unequipLabelText != null)\n'
         f'                    _unequipLabelText.text = {G}("ui_relic_unequip",\n'
         '                                                 alreadyOn ? unequipLabel : unequipOtherLabel);'),
        ('            if (_equipLabelText != null)\n'
         '                _equipLabelText.text = toggleMode && alreadyOn ? unequipLabel : equipLabel;',
         '            if (_equipLabelText != null)\n'
         '                _equipLabelText.text = toggleMode && alreadyOn\n'
         f'                    ? {G}("ui_relic_unequip", unequipLabel)\n'
         f'                    : {G}("ui_relic_equip", equipLabel);'),
    ],
    "UI/CharacterGrowthPanel.cs": [
        ('                        card.Name.text = has ? "빈 칸" : "캐릭터를 선택하세요";',
         '                        card.Name.text = has\n'
         f'                            ? {G}("ui_relic_empty_slot", "빈 칸")\n'
         f'                            : {G}("ui_relic_pick_character", "캐릭터를 선택하세요");'),
        ('                    card.Effect.text = relic != null ? relic.Desc\n'
         '                                     : has ? "눌러서 유물을 끼웁니다."\n'
         '                                     : "";',
         '                    card.Effect.text = relic != null ? relic.Desc\n'
         f'                                     : has ? {G}("ui_relic_empty_hint", "눌러서 유물을 끼웁니다.")\n'
         '                                     : "";'),
    ],
}


def main():
    total = 0
    for rel, pairs in EDITS.items():
        path = os.path.join(SCRIPTS, rel.replace("/", os.sep))
        with open(path, encoding="utf-8-sig", newline="") as f:
            src = f.read()
        crlf = "\r\n" in src
        flat = src.replace("\r\n", "\n")

        hit = 0
        for old, new in pairs:
            if new in flat:
                continue
            if old not in flat:
                sys.exit(f"! 못 찾음 — {rel}\n  찾던 것: {old[:100]}")
            flat = flat.replace(old, new, 1)
            hit += 1

        if hit:
            out = flat.replace("\n", "\r\n") if crlf else flat
            with open(path, "w", encoding="utf-8-sig", newline="") as f:
                f.write(out)
        print(f"  {rel} — {hit}곳")
        total += hit

    print(f"→ {total}곳")
    print("⚠ 새 키 ui_relic_row_wearer 는 1차 표에 없다 — gen_string_table 뒤 "
          "표에 손으로 넣거나 다음 차수에서 넣는다(폴백이 있으므로 화면은 멀쩡하다).")


if __name__ == "__main__":
    main()
