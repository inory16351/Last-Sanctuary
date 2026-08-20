# -*- coding: utf-8 -*-
"""카이론(캐릭터 9009) 모션 시트 → 프레임 분해 (2026-08-20).

원본: ``<볼트>/리소스/sprites/Chiron_asset.png`` (1536x1024)

119-7절이 «모션 시트가 없다»고 남겨둔 그 시트가 왔다. 자르는 방법은
:mod:`char_sheet` 가 갖고 있고 여기는 **좌표표와 배선표**만 든다.

★★ 이 시트의 함정 — **몸통과 이펙트가 «번갈아» 들어 있다**
-----------------------------------------------------------
아루 시트는 «앞쪽 몇 칸이 몸통»이라 ``take`` 로 잘렸는데, 카이론은 <b>가운데 칸이 이펙트</b>다:

    근거리 공격 :  1 2 4 5 6 [7=이펙트] 8 9 10 [12=이펙트]
    회복       :  1 2 3 4 6 7 8 9 [10 11=이펙트] 12 [13=이펙트]

앞에서 N칸을 자르는 방식으로는 가운데를 못 버린다 → :attr:`char_sheet.Row.keep` 를
새로 만들어 **남길 칸 번호를 직접 적는다**. 그대로 구우면 «카이론이 파란 빛 자체가 되는»
사고가 난다 — 베일이 담배연기가 되어 버린 것과 <b>같은 사고</b>다(이번에 같이 고쳤다).

★ 단(段) 경계가 **줄마다 다르다**
---------------------------------
왼쪽/오른쪽 두 단인데 그 경계가 한 줄로 곧지 않다(실측한 빈 열):

    대기 | 원거리      x 672~712  → 690 에서 가른다
    이동 | 마법        x 669~717  → 690
    근거리 | 회복      x 685~704  → 695
    스킬1 | 스킬2      x 539~629  → **584**   ← 여기만 크게 왼쪽이다
    스킬3               한 줄이 **시트 폭 전체**를 쓴다 (x 21~1248)

★ **이동은 좌/우 두 줄**이 따로 그려져 있다(제목에 → · ← 화살표가 있다).
  미러하지 않고 그대로 쓴다. :func:`char_sheet.report_tilt` 로 방향을 검산한다.

무엇이 어디로 가나
------------------
| 폴더 | 장수 | 스킨 칸 | 근거 |
|---|---:|---|---|
| ``Idle`` | 8 | ``idleRight/Left`` | |
| ``Walk`` | 7+7 | ``walkRight/Left`` | 원화 두 줄 |
| ``MeleeAttack`` | 8 | ``attackRight/Left`` | 이펙트 칸 둘 제외 |
| ``RangedAttack`` | 7 | ``rangedRight/Left`` | 뒤 4칸은 투사체 |
| ``MagicAttack`` | 6 | ``magicRight/Left`` | 뒤 5칸은 투사체 |
| ``Heal`` | 9 | ``healRight/Left`` | 이펙트 칸 셋 제외 |
| ``Projectile`` | 7 | ``projectileFrames`` | 아래 분리 이미지 줄 |
| ``ImpactMagic`` | 7 | ``magicImpactFrames`` | 〃 |
| ``HealFx`` | 6 | ``healFxFrames`` | 〃 |
| ``Skill1`` | 7 | ``skill1Right/Left`` | **타락한 육체**(80025 · 보호막) |
| ``Skill2`` | 8 | ``skill2Right/Left`` | **천상의 방패**(80026 · 도발 후 폭발) |
| ``Skill2Fx`` | 4 | ``skill2Fx`` | 아래 분리 이미지 줄(지면 폭발) |
| ``Skill3`` | 11 | ``skill3Right/Left`` | **천벌**(80027 · 뇌호격) ★ 이번에 신설한 칸 |
| ``Skill3Fx`` | 3 | ``skill3Fx`` | 같은 줄 뒤쪽 3칸(뻗어 나가는 빔) |

⚠ 슬롯 번호는 **표의 `skill_01`·`02`·`03` 순서**와 같다 — 원화의 「스킬 N」 번호와도 맞는다.

⚠ 맨 아래 「공통 추가 이펙트(선택 사용)」 상자는 **안 쓴다** — 지금 배선할 칸이 없고,
  «쓸 수도 있는 그림»을 스킨에 넣어두면 다음 사람이 배선된 줄로 착각한다.

사용법:  py -3 Tools/chiron_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        그 다음 `py -3 Tools/measure_skin_tiles.py`
"""

import os

from vault_path import VAULT, PROJECT
from char_sheet import Row, Spec, run

SRC = os.path.join(VAULT, "리소스", "sprites", "Chiron_asset.png")
DST = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Chiron", "Char")

SPEC = Spec(
    title="카이론",
    sources={"01": SRC},
    dst_root=DST,
    skin_spec={
        "skinAssetName": "Skin_Chiron",
        "displayName": "카이론",
        "framesPerSecond": "10",
        "attackFramesPerSecond": "14",
        # 날아가는 기공탄 원화가 실재한다.
        "groundImpactOnly": "0",
        "projectileWidthTiles": "0.9",
        "impactWidthTiles": "1.4",
    },
    rows=[
        # ── 왼쪽 단 ─────────────────────────────────────────────────
        Row("Idle",         "body",  45, 128,   0,  690, ("labels", 140, 148), 8),
        Row("WalkRight",    "body", 192, 252,   0,  690, ("labels", 261, 270), 7,
            folder="Walk", side="Right"),
        Row("WalkLeft",     "body", 279, 339,   0,  690, ("labels", 346, 355), 7,
            folder="Walk", side="Left"),
        # ★ 가운데 칸이 이펙트다 (맨 위 ★★)
        Row("MeleeAttack",  "body", 393, 467,   0,  695, ("labels", 477, 485), 10,
            keep=[0, 1, 2, 3, 4, 6, 7, 8]),
        Row("Skill1",       "body", 532, 614,   0,  584, ("labels", 625, 633), 7),
        Row("Skill3",       "body", 679, 758,   0, 1250, ("labels", 770, 779), 14, take=11),
        Row("Skill3Fx",     "fx",   679, 758,   0, 1250, ("labels", 770, 779), 14, skip=11),

        # ── 오른쪽 단 ───────────────────────────────────────────────
        Row("RangedAttack", "body",  52, 128, 712, 1518, ("labels", 141, 149), 11, take=7),
        Row("MagicAttack",  "body", 207, 284, 717, 1512, ("labels", 295, 304), 11, take=6),
        Row("Heal",         "body", 361, 448, 704, 1519, ("labels", 459, 467), 12,
            keep=[0, 1, 2, 3, 4, 5, 6, 7, 10]),
        Row("Skill2",       "body", 537, 614, 629, 1518, ("labels", 625, 634), 15, take=8),

        # ── 맨 아래 분리 이펙트 줄 (한 밴드에 네 묶음) ──────────────
        Row("Projectile",   "fx",   827, 905,   0,  350, ("labels", 905, 920), 7),
        Row("ImpactMagic",  "fx",   827, 905, 360,  740, ("labels", 905, 920), 7),
        Row("HealFx",       "fx",   827, 905, 750, 1135, ("labels", 905, 920), 6),
        Row("Skill2Fx",     "fx",   827, 905, 1150, 1535, ("labels", 905, 920), 4),
    ],
    no_direction=("Projectile", "ImpactMagic", "HealFx", "Skill2Fx", "Skill3Fx"),
    # ⚠ 크기 정규화 기준에서 빼는 줄 — 아래 ROWS 의 scale=False 대신 여기서 한 번에.
    scale_reference="Idle",
    # ★★ <b>크기 정규화를 하지 않는다</b> (유저 지시: *"모션 생성중에 캐릭터의 크기가 바뀌지 않도록 주의"*).
    #
    #   이 시트는 맨 아래에 <b>«캐릭터 크기 기준: 약 64x64px»</b> 이라고 적혀 있고,
    #   실제로 줄마다 같은 크기로 그려져 있다. 이런 시트에 정규화를 걸면
    #   <b>오히려 망가지는 것</b>이 생긴다 — 실측:
    #
    #       머리 면적 기준   대기 425 · 이동 332 · 근거리 277 · 회복 347
    #       → 근거리를 <b>x1.24</b> 로 키우게 된다. 그런데 근거리 원화는 대기와 <b>같은 크기</b>다 —
    #         주먹을 얼굴 옆으로 몰아 머리 덩어리가 가려졌을 뿐이다.
    #
    #   시그리드·시카리아·아루 시트는 <b>줄마다 진짜로 크기가 달랐기 때문에</b> 정규화가
    #   필요했다. 여기서는 반대로 <b>안 건드리는 것</b>이 «크기가 안 바뀜다» 를 보장한다.
    scale_metric=None,
)

# ★ 스킬 자세는 팔을 크게 뻗거나 기를 모아 <b>머리 판정이 흔들린다</b> — 배율 기준에서 뺀다
#   (아루에서 세운 규칙과 같다). 크기 자체는 대기 기준으로 그대로 유지된다.
for _r in SPEC.rows:
    if _r.name in ("Skill1", "Skill2", "Skill3"):
        _r.scale = False


if __name__ == "__main__":
    run(SPEC)
