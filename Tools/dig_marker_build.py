# -*- coding: utf-8 -*-
"""발굴 표식 느낌표 원화(볼트 `리소스/sprites/!.png`) → `Resources/DigMarker/dig_marker.png`
(2026-08-25).

유저 지시: *"느낌표 스프라이트 볼트에 넣어놨으니까 <b>텍스트 대신 주황색 느낌표 짤라서 써</b>
발굴칸에"*.

원화에는 느낌표가 <b>다섯 벌</b> 들어 있다 — 노랑 · 빨강 · 파랑 · 흰색 · <b>주황(반짝임 있음)</b>.
유저가 «주황색» 이라고 했고, 다섯째만 <b>반짝임 조각</b>이 둘러 있어 «주의를 끄는 것» 이라는
성격도 맞는다. 그래서 <b>다섯째</b>를 쓴다.

★★ <b>배경을 «흰색 지우기» 로 없애지 않는다</b>
--------------------------------------------
원화는 알파가 없는 RGB 이고 배경이 흰색이다. 그런데 <b>느낌표 안에도 흰 하이라이트가 있다</b>
(볼록한 느낌을 내는 빛). 「흰 픽셀을 전부 투명하게」 로 처리하면 <b>그 하이라이트에 구멍이
뚫린다</b>. 그래서 <b>테두리에서 흘려보내(flood fill)</b> «바깥과 이어진 흰색» 만 지운다 —
안쪽 하이라이트는 바깥과 이어져 있지 않으므로 그대로 남는다.

★ 잘라내는 자리도 <b>세어서</b> 찾는다 — 열마다 «배경이 아닌 픽셀» 을 세어 빈 열로 다섯 덩이를
  가른다. 좌표를 손으로 박아 두면 원화가 한 번 바뀔 때 조용히 어긋난다.

⚠ <b>.meta 를 다시 만들지 않는다</b>(이미 있으면). 씬의 표식이 guid 로 이 스프라이트를 가리키게
  되므로, guid 가 바뀌면 참조가 끊긴다(`relic_icon_build.py` 와 같은 규칙).
⚠ 픽셀아트라 <b>filterMode 0(Point)</b> · 압축 없음으로 굽는다. 기본값(Bilinear)으로 두면
  확대했을 때 뭉개져 «픽셀» 로 안 보인다.

사용법:  py -3 Tools/dig_marker_build.py
         py -3 Tools/dig_marker_build.py --contact   (다섯 벌을 갈라 본 결과를 그림으로 확인)
다음:    유니티에서 Assets/Refresh
"""

import hashlib
import io
import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

SRC = os.path.join(VAULT, "리소스", "sprites", "!.png")
OUT_DIR = os.path.join(PROJECT, "Assets", "_Project", "Resources", "DigMarker")
OUT_PNG = os.path.join(OUT_DIR, "dig_marker.png")

#: 왼쪽부터 몇 번째 느낌표를 쓸 것인가 (1부터). 5 = 주황 + 반짝임.
PICK = 5

#: 잘라낸 뒤 사방에 남길 여백(px). 반짝임이 테두리에 딱 붙어 잘리지 않게 조금 둔다.
MARGIN = 2

#: «배경 흰색» 판정 문턱. 원화 배경은 순백(255)이고 하이라이트는 그보다 약간 어둡다.
WHITE = 244

#: ★★ 이보다 좁은 덩이는 <b>부스러기</b>로 보고 버린다.
#   ⚠ 실측 — 처음 돌렸을 때 덩이가 <b>다섯이 아니라 아홉</b>으로 갈렸다. 느낌표 사이에
#     <b>1~2px 짜리 점</b>이 끼어 있었고(원화의 잡티), 그것을 덩이로 세어서 «다섯째» 가
#     주황이 아니라 <b>파랑</b>을 집었다. 느낌표는 135px 이 넘으므로 이 문턱이면 안전하다.
MIN_WIDTH = 24


def guid_for(key):
    """경로에서 결정적으로 만든다 — 다시 돌려도 같은 guid 라 참조가 안 끊긴다."""
    return hashlib.md5(("LastSanctuary/" + key).encode("utf-8")).hexdigest()


FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

#: ⚠ 픽셀아트용으로 <b>filterMode 0(Point)</b> · <b>textureCompression 0(없음)</b> 이다.
#   나머지 칸은 이 프로젝트의 다른 스프라이트 meta 와 같은 규약이다.
PNG_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
  spritePackingTag:
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def background_mask(rgb):
    """
    ★★ <b>테두리에서 흘려보내</b> «바깥과 이어진 흰색» 만 배경으로 잡는다 (맨 위 ★★).

    안쪽 하이라이트는 검은 테두리에 둘러싸여 바깥과 이어지지 않으므로 <b>살아남는다</b>.
    """
    h, w, _ = rgb.shape
    whiteish = np.all(rgb >= WHITE, axis=2)

    bg = np.zeros((h, w), dtype=bool)
    stack = []

    # 네 변의 흰 픽셀에서 시작한다.
    for x in range(w):
        for y in (0, h - 1):
            if whiteish[y, x] and not bg[y, x]:
                bg[y, x] = True
                stack.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if whiteish[y, x] and not bg[y, x]:
                bg[y, x] = True
                stack.append((y, x))

    while stack:
        y, x = stack.pop()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and whiteish[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                stack.append((ny, nx))
    return bg


def clusters(mask):
    """열마다 «배경이 아닌 픽셀» 을 세어 빈 열로 덩이를 가른다. [(x0, x1), …]"""
    counts = (~mask).sum(axis=0)
    out, run = [], None
    for x, n in enumerate(counts):
        if n > 0 and run is None:
            run = x
        elif n == 0 and run is not None:
            out.append((run, x - 1))
            run = None
    if run is not None:
        out.append((run, len(counts) - 1))

    # ⚠ 부스러기를 버린다 — 위 MIN_WIDTH 의 ★★.
    return [(a, b) for (a, b) in out if b - a + 1 >= MIN_WIDTH]


def main():
    contact = "--contact" in sys.argv

    if not os.path.isfile(SRC):
        raise SystemExit("⚠ 원화가 없습니다: %s" % SRC)

    im = Image.open(SRC).convert("RGB")
    rgb = np.array(im)
    bg = background_mask(rgb)
    fg = ~bg

    cols = clusters(bg)
    print("[발굴 표식]")
    print("  원화 %s · 갈라낸 덩이 %d개" % (im.size, len(cols)))
    for i, (x0, x1) in enumerate(cols, start=1):
        rows = np.where(fg[:, x0:x1 + 1].any(axis=1))[0]
        print("    %d) x %d~%d (%dpx) · y %d~%d (%dpx)%s"
              % (i, x0, x1, x1 - x0 + 1, rows[0], rows[-1], rows[-1] - rows[0] + 1,
                 "  ← 쓸 것" if i == PICK else ""))

    if contact:
        # 갈라낸 결과를 눈으로 확인하는 그림. 굽지는 않는다.
        strip = Image.new("RGB", (im.size[0], im.size[1]), (30, 30, 34))
        strip.paste(im, (0, 0), Image.fromarray((fg * 255).astype("uint8"), "L"))
        p = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_dig_marker_contact.png")
        strip.save(p)
        print("  대조표: %s" % p)
        return 0

    if len(cols) < PICK:
        raise SystemExit("⚠ 느낌표를 %d개만 찾았습니다 — PICK=%d 를 쓸 수 없습니다"
                         % (len(cols), PICK))

    x0, x1 = cols[PICK - 1]
    rows = np.where(fg[:, x0:x1 + 1].any(axis=1))[0]
    y0, y1 = rows[0], rows[-1]

    # 여백을 두되 원화 밖으로 나가지 않게 자른다.
    x0 = max(0, x0 - MARGIN)
    x1 = min(im.size[0] - 1, x1 + MARGIN)
    y0 = max(0, y0 - MARGIN)
    y1 = min(im.size[1] - 1, y1 + MARGIN)

    rgba = np.dstack([rgb, np.where(bg, 0, 255).astype("uint8")])
    crop = Image.fromarray(rgba[y0:y1 + 1, x0:x1 + 1], "RGBA")

    os.makedirs(OUT_DIR, exist_ok=True)
    folder_meta = OUT_DIR + ".meta"
    if not os.path.isfile(folder_meta):
        rel = os.path.relpath(OUT_DIR, PROJECT).replace("\\", "/")
        with io.open(folder_meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid_for(rel)))

    crop.save(OUT_PNG)

    rel = os.path.relpath(OUT_PNG, PROJECT).replace("\\", "/")
    meta = OUT_PNG + ".meta"
    if os.path.isfile(meta):
        # ⚠ 이미 있으면 <b>손대지 않는다</b> — guid 가 바뀌면 씬의 참조가 끊긴다.
        print("  .meta 는 그대로 둡니다 (guid 유지)")
    else:
        g = guid_for(rel)
        with io.open(meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(PNG_META.format(guid=g, sprite_id=guid_for(rel + "#sprite")))
        print("  .meta 새로 만듦 · guid %s" % g)

    print("  → %s  (%dx%d · Point 필터 · 압축 없음)" % (rel, crop.size[0], crop.size[1]))
    print("  다음: 유니티에서 Assets/Refresh 후 py -3 Tools/mcp_dig_marker.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
