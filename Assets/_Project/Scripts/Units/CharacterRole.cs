using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 캐릭터 정의에서 <b>공격 유형을 손으로 지정</b>할 때 쓰는 값.
    /// <see cref="RoleAttackPreset.Auto"/> 면 능력치를 보고 <see cref="CharacterRole"/> 이 고른다.
    /// </summary>
    public enum RoleAttackPreset { Auto = 0, Melee = 1, Ranged = 2, Magic = 3, Heal = 4 }

    /// <summary>
    /// 캐릭터 정의에서 <b>전열 위치를 손으로 지정</b>할 때 쓰는 값.
    /// <see cref="RolePositionPreset.Auto"/> 면 능력치와 공격 유형을 보고 고른다.
    /// </summary>
    public enum RolePositionPreset { Auto = 0, Front = 1, Mid = 2, Back = 3 }

    /// <summary>
    /// <b>캐릭터가 태어날 때 자기에게 맞는 역할을 갖고 나오게 한다</b>
    /// (유저 지시 2026-08-14: "지금 캐릭터 생성될 때 공격유형이나 위치가 걍 근거리에 전방으로
    /// 고정인데 각 캐릭터에게 최적의 역할인 상태로 생성되게 로직 변경").
    ///
    /// <b>왜 인물 이름으로 하드코딩하지 않았나</b> — 캐릭터는 계속 추가될 예정이고(33-6절),
    /// 능력치는 데이터 테이블에서 재밸런싱된다(54-2절). 이름으로 박아두면 캐릭터를 하나 넣을
    /// 때마다 코드를 고쳐야 하고, 표가 바뀌어도 역할이 안 따라간다. 그래서 <b>능력치에서
    /// 역산</b>하는 것을 기본으로 두고, 그 결과가 마음에 안 드는 인물만
    /// <see cref="CharacterDefinitionSO.attackPreset"/> · <see cref="CharacterDefinitionSO.positionPreset"/>
    /// 로 덮어쓸 수 있게 했다(둘 다 기본값이 <c>Auto</c> 라 기존 에셋은 손댈 필요가 없다).
    ///
    /// <b>규칙</b>
    /// <code>
    ///   공격 유형 = 네 공격 계열 능력치 중 가장 높은 것
    ///               근거리 공격력 / 원거리 공격력 / 마법 / 회복력
    ///               (동률이면 근거리 → 원거리 → 마법 → 회복 순)
    ///   전열 위치 = 근거리이고 튼튼하면(체력+방어력 ≥ frontBulkThreshold) 전방
    ///               근거리인데 무르면                                     중위
    ///               치유                                                  중위
    ///               원거리 · 마법                                         후방
    /// </code>
    /// 이 규칙을 지금 캐릭터 4명에 대입한 결과 (능력치는 2026-08-13 기준):
    /// <code>
    ///   비기오르 근접7  체12 방11 → 근거리 · 전방   (탱커. 유저 예시와 일치)
    ///   프레이야 근접11 체6  방4  → 근거리 · 중위   (무른 근접 딜러)
    ///   엘린     마법11        → 마법   · 후방
    ///   피올로   회복11        → 치유   · 중위
    /// </code>
    /// ⚠ 유저 예시는 "엘린 → 치유 / 중위" 였지만, 엘린의 <b>가장 높은 공격 계열 능력치는
    /// 마법 11</b>(회복력은 9)이고 회복력 11 로 로스터 1위인 인물은 피올로다. 유저가
    /// "더 적절한 역할이 있다고 생각하면 변경 가능"이라 명시했으므로 능력치를 따랐다 —
    /// 이 판단을 되돌리려면 <c>Character_9001_Elin</c> 에셋의 <c>attackPreset</c> 을
    /// <c>Heal</c>, <c>positionPreset</c> 을 <c>Mid</c> 로 두면 된다(코드 수정 불필요).
    /// </summary>
    public static class CharacterRole
    {
        /// <summary>
        /// 근거리 캐릭터가 전방에 설 기준 — 체력 + 방어력의 합.
        ///
        /// 지금 로스터에서 비기오르(12+11=23)만 넘고 프레이야(6+4=10)는 못 넘는다.
        /// 캐릭터 초기 능력치 예산이 60 안팎이므로(54-2절) 두 능력치에 그 1/4 을 쓴 인물이
        /// "탱커로 세울 만하다"는 뜻이다.
        /// </summary>
        public const int FrontBulkThreshold = 15;

        /// <summary>
        /// 능력치에서 공격 유형을 역산한다. 네 공격 계열 중 가장 높은 것.
        ///
        /// 이 값이 곧 <see cref="CharacterUnit.AttackStat"/> 이 실제로 쓰는 능력치이므로
        /// (33-2절 — 공격 능력치는 전술 공격 유형에 따라 달라진다), 가장 높은 것을 고르는 것이
        /// 곧 "이 인물이 가장 세게 때리는 방식"이다.
        /// </summary>
        public static TacticalAttackType ResolveAttackType(StatBlock stats)
        {
            TacticalAttackType best = TacticalAttackType.Melee;
            int bestValue = stats[StatType.Attack];

            if (stats[StatType.RangedAttack] > bestValue)
            {
                best = TacticalAttackType.Ranged;
                bestValue = stats[StatType.RangedAttack];
            }
            if (stats[StatType.Magic] > bestValue)
            {
                best = TacticalAttackType.Magic;
                bestValue = stats[StatType.Magic];
            }
            if (stats[StatType.Cure] > bestValue)
                best = TacticalAttackType.Heal;

            return best;
        }

        /// <summary>
        /// ★ <b>한 유형을 빼고</b> 역산한다 (2026-08-20 — 아르세니아 「불안정성」 80028).
        ///
        /// 그 스킬이 <b>근거리 선택 자체를 막기</b> 때문에, 「가장 높은 공격 계열」을 그대로
        /// 쓰면 근거리가 최고인 순간 갈 곳이 없어진다. 제외한 셋 중에서 다시 고른다 —
        /// 규칙은 <see cref="ResolveAttackType"/> 과 같다(가장 높은 것).
        /// </summary>
        public static TacticalAttackType ResolveAttackExcluding(StatBlock stats,
                                                               TacticalAttackType banned)
        {
            TacticalAttackType best = TacticalAttackType.Ranged;
            int bestValue = int.MinValue;

            void Consider(TacticalAttackType type, StatType stat)
            {
                if (type == banned) return;
                int v = stats[stat];
                if (v <= bestValue) return;
                best = type;
                bestValue = v;
            }

            Consider(TacticalAttackType.Melee, StatType.Attack);
            Consider(TacticalAttackType.Ranged, StatType.RangedAttack);
            Consider(TacticalAttackType.Magic, StatType.Magic);
            Consider(TacticalAttackType.Heal, StatType.Cure);
            return best;
        }

        /// <summary>
        /// 공격 유형과 맷집으로 전열 위치를 정한다.
        ///
        /// <b>치유가 중위인 이유</b> — 치유 유형의 이동은 <c>CharacterBehavior.TryPickHealSupportSpot</c>
        /// 이 따로 맡아 "지원할 동료의 넥서스 쪽 뒤"에 선다(73-2절). 그래서 지침상 위치는
        /// 그 판단이 안 걸릴 때(지원할 동료가 없을 때)의 기본 자리이고, 앞뒤 어느 쪽으로도
        /// 붙을 수 있는 중위가 맞다.
        /// </summary>
        public static TacticalPosition ResolvePosition(StatBlock stats, TacticalAttackType attackType)
        {
            switch (attackType)
            {
                case TacticalAttackType.Ranged:
                case TacticalAttackType.Magic:
                    return TacticalPosition.Back;

                case TacticalAttackType.Heal:
                    return TacticalPosition.Mid;

                default:
                    return stats[StatType.Hp] + stats[StatType.Defense] >= FrontBulkThreshold
                        ? TacticalPosition.Front
                        : TacticalPosition.Mid;
            }
        }

        /// <summary>정의의 지정값이 있으면 그것을, <c>Auto</c> 면 능력치에서 역산한 값을 돌려준다.</summary>
        public static TacticalAttackType Resolve(RoleAttackPreset preset, StatBlock stats) => preset switch
        {
            RoleAttackPreset.Melee  => TacticalAttackType.Melee,
            RoleAttackPreset.Ranged => TacticalAttackType.Ranged,
            RoleAttackPreset.Magic  => TacticalAttackType.Magic,
            RoleAttackPreset.Heal   => TacticalAttackType.Heal,
            _                       => ResolveAttackType(stats),
        };

        /// <summary>정의의 지정값이 있으면 그것을, <c>Auto</c> 면 능력치·공격 유형에서 역산한 값을 돌려준다.</summary>
        public static TacticalPosition Resolve(RolePositionPreset preset, StatBlock stats,
                                               TacticalAttackType attackType) => preset switch
        {
            RolePositionPreset.Front => TacticalPosition.Front,
            RolePositionPreset.Mid   => TacticalPosition.Mid,
            RolePositionPreset.Back  => TacticalPosition.Back,
            _                        => ResolvePosition(stats, attackType),
        };

        /// <summary>
        /// 캐릭터 하나에게 역할을 실제로 적용한다 — 전술 지침의 <b>공격 유형</b>과 <b>전열 위치</b>.
        ///
        /// <b>왜 CharacterTactics 를 통해 넣나</b> — 지침의 정본이 그쪽이기 때문이다.
        /// <c>UnitCombat</c>/<c>CharacterBehavior</c> 에 직접 밀어 넣으면 다음
        /// <c>CharacterTactics.Apply()</c>(Start 에서 한 번 돈다)가 템플릿 값으로 되돌린다.
        ///
        /// ⚠ <b>성장 유형(<see cref="StatGrowthFocus"/>)은 건드리지 않는다</b> — 그건 플레이어가
        /// 캐릭터 성장 창에서 고르는 값이고, 미선택 상태여야 그 창의 버튼이
        /// "성장 유형 결정"으로 뜬다(유저 지시 2026-08-14).
        /// </summary>
        public static void Apply(CharacterUnit unit, CharacterDefinitionSO definition)
        {
            if (unit == null) return;

            var tactics = unit.GetComponent<CharacterTactics>();
            if (tactics == null) return;

            StatBlock stats = unit.Stats;
            RoleAttackPreset attackPreset =
                definition != null ? definition.attackPreset : RoleAttackPreset.Auto;
            RolePositionPreset positionPreset =
                definition != null ? definition.positionPreset : RolePositionPreset.Auto;

            TacticalAttackType attackType = Resolve(attackPreset, stats);
            TacticalPosition position = Resolve(positionPreset, stats, attackType);

            tactics.SetAttackType(attackType);
            tactics.SetPosition(position);

            Debug.Log($"[Role] {unit.DisplayName} → {TacticalOrder.Label(attackType)} · " +
                      $"{TacticalOrder.Label(position)}", unit);
        }
    }
}
