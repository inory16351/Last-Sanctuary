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

        /// <summary>
        /// ★ 초상화 (2026-08-20 · 유저 지시 *"아루 골렘 일러스트 연동(Aru_dawn_illust)"*).
        ///
        /// <c>Resources/Illust/</c> 아래의 파일 이름이다(확장자 없이) —
        /// <see cref="CharacterDefinitionSO.Illust"/> 가 그 규칙으로 읽는다.
        /// 볼트 원본은 <c>리소스/illust/char/illust_AruDawn.png</c> 이고
        /// <c>Tools/crop_illust_faces.py</c> 가 얼굴 크롭을 만든다.
        ///
        /// ⚠ 골렘은 <b>표에 행이 없다</b>. 다른 캐릭터의 <c>illustName</c> 은
        ///   `gen_character_assets.py` 가 캐릭터 테이블의 `illust` 칸에서 채우지만
        ///   골렘의 정의는 여기서 손으로 만든다 — 그래서 이 이름도 여기 적는다.
        ///   ★ 이름을 바꿀 일이 생기면 <b>크롭 표(FACES)와 이 상수 두 곳</b>이다.
        /// </summary>
        const string IllustName = "illust_AruDawn";

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
            int value = StatValueFor(owner, so);

            var def = ScriptableObject.CreateInstance<CharacterDefinitionSO>();
            def.name = "Character_Summon_AruGolem";
            // ⚠ id 0 은 <see cref="CharacterDefinitionSO.IsUsable"/> 가 «못 쓰는 정의» 로 본다.
            //   표의 캐릭터 id 대역(9000번대)과 겹치지 않는 번호를 쓴다.
            def.characterId = 90081;
            def.nameKey = NameKey;
            def.characterName = NameFallback;
            def.characterNameEn = "AruGolem";
            def.skinAssetName = SkinName;
            def.illustName = IllustName;        // ★ 초상화 — 위 상수의 주석 참조
            def.attackPreset = RoleAttackPreset.Melee;      // 정의문 "근접 / 전방으로 고정"
            def.positionPreset = RolePositionPreset.Front;

            def.stats = StatsOf(value);
            return def;
        }

        /// <summary>
        /// 「강림」의 능력치 값 — <b>아루가 지금 쓰는 공격 능력치의 value01%</b>.
        /// <see cref="BuildDefinition"/> 과 <see cref="SyncStats"/> 가 <b>같은 한 줄</b>을 쓰게
        /// 따로 뺐다(규칙이 두 벌이면 소환 직후와 성장 뒤의 값이 어긋난다).
        /// </summary>
        static int StatValueFor(CharacterUnit owner, PassiveSkillSO so)
        {
            int baseStat = owner.EffectiveStat(owner.AttackStatType);
            return Mathf.Max(1, Mathf.RoundToInt(baseStat * so.value01 * 0.01f));
        }

        /// <summary>한 값을 <b>모든 칸에</b> 넣은 능력치 묶음(정의문: «모든 능력치»).</summary>
        static StatBlock StatsOf(int value) => new StatBlock
        {
            hp = value, attack = value, defense = value, regen = value,
            rangedAttack = value, magic = value, cure = value,
            accuracy = value, critical = value,
            attackSpeed = value, moveSpeed = value,
            // ⚠ 저항력만 예외다 — 침식이 <b>아예 없는</b> 유닛이라 이 값이 쓰일 곳이 없다.
            //   0 으로 두면 로스터의 저항 칸이 «0» 으로 뜨므로 기준값 50 을 넣는다.
            resistance = 50,
        };

        /// <summary>
        /// ★★ <b>아루가 성장하면 골렘도 그 자리에서 같이 성장한다</b> (2026-08-21 · 유저 지시:
        /// *"아루가 성장할 때 마다 골렘도 실시간으로 성장(지금은 골렘이 죽고 아루가 성장한
        /// 상태에서 다시 골렘이 리스폰되면 골렘도 성장한 채로 나옴)"*).
        ///
        /// <b>무엇이 어긋났나</b> — 골렘의 능력치는 <see cref="Summon"/> 시점에 <b>한 번</b>
        /// 계산해 정의 에셋에 구워 넣는 값이었다. 그래서 아루를 강화해도 <b>이미 서 있는
        /// 골렘은 그대로</b>였고, 죽고 다시 소환될 때만 새 값으로 나왔다.
        ///
        /// → 매 프레임 <b>같은 식</b>(<see cref="StatValueFor"/>)을 다시 계산해서 값이 달라졌을
        ///   때만 덮어쓴다. 강화뿐 아니라 <b>능력치 보정·패시브·정신 이상</b>으로 아루의 값이
        ///   변할 때도 저절로 따라간다 — «아루가 사용하는 능력치의 value01%» 라는 정의문을
        ///   시점이 아니라 <b>상태</b>로 읽은 것이다.
        ///
        /// ★ <see cref="CharacterUnit.ApplyStats"/> 를 쓴다 — <b>체력 비율을 유지</b>하는
        ///   경로다(성장 시스템이 쓰는 것과 같은 함수). 반쯤 다친 골렘이 아루의 강화로
        ///   갑자기 만피가 되지 않는다.
        /// ⚠ 값이 같으면 <b>아무것도 하지 않는다</b> — 매 프레임 <c>ApplyStats</c> 를 부르면
        ///   반올림 때문에 체력이 1 씩 흔들린다.
        /// </summary>
        public static void SyncStats(CharacterUnit owner, CharacterUnit golem, PassiveSkillSO so)
        {
            if (owner == null || golem == null || so == null || !golem.IsAlive) return;

            int value = StatValueFor(owner, so);
            if (golem.Stats.attack == value) return;      // 이미 그 값이다

            golem.ApplyStats(StatsOf(value));
        }

        /// <summary>
        /// 정의문이 규정한 «캐릭터와 다른 점» 을 건다. 순서가 있다 —
        /// 전술을 잠그기 <b>전에</b> 값을 넣어야 잠금이 그 값을 지킨다.
        /// </summary>
        static void ApplyRules(CharacterUnit owner, CharacterUnit golem, PassiveSkillSO so)
        {
            // ── 크기 : 가로 value02 · 세로 value03 타일 (아래 <see cref="FillBoxHeight"/>) ──
            var anim = golem.GetComponent<CharacterAnimator>();
            if (anim != null && so.value02 > 0f && so.value03 > 0f)
            {
                float height = FillBoxHeight(anim, so.value02, so.value03);
                if (height > 0f)
                {
                    // ⚠ <b>순서가 있다</b> — 콜라이더 상자가 남아 있으면
                    //   <see cref="CharacterAnimator.ResolveScale"/> 가 그쪽(contain)을 먼저 본다.
                    anim.SetColliderBoxTiles(0f, 0f);
                    anim.SetRenderHeightTiles(height);
                }
            }

            // ── 침식이 일어나지 않는다 : 컴포넌트를 아예 끈다 ──
            //    (템플릿에 붙어 있으므로 «안 붙이기» 가 아니라 «끄기» 가 맞다.
            //     지우면 ErosionService 가 매 프레임 null 을 만난다.)
            var erosion = golem.GetComponent<CharacterErosion>();
            if (erosion != null) erosion.enabled = false;

            var tactics = golem.GetComponent<CharacterTactics>();
            if (tactics != null)
            {
                // ── 배회 범위 : 주인의 값을 물려받는다 (2026-08-21) ──
                //
                // ★ 골렘의 지침은 <b>템플릿 기본값</b>으로 시작한다 — 배회 범위도 «근방» 이다.
                //   그런데 골렘은 <b>아루 옆</b>에 나타나므로, 아루가 «전역»으로 멀리 나가 있으면
                //   골렘은 <b>태어난 순간부터 자기 배회 한계 밖</b>이다. 그 상태에서는
                //   <c>CharacterBehavior.IsBeyondRoamLimit</c> 이 <b>새 사냥감을 물지 못하게</b>
                //   막으므로(먼저 돌아오라는 규칙), 눈앞의 중립 몬스터를 그냥 지나친다.
                // ⚠ 주인의 값을 <b>잠그기 전에</b> 넣는다 — 잠근 뒤에는 이 통로도 막힌다.
                var ownerTactics = owner.GetComponent<CharacterTactics>();
                if (ownerTactics != null) tactics.SetRoamRange(ownerTactics.Order.roamRange);

                // ── 후퇴하지 않는다 : 기준을 0% 로 두고 <b>잠근다</b> ──
                //    「가학증」이 쓰는 잠금과 같은 통로다(값만 다르다) — 잠긴 칸은 UI 로도
                //    코드로도 못 바꾼다(CharacterTactics.SetRetreatHpPercent 의 첫 줄).
                tactics.SetRetreatHpPercent(0);
                tactics.SetRetreatHpLock(0);

                // ── 전술을 수정할 수 없다 ──
                //    ⚠⚠ 예전에는 <b>역할 잠금만</b> 걸었다(「선봉장」과 같은 통로). 그런데 그것은
                //      <b>공격 유형·포지션 두 칸만</b> 막는다 — 그래서 골렘의 교전 대상·탐험 유형·
                //      웨이브 반응·배회 범위·후퇴 행동은 <b>전술 창에서 그대로 바꿀 수 있었다</b>
                //      (유저 리포트: *"골렘 강화/전술 변경 가능한 버그"*).
                //      정의문은 «전술을 수정할 수도 없습니다» 라고 <b>전부</b>를 말한다.
                //    ★ 둘을 <b>같이</b> 건다 — 역할 잠금은 «전방·근거리로 스냅» 하는 일까지 하고
                //      (`ForceLockedRole`), 전면 잠금은 «앞으로 아무것도 못 바꾼다» 를 한다.
                //      순서도 이대로다: 스냅을 먼저 하고 나서 문을 닫는다.
                tactics.SetRoleLock(true);
                tactics.SetTacticsLock(true);
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
        /// ★★ <b>표의 상자를 «꽉 채우는» 세로 크기(타일)</b> — 골렘이 실제로 그려질 키다.
        ///
        /// <b>무엇이 어긋났나</b> — 표의 골렘 상자는 <b>3(가로) x 2(세로)</b>, 즉 <b>눕힌 상자</b>다.
        /// 그런데 원화는 <b>서 있는 골렘</b>이라 세로가 길다(실측 1.469 x 2.078 · 비율 0.71).
        /// <see cref="CharacterAnimator"/> 의 콜라이더 상자는 «상자 <b>안에</b> 들어가는 최대
        /// 배율»(contain)이라, 선 그림을 눕힌 상자에 넣으면 <b>세로에 걸려</b> 배율이 0.962 가 되고
        /// 골렘이 <b>1.41 x 2.00 타일</b> — 즉 <b>소환한 사람보다 작아진다</b>(아루 1.82 x 2.15).
        ///
        /// ★★ <b>유저 확정 (2026-08-21)</b> — 유저 리포트 *"아루 골렘 크기가 3x2 타일 크기로
        ///   아마 내가 설정했을 텐데 <b>여전히 캐릭터 크기랑 동일함</b>"*. 세 안을 숫자로 놓고
        ///   물었고 유저가 <b>「상자를 꽉 채운다」</b> 를 골랐다:
        ///
        /// <code>
        ///   캐릭터        1.82 x 2.15 타일
        ///   contain       1.41 x 2.00 타일   ← 캐릭터보다 작다 (원래 버그)
        ///   상자 돌리기   2.00 x 2.83 타일   ← 2026-08-20 의 보정. 32% 크지만 «같아 보인다»
        ///   ★ cover      3.00 x 4.24 타일   ← 지금. <b>표의 «가로 3타일» 을 그대로 지킨다</b>
        /// </code>
        ///
        /// 그래서 배율을 <b>두 변 중 더 넉넉한 쪽</b>으로 잡는다(cover):
        /// <c>배율 = max(가로 ÷ 원화가로, 세로 ÷ 원화세로)</c>. 3x2 · 1.469x2.078 이면
        /// <c>max(2.042, 0.962) = 2.042</c> → 3.00 x 4.24.
        ///
        /// ★ <b>왜 콜라이더 상자가 아니라 세로 크기로 돌려주나</b> — 콜라이더 상자 경로는
        ///   구조적으로 contain 이고, 그 규칙은 <b>몬스터 전원</b>이 쓴다(«그림이 판정 밖으로
        ///   삐져나가지 않게»). 골렘 하나를 위해 그 규칙을 흔들 이유가 없다. 캐릭터는 원래
        ///   <c>renderHeightTiles</c>(세로 전용) 경로로 그려지므로, <b>같은 문으로</b>
        ///   더 큰 키를 넣어 주는 것이 이 프로젝트의 결에 맞는다.
        ///   ⚠ 그래도 <b>«보이는 몸집 = 판정 몸집»</b> 은 지켜진다 —
        ///   <see cref="CharacterAnimator.ColliderSizeTiles"/> 가 <b>그려진 크기</b>를 돌려주기 때문이다.
        ///
        /// ⚠ <b>표를 고치지 않는다.</b> 표의 3x2 는 기획이 적는 값이고, 그 문장이 바뀌면
        ///   이 계산이 저절로 따라가야 한다.
        /// ⚠ 원화 실측값이 없으면(스킨 미배선) <b>0</b> 을 돌려준다 — 부르는 쪽이 크기를
        ///   건드리지 않고 템플릿 값(2.15)을 그대로 쓴다. 지어내지 않는다.
        /// </summary>
        static float FillBoxHeight(CharacterAnimator anim, float widthTiles, float heightTiles)
        {
            CharacterSkinSO skin = anim.Skin;
            if (skin == null) return 0f;

            Vector2 art = skin.contentSizeTiles;
            if (art.x <= 0.0001f || art.y <= 0.0001f) return 0f;

            float scale = Mathf.Max(widthTiles / art.x, heightTiles / art.y);
            return scale * art.y;
        }
#pragma warning restore IDE0051

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
                // ★★ 2026-08-21 — <b>주인이 표적을 놓았다고 골렘의 «자기 사냥» 까지 놓으면 안 된다</b>
                //    (유저 리포트: *"아루의 골렘이 비전투 상황일 때 중립몹은 공격 안 하는 문제"*).
                //
                //    <b>무엇이 어긋났나</b> — 이 함수는 <b>매 프레임</b> 불린다. 그래서 아루가
                //    싸우지 않는 동안 «무조건 ClearHuntTarget» 은 골렘이 스스로 문 사냥감을
                //    <b>문 프레임에 바로 다시 뱉게</b> 만들었다: 골렘의 <c>CharacterBehavior</c> 가
                //    중립을 찾아 <c>SetHuntTarget</c> 을 해도 다음 프레임에 여기서 지워지고,
                //    <c>ClearHuntTarget</c> 은 타겟까지 비우므로(그 함수) <b>한 대도 못 때린다.</b>
                //
                //    ★ 그래서 <b>주인에게서 물려받은 표적만</b> 놓는다. 구분은 <b>진영</b>으로 한다 —
                //      골렘이 <b>스스로</b> 무는 사냥감은 언제나 중립이고(<c>TryFindHuntPrey</c> 는
                //      <c>Faction.Neutral</c> 만 고른다), 그 밖의 사냥감은 여기서 넣은 것밖에 없다
                //      (골렘은 침식을 끄므로 「혼란」이 잡는 사냥감도 없다 — <see cref="ApplyRules"/>).
                //    ⚠ 물려받은 것이 <b>중립</b>이었으면 그대로 둔다 — 골렘이 원래 사냥할 상대이고,
                //      죽거나 너무 멀리 도망가면 <c>UnitCombat</c> 이 스스로 놓는다(추격 한계).
                DamageableUnit hunted = golemCombat.HuntTarget;
                if (hunted != null && hunted.Faction != Faction.Neutral) golemCombat.ClearHuntTarget();
                return;
            }
            golemCombat.SetHuntTarget(target);
        }

        /// <summary>
        /// ★★ 주인이 사라질 때 골렘도 <b>죽는다</b>(<see cref="CharacterPassives"/> 가 부른다).
        ///
        /// 유저 지시: *"아루 사망 시 골렘도 같이 사망하게 해"*.
        ///
        /// ⚠⚠ <b>예전에는 «치우기» 였고 «죽이기» 가 아니었다</b> — 죽음 처리를 건너뛰고
        ///   <c>Destroy(오브젝트, 시간)</c> 만 예약했다. 그래서 두 가지가 어긋났다:
        ///
        ///   ① <b>골렘이 그 시간 동안 계속 싸웠다.</b> 체력이 그대로여서
        ///      <c>IsAlive</c> 가 참이고, 표적 탐색·피격 판정이 전부 살아 있었다.
        ///      아루가 죽은 뒤에도 골렘이 몇 초간 적을 때리는 것이 그 정체다.
        ///   ② <b>죽음을 세는 곳이 아무것도 못 들었다.</b> <c>OnDied</c>·<c>OnAnyDied</c> 가
        ///      발생하지 않으니 로스터의 사망 표시·부대 정리·처치 기록이 안 돌았다.
        ///
        /// → <b>실제 피해로 죽인다</b>(<see cref="DamageableUnit.ApplyDamage"/>).
        ///   그러면 죽음 경로 <b>한 벌</b>만 남는다 — 골렘이 적에게 맞아 죽는 것과
        ///   주인을 잃어 죽는 것이 <b>같은 길</b>을 지나고, 사망 모션·정리·이벤트가
        ///   저절로 따라온다(<see cref="CharacterUnit.OnDeath"/> 의 사망 모션 갈림길).
        ///
        /// ★ 부대에서 빼는 것은 <b>여기서 먼저</b> 한다 — 죽음 이벤트를 듣고 부대를
        ///   정리하는 쪽이 있더라도, 골렘은 «주인과 함께 사라지는» 유닛이라
        ///   두 번 빼도 문제가 없고 한 번도 안 빠지는 것보다 낫다.
        /// ⚠ 이미 죽어 있으면(체력 0) <b>아무것도 하지 않는다</b> — 죽음 이벤트가
        ///   두 번 나가면 처치 기록이 두 번 올라간다.
        /// </summary>
        public static void Dismiss(CharacterUnit golem)
        {
            if (golem == null) return;

            var squads = SquadService.Instance;
            if (squads != null) squads.Unassign(golem);

            if (!golem.IsAlive)
            {
                // 죽어 있는데 몸만 남아 있는 경우(사망 모션 재생 중) — 그냥 치운다.
                Object.Destroy(golem.gameObject);
                return;
            }

            golem.ApplyDamage(golem.MaxHp > 0 ? golem.MaxHp : 999999);
        }
    }
}
