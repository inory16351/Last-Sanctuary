using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Relics
{
    /// <summary>
    /// <c>Resources/Relics</c> 의 유물 정의를 한 번 읽어 들고 있는 <b>사전</b>.
    ///
    /// <b>왜 정적 클래스인가</b> — 유물 정의는 «판마다 달라지지 않는 데이터» 이고,
    /// 이 프로젝트의 다른 정의 사전(<see cref="LastSanctuary.Units.CharacterDefinitionRegistry"/>)도
    /// 같은 모양이다. 씬에 오브젝트를 하나 더 두면 «누가 먼저 깨어나는가» 문제가 생긴다.
    ///
    /// ⚠ <b>등록기가 «구멍» 을 알린다</b> — 표에는 있는데 코드에 가지가 없는 효과 타입
    ///   (<see cref="RelicEffectType.None"/>)이 섞이면 그 유물은 «장착은 되는데 아무 일도
    ///   안 일어나는» 것이 된다. 조용히 넘어가면 못 찾으므로 <b>한 번 경고</b>한다.
    /// </summary>
    public static class RelicRegistry
    {
        const string ResourceFolder = "Relics";

        static RelicDefinitionSO[] _all;
        static Dictionary<int, RelicDefinitionSO> _byId;

        /// <summary>보스 몬스터 ID → 그 보스의 고유 에픽 유물.</summary>
        static Dictionary<int, RelicDefinitionSO> _byBoss;

        /// <summary>등급별 «발굴·일반 몹» 풀 (보스 전용은 빠진다).</summary>
        static Dictionary<RelicGrade, List<RelicDefinitionSO>> _commonPool;

        /// <summary>등급별 «발굴에서만» 풀 — 지금은 에픽 둘뿐이다.</summary>
        static Dictionary<RelicGrade, List<RelicDefinitionSO>> _digOnlyPool;

        public static IReadOnlyList<RelicDefinitionSO> All
        {
            get { EnsureLoaded(); return _all; }
        }

        public static RelicDefinitionSO ById(int relicId)
        {
            EnsureLoaded();
            return _byId.TryGetValue(relicId, out var r) ? r : null;
        }

        /// <summary>이 보스가 떨구는 고유 에픽 유물. 없으면 null.</summary>
        public static RelicDefinitionSO ForBoss(int monsterId)
        {
            EnsureLoaded();
            return _byBoss.TryGetValue(monsterId, out var r) ? r : null;
        }

        /// <summary>
        /// 이 등급에서 하나 뽑는다. <paramref name="digOnly"/> 가 참이면 <b>발굴 전용 풀</b>에서
        /// 뽑는다(발굴 결과 <c>dig_relic_epic</c> 전용 · 표 Info 시트 «③ 보스» 항목).
        /// 풀이 비어 있으면 null.
        ///
        /// ★★★ <b>이미 가진 유물은 빼고 뽑는다</b> (2026-08-25 · 유저 지시:
        /// *"유물 중복 획득 안되게 수정해줘"*).
        ///
        /// ★ <b>«뽑고 나서 버리는» 방식이 아니다.</b> 그러면 다 모을수록 «아무것도 안 나오는»
        ///   판정이 늘어 <b>체감 확률이 조용히 떨어진다</b>. 후보에서 <b>먼저 빼고</b> 가중치를
        ///   다시 합하면 남은 것들 사이의 비율이 표 그대로 유지된다.
        /// ⚠ 남은 후보가 없으면 <c>null</c> — 부르는 쪽이 «다 모았습니다» 로 알린다.
        ///   조용히 넘어가면 유저는 «드랍이 고장났다» 로 읽는다.
        /// ⚠ <paramref name="excludeOwned"/> 를 <c>false</c> 로 주면 옛 동작이다(치트·시험용).
        /// </summary>
        public static RelicDefinitionSO RollGrade(RelicGrade grade, bool digOnly = false,
                                                  bool excludeOwned = true)
        {
            EnsureLoaded();
            var table = digOnly ? _digOnlyPool : _commonPool;
            if (!table.TryGetValue(grade, out var pool) || pool.Count == 0) return null;

            RelicInventory inv = excludeOwned ? RelicInventory.Instance : null;

            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (IsOwned(inv, pool[i])) continue;
                total += Mathf.Max(1, pool[i].dropWeight);
            }
            if (total <= 0) return null;              // 이 등급은 이미 다 모았다

            int pick = Random.Range(0, total);
            RelicDefinitionSO last = null;
            for (int i = 0; i < pool.Count; i++)
            {
                if (IsOwned(inv, pool[i])) continue;
                last = pool[i];
                pick -= Mathf.Max(1, pool[i].dropWeight);
                if (pick < 0) return pool[i];
            }
            return last;
        }

        /// <summary>이미 가지고 있는가. 보관함이 없으면(로비·테스트) 언제나 <c>false</c>.</summary>
        static bool IsOwned(RelicInventory inv, RelicDefinitionSO relic) =>
            inv != null && relic != null && inv.OwnedCount(relic.relicId) > 0;

        /// <summary>에디터에서 도메인 리로드 없이 다시 읽고 싶을 때.</summary>
        public static void Reload()
        {
            _all = null;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (_all != null) return;

            _all = Resources.LoadAll<RelicDefinitionSO>(ResourceFolder) ?? new RelicDefinitionSO[0];
            _byId = new Dictionary<int, RelicDefinitionSO>(_all.Length);
            _byBoss = new Dictionary<int, RelicDefinitionSO>();
            _commonPool = new Dictionary<RelicGrade, List<RelicDefinitionSO>>();
            _digOnlyPool = new Dictionary<RelicGrade, List<RelicDefinitionSO>>();

            int broken = 0;
            for (int i = 0; i < _all.Length; i++)
            {
                RelicDefinitionSO r = _all[i];
                if (r == null || r.relicId <= 0) continue;

                if (!_byId.ContainsKey(r.relicId)) _byId.Add(r.relicId, r);
                else Debug.LogWarning($"[유물] ID 중복 {r.relicId} — {r.name} 은(는) 무시합니다.");

                if (r.effectType == RelicEffectType.None) broken++;

                if (r.grade == RelicGrade.None) continue;

                switch (r.source)
                {
                    case RelicSource.Boss:
                        if (r.sourceId > 0) _byBoss[r.sourceId] = r;
                        break;
                    case RelicSource.DigOnly:
                        Bucket(_digOnlyPool, r.grade).Add(r);
                        break;

                    // ★★ <b>사건 전용은 어느 풀에도 넣지 않는다</b> (2026-08-24).
                    //   아래 default 가 «일반 풀»(발굴·처치 드랍)이라, 이 가지가 없으면
                    //   사건 보상 전용 유물이 <b>조용히 발굴에서 튀어나온다</b>.
                    //   주는 통로는 EventRewardService 의 relic_gain 하나뿐이다.
                    case RelicSource.Event:
                        break;

                    default:
                        Bucket(_commonPool, r.grade).Add(r);
                        break;
                }
            }

            // ⚠ 한 번만 알린다 — 매 판 뜨면 로그가 묻힌다.
            if (broken > 0)
                Debug.LogWarning($"[유물] 효과 타입을 못 읽은 유물이 {broken}개 있습니다 — " +
                                 "표 EffectType 시트에는 있는데 RelicEffectType enum 에 " +
                                 "가지가 없는 값일 수 있습니다(그 유물은 장착해도 아무 일이 없습니다).");

            if (_all.Length == 0)
                Debug.LogWarning("[유물] Resources/Relics 가 비어 있습니다 — " +
                                 "py -3 Tools/gen_relic_assets.py 를 돌려주세요.");
        }

        static List<RelicDefinitionSO> Bucket(
            Dictionary<RelicGrade, List<RelicDefinitionSO>> table, RelicGrade g)
        {
            if (!table.TryGetValue(g, out var list))
            {
                list = new List<RelicDefinitionSO>();
                table[g] = list;
            }
            return list;
        }
    }
}
