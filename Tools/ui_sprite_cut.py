# -*- coding: utf-8 -*-
"""
UI 스프라이트 시트 → 개별 PNG (픽셀 아트 전용, 2026-08-25 2차)

생성 AI 가 «네이티브의 N배, 픽셀 하나를 N×N 블록으로» 뽑아준 시트를 유니티가 쓸
<b>진짜 픽셀 아트</b> 낱장으로 되돌린다.

 ① 배경을 <b>자동 판별</b>해 지운다 — 자홍(#FF00FF)일 수도 있고 이미 알파일 수도 있다
 ② 시트 안의 «칸» 을 분리
 ③ <b>네이티브 크기로 되줄인다</b> (BOX = 블록 평균. NEAREST 로 줄이면 블록 안의
    어느 점을 집느냐에 따라 테두리가 한 줄씩 사라진다)
 ④ <b>16색 팔레트로 스냅</b> — 생성기가 몰래 넣은 그라데이션·안티에일리어싱을 없앤다
 ⑤ <b>알파를 이진화</b>(0 또는 255) — 픽셀 아트에 반투명 가장자리는 없다.
    ★ 이게 «초상화 액자 안쪽이 지저분하게 들어간다» 의 답이다
 ⑥ 9-슬라이스 경계를 산출해 Temp/ui_sprite_cut.json 에 적는다

사용법:  python Tools/ui_sprite_cut.py
"""
import os, sys, json
import numpy as np
from PIL import Image

SRC = r"C:\Project\Last-Sanctuary-Vault\리소스\sprites"
PROJ = r"C:\Project\Last Sanctuary"
DST = os.path.join(PROJ, "Assets", "_Project", "Resources", "UI")

try: sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception: pass


#: HUD 의 정본 팔레트. <see cref="HudTheme"/> 의 실제 색에서 뽑았다 —
#: 그림과 코드가 <b>같은 색표</b>를 써야 글자와 판이 한 벌로 보인다.
PALETTE = [
    (0x0D, 0x0F, 0x14), (0x12, 0x14, 0x1C), (0x1A, 0x1C, 0x21), (0x1A, 0x1F, 0x29),
    (0x21, 0x2B, 0x38), (0x2E, 0x42, 0x52), (0x3D, 0x54, 0x68), (0x5A, 0x71, 0x86),
    (0x29, 0x6B, 0x61), (0x3F, 0xA0, 0x8C), (0x73, 0xF2, 0xC7), (0x94, 0xA3, 0xB3),
    (0xE0, 0xEB, 0xF0), (0x66, 0xD9, 0x85), (0xFA, 0xB8, 0x59), (0xF5, 0x6B, 0x6B),
]

#: 게이지 채움 전용. 색이 하나라도 섞이면 체력 초록을 곱했을 때 탁해진다.
GREYS = [(0x80, 0x80, 0x80), (0xB0, 0xB0, 0xB0), (0xD8, 0xD8, 0xD8), (0xFF, 0xFF, 0xFF)]


def strip_background(path):
    """
    배경을 지우고 RGBA 를 돌려준다.

    ⚠ 파일마다 배경이 다르다(실측): 어떤 것은 자홍, 어떤 것은 <b>이미 알파</b>가 들어 있다.
      한 가지로 가정하면 알파본은 «전부 불투명한 검정 판» 이 되어 통째로 망가진다.
      그래서 <b>알파가 실제로 쓰이고 있는지 먼저 보고</b> 갈린다.
    """
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(np.float64)

    if (a[:, :, 3] < 250).mean() > 0.05:
        return a, "알파"

    # 자홍 키잉 — 순수 자홍은 min(R,B)-G 가 255, 회색·검정은 0,
    # 붉은 계열(R 만 크고 B 는 낮다)도 0 근처라 장식을 갉아먹지 않는다.
    mag = np.clip((np.minimum(a[:, :, 0], a[:, :, 2]) - a[:, :, 1]) / 255.0, 0, 1)
    al = 1.0 - mag
    out = np.zeros_like(a)
    safe = np.maximum(al, 1e-4)[:, :, None]
    out[:, :, :3] = np.clip((a[:, :, :3] - (1 - al)[:, :, None] * np.array([255., 0., 255.])) / safe, 0, 255)
    out[:, :, 3] = al * 255.0
    return out, "자홍"


def bands(alpha, gap=12, minrun=20):
    """세로로 쌓인 «칸» 을 찾는다. 줄당 개수로 거른다(잔여 픽셀 한둘에 붙지 않게)."""
    solid = alpha > 96
    cnt = solid.sum(axis=1)
    need = max(12, int(0.02 * solid.shape[1]))
    on = cnt >= need
    runs, s = [], None
    for i, v in enumerate(on):
        if v and s is None: s = i
        elif not v and s is not None: runs.append((s, i)); s = None
    if s is not None: runs.append((s, len(on)))
    merged = []
    for r in runs:
        if merged and r[0] - merged[-1][1] < gap: merged[-1] = (merged[-1][0], r[1])
        else: merged.append(r)
    return [r for r in merged if r[1] - r[0] >= minrun]


def snap_palette(rgba, palette):
    """
    색을 팔레트의 가장 가까운 값으로 못박는다.

    ★ 생성기는 «픽셀 아트» 라고 해도 블록 안에 미세한 그라데이션을 넣는다. 그대로 두면
      네이티브로 줄였을 때 <b>16색이 수백 색</b>이 되어 폰트(네오둥근모)의 단단한 획 옆에서
      흐물거려 보인다. 여기서 색 수를 강제로 되돌린다.
    ⚠ 투명한 픽셀은 건드리지 않는다 — 색이 없는 자리다.
    """
    pal = np.array(palette, dtype=np.float64)
    rgb = rgba[:, :, :3]
    d = ((rgb[:, :, None, :] - pal[None, None, :, :]) ** 2).sum(axis=3)
    idx = d.argmin(axis=2)
    out = rgba.copy()
    out[:, :, :3] = pal[idx]
    out[rgba[:, :, 3] < 128] = 0
    return out


def to_native(rgba, w=None, h=None):
    """
    네이티브 크기로 되줄인다.

    ★ <b>BOX(면적 평균)</b> 로 줄인다. NEAREST 는 N×N 블록 안의 어느 점을 집느냐에 따라
      1픽셀짜리 테두리가 통째로 사라진다 — 이 그림들은 테두리가 1픽셀이라 치명적이다.
      블록이 균일하므로 평균 = 블록 색이고, 이어지는 팔레트 스냅이 흐릿함을 되돌린다.
    """
    im = Image.fromarray(rgba.astype(np.uint8), "RGBA")
    if h: w2 = max(1, round(im.width * h / im.height)); size = (w2, h)
    else: h2 = max(1, round(im.height * w / im.width)); size = (w, h2)
    return np.asarray(im.resize(size, Image.BOX)).astype(np.float64)


def binarize_alpha(rgba, cut=128):
    """
    ★★ <b>알파를 0 아니면 255 로</b>.

    픽셀 아트에는 반투명 가장자리가 없다. 그런데 생성기의 부드러운 경계 + BOX 축소가
    합쳐지면 테두리 바깥에 알파 20~200 짜리 띠가 남는다. 그게 어두운 배경 위에서
    <b>지저분한 후광</b>으로 보이고, 특히 <b>초상화 액자</b>처럼 «안쪽이 완전히 뚫려야»
    하는 그림에서는 뚫린 자리에 얇은 막이 껴서 인물이 뿌옇게 된다.
    """
    out = rgba.copy()
    solid = out[:, :, 3] >= cut
    out[:, :, 3] = np.where(solid, 255.0, 0.0)
    out[~solid] = 0
    return out


def auto_border(a, horizontal=True, tol=2.0):
    """
    가운데와 «똑같은» 열/행이 이어지는 구간 = 늘려도 되는 곳. 그 바깥이 경계.

    ★ 이번엔 자동이 <b>먹힌다</b> — 팔레트 스냅을 거쳐 평평한 면이 진짜로 평평해졌다.
      (지난 painted 세트는 금속 얼룩 때문에 같은 열이 두 개도 없어서 못 썼다.)
    """
    f = a.astype(np.float64)
    n = f.shape[1] if horizontal else f.shape[0]
    mid = n // 2
    ref = f[:, mid, :] if horizontal else f[mid, :, :]
    def d(i):
        c = f[:, i, :] if horizontal else f[i, :, :]
        return np.abs(c - ref).mean()
    lo = mid
    while lo > 0 and d(lo - 1) < tol: lo -= 1
    hi = mid
    while hi < n - 1 and d(hi + 1) < tol: hi += 1
    l, r = lo, n - 1 - hi
    if l + r > 0.70 * n: return None
    return l, r


#: 자동 측정이 못 잡는 <b>세로 경계</b>를 손으로 준다. (name → (T, B), 네이티브 픽셀)
#:
#: ★ 자동은 «가운데 행과 똑같은 행» 을 찾는데, 배너는 판 아래쪽 모서리가 <b>대각선으로
#:   깎여 있어</b> 행마다 폭이 달라 아무것도 안 잡힌다. 그런데 세로로 늘려야 글자가
#:   들어갈 자리가 생긴다 — 그래서 «진짜로 균일한 구간» 을 실측해 못박는다.
#: ⚠ Wave_Banner(338×92) 실측: y 0~55 사슬+판 윗테두리 · <b>y 56~70 균일</b> ·
#:   y 71~91 아래 테두리와 대각선. 그래서 위 56, 아래 21 을 고정하고 가운데 15 만 늘린다.
MANUAL_VERT = {
    "Wave_Banner": (56, 2),
    # ★★ 로스터 행(2026-08-26 2차) — 위 레일(노치·이중선) 18px · 아래 레일(볼트·해칭) 14px.
    #   자동은 «가운데 행과 똑같은 행» 을 찾는데 레일마다 장식이 달라 아무것도 못 잡는다.
    "Btn_Roster_Normal": (18, 14),
}

#: ★★ <b>가로 경계</b>를 손으로 준다 (name → (L, R), 네이티브 픽셀) — 2026-08-26 신설.
#:
#: <b>왜 생겼나</b> — 새 로스터 카드는 위·아래 레일에 <b>노치와 볼트</b>가 있어 «가운데
#: 열과 똑같은 열» 이 하나도 없다. <see cref="auto_border"/> 가 <b>L0 R0</b> 을 내놓았고,
#: 경계 0 은 «9-슬라이스를 안 쓴다» 는 뜻이라 카드를 늘리면 <b>모서리 브래킷이 같이
#: 늘어난다</b>. 실측(문턱을 올려 가며): 왼 12~23 · 오 4~23 구간에 장식이 있다.
#: → 모서리 브래킷을 통째로 품도록 <b>24</b> 로 넉넉히 잡는다.
MANUAL_HORIZ = {
    "Btn_Roster_Normal": (24, 24),
}

#: 그림의 <b>아래를 잘라낸다</b> (name → 남길 높이, 네이티브 픽셀).
#:
#: ★ 웨이브 배너는 판 아래쪽이 <b>대각선으로 길게 늘어져</b> 있었다. 글자는 판 위쪽에
#:   들어가는데 그 꼬리가 아래로 40px 넘게 뻗어서, 배너가 쓸데없이 크고 <b>보스 체력바와
#:   부딪혔다</b>. 유저 지시(2026-08-25): *"구분선을 이용해서 이미지를 자르라고"* —
#:   꼬리를 잘라 <b>평평한 단면</b>으로 끝내고, 그 자리를 구분선이 막는다.
#: ⚠ 자를 행은 <b>실측해서</b> 정한다. y 0~64 까지가 전폭(338)이고 <b>65부터 대각선이
#:   파고든다</b> — 65 이후에서 자르면 단면이 삐뚤어진다.
CROP_BOTTOM = {
    "Wave_Banner": 65,
}

STATES = ["Normal", "Hover", "On", "Off"]

#: 파일 → 이름 · 폴더 · 네이티브 크기(높이 우선) · 경계 지정
#:
#: ⚠ 경계가 <b>0</b> 인 것은 늘리지 않는 것이다 — 닫기(32×32) · 슬롯 · 구분선.
#:   작은 정사각에 9-슬라이스를 걸면 L+R 이 표시 폭보다 커져 오히려 깨진다.
#: ⚠ `vert` 는 세로로도 늘어나는 것(창·판·액자·미니맵).
JOBS = [
    # 파일,        이름,             폴더,      높이, 너비,  경계,      세로도 늘림
    ("BUTTON_01.png", "Btn_Action_%s", "Buttons",  40, None, "auto",  False),
    ("BUTTON_02.png", "Btn_Panel_%s",  "Buttons",  40, None, "auto",  False),
    ("BUTTON_03.png", "Btn_Chip_%s",   "Buttons",  32, None, "auto",  False),
    ("BUTTON_04.png", "Btn_Close_%s",  "Buttons",  32, None, "zero",  False),
    ("BUTTON_05.png", "Btn_Speed_%s",  "Buttons",  40, None, "auto",  False),
    ("BUTTON_06.png", "Btn_Choice_%s", "Buttons",  52, None, "auto",  False),

    ("UI_01.png", "Win_Frame",      "Frames", 240,  None, "auto", True),
    ("UI_02.png", "Hud_Plate",      "Frames", 128,  None, "auto", True),
    ("UI_03.png", "Bar_Track",      "Frames",  18,  None, "auto", False),
    ("UI_04.png", "Bar_Fill",       "Frames",  14,  None, "zero", False),
    ("UI_05.png", "Portrait_Frame", "Frames", 302,  None, "auto", True),
    ("UI_06.png", "Minimap_Bezel",  "Frames", 128,  None, "auto", True),
    ("UI_07.png", ["Slot_Empty", "Slot_Filled", "Slot_Locked"], "Frames", 44, None, "zero", False),
    ("UI_08.png", "Wave_Banner",    "Frames",  92,  None, "auto", False),
    ("UI_09.png", ["Divider_Plain", "Divider_Diamond", "Divider_Header"], "Frames", None, 208, "zero", False),

    # ★★ 2026-08-26 — 로스터 전용 세 장 (볼트 `로스터 UI 프롬프트.md` 로 뽑았다).
    #
    #   `Btn_Roster_*` 는 <b>340×78</b> 이다 — 지금까지 로스터 행은 `Btn_Panel`(178×40)을
    #   그 크기로 늘려 쓰고 있어서 좌우 마개 장식이 뭉툭하고 세로 테두리가 두 배로 불었다.
    #   ⚠ 이름이 <c>Btn_&lt;계열&gt;_&lt;상태&gt;</c> 규약을 지켜야 <c>HudTheme.PaintButton</c> 이
    #     «고른 행/죽은 행» 을 스스로 갈아끼운다(도움말 분류 탭이 붙은 그 방식).
    # ⚠ 78 → <b>90</b> (2026-08-26 2차). 원화를 <b>직사각형으로 다시 뽑았다</b> — 첫 원화는
    #   좌우 마개 장식이 <b>각 49px</b> 이라 380px 행에서 98px(26%)를 장식이 먹었다
    #   (유저: *"캐릭터 선택 버튼 끝 부분 너무 쓸데없이 기니까"*). 새 원화는 좌우를 8px 로
    #   묶고 <b>장식을 위·아래 레일과 모서리로</b> 옮겨서 가운데를 넓혔다.
    ("BUTTON_07.png", "Btn_Roster_%s", "Buttons", 90, None, "auto", False),

    # ⚠ <b>부대 색 띠는 회색조로 왔다</b> — 코드가 부대 색을 <b>곱한다</b>. 색이 구워져
    #   있으면 곱한 결과가 탁해진다(`Bar_Fill` 이 흰 그라디언트인 것과 같은 규약).
    #   그래서 <b>팔레트 스냅에서 빼야 한다</b> — 16색 HUD 팔레트에 넣으면 회색 층이
    #   청록 계열로 끌려간다. 아래 GREY_ONLY 를 볼 것.
    ("UI_10.png", "Roster_SquadTab", "Frames", 78, None, "auto", True),

    # 얼굴 칸 액자 — 안쪽이 <b>완전히 뚫려 있어야</b> 한다(알파 이진화가 그 일을 한다).
    ("UI_11.png", "Roster_PortraitSlot", "Frames", 64, None, "auto", True),
]

#: ★★ <b>팔레트 스냅을 건너뛰는 그림</b> (2026-08-26 신설).
#:
#: 코드가 색을 <b>곱하는</b> 그림은 회색조로 와야 하고, 그 회색을 16색 HUD 팔레트에
#: 스냅하면 <b>청록 계열로 끌려간다</b> — 곱했을 때 부대 색이 탁해진다.
#: <c>Bar_Fill</c> 이 이미 :data:`GREYS` 로 따로 처리되던 것과 같은 이유이고,
#: 여기 적힌 이름은 그 <c>GREYS</c> 표를 쓴다.
GREY_ONLY = {"Bar_Fill", "Roster_SquadTab"}


def run():
    meta = []
    for fn, names, sub, h, w, mode, vert in JOBS:
        path = os.path.join(SRC, fn)
        if not os.path.exists(path):
            print(f"  ⚠ 없음: {fn}"); continue

        rgba, how = strip_background(path)
        want = [names % s for s in STATES] if isinstance(names, str) and "%s" in names \
            else (names if isinstance(names, list) else [names])
        bs = bands(rgba[:, :, 3])
        print("")
        print(f"■ {fn} [{how}] → 칸 {len(bs)} (기대 {len(want)})")
        if len(bs) != len(want):
            print("  ⚠ 칸 수 불일치 — 건너뜀"); continue

        # ① 잘라서 여백 제거
        #
        # ⚠ <b>자르기 전에 옅은 알파를 먼저 끊는다.</b> 키잉이 남긴 알파 1~127 짜리
        #   잔여물은 눈에 안 보이는데 getbbox 가 «내용» 으로 쳐서 여백이 안 잘린다.
        #   그러면 32×32 짜리 닫기 버튼이 79×32 로 나오고(실측), 뒤이어 높이 기준으로
        #   줄일 때 <b>가로가 두 배 넘게 늘어난 채</b> 확정된다.
        cuts = []
        for s0, e0 in bs:
            piece = rgba[s0:e0].copy()
            piece[piece[:, :, 3] < 128] = 0
            im = Image.fromarray(piece.astype(np.uint8), "RGBA")
            bb = im.getbbox()
            cuts.append(np.asarray(im.crop(bb) if bb else im).astype(np.float64))

        # ② 상태 4장은 같은 판 위에 올려 크기를 맞춘다 — «켜짐» 의 발광이 실루엣 밖으로
        #    번져 폭이 몇 픽셀 달라지는데, 그대로 두면 마우스를 올릴 때 버튼이 씰룩거린다.
        if len(want) == 4 and want[0].endswith("Normal"):
            W = max(c.shape[1] for c in cuts); H = max(c.shape[0] for c in cuts)
            padded = []
            for c in cuts:
                cv = np.zeros((H, W, 4));
                y = (H - c.shape[0]) // 2; x = (W - c.shape[1]) // 2
                cv[y:y + c.shape[0], x:x + c.shape[1]] = c
                padded.append(cv)
            cuts = padded

        # ③ 구분선처럼 «폭 기준» 인 묶음은 한 배율로 같이 줄여야 굵기가 안 갈린다
        common = None
        if w and len(cuts) > 1:
            common = w / max(c.shape[1] for c in cuts)

        palette = GREYS if want[0] in GREY_ONLY else PALETTE

        # ④ 낱장을 만든다 — 아직 저장하지 않는다.
        #    ★ 한 묶음(상태 4장)은 <b>크기와 경계를 통일</b>해야 한다. 상태마다 따로
        #      자르면 «켜짐» 의 발광이나 «올림» 의 호박색 선 때문에 bbox 가 몇 픽셀씩
        #      달라져, 마우스를 올릴 때 버튼이 씰룩거리고 장식 위치가 튄다(실측:
        #      Btn_Choice 가 44/50/52/50 으로 갈렸다).
        made = []
        for c in cuts:
            a = to_native(c, w=max(1, round(c.shape[1] * common))) if common                 else to_native(c, h=h, w=w)
            a = snap_palette(a, palette)
            made.append(binarize_alpha(a))

        # ⚠ 슬롯·구분선처럼 <b>서로 다른 그림</b>인 묶음은 크기를 맞추지 않는다 —
        #   맞추면 얇은 구분선이 22px 짜리 빈 칸을 이고 다니게 된다. 상태 4장일 때만 맞춘다.
        is_states = len(want) == 4 and want[0].endswith("Normal")
        if is_states and len(made) > 1:
            # 묶음의 <b>합집합</b> 렉트로 한 번에 자른다 — 낱장마다 따로 자르지 않는다.
            H = max(m.shape[0] for m in made); W = max(m.shape[1] for m in made)
            fixed = []
            for m in made:
                cv = np.zeros((H, W, 4))
                y = (H - m.shape[0]) // 2; x = (W - m.shape[1]) // 2
                cv[y:y + m.shape[0], x:x + m.shape[1]] = m
                fixed.append(cv)
            made = fixed

        # ⑤ 경계도 묶음에서 <b>한 값</b>으로 정한다 — 상태마다 재면 한 장이 0 으로
        #    떨어지는 일이 생긴다(발광 때문에 «평평한 구간» 검출이 실패). 가장 넉넉한
        #    값을 쓰면 어느 상태에서도 장식이 안 늘어난다.
        if mode == "zero":
            L = R = T = B = 0
        else:
            ls, rs, ts, bs2 = [], [], [], []
            for m in made:
                hz = auto_border(m, True)
                if hz: ls.append(hz[0]); rs.append(hz[1])
                if vert:
                    vt = auto_border(m, False)
                    if vt: ts.append(vt[0]); bs2.append(vt[1])
            L = max(ls) if ls else 0
            R = max(rs) if rs else 0
            T = max(ts) if ts else 0
            B = max(bs2) if bs2 else 0
            if want[0] in MANUAL_VERT:
                T, B = MANUAL_VERT[want[0]]
            if want[0] in MANUAL_HORIZ:
                L, R = MANUAL_HORIZ[want[0]]

        for m, nm in zip(made, want):
            if nm in CROP_BOTTOM:
                m = m[:CROP_BOTTOM[nm]]
            im = Image.fromarray(m.astype(np.uint8), "RGBA")
            d = os.path.join(DST, sub); os.makedirs(d, exist_ok=True)
            im.save(os.path.join(d, nm + ".png"))
            ncol = len(np.unique(m[:, :, :3].reshape(-1, 3), axis=0))
            print(f"  {sub}/{nm:22s} {im.width:4d}x{im.height:4d}  경계 L{L} R{R} T{T} B{B}  색 {ncol}")
            meta.append(dict(path=f"Assets/_Project/Resources/UI/{sub}/{nm}.png",
                             border=[L, B, R, T]))   # 유니티 spriteBorder = (x=L, y=B, z=R, w=T)

    os.makedirs(os.path.join(PROJ, "Temp"), exist_ok=True)
    with open(os.path.join(PROJ, "Temp", "ui_sprite_cut.json"), "w", encoding="utf-8") as f:
        # ⚠ 유니티 JsonUtility 는 최상위 배열을 못 읽는다 — 반드시 감싸서 준다.
        json.dump({"items": meta}, f, ensure_ascii=False, indent=1)
    print("")
    print(f"총 {len(meta)}장 → Temp/ui_sprite_cut.json")


if __name__ == "__main__":
    run()
