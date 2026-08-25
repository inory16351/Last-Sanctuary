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
    /// 패배 화면. 중앙 건물(성역)이 파괴되면 게임을 멈추고 결과를 보여준 뒤 다시 시작하게 한다.
    ///
    /// <b>왜 이 패널이 필요했나</b> — 패배 <i>판정</i> 자체는 이미 있었다(<c>Nexus.OnDeath</c> →
    /// <c>DamageableUnit.OnAnyDied</c> → <see cref="WaveManager"/> 가 <see cref="WavePhase.Defeat"/> 로
    /// 전환하고 <see cref="WaveManager.OnDefeat"/> 를 쏜다). 하지만 그 뒤에 아무 일도 일어나지
    /// 않아서, 실제 화면에서는 <b>타이머만 멈춘 채 몬스터와 캐릭터가 계속 싸우는</b> 상태로 남았다
    /// (진행상황 11절이 "패배 연출/재시작은 미구현"으로 남겨둔 항목). 이 패널이 그 마지막 한
    /// 칸을 채운다.
    ///
    /// <b>멈추는 방법 — <see cref="Time.timeScale"/> = 0</b>. 유닛을 하나하나 끄는 대신 시간을
    /// 세우는 쪽을 골랐다. 이 프로젝트의 모든 게임플레이가 <c>Time.deltaTime</c> 기반이라
    /// (이동·공격 쿨다운·재생·침식) 한 줄로 전부 멈추고, HUD 는 이미 <c>Time.unscaledTime</c> 을
    /// 쓰고 있어서(<c>CharacterRosterPanel</c>·<c>BgmService</c>) 화면과 음악은 그대로 살아 있다.
    /// 되돌리기도 한 줄이라 재시작이 안전하다.
    ///
    /// <b>이 오브젝트 자체는 끄지 않는다</b> — 끄면 <see cref="Start"/> 가 돌지 않아
    /// <see cref="WaveManager.OnDefeat"/> 구독이 아예 걸리지 않고, 그러면 패배해도 이 패널이
    /// 영원히 안 나타난다. 그래서 <c>HUD_Defeat</c> 는 항상 활성으로 두고 자식
    /// <c>Body</c> 만 켜고 끈다 — 보스 체력바(<see cref="BossHealthPanel"/>)가 같은 이유로 쓰는 방식이다.
    /// 전체 화면을 덮는 반투명 이미지도 <c>Body</c> 쪽에 있어서, 숨어 있는 동안에는 클릭을
    /// 가로막지 않는다.
    /// </summary>
    public class DefeatPanel : MonoBehaviour
    {
        [Header("문구")]
        [SerializeField] string titleText = "패배";

        [Tooltip("사유를 알 수 없을 때 쓰는 기본 문구. 사유별 문구는 아래 두 필드가 쓰인다")]
        [SerializeField] string reasonText = "중앙 건물이 파괴되었습니다.";

        [Tooltip("성역 파괴로 졌을 때")]
        [SerializeField] string reasonNexusText = "중앙 건물이 파괴되었습니다.";

        [Tooltip("캐릭터 전멸로 졌을 때 (다시 생성할 에너지도, 남은 포탑도 없는 상태)")]
        [SerializeField] string reasonPartyText = "캐릭터가 전멸하고, 다시 세울 수단도 남지 않았습니다.";

        [Tooltip("{0}=도달 웨이브, {1}=생존 시간, {2}=남은 캐릭터 수")]
        [SerializeField] string summaryFormat = "웨이브 {0} 도달 · 생존 {1} · 남은 인원 {2}명";

        [SerializeField] string restartLabel = "다시 시작";

        [Header("동작")]
        [Tooltip("패배 시 Time.timeScale 을 0 으로 만들어 게임을 멈춘다. " +
                 "끄면 패배 화면만 뜨고 전투는 계속 진행된다(연출·디버그 확인용)")]
        [SerializeField] bool pauseGameOnDefeat = true;

        [Tooltip("패배 화면이 뜨기까지의 지연(초). 성역이 부서지는 순간을 잠깐 보여주기 위한 여유. " +
                 "Time.unscaledTime 기준이라 게임이 멈춰도 흐른다")]
        [Min(0f)] [SerializeField] float showDelaySeconds = 1.2f;

        GameObject _body;
        TMP_Text _title, _reason, _summary, _restartTextLabel;
        Button _restartButton;

        WaveManager _wave;

        /// <summary>게임 시작 시각(unscaled). 생존 시간 표시에 쓴다.</summary>
        float _startedAt;

        bool _defeated;
        float _showAt;
        bool _shown;

        // 패배 시점의 값을 찍어둔다 — 표시가 지연되는 동안 캐릭터가 더 죽어서
        // "패배한 순간의 결과"가 달라지면 안 된다.
        int _finalWave;
        float _finalSeconds;
        int _finalAlive;
        DefeatReason _finalReason;

        void Awake() => BuildBindings();

        void Start()
        {
            _startedAt = Time.unscaledTime;

            _wave = FindAnyObjectByType<WaveManager>();
            if (_wave != null) _wave.OnDefeat += HandleDefeat;
            else Debug.LogWarning("[패배] WaveManager 를 찾지 못했습니다. 패배 화면이 뜨지 않습니다.", this);

            if (_body != null) _body.SetActive(false);
        }

        void OnDestroy()
        {
            if (_wave != null) _wave.OnDefeat -= HandleDefeat;

            // ⚠️ 반드시 되돌린다 — 에디터의 Time.timeScale 은 <b>플레이 모드를 나가도 유지된다.</b>
            // 패배한 채로 Stop 을 누르면 timeScale 이 0 인 상태로 남아, 그 다음 플레이가
            // "눌렀는데 아무것도 안 움직인다"로 시작한다(진행상황 11절의 Run In Background 함정과
            // 같은 종류의, 원인을 찾기 매우 어려운 상태). 씬 재로드 경로도 이 OnDestroy 를 지나므로
            // 되돌리는 지점이 한 곳으로 모인다.
            if (_shown && pauseGameOnDefeat) Time.timeScale = 1f;
        }

        void Update()
        {
            // 지연 표시만 확인한다. timeScale 이 0 이어도 흐르도록 unscaledTime 을 쓴다.
            if (!_defeated || _shown) return;
            if (Time.unscaledTime < _showAt) return;

            Show();
        }

        // ------------------------------------------------------------------

        void HandleDefeat()
        {
            if (_defeated) return;
            _defeated = true;

            _finalWave = _wave != null ? _wave.WaveNumber : 0;
            _finalSeconds = Time.unscaledTime - _startedAt;
            _finalAlive = CountAliveCharacters();
            _finalReason = _wave != null ? _wave.Reason : DefeatReason.None;

            _showAt = Time.unscaledTime + showDelaySeconds;
        }

        /// <summary>패배 사유에 맞는 한 줄. 사유를 모르면 기본 문구로 떨어진다.</summary>
        string ReasonLine() => _finalReason switch
        {
            DefeatReason.NexusDestroyed    => reasonNexusText,
            DefeatReason.AllCharactersLost => reasonPartyText,
            _                              => reasonText,
        };

        void Show()
        {
            _shown = true;

            if (_title != null) _title.text = titleText;
            if (_reason != null) _reason.text = ReasonLine();
            if (_summary != null)
                _summary.text = string.Format(summaryFormat, _finalWave,
                                              FormatDuration(_finalSeconds), _finalAlive);
            if (_restartTextLabel != null) _restartTextLabel.text = restartLabel;

            if (_body != null) _body.SetActive(true);

            // ★★ <b>떠 있는 전투 연출을 먼저 치운다</b> (2026-08-21 · 유저 리포트:
            //   *"아르세니아 이펙트가 중앙 건물 청크에 걸려서 장식물처럼 안없어져"*).
            //
            //   ⚠ <b>아래 timeScale = 0 보다 먼저여야 한다.</b> 0 이 되면 연출의 타이머도
            //     그것을 지워 줄 <c>SacredZone</c> 도 <b>같이 굳어</b> 영영 안 사라진다.
            //     패배는 성역이 부서질 때 일어나므로 굳은 그림은 정확히 «중앙 건물 자리» 에
            //     남는다 — 유저가 본 그 «장식물» 이다.
            Combat.CombatProjectileFx.ClearAll();

            // 멈추는 것은 화면을 띄우는 시점이다 — 성역이 부서지는 순간은 그대로 보여준다.
            if (pauseGameOnDefeat) Time.timeScale = 0f;

            HudLog.Add($"패배 — {ReasonLine()}", HudLogKind.Danger);
        }

        /// <summary>
        /// 씬을 다시 로드해 처음부터 시작한다.
        ///
        /// <b>씬 재로드를 고른 이유</b> — 상태를 손으로 되돌리려면 유닛·자원·웨이브 번호·침식·
        /// 건설 예정지·집결지·안개까지 전부 초기화해야 하고, 그중 하나라도 빠뜨리면 이전 판의
        /// 흔적이 남는다. 이 프로젝트는 씬이 하나뿐이고(<c>Proto_01</c>) 맵도 시작 시 생성되므로
        /// 재로드가 가장 확실하고 짧다. <b>timeScale 을 먼저 되돌리는 것이 중요하다</b> — 0 인
        /// 상태로 새 씬이 시작되면 아무것도 움직이지 않는 채로 멈춘 게임이 된다.
        /// </summary>
        /// ★★ <b>2026-08-21 — 씬만 다시 열면 안 된다.</b> 유저 리포트:
        ///   *"캐릭터가 죽으면 캐릭터 생성이 안되는 버그"*.
        ///   «이미 등장한 인물» 기록은 <c>static</c> 이라 <b>씬을 다시 열어도 살아남는다</b> —
        ///   여기서 비우지 않으면 인물 11명이 다 소진된 채 새 판이 시작되고,
        ///   시작 캐릭터 3명조차 못 나오면서 「캐릭터 생성」이 영구히 죽는다.
        ///   판을 비우는 순서는 <see cref="Save.RunResetService"/> 한 곳에 있다.
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
                Debug.LogError("[패배] HUD_Defeat/Body 를 찾지 못했습니다.", this);
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
