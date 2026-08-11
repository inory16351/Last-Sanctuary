using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 캐릭터 정의 후보 풀. <c>Resources/Characters/</c> 안의 모든
    /// <see cref="CharacterDefinitionSO"/> 를 읽어 두고, 생성할 때마다 하나를 골라준다.
    ///
    /// <b>등장 규칙 (유저 확정 2026-08-11)</b>
    /// 캐릭터는 한 판에 한 번만 등장하며 <b>사망하더라도 다시 등장할 수 없다.</b>
    /// 그래서 "살아있는 캐릭터"가 아니라 <b>"이 판에 한 번이라도 등장한 id"</b>(<see cref="_spawned"/>)를
    /// 기록하고, 그 집합에서는 절대 원소를 빼지 않는다 — 죽어도 남아 있어야 재등장이 막힌다.
    ///
    /// 지금은 유저 지시대로 <see cref="preventReappearance"/> 가 <b>꺼져 있어</b>
    /// 중복 등장이 허용된다(캐릭터가 2명뿐이라 켜면 3번째 생성부터 막힌다).
    /// 캐릭터를 더 추가한 뒤 이 값만 켜면 규칙이 그대로 적용된다.
    ///
    /// 스프라이트·에셋 참조를 MCP 로 씬에 넣을 수 없다는 제약(진행상황 8절 1번) 때문에,
    /// 씬에 배선하지 않고 <c>Resources</c> 경로로 읽는다 — <c>CharacterAnimator</c> 의 스킨 로딩과 같은 패턴.
    /// </summary>
    public static class CharacterDefinitionRegistry
    {
        const string ResourceFolder = "Characters";

        /// <summary>
        /// 켜면 "이 판에 이미 등장한 인물"은 후보에서 빠진다(사망자 포함).
        /// 유저 지시로 지금은 꺼둔다 — 캐릭터가 더 추가되면 켤 것.
        /// </summary>
        public static bool preventReappearance = false;

        static CharacterDefinitionSO[] _all;
        static readonly HashSet<int> _spawned = new HashSet<int>();

        /// <summary>사용 가능한 정의 전부. 없으면 빈 배열.</summary>
        public static CharacterDefinitionSO[] All
        {
            get
            {
                if (_all != null) return _all;

                var loaded = Resources.LoadAll<CharacterDefinitionSO>(ResourceFolder);
                var usable = new List<CharacterDefinitionSO>(loaded.Length);
                foreach (var d in loaded)
                    if (d != null && d.IsUsable) usable.Add(d);

                // 로드 순서가 플랫폼마다 다를 수 있어 id 로 정렬해 결정적으로 만든다
                usable.Sort((a, b) => a.characterId.CompareTo(b.characterId));
                _all = usable.ToArray();

                if (_all.Length == 0)
                    Debug.LogWarning($"[Character] Resources/{ResourceFolder} 에 쓸 수 있는 캐릭터 정의가 없습니다. " +
                                     "능력치를 무작위로 굴리는 기존 방식으로 넘어갑니다.");
                return _all;
            }
        }

        /// <summary>
        /// 다음에 생성할 캐릭터를 고른다. 후보가 없으면 null 을 돌려주고,
        /// 호출부(<c>UnitSpawner</c>)는 그때 기존의 무작위 능력치 롤로 넘어간다.
        /// </summary>
        public static CharacterDefinitionSO Pick(System.Random rng)
        {
            var all = All;
            if (all.Length == 0) return null;

            IList<CharacterDefinitionSO> pool = all;

            if (preventReappearance)
            {
                var fresh = new List<CharacterDefinitionSO>(all.Length);
                foreach (var d in all)
                    if (!_spawned.Contains(d.characterId)) fresh.Add(d);

                if (fresh.Count == 0)
                {
                    Debug.LogWarning("[Character] 등장 가능한 캐릭터를 모두 소진했습니다. " +
                                     "캐릭터는 사망해도 재등장하지 않습니다.");
                    return null;
                }
                pool = fresh;
            }

            var picked = pool.Count == 1 ? pool[0] : pool[rng.Next(pool.Count)];
            _spawned.Add(picked.characterId);   // 죽어도 지우지 않는다 — 재등장 금지의 핵심
            return picked;
        }

        /// <summary>이 판에 이미 등장했는가 (사망자 포함).</summary>
        public static bool HasAppeared(int characterId) => _spawned.Contains(characterId);

        /// <summary>
        /// 새 게임을 시작할 때 등장 기록을 비운다.
        /// 도메인 리로드를 꺼도 static 이 남으므로 플레이 시작 시 반드시 한 번 호출돼야 한다.
        /// </summary>
        public static void ResetRun() => _spawned.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _all = null;
            _spawned.Clear();
        }
    }
}
