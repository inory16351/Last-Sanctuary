using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중립 몬스터 한 마리가 <b>어느 무리에 속하는지</b>. 스폰될 때 붙는다 (유저 지시 2026-08-15).
    ///
    /// <b>무리의 정의</b> — *"일정 타일 범위 내에서 생성된 같은 개체의 몬스터는 동료로 인식하여
    /// 같은 부대로 묶이고, 같은 부대의 동료 몬스터가 공격 당할 시 반격하는 옵션이 무리 반격 여부"*.
    /// 두 조건이 모두 맞아야 한 무리다:
    ///   ① <b>같은 종</b>(같은 <see cref="NeutralMonsterDefinitionSO"/>)
    ///   ② 스폰 시점에 기존 무리의 <b>중심에서 일정 타일 안</b>
    ///
    /// <b>왜 컴포넌트 하나로 끝나는가</b> — 무리는 "누가 누구를 동료로 보는가" 만 정하면 되고,
    /// 그 판정은 <see cref="UnitCombat"/> 의 동료 구원(ally call) 경로가 이미 갖고 있다.
    /// 여기서는 <b>같은 무리인지</b>만 답해주면 된다. 무리 전용 이동·대형은 만들지 않는다 —
    /// 요청에 없고, 만들면 배회(<see cref="NeutralMonsterWander"/>)와 목적지를 두고 싸운다.
    ///
    /// <b>왜 리더를 두지 않는가</b> — 리더가 죽으면 무리가 흩어지는 처리를 또 만들어야 한다.
    /// <see cref="PackId"/> 라는 번호만 공유하면 누가 죽든 나머지는 그대로 한 무리다
    /// (<c>SquadService</c> 가 캐릭터 부대를 <b>목록</b>으로 들고 있다가 죽은 항목을 지워야 했던
    /// 것과 반대로, 번호 공유는 지울 것이 없다).
    /// </summary>
    [RequireComponent(typeof(NeutralMonsterUnit))]
    public class NeutralPack : MonoBehaviour
    {
        /// <summary>같은 값이면 같은 무리. 0 은 "무리 없음"(혼자)이다.</summary>
        public int PackId { get; private set; }

        /// <summary>이 무리가 동료의 피격에 함께 반응하는가 (표 <c>atk_take</c>).</summary>
        public bool Retaliates { get; private set; }

        /// <summary>무리에 속해 있고 무리 반격도 켜져 있는가.</summary>
        public bool AnswersPackCalls => PackId != 0 && Retaliates;

        NeutralMonsterUnit _unit;

        /// <summary>이 무리의 종. 같은 번호를 다른 종이 쓰지 않게 하는 검증용이다.</summary>
        public NeutralMonsterDefinitionSO Definition =>
            _unit != null ? _unit.Definition : null;

        void Awake() => _unit = GetComponent<NeutralMonsterUnit>();

        /// <summary>스포너가 배치 직후 호출한다.</summary>
        public void Assign(int packId, bool retaliates)
        {
            if (_unit == null) _unit = GetComponent<NeutralMonsterUnit>();
            PackId = packId;
            Retaliates = retaliates;
        }

        // ------------------------------------------------------------------
        // 조회 — UnitCombat 이 "이 유닛이 내 무리인가" 를 물을 때 쓴다
        // ------------------------------------------------------------------

        /// <summary>
        /// 두 유닛이 <b>같은 무리</b>인가. 어느 쪽이든 중립이 아니거나 무리가 없으면 false.
        ///
        /// <c>static</c> 인 이유: 물어보는 쪽(<see cref="UnitCombat"/>)은 상대가 중립인지조차
        /// 모른 채 <see cref="DamageableUnit"/> 두 개만 들고 있다. 여기서 한 번에 판정한다.
        /// </summary>
        public static bool SamePack(DamageableUnit a, DamageableUnit b)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return false;

            NeutralPack pa = Of(a);
            if (pa == null || pa.PackId == 0) return false;

            NeutralPack pb = Of(b);
            return pb != null && pb.PackId == pa.PackId;
        }

        /// <summary>
        /// 유닛에 붙은 무리 정보. 없으면 null.
        ///
        /// <b>캐시하지 않는다</b> — 부르는 쪽(동료 구원 루프)이 이미 후보를 거리로 걸러낸
        /// 뒤라 호출 횟수가 적고, 캐시를 두면 유닛이 파괴될 때 비워줄 곳이 또 생긴다.
        /// </summary>
        public static NeutralPack Of(DamageableUnit unit) =>
            unit is NeutralMonsterUnit ? unit.GetComponent<NeutralPack>() : null;

        // ------------------------------------------------------------------
        // 번호 발급
        // ------------------------------------------------------------------

        static int _nextPackId = 1;

        /// <summary>새 무리 번호를 하나 발급한다.</summary>
        public static int NewPackId() => _nextPackId++;

        /// <summary>
        /// 도메인 리로드를 꺼도 번호가 이어지지 않도록 초기화한다
        /// (<see cref="UnitRegistry"/> · <c>SquadService</c> 와 같은 프로젝트 규칙).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _nextPackId = 1;

        // ------------------------------------------------------------------
        // 무리 찾기 — 스포너가 배치 직전에 부른다
        // ------------------------------------------------------------------

        static readonly List<NeutralPack> _scratch = new List<NeutralPack>();

        /// <summary>
        /// <paramref name="position"/> 근처에 이미 있는 <b>같은 종</b>의 무리를 찾는다.
        /// 자리가 남은 무리가 있으면 그 번호를, 없으면 0(= 새 무리를 내야 함)을 돌려준다.
        ///
        /// <b>정원(<paramref name="maxMembers"/>)을 세는 방식</b> — 지금 살아있는 개체만 센다.
        /// 무리가 사냥당해 줄어들면 그 자리에 다시 채워지는 게 자연스럽다(정글 캠프가
        /// 리스폰되는 것과 같은 그림이다).
        /// </summary>
        /// <param name="mergeRadiusTiles">이 거리 안의 동족을 같은 무리로 본다.</param>
        public static int FindNearbyPack(NeutralMonsterDefinitionSO definition, Vector3 position,
                                         float mergeRadiusTiles, int maxMembers)
        {
            if (definition == null || maxMembers <= 0 || mergeRadiusTiles <= 0f) return 0;

            float sqr = mergeRadiusTiles * mergeRadiusTiles;

            // 후보 무리별 (가장 가까운 거리², 인원). 무리 수가 많아야 수십이라 리스트로 충분하다.
            _scratch.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is NeutralMonsterUnit n) || !n.IsAlive) continue;
                if (n.Definition != definition) continue;                 // ① 같은 종만

                NeutralPack pack = n.GetComponent<NeutralPack>();
                if (pack == null || pack.PackId == 0) continue;

                _scratch.Add(pack);
            }

            int bestId = 0;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < _scratch.Count; i++)
            {
                NeutralPack pack = _scratch[i];
                float d = ((Vector2)(pack.transform.position - position)).sqrMagnitude;
                if (d > sqr) continue;                                     // ② 일정 타일 안만
                if (d >= bestSqr) continue;
                if (CountMembers(pack.PackId) >= maxMembers) continue;     // 정원이 찼다

                bestId = pack.PackId;
                bestSqr = d;
            }

            _scratch.Clear();
            return bestId;
        }

        /// <summary>지금 살아있는 무리원 수.</summary>
        public static int CountMembers(int packId)
        {
            if (packId == 0) return 0;

            int n = 0;
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is NeutralMonsterUnit u) || !u.IsAlive) continue;
                NeutralPack pack = u.GetComponent<NeutralPack>();
                if (pack != null && pack.PackId == packId) n++;
            }
            return n;
        }
    }
}
