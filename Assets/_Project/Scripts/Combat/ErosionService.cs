using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 침식(Erosion) · 정신 이상 시스템의 규칙과 수치를 한 곳에 모은 서비스.
    /// 씬의 <c>GameSystems</c> 오브젝트에 붙어 있다 — <see cref="CharacterUpgradeService"/> ·
    /// <c>CharacterCreationService</c> 와 같은 자리·같은 패턴(규칙은 서비스, 상태는 유닛)이다.
    ///
    /// <b>침식 규칙(유저 확정)</b>: 캐릭터가 <b>웨이브 몬스터(<see cref="Faction.Cancer"/>)와 전투
    /// 중일 때만</b> 침식이 지속해서 쌓인다. 중립 몬스터 사냥은 침식을 올리지 않는다 — 침식은
    /// "웨이브의 압박"을 표현하는 수치이고, 중립 사냥은 정비 시간의 자원 활동이라 성격이 다르다.
    /// 전투에서 벗어나면 잠시 뒤부터 회복한다. 상한(<see cref="erosionMax"/>)에 닿으면
    /// 테이블 가중치로 정신 이상 한 종류를 추첨해 발동시키고, 침식을 그 종류의
    /// <see cref="MentalErrorDefinitionSO.afterErosion"/> 값으로 떨어뜨린다.
    ///
    /// <b>획득·회복 속도는 전부 인스펙터 값이다</b>(유저 요청: "임의로 지정하되 에딧모드에서
    /// 변경 가능하게"). 기획 테이블에 이 두 값의 근거가 없어서 아래 기본값은 이번에 정한 것이다 —
    /// 전투 타이머 120초 중 절반 정도를 교전한다고 보면 대략 웨이브당 한 번 발동하는 속도다.
    ///
    /// <b>왜 캐릭터마다 Update 를 돌리지 않는가</b> — 상태는 <see cref="CharacterErosion"/> 이
    /// 캐릭터별로 들고 있지만, 진행은 이 서비스가 한 루프에서 몰아서 돌린다. 캐릭터는 런타임에
    /// 프레임 중간에 생성되므로(<c>CharacterCreationService</c>) 컴포넌트의 <c>Start</c> 가
    /// <c>Update</c> 보다 늦게 도는 함정이 있는데(진행상황 27-9·28-4절에서 두 번 겪었다),
    /// 진행을 서비스가 밀어주면 그 순서 문제 자체가 생기지 않는다. 컴포넌트도 여기서
    /// 붙여준다(<c>NeutralMonsterSpawner</c> 가 <c>NeutralMonsterWander</c> 를 붙이는 것과 같은 패턴).
    /// </summary>
    public class ErosionService : MonoBehaviour
    {
        [Header("침식 획득 — 웨이브 몬스터와 전투 중 (초당)")]
        [Tooltip("웨이브 몬스터와 교전 중일 때 1초에 쌓이는 침식량. 기본 1.5 = 상한 100 까지 약 67초")]
        [Min(0f)] [SerializeField] float erosionPerSecondInCombat = 1.5f;

        [Tooltip("마지막으로 웨이브 몬스터에게 맞은 뒤 이 시간(초)까지는 계속 '전투 중'으로 본다. " +
                 "타겟을 놓친 순간마다 침식이 끊기지 않게 하는 여유값")]
        [Min(0f)] [SerializeField] float waveCombatMemorySeconds = 3f;

        [Header("침식 회복 — 전투에서 벗어난 뒤 (초당)")]
        [Tooltip("전투에서 벗어난 뒤 회복이 시작되기까지의 대기 시간(초). " +
                 "체력 재생의 outOfCombatRegenDelay 와 같은 결로 5초를 기본값으로 뒀다")]
        [Min(0f)] [SerializeField] float recoverDelaySeconds = 5f;

        [Tooltip("회복이 시작된 뒤 1초에 줄어드는 침식량. 기본 1.0 = 정비 100초에 100 회복")]
        [Min(0f)] [SerializeField] float erosionRecoverPerSecond = 1f;

        [Header("발동")]
        [Tooltip("침식 상한. 이 값에 닿으면 정신 이상이 발동한다")]
        [Min(1)] [SerializeField] int erosionMax = 100;

        [Tooltip("정신 이상이 이미 발동 중인 캐릭터는 다음 발동을 하지 않는다(중첩 방지). " +
                 "끄면 지속 중에도 상한에 닿는 대로 새 종류가 덮어쓴다")]
        [SerializeField] bool blockWhileActive = true;

        [Tooltip("즉발(지속시간 0) 종류가 로스터·로그에 남아 보이는 시간(초). " +
                 "순수 표시용 — 효과는 이미 한 번에 끝나 있다")]
        [Min(0f)] [SerializeField] float instantStateDisplaySeconds = 3f;

        [Header("랜덤")]
        [Tooltip("같은 시드 = 항상 같은 추첨 결과. 밸런싱 테스트에 필요")]
        [SerializeField] int seed = 20260810;

        [Tooltip("켜면 실행할 때마다 다른 종류가 뽑힌다")]
        [SerializeField] bool randomizeSeed = true;

        [Header("디버그")]
        [Tooltip("정신 이상 발동을 콘솔에도 남긴다(HUD 로그는 이 값과 무관하게 항상 남는다)")]
        [SerializeField] bool logMentalErrors = true;

        [Tooltip("끄면 침식이 쌓이지 않고 정신 이상도 발동하지 않는다 — 밸런스 테스트용 " +
                 "(이미 발동 중인 상태는 지속시간이 끝나면 정상적으로 해제된다)")]
        [SerializeField] bool enableErosion = true;

        /// <summary>정신 이상 정의 에셋을 읽는 Resources 폴더 이름.</summary>
        [Tooltip("정신 이상 정의 에셋이 든 Resources 폴더 이름")]
        [SerializeField] string definitionResourceFolder = "MentalErrors";

        public static ErosionService Instance { get; private set; }

        readonly List<MentalErrorDefinitionSO> _definitions = new List<MentalErrorDefinitionSO>();
        System.Random _rng;
        float _probabilityTotal;
        bool _warnedMissing;

        // ── 인스펙터 값 읽기 전용 노출 (CharacterErosion 이 매 프레임 읽는다) ─────────

        public float ErosionPerSecondInCombat => erosionPerSecondInCombat;
        public float WaveCombatMemorySeconds => waveCombatMemorySeconds;
        public float RecoverDelaySeconds => recoverDelaySeconds;
        public float ErosionRecoverPerSecond => erosionRecoverPerSecond;
        public int ErosionMax => Mathf.Max(1, erosionMax);
        public bool BlockWhileActive => blockWhileActive;
        public float InstantStateDisplaySeconds => instantStateDisplaySeconds;
        public bool LogMentalErrors => logMentalErrors;
        public bool EnableErosion => enableErosion;

        /// <summary>정신 이상이 발동할 때마다 발생 (대상, 발동한 종류). UI·연출이 구독할 수 있다.</summary>
        public static event System.Action<CharacterUnit, MentalErrorDefinitionSO> OnMentalErrorTriggered;

        void Awake()
        {
            Instance = this;
            _rng = new System.Random(randomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : seed);
            LoadDefinitions();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LoadDefinitions()
        {
            _definitions.Clear();
            _probabilityTotal = 0f;

            MentalErrorDefinitionSO[] loaded =
                Resources.LoadAll<MentalErrorDefinitionSO>(definitionResourceFolder);

            for (int i = 0; i < loaded.Length; i++)
            {
                MentalErrorDefinitionSO def = loaded[i];
                if (def == null || def.type == MentalErrorType.None) continue;
                if (def.activationProbability <= 0f) continue;

                _definitions.Add(def);
                _probabilityTotal += def.activationProbability;
            }

            // 표시 순서를 id 순으로 고정한다 — Resources.LoadAll 은 순서를 보장하지 않아서,
            // 로그·디버그 출력이 실행마다 뒤바뀌면 읽기 어렵다. 추첨 자체는 순서와 무관하다.
            _definitions.Sort((a, b) => a.mentalErrorId.CompareTo(b.mentalErrorId));

            if (_definitions.Count == 0)
                Debug.LogWarning($"[침식] Resources/{definitionResourceFolder} 에서 정신 이상 정의를 " +
                                 "찾지 못했습니다. 침식은 쌓이지만 정신 이상이 발동하지 않습니다.", this);
            else if (logMentalErrors)
                Debug.Log($"[침식] 정신 이상 {_definitions.Count}종 로드 · 가중치 합 {_probabilityTotal:0.####}", this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // UnitRegistry 는 살아있는 유닛 전체를 이미 들고 있다 — FindObjectsByType 을 돌지 않는다(U-D10).
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var character = all[i] as CharacterUnit;
                if (character == null || !character.IsAlive) continue;

                CharacterErosion erosion = CharacterErosion.EnsureOn(character);
                if (erosion != null) erosion.Tick(this, dt);
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 발동할 정신 이상 한 종류를 가중치로 뽑는다. 테이블의 확률 합이 1.00 이라
        /// 이 값들은 "발동 시 어느 종류가 나오는지"의 분포다(<see cref="MentalErrorDefinitionSO"/> 참조).
        /// </summary>
        public MentalErrorDefinitionSO RollDefinition()
        {
            if (_definitions.Count == 0)
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogWarning("[침식] 정신 이상 정의가 없어 발동을 건너뜁니다.", this);
                }
                return null;
            }

            if (_probabilityTotal <= 0f) return _definitions[0];

            float roll = (float)_rng.NextDouble() * _probabilityTotal;
            for (int i = 0; i < _definitions.Count; i++)
            {
                roll -= _definitions[i].activationProbability;
                if (roll <= 0f) return _definitions[i];
            }
            return _definitions[_definitions.Count - 1];   // 부동소수 오차 보정
        }

        /// <summary>정신 이상 발동을 알린다 — 로그 문구는 유저가 지정한 형식 그대로다.</summary>
        public void ReportTriggered(CharacterUnit unit, MentalErrorDefinitionSO def)
        {
            if (unit == null || def == null) return;

            // 유저 확정 문구: "[캐릭터 이름]이/가 [한글 설명] 상태에 빠집니다."
            string line = $"{KoreanParticle.WithIGa(unit.name)} {def.DisplayName} 상태에 빠집니다.";

            UI.HudLog.Add(line, UI.HudLogKind.Danger);
            if (logMentalErrors) Debug.Log($"[침식] {line} ({def})", unit);

            OnMentalErrorTriggered?.Invoke(unit, def);
        }

        /// <summary>인스펙터에서 값을 고쳤을 때 즉시 반영되도록 정규화만 해둔다.</summary>
        void OnValidate()
        {
            if (erosionMax < 1) erosionMax = 1;
        }
    }
}
