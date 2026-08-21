using UnityEngine;
using LastSanctuary.Resource;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 캐릭터 생성 규칙 — 비용 계산과 소비만 담당하고, 실제 생성은
    /// <see cref="UnitSpawner.SpawnOneCharacter"/> 에 맡긴다.
    ///
    /// 규칙(비용)과 입력(버튼)을 갈라두는 건 이 프로젝트가 이미 쓰는 구조다 —
    /// <c>CharacterUpgradeService</c> ↔ <c>UpgradeButtonUI</c> 와 같은 모양으로 맞췄다.
    /// 덕분에 버튼을 다시 만들어도 비용 규칙은 그대로 남는다.
    ///
    /// <b>비용 공식(유저 확정)</b>: <c>150 + 100n</c> — <c>n</c> 은 몇 번째로 만드는 캐릭터인지
    /// (1부터, ex: 1 → 2 → 3 …). 캐릭터 성장 기획서 5장의 "생성한 캐릭터 수에 비례하여
    /// 자원 소모량 점진적 상승"을 그대로 수치화한 것 — 문서엔 구체적인 공식이 없어서
    /// 유저가 직접 지정한 값을 그대로 반영한다.
    ///
    ///   비용 = baseCost(150) + costPerCreation(100) × n
    ///
    /// 시작 인원(UnitSpawner 가 게임 시작에 만드는 3명)은 비용 계산에서 빼서,
    /// 첫 추가 생성이 항상 n=1(=250) 부터 시작하게 했다 — 시작 캐릭터는 "생성" 이 아니라
    /// 게임이 처음부터 쥐여주는 인원이라, 기획서가 말하는 "생성한 캐릭터 수"에 포함되지 않는다.
    /// </summary>
    public class CharacterCreationService : MonoBehaviour
    {
        [Header("비용 — 캐릭터 성장 기획서 5장 + 유저 확정 공식 150+100n")]
        [Tooltip("공식의 상수항(150)")]
        [Min(0)] [SerializeField] int baseCost = 150;

        [Tooltip("공식의 n 배율(100) — n 은 몇 번째 생성인지(1부터)")]
        [Min(0)] [SerializeField] int costPerCreation = 100;

        [Header("제한")]
        [Tooltip("이 인원을 넘겨서는 만들 수 없다. 0 이면 제한 없음")]
        [Min(0)] [SerializeField] int maxCharacters = 12;

        [Header("디버그")]
        [SerializeField] bool logCreation = true;

        UnitSpawner _spawner;
        int _startingCount = -1;   // 시작 인원. 첫 조회 때 확정한다

        public static CharacterCreationService Instance { get; private set; }

        /// <summary>생성이 성사될 때마다 (생성된 캐릭터, 소비한 비용).</summary>
        public event System.Action<CharacterUnit, int> OnCreated;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _spawner = FindAnyObjectByType<UnitSpawner>();
            if (_spawner == null)
                Debug.LogWarning("[Create] UnitSpawner 를 찾지 못했습니다. 캐릭터 생성 버튼이 동작하지 않습니다.", this);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 지금 자리를 차지하고 있는 캐릭터 수. 죽으면 오브젝트가 파괴되므로 레지스트리를 세는 게 정확하다.
        ///
        /// ★ <b>부활 대기 중인 캐릭터도 센다</b>(<see cref="CharacterUnit.IsRevivePending"/>) —
        /// 이 값은 <b>인원 상한</b>(<c>AtLimit</c>) 판정에 쓰이는데, 3초 동안 한 칸이 비는 것으로
        /// 보이면 <b>그 틈에 상한을 넘겨 생성</b>할 수 있고 부활한 뒤 정원이 초과된다.
        /// </summary>
        public int AliveCount
        {
            get
            {
                var all = LastSanctuary.Combat.UnitRegistry.All;
                int n = 0;
                for (int i = 0; i < all.Count; i++)
                    // ★ <b>소환수(아루의 골렘)는 세지 않는다</b> (2026-08-21) —
                    //   골렘도 <c>CharacterUnit</c> 이라 예전에는 <b>정원 한 칸을 먹었다</b>.
                    //   골렘은 주인이 죽으면 같이 사라지므로, 캐릭터가 죽는 순간 인원수가
                    //   둘씩 출렁여 «상한에 걸렸다/안 걸렸다» 가 흔들렸다.
                    //   ⚠ 이 프로젝트의 다른 인원 집계 셋(WaveManager·DefeatPanel·VictoryPanel)은
                    //     이미 <c>!IsSummoned</c> 를 쓰고 있었다 — 여기만 빠져 있었다.
                    if (all[i] is CharacterUnit c && !c.IsSummoned &&
                        (c.IsAlive || c.IsRevivePending)) n++;
                return n;
            }
        }

        /// <summary>지금까지 만들어진 캐릭터 수(죽은 것 포함). 비용은 이 값 기준으로 오른다.</summary>
        public int CreatedCount => _spawner != null ? _spawner.SpawnedCharacters.Count : 0;

        /// <summary>다음에 만들 캐릭터가 몇 번째 생성인지(1부터) — 공식의 n.</summary>
        public int NextCreationNumber
        {
            get
            {
                if (_spawner == null) return 1;

                // 시작 인원은 게임이 캐릭터를 다 만든 뒤(첫 프레임 이후)에야 확정된다.
                if (_startingCount < 0 && _spawner.SpawnedCharacters.Count > 0)
                    _startingCount = _spawner.SpawnedCharacters.Count;

                int start = Mathf.Max(0, _startingCount);
                int extra = Mathf.Max(0, _spawner.SpawnedCharacters.Count - start);
                return extra + 1;   // 1번째 생성부터 시작 (ex: 1 → 2 → 3 …)
            }
        }

        /// <summary>다음 캐릭터를 만드는 데 드는 에너지. 150 + 100n.</summary>
        public int CurrentCost => baseCost + costPerCreation * NextCreationNumber;

        /// <summary>인원 상한에 걸렸는지.</summary>
        public bool AtLimit => maxCharacters > 0 && AliveCount >= maxCharacters;

        /// <summary>
        /// 인원 상한(0 이면 제한 없음). 로스터 제목의 «8/12» 분모가 이 값이다
        /// (2026-08-21 · <see cref="CharacterRosterPanel"/>).
        /// </summary>
        public int MaxCharacters => maxCharacters;

        /// <summary>
        /// ★ 더 나올 <b>인물</b>이 없는지 (2026-08-21 · 재등장 금지를 켠 뒤로 생겼다).
        /// 인원 상한과 <b>다른 벽</b>이다 — 자리가 남아도 인물이 없으면 만들 수 없다.
        /// </summary>
        public bool OutOfCandidates => CharacterDefinitionRegistry.Exhausted;

        /// <summary>지금 만들 수 있는지 (스포너가 있고, 상한 미만이고, 에너지가 충분한지).</summary>
        public bool CanCreate
        {
            get
            {
                if (_spawner == null || AtLimit || OutOfCandidates) return false;
                ResourceManager resources = ResourceManager.Instance;
                return resources != null && resources.CanAfford(CurrentCost);
            }
        }

        /// <summary>
        /// 에너지를 소비하고 캐릭터를 한 명 만든다. 부족하거나 상한이면 아무것도 하지 않는다.
        /// <b>비용을 먼저 차감하고 성공했을 때만 생성한다</b> — 순서를 반대로 하면
        /// 에너지가 모자랄 때 캐릭터만 생기는 구멍이 난다.
        /// </summary>
        public CharacterUnit TryCreate()
        {
            if (_spawner == null)
            {
                Debug.LogWarning("[Create] UnitSpawner 가 없어 생성할 수 없습니다.", this);
                return null;
            }

            if (AtLimit)
            {
                HudLog.Add($"인원 상한 {maxCharacters} 명에 도달했습니다", HudLogKind.Warn);
                return null;
            }

            // ★ 인물 소진 — 자리는 남았지만 <b>같은 인물을 두 번 낼 수 없다</b>(재등장 금지).
            //   비용을 깎기 <b>전에</b> 막는다: 환불 경로를 타면 로그가 두 줄 나와 헷갈린다.
            if (OutOfCandidates)
            {
                HudLog.Add("더 등장할 인물이 없습니다", HudLogKind.Warn);
                return null;
            }

            ResourceManager resources = ResourceManager.Instance;
            if (resources == null)
            {
                Debug.LogWarning("[Create] ResourceManager 를 찾을 수 없어 생성할 수 없습니다.", this);
                return null;
            }

            int cost = CurrentCost;
            if (!resources.TrySpend(cost))
            {
                if (logCreation)
                    Debug.Log($"[Create] 에너지 부족 — 생성에 {cost} 필요, 보유 {resources.Energy}");
                HudLog.Add($"에너지 부족 — 생성에 {cost} 필요", HudLogKind.Warn);
                return null;
            }

            CharacterUnit unit = _spawner.SpawnOneCharacter();
            if (unit == null)
            {
                // 생성이 실패했으면 깎은 에너지를 돌려준다.
                resources.AddEnergy(cost);
                Debug.LogWarning("[Create] 생성에 실패해 에너지를 돌려주었습니다.", this);
                return null;
            }

            if (logCreation) Debug.Log($"[Create] {unit.name} 생성 · 비용 {cost} · 다음 비용 {CurrentCost}", unit);
            HudLog.Add($"{unit.name} 생성 (−{cost})", HudLogKind.Good);

            OnCreated?.Invoke(unit, cost);
            return unit;
        }

        void OnValidate()
        {
            if (maxCharacters > 0 && maxCharacters < 1) maxCharacters = 1;
        }
    }
}
