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
        Defeat,         // 패배 — 모든 타이머 정지
        Victory,        // 승리 — 목표 웨이브를 클리어했다. 모든 타이머 정지
    }

    /// <summary>
    /// 왜 졌는지. 패배 화면(<c>DefeatPanel</c>)이 이 값으로 사유 문구를 고른다 —
    /// 화면에 "중앙 건물이 파괴되었습니다" 만 뜨는데 실제로는 전멸이었으면 유저가 원인을 못 찾는다.
    /// </summary>
    public enum DefeatReason
    {
        None,
        NexusDestroyed,      // 중앙 건물 파괴
        AllCharactersLost,   // 캐릭터 전멸 + 새로 뽑을 에너지도 없음
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
    ///
    /// <b>끝나는 두 가지 방식 — 둘 다 여기서 판정한다</b>(유저 지시 2026-08-12):
    /// <code>
    ///   승리: victoryWave(기본 20) 를 클리어한 순간 → WavePhase.Victory + OnVictory
    ///   패배: 넥서스 파괴 · 캐릭터 전멸(재생성 불가) → WavePhase.Defeat  + OnDefeat
    /// </code>
    /// 두 조건 모두 <b>인스펙터에서 켜고/끄고, 기준 웨이브를 바꿀 수 있다</b> — 밸런싱을 하려면
    /// 이 값들이 코드 상수가 아니라 에딧 모드에서 만질 수 있어야 한다는 것이 유저 요구였다.
    /// 화면 표현은 <c>VictoryPanel</c> / <c>DefeatPanel</c> 이 이벤트를 구독해서 맡는다 —
    /// 이 클래스는 판정만 하고 UI 를 모른다.
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

        // ------------------------------------------------------------------
        // 승리 조건 (유저 지시 2026-08-12: "승리 기준을 웨이브 매니저에 추가해
        // 에딧 모드에서 수정 가능하게") — 그래서 상수가 아니라 직렬화 필드다.
        // ------------------------------------------------------------------

        [Header("승리 조건")]
        [Tooltip("이 웨이브를 클리어하면 승리한다. 기획서 p9 의 '20웨이브 = 게임 클리어' 가 근거이고, " +
                 "웨이브 테이블도 20웨이브까지만 있다(진행상황 23절). 에디터에서 자유롭게 바꿀 수 있다")]
        [Min(1)] [SerializeField] int victoryWave = 20;

        [Tooltip("끄면 승리 판정을 하지 않는다 — 목표 웨이브를 넘겨도 계속 진행한다(무한 모드·밸런스 테스트용)")]
        [SerializeField] bool enableVictory = true;

        // ------------------------------------------------------------------
        // 패배 조건
        //
        // ⚠️ 넥서스 파괴는 <b>정적 이벤트(OnAnyDied) 구독 + 매 프레임 폴링</b> 두 겹으로 본다.
        //    이벤트 한 겹만 두면 구독이 어긋나는 경로(도메인 리로드 off, 씬 재로드 직후 순서,
        //    넥서스가 피해가 아닌 경로로 사라진 경우)에서 <b>패배가 조용히 안 잡히고 게임이
        //    그대로 계속된다</b> — 실제로 "패배 판정이 구현이 안 됐다"는 리포트를 받은 상태였고,
        //    이런 종류의 침묵은 원인을 찾기가 매우 어렵다. 폴링은 살아있는 유닛 목록을 훑는
        //    가벼운 검사(UnitRegistry)라 비용도 문제되지 않는다.
        // ------------------------------------------------------------------

        [Header("패배 조건")]
        [Tooltip("중앙 건물(넥서스)이 파괴되면 패배한다. 기획서 p9 의 기본 패배 조건")]
        [SerializeField] bool defeatWhenNexusDestroyed = true;

        [Tooltip("캐릭터가 전멸하고 <b>새로 뽑을 에너지도 없고 살아있는 포탑도 없으면</b> 패배한다.\n" +
                 "에너지가 남아 있으면 다시 뽑으면 되고, 포탑이 남아 있으면 그 포탑이 몬스터를 잡아 " +
                 "에너지가 다시 들어온다 — 셋 다 없는 '되돌릴 수 없는 상태' 만 패배로 잡는다")]
        [SerializeField] bool defeatWhenAllCharactersLost = true;

        [Tooltip("전멸 판정을 시작하기 전 유예 시간(초). 게임 시작 직후엔 스포너가 아직 캐릭터를 " +
                 "만들지 않았을 수 있어서, 이 시간 안에는 전멸로 보지 않는다")]
        [Min(0f)] [SerializeField] float allCharactersLostGraceSeconds = 3f;

        [Header("실행")]
        [SerializeField] bool autoStart = true;
        [SerializeField] bool logPhaseChanges = true;

        WavePhase _phase = WavePhase.Idle;
        float _remaining;
        float _phaseDuration;
        int _waveNumber;

        DefeatReason _defeatReason = DefeatReason.None;

        /// <summary>캐릭터가 한 번이라도 살아 있었는지. 전멸 판정이 시작 프레임에 오발하지 않게 한다.</summary>
        bool _anyCharacterSeen;
        float _startedAt;

        // 광폭화 진행 중에만 쓰는 상태.
        float _enrageElapsed;
        float _pendingPreparationRemaining;   // 광폭화 중 뒤에서 이미 흐르고 있는 다음 대기시간
        int _lastAppliedEnragePercent = -1;

        public WavePhase Phase => _phase;
        public int WaveNumber => _waveNumber;

        /// <summary>승리 목표 웨이브. UI 가 "20 중 7웨이브" 처럼 진행도를 보여줄 때 읽는다.</summary>
        public int VictoryWave => victoryWave;

        /// <summary>승리 판정을 쓰는지. 끄면 <see cref="VictoryWave"/> 는 의미가 없다.</summary>
        public bool VictoryEnabled => enableVictory;

        /// <summary>왜 졌는지. 아직 안 졌으면 <see cref="DefeatReason.None"/>.</summary>
        public DefeatReason Reason => _defeatReason;

        /// <summary>게임이 끝났는지 (승리든 패배든). 이 뒤로는 타이머가 흐르지 않는다.</summary>
        public bool IsFinished => _phase == WavePhase.Defeat || _phase == WavePhase.Victory;

        /// <summary>현재 타이머의 남은 시간(초). 진군 구간에서는 0.</summary>
        public float PhaseRemaining => _remaining;

        /// <summary>현재 타이머의 전체 길이(초). 진행 바 표시용.</summary>
        public float PhaseDuration => _phaseDuration;

        public event System.Action<WavePhase> OnPhaseChanged;
        public event System.Action<int> OnWaveSpawned;   // 몬스터 소환 시점
        public event System.Action<int> OnWaveEnded;
        public event System.Action OnDefeat;

        /// <summary>목표 웨이브를 클리어했다 (인자 = 클리어한 웨이브 번호).</summary>
        public event System.Action<int> OnVictory;

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
            _startedAt = Time.time;
            if (autoStart) StartGame();
        }

        void Update()
        {
            // 패배 판정을 타이머보다 먼저 본다 — 이미 진 판에서 웨이브가 한 프레임 더 진행되면
            // 결과 화면에 찍히는 웨이브 번호가 어긋난다.
            if (!IsFinished && CheckDefeatConditions()) return;

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
            _defeatReason = DefeatReason.None;
            BeginPreparation();
        }

        /// <summary>
        /// 저장된 웨이브 상태를 그대로 되돌린다 (98절 — 저장 복원 전용).
        ///
        /// <b>왜 <see cref="StartGame"/> 을 안 쓰는가</b> — 그쪽은 "첫 웨이브를 시작한다"는 게임
        /// 규칙이라 항상 <see cref="WavePhase.Preparation"/> 부터 돈다. 복원은 <b>전투 도중</b>일 수도
        /// 있고(자동 저장이 캐릭터 사망 시점에도 걸린다), 그때 준비 단계로 되돌리면
        /// 죽은 웨이브를 처음부터 다시 하게 되어 자동 저장이 되돌리기 수단이 된다.
        ///
        /// ⚠ 몬스터는 <see cref="MonsterSpawner"/> 가 따로 복원한다 — 여기서는 단계와 시계만 맞춘다.
        /// 전투 단계로 복원했는데 몬스터가 하나도 없으면 <see cref="AllMonstersCleared"/> 가
        /// 곧바로 참이 되어 그 프레임에 웨이브가 끝난다(그것이 옳은 동작이다).
        /// </summary>
        public void RestoreState(int waveNumber, WavePhase phase, float remaining)
        {
            _waveNumber = Mathf.Max(1, waveNumber);
            _defeatReason = DefeatReason.None;
            _enrageElapsed = 0f;

            _remaining = Mathf.Max(0f, remaining);
            _phaseDuration = _remaining > 0f ? _remaining : 0f;

            SetPhase(phase, $"저장 복원 — 웨이브 {_waveNumber} · {phase} · 남은 {_remaining:0.#}초");
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

        /// <summary>
        /// 승리 화면을 지금 띄운다 (테스트용).
        ///
        /// <b>왜 필요한가</b> — 승리는 20웨이브를, 패배는 넥서스가 부서질 때까지를 기다려야
        /// 확인할 수 있다. 실제로 진행상황 29-1절이 "실제 패배가 발생하는 장면을 못 봤다"로
        /// 남긴 항목이 <b>그 뒤로도 검증되지 않은 채 남아 있었다.</b> 인스펙터 우클릭으로
        /// 두 화면을 바로 띄울 수 있어야 다시 같은 일이 반복되지 않는다.
        /// </summary>
        [ContextMenu("[테스트] 즉시 승리")]
        public void DebugForceVictory()
        {
            if (IsFinished) return;

            if (monsterSpawner != null) monsterSpawner.StopReinforcements();
            _phaseDuration = 0f;
            _remaining = 0f;
            SetPhase(WavePhase.Victory, $"[테스트] 즉시 승리 (웨이브 {_waveNumber})");
            OnVictory?.Invoke(_waveNumber);
        }

        /// <summary>패배 화면을 지금 띄운다 (테스트용). 사유는 넥서스 파괴로 표시된다.</summary>
        [ContextMenu("[테스트] 즉시 패배")]
        public void DebugForceDefeat() =>
            BeginDefeat(DefeatReason.NexusDestroyed, "[테스트] 즉시 패배");

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

            // 전투 타이머 길이를 함께 넘긴다 — 스포너가 "마지막 몬스터가 타이머 종료 직전에
            // 넥서스에 닿도록" 소환 주기를 역산하는 데 쓴다(진행상황 27절).
            if (monsterSpawner != null) monsterSpawner.SpawnWave(_waveNumber, battleSeconds);
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

            if (TryBeginVictory(finished)) return;

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

            if (TryBeginVictory(finished)) return;

            _waveNumber++;
            BeginPreparationContinued(_pendingPreparationRemaining);
        }

        // ------------------------------------------------------------------
        // 승리
        // ------------------------------------------------------------------

        /// <summary>
        /// 방금 끝낸 웨이브가 목표 웨이브 이상이면 승리로 확정한다.
        ///
        /// <b>왜 <c>&gt;=</c> 인가</b> — <see cref="startWave"/> 를 목표보다 크게 두고 테스트하거나
        /// 목표를 플레이 중에 낮추는 경우가 있어서, <c>==</c> 로 두면 승리 조건을 지나쳐 버린다.
        /// </summary>
        /// <returns>승리로 전환했으면 true — 호출부는 다음 대기시간을 시작하지 않아야 한다.</returns>
        bool TryBeginVictory(int finishedWave)
        {
            if (!enableVictory || finishedWave < victoryWave) return false;

            _phaseDuration = 0f;
            _remaining = 0f;
            SetPhase(WavePhase.Victory,
                     $"웨이브 {finishedWave} 클리어 → 승리 (목표 {victoryWave}웨이브)");
            OnVictory?.Invoke(finishedWave);
            return true;
        }

        // ------------------------------------------------------------------
        // 패배
        // ------------------------------------------------------------------

        /// <summary>
        /// 패배 조건을 매 프레임 확인한다. <see cref="HandleDeath"/>(정적 이벤트 경로)와
        /// <b>같은 결론에 이르는 두 번째 경로</b>다 — 이벤트가 어긋나도 여기서 잡힌다
        /// (필드 선언부의 ⚠️ 주석 참조).
        /// </summary>
        /// <returns>이 프레임에 패배로 전환했으면 true.</returns>
        bool CheckDefeatConditions()
        {
            if (defeatWhenNexusDestroyed && !HasLivingNexus())
            {
                BeginDefeat(DefeatReason.NexusDestroyed, "중앙 건물 파괴");
                return true;
            }

            if (defeatWhenAllCharactersLost && IsPartyUnrecoverable())
            {
                BeginDefeat(DefeatReason.AllCharactersLost, "캐릭터 전멸 · 재생성 불가");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 살아있는 아군 넥서스가 있는지. <b>없는 것</b>이 패배다 —
        /// 파괴(체력 0)든 오브젝트가 사라진 경우든 같은 결론이라 한 판정으로 묶인다.
        ///
        /// ⚠️ 넥서스가 아직 생성되지 않은 첫 프레임에 오발하지 않도록,
        /// <b>한 번이라도 넥서스를 본 뒤</b>에만 없어진 것으로 인정한다.
        /// </summary>
        bool HasLivingNexus()
        {
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || u.Kind != UnitKind.Nexus || u.Faction != Faction.Angel) continue;
                if (!u.IsAlive) continue;
                _nexusSeen = true;
                return true;
            }
            return !_nexusSeen;   // 아직 한 번도 못 봤으면 "생성 전" 으로 본다
        }

        bool _nexusSeen;

        /// <summary>
        /// 캐릭터가 전멸했고 <b>만회할 방법도 없는</b> 상태인지. 조건 세 개를 모두 만족해야 한다:
        /// <code>
        ///   ① 살아있는 캐릭터가 없다
        ///   ② 새 캐릭터를 뽑을 에너지가 없다   (CharacterCreationService.CanCreate)
        ///   ③ 살아있는 포탑도 없다
        /// </code>
        /// ②③ 이 왜 필요한가 — <b>"전멸했다"와 "졌다"는 다르다.</b> 에너지가 남아 있으면 캐릭터를
        /// 다시 만들면 되고, 포탑이 살아 있으면 그 포탑이 몬스터를 잡아 <b>에너지가 다시 들어온다</b>
        /// (<c>ResourceManager.HandleDeath</c> 는 누가 잡았는지 보지 않는다). 둘 중 하나라도 남아
        /// 있으면 아직 판이 끝난 게 아니므로 패배를 선언하면 플레이어의 판을 빼앗는 셈이 된다.
        /// 이 조건의 기준은 "전멸" 이 아니라 <b>"되돌릴 수 없는가"</b> 다.
        ///
        /// 생성 비용 규칙은 <c>CharacterCreationService</c> 한 곳에만 두고 그 판정을 그대로 읽는다 —
        /// 여기서 비용을 다시 계산하면 두 곳이 조용히 어긋난다.
        /// </summary>
        bool IsPartyUnrecoverable()
        {
            if (Time.time - _startedAt < allCharactersLostGraceSeconds) return false;

            if (CountAliveCharacters() > 0)
            {
                _anyCharacterSeen = true;
                return false;
            }

            if (!_anyCharacterSeen) return false;   // 아직 한 명도 생성되지 않았다

            return !CanStillCreateCharacter() && !HasLivingTower();
        }

        /// <summary>
        /// 아직 남아 있는 캐릭터 수 — 패배 판정용이다.
        ///
        /// ★ <b>부활 대기 중인 캐릭터를 살아있는 것으로 센다</b>
        /// (<see cref="CharacterUnit.IsRevivePending"/>). 여기가 묻는 것은
        /// "지금 싸울 수 있나"가 아니라 <b>"부대가 전멸했나"</b>이고, 3초 뒤 반드시
        /// 일어나는 캐릭터는 전멸이 아니다.
        ///
        /// 이 한 줄이 없으면 <b>마지막 생존자가 히스톤일 때 부활을 못 기다리고 진다</b> —
        /// <see cref="BeginDefeat"/> 는 <c>IsFinished</c> 로 잠기므로 부활해도 되돌아오지 않는다.
        /// </summary>
        static int CountAliveCharacters()
        {
            var all = UnitRegistry.All;
            int n = 0;
            for (int i = 0; i < all.Count; i++)
                // ★ 소환수는 세지 않는다 — 공렘만 남았는데 «캐릭터가 있다» 로 보면 전멸 판정이 안 난다.
                if (all[i] is CharacterUnit c && !c.IsSummoned &&
                    (c.IsAlive || c.IsRevivePending)) n++;
            return n;
        }

        /// <summary>살아있는 아군 포탑이 하나라도 있는지. 있으면 아직 에너지를 벌 수 있다.</summary>
        static bool HasLivingTower()
        {
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u != null && u.IsAlive && u.Kind == UnitKind.Tower && u.Faction == Faction.Angel)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 지금 에너지로 캐릭터를 한 명이라도 더 만들 수 있는지.
        /// 생성 서비스를 못 찾으면 <b>만들 수 있다고 본다</b> — 판정을 모르는 상태에서
        /// 패배를 선언하는 쪽이 훨씬 나쁜 오류다.
        /// </summary>
        static bool CanStillCreateCharacter()
        {
            var creation = UI.CharacterCreationService.Instance;
            return creation == null || creation.CanCreate;
        }

        void BeginDefeat(DefeatReason reason, string label)
        {
            if (IsFinished) return;

            _defeatReason = reason;
            if (monsterSpawner != null) monsterSpawner.StopReinforcements();

            _phaseDuration = 0f;
            _remaining = 0f;
            SetPhase(WavePhase.Defeat, $"{label} → 패배 (웨이브 {_waveNumber} 진행 중이었음)");
            OnDefeat?.Invoke();
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

        /// <summary>
        /// 넥서스가 파괴되면 모든 타이머를 멈춘다. 안 그러면 패배 후에도 웨이브가 계속 돈다.
        ///
        /// <b>이 경로는 "즉시 반응" 담당이다</b> — 같은 판정을 <see cref="CheckDefeatConditions"/> 도
        /// 매 프레임 하고 있으므로, 여기서 놓쳐도 다음 프레임에 반드시 잡힌다(이중화).
        /// </summary>
        void HandleDeath(DamageableUnit unit)
        {
            if (IsFinished || !defeatWhenNexusDestroyed) return;
            if (unit == null || unit.Kind != UnitKind.Nexus || unit.Faction != Faction.Angel) return;

            BeginDefeat(DefeatReason.NexusDestroyed, "넥서스 파괴");
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
