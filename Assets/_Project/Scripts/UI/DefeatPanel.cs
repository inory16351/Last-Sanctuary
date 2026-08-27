using System.Collections;
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
    ///
    /// ═══════════════════════════════════════════════════════════════
    ///  ★★★ <b>2026-08-27 — 한꺼번에 뜨는 대신 «떠오른다»</b>
    /// ═══════════════════════════════════════════════════════════════
    /// 유저 지시: *"게임 오버 시 게임 오버 배경 Defeat_bg.png 가 천천히 페이드인 되면서
    /// 떠오르고"* · *"그냥 배경에 UI 뜨는걸로 하자 뒷배경으로 깔고 UI가 배경이 다
    /// 떠오른 후에 페이드 인으로 떠오르게 해줘"*.
    ///
    /// 두 박자다 — ① <b>어둠 + 배경 그림</b>이 아래에서 올라오며 서서히 나타나고,
    /// 그것이 <b>다 끝난 뒤에</b> ② 문구·버튼이 같은 결로 한 번 더 떠오른다.
    /// 지시가 «천천히» 라 <c>backgroundRiseSeconds</c> 기본값을 <b>3.2초</b>로 잡았다.
    ///
    /// ★ <b>요약 줄과 「패배」 제목은 뺐다</b>(유저 확정) — 남는 것은 <b>사유 두 줄</b>과
    ///   「다시 시작」 버튼이다. 씬에서 지우지 않고 <c>showTitle</c>·<c>showSummary</c> 로
    ///   끈다 — 이유는 그 칸 옆의 ⚠ 에 적었다.
    /// ★★★ <b>배경은 코드가 짓는다</b>(<see cref="BuildBackground"/>) — 이유가 셋이다.
    ///   ① MCP 로는 씬에 <b>스프라이트 참조</b>를 넣을 수 없다(진행상황 8절 1번).
    ///   ② MCP 로는 <b>형제 순서</b>를 정할 수 없다 — 새 <c>Image</c> 를 씬에 넣으면
    ///      글자 위로 올라갈 위험이 있다(164-5절의 그 제약).
    ///   ③ ⚠⚠ <b>씬에 꽂아 두면 지워진다.</b> 164-3절은 이 그림을 <c>Panel</c> 의
    ///      <c>Image.sprite</c> 에 꽂았는데 <b>지금 씬에는 그 참조가 한 군데도 없다</b>
    ///      (실측 — 씬 파일에서 <c>DefeatBg.png</c> 의 guid 가 <b>0번</b> 나온다).
    ///      범인은 에디터 도구 <c>UiSkinApplier</c> 다 — 그 도구의 <c>Plates</c> 목록에
    ///      <b><c>HUD_Defeat/Body/Panel</c> 이 들어 있어서</b>, 한 번 돌릴 때마다
    ///      그 칸을 <c>Hud_Plate</c> 로 <b>덮는다</b>. 즉 «누가 실수로 지웠다» 가 아니라
    ///      <b>구조적으로 덮일 자리</b>였다. 코드가 자기 오브젝트를 따로 지으면
    ///      그 도구가 손댈 것이 없다.
    /// ⚠ 같은 이유로 <b><c>Panel</c> 의 색·그림은 언제든 그 도구가 되돌릴 수 있다</b> —
    ///   판을 «조금 투명하게»(a 0.8) 해 둔 것도 그 도구를 다시 돌리면 0.95 로 돌아간다.
    ///   거기에 의존하는 연출을 만들지 말 것.
    /// </summary>
    public class DefeatPanel : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  ★★ 문구 — <b>이벤트 대사의 어투</b>로 다시 썼다 (2026-08-25 · 유저 지시:
        //     *"패배 문구도 좀 수정 문어체로 이벤트처럼"*)
        // ══════════════════════════════════════════════════════════════════
        // 예전 문구는 <b>기능 설명</b>이었다 — 「중앙 건물이 파괴되었습니다.」는 이 게임의
        // 화자가 쓰는 말이 아니라 <b>로그</b>의 말이다. 이벤트 결과 대사(<c>resultScript</c>)를
        // 보면 이 게임의 어투는 정해져 있다 —
        //
        //     "천사들은 유해를 삼켜 성역의 양분으로 바꿉니다.
        //      목을 넘긴 뒤의 뒷맛은 생각보다 오래 남습니다."
        //
        // <b>두 줄 · «~습니다» · 첫 줄은 사실, 둘째 줄은 여운</b>. 그 모양을 그대로 따랐다.
        //
        // ★ <b>스트링 테이블로 옮겼다</b> — 진행상황 미결 108번(*"패배·승리 화면 문구가
        //   씬·코드에 하드코딩"*)의 절반을 여기서 닫는다. 필드는 <b>폴백</b>으로 남는다.
        // ⚠ 그래도 <b>씬에 직렬화된 값이 이 코드보다 이긴다</b> — 씬의 값은 «키가 비었을 때의
        //   폴백» 으로 쓰이므로, 키가 표에 있으면 표가 이긴다. 그래서 문구를 고칠 자리는
        //   이제 <b>스트링 키 테이블 하나</b>다.

        [Header("문구 (스트링 키가 정본 · 아래 문자열은 폴백)")]
        [SerializeField] string titleKey = "defeat_title";
        [SerializeField] string titleText = "패배";

        [Tooltip("사유를 알 수 없을 때 쓰는 기본 문구. 사유별 문구는 아래 두 쌍이 쓰인다")]
        [SerializeField] string reasonKey = "defeat_reason_unknown";
        [SerializeField] string reasonText =
            "성역이 무너졌습니다.\n무엇이 마지막 한 걸음이었는지는 아무도 적어두지 못했습니다.";

        [Tooltip("성역 파괴로 졌을 때")]
        [SerializeField] string reasonNexusKey = "defeat_reason_nexus";
        [SerializeField] string reasonNexusText =
            "심장부의 박동이 멎었습니다.\n성역을 지탱하던 빛이 마지막으로 한 번 떨리고, 꺼졌습니다.";

        [Tooltip("캐릭터 전멸로 졌을 때 (다시 생성할 에너지도, 남은 포탑도 없는 상태)")]
        [SerializeField] string reasonPartyKey = "defeat_reason_party";
        [SerializeField] string reasonPartyText =
            "문턱을 지키던 이들이 모두 쓰러졌습니다.\n다시 부를 이름도, 그 값을 치를 양분도 남지 않았습니다.";

        [Tooltip("{0}=도달 웨이브, {1}=생존 시간, {2}=남은 캐릭터 수")]
        [SerializeField] string summaryFormat = "웨이브 {0} 도달 · 생존 {1} · 남은 인원 {2}명";

        [SerializeField] string restartLabel = "다시 시작";

        // ══════════════════════════════════════════════════════════════════
        //  ★★★ <b>패배 연출 — 배경이 «떠오른» 뒤에 UI 가 «떠오른다»</b>
        //     (2026-08-27 · 유저 지시: *"게임 오버 시 게임 오버 배경 Defeat_bg.png 가 천천히
        //      페이드인 되면서 떠오르고"* → *"그냥 배경에 UI 뜨는걸로 하자 뒷배경으로 깔고
        //      UI가 배경이 다 떠오른 후에 페이드 인으로 떠오르게 해줘"*)
        // ══════════════════════════════════════════════════════════════════
        // 예전에는 <c>Body</c> 를 켜는 순간 <b>전부 한꺼번에</b> 나타났다. 이제 두 박자다 —
        //   ① 화면을 덮는 어둠과 <b>배경 그림</b>이 함께 «아래에서 위로» 떠오르며 나타난다.
        //   ② 그것이 <b>다 끝난 뒤에</b> 문구·버튼이 같은 결로 한 번 더 떠오른다.
        //
        // ★ <b>배경은 코드가 짓는다.</b> MCP 로는 씬에 스프라이트 참조를 넣을 수 없고
        //   (진행상황 8절 1번), 형제 순서도 정할 수 없다(164-5절). 코드가 만들면 둘 다
        //   해결되고 — <c>SetAsFirstSibling</c> 로 <b>패널보다 뒤</b>임이 못박힌다 —
        //   사람이 하이라키에서 드래그로 깨뜨릴 수도 없다.
        // ⚠ <b>액자(RectMask2D) + AspectRatioFitter(EnvelopeParent)</b> 로 담는다.
        //   <c>preserveAspect</c> 로는 화면비가 다를 때 <b>검은 띠</b>가 생긴다
        //   (<see cref="EndingDirector"/> 가 같은 이유로 같은 구조를 쓴다).
        // ⚠ 떠오르는 거리만큼 그림을 <b>미리 키워 둔다</b>(액자 안쪽 칸을 위아래로
        //   <c>backgroundRisePixels</c> 만큼 넓힌다) — 안 그러면 다 떠오르기 전까지
        //   <b>화면 아래에 빈 띠</b>가 보인다.

        [Header("패배 연출")]
        [Tooltip("배경 그림의 Resources 경로(확장자 없음). 비우면 배경 없이 UI 만 뜬다")]
        [SerializeField] string backgroundResource = "UI/Result/DefeatBg";

        [Tooltip("배경이 다 떠오르기까지의 시간(초). 지시가 «천천히» 라 길게 잡았다. " +
                 "Time.unscaledDeltaTime 기준이라 게임이 멈춰도 흐른다")]
        [Min(0f)] [SerializeField] float backgroundRiseSeconds = 3.2f;

        [Tooltip("배경이 아래에서 떠오르는 거리(px · 1920×1080 기준)")]
        [Min(0f)] [SerializeField] float backgroundRisePixels = 90f;

        [Tooltip("배경이 다 뜬 뒤 UI 가 나타나기까지의 뜸(초)")]
        [Min(0f)] [SerializeField] float uiDelaySeconds = 0.35f;

        [Tooltip("UI(문구·버튼)가 떠오르며 나타나는 시간(초)")]
        [Min(0f)] [SerializeField] float uiRiseSeconds = 1.1f;

        [Tooltip("UI 가 떠오르는 거리(px). 배경보다 짧아야 «뒤가 크게, 앞이 작게» 로 보인다")]
        [Min(0f)] [SerializeField] float uiRisePixels = 40f;

        // ★ 유저 확정 — <b>사유 두 줄만</b> 남긴다(«패배» 제목도, «웨이브 N 도달 · 생존 …»
        //   요약도 뺀다). 요약은 <b>기록</b>의 말투라 연출과 어울리지 않는다.
        // ⚠ <b>씬에서 지우거나 끄지 않았다</b> — 181-6절과 같은 판단이다. 씬에서 끄면
        //   «누가 왜 껐는지» 가 아무 데도 안 남고, 되살릴 때 자리를 다시 재야 한다.
        //   칸을 켜면 그대로 돌아온다(자리는 씬에 그대로 있다).
        [Tooltip("켜면 「패배」 제목 줄을 다시 보여준다")]
        [SerializeField] bool showTitle = false;

        [Tooltip("켜면 「웨이브 N 도달 · 생존 …」 요약 줄을 다시 보여준다")]
        [SerializeField] bool showSummary = false;

        /// <summary>
        /// 문구 칸을 표의 값으로 갈아 끼운다(2026-08-26 · 하드코딩 이관).
        /// ★ 제목·패배 사유는 이미 <c>…Key</c> 칸으로 표를 보고 있다 — 여기는 나머지다.
        /// </summary>
        void LocalizeLabels()
        {
            summaryFormat = HudTheme.T("ui_defeat_summary", summaryFormat);
            restartLabel = HudTheme.T("ui_restart", restartLabel);
        }

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

        /// <summary>화면을 덮는 어둠(<c>Body</c> 자신의 그림). 배경과 함께 서서히 짙어진다.</summary>
        Image _curtain;
        /// <summary>씬에 직렬화된 어둠의 진하기 — 연출이 끝났을 때의 목표값이다.</summary>
        float _curtainAlpha = 1f;

        /// <summary>배경 그림을 담은 칸(액자 안쪽). <b>이것이 떠오른다</b>.</summary>
        RectTransform _backgroundRect;
        CanvasGroup _backgroundGroup;

        /// <summary>문구·버튼이 든 패널. 배경이 다 뜬 뒤에 떠오른다.</summary>
        RectTransform _panelRect;
        CanvasGroup _panelGroup;
        /// <summary>패널의 제자리(씬 값). 떠오르기 «전» 위치는 여기서 아래로 뺀 값이다.</summary>
        float _panelBaseY;

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
            // ★ 문구를 스트링 표에서 가져온다(2026-08-26 하드코딩 이관). 언어가 바뀌면 다시 부른다.
            LocalizeLabels();
            Data.StringTable.OnLanguageChanged -= LocalizeLabels;
            Data.StringTable.OnLanguageChanged += LocalizeLabels;
            _startedAt = Time.unscaledTime;

            _wave = FindAnyObjectByType<WaveManager>();
            if (_wave != null) _wave.OnDefeat += HandleDefeat;
            else Debug.LogWarning("[패배] WaveManager 를 찾지 못했습니다. 패배 화면이 뜨지 않습니다.", this);

            if (_body != null) _body.SetActive(false);
        }

        void OnDestroy()
        {
            Data.StringTable.OnLanguageChanged -= LocalizeLabels;

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

        /// <summary>
        /// 패배 사유에 맞는 한 줄. 사유를 모르면 기본 문구로 떨어진다.
        /// ★ 스트링 테이블이 정본이고 직렬화된 문자열이 폴백이다(위 문구 절의 ★).
        /// </summary>
        string ReasonLine() => _finalReason switch
        {
            DefeatReason.NexusDestroyed    => Data.StringTable.Get(reasonNexusKey, reasonNexusText),
            DefeatReason.AllCharactersLost => Data.StringTable.Get(reasonPartyKey, reasonPartyText),
            _                              => Data.StringTable.Get(reasonKey, reasonText),
        };

        void Show()
        {
            _shown = true;

            if (_title != null) _title.text = Data.StringTable.Get(titleKey, titleText);
            if (_reason != null) _reason.text = ReasonLine();
            if (_summary != null)
                _summary.text = string.Format(summaryFormat, _finalWave,
                                              FormatDuration(_finalSeconds), _finalAlive);
            if (_restartTextLabel != null) _restartTextLabel.text = restartLabel;

            // ★ <b>연출의 «0초» 를 먼저 만들고</b> 켠다 — 켠 뒤에 값을 넣으면 첫 프레임에
            //   완성된 화면이 <b>한 번 번쩍</b>인다(182-2절의 «글자를 먼저 쓰고 창을 켠다» 와
            //   같은 자리, 부호만 반대다).
            PrimePresentation();

            if (_body != null) _body.SetActive(true);

            // ⚠ <b>코루틴은 이 컴포넌트가 돌린다</b> — <c>HUD_Defeat</c> 는 항상 활성이므로
            //   <c>Body</c> 를 켜고 끄는 것과 무관하게 연출이 끊기지 않는다(기존 설계 덕이다).
            //   ⚠ 아래에서 <c>timeScale</c> 을 0 으로 만들지만 코루틴은 <b>계속 돈다</b> —
            //     연출이 전부 <c>unscaledDeltaTime</c> 이기 때문이다(<see cref="RiseIn"/>).
            StartCoroutine(PlayPresentation());

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

            // ⚠ {0} = 패배 사유 한 줄(ReasonLine 이 이미 표를 거친다). 지우지 말 것.
            HudLog.Add(string.Format(HudTheme.T("log_defeat", "패배 — {0}"), ReasonLine()),
                       HudLogKind.Danger);
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

        // ═══════════════════════════════════════════════════════════════════
        //  연출 — ① 어둠+배경이 떠었다가 → ② 다 뜨면 UI 가 떠오른다
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 연출의 첫 프레임 상태를 만든다 — <b>전부 투명하고, 제자리보다 아래에 있다</b>.
        /// ⚠ <c>blocksRaycasts</c> 를 끈다 — 안 끄면 문구가 보이기도 전에
        /// 「다시 시작」이 <b>눌린다</b>(투명한 버튼도 클릭은 받는다).
        /// </summary>
        void PrimePresentation()
        {
            if (_curtain != null)
            {
                Color c = _curtain.color;
                _curtain.color = new Color(c.r, c.g, c.b, 0f);
            }

            if (_backgroundGroup != null) _backgroundGroup.alpha = 0f;
            if (_backgroundRect != null)
                _backgroundRect.anchoredPosition = new Vector2(0f, -backgroundRisePixels);

            if (_panelGroup != null)
            {
                _panelGroup.alpha = 0f;
                _panelGroup.interactable = false;
                _panelGroup.blocksRaycasts = false;
            }
            if (_panelRect != null)
                _panelRect.anchoredPosition = new Vector2(_panelRect.anchoredPosition.x,
                                                          _panelBaseY - uiRisePixels);
        }

        /// <summary>
        /// ⚠ <b>전부 <see cref="Time.unscaledDeltaTime"/> 이다.</b> 이 연출이 도는 동안
        /// <see cref="Show"/> 가 <c>timeScale</c> 을 0 으로 만든다 — 스케일된 시간을 쓰면
        /// 그 순간 <b>영원히 멈춘 페이드</b>가 된다.
        /// </summary>
        IEnumerator PlayPresentation()
        {
            yield return RiseIn(_backgroundGroup, _backgroundRect, backgroundRisePixels,
                                0f, backgroundRiseSeconds, fadeCurtain: true);

            float wait = 0f;
            while (wait < uiDelaySeconds) { wait += Time.unscaledDeltaTime; yield return null; }

            yield return RiseIn(_panelGroup, _panelRect, uiRisePixels,
                                _panelBaseY, uiRiseSeconds, fadeCurtain: false);

            if (_panelGroup != null)
            {
                _panelGroup.interactable = true;
                _panelGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// «아래에서 떠오르며 나타난다» 한 번. <paramref name="baseY"/> 가 도착점이고
        /// 출발점은 거기서 <paramref name="rise"/> 만큼 아래다.
        ///
        /// ★ <b>부드럽기(smoothstep)를 쓴다</b> — 선형으로 움직이면 끝나는 순간 «툭» 멈춰
        /// 미끄러지다 만 것처럼 보인다. 알파와 자리에 <b>같은 값</b>을 먹여 둘이 어긋나지
        /// 않게 한다.
        /// </summary>
        IEnumerator RiseIn(CanvasGroup group, RectTransform rect, float rise,
                           float baseY, float seconds, bool fadeCurtain)
        {
            float t = 0f;
            while (t < 1f)
            {
                t = seconds <= 0f ? 1f : Mathf.Clamp01(t + Time.unscaledDeltaTime / seconds);
                float e = t * t * (3f - 2f * t);          // smoothstep

                if (group != null) group.alpha = e;
                if (rect != null)
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,
                                                        baseY - rise * (1f - e));
                if (fadeCurtain && _curtain != null)
                {
                    Color c = _curtain.color;
                    _curtain.color = new Color(c.r, c.g, c.b, _curtainAlpha * e);
                }
                yield return null;
            }
        }

        /// <summary>
        /// 배경 그림을 <b>코드가 짓는다</b> — <c>Body</c> 의 <b>첫 형제</b>로 넣어
        /// 패널·문구보다 <b>뒤</b>에 그려지게 한다(한 캔버스 안의 순서는 형제 순서다 —
        /// 164-5절). 세 겹인 이유는 위 «패배 연출» 절의 ⚠ 둘이다:
        /// <code>
        ///   Background        액자 — 화면에 딱 맞게 · RectMask2D 로 넘치는 부분을 자른다
        ///     Inner           떠오르는 칸 — 액자보다 위아래로 rise 만큼 크다 · CanvasGroup
        ///       Sprite        그림 — AspectRatioFitter(EnvelopeParent) 로 «꽉 채우기»
        /// </code>
        /// 그림을 못 찾으면 <b>아무것도 만들지 않고</b> 경고만 남긴다 — 배경 없이도
        /// 패배 화면은 떠야 한다.
        /// ⚠ 새 PNG 를 넣을 때는 <b>Sprite 로 들여왔는지</b> 볼 것(164-7절 —
        /// Default 텍스처면 <c>Resources.Load&lt;Sprite&gt;</c> 가 null 이라 검은 화면이 된다).
        /// </summary>
        void BuildBackground()
        {
            if (_body == null || string.IsNullOrWhiteSpace(backgroundResource)) return;

            var sprite = Resources.Load<Sprite>(backgroundResource.Trim());
            if (sprite == null)
            {
                Debug.LogWarning($"[패배] 배경 'Resources/{backgroundResource}' 를 찾지 못했습니다 — " +
                                 "배경 없이 문구만 뜹니다.", this);
                return;
            }

            var frame = new GameObject("Background", typeof(RectTransform), typeof(RectMask2D));
            RectTransform frameRect = (RectTransform)frame.transform;
            frameRect.SetParent(_body.transform, false);
            frame.layer = _body.layer;
            Stretch(frameRect, 0f);
            frameRect.SetAsFirstSibling();      // ★ 패널·문구보다 «뒤»

            var inner = new GameObject("Inner", typeof(RectTransform), typeof(CanvasGroup));
            _backgroundRect = (RectTransform)inner.transform;
            _backgroundRect.SetParent(frameRect, false);
            inner.layer = _body.layer;
            Stretch(_backgroundRect, backgroundRisePixels);
            _backgroundGroup = inner.GetComponent<CanvasGroup>();
            _backgroundGroup.blocksRaycasts = false;
            _backgroundGroup.alpha = 0f;

            var art = new GameObject("Sprite", typeof(RectTransform), typeof(Image),
                                     typeof(AspectRatioFitter));
            RectTransform artRect = (RectTransform)art.transform;
            artRect.SetParent(_backgroundRect, false);
            art.layer = _body.layer;
            Stretch(artRect, 0f);

            var image = art.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = false;       // ⚠ 켜면 화면비가 다를 때 검은 띠가 생긴다

            var fitter = art.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height
                : 16f / 9f;
        }

        /// <summary>부모에 꽉 차게 편다. <paramref name="overscan"/> 만큼 위아래로 더 키운다.</summary>
        static void Stretch(RectTransform rect, float overscan)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(0f, -overscan);
            rect.offsetMax = new Vector2(0f, overscan);
        }

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

            // ── 연출에 쓰는 것들 (2026-08-27) ─────────────────────────────

            // 화면을 덮는 어둠은 <c>Body</c> 자신의 그림이다. 진하기는 <b>씬에 직렬화된
            // 값</b>을 그대로 목표로 삼는다 — 코드에 0.82 를 박아 두면, 사람이 씬에서
            // 어둠을 새로 맞춰도 <b>연출이 매번 자기 값으로 되돌려 놓는다</b>.
            _curtain = _body.GetComponent<Image>();
            if (_curtain != null) _curtainAlpha = _curtain.color.a;

            // 문구·버튼을 한 번에 투명하게 하려면 <c>CanvasGroup</c> 이 필요하다.
            // ★ 없으면 <b>붙인다</b> — 씬에서 누가 지워도 연출이 살아 있게 하는
            //   «안전망» 이다(<c>UiFillBar.Prepare</c>·<c>CharacterKills.EnsureOn</c> 와 같은 결).
            Transform panel = transform.Find("Body/Panel");
            if (panel != null)
            {
                _panelRect = panel as RectTransform;
                if (_panelRect != null) _panelBaseY = _panelRect.anchoredPosition.y;

                _panelGroup = panel.GetComponent<CanvasGroup>();
                if (_panelGroup == null) _panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            }

            // ★ 유저 확정 — <b>사유 두 줄만</b> 남긴다. 씬에서 지우지 않은 이유는
            //   위 «패배 연출» 절의 ⚠ 다 — 칸을 다시 켜면 자리가 그대로 돌아온다.
            if (_title != null) _title.gameObject.SetActive(showTitle);
            if (_summary != null) _summary.gameObject.SetActive(showSummary);

            BuildBackground();
        }

        TMP_Text FindText(string path)
        {
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<TMP_Text>() : null;
        }
    }
}
