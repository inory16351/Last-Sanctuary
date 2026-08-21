using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 캐릭터 정의 후보 풀. <c>Resources/Characters/</c> 안의 모든
    /// <see cref="CharacterDefinitionSO"/> 를 읽어 두고, 생성할 때마다 하나를 골라준다.
    ///
    /// ══════════════════════════════════════════════════════════════════════
    ///  ★★★ <b>등장 규칙이 «살아 있는 동안만 중복 금지» 로 바뀌었다</b> (2026-08-21)
    /// ══════════════════════════════════════════════════════════════════════
    /// 유저 리포트: *"캐릭터가 죽으면 해당 캐릭터가 생성되어야 하는데 지금 생성이 안됌 →
    /// 히스톤 캐릭터가 죽으면 히스톤 캐릭터가 생성되어야되는데 생성이 안되는 버그"*.
    ///
    /// <b>무엇이 어긋났나</b> — 예전 규칙은 «이 판에 <b>한 번이라도</b> 등장한 id» 를 기록하고
    /// <b>절대 빼지 않는</b> 것이었다(2026-08-11 확정: «사망하더라도 다시 등장할 수 없다»).
    /// 그래서 히스톤이 죽으면 히스톤은 <b>그 판이 끝날 때까지</b> 다시 뽑히지 않았다.
    ///
    /// ★ <b>두 지시를 함께 만족시키는 읽기</b> — 2026-08-21 의 *"같은 캐릭터 중복 생성
    ///   안되게"* 는 «히스톤이 <b>둘</b> 있으면 안 된다» 는 뜻이고, 이번 지시는 «죽었으면
    ///   다시 뽑혀야 한다» 는 뜻이다. 둘을 모두 지키는 기준은 하나다:
    ///
    ///       <b>«지금 살아 있는(또는 부활 대기 중인) 인물» 만 후보에서 뺀다.</b>
    ///
    ///   · 히스톤이 살아 있으면 → 둘째 히스톤은 안 나온다 (중복 금지 ✓)
    ///   · 히스톤이 죽으면      → 히스톤이 후보로 <b>돌아온다</b> (이번 지시 ✓)
    ///
    /// ⚠ <b>부활 대기(<see cref="CharacterUnit.IsRevivePending"/>)도 «살아 있다» 로 센다</b> —
    ///   히스톤의 「분노」가 되살릴 시체를 «죽었다» 로 보면 그 3초 사이에 <b>둘째 히스톤</b>을
    ///   만들 수 있고, 부활한 순간 같은 인물이 둘이 된다.
    ///
    /// ★★ <b>판 전역 <c>static</c> 기록이 사라졌다.</b> 예전에는 «등장한 id» 집합을 들고
    ///   있어서, 씬을 다시 열어도 살아남아 «캐릭터 생성이 영구히 잠기는» 버그를 만들었다
    ///   (그 때문에 <c>ResetRun()</c>·<c>MarkAppeared()</c>·<see cref="Save.RunResetService"/> 가
    ///   필요했다). 이제 <b>기준이 «지금 씬에 살아 있는 유닛» 이라</b> 판이 바뀌면 저절로
    ///   비어 있다 — 그 버그의 <b>뿌리</b>가 없어진 것이다.
    ///
    /// 스프라이트·에셋 참조를 MCP 로 씬에 넣을 수 없다는 제약(진행상황 8절 1번) 때문에,
    /// 씬에 배선하지 않고 <c>Resources</c> 경로로 읽는다 — <c>CharacterAnimator</c> 의 스킨 로딩과 같은 패턴.
    /// </summary>
    public static class CharacterDefinitionRegistry
    {
        const string ResourceFolder = "Characters";

        /// <summary>
        /// 켜면 <b>지금 살아 있는(부활 대기 포함) 인물</b>은 후보에서 빠진다 —
        /// 즉 같은 인물이 동시에 둘 있을 수 없다. 죽으면 <b>다시 후보가 된다</b>.
        /// 끄면 같은 인물이 여럿 나올 수 있다(테스트용).
        /// </summary>
        public static bool preventDuplicateWhileAlive = true;

        static CharacterDefinitionSO[] _all;

        /// <summary>
        /// 지금 <b>쓰이고 있는</b> 인물 id — 살아 있거나 부활 대기 중인 캐릭터의 것.
        /// 매번 새로 담는다(<see cref="HashSet{T}"/> 를 재사용해 할당을 피한다).
        ///
        /// ⚠ <b>소환수는 세지 않는다</b> — 아루의 골렘은 인물 정의를 쓰지 않는다
        ///   (런타임에 만든 정의라 <c>characterId</c> 가 표의 대역 밖이다). 그래도
        ///   <c>IsSummoned</c> 로 확실히 걸러 «골렘 때문에 누가 안 나온다» 를 막는다.
        /// </summary>
        static readonly HashSet<int> _inUse = new HashSet<int>();

        static void RefreshInUse()
        {
            _inUse.Clear();

            var all = Combat.UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is not CharacterUnit c) continue;
                if (c.IsSummoned) continue;
                if (!c.IsAlive && !c.IsRevivePending) continue;
                if (c.Definition == null) continue;

                _inUse.Add(c.Definition.characterId);
            }
        }

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

            if (preventDuplicateWhileAlive)
            {
                RefreshInUse();

                var fresh = new List<CharacterDefinitionSO>(all.Length);
                foreach (var d in all)
                    if (!_inUse.Contains(d.characterId)) fresh.Add(d);

                if (fresh.Count == 0)
                {
                    Debug.LogWarning("[Character] 인물 정의를 모두 쓰고 있습니다 — " +
                                     $"{all.Length}명이 전부 살아 있습니다. " +
                                     "누군가 죽으면 그 인물이 다시 후보가 됩니다.");
                    return null;
                }
                pool = fresh;
            }

            // ★ 기록을 남기지 않는다 — 후보에서 빠지는 기준이 «살아 있는가» 이므로
            //   방금 만든 캐릭터는 <b>존재 자체로</b> 다음 추첨에서 빠진다(맨 위 ★★).
            return pool.Count == 1 ? pool[0] : pool[rng.Next(pool.Count)];
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
        /// ★ <b>더 나올 인물이 남아 있지 않은가</b> (2026-08-21 개정).
        ///
        /// <see cref="Pick"/> 이 <b>null 을 돌려줄 이유가 두 가지</b>라서 필요하다:
        /// <list type="number">
        /// <item>정의 에셋을 하나도 못 읽었다 → 예전처럼 <b>능력치 무작위</b>로 만드는 것이 맞다
        ///       (그래야 에셋이 없어도 게임이 돈다)</item>
        /// <item>인물을 <b>전부 쓰고 있다</b> → 만들면 «이름 없는 캐릭터» 가 나온다.
        ///       이때는 <b>만들지 않아야</b> 한다</item>
        /// </list>
        ///
        /// ⚠ 이제 «소진» 은 <b>영구</b>가 아니라 <b>지금 상태</b>다 — 누군가 죽으면 다시 거짓이 된다.
        /// </summary>
        public static bool Exhausted
        {
            get
            {
                var all = All;
                if (all.Length == 0) return false;              // ① 정의가 아예 없다 — 소진이 아니다
                if (!preventDuplicateWhileAlive) return false;  // 중복이 허용되면 마를 일이 없다

                RefreshInUse();
                for (int i = 0; i < all.Length; i++)
                    if (!_inUse.Contains(all[i].characterId)) return false;
                return true;
            }
        }

        /// <summary>지금 <b>뽑을 수 있는</b> 인물의 수 (UI 가 «남은 인물 N» 을 쓰고 싶을 때).</summary>
        public static int RemainingCount
        {
            get
            {
                var all = All;
                if (all.Length == 0 || !preventDuplicateWhileAlive) return all.Length;

                RefreshInUse();
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                    if (!_inUse.Contains(all[i].characterId)) n++;
                return n;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _all = null;
            _inUse.Clear();
        }
    }
}
