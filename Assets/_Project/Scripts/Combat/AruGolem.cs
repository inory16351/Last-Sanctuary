using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 아루의 패시브 <b>「강림」(Dawn · 80024)</b> 이 부르는 골렘.
    ///
    /// 정의문(<c>skill_type_desc_Dawn</c>)이 이 유닛을 <b>거의 전부</b> 규정한다 —
    /// 아래 구현은 그 문장을 한 줄씩 옮긴 것이다:
    ///
    /// <list type="table">
    /// <item><term>능력치</term><description>아루가 <b>지금 쓰는 공격 능력치</b>의 value01%
    ///   를 <b>모든 능력치</b>로 쓴다 (근거리/원거리/마법/회복 중 하나)</description></item>
    /// <item><term>공격 유형·포지션</term><description><b>근접 / 전방 고정</b></description></item>
    /// <item><term>표적</term><description>아루가 공격 중인 대상을 <b>우선</b> 공격.
    ///   아루가 회복 유형이면 «가장 가까운 적»(= 평소 판단)</description></item>
    /// <item><term>부대</term><description>아루가 부대에 있으면 <b>같이</b> 배정된다</description></item>
    /// <item><term>로스터</term><description>표기된다 — <c>UnitRegistry</c> 를 훑으므로 저절로</description></item>
    /// <item><term>침식</term><description><b>일어나지 않는다</b></description></item>
    /// <item><term>전술</term><description><b>수정할 수 없다</b></description></item>
    /// <item><term>후퇴</term><description><b>하지 않는다</b></description></item>
    /// <item><term>크기</term><description>가로 value02 · 세로 value03 타일</description></item>
    /// <item><term>쿨타임</term><description><b>골렘이 죽은 시점부터</b> 돈다 —
    ///   그 계산은 <see cref="CharacterPassives"/> 쪽에 있다(주인이 세는 값이므로)</description></item>
    /// </list>
    ///
    /// ★★ <b>왜 «캐릭터» 로 만드는가</b> — 몬스터가 아니라 아군이고, 이동·전투·경로·안개·
    /// 로스터·부대가 전부 캐릭터 쪽 코드에 있다. 새 유닛 종류를 만들면 그 여덟 가지를
    /// 처음부터 다시 이어야 한다. 대신 «캐릭터로 세면 안 되는» 한 곳(전멸 판정)만
    /// <see cref="CharacterUnit.IsSummoned"/> 로 갈랐다.
    ///
    /// ★ <b>정의 에셋을 런타임에 만든다</b>(<see cref="ScriptableObject.CreateInstance"/>) —
    /// 골렘은 캐릭터 테이블에 <b>행이 없다</b>(플레이어가 뽑는 인물이 아니다). 능력치도
    /// 소환 시점의 아루에 따라 달라지므로 미리 구운 에셋으로는 표현할 수 없다.
    /// </summary>
    public static class AruGolem
    {
        /// <summary>스킨 에셋 이름. <c>Tools/aru_golem_skin_build.py</c> 가 쓰는 이름과 같아야 한다.</summary>
        const string SkinName = "Skin_AruGolem";

        /// <summary>이름의 스트링 키. 표에 행이 없으므로 «UI 문구» 처럼 사람이 지은 키다(51-1절 규칙).</summary>
        const string NameKey = "unit_name_aru_golem";

        /// <summary>스트링 테이블을 못 읽을 때의 폴백 리터럴.</summary>
        const string NameFallback = "강림한 골렘";

        /// <summary>아루가 어느 방향에, 몇 타일 떨어져 나타나는가. 순수 연출값이라 표에 칸이 없다.</summary>
        const float SpawnOffsetTiles = 1.6f;

        /// <summary>
        /// 골렘 한 기를 소환한다. 실패하면 <b>null</b> — 부르는 쪽이 쿨타임을 태우지 않는다.
        /// </summary>
        public static CharacterUnit Summon(CharacterUnit owner, PassiveSkillSO so)
        {
            if (owner == null || so == null) return null;

            var spawner = Object.FindFirstObjectByType<UnitSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[강림] UnitSpawner 를 찾지 못해 골렘을 소환하지 못했습니다.", owner);
                return null;
            }

            CharacterDefinitionSO def = BuildDefinition(owner, so);

            // ⚠ <b>localScale 로 방향을 보면 안 된다</b> — 이 프로젝트는 좌우를 스프라이트
            //   뒤집기로 표현하고 스케일은 <b>언제나 양수</b>다(CharacterAnimator.ApplyRenderSize
            //   가 균등 배율만 건다). 방향은 애니메이터가 따로 들고 있다.
            var ownerAnim = owner.GetComponent<CharacterAnimator>();
            float side = ownerAnim != null && !ownerAnim.FacingRight ? -1f : 1f;
            Vector3 pos = owner.transform.position + new Vector3(side * SpawnOffsetTiles, 0f, 0f);

            CharacterUnit golem = spawner.SpawnSummon(def, pos);
            if (golem == null) return null;

            golem.gameObject.name = def.DisplayName;
            ApplyRules(owner, golem, so);
            return golem;
        }

        /// <summary>
        /// 소환 시점의 아루를 <b>그대로 옮긴</b> 정의를 만든다.
        ///
        /// ★ 「아루의 선택 된 공격 유형이 사용하는 능력치」 — 그 한 값을 골라
        ///   <b>모든 칸에 같이 넣는다</b>. <see cref="CharacterUnit.AttackStatType"/> 이
        ///   이미 «지금 유형이 쓰는 능력치» 를 알고 있으므로 그대로 쓴다(규칙을 두 벌로
        ///   적으면 유형이 늘 때 어긋난다).
        ///
        /// ⚠ <b>강화 보정을 포함한 값</b>(<see cref="CharacterUnit.EffectiveStat"/>)을 쓴다 —
        ///   정의문이 «아루가 사용하는 능력치» 라고 했으므로, 강화로 오른 아루의 힘이
        ///   골렘에 반영되는 편이 그 문장에 맞다.
        /// </summary>
        static CharacterDefinitionSO BuildDefinition(CharacterUnit owner, PassiveSkillSO so)
        {
            int baseStat = owner.EffectiveStat(owner.AttackStatType);
            int value = Mathf.Max(1, Mathf.RoundToInt(baseStat * so.value01 * 0.01f));

            var def = ScriptableObject.CreateInstance<CharacterDefinitionSO>();
            def.name = "Character_Summon_AruGolem";
            // ⚠ id 0 은 <see cref="CharacterDefinitionSO.IsUsable"/> 가 «못 쓰는 정의» 로 본다.
            //   표의 캐릭터 id 대역(9000번대)과 겹치지 않는 번호를 쓴다.
            def.characterId = 90081;
            def.nameKey = NameKey;
            def.characterName = NameFallback;
            def.characterNameEn = "AruGolem";
            def.skinAssetName = SkinName;
            def.attackPreset = RoleAttackPreset.Melee;      // 정의문 "근접 / 전방으로 고정"
            def.positionPreset = RolePositionPreset.Front;

            def.stats = new StatBlock
            {
                hp = value, attack = value, defense = value, regen = value,
                rangedAttack = value, magic = value, cure = value,
                accuracy = value, critical = value,
                attackSpeed = value, moveSpeed = value,
                // ⚠ 저항력만 예외다 — 침식이 <b>아예 없는</b> 유닛이라 이 값이 쓰일 곳이 없다.
                //   0 으로 두면 로스터의 저항 칸이 «0» 으로 뜨므로 기준값 50 을 넣는다.
                resistance = 50,
            };
            return def;
        }

        /// <summary>
        /// 정의문이 규정한 «캐릭터와 다른 점» 을 건다. 순서가 있다 —
        /// 전술을 잠그기 <b>전에</b> 값을 넣어야 잠금이 그 값을 지킨다.
        /// </summary>
        static void ApplyRules(CharacterUnit owner, CharacterUnit golem, PassiveSkillSO so)
        {
            // ── 크기 : 가로 value02 · 세로 value03 타일 ──
            var anim = golem.GetComponent<CharacterAnimator>();
            if (anim != null && so.value02 > 0f && so.value03 > 0f)
                anim.SetColliderBoxTiles(so.value02, so.value03);

            // ── 침식이 일어나지 않는다 : 컴포넌트를 아예 끈다 ──
            //    (템플릿에 붙어 있으므로 «안 붙이기» 가 아니라 «끄기» 가 맞다.
            //     지우면 ErosionService 가 매 프레임 null 을 만난다.)
            var erosion = golem.GetComponent<CharacterErosion>();
            if (erosion != null) erosion.enabled = false;

            var tactics = golem.GetComponent<CharacterTactics>();
            if (tactics != null)
            {
                // ── 후퇴하지 않는다 : 기준을 0% 로 두고 <b>잠근다</b> ──
                //    「가학증」이 쓰는 잠금과 같은 통로다(값만 다르다) — 잠긴 칸은 UI 로도
                //    코드로도 못 바꾼다(CharacterTactics.SetRetreatHpPercent 의 첫 줄).
                tactics.SetRetreatHpPercent(0);
                tactics.SetRetreatHpLock(0);

                // ── 전술을 수정할 수 없다 : 역할 잠금(「선봉장」과 같은 통로) ──
                tactics.SetRoleLock(true);
            }

            // ── 부대 : 아루가 부대에 있으면 같이 들어간다 ──
            var squads = SquadService.Instance;
            if (squads != null)
            {
                int squadId = squads.SquadIdOf(owner);
                if (squadId > 0) squads.Assign(golem, squadId);
            }

            // ── 소환 모션 : 도는 동안은 싸우지 않는다 ──
            //    원화가 없으면 0 이 돌아오고, 그러면 바로 싸운다(연출이 없을 뿐이다).
            float clip = anim != null ? anim.PlaySummonMotion() : 0f;
            if (clip > 0f)
            {
                var suppress = golem.gameObject.AddComponent<SummonDelay>();
                suppress.Begin(clip);
            }
        }

        /// <summary>
        /// 매 프레임 주인이 부른다 — <b>아루가 때리는 적을 골렘도 때린다</b>(정의문).
        ///
        /// ⚠ 아루가 <b>회복 유형</b>이면 아무것도 하지 않는다: 정의문이
        ///   <i>"아루의 공격 유형이 회복이라면 가장 가까운 적을 공격합니다"</i> 라고 했고,
        ///   «가장 가까운 적» 은 골렘의 <b>평소 판단</b> 그대로다(<c>TacticalTargetPriority.Nearest</c>).
        ///   즉 «아무것도 안 하는 것» 이 그 문장의 구현이다.
        ///
        /// ★ <see cref="UnitCombat.SetHuntTarget"/> 을 쓴다 — «지정한 상대를 끝까지 쫓는»
        ///   칸이고, 매 프레임 같은 값을 다시 넣어도 안전하게 만들어져 있다(그 함수 주석).
        /// </summary>
        public static void Follow(CharacterUnit owner, CharacterUnit golem)
        {
            if (owner == null || golem == null || !golem.IsAlive) return;

            var ownerCombat = owner.GetComponent<UnitCombat>();
            var golemCombat = golem.GetComponent<UnitCombat>();
            if (ownerCombat == null || golemCombat == null) return;

            if (ownerCombat.AttackType == TacticalAttackType.Heal) return;

            DamageableUnit target = ownerCombat.Target;
            if (target == null || !target.IsAlive || target.Faction == owner.Faction)
            {
                golemCombat.ClearHuntTarget();
                return;
            }
            golemCombat.SetHuntTarget(target);
        }

        /// <summary>주인이 사라질 때 골렘도 치운다(<see cref="CharacterPassives"/> 가 부른다).</summary>
        public static void Dismiss(CharacterUnit golem)
        {
            if (golem == null) return;

            var squads = SquadService.Instance;
            if (squads != null) squads.Unassign(golem);

            var anim = golem.GetComponent<CharacterAnimator>();
            float clip = anim != null ? anim.PlayDeathMotion() : 0f;
            Object.Destroy(golem.gameObject, Mathf.Max(0.05f, clip));
        }
    }
}
