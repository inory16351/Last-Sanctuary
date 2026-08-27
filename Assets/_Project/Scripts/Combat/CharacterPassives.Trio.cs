using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 신규 3인의 패시브 9종 — 엘리시아 9012 · 세라피엘 9013 · 시안 9014 (2026-08-21).
    ///
    /// <b>왜 파일을 또 갈랐나</b> — <c>CharacterPassives.cs</c> 본체가 1,000줄이 넘고
    /// <c>CharacterPassives.Newcomers.cs</c> 도 1,000줄에 가깝다. 아홉 종을 그 안에 이어
    /// 붙이면 «어느 스킬이 어디 있는지» 를 찾는 비용이 또 오른다 — 119-9절이 내린 것과
    /// <b>같은 판단</b>이다(클래스는 하나, 읽는 단위만 나눈다).
    ///
    /// 본체와 이어지는 지점은 <b>다섯 곳</b>이다:
    /// <list type="bullet">
    /// <item><see cref="ApplyAlwaysOnTrio"/>  ← <c>ApplyAlwaysOn()</c></item>
    /// <item><see cref="TickTrio"/>           ← <c>Tick(dt)</c></item>
    /// <item><see cref="TickCooldownSkills"/> 의 우선순위 표 (Try* 넷)</item>
    /// <item><see cref="HookTrioEvents"/> / <see cref="UnhookTrioEvents"/> ← <c>OnEnable/OnDisable</c></item>
    /// <item><see cref="ClearTrioEffects"/>   ← <c>OnDisable()</c></item>
    /// </list>
    ///
    /// ★★ <b>이 세 인물이 새로 요구한 것</b> — 앞의 열둘에는 없던 계기가 셋 생겼다:
    /// <list type="number">
    /// <item><b>«맞을 때»</b> — 「군단의 방패」(반사). 지금까지 계기는 «때릴 때»·«상시»·«쿨타임» 뿐이었다.</item>
    /// <item><b>«적이 죽을 때»</b> — 「영혼 흡수」(자원 누적). 캐릭터가 <b>상태를 모으는</b> 첫 사례다.</item>
    /// <item><b>«평타를 낼 때 확률로»</b> — 「사신의 낫」. 쿨타임이 아니라 <b>확률</b>이 문이다.</item>
    /// </list>
    /// 셋 다 <b>이미 있는 이벤트</b>로 붙였다(<c>OnAnyDamaged</c>·<c>OnAnyDied</c>·
    /// <c>OnAttackPerformed</c>) — 전투 루프를 건드리지 않는다는 25-5절의 제약 그대로다.
    /// </summary>
    public partial class CharacterPassives
    {
        // ==================================================================
        // 상태
        // ==================================================================

        /// <summary>「강인한 정신」·「네 날개의 가호」·「회피 기동」·「종말의 선언」의 다음 사용 시각.</summary>
        float _strongMindReadyAt;
        float _fourWingsReadyAt;
        float _evasiveReadyAt;
        float _declarationReadyAt;

        /// <summary>
        /// ★ 「군단의 방패」가 <b>지금 반사 중</b>인가. 방패를 든 둘이 서로 때리면 반사가
        /// 무한히 오간다 — 재진입을 여기서 끊는다(enum 주석의 ⚠).
        /// </summary>
        bool _reflecting;

        /// <summary>「영혼 흡수」로 모은 영혼. UI 가 <see cref="SoulCount"/> 로 읽는다.</summary>
        int _souls;

        /// <summary>「한계 돌파」로 <b>이미 준</b> 근거리 공격력. 되돌릴 때 이 값만큼만 뺀다.</summary>
        int _appliedSoulAttack;

        /// <summary>「명사수」로 이미 준 크리티컬 확률.</summary>
        int _appliedSharpCritical;

        /// <summary>이벤트를 두 번 걸지 않으려는 깃발(<see cref="HookTrioEvents"/>).</summary>
        bool _trioHooked;

        /// <summary>범위 판정용 임시 목록 — 정적으로 공유한다(본체·Newcomers 와 같은 규칙).</summary>
        static readonly List<DamageableUnit> _trioScratch = new List<DamageableUnit>();

        /// <summary>연출이 화면에 남는 시간(초). 순수 연출값이라 표에 칸이 없다.</summary>
        const float TrioFxSeconds = 0.6f;

        /// <summary>
        /// 「영혼 흡수」로 모은 영혼 수. <b>스킬 칸에 표시</b>하기 위해 공개한다
        /// (정의문: *"획득한 영혼은 해당 스킬 슬롯에 표기 됩니다"*).
        /// 스킬이 없으면 언제나 0 이라 UI 가 «칸이 있는지» 만 보면 된다.
        /// </summary>
        public int SoulCount => Find(PassiveSkillType.SoulAbsorption) != null ? _souls : 0;

        // ==================================================================
        // 이벤트 연결
        // ==================================================================

        /// <summary>
        /// 세 계기를 잇는다. <b>스킬이 있든 없든 건다</b> — 강화로 스킬이 나중에 해금될 수 있고,
        /// 콜백 안에서 <see cref="Find"/> 로 다시 확인하므로 없으면 아무 일도 안 한다.
        /// ⚠ 두 번 걸면 반사·영혼이 <b>두 배</b>가 되므로 깃발로 막는다.
        /// </summary>
        void HookTrioEvents()
        {
            if (_trioHooked) return;
            _trioHooked = true;
            DamageableUnit.OnAnyDamaged += OnAnyDamagedTrio;
            DamageableUnit.OnAnyDied += OnAnyDiedTrio;
            if (_combat != null) _combat.OnAttackPerformed += OnAttackPerformedTrio;
        }

        void UnhookTrioEvents()
        {
            if (!_trioHooked) return;
            _trioHooked = false;
            DamageableUnit.OnAnyDamaged -= OnAnyDamagedTrio;
            DamageableUnit.OnAnyDied -= OnAnyDiedTrio;
            if (_combat != null) _combat.OnAttackPerformed -= OnAttackPerformedTrio;
        }

        // ==================================================================
        // 상시 효과
        // ==================================================================

        /// <summary>
        /// 상시 둘을 건다 — <b>다시 불려도 안전하다</b>(이미 준 만큼만 조정한다).
        ///
        /// ★ 「명사수」는 값이 안 바뀌므로 한 번 걸면 끝이고, 「한계 돌파」는 <b>영혼이 늘 때마다</b>
        ///   목표치가 오른다 — 그래서 <see cref="TickTrio"/> 가 매 프레임 이 함수를 다시 부른다
        ///   (비교 한 번이라 비용이 없다 · 「고조된 감각」과 같은 방식).
        /// </summary>
        void ApplyAlwaysOnTrio()
        {
            // ── 명사수 : 크리티컬 확률 +value01 영구 (상한 초월) ──
            PassiveSkillSO sharp = Find(PassiveSkillType.Sharpshooter);
            int wantCrit = sharp != null ? Mathf.Max(0, Mathf.RoundToInt(sharp.value01)) : 0;
            if (wantCrit != _appliedSharpCritical)
            {
                _unit.AddFlatStatBonus(StatType.Critical, wantCrit - _appliedSharpCritical);
                _appliedSharpCritical = wantCrit;
            }

            // ── 한계 돌파 : 영혼 value01 개마다 근거리 공격력 +value02 (상한 초월) ──
            //   ⚠ «지금 받아야 할 총량 − 이미 준 총량» 만 더한다(enum 주석의 ⚠).
            PassiveSkillSO limit = Find(PassiveSkillType.BreakingThroughLimits);
            int wantAtk = 0;
            if (limit != null)
            {
                int per = Mathf.Max(1, Mathf.RoundToInt(limit.value01));
                int step = Mathf.Max(0, Mathf.RoundToInt(limit.value02));
                wantAtk = (_souls / per) * step;
            }
            if (wantAtk != _appliedSoulAttack)
            {
                _unit.AddFlatStatBonus(StatType.Attack, wantAtk - _appliedSoulAttack);
                _appliedSoulAttack = wantAtk;
            }
        }

        /// <summary>사라질 때 <b>영구 보정을 되돌린다</b>(본체 OnDisable 규칙).</summary>
        void ClearTrioEffects()
        {
            UnhookTrioEvents();

            if (_appliedSharpCritical != 0)
            {
                _unit.AddFlatStatBonus(StatType.Critical, -_appliedSharpCritical);
                _appliedSharpCritical = 0;
            }
            if (_appliedSoulAttack != 0)
            {
                _unit.AddFlatStatBonus(StatType.Attack, -_appliedSoulAttack);
                _appliedSoulAttack = 0;
            }
            // ★ 영혼은 <b>그 유닛의 상태</b>다 — 사라질 때 0 으로 돌린다(enum 주석의 ⚠).
            _souls = 0;
        }

        /// <summary>본체 <c>Tick</c> 의 갈래. 상시 갱신만 한다 — 발동은 쿨타임 표가 부른다.</summary>
        void TickTrio(float dt)
        {
            HookTrioEvents();       // 첫 프레임에 붙는다(Awake 순서에 기대지 않는다)
            ApplyAlwaysOnTrio();    // 영혼이 늘었으면 「한계 돌파」가 여기서 따라온다
        }

        // ==================================================================
        // 엘리시아 9012
        // ==================================================================

        /// <summary>
        /// 「강인한 정신」(80034) — 체력이 최대의 <c>value01</c>% <b>이하</b>로 떨어지면
        /// <c>value02</c>초에 걸쳐 최대 체력의 <c>value03</c>% 를 회복한다.
        ///
        /// ★ <b>조건이 안 맞으면 <c>false</c></b> — 그래야 쿨타임을 태우지 않고 «아낀다»
        ///   (보스 「급속 재생」과 같은 규칙 · 본체 <c>TickCooldownSkills</c> 가 그 뜻으로 읽는다).
        /// </summary>
        bool TryStrongMind()
        {
            PassiveSkillSO so = Find(PassiveSkillType.StrongMind);
            if (so == null || Time.time < _strongMindReadyAt) return false;

            int max = _unit.MaxHp;
            if (max <= 0) return false;

            float threshold = Mathf.Clamp(so.value01, 0f, 100f);
            if (threshold <= 0f) return false;
            if ((float)_unit.CurrentHp / max * 100f > threshold) return false;   // 아직 아낀다

            int amount = Mathf.RoundToInt(max * Mathf.Max(0f, so.value03) / 100f);
            if (amount <= 0) return false;

            float seconds = Mathf.Max(0f, so.value02);
            if (seconds > 0f) StartCoroutine(HealOverTimeTrio(amount, seconds));
            else _unit.Heal(amount);

            _strongMindReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            _animator?.PlaySkillMotion(0, TrioFxSeconds, transform.position + Vector3.right);
            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_hp_gain_over",
                                                                          "체력 +{0} ({1:0.#}초)"),
                                                            amount, seconds)),
                          UI.HudLogKind.Good);
            return true;
        }

        /// <summary>
        /// 회복 <paramref name="total"/> 을 <paramref name="seconds"/> 초에 <b>나눠</b> 넣는다.
        ///
        /// ★ 매 틱 «지금까지 들어가야 할 <b>누적량</b> − 이미 넣은 양» 을 넣는다. 틱마다
        ///   <c>total/n</c> 을 반올림하면 몇 점씩 새므로, 이렇게 하면 합계가 <b>정확히</b> total 이다
        ///   (보스 「강제 보급」의 <c>HealOverTime</c> 과 같은 계산).
        /// ⚠ 도중에 <b>죽으면 멈춘다</b> — 죽은 유닛을 회복시키면 시체가 되살아난다.
        /// </summary>
        IEnumerator HealOverTimeTrio(int total, float seconds)
        {
            const float Tick = 0.2f;
            float elapsed = 0f;
            int given = 0;
            while (elapsed < seconds)
            {
                yield return new WaitForSeconds(Tick);
                if (_unit == null || !_unit.IsAlive) yield break;
                elapsed += Tick;
                int want = Mathf.RoundToInt(total * Mathf.Clamp01(elapsed / seconds));
                int step = want - given;
                if (step <= 0) continue;
                _unit.Heal(step);
                given = want;
            }
        }

        /// <summary>
        /// 「군단의 방패」(80035) — 맞을 때마다 <b>때린 적</b>에게 자기 최대 체력의
        /// <c>value01</c>% 를 그대로 준다.
        ///
        /// ⚠ <b>구조물·아군은 때리지 않는다</b> — 공격자가 적일 때만 반사한다.
        /// ⚠ <b>재진입을 끊는다</b>(<see cref="_reflecting"/>) — 방패 둘이 서로를 무한히 때린다.
        /// ⚠ <b>대가로 잃는 체력에는 반응하지 않는다</b> — <paramref name="attacker"/> 가
        ///   비어 있으면(자기 비용·장판 등) 때릴 대상이 없다.
        /// </summary>
        void OnAnyDamagedTrio(DamageableUnit attacker, DamageableUnit victim, int amount, bool critical)
        {
            if (victim != (DamageableUnit)_unit) return;
            if (_reflecting || attacker == null || !attacker.IsAlive) return;
            if (attacker.Faction == _unit.Faction) return;

            PassiveSkillSO so = Find(PassiveSkillType.LegionsShield);
            if (so == null || so.value01 <= 0f) return;

            int reflect = Mathf.RoundToInt(_unit.MaxHp * so.value01 / 100f);
            if (reflect <= 0) return;

            _reflecting = true;
            try { attacker.ApplyDamage(reflect); }
            finally { _reflecting = false; }
        }

        /// <summary>
        /// 「네 날개의 가호」(80036) — 반지름 <c>value01</c>: 적에게 마법 x <c>value02</c>% 피해 +
        /// <c>value03</c>초 기절, <b>같은 범위 아군</b>은 자기 최대 체력의 <c>value04</c>% 회복.
        ///
        /// ★ <b>적이 하나도 없으면 쓰지 않는다</b> — 회복만 하려고 60초 쿨타임을 태우지 않는다
        ///   (정의문의 주어가 «적을 섬멸하고» 다).
        /// </summary>
        bool TryBlessingOfFourWings()
        {
            PassiveSkillSO so = Find(PassiveSkillType.BlessingOfFourWings);
            if (so == null || Time.time < _fourWingsReadyAt) return false;

            float radius = Mathf.Max(0.5f, so.value01);
            Vector3 at = transform.position;

            UnitRegistry.CollectEnemiesInRadius(at, radius, _unit.Faction, _trioScratch);
            if (_trioScratch.Count == 0) return false;

            int percent = Mathf.Max(0, Mathf.RoundToInt(so.value02));
            float stun = Mathf.Max(0f, so.value03);
            int hits = 0;
            for (int i = 0; i < _trioScratch.Count; i++)
            {
                DamageableUnit u = _trioScratch[i];
                if (u == null || !u.IsAlive) continue;
                // ⚠ 정의문이 «마법 x %» 라 <b>마법 능력치</b>가 기준이다 — 평타 공격력이 아니다.
                u.ApplyDamage(Mathf.RoundToInt(_unit.EffectiveStat(StatType.Magic) * percent / 100f));
                hits++;
                if (u.IsAlive && stun > 0f) u.GetComponent<UnitCombat>()?.ApplyBind(stun);
            }

            // ── 아군 회복 — 같은 반지름이다(정의문 그대로) ──
            int healed = 0;
            int heal = Mathf.RoundToInt(_unit.MaxHp * Mathf.Max(0f, so.value04) / 100f);
            if (heal > 0)
            {
                // ⚠ 이 함수는 <b>exclude 인자를 요구한다</b> — 자기를 뺀 «주변 아군» 을 모은다.
                //   자기 회복은 아래에서 따로 넣는다(두 번 받지 않게).
                UnitRegistry.CollectAlliesInRadius(at, radius, _unit.Faction, _unit, _trioScratch);
                for (int i = 0; i < _trioScratch.Count; i++)
                {
                    DamageableUnit a = _trioScratch[i];
                    if (a == null || !a.IsAlive || !a.AcceptsExternalHeal) continue;
                    a.Heal(heal);
                    healed++;
                }
                // ★ 엘리시아 <b>자신도</b> 아군이다 — 정의문의 «주변의 아군» 에서 자기를 뺄
                //   근거가 없고, 회복 역할의 생존이 곧 전열의 생존이다.
                if (_unit.AcceptsExternalHeal) { _unit.Heal(heal); healed++; }
            }

            _fourWingsReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            _animator?.PlaySkillMotion(0, TrioFxSeconds, at + Vector3.right);
            PlayTrioAreaFx(0, at, radius * 2f);
            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_hits_and_heal",
                                                                          "{0}명 피격 · 아군 {1}명 +{2}"),
                                                            hits, healed, heal)),
                          UI.HudLogKind.Good);
            return true;
        }

        // ==================================================================
        // 세라피엘 9013
        // ==================================================================

        /// <summary>
        /// 「회피 기동」(80037) — 반지름 <c>value01</c> 안에 적이 있으면 <b>그 적의 반대쪽</b>으로
        /// <c>value02</c> 타일 도약하고 <c>value03</c>회 원거리 공격.
        ///
        /// ⚠ 도착 자리는 <b>배치 가능한 칸으로 스냅</b>한다 — 순간이동을 그대로 두면 벽에 낀다
        ///   (116절의 사고 · 「도움의 손길」과 같은 규칙).
        /// ★ 원거리 공격은 <b>있는 통로</b>를 쓴다(<see cref="UnitCombat.FireRangedShot"/>) —
        ///   피해·명중·치명타 파이프라인을 새로 만들지 않는다.
        /// </summary>
        bool TryEvasiveManeuver()
        {
            PassiveSkillSO so = Find(PassiveSkillType.EvasiveManeuver);
            if (so == null || Time.time < _evasiveReadyAt) return false;

            float radius = Mathf.Max(0.5f, so.value01);
            Vector3 me = transform.position;
            UnitRegistry.CollectEnemiesInRadius(me, radius, _unit.Faction, _trioScratch);

            DamageableUnit near = null;
            float best = float.MaxValue;
            for (int i = 0; i < _trioScratch.Count; i++)
            {
                DamageableUnit u = _trioScratch[i];
                if (u == null || !u.IsAlive) continue;
                float d = ((Vector2)(u.transform.position - me)).sqrMagnitude;
                if (d < best) { best = d; near = u; }
            }
            if (near == null) return false;

            // ── 도약 : 적의 반대쪽으로 value02 타일 ──
            Vector2 away = (Vector2)(me - near.transform.position);
            if (away.sqrMagnitude < 0.0001f) away = Vector2.right;
            Vector3 want = me + (Vector3)(away.normalized * Mathf.Max(0f, so.value02));
            transform.position = SnapToPlaceable(want, me);

            // ── 원거리 공격 value03 회 ──
            int shots = Mathf.Max(1, Mathf.RoundToInt(so.value03));
            _evasiveReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            _animator?.PlaySkillMotion(0, TrioFxSeconds, near.transform.position);
            StartCoroutine(FireVolley(near, shots));

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_leap_shots",
                                                                          "{0:0.#}타일 도약 · {1}발"),
                                                            so.value02, shots)),
                          UI.HudLogKind.Good);
            return true;
        }

        /// <summary>
        /// 도약 직후의 연사. <b>한 프레임에 몰지 않는다</b> — 원화가 갯틀링 연사라 시간 간격이
        /// 있어야 보이고, 몰아서 넣으면 회복·방어가 끼어들 틈이 없다(「죽음의 노래」와 같은 판단).
        /// </summary>
        IEnumerator FireVolley(DamageableUnit target, int shots)
        {
            for (int i = 0; i < shots; i++)
            {
                if (_unit == null || !_unit.IsAlive) yield break;
                if (target == null || !target.IsAlive) yield break;
                // ★ «한 발» 전용 통로가 없다 — 평타와 <b>같은 통로</b>를 그 횟수만큼 부른다
                //   (`UnitCombat.PerformRangedMultiShot` 도 이 함수로 때린다). 그래서 명중·
                //   치명타·방어력이 평타와 완전히 같게 계산된다.
                target.TakeDamageFrom(_unit);
                yield return new WaitForSeconds(0.12f);
            }
        }

        /// <summary>
        /// 「종말의 선언」(80039) — 조준 방향 <b>전방 <c>value01</c> x <c>value02</c></b> 직사각형에
        /// <b>초당</b> 원거리 x <c>value03</c>% 피해를 <c>value04</c>초 동안.
        ///
        /// ★ 상자는 <b>시전한 자리에 고정</b>이다(enum 주석의 ⚠) — 따라다니면 «전방 포격» 이 아니다.
        /// </summary>
        bool TryDeclarationOfTheEnd()
        {
            PassiveSkillSO so = Find(PassiveSkillType.DeclarationOfTheEnd);
            if (so == null || Time.time < _declarationReadyAt) return false;

            DamageableUnit target = _combat != null ? _combat.Target : null;
            if (target == null || !target.IsAlive) return false;   // 조준할 것이 없으면 «전방» 이 없다

            float length = Mathf.Max(1f, so.value01);
            float width = Mathf.Max(1f, so.value02);
            Vector2 dir = ((Vector2)(target.transform.position - transform.position)).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

            // 자기 칸을 포함해 앞으로 뻗는다(BossSkillCaster 의 직사각형 갈래와 같은 계산).
            Vector3 center = transform.position + (Vector3)(dir * ((length - 1f) * 0.5f));

            _declarationReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            _animator?.PlaySkillMotion(1, Mathf.Max(TrioFxSeconds, so.value04),
                                       target.transform.position);
            StartCoroutine(Barrage(so, center, dir, length, width));
            StartCoroutine(BarrageFx(center, dir, length, width,
                                     Mathf.Max(1, Mathf.RoundToInt(so.value04))));

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_box_seconds",
                                                                          "{0:0.#}x{1:0.#}타일 · {2:0.#}초"),
                                                            length, width, so.value04)),
                          UI.HudLogKind.Good);
            return true;
        }

        /// <summary>
        /// 고정된 상자에 <b>매초</b> 피해를 넣는다 — <c>value04</c>초 동안.
        /// 매 초 <b>대상을 다시 모은다</b>(그 사이 들어온 적도 맞고, 죽은 적은 빠진다).
        /// </summary>
        IEnumerator Barrage(PassiveSkillSO so, Vector3 center, Vector2 dir,
                            float length, float width)
        {
            int ticks = Mathf.Max(1, Mathf.RoundToInt(so.value04));
            int percent = Mathf.Max(0, Mathf.RoundToInt(so.value03));
            var half = new Vector2(length * 0.5f, width * 0.5f);

            for (int t = 0; t < ticks; t++)
            {
                if (_unit == null || !_unit.IsAlive) yield break;

                // ⚠ 연출은 <see cref="BarrageFx"/> 가 따로 돈다 — 여기서 그리면 «1초에 한 번
                //   번쩍» 이라 포격으로 안 보인다(2026-08-24 유저 지시로 갈라냈다).
                UnitRegistry.CollectEnemiesInOrientedRect(center, half, dir, _unit.Faction, _trioScratch);
                for (int i = 0; i < _trioScratch.Count; i++)
                {
                    DamageableUnit u = _trioScratch[i];
                    if (u == null || !u.IsAlive) continue;
                    u.TakeDamageFrom(_unit, percent);
                }
                yield return new WaitForSeconds(1f);
            }
        }

        // ------------------------------------------------------------------
        // 「종말의 선언」 연출 — 포탄을 흩뿌린다 (2026-08-24)
        // ------------------------------------------------------------------

        /// <summary>한 «초» 에 떨어지는 포탄 수. 늘리면 화려해지고 그만큼 그림이 많아진다.</summary>
        const int BarrageShellsPerSecond = 7;

        /// <summary>포탄 하나가 화면에 남는 시간(초). 짧아야 «터지고 사라진다» 로 읽힌다.</summary>
        const float BarrageShellSeconds = 0.34f;

        /// <summary>
        /// ★★ <b>포격처럼 보이게 한다</b> (2026-08-24 · 유저 지시:
        /// <i>"세라피엘 세번째 스킬 포격 느낌이 안 나니까 좀 여러개 섞어서 화려하게"</i>).
        ///
        /// <b>예전에는 왜 밋밋했나</b> — 1초에 한 번, <c>skill2Fx</c> <b>한 장</b>을 상자 크기로
        /// 늘려 깔았다. 그림이 <b>정사각</b>으로 늘어나(<c>max(length,width)</c>) 6x2 상자와
        /// 모양도 안 맞았고, 무엇보다 «한 번 번쩍» 이라 <b>연사</b>로 읽히지 않았다.
        ///
        /// ★ 세 겹으로 쌓는다:
        /// <list type="number">
        /// <item><b>상자</b> — 어디가 맞는지 한 번만 깐다. 이제 <b>가로x세로를 따로</b> 준다.</item>
        /// <item><b>포탄</b> — 상자 안 아무 데나, 초당 <see cref="BarrageShellsPerSecond"/> 발.
        ///       크기·각도·프레이즈를 매번 흔들어 같은 그림이 반복돼 보이지 않게 한다.</item>
        /// <item><b>총구 섬광</b> — 매 초 시전자 앞에. «쏘는 쪽» 이 보여야 포격이 된다.</item>
        /// </list>
        ///
        /// ★ <b>스킨의 여러 칸을 섞어 쓴다</b>(유저 지시의 «여러개 섞어서») —
        ///   <c>skill2Fx</c> · <c>magicImpactFrames</c> · <c>impactFrames</c> ·
        ///   <c>muzzleFlashFrames</c> 중 <b>있는 것만</b> 모아 돌려 쓴다. 세라피엘은 넷 중
        ///   셋을 갖고 있다(3 · 4 · 4장). 없으면 조용히 빠진다 — 다른 인물이 이 스킬을
        ///   갖게 돼도 안 깨진다.
        ///
        /// ⚠ <b>순수 연출이다.</b> 피해는 <see cref="Barrage"/> 가 넣는다 —
        ///   여기서 한 번 더 넣으면 이중 타격이 된다(<c>CombatProjectileFx</c> 의 대원칙).
        /// </summary>
        IEnumerator BarrageFx(Vector3 center, Vector2 dir, float length, float width, int ticks)
        {
            CharacterSkinSO skin = _animator != null ? _animator.Skin : null;
            if (skin == null) yield break;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // ① 상자 — 가로는 사거리, 세로는 두께. 포격이 끝날 때까지 깔려 있는다.
            Sprite[] box = skin.SkillFx(1);
            if (box != null)
                CombatProjectileFx.PlayArea(box, center, new Vector2(length, width),
                                            angle, _unit, ticks);

            // 섞어 쓸 프레임 묶음 — 있는 것만 모은다.
            var kinds = new List<Sprite[]>();
            void Add(Sprite[] f) { if (f != null && f.Length > 0) kinds.Add(f); }
            Add(box);
            Add(skin.magicImpactFrames);
            Add(skin.impactFrames);
            Add(skin.muzzleFlashFrames);
            if (kinds.Count == 0) yield break;

            var perp = new Vector2(-dir.y, dir.x);
            float gap = 1f / Mathf.Max(1, BarrageShellsPerSecond);

            for (int t = 0; t < ticks; t++)
            {
                if (_unit == null || !_unit.IsAlive) yield break;

                // ③ 총구 섬광 — 몸 앞 한 칸.
                if (skin.muzzleFlashFrames != null && skin.muzzleFlashFrames.Length > 0)
                    CombatProjectileFx.PlayArea(skin.muzzleFlashFrames,
                                                transform.position + (Vector3)(dir * 0.8f),
                                                new Vector2(1.1f, 1.1f), angle, _unit, 0.18f);

                for (int i = 0; i < BarrageShellsPerSecond; i++)
                {
                    if (_unit == null || !_unit.IsAlive) yield break;

                    // ② 상자 안 아무 자리. 가장자리에 몰리지 않게 0.9 만큼만 쓴다.
                    Vector3 at = center
                               + (Vector3)(dir * (Random.Range(-0.45f, 0.45f) * length))
                               + (Vector3)(perp * (Random.Range(-0.45f, 0.45f) * width));

                    Sprite[] frames = kinds[Random.Range(0, kinds.Count)];
                    float size = Random.Range(0.9f, 1.7f);

                    // 각도를 통째로 흔든다 — 같은 그림이 같은 방향으로 반복되면 «복사» 로 보인다.
                    CombatProjectileFx.PlayArea(frames, at, new Vector2(size, size),
                                                Random.Range(0f, 360f), _unit,
                                                BarrageShellSeconds);

                    yield return new WaitForSeconds(gap);
                }
            }
        }

        // ==================================================================
        // 시안 9014
        // ==================================================================

        /// <summary>
        /// 「영혼 흡수」(80040) — 반지름 <c>value01</c> 안에서 적이 죽으면 영혼 +1.
        ///
        /// ⚠ <b>내가 죽인 것만이 아니다</b> — 정의문은 *"근처 … 범위에서 적이 사망할 때마다"*
        ///   라 <b>거리만</b> 조건이다. 누가 죽였는지는 보지 않는다.
        /// ★ 늘어난 영혼은 <see cref="ApplyAlwaysOnTrio"/> 가 「한계 돌파」로 옮긴다.
        /// </summary>
        void OnAnyDiedTrio(DamageableUnit dead)
        {
            if (dead == null || _unit == null || !_unit.IsAlive) return;
            if (dead.Faction == _unit.Faction) return;

            PassiveSkillSO so = Find(PassiveSkillType.SoulAbsorption);
            if (so == null) return;

            float radius = Mathf.Max(0.5f, so.value01);
            if (((Vector2)(dead.transform.position - transform.position)).sqrMagnitude
                > radius * radius) return;

            _souls++;
        }

        /// <summary>
        /// 「사신의 낫」(80041) — 근거리 평타마다 <c>value01</c>% 확률로 반지름 <c>value02</c>
        /// 범위를 후려친다. 피해 = 근거리 x <c>value03</c>% <b>+ 모은 영혼 수</b>.
        ///
        /// ⚠ <b>근거리 평타일 때만</b> 돈다 — 정의문이 «근거리 공격을 할 때마다» 다.
        ///   원거리·마법으로 전술을 바꾸면 이 스킬은 쉬는 것이 맞다.
        /// </summary>
        void OnAttackPerformedTrio()
        {
            if (_unit == null || !_unit.IsAlive) return;
            if (_combat == null || _combat.AttackType != TacticalAttackType.Melee) return;

            PassiveSkillSO so = Find(PassiveSkillType.ReapersScythe);
            if (so == null) return;
            if (Random.Range(0f, 100f) >= Mathf.Max(0f, so.value01)) return;

            float radius = Mathf.Max(0.5f, so.value02);
            Vector3 at = transform.position;
            UnitRegistry.CollectEnemiesInRadius(at, radius, _unit.Faction, _trioScratch);
            if (_trioScratch.Count == 0) return;

            // ★ «근거리 공격력 x % + 영혼» — 영혼은 <b>고정 추가 피해</b>다(enum 주석의 ★).
            int flat = Mathf.RoundToInt(_unit.EffectiveStat(StatType.Attack)
                                        * Mathf.Max(0f, so.value03) / 100f) + _souls;
            if (flat <= 0) return;

            int hits = 0;
            for (int i = 0; i < _trioScratch.Count; i++)
            {
                DamageableUnit u = _trioScratch[i];
                if (u == null || !u.IsAlive) continue;
                u.ApplyDamage(flat);
                hits++;
            }

            _animator?.PlaySkillMotion(0, TrioFxSeconds, at + Vector3.right);
            PlayTrioAreaFx(0, at, radius * 2f);
            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_hits_souls",
                                                                          "{0}명 · 영혼 {1}"),
                                                            hits, _souls)),
                          UI.HudLogKind.Good);
        }

        // ==================================================================
        // 공용 — 연출 · 자리 스냅
        // ==================================================================

        /// <summary>
        /// 스킨의 <c>skillNFx</c> 를 바닥에 한 번 깐다. 그 칸이 비어 있으면 <b>조용히 넘어간다</b>
        /// (연출이 없어도 스킬은 성립한다 — 본체의 다른 스킬과 같은 규칙).
        /// </summary>
        void PlayTrioAreaFx(int slot, Vector3 center, float sizeTiles)
        {
            CharacterSkinSO skin = _animator != null ? _animator.Skin : null;
            Sprite[] fx = skin != null ? skin.SkillFx(slot) : null;
            if (fx == null || fx.Length == 0) return;
            CombatProjectileFx.PlayArea(fx, center, new Vector2(sizeTiles, sizeTiles), 0f,
                                        _unit, TrioFxSeconds);
        }

        /// <summary>
        /// <paramref name="want"/> 를 <b>배치 가능한 칸</b>으로 스냅한다. 못 찾으면
        /// <paramref name="fallback"/>(원래 자리)로 돌려보낸다 — <b>벽에 끼는 것보다 안 움직이는
        /// 것이 낫다</b>(116절의 사고).
        /// </summary>
        Vector3 SnapToPlaceable(Vector3 want, Vector3 fallback)
        {
            var map = Object.FindFirstObjectByType<Map.MapGenerator>();
            if (map == null) return want;
            return map.TryFindPlaceableNear(map.WorldToCell(want), 3, null, out Vector3Int cell)
                ? map.CellCenterWorld(cell)
                : fallback;
        }
    }
}
