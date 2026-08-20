# -*- coding: utf-8 -*-
"""아르세니아(캐릭터 9010) 모션 시트 → 프레임 분해 (2026-08-20).

원본: ``<볼트>/리소스/sprites/Arsenia_asset.png`` (1536x1024)

★ **한 팔이 없는 것은 원화가 이미 그렇게 그려져 있다**
------------------------------------------------------
유저 요청: *"가능하다면 아르세니아는 한팔이 없는게 인게임에서도 보이면 좋겠어"*.
시트 맨 아래 주석이 스스로 <b>«캐릭터는 왼팔이 없으며, 모든 동작에 표현되었습니다»</b> 라고
적어 두었다 — 즉 <b>자르기만 제대로 하면 저절로 보인다.</b> 여기서 할 일은 «없는 팔을
만들지 않는 것», 곧 <b>옆 칸 조각이 팔처럼 붙어 들어오지 않게</b> 하는 것이다
(:data:`char_sheet.Spec.dominant_join`). 실제로 잘라서 눈으로 확인했다.

★★ 이 시트는 **라벨을 믿을 수 없다**
------------------------------------
프레임 번호가 그림에 가려지거나 빠진 줄이 많다(실측: 이동 → 줄은 7칸인데 라벨이 6개,
← 줄은 잡음까지 11개). 그래서 줄마다 **가장 잘 듣는 방법**을 따로 골랐다:

| 방법 | 어디에 | 왜 |
|---|---|---|
| ``clusters`` | 몸통 줄 전부 | 프레임이 서로 떨어져 있어 덩어리가 곧 칸이다(부스러기·붙은 덩어리는 그 함수가 정리한다) |
| ``span`` | **반짝이는 이펙트 줄** | 별·입자가 흩어져 있어 덩어리로 세면 <b>38~62칸</b>이 나온다(실측). 폭 ÷ 장수가 유일하게 통한다 |

⚠ ``span`` 은 «장수를 안다»는 전제이므로 :attr:`Row.expect` 가 **검산이 아니라 입력**이 된다 —
  값은 시트를 눈으로 세어 적었다. 잘못 적으면 조용히 어긋나므로 잘라서 확인했다.

★ 한 줄에 여러 묶음이 들어 있다
-------------------------------
「원거리 공격」 줄에는 몸통 3칸 + <b>손을 떠난 물약 2칸</b>이 붙어 있고, 그 오른쪽에
「투사체」가, 그 아래에 「폭발 이펙트」가 따로 있다. 마법·회복도 같은 짜임이다.
몸통 줄은 ``take`` 로 앞쪽만 굽는다.

무엇이 어디로 가나
------------------
| 폴더 | 장수 | 스킨 칸 | 근거 |
|---|---:|---|---|
| ``Idle`` | 3 | ``idleRight/Left`` | |
| ``Walk`` | 7+7 | ``walkRight/Left`` | 원화 두 줄(→ · ←) |
| ``RangedAttack`` | 3 | ``rangedRight/Left`` | 뒤 2칸은 손을 떠난 물약 |
| ``MagicAttack`` | 3 | ``magicRight/Left`` | 〃 |
| ``Heal`` | 3 | ``healRight/Left`` | 〃 |
| ``Projectile`` | 5 | ``projectileFrames`` | 원거리 투사체(보라 물약) |
| ``Impact`` | 5 | ``impactFrames`` | 원거리 폭발 |
| ``ImpactMagic`` | 5 | ``magicImpactFrames`` | 마법 폭발 |
| ``HealFx`` | 5 | ``healFxFrames`` | 회복 이펙트 |
| ``Skill2`` | 5 | ``skill2Right/Left`` | **성스러운 축복**(80029) |
| ``Skill2Fx`` | 3 | ``skill2Fx`` | <b>회복 공간</b> — 스킬이 «공간을 생성»하므로 이쪽이 맞다 |
| ``Skill3`` | 5 | ``skill3Right/Left`` | **완성되지 못한 고귀함**(80030 · 시전 준비) |
| ``Skill3Fx`` | 7 | ``skill3Fx`` | 레이저 낙하 |
| ``Stun`` | 4 | ``stunRight/Left`` | ★ 신설 — 스킬 3의 «{value_05}초 행동 불능» |
| ``Unused_Skill2Burst`` | 5 | — | 스킬 2 의 원형 폭발. 배선할 칸이 없어 남긴다 |
| ``Unused_Angel`` | 1 | — | 「천사 강림」 한 장. 한 장짜리라 재생할 자리가 없다 |

⚠ ``근거리 공격`` 줄이 **아예 없다** — 표의 「불안정성」(80028)이
  *"아르세니아는 근거리 공격 유형을 선택할 수 없습니다"* 라고 못박고 있어 원화도 안 그렸다.
  스킨에 근접 칸이 비면 `Attack()` 이 원거리로 폴백하므로 문제가 없다.

⚠ 크기 정규화는 **하지 않는다** — 시트가 «캐릭터 크기 기준: 약 64x64px» 이라고 적어 두었고
  실제로 줄마다 같은 크기다(카이론과 같은 판단 · 유저 지시 *"모션 생성중에 캐릭터의 크기가
  바뀌지 않도록 주의"*).

사용법:  py -3 Tools/arsenia_skin_build.py
"""

import os

from vault_path import VAULT, PROJECT
from char_sheet import Row, Spec, run

SRC = os.path.join(VAULT, "리소스", "sprites", "Arsenia_asset.png")
DST = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Arsenia", "Char")

C = ("clusters",)
S = ("span",)
G = ("gaps",)
G8 = ("gaps", 8)      # 입자 사이의 3px 틈을 무시한다(맨 위 ★)

SPEC = Spec(
    title="아르세니아",
    sources={"01": SRC},
    dst_root=DST,
    skin_spec={
        "skinAssetName": "Skin_Arsenia",
        "displayName": "아르세니아",
        "framesPerSecond": "10",
        "attackFramesPerSecond": "14",
        "groundImpactOnly": "0",
        "projectileWidthTiles": "0.8",
        "impactWidthTiles": "1.6",
    },
    rows=[
        # ── 좌표는 **구획마다 따로 실측**했다 ──────────────────────────
        #   ⚠ 이 시트는 한 가로 띠 안에 구획이 셋씩 들어 있고 <b>구획마다 제목·번호 줄의
        #     y 가 다르다.</b> 처음에 띠 전체로 잡았다가 프레임에 <b>번호와 한글 제목이
        #     그대로 박혀</b> 나왔다(잘라서 눈으로 확인). 그래서 구획별로 다시 쟀다.
        #   ⚠ 분리 이펙트 줄은 <b>제목이 그림과 같은 y</b> 에 왼쪽으로 붙어 있다 —
        #     지우는 대신 **x0 를 제목 오른쪽 빈 열로** 민다(실측값).
        #   ★ 이펙트 줄은 ``gaps``(빈 열) 로 가른다 — ``clusters`` 는 흩어진 입자를
        #     제각각 세어 <b>38~62칸</b>이 나오고, ``span``(폭÷장수) 은 간격이 고르지 않아
        #     <b>옆 폭발의 조각이 딸려 들어온다</b>(둘 다 실제로 겪었다).
        Row("Idle",        "body",  68, 154,    0,  300, C, 3),
        Row("WalkRight",   "body",  62, 131,  300,  800, C, 7, folder="Walk", side="Right"),
        Row("WalkLeft",    "body", 184, 256,  300,  800, C, 7, folder="Walk", side="Left"),

        Row("RangedAttack", "body", 75, 139,  820, 1185, C, 5, take=3),
        Row("Projectile",   "fx",   90, 120, 1220, 1530, G8, 5),
        Row("Impact",       "fx",  187, 263,  975, 1530, G, 5),

        Row("MagicAttack",  "body", 333, 400,   22,  395, C, 5, take=3),
        Row("Unused_MagicProjectile", "fx", 355, 390, 430, 760, G8, 5),
        Row("ImpactMagic",  "fx",   433, 516,  175,  760, G8, 5),

        Row("Heal",         "body", 336, 402,  770, 1128, C, 5, take=3),
        Row("Unused_HealProjectile", "fx", 355, 390, 1170, 1530, G8, 5),
        Row("HealFx",       "fx",   438, 517,  905, 1530, G, 5),

        Row("Skill2",       "body", 599, 671,   17,  399, C, 6, take=5),
        Row("Unused_Skill2Projectile", "fx", 622, 647, 425, 700, G8, 4),
        Row("Unused_Skill2Burst",      "fx", 604, 665, 710, 1055, G, 5),
        Row("Skill2Fx",     "fx",   596, 662, 1096, 1530, C, 3),

        Row("Skill3",       "body", 755, 846,   17,  323, S, 5),
        Row("Unused_Angel", "fx",   754, 861,  364,  570, C, 1),
        Row("Skill3Fx",     "fx",   735, 862,  608, 1185, C, 7),
        # ★★ <b>누운 자세라 가로로 넘친다</b> — 밴드를 좌우로 더 넣지 않으면 잘린다.
        #   유저 리포트: *"탈진 모습이 잘리네"*. 실측한 잉크 범위는 x 1163~1516 · y 772~857.
        Row("Stun",         "body", 772, 857, 1179, 1516, S, 4, scale=False),
    ],
    no_direction=("Projectile", "Impact", "ImpactMagic", "HealFx", "Skill2Fx", "Skill3Fx",
                  "Unused_MagicProjectile", "Unused_HealProjectile",
                  "Unused_Skill2Projectile", "Unused_Skill2Burst", "Unused_Angel"),
    scale_reference="Idle",
    # ★★ 정규화 안 함 — 맨 위 ⚠ 참조.
    scale_metric=None,
    # ★ 한 팔이 없는 실루엣을 지키려면 <b>옆 칸 조각을 확실히 끊어야</b> 한다 —
    #   물약·입자가 몸 근처까지 흩어져 있어 기본값(0.12)으로는 붙는다.
    dominant_join=0.05,
)


if __name__ == "__main__":
    run(SPEC)
