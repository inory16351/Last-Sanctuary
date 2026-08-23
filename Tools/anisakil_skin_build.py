# -*- coding: utf-8 -*-
"""아니사킬(에픽 중립 보스 1005) 모션 시트 → 프레임 분해 (2026-08-19).

원본: ``<볼트>/리소스/asset/anisakil_asset.png`` (1664x2080, RGBA 이지만 알파는 전부 255).

★ 이 시트는 <b>안내판(guide sheet)</b>이다
------------------------------------------
제목("ANISAKIL — LEFT / RIGHT MOTION GUIDE")·둥근 패널·좌/우 라벨 상자·프레임 번호가 같이
그려져 있다. 그래서 <b>패널 안의 프레임만</b> 골라내는 단계가 필요하다.

★★ <b>좌/우 라벨이 실제 방향과 뒤집혀 있다</b>
---------------------------------------------
입(둥근 톱니 아귀)의 x 위치를 프레임마다 재서 확인했다:

    시트 라벨 LEFT  줄 → 입이 <b>오른쪽</b>(0.76~0.87) → 실제로는 <b>오른쪽</b>을 본다
    시트 라벨 RIGHT 줄 → 입이 <b>왼쪽</b>(0.11~0.31)   → 실제로는 <b>왼쪽</b>을 본다

눈으로도 확인했다(대기 1·2번 프레임을 위아래로 붙여 봤다). 그래서 <b>라벨을 무시하고
실제 방향대로</b> 넣는다 — 첫 줄이 ``Right``, 둘째 줄이 ``Left`` 다.

⚠ 같은 날 받은 고르도네 시트도 <b>같은 방향으로 뒤집혀</b> 있었다. 앞으로 오는 시트마다
  라벨을 믿지 말고 <b>입·투사체 방향으로 확인할 것</b>.

⚠ 두 줄은 <b>단순 좌우 반전이 아니다</b>(대기 첫 줄은 몸을 말고 있고 둘째 줄은 펴고 있다).
  그래서 한 줄만 뽑아 뒤집지 않고 <b>두 줄을 그대로 쓴다</b> — 원화를 버리지 않는다.

★ 프레임 가르기 — <b>행마다 찾지 않고 격자를 한 번만 만든다</b>
--------------------------------------------------------------
행마다 빈 열을 찾는 방식(카르시노스)은 여기서 안 통했다. 이 개체는 몸통에서 떨어진
<b>보랏빙 발광 점</b>이 많아 한 행이 12~41개 조각으로 부서진다(사망 행은 41개다).

대신 <b>12개 몸통 행을 세로로 겹쳐</b> 열 프로파일을 만들면 프레임 사이 틈이 뚜렷하게 남는다
(6개 프레임이 전부 같은 격자에 그려져 있기 때문이다). 그 틈의 가운데를 잘라 격자를 만들고,
모든 행이 <b>같은 격자</b>를 쓴다. 프레임 하나가 옆칸으로 몇 px 삐져나오는 경우가 있어도
꼬리 끝이 조금 잘릴 뿐 몸통은 안 잘린다.

★ 알파 — <b>어두운 개체 · 어두운 패널</b>이라 밝기로는 못 가른다
---------------------------------------------------------------
패널 배경이 (20,24,27) 이고 개체의 어두운 부분도 그 대역이다. 넥서스 시트와 같은 문제이고
같은 해법을 쓴다(102-7절): <b>프레임을 잘라낸 뒤 그 조각의 테두리에서 흘려 채워</b>
(``scipy.ndimage.label``) <b>테두리와 이어진</b> 배경색 덩어리만 투명하게 만든다.
개체 안쪽의 어두운 픽셀은 테두리와 이어지지 않으므로 <b>지워지지 않는다</b>.

⚠ 배경 판정은 <b>채도까지</b> 본다 — 개체는 보라/주황이라 채도가 있고 패널은 회색이다.
  밝기만 보면 개체의 검은 비늘이 배경으로 잡힌다.

넣는 모션: 대기 · 이동 · 근접 공격 · 스킬1 · 스킬2 + 스킬 이펙트 2벌.
⚠ <b>사망 행은 안 넣는다</b> — `CharacterSkinSO` 에 사망 모션 칸이 없다(미결 266번).
  원화에는 좌/우 6장씩 그려져 있으니 칸이 생기면 `WANTED` 에 두 줄만 켜면 된다.

사용법:  python Tools/anisakil_skin_build.py
다음:    python Tools/gen_anisakil_skin.py  →  python Tools/measure_skin_tiles.py
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT, find_art
# ★ 자르기·저장·메타는 카르시노스 빌더의 것을 <b>그대로 쓴다</b> — 같은 규약(피벗 하단
#   중앙 · PPU 64 · 결정적 guid)이라 복제하면 두 벌이 갈라진다.
from carcinos_skin_build import (PPU, bands, merge_to_count, split_to_count,
                                 write_png, ensure_folder_meta)

SRC = find_art("anisakil_asset.png")
DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Anisakil", "Char")

#: 프레임을 <b>가르는</b> 기준 — 채도. 패널은 회색(채도 7 남짓), 개체는 보라/주황이다.
SEG_SAT = 28

# ⚠⚠ <b>배경 판정을 「채도·밝기」로 하면 개체가 통째로 지워진다</b> (2026-08-19 실사고).
#   처음엔 `채도<=14 & 밝기<=46` 을 배경 후보로 썼는데, 이 개체의 <b>검은 비늘과 그림자</b>가
#   정확히 그 대역이라 테두리부터 흘려 채운 물이 <b>몸통을 다 먹었다</b> — 초록 배경에 얹어
#   보니 보라 발광 점과 테두리만 남아 있었다.
#
# ★ 그래서 <b>패널 배경색과의 거리</b>로 판정한다(넥서스 시트와 같은 방법 · 102-7절).
#   실측(대기 1번 프레임): 패널은 (21,25,28) 에 몰려 있고, <b>개체의 채도 있는 픽셀은
#   거리 14 이상</b>, 개체의 순검정 부분은 거리 27 이다. 9 면 둘 다 안 건드린다.

#: 패널 배경색 — 시트에서 <b>가장 많이 나온 색</b>을 쓴다(손으로 안 적는다).
#: 이 색과의 채널 최대차가 BG_TOL 이내인 픽셀만 배경 후보다.
BG_TOL = 9

#: 불투명으로 남은 덩어리 중 이보다 작은 것은 버린다(잡티).
MIN_COMPONENT_PX = 8

#: ★ 몸통 프레임에서 <b>가장 큰 덩어리의 이 비율보다 작은</b> 덩어리는 버린다.
#: 격자 경계를 넘어온 <b>옆 프레임의 꼬리 끝</b>이 조각으로 남는데, 재생하면 그것만
#: 프레임마다 나타났다 사라져 <b>깜빡이는 점</b>으로 보인다. 이펙트 행에는 안 쓴다
#: (반짝임이 원래 여러 조각이다).
MIN_COMPONENT_RATIO = 0.06

# ── ⚠⚠ 안내판 <b>제목 바</b>를 지우는 규칙 (2026-08-19 실사고) ──────────────
#
# 패널마다 위쪽에 「5) SKILL 2 — GREAT THREAT ROAR / 스킬 2 거대한 위협 표효」 같은
# <b>검은 둥근 막대 + 흰 글자</b>가 있고, 그 막대가 프레임 상자 안까지 내려온다.
# 실제로 「표효」 두 글자가 스킬2 3번 프레임에 찍혀 나왔다.
#
# ⚠ <b>색으로는 못 가른다</b> — 막대 안쪽도 개체의 어두운 부분도 <b>둘 다 (0,0,0)</b> 이다
#   (실측). 채도로도 못 가른다(막대는 회색, 글자는 흰색 = 채도 0 이지만 개체와 붙어 있어서
#   같은 덩어리가 된다).
#
# ★ 가르는 것은 <b>「막대 재질」이 가로로 이어진 길이</b>다.
#
#   막대는 <b>순검정 + 순백</b> 두 가지 색으로만 되어 있고(테두리·글자), 개체는 그 사이의
#   <b>중간톤 보라</b>가 반드시 섞인다. 그래서 「검정 또는 흰색」을 한 재질로 묶어 세면
#   막대만 길게 이어진다. 실측(행별 최장 런):
#
#       막대가 있는 행      216 ~ 737 px
#       개체만 있는 행      최대 164 px
#
#   그래서 문턱을 <b>180</b> 으로 둔다. ⚠ 순검정만 세면(처음 시도) 개체도 최대 75 라
#   100 으로 갈렸지만, <b>막대 안쪽의 글자 줄</b>이 44~53 밖에 안 나와 <b>글자가 남았다</b>.
CHROME_DARK_LUM = 14
CHROME_BRIGHT_LUM = 200
CHROME_MIN_RUN = 180

#: ★ 막대의 <b>글자 줄</b>을 메우는 세로 높이(px). 실측: 막대 위 테두리(y1174~1185)와
#: 아래 테두리(y1207~1214) 사이에 글자 줄 21행이 비어 있다 — 그보다 넉넉해야 한다.
#: ⚠ <b>세로 닫기(closing)</b>다: 위·아래가 모두 막힌 구멍만 메우므로 개체 쪽으로 번지지 않는다.
CHROME_CLOSE_V = 35

CHROME_GROW = 3

#: 잘라낸 조각의 사방에 이만큼 여백을 두고 흘려 채운다 — 개체가 조각 테두리에 닿아 있으면
#: 흘릴 시작점이 없어져 배경이 하나도 안 지워진다.
PAD = 3

#: 가장자리 한 겹을 반투명으로 — 계단을 눕힌다.
EDGE_SOFT = 1

# ── 시트 배치 ─────────────────────────────────────────────────────────────
#
# 14개 가로 밴드가 <b>순서대로</b> 아래와 같다(채도 프로파일로 찾는다 — y 를 손으로 적지
# 않는다). ★ 이름의 Right/Left 는 <b>실제 방향</b>이다 — 시트 라벨과 반대다(맨 위 ★★).
ROW_ORDER = [
    ("Idle",        "Right"),   # 시트 라벨 LEFT
    ("Idle",        "Left"),    # 시트 라벨 RIGHT
    ("Walk",        "Right"),
    ("Walk",        "Left"),
    ("MeleeAttack", "Right"),
    ("MeleeAttack", "Left"),
    ("Skill1",      "Right"),
    ("Skill1",      "Left"),
    ("Skill2",      "Right"),
    ("Skill2",      "Left"),
    ("Death",       "Right"),
    ("Death",       "Left"),
    ("Fx1",         None),      # 스킬1 히트 이펙트 (방향 없음)
    ("Fx2",         None),      # 스킬2 포효 이펙트 (방향 없음)
]

#: 실제로 뽑을 모션. 사망은 담을 칸이 없어 뺀다(맨 위 ⚠).
WANTED = {"Idle", "Walk", "MeleeAttack", "Skill1", "Skill2", "Fx1", "Fx2"}

#: 모든 행이 6프레임이다 — 시트가 한 줄에 1~6 을 적어 놨다.
FRAMES_PER_ROW = 6

#: 몸통 행은 이 중 앞 12개다(나머지 2개가 이펙트 행).
BODY_ROWS = 12


def panel_bg(arr):
    """시트에서 가장 많이 나온 색 = 패널 배경. 8픽셀마다 표본을 뽑아 센다."""
    from collections import Counter
    sample = arr[::8, ::8].reshape(-1, 3)
    common = Counter(map(tuple, sample.tolist())).most_common(1)[0][0]
    return np.array(common, dtype=np.int16)


def chrome_mask(arr):
    """
    안내판 <b>제목 바</b> 픽셀 (위 ⚠⚠ 참조). 가로로 <see cref="CHROME_MIN_RUN"/> 이상
    이어지는 순검정 런을 찾아 그 런 전체를 표시하고, 둥근 끝과 안티에일리어싱을 위해 조금 부풀린다.
    """
    from scipy import ndimage

    lum = arr.mean(axis=2)
    barmat = (lum < CHROME_DARK_LUM) | (lum > CHROME_BRIGHT_LUM)
    out = np.zeros_like(barmat)

    for y in range(barmat.shape[0]):
        row = barmat[y]
        if not row.any():
            continue
        # 런 길이 = 누적합 트릭 없이 단순 스캔(행 수가 2천 줄뿐이라 충분히 빠르다)
        start = None
        for x in range(len(row) + 1):
            on = x < len(row) and row[x]
            if on and start is None:
                start = x
            elif not on and start is not None:
                if x - start >= CHROME_MIN_RUN:
                    out[y, start:x] = True
                start = None

    # ★ <b>글자 줄을 메운다.</b> 막대의 위·아래 테두리 줄은 문턱을 넘지만 <b>글자가 있는
    #   가운데 줄</b>은 안티에일리어싱 때문에 중간톤이 섞여 런이 44~53 로 짧아진다.
    #   세로로 닫으면 그 사이가 <b>막대 안쪽</b>으로 채워진다.
    if CHROME_CLOSE_V > 1:
        out = ndimage.binary_closing(out, structure=np.ones((CHROME_CLOSE_V, 1), bool))

    if CHROME_GROW > 0:
        out = ndimage.binary_dilation(out, iterations=CHROME_GROW)
    return out


def masks(arr):
    """(가르기용 마스크 = 채도, 배경 후보 마스크 = 패널색과의 거리, 채도 마스크)."""
    sat = arr.max(axis=2) - arr.min(axis=2)
    bg = panel_bg(arr)
    near = np.abs(arr - bg).max(axis=2) <= BG_TOL
    return sat > SEG_SAT, near, sat


def detect_rows(seg):
    """내용이 있는 가로 밴드 14개. 위에서 아래 순서."""
    found = bands(seg.sum(axis=1) > 3, min_len=25)
    if len(found) != len(ROW_ORDER):
        raise SystemExit("⚠ 가로 밴드가 %d개가 아니라 %d개입니다: %s"
                         % (len(ROW_ORDER), len(found), found))
    return found


def body_grid(seg, rows):
    """
    몸통 12행을 겹쳐 만든 <b>공통 열 격자</b>. 돌려주는 것은 자를 경계 7개다.

    프레임 사이 틈의 <b>가운데</b>를 자른다 — 틈 끝을 자르면 옆 프레임의 잔광이 섞인다.
    """
    acc = np.zeros(seg.shape[1], dtype=int)
    for y0, y1 in rows[:BODY_ROWS]:
        acc += (seg[y0:y1 + 1].sum(axis=0) > 0).astype(int)

    # ★ <b>몇 행에 걸쳐 나타나는 열만</b> 센다. 한두 행에만 있는 잔광(꼬리·궤적)은
    #   프레임 경계를 흐리므로 문턱을 준다. 실측: 3 이면 6덩어리 + 잔조각 1개다.
    chunks = bands(acc > 3, min_len=10)
    if len(chunks) < FRAMES_PER_ROW:
        raise SystemExit("⚠ 열 격자를 만들 수 없습니다(%d덩어리): %s" % (len(chunks), chunks))
    if len(chunks) > FRAMES_PER_ROW:
        chunks = merge_to_count(chunks, FRAMES_PER_ROW)

    # ⚠ <b>바깥 두 경계는 격자 덩어리로 정하지 않는다.</b> 처음엔 첫 덩어리에서 8px 물러난
    #   값을 썼는데(211), 실제로 어떤 행의 1번 프레임은 x 183 에서 시작해서 <b>28px 이 잘렸다</b>
    #   (이동 행의 꼬리). 그래서 <b>몸통 12행 전체의 실제 최소·최대</b>를 바깥 경계로 쓴다 —
    #   안쪽 5개 경계만 격자에서 가져온다.
    any_col = np.zeros(seg.shape[1], dtype=bool)
    for y0, y1 in rows[:BODY_ROWS]:
        any_col |= seg[y0:y1 + 1].any(axis=0)
    hit = np.where(any_col)[0]

    cuts = [max(0, int(hit.min()) - 2)]
    for i in range(len(chunks) - 1):
        cuts.append((chunks[i][1] + chunks[i + 1][0]) // 2)
    cuts.append(min(seg.shape[1] - 1, int(hit.max()) + 2))
    return cuts


#: ★ 한 줄이 «개체» 로 인정되려면 칸 폭의 이 비율만큼 불투명해야 한다
#: (:func:`grow_to_body`). 실측: 개체의 가장 얇은 끝단이 8.4%, 프레임 번호 글자가 1.8~5.3%.
BODY_ROW_FILL = 0.06

#: 세로로 넓힐 때 밴드 밖으로 나갈 수 있는 최대 폭(px) — 밴드 사이 절반과 함께 상한이 된다.
BODY_GROW_MAX = 30


def band_limits(rows, height):
    """밴드마다 «위·아래로 여기까지만» 이라는 한계. <b>이웃 밴드와의 중간</b>이다.

    ⚠ 구획 사이에는 <b>가로 구분선</b>이 있고 그것은 칸 폭 전체가 불투명하다(실측 225/225).
      한계를 두지 않으면 :func:`grow_to_body` 가 그 선까지 먹는다.
    """
    out = {}
    for k, (y0, y1) in enumerate(rows):
        top = (rows[k - 1][1] + y0) // 2 if k > 0 else 0
        bot = (y1 + rows[k + 1][0]) // 2 if k < len(rows) - 1 else height - 1
        out[(y0, y1)] = (max(top, y0 - BODY_GROW_MAX), min(bot, y1 + BODY_GROW_MAX))
    return out


def grow_to_body(opaque, box, cx0, cx1, limit):
    """
    ★★ <b>상자를 «채도» 가 아니라 «개체» 에 맞춘다</b> (2026-08-22 신설).

    <b>왜 필요했나</b> (유저 리포트: *"뱀 모양 보스 몬스터 위아래로 짤림"*) — 이 스크립트는
    프레임 상자를 <b>채도 마스크</b>(:data:`SEG_SAT`)로 잡는다. 그런데 이 개체는
    <b>등만 보라색으로 빛나고 배·꼬리 밑동은 거의 검다</b>. 검은 부분은 채도 문턱을 못 넘어
    상자에서 빠지고, 그래서 <b>배가 평평하게 잘린 벌레</b>가 구워졌다.

    실측(이동 오른쪽 1번 칸 · 폭 225px):

        y422 ~ y510   개체(불투명 19~179 px)      ← 진짜 몸
        y423 ~ y495   채도 마스크가 잡은 범위     ← <b>아래 15px 손실</b>
        y511 ~ y529   프레임 번호 글자(4~9 px)    ← 들어오면 안 된다
        y533          가로 구분선(225 px = 전부)  ← 절대 안 된다

    ★ 그래서 «패널색이 아닌 픽셀»(불투명) 로 <b>밴드에서 위·아래로 이어서 넓힌다</b>.
      규칙 하나다: <b>한 줄이 칸 폭의 6% 이상 불투명하면 몸이다.</b>
      번호 글자는 얇아서(1.8~5.3%) 문턱을 못 넘고, <b>이어짐이 끊기므로</b> 더 멀리 있는
      것은 애초에 닿지 않는다. 구분선은 :func:`band_limits` 가 막는다.
    """
    bx0, bx1, by0, by1 = box
    lo, hi = limit
    w = cx1 - cx0 + 1
    thr = max(3, int(w * BODY_ROW_FILL))

    top, bot = by0, by1
    while top - 1 >= lo and int(opaque[top - 1, cx0:cx1 + 1].sum()) >= thr:
        top -= 1
    while bot + 1 <= hi and int(opaque[bot + 1, cx0:cx1 + 1].sum()) >= thr:
        bot += 1

    # 가로도 같은 규칙으로 다시 잡는다 — 넓힌 줄에 몸이 더 있으면 x 도 따라 늘어난다.
    h = bot - top + 1
    cthr = max(2, int(h * BODY_ROW_FILL))
    cols = opaque[top:bot + 1, cx0:cx1 + 1].sum(axis=0) >= cthr
    if cols.any():
        xs = np.where(cols)[0]
        bx0 = min(bx0, cx0 + int(xs.min()))
        bx1 = max(bx1, cx0 + int(xs.max()))
    return (bx0, bx1, top, bot)


def fx_frames(seg, y0, y1):
    """이펙트 행은 격자를 안 쓴다 — 폭이 프레임마다 크게 달라서 스스로 찾는다."""
    col = seg[y0:y1 + 1].sum(axis=0) > 0
    raw = bands(col, min_len=3)
    if len(raw) > FRAMES_PER_ROW:
        raw = merge_to_count(raw, FRAMES_PER_ROW)
    elif len(raw) < FRAMES_PER_ROW:
        raw = split_to_count(raw, FRAMES_PER_ROW)
    return raw


def to_rgba(rgb, bgcand, satcrop, chromecrop, drop_small):
    """
    <b>테두리에서 이어진 배경만</b> 투명하게 한다 (맨 위 ★ 알파 참조).

    바깥에 <see cref="PAD"/> 만큼 여백을 붙여서 흘린다 — 개체가 조각 테두리에 닿아 있으면
    흘릴 시작점이 없어 배경이 하나도 안 지워진다.
    """
    from scipy import ndimage

    h, w = bgcand.shape
    padded = np.zeros((h + PAD * 2, w + PAD * 2), dtype=bool)
    padded[PAD:PAD + h, PAD:PAD + w] = bgcand
    padded[:PAD, :] = True
    padded[-PAD:, :] = True
    padded[:, :PAD] = True
    padded[:, -PAD:] = True

    # ★ 안내판 제목 바는 <b>배경으로 강제한다</b> — 색으로는 개체와 구별되지 않는다.
    if chromecrop is not None:
        padded[PAD:PAD + h, PAD:PAD + w] |= chromecrop

    lbl, n = ndimage.label(padded)                 # 4-이웃
    if n == 0:
        return np.dstack([rgb, np.full((h, w), 255, np.uint8)])

    outside = np.unique(np.concatenate([lbl[0, :], lbl[-1, :], lbl[:, 0], lbl[:, -1]]))
    outside = outside[outside > 0]
    background = np.isin(lbl, outside)[PAD:PAD + h, PAD:PAD + w]

    opaque = ~background

    # ★ <b>채도 없는 덩어리를 버린다</b> — 패널 제목 글자가 프레임 상자에 걸려 들어온다
    #   (실제로 「표효」 두 글자가 스킬2 프레임에 찍혀 나왔다). 개체는 보라·주황이라 반드시
    #   채도 있는 픽셀을 갖고, 글자·패널 선은 회색이라 하나도 없다.
    lbl2, n2 = ndimage.label(opaque)
    if n2 > 0:
        keep = np.zeros(n2 + 1, dtype=bool)
        inked = satcrop > SEG_SAT
        for idx in np.unique(lbl2[inked]):
            if idx > 0:
                keep[idx] = True
        sizes = np.asarray(ndimage.sum(opaque, lbl2, index=np.arange(1, n2 + 1)))
        floor = MIN_COMPONENT_PX
        if drop_small and sizes.size:
            floor = max(floor, sizes.max() * MIN_COMPONENT_RATIO)
        for i, sz in enumerate(sizes, start=1):
            if sz < floor:
                keep[i] = False
        opaque = keep[lbl2]
        background = ~opaque

    alpha = np.where(background, 0, 255).astype(np.uint8)
    if EDGE_SOFT > 0:
        grown = ndimage.binary_dilation(background, iterations=EDGE_SOFT)
        alpha[np.logical_and(grown, ~background)] = 128
    return np.dstack([rgb, alpha])



# ★★ <b>발을 피벗에 맞춰 얹기</b> (2026-08-22 신설 · 유저 지시 *"캐릭터가 커졌다 작아졌다
#   도 안하게 확실하게 분석해서 피벗 맞추고"*).
#
# 옛 코드는 상자를 캔버스 <b>한가운데</b> 에 얹었다. 상자는 <b>낫·촉수·포효가 뻗은 쪽</b>으로
# 늘어나므로 그 중심이 모션마다 옆으로 밀린다 — 실측으로 모션이 바뀔 때
# 카르시노스 <b>15.5px</b> · 고르도네 <b>28.0px</b> · 아니사킬 <b>5.5px</b> 씩 <b>미끄러졌다</b>.
# → 묶음마다 <b>발 중심의 중앙값</b>을 재서 그것이 캔버스 한가운데 오도록 <b>같은 양</b>을
#   민다. 묶음 안의 움직임은 그대로 남고 묶음끼리만 맞는다(`skin_sheet.plant_feet` 와 같은 규칙).


def foot_layout(rgbas):
    """``(가로 시작 위치 목록, 캔버스 폭)`` — 발 중심을 피벗에 맞춘다.

    ⚠⚠ <b>캔버스 폭을 여기서 정한다</b> — 처음에는 «폭은 그대로 두고 안에서 민다» 로
      했는데, 폭이 <b>가장 넓은 프레임에 딱 맞게</b> 잡혀 있어서 <b>밀 자리가 없었다</b>
      (고르도네 원거리는 탄이 뻗어 프레임이 캔버스만큼 넓다 → 28px 어긋남이 그대로 남았다).
      :func:`skin_sheet.compose` 가 «피벗 좌우로 같은 폭» 을 잡는 것과 <b>같은 계산</b>을 한다.

    ★ 미는 양은 <b>묶음 하나에 한 값</b>이다 — 프레임마다 발에 맞추면 다리 놀림이 지워진다.
    """
    from skin_sheet import foot_center
    got = []
    for r in rgbas:
        c = foot_center(r)
        if c is not None:
            got.append(c - r.shape[1] / 2.0)
    shift = float(np.median(got)) if got else 0.0
    anchors = [r.shape[1] / 2.0 + shift for r in rgbas]
    pad = max(max(anchors), max(r.shape[1] - a for r, a in zip(rgbas, anchors)))
    cw = int(np.ceil(pad * 2))
    oxs = [max(0, min(cw - r.shape[1], int(round(cw / 2.0 - a))))
           for r, a in zip(rgbas, anchors)]
    return oxs, cw


def main():
    if not os.path.isfile(SRC):
        print("⚠ 원본이 없습니다:", SRC)
        return 1

    im = Image.open(SRC).convert("RGB")
    arr = np.asarray(im).astype(np.int16)
    seg, bgcand, satmap = masks(arr)
    chrome = chrome_mask(arr)
    # 제목 바는 프레임 상자를 재는 단계에서도 빼야 한다 — 안 그러면 상자가 위로 늘어난다.
    seg = seg & ~chrome
    rgb8 = np.asarray(im).astype(np.uint8)

    rows = detect_rows(seg)
    cuts = body_grid(seg, rows)
    print("원본 %dx%d · 가로 밴드 %d개" % (im.size[0], im.size[1], len(rows)))
    print("열 격자:", cuts)

    # ★★ <b>«개체» 마스크</b> — 채도가 아니라 «패널색이 아니다» 로 잡는다(아래 ★★).
    opaque = (~bgcand) & (~chrome)
    limits = band_limits(rows, seg.shape[0])

    made = 0
    for (motion, side), (y0, y1) in zip(ROW_ORDER, rows):
        if motion not in WANTED:
            continue

        if motion.startswith("Fx"):
            spans = fx_frames(seg, y0, y1)
        else:
            spans = [(cuts[i], cuts[i + 1]) for i in range(FRAMES_PER_ROW)]

        # ── 이 행의 프레임별 실제 내용 상자 ────────────────────────────
        boxes = []
        for x0, x1 in spans:
            sub = seg[y0:y1 + 1, x0:x1 + 1]
            ys = np.where(sub.any(axis=1))[0]
            xs = np.where(sub.any(axis=0))[0]
            if len(ys) == 0:
                raise SystemExit("⚠ %s %s: x %d~%d 가 비어 있습니다" % (motion, side, x0, x1))
            box = (x0 + xs.min(), x0 + xs.max(), y0 + ys.min(), y0 + ys.max())
            if not motion.startswith("Fx"):
                box = grow_to_body(opaque, box, x0, x1, limits[(y0, y1)])
            boxes.append(box)

        # ── 캔버스: 이 행의 모든 프레임이 안 잘리는 최소 크기 ──────────
        #    ⚠ 프레임마다 캔버스를 따로 잡으면 <b>재생 중에 개체가 튄다</b> —
        #      피벗이 하단 중앙이라 캔버스 크기가 곧 발밑 기준이다.
        cw = max(b[1] - b[0] + 1 for b in boxes)
        ch = max(b[3] - b[2] + 1 for b in boxes)

        folder = os.path.join(DST_ROOT, motion)
        rgbas = [to_rgba(rgb8[b[2]:b[3] + 1, b[0]:b[1] + 1],
                         bgcand[b[2]:b[3] + 1, b[0]:b[1] + 1],
                         satmap[b[2]:b[3] + 1, b[0]:b[1] + 1],
                         chrome[b[2]:b[3] + 1, b[0]:b[1] + 1],
                         drop_small=not motion.startswith("Fx")) for b in boxes]
        # ★ 이펙트는 «밑동» 이 기준이라 그대로 한가운데 두고, 몸통만 발에 맞춘다(위 ★★).
        if motion.startswith("Fx"):
            oxs = [(cw - r.shape[1]) // 2 for r in rgbas]
        else:
            # ★★ <b>옆 칸에서 들어온 «떠 있는 조각» 을 뗀다</b> (2026-08-22 · 유저 리포트:
            #   *"이동 모션 사이 사이에 전 동작 모션과 함께 짤려 들어가서 어색해지는 부분들"*).
            #   이 시트는 격자를 <b>열두 줄을 겹쳐</b> 만들므로 어떤 줄에서는 옆 개체의
            #   <b>머리</b>가 칸 안까지 들어온다(실측: 이동 5·6번). 위쪽의
            #   :data:`MIN_COMPONENT_RATIO`(6%)는 그것보다 커서 못 거른다 —
            #   <b>몸에 붙어 있는가</b>로 가르는 :func:`skin_sheet.drop_stray_parts` 가 잡는다.
            from skin_sheet import drop_stray_parts
            rgbas = [drop_stray_parts(r)[0] for r in rgbas]
            oxs, cw = foot_layout(rgbas)
            # 조각을 뗀 뒤에는 캔버스 높이도 다시 잡아야 한다(떼어낸 만큼 줄 수 있다).
            ch = max(r.shape[0] for r in rgbas)
        for i, rgba in enumerate(rgbas):
            canvas = np.zeros((ch, cw, 4), dtype=np.uint8)
            # ⚠ 조각을 떼면 상자가 줄어드므로 <b>실제 배열 크기</b>로 얹는다(boxes 가 아니다).
            bh, bw = rgba.shape[0], rgba.shape[1]
            canvas[ch - bh:ch, oxs[i]:oxs[i] + bw] = rgba

            name = ("Char_%s_%02d" % (motion, i) if side is None
                    else "Char_%s_%s_%02d" % (motion, side, i))
            write_png(Image.fromarray(canvas, "RGBA"), folder, name)
            made += 1

        ensure_folder_meta(folder)
        print("  %-12s %-5s %d x %d · %d장" % (motion, side or "-", cw, ch, len(boxes)))

    ensure_folder_meta(DST_ROOT)
    print("\n%d장 → Art/Char_Asset/Char_Asset_Anisakil/Char/" % made)
    print("다음: python Tools/gen_anisakil_skin.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
