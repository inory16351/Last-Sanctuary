# -*- coding: utf-8 -*-
"""itch.io 페이지용 이미지 조립 — 프로젝트에 이미 있는 리소스만 사용한다."""
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
import os, shutil

ROOT = r'C:\Project\Last-Sanctuary'
OUT  = os.path.join(ROOT, 'Docs', 'itchio', 'images')
SHOT = r'C:\Users\user\Pictures\Screenshots'
os.makedirs(OUT, exist_ok=True)

LOBBY_BG   = os.path.join(ROOT, r'Assets\_Project\Resources\UI\Lobby\LobbyBg.png')
LOBBY_LOGO = os.path.join(ROOT, r'Assets\_Project\Resources\UI\Lobby\LobbyTitle.png')

S_TITLE   = os.path.join(SHOT, '스크린샷 2026-08-26 151612.png')  # 1919x1079 타이틀
S_PREP    = os.path.join(SHOT, '스크린샷 2026-08-26 151641.png')  # 1917x1079 웨이브1 정비
S_MARCH   = os.path.join(SHOT, '스크린샷 2026-08-20 140226.png')  # 1911x1058 웨이브7 진군
S_TACTICS = os.path.join(SHOT, '스크린샷 2026-08-12 184900.png')  # 1219x769  전술 지침
S_GROWTH  = os.path.join(SHOT, '스크린샷 2026-08-25 155244.png')  # 1096x831  캐릭터 성장
S_FIGHT   = os.path.join(SHOT, '스크린샷 2026-08-20 092825.png')  # 1170x867  근접 전투

BG   = (8, 9, 12)
EDGE = (46, 92, 84)

def vignette(im, strength=0.75, feather=0.55):
    w, h = im.size
    mask = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(mask)
    pad_x, pad_y = int(w * (1 - feather) / 2), int(h * (1 - feather) / 2)
    d.ellipse([-pad_x, -pad_y, w + pad_x, h + pad_y], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) // 7))
    dark = Image.new('RGB', (w, h), BG)
    return Image.composite(im, Image.blend(im, dark, strength), mask)

def cover_crop(im, tw, th, cx=0.5, cy=0.5):
    """비율을 유지한 채 tw x th 로 채워 자른다."""
    w, h = im.size
    if w / h > tw / th:
        nh = h; nw = int(h * tw / th)
    else:
        nw = w; nh = int(w * th / tw)
    left = max(0, min(w - nw, int(w * cx - nw / 2)))
    top  = max(0, min(h - nh, int(h * cy - nh / 2)))
    return im.crop((left, top, left + nw, top + nh)).resize((tw, th), Image.LANCZOS)

def framed(src, box, tw, th, pad=0.0):
    """스크린샷 일부를 잘라 tw x th 캔버스 한가운데에 얹고 얇은 테두리를 준다."""
    im = Image.open(src).convert('RGB').crop(box)
    inner_w, inner_h = int(tw * (1 - pad)), int(th * (1 - pad))
    r = min(inner_w / im.width, inner_h / im.height)
    im = im.resize((int(im.width * r), int(im.height * r)), Image.LANCZOS)
    canvas = Image.new('RGB', (tw, th), BG)
    x, y = (tw - im.width) // 2, (th - im.height) // 2
    canvas.paste(im, (x, y))
    ImageDraw.Draw(canvas).rectangle([x, y, x + im.width - 1, y + im.height - 1], outline=EDGE, width=2)
    return canvas

def logo(width):
    lg = Image.open(LOBBY_LOGO).convert('RGBA')
    lg = lg.crop(lg.getbbox())
    h = int(lg.height * width / lg.width)
    return lg.resize((width, h), Image.LANCZOS)

# ── 1. 커버 630x500 ─────────────────────────────────────────────
bg = Image.open(LOBBY_BG).convert('RGB')
cov = cover_crop(bg, 630, 500, cx=0.49, cy=0.46)
cov = vignette(cov, 0.55, 0.62)
cov = ImageEnhance.Brightness(cov).enhance(0.88)
lg = logo(540)
cov.paste(lg, (45, 26), lg)
cov.save(os.path.join(OUT, 'cover_630x500.png'))

# ── 2. 배너 1920x480 ────────────────────────────────────────────
ban = cover_crop(bg, 1920, 480, cx=0.49, cy=0.42)
ban = ImageEnhance.Brightness(ban).enhance(0.62)
ban = vignette(ban, 0.5, 0.85)
lg = logo(760)
ban.paste(lg, ((1920 - lg.width) // 2, (480 - lg.height) // 2 - 10), lg)
ban.save(os.path.join(OUT, 'banner_1920x480.png'))

# ── 3. 스크린샷 6장 (1920x1080 통일) ────────────────────────────
def shot(src, name, box=None):
    im = Image.open(src).convert('RGB')
    if box: im = im.crop(box)
    if im.size == (1920, 1080):
        out = im
    elif abs(im.width / im.height - 16 / 9) < 0.06:
        out = im.resize((1920, 1080), Image.LANCZOS)
    else:  # 패널 캡처는 어두운 캔버스 한가운데에 얹는다
        r = min(1750 / im.width, 960 / im.height)
        im = im.resize((int(im.width * r), int(im.height * r)), Image.LANCZOS)
        out = Image.new('RGB', (1920, 1080), BG)
        x, y = (1920 - im.width) // 2, (1080 - im.height) // 2
        out.paste(im, (x, y))
        ImageDraw.Draw(out).rectangle([x, y, x + im.width - 1, y + im.height - 1], outline=EDGE, width=2)
    out.save(os.path.join(OUT, name))

shot(S_TITLE,   'screenshot_01_title.png')
shot(S_PREP,    'screenshot_02_prep.png')
shot(S_MARCH,   'screenshot_03_march.png')
shot(S_TACTICS, 'screenshot_04_tactics.png', (0, 0, 966, 660))
shot(S_GROWTH,  'screenshot_05_growth.png',  (40, 60, 1024, 800))
shot(S_FIGHT,   'screenshot_06_battle.png',  (60, 230, 1170, 867))

# ── 4. 핵심 시스템 / 핵심 루프 카드 1200x675 ────────────────────
CW, CH = 1200, 675
framed(S_TACTICS, (0, 0, 966, 660),        CW, CH, 0.06).save(os.path.join(OUT, 'system_01_tactics.png'))
framed(S_GROWTH,  (40, 60, 1024, 800),     CW, CH, 0.06).save(os.path.join(OUT, 'system_02_growth.png'))
framed(S_MARCH,   (15, 55, 735, 460),      CW, CH, 0.05).save(os.path.join(OUT, 'system_03_erosion.png'))

framed(S_PREP,    (700, 0, 1917, 700),     CW, CH, 0.04).save(os.path.join(OUT, 'loop_01_prep.png'))
framed(S_FIGHT,   (60, 230, 1170, 867),    CW, CH, 0.04).save(os.path.join(OUT, 'loop_02_battle.png'))
framed(S_MARCH,   (390, 0, 1911, 1058),    CW, CH, 0.04).save(os.path.join(OUT, 'loop_03_expand.png'))

# ── 5. 분위기 컷 (이벤트 배경 원본 복사) ────────────────────────
for src, dst in [('EventBg/bg_nexus.png',  'art_nexus.png'),
                 ('EventBg/bg_mind.png',   'art_mind.png'),
                 ('EventBg/bg_fog.png',    'art_fog.png'),
                 ('Opening/BG_01.png',     'art_opening_01.png'),
                 ('Opening/BG_03.png',     'art_opening_03.png')]:
    shutil.copy(os.path.join(ROOT, r'Assets\_Project\Resources', src.replace('/', os.sep)),
                os.path.join(OUT, dst))

for f in sorted(os.listdir(OUT)):
    p = os.path.join(OUT, f)
    print(Image.open(p).size, os.path.getsize(p) // 1024, 'KB', f)
