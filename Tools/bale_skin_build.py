# -*- coding: utf-8 -*-
"""베일(웨이브 최종보스 120005) 모션 시트 → 프레임 분해 (2026-08-20).

유저 지시: *"베일 스킨 모션 동작 값은 너가 스킨 이미지 분석해서 적절한 값 지정해서 잘라서 써
내가 넣은 두가지 이미지 조합해서"*

원본 — **두 장을 실제로 나눠 쓴다**
====================================
``<볼트>/리소스/sprites/Bale_asset_01.png``  (1536x1024) — ★ **몸통 전부**
``<볼트>/리소스/sprites/Bale_asset_02.png``  (1536x1024) — ★ **투사체만**

두 장은 <b>구성이 완전히 같고 장수만 다르다</b>(``_02`` 가 대략 두 배). 처음에는 장수가
많은 ``_02`` 를 정본으로 잡았는데 **그게 틀린 선택이었다**:

★★ 판단 근거 — **`_02` 는 칸을 가를 수가 없다**
-----------------------------------------------
``_02`` 는 같은 폭에 두 배를 그려 넣느라 **프레임이 서로 붙어 있다.** 네 가지 방법을
전부 재봤는데 다 실패했다(실측):

| 방법 | ``_02`` 결과 |
|---|---|
| 빈 열로 가르기 | 24칸이 **4~11칸**으로 붙는다 |
| 라벨 개수 | 두 자리 숫자가 붙어 16칸 줄에서 **13~14개** |
| 맨 앞·뒤 라벨 간격 | **맨 앞 라벨이 「1」이 아니다**(원거리 줄) — 한 칸씩 밀린다 |
| 폭 ÷ 장수 | 간격이 완전히 일정하지 않아 **프레임마다 옆 칸 망토가 딸려 온다** |

⚠ 게다가 ``_02`` 는 <b>헤더의 프레임 수도 틀리다</b> — 「대기 (16프레임)」인데 실제로
  그려진 것은 **14장**이고(빈 열로 세면 정확히 14덩어리), 라벨 번호가 건너뛰거나
  중복된다(대기에 12·15 없음 · 원거리에 14 가 두 번).

반면 ``_01`` 은 **프레임이 깨끗하게 떨어져 있다** — 대기 줄이 8덩어리에 폭 91~96px 로
고르다. 그림도 <b>1.4배 크게</b> 그려져 있어 픽셀이 더 많다. 장수(8~12장)는 이 프로젝트의
다른 유닛과 같은 대역이다(엘린 대기 8 · 라린길 대기 8 · 시그리드 대기 8).

→ **몸통 여섯 줄은 ``_01``**. 장수가 절반이지만 <b>자를 수 있는 쪽</b>이 정본이다.

★ 투사체만 ``_02`` 를 쓴다 — 이쪽은 반대다
------------------------------------------
투사체는 <b>연기 구체가 커졌다 흩어지는</b> 그림이라 장수가 많을수록 부드럽고, 서로
겹칠 일이 없어 ``_02`` 에서도 **덩어리가 깨끗하게 갈린다**(폭 32~42px 로 고르다).
``_01`` 은 8장인데 폭이 31~72px 로 들쭉날쭉하다. 그래서 **투사체만 ``_02``(16장)** 다.

⚠ 몸통과 투사체는 <b>따로 그려져 따로 재생</b>되므로 판본이 갈려도 화면에서 어긋날 곳이
  없다 — 라린길이 화염만 다른 판본에서 가져온 것과 같은 이유다(115절).

칸을 어떻게 가르나 — :func:`skin_sheet.cells_by_clusters`
---------------------------------------------------------
덩어리를 그대로 쓰되 **뒷정리 두 가지**를 한다(그쪽 주석에 자세히):
① 폭이 중앙값의 40% 미만인 **부스러기를 버린다**(이동 줄의 7px·5px 두 점),
② 중앙값의 1.55배를 넘는 **붙은 덩어리를 가른다** — 경계는 그 근처에서 잉크가 가장 적은
   열로 옮겨 팔·망토 한가운데를 자르지 않는다.

★ 그래서 <b>장수를 코드에 적지 않는다.</b> 시트 헤더가 틀렸으므로 세는 쪽이 정본이다 —
  아래 실행 결과가 그 값이고, 헤더와 ±1 안쪽이다.

★★ 시트에 **검은 액자**가 있다
------------------------------
구획 상자는 연회색인데 시트 전체를 감싼 테두리는 **검정**이다. 그래서 배경 흘려 채우기의
씨앗이 전부 액자 위에 떨어져 **시트 전부가 그림으로 잡혔다**(전 화소).
:func:`skin_sheet.erase_box_borders` 의 어두운 액자 판정과 :func:`background_mask` 의
안쪽 씨앗(``SEED_INSET``)이 그것을 막는다.

무엇이 어디로 가나 (배선은 `Editor/CharacterSkinBuilder.cs` 가 한다)
--------------------------------------------------------------------
| 폴더 | 원본 | 스킨 칸 |
|---|---|---|
| ``Char/Idle`` | ``_01`` | ``idleRight`` · ``idleLeft`` |
| ``Char/Move`` | ``_01`` | ``walkRight`` · ``walkLeft`` |
| ``Char/MeleeAttack`` | ``_01`` | ``attackRight`` · ``attackLeft`` |
| ``Char/RangedAttack`` | ``_01`` | ``rangedRight`` · ``rangedLeft`` |
| ``Char/Skill1`` | ``_01`` | ``skill1Right`` · ``skill1Left`` — 담뱃대 강타(130009) |
| ``Char/Skill2`` | ``_01`` | ``skill2Right`` · ``skill2Left`` — 담배연기(130010) |
| ``Char/Skill2Fx`` | ``_01`` | ``skill2Fx`` — 반원형 범위 이펙트 |
| ``Char/Projectile`` | ``_02`` | ``projectileFrames`` — 원거리 평타 탄환 |

⚠ **``skill1Fx`` 는 없다.** 담뱃대 강타는 범위 연출이 따로 없고 모션 안에 휘두르는 궤적이
  그려져 있다. 비워두면 `BossSkillCaster` 가 범위 표시를 생략한다 — 없는 그림을 지어내지 않는다.

방향 — **전부 오른쪽**
----------------------
담뱃대가 오른쪽으로 뻗고 연기도 오른쪽으로 퍼진다. 왼쪽은 미러다.

사용법:  python Tools/bale_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        그 다음 `python Tools/measure_skin_tiles.py`
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

from skin_sheet import (  # noqa: F401
    PPU, SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, cells_by_clusters, boxes_for, crop_rgba,
    body_anchor, base_anchor, compose, plant_feet, drop_stray_parts, write_png, ensure_folder_meta,
    shadow_in_box, sharpen_rgba,
    resample_rgba,
    clear_frames,
)

SRC_BODY = os.path.join(VAULT, "리소스", "sprites", "Bale_asset_01.png")
SRC_PROJ = os.path.join(VAULT, "리소스", "sprites", "Bale_asset_02.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Bale", "Char")

#: ★★ <b>선명도 보정</b> (유저 지시 2026-08-20 — 자세한 근거는 :func:`skin_sheet.sharpen_rgba`).
#:   베일은 콜라이더가 15x10 으로 <b>표에서 혼자 크고</b>(나머지 보스 11x7.5) 원화는 85px 라
#:   화면에서 <b>x7.4</b> 로 확대된다. 게다가 이 원화는 음영이 부드러워 확대하면 뭉개진다.
#:
#: ★ 값은 <b>네 단계를 눈으로 비교해서</b> 골랐다. 「인접 화소 대비」가 올라가는 만큼
#:   털·망토에 <b>흰 점(언샵이 부풀린 잡티)</b>도 늘어나므로, 대비 이득의 대부분을 가져가면서
#:   잡티가 가장 적은 지점을 잡았다(실측 — 밝은 점 개수는 원본 34개 기준):
#: <code>
#:     원본          대비 20.8   흰 점  34
#:     0.40/1.2/6    대비 30.3   흰 점 176   ← 채택 (대비 +46%)
#:     0.55/1.2/5    대비 32.0   흰 점 229
#:     0.90/1.0/2    대비 34.7   흰 점 282   ← 털에 흰 점이 눈에 띈다
#: </code>
#: ⚠ threshold 를 6 으로 올린 것이 잡티를 줄이는 핵심이다 — 평탄한 면(검은 옷)은 건드리지
#:   않고 <b>경계만</b> 조인다.
SHARPEN_AMOUNT = 0.40
SHARPEN_RADIUS = 1.2
SHARPEN_THRESHOLD = 6

SKIN_SPEC = {
    "skinAssetName": "Skin_Bale",
    # ⚠ 웨이브 보스는 종마다 폴더 하나다 — 한 폴더에 몰아넣으면
    #   `CharacterAnimator.PickRandomSkin` 이 다른 몬스터에게 이 외형을 줄 수 있다.
    "outputFolder": "Assets/_Project/Resources/MonsterSkins/Bale",
    "displayName": "베일",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "14",
    # 담뱃대에서 뿜는 연기 구체. 베일 몸집이 15타일이라 1.4 면 주먹만 하게 보인다.
    "projectileWidthTiles": "1.4",
}

# ──────────────────────────────────────────────────────────────────────────
# 시트 좌표 — **실측**. 장수는 적지 않는다(세는 쪽이 정본 · 맨 위 ★).
#   (폴더, 원본, y0, y1, x0, x1, kind)
# ──────────────────────────────────────────────────────────────────────────
ROWS = [
    ("Idle",         "body",  66, 152,  10, 1523, "body"),
    ("Move",         "body", 186, 269,  10, 1523, "body"),
    ("MeleeAttack",  "body", 303, 388,  10, 1523, "body"),
    ("RangedAttack", "body", 423, 510,  10, 1523, "body"),
    # ⚠ 패턴 두 줄은 **구획 제목과 프레임 번호가 그림과 같은 y 에 걸친다**(캐릭터가
    #   그만큼 높다). 위 네 줄과 달리 제목 줄이 따로 떨어져 있지 않아 잉크 밴드로는
    #   못 가른다 — 그래서 y0 를 글자 아래로 **직접 내려 잡았다**(실측: 제목이
    #   Skill1 560~577 · Skill2 706~730). 담뱃대 끝이 몇 px 잘리지만 글자가 프레임에
    #   섞여 들어가는 것보다 낫다.
    # ★★ 2026-08-20 — <b>밴드를 위로 다시 잡았다</b> (유저: *"지금도 이펙트 짤리는거 같은데 베일"*).
    #   예전 값(586 · 732)은 «제목이 섮이느니 안전하게» 내려 잡은 것이고, 그만큼
    #   <b>들어올린 담배대와 연기 기둥의 윗부분이 잘려</b> 있었다. 실측:
    #
    #       담배대 줄   번호 560~577 · <b>그림 578</b>~688   → 예전에 8줄 잘림
    #       담배연기 줄 번호 706~721 · <b>그림 722</b>~815   → 예전에 10줄 잘림
    #
    #   ⚠ 이 두 줄은 번호가 <b>그림 바로 위</b>에 붙어 있어 여유가 거의 없다 —
    #     다시 재지 않고 숫자만 옮기지 말 것.
    ("Skill1",       "body", 578, 688,  10, 1523, "body"),
    ("Skill2",       "body", 722, 815,  10, 1523, "body"),
    # ★ 투사체만 다른 판본 (맨 위 ★). 오른쪽의 반원형 이펙트를 안 먹게 x 를 막는다.
    #   ⚠ 이 판본은 라벨(896~904)이 그림(925~963)과 **떨어져** 있어 그대로 잘라내면 된다.
    ("Projectile",   "proj", 925, 963,  10,  755, "fx"),
]

#: ★★ <b>줄 안에서 «몸통» 칸만 골라낸다</b> (2026-08-20 · 유저 리포트 그대로 고친 것)
#:
#:   *"베일 스킬 쓰면 베일에게서 담배연기가 나가는게 아니라 베일이 담배연기 자체가
#:   되어 버리던데"* — 원인은 <b>패턴 두 줄이 몸통과 연기를 한 줄에 담고 있다</b>는 것이다.
#:   칸을 세어 전부 모션으로 구웠더니 재생 중간에 <b>몸이 사라지고 연기만</b> 남았다.
#:
#:   어느 칸이 몸통인지는 <b>어두운 픽셀의 비율</b>로 갈린다(실측 · 광도 70 미만):
#:
#:       담배연기 줄  0~3번 56~59%(몸통) · 4~8번 <b>6~23%</b>(연기) · 9~11번 42~59%(몸통)
#:       담뱃대 줄    6번 <b>3.3%</b> · 9번 <b>10.3%</b> 만 이펙트, 나머지는 몸통
#:
#:   베일은 <b>검은 갑옷</b>이고 연기는 연보라라서 이 값이 두 배 넘게 갈린다. 다만
#:   <b>자동 판정에 맡기지 않고</b> 실측한 번호를 여기 박는다 — 원화가 바뀌면 다시 재야 한다
#:   (카이론·불칸도 같은 방식으로 처리했다 · `char_sheet.Row.keep`).
CELL_KEEP = {
    "Skill1": [0, 1, 2, 3, 4, 5, 7, 8, 10, 11, 12],
    "Skill2": [0, 1, 2, 3, 9, 10, 11],
}

#: 버리는 칸을 <b>따로 구워 두는</b> 폴더 — 원화를 잃지 않기 위해서다.
#: ⚠ ``Unused_`` 라 유니티 빌더가 스킨 칸으로 읽지 않는다.
#:
#: ★★ 2026-08-21 — <b>``Skill2`` 의 남는 칸을 ``Skill2Fx`` 로 굽는다</b>
#:   (유저: *"보스 베일 이펙트 이상한거 있었으니까"*).
#:
#:   UI-44 에서 «반원형 범위 표시» 를 스킬 칸에서 빼고 `BossSkillCaster.PlayBreath` 가
#:   <b>투사체 원화</b>(연기 구체 14장)를 앞쪽에 깔게 했다. 그런데 그 원화는
#:   «날아가다 흩어지는 <b>둥근 구체</b>» 라, 앞쪽 반원에 깔면 <b>공깃돌이 굴러다니는</b>
#:   것처럼 보인다 — 「입에서 뿜는 연기」가 아니다.
#:
#:   ★ 정작 <b>진짜 연기 브레스 원화가 이 시트에 있었다</b> — 스킬 2 줄 가운데의
#:     «입에서 앞으로 길게 뿜어지는 연기» 다. 그런데 그것은 <b>프레임 다섯 장이 아니라
#:     한 장의 이어진 그림</b>이다(실측: 잉크가 ``x435~916`` 로 한 덩어리 · 481px).
#:     덩어리 판정이 그 긴 구름을 <b>다섯 조각으로 잘라</b> ``Unused_Skill2Smoke`` 에
#:     넣어 두었던 것이고, 그래서 «구름 토막» 들이 남아 있었다.
#:   → 그 구름은 :data:`SINGLE_FX` 에서 <b>한 장으로</b> 잘라 ``Skill2Fx`` 로 굽는다.
#:     조각들은 ``Unused_Skill2Smoke`` 로 그대로 남긴다(원화 보존).
#:   ⚠ 반원형 범위 표시(``Unused_Skill2Fx``)는 <b>그대로 안 쓴다</b> — 그 판단은 유효하다.
CELL_SPILL = {
    "Skill1": "Unused_Skill1Fx",
    "Skill2": "Unused_Skill2Smoke",
}

#: ★★ <b>해상도를 3배로 굽는다</b> (2026-08-20 · 유저 리포트: *"베일 너무 이미지 깨짐"*).
#:
#:   원인은 <b>원화가 작은데 게임에서 크게 그린다</b>는 것이다 — 실측:
#:
#:       원화 대기 110 x 87 px  ·  게임 크기 <b>15 x 10 타일</b>(보스 중 가장 크다)
#:       → 배율 약 <b>7.4배</b>. 다른 보스는 11 x 7.5 타일이라 5.8배다.
#:
#:   필터가 Point 라 7.4배로 늘리면 <b>7px 짜리 계단</b>이 그대로 보인다. 그래서
#:   ① 프레임을 Lanczos 로 <b>3배</b>로 키워 굽고 ② ppu 도 3배(64 → 192)로 올린다.
#:   둘을 같이 올리므로 <b>게임 안의 크기는 한 픽셀도 안 바뀌고</b> 텍스처만 촘촘해진다
#:   (크기는 ``contentSizeTiles`` = 픽셀 ÷ ppu 로 정해지기 때문이다).
#:   ③ 필터도 <b>Bilinear</b> 로 바꾼다 — 베일 원화는 픽셀아트가 아니라 손그림이라
#:      남은 2.5배 확대는 부드럽게 늘리는 편이 맞다.
#:
#:   ⚠ <b>없던 디테일이 생기지는 않는다.</b> 계단이 곡선으로 바뀌는 것이지 해상도가
#:     진짜로 오르는 것이 아니다 — 더 선명하게 하려면 <b>원화를 크게 다시</b> 받아야 한다.
SUPERSAMPLE = 3
from skin_sheet import PPU as _PPU, FILTER_BILINEAR
BAKE_PPU = _PPU * SUPERSAMPLE

#: 한 장짜리 이펙트 — 칸을 가를 것이 없다. (폴더, 원본, y0, y1, x0, x1)
#:
#: ★★ 2026-08-20 — <b>``Skill2Fx`` → ``Unused_Skill2Fx``</b> (유저 지시: *"Pipe_smoke 의
#:   기획 의도가 베일이 바라보는 방향으로 연기 브레스를 쏘는건데 지금 에셋에 있는 담배연기
#:   패턴 반원형 이펙트를 빼고 만들어줘"*).
#:   이 원화는 <b>바닥에 깔던 「반원형 범위 표시」</b>다 — 범위를 알려주는 그림이지
#:   «입에서 뿜는 연기» 가 아니다. 그래서 스킬 칸에서 빼고, 연출은
#:   `BossSkillCaster.PlayBreath` 가 <b>연기 구체 원화</b>(투사체 칸)를 앞쪽에 깔아 만든다.
#:   ⚠ 원화는 <b>지우지 않고</b> ``Unused_`` 로 남긴다 — 나중에 범위 표시가 다시 필요해지면
#:     접두사만 떼면 된다.
SINGLE_FX = [
    ("Unused_Skill2Fx", "body", 881, 1004, 780, 1523),
    # ★★ 2026-08-21 — <b>담배연기 브레스</b>. 스킬 2 줄 가운데의 이어진 구름 한 장이다
    #   (위 :data:`CELL_SPILL` 의 ★). x0=435 는 <b>담뱃대 끝 다음</b>이다 — 430 이면
    #   4번 몸통의 담뱃대가 들어오고, 440 부터면 구름 앞머리가 잘린다(실측 잉크 435~916).
    ("Skill2Fx", "body", 722, 815, 435, 920),
]

#: 좌우 방향이 없는 묶음 — 파일 이름에 Right/Left 를 안 붙인다.
NO_DIRECTION = {"Projectile", "Unused_Skill2Fx", "Skill2Fx"}


#: ★ 이름을 바꾼 폴더의 <b>옛 자리</b>. 지우지 않으면 유니티 빌더가 그 폴더를 여전히
#:   스킨 칸으로 읽어 <b>빼려던 원화가 계속 배선된다</b>(실제로 그랬다 —
#:   `Skill2Fx(1)` 이 그대로 남아 있었다).
#:   ⚠ 옛 파일은 <b>같은 원화</b>가 `Unused_Skill2Fx` 로 옮겨간 것이라 잃는 것이 없다.
#: ⚠ 2026-08-21 — ``Skill2Fx`` 는 다시 <b>만드는</b> 폴더가 되었으므로 목록에서 뺐다
#:   (이번에는 :data:`SINGLE_FX` 가 만든다).
STALE_FOLDERS = []


def drop_stale_folders():
    """이름이 바뀌어 더 이상 만들지 않는 폴더를 지운다 (엘린 스크립트의 옛 파일 정리와 같은 취지)."""
    removed = 0
    for name in STALE_FOLDERS:
        folder = os.path.join(DST_ROOT, name)
        if not os.path.isdir(folder):
            continue
        for f in os.listdir(folder):
            os.remove(os.path.join(folder, f))
            removed += 1
        os.rmdir(folder)
        meta = folder + ".meta"
        if os.path.exists(meta):
            os.remove(meta)
        print("  옛 폴더 삭제: %s (%d개 파일)" % (name, removed))
    return removed


def write_group(images, name):
    folder = os.path.join(DST_ROOT, name)
    gone = clear_frames(folder)
    if gone:
        print('      ㈛ 예전 프레임 %d개 지움 (%s)' % (gone, name))
    kw = {"ppu": BAKE_PPU, "filter_mode": FILTER_BILINEAR}
    n = 0
    for i, img in enumerate(images):
        if name in NO_DIRECTION or name.startswith("Unused_"):
            write_png(img, folder, "Char_%s_%02d" % (name, i), **kw)
            n += 1
        else:
            write_png(img, folder, "Char_%s_Right_%02d" % (name, i), **kw)
            write_png(img.transpose(Image.FLIP_LEFT_RIGHT), folder,
                      "Char_%s_Left_%02d" % (name, i), **kw)
            n += 2
    ensure_folder_meta(folder)
    return n


def build(sheets):
    made = 0
    for name, src, y0, y1, x0, x1, kind in ROWS:
        sheet = sheets[src]
        cells = cells_by_clusters(sheet["mask"], y0, y1, x0, x1)
        if not cells:
            raise SystemExit("⚠ %s: 칸을 하나도 못 찾았습니다 (y%d~%d)" % (name, y0, y1))

        # ★ 몸통 줄만 발밑 그림자를 지운다 — 이펙트는 그 자체가 연출이다.
        if kind == "body":
            rough = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
            shadow = np.zeros(sheet["mask"].shape, dtype=bool)
            for b in rough:
                shadow |= shadow_in_box(sheet, b)
            sheet["mask"] &= ~shadow

        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1, name=name) if b is not None]

        # ★★ 몸통 칸만 남긴다. 버리는 칸은 잃지 않게 따로 굽는다 (위 CELL_KEEP).
        keep = CELL_KEEP.get(name)
        spill = []
        if keep is not None:
            if max(keep) >= len(boxes):
                raise SystemExit(
                    "⚠ %s: 칸이 %d개인데 CELL_KEEP 이 %d번을 가리킵니다 — "
                    "원화가 바뀌었으면 어두운 픽셀 비율을 다시 재세요." % (name, len(boxes), max(keep)))
            spill = [b for i, b in enumerate(boxes) if i not in keep]
            boxes = [boxes[i] for i in keep]

        def bake(bs):
            fr = [sharpen_rgba(crop_rgba(sheet, b), SHARPEN_AMOUNT, SHARPEN_RADIUS,
                               SHARPEN_THRESHOLD) for b in bs]
            if SUPERSAMPLE != 1:
                fr = [resample_rgba(f, float(SUPERSAMPLE)) for f in fr]
            return fr

        frames = bake(boxes)
        anchor = body_anchor if kind == "body" else base_anchor
        if kind == "body":
            # ★★ <b>옆 프레임에서 들어온 떠 있는 조각을 뗀다</b>
            #   (:func:`skin_sheet.drop_stray_parts` 의 ★★ · 2026-08-22).
            #   유저 리포트: *"이동 모션 사이 사이에 전 동작 모션과 함께 짤려 들어가서
            #   어색해지는 부분들"*.
            frames = [drop_stray_parts(f)[0] for f in frames]
        anchors = [anchor(f) for f in frames]
        if kind == "body":
            # ★★ <b>발을 피벗에 맞춘다</b> — 모션이 바뀔 때 옆으로 미끄러지지 않게
            #   (:func:`skin_sheet.plant_feet` 의 ★★). 묶음 안의 움직임은 그대로 둔다.
            anchors, _shift = plant_feet(frames, anchors)
        images, w, h = compose(frames, anchors)

        made += write_group(images, name)
        print("  %-13s (%s) %3d x %3d · %2d장  폭 %s"
              % (name, src, w, h, len(images), [e - s + 1 for s, e in cells]))

        if spill:
            sf = bake(spill)
            si, sw, sh = compose(sf, [base_anchor(f) for f in sf])
            made += write_group(si, CELL_SPILL[name])
            print("  %-13s      %3d x %3d · %2d장  (배선 안 함 — 원화 보존)"
                  % (CELL_SPILL[name], sw, sh, len(si)))

    for name, src, y0, y1, x0, x1 in SINGLE_FX:
        sheet = sheets[src]
        box = boxes_for(sheet["mask"], [(x0, x1)], y0, y1)[0]
        if box is None:
            raise SystemExit("⚠ %s: 그림을 못 찾았습니다" % name)
        rgba = sharpen_rgba(crop_rgba(sheet, box), SHARPEN_AMOUNT, SHARPEN_RADIUS,
                            SHARPEN_THRESHOLD)
        if SUPERSAMPLE != 1:
            rgba = resample_rgba(rgba, float(SUPERSAMPLE))
        images, w, h = compose([rgba], [base_anchor(rgba)])
        made += write_group(images, name)
        print("  %-13s (%s) %3d x %3d ·  1장" % (name, src, w, h))

    return made


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[베일 모션 시트 분해]")

    # ★★ 검은 액자를 배경 판정보다 먼저 지운다 (맨 위 ★★).
    sheets = {
        "body": load_sheet(SRC_BODY, box_borders=True),
        "proj": load_sheet(SRC_PROJ, box_borders=True),
    }

    drop_stale_folders()
    n = build(sheets)
    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/bale_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  → 프레임 %d장" % n)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
