# -*- coding: utf-8 -*-
"""스킨 에셋의 **몸집 보정**(`bodyScaleAdjust`) 칸을 채운다 (2026-08-21).

유저 지시: *"카이론, 아루, 불칸 3명 캐릭터를 프레이아를 표준으로 크기 맞춰줘"*.

**무엇이 어긋났나** — 캐릭터는 전원 `renderHeightTiles` **2.15** 로 그려진다. 즉
**상자(원화의 알파 경계)** 높이는 이미 전원 같다. 그런데 상자 안에 든 것이 캐릭터마다
다르다 — 아루는 머리 위 **후광**, 불칸은 **지팡이**가 상자를 위로 늘린다. 그래서 상자가
같아도 **「사람 키」는 다르다.** 실측(대기 원화 · 중앙값):

    캐릭터        사람px / 상자px      사람 키(타일)    프레이야 대비
    프레이야       109 / 114 = 0.96      1.921          기준
    카이론          83 /  90 = 0.92      1.879          -2.2%
    아루           107 / 122 = 0.87      1.855          -3.4%
    불칸            78 /  95 = 0.82      1.747          -9.0%   ← 지팡이가 상자를 늘린다

**어떻게 재나** — 행마다 알파 픽셀 수를 세어, 가장 두꺼운 행의 25% 이상인 행만
「사람의 몸」으로 보고 그 **연속 구간**을 잡는다. 후광·지팡이 끝·검 끝은 얇아서 빠지고,
날개는 두꺼워도 **몸통과 같은 높이**라 세로 구간을 늘리지 않는다.

★ **왜 `contentSizeTiles` 를 안 고치나** — 그 칸은 «측정값이라 항상 덮어쓴다»
  (`measure_skin_tiles.py`). 거기에 사람의 판단을 적으면 다음에 그 스크립트를 돌릴 때
  **조용히 원복된다**. 그래서 이 프로젝트가 이미 갖고 있는 구분
  («실측값» vs «표시 크기 — 사람이 정하는 값») 에 맞춰 표시 쪽에 칸을 하나 뒀다.

⚠ **판정도 같이 움직인다** — `CharacterAnimator.ColliderSizeTiles` 가 «그려진 크기» 를
  돌려주므로 근접 거리·선택 판정이 따라 커진다. 그것이 이 프로젝트의 규칙이다
  («보이는 몸집 = 판정 몸집»).

⚠ .asset YAML 에 **빈 줄을 넣지 않는다** — Unity 파서가 그 뒤 필드를 전부 무시한다
  (진행상황 8절 3번).
⚠ MCP 에는 SO 에셋을 다루는 도구가 없다 — 그래서 이 종류만 스크립트로 쓴다(59-2절).
⚠ **몇 번을 돌려도 결과가 같다**(멱등). 이미 그 값이면 «변경 없음» 으로 지나간다.

사용법:  python Tools/set_body_scale_adjust.py
"""

import io
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
SKINS = os.path.join(PROJECT, "Assets", "_Project", "Resources", "Skins")

#: 채울 값 — 프레이야의 «사람 키»(1.921타일) 를 1.00 으로 본 역수다.
#:   보정 = 프레이야 사람키 / 그 캐릭터의 사람키
#: 값을 바꾸고 싶으면 **인스펙터에서** 바로 만지면 된다(그게 칸을 만든 이유다).
#: 여기 적힌 값은 «처음 한 번 넣는 값» 이다.
TARGETS = {
    "Skin_Chiron": 1.022,      # 카이론  1.879 → 1.921
    "Skin_Aru":    1.035,      # 아루    1.855 → 1.921
    "Skin_Vulcan": 1.099,      # 불칸    1.747 → 1.921
}

#: 이 필드 **바로 뒤**에 넣는다 — C# 의 선언 순서와 같게 두면 사람이 읽기 쉽다.
ANCHOR = "impactFlattenY"

FIELD = "bodyScaleAdjust"


def patch(path, value):
    """한 에셋에 `bodyScaleAdjust` 를 넣거나 고친다. (바뀌었는가, 메시지)"""
    with io.open(path, encoding="utf-8", newline="") as f:
        raw = f.read()

    crlf = "\r\n" in raw
    lines = raw.replace("\r\n", "\n").split("\n")

    want = "  %s: %s" % (FIELD, ("%.4f" % value).rstrip("0").rstrip("."))

    # ── 이미 있으면 값만 맞춘다 (멱등) ──────────────────────────────────
    for i, line in enumerate(lines):
        if re.match(r"\s*%s:" % FIELD, line):
            if line == want:
                return False, "이미 %s — 변경 없음" % want.strip()
            old = line.strip()
            lines[i] = want
            changed = True
            msg = "%s → %s" % (old, want.strip())
            break
    else:
        # ── 없으면 앵커 바로 뒤에 끼운다 ────────────────────────────────
        for i, line in enumerate(lines):
            if re.match(r"\s*%s:" % ANCHOR, line):
                lines.insert(i + 1, want)
                changed = True
                msg = "추가 %s" % want.strip()
                break
        else:
            return False, "⚠ %s 를 찾지 못해 건너뜀 — 손으로 확인하세요" % ANCHOR

    if not changed:
        return False, "변경 없음"

    text = "\n".join(lines)
    if crlf:
        text = text.replace("\n", "\r\n")
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    return True, msg


def main():
    print("[스킨 몸집 보정 — 프레이야 기준]")
    if not os.path.isdir(SKINS):
        raise SystemExit("⚠ 스킨 폴더를 찾지 못했습니다: %s" % SKINS)

    n = 0
    for name in sorted(TARGETS):
        path = os.path.join(SKINS, name + ".asset")
        if not os.path.isfile(path):
            print("  %-14s ⚠ 파일이 없습니다 — 건너뜀" % name)
            continue
        did, msg = patch(path, TARGETS[name])
        n += 1 if did else 0
        print("  %-14s %s" % (name, msg))

    print("  바뀐 파일 %d개" % n)
    if n:
        print("  ⚠ 유니티가 켜져 있으면 에셋을 다시 불러옵니다(자동) — "
              "인스펙터에서 «몸집 보정» 칸을 확인하세요.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
