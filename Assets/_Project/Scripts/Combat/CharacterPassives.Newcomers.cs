using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 신규 캐릭터 3인(시카리아 9007 · 아루 9008 · 카이론 9009)의 패시브 <b>아홉 종</b>.
    ///
    /// <b>왜 파일을 갈랐나</b> — <see cref="CharacterPassives"/> 본체는 이미 1,000줄이 넘고
    /// 열두 종이 서로 다른 방식으로 엉켜 있다. 아홉 종을 그 안에 이어 붙이면 «어느 스킬이
    /// 어디 있는지»를 찾는 비용이 급격히 오른다. 클래스는 하나(<c>partial</c>)이므로
    /// 필드·헬퍼는 그대로 공유하고, <b>읽는 단위만</b> 나눈다.
    ///
    /// 본체와 이어지는 지점은 <b>네 곳뿐</b>이다:
    /// <list type="bullet">
    /// <item><see cref="ApplyAlwaysOnNewcomers"/> ← <c>ApplyAlwaysOn()</c></item>
    /// <item><see cref="TickNewcomers"/> ← <c>Tick(dt)</c></item>
    /// <item><c>TryArrowRain</c>·<c>TryDawn</c>·<c>TryFallenBody</c>·<c>TryCelestialShield</c>·
    ///       <c>TryDivineWrath</c> ← <c>TickCooldownSkills()</c> 의 우선순위 표</item>
    /// <item><see cref="ClearNewcomerEffects"/> ← <c>OnDisable()</c></item>
    /// </list>
    ///
    /// ⚠ <b>「걸었으면 반드시 되돌린다」</b> 는 본체의 규칙을 그대로 따른다 — 사거리 보정과
    ///   다중 사격은 <b>걸어둔 양을 기억</b>했다가 정확히 같은 값을 뺀다.
    /// </summary>
    public partial class CharacterPassives
    {
        // ==================================================================
        // 상태
        // ==================================================================

        /// <summary>「고조된 감각」이 지금 걸어둔 사거리 보정. 되돌릴 때 이 값을 뺀다.</summary>
        float _appliedSenseRange;

        /// <summary>「한발에 두마리」가 지금 걸어둔 다중 사격 수. 1 이면 안 걸린 것이다.</summary>
        int _appliedMultiShot = 1;

        /// <summary>「도움의 손길」을 다음에 검사할 시각. 쿨타임이 0 이라 여기서 간격을 준다.</summary>
        float _helpingHandNextAt;

        /// <summary>
        /// 「도움의 손길」의 자체 검사 간격(초). <b>표에 칸이 없다</b> — 정의문에 쿨타임이 없고
        /// (<c>cool_time = 0</c>) 「조건이 맞으면 즉시」라고만 적혀 있다. 그렇다고 매 프레임
        /// 돌리면 60번/초로 전 유닛을 훑게 되므로, «즉시» 로 느껴지는 가장 긴 간격을 둔다.
        /// </summary>
        const float HelpingHandInterval = 0.5f;

        /// <summary>「강림」으로 소환한 골렘. 살아 있는 동안은 다시 소환하지 않는다.</summary>
        CharacterUnit _golem;

        /// <summary>골렘이 죽은 뒤 쿨타임이 시작되는 규칙(정의문) 때문에 별도로 센다.</summary>
        float _dawnReadyAt;

        /// <summary>지금 정신집중 중인 스킬. <see cref="PassiveSkillType.None"/> 이면 아니다.</summary>
        PassiveSkillType _channel = PassiveSkillType.None;

        /// <summary>정신집중이 끝나는 시각.</summary>
        float _channelEndAt;

        /// <summary>정신집중을 시작할 때 바라본 방향(단위 벡터). 「천벌」의 직사각형이 이 쪽으로 뻗는다.</summary>
        Vector2 _channelAim = Vector2.right;

        /// <summary>「천상의 방패」의 도발이 끝나 <b>피해가 터지는</b> 시각. 0 이면 예약 없음.</summary>
        float _shieldBurstAt;

        // 쿨타임 — 본체의 _sacrificeReadyAt 들과 같은 방식이다.
        float _arrowRainReadyAt;
        float _fallenBodyReadyAt;
        float _celestialReadyAt;
        float _divineReadyAt;

        /// <summary>범위 판정용 임시 목록. 유닛마다 갖지 않도록 정적으로 공유한다(본체와 같은 규칙).</summary>
        static readonly List<DamageableUnit> _newcomerScratch = new List<DamageableUnit>();

        /// <summary>연출이 화면에 남는 시간(초). 순수 연출값이라 표에 칸이 없다(복수자와 같다).</summary>
        const float FxSeconds = 0.6f;

        // ==================================================================
        // 상시 효과
        // ==================================================================

        /// <summary>
        /// 상시 효과 둘을 건다 — <b>다시 불려도 안전하다</b>(이전 값을 정확히 빼고 새로 건다).
        ///
        /// ⚠ 「고조된 감각」의 값은 <b>공격 유형에 따라 달라진다</b>(원거리면 value02 만큼 더).
        ///   공격 유형은 전술 지침으로 <b>언제든 바뀌므로</b> 이 함수만으로는 부족하다 —
        ///   <see cref="TickNewcomers"/> 가 매 프레임 «원하는 값 ≠ 걸어둔 값» 만 확인해
        ///   달라졌을 때만 다시 건다(비교 한 번이라 비용이 없다).
        /// </summary>
        void ApplyAlwaysOnNewcomers()
        {
            SyncHeightenedSenses();

            // ── 한발에 두마리 : 원거리 평타가 동시에 때리는 적 수 ──
            PassiveSkillSO multi = Find(PassiveSkillType.TwoOnOneLeg);
            int wantShots = multi != null ? Mathf.Max(1, Mathf.RoundToInt(multi.value01)) : 1;
            if (wantShots != _appliedMultiShot)
            {
                _combat?.SetRangedMultiShot(wantShots);
                _appliedMultiShot = wantShots;
            }
        }

        /// <summary>
        /// 「고조된 감각」 — 사거리 +value01, <b>공격 유형이 원거리면</b> +value02 를 더한다.
        ///
        /// 정의문: <i>"시카리아의 사거리는 다른 캐릭터보다 {value_01} 타일 만큼 높습니다.
        /// 시카리아의 공격 유형이 원거리 일 경우 추가로 사거리를 {Value_02}만큼 획득합니다."</i>
        ///
        /// <b>왜 「타고난 섬세함」처럼 한 번만 걸지 않나</b> — 그쪽은 조건이 없어서 한 번 걸면
        /// 끝이지만, 이쪽은 <b>공격 유형</b>이라는 변하는 조건이 붙어 있다. 전술 창에서 유형을
        /// 바꾸면 그 순간 값이 달라져야 한다.
        /// </summary>
        void SyncHeightenedSenses()
        {
            PassiveSkillSO so = Find(PassiveSkillType.HeightenedSenses);
            float want = 0f;
            if (so != null)
            {
                want = so.value01;
                if (_combat != null && _combat.AttackType == TacticalAttackType.Ranged)
                    want += so.value02;
            }

            if (Mathf.Approximately(want, _appliedSenseRange)) return;

            _combat?.AddAttackRangeBonus(want - _appliedSenseRange);
            _appliedSenseRange = want;
        }

        /// <summary>사라질 때 <b>남에게·자기에게 걸어둔 것을 전부 되돌린다</b>(본체 OnDisable 규칙).</summary>
        void ClearNewcomerEffects()
        {
            if (_appliedSenseRange != 0f)
            {
                _combat?.AddAttackRangeBonus(-_appliedSenseRange);
                _appliedSenseRange = 0f;
            }
            if (_appliedMultiShot != 1)
            {
                _combat?.SetRangedMultiShot(1);
                _appliedMultiShot = 1;
            }

            // ★ 골렘은 <b>같이 사라진다</b> — 아루가 없는데 골렘만 남으면 «주인 없는 아군» 이
            //   되고, 쿨타임을 세는 주체도 사라져 다시는 소환되지 않는다.
            if (_golem != null && _golem.IsAlive) AruGolem.Dismiss(_golem);
            _golem = null;
        }

        // ==================================================================
        // 매 프레임
        // ==================================================================

        /// <summary>본체 <c>Tick</c> 의 마지막 갈래. 상시 조건 갱신 + 예약된 일 처리.</summary>
        void TickNewcomers(float dt)
        {
            SyncHeightenedSenses();     // 공격 유형이 바뀌었을 수 있다(위 주석)
            TickHelpingHand();
            TickChannel();
            TickShieldBurst();
            TickGolemLifetime();
        }

        // ------------------------------------------------------------------
        // 시카리아 9007
        // ------------------------------------------------------------------

        /// <summary>
        /// 애로우 레인 — <b>지금 때리고 있는 적</b>을 중심으로 반경 value01 칸 안의 적에게
        /// 원거리 공격력의 value02% 피해. 쿨타임 value(cool_time).
        ///
        /// <b>왜 «현재 대상» 이 필요한가</b> — 정의문이 <i>"현재 대상을 중심으로"</i> 라고
        /// 못박았다. 그래서 <b>싸우고 있지 않으면 발동하지 않는다</b> — 허공에 쏘면
        /// 쿨타임만 날린다.
        ///
        /// ★ 피해는 <see cref="DamageableUnit.TakeDamageFrom"/> 로 넣는다(「복수자」와 같은 이유):
        ///   정의문 기준이 «공격력의 %» 이므로 방어력·명중·치명타를 포함한 정상 파이프라인이 맞다.
        ///   ⚠ 그 함수는 공격자의 <b>현재 공격 유형</b>에 맞는 공격력을 쓴다
        ///   (<see cref="CharacterUnit.AttackStatType"/>). 시카리아는 표에서 원거리 9 가 최고라
        ///   역산도 원거리로 나오므로 «원거리 공격력» 이라는 정의문과 어긋나지 않는다.
        ///   근거리로 바꿔 쓰면 근거리 공격력이 기준이 된다 — 유형을 바꾼 대가로 자연스럽다.
        /// </summary>
        bool TryArrowRain()
        {
            PassiveSkillSO so = Find(PassiveSkillType.ArrowRain);
            if (so == null || Time.time < _arrowRainReadyAt) return false;

            DamageableUnit target = _combat != null ? _combat.Target : null;
            if (target == null || !target.IsAlive) return false;
            if (target.Faction == _unit.Faction) return false;   // 회복 대상은 중심이 될 수 없다

            float radius = Mathf.Max(0f, so.value01);
            int percent = Mathf.RoundToInt(so.value02);
            if (radius <= 0f || percent <= 0) return false;

            _arrowRainReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            Vector3 center = target.transform.position;

            // 시전 모션 — 하늘로 활을 겨누는 12장. 원화가 없으면 조용히 평타로 떨어진다.
            _animator?.PlaySkillMotion(0, so.coolTime > 0f ? 0.6f : 0.4f, center);

            // 화살비가 떨어지는 자리를 <b>피해 범위 그대로</b> 깐다(61-5절 "보이는 범위 = 맞는 범위").
            Sprite[] fx = _animator != null && _animator.Skin != null ? _animator.Skin.SkillFx(0) : null;
            if (fx != null)
                CombatProjectileFx.PlayArea(fx, center, new Vector2(radius * 2f, radius * 2f),
                                            0f, null, FxSeconds);

            int hits = DamageEnemiesInRadius(center, radius, percent);

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              $"반경 {radius:0.#}타일 · {hits}명"), UI.HudLogKind.Good);
            return true;
        }

        // ------------------------------------------------------------------
        // 아루 9008
        // ------------------------------------------------------------------

        /// <summary>
        /// 도움의 손길(+구원) — 반경 value01 안에서 <b>침식이 value02 이상</b>이거나
        /// <b>후퇴 중</b>인 동료를 즉시 아루 곁으로 옮긴다.
        ///
        /// ⚠ 표의 쿨타임이 <b>0</b> 이라 이 스킬은 «쿨타임 스킬» 목록에 넣지 않았다 —
        ///   그 목록은 «한 프레임에 하나만» 규칙을 강제하는데, 여기 걸리면 아루가 다른 스킬을
        ///   쓰는 프레임에 구조가 밀린다. 대신 <see cref="HelpingHandInterval"/> 로 스스로 간격을 둔다.
        ///
        /// ★ 「구원」(80023)이 붙어 있으면 옮겨진 아군은 <b>즉시 체력 재생 가능</b>이 된다.
        ///   정의문이 그 스킬을 «'구원의 손길'로 이송 되어진 아군» 이라고 <b>이 스킬에 매달아</b>
        ///   정의했으므로, 독립된 갈래를 만들지 않고 여기서 같이 처리한다.
        /// </summary>
        void TickHelpingHand()
        {
            PassiveSkillSO so = Find(PassiveSkillType.AHelpingHand);
            if (so == null) return;
            if (Time.time < _helpingHandNextAt) return;
            _helpingHandNextAt = Time.time + HelpingHandInterval;

            float radius = Mathf.Max(0f, so.value01);
            if (radius <= 0f) return;
            float erosionGate = so.value02;

            bool salvation = Has(PassiveSkillType.Salvation);
            Vector3 myPos = transform.position;
            float sqr = radius * radius;
            int moved = 0;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CharacterUnit c) || !c.IsAlive) continue;
                if (ReferenceEquals(c, _unit)) continue;
                if (((Vector2)(c.transform.position - myPos)).sqrMagnitude > sqr) continue;

                // 이미 곁에 있으면 옮길 필요가 없다 — 매 0.5초 제자리로 순간이동시키면
                // 그 동료는 <b>영영 걷지 못한다</b>(경로가 계속 지워진다).
                if (((Vector2)(c.transform.position - myPos)).sqrMagnitude <= ArrivedSqr) continue;

                if (!NeedsHelpingHand(c, erosionGate)) continue;

                c.transform.position = BesideMe(moved);
                moved++;

                // ★ 「구원」 — 옮겨진 순간 재생 대기가 풀린다.
                if (salvation) c.MakeRegenReady();
            }

            if (moved <= 0) return;

            Sprite[] fx = _animator != null && _animator.Skin != null ? _animator.Skin.SkillFx(0) : null;
            if (fx != null)
                CombatProjectileFx.PlayArea(fx, myPos, new Vector2(2f, 2f), 0f, null, FxSeconds);

            _animator?.PlaySkillMotion(0, 0.4f, myPos + Vector3.right);

            PassiveSkillSO salv = Find(PassiveSkillType.Salvation);
            string label = salv != null ? $"{moved}명 이송 · 재생 해제" : $"{moved}명 이송";
            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName, label),
                          UI.HudLogKind.Good);
        }

        /// <summary>이미 «곁» 으로 볼 거리의 제곱(타일²). 순환 순간이동을 막는 값이다.</summary>
        const float ArrivedSqr = 2.25f;    // 1.5타일

        /// <summary>이 동료를 끌어와야 하는가 — 침식이 기준 이상이거나 후퇴 중이거나.</summary>
        bool NeedsHelpingHand(CharacterUnit c, float erosionGate)
        {
            var behavior = c.GetComponent<CharacterBehavior>();
            if (behavior != null && behavior.IsRetreating) return true;

            var erosion = CharacterErosion.Of(c);
            return erosion != null && erosion.Erosion >= erosionGate;
        }

        /// <summary>
        /// 아루 주변에 겹치지 않게 내려놓을 자리. 한 번에 여러 명이면 부채꼴로 흩는다.
        ///
        /// ⚠⚠ <b>벽 안에 내려놓으면 안 된다.</b> 이 프로젝트는 «벽에 낀 유닛» 사고를 여러 번
        ///   겪었고(116절 고르도네 · UnitCombat 의 EmbedEscape), 순간이동은 그 사고를 만드는
        ///   가장 쉬운 길이다 — 부채꼴 자리가 하필 벽이면 그 동료는 거기서 못 나온다.
        ///   그래서 <see cref="Map.MapGenerator.TryFindPlaceableNear"/> 로 <b>가장 가까운 빈 칸</b>에
        ///   맞춰 놓는다(집결지 스냅·경로 목적지 보정이 쓰는 그 함수다).
        ///
        /// 맵을 못 찾으면 계산한 자리를 그대로 쓴다 — 지금까지와 같은 동작이라 더 나빠지지 않는다.
        /// </summary>
        Vector3 BesideMe(int index)
        {
            float angle = (index * 60f + 30f) * Mathf.Deg2Rad;
            Vector3 want = transform.position +
                           new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * BesideDistanceTiles;

            var map = Object.FindFirstObjectByType<Map.MapGenerator>();
            if (map == null) return want;

            return map.TryFindPlaceableNear(map.WorldToCell(want), BesideSearchRadius, null,
                                            out Vector3Int cell)
                ? map.CellCenterWorld(cell)
                : want;
        }

        /// <summary>아루에게서 몇 타일 떨어뜨려 놓는가. 순수 연출값이라 표에 칸이 없다.</summary>
        const float BesideDistanceTiles = 1.2f;

        /// <summary>빈 칸을 찾을 반경(칸). 이보다 멀어지면 «곁으로» 라는 말이 무색해진다.</summary>
        const int BesideSearchRadius = 3;

        /// <summary>
        /// 강림 — 골렘을 소환한다. <b>쿨타임이 골렘의 사망 시점부터</b> 돈다는 것이
        /// 다른 쿨타임 스킬과 유일하게 다른 점이다(정의문).
        ///
        /// 그래서 «쿨타임이 됐는가» 를 두 조건으로 본다:
        /// <list type="number">
        /// <item>지금 골렘이 <b>없어야</b> 한다 (살아 있으면 두 마리가 된다)</item>
        /// <item>골렘이 죽은 시각 + 쿨타임을 지나야 한다 (<see cref="_dawnReadyAt"/>)</item>
        /// </list>
        /// </summary>
        bool TryDawn()
        {
            PassiveSkillSO so = Find(PassiveSkillType.Dawn);
            if (so == null) return false;
            if (_golem != null && _golem.IsAlive) return false;
            if (Time.time < _dawnReadyAt) return false;

            _golem = AruGolem.Summon(_unit, so);
            if (_golem == null)
            {
                // 소환에 실패했으면(템플릿을 못 찾음 등) 쿨타임을 태우지 않는다 —
                // 여기서 시각을 밀면 <b>영영 안 나오는</b> 스킬이 조용히 만들어진다.
                _dawnReadyAt = Time.time + 1f;      // 매 프레임 재시도만 막는다
                return false;
            }

            _animator?.PlaySkillMotion(1, 0.8f, _golem.transform.position);

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName, "골렘 소환"),
                          UI.HudLogKind.Good);
            return true;
        }

        /// <summary>골렘이 죽었으면 그 순간부터 쿨타임을 센다(위 <see cref="TryDawn"/> 주석).</summary>
        void TickGolemLifetime()
        {
            if (_golem == null) return;
            if (_golem.IsAlive) { AruGolem.Follow(_unit, _golem); return; }

            PassiveSkillSO so = Find(PassiveSkillType.Dawn);
            _dawnReadyAt = Time.time + (so != null ? Mathf.Max(0f, so.coolTime) : 0f);
            _golem = null;
        }

        // ------------------------------------------------------------------
        // 카이론 9009
        // ------------------------------------------------------------------

        /// <summary>
        /// 타락한 육체 — value01 초 동안 최대 체력의 value02% 짜리 보호막. 쿨타임.
        ///
        /// <b>언제 쓰는가</b> — 정의문에 조건이 없다. 아무 때나 쓰면 <b>싸우지도 않는데</b>
        /// 쿨타임이 돌아 정작 필요할 때 없으므로, <b>전투 중일 때만</b> 건다
        /// (「희생」이 "다친 동료가 있을 때만" 인 것과 같은 종류의 판단이다).
        /// </summary>
        bool TryFallenBody()
        {
            PassiveSkillSO so = Find(PassiveSkillType.FallenBody);
            if (so == null || Time.time < _fallenBodyReadyAt) return false;
            if (!_unit.IsInCombat) return false;
            if (_unit.HasShield) return false;                  // 이미 걸려 있다

            float seconds = Mathf.Max(0f, so.value01);
            int amount = Mathf.RoundToInt(_unit.MaxHp * so.value02 * 0.01f);
            if (seconds <= 0f || amount <= 0) return false;

            _fallenBodyReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            _unit.GrantShield(amount, seconds);

            // ★ 슬롯 0 = 원화 시트의 「스킬 1」(보호막 획득). 표의 skill_01 과도 같은 번호다.
            _animator?.PlaySkillMotion(0, seconds, transform.position + Vector3.right);

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              $"보호막 {amount} · {seconds:0.#}초"), UI.HudLogKind.Good);
            return true;
        }

        /// <summary>
        /// 천상의 방패 — value01 초 <b>정신집중</b> → 지름 value02 안의 적을 value03 초
        /// <b>도발</b> → 도발이 끝나면 그 범위에 근거리 공격력의 value04% 피해.
        ///
        /// 세 단계가 <b>시간으로 이어진다</b>. 그래서 여기서는 «정신집중 시작» 까지만 하고
        /// 나머지는 <see cref="TickChannel"/> · <see cref="TickShieldBurst"/> 가 이어받는다.
        /// </summary>
        bool TryCelestialShield() => TryStartChannel(PassiveSkillType.CelestialShield,
                                                     ref _celestialReadyAt);

        /// <summary>
        /// 천벌 — value01 초 정신집중 → <b>가로 value02 x 세로 value03</b> 직사각형 안의 적에게
        /// 근거리 공격력의 value04% 피해 + value05 초 동안 방어력 value06% 감소.
        /// </summary>
        bool TryDivineWrath() => TryStartChannel(PassiveSkillType.DivineWrath, ref _divineReadyAt);

        /// <summary>
        /// 정신집중형 두 스킬의 <b>공통 시작 절차</b>.
        ///
        /// 공통으로 묶은 이유는 시작 조건이 완전히 같기 때문이다: 쿨타임 · 적이 있을 것 ·
        /// 이미 다른 정신집중 중이 아닐 것. 갈라지는 것은 «끝났을 때 무엇을 하는가» 뿐이라
        /// 그쪽만 <see cref="ResolveChannel"/> 에서 나눈다.
        ///
        /// ⚠ <b>정신집중 중에는 다른 정신집중을 시작할 수 없다</b> — 카이론은 이 유형을 둘
        ///   갖고 있어서, 막지 않으면 첫 번째가 터지기 전에 두 번째가 상태를 덮어써
        ///   <b>첫 번째가 통째로 사라진다.</b>
        /// </summary>
        bool TryStartChannel(PassiveSkillType type, ref float readyAt)
        {
            PassiveSkillSO so = Find(type);
            if (so == null || Time.time < readyAt) return false;
            if (_channel != PassiveSkillType.None) return false;
            if (_shieldBurstAt > 0f) return false;              // 앞 스킬의 도발이 아직 안 끝났다

            DamageableUnit target = _combat != null ? _combat.Target : null;
            if (target == null || !target.IsAlive || target.Faction == _unit.Faction) return false;

            float seconds = Mathf.Max(0f, so.value01);
            readyAt = Time.time + Mathf.Max(0f, so.coolTime);

            Vector2 aim = (Vector2)(target.transform.position - transform.position);
            _channelAim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            _channel = type;
            _channelEndAt = Time.time + seconds;

            // 정신집중 내내 시전 모션을 돌린다 — 카이론은 원화가 없어 지금은 평타로 떨어진다
            // (<see cref="CharacterAnimator.PlaySkillMotion"/> 의 폴백).
            // ★ 슬롯 번호는 <b>표의 skill_01·02·03 순서</b>다 — 원화 시트의 「스킬 N」과 같다.
            //   0 타락한 육체 · 1 천상의 방패 · 2 천벌.
            _animator?.PlaySkillMotion(type == PassiveSkillType.CelestialShield ? 1 : 2,
                                       seconds, target.transform.position);

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              $"정신집중 {seconds:0.#}초"), UI.HudLogKind.Good);
            return true;
        }

        /// <summary>정신집중이 끝났으면 그 스킬의 결과를 낸다.</summary>
        void TickChannel()
        {
            if (_channel == PassiveSkillType.None) return;
            if (Time.time < _channelEndAt) return;

            PassiveSkillType type = _channel;
            _channel = PassiveSkillType.None;
            ResolveChannel(type);
        }

        /// <summary>정신집중이 끝난 순간의 처리 — 두 스킬이 여기서만 갈린다.</summary>
        void ResolveChannel(PassiveSkillType type)
        {
            PassiveSkillSO so = Find(type);
            if (so == null) return;

            if (type == PassiveSkillType.CelestialShield)
            {
                // 정의문의 「지름값 value02」 — <b>지름</b>이라고 적혀 있으므로 반경은 절반이다.
                float radius = Mathf.Max(0f, so.value02) * 0.5f;
                float tauntSeconds = Mathf.Max(0f, so.value03);
                int taunted = TauntEnemiesInRadius(transform.position, radius, tauntSeconds);

                // 도발이 끝나는 순간 피해가 터진다 — 예약해 두고 TickShieldBurst 가 집행한다.
                _shieldBurstAt = Time.time + tauntSeconds;

                UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                                  $"도발 {taunted}명 · {tauntSeconds:0.#}초"),
                              UI.HudLogKind.Good);
                return;
            }

            // ── 천벌 : 바라보던 쪽으로 뻗는 직사각형 ──
            //   x = 조준 방향 길이 · y = 그와 직각인 두께 (CombatProjectileFx.PlayArea 의 규약과 같다).
            float length = Mathf.Max(0f, so.value02);
            float thick = Mathf.Max(0f, so.value03);
            int percent = Mathf.RoundToInt(so.value04);
            if (length <= 0f || thick <= 0f || percent <= 0) return;

            Vector3 center = transform.position + (Vector3)(_channelAim * (length * 0.5f));
            float angle = Mathf.Atan2(_channelAim.y, _channelAim.x) * Mathf.Rad2Deg;

            _newcomerScratch.Clear();
            UnitRegistry.CollectEnemiesInOrientedRect(center, new Vector2(length * 0.5f, thick * 0.5f),
                                                      _channelAim, _unit.Faction, _newcomerScratch);

            int defenseDownSeconds = Mathf.RoundToInt(so.value05);
            float defenseDownPercent = so.value06;
            int hits = 0;

            for (int i = 0; i < _newcomerScratch.Count; i++)
            {
                DamageableUnit u = _newcomerScratch[i];
                if (u == null || !u.IsAlive) continue;

                // ★ 방어력 감소를 <b>피해보다 먼저</b> 건다 — 정의문은 "피해를 당한 적은
                //   방어력이 감소한다" 지만, 순서를 뒤집으면 <b>죽은 적</b>에게 거는 꼴이 되고
                //   이 타격 자체가 감소된 방어력으로 계산되는 편이 «천벌» 이라는 말에도 맞다.
                if (defenseDownSeconds > 0 && defenseDownPercent > 0f)
                {
                    int amount = Mathf.RoundToInt(u.DefenseStat * defenseDownPercent * 0.01f);
                    if (amount > 0) PassiveSkillService.ApplyCorrosion(u, amount, defenseDownSeconds);
                }

                u.TakeDamageFrom(_unit, percent);
                hits++;
            }
            _newcomerScratch.Clear();

            Sprite[] fx = _animator != null && _animator.Skin != null ? _animator.Skin.SkillFx(2) : null;
            if (fx != null)
                CombatProjectileFx.PlayArea(fx, center, new Vector2(length, thick), angle, null, FxSeconds);

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              $"{length:0.#}x{thick:0.#}타일 · {hits}명"),
                          UI.HudLogKind.Good);
        }

        /// <summary>「천상의 방패」의 도발이 끝나는 순간 터지는 피해.</summary>
        void TickShieldBurst()
        {
            if (_shieldBurstAt <= 0f || Time.time < _shieldBurstAt) return;
            _shieldBurstAt = 0f;

            PassiveSkillSO so = Find(PassiveSkillType.CelestialShield);
            if (so == null || !_unit.IsAlive) return;

            float radius = Mathf.Max(0f, so.value02) * 0.5f;
            int percent = Mathf.RoundToInt(so.value04);
            if (radius <= 0f || percent <= 0) return;

            Sprite[] fx = _animator != null && _animator.Skin != null ? _animator.Skin.SkillFx(1) : null;
            if (fx != null)
                CombatProjectileFx.PlayArea(fx, transform.position,
                                            new Vector2(radius * 2f, radius * 2f), 0f, null, FxSeconds);

            int hits = DamageEnemiesInRadius(transform.position, radius, percent);
            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName, $"반격 {hits}명"),
                          UI.HudLogKind.Good);
        }

        // ==================================================================
        // 공용 헬퍼
        // ==================================================================

        /// <summary>
        /// 반경 안의 <b>적만</b> 골라 공격력의 <paramref name="percent"/>% 피해. 맞은 수를 돌려준다.
        ///
        /// ⚠ <see cref="UnitRegistry.CollectEnemiesInRadius"/> 는 <b>반대 진영</b>만 모은다 —
        ///   사냥 중인 중립은 그 진영이 아니라서 빠진다. 「복수자」가 <c>UnitRegistry.All</c> 을
        ///   직접 훑은 것과 같은 이유로 여기서도 전체를 훑고 진영을 직접 본다.
        /// </summary>
        int DamageEnemiesInRadius(Vector3 center, float radius, int percent)
        {
            float sqr = radius * radius;

            // 목록을 먼저 복사한다 — 피해로 유닛이 죽으면 UnitRegistry.All 이 그 자리에서 바뀐다.
            _newcomerScratch.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction == _unit.Faction) continue;
                if (((Vector2)(u.transform.position - center)).sqrMagnitude > sqr) continue;
                _newcomerScratch.Add(u);
            }

            int hits = 0;
            for (int i = 0; i < _newcomerScratch.Count; i++)
            {
                DamageableUnit u = _newcomerScratch[i];
                if (u == null || !u.IsAlive) continue;
                u.TakeDamageFrom(_unit, percent);
                hits++;
            }
            _newcomerScratch.Clear();
            return hits;
        }

        /// <summary>반경 안의 적을 도발한다. 도발한 수를 돌려준다.</summary>
        int TauntEnemiesInRadius(Vector3 center, float radius, float seconds)
        {
            if (radius <= 0f || seconds <= 0f) return 0;
            float sqr = radius * radius;
            int count = 0;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction == _unit.Faction) continue;
                if (((Vector2)(u.transform.position - center)).sqrMagnitude > sqr) continue;

                var combat = u.GetComponent<UnitCombat>();
                if (combat == null) continue;
                combat.ApplyTaunt(_unit, seconds);
                count++;
            }
            return count;
        }
    }
}
