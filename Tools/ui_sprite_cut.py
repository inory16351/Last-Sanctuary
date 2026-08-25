# -*- coding: utf-8 -*-
"""
UI 스프라이트 시트 → 개별 PNG (자홍 키잉 + 슬라이스 + 9-슬라이스 경계 산출)

생성 AI 가 자홍(#FF00FF) 바탕에 뽑아준 시트를 유니티가 쓸 낱장으로 자른다.
 ① 자홍을 알파로 (경계에 밴 자홍까지 색에서 빼낸다 — alpha_key 참조)
 ② 시트 안의 «칸» 을 세로/가로로 자동 분리
 ③ 9-슬라이스 경계를 산출해 Temp/ui_sprite_cut.json 에 적는다
    → 그 값을 Editor/UiSpriteImporter.cs 가 읽어 임포터에 박는다

⚠ 전부 <b>2배 크기</b>로 내보내고 PPU 를 200 으로 준다. 그래야 경계(장식)가
   화면에서 표시 크기의 절반으로 그려져 «장식이 버튼보다 큰» 사고가 안 난다.

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


def alpha_key(rgb):
    """자홍 바탕 → 알파. 섞인 자홍은 색에서 되빼낸다.

    P = a·F + (1-a)·K (K=255,0,255). 순수 자홍은 min(R,B)-G 가 255,
    회색·검정은 0, 붉은 균열(R 만 큼, B 낮음)도 0 근처 —
    그래서 붉은 장식을 갉아먹지 않는다.
    """
    a = rgb.astype(np.float64)
    mag = np.clip((np.minimum(a[:, :, 0], a[:, :, 2]) - a[:, :, 1]) / 255.0, 0, 1)
    alpha = 1.0 - mag
    out = np.zeros(a.shape[:2] + (4,), np.float64)
    safe = np.maximum(alpha, 1e-4)[:, :, None]
    out[:, :, :3] = np.clip((a - (1.0 - alpha)[:, :, None] * np.array([255.0, 0.0, 255.0])) / safe, 0, 255)
    # ⚠ 아주 옅은 알파(1~24)를 그냥 두면 안 된다 — 눈에는 안 보이는데
    #   ① getbbox 가 «내용» 으로 쳐서 여백이 안 잘리고
    #   ② 어두운 배경 위에서 자홍 기운이 낀 후광으로 보인다.
    #   문턱 아래는 지우고, 남은 것은 0~1 로 다시 펴서 가장자리를 매끄럽게 둔다.
    alpha = np.clip((alpha - 0.10) / 0.90, 0, 1)
    out[:, :, 3] = alpha * 255.0
    out[alpha <= 0.0] = 0
    return out.astype(np.uint8)


def bands(alpha, vertical=True, gap=24, minrun=24):
    """알파가 실제로 «찬» 띠만 찾는다.

    ⚠ any() 로 하면 안 된다 — 키잉 뒤 한두 픽셀짜리 잔여물이 칸 사이에 남아
      시트 전체가 한 덩어리로 붙어버린다(실측). 줄당 개수로 걸러야 한다.
    """
    solid = alpha > 24
    cnt = solid.sum(axis=1 if vertical else 0)
    need = max(12, int(0.02 * (solid.shape[1] if vertical else solid.shape[0])))
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


def auto_border(a, horizontal=True, tol=7.0):
    """가운데와 «똑같은» 열/행이 이어지는 만큼이 늘려도 되는 구간. 그 바깥이 경계."""
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
    if l + r > 0.62 * n: return None          # 평평한 구간이 없다 → 손으로 줘야 한다
    return l, r


#: 자를 것.  frac=(L,R,T,B) 는 <b>내보낸 크기 대비 비율</b> 로 준 9-슬라이스 경계.
#:
#: ⚠ 자동 측정을 쓰지 않는다 — 금속 질감의 얼룩 때문에 «똑같은 열» 이 두 개도 없어서
#:   0 아니면 판 전체가 나온다(실측). 그림을 보고 장식이 끝나는 지점을 비율로 준다.
#: ⚠ 경계가 0 인 것들은 <b>늘리지 않는 것</b>이다 — 닫기 버튼(32~44 정사각) · 슬롯 ·
#:   구분선은 9-슬라이스를 걸면 L+R 이 표시 폭보다 커져서 오히려 깨진다.
#: gap/minrun 은 칸 사이 여백이 좁거나(슬롯) 칸이 아주 얇을 때(구분선) 준다.
JOBS = [
    # 파일,          이름들,                폴더,      높이, 너비, 경계(비율),           gap, minrun
    ("BUTTON_02.png", ["Btn_Action_%s"], "Buttons", 80,  None, (.19, .19, 0, 0),  24, 24),
    ("BUTTON_01.png", ["Btn_Panel_%s"],  "Buttons", 100, None, (.21, .21, 0, 0),  24, 24),
    ("BUTTON_03.png", ["Btn_Chip_%s"],   "Buttons", 64,  None, (.12, .12, 0, 0),  24, 24),
    ("BUTTON_04.png", ["Btn_Close_%s"],  "Buttons", 80,  None, (0, 0, 0, 0),      24, 24),
    ("BUTTON_05.png", ["Btn_Choice_%s"], "Buttons", 104, None, (.13, .13, 0, 0),  24, 24),
    ("BUTTON_06.png", ["Btn_Wide_%s"],   "Buttons", 184, None, (.19, .19, 0, 0),  24, 24),

    ("UI_01.png", ["Win_Frame"],      "Frames", None, 960, (.113, .113, .124, .124), 24, 24),
    ("UI_02.png", ["Hud_Plate"],      "Frames", None, 512, (.06, .06, .06, .06),     24, 24),
    ("UI_03.png", ["Bar_Track"],      "Frames", 68,   None, (.05, .05, 0, 0),        24, 24),
    ("UI_04.png", ["Bar_Fill"],       "Frames", 32,   None, (0, 0, 0, 0),            24, 24),
    ("UI_05.png", ["Portrait_Frame"], "Frames", None, 300, (.075, .075, .20, .09),   24, 24),
    ("UI_06.png", ["Minimap_Bezel"],  "Frames", None, 320, (.10, .10, .10, .10),     24, 24),
    ("UI_07.png", ["Slot_Empty", "Slot_Filled", "Slot_Locked"],          "Frames", 96,  None, (0, 0, 0, 0), 10, 24),
    ("UI_08.png", ["Divider_Plain", "Divider_Diamond", "Divider_Header"], "Frames", None, 420, (0, 0, 0, 0), 24, 8),
]
STATES = ["Normal", "Hover", "On", "Off"]


def run():
    meta = []
    for fn, names, sub, h, w, frac, gap, minrun in JOBS:
        rgb = np.asarray(Image.open(os.path.join(SRC, fn)).convert("RGB"))
        rgba = alpha_key(rgb)
        want = [n % s for n in names for s in STATES] if "%s" in names[0] else names
        bs = bands(rgba[:, :, 3], True, gap, minrun)
        print("")
        print(f"■ {fn} → 칸 {len(bs)} (기대 {len(want)})")
        if len(bs) != len(want):
            print("  ⚠ 칸 수 불일치 — 건너뜀"); continue

        # 잘라서 여백만 제거한 낱장들
        cuts = []
        for (s0, e0) in bs:
            im = Image.fromarray(rgba[s0:e0, :, :], "RGBA")
            bb = im.getbbox()
            cuts.append(im.crop(bb) if bb else im)

        # ★ 상태 4장은 <b>같은 판 위에</b> 올려 크기를 맞춘다.
        #   «켜짐» 의 청록 발광이 실루엣 밖으로 번져 폭이 몇 픽셀 달라지는데,
        #   그대로 두면 마우스를 올릴 때마다 버튼이 씰룩거린다.
        if "%s" in names[0]:
            W = max(c.width for c in cuts); H = max(c.height for c in cuts)
            padded = []
            for c in cuts:
                cv = Image.new("RGBA", (W, H), (0, 0, 0, 0))
                cv.paste(c, ((W - c.width) // 2, (H - c.height) // 2))
                padded.append(cv)
            cuts = padded

        for im, nm in zip(cuts, want):
            if h: im = im.resize((max(1, round(im.width * h / im.height)), h), Image.LANCZOS)
            elif w: im = im.resize((w, max(1, round(im.height * w / im.width))), Image.LANCZOS)
            d = os.path.join(DST, sub); os.makedirs(d, exist_ok=True)
            im.save(os.path.join(d, nm + ".png"))
            L, R = round(frac[0] * im.width), round(frac[1] * im.width)
            T, B = round(frac[2] * im.height), round(frac[3] * im.height)
            print(f"  {sub}/{nm:22s} {im.width:4d}x{im.height:4d}  경계 L{L} R{R} T{T} B{B}")
            meta.append(dict(path=f"Assets/_Project/Resources/UI/{sub}/{nm}.png",
                             border=[L, B, R, T]))   # 유니티 spriteBorder = (x=L, y=B, z=R, w=T)
    os.makedirs(os.path.join(PROJ, "Temp"), exist_ok=True)
    with open(os.path.join(PROJ, "Temp", "ui_sprite_cut.json"), "w", encoding="utf-8") as f:
        # ⚠ 유니티 JsonUtility 는 <b>최상위 배열</b>을 못 읽는다 — 반드시 감싸서 준다.
        json.dump({"items": meta}, f, ensure_ascii=False, indent=1)
    print("")
    print(f"총 {len(meta)}장 → Temp/ui_sprite_cut.json")


if __name__ == "__main__":
    run()
