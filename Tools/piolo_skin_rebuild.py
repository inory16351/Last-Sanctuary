# -*- coding: utf-8 -*-
"""피올로 스킨 원화를 **분석해서 4배 해상도로 재구성**한다 (2026-08-13).

**왜 필요한가** — 캐릭터 4명 중 피올로만 원화가 `64x64` 다(엘린 189x114 · 비기오르
171x135 · 프레이야 218x147). 게임 안 크기는 전부 세로 2.14 타일로 같으므로,
**한 타일에 들어가는 픽셀 수**가 피올로만 1/3 이다:

    엘린   130px(실그림) ÷ 2.14타일 = 60.7 px/타일
    피올로  45px(실그림) ÷ 2.14타일 = 21.0 px/타일   ← 여기만 3배 성기다

거기에 임포트 설정이 `filterMode: 0`(Point)이라 원본 한 픽셀이 화면에서 딱딱한
사각 덩어리로 확대된다. "해상도가 너무 안 좋다"는 지적의 실체가 이 두 가지다.

**무엇을 하는가**
  ① 원본을 분석한다 — 알파 경계 · 반투명 비율 · 실제 그림이 캔버스에서 차지하는 비율
  ② **4배(64 → 256)로 재구성**한다. 아래 '재구성 방식' 참조
  ③ `.meta` 의 `spritePixelsToUnits` 를 **21 → 84 로 같이 4배** 한다

③ 이 핵심이다 — 게임 안 크기 기준(`CharacterSkinSO.contentSizeTiles`)은
`알파 경계 픽셀 ÷ PPU` 로 계산되므로(`measure_skin_tiles.py`), 픽셀과 PPU 를 **같은
배수로** 올리면 **타일 크기가 정확히 그대로**다. 스킨 에셋도 씬도 건드릴 필요가 없다.

**재구성 방식** (원본이 픽셀아트가 아니라 *축소된 채색 원화*라는 분석 결과에 맞춘 것)
  · 알파 프리멀티플 후 확대 — 안 하면 투명 영역의 검은 RGB 가 경계로 번져 어두운 테가 생긴다
  · Lanczos 4배 — 계단이 아니라 원본의 부드러운 그라데이션을 이어서 복원한다
  · 언샤프 마스크를 **알파가 진한 부분에만** 건다 — 몸/모자/부리의 윤곽은 또렷해지고,
    등불 빛무리·바닥 그림자 같은 반투명 부분은 뭉개거나 링이 생기지 않는다
  · 알파는 **실루엣을 조이지 않는다**(부드러운 확대만) — 이 원화는 반투명 픽셀이 95% 라
    억지로 조이면 빛무리와 날개 깃털이 잘려나간다. 다만 Lanczos 링잉이 원화 **바깥**에
    흘리는 알파 1~2/255 짜리 실오라기만 잘라낸다(`ALPHA_CUT`) — 눈에는 안 보이지만
    알파 경계를 넓혀 몸집 실측값을 2.8% 부풀렸다. 자르고 나면 원본과 같은 크기다
    (2.476 x 2.143 → 2.476 x 2.131 타일).

⚠ **원본은 볼트에서 읽고 Assets 로 쓴다** — 몇 번을 돌려도 결과가 같고(멱등)
   원본이 상할 일이 없다. `crop_illust_faces.py` 와 같은 방식이다.
⚠ guid 를 유지하려고 **파일 경로·이름을 그대로 덮어쓴다** — 스킨 에셋(`Skin_Piolo.asset`)의
   스프라이트 참조 66개를 하나도 안 건드린다(진행상황 8절 1번의 그 방법).

사용법:  python Tools/piolo_skin_rebuild.py            (재구성 + .meta PPU 갱신)
         python Tools/piolo_skin_rebuild.py --analyze  (분석만, 파일은 안 쓴다)
"""

import os
import re
import sys

import numpy as np
from PIL import Image, ImageFilter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 원본(볼트) → 결과(Assets). 원본은 절대 쓰지 않는다.
SRC = r"C:\Project\Last-Sanctuary-Vault\리소스\asset\char_asset\Char_Asset_Piolo\Char"
DST = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Piolo", "Char")

SCALE = 4                    # 64 → 256. 2의 거듭제곱이라 리샘플 찌꺼기가 없다
BASE_PPU = 21                # 원본 .meta 값 — 결과는 BASE_PPU * SCALE
UNSHARP_RADIUS = 2.0
UNSHARP_PERCENT = 95
UNSHARP_THRESHOLD = 2

# 언샤프를 걸 알파 하한. 이보다 옅은 곳(빛무리·그림자)은 원본 그대로 둔다.
SHARPEN_ALPHA_MIN = 0.55

# 이보다 옅은 알파는 0 으로 잘라낸다 (Lanczos 링잉이 남기는 보이지 않는 실오라기).
ALPHA_CUT = 2.0 / 255.0

# 볼트 원본은 아트팩 이름(`Char_kim_…`)을 그대로 쓰고, 임포트할 때 그 토큰이 빠졌다
# (`Char_…`). Assets 쪽 파일명은 스킨 에셋이 guid 로 참조하므로 **바꾸면 안 된다** —
# 원본을 찾을 때만 이 규칙으로 이름을 옮긴다.
SRC_NAME_TOKEN = "Char_kim_"
DST_NAME_TOKEN = "Char_"


# ---------------------------------------------------------------------------
# ① 분석
# ---------------------------------------------------------------------------

def analyze(path):
    """원화 한 장의 상태를 잰다 — 캔버스 대비 그림 크기 · 반투명 비율."""
    with Image.open(path) as im:
        rgba = im.convert("RGBA")
        box = rgba.getbbox()
        a = np.asarray(rgba, dtype=np.uint8)[..., 3]

    visible = a > 0
    partial = visible & (a < 255)
    return {
        "canvas": rgba.size,
        "content": (box[2] - box[0], box[3] - box[1]) if box else (0, 0),
        "partial_ratio": float(partial.sum()) / max(1, int(visible.sum())),
    }


# ---------------------------------------------------------------------------
# ② 재구성
# ---------------------------------------------------------------------------

def rebuild(path):
    """64x64 원화 한 장 → 256x256 재구성본(RGBA)."""
    src = Image.open(path).convert("RGBA")
    arr = np.asarray(src, dtype=np.float32) / 255.0
    rgb, alpha = arr[..., :3], arr[..., 3:4]

    # 알파 프리멀티플 — 투명 영역의 RGB(대개 검정)가 확대되면서 경계로 번지는 것을 막는다.
    premul = np.concatenate([rgb * alpha, alpha], axis=2)

    big = Image.fromarray((premul * 255.0 + 0.5).astype(np.uint8), "RGBA").resize(
        (src.width * SCALE, src.height * SCALE), Image.LANCZOS)

    out = np.asarray(big, dtype=np.float32) / 255.0
    up_alpha = out[..., 3:4]

    # Lanczos 는 링잉 때문에 알파 1~2/255 짜리 실오라기를 원화 바깥으로 몇 px 흘린다.
    # 눈에는 안 보이지만 **알파 경계가 넓어져 몸집 실측값이 커진다**(`measure_skin_tiles.py`
    # 가 그 경계로 타일 크기를 잰다 — 실제로 세로가 2.8% 커졌다). 잘라내야 원본과 같은 크기다.
    up_alpha = np.where(up_alpha < ALPHA_CUT, 0.0, up_alpha)
    out = np.concatenate([out[..., :3], up_alpha], axis=2)

    # 언프리멀티플. 알파가 0 에 가까운 곳은 나누면 노이즈가 폭발하므로 하한을 둔다.
    up_rgb = np.clip(out[..., :3] / np.maximum(up_alpha, 1e-3), 0.0, 1.0)

    # Lanczos 는 필연적으로 조금 물러진다 — 언샤프로 되돌린다.
    # 단 **알파가 진한 부분에만** 섞는다: 빛무리·그림자에 걸면 링(halo)이 눈에 띈다.
    rgb_img = Image.fromarray((up_rgb * 255.0 + 0.5).astype(np.uint8), "RGB")
    sharp = np.asarray(
        rgb_img.filter(ImageFilter.UnsharpMask(UNSHARP_RADIUS, UNSHARP_PERCENT,
                                               UNSHARP_THRESHOLD)),
        dtype=np.float32) / 255.0

    mask = np.clip((up_alpha - SHARPEN_ALPHA_MIN) / (1.0 - SHARPEN_ALPHA_MIN), 0.0, 1.0)
    final_rgb = up_rgb * (1.0 - mask) + sharp * mask

    final = np.concatenate([final_rgb, up_alpha], axis=2)
    return Image.fromarray((np.clip(final, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGBA")


# ---------------------------------------------------------------------------
# ③ .meta 의 PPU — 픽셀과 같은 배수로 올려야 게임 안 크기가 안 변한다
# ---------------------------------------------------------------------------

def patch_meta(png_path, ppu):
    meta = png_path + ".meta"
    if not os.path.isfile(meta):
        return False
    with open(meta, encoding="utf-8") as f:
        text = f.read()

    patched = re.sub(r"(spritePixelsToUnits:\s*)([0-9.]+)", r"\g<1>%d" % ppu, text, count=1)
    if patched == text:
        return False
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(patched)
    return True


# ---------------------------------------------------------------------------

def main():
    analyze_only = "--analyze" in sys.argv
    if not os.path.isdir(SRC):
        print("원본 폴더를 찾지 못했습니다: %s" % SRC)
        return 1

    total = metas = 0
    for motion in sorted(os.listdir(SRC)):
        src_dir = os.path.join(SRC, motion)
        dst_dir = os.path.join(DST, motion)
        if not os.path.isdir(src_dir) or not os.path.isdir(dst_dir):
            continue

        frames = sorted(f for f in os.listdir(src_dir) if f.endswith(".png"))
        if not frames:
            continue

        info = analyze(os.path.join(src_dir, frames[0]))
        print("[%-16s] %2d프레임  캔버스 %s → 그림 %s  반투명 %.0f%%  →  %dx%d" % (
            motion, len(frames), "x".join(map(str, info["canvas"])),
            "x".join(map(str, info["content"])), info["partial_ratio"] * 100,
            info["canvas"][0] * SCALE, info["canvas"][1] * SCALE))

        if analyze_only:
            continue

        for name in frames:
            dst = os.path.join(dst_dir, name.replace(SRC_NAME_TOKEN, DST_NAME_TOKEN))
            if not os.path.isfile(dst):
                print("   ⚠ Assets 에 없는 프레임이라 건너뜀: %s" % name)
                continue
            rebuild(os.path.join(src_dir, name)).save(dst)
            total += 1
            metas += patch_meta(dst, BASE_PPU * SCALE)

    if analyze_only:
        return 0

    print("\n%d프레임 재구성 · .meta %d개 PPU %d → %d" %
          (total, metas, BASE_PPU, BASE_PPU * SCALE))
    print("게임 안 크기는 그대로다(픽셀 %d배 · PPU %d배). Unity 에서 Assets/Refresh 를 실행할 것."
          % (SCALE, SCALE))
    return 0


if __name__ == "__main__":
    sys.exit(main())
