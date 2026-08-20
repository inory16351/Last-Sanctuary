# -*- coding: utf-8 -*-
"""불칸(캐릭터 9011) 모션 시트 → 프레임 분해 (2026-08-20).

원본: ``<볼트>/리소스/sprites/Vulcan_asset.png`` (1536x1024)

⚠ **탈진 모션은 굽지 않는다** — 유저 지시: *"불칸은 탈진 모션 필요 없으니까 빼고"*.
   원화(시트 오른쪽 아래 「사용 후 탈진 모션」 3장)는 그대로 두고 **읽지 않는다**.
   아르세니아는 반대다 — 그쪽은 스킬 정의문에 «{value_05}초 행동 불능» 이 있어 굽는다.

⚠ **「스킬 3 : 메테오 강하」 줄도 배선하지 않는다** — 표의 불칸 스킬은 셋인데
   (`Blazing_anger`·`The_wisdom_of_a_sage`·`Flame_blast`) **발동형은 「화염 세례」 하나**다.
   나머지 둘은 상시 효과라 시전 모션이 없다. 메테오는 그 셋 중 <b>어디에도 해당하지 않는다</b> —
   표가 정본이므로 지어내지 않고 ``Unused_`` 로 남긴다(그림은 보존된다).

시트 구조
---------
가로 띠 다섯 벌이고, 띠마다 구획이 2~5개씩 들어 있다. 구획마다 제목·번호 줄의 y 가
달라서 **구획별로 따로 실측**했다(아르세니아와 같은 이유).

무엇이 어디로 가나
------------------
| 폴더 | 장수 | 스킨 칸 | 근거 |
|---|---:|---|---|
| ``Idle`` | 3 | ``idleRight/Left`` | |
| ``Walk`` | 7+7 | ``walkRight/Left`` | 원화 두 줄(→ · ←) |
| ``MeleeAttack`` | 5 | ``attackRight/Left`` | 지팡이 휘두르기 |
| ``MeleeTravelFx`` | 4 | ``meleeTravelFrames`` | 「타격 이펙트」 — 휘두른 궤적이 앞으로 뻗는다 |
| ``RangedAttack`` | 3 | ``rangedRight/Left`` | 뒤 1칸은 손을 떠난 불덩이 |
| ``Projectile`` | 5 | ``projectileFrames`` | 푸른 불덩이 |
| ``MagicAttack`` | 4 | ``magicRight/Left`` | |
| ``ImpactMagic`` | 4 | ``magicImpactFrames`` | 마법 타격 이펙트 |
| ``Heal`` | 7 | ``healRight/Left`` | |
| ``HealFx`` | 5 | ``healFxFrames`` | |
| ``Skill1`` | 3 | ``skill1Right/Left`` | **화염 세례**(80033) |
| ``Skill1Projectile`` | 5 | ``skill1Projectile`` | 붉은 불덩이 |
| ``Skill1Fx`` | 4 | ``skill1Fx`` | 착탄 폭발 |
| ``Unused_Skill3*`` | — | — | 메테오(표에 없는 스킬) |

⚠ 크기 정규화는 **하지 않는다** — 시트가 «캐릭터 크기 기준: 약 64x64px» 이라고 적어 두었다
  (카이론·아르세니아와 같은 판단 · 유저 지시 *"모션 생성중에 캐릭터의 크기가 바뀌지 않도록"*).

사용법:  py -3 Tools/vulcan_skin_build.py
"""

import os

from vault_path import VAULT, PROJECT
from char_sheet import Row, Spec, run

SRC = os.path.join(VAULT, "리소스", "sprites", "Vulcan_asset.png")
DST = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Vulcan", "Char")

C = ("clusters",)
G8 = ("gaps", 8)
S = ("span",)

SPEC = Spec(
    title="불칸",
    sources={"01": SRC},
    dst_root=DST,
    skin_spec={
        "skinAssetName": "Skin_Vulcan",
        "displayName": "불칸",
        "framesPerSecond": "10",
        "attackFramesPerSecond": "14",
        "groundImpactOnly": "0",
        "projectileWidthTiles": "1.0",
        "impactWidthTiles": "1.6",
        "meleeTravelWidthTiles": "1.2",
    },
    rows=[
        Row("Idle",        "body",  63, 147,    0,  270, C, 3),
        Row("WalkRight",   "body",  58, 131,  270,  790, C, 7, folder="Walk", side="Right"),
        Row("WalkLeft",    "body", 184, 256,  270,  790, C, 7, folder="Walk", side="Left"),

        # ★ 4번째 칸은 <b>몸통 없이 그려진 푸른 굤적</b>이다 — 그대로 굽으면
        #   «불칸이 굤적 자체가 되는» 사고가 난다(베일·카이론과 같은 사고).
        Row("MeleeAttack",   "body",  56, 138,  790, 1230, C, 6, keep=[0, 1, 2, 4, 5]),
        Row("MeleeTravelFx", "fx",    82, 152, 1240, 1535, C, 4),

        Row("RangedAttack", "body", 328, 406,    0,  330, C, 4, take=3),
        Row("Projectile",   "fx",   358, 387,  340,  760, C, 5),

        Row("MagicAttack",  "body", 295, 383,  790, 1180, C, 5, take=4),
        Row("Unused_MagicProjectile", "fx", 330, 373, 1190, 1535, C, 4),
        Row("ImpactMagic",  "fx",   428, 496,  960, 1400, C, 4),

        Row("Heal",         "body", 527, 609,   19,  632, C, 7),
        Row("HealFx",       "fx",   561, 608,  652, 1075, C, 6, take=5),

        Row("Skill1",           "body", 680, 751,   0,  330, C, 4, take=3),
        Row("Skill1Projectile", "fx",   709, 743, 340,  760, C, 5),
        Row("Skill1Fx",         "fx",   692, 752, 793, 1132, C, 4),

        # ── 표에 없는 스킬(메테오) — 그림만 보존한다 ────────────────
        Row("Unused_Skill3Cast",   "body", 811, 894,   0,  270, C, 4),
        Row("Unused_Skill3Circle", "fx",   838, 899, 280,  470, C, 2),
        Row("Unused_Skill3Meteor", "fx",   791, 901, 485,  900, C, 5),
        Row("Unused_Skill3Burst",  "fx",   809, 901, 910, 1230, C, 3),
    ],
    no_direction=("MeleeTravelFx", "Projectile", "ImpactMagic", "HealFx",
                  "Skill1Projectile", "Skill1Fx", "Unused_MagicProjectile",
                  "Unused_Skill3Circle", "Unused_Skill3Meteor", "Unused_Skill3Burst"),
    scale_reference="Idle",
    scale_metric=None,      # ★ 맨 위 ⚠ — 시트가 이미 같은 크기로 그려져 있다
    dominant_join=0.05,
)


if __name__ == "__main__":
    run(SPEC)
