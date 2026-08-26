using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LastSanctuary.Save;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 로비(타이틀) 화면 (2026-08-18 신설 — 유저 지시 <i>"로비 화면 만들어 … 환경설정 /
    /// 이어하기 / 새로하기 / 게임 종료 버튼 … 게임 시작하면 로비화면에서 시작"</i>).
    ///
    /// <b>왜 별도 씬인가</b> — "저장하고 로비로 돌아가기" 와 "새로하기" 가 둘 다 <b>게임 판을
    /// 완전히 새로 만드는</b> 동작이다. 게임 씬(<c>Proto_01</c>) 안에 덮개로 만들면 맵·유닛·
    /// 웨이브가 뒤에서 이미 돌고 있어서, 새로하기를 누를 때마다 그것들을 <b>손으로 되돌려야</b>
    /// 한다 — 되돌릴 것을 하나라도 빠뜨리면 이전 판의 잔재가 새 판에 섞인다.
    /// 씬을 새로 부르면 그 문제가 통째로 없어진다.
    ///
    /// <b>연출</b>(유저 지시 2026-08-18 — <b>순서대로 하나씩</b>, 겹치지 않는다):
    /// <code>
    ///   ① 흰 화면에서 시작 → 흰 막이 걷히며 배경이 드러난다   (screenFadeSeconds)
    ///   ② 그것이 <b>완전히</b> 끝난 뒤 타이틀이 화면 중앙에서 제자리로 떠오르며 밝아진다
    ///   ③ 타이틀이 다 떠오른 뒤 버튼들이 <b>순차적으로</b> 페이드 인 한다
    /// </code>
    /// <b>2026-08-24 (유저 지시)</b> — 연출을 손볼 두 가지:
    /// <code>
    ///   ① 버튼은 <b>다 뜬 뒤에야</b> 눌린다. 예전에는 알파만 0 이라 «아직 안 보이는 버튼»
    ///      자리를 누르면 그대로 게임이 시작됐다 (EnsureGroup 의 blocksRaycasts).
    ///   ② 연출 중에 <b>화면을 누르면 끝까지 감긴다</b> (Update / SkipToEnd).
    /// </code>
    ///
    /// 시간은 <see cref="Time.unscaledDeltaTime"/> 으로 잰다 —
    /// 게임에서 일시정지(<c>timeScale = 0</c>)한 채 로비로 나오면 연출이 멈춰버린다.
    /// (<see cref="SettingsPanel"/> 이 씬을 넘기기 전에 되돌리지만, 그 한 곳에만 의존하지 않는다.)
    /// </summary>
    public class LobbyPanel : MonoBehaviour
    {
        [Header("하이라키 이름 (MCP 로는 참조를 못 넣어 이름으로 찾는다 — 진행상황 8절 4번)")]
        [SerializeField] string titlePath = "Title";
        [SerializeField] string continueButtonPath = "Menu/ContinueButton";
        [SerializeField] string newGameButtonPath = "Menu/NewGameButton";
        [SerializeField] string settingsButtonPath = "Menu/SettingsButton";
        [SerializeField] string quitButtonPath = "Menu/QuitButton";
        [SerializeField] string savedAtPath = "Menu/SavedAt";
        [SerializeField] string settingsWindowPath = "SettingsWindow";

        [Header("그림 (2026-08-18 — 볼트의 타이틀·배경 반영)")]
        [Tooltip("배경 액자(RectMask2D). 이 아래의 Sprite 에 그림이 들어간다")]
        [SerializeField] string backgroundPath = "Background";

        [Tooltip("배경 그림을 그리는 Image. 그 부모가 잘라내는 액자다(RectMask2D)")]
        [SerializeField] string backgroundSpritePath = "Background/Sprite";

        [Tooltip("맨 앞을 덮는 흰 막. 이것이 걷히면서 화면이 페이드 인 한다")]
        [SerializeField] string curtainPath = "Curtain";

        [Tooltip("타이틀 뒤에 깔아 배경의 밝은 후광을 누르는 어두운 원(2026-08-18 유저 확정)")]
        [SerializeField] string vignettePath = "Vignette";

        [SerializeField] string vignetteResource = "UI/Lobby/TitleVignette";

        [Tooltip("버튼 네 개가 공통으로 쓰는 판 그림. 그림자는 그림에 구워져 있다")]
        [SerializeField] string buttonResource = "UI/Lobby/LobbyButton";

        [Tooltip("Resources 경로 — ⚠ 스프라이트 참조는 MCP 로 인스펙터에 못 넣는다(8절 4번)")]
        [SerializeField] string backgroundResource = "UI/Lobby/LobbyBg";

        [SerializeField] string titleResource = "UI/Lobby/LobbyTitle";

        [Header("씬")]
        [Tooltip("게임 본편 씬 이름. 빌드 세팅에 들어 있어야 한다")]
        [SerializeField] string gameSceneName = "Proto_01";

        /// <summary>
        /// <b>새로하기</b>만 거쳐 가는 오프닝 씬 (2026-08-24 · 유저 지시
        /// <i>"오프닝 … 새 게임 시작하면 나오는 거고"</i>).
        ///
        /// ★ <b>이어하기와 갈라놓은 이유</b> — 두 버튼이 <see cref="gameSceneName"/> 하나를
        ///   같이 쓰고 있었다. 거기에 오프닝을 끼우면 <b>이어하기에도</b> 오프닝이 붙어,
        ///   판을 이어갈 때마다 90초짜리 연출을 다시 보게 된다.
        /// ⚠ 비워두면 오프닝을 건너뛰고 곧바로 <see cref="gameSceneName"/> 으로 간다 —
        ///   오프닝 씬을 빌드 세팅에서 뺀 채로도 새로하기가 죽지 않게 하는 안전판이다.
        /// </summary>
        [Tooltip("새로하기에서 거쳐 갈 오프닝 씬. 비우면 오프닝 없이 바로 본편으로 간다")]
        [SerializeField] string openingSceneName = "Opening";

        [Header("페이드 인")]
        [Tooltip("흰 화면이 걷히는 시간(초). 이것이 <b>완전히</b> 끝난 뒤에 타이틀이 시작한다 " +
                 "(유저 지시 2026-08-18)")]
        [Min(0f)] [SerializeField] float screenFadeSeconds = 2f;

        [Tooltip("타이틀 이미지가 떠오르는 시간(초)")]
        [Min(0f)] [SerializeField] float titleFadeSeconds = 2.4f;

        [Tooltip("타이틀이 떠오르면서 올라오는 높이(px). 화면 <b>중앙</b>에서 출발해 제자리로 " +
                 "올라오는 값이다(200 ≈ 중앙). 0 이면 제자리에서 밝아지기만 한다")]
        [Min(0f)] [SerializeField] float titleRisePixels = 200f;

        [Tooltip("타이틀이 다 뜬 뒤 첫 버튼이 뜨기까지 기다리는 시간(초)")]
        [Min(0f)] [SerializeField] float menuDelaySeconds = 0.25f;

        [Tooltip("버튼 하나가 떠오르는 시간(초)")]
        [Min(0f)] [SerializeField] float buttonFadeSeconds = 0.45f;

        [Tooltip("버튼과 버튼 사이의 간격(초). 이 값 때문에 '순차적으로' 뜬다")]
        [Min(0f)] [SerializeField] float buttonStaggerSeconds = 0.18f;

        [Header("문구")]
        [SerializeField] string savedAtFormat = "마지막 저장: {0}";
        [SerializeField] string noSaveText = "저장된 게임이 없습니다";

        CanvasGroup _titleGroup;

        /// <summary>
        /// 타이틀이 <b>도착해야 할 자리</b>. 씬에 잡아둔 값을 <see cref="Bind"/> 에서 기억한다 —
        /// 연출을 건너뛸 때도 여기로 되돌려야 하므로 <see cref="RiseIn"/> 안에 두면 안 된다.
        /// </summary>
        Vector2 _titleHome;

        /// <summary>연출이 도는 중인가. 이 동안만 «클릭하면 건너뛰기» 가 열려 있다.</summary>
        bool _introPlaying;
        CanvasGroup _vignetteGroup;
        CanvasGroup _curtainGroup;
        RectTransform _titleRect;
        Button _continueButton;
        Button _newGameButton;
        Button _settingsButton;
        Button _quitButton;
        TMP_Text _savedAt;
        LobbySettingsWindow _settingsWindow;

        /// <summary>페이드 인 순서대로의 버튼 묶음. 각자 <see cref="CanvasGroup"/> 을 갖는다.</summary>
        readonly List<CanvasGroup> _menuGroups = new List<CanvasGroup>();

        void Start()
        {
            LocalizeLabels();
            SaveService.ApplyVolume();     // 빌드를 새로 켰을 때 저장된 음량을 반영한다

            // 폰트는 씬에 이미 배선돼 있다 — 유저 지시 2026-08-18: <i>"폰트는 네오 둥근모
            // 베이크 해서 써라"</i>. 에디터 메뉴 <b>LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고
            // 씬에 적용</b> 이 이 씬의 TMP 전부에 정본 에셋을 붙인다.
            // ⚠ <b>로비에 글자를 새로 추가하면 그 메뉴를 다시 실행할 것.</b>
            Bind();
            _introPlaying = true;
            StartCoroutine(PlayIntro());
        }

        // ------------------------------------------------------------------

        void Bind()
        {
            ApplyArt();
            ApplyDrawOrder();

            _titleGroup = EnsureGroup(titlePath);
            _titleRect = transform.Find(titlePath) as RectTransform;
            _titleHome = _titleRect != null ? _titleRect.anchoredPosition : Vector2.zero;

            // 비네트는 타이틀과 <b>같이</b> 떠오른다 — 먼저 나오면 배경에 검은 얼룩이 지고,
            // 나중에 나오면 로고가 밝은 후광 위에 떠 있는 그 상태를 한 번 보여주게 된다.
            _vignetteGroup = EnsureGroup(vignettePath);

            // ⚠ 흰 막은 <b>덮은 상태로</b> 시작한다 — EnsureGroup 은 알파를 0 으로 두므로
            //   (페이드 인 하는 것들의 규칙) 여기만 따로 1 로 세운다.
            _curtainGroup = FindComponent<CanvasGroup>(curtainPath);
            if (_curtainGroup != null)
            {
                _curtainGroup.alpha = 1f;
                _curtainGroup.blocksRaycasts = false;   // 막이 버튼을 먹지 않게(연출용일 뿐이다)
            }

            _continueButton = FindComponent<Button>(continueButtonPath);
            _newGameButton = FindComponent<Button>(newGameButtonPath);
            _settingsButton = FindComponent<Button>(settingsButtonPath);
            _quitButton = FindComponent<Button>(quitButtonPath);
            _savedAt = FindComponent<TMP_Text>(savedAtPath);

            _settingsWindow = FindComponent<LobbySettingsWindow>(settingsWindowPath);
            if (_settingsWindow != null) _settingsWindow.Close();

            if (_continueButton != null) _continueButton.onClick.AddListener(HandleContinue);
            if (_newGameButton != null) _newGameButton.onClick.AddListener(HandleNewGame);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettings);
            if (_quitButton != null) _quitButton.onClick.AddListener(HandleQuit);

            RefreshSaveInfo();

            // 페이드 인 순서 — 유저가 제일 먼저 쓸 것부터.
            AddMenuGroup(continueButtonPath);
            AddMenuGroup(newGameButtonPath);
            AddMenuGroup(settingsButtonPath);
            AddMenuGroup(quitButtonPath);
        }

        /// <summary>
        /// 타이틀·배경 그림을 얹는다 (2026-08-18 — 유저 지시 <i>"타이틀 이미지랑 로비 배경 화면
        /// 볼트에 넣어놨으니까 확인하고 넣어"</i>. 99-6절의 회색 사각형 자리 표시를 대체한다).
        ///
        /// ⚠ <b>스프라이트는 오브젝트 참조라 MCP 로 인스펙터에 넣을 수 없다</b>(8절 4번) —
        /// 그래서 <c>Resources</c> 에서 <b>코드가 읽어 꽂는다</b>. 이 프로젝트의 초상화·일러스트가
        /// 전부 같은 방식이다.
        ///
        /// ⚠ 그림이 null 로 나오면 십중팔구 <c>.meta</c> 의 <c>textureType</c> 이 8(Sprite)이 아니다 —
        /// 히스톤 초상화가 인게임 모션으로 뜨던 그 함정이다(84-8절 ②). 조용히 넘기지 말고 경고한다.
        /// </summary>
        void ApplyArt()
        {
            Image background = FindComponent<Image>(backgroundSpritePath);
            if (background != null)
            {
                Sprite sprite = Load(backgroundResource);
                if (sprite != null)
                {
                    background.sprite = sprite;
                    background.color = Color.white;

                    // ★ <b>화면비가 달라도 빈 곳이 생기지 않게 cover 로 채운다</b> — preserveAspect
                    //   (contain)로 두면 16:10 같은 화면에서 위아래에 띠가 남는다(90-7절).
                    PortraitFit.Cover(background, 0.5f, 0.5f);
                }
            }

            // ★ 타이틀 뒤 비네트 — 배경에서 <b>가장 밝은 자리</b>(성 첨탑 + 붉은 후광)가 하필
            //   타이틀 자리라, 그 밝기를 눌러 로고가 앉을 어두운 자리를 만든다.
            //   ⚠ 배경 그림 자체를 어둡게 굽지 않는다 — 그러면 나중에 배경만 갈아끼울 때
            //     이 보정이 따라오지 않는다.
            Image vignette = FindComponent<Image>(vignettePath);
            if (vignette != null)
            {
                Sprite sprite = Load(vignetteResource);
                if (sprite != null)
                {
                    vignette.sprite = sprite;
                    vignette.color = Color.white;      // 어두움은 그림의 알파가 들고 있다
                    vignette.preserveAspect = false;   // 타원으로 늘려 쓴다
                }
            }

            Image title = FindComponent<Image>(titlePath);
            if (title == null) return;

            Sprite titleSprite = Load(titleResource);
            if (titleSprite == null) return;

            title.sprite = titleSprite;
            title.color = Color.white;

            // 타이틀은 <b>잘리면 안 된다</b> — 로고가 반쯤 사라지면 무엇인지 알 수 없다.
            // 그래서 배경과 달리 contain(preserveAspect)이다. 씬의 칸도 그림 비율로 잡아 뒀다.
            title.preserveAspect = true;

            ApplyButtonArt();
        }

        /// <summary>
        /// 버튼 네 개에 <b>같은 판 그림</b>을 깐다 (2026-08-18 유저 지시:
        /// <i>"버튼도 이미지 넣었으니까 자연스럽게 그림자 등등 효과 넣어서 적용"</i>).
        ///
        /// ★ 그림 하나를 넷이 나눠 쓴다 — 버튼마다 그림을 따로 두면 판 모양을 바꿀 때
        /// 네 군데를 고쳐야 하고, 하나를 빼먹으면 그 버튼만 다른 모양이 된다.
        ///
        /// ⚠ <b>그림자는 그림에 구워져 있다</b>(<c>import_lobby_art.py</c> 의 <c>BUTTON_FX</c>).
        /// 그래서 판보다 위아래로 여백이 있는 그림이고, 씬의 버튼 칸(70px)도 그만큼 크다 —
        /// <b>보이는 판은 52px</b> 이다. 칸 높이를 판 높이로 착각해 줄이면 그림자가 잘린다.
        ///
        /// ⚠ <c>preserveAspect</c> 를 <b>끈다</b> — 켜면 화면비가 다른 해상도에서 판이 칸
        /// 가운데로 모여 좌우에 빈 곳이 생긴다. 판은 가로로 늘어나도 이상하지 않은 그림이다.
        /// </summary>
        void ApplyButtonArt()
        {
            Sprite plate = Load(buttonResource);
            if (plate == null) return;

            ApplyPlate(continueButtonPath, plate);
            ApplyPlate(newGameButtonPath, plate);
            ApplyPlate(settingsButtonPath, plate);
            ApplyPlate(quitButtonPath, plate);
        }

        void ApplyPlate(string path, Sprite plate)
        {
            Image image = FindComponent<Image>(path);
            if (image == null) return;

            image.sprite = plate;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;

            // ⚠ 색은 <b>건드리지 않는다</b> — 버튼의 ColorTint 전이가 매 프레임 이 값을
            //   상태 색으로 덮어쓴다. 여기서 흰색으로 바꿔도 곧바로 normalColor 가 이긴다.
            //   판의 밝기는 씬의 Button 상태 색(정본)에서 정한다.
        }

        /// <summary>
        /// 겹치는 순서를 <b>코드가 정한다</b> — 배경 → 타이틀 → (메뉴 · 환경 설정 창) → 흰 막.
        ///
        /// ⚠ <b>MCP 로 만든 오브젝트는 형제 중 맨 마지막</b>에 들어와 <b>가장 위에</b> 그려진다.
        /// 그대로 두면 배경이 타이틀과 버튼을 덮는다. 겹침을 <b>형제 순서 한 곳</b>에서 정하는 것은
        /// 94-3절이 초상화에서 내린 결론과 같다.
        ///
        /// ★ <b>흰 막만은 맨 위</b>여야 한다 — 화면 전체를 덮는 것이 그 일이다.
        /// </summary>
        void ApplyDrawOrder()
        {
            int next = 0;
            Place(backgroundPath, ref next);
            Place(vignettePath, ref next);      // 배경보다 위 · 타이틀보다 아래
            Place(titlePath, ref next);

            Transform curtain = string.IsNullOrWhiteSpace(curtainPath)
                ? null : transform.Find(curtainPath);
            if (curtain != null) curtain.SetAsLastSibling();
        }

        void Place(string path, ref int index)
        {
            Transform node = string.IsNullOrWhiteSpace(path) ? null : transform.Find(path);
            if (node == null) return;

            node.SetSiblingIndex(index);
            index++;
        }

        static Sprite Load(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;

            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
                Debug.LogWarning($"[로비] 'Resources/{resourcePath}' 을 찾지 못했습니다. " +
                                 "Tools/import_lobby_art.py 를 돌렸는지, .meta 의 textureType 이 " +
                                 "8(Sprite)인지 확인해주세요.");
            return sprite;
        }

        void RefreshSaveInfo()
        {
            bool hasSave = SaveService.HasSave;

            // 저장이 없으면 "이어하기" 를 <b>끈다</b> — 눌러도 아무 일이 없는 버튼은 고장으로 보인다.
            if (_continueButton != null) _continueButton.interactable = hasSave;

            if (_savedAt == null) return;

            string savedAt = hasSave ? SaveService.SavedAtLabel() : string.Empty;
            _savedAt.text = string.IsNullOrEmpty(savedAt)
                ? noSaveText
                : string.Format(savedAtFormat, savedAt);
        }

        void AddMenuGroup(string path)
        {
            CanvasGroup group = EnsureGroup(path);
            if (group != null) _menuGroups.Add(group);
        }

        /// <summary>
        /// 페이드에 쓸 <see cref="CanvasGroup"/> 을 보장한다. 없으면 붙인다 —
        /// MCP 로 컴포넌트를 붙일 수는 있지만, 코드가 스스로 보장하면 씬이 되돌아가도 안 깨진다
        /// (<c>CharacterKills.EnsureOn</c> 이 같은 이유로 같은 방식을 쓴다).
        /// </summary>
        CanvasGroup EnsureGroup(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            Transform node = transform.Find(path);
            if (node == null) return null;

            if (!node.TryGetComponent(out CanvasGroup group))
                group = node.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;      // 연출이 시작되기 전에는 전부 숨어 있다

            // ★★ 2026-08-24 — <b>안 보이는 것은 눌리지도 않아야 한다</b> (유저 지시:
            //   *"버튼 등장시 지금 버튼 있는 부분 누르면 바로 들어가게 하지 말고"*).
            //   <c>alpha = 0</c> 은 <b>그리기만</b> 끈다 — 레이캐스트는 그대로 살아 있어서
            //   버튼이 뜨기 전에 그 자리를 누르면 «투명한 버튼» 이 그대로 눌렸다.
            //   ⚠ <c>interactable</c> 이 아니라 <c>blocksRaycasts</c> 를 끈다 —
            //     <c>interactable = false</c> 로 막으면 버튼이 <b>비활성 색</b>으로 떠올라
            //     페이드 인 하는 동안 회색으로 보인다(«이어하기» 의 진짜 비활성과 헷갈린다).
            group.blocksRaycasts = false;
            return group;
        }

        T FindComponent<T>(string path) where T : Component
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }

        // ------------------------------------------------------------------
        // 연출 — 타이틀 → 버튼 순차
        // ------------------------------------------------------------------

        IEnumerator PlayIntro()
        {
            // ① 흰 화면에서 시작해 배경이 드러난다 (유저 지시 2026-08-18:
            //    <i>"하얀색 배경에서 시작해서 화면 페이드 인"</i>).
            //
            // ★ 배경 자체의 알파를 올리지 않고 <b>덮고 있던 흰 막을 걷는다.</b> 두 방식의
            //   보이는 결과가 다르다 — 알파를 올리면 <b>검은 바닥에서</b> 그림이 차오르고,
            //   막을 걷으면 <b>흰 화면에서</b> 밝은 쪽부터 드러난다. 유저가 말한 것은 후자다.
            //
            // ★ <b>완전히 끝난 뒤에</b> 타이틀로 넘어간다(지시: "화면이 완전히 끝나면").
            //   여기서 기다리므로 앞 절처럼 겹쳐 시작하지 않는다.
            yield return FadeOut(_curtainGroup, screenFadeSeconds);

            // ② 타이틀이 화면 중앙에서 제자리로 떠오르며 밝아진다.
            //    비네트는 <b>제자리에서</b> 같은 시간에 걸쳐 같이 밝아진다 — 따라 움직이면
            //    어두운 얼룩이 화면을 타고 올라가는 것이 보인다.
            StartCoroutine(Fade(_vignetteGroup, titleFadeSeconds));
            yield return RiseIn(_titleGroup, _titleRect, _titleHome, titleFadeSeconds, titleRisePixels);

            if (menuDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(menuDelaySeconds);

            for (int i = 0; i < _menuGroups.Count; i++)
            {
                // ★ 앞 버튼이 <b>다 뜨기를 기다리지 않는다</b> — 간격(stagger)만 두고 다음을 시작해
                //   물결처럼 이어지게 한다. 다 기다리면 버튼 4개에 2.5초가 걸려 답답하다.
                // ★★ 2026-08-24 — 다 뜬 <b>그 버튼만</b> 눌리게 연다(FadeInButton).
                StartCoroutine(FadeInButton(_menuGroups[i], buttonFadeSeconds));

                if (buttonStaggerSeconds > 0f && i < _menuGroups.Count - 1)
                    yield return new WaitForSecondsRealtime(buttonStaggerSeconds);
            }

            // 마지막 버튼이 다 뜰 때까지 기다린 뒤에야 «연출이 끝났다» 로 본다 —
            // 그 전에 끄면 마지막 버튼이 떠오르는 동안 클릭이 «건너뛰기» 로 먹히지 않는다.
            yield return new WaitForSecondsRealtime(buttonFadeSeconds);
            _introPlaying = false;
        }

        /// <summary>
        /// 버튼 하나를 띄우고, <b>다 뜬 뒤에</b> 클릭을 연다.
        /// (<see cref="EnsureGroup"/> 이 꺼둔 <c>blocksRaycasts</c> 를 여기서만 되돌린다.)
        /// </summary>
        IEnumerator FadeInButton(CanvasGroup group, float seconds)
        {
            yield return Fade(group, seconds);
            if (group != null) group.blocksRaycasts = true;
        }

        // ------------------------------------------------------------------
        // 연출 건너뛰기 (2026-08-24 유저 지시: <i>"연출 나올때 화면 클릭하면 연출 스킵"</i>)
        // ------------------------------------------------------------------

        /// <summary>
        /// 연출이 도는 동안만 «아무 데나 누르면 끝까지 감기» 를 받는다.
        ///
        /// ⚠ 이 프로젝트는 <b>Input System 패키지 전용</b>이다(<c>activeInputHandler: 1</c>) —
        ///   <c>UnityEngine.Input</c> 을 쓰면 실행 시점에 예외가 난다.
        /// ★ 누르는 <b>순간</b>(wasPressedThisFrame)에 건너뛴다. 그 순간 버튼들은 아직
        ///   <c>blocksRaycasts = false</c> 라 <b>누름이 버튼에 등록되지 않았고</b>,
        ///   유니티 버튼은 «같은 대상에서 누르고 뗐을 때» 만 클릭이 되므로
        ///   손을 떼는 순간에도 그 버튼이 눌리지 않는다 — 건너뛰기가 곧바로
        ///   «이어하기» 로 이어지는 사고가 구조적으로 막힌다.
        /// </summary>
        void Update()
        {
            if (!_introPlaying) return;
            if (!SkipRequested()) return;

            StopAllCoroutines();
            SkipToEnd();
        }

        static bool SkipRequested()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        /// <summary>연출의 <b>마지막 프레임</b> 상태를 그대로 만든다 — 중간 상태를 남기지 않는다.</summary>
        void SkipToEnd()
        {
            _introPlaying = false;

            if (_curtainGroup != null) _curtainGroup.alpha = 0f;

            if (_titleGroup != null) _titleGroup.alpha = 1f;
            if (_titleRect != null) _titleRect.anchoredPosition = _titleHome;
            if (_vignetteGroup != null) _vignetteGroup.alpha = 1f;

            for (int i = 0; i < _menuGroups.Count; i++)
            {
                if (_menuGroups[i] == null) continue;
                _menuGroups[i].alpha = 1f;
                _menuGroups[i].blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 덮고 있던 막을 걷는다(알파 1 → 0). <see cref="Fade"/> 의 반대 방향이다.
        /// ⚠ 같은 이유로 <see cref="Time.unscaledDeltaTime"/> 을 쓴다.
        /// </summary>
        static IEnumerator FadeOut(CanvasGroup group, float seconds)
        {
            if (group == null) yield break;

            if (seconds <= 0f)
            {
                group.alpha = 0f;
                yield break;
            }

            group.alpha = 1f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                group.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }
            group.alpha = 0f;
        }

        /// <summary>
        /// 투명도를 0 → 1 로 올리면서 <b>살짝 올라오게</b> 한다 (유저 지시 2026-08-18:
        /// <i>"타이틀 글씨가 조금 더 천천히 올라오게"</i>).
        ///
        /// ⚠ <b>제자리(씬에 잡아둔 위치)를 먼저 기억하고 그 아래에서 출발한다</b> — 반대로 하면
        /// (지금 위치에서 위로 올리면) 연출이 끝난 뒤 타이틀이 <b>씬에서 맞춰둔 자리보다 위에</b>
        /// 남는다. 연출은 자리를 옮기는 것이 아니라 <b>제자리로 도착하는 것</b>이다.
        ///
        /// 끝에서 감속한다(<c>1 - (1-t)²</c>) — 등속으로 멈추면 툭 서는 느낌이 난다.
        /// </summary>
        static IEnumerator RiseIn(CanvasGroup group, RectTransform rect, Vector2 home,
                                  float seconds, float rise)
        {
            if (group == null) yield break;

            if (seconds <= 0f)
            {
                group.alpha = 1f;
                if (rect != null) rect.anchoredPosition = home;
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                float e = Mathf.Clamp01(t);
                float eased = 1f - (1f - e) * (1f - e);

                group.alpha = eased;
                if (rect != null)
                    rect.anchoredPosition = new Vector2(home.x, home.y - rise * (1f - eased));

                yield return null;
            }

            group.alpha = 1f;
            if (rect != null) rect.anchoredPosition = home;
        }

        /// <summary>
        /// 투명도를 0 → 1 로 올린다.
        /// ⚠ <see cref="Time.unscaledDeltaTime"/> 을 쓴다 — 게임에서 일시정지한 채 로비로
        /// 나오면 <c>deltaTime</c> 이 0 이라 연출이 <b>영영 멈춘다</b>.
        /// </summary>
        static IEnumerator Fade(CanvasGroup group, float seconds)
        {
            if (group == null) yield break;

            if (seconds <= 0f)
            {
                group.alpha = 1f;
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                group.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            group.alpha = 1f;
        }

        // ------------------------------------------------------------------
        // 버튼
        // ------------------------------------------------------------------

        /// <summary>이어하기 — 저장을 읽어 들고 게임 씬으로 넘어간다.</summary>
        void HandleContinue()
        {
            SaveData data = SaveService.Load();
            if (data == null)
            {
                // 파일이 깨졌거나 형식이 다르다. 표시를 사실에 맞추고 넘어가지 않는다.
                RefreshSaveInfo();
                if (_savedAt != null) _savedAt.text = SaveService.LastMessage;
                return;
            }

            SaveService.PendingLoad = data;   // 게임 씬의 GameSnapshot 이 첫 프레임에 꺼내 쓴다
            LoadGame();
        }

        /// <summary>
        /// 새로하기 — <b>저장 파일을 지우고</b> 시작한다.
        ///
        /// 지우지 않으면 새 판에서 첫 자동 저장이 일어나기 전까지 옛 세이브가 남아,
        /// 그 사이에 게임을 껐다 켜면 <b>버린 판으로 되돌아간다</b>.
        /// </summary>
        void HandleNewGame()
        {
            // ★★ 2026-08-21 — <b>판 전역 기록도 비운다</b>. 예전에는 저장만 지웠다.
            //   «이미 등장한 인물»·«중립 사냥 수» 는 <c>static</c> 이라 씬을 넘겨도 남는다 —
            //   로비를 거쳐 새로하기를 눌러도 <b>인물이 소진된 채</b> 새 판이 시작됐다.
            //   그것이 «캐릭터가 죽으면 생성이 안 되는» 버그의 세 번째 경로였다
            //   (<see cref="Save.RunResetService"/> 의 맨 위 표).
            //   ⚠ 저장 삭제·씬 열기는 아래 LoadGame 이 하던 대로 둔다 — 로비는 «지금 씬» 이
            //     아니라 <b>게임 씬</b>을 열어야 하므로 BeginNewRun 을 그대로 쓸 수 없다.
            Save.RunResetService.ClearRunState();

            SaveService.Delete();
            SaveService.PendingLoad = null;

            // ★ 2026-08-24 — 새로하기만 오프닝을 거친다(openingSceneName 주석).
            //   오프닝이 끝나면 OpeningDirector 가 본편 씬을 연다.
            LoadGame(string.IsNullOrWhiteSpace(openingSceneName) ? gameSceneName : openingSceneName);
        }

        void LoadGame() => LoadGame(gameSceneName);

        void LoadGame(string sceneName)
        {
            // 게임에서 일시정지·배속을 걸어둔 채 나왔을 수 있다. 씬을 넘겨도 timeScale 은
            // 유지되므로(GameSpeedPanel 의 그 함정) 반드시 되돌려 놓는다.
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        void HandleSettings()
        {
            if (_settingsWindow == null) return;
            _settingsWindow.Toggle();
        }

        /// <summary>
        /// 게임 종료. 에디터에서는 <c>Application.Quit()</c> 이 아무 일도 하지 않으므로
        /// 플레이 모드를 끄는 것으로 대신한다.
        /// </summary>
        void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    
        /// <summary>
        /// ★ 이 창의 문구를 <b>스트링 표</b>에서 가져온다 (2026-08-26 · 178-5절).
        /// 인스펙터 값은 <b>폴백</b>이다 — 표에 키가 없으면 화면은 지금과 같다.
        /// </summary>
        void LocalizeLabels()
        {
            savedAtFormat = HudTheme.T("ui_lobby_saved_at", savedAtFormat);
            noSaveText = HudTheme.T("ui_lobby_no_save", noSaveText);
        }
}
}
