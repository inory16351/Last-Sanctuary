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
        // ⚠⚠ <b>이 아래 기본값은 「능력치 및 공식 정리.xlsx」의 「계수」 시트와 같아야 한다</b>
        //   (2026-08-27 정리 · <c>BalanceConfigSO</c> 머리글의 그 규약과 같다). 예전에는
        //   기본값이 옛 값(1.5 · 1.0)이고 씬에만 개정값(1.3 · 0.35)이 들어 있었다 — 씬이
        //   덮으므로 판은 정상이었지만, <b>이 컴포넌트를 새로 붙이면 조용히 옛 밸런스로 돌아간다</b>.
        //   씬 값은 한 톨도 안 바뀐다(직렬화된 값이 기본값을 이긴다).

        [Header("침식 획득 — 웨이브 몬스터와 전투 중 (초당)")]
        [Tooltip("웨이브 몬스터와 교전 중일 때 1초에 쌓이는 침식량. 기본 1.3 = 상한 100 까지 약 77초. " +
                 "옛 값 1.9 는 전투 120초에 228 이 쌓여 정비시간 회복으로 못 따라갔다(2026-08-24 개정)")]
        [Min(0f)] [SerializeField] float erosionPerSecondInCombat = 1.3f;

        [Tooltip("마지막으로 웨이브 몬스터에게 맞은 뒤 이 시간(초)까지는 계속 '전투 중'으로 본다. " +
                 "타겟을 놓친 순간마다 침식이 끊기지 않게 하는 여유값")]
        [Min(0f)] [SerializeField] float waveCombatMemorySeconds = 3f;

        // ══════════════════════════════════════════════════════════════
        // ★★★ 후방 침식 (2026-09-01 신설 · 유저 지시)
        //
        //   *"후방에 있는 아군도 어느 정도는 침식이 되는 로직을 만들거나"*
        //
        //   <b>무엇이 문제였나</b> — <see cref="CharacterErosion"/> 의 <c>IsInWaveCombat</c> 은
        //   «내 타겟이 <see cref="Faction.Cancer"/> 이거나 / 최근에 Cancer 에게 맞았거나» 다.
        //   그런데 <b>힐러는 타겟이 아군</b>이라 이 조건에 <b>영영 안 걸린다</b>. 원거리·보조도
        //   사거리 밖에 서 있으면 0 이다. 그래서 침식이 «앞줄 몇 명만의 시스템» 이 됐고,
        //   유저 체감이 *"침식도 사실상 안되는 느낌"* 이 됐다.
        //
        //   ★ <b>웨이브 몬스터가 맵에 살아 있는 동안</b>에는 교전하지 않는 캐릭터도
        //     전투치의 이 비율만큼 침식이 쌓인다. 침식은 «웨이브의 압박» 을 나타내는
        //     수치이므로, 전장에 몬스터가 도는 동안 후방이라고 압박이 0 인 것이 오히려 이상하다.
        //
        //   ⚠ <b>중립 몬스터는 여기에도 안 들어간다</b> — <c>UnitKind.Monster</c> 를
        //     공유하므로 <see cref="Faction.Cancer"/> 까지 확인해야 갈린다. 중립 사냥은
        //     정비 시간의 자원 활동이라는 기존 규칙이 그대로다.
        //
        //   30% · 전투 120초 기준 후방 캐릭터가 웨이브당 약 47 을 쌓아
        //   <b>2~3 웨이브에 한 번</b> 정신 이상이 온다.
        // ══════════════════════════════════════════════════════════════

        [Tooltip("★ 웨이브 몬스터가 맵에 살아있는 동안, <b>교전하지 않는</b> 캐릭터가 쌓는 " +
                 "침식량(전투 중 침식의 %). 0 이면 예전 동작(후방은 전혀 안 쌓인다).\n" +
                 "⚠ 이 값이 0 보다 크면 웨이브가 도는 내내 <b>회복 대기가 시작되지 않는다</b> — " +
                 "침식이 빠지는 것은 웨이브를 정리한 뒤부터다")]
        [Range(0, 100)] [SerializeField] int rearErosionPercent = 30;

        [Header("침식 회복 — 전투에서 벗어난 뒤 (초당)")]
        [Tooltip("전투에서 벗어난 뒤 회복이 시작되기까지의 대기 시간(초). " +
                 "체력 재생의 outOfCombatRegenDelay 와 같은 결이지만 그보다 조금 길다")]
        // ⚠ 2026-09-01 — 7 → <b>12</b>. 「계수」 시트에 자리를 만들어 함께 올렸다.
        //   후방 침식이 생기면서 «웨이브가 끝난 뒤» 가 유일한 회복 창이 됐는데,
        //   대기시간 30초 중 23초가 회복 구간이면 쌓은 것의 상당량이 그대로 빠졌다.
        [Min(0f)] [SerializeField] float recoverDelaySeconds = 12f;

        // ★ 2026-09-01 — 0.35 → <b>0.15</b> (유저 지시: *"회복되는 속도를 낮추거나 해야 할듯"*).
        //   0.35 이면 대기시간 30초에 약 8 이 빠지는데, 조기 처치 보너스로 대기시간이
        //   늘어나면 그만큼 더 빠져서 «잘 싸울수록 침식이 안 쌓이는» 모양이 됐다.
        [Tooltip("회복이 시작된 뒤 1초에 줄어드는 침식량. 기본 0.15 " +
                 "(2026-09-01 개정 · 옛 값 0.35 · 그 전 0.2). 후방 침식이 생기면서 " +
                 "«쌓는 창» 이 넓어졌으므로 «빠지는 창» 을 같이 좁혔다")]
        [Min(0f)] [SerializeField] float erosionRecoverPerSecond = 0.15f;

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

        /// <summary>후방(비교전) 캐릭터가 쌓는 침식량 — 전투 중 침식의 %. 0 이면 안 쌓인다.</summary>
        public int RearErosionPercent => Mathf.Clamp(rearErosionPercent, 0, 100);

        /// <summary>후방 침식이 초당 얼마인가. <see cref="CharacterErosion.Tick"/> 이 쓴다.</summary>
        public float RearErosionPerSecond => erosionPerSecondInCombat * RearErosionPercent / 100f;
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

            // ★★ 후방 침식의 조건 — <b>웨이브 몬스터가 맵에 살아 있는가</b> (2026-09-01).
            //
            //   <b>왜 WaveManager 를 안 보나</b> — 그 클래스에는 static 진입점이 없어서
            //   참조를 새로 뚫어야 하고, «웨이브 단계» 와 «몬스터가 실제로 전장에 있나» 는
            //   미묘하게 다르다(진군 중·정리 중). 침식은 <b>압박</b>의 수치이므로
            //   «전장에 웨이브 몬스터가 도는가» 로 묻는 것이 정의에 더 가깝다.
            //
            //   ⚠ <see cref="Faction.Cancer"/> 까지 확인해야 <b>중립 몬스터가 안 걸린다</b> —
            //     둘 다 <c>UnitKind.Monster</c> 를 쓴다(<c>IsInWaveCombat</c> 이 밟은 함정과 같다).
            //   비용은 «첫 한 마리를 찾으면 즉시 반환» 이라 실질적으로 무시할 수 있다.
            bool waveMonstersAlive =
                rearErosionPercent > 0 &&
                UnitRegistry.FindFirst(Faction.Cancer, UnitKind.Monster) != null;

            // UnitRegistry 는 살아있는 유닛 전체를 이미 들고 있다 — FindObjectsByType 을 돌지 않는다(U-D10).
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var character = all[i] as CharacterUnit;
                if (character == null || !character.IsAlive) continue;

                // ★★ 2026-08-21 — <b>소환수(아루의 골렘)는 침식하지 않는다</b>
                //   (「강림」 정의문: *"침식이 일어나지 않습니다"* · 유저 리포트:
                //   *"아루의 골렘에게 침식이 적용 안되도록"*).
                //
                //   ⚠ <b>왜 컴포넌트를 꺼두는 것으로는 안 됐나</b> — <see cref="AruGolem"/> 가
                //     <c>erosion.enabled = false</c> 로 껐지만, 이 루프는 <c>EnsureOn</c> +
                //     <c>Tick</c> 을 <b>직접</b> 부른다. <c>Update</c> 를 쓰지 않는 구조라
                //     («왜 캐릭터마다 Update 를 돌리지 않는가» 클래스 주석) <b>enabled 가
                //     아무것도 막지 못한다</b> — 그래서 골렘에게 침식이 그대로 쌓였다.
                //   → 판정을 <b>여기</b> 둔다. 켜고 끄는 곳(스킬)과 도는 곳(서비스)이 다르면
                //     또 새므로, «누가 침식하는가» 는 도는 쪽이 정하는 것이 맞다.
                if (character.IsSummoned) continue;

                CharacterErosion erosion = CharacterErosion.EnsureOn(character);
                if (erosion != null) erosion.Tick(this, dt, waveMonstersAlive);
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
            //
            // ★ 문장을 조각으로 잇지 않는다 — 자리표 둘짜리 «형식 하나»를 표에서 가져온다.
            //   영어는 어순이 달라서(«X falls into Y») 조각을 이어 붙이면 못 만든다.
            // ⚠ 조사(이/가)는 <b>한국어일 때만</b> 붙인다. 영어에는 주격 조사가 없어서
            //   그대로 붙이면 "Elin이 …" 가 된다.
            bool korean = Data.StringTable.Language == Data.GameLanguage.Korean;
            string who = korean ? KoreanParticle.WithIGa(unit.name) : unit.name;
            string line = string.Format(
                UI.HudTheme.T("log_mental_error_onset", "{0} {1} 상태에 빠집니다."),
                who, def.DisplayName);

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
