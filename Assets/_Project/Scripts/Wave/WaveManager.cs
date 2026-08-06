using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.Wave
{
    /// <summary>웨이브 진행 단계.</summary>
    public enum WavePhase
    {
        Idle,           // 아직 시작 전
        Preparation,    // 대기시간 — 타이머가 끝나면 몬스터가 소환된다
        Marching,       // 몬스터 진군 중 — 첫 전투가 벌어질 때까지 웨이브 타이머는 멈춰 있다
        Battle,         // 전투 중 — 웨이브 타이머가 흐른다
        Enrage,         // 웨이브 타이머 종료 후에도 몬스터가 남아있는 상태 — 능력치 배율이 계속 오른다
        Defeat,         // 넥서스 파괴 — 모든 타이머 정지
    }

    /// <summary>
    /// 대기시간 타이머와 웨이브 타이머를 관리한다.
    ///
    ///   시작 → [대기시간] → 타이머 종료 시 맵 가장자리에서 몬스터 소환 (넥서스로 진군)
    ///        → [진군] 첫 전투(몬스터 ↔ 아군)가 발생하면 웨이브 타이머 시작
    ///        → [전투] 웨이브 몬스터를 모두 처치 → 즉시 종료, 남은 시간을 다음 대기시간에 보너스로 더함
    ///               │  웨이브 타이머 종료인데 몬스터가 남아있음
    ///               ▼
    ///        → [광폭화] 1초마다 남은 몬스터의 능력치 배율이 오른다. 이 구간에서도 다음 대기시간
    ///                  타이머는 뒤에서 이미 흐르기 시작한다 — 처치가 끝나는 순간 그 시점까지 줄어든
    ///                  값으로 곧바로 대기시간에 들어간다(광폭화가 길어진 만큼 대기시간이 짧아진다)
    ///        → 웨이브 번호 +1 → 다시 [대기시간]
    ///
    /// 진군 구간에서 웨이브 타이머를 세지 않기 때문에, 맵이 커서 이동에 오래 걸려도
    /// 전투 시간이 깎이지 않는다.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] MonsterSpawner monsterSpawner;

        [Header("타이머 (초)")]
        [Tooltip("웨이브가 끝난 뒤 다음 몬스터가 소환되기까지의 정비 시간. 게임 시작 시에도 이 타이머가 먼저 돈다")]
        [Min(0f)] [SerializeField] float preparationSeconds = 30f;

        [Tooltip("첫 전투가 시작된 시점부터 재는 웨이브 지속 시간")]
        [Min(1f)] [SerializeField] float battleSeconds = 120f;

        [Header("광폭화 (웨이브 타이머 종료 후에도 몬스터가 남아있을 때)")]
        [Tooltip("광폭화 진행 중 1초마다 남은 웨이브 몬스터의 체력/공격력에 추가되는 배율(%)")]
        [Min(0f)] [SerializeField] float enragePercentPerSecond = 1f;

        [Header("웨이브")]
        [Min(1)] [SerializeField] int startWave = 1;

        [Header("실행")]
        [SerializeField] bool autoStart = true;
        [SerializeField] bool logPhaseChanges = true;

        WavePhase _phase = WavePhase.Idle;
        float _remaining;
        float _phaseDuration;
        int _waveNumber;

        // 광폭화 진행 중에만 쓰는 상태.
        float _enrageElapsed;
        float _pendingPreparationRemaining;   // 광폭화 중 뒤에서 이미 흐르고 있는 다음 대기시간
        int _lastAppliedEnragePercent = -1;

        public WavePhase Phase => _phase;
        public int WaveNumber => _waveNumber;

        /// <summary>현재 타이머의 남은 시간(초). 진군 구간에서는 0.</summary>
        public float PhaseRemaining => _remaining;

        /// <summary>현재 타이머의 전체 길이(초). 진행 바 표시용.</summary>
        public float PhaseDuration => _phaseDuration;

        public event System.Action<WavePhase> OnPhaseChanged;
        public event System.Action<int> OnWaveSpawned;   // 몬스터 소환 시점
        public event System.Action<int> OnWaveEnded;
        public event System.Action OnDefeat;

        void OnEnable()
        {
            DamageableUnit.OnAnyAttack += HandleAttack;
            DamageableUnit.OnAnyDied += HandleDeath;
        }

        void OnDisable()
        {
            DamageableUnit.OnAnyAttack -= HandleAttack;
            DamageableUnit.OnAnyDied -= HandleDeath;
        }

        void Start()
        {
            if (autoStart) StartGame();
        }

        void Update()
        {
            switch (_phase)
            {
                case WavePhase.Preparation:
                    _remaining -= Time.deltaTime;
                    if (_remaining <= 0f) BeginMarch();
                    break;

                case WavePhase.Battle:
                    _remaining -= Time.deltaTime;
                    if (AllMonstersCleared())
                    {
                        EndWaveEarly(Mathf.Max(0f, _remaining));
                    }
                    else if (_remaining <= 0f)
                    {
                        BeginEnrage();
                    }
                    break;

                case WavePhase.Enrage:
                    _enrageElapsed += Time.deltaTime;
                    _pendingPreparationRemaining = Mathf.Max(0f, _pendingPreparationRemaining - Time.deltaTime);
                    ApplyEnrageScale();
                    if (AllMonstersCleared()) EndWaveFromEnrage();
                    break;
            }
        }

        /// <summary>스포너가 스폰을 끝냈고, 예정된 증원도 없고, 남은 웨이브 몬스터가 없으면 true.</summary>
        bool AllMonstersCleared() =>
            monsterSpawner != null && !monsterSpawner.IsSpawning &&
            !monsterSpawner.HasPendingReinforcements && monsterSpawner.AliveCount == 0;

        // ------------------------------------------------------------------

        /// <summary>첫 웨이브의 대기시간부터 시작한다.</summary>
        public void StartGame()
        {
            _waveNumber = startWave;
            BeginPreparation();
        }

        /// <summary>현재 타이머를 즉시 끝낸다 (밸런싱 테스트용).</summary>
        [ContextMenu("현재 단계 건너뛰기")]
        public void SkipPhase()
        {
            switch (_phase)
            {
                case WavePhase.Preparation: BeginMarch(); break;
                case WavePhase.Marching: BeginBattle(); break;
                case WavePhase.Battle:
                    if (AllMonstersCleared()) EndWaveEarly(Mathf.Max(0f, _remaining));
                    else BeginEnrage();
                    break;
                case WavePhase.Enrage: EndWaveFromEnrage(); break;
            }
        }

        // ------------------------------------------------------------------

        /// <summary>다음 대기시간을 시작한다. <paramref name="bonusSeconds"/>는 조기 처치로 남은 웨이브 시간을 더해준다.</summary>
        void BeginPreparation(float bonusSeconds = 0f)
        {
            _phaseDuration = preparationSeconds + bonusSeconds;
            _remaining = _phaseDuration;
            SetPhase(WavePhase.Preparation,
                     bonusSeconds > 0f
                         ? $"웨이브 {_waveNumber} 대기시간 {_remaining:0.#}초 (조기 처치 보너스 +{bonusSeconds:0.#}초)"
                         : $"웨이브 {_waveNumber} 대기시간 {_remaining:0.#}초");
        }

        /// <summary>광폭화 중 뒤에서 이미 흐르고 있던 대기시간을 그대로 이어 시작한다.</summary>
        void BeginPreparationContinued(float remainingSeconds)
        {
            _phaseDuration = preparationSeconds;
            _remaining = Mathf.Clamp(remainingSeconds, 0f, preparationSeconds);
            SetPhase(WavePhase.Preparation,
                     $"웨이브 {_waveNumber} 대기시간 {_remaining:0.#}초 (광폭화 중 소진)");
        }

        void BeginMarch()
        {
            _phaseDuration = 0f;
            _remaining = 0f;
            SetPhase(WavePhase.Marching,
                     $"웨이브 {_waveNumber} 몬스터 소환 · 첫 전투까지 웨이브 타이머 대기");

            if (monsterSpawner != null) monsterSpawner.SpawnWave(_waveNumber);
            else Debug.LogError("[WaveManager] Monster Spawner 가 연결되지 않았습니다.", this);

            OnWaveSpawned?.Invoke(_waveNumber);
        }

        void BeginBattle()
        {
            _phaseDuration = battleSeconds;
            _remaining = battleSeconds;
            SetPhase(WavePhase.Battle, $"웨이브 {_waveNumber} 전투 개시 · {battleSeconds:0.#}초");

            // 웨이브 테이블에 증원 설정이 있으면 전투 내내 조금씩 흘려보낸다 — 몰아서 한 번에
            // 소환하고 끝나면 광폭화가 거의 안 걸린다는 피드백으로 추가했다(WaveManager 문서 참조).
            if (monsterSpawner != null) monsterSpawner.BeginReinforcements(_waveNumber, battleSeconds);
        }

        /// <summary>웨이브 타이머가 끝나기 전에 몬스터를 모두 처치했을 때 — 남은 시간을 다음 대기시간에 더해준다.</summary>
        void EndWaveEarly(float bonusSeconds)
        {
            int finished = _waveNumber;
            OnWaveEnded?.Invoke(finished);

            if (monsterSpawner != null) monsterSpawner.StopReinforcements();

            _waveNumber++;
            BeginPreparation(bonusSeconds);
        }

        /// <summary>웨이브 타이머는 끝났지만 몬스터가 남아있을 때 — 광폭화로 전환한다.</summary>
        void BeginEnrage()
        {
            // 광폭화 구간에서는 마리 수를 더 늘리지 않는다 — 이미 능력치 배율로 압박하므로
            // (디버그로 Battle 을 건너뛰어 일찍 들어온 경우도 포함해 방어적으로 끈다).
            if (monsterSpawner != null) monsterSpawner.StopReinforcements();

            _enrageElapsed = 0f;
            _pendingPreparationRemaining = preparationSeconds;
            _lastAppliedEnragePercent = -1;
            _phaseDuration = 0f;
            _remaining = 0f;
            SetPhase(WavePhase.Enrage,
                     $"웨이브 {_waveNumber} 광폭화 시작 · 시간 내 처치 실패, 능력치 배율 상승 시작");
        }

        /// <summary>광폭화 중 남은 몬스터를 모두 처치했을 때 — 뒤에서 흐르던 대기시간을 그대로 이어간다.</summary>
        void EndWaveFromEnrage()
        {
            int finished = _waveNumber;
            OnWaveEnded?.Invoke(finished);

            if (monsterSpawner != null)
            {
                monsterSpawner.StopReinforcements();   // 보통 이 시점엔 이미 꺼져 있다 — 방어적으로 한 번 더
                monsterSpawner.SetEnragePercent(100);
            }

            _waveNumber++;
            BeginPreparationContinued(_pendingPreparationRemaining);
        }

        /// <summary>경과 시간에 맞춰 남은 웨이브 몬스터의 능력치 배율을 올린다. 값이 바뀔 때만 적용한다.</summary>
        void ApplyEnrageScale()
        {
            int percent = 100 + Mathf.FloorToInt(_enrageElapsed * enragePercentPerSecond);
            if (percent == _lastAppliedEnragePercent) return;

            _lastAppliedEnragePercent = percent;
            if (monsterSpawner != null) monsterSpawner.SetEnragePercent(percent);
        }

        void SetPhase(WavePhase next, string logMessage)
        {
            _phase = next;
            if (logPhaseChanges) Debug.Log($"[WaveManager] {logMessage}", this);
            OnPhaseChanged?.Invoke(next);
        }

        /// <summary>진군 중 몬스터와 아군이 처음 맞붙는 순간 웨이브 타이머를 켠다.</summary>
        void HandleAttack(DamageableUnit attacker, DamageableUnit target)
        {
            if (_phase != WavePhase.Marching) return;
            if (!IsMonsterVersusAngel(attacker, target)) return;
            BeginBattle();
        }

        /// <summary>넥서스가 파괴되면 모든 타이머를 멈춘다. 안 그러면 패배 후에도 웨이브가 계속 돈다.</summary>
        void HandleDeath(DamageableUnit unit)
        {
            if (_phase == WavePhase.Defeat) return;
            if (unit == null || unit.Kind != UnitKind.Nexus || unit.Faction != Faction.Angel) return;

            if (monsterSpawner != null) monsterSpawner.StopReinforcements();

            _phaseDuration = 0f;
            _remaining = 0f;
            SetPhase(WavePhase.Defeat, $"넥서스 파괴 → 패배 (웨이브 {_waveNumber} 진행 중이었음)");
            OnDefeat?.Invoke();
        }

        /// <summary>
        /// "웨이브 몬스터가 아군을 공격했는지" — Kind 만 보면 안 된다. 중립 몬스터도
        /// Kind.Monster 를 공유해서, 캐릭터가 사냥 중(진군 중에도 사냥함, CharacterBehavior 참조)
        /// 중립 몬스터를 때리면 여기 걸려 웨이브 몬스터가 아직 안 왔는데도 전투 타이머가
        /// 즉시 켜지는 버그가 있었다 — Faction.Cancer(웨이브 몬스터 진영)까지 확인해서 막는다.
        /// </summary>
        static bool IsMonsterVersusAngel(DamageableUnit a, DamageableUnit b)
        {
            if (a == null || b == null) return false;
            return (a.Kind == UnitKind.Monster && a.Faction == Faction.Cancer && b.Faction == Faction.Angel)
                || (b.Kind == UnitKind.Monster && b.Faction == Faction.Cancer && a.Faction == Faction.Angel);
        }
    }
}
