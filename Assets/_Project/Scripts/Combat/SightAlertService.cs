using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// ★ <b>"저기서 뭔가 날아왔다" 경보 게시판</b> (유저 지시 2026-08-13).
    ///
    /// <b>왜 필요한가</b> — 이제 캐릭터는 <b>시야 밖의 적을 공격하지 못한다</b>
    /// (<see cref="UnitCombat.IsFogVisible"/>). 그러면 안개 밖에서 쏘는 적에게
    /// 일방적으로 맞으면서 <b>아무 반응도 못 하는</b> 상태가 된다. 그래서 반격을 막은 대신
    /// <b>전방 캐릭터가 그 자리로 가서 확인</b>하게 한다 — 유저 지시의 "대신" 부분이 이것이다.
    ///
    /// <b>왜 씬 컴포넌트가 아닌 정적 클래스인가</b> — <see cref="UnitRegistry"/> 와 같은
    /// 이유다. 경보는 <b>진영 전체가 공유하는 하나뿐인 목록</b>이고, 씬에 오브젝트를 두면
    /// MCP 로 배선할 참조가 늘어나기만 한다(진행상황 8절 4번). 조정할 값들은
    /// <c>CharacterBehavior</c> 인스펙터에 있으므로 "하드코딩 금지" 규약(35절)도 지켜진다.
    ///
    /// <b>경보가 사라지는 세 가지 경우</b>
    /// <list type="number">
    /// <item><b>누군가 그 자리를 보게 됨</b> — 확인이 끝났다는 뜻이라 이게 정상 종료다.
    ///       확인하러 간 캐릭터가 도착하면 자기 시야로 밝히므로 저절로 걸린다.</item>
    /// <item>수명(<c>ttl</c>) 초과 — 확인하러 갈 캐릭터가 없거나 길이 막힌 경우.</item>
    /// <item>담당 캐릭터가 죽음 — 소유권만 풀리고 경보는 남아 다른 캐릭터가 이어받는다.</item>
    /// </list>
    /// </summary>
    public static class SightAlertService
    {
        /// <summary>확인해야 할 지점 하나.</summary>
        public class Alert
        {
            public Vector3 Position;

            /// <summary>마지막으로 보고된 시각. 같은 자리에서 계속 맞으면 갱신된다.</summary>
            public float ReportedAt;

            /// <summary>지금 확인하러 가고 있는 캐릭터. 없으면 null.</summary>
            public Object Owner;
        }

        /// <summary>
        /// 동시에 들고 있을 경보 수 상한. 넘으면 <b>가장 오래된 것</b>을 밀어낸다 —
        /// 여러 방향에서 동시에 저격당하는 상황에서 목록이 무한히 자라지 않게 하는 안전장치다.
        /// </summary>
        const int MaxAlerts = 8;

        static readonly List<Alert> _alerts = new List<Alert>(MaxAlerts);

        /// <summary>플레이 모드를 다시 시작할 때 남지 않게 (도메인 리로드 off 대비).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _alerts.Clear();

        public static IReadOnlyList<Alert> All => _alerts;

        /// <summary>
        /// 이 경보가 아직 목록에 있는지. <b>LINQ 를 쓰지 않으려고</b> 직접 둔다 —
        /// 호출부(<c>CharacterBehavior</c>)는 <c>System.Linq</c> 를 안 쓰고, 매 프레임 도는
        /// 경로에서 열거자를 만들 이유도 없다.
        /// </summary>
        public static bool Contains(Alert alert) => alert != null && _alerts.Contains(alert);

        /// <summary>
        /// 안 보이는 적에게 맞았다고 보고한다.
        ///
        /// <paramref name="mergeRadiusTiles"/> 안에 이미 경보가 있으면 <b>새로 만들지 않고
        /// 시각만 갱신</b>한다 — 매 프레임 보고되므로(<c>CharacterBehavior</c> 가 폴링한다)
        /// 합치지 않으면 같은 저격수 하나에 경보가 수백 개 쌓인다.
        /// </summary>
        public static void Report(Vector3 worldPos, float mergeRadiusTiles)
        {
            float mergeSqr = Mathf.Max(0.01f, mergeRadiusTiles * mergeRadiusTiles);

            for (int i = 0; i < _alerts.Count; i++)
            {
                if (((Vector2)(_alerts[i].Position - worldPos)).sqrMagnitude > mergeSqr) continue;

                // 같은 자리 — 시각만 갱신한다. 위치를 옮기면 확인하러 가던 캐릭터의
                // 목적지가 매 프레임 흔들려 제자리에서 떤다.
                _alerts[i].ReportedAt = Time.time;
                return;
            }

            if (_alerts.Count >= MaxAlerts) _alerts.RemoveAt(0);   // 가장 오래된 것부터 밀어낸다
            _alerts.Add(new Alert { Position = worldPos, ReportedAt = Time.time, Owner = null });
        }

        /// <summary>
        /// 수명이 지났거나 <b>이미 눈으로 확인된</b> 경보를 치운다. 죽은 담당자의 소유권도 푼다.
        /// 매 프레임 캐릭터마다 부르지 말고 <b>한 번만</b> 부르면 된다 —
        /// <c>CharacterBehavior</c> 가 시간 간격을 두고 부른다.
        /// </summary>
        public static void Prune(float ttlSeconds, System.Func<Vector3, bool> isVisible)
        {
            float now = Time.time;

            for (int i = _alerts.Count - 1; i >= 0; i--)
            {
                Alert a = _alerts[i];

                // 담당자가 죽어 파괴되면 <b>따로 치울 것이 없다</b> — 유니티의 <c>Object</c> 는
                // 파괴된 뒤 `== null` 이 true 라, 아래 조회들이 저절로 "임자 없음"으로 본다.

                bool seen = isVisible != null && isVisible(a.Position);
                if (seen || now - a.ReportedAt > ttlSeconds) _alerts.RemoveAt(i);
            }
        }

        /// <summary>
        /// <paramref name="from"/> 에서 <paramref name="maxRangeTiles"/> 안의 <b>임자 없는</b>
        /// 경보 중 가장 가까운 것. 없으면 null.
        /// </summary>
        public static Alert FindUnclaimedNearest(Vector3 from, float maxRangeTiles)
        {
            float bestSqr = maxRangeTiles * maxRangeTiles;
            Alert best = null;

            for (int i = 0; i < _alerts.Count; i++)
            {
                Alert a = _alerts[i];
                if (a.Owner != null) continue;

                float sqr = ((Vector2)(a.Position - from)).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                best = a;
            }
            return best;
        }

        /// <summary>이 경보를 내가 맡는다. 이미 다른 캐릭터가 맡고 있으면 false.</summary>
        public static bool TryClaim(Alert alert, Object claimant)
        {
            if (alert == null || claimant == null) return false;
            if (alert.Owner != null && alert.Owner != claimant) return false;

            alert.Owner = claimant;
            return true;
        }

        /// <summary>
        /// 담당을 내려놓는다. <paramref name="resolved"/> 가 true 면 <b>확인을 마친 것</b>이라
        /// 경보 자체를 지운다 — 도착했는데 아무것도 없었던 경우가 이쪽이다
        /// (적이 있었다면 <see cref="Prune"/> 의 "눈으로 확인" 조건이 먼저 지운다).
        /// </summary>
        public static void Release(Alert alert, Object claimant, bool resolved)
        {
            if (alert == null) return;
            if (alert.Owner != null && alert.Owner != claimant) return;

            alert.Owner = null;
            if (resolved) _alerts.Remove(alert);
        }
    }
}
