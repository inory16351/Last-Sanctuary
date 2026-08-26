using TMPro;
using UnityEngine;
using LastSanctuary.Wave;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 중앙 상단 웨이브 표시 — 웨이브 번호·단계·남은 타이머.
    ///
    /// 진군(<see cref="WavePhase.Marching"/>) 단계에서는 <b>타이머가 멈춰 있는 게 정상</b>이다.
    /// 웨이브 타이머는 첫 전투가 벌어져야 돌기 시작하도록 설계돼 있다(진행상황 11절).
    /// 그래서 진군 중에는 숫자 대신 "진군 중" 을 보여준다 — 안 그러면 "타이머가 고장 났다"로 보인다.
    ///
    /// ⚠️ 타이머가 아예 안 흐르면 <c>ProjectSettings > Player > Run In Background</c> 를 먼저 확인할 것.
    /// 꺼져 있으면 에디터가 포커스를 잃는 순간 게임 루프가 멈춘다(진행상황 11절).
    /// </summary>
    public class WaveStatusPanel : MonoBehaviour
    {
        [Header("하이라키 연결")]
        [SerializeField] TMP_Text phaseLabel;
        [SerializeField] TMP_Text timerLabel;

        [Header("색")]
        [SerializeField] Color normalColor = new Color(0.45f, 0.95f, 0.78f, 1f);
        [SerializeField] Color battleColor = new Color(0.98f, 0.72f, 0.35f, 1f);
        [SerializeField] Color defeatColor = new Color(0.96f, 0.42f, 0.42f, 1f);
        [SerializeField] Color enrageColor = new Color(0.92f, 0.18f, 0.35f, 1f);
        [SerializeField] Color victoryColor = new Color(1f, 0.88f, 0.45f, 1f);

        [Tooltip("남은 시간이 이 값 아래로 내려가면 타이머가 붉게 바뀐다(초)")]
        [Min(0f)] [SerializeField] float urgentSeconds = 10f;

        WaveManager _wave;

        // 마지막으로 화면에 쓴 값. 바뀔 때만 문자열을 다시 만든다 —
        // 매 프레임 string 을 만들면 TMP 가 메시를 다시 굽는다(EnergyLabel 과 같은 이유).
        int _shownWave = -1;
        WavePhase _shownPhase = (WavePhase)(-1);
        int _shownSeconds = -1;

        void Start()
        {
            _wave = FindAnyObjectByType<WaveManager>();

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            if (phaseLabel == null) phaseLabel = FindLabel("Phase");
            if (timerLabel == null) timerLabel = FindLabel("Timer");

            if (phaseLabel == null || timerLabel == null)
            {
                Debug.LogError("[Wave HUD] Phase / Timer 라벨이 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            if (_wave == null)
            {
                phaseLabel.text = Data.StringTable.Get("ui_wave_unknown", "웨이브 정보 없음");
                timerLabel.text = "--:--";
                Debug.LogWarning("[Wave HUD] WaveManager 를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _wave.OnWaveSpawned += HandleWaveSpawned;
            _wave.OnDefeat += HandleDefeat;
            _wave.OnVictory += HandleVictory;
        }

        void OnDestroy()
        {
            if (_wave == null) return;
            _wave.OnWaveSpawned -= HandleWaveSpawned;
            _wave.OnDefeat -= HandleDefeat;
            _wave.OnVictory -= HandleVictory;
        }

        void Update()
        {
            WavePhase phase = _wave.Phase;
            int wave = _wave.WaveNumber;

            if (phase != _shownPhase || wave != _shownWave)
            {
                _shownPhase = phase;
                _shownWave = wave;
                _shownSeconds = -1;                       // 단계가 바뀌면 타이머도 다시 쓴다
                phaseLabel.text = string.Format(
                    Data.StringTable.Get("ui_wave_phase_format", "웨이브 {0} · {1}"),
                    wave, PhaseName(phase));
            }

            // 진군 중에는 타이머가 멈춰 있는 게 정상이라 숫자를 보여주지 않는다.
            if (phase == WavePhase.Marching)
            {
                if (_shownSeconds != -2)
                {
                    _shownSeconds = -2;
                    timerLabel.text = Data.StringTable.Get("ui_timer_marching", "진군 중");
                    timerLabel.color = battleColor;
                }
                return;
            }

            if (phase == WavePhase.Defeat)
            {
                if (_shownSeconds != -3)
                {
                    _shownSeconds = -3;
                    timerLabel.text = Data.StringTable.Get("ui_phase_defeat", "패배");
                    timerLabel.color = defeatColor;
                }
                return;
            }

            if (phase == WavePhase.Victory)
            {
                if (_shownSeconds != -5)
                {
                    _shownSeconds = -5;
                    timerLabel.text = Data.StringTable.Get("ui_phase_victory", "승리");
                    timerLabel.color = victoryColor;
                }
                return;
            }

            // 웨이브 타이머가 끝났는데 몬스터가 남아있는 상태 — 숫자 대신 경고 배너를 보여준다.
            // (다음 대기시간은 이 구간에서도 뒤에서 이미 흐르고 있지만, 처치가 끝나기 전까지는 표시하지 않는다)
            if (phase == WavePhase.Enrage)
            {
                if (_shownSeconds != -4)
                {
                    _shownSeconds = -4;
                    timerLabel.text = Data.StringTable.Get("ui_timer_enraged", "광폭화!");
                    timerLabel.color = enrageColor;
                }
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt(_wave.PhaseRemaining));
            if (seconds == _shownSeconds) return;

            _shownSeconds = seconds;
            timerLabel.text = $"{seconds / 60:00}:{seconds % 60:00}";
            timerLabel.color = seconds <= urgentSeconds ? battleColor : normalColor;
        }

        TMP_Text FindLabel(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        static string PhaseName(WavePhase phase) => phase switch
        {
            WavePhase.Idle        => Data.StringTable.Get("ui_phase_idle", "대기 전"),
            WavePhase.Preparation => Data.StringTable.Get("ui_phase_prep", "정비"),
            WavePhase.Marching    => Data.StringTable.Get("ui_phase_advance", "진군"),
            WavePhase.Battle      => Data.StringTable.Get("ui_phase_combat", "전투"),
            WavePhase.Enrage      => Data.StringTable.Get("ui_phase_enraged", "광폭화"),
            WavePhase.Defeat      => Data.StringTable.Get("ui_phase_defeat", "패배"),
            WavePhase.Victory     => Data.StringTable.Get("ui_phase_victory", "승리"),
            _                     => phase.ToString(),
        };

        void HandleWaveSpawned(int wave) =>
            HudLog.Add($"웨이브 {wave} 몬스터 소환", HudLogKind.Warn);

        /// <summary>패배 사유는 <see cref="WaveManager.Reason"/> 이 정본이다 — 문구를 여기서 짐작하지 않는다.</summary>
        void HandleDefeat() =>
            HudLog.Add(_wave != null && _wave.Reason == DefeatReason.AllCharactersLost
                           ? Data.StringTable.Get("ui_defeat_party", "캐릭터가 전멸했습니다")
                           : Data.StringTable.Get("ui_defeat_nexus", "성역이 파괴되었습니다"),
                       HudLogKind.Danger);

        void HandleVictory(int wave) =>
            HudLog.Add($"웨이브 {wave} 클리어 — 승리!", HudLogKind.Good);
    }
}
