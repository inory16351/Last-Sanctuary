# -*- coding: utf-8 -*-
"""히스톤(9005) 스프라이트 시트를 프레임 단위로 잘라 스킨 프레임을 만든다 (2026-08-14).

원본은 **한 장짜리 모션 시트**다(`리소스/asset/char_asset/Char_Asset_Histon/histon_motion.png`,
1536x1024). 지금까지 캐릭터들은 프레임이 낱장으로 온 팩이었으므로 이 형태는 처음이다.

시트 구성 — 5행 x 6열, 왼쪽에 한글 행 라벨, 위에 열 번호(1~6):

    근거리 공격 모션   → MeleeAttack
    대기 모션          → Idle
    이동 모션          → Walk
    부활 모션          → Revive      ★ 신규 모션 (스킬 Rage_on 이 쓴다)
    부활 시 주변 이펙트 → ReviveFx    ★ 신규 (스킬 Reaver 의 범위 연출)

★★ 왜 단순 격자 슬라이스를 못 쓰나 (이 파일의 존재 이유)
────────────────────────────────────────────────────────────
열 중심은 헤더 숫자로 정확히 잡힌다 — 236.5 부터 225px 간격. 그런데 **근거리 공격 모션의
3·4번 프레임이 셀 경계를 넘는다**: 3번의 검격 궤적이 오른쪽으로 +150px, 4번의 찌르기 검신이
+173px 까지 뻗는데 반칸은 112.5px 뿐이다. 격자로 자르면 **4번 프레임의 검이 반토막 난다**
(실제로 잘라 눈으로 확인했다).

그렇다고 셀을 넓히면 3번의 궤적이 4번 칸에 딸려 들어온다 — 두 프레임은 x 축에서 실제로
겹쳐 있어서 **빈 열이 아예 없다**(임계값을 60까지 올려도 최소 9픽셀이 남는다). 즉 직선
한 줄로는 절대 못 나눈다.

그래서 **경계를 격자에 고정하지 않고, 프레임 사이에서 내용이 가장 옅은 열에서 자른다**:
  ① 격자 경계 ±`BOUNDARY_SEARCH` 안에서 열별 픽셀 수를 센다
  ② 그중 가장 적은 열을 실제 경계로 쓴다 (동률이면 격자에 가까운 쪽)
3·4번 사이는 x≈855 부근이 최소(9픽셀)라 거기서 갈리고, 4·5번 사이는 진짜 빈 구간(1083~1091)이
있어 0에서 갈린다. 결과적으로 **4번의 검신(+173px)이 온전히 남는다.**

⚠ 처음엔 연결 성분(connected component)으로 소유권을 나눠 봤는데 실패했다 —
   검격 궤적이 3번과 4번 캐릭터를 **하나의 덩어리로 이어버려서** 두 프레임이 한 주인이 되고
   캔버스가 796px 까지 부풀었다. 같은 방법을 다시 시도하지 말 것.

캔버스는 행마다 "모든 프레임이 안 잘리는 최소 크기"로 잡고, **열 중심을 캔버스 가로 중앙에,
행 바닥을 캔버스 아래에** 맞춘다 — 피벗이 (0.5, 0) 발밑이라(다른 캐릭터와 동일) 바닥을
맞춰야 모션이 바뀔 때 캐릭터가 위아래로 튀지 않는다.

왼쪽 방향 프레임은 **좌우 반전**으로 만든다(원본이 오른쪽을 보고 있다). 피올로·엘린과 같다.

⚠ 다시 돌려도 결과가 같다(멱등). guid 는 경로 md5 라 프레임을 다시 만들어도 스킨 에셋의
   참조가 안 끊긴다 — `char_asset_preyja_build.guid_for` 와 같은 규칙을 그대로 쓴다.

사용법:  python Tools/histon_skin_build.py
"""

import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vault_path import find_art                                       # noqa: E402
from char_asset_preyja_build import guid_for, png_meta, FOLDER_META   # noqa: E402

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def find_vault():
    """
    볼트 위치. **PC 마다 다르다** — 이 저장소의 도구들이 전부
    ``C:\\Project\\Last-Sanctuary-Vault`` 를 박아 두었는데 그 경로가 없는 PC 가 실재한다
    (2026-08-15: 볼트가 ``H:\\c팀\\Last-Sanctuary-Vault`` 였다). 그래서 고정하지 않고 찾는다:

      ① 환경변수 ``LAST_SANCTUARY_VAULT``
      ② 유니티 프로젝트의 **형제 폴더** ``Last-Sanctuary-Vault``  ← 보통 여기다
      ③ 옛 고정 경로 (기존 PC 호환)
    """
    cands = []
    env = os.environ.get("LAST_SANCTUARY_VAULT")
    if env:
        cands.append(env)
    cands.append(os.path.join(os.path.dirname(PROJECT), "Last-Sanctuary-Vault"))
    cands.append(r"C:\Project\Last-Sanctuary-Vault")
    for c in cands:
        if os.path.isdir(c):
            return c
    return cands[-1]


VAULT = find_vault()

# 원본은 볼트가 정본이다(유저 지시 2026-08-14: "볼트에도 에셋 리소스 넣어주고").
SRC = find_art("Char_Asset_Histon", "histon_motion.png")

DST = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset", "Char_Asset_Histon", "Char")
DST_REL = "Assets/_Project/Art/Char_Asset/Char_Asset_Histon/Char"

# 열 중심 — 헤더 숫자(1~6)의 무게중심을 재서 얻은 값. 간격이 딱 225px 로 떨어진다.
COL0, COL_STEP, COL_COUNT = 236.5, 225.0, 6

# 행 — (모션 이름, y 시작, y 끝, 좌우 반전본을 만들지)
#
# ⚠ y 시작은 <b>열 번호(1~6) 아래</b>다. 시트에는 행마다 위에 번호가 찍혀 있어서,
#   내용 밴드를 그대로 쓰면 <b>번호가 프레임에 딸려 들어간다</b>(실제로 한 번 그렇게 나왔다).
#   번호 밴드를 실측해 잘라냈다:  대기 227~239 · 이동 428~440 · 부활 614~627 · 이펙트 801~813.
#   근거리 공격만 번호가 시트 맨 위 헤더(19~43)에 있어 밴드 밖이다.
# ReviveFx 는 방향이 없는 지면 연출이라 반전본을 만들지 않는다.
ROWS = [
    ("MeleeAttack", 48, 203, True),
    ("Idle", 246, 402, True),
    ("Walk", 444, 590, True),
    ("Revive", 628, 777, True),
    ("ReviveFx", 815, 999, False),
]

# 왼쪽 행 라벨("대기 모션" 등)은 x < 175 에 있고 각 행의 <b>맨 윗줄과 y 가 겹친다</b>.
# 그 구석만 지운다 — 캐릭터 날개는 x 156 까지 오지만 그건 훨씬 아래쪽이라 안 걸린다.
LABEL_X = 175
LABEL_ROWS = 10

# ★★ 알파 — 밝기 한 개로는 절대 못 가른다 (2026-08-14 "투명하다" → 2026-08-15 "다리가 투명하다")
#
# 이 원화에는 성질이 다른 세 가지가 섞여 있다. 실측값:
#
#   배경        밝기 0~1   (완전히 검지 않다 — 1 짜리 노이즈가 화면 전체에 깔려 있다)
#   검은 갑옷   밝기 0~   (<b>순흑 픽셀이 9%</b>. 배경과 밝기가 겹친다)
#   연무        밝기 2~11  (검격 궤적 뒤에 깔린 희미한 연기)
#
# 시도한 것과 실패한 이유 — <b>같은 길을 다시 가지 말 것</b>:
#
#   ① 밝기 임계값 하나 (24 이상 불투명)     → 갑옷에 구멍이 숭숭 뚫린다
#   ② 실루엣 + <b>갇힌 구멍만</b> 메우기     → 2026-08-14 에 쓴 방법. <b>다리가 날아갔다</b> —
#      하반신 로브가 배경과 같은 순흑이라 밝기 2 미만으로 탈락하는데, 다리 사이 틈이
#      <b>바깥 배경과 이어져 있어</b> "갇힌 구멍"이 아니다. 실제로 생성된 프레임을 전수
#      조사하니 <b>닫힌 구멍이 0개</b>였다 — 채울 대상 자체가 없었던 것이다.
#   ③ 밝기 램프 (어두울수록 반투명)          → 갑옷이 통째로 비친다. 갑옷이 진짜 어둡기 때문
#
# 지금 방식 — <b>몸통은 모양으로, 연무는 밝기로</b> 나눠서 판단한다:
#
#   body = 채우기(원형닫힘(밝기 >= BODY_MIN))   → 알파 255
#          닫힘이 다리 사이 틈을 <b>먼저 봉합</b>하므로 그 다음 채우기가 실제로 먹는다.
#          이게 ② 가 놓친 부분이다. 반경 4 = 폭 8px 까지의 틈을 메운다.
#   haze = body 밖의 밝기 HAZE_MIN~BODY_MIN     → 밝기에 비례하는 알파 (서서히 사라진다)
#          연무를 불투명하게 만들면 검격 궤적 안쪽이 <b>검은 블록</b>이 된다(옛 미결 186).
#   그 외                                        → 투명
#
# ⚠ HAZE_MIN 은 <b>2 가 아니라 4</b> 다 — 배경 노이즈가 1 까지 올라오므로 2 로 잡으면
#   캐릭터 둘레에 희미한 검은 테가 생긴다. 실측으로 4 에서 테가 사라지는 것을 확인했다.
HAZE_MIN = 4      # 이 밝기 미만은 배경으로 버린다
BODY_MIN = 12     # 이 밝기 이상은 확실한 몸통 — 닫힘·채우기의 씨앗이 된다
CLOSE_RADIUS = 4  # 원형 닫힘 반경. 사각 커널은 윤곽이 각져서 원형을 쓴다

# 프레임 경계를 격자에서 바꿔야 하는 행만 여기에 적는다(frame_bounds 주석 참조).
#   근거리 공격 — 3번의 검격 궤적과 4번의 검신이 격자를 넘는다.
#   부활 이펙트 — 6번의 잔광이 마지막 격자선(1474)을 넘어 1493 까지 간다.
BOUNDS_OVERRIDE = {
    "MeleeAttack": [124, 349, 583, 841, 1088, 1249, 1474],
    "ReviveFx":    [124, 349, 574, 799, 1024, 1249, 1499],
}

PPU = 80             # 대기 프레임 세로 176px → 약 2.2 타일. 다른 캐릭터(2.13~2.26)와 같은 대역


def disk(r):
    """반경 r 의 원형 구조 요소. 사각형을 쓰면 윤곽에 각진 계단이 남는다."""
    y, x = np.ogrid[-r:r + 1, -r:r + 1]
    return (x * x + y * y) <= r * r + r


def build_alpha(lum):
    """
    프레임 하나의 알파(0~255). 위 HAZE_MIN/BODY_MIN 주석이 근거다.

    ⚠ <b>프레임마다 따로</b> 불러야 한다. 띠 전체에 닫힘을 걸면 x 축에서 겹치는
       이웃 프레임(근거리 공격 3·4번)이 하나로 이어져 소유권이 뒤섞인다.
    """
    strong = lum >= BODY_MIN
    grown = ndimage.binary_dilation(strong, structure=disk(CLOSE_RADIUS))
    # border_value=1 — 프레임 가장자리에 닿은 그림이 침식으로 깎이지 않게 한다.
    sealed = ndimage.binary_erosion(grown, structure=disk(CLOSE_RADIUS), border_value=1)
    body = ndimage.binary_fill_holes(sealed)

    alpha = np.zeros(lum.shape, dtype=np.uint8)
    haze = (~body) & (lum >= HAZE_MIN)
    span = BODY_MIN - HAZE_MIN
    ramp = (lum[haze].astype(np.int32) - HAZE_MIN + 1) * 255 // span
    alpha[haze] = np.clip(ramp, 0, 255).astype(np.uint8)
    alpha[body] = 255
    return alpha


def frame_bounds(name):
    """
    이 행의 프레임 경계 7개. 기본은 격자(124 + 225*i)이고, 필요한 행만 표로 덮어쓴다.

    ★★ 왜 자동 탐지를 안 쓰나 — 세 번 시도해서 세 번 다 틀렸다. 근거를 남긴다.

    ① <b>격자 그대로</b> → 근거리 공격 4번의 검신이 반칸(112px)을 <b>170px</b> 넘어 반토막 난다.
    ② <b>연결 성분(덩어리)으로 소유권 배정</b> → 임계 10 이면 검격 궤적이 3·4번 캐릭터를
       하나로 이어버리고(x 595~1082), 임계 20 으로 올려 갈라도 <b>궤적 덩어리의 무게중심이
       경계(x≈800)에 걸려</b> 통째로 4번 것이 된다 — 3번은 궤적을 잃고 4번엔 남의 궤적이 붙는다.
    ③ <b>세로선 자동 탐지(가장 옅은 열 / 빈 구간)</b> → 두 프레임 사이에 <b>빈 열이 아예 없고</b>
       (임계 60에서도 최소 9픽셀), 궤적의 얇은 부분(x≈745, 19픽셀)이 프레임 사이보다 옅어서
       <b>궤적 한가운데</b>가 경계로 뽑힌다.

    자동 탐지가 통하지 않는 이유는 하나다 — <b>프레임끼리 x 축에서 실제로 겹쳐 있다.</b>
    그래서 눈으로 재서 표에 박았다. 값의 근거(근거리 공격 행):
        3번 궤적 끝 ≈ 838 · 4번 날개 시작 ≈ 845  → 그 사이 841 에서 자른다
        4번 검신 끝 ≈ 1082 · 5번 시작 = 1092     → 1088 에서 자른다
    ⚠ 원화를 다시 받으면 이 표도 다시 재야 한다.
    """
    grid = [int(round(COL0 - COL_STEP / 2 + COL_STEP * i)) for i in range(COL_COUNT + 1)]
    return BOUNDS_OVERRIDE.get(name, grid)


def build_row(rgb, y0, y1, name):
    """행 하나를 프레임 6장(오른쪽 방향)으로 자른다."""
    band = rgb[y0:y1 + 1]
    lum = band.max(axis=2).copy()
    lum[:LABEL_ROWS, :LABEL_X] = 0          # 왼쪽 행 라벨 제거 (위 LABEL_X 주석 참조)
    bounds = frame_bounds(name)

    # 알파는 <b>프레임마다 따로</b> 잰다 (build_alpha 주석 참조 — 띠 전체에 걸면 겹친
    # 이웃 프레임이 닫힘으로 이어져 버린다).
    alphas = [build_alpha(lum[:, bounds[i]:bounds[i + 1]]) for i in range(COL_COUNT)]

    # 캔버스 크기 — 모든 프레임이 안 잘리는 최소 폭(열 중심 기준 좌우 최대 확장)
    half, spans = 0.0, []
    for i in range(COL_COUNT):
        lo, hi = bounds[i], bounds[i + 1]
        sub = alphas[i] > 0
        if not sub.any():
            spans.append(None)
            continue
        xs = np.where(sub.any(axis=0))[0]
        gx0, gx1 = lo + int(xs[0]), lo + int(xs[-1])
        spans.append((lo, hi, gx0, gx1))
        c = COL0 + COL_STEP * i
        half = max(half, c - gx0, gx1 - c)
    width = int(np.ceil(half)) * 2 + 4        # 좌우 2px 여백
    height = y1 - y0 + 1

    frames = []
    for i in range(COL_COUNT):
        canvas = np.zeros((height, width, 4), dtype=np.uint8)
        if spans[i] is not None:
            lo, hi, _, _ = spans[i]
            c = COL0 + COL_STEP * i
            # 열 중심 → 캔버스 가로 중앙, 행 바닥 → 캔버스 아래 (피벗이 발밑이라 바닥을 맞춘다)
            shift = int(round(width / 2.0 - c))
            a = alphas[i]
            ys, lx = np.where(a > 0)          # lx = 프레임 안에서의 x
            gx = lx + lo                      # gx = 시트 좌표계의 x
            tx = gx + shift                   # tx = 캔버스 좌표계의 x
            valid = (tx >= 0) & (tx < width)
            ys, lx, gx, tx = ys[valid], lx[valid], gx[valid], tx[valid]

            canvas[ys, tx, 0:3] = band[ys, gx]
            canvas[ys, tx, 3] = a[ys, lx]
        frames.append(Image.fromarray(canvas, "RGBA"))
    return frames, [s[2:] if s else None for s in spans]


def write_frames(motion, side, frames):
    folder = os.path.join(DST, motion)
    os.makedirs(folder, exist_ok=True)
    meta = folder + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid_for(f"{DST_REL}/{motion}")))

    for i, img in enumerate(frames):
        stem = f"Char_{motion}_{side}_{i:02d}" if side else f"Char_{motion}_{i:02d}"
        path = os.path.join(folder, stem + ".png")
        img.save(path)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(png_meta(guid_for(f"{DST_REL}/{motion}/{stem}.png"), PPU, pivot_bottom=True))


def main():
    if not os.path.exists(SRC):
        raise SystemExit(f"원본 시트를 찾지 못했습니다: {SRC}")

    sheet = Image.open(SRC).convert("RGB")
    rgb = np.asarray(sheet).astype(np.uint8)
    print(f"시트 {sheet.size}  →  {DST_REL}")

    os.makedirs(DST, exist_ok=True)
    root_meta = os.path.dirname(DST) + ".meta"
    if not os.path.exists(root_meta):
        with open(root_meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(
                guid=guid_for("Assets/_Project/Art/Char_Asset/Char_Asset_Histon")))
    char_meta = DST + ".meta"
    if not os.path.exists(char_meta):
        with open(char_meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid_for(DST_REL)))

    for name, y0, y1, mirror in ROWS:
        frames, cuts = build_row(rgb, y0, y1, name)
        w, h = frames[0].size
        write_frames(name, "Right" if mirror else "", frames)
        if mirror:
            write_frames(name, "Left", [f.transpose(Image.FLIP_LEFT_RIGHT) for f in frames])
        print(f"  {name:12} {len(frames)}프레임 · {w}x{h}  범위 {cuts}"
              + ("  (+ 좌우 반전)" if mirror else "  (방향 없음)"))

    print(f"\nPPU {PPU} · 피벗 (0.5, 0) 발밑. Unity 에서 Assets/Refresh 를 실행할 것.")


if __name__ == "__main__":
    main()
