# -*- coding: utf-8 -*-
"""로비용 타이틀(그림자 구움) · 타이틀 비네트를 만든다.

★ 그림자는 **로비 그림에만** 굽는다. PV 는 자기 글로우와 방사 스크림을 따로 얹으므로
  거기엔 깨끗한 원본(`pv_build/src/logo.png`)이 들어간다.

⚠ 새 타이틀은 여백이 좌우 13px · 아래 8px 뿐이라 그림자가 캔버스 밖으로 잘린다.
  그래서 **PAD 만큼 넓힌다.** 넓힌 만큼 씬의 Title RectTransform 을 키워 주면
  화면에 찍히는 크기·자리는 한 픽셀도 안 움직인다 (계산은 아래 print).
"""
from PIL import Image, ImageFilter
import numpy as np, os

VAULT_TITLE = r'C:/Project/Last-Sanctuary-Vault/리소스/Loby/Title.png'
GAME        = r'C:/Project/Last Sanctuary/Assets/_Project/Resources/UI/Lobby'
OUT_DIR     = os.environ.get('LOBBY_OUT', GAME)

PAD = 56

# (블러, 아래로 민 양, 세기) — 접지 그림자 하나 + 넓은 앰비언트 둘
LAYERS = [(10, 8, 0.62), (34, 22, 0.50), (78, 34, 0.34)]


def build_title():
    src = Image.open(VAULT_TITLE).convert('RGBA')
    W, H = src.width + PAD * 2, src.height + PAD * 2
    art = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    art.alpha_composite(src, (PAD, PAD))

    a0 = np.asarray(art)[..., 3].astype(np.float32) / 255.0
    acc = np.zeros_like(a0)
    for blur, dy, gain in LAYERS:
        m = Image.fromarray((a0 * 255).astype(np.uint8), 'L')
        m = m.transform(m.size, Image.AFFINE, (1, 0, 0, 0, 1, -dy), resample=Image.BILINEAR)
        m = m.filter(ImageFilter.GaussianBlur(blur))
        lay = np.asarray(m).astype(np.float32) / 255.0 * gain
        acc = 1.0 - (1.0 - acc) * (1.0 - lay)      # 겹쳐 쌓기

    sh = np.zeros((H, W, 4), dtype=np.uint8)
    sh[..., 3] = np.clip(acc * 255, 0, 255).astype(np.uint8)
    out = Image.alpha_composite(Image.fromarray(sh, 'RGBA'), art)
    p = os.path.join(OUT_DIR, 'LobbyTitle.png')
    out.save(p)

    # 씬 보정값 — 원래 화면 크기를 유지하는 박스
    BOX_W, BOX_H, POS_Y = 762, 444, -7
    s = BOX_W / src.width                       # 화면px / 원본px (가로 제한이었다)
    nw, nh = BOX_W * W / src.width, BOX_W * W / src.width * H / W
    art_cy = (1080 + POS_Y) - (BOX_H - BOX_W * src.height / src.width) / 2 \
             - (BOX_W * src.height / src.width) / 2
    print(f'새 캔버스 {W}x{H}  (원본 {src.width}x{src.height}, PAD {PAD})')
    print(f'  → Title sizeDelta  ({nw:.0f}, {nh:.0f})')
    print(f'  → Title anchoredPos (0, {art_cy + nh / 2 - 1080:.0f})   [그림 중심 y={art_cy:.1f} 유지]')
    return p


def build_vignette(size=768, peak=0.78, core=0.30, name='TitleVignette.png'):
    """가운데가 가장 어둡고 밖으로 부드럽게 풀리는 타원 판.

    ⚠ 색은 검정 고정, **어두움은 알파가 든다** — `LobbyPanel` 이
    `vignette.color = Color.white` 로 못박아 두었기 때문이다."""
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    c = (size - 1) / 2.0
    r = np.sqrt(((xx - c) / c) ** 2 + ((yy - c) / c) ** 2)
    a = np.clip(1.0 - r, 0, 1)
    a = a ** 1.9                      # 가장자리를 길게 풀어 «판» 이 안 보이게
    a = np.clip(a + core * np.clip(1.0 - r / 0.55, 0, 1) ** 2, 0, 1)   # 가운데를 조금 더
    img = np.zeros((size, size, 4), dtype=np.uint8)
    img[..., 3] = np.clip(a * peak * 255, 0, 255).astype(np.uint8)
    p = os.path.join(OUT_DIR, name)
    Image.fromarray(img, 'RGBA').save(p)
    print(f'{name}  {size}x{size}  최대 알파 {img[...,3].max()}')
    return p


if __name__ == '__main__':
    os.makedirs(OUT_DIR, exist_ok=True)
    build_title()
    build_vignette()
