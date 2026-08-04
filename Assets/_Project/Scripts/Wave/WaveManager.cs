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
        Defeat,         // 넥서스 파괴 — 모든 타이머 정지
    }

    /// <summary>
    /// 대기시간 타이머와 웨이브 타이머를 관리한다.
    ///
    ///   시작 → [대기시간] → 타이머 종료 시 맵 가장자리에서 몬스터 소환 (넥서스로 진군)
    ///        → [진군] 첫 전투(몬스터 ↔ 아군)가 발생하면 웨이브 타이머 시작
    ///        → [전투] 웨이브 타이머 종료 → 웨이브 번호 +1 → 다시 [대기시간]
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

        [Header("웨이브")]
        [Min(1)] [SerializeField] int startWave = 1;

        [Header("실행")]
        [SerializeField] bool autoStart = true;
        [SerializeField] bool logPhaseChanges = true;

        WavePhase _phase = WavePhase.Idle;
        float _remaining;
        float _phaseDuration;
        int _waveNumber;

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
                    if (_remaining <= 0f) EndWave();
                    break;
            }
        }

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
                case WavePhase.Battle: EndWave(); break;
            }
        }

        // ------------------------------------------------------------------

        void BeginPreparation()
        {
            _phaseDuration = preparationSeconds;
            _remaining = preparationSeconds;
            SetPhase(WavePhase.Preparation,
                     $"웨이브 {_waveNumber} 대기시간 {preparationSeconds:0.#}초");
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
        }

        void EndWave()
        {
            int finished = _waveNumber;
            OnWaveEnded?.Invoke(finished);

            _waveNumber++;
            BeginPreparation();
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

            _phaseDuration = 0f;
            _remaining = 0f;
            SetPhase(WavePhase.Defeat, $"넥서스 파괴 → 패배 (웨이브 {_waveNumber} 진행 중이었음)");
            OnDefeat?.Invoke();
        }

        static bool IsMonsterVersusAngel(DamageableUnit a, DamageableUnit b)
        {
            if (a == null || b == null) return false;
            return (a.Kind == UnitKind.Monster && b.Faction == Faction.Angel)
                || (b.Kind == UnitKind.Monster && a.Faction == Faction.Angel);
        }
    }
}
