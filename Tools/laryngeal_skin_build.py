# -*- coding: utf-8 -*-
"""라린길(웨이브 최종보스 120004) 모션 시트 → 프레임 분해 (2026-08-19).

원본 — **두 장이다**
--------------------
``<볼트>/리소스/Laryngeal_asset_02.png``  (1536x1024) — <b>몸통 모션 전부</b>
``<볼트>/리소스/Laryngeal_asset.png``     (1536x1024) — <b>스킬2 화염 이펙트만</b>

두 장은 같은 기획 시트의 <b>두 판본</b>이다. 어느 한 장을 고르지 않고 <b>줄 단위로</b>
좋은 쪽을 쓴다:

* 몸통은 ``_02`` 가 정본이다 — 이동 라벨이 01~12 로 <b>빠짐없이</b> 찍혀 있고(첫 판본은
  07 이 없다) 「타오르는 숨결」이 <b>한 줄</b>로 정리돼 있다(첫 판본은 6-1 줄로 넘어간다).
* 화염 이펙트만 <b>첫 판본</b>을 쓴다 — 같은 그림인데 단계가 <b>5줄</b>이다(``_02`` 는 4줄).
  숨결은 시전 시간 2.5초에 걸쳐 <b>자라나는 연출</b>이라(아래 :func:`build_flame`) 단계가
  많을수록 부드럽다. 이펙트는 스킬 상자에 맞춰 따로 깔리므로 몸통과 <b>같은 판본일 필요가
  없다</b> — 시트가 갈려도 화면에서 어긋날 곳이 없다.

★★ <b>발밑 그림자를 먼저 지운다</b>
--------------------------------
``_02`` 시트가 <b>스스로 적어 놨다</b>: *"※ 모든 모션 이미지에 그림자(바닥 그림자)가
포함되어 있습니다."* 실제로 각 프레임 맨 아래에 회색 타원이 깔려 있다(대기 줄 기준
y 190 한 줄에만 407픽셀).

밝기만 보는 배경 판정으로는 이게 안 걸린다 — 카시노마에서 겪은 것과 <b>같은 사고</b>다
(113-7절). 그래서 같은 방법을 쓴다: <b>테두리에서 흘려 채워</b>(flood fill) 시트 바깥과
이어진 무채색·밝은 덩어리만 배경으로 확정한다. 몸 안의 이빨·뼈는 검은 외곽선에 둘러싸여
흐름이 닿지 못한다.

⚠ 카시노마와 <b>다른 점</b> — 여기서는 마스크를 따로 들고 다니지 않고 <b>원본 배열에
흰색을 칠해 버린다</b>(:func:`erase_ground_shadow`). 말파스 계열 파이프라인은 프레임마다
크기를 다시 재고(리샘플) 그 <b>뒤에</b> 알파를 만드는데, 마스크를 따로 두면 그 마스크도
같이 리샘플해야 해서 경계가 어긋난다. 원본에서 지워 두면 잉크 측정·경계 찾기·알파 만들기가
<b>전부</b> 그림자 없는 그림을 본다.

⚠ 흘려 채우기의 <b>씨앗을 시트 맨 가장자리에서 뿌리면 안 된다</b> — 이 두 장은 바깥에
<b>어두운 테두리 선</b>이 있어(모서리 픽셀 119,117,121) 맨 끝 줄에는 배경이 없다.
안쪽으로 :data:`SEED_INSET` 만큼 들어간 띠에서 뿌린다.

프레임을 어떻게 가르나 — 말파스와 같다(113-1절)
-----------------------------------------------
① <b>라벨</b>(프레임 번호)로 칸이 <b>몇 개</b>인지 센다.
② 경계는 그 근처에서 <b>잉크가 완전히 비는 열</b>로 옮긴다(:func:`snap_cells_to_gaps`).
③ 빈 열이 없으면(숨결 줄처럼 그림이 실제로 이어진 줄) <b>옮기지 않는다.</b>

⚠ <b>라벨은 회색조로만 찾는다</b>(카시노마와 같은 이유 · 102-3절). 스킬1 줄의 보라색
파동 고리가 라벨 줄 높이까지 올라와 있어서, 흰색과의 거리만으로 찾으면 <b>고리가 라벨에
붙어</b> 폭 50px 짜리 가짜 덩어리가 된다(실측). 채도로 거르면 숫자만 남는다.

★ <b>본체가 없는 칸을 「채도」로 가린다</b> (:data:`BODY_SAT_MAX`)
-----------------------------------------------------------------
「타오르는 숨결」 줄은 07~10번 칸이 <b>불꽃만</b> 있고 몸통이 없다. 말파스는 이런 칸을
<b>잉크 양</b>으로 걸렀는데(그쪽은 투사체가 작았다) 여기서는 그 방법이 <b>거꾸로 돈다</b> —
불꽃 칸의 잉크가 몸통 칸보다 <b>많아서</b> 몸통 쪽이 걸러진다.

라린길의 몸은 회색·자줏빛 살(채도 낮음)이고 불꽃은 <b>새빨갛다</b>(채도 높음). 그래서
"채도 낮은 잉크가 얼마나 있나"로 재면 둘이 깨끗하게 갈린다.

방향 — <b>줄마다 다르다</b>
---------------------------
⚠⚠ 113-6절의 교훈이 이 시트에서 <b>그대로 재현된다.</b> 확인 단위는 시트가 아니라 <b>행</b>이다:

  · 이동 · 근거리 공격 → <b>왼쪽</b> (걸어가는 쪽·발톱이 휘둘리는 쪽·핏자국이 튀는 쪽이 전부 왼쪽)
  · 스킬2 타오르는 숨결 → <b>오른쪽</b> (붉은 숨결이 오른쪽으로 뻗는다)
  · 대기 · 스킬1 아우성 · 방향 전환 → 정면 대칭 (아무 쪽이나 같다 — 원본을 오른쪽으로 둔다)

사용법:  python Tools/laryngeal_skin_build.py
다음:    python Tools/gen_laryngeal_skin.py
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT, find_art
# ★ 발을 피벗에 맞추는 공통 함수만 가져온다 — 이 스크립트는 자기 body_anchor 를 쓴다.
from skin_sheet import plant_feet

#: 몸통 모션 + 근접/아우성/히트 이펙트.
# ⚠ 2026-08-20 — 원본이 `리소스/` 에서 `리소스/sprites/` 로 옮겨졌다(다른 시트와 함께 정리된 듯).
SRC_MAIN = os.path.join(VAULT, "리소스", "sprites", "Laryngeal_asset_02.png")

#: 「타오르는 숨결」 화염만 — 이 판본이 <b>5단계</b>다(맨 위 주석).
SRC_FLAME = os.path.join(VAULT, "리소스", "sprites", "Laryngeal_asset.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Laryngeal", "Char")

#: 1픽셀당 유니티 단위. 게임 안 크기는 contentSizeTiles 로 정규화되므로(61·66절)
#: 이 값이 화면 크기를 정하지 않는다 — 다른 유닛과 같은 대역이면 된다.
PPU = 64

#: 배경(흰색)과 이만큼 떨어지면 그림으로 본다(세 채널 차이의 <b>합</b>).
#: 말파스와 같은 값 — 두 시트 모두 균일한 흰 배경 위의 안티에일리어싱된 그림이다.
ALPHA_LO = 60
ALPHA_HI = 180

#: 라벨(작은 회색 숫자) 판정 — <b>회색조로만</b> 찾는다(맨 위 주석).
LABEL_LUM = 130
LABEL_SAT = 40

#: 라벨 덩어리를 가르는 최소 빈 열. 두 자리 숫자 안쪽 간격은 3px, 라벨끼리는 70px 이상이다.
LABEL_GAP = 12

#: 라벨 덩어리의 최대 폭(px). 실측으로 진짜 라벨은 9~12px 다.
LABEL_MAX_W = 20

#: 칸 경계를 빈 열로 옮길 때 살펴보는 범위 — 칸 폭의 이 비율만큼 좌우로 본다(말파스와 같다).
GAP_SEARCH_RATIO = 0.45

#: 칸 경계를 <b>뚫고 지나가는 얇은 줄기</b>를 걷어낼 때의 두께 기준(말파스 113-4절).
#: 숨결 줄에서 불길이 옆 칸 몸통까지 이어져 있어 이게 필요하다.
EDGE_STREAK_RATIO = 0.35

#: 크기를 맞출 기준 모션 — 게임의 몸집 계산이 <b>대기 원화만</b> 재기 때문이다
#: (`CharacterSkinSO.contentSizeTiles` · `Tools/measure_skin_tiles.py` · 113-2절).
SCALE_REFERENCE_MOTION = "Idle"

#: 리샘플할 때 원본에서 더 떼어 오는 여백(px). ⚠ 칸 밖으로는 안 나간다.
RESAMPLE_MARGIN = 4

#: ★ <b>몸통 판정</b>(맨 위 주석) — 채도가 이 값 이하인 잉크만 「몸」으로 센다.
#:
#: ⚠ 라린길의 살은 잿빛·자줏빛(채도 20~50)이고 숨결 불꽃은 순색 빨강(채도 120 이상)이다.
#:   실측으로 몸통 칸은 이 기준의 잉크가 3000~5000px, 불꽃만 있는 칸은 400px 미만이다.
BODY_SAT_MAX = 70

#: 몸통 모션에서 <b>이 비율보다 몸이 적은</b> 칸은 버린다(그 모션의 최대 몸통량 대비).
MIN_BODY_AREA_RATIO = 0.35

# ──────────────────────────────────────────────────────────────────────────
# 발밑 드롭 섀도 판정 (맨 위 ★★). 카시노마와 같은 값이다 — 두 시트가 같은 톤이다.
# ⚠ 채도 상한을 올리면 붉은 잔광·핏자국까지 먹는다. 광도 하한을 내리면 몸의 어두운
#   회색 부위로 흐름이 새어 들어간다.
# ──────────────────────────────────────────────────────────────────────────
SHADOW_SAT_MAX = 20
SHADOW_LUM_MIN = 130

#: 흘려 채우기의 씨앗을 뿌릴 <b>안쪽 띠</b> 두께(px). 시트 맨 가장자리는 어두운 테두리
#: 선이라 거기서 뿌리면 씨앗이 하나도 안 잡힌다(맨 위 ⚠).
SEED_INSET = 8

# ──────────────────────────────────────────────────────────────────────────
# 시트 배치표 — **실측값이다** (잉크 밴드 + 라벨 탐지로 재고 눈으로 확인함).
#
#   (모션, x0, x1, 라벨 y0, 라벨 y1, 프레임 y0, 프레임 y1)
#
# ⚠ 근거리 공격과 방향 전환은 <b>같은 줄에 나란히</b> 있다 — x 로만 갈린다.
#   가운데 세로 구분선(x≈780)이 들어오면 모든 칸에 그 선이 조각으로 잡힌다.
# ⚠ 스킬1 의 프레임 위끝은 <b>라벨 바로 아래</b>(639)다 — 보라색 고리가 거기까지 올라온다.
# ──────────────────────────────────────────────────────────────────────────
BANDS = [
    ("Idle",          8, 1100,  52,  60,  64, 200),
    ("Move",          8, 1100, 253, 261, 265, 395),
    ("MeleeAttack",   8,  770, 455, 463, 468, 578),
    # ⚠ 「방향 전환」은 스킨에 받을 칸이 없다(좌우 반전으로 방향을 바꾼다 · 69-6절) —
    #   ``Unused_`` 로 두면 유니티 빌더가 조용히 건너뛴다.
    ("Unused_Turn", 790, 1100, 455, 463, 468, 578),
    ("Skill1",        8, 1100, 630, 638, 639, 765),
    ("Skill2",        8, 1100, 820, 828, 832, 952),
]

#: 파일 이름 접두사 — 다른 캐릭터와 같은 규약.
FILE_PREFIX = {
    "Idle": "Char_Idle",
    "Move": "Char_Move",
    "MeleeAttack": "Char_MeleeAttack",
    "Unused_Turn": "Char_Unused_Turn",
    "Skill1": "Char_Skill1",
    "Skill2": "Char_Skill2",
}

#: ★★ <b>원본이 왼쪽을 보고 있는 모션</b> — 맨 위 「방향」 절 참조.
SOURCE_FACES_LEFT = {"Move", "MeleeAttack"}

# ──────────────────────────────────────────────────────────────────────────
# 이펙트 — 오른쪽 단(x 1128~1508)에 네 묶음이 세로로 쌓여 있다.
#
# ⚠ <b>여기는 자동 분할을 쓰지 않는다.</b> 아우성 고리 사이의 빈 열이 7~13px 밖에 안 돼서,
#   조각을 붙이는 간격을 무엇으로 잡아도 <b>어느 한 쌍은 반드시 틀린다</b>(9면 고리3과
#   파동이 붙고, 11이면 고리2와 고리3이 붙는다 — 실측). 이럴 때는 임계값을 맞히려고
#   애쓰는 것보다 <b>시트에서 재서 적어 두는 것</b>이 맞다(말파스 BANDS 의 라벨 상한과
#   같은 판단).
#
#   (이름, 원본, y0, y1, [칸 x 범위…])
# ──────────────────────────────────────────────────────────────────────────
FX_CELLS = [
    # 근거리 공격 이펙트 — ⚠ 배선하지 않는다(스킨에 「평타 이펙트」 칸이 없다).
    # ★★ 2026-08-20 — 이제 <b>배선한다</b>. 유저 지시: *"라린길 돌진 이펙트를 투사체의
    #   형태로 만들어서 날아가는 모습 연출"*. 이 네 장이 시트의 「3. 근거리 공격 이펙트」이고
    #   <b>초승달 참격이 앞으로 뻗는</b> 그림이다. `CharacterSkinSO.meleeTravelFrames` 칸을
    #   새로 만들어 근접 평타에 실어 보낸다(그쪽 주석에 근거).
    ("MeleeTravelFx", "main", 104, 178, [(1140, 1205), (1206, 1315), (1325, 1440), (1441, 1508)]),

    # ★ 아우성 — 고리가 <b>커지는</b> 세 단계. 원형 스킬(반지름 5)의 범위 연출이다.
    ("Skill1Fx", "main", 277, 377, [(1128, 1193), (1194, 1286), (1287, 1385)]),

    # 아우성이 파동으로 흩어지는 그림 — ⚠ 배선하지 않는다(원형 범위에 가로로 긴 그림을
    # 깔면 상자 비율이 통째로 눕는다 · ResolveArea 는 <b>첫 장</b>으로 비율을 잡는다).
    ("Unused_ScreechWave", "main", 277, 377, [(1386, 1508)]),
    ("Unused_ScreechWave", "main", 399, 456, [(1128, 1508)]),

    # 숨결이 닿은 자리의 폭발 — ⚠ 배선하지 않는다. 보스 스킬에는 「착탄」 칸이 없고
    # (`impactFrames` 는 투사체 전용인데 라린길은 근접이라 투사체를 안 쓴다),
    # 범위 연출(skill2Fx)에 이어 붙이면 <b>캔버스가 세로로 커져</b> 불길이 그만큼 얇아진다.
    ("Unused_BreathHit", "main", 852, 975, [(1140, 1200), (1210, 1290), (1295, 1388), (1389, 1508)]),
]

#: ★ 「타오르는 숨결」 화염 — <b>첫 판본</b>의 다섯 줄. 한 줄이 곧 한 단계다.
#:   (y0, y1) · x 는 아래 :data:`FLAME_X`.
FLAME_ROWS = [(520, 551), (563, 604), (612, 661), (666, 724), (726, 807)]
FLAME_X = (1128, 1508)

#: 숨결의 <b>축</b>(세로 기준선)을 잴 때 왼쪽 끝에서부터 살펴보는 열 수 —
#: 입 폭발이 들어오는 만큼만 본다. 꼬리까지 보면 흩어진 불티가 축을 끌어내린다.
FLAME_AXIS_SCAN = 40

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
  alignment: 9
  spritePivot: {{x: 0.5, y: 0}}
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


# ---------------------------------------------------------------------------
# 발밑 그림자 지우기
# ---------------------------------------------------------------------------

def erase_ground_shadow(arr, bg):
    """
    <b>시트 테두리와 이어진</b> 무채색·밝은 덩어리를 배경색으로 칠한다 (맨 위 ★★).

    "밝고 무채색이면 배경" 으로 끝내면 몸 안의 이빨·뼈까지 지워진다. 그것들은 검은
    외곽선에 둘러싸여 <b>바깥과 이어져 있지 않다</b> — 그래서 이어짐을 따진다.

    scipy 는 쓰지 않는다(없는 PC 가 있다 · 113-7절). numpy 슬라이싱 전파로 같은 결과를 낸다.
    """
    lum = arr.mean(axis=2)
    sat = arr.max(axis=2) - arr.min(axis=2)
    ok = (sat <= SHADOW_SAT_MAX) & (lum >= SHADOW_LUM_MIN)

    # ⚠ 맨 가장자리가 아니라 <b>안쪽 띠</b>에서 씨앗을 뿌린다 (맨 위 ⚠).
    seen = np.zeros(ok.shape, dtype=bool)
    i = SEED_INSET
    seen[i, :] = ok[i, :]
    seen[-i - 1, :] |= ok[-i - 1, :]
    seen[:, i] |= ok[:, i]
    seen[:, -i - 1] |= ok[:, -i - 1]

    while True:
        grown = seen.copy()
        grown[1:, :] |= seen[:-1, :]
        grown[:-1, :] |= seen[1:, :]
        grown[:, 1:] |= seen[:, :-1]
        grown[:, :-1] |= seen[:, 1:]
        grown &= ok
        if np.array_equal(grown, seen):
            break
        seen = grown

    # 그림자였던 픽셀 수 = 이 마스크에서 "원래 배경이 아니었던" 것.
    was_ink = np.abs(arr.astype(int) - bg).sum(axis=2) > ALPHA_LO
    erased = int((seen & was_ink).sum())

    arr[seen] = bg
    return arr, erased


# ---------------------------------------------------------------------------
# 라벨 · 경계 찾기 (말파스와 같은 구조)
# ---------------------------------------------------------------------------

def bands(flags, gap=1, min_len=1):
    """True 가 이어지는 구간 목록. <paramref>gap</paramref> 이하로 떨어지면 하나로 본다."""
    out, run, hole = [], None, 0
    for i, v in enumerate(flags):
        if v:
            run = i if run is None else run
            hole = 0
        elif run is not None:
            hole += 1
            if hole > gap:
                if i - hole - run >= min_len:
                    out.append((run, i - hole - 1))
                run, hole = None, 0
    if run is not None and len(flags) - run >= min_len:
        out.append((run, len(flags) - 1))
    return out


def label_centers(gray, x0, x1, ly0, ly1):
    """라벨 행에서 프레임 번호 덩어리의 x 중심 목록. <b>칸 수의 유일한 근거</b>다."""
    strip = gray[ly0:ly1 + 1, x0:x1 + 1]
    found = bands(strip.any(axis=0), gap=LABEL_GAP, min_len=3)
    return [x0 + (a + b) // 2 for a, b in found if b - a + 1 <= LABEL_MAX_W]


def split_by_centers(centers, x0, x1):
    """라벨 중심 사이의 중점을 경계로. 양 끝은 반 칸씩 더 준다(균등 분할이 아니다)."""
    if not centers:
        return []
    if len(centers) == 1:
        return [(x0, x1)]

    pitch = int(round((centers[-1] - centers[0]) / (len(centers) - 1)))
    out = []
    for i, c in enumerate(centers):
        left = x0 if i == 0 else (centers[i - 1] + c) // 2
        right = x1 if i == len(centers) - 1 else (c + centers[i + 1]) // 2 - 1
        right = min(right, c + pitch // 2)
        left = max(left, c - pitch // 2)
        out.append((left, right))
    return out


def snap_cells_to_gaps(mask, cells, y0, y1):
    """
    라벨 중점으로 잡은 경계를 <b>잉크가 완전히 비는 열</b>로 옮긴다 (113-1절).

    ⚠ 빈 열이 하나도 없으면 <b>그대로 둔다</b> — 숨결처럼 그림이 실제로 이어진 줄까지
      억지로 자르면 연출이 토막 난다.
    """
    if len(cells) < 1:
        return cells

    x0, x1 = cells[0][0], cells[-1][1]
    ink = mask[y0:y1 + 1, x0:x1 + 1].sum(axis=0)

    def empty(x):
        return 0 <= x - x0 < len(ink) and ink[x - x0] == 0

    pitch = max(1, (x1 - x0 + 1) // max(1, len(cells)))
    win = max(2, int(round(pitch * GAP_SEARCH_RATIO)))

    cuts = []
    for i in range(len(cells) - 1):
        border = cells[i][1]
        best = None
        lo, hi = max(x0, border - win), min(x1, border + win)

        x = lo
        while x <= hi:
            if not empty(x):
                x += 1
                continue
            run = x
            while x + 1 <= hi and empty(x + 1):
                x += 1
            mid = (run + x) // 2
            if best is None or abs(mid - border) < abs(best - border):
                best = mid
            x += 1

        cuts.append(best if best is not None else border)

    left = cells[0][0]
    while left > x0 and not empty(left):
        left -= 1
    right = cells[-1][1]
    while right < x1 and not empty(right):
        right += 1

    out, start = [], left
    for c in cuts:
        out.append((start, c))
        start = c + 1
    out.append((start, right))
    return out


def trim_edge_streaks(mask, box, cell, y0, y1):
    """
    칸 경계를 <b>뚫고 지나가는 얇은 줄기</b>를 상자에서 걷어낸다 (113-4절).

    둘을 <b>다</b> 만족할 때만 걷어낸다: ① 잉크가 칸 맨 끝 열까지 닿아 있다 ② 그 끝에서
    안쪽으로 이어지는 열들이 얇다. 여기서는 숨결 불길이 옆 칸 몸통까지 이어진 경우다.
    """
    bx0, bx1, by0, by1 = box

    heights = np.zeros(bx1 - bx0 + 1, dtype=int)
    for i in range(bx1 - bx0 + 1):
        ys = np.where(mask[y0:y1 + 1, bx0 + i])[0]
        heights[i] = (ys.max() - ys.min() + 1) if len(ys) else 0

    thick = heights.max() * EDGE_STREAK_RATIO
    left, right = 0, len(heights) - 1

    if bx0 <= cell[0]:
        while left < right and 0 < heights[left] <= thick:
            left += 1
    if bx1 >= cell[1]:
        while right > left and 0 < heights[right] <= thick:
            right -= 1

    if left == 0 and right == len(heights) - 1:
        return box

    nx0, nx1 = bx0 + left, bx0 + right
    sub = mask[y0:y1 + 1, nx0:nx1 + 1]
    if not sub.any():
        return box
    ys = np.where(sub.any(axis=1))[0]
    return nx0, nx1, y0 + ys.min(), y0 + ys.max()


def body_anchor(rgba):
    """
    이 프레임에서 <b>몸통의 가로 중심</b>(px). 캔버스 정렬 기준이다 (113-4절).

    그림 전체 중심을 쓰면 불길·핏자국이 있는 프레임에서 몸통이 반대쪽으로 밀린다 —
    피벗이 캔버스 가로 한가운데(0.5)라 그만큼 보스가 옆으로 미끄러져 보인다.
    """
    solid = rgba[:, :, 3] > 0
    heights = np.zeros(rgba.shape[1], dtype=int)
    for i in range(rgba.shape[1]):
        ys = np.where(solid[:, i])[0]
        heights[i] = (ys.max() - ys.min() + 1) if len(ys) else 0

    if heights.max() <= 0:
        return rgba.shape[1] / 2.0

    thick = np.where(heights > heights.max() * EDGE_STREAK_RATIO)[0]
    if not len(thick):
        return rgba.shape[1] / 2.0
    return (thick.min() + thick.max() + 1) / 2.0


def median_height(items):
    """프레임들의 세로 <b>중앙값</b>(px). 평균을 쓰면 팔을 뻗은 한두 장이 배율을 끌고 간다."""
    hs = sorted(b[3] - b[2] + 1 for b, _cell, _y0, _y1 in items)
    return hs[len(hs) // 2] if hs else 0


def to_rgba(rgb_block, bg):
    """배경(흰색)과의 거리로 알파를 만든다. 가장자리만 부드럽게."""
    dist = np.abs(rgb_block.astype(int) - bg).sum(axis=2)
    alpha = np.clip((dist - ALPHA_LO) * 255.0 / (ALPHA_HI - ALPHA_LO), 0, 255)
    return np.dstack([rgb_block, alpha.astype(np.uint8)]).astype(np.uint8)


def render_frame(arr, bg, box, cell, y0, y1, factor):
    """
    프레임 한 장을 RGBA 로 굽는다. <b>RGB 를 먼저 리샘플하고 그 다음에 알파를 만든다</b> —
    RGBA 를 리샘플하면 알파 0 픽셀의 흰색이 가장자리로 번져 흰 테두리가 생긴다(113-2절).
    """
    bx0, bx1, by0, by1 = box
    ax0 = max(cell[0], bx0 - RESAMPLE_MARGIN)
    ax1 = min(cell[1], bx1 + RESAMPLE_MARGIN)
    ay0 = max(y0, by0 - RESAMPLE_MARGIN)
    ay1 = min(y1, by1 + RESAMPLE_MARGIN)

    block = arr[ay0:ay1 + 1, ax0:ax1 + 1].astype(np.uint8)
    if abs(factor - 1.0) > 0.002:
        img = Image.fromarray(block, "RGB")
        img = img.resize((max(1, int(round(img.width * factor))),
                          max(1, int(round(img.height * factor)))), Image.LANCZOS)
        block = np.asarray(img).astype(np.uint8)

    rgba = to_rgba(block, bg)

    solid = rgba[:, :, 3] > 0
    if not solid.any():
        return rgba
    ys = np.where(solid.any(axis=1))[0]
    xs = np.where(solid.any(axis=0))[0]
    return rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def write_png(img, folder, name):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".png")
    img.save(path)

    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    g = guid_for(rel)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=g, ppu=PPU, sprite_id=g[:32]))
    return path


def ensure_folder_meta(path):
    mp = path.rstrip("\\/") + ".meta"
    if os.path.exists(mp):
        return
    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    with open(mp, "w", encoding="utf-8", newline="\n") as f:
        f.write(FOLDER_META.format(guid=guid_for(rel)))


def boxes_for(mask, cells, y0, y1):
    """각 칸 안의 그림 경계 상자. 그림이 없는 칸은 None."""
    out = []
    for cx0, cx1 in cells:
        sub = mask[y0:y1 + 1, cx0:cx1 + 1]
        if not sub.any():
            out.append(None)
            continue
        ys = np.where(sub.any(axis=1))[0]
        xs = np.where(sub.any(axis=0))[0]
        out.append((cx0 + xs.min(), cx0 + xs.max(), y0 + ys.min(), y0 + ys.max()))
    return out


def load_sheet(path):
    """원본을 읽고 <b>발밑 그림자를 지운 뒤</b> 파생 마스크를 함께 돌려준다."""
    if not os.path.isfile(path):
        raise SystemExit("⚠ 원본이 없습니다: " + path)

    arr = np.asarray(Image.open(path).convert("RGB")).astype(np.uint8).copy()
    bg = np.array([254, 254, 254], dtype=int)

    arr, erased = erase_ground_shadow(arr, bg)
    print("  %s · 발밑 그림자 %d픽셀 제거" % (os.path.basename(path), erased))

    a = arr.astype(int)
    dist = np.abs(a - bg).sum(axis=2)
    lum = a.mean(axis=2)
    sat = a.max(axis=2) - a.min(axis=2)

    return {
        "arr": arr,
        "bg": bg,
        "mask": dist > ALPHA_LO,                       # 그림 경계용
        "gray": (lum < LABEL_LUM) & (sat < LABEL_SAT),  # 라벨용(회색조만)
        "body": (dist > ALPHA_LO) & (sat <= BODY_SAT_MAX),  # 몸통 판정용
    }


# ---------------------------------------------------------------------------

def build_bodies(sheet):
    """몸통 모션 여섯 줄."""
    arr, bg, mask = sheet["arr"], sheet["bg"], sheet["mask"]

    collected = {}
    for motion, x0, x1, ly0, ly1, y0, y1 in BANDS:
        centers = label_centers(sheet["gray"], x0, x1, ly0, ly1)
        rough = split_by_centers(centers, x0, x1)
        cells = snap_cells_to_gaps(mask, rough, y0, y1)
        moved = sum(1 for a, b in zip(rough, cells) if a != b)

        trimmed, found = 0, []
        for b, c in zip(boxes_for(mask, cells, y0, y1), cells):
            if b is None:
                continue
            t = trim_edge_streaks(mask, b, c, y0, y1)
            if t != b:
                trimmed += 1
            found.append((t, c, y0, y1))

        collected.setdefault(motion, []).extend(found)
        print("  %-12s 라벨 %2d개 → 칸 %2d개 (경계 %d칸 보정%s)"
              % (motion, len(centers), len(found), moved,
                 "" if trimmed == 0 else " · 지나가는 줄기 %d칸 정리" % trimmed))

    # ★ 몸통이 없는 칸을 <b>채도</b>로 가린다 (맨 위 ★ · 숨결 07~10번).
    for motion in list(collected):
        items = collected[motion]
        if not items:
            continue
        areas = [int(sheet["body"][b[2]:b[3] + 1, b[0]:b[1] + 1].sum())
                 for b, _c, _a, _z in items]
        biggest = max(areas) if areas else 0
        kept = [it for it, a in zip(items, areas) if a >= biggest * MIN_BODY_AREA_RATIO]
        if len(kept) != len(items):
            print("  %-12s 몸통 없는 칸 %d장 버림 (불꽃만 남은 프레임 · 몸통 잉크 %s)"
                  % (motion, len(items) - len(kept),
                     " ".join(str(a) for a in areas)))
            collected[motion] = kept

    reference = median_height(collected.get(SCALE_REFERENCE_MOTION, []))
    if reference <= 0:
        print("  ⚠ 기준 모션(%s)이 비어 있어 크기 정규화를 건너뜁니다" % SCALE_REFERENCE_MOTION)

    made = 0
    for motion, items in collected.items():
        if not items:
            print("  ⚠ %s: 프레임을 못 찾았습니다" % motion)
            continue

        own = median_height(items)
        factor = (reference / own) if (reference > 0 and own > 0) else 1.0

        frames = [render_frame(arr, bg, b, c, fy0, fy1, factor)
                  for b, c, fy0, fy1 in items]

        # ★ 가로 정렬은 <b>몸통 중심</b> 기준 · 좌우 여백을 <b>같게</b> (113-4절).
        anchors, _shift = plant_feet(frames, [body_anchor(f) for f in frames])
        pad = max(max(anchors), max(f.shape[1] - a for f, a in zip(frames, anchors)))
        w = int(np.ceil(pad * 2))
        h = max(f.shape[0] for f in frames)
        pad_left = w / 2.0

        folder = os.path.join(DST_ROOT, motion)
        prefix = FILE_PREFIX.get(motion, "Char_" + motion)

        for i, rgba in enumerate(frames):
            # 세로는 <b>바닥</b>을 맞춘다 — 피벗이 (0.5, 0) = 발밑이다.
            canvas = np.zeros((h, w, 4), dtype=np.uint8)
            bh, bw = rgba.shape[0], rgba.shape[1]
            ox = int(round(pad_left - anchors[i]))
            ox = max(0, min(ox, w - bw))
            canvas[h - bh:h, ox:ox + bw] = rgba

            drawn = Image.fromarray(canvas, "RGBA")
            flipped = drawn.transpose(Image.FLIP_LEFT_RIGHT)
            left, right = ((drawn, flipped) if motion in SOURCE_FACES_LEFT
                           else (flipped, drawn))
            write_png(right, folder, "%s_Right_%02d" % (prefix, i))
            write_png(left, folder, "%s_Left_%02d" % (prefix, i))
            made += 2

        ensure_folder_meta(folder)
        print("  %-12s %3d x %3d · %2d장 · 크기 x%.3f (세로 %d → %d) (원본 %s)"
              % (motion, w, h, len(frames), factor, own, int(round(own * factor)),
                 "←" if motion in SOURCE_FACES_LEFT else "→"))
    return made


def build_fx(sheets):
    """
    이펙트 묶음 — 묶음마다 캔버스를 따로 잡는다(가운데 정렬).

    ★ 2026-08-20 — 묶음마다 <b>자기 폴더</b>에 쓴다. 폴더 이름이 곧 스킨 칸이고
      `Editor/CharacterSkinBuilder.cs` 가 그 이름으로 배선한다(하드코딩 없이).
      예전에는 전부 `Fx/` 에 몰아넣고 파이썬이 YAML 로 배선했다.
    """
    made = 0
    counter = {}

    for name, src, y0, y1, cells in FX_CELLS:
        sheet = sheets[src]
        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
        if not boxes:
            print("  ⚠ Fx/%s: 그림을 못 찾았습니다 (y %d~%d)" % (name, y0, y1))
            continue

        w = max(b[1] - b[0] + 1 for b in boxes)
        h = max(b[3] - b[2] + 1 for b in boxes)
        n = counter.get(name, 0)

        for bx0, bx1, by0, by1 in boxes:
            rgba = to_rgba(sheet["arr"][by0:by1 + 1, bx0:bx1 + 1], sheet["bg"])
            canvas = np.zeros((h, w, 4), dtype=np.uint8)
            bw, bh = bx1 - bx0 + 1, by1 - by0 + 1
            canvas[(h - bh) // 2:(h - bh) // 2 + bh, (w - bw) // 2:(w - bw) // 2 + bw] = rgba
            folder = os.path.join(DST_ROOT, name)
            write_png(Image.fromarray(canvas, "RGBA"), folder,
                      "Char_%s_%02d" % (name, n))
            n += 1
            made += 1

        counter[name] = n
        ensure_folder_meta(os.path.join(DST_ROOT, name))
        print("  %-20s %3d x %3d · %2d장" % (name, w, h, len(boxes)))

    # 「타오르는 숨결」 화염 = 스킬 2 의 범위 연출.
    made += build_flame(sheets["flame"], os.path.join(DST_ROOT, "Skill2Fx"))
    ensure_folder_meta(os.path.join(DST_ROOT, "Skill2Fx"))
    return made


def build_flame(sheet, folder):
    """
    ★ 「타오르는 숨결」 — <b>자라나는 다섯 단계</b>로 엮는다.

    시트의 이 다섯 줄은 서로 다른 다섯 개의 불꽃이 아니라 <b>한 번의 숨결이 굵어지는
    다섯 단계</b>다. 줄마다 왼쪽에 <b>입(총구) 폭발</b>이 있고 거기서 불길이 오른쪽으로
    뻗는다 — 말파스의 저주광선과 같은 구조다(113-3절).

    ⚠ <b>다섯 장의 캔버스가 모두 같아야 한다.</b> `CombatProjectileFx.PlayArea` 는 배율을
      <b>첫 장</b>(`frames[0].bounds`)으로 잡아 스킬 상자에 맞춘다 — 장마다 캔버스가 다르면
      불길이 굵어지는 게 아니라 <b>상자를 넘나든다</b>.

    ⚠ <b>기준점은 「입이 시작되는 자리」다.</b> 처음에는 입 폭발을 <b>따로 잡아</b> 그
      한가운데를 맞추려 했는데, 1단계에서만 폭발이 불길과 빈 열로 떨어져 있고 2단계부터는
      <b>붙어 있어</b> 조각 나누기가 통째로 실패한다(그때 기준이 줄 전체의 한가운데가 되어
      1단계가 177px 오른쪽으로 밀려 나갔다 — 실제로 그렇게 나왔다). 폭발의 <b>가장 두꺼운
      열</b>을 찾는 방법도 안 된다: 폭발이 커질수록 그 열이 오른쪽으로 25px 움직인다.

      <b>왼쪽 끝</b>은 다섯 단계에서 1137·1135·1133·1132·1130 — 7px 안에서만 움직인다
      (캔버스 폭의 2%). 입이 자라며 조금씩 번지는 그 7px 이 곧 연출이므로 그대로 둔다.
      세로는 그 근처(:data:`FLAME_AXIS_SCAN` 열) 잉크의 한가운데 = <b>숨결의 축</b>이다.
    """
    arr, bg, mask = sheet["arr"], sheet["bg"], sheet["mask"]
    fx0, fx1 = FLAME_X

    pieces = []
    for y0, y1 in FLAME_ROWS:
        sub = mask[y0:y1 + 1, fx0:fx1 + 1]
        if not sub.any():
            print("  ⚠ Fx/Flame: y %d~%d 에 그림이 없습니다" % (y0, y1))
            continue
        xs = np.where(sub.any(axis=0))[0]
        ys = np.where(sub.any(axis=1))[0]

        head = sub[:, xs.min():xs.min() + FLAME_AXIS_SCAN]
        hy = np.where(head.any(axis=1))[0]
        pieces.append({
            "x0": fx0 + int(xs.min()), "x1": fx0 + int(xs.max()),
            "y0": y0 + int(ys.min()), "y1": y0 + int(ys.max()),
            "axis": y0 + (int(hy.min()) + int(hy.max())) // 2,
        })

    if not pieces:
        return 0

    # 기준점(왼쪽 끝 · 숨결 축)에서 사방으로 가장 먼 만큼이 곧 <b>공통 캔버스</b>다.
    right = max(p["x1"] - p["x0"] for p in pieces)
    up = max(p["axis"] - p["y0"] for p in pieces)
    down = max(p["y1"] - p["axis"] for p in pieces)
    left, w, h = 0, right + 1, up + down + 1

    made = 0
    for i, p in enumerate(pieces):
        rgba = to_rgba(arr[p["y0"]:p["y1"] + 1, p["x0"]:p["x1"] + 1], bg)
        canvas = np.zeros((h, w, 4), dtype=np.uint8)
        ox = left
        oy = up - (p["axis"] - p["y0"])
        canvas[oy:oy + rgba.shape[0], ox:ox + rgba.shape[1]] = rgba
        write_png(Image.fromarray(canvas, "RGBA"), folder, "Char_Skill2Fx_%02d" % i)
        made += 1

    print("  Fx/%-12s %3d x %3d · %2d장 (자라나는 숨결 · 입 기준 정렬)" % ("Flame", w, h, made))
    return made


#: ★ 스킨 에셋의 «값» 칸 — 원화만 봐서는 알 수 없는 것.
#:   유니티 빌더(`Editor/CharacterSkinBuilder.cs`)가 읽는다.
SKIN_SPEC = {
    "skinAssetName": "Skin_Laryngeal",
    # ⚠ 라린길은 <b>웨이브 보스</b>라 캐릭터 스킨 폴더가 아니다 — 종마다 폴더 하나다
    #   (`CharacterAnimator.PickRandomSkin` 이 폴더 안에서 무작위로 고르므로 한 폴더에
    #    몰아넣으면 다른 몬스터가 라린길 외형으로 나온다).
    "outputFolder": "Assets/_Project/Resources/MonsterSkins/Laryngeal",
    "displayName": "라린길",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "14",
    # ★ 근접 평타의 날아가는 참격을 가로 몇 타일로 그릴지 (2026-08-20).
    #   라린길 몸집이 11타일이라 1.6 이면 발톱 자국 정도로 보인다.
    "meleeTravelWidthTiles": "1.6",
}


def write_spec():
    """
    ★ 2026-08-20 — 스킨 에셋을 파이썬이 YAML 로 엮지 않는다. 유저 지시
    *"하드 코딩 최대한 자제하고 웬만한건 다 mcp로 직접 만들어줘"* 에 따라
    <b>유니티가 직접</b> 만들고(`Editor/CharacterSkinBuilder.cs` · MCP 메뉴),
    여기서는 원화만 봐서는 알 수 없는 값을 파일로 옆에 둔다.
    """
    from skin_sheet import SKIN_SPEC_NAME, write_skin_spec
    n = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/laryngeal_skin_build.py")
    print("  스킨 설정 %s (%d줄) — 유니티 빌더가 읽는다" % (SKIN_SPEC_NAME, n))


def clear_old_fx_folder():
    """
    ⚠ 예전 판이 이펙트를 전부 `Char/Fx/` 한 폴더에 몰아넣었다. 이제 칸마다 폴더가
    따로 생기므로 그 폴더는 <b>남아 있으면 안 된다</b> — 유니티 빌더가 «칸 이름이
    아닌 폴더» 라고 경고하고, 옛 프레임이 에셋 목록에 계속 뜬다.
    """
    import shutil
    old = os.path.join(DST_ROOT, "Fx")
    if os.path.isdir(old):
        n = sum(1 for _ in os.scandir(old))
        shutil.rmtree(old)
        meta = old + ".meta"
        if os.path.isfile(meta):
            os.remove(meta)
        print("  옛 Fx/ 폴더 삭제 (%d개 파일)" % n)


def main():
    print("원본 두 장을 읽습니다")
    clear_old_fx_folder()
    sheets = {
        "main": load_sheet(SRC_MAIN),
        "flame": load_sheet(SRC_FLAME),
    }

    print("\n몸통 모션")
    made = build_bodies(sheets["main"])

    print("\n이펙트")
    made += build_fx(sheets)

    write_spec()
    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("\n프레임 %d장 생성 → %s" % (made, DST_ROOT))
    print("다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
