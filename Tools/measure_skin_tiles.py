# -*- coding: utf-8 -*-
"""스킨 에셋의 **실측 크기(타일)** 를 채운다 (2026-08-13).

**왜 필요한가** — 이 프로젝트의 크기 기준은 **타일**이다(유저 확정 2026-08-13).
정의 테이블에는 "몇 타일로 보일지"만 적고, 배율은 코드가

    배율 = 목표 세로(타일) ÷ 스킨 실측 세로(타일)

로 계산한다. 그 **실측 세로**를 여기서 잰다.

**왜 런타임에 재지 않는가** — 유니티의 `Sprite.bounds` 는 **캔버스(rect)** 기준이라
여백까지 포함한다. 이 프로젝트의 원화는 팩마다 여백이 제각각이다(엘린 캔버스 189px 에
실제 그림 130px · 피올로 64px 에 52px). 캔버스로 재면 그림이 작은 원화가 크게 잡혀
크기가 뒤죽박죽이 된다. **알파 경계**로 재야 정확한데, 런타임에서 텍스처 픽셀을 읽으려면
`isReadable` 을 켜야 해서 메모리를 두 배로 쓴다. 그래서 **에셋을 만들 때 한 번 재서 적어둔다.**

**무엇을 쓰는가** (`CharacterSkinSO` / `TowerSkinSO`)
  · `contentSizeTiles`     대기(Idle) 원화의 실제 크기 — 유닛 몸집 계산의 기준
  · `projectileSizeTiles`  탄환 원화의 실제 크기
  · `impactSizeTiles`      착탄 원화의 실제 크기
  이 셋은 **측정값이라 항상 덮어쓴다.**

  · `projectileWidthTiles` / `impactWidthTiles` / `impactFlattenY`
  이 셋은 **표시 크기(사람이 정하는 값)** 라 **없을 때만** 채운다 — 구식 배율
  (`projectileScale` / `impactScale`)로 지금 보이는 크기를 그대로 타일로 환산해 넣는다.
  즉 이 스크립트를 처음 돌려도 **화면에서 보이던 크기가 바뀌지 않는다.**

⚠ .asset YAML 에 빈 줄을 넣으면 Unity 파서가 그 뒤 필드를 전부 무시한다(진행상황 8절 3번).
⚠ MCP 에는 SO 에셋을 다루는 도구가 없다 — 그래서 이 종류만 스크립트로 쓴다(59-2절).
⚠ 몇 번을 돌려도 결과가 같다(멱등). 측정값만 다시 쓰고 사람이 정한 값은 손대지 않는다.

사용법:  python Tools/measure_skin_tiles.py
"""

import os
import re
import sys

from PIL import Image

# 콘솔이 cp949 라 한글·기호 출력에서 죽는다 — 출력만 UTF-8 로 바꾼다(파일 내용과 무관).
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(PROJECT, "Assets")
RES = os.path.join(ASSETS, "_Project", "Resources")

# 스킨 에셋이 있는 곳 — 캐릭터 / 몬스터 / 건물
SKIN_ROOTS = [
    os.path.join(RES, "Skins"),
    os.path.join(RES, "MonsterSkins"),
    os.path.join(RES, "BuildingSkins"),
]

# 몸집 기준이 되는 모션. 대기(Idle)가 그 유닛의 "서 있는 크기"다.
# TowerSkinSO 는 방향이 없어 필드 이름이 `idle` 하나다.
IDLE_KEYS = ("idleRight", "idleLeft", "idle")


# ---------------------------------------------------------------------------
# guid → PNG 경로 색인
# ---------------------------------------------------------------------------

def build_guid_index():
    """Assets 아래 모든 .png.meta 를 훑어 guid → png 경로 표를 만든다."""
    index = {}
    for root, _dirs, files in os.walk(ASSETS):
        for f in files:
            if not f.endswith(".png.meta"):
                continue
            meta = os.path.join(root, f)
            with open(meta, encoding="utf-8", errors="ignore") as fp:
                for line in fp:
                    if line.startswith("guid:"):
                        index[line.split(":", 1)[1].strip()] = meta[:-5]
                        break
    return index


def ppu_of(png_path):
    """이 PNG 의 Pixels Per Unit. .meta 에 없으면 유니티 기본값 100."""
    meta = png_path + ".meta"
    if not os.path.isfile(meta):
        return 100.0
    with open(meta, encoding="utf-8", errors="ignore") as f:
        m = re.search(r"spritePixelsToUnits:\s*([0-9.]+)", f.read())
    return float(m.group(1)) if m else 100.0


def content_tiles(png_path):
    """알파 경계로 잰 실제 그림 크기(타일). 빈 그림이면 None."""
    with Image.open(png_path) as im:
        box = im.convert("RGBA").getbbox()
    if not box:
        return None
    ppu = ppu_of(png_path)
    if ppu <= 0:
        return None
    return ((box[2] - box[0]) / ppu, (box[3] - box[1]) / ppu)


# ---------------------------------------------------------------------------
# .asset 파싱 / 패치
# ---------------------------------------------------------------------------

def frame_guids(lines, key):
    """`key:` 아래에 나열된 스프라이트 참조의 guid 목록. 빈 배열(`key: []`)이면 빈 목록."""
    out = []
    inside = False
    for line in lines:
        if inside:
            m = re.match(r"\s*-\s*\{fileID: \d+, guid: ([0-9a-f]+),", line)
            if m:
                out.append(m.group(1))
                continue
            inside = False
        if re.match(r"\s*%s:\s*$" % re.escape(key), line):
            inside = True
    return out


def measure_frames(lines, keys, guid_index):
    """여러 모션 키의 프레임을 한꺼번에 재서 **가장 큰** 가로·세로(타일)를 돌려준다.

    가장 큰 값을 쓰는 이유: 한 모션 안에서도 프레임마다 팔·자락이 튀어나와 크기가
    조금씩 다르다. 평균을 쓰면 프레임이 바뀔 때마다 기준이 흔들리는 것처럼 보이고,
    최댓값을 쓰면 **그 유닛이 차지하는 최대 크기**가 되어 발판 판정과도 맞는다.
    """
    w = h = 0.0
    for key in keys:
        for guid in frame_guids(lines, key):
            png = guid_index.get(guid)
            if not png or not os.path.isfile(png):
                continue
            size = content_tiles(png)
            if size is None:
                continue
            w = max(w, size[0])
            h = max(h, size[1])
    return (round(w, 3), round(h, 3)) if w > 0 and h > 0 else None


def read_float(lines, key, default=0.0):
    for line in lines:
        m = re.match(r"\s*%s:\s*(-?[0-9.]+)\s*$" % re.escape(key), line)
        if m:
            return float(m.group(1))
    return default


def read_vector2(lines, key):
    for line in lines:
        m = re.match(r"\s*%s:\s*\{x:\s*(-?[0-9.]+),\s*y:\s*(-?[0-9.]+)\}" % re.escape(key), line)
        if m:
            return float(m.group(1)), float(m.group(2))
    return None


def has_key(lines, key):
    return any(re.match(r"\s*%s:" % re.escape(key), line) for line in lines)


def set_scalar(lines, key, value):
    """`  key: value` 를 갈아끼우거나, 없으면 파일 끝에 덧붙인다."""
    text = "  %s: %s\n" % (key, value)
    for i, line in enumerate(lines):
        if re.match(r"\s*%s:" % re.escape(key), line):
            lines[i] = text
            return
    while lines and lines[-1].strip() == "":
        lines.pop()          # ⚠ 빈 줄이 있으면 그 뒤 필드를 유니티가 통째로 무시한다
    lines.append(text)


def set_vector2(lines, key, xy):
    set_scalar(lines, key, "{x: %s, y: %s}" % (fmt(xy[0]), fmt(xy[1])))


def fmt(v):
    """YAML 에 넣을 숫자 — 소수점 세 자리, 뒤 0 제거."""
    s = ("%.3f" % float(v)).rstrip("0").rstrip(".")
    return s if s else "0"


# ---------------------------------------------------------------------------

def patch_skin(path, guid_index):
    with open(path, encoding="utf-8") as f:
        lines = f.readlines()
    before = list(lines)

    name = os.path.basename(path)[:-6]
    notes = []

    # ① 실측값 — 항상 다시 잰다.
    body = measure_frames(lines, IDLE_KEYS, guid_index)
    if body:
        set_vector2(lines, "contentSizeTiles", body)
        notes.append("몸집 %s x %s 타일" % (fmt(body[0]), fmt(body[1])))
    else:
        notes.append("⚠ 대기 원화를 못 찾음")

    proj = measure_frames(lines, ("projectileFrames",), guid_index)
    if proj:
        set_vector2(lines, "projectileSizeTiles", proj)

    impact = measure_frames(lines, ("impactFrames",), guid_index)
    if impact:
        set_vector2(lines, "impactSizeTiles", impact)

    # ② 표시 크기 — 없을 때만, 지금 보이는 크기를 그대로 타일로 환산해 채운다.
    if proj and not has_key(lines, "projectileWidthTiles"):
        old = read_float(lines, "projectileScale", 1.0)
        set_scalar(lines, "projectileWidthTiles", fmt(proj[0] * old))
        notes.append("탄환 %s 타일" % fmt(proj[0] * old))

    if impact and not has_key(lines, "impactWidthTiles"):
        old = read_vector2(lines, "impactScale") or (1.0, 1.0)
        set_scalar(lines, "impactWidthTiles", fmt(impact[0] * old[0]))
        flatten = old[1] / old[0] if old[0] > 0.0001 else 1.0
        set_scalar(lines, "impactFlattenY", fmt(min(max(flatten, 0.1), 1.0)))
        notes.append("착탄 %s 타일" % fmt(impact[0] * old[0]))

    if lines != before:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.writelines(lines)
        print("  %-22s %s" % (name, " · ".join(notes)))
        return True

    print("  %-22s 변경 없음 (%s)" % (name, " · ".join(notes)))
    return False


def main():
    print("guid 색인 만드는 중…")
    guid_index = build_guid_index()
    print("  PNG %d개" % len(guid_index))

    changed = 0
    for root in SKIN_ROOTS:
        if not os.path.isdir(root):
            continue
        print("\n[%s]" % os.path.relpath(root, PROJECT))
        for dirpath, _dirs, files in os.walk(root):
            for f in sorted(files):
                if f.startswith("Skin_") and f.endswith(".asset"):
                    changed += patch_skin(os.path.join(dirpath, f), guid_index)

    print("\n%d개 에셋 갱신 — Unity 에서 Assets/Refresh 를 실행할 것." % changed)
    return 0


if __name__ == "__main__":
    sys.exit(main())
