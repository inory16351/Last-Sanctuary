# -*- coding: utf-8 -*-
"""중립 몬스터 3종(1001~1003) 모션 시트 → 프레임 분해 (2026-08-15).

유저 지시: *"몬스터들 스킨 에셋 리소스 폴더 찾아보고 만들어."*

원본 (볼트 `리소스/`)
---------------------
  · ``Tumor spider_asset.png``  1536x1024 — 종양 거미(1001, 근거리)
  · ``Tumor_mole_asset.png``    1536x1024 — 종양 두더지(1003, 근거리)
  · ``Tumorling_asset.png``     1402x1122 — 종양귀(1002, 원거리)

★ 어느 시트가 어느 id 인가는 <b>표의 `atk_type` 이 갈라준다</b> — 종양귀 시트에만
  「원거리 공격」·「투사체」 행이 있고, 표에서 원거리인 종은 1002 하나다.
  자세한 근거는 `table_update_20260815_neutral_names.py` 주석에 있다.

★★ 세 시트가 <b>전부 레이아웃이 다르다</b> — 한 가지 방법으로는 못 자른다
--------------------------------------------------------------------
카르시노스는 프레임끼리 완전히 떨어져 있어 빈 열 탐지 하나로 끝났다(`carcinos_skin_build.py`).
히스톤은 겹쳐 있어 자동 분리를 네 번 시도해 네 번 다 실패하고 경계표를 손으로 쟀다(84-1절).
이 세 장은 그 중간이라, <b>시트마다 프레임 경계를 어디서 얻을지</b>를 설정으로 갈랐다:

  ``digits``  — 프레임 <b>번호 라벨</b>(1~6)의 중심을 프레임 중심으로 삼고, 이웃 중심의
                <b>중점</b>에서 자른다. 종양 거미가 이 방법이다. 근거리 공격 행의 보라색
                할퀴기 궤적이 <b>앞 프레임 칸까지 뻗어 있어</b> 빈 열 탐지가 6덩어리를
                3덩어리로 뭉갠다(실측). 번호는 그 영향을 안 받는다.

  ``grid``    — 시트에 <b>실제 격자선이 그려져 있으면</b> 그 선을 찾아 칸으로 쓴다.
                종양 두더지가 그렇다(세로선 x≈15·262·513·767·1020·1271·1519).
                ⚠ 이 시트는 <b>행 순서가 대기 → 근거리 공격 → 이동</b>이다 (다른 둘과 다르다).
                ⚠ 행마다 <b>첫 칸 위에 한글 라벨 상자</b>가 얹혀 있어 그 띠를 잘라내고 잰다.

  ``auto``    — 빈 열 탐지 + 가까운 조각 합치기(카르시노스와 같은 방법).
  ``auto_low``— 빈 열 탐지를 <b>아래쪽 일부만</b> 보고 한다. 종양귀의 원거리 공격 행은
                <b>기 모으는 구체가 옆 프레임까지 겹쳐</b> 위쪽으로는 11덩어리가 5덩어리로
                붙는다. 발밑(하단 45%)은 프레임마다 깨끗이 떨어져 있어 11개가 그대로 나온다.

방향
----
게임은 좌우 두 벌을 쓴다(<c>CharacterSkinSO.idleRight/idleLeft</c>). 원본이 보는 쪽을
그대로 쓰고 반대쪽은 좌우 반전으로 만든다.

  · 종양 거미   — <b>왼쪽</b> (이동 행의 흙먼지가 오른쪽 뒤에 남고, 할퀴기 궤적이 왼쪽으로 나간다)
  · 종양 두더지 — <b>왼쪽</b> (주둥이·앞발이 왼쪽, 공격 궤적도 왼쪽)
  · 종양귀      — <b>오른쪽</b> (이동 행 흙먼지가 왼쪽 뒤에 남고, 기 모으는 구체가 오른쪽 위에 뜬다)

⚠ <b>투사체만 예외로 한 번 더 뒤집는다.</b> `CharacterSkinSO.projectileFrames` 는
  "<b>+X(오른쪽)를 향한 그림 한 벌</b>" 이 계약이다(연출 쪽이 진행 방향으로 회전시키므로
  방향별 원화를 넣으면 왼쪽으로 갈 때 두 번 뒤집혀 거꾸로 난다). 그런데 종양귀 시트의
  투사체는 <b>탄두가 왼쪽·꼬리가 오른쪽</b>으로 그려져 있어 그대로 넣으면 뒤로 난다.
  `PROJECTILE_FLIP` 한 곳에서 뒤집는다.

★ ``Tumor spider_asset_02.png`` 의 <b>다듬은 이동 16프레임을 쓴다</b> (유저 확정 2026-08-16).

  ⚠ 이 시트에는 "16프레임" 이라고 적혀 있지만 실제로 그려진 것은 <b>15장</b>이다 —
  번호가 <b>1~7 다음 9~16</b> 으로 뛴다. <b>8번이 통째로 빠져 있다.</b> 그대로 쓰면
  걷기 순환이 매 바퀴 같은 자리에서 살짝 끊긴다.

  그래서 <b>7번과 9번을 섞어 8번을 만들어 끼워 넣는다</b>(유저 확정: "15장 + 8번을 보간으로 생성").
  두 프레임을 알파 가중 평균으로 겹치는 단순 크로스페이드다 — 픽셀 아트라 보간된 한 장이
  이웃보다 살짝 흐릿하지만, 16장을 초당 10장으로 돌리면 0.1초만 보이고 <b>순환이 끊기는
  것보다 훨씬 덜 거슬린다.</b> 8번을 실제로 그린 시트를 받으면 아래 ``interpolate`` 를
  지우기만 하면 된다.

사용법:  py -3 Tools/neutral_skin_build.py
다음:    py -3 Tools/gen_neutral_skins.py
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

SRC_DIR = os.path.join(VAULT, "리소스")
ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset")

#: 1픽셀당 유니티 단위. 게임 안 크기는 `contentSizeTiles` 로 정규화되므로(61·66절)
#: 이 값이 화면 크기를 정하지는 않는다 — 다른 유닛과 같은 대역이면 된다.
PPU = 64

#: 배경과 이만큼 떨어지면 그림으로 본다. 안티에일리어싱 가장자리는 두 값 사이에서 부드럽게 뺀다.
ALPHA_LO = 24
ALPHA_HI = 60

#: 투사체 원화가 +X 를 향하도록 뒤집을지 (맨 위 "방향" 주석 참조).
PROJECTILE_FLIP = True

#: 빠진 프레임을 채우는 방식 — ``"blend"`` 또는 ``"duplicate"``.
#:
#: ★ ``blend``  (유저 확정 2026-08-16) 앞뒤 프레임을 섞어 <b>없던 한 장을 만든다</b>.
#:              ⚠ 픽셀 아트에는 진짜 중간 프레임이 없으므로 <b>두 자세의 합집합</b>이 나온다 —
#:              거미는 <b>다리가 겹쳐 보여 그 한 장만 살짝 통통하다</b>(실측). 16장을 초당
#:              10장으로 돌리면 0.1초만 보인다.
#: ``duplicate`` 앞 프레임을 그대로 한 번 더 쓴다. 통통해지지는 않지만 그 자리에서
#:              <b>0.1초 멈춘 것처럼</b> 보인다.
#:
#: 화면에서 보고 마음에 안 들면 이 값만 바꿔 다시 돌리면 된다.
INTERPOLATE_MODE = "blend"

# ──────────────────────────────────────────────────────────────────────────
# 시트별 설정
#
# y0/y1 은 <b>실측한 행의 세로 범위</b>다 — 시트를 다시 그리면 여기를 다시 재야 한다.
# 재는 방법은 `Tools/` 에 남겨둔 정찰 스크립트와 같다(임계값을 바꿔가며 가로 밴드를 센다).
# ──────────────────────────────────────────────────────────────────────────

SHEETS = [
    {
        "species": "TumorSpider",
        "src": "Tumor spider_asset.png",
        "faces": "left",
        "rows": [
            # 번호 라벨 띠(label)의 숫자 중심이 곧 프레임 중심이다.
            {"motion": "Idle",        "y": (112, 293), "n": 6, "how": "digits", "label": (39, 78)},
            {"motion": "MeleeAttack", "y": (779, 965), "n": 6, "how": "digits", "label": (723, 763)},
        ],
    },
    {
        # ★ 이동만 <b>다듬은 시트</b>에서 뽑는다 (유저 확정 2026-08-16 — 맨 위 주석).
        #   같은 종에 대해 시트를 두 장 쓰는 첫 사례다. 종 이름이 같으므로 결과가
        #   같은 폴더(Char_Asset_TumorSpider/Char/Walk)에 들어간다.
        "species": "TumorSpider",
        "src": "Tumor spider_asset_02.png",
        "faces": "left",
        "rows": [
            # 두 줄로 나뉘어 있다 — 윗줄 7장(1~7) · 아랫줄 8장(9~16).
            # 8번이 없어서 아래 `interpolate` 가 7번과 9번을 섞어 만들어 끼운다.
            {"motion": "Walk", "y": (264, 437), "n": 7, "how": "auto",
             "index_offset": 0},
            {"motion": "Walk", "y": (649, 832), "n": 8, "how": "auto",
             "index_offset": 8, "append": True},
        ],
        # (모션, 새로 만들 인덱스, 섞을 두 인덱스)
        "interpolate": [("Walk", 7, 6, 8)],
    },
    {
        "species": "TumorMole",
        "src": "Tumor_mole_asset.png",
        "faces": "left",
        # ⚠ 이 시트만 행 순서가 다르다 (대기 → 근거리 공격 → 이동).
        "rows": [
            {"motion": "Idle",        "y": (16, 335),   "n": 6, "how": "grid", "label_h": 70},
            {"motion": "MeleeAttack", "y": (352, 671),  "n": 6, "how": "grid", "label_h": 70},
            {"motion": "Walk",        "y": (688, 1007), "n": 6, "how": "grid", "label_h": 70},
        ],
    },
    {
        "species": "Tumorling",
        "src": "Tumorling_asset.png",
        "faces": "right",
        "rows": [
            {"motion": "Idle",         "y": (43, 200),  "n": 7,  "how": "auto"},
            {"motion": "Walk",         "y": (264, 393), "n": 8,  "how": "auto"},
            # ⚠ 이 행만 가르는 임계값이 높다. 기 모으는 구체의 옅은 잔광이 60 에서는
            #   프레임 사이를 메워 11개가 통째로 하나로 붙는다(실측: 1덩어리).
            #   120 이면 발밑 45% 구간이 정확히 11덩어리로 갈라진다.
            {"motion": "RangedAttack", "y": (456, 622), "n": 11, "how": "auto_low",
             "low": 0.45, "seg": 120},
            # 투사체는 방향이 없어 한 벌만 만든다(아래 `sided: False`).
            {"motion": "Projectile",   "y": (675, 761), "n": 11, "how": "auto", "sided": False},
        ],
        # 참고용 「방향별 대기(8방향)」 블록은 y 772 아래에 있다 — 쓰지 않는다.
    },
]

SEG_THRESHOLD = 60      # 프레임을 <b>가르는</b> 기준 (알파 기준보다 높다 — 잔광에 안 붙게)
MERGE_GAP = 10          # 이보다 가까운 덩어리는 같은 프레임의 조각으로 본다
MIN_CHUNK = 3           # 이보다 얇은 덩어리는 격자선·먼지로 본다

META = """fileFormatVersion: 2
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
  alignment: {alignment}
  spritePivot: {{x: {pivot_x}, y: {pivot_y}}}
  spritePixelsToUnits: {ppu}
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
  - serializedVersion: 4
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
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = ("fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
               "DefaultImporter:\n  externalObjects: {{}}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")


def guid_for(key):
    """경로에서 결정적으로 뽑은 guid — 다시 돌려도 같은 값이라 참조가 안 끊긴다."""
    import hashlib
    return hashlib.md5(("LastSanctuary/" + key).encode("utf-8")).hexdigest()


def bands(flags, min_len=1):
    """True 가 이어지는 구간 목록 [(시작, 끝)]."""
    out, run = [], None
    for i, v in enumerate(flags):
        if v:
            if run is None:
                run = i
        elif run is not None:
            out.append((run, i - 1))
            run = None
    if run is not None:
        out.append((run, len(flags) - 1))
    return [b for b in out if b[1] - b[0] + 1 >= min_len]


def merge_close(chunks, gap):
    out = [list(chunks[0])]
    for s, e in chunks[1:]:
        if s - out[-1][1] <= gap:
            out[-1][1] = e
        else:
            out.append([s, e])
    return [tuple(m) for m in out]


def merge_to_count(chunks, n):
    """
    덩어리가 <b>n 개가 될 때까지 가장 좁은 틈부터</b> 합친다.

    ★ 고정 임계값(``MERGE_GAP``)을 쓰지 않는 이유 — 시트마다 "한 프레임 안의 조각 사이"와
      "프레임과 프레임 사이"의 간격 대비가 다르다. 종양귀의 원거리 공격 행은 프레임 사이가
      <b>4~6px</b> 밖에 안 벌어져서 카르시노스에 쓰던 10 을 그대로 넣으면 11개가 통째로
      하나가 되고, 2 로 낮추면 이번엔 9·10번만 붙는다(그 둘 사이가 정확히 2px 였다).
      <b>몇 개여야 하는지는 우리가 알고 있으므로</b>, 임계값을 맞히려 하지 말고
      좁은 틈부터 차례로 없애 그 개수에 도달하는 편이 흔들리지 않는다.

    n 보다 적으면 애초에 갈라지지 않은 것이라 여기서는 못 고친다 — 호출한 쪽이 실패로 본다.
    """
    out = [list(c) for c in chunks]
    while len(out) > n:
        gaps = [out[i + 1][0] - out[i][1] for i in range(len(out) - 1)]
        i = gaps.index(min(gaps))
        out[i][1] = out[i + 1][1]
        del out[i + 1]
    return [tuple(m) for m in out]


# ── 프레임 경계를 얻는 네 가지 방법 ────────────────────────────────────────

def drop_specks(chunks, keep_at_least, ratio=0.2):
    """
    <b>프레임이라고 볼 수 없을 만큼 얇은 조각</b>을 버린다 — 시트 가장자리의 자투리 등.

    기준은 <b>이 행의 중앙값 폭 대비 비율</b>이다. 절대 픽셀값으로 자르면 시트마다 달라지고,
    무엇보다 <b>먼지와 자투리를 구분할 수 없다</b>:

      · 종양귀 이동 행 — 덩어리 폭의 중앙값이 <b>6</b>(흙먼지가 많다). 임계값 1.2 라
        아무것도 안 버린다 → 먼지는 그대로 남아 이웃 프레임에 <b>합쳐진다</b>. 맞는 동작이다.
      · 거미 다듬은 이동 행 — 중앙값이 <b>177</b>. 임계값 35 라 오른쪽 끝의 <b>4px 자투리</b>가
        버려진다.

    ⚠ <b>간격으로 판단하면 안 된다</b>(처음엔 그렇게 짰다가 실패했다). 그 자투리는 앞
    프레임에서 <b>7px</b> 떨어져 있었는데, 진짜 프레임 사이 간격 중에 <b>6px</b> 짜리가 있었다.
    그래서 `merge_to_count` 가 <b>진짜 프레임 두 개를 합쳐버리고</b> 자투리를 프레임으로
    남겼다 — 캔버스가 219 → <b>357</b> 로 부풀어 모든 프레임에 거대한 여백이 붙었다.

    <paramref name="keep_at_least"/> 장은 반드시 남긴다 — 버리다가 개수가 모자라면 안 된다.
    """
    if len(chunks) <= keep_at_least:
        return chunks

    widths = sorted(c[1] - c[0] + 1 for c in chunks)
    median = widths[len(widths) // 2]
    limit = median * ratio

    kept = [c for c in chunks if (c[1] - c[0] + 1) >= limit]
    return kept if len(kept) >= keep_at_least else chunks


def windows_auto(seg, y0, y1, n, low=None):
    """빈 열 탐지. ``low`` 를 주면 <b>아래쪽 그 비율만</b> 보고 가른다."""
    ys = y0 if low is None else int(y1 - (y1 - y0) * low)
    sub = seg[ys:y1 + 1, :]
    chunks = bands(sub.sum(axis=0) > 0, MIN_CHUNK)
    if len(chunks) > n:
        chunks = drop_specks(chunks, n)
    if len(chunks) < n:
        raise SystemExit(f"⚠ 프레임이 {n}개로 갈라지지 않습니다({len(chunks)}개): {chunks}")
    return merge_to_count(chunks, n)


def windows_digits(seg, label_y, n, width):
    """
    번호 라벨의 <b>숫자</b> 중심을 프레임 중심으로 보고, 이웃 중심의 중점에서 자른다.

    한글 라벨 상자는 숫자보다 훨씬 넓어(80~150px) 폭으로 걸러진다.
    """
    ly0, ly1 = label_y
    chunks = bands(seg[ly0:ly1 + 1, :].sum(axis=0) > 0, 1)
    digits = [c for c in chunks if c[1] - c[0] + 1 <= 30]
    if len(digits) != n:
        raise SystemExit(f"⚠ 번호 라벨이 {n}개가 아닙니다({len(digits)}개): {digits}")

    centers = [(c[0] + c[1]) // 2 for c in digits]
    pitch = (centers[-1] - centers[0]) / float(n - 1)
    edges = [int(centers[0] - pitch / 2)]
    for i in range(n - 1):
        edges.append((centers[i] + centers[i + 1]) // 2)
    edges.append(int(centers[-1] + pitch / 2))
    return [(max(0, edges[i]), min(width - 1, edges[i + 1] - 1)) for i in range(n)]


def windows_grid(dist, n, width, height):
    """
    시트에 그려진 <b>세로 격자선</b>을 찾아 칸으로 쓴다.

    격자선은 배경과 살짝만 다른 옅은 회색이라 <b>세로로 거의 끝까지 이어진다</b>는
    성질로 찾는다(그림은 그렇지 않다).
    """
    frac = (dist > 20).mean(axis=0)
    # 선이 1~3px 두께라 붙은 픽셀을 하나로 묶는다.
    lines = merge_close(bands(frac > 0.85, 1), 4)
    if len(lines) != n + 1:
        raise SystemExit(f"⚠ 세로 격자선이 {n + 1}개가 아닙니다({len(lines)}개): {lines}")
    # 선의 안쪽만 쓴다(선 자체가 프레임에 섞이지 않게 2px 여유).
    return [(lines[i][1] + 2, lines[i + 1][0] - 2) for i in range(n)]


# ── 자르기 ────────────────────────────────────────────────────────────────

def to_rgba(rgb_block, bg):
    """배경(밝은 색)과의 거리로 알파를 만든다. 가장자리만 부드럽게."""
    dist = np.abs(rgb_block.astype(int) - bg).sum(axis=2)
    alpha = np.clip((dist - ALPHA_LO) * 255.0 / (ALPHA_HI - ALPHA_LO), 0, 255)
    return np.dstack([rgb_block, alpha.astype(np.uint8)]).astype(np.uint8)


def write_png(img, folder, name, bottom_pivot=True):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".png")
    img.save(path)

    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    g = guid_for(rel)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        # 유닛은 발밑(0.5, 0) 이 기준이라 alignment 9(Custom) + pivot 을 준다.
        # 투사체는 회전시켜 날리므로 <b>한가운데</b>(alignment 0 = Center)여야 한다 —
        # 발밑 피벗이면 회전축이 아래로 쏠려 궤적이 휜다.
        f.write(META.format(guid=g, ppu=PPU, sprite_id=g[:32],
                            alignment=9 if bottom_pivot else 0,
                            pivot_x=0.5, pivot_y=0 if bottom_pivot else 0.5))
    return path


def ensure_folder_meta(path):
    mp = path.rstrip("\\/") + ".meta"
    if os.path.exists(mp):
        return
    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    with open(mp, "w", encoding="utf-8", newline="\n") as f:
        f.write(FOLDER_META.format(guid=guid_for(rel)))


def build(sheet):
    src = os.path.join(SRC_DIR, sheet["src"])
    if not os.path.isfile(src):
        print("⚠ 원본이 없습니다:", src)
        return 0

    im = Image.open(src).convert("RGB")
    arr = np.asarray(im).astype(np.uint8)
    bg = arr[0, 0].astype(int)
    dist = np.abs(arr.astype(int) - bg).sum(axis=2)
    mask = dist > ALPHA_LO       # 그림의 실제 경계 (자를 상자를 재는 데 쓴다)
    seg = dist > SEG_THRESHOLD   # 프레임을 가르는 데만 쓴다
    h, w = dist.shape

    dst_root = os.path.join(ART_ROOT, "Char_Asset_" + sheet["species"], "Char")
    faces_right = sheet["faces"] == "right"
    print(f"[{sheet['species']}] {sheet['src']} {im.size} · 배경 {tuple(bg)} · 원본 {sheet['faces']}")

    # ── ① 행마다 프레임 상자를 잰다 ─────────────────────────────────────
    #
    # ★ <b>모션별로 모은다</b>(2026-08-16) — 한 모션이 <b>여러 행</b>에 걸쳐 있을 수 있다.
    #   종양 거미의 다듬은 이동이 윗줄 7장 + 아랫줄 8장으로 나뉜 것이 그 경우다.
    #   ⚠ 캔버스 크기를 <b>행마다 따로</b> 재면 7번과 9번 사이에서 개체가 튄다 —
    #     모션 전체를 한 상자로 재야 한다.
    motions = {}          # 모션 → {"boxes": {인덱스: 상자}, "sided": bool, "rows": [설명]}

    for row in sheet["rows"]:
        y0, y1 = row["y"]
        n = row["n"]
        how = row["how"]
        sided = row.get("sided", True)
        offset = row.get("index_offset", 0)

        # 행마다 가르는 임계값을 따로 줄 수 있다 — 시트에 따라 잔광의 세기가 다르다.
        row_seg = dist > row["seg"] if "seg" in row else seg

        if how == "digits":
            wins = windows_digits(row_seg, row["label"], n, w)
        elif how == "grid":
            wins = windows_grid(dist, n, w, h)
            # 첫 칸 위의 한글 라벨 띠를 잘라내고, <b>아래 격자선도 2px 물러선다</b>.
            # ⚠ 격자선을 남기면 그림 경계를 재는 mask 가 <b>칸 전체</b>를 그림으로 보고
            #   모든 프레임의 상자가 칸 크기로 부풀어 오른다(실측: 229 → 250).
            y0 += row.get("label_h", 0)
            y1 -= 2
        elif how == "auto_low":
            wins = windows_auto(row_seg, y0, y1, n, low=row["low"])
        else:
            wins = windows_auto(row_seg, y0, y1, n)

        entry = motions.setdefault(row["motion"], {"boxes": {}, "sided": sided, "rows": []})
        for i, (x0, x1) in enumerate(wins):
            sub = mask[y0:y1 + 1, x0:x1 + 1]
            ys = np.where(sub.any(axis=1))[0]
            xs = np.where(sub.any(axis=0))[0]
            if len(ys) == 0 or len(xs) == 0:
                raise SystemExit(f"⚠ {row['motion']} 의 칸 {x0}~{x1} 이 비어 있습니다")
            entry["boxes"][offset + i] = (x0 + xs.min(), x0 + xs.max(),
                                          y0 + ys.min(), y0 + ys.max())
        entry["rows"].append(f"y {y0}~{y1} [{how}] {n}장")

    # ── ② 모션마다 한 캔버스로 써낸다 ───────────────────────────────────
    made = 0
    for motion, entry in motions.items():
        boxes = entry["boxes"]
        sided = entry["sided"]

        cw = max(b[1] - b[0] + 1 for b in boxes.values())
        ch = max(b[3] - b[2] + 1 for b in boxes.values())

        folder = os.path.join(dst_root, motion)
        written = {}          # 인덱스 → 캔버스(보간에 쓴다)

        for i in sorted(boxes):
            bx0, bx1, by0, by1 = boxes[i]
            rgba = to_rgba(arr[by0:by1 + 1, bx0:bx1 + 1], bg)

            canvas = np.zeros((ch, cw, 4), dtype=np.uint8)
            bw, bh = bx1 - bx0 + 1, by1 - by0 + 1
            if sided:
                # 가로는 그림 중심, 세로는 <b>바닥</b>을 맞춘다 — 피벗이 발밑이라
                # 바닥을 맞춰야 모션이 바뀔 때 위아래로 안 튄다.
                oy = ch - bh
            else:
                oy = (ch - bh) // 2      # 투사체는 한가운데 피벗이라 세로도 가운데
            ox = (cw - bw) // 2
            canvas[oy:oy + bh, ox:ox + bw] = rgba
            written[i] = canvas

        # ── ③ 빠진 프레임을 이웃 둘로 만들어 끼운다 ────────────────────
        for target_motion, at, a, b in sheet.get("interpolate", []):
            if target_motion != motion:
                continue
            if a not in written or b not in written:
                raise SystemExit(f"⚠ {motion} 보간 원본 {a}·{b} 가 없습니다")
            if at in written:
                print(f"    (보간 생략) {motion} {at} 번은 이미 있다")
                continue
            if INTERPOLATE_MODE == "duplicate":
                written[at] = written[a].copy()
                print(f"    ★ {motion} {at} 번을 {a} 번 복제로 채웠다 (원본 시트에 없다)")
            else:
                written[at] = blend(written[a], written[b])
                print(f"    ★ {motion} {at} 번을 {a}·{b} 로 보간해 채웠다 (원본 시트에 없다)")

        for i in sorted(written):
            img = Image.fromarray(written[i], "RGBA")
            if not sided:
                # 투사체 한 벌 — +X 를 향하도록 맞춘다(맨 위 주석).
                if PROJECTILE_FLIP:
                    img = img.transpose(Image.FLIP_LEFT_RIGHT)
                write_png(img, folder, f"Char_{motion}_{i:02d}", bottom_pivot=False)
                made += 1
            else:
                flip = img.transpose(Image.FLIP_LEFT_RIGHT)
                right, left = (img, flip) if faces_right else (flip, img)
                write_png(right, folder, f"Char_{motion}_Right_{i:02d}")
                write_png(left, folder, f"Char_{motion}_Left_{i:02d}")
                made += 2

        ensure_folder_meta(folder)
        print(f"  {motion:<13} {cw:4d} x {ch:3d} · {len(written)}장"
              f"{' (+ 좌우 반전)' if sided else ' (방향 없음)'} · {' + '.join(entry['rows'])}")

    ensure_folder_meta(dst_root)
    ensure_folder_meta(os.path.dirname(dst_root))
    return made


def blend(a, b):
    """
    두 프레임을 섞어 빠진 한 장을 만든다 — <b>알파를 고려한</b> 크로스페이드.

    ⚠ <b>단순 평균을 내면 안 된다.</b> 두 가지가 동시에 망가진다:

      ① 색을 그냥 평균하면 한쪽이 투명한 자리에서 <b>검은 테두리</b>가 생긴다
         (투명 픽셀의 RGB 가 0 이라 그 0 이 섞여 들어간다) → 색은 <b>알파로 가중</b>해 섞는다.
      ② 알파를 평균하면 <b>온몸이 반투명한 유령</b>이 나온다(실측 — 첫 시도에서 그렇게 나왔다).
         두 프레임은 다리 위치만 조금 다를 뿐 몸통은 같은 자리라, 알파는 <b>둘 중 진한 쪽</b>을
         쓰는 것이 맞다 → <c>maximum</c>. 그러면 실루엣이 불투명하게 유지되고,
         겹치지 않는 다리 끝만 살짝 두꺼워진다.
    """
    af = a[:, :, 3:4].astype(np.float64)
    bf = b[:, :, 3:4].astype(np.float64)
    wsum = af + bf

    rgb = np.where(wsum > 0,
                   (a[:, :, :3].astype(np.float64) * af +
                    b[:, :, :3].astype(np.float64) * bf) / np.maximum(wsum, 1e-6),
                   0.0)
    alpha = np.maximum(af, bf)

    return np.dstack([rgb, alpha]).clip(0, 255).astype(np.uint8)


def main():
    ensure_folder_meta(ART_ROOT)
    total = 0
    for sheet in SHEETS:
        total += build(sheet)
        print()
    print(f"프레임 {total}장 생성 → {ART_ROOT}")
    print("다음: py -3 Tools/gen_neutral_skins.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
