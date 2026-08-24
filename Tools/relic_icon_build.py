# -*- coding: utf-8 -*-
"""유물 아이콘 원화(3장) → `Resources/RelicIcons` 31장 (2026-08-24).

유저 지시: *"유물 아이콘들 뽑아 놨으니까 연동"*.

무엇을 하나
-----------
볼트의 ``리소스/sprites/Lelic_icon_01~03.png`` 은 <b>한 장에 6x6 = 36칸</b>씩 들어 있는
아이콘 시트다(세 장 합쳐 108칸). 그중 <b>31칸</b>을 골라 유물 표(`Relic` 시트)의
``icon`` 이름 그대로 ``Assets/_Project/Resources/RelicIcons/*.png`` 을 <b>덮어쓴다</b>.

★★ <b>.meta 를 건드리지 않는다</b> — 유물 에셋(`Resources/Relics/Relic_*.asset`)이
  아이콘을 <b>guid + fileID 21300000</b> 으로 직접 참조한다(`gen_relic_assets.py` 가
  그렇게 썼다). PNG <b>픽셀만</b> 갈아끼우면 참조가 그대로 산다 —
  .meta 를 다시 쓰면 guid 가 바뀌어 <b>31개 참조가 전부 끊긴다</b>.
  (UI-50 절이 *"원화가 오면 같은 이름으로 덮으면 된다"* 라고 적어둔 그 자리다.)

칸을 어떻게 자르나
------------------
시트마다 <b>격자 간격이 다르다</b>(01/02 는 ~203px, 03 은 ~205px 이고 시작 여백도
다르다). 그래서 픽셀을 세지 않고 <b>새까만 고랑(gutter)</b>을 찾아 그 가운데를 자른다 —
행/열 평균 밝기가 2.0 미만인 구간이 칸과 칸 사이다. 아래 ``CUTS`` 가 그 실측값이다
(``relic_icon_build.py --probe`` 로 다시 잴 수 있다).

⚠ 칸의 <b>테두리 액자까지 함께</b> 굽는다. 원화가 액자를 포함해 그려져 있어서
  떼어내면 아이콘마다 여백이 제각각이 된다 — 액자가 있는 편이 목록에서 줄이 고르다.

배정 근거
---------
이름과 서사(표의 ``relic_flavor``)에 맞춰 사람이 골랐다. 아래 ``PICK`` 의 주석이 근거다.
바꾸고 싶으면 그 표의 (시트, 행, 열) 만 고치고 다시 돌리면 된다.

사용법:
    python Tools/relic_icon_build.py            # 31장 굽기
    python Tools/relic_icon_build.py --probe    # 고랑 위치 다시 재기
    python Tools/relic_icon_build.py --contact  # 배정 확인용 대조표 PNG
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import TABLE_DIR

VAULT = os.path.dirname(TABLE_DIR)
SHEET_DIR = os.path.join(VAULT, "리소스", "sprites")
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(PROJECT, "Assets", "_Project", "Resources", "RelicIcons")

ICON_SIZE = 128

#: 시트별 자를 자리 — (행 경계 7개, 열 경계 7개). `--probe` 로 잰 «새까만 고랑» 의 가운데.
CUTS = {
    1: ([16, 215, 416, 618, 821, 1021, 1235],
        [8, 214, 418, 623, 830, 1036, 1245]),
    2: ([10, 216, 417, 618, 819, 1019, 1232],
        [8, 214, 418, 623, 830, 1036, 1244]),
    3: ([5, 209, 412, 615, 814, 1013, 1231],
        [3, 203, 411, 621, 832, 1039, 1247]),
}

#: 유물 아이콘 이름 → (시트, 행, 열).  행·열은 <b>1부터</b>.
PICK = [
    # ── 일반 11종 — 비특이 면역. 소박한 그림을 골랐다 ──────────────────
    ("relic_scab",       3, 3, 6),  # 굳은 딱지    ← 피 밴 천 조각
    ("relic_fever",      2, 1, 3),  # 오른 열      ← 촛불 (몸이 스스로 올린 체온)
    ("relic_swelling",   1, 4, 6),  # 부어오른 자리 ← 붉은 살덩이가 든 통
    ("relic_capillary",  1, 1, 1),  # 붉은 실      ← 붉은 물약 (피가 닿는 곳)
    ("relic_keratin",    2, 1, 6),  # 마른 각질    ← 바스러진 양피지
    ("relic_phagocyte",  3, 4, 5),  # 삼킨 티끌    ← 가마솥 (삼켜 곱씹는다)
    ("relic_cilia",      1, 1, 4),  # 곤두선 솜털  ← 흰 깃털
    ("relic_nerve",      1, 3, 3),  # 저린 손끝    ← 날개 달린 장화 (이동속도)
    ("relic_lymph",      1, 1, 2),  # 맑은 진물    ← 맑은 푸른 물약
    ("relic_mucosa",     3, 4, 3),  # 첫 재채기    ← 나침반 (시야가 넓어진다)
    ("relic_nail",       2, 4, 1),  # 들뜬 손톱    ← 뼈 발톱 (긁어내는 끝)

    # ── 레어 8종 — 특이 면역. 형태가 분명한 물건 ────────────────────────
    ("relic_antibody",   2, 6, 4),  # 항체의 낙인   ← 피 손자국이 찍힌 석판 («낙인»)
    ("relic_complement", 3, 4, 4),  # 보체의 사슬   ← 피 묻은 사슬
    ("relic_interferon", 3, 1, 5),  # 인터페론 결정 ← 푸른 결정 덩어리
    ("relic_mastcell",   2, 5, 6),  # 비만세포 주머니← 가시 철퇴 (건드리면 터진다 = 반사)
    ("relic_macrophage", 2, 3, 4),  # 굶주린 대식세포← 피가 담긴 원통 (흡혈)
    ("relic_memorycell", 3, 5, 6),  # 기억 세포     ← 봉인된 책 (기억)
    ("relic_eschar",     1, 6, 4),  # 두꺼워진 가피 ← 은 방패 (몰릴수록 단단해진다)
    ("relic_antipyretic",3, 1, 3),  # 서늘한 해열   ← 맑은 액체가 담긴 성배

    # ── 에픽 12종 — 침입자에게서 빼앗은 것 ──────────────────────────────
    ("relic_boss_120001", 2, 4, 5),  # 형상을 잊은 핵   ← 뛰는 심장 (단탈리온)
    ("relic_boss_120002", 2, 1, 4),  # 구속의 인장      ← 피 묻은 족쇄 (말파스 = 구속)
    ("relic_boss_120003", 3, 2, 6),  # 유혹하는 피주머니← 심장 모양 피 병 (카시노마)
    ("relic_boss_120004", 2, 2, 3),  # 비명을 삼킨 성대 ← 갈라진 가면 (라린길 = 후두)
    ("relic_boss_120005", 2, 5, 2),  # 잿빛 담뱃대      ← 연기 오르는 향로 (베일)
    ("relic_boss_120006", 2, 6, 6),  # 증식하는 촉수    ← 눈이 돋은 포자 덩이 (레기미아)
    ("relic_boss_1101",   1, 5, 5),  # 검은 숲의 홀씨   ← 초록 포자 후광
    ("relic_boss_1102",   2, 2, 6),  # 삼킨 것의 이빨   ← 피 묻은 이빨 줄
    ("relic_boss_1103",   3, 4, 2),  # 얼금뱅이 뿔      ← 검은 뿔
    ("relic_boss_1104",   1, 3, 5),  # 영원한 숙적의 눈 ← 감기지 않은 보랏빛 눈
    ("relic_marrow",      1, 3, 6),  # 태초의 골수      ← 피가 밴 뼈
    ("relic_thymus",      3, 2, 1),  # 흉선의 씨앗      ← 실핏줄이 뻗은 알
    # ── 표 Ver02 신설 14종 (2026-08-24) ────────────────────────────────
    ("relic_bruise",      3, 6, 3),  # 배인 멍          <- 붉게 뭉친 덩어리
    ("relic_saliva",      3, 5, 5),  # 굳은 침          <- 녹색 산성 약병
    ("relic_afterfever",  2, 6, 1),  # 미열의 잔향       <- 아직 타는 보랏빛 불씨
    ("relic_abscess",     1, 2, 6),  # 곪은 자리        <- 부풀어 터지기 직전의 구체
    ("relic_shallowsleep",1, 4, 1),  # 얕은 잠          <- 흰 백합
    ("relic_helix",       2, 5, 5),  # 이중 나선의 자국   <- 두 갈래로 꼬인 사슬
    ("relic_lymphnode",   3, 3, 4),  # 부푼 림프절       <- 부어오른 살덩이가 든 통
    ("relic_dendritic",   2, 4, 3),  # 각성한 수지상세포  <- 가지처럼 뻗은 손
    ("relic_clot",        2, 4, 4),  # 굳은 혈병        <- 막아 세운 돌 뚜껑
    ("relic_breath",      1, 6, 5),  # 삼킨 숨          <- 초록 정화의 빛
    ("relic_thorn_heart", 3, 1, 4),  # 심장에 박힌 가시   <- 붉은 가시 고리
    ("relic_spring",      1, 2, 5),  # 마르지 않는 샘     <- 늘 차 있는 잔
    ("relic_lastgasp",    1, 6, 1),  # 최후의 발버둥      <- 죽음의 낫
    ("relic_rampart",     3, 6, 4),  # 세 겹의 방벽      <- 겹겹의 스테인드글라스
    # ── 사건 전용 에픽 3종 (2026-08-24) ─────────────────────────────────
    #   ★ 셋 다 «어디서 왔는지 알 수 없는 것» 이라, 장기·체액 그림이 아니라
    #     <b>«주어진 물건»</b> 쪽에서 골랐다 — 그것이 이 셋의 성격이다.
    ("relic_grace",       1, 6, 3),  # 값이 붙지 않은 은혜 <- 내리쬐는 금빛 광휘
    ("relic_fleshmend",   1, 3, 1),  # 스스로 메운 살     <- 날개 돋은 붉은 심장
    ("relic_homing",      2, 5, 4),  # 돌아갈 곳의 기억   <- 유리 종 안에 남은 상
]


def sheet_path(n):
    return os.path.join(SHEET_DIR, "Lelic_icon_%02d.png" % n)


def probe():
    """시트마다 «새까만 고랑» 을 찾아 `CUTS` 에 넣을 값을 뽑는다."""
    for n in (1, 2, 3):
        im = np.array(Image.open(sheet_path(n)).convert("RGB")).astype(int)
        for axis, tag in ((1, "rows"), (0, "cols")):
            prof = im.mean(axis=(axis, 2))
            bands, run = [], None
            for i, v in enumerate(prof):
                if v < 2.0:
                    if run is None:
                        run = i
                else:
                    if run is not None:
                        bands.append(((run + i - 1) // 2, i - run))
                        run = None
            if run is not None:
                bands.append(((run + len(prof) - 1) // 2, len(prof) - run))
            # 폭 5px 이상인 것만 = 진짜 고랑 (그림 속 검은 부분은 얇게 걸린다)
            print("시트%d %s: %s" % (n, tag, [c for c, w in bands if w >= 5]))


def cell(sheet_cache, s, row, col):
    """(시트, 행, 열) 칸을 잘라 `ICON_SIZE` 정사각으로 돌려준다."""
    if s not in sheet_cache:
        sheet_cache[s] = Image.open(sheet_path(s)).convert("RGB")
    im = sheet_cache[s]
    ry, cx = CUTS[s]
    box = (cx[col - 1], ry[row - 1], cx[col], ry[row])
    crop = im.crop(box)

    # 칸이 정사각이 아니다(가로 199~211 · 세로 199~205). 긴 쪽에 맞춰 검은 여백을
    # 채워 <b>비율을 지킨 채</b> 정사각으로 만든다 — 늘리면 액자가 찌그러진다.
    side = max(crop.size)
    pad = Image.new("RGB", (side, side), (0, 0, 0))
    pad.paste(crop, ((side - crop.width) // 2, (side - crop.height) // 2))
    return pad.resize((ICON_SIZE, ICON_SIZE), Image.LANCZOS).convert("RGBA")


def contact():
    """배정 확인용 대조표 — 고른 31칸을 한 장에 늘어놓는다."""
    cache = {}
    cols = 8
    rows = (len(PICK) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * ICON_SIZE, rows * ICON_SIZE), (24, 24, 28))
    for i, (name, s, r, c) in enumerate(PICK):
        sheet.paste(cell(cache, s, r, c).convert("RGB"),
                    ((i % cols) * ICON_SIZE, (i // cols) * ICON_SIZE))
    out = os.path.join(os.environ.get("TEMP", "."), "relic_icon_contact.png")
    sheet.save(out)
    print("대조표:", out)
    for i, (name, s, r, c) in enumerate(PICK):
        print("%2d %-20s 시트%d r%dc%d" % (i, name, s, r, c))


def main():
    if not os.path.isdir(OUT_DIR):
        print("⚠ 폴더 없음:", OUT_DIR)
        return 1

    cache = {}
    seen = set()
    written = 0
    for name, s, r, c in PICK:
        key = (s, r, c)
        if key in seen:
            print("⚠ 같은 칸을 두 번 썼다:", name, key)
        seen.add(key)

        png = os.path.join(OUT_DIR, name + ".png")
        if not os.path.isfile(png):
            print("⚠ 대상 없음(표의 icon 이름과 다르다):", png)
            continue
        if not os.path.isfile(png + ".meta"):
            print("⚠ .meta 없음 — 참조가 끊긴다:", png)
            continue

        cell(cache, s, r, c).save(png)     # ★ .meta 는 손대지 않는다
        written += 1
        print("  %-20s ← 시트%d r%dc%d" % (name, s, r, c))

    print("\n%d/%d 장 교체 (.meta 무변경 → guid·스프라이트 참조 유지)"
          % (written, len(PICK)))
    print("다음: 유니티에서 Assets/Refresh")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if "--probe" in sys.argv:
        probe()
    elif "--contact" in sys.argv:
        contact()
    else:
        sys.exit(main())
