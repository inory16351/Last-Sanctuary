using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LastSanctuary.Combat;
using LastSanctuary.Units;
using LastSanctuary.Wave;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 승리 화면. 목표 웨이브(<see cref="WaveManager.VictoryWave"/>)를 클리어하면 게임을 멈추고
    /// 결과를 보여준 뒤 다시 시작하게 한다.
    ///
    /// <b><see cref="DefeatPanel"/> 과 같은 구조를 의도적으로 그대로 따랐다.</b> 두 화면은
    /// "게임이 끝났다 → 멈춘다 → 결과를 보여준다 → 재시작" 이라는 흐름이 완전히 같고,
    /// 이 프로젝트에서 반복해서 밟은 함정(아래 셋)도 똑같이 적용된다. 한쪽을 고칠 때 다른 쪽도
    /// 같은 자리를 보면 되도록 구조를 갈라놓지 않았다:
    ///
    ///   1. <b>멈추는 방법은 <see cref="Time.timeScale"/> = 0</b> — 게임플레이가 전부
    ///      <c>Time.deltaTime</c> 기반이라 한 줄로 멈추고, HUD 는 <c>unscaledTime</c> 을 쓰므로
    ///      화면·음악은 살아 있다.
    ///   2. <b>⚠️ <c>timeScale</c> 은 플레이 모드를 나가도 유지된다</b> — 승리한 채로 Stop 을
    ///      누르면 다음 플레이가 "눌렀는데 아무것도 안 움직인다"로 시작한다. 그래서
    ///      <see cref="OnDestroy"/> 에서 반드시 되돌린다(씬 재로드도 이 지점을 지난다).
    ///   3. <b>이 오브젝트 자체는 끄지 않는다</b> — 끄면 <see cref="Start"/> 가 안 돌아
    ///      <see cref="WaveManager.OnVictory"/> 구독이 아예 걸리지 않고, 그러면 이겨도 영원히
    ///      안 뜬다. <c>HUD_Victory</c> 는 항상 활성으로 두고 자식 <c>Body</c> 만 켜고 끈다
    ///      (<see cref="BossHealthPanel"/>·<see cref="DefeatPanel"/> 과 같은 방식).
    /// </summary>
    public class VictoryPanel : MonoBehaviour
    {
        [Header("문구")]
        [SerializeField] string titleText = "승리";

        [Tooltip("{0}=클리어한 웨이브")]
        [SerializeField] string reasonFormat = "웨이브 {0}까지 방어에 성공했습니다.";

        [Tooltip("{0}=클리어한 웨이브, {1}=생존 시간, {2}=남은 캐릭터 수")]
        [SerializeField] string summaryFormat = "웨이브 {0} 클리어 · 생존 {1} · 남은 인원 {2}명";

        [SerializeField] string restartLabel = "다시 시작";

        [Header("동작")]
        [Tooltip("승리 시 Time.timeScale 을 0 으로 만들어 게임을 멈춘다. " +
                 "끄면 승리 화면만 뜨고 전투는 계속 진행된다(연출·디버그 확인용)")]
        [SerializeField] bool pauseGameOnVictory = true;

        [Tooltip("승리 화면이 뜨기까지의 지연(초). 마지막 몬스터가 쓰러지는 순간을 보여주기 위한 여유. " +
                 "Time.unscaledTime 기준이라 게임이 멈춰도 흐른다")]
        [Min(0f)] [SerializeField] float showDelaySeconds = 1.2f;

        GameObject _body;
        TMP_Text _title, _reason, _summary, _restartTextLabel;
        Button _restartButton;

        WaveManager _wave;

        /// <summary>게임 시작 시각(unscaled). 생존 시간 표시에 쓴다.</summary>
        float _startedAt;

        bool _won;
        float _showAt;
        bool _shown;

        // 승리 시점의 값을 찍어둔다 — 표시가 지연되는 동안 값이 달라지면 안 된다.
        int _finalWave;
        float _finalSeconds;
        int _finalAlive;

        void Awake() => BuildBindings();

        void Start()
        {
            _startedAt = Time.unscaledTime;

            _wave = FindAnyObjectByType<WaveManager>();
            if (_wave != null) _wave.OnVictory += HandleVictory;
            else Debug.LogWarning("[승리] WaveManager 를 찾지 못했습니다. 승리 화면이 뜨지 않습니다.", this);

            if (_body != null) _body.SetActive(false);
        }

        void OnDestroy()
        {
            if (_wave != null) _wave.OnVictory -= HandleVictory;
            if (_shown && pauseGameOnVictory) Time.timeScale = 1f;   // 위 doc 2번
        }

        void Update()
        {
            if (!_won || _shown) return;
            if (Time.unscaledTime < _showAt) return;
            Show();
        }

        // ------------------------------------------------------------------

        void HandleVictory(int clearedWave)
        {
            if (_won) return;
            _won = true;

            _finalWave = clearedWave;
            _finalSeconds = Time.unscaledTime - _startedAt;
            _finalAlive = CountAliveCharacters();

            _showAt = Time.unscaledTime + showDelaySeconds;
        }

        void Show()
        {
            _shown = true;

            if (_title != null) _title.text = titleText;
            if (_reason != null) _reason.text = string.Format(reasonFormat, _finalWave);
            if (_summary != null)
                _summary.text = string.Format(summaryFormat, _finalWave,
                                              FormatDuration(_finalSeconds), _finalAlive);
            if (_restartTextLabel != null) _restartTextLabel.text = restartLabel;

            if (_body != null) _body.SetActive(true);

            // ★★ 떠 있는 전투 연출을 <b>timeScale 을 0 으로 만들기 전에</b> 치운다 —
            //   이유는 <see cref="DefeatPanel"/> 의 같은 자리에 적어 두었다(2026-08-21).
            Combat.CombatProjectileFx.ClearAll();

            if (pauseGameOnVictory) Time.timeScale = 0f;

            HudLog.Add($"승리 — 웨이브 {_finalWave} 클리어", HudLogKind.Good);
        }

        /// <summary>
        /// 새 판을 시작한다 (<see cref="DefeatPanel.Restart"/> 와 같은 문을 지난다).
        /// ★ 2026-08-21 — 씬만 다시 열던 것을 <see cref="Save.RunResetService"/> 로 바꿨다.
        ///   이유는 그 클래스의 맨 위 주석(«캐릭터 생성이 안 되는» 버그).
        /// </summary>
        public void Restart() => Save.RunResetService.BeginNewRun();

        static int CountAliveCharacters()
        {
            var all = UnitRegistry.All;
            int n = 0;
            for (int i = 0; i < all.Count; i++)
                // ★ 소환수(아루의 골렘)는 세지 않는다 — CharacterUnit.IsSummoned 의 긴 주석 참조.
                if (all[i] is CharacterUnit c && c.IsAlive && !c.IsSummoned) n++;
            return n;
        }

        static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        // ------------------------------------------------------------------
        // 하이라키 연결 — 경로로 찾는다 (MCP 로는 인스펙터 참조를 못 넣는다, 진행상황 8절 4번)
        // ------------------------------------------------------------------

        void BuildBindings()
        {
            _body = transform.Find("Body")?.gameObject;
            if (_body == null)
            {
                Debug.LogError("[승리] HUD_Victory/Body 를 찾지 못했습니다.", this);
                return;
            }

            _title = FindText("Body/Panel/Title");
            _reason = FindText("Body/Panel/Reason");
            _summary = FindText("Body/Panel/Summary");
            _restartTextLabel = FindText("Body/Panel/RestartButton/Label");

            _restartButton = transform.Find("Body/Panel/RestartButton")?.GetComponent<Button>();
            if (_restartButton != null) _restartButton.onClick.AddListener(Restart);
        }

        TMP_Text FindText(string path)
        {
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<TMP_Text>() : null;
        }
    }
}
