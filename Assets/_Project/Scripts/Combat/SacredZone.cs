using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 아르세니아의 <b>「성스러운 축복」(80029)</b> 이 만드는 <b>바닥 공간</b>.
    ///
    /// 정의문: <i>"물약을 던져 적중 대상을 중심으로 반지름 {value_01} 크기의 원형 공간을
    /// {value_02}초 동안 생성합니다. 해당 공간에 있는 몬스터는 초당 아르세니아의 마법 *
    /// {value_03}% 의 데미지를 입고, 캐릭터는 자신이 받는 회복 효과가 {value_03} 만큼
    /// 증폭됩니다."</i>
    ///
    /// ★★ <b>왜 «장부» 가 아니라 오브젝트인가</b> — 이 프로젝트의 지속 효과는 대부분
    /// 서비스가 딕셔너리로 관리한다(부식·정화). 그런데 이건 <b>«자리» 가 있는 효과</b>다:
    /// 누가 걸렸는지가 아니라 <b>어디에 서 있는지</b> 로 매 순간 바뀐다. 자리를 가진 것을
    /// 장부로 표현하면 «지금 이 좌표에 어떤 공간들이 겹쳐 있나» 를 매번 다시 계산해야 한다.
    /// 오브젝트로 두면 그 질문이 사라진다 — 공간이 스스로 자기 안을 훑는다.
    ///
    /// ★ <b>회복 증폭은 «걸었다/풀었다» 로 관리한다.</b> 매 틱 더하면 공간 안에 오래 서 있는
    /// 아군의 증폭이 무한히 커진다. 들어올 때 한 번 걸고 나갈 때 정확히 같은 값을 뺀다 —
    /// 이 파일의 규칙이자 <see cref="CharacterPassives"/> 의 규칙이다(「걸었으면 되돌린다」).
    ///
    /// ⚠ <b>사라질 때 반드시 되돌린다</b>(<see cref="OnDestroy"/>) — 공간이 먼저 사라지고
    ///   아군만 남으면 그 아군은 <b>영원히 회복이 증폭된 채</b>가 된다.
    /// </summary>
    public class SacredZone : MonoBehaviour
    {
        /// <summary>피해를 넣는 간격(초). 정의문이 «초당» 이라 1이다.</summary>
        const float TickSeconds = 1f;

        CharacterUnit _owner;
        float _radius;
        float _endAt;
        int _damagePercent;
        int _healPercent;

        float _nextTickAt;
        readonly HashSet<DamageableUnit> _boosted = new HashSet<DamageableUnit>();
        static readonly List<DamageableUnit> _scratch = new List<DamageableUnit>();

        /// <summary>
        /// 공간 하나를 만든다. <paramref name="center"/> 는 물약이 맞은 자리다.
        /// </summary>
        /// <param name="fx">
        /// ★★ <b>이 공간을 그리는 원화</b> (2026-08-21). 넘기면 <b>공간이 그림의 주인</b>이 된다 —
        /// 공간이 사라질 때 그림도 같이 지운다(<see cref="OnDestroy"/>).
        ///
        /// <b>왜 이 인자가 생겼나</b> — 유저 리포트: *"아르세니아가 없는데 아르세니아의 2번째
        /// 스킬이 맵에 장식물처럼 구현되어있음"*. 예전에는 부르는 쪽
        /// (<c>CharacterPassives.TrySacredBlessing</c>)이 <b>공간과 그림을 따로</b> 띄웠다:
        /// <code>
        ///   SacredZone.Spawn(...);                 // 8초 뒤 · 또는 주인이 죽으면 사라진다
        ///   CombatProjectileFx.PlayArea(..., 8f);  // <b>자기 타이머로만</b> 사라진다
        /// </code>
        /// 수명이 <b>두 벌</b>이라 주인이 죽으면 공간만 없어지고 <b>마법진은 남았다</b> —
        /// 실체 없는 그림, 즉 «장식물» 이다.
        /// → 이제 <b>한 벌</b>이다. 그림의 수명이 공간의 수명이다.
        /// </param>
        public static SacredZone Spawn(CharacterUnit owner, Vector3 center,
                                       float radius, float seconds,
                                       int damagePercent, int healPercent,
                                       Sprite[] fx = null)
        {
            if (owner == null || radius <= 0f || seconds <= 0f) return null;

            var go = new GameObject("SacredZone");
            go.transform.position = center;
            var z = go.AddComponent<SacredZone>();
            z._owner = owner;
            z._radius = radius;
            z._endAt = Time.time + seconds;
            z._damagePercent = damagePercent;
            z._healPercent = healPercent;
            z._nextTickAt = Time.time + TickSeconds;

            // ★ 그림을 여기서 띄우고 <b>손잡이를 들고 있는다</b>. 지우는 것은 OnDestroy 다.
            if (fx != null && fx.Length > 0)
                // ⚠⚠ <b>기준 유닛을 넘기지 않는다</b>(anchor = null) — 2026-08-21 유저 리포트:
                //   *"아르세니아 두번째 스킬의 이펙트가 캐릭터의 이미지랑 겹치면 캐릭터를 가림"*.
                //
                //   <c>PlayArea</c> 에 유닛을 넘기면 그 유닛의 정렬을 물려받고 <b>순서를
                //   +20 올린다</b>(«탄환이 몸에 가려지지 않게» 라는 그쪽의 원래 목적이다).
                //   그러면 이 마법진이 <b>모든 캐릭터 위에</b> 깔려 캐릭터를 덮는다.
                //
                //   ★ 이것은 <b>밟고 서는 지면 연출</b>이라 «유닛 아래» 가 맞다. null 을
                //     넘기면 <c>GroundFxSortingLayer</c>(Floor · 순서 10) 로 떨어지고,
                //     레이어 순서가 Floor &lt; Object 이므로 캐릭터보다 확실히 아래로 간다.
                z._fxHandle = CombatProjectileFx.PlayArea(
                    fx, center, new Vector2(radius * 2f, radius * 2f),
                    0f, null, seconds);

            return z;
        }

        /// <summary>띄워 둔 마법진의 취소용 손잡이. 0 이면 그림이 없다.</summary>
        int _fxHandle;

        void Update()
        {
            if (Time.time >= _endAt || _owner == null)
            {
                Destroy(gameObject);
                return;
            }

            SyncHealBoost();

            if (Time.time < _nextTickAt) return;
            _nextTickAt += TickSeconds;
            DamageEnemies();
        }

        /// <summary>
        /// 지금 안에 있는 <b>아군 캐릭터</b>에게만 증폭을 걸어 둔다 — 들어오면 걸고 나가면 푼다.
        ///
        /// ⚠ <b>죽은 유닛도 반드시 푼다.</b> 안 그러면 그 유닛이 부활했을 때(히스톤)
        ///   증폭이 남아 있고, 이 공간이 사라져도 되돌릴 대상을 못 찾는다.
        /// </summary>
        void SyncHealBoost()
        {
            if (_healPercent == 0) return;

            float sqr = _radius * _radius;
            Vector3 c = transform.position;

            _scratch.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction != _owner.Faction) continue;
                if (u.Kind != UnitKind.Character) continue;
                if (((Vector2)(u.transform.position - c)).sqrMagnitude > sqr) continue;
                _scratch.Add(u);
            }

            // 들어온 쪽
            for (int i = 0; i < _scratch.Count; i++)
            {
                DamageableUnit u = _scratch[i];
                if (_boosted.Add(u)) u.AddHealReceivedPercent(_healPercent);
            }

            // 나간 쪽 (죽은 것 포함)
            if (_boosted.Count == _scratch.Count) return;
            _leaving.Clear();
            foreach (DamageableUnit u in _boosted)
                if (u == null || !u.IsAlive || !_scratch.Contains(u)) _leaving.Add(u);
            for (int i = 0; i < _leaving.Count; i++)
            {
                DamageableUnit u = _leaving[i];
                if (u != null) u.AddHealReceivedPercent(-_healPercent);
                _boosted.Remove(u);
            }
        }

        static readonly List<DamageableUnit> _leaving = new List<DamageableUnit>();

        /// <summary>
        /// 안에 있는 적을 초당 한 번 때린다.
        ///
        /// ⚠ 목록을 <b>먼저 복사</b>한다 — 피해로 유닛이 죽으면 <see cref="UnitRegistry.All"/>
        ///   이 그 자리에서 바뀐다(「복수자」가 세운 규칙).
        /// </summary>
        void DamageEnemies()
        {
            if (_damagePercent <= 0) return;

            float sqr = _radius * _radius;
            Vector3 c = transform.position;

            _scratch.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction == _owner.Faction) continue;
                if (((Vector2)(u.transform.position - c)).sqrMagnitude > sqr) continue;
                _scratch.Add(u);
            }

            for (int i = 0; i < _scratch.Count; i++)
            {
                DamageableUnit u = _scratch[i];
                if (u != null && u.IsAlive) u.TakeDamageFrom(_owner, _damagePercent);
            }
            _scratch.Clear();
        }

        void OnDestroy()
        {
            // ★ 걸어둔 것을 전부 되돌린다 (맨 위 ⚠).
            foreach (DamageableUnit u in _boosted)
                if (u != null) u.AddHealReceivedPercent(-_healPercent);
            _boosted.Clear();

            // ★★ <b>그림도 되돌린다</b> (2026-08-21) — 이 클래스의 «걸었으면 되돌린다» 규칙에
            //   <b>마법진</b>을 포함시킨 것이다. 유저 리포트: *"아르세니아가 없는데
            //   아르세니아의 2번째 스킬이 맵에 장식물처럼 구현되어있음"*.
            //
            //   예전에는 부르는 쪽이 공간과 그림을 <b>따로</b> 띄워 수명이 두 벌이었다.
            //   주인이 죽으면 공간만 사라지고 마법진은 자기 타이머(8초)까지 남았고,
            //   씬을 넘기면 <c>CombatProjectileFx</c> 가 <c>DontDestroyOnLoad</c> 라
            //   <b>새 판까지 넘어갔다</b> — 아르세니아가 없는 판에 그 그림이 남은 이유다.
            //
            //   이 함수를 지나는 길이 <b>셋</b>이고 전부 같은 정리를 받는다:
            //     · 지속시간이 끝났다              (Update 의 Time.time >= _endAt)
            //     · <b>주인이 죽었다</b>            (Update 의 _owner == null)  ← 버그였던 길
            //     · 씬이 바뀌어 오브젝트가 파괴됐다
            if (_fxHandle != 0)
            {
                CombatProjectileFx.Cancel(_fxHandle);
                _fxHandle = 0;
            }
        }
    }
}
