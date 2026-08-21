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
    /// ★★ <b>2026-08-21 — 켰다</b>(유저 지시: *"같은 캐릭터 중복 생성 안되게 설정"*).
    /// 켜기만 하면 되도록 처음부터 만들어 둔 스위치다(<see cref="preventReappearance"/>) —
    /// 캐릭터가 2명뿐이던 때는 3번째 생성부터 막혀서 꺼 뒀고, 지금은 <b>11명</b>이라 켤 수 있다.
    /// ⚠ 그래서 <b>한 판에 나올 수 있는 인물은 정의 수(11)가 상한</b>이다 —
    ///   인원 상한(<c>CharacterCreationService.maxCharacters</c> = 12)보다 이쪽이 먼저 걸린다.
    ///   다 소진되면 <see cref="Exhausted"/> 가 참이 되고 생성이 «더 나올 인물이 없다» 로
    ///   막힌다 — 예전처럼 «능력치만 무작위인 무명 캐릭터» 로 떨어지지 않는다.
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
        public static bool preventReappearance = true;

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

        /// <summary>
        /// id 로 정의를 찾는다 (저장 복원 전용 — 98절). 저장 파일은 <b>에셋 이름이 아니라 id</b> 를
        /// 적는다 — 파일명은 언제든 바뀌지만 id 는 캐릭터 테이블이 정한 정본이기 때문이다.
        /// </summary>
        public static CharacterDefinitionSO ById(int characterId)
        {
            if (characterId == 0) return null;

            var all = All;
            for (int i = 0; i < all.Length; i++)
                if (all[i].characterId == characterId) return all[i];

            Debug.LogWarning($"[Character] 저장된 캐릭터 id {characterId} 의 정의를 찾지 못했습니다. " +
                             "능력치만 복원합니다.");
            return null;
        }

        /// <summary>
        /// 복원한 캐릭터를 "이미 등장했다"로 표시한다. <see cref="preventReappearance"/> 를 켰을 때
        /// 불러온 캐릭터가 <b>다시 뽑히는 것</b>을 막는다 — 표시하지 않으면 같은 인물이 두 명 생긴다.
        /// </summary>
        public static void MarkAppeared(int characterId)
        {
            if (characterId != 0) _spawned.Add(characterId);
        }

        /// <summary>이 판에 이미 등장했는가 (사망자 포함).</summary>
        public static bool HasAppeared(int characterId) => _spawned.Contains(characterId);

        /// <summary>
        /// ★ <b>더 나올 인물이 남아 있지 않은가</b> (2026-08-21).
        ///
        /// <see cref="Pick"/> 이 <b>null 을 돌려줄 이유가 두 가지</b>라서 필요해졌다:
        /// <list type="number">
        /// <item>정의 에셋을 하나도 못 읽었다 → 예전처럼 <b>능력치 무작위</b>로 만드는 것이 맞다
        ///       (그래야 에셋이 없어도 게임이 돈다)</item>
        /// <item>재등장 금지로 <b>다 써버렸다</b> → 만들면 «이름 없는 캐릭터» 가 나온다.
        ///       이때는 <b>만들지 않아야</b> 한다</item>
        /// </list>
        /// 부르는 쪽이 그 둘을 구분하려면 이 값이 있어야 한다.
        /// </summary>
        public static bool Exhausted
        {
            get
            {
                var all = All;
                if (all.Length == 0) return false;          // ① 정의가 아예 없다 — 소진이 아니다
                if (!preventReappearance) return false;     // 중복이 허용되면 마를 일이 없다

                for (int i = 0; i < all.Length; i++)
                    if (!_spawned.Contains(all[i].characterId)) return false;
                return true;
            }
        }

        /// <summary>아직 등장하지 않은 인물의 수 (UI 가 «남은 인물 N» 을 쓰고 싶을 때).</summary>
        public static int RemainingCount
        {
            get
            {
                var all = All;
                if (all.Length == 0 || !preventReappearance) return all.Length;
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                    if (!_spawned.Contains(all[i].characterId)) n++;
                return n;
            }
        }

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
