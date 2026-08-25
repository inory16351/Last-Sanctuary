# -*- coding: utf-8 -*-
"""로비 화면의 타이틀·배경 그림을 `Resources/UI/Lobby/` 로 임포트한다 (2026-08-18).

유저 지시: *"타이틀 이미지랑 로비 배경 화면 볼트에 넣어놨으니까 확인하고 넣어"*.
99-6절이 자리 표시용 회색 사각형으로 만들어 둔 것(미결 236번)을 실제 그림으로 바꾼다.

원본 (볼트 `리소스/Loby/`)
-------------------------
  · ``Title.png``   1536x1024  → 타이틀 로고. ⚠ **알파가 없다**(RGB · 배경이 순흑)
  · ``BG.png``      1672x941   → 로비 배경 (16:9)
  · ``Button.png``  2172x724   → 버튼 판. ⚠ **이쪽은 알파가 있다**(딸 필요 없음)

만드는 것 (Resources/UI/Lobby/)
------------------------------
  · ``LobbyTitle``    로고 + 그림자·글로우를 구운 것
  · ``LobbyBg``       배경
  · ``LobbyButton``   버튼 판 + **아래로 내린** 그림자를 구운 것
  · ``TitleVignette`` 타이틀 뒤에 깔 어두운 원(원본 없음 — 코드로 만든다)

★ 씬에 넣을 **칸 크기·위치를 계산해 찍어준다**
-----------------------------------------------
그림자 여백이 그림 크기의 10% 가까이 되므로 **"칸 = 보이는 크기" 가 아니다.** 그림을
새로 넣으면 여백 비율이 달라지고, 손으로 맞추면 그때마다 배경 인물의 얼굴을 다시 가린다
(2026-08-18 실제로 그랬다). 그래서 스크립트가 값을 계산해 출력하고, 그 값을 MCP 로 씬에
넣는다 — 100-9절.

★ 타이틀은 <b>검은 배경을 알파로 따야 한다</b>
----------------------------------------------
배경 그림 위에 얹을 것이라 검은 사각형이 그대로 남으면 안 된다. 다만 **밝기를 알파로
쓰면 안 된다** — 84-8절 ①에서 히스톤이 그렇게 투명해졌다(검은 갑옷에 구멍이 뚫렸다).
로고 안에도 어두운 음영·검은 윤곽이 많아 같은 사고가 난다.

그래서 <b>실루엣</b>으로 딴다:
  ① 테두리에서 시작해 "어두운 칸"만 타고 번져 나간다(flood fill) — 이렇게 닿은 곳만
     <b>바깥 배경</b>이다. 글자 안쪽의 검은 음영은 테두리와 이어져 있지 않아 살아남는다.
  ② 경계 한 겹만 밝기로 부드럽게 깎아 계단을 없앤다(안티에일리어싱 복원).
  ③ 남은 실루엣의 경계 상자로 자른다 — 원본은 위아래에 검은 여백이 넓어서, 안 자르면
     UI 칸의 절반이 빈 채로 배치된다.

⚠ <b>`textureType` 이 8(Sprite) 이어야 한다.</b> 0(Default) 이면 `Resources.Load<Sprite>` 가
  조용히 null 을 돌려주고 화면에 아무것도 안 나온다(84-8절 ②의 그 함정).

⚠ 원본에서 읽어 Resources 로 쓰므로 몇 번을 돌려도 결과가 같다(멱등).

사용법:  py -3 Tools/import_lobby_art.py
"""

import hashlib
import os
import sys
from collections import deque

from PIL import Image, ImageFilter

from vault_path import VAULT, PROJECT

SRC_DIR = os.path.join(VAULT, "리소스", "Loby")
DST = os.path.join(PROJECT, "Assets", "_Project", "Resources", "UI", "Lobby")

#: 배경은 화면을 꽉 채우므로 캔버스 크기(1920x1080)면 충분하다.
BG_MAX_EDGE = 1920

#: 타이틀은 화면 폭의 절반 남짓을 쓴다. 그보다 큰 해상도는 Resources 용량만 먹는다.
TITLE_MAX_EDGE = 1280

#: 버튼 판은 화면에서 360px 폭으로 쓴다 — 원본 2100px 을 그대로 두면 낭비다.
BUTTON_MAX_EDGE = 768

#: 이 밝기(0~255) 이하를 "배경일 수 있는 어두운 칸"으로 본다. 로고의 가장 어두운 금속
#: 음영보다는 낮게, 원본 배경의 미세한 노이즈보다는 높게 잡은 값이다.
DARK = 26

#: 경계에서 이 폭(px)만 밝기로 알파를 깎아 계단을 없앤다.
FEATHER = 2

# ── 로고 그림자·글로우 (2026-08-18, 유저 확정 — "타이틀이랑 배경이 안 붙는다") ──
#
# ★ 왜 굽는가 — 유니티 UI 의 `Image` 에는 그림자·글로우가 없다(TMP 는 있지만 이건 그림이다).
#   오브젝트를 두 겹 더 쌓아 흉내낼 수도 있지만, 그러면 흐림 반경을 조절할 수단이 없어
#   "확대한 복사본"이 되어 로고 모양이 그대로 두 번 보인다. 그림에 구워 넣는 편이 맞다.

#: 붉은 글로우의 색 — 배경(적갈색 톤)과 로고(차가운 은색) 사이를 <b>이어주는</b> 층이다.
GLOW_RGB = (148, 26, 26)

#: 타이틀: (여백, 그림자흐림, 그림자세기, 그림자내림, 글로우흐림, 글로우세기)
#:   그림자를 <b>내리지 않는다</b>(offset 0) — 타이틀은 공중에 뜬 로고라 광원 방향이 없다.
TITLE_FX = dict(pad=44, shadow_blur=18, shadow_strength=0.92, shadow_drop=0,
                glow_blur=7, glow_strength=0.5)

#: 버튼: 그림자를 <b>아래로 내린다</b> — 판이 화면에 놓여 있다는 느낌을 주는 것이 목적이다
#:  (2026-08-18 유저 지시: *"버튼도 이미지 넣었으니까 자연스럽게 그림자 등등 효과"*).
#:  UI 축척이 크게 줄어들므로(2100px → 360px) 흐림·내림을 원본 기준으로 넉넉히 잡는다.
BUTTON_FX = dict(pad=40, shadow_blur=22, shadow_strength=0.85, shadow_drop=14,
                 glow_blur=12, glow_strength=0.3)

# ── 타이틀 뒤 비네트 (같은 유저 확정) ──
#
# ★ 배경에서 가장 밝은 자리(성 첨탑 + 붉은 후광)가 하필 타이틀 자리다. 그 밝기를 눌러
#   <b>로고가 앉을 어두운 자리</b>를 만든다. 배경 그림 자체는 건드리지 않는다 — 원본을
#   고치면 나중에 배경만 갈아끼울 때 이 보정이 같이 따라오지 않는다.

# ── 타이틀을 어디까지 내릴 수 있나 (2026-08-18, 유저 지시) ──
#
# 유저 지시: *"타이틀이 메인 로비의 가운데 캐릭터의 얼굴을 가리지 않게 조정"*.
#
# ★ 1920x1080 로 늘린 배경에서 <b>가운데 기사의 머리카락 윗선이 위에서 447px</b>,
#   <b>얼굴(눈~턱)이 470~575px</b> 이다(배경을 잘라 눈으로 확인한 값). 그래서 타이틀의
#   <b>보이는 아래끝</b>을 그보다 위인 432px 에 둔다 — 머리카락까지 15px 남는다.
#
# ⚠ <b>"보이는" 아래끝</b>이다. 그림에는 그림자 여백이 둘려 있어 <b>칸(RectTransform)의
#   아래끝은 이보다 더 내려간다.</b> 그 차이를 아래 report_frame 이 계산한다.
TITLE_VISIBLE_TOP = 26
TITLE_VISIBLE_BOTTOM = 432

#: 화면 세로 절반. "중앙에서 떠오른다"의 그 중앙이다(캔버스 1920x1080).
SCREEN_HALF_H = 540

#: 버튼 칸의 가로 폭(씬의 Menu 폭). 세로는 그림 비율로 정한다.
BUTTON_FRAME_W = 360

#: 비네트 해상도(정사각으로 만들어 UI 에서 타원으로 늘린다).
VIGNETTE_SIZE = 512

#: 가운데 불투명도. 1 이면 검은 원이 그대로 보인다.
VIGNETTE_STRENGTH = 0.62

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
    filterMode: 1
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


def guid_for(rel):
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


def luminance(px):
    return (px[0] * 299 + px[1] * 587 + px[2] * 114) // 1000


def has_real_alpha(im, transparent_ratio=0.02):
    """
    ★★★ <b>원화가 이미 배경을 따 놓았는가</b> (2026-08-25 신설).

    <b>왜 필요한가</b> — 2026-08-18 의 원화는 알파가 없는 RGB 였고 배경이 순흑이라
    <see cref="key_out_black"/> 의 flood fill 이 유일한 방법이었다. 그런데 새로 온 원화는
    <b>이미 알파로 깨끗이 따여 있다</b>. 그때 다시 키잉을 돌리면 <b>망가진다</b> —
    <c>convert("RGB")</c> 가 알파를 버리는 순간 투명한 바깥이 <b>순흑</b>이 되고,
    로고의 <b>어두운 날개 끝</b>이 그 검정과 이어져 있어 <b>바깥으로 오인되어 먹힌다</b>
    (84-8절 ①이 히스톤에서 겪은 «검은 갑옷에 구멍» 과 같은 사고다).

    → 알파가 «진짜로 쓰이고 있으면»(완전 투명이 일정 비율 이상) <b>그대로 쓴다</b>.
      유저 지시 *"그대로 넣어줘 배경이랑 뜨지 않게"* 가 요구하는 것이 이것이다.
    ⚠ «알파 채널이 있다» 만으로는 판정할 수 없다 — 전부 255 인 RGBA 도 흔하다.
    """
    if im.mode not in ("RGBA", "LA"):
        return False
    alpha = im.convert("RGBA").getchannel("A")
    clear = alpha.histogram()[0]
    return clear >= im.size[0] * im.size[1] * transparent_ratio


def key_out_black(im):
    """
    바깥 검은 배경만 투명하게 만든 RGBA 이미지를 돌려준다.
    ⚠ <b>알파가 이미 있는 원화에는 쓰지 말 것</b> — 위 <see cref="has_real_alpha"/> 참조.

    ★ 밝기가 아니라 <b>실루엣</b>으로 딴다 — 테두리에서 어두운 칸만 타고 번져 나가
    (flood fill) 닿은 곳만 배경으로 본다. 로고 안쪽의 검은 음영은 테두리와 이어져
    있지 않으므로 <b>불투명하게 남는다</b>.
    """
    im = im.convert("RGB")
    w, h = im.size
    px = im.load()

    lum = [[luminance(px[x, y]) for x in range(w)] for y in range(h)]

    outside = bytearray(w * h)          # 1 = 바깥 배경
    q = deque()

    def push(x, y):
        if 0 <= x < w and 0 <= y < h and not outside[y * w + x] and lum[y][x] <= DARK:
            outside[y * w + x] = 1
            q.append((x, y))

    for x in range(w):
        push(x, 0)
        push(x, h - 1)
    for y in range(h):
        push(0, y)
        push(w - 1, y)

    while q:
        x, y = q.popleft()
        push(x - 1, y)
        push(x + 1, y)
        push(x, y - 1)
        push(x, y + 1)

    out = Image.new("RGBA", (w, h))
    op = out.load()

    for y in range(h):
        row = lum[y]
        for x in range(w):
            r, g, b = px[x, y]
            if outside[y * w + x]:
                op[x, y] = (r, g, b, 0)
                continue

            # 경계 한 겹만 밝기로 깎는다 — 원본의 안티에일리어싱을 되살리는 것이다.
            near_bg = False
            for dy in range(-FEATHER, FEATHER + 1):
                yy = y + dy
                if yy < 0 or yy >= h:
                    continue
                for dx in range(-FEATHER, FEATHER + 1):
                    xx = x + dx
                    if 0 <= xx < w and outside[yy * w + xx]:
                        near_bg = True
                        break
                if near_bg:
                    break

            alpha = 255
            if near_bg:
                # 밝기 0 → 완전 투명, DARK*3 이상 → 완전 불투명 (선형)
                alpha = min(255, int(row[x] * 255 / (DARK * 3)))
            op[x, y] = (r, g, b, alpha)

    return out


def crop_to_alpha(im, pad=4):
    """알파 경계 상자로 자른다 — 원본의 넓은 검은 여백을 UI 칸에 끌고 들어가지 않는다."""
    box = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    if box is None:
        return im

    x0, y0, x1, y1 = box
    x0 = max(0, x0 - pad)
    y0 = max(0, y0 - pad)
    x1 = min(im.size[0], x1 + pad)
    y1 = min(im.size[1], y1 + pad)
    return im.crop((x0, y0, x1, y1))


def add_shadow_and_glow(art, pad, shadow_blur, shadow_strength, shadow_drop,
                        glow_blur, glow_strength):
    """
    그림 <b>아래에</b> 검은 그림자와 붉은 글로우를 깔아 배경에 앉힌다
    (2026-08-18 유저 확정: *"로고에 그림자·글로우 구워넣기"* · *"버튼도 … 자연스럽게 그림자"*).

    <code>
      ① 투명 여백을 두른다        ← 안 두르면 흐림이 가장자리에서 잘려 한쪽만 그림자가 없다
      ② 알파를 크게 흐려 검게 깐다 (떼어내는 층 · shadow_drop 만큼 내려서)
      ③ 알파를 조금 흐려 붉게 깐다 (이어주는 층 — 배경이 적갈색이다)
      ④ 그 위에 원본 그림을 얹는다
    </code>

    ⚠ 순서가 뜻이다. 그림자를 <b>위에</b> 얹으면 그림이 흐려지고, 글로우를 그림자 아래에
    깔면 검정에 묻혀 안 보인다.

    ★ <c>shadow_drop</c> 이 <b>광원의 방향</b>이다. 0 이면 사방으로 고르게 번져 "떠 있는"
    느낌(타이틀), 아래로 내리면 "놓여 있는" 느낌(버튼)이 된다.

    ⚠ 여백은 <b>위아래 대칭</b>으로 둔다 — 그림자를 내리는 만큼 아래 여백만 키우면 그림의
    가운데가 위로 밀려, 씬에서 칸 가운데에 두었을 때 그림이 위로 치우쳐 보인다.
    """
    w, h = art.size
    pad_y = pad + abs(shadow_drop)                      # 내린 그림자가 잘리지 않게
    canvas_size = (w + pad * 2, h + pad_y * 2)

    padded = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    padded.paste(art, (pad, pad_y))

    def layer(blur, strength, rgb, drop=0):
        src = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
        src.paste(art, (pad, pad_y + drop))
        a = src.getchannel("A").filter(ImageFilter.GaussianBlur(blur))
        a = a.point(lambda v: int(min(255, v * strength)))
        tint = Image.new("RGBA", canvas_size, rgb + (0,))
        tint.putalpha(a)
        return tint

    out = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    out = Image.alpha_composite(out, layer(shadow_blur, shadow_strength, (0, 0, 0),
                                          drop=shadow_drop))
    out = Image.alpha_composite(out, layer(glow_blur, glow_strength, GLOW_RGB))
    return Image.alpha_composite(out, padded)


def make_title_vignette():
    """
    타이틀 뒤에 깔 <b>부드러운 어두운 원</b>. 가운데가 가장 어둡고 가장자리에서 0 이 된다.

    ★ 알파를 <c>(1-r)²</c> 로 떨군다 — 선형으로 떨구면 <b>원의 테두리가 보인다</b>(사람 눈이
    기울기의 급변을 경계선으로 읽는다). 제곱이면 끝이 완만해져 어디서 끝나는지 알 수 없다.
    """
    size = VIGNETTE_SIZE
    im = Image.new("RGBA", (size, size))
    px = im.load()
    half = size / 2.0

    for y in range(size):
        dy = (y - half) / half
        for x in range(size):
            dx = (x - half) / half
            r = min(1.0, (dx * dx + dy * dy) ** 0.5)
            a = (1.0 - r) ** 2 * VIGNETTE_STRENGTH
            px[x, y] = (0, 0, 0, int(round(a * 255)))

    return im


def report_title_frame(content_h, pad_x, pad_y, padded_w, padded_h):
    """
    씬의 <c>Title</c> 칸을 <b>얼마로 두어야</b> 그림의 <b>보이는</b> 위/아래끝이 원하는
    자리에 오는지 계산해 찍는다.

    ★ 손으로 맞추면 안 되는 값이다 — 그림자 여백이 그림 크기의 10% 가까이 되므로
    "칸 = 보이는 크기" 가 아니다. 그림을 새로 넣을 때마다 여백 비율이 달라지고,
    그때마다 얼굴을 다시 가리게 된다. 그래서 <b>스크립트가 계산해 찍는다.</b>
    """
    top_frac = pad_y / float(padded_h)
    vis_frac = content_h / float(padded_h)

    visible_h = TITLE_VISIBLE_BOTTOM - TITLE_VISIBLE_TOP
    frame_h = visible_h / vis_frac
    frame_w = frame_h * padded_w / float(padded_h)
    frame_y = TITLE_VISIBLE_TOP - frame_h * top_frac      # 칸 위끝(위에서 아래로, 양수)

    # "중앙에서 떠오른다" 는 <b>보이는 그림의 중심</b>이 화면 중심에서 출발한다는 뜻이다.
    visible_center = (TITLE_VISIBLE_TOP + TITLE_VISIBLE_BOTTOM) / 2.0
    rise = SCREEN_HALF_H - visible_center

    print(f"       └ 씬 Title 칸: {frame_w:.0f}x{frame_h:.0f} · anchoredPosition.y "
          f"{-frame_y:.0f} · titleRisePixels {rise:.0f}")
    print(f"         (보이는 범위 위 {TITLE_VISIBLE_TOP} ~ 아래 {TITLE_VISIBLE_BOTTOM}px "
          f"— 얼굴 470~575px 을 가리지 않는다)")
    # 비네트는 타이틀보다 <b>넉넉히 크게</b> — 로고 크기와 같으면 어두운 자리가 로고
    # 윤곽을 따라가 "검은 판을 깔았다"로 보인다. 가로 1.5배 · 세로 1.8배가 눈으로
    # 맞춰본 값이다(가로로 넓은 로고라 세로 배수를 더 크게 준다).
    print(f"       └ 씬 Vignette 칸: {frame_w * 1.5:.0f}x{visible_h * 1.8:.0f} · "
          f"anchoredPosition.y {-visible_center:.0f}")


def fit(im, max_edge):
    w, h = im.size
    if max(w, h) <= max_edge:
        return im
    s = max_edge / float(max(w, h))
    return im.resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS)


def write(im, name):
    out = os.path.join(DST, name + ".png")
    im.save(out)

    rel = os.path.relpath(out, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    g = guid_for(rel)
    with open(out + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=g, sprite_id=g[:32]))

    print(f"  {name:<12} {im.size[0]}x{im.size[1]}  ({os.path.getsize(out) / 1024:.0f}KB)")


def main():
    os.makedirs(DST, exist_ok=True)

    src_title = os.path.join(SRC_DIR, "Title.png")
    src_bg = os.path.join(SRC_DIR, "BG.png")
    src_button = os.path.join(SRC_DIR, "Button.png")

    missing = [p for p in (src_title, src_bg, src_button) if not os.path.isfile(p)]
    if missing:
        for p in missing:
            print("  ⚠ 원본 없음:", p)
        return 1

    title = Image.open(src_title)

    # ★★★ 2026-08-25 — <b>원화가 이미 따여 있으면 그대로 쓴다</b> (유저 지시:
    #   *"로비 타이틀 이미지도 바꿨으니까 적용법 찾아보고 <b>그대로 넣어줘 배경이랑 뜨지 않게</b>"*).
    #   ⚠ 새 원화에 다시 키잉을 돌리면 <b>어두운 날개 끝이 먹힌다</b>(has_real_alpha 의 doc).
    if has_real_alpha(title):
        print(f"  타이틀 원본 {title.size[0]}x{title.size[1]} {title.mode} → "
              f"<b>알파가 이미 있다</b> · 키잉을 건너뛰고 그대로 씁니다")
        keyed = crop_to_alpha(title.convert("RGBA"))

        # ★★ <b>그림자·글로우는 굽는다</b> (2026-08-25 · 유저: *"그림자 비네트 켜"*).
        #   처음에는 «배경이랑 뜨지 않게» 를 «로고 뒤의 모든 겹» 으로 읽고 이것도 뺐는데,
        #   유저가 <b>다시 켜라</b>고 했다. 뺄 것은 <b>그림 자체의 검은 사각형</b>이었지
        #   <b>로고를 배경에서 떼어 놓는 받침</b>이 아니었다.
        #   ★ 받침이 필요한 이유는 <b>배경이 밝기 때문</b>이다 — 로비 배경의 성 첨탑·붉은
        #     후광이 하필 타이틀 자리라, 받침이 없으면 로고 가장자리가 그 밝기에 묻힌다.
        #   ⚠ 이 겹은 <b>그림에 굽는</b> 것이라 여백이 생긴다 — 아래 pad_x/pad_y 가 그 값이고,
        #     씬의 Title 칸 크기를 그만큼 키워야 로고가 작아 보이지 않는다(151-3절).
        title = fit(add_shadow_and_glow(keyed, **TITLE_FX), TITLE_MAX_EDGE)
        pad_x = TITLE_FX["pad"]
        pad_y = TITLE_FX["pad"] + abs(TITLE_FX["shadow_drop"])
    else:
        print(f"  타이틀 원본 {title.size[0]}x{title.size[1]} {title.mode} → 검은 배경 알파 처리")
        keyed = crop_to_alpha(key_out_black(title))
        title = fit(add_shadow_and_glow(keyed, **TITLE_FX), TITLE_MAX_EDGE)
        pad_x = TITLE_FX["pad"]
        pad_y = TITLE_FX["pad"] + abs(TITLE_FX["shadow_drop"])

    write(title, "LobbyTitle")

    # ⚠ 그림자 여백 때문에 <b>비율이 바뀐다</b> — 씬의 Title 칸도 이 비율로 맞춰야
    #   preserveAspect 가 여백을 만들지 않는다. 값을 계산해 찍어준다.
    #   ★ 2026-08-25 — 여백은 <b>위에서 정한 값</b>을 쓴다(그림자를 안 구우면 0 이다).
    #     예전에는 여기서 TITLE_FX 를 다시 읽어, 굽지 않은 경우에도 <b>있지도 않은 여백</b>을
    #     넣어 계산했다 — 그러면 씬의 칸이 로고보다 커져 <b>가운데가 어긋난다</b>.
    kw, kh = keyed.size
    report_title_frame(kh, pad_x, pad_y,
                       kw + pad_x * 2, kh + pad_y * 2)

    bg = Image.open(src_bg).convert("RGBA")
    print(f"  배경 원본 {bg.size[0]}x{bg.size[1]}")
    write(fit(bg, BG_MAX_EDGE), "LobbyBg")

    # ── 버튼 판 (2026-08-18 유저 지시) ──
    # ⚠ 원본에 <b>이미 알파가 있다</b>(타이틀과 다르다) — 검은 배경을 딸 필요가 없다.
    #   그래도 crop_to_alpha 는 거친다: 원본의 위아래 빈 여백을 그대로 UI 칸에 끌고
    #   들어가면 판이 칸 가운데에서 작아 보인다.
    button = Image.open(src_button).convert("RGBA")
    print(f"  버튼 원본 {button.size[0]}x{button.size[1]}")

    plate = crop_to_alpha(button, pad=0)
    button = fit(add_shadow_and_glow(plate, **BUTTON_FX), BUTTON_MAX_EDGE)
    write(button, "LobbyButton")

    bw, bh = button.size
    frame_h = BUTTON_FRAME_W * bh / float(bw)

    # ⚠ 판이 화면에서 몇 px 인지는 <b>줄이기 전(pre-fit) 비율</b>로 재야 한다 — 줄인 뒤
    #   높이(bh)로 나누면 축척이 두 번 섞여 엉뚱한 값이 나온다(처음에 149px 이라고 찍었다).
    padded_h = plate.size[1] + 2 * (BUTTON_FX["pad"] + abs(BUTTON_FX["shadow_drop"]))
    plate_h = frame_h * plate.size[1] / float(padded_h)
    print(f"       └ 씬 버튼 칸: {BUTTON_FRAME_W}x{frame_h:.0f} "
          f"(판 자체는 {plate_h:.0f}px — 나머지는 그림자 여백이다)")

    write(make_title_vignette(), "TitleVignette")

    print("\n로비 그림 4장 → Resources/UI/Lobby")
    print("Unity 에서 Assets/Refresh 를 실행할 것.")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
