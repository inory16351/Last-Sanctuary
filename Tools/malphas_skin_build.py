# -*- coding: utf-8 -*-
"""말파스(최종보스 120002) 모션 시트 → 프레임 분해 (2026-08-18).

원본
----
``<볼트>/리소스/Malphas_asset.png`` (1536x1024, 배경 흰색 254,254,254).
카르시노스와 달리 **한 장에 설명·일러스트·모션·이펙트가 전부 들어 있는 기획 시트**다.
왼쪽/오른쪽 두 단으로 나뉘고(세로 구분선 x≈634), 단마다 제목 막대 + 프레임 번호 라벨 +
프레임 행이 반복된다.

★ 카르시노스와 결정적으로 다른 점 — **빈 열 탐지로 프레임을 가를 수 없다**
--------------------------------------------------------------------
저주광선 행의 14~16번은 **레이저가 프레임 세 칸을 가로질러** 그려져 있고, 구속탄 행도
초록 구체가 다음 칸 몸통까지 닿는다. 즉 프레임 사이에 빈 열이 아예 없다.

대신 이 시트에는 **프레임 번호 라벨(01, 02 …)이 프레임마다 하나씩** 찍혀 있고
간격이 일정하다. 그래서 **라벨 덩어리의 x 중심으로 경계를 잡는다** — 라벨은 서로
절대 붙지 않으므로 이 방법은 레이저가 어떻게 뻗든 영향을 받지 않는다.
(히스톤이 겹친 프레임 때문에 자동 분리를 네 번 실패하고 손으로 잰 것과 같은 문제인데,
이 시트는 라벨이라는 더 나은 단서가 있다 — 84-1절)

⚠ 라벨이 프레임 수와 안 맞는 행이 있다
--------------------------------------
시트가 손으로 만들어져서 **번호가 건너뛴다**(이동 행에 13 없음 · 근거리 행에 08·11 없음).
그래서 "몇 장이어야 한다"를 강요하지 않고 **찾은 라벨 수만큼** 뽑는다 —
게임에서는 프레임 수가 몇 장이든 그대로 순환 재생되므로 문제가 없다.

방향
----
말파스는 **정면을 보는 좌우 대칭** 개체다. 다만 투사체·레이저가 **오른쪽으로** 나가므로
원본을 ``Right`` 로 보고 ``Left`` 를 좌우 반전으로 만든다.
⚠ 카르시노스는 반대다(원본이 왼쪽) — 새 원화마다 확인할 것.

⚠⚠ **한 시트 안에서도 행마다 방향이 다르다** (2026-08-19). 이동 행만 **왼쪽을 보는
그림**이라 위 규칙을 그대로 적용했더니 왼쪽으로 걸을 때 오른쪽 모션이 나왔다 —
:data:`SOURCE_FACES_LEFT` 참조. **행마다 확인할 것.**

────────────────────────────────────────────────────────────────────────────
2026-08-19 개정 — 유저 리포트 3건 (진행상황 113절)
────────────────────────────────────────────────────────────────────────────

**① "이동중에 다음 모션 덜 짤려서 다리 섞여 나온다"** → :func:`snap_cells_to_gaps`

라벨 중점만으로 자르면 경계가 **그림 한가운데**에 떨어진다. 실제로 이동 행의 칸
경계는 733/734 였는데 프레임 사이의 빈 열은 741~751 이라, 모든 칸이 **옆 프레임의
다리 끝을 8px 씩 물고** 있었다(칸 폭 105px 인데 몸통은 75px — 나머지가 남의 다리다).
그 조각이 알파 경계를 넓혀 몸통이 캔버스 한가운데에서 왼쪽으로 밀리기까지 했다.

고친 방식은 카시노마가 쓰던 것과 같다 — **라벨로 칸 수를 세고, 경계는 그 근처에서
잉크가 완전히 비는 열로 옮긴다.** ⚠ **빈 열이 없으면 옮기지 않는다.** 저주광선 행처럼
그림이 실제로 이어져 있는 줄까지 억지로 자르면 빔이 토막 난다 — 라벨 중점은 그런
줄에서는 여전히 유일한 근거다.

**② "원거리 공격 시 말파스가 작아진다"** → :data:`SCALE_REFERENCE_MOTION`

원인은 원화다. 이 시트는 **행마다 그린 크기가 다르다** — 대기 83px · 이동 76px ·
원거리 68px · 근접 62px · 스킬1 59px · 스킬2 55px · 피격 53px. 그런데 게임의 크기
기준(`CharacterSkinSO.contentSizeTiles`)은 **대기 원화 하나만** 재고(61·66절), 그
배율이 모든 모션에 그대로 곱해진다. 그래서 원거리 공격에 들어가는 순간 몸이 18%
작아졌다 — 코드가 아니라 **원화가 작았던 것**이다.

그래서 여기서 **모션마다 대기 크기에 맞춰 리샘플**한다. 배율은 그 모션 프레임들의
**세로 중앙값**으로 잰다(한두 장이 팔을 뻗어 커져도 흔들리지 않는다). ⚠ 대기가 기준인
이유는 `measure_skin_tiles.py` 가 대기만 재기 때문이다 — 기준을 바꾸려면 그쪽도 같이
바꿔야 한다.

**③ "레이저를 통짜 한 장 말고 하나하나 잘라서 점진적으로 발사되게"** → :func:`build_beam`

예전에는 저주광선 묶음을 **통짜 한 장**으로 뽑았다(맨 아래 ``FX_STRIPS`` 주석의 옛
설명). 칸으로 자르면 53px 짜리 토막 여덟 개가 나와 레이저처럼 안 보였기 때문인데,
그건 **자른 조각을 그대로 쓴다**는 전제에서만 맞는 말이었다.

시트를 다시 읽으면 이 줄은 **한 발이 자라나는 여덟 단계**다: 앞 칸들은 총구에서
커지는 섬광이고, 어느 칸부터 빔이 오른쪽으로 뻗어 마지막 칸의 착탄 폭발로 끝난다.
그래서 조각을 그대로 쓰지 않고 **여덟 장을 같은 캔버스에 왼쪽(총구) 기준으로 얹는다**:

    01~04  총구 섬광만 (점점 커진다)
    05~08  총구 + 빔 — 오른쪽으로 점점 길어지고 마지막에 착탄 폭발

`CombatProjectileFx.PlayArea` 는 **프레임을 수명 전체에 고르게 펼치고** 캔버스를 스킬
상자(10 x 2 타일)에 늘려 깐다. 캔버스가 여덟 장 모두 같으므로 상자 안에서 빔이
**자라나는 것처럼** 보인다. ⚠ 캔버스 크기가 장마다 다르면 배율이 첫 장 기준이라
(그 함수는 `frames[0].bounds` 로 배율을 잡는다) 빔이 길어지는 게 아니라 뚱뚱해진다.

**④ 이동 중 방향**은 원화가 아니라 코드다 — `CharacterAnimator.UpdateFacing` 에서 고쳤다.

사용법:  python Tools/malphas_skin_build.py
다음:    python Tools/gen_malphas_skin.py
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT, find_art
# ★ 발을 피벗에 맞추는 공통 함수만 가져온다 — 이 스크립트는 자기 body_anchor 를 쓴다.
from skin_sheet import plant_feet

SRC = find_art("Malphas_asset.png")
DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Malphas", "Char")

#: 1픽셀당 유니티 단위. 게임 안 크기는 contentSizeTiles 로 정규화되므로(61·66절)
#: 이 값이 화면 크기를 정하지 않는다 — 다른 유닛과 같은 대역이면 된다.
PPU = 64

#: 배경과 이만큼 떨어지면 그림으로 본다(세 채널 차이의 <b>합</b>). 가장자리는 두 값 사이에서
#: 부드럽게 뺀다.
#:
#: ⚠ 카르시노스(24/60)보다 <b>훨씬 높다.</b> 그 값으로 뽑아 보니 촉수 둘레에 <b>흰 테두리</b>가
#:   남았다 — 원본이 안티에일리어싱된 흰 배경 위에 있어서 (230,230,230) 같은 가장자리 픽셀의
#:   거리가 72 라 24/60 기준으로는 <b>완전 불투명</b>이 된다. 60/180 으로 올리면 그 픽셀은
#:   알파 25 로 거의 사라지고, 진짜 밝은 부분(뼈 크림색 213,170,131 → 거리 248)은 그대로 남는다.
ALPHA_LO = 60
ALPHA_HI = 180

#: 라벨(작은 검은 숫자)을 찾을 때 쓰는 임계값. 알파용보다 높다 — 옅은 잔광을 라벨로
#: 오인하면 프레임 경계가 통째로 어긋난다.
LABEL_THRESHOLD = 90

#: 라벨 글자 높이(px). 라벨 행은 이 높이 안에서 전부 끝난다.
LABEL_H = 14

#: 라벨 덩어리를 가르는 최소 빈 열. 숫자 두 자리('1'과 '2') 사이는 2~3px 이고
#: 라벨끼리는 40px 이상 떨어져 있다 — 그 사이 값이면 어디든 된다.
LABEL_GAP = 12

#: 라벨 덩어리의 최대 폭(px). ⚠ <b>이게 없으면 안 된다</b> — 저주광선 행에서 레이저 착탄
#: 폭발이 라벨 줄까지 세로로 걸쳐서 <b>아홉 번째 라벨</b>로 잡혔고, 그 결과 프레임이 하나
#: 더 생겼다. 두 자리 숫자는 22px 안쪽이라 34 면 넉넉하다.
LABEL_MAX_W = 34

#: 몸통 모션에서 <b>이 비율보다 그림이 적은</b> 프레임은 버린다(그 모션의 최대 잉크량 대비).
#: 원거리 공격 08·16번, 저주광선 마지막 칸처럼 <b>투사체·빔만 남고 본체가 없는</b> 칸이
#: 실제로 있는데, 그대로 두면 재생 중에 보스가 한 프레임 사라진다.
#: 투사체·빔은 따로 뽑으므로(FX_STRIPS) 버려도 손해가 없다.
#:
#: ⚠ 높이 비율이 아니라 <b>잉크 픽셀 수</b>로 잰다 — 저주광선 마지막 칸은 빔 착탄이
#:   위아래로 퍼져서 <b>높이는 본체만큼 크다.</b> 높이로 재면 안 걸러진다(실제로 그랬다).
MIN_BODY_AREA_RATIO = 0.35

#: 칸 경계를 **빈 열**로 옮길 때 살펴보는 범위 — 칸 폭(pitch)의 이 비율만큼 좌우로 본다.
#:
#: 0.45 면 이웃 라벨의 중점까지는 절대 못 넘어간다(중점끼리의 거리가 곧 pitch 다).
#: 너무 좁게 잡으면(0.1) 이동 행의 진짜 빈 열 741~751 을 놓쳐 예전처럼 다리가 섞이고,
#: 너무 넓게 잡으면 두 칸 건너의 빈 열로 도망가 프레임이 통째로 밀린다.
GAP_SEARCH_RATIO = 0.45

#: 칸 <b>경계를 뚫고 지나가는 얇은 줄기</b>를 몸통 프레임에서 걷어낼 때의 두께 기준 —
#: 그 칸에서 가장 두꺼운 열의 이 비율보다 얇으면 "지나가는 줄기"로 본다.
#:
#: ★ 저주광선 시전 행(스킬2 둘째 줄 14~16번)이 이 경우다. 원화가 <b>레이저 한 줄을 세 칸에
#: 걸쳐</b> 그려놨다 — 프레임 사이에 빈 열이 없어 :func:`snap_cells_to_gaps` 도 손을 못 댄다.
#: 그대로 두면 ① 캔버스가 158px 로 부풀어 몸통이 가운데에서 밀리고 ② `skill2Fx` 가 그리는
#: <b>진짜 빔과 겹쳐</b> 레이저가 두 줄로 보인다.
#:
#: ⚠ **칸 경계에 잉크가 닿아 있을 때만** 걷어낸다. 경계가 빈 열에 떨어진 칸(이동·대기 등
#: 대부분)은 그림이 그 안에서 끝난 것이므로 촉수 끝을 잘라내면 안 된다.
EDGE_STREAK_RATIO = 0.35

#: 크기를 맞출 **기준 모션**. 게임의 몸집 계산이 대기 원화만 재기 때문이다
#: (`CharacterSkinSO.contentSizeTiles` · `Tools/measure_skin_tiles.py`).
SCALE_REFERENCE_MOTION = "Idle"

#: 리샘플할 때 원본에서 더 떼어 오는 여백(px). 잘린 면에서 LANCZOS 가 가장자리 픽셀을
#: 복제하며 생기는 얇은 띠를 없앤다. ⚠ **칸 밖으로는 절대 안 나간다**(옆 프레임을 물면
#: ① 번 문제가 되살아난다) — 칸 안의 여백은 어차피 배경이라 새로 들어오는 잉크가 없다.
RESAMPLE_MARGIN = 4

# ──────────────────────────────────────────────────────────────────────────
# 시트 배치표 — **실측값이다.** (세로 잉크 밴드 + 라벨 행 탐지로 재고 눈으로 확인함)
#
#   (모션, x0, x1, 라벨행 y, 프레임 y0, 프레임 y1, 라벨 상한)
#
# 같은 모션이 여러 줄이면 여기 여러 번 나온다 — 위에서 아래 순서가 곧 프레임 순서다.
# ⚠ x 범위는 <b>단 안쪽</b>으로 잡는다. 가운데 세로 구분선(x 634~635)이 들어오면
#   모든 행에서 그 선이 프레임 조각으로 잡힌다.
#
# <b>라벨 상한</b>(마지막 칸) — 0 이면 제한 없음. 저주광선 둘째 줄에서만 필요하다:
# 레이저 <b>착탄 폭발</b>이 라벨 줄 높이까지 걸쳐서 아홉 번째 라벨로 잡힌다. 폭·잉크량
# 어느 쪽으로도 안정적으로 못 거른다(폭발은 두 자리 숫자만큼 좁고 본체의 43%나 된다).
# 이럴 때는 "이 줄은 여덟 칸"이라는 <b>시트에 적힌 사실</b>을 그대로 쓰는 것이 맞다 —
# 임계값을 맞히려고 다른 줄까지 위태롭게 만들지 않는다.
# ──────────────────────────────────────────────────────────────────────────
BANDS = [
    # ── 오른쪽 단 ────────────────────────────────────────────────────
    ("Idle",         640, 1533,  36,  58, 146, 0),
    ("Move",         640, 1533, 188, 206, 287, 0),
    ("Move",         640, 1533, 300, 309, 391, 0),
    ("Skill1",       640, 1533, 423, 439, 505, 0),
    ("Skill1",       640, 1533, 504, 525, 590, 0),
    ("Skill2",       640, 1533, 622, 636, 698, 0),
    ("Skill2",       640, 1533, 697, 718, 779, 8),   # ← 레이저 착탄이 9번째 라벨로 잡힌다
    ("Hit",          640, 1533, 805, 821, 882, 0),
    # ── 왼쪽 단 ─────────────────────────────────────────────────────
    ("RangedAttack",   4,  630, 272, 289, 363, 0),
    ("RangedAttack",   4,  630, 377, 384, 458, 0),
    ("MeleeAttack",    4,  630, 489, 506, 578, 0),
    ("MeleeAttack",    4,  630, 587, 596, 665, 0),
    ("FxBindingOrb",   4,  630, 696, 718, 779, 0),
]

#: 파일 이름 접두사 — 다른 캐릭터와 같은 규약.
FILE_PREFIX = {
    "Idle": "Char_Idle",
    "Move": "Char_Move",
    "MeleeAttack": "Char_MeleeAttack",
    "RangedAttack": "Char_RangedAttack",
    "Skill1": "Char_Skill1",
    "Skill2": "Char_Skill2",
    "Hit": "Char_Hit",
}

#: 방향(좌/우) 두 벌을 만들지 않는 모션. 이펙트·투사체는 조준 각도만큼 통째로
#: 회전시켜 깔리므로(`CombatProjectileFx`) 방향별 원화를 넣으면 두 번 돌아간다.
NO_FACING = {"FxBindingOrb"}

#: ★★ <b>원본이 왼쪽을 보고 있는 모션</b> (2026-08-19 · 유저 리포트:
#: *"말파스가 왼쪽을 가고있는데 오른쪽으로 움직이는 모션을 취하고있어"*).
#:
#: 맨 위 「방향」 절은 이 시트 전체가 오른쪽을 본다고 적어 뒀는데, **이동 행만 반대다.**
#: 근거는 그림 자체다 — 이동 프레임은 <b>몸이 왼쪽으로 기울고 촉수가 오른쪽으로 흘러</b>
#: 뒤로 끌린다. 즉 <b>왼쪽으로 가는 그림</b>이다. 반면 원거리·스킬1·스킬2·근접은 구체·
#: 레이저·촉수 채찍이 전부 <b>오른쪽</b>으로 나가므로 오른쪽을 본다.
#:
#: 대기는 좌우 대칭이라 어느 쪽으로 넣어도 같고, 피격은 스킨에 배선돼 있지 않다.
#:
#: ⚠ <b>새 원화를 받으면 행마다 확인할 것.</b> 한 시트 안에서도 방향이 갈린다는 사실이
#:   여기서 처음 확인됐다 — 카시노마는 시트 전체가 왼쪽이었다(102-3절).
SOURCE_FACES_LEFT = {"Move"}

# ──────────────────────────────────────────────────────────────────────────
# 맨 아래 「투사체 / 이펙트」 줄 — 네 묶음이 가로로 나란히 있다.
# 여기는 라벨이 아니라 <b>빈 열</b>로 갈라도 된다(각 조각이 완전히 떨어져 있다).
#   ⚠ 저주광선만 예외 — 레이저가 이어져 보이지만 실제로는 조각마다 끊겨 있다.
#     안 갈라지면 라벨 방식으로 떨어뜨린다(아래 count 를 근거로).
# ──────────────────────────────────────────────────────────────────────────
FX_LABEL_Y = 944
FX_ROW = (957, 1006)

#: (이름, x 범위, 기대 프레임 수, 뽑는 방식)
#:
#: 방식은 세 가지다:
#:   ``"cut"``   칸마다 따로 뽑는다 — 조각들이 서로 떨어져 있는 보통의 묶음.
#:   ``"beam"``  ★ <b>자라나는 한 발</b>로 엮는다 (:func:`build_beam`).
#:
#: ⚠ <b>저주광선을 예전에는 「통짜 한 장」으로 뽑았다</b>(2026-08-18). 칸으로 자르면
#:   53px 짜리 토막 여덟 개가 나와 레이저로 안 보였기 때문인데, 그건 <b>조각을 그대로
#:   쓴다</b>는 전제에서만 맞는 말이었다. 조각을 <b>같은 캔버스에 총구 기준으로 누적해
#:   얹으면</b> 여덟 장이 그대로 "점점 뻗는 빔"이 된다 — 맨 위 주석 ③ 참조.
FX_STRIPS = [
    ("Projectile", (12, 402), 8, "cut"),      # 기본 원거리 투사체 (검은 구체)
    ("BindingOrb", (408, 766), 7, "cut"),     # 구속탄 투사체 (초록 구체 · 시트에 05번이 없다)
    ("CurseBeam", (772, 1230), 8, "beam"),    # 저주광선 (레이저) — 자라나는 여덟 단계
    ("Impact", (1234, 1533), 4, "cut"),       # 레이저 임팩트
]

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


def bands(flags, gap=1, min_len=1):
    """
    True 가 이어지는 구간 목록 [(시작, 끝)]. <paramref>gap</paramref> 픽셀 이하로 떨어진
    구간은 하나로 본다 — 숫자 두 자리를 한 라벨로 묶는 데 쓴다.
    """
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


def label_centers(dark, x0, x1, label_y):
    """
    라벨 행에서 프레임 번호 덩어리의 x 중심 목록. **프레임 경계의 유일한 근거**다.
    """
    strip = dark[label_y:label_y + LABEL_H, x0:x1 + 1]
    hit = strip.any(axis=0)
    found = bands(hit, gap=LABEL_GAP, min_len=3)
    return [x0 + (a + b) // 2 for a, b in found if b - a + 1 <= LABEL_MAX_W]


def split_by_centers(centers, x0, x1):
    """
    라벨 중심 사이의 <b>중점</b>을 경계로 삼는다. 양 끝은 반 칸씩 더 준다.

    ⚠ 균등 분할이 아니다 — 마지막 줄처럼 프레임이 몇 장 없는 행에서도
      칸 폭이 앞줄과 같게 유지된다(균등 분할하면 3장짜리 줄에서 칸이 5배로 넓어진다).
    """
    if not centers:
        return []
    if len(centers) == 1:
        return [(x0, x1)]

    pitch = int(round((centers[-1] - centers[0]) / (len(centers) - 1)))
    out = []
    for i, c in enumerate(centers):
        left = x0 if i == 0 else (centers[i - 1] + c) // 2
        right = x1 if i == len(centers) - 1 else (c + centers[i + 1]) // 2 - 1
        # 마지막 칸이 단 끝까지 늘어나 옆 이펙트를 삼키지 않게 한 칸 폭으로 제한한다.
        right = min(right, c + pitch // 2)
        left = max(left, c - pitch // 2)
        out.append((left, right))
    return out


def snap_cells_to_gaps(mask, cells, y0, y1):
    """
    라벨 중점으로 잡은 칸 경계를 **잉크가 완전히 비는 열**로 옮긴다.

    라벨은 "프레임이 몇 개인지"의 유일한 근거지만(맨 위 주석), **어디서 잘라야 하는지**의
    근거는 아니다. 라벨이 그림 중심과 조금씩 어긋나 있어서 중점으로 자르면 경계가
    옆 프레임의 다리 위에 떨어진다 — 유저 리포트 ①.

    규칙:
      · 칸 사이 경계는 ±pitch x :data:`GAP_SEARCH_RATIO` 안에서 **빈 열 구간**을 찾아
        그 <b>한가운데</b>로 옮긴다. 여러 개면 원래 경계에 가장 가까운 것.
      · ⚠ **빈 열이 하나도 없으면 그대로 둔다.** 그림이 실제로 이어진 줄(저주광선)까지
        억지로 자르면 연출이 토막 난다.
      · 양 끝은 <b>잉크가 이어지는 동안 바깥으로 넓힌다</b> — 라벨 중점 방식이 마지막
        칸을 반 칸으로 잘라 프레임 끝이 날아가던 것(이동 행 8번이 10px 잘렸다)을 막는다.
        빈 열을 만나면 멈추므로 옆 묶음을 삼키지 않는다.
    """
    if len(cells) < 1:
        return cells

    x0, x1 = cells[0][0], cells[-1][1]
    ink = mask[y0:y1 + 1, x0:x1 + 1].sum(axis=0)      # 0 이면 그 열은 완전히 비어 있다

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

    # 양 끝 — 잉크가 이어지는 만큼만 넓힌다.
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
    칸 경계를 **뚫고 지나가는 얇은 줄기**를 상자에서 걷어낸다 (:data:`EDGE_STREAK_RATIO`).

    판단 근거는 두 가지를 <b>둘 다</b> 만족할 때뿐이다:
      ① 잉크가 칸의 <b>맨 끝 열까지 닿아 있다</b> — 그림이 이 칸에서 끝나지 않았다는 뜻.
      ② 그 끝에서 안쪽으로 이어지는 열들이 <b>얇다</b> — 몸통이 아니라 지나가는 줄기다.

    두 번째만 보면 촉수 끝·지팡이 구슬처럼 원래 얇은 그림까지 잘린다. 첫 번째만 보면
    경계가 몸통 한가운데에 떨어진 칸을 통째로 지운다.
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
        return box                       # 다 지워질 판이면 손대지 않는다
    ys = np.where(sub.any(axis=1))[0]
    return nx0, nx1, y0 + ys.min(), y0 + ys.max()


def body_anchor(rgba):
    """
    이 프레임에서 <b>몸통의 가로 중심</b>(프레임 왼쪽 끝 기준 px). 캔버스 정렬 기준이다.

    ★ <b>왜 그림 전체의 중심이 아닌가</b> — 시전 프레임에는 몸통 말고도 <b>날아가는 구슬
    (구속탄)·뻗어 나가는 레이저</b>가 같이 그려져 있다. 그림 전체를 가운데에 맞추면
    그 부속이 나타나는 프레임에서 <b>몸통이 반대쪽으로 밀린다</b> — 재생 중에 보스가
    옆으로 훌쩍 뛰는 것처럼 보이는 것의 정체가 이것이다.

    몸통은 <b>세로로 두꺼운 열</b>이고 부속은 얇다(:data:`EDGE_STREAK_RATIO`). 그래서
    두꺼운 열만 모아 그 한가운데를 잡는다. 부속이 없는 프레임에서는 그림 전체 중심과
    같은 값이 나오므로 예전 동작과 어긋나지 않는다.
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
    """
    모아둔 프레임들의 세로 <b>중앙값</b>(px). 크기 정규화의 기준이다.

    평균이 아니라 중앙값인 이유: 한 모션 안에서도 팔을 뻗거나 촉수가 튀는 프레임이
    한두 장 섞여 있다. 평균을 쓰면 그 한 장이 모션 전체의 배율을 끌고 간다.
    """
    hs = sorted(b[3] - b[2] + 1 for b, _cell, _y0, _y1 in items)
    return hs[len(hs) // 2] if hs else 0


def render_frame(arr, bg, box, cell, y0, y1, factor):
    """
    프레임 한 장을 RGBA 로 굽는다. <paramref name="factor"/> 가 1 이 아니면 **먼저 원본
    RGB 를 리샘플한 뒤** 알파를 만든다 — 순서가 중요하다.

    RGBA 를 리샘플하면 알파 0 인 픽셀의 <b>색</b>(여기서는 흰 배경)이 가장자리로 번져
    촉수 둘레에 흰 테두리가 생긴다. 배경이 균일한 흰색이므로 <b>RGB 를 먼저 늘리고
    그 다음에 배경과의 거리로 알파를 만들면</b> 번짐이 원리적으로 없다.
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

    # 여백(RESAMPLE_MARGIN)과 리샘플이 남긴 반투명 띠를 다시 잘라낸다.
    solid = rgba[:, :, 3] > 0
    if not solid.any():
        return rgba
    ys = np.where(solid.any(axis=1))[0]
    xs = np.where(solid.any(axis=0))[0]
    return rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def to_rgba(rgb_block, bg):
    """배경(흰색)과의 거리로 알파를 만든다. 가장자리만 부드럽게."""
    dist = np.abs(rgb_block.astype(int) - bg).sum(axis=2)
    alpha = np.clip((dist - ALPHA_LO) * 255.0 / (ALPHA_HI - ALPHA_LO), 0, 255)
    return np.dstack([rgb_block, alpha.astype(np.uint8)]).astype(np.uint8)


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


def main():
    if not os.path.isfile(SRC):
        print("⚠ 원본이 없습니다:", SRC)
        return 1

    im = Image.open(SRC).convert("RGB")
    arr = np.asarray(im).astype(np.uint8)
    bg = arr[0, 0].astype(int)
    dist = np.abs(arr.astype(int) - bg).sum(axis=2)

    mask = dist > ALPHA_LO              # 그림 경계를 재는 데 쓴다
    dark = dist > LABEL_THRESHOLD       # 라벨 글자를 찾는 데만 쓴다

    print("원본 %dx%d · 배경 %s" % (im.size[0], im.size[1], tuple(bg)))

    # ── 1) 모션 행 ────────────────────────────────────────────────────
    #    같은 모션의 여러 줄을 <b>한 캔버스 크기</b>로 맞춰야 재생 중에 안 튄다 —
    #    그래서 먼저 전부 모아 상자를 재고, 그 다음에 쓴다.
    #    상자와 함께 <b>그 상자가 속한 칸</b>도 들고 다닌다 — 리샘플할 때 여백을
    #    칸 밖으로 넘기지 않으려면 필요하다(:func:`render_frame`).
    collected = {}
    for motion, x0, x1, ly, y0, y1, limit in BANDS:
        centers = label_centers(dark, x0, x1, ly)
        if limit and len(centers) > limit:
            centers = centers[:limit]      # 남는 것은 항상 오른쪽 끝의 연출이다
        rough = split_by_centers(centers, x0, x1)
        cells = snap_cells_to_gaps(mask, rough, y0, y1)
        moved = sum(1 for a, b in zip(rough, cells) if a != b)
        boxes = boxes_for(mask, cells, y0, y1)

        # 칸을 뚫고 지나가는 얇은 줄기(저주광선)를 걷어낸다.
        trimmed = 0
        found = []
        for b, c in zip(boxes, cells):
            if b is None:
                continue
            t = trim_edge_streaks(mask, b, c, y0, y1)
            if t != b:
                trimmed += 1
            found.append((t, c, y0, y1))

        collected.setdefault(motion, []).extend(found)
        print("  %-13s y %4d~%-4d · 라벨 %2d개 → 프레임 %2d장 (경계 %d칸 보정%s)"
              % (motion, y0, y1, len(centers), len(found), moved,
                 "" if trimmed == 0 else " · 지나가는 줄기 %d칸 제거" % trimmed))

    # ★ 본체가 없는 칸을 버린다 (MIN_BODY_AREA_RATIO 주석 참조).
    #   이펙트 묶음은 원래 본체가 없으므로 거르지 않는다.
    for motion in list(collected):
        items = collected[motion]
        if not items or motion in NO_FACING:
            continue
        areas = [int(mask[b[2]:b[3] + 1, b[0]:b[1] + 1].sum()) for b, _c, _a, _z in items]
        biggest = max(areas) if areas else 0
        kept = [it for it, a in zip(items, areas) if a >= biggest * MIN_BODY_AREA_RATIO]
        if len(kept) != len(items):
            print("  %-13s 본체 없는 칸 %d장 버림 (투사체만 남은 프레임)"
                  % (motion, len(items) - len(kept)))
            collected[motion] = kept

    # ★ 모션마다 그린 크기가 다르다 → 대기 크기에 맞춰 리샘플한다 (유저 리포트 ②).
    #   기준은 프레임 <b>세로의 중앙값</b> — 한두 장이 팔을 뻗어도 흔들리지 않는다.
    reference = median_height(collected.get(SCALE_REFERENCE_MOTION, []))
    if reference <= 0:
        print("  ⚠ 기준 모션(%s)이 비어 있어 크기 정규화를 건너뜁니다" % SCALE_REFERENCE_MOTION)

    made = 0
    for motion, items in collected.items():
        if not items:
            print("  ⚠ %s: 프레임을 못 찾았습니다" % motion)
            continue

        # 이펙트(FxBindingOrb)는 몸통이 아니라 <b>범위 연출</b>이라 몸집 기준이 없다 — 손대지 않는다.
        own = median_height(items)
        factor = (reference / own) if (reference > 0 and own > 0
                                       and motion not in NO_FACING) else 1.0

        frames = [render_frame(arr, bg, b, c, fy0, fy1, factor)
                  for b, c, fy0, fy1 in items]

        # ★ 가로 정렬은 <b>몸통 중심</b> 기준이다 (:func:`body_anchor`) — 구슬·레이저가
        #   나타나는 프레임에서 몸통이 옆으로 밀리지 않게. 캔버스는 그 기준점의 좌·우로
        #   가장 멀리 뻗은 만큼을 합친 크기라 어느 프레임도 잘리지 않는다.
        #   ⚠ 좌·우 여백을 <b>같게</b> 준다. 스프라이트 피벗이 (0.5, 0) = 캔버스 가로
        #     한가운데라, 여백이 한쪽만 넓으면 <b>몸통이 피벗에서 비켜난다</b> —
        #     대기 → 원거리로 넘어가는 순간 보스가 옆으로 미끄러진다(구슬이 오른쪽에만
        #     그려져 있어 실제로 10px 어긋나 있었다). 남는 쪽은 투명 여백일 뿐이다.
        anchors, _shift = plant_feet(frames, [body_anchor(f) for f in frames])
        pad = max(max(anchors),
                  max(f.shape[1] - a for f, a in zip(frames, anchors)))
        w = int(np.ceil(pad * 2))
        h = max(f.shape[0] for f in frames)
        pad_left = w / 2.0

        folder = os.path.join(DST_ROOT, motion)
        prefix = FILE_PREFIX.get(motion, "Char_" + motion)

        for i, rgba in enumerate(frames):
            # 세로는 <b>바닥</b>을 맞춘다 — 피벗이 (0.5, 0) = 발밑이라 바닥을 맞춰야
            # 모션 전환에서 위아래로 안 튄다.
            canvas = np.zeros((h, w, 4), dtype=np.uint8)
            bh, bw = rgba.shape[0], rgba.shape[1]
            ox = int(round(pad_left - anchors[i]))
            ox = max(0, min(ox, w - bw))
            oy = h - bh
            canvas[oy:oy + bh, ox:ox + bw] = rgba

            drawn = Image.fromarray(canvas, "RGBA")
            if motion in NO_FACING:
                write_png(drawn, folder, "%s_%02d" % (prefix, i))
                made += 1
            else:
                # 원본이 어느 쪽을 보는지에 따라 어느 벌이 원본인지가 갈린다
                # (:data:`SOURCE_FACES_LEFT` — 이동 행만 왼쪽을 본다).
                flipped = drawn.transpose(Image.FLIP_LEFT_RIGHT)
                left, right = ((drawn, flipped) if motion in SOURCE_FACES_LEFT
                               else (flipped, drawn))
                write_png(right, folder, "%s_Right_%02d" % (prefix, i))
                write_png(left, folder, "%s_Left_%02d" % (prefix, i))
                made += 2

        ensure_folder_meta(folder)
        facing = ("" if motion in NO_FACING
                  else " (원본 ←)" if motion in SOURCE_FACES_LEFT
                  else " (원본 →)")
        print("  %-13s %3d x %3d · %2d장 · 크기 x%.3f (세로 %d → %d)%s"
              % (motion, w, h, len(frames), factor, own, int(round(own * factor)),
                 facing))

    # ── 2) 투사체 / 이펙트 줄 ────────────────────────────────────────
    made += build_fx(arr, mask, dark, bg)

    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("\n프레임 %d장 생성 → %s" % (made, DST_ROOT))
    print("다음: python Tools/gen_malphas_skin.py")
    return 0


def build_fx(arr, mask, dark, bg):
    """
    맨 아래 「투사체 / 이펙트」 줄. 묶음마다 <b>캔버스를 따로</b> 잡는다 —
    레이저(가로로 아주 긴 것)와 구체(작고 둥근 것)를 한 캔버스로 묶으면
    구체가 레이저 길이만큼의 투명 여백을 달고 다녀서 연출이 작아 보인다.
    """
    y0, y1 = FX_ROW
    folder = os.path.join(DST_ROOT, "Fx")
    made = 0

    for name, (x0, x1), count, mode in FX_STRIPS:
        rough = split_by_centers(label_centers(dark, x0, x1, FX_LABEL_Y), x0, x1)
        cells = snap_cells_to_gaps(mask, rough, y0, y1)

        if mode == "beam":
            made += build_beam(arr, mask, bg, cells, y0, y1, folder, name, count)
            continue

        boxes = [b for b in boxes_for(mask, cells, y0, y1) if b is not None]

        if not boxes:
            print("  ⚠ Fx/%s: 프레임을 못 찾았습니다" % name)
            continue

        w = max(b[1] - b[0] + 1 for b in boxes)
        h = max(b[3] - b[2] + 1 for b in boxes)

        for i, (bx0, bx1, by0, by1) in enumerate(boxes):
            rgba = to_rgba(arr[by0:by1 + 1, bx0:bx1 + 1], bg)
            canvas = np.zeros((h, w, 4), dtype=np.uint8)
            bw, bh = bx1 - bx0 + 1, by1 - by0 + 1
            # 이펙트는 <b>가운데</b> 정렬 — 발밑 기준이 아니라 범위 한가운데 깔린다.
            canvas[(h - bh) // 2:(h - bh) // 2 + bh, (w - bw) // 2:(w - bw) // 2 + bw] = rgba
            write_png(Image.fromarray(canvas, "RGBA"), folder, "Char_Fx_%s_%02d" % (name, i))
            made += 1

        note = "" if len(boxes) == count else "  ⚠ 기대 %d장" % count
        print("  Fx/%-11s %3d x %3d · %2d장 (방향 없음)%s" % (name, w, h, len(boxes), note))

    ensure_folder_meta(folder)
    return made


def build_beam(arr, mask, bg, cells, y0, y1, folder, name, count):
    """
    ★ <b>자라나는 한 발</b>로 엮는다 (유저 리포트 ③ — 맨 위 주석).

    시트의 이 줄은 조각 여덟 개가 아니라 <b>한 발의 여덟 단계</b>다. 앞쪽 칸들은
    총구에서 커지는 섬광이고, 어느 칸부터 빔이 오른쪽으로 뻗어 마지막 칸의 착탄
    폭발로 끝난다. 그래서 조각을 따로 저장하지 않고 <b>여덟 장을 같은 캔버스에
    총구(왼쪽) 기준으로</b> 얹는다.

    <b>총구가 어느 칸인지를 세로 두께로 찾는다</b> — 섬광은 칸이 갈수록 커지다가
    빔만 지나가는 칸에서 <b>뚝 얇아진다.</b> 그 직전 칸이 총구다. 픽셀 좌표를
    적어두지 않으므로 원화를 다시 그려도 따라간다.

    ⚠ 여덟 장의 <b>캔버스가 모두 같아야</b> 한다. `CombatProjectileFx.PlayArea` 는
      배율을 <b>첫 장</b>으로 잡아 스킬 상자에 맞추기 때문에, 장마다 캔버스가 다르면
      빔이 길어지는 대신 뚱뚱해진다.
    """
    heights = []
    for cx0, cx1 in cells:
        sub = mask[y0:y1 + 1, cx0:cx1 + 1]
        ys = np.where(sub.any(axis=1))[0]
        heights.append(int(ys.max() - ys.min() + 1) if len(ys) else 0)

    # 두께가 <b>처음 줄어드는</b> 칸 직전이 총구다. 끝까지 안 줄면 첫 칸이 총구.
    muzzle = 0
    for i in range(1, len(heights)):
        if heights[i] < heights[i - 1]:
            muzzle = i - 1
            break

    beam = boxes_for(mask, [(cells[muzzle][0], cells[-1][1])], y0, y1)[0]
    if beam is None:
        print("  ⚠ Fx/%s: 빔을 못 찾았습니다" % name)
        return 0

    bx0, bx1, by0, by1 = beam
    w, h = bx1 - bx0 + 1, by1 - by0 + 1

    made = 0
    for i, (cx0, cx1) in enumerate(cells):
        canvas = np.zeros((h, w, 4), dtype=np.uint8)

        if i < muzzle:
            # 충전 단계 — 그 칸의 섬광만 총구 자리에 얹는다.
            # ⚠ 세로는 <b>원본 그대로</b> 둔다: 시트의 모든 단계가 같은 중심선 위에
            #   그려져 있어서, 캔버스 한가운데로 맞추면 오히려 빔과 어긋난다.
            piece = boxes_for(mask, [(cx0, cx1)], y0, y1)[0]
            if piece is None:
                continue
            px0, px1, py0, py1 = piece
            rgba = to_rgba(arr[py0:py1 + 1, px0:px1 + 1], bg)
            oy = min(max(0, py0 - by0), h - rgba.shape[0])
            canvas[oy:oy + rgba.shape[0], 0:rgba.shape[1]] = rgba
        else:
            # 발사 단계 — 총구부터 이 칸의 끝까지. 뒤로 갈수록 길어진다.
            cut = min(cx1, bx1)
            rgba = to_rgba(arr[by0:by1 + 1, bx0:cut + 1], bg)
            canvas[:, 0:rgba.shape[1]] = rgba

        write_png(Image.fromarray(canvas, "RGBA"), folder, "Char_Fx_%s_%02d" % (name, i))
        made += 1

    note = "" if made == count else "  ⚠ 기대 %d장" % count
    print("  Fx/%-11s %3d x %3d · %2d장 (자라나는 빔 · 총구 %d번째 칸)%s"
          % (name, w, h, made, muzzle + 1, note))
    return made


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
