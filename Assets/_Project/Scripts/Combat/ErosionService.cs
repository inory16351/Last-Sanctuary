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

        // ------------------------------------------------------------------
        // 보스에게 맞으면 즉시 오르는 침식 (유저 확정 2026-08-13)
        //
        // "보스의 공격에 피격 당하면 즉시 10의 침식 수치가 오른다" — 위의 초당 누적과
        // 성격이 다르다. 누적은 "웨이브와 오래 붙어 있으면 정신이 갉인다"이고, 이쪽은
        // <b>한 방</b>이다. 그래서 잡몹에게 맞는 것과 달리 보스전은 정신 이상이 훨씬 빨리 뜬다.
        // ------------------------------------------------------------------

        [Header("보스 피격 — 즉시 침식")]
        [Tooltip("보스의 공격에 맞을 때마다 즉시 오르는 침식량. 유저 확정 기본값 10 " +
                 "(상한 100 이므로 10대만 맞아도 정신 이상이 터진다)")]
        [Min(0f)] [SerializeField] float erosionPerBossHit = 10f;

        [Tooltip("중간보스(혈인·공허의 속삭임)도 보스로 볼지. 끄면 최종보스(단탈리온)의 " +
                 "공격만 이 규칙을 적용한다")]
        [SerializeField] bool midBossCountsAsBoss = true;

        [Tooltip("한 번 오른 뒤 이 시간(초) 안에 또 맞으면 다시 오르지 않는다. " +
                 "0 이면 맞을 때마다 전부 적용(유저가 말한 규칙 그대로). " +
                 "보스 스킬은 광역이라 한 번 시전에 여러 대상을 때리는데, 같은 캐릭터가 " +
                 "겹쳐 맞는 구성이 생기면 여기에 0.5 정도를 넣어 완충할 수 있다")]
        [Min(0f)] [SerializeField] float bossHitErosionCooldown = 0f;

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
        public float ErosionPerBossHit => erosionPerBossHit;

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

        // ------------------------------------------------------------------
        // 보스 피격 — 즉시 침식
        //
        // <b>왜 여기서 구독하나</b> — 침식의 규칙은 전부 이 서비스에 모아둔다는 것이
        // 29-2절의 구조다(상태만 <see cref="CharacterErosion"/>). 몬스터 쪽
        // (<c>MonsterUnit</c>/<c>UnitCombat</c>)에 침식 코드를 넣으면 PROTO 소유 파일에
        // 규칙이 새어나가고, 규칙을 고칠 때 두 군데를 봐야 한다.
        //
        // <b>왜 <c>OnAnyHit</c> 인가</b> — <c>OnAnyAttack</c> 은 명중 판정 <b>전에</b> 나므로
        // 빗나간 공격도 센다. 유저 규칙은 "피격 당하면" 이다.
        // ------------------------------------------------------------------

        void OnEnable() => DamageableUnit.OnAnyHit += HandleHit;
        void OnDisable() => DamageableUnit.OnAnyHit -= HandleHit;

        /// <summary>캐릭터별 마지막 보스 피격 침식 시각 — <see cref="bossHitErosionCooldown"/> 용.</summary>
        readonly Dictionary<CharacterErosion, float> _lastBossHitTime =
            new Dictionary<CharacterErosion, float>();

        void HandleHit(DamageableUnit attacker, DamageableUnit target, int damage)
        {
            if (!enableErosion || erosionPerBossHit <= 0f) return;
            if (attacker == null || target == null) return;

            var character = target as CharacterUnit;
            if (character == null || !character.IsAlive) return;

            var monster = attacker as MonsterUnit;
            if (monster == null) return;

            // 중립 몬스터는 MonsterUnit 이 아니라 NeutralMonsterUnit 이므로 여기 안 걸린다 —
            // 침식이 "웨이브의 압박"이라는 29-2절의 정의가 그대로 유지된다.
            if (monster.Tier == MonsterTier.Normal) return;
            if (monster.Tier == MonsterTier.MidBoss && !midBossCountsAsBoss) return;

            CharacterErosion erosion = CharacterErosion.EnsureOn(character);
            if (erosion == null) return;

            // 완충을 안 쓰면(기본값) 표 자체를 만들지 않는다 — 죽은 캐릭터의 항목이
            // 쌓이지 않게. 쓸 때만 캐릭터 12명 분량이라 크기 걱정이 없다.
            if (bossHitErosionCooldown > 0f)
            {
                if (_lastBossHitTime.TryGetValue(erosion, out float last) &&
                    Time.time - last < bossHitErosionCooldown)
                    return;
                _lastBossHitTime[erosion] = Time.time;
            }

            erosion.AddErosion(erosionPerBossHit);
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
        /// <summary>
        /// <paramref name="unit"/> 의 <b>패시브 보정을 반영해</b> 한 종류를 뽑는다.
        ///
        /// 보정을 여기서 처리하는 이유 — 가중치 추첨은 이 한 곳뿐이므로, 여기에 배수를 얹으면
        /// '강철의 의지'(좋은 효과 ×N)와 '광란'(이기심·광분으로 고정)이 <b>추첨 로직을 복제하지
        /// 않고</b> 성립한다. 배수 0 은 "그 종류는 뽑히지 않는다" 로 자연스럽게 떨어진다.
        ///
        /// <paramref name="unit"/> 이 null 이거나 패시브가 없으면 <see cref="RollDefinition()"/> 과
        /// 완전히 같은 결과다 — 몬스터·확장 전 캐릭터의 동작이 바뀌지 않는다.
        /// </summary>
        public MentalErrorDefinitionSO RollDefinition(CharacterUnit unit)
        {
            var passives = unit != null ? unit.GetComponent<CharacterPassives>() : null;
            if (passives == null) return RollDefinition();

            float total = 0f;
            for (int i = 0; i < _definitions.Count; i++)
                total += _definitions[i].activationProbability *
                         passives.MentalWeightMultiplier(_definitions[i]);

            // 보정이 모든 후보를 0 으로 만들었다 — 보정 없는 추첨으로 떨어진다(발동을 삼키지 않는다).
            if (total <= 0f) return RollDefinition();

            float roll = (float)_rng.NextDouble() * total;
            for (int i = 0; i < _definitions.Count; i++)
            {
                roll -= _definitions[i].activationProbability *
                        passives.MentalWeightMultiplier(_definitions[i]);
                if (roll <= 0f) return _definitions[i];
            }
            return _definitions[_definitions.Count - 1];   // 부동소수 오차 보정
        }

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
