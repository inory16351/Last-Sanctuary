using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>클릭한 유닛의 일러스트</b>를 로그 창 아래에 띄우는 패널 (2026-08-15, 유저 지시).
    ///
    /// <i>"캐릭터 뿐만 아니라 몬스터 들도 클릭 가능하게 만들고 클릭하면 콘솔 로그 아래에
    /// 클릭한 객체의 일러스트가 연동되는 ui 만들기"</i> ·
    /// <i>"해당 ui 는 다시 땅을 클릭하면 비활성화 되는 로직(게임 시작 시 비활성화)"</i>
    ///
    /// ★ <b>선택을 따라가기만 한다</b>
    /// ----------------------------
    /// 이 패널은 <b>아무것도 고르지 않는다.</b> <see cref="UnitSelector.OnUnitSelectionChanged"/>
    /// 를 구독해 값이 오면 켜고, null 이 오면 끈다 — 그게 곧 "땅을 클릭하면 비활성화" 다
    /// (빈 땅 클릭 → <c>UnitSelector.Clear()</c> → null). <b>땅 클릭을 따로 감지하지 않는다</b>:
    /// 클릭 판정이 두 곳에 생기면 드래그 임계값·UI 위 클릭 제외 같은 규칙을 두 벌 유지해야 한다.
    /// <c>TacticalOrderPanel</c> 이 선택을 따라가는 구조와 같다.
    ///
    /// ★ <b>게임 시작 시 비활성</b> — <see cref="Awake"/> 에서 스스로 꺼진다. 씬에 켜진 채로
    /// 저장돼 있어도 첫 프레임에 닫힌다(<c>SkillDetailPanel</c> 과 같은 방식).
    ///
    /// ⚠ <b>HudExclusive 에 넣지 않았다.</b> 그 배타 규칙은 "창을 열면 다른 창이 닫힌다" 인데,
    /// 이 패널은 <b>창이 아니라 표시</b>다 — 전술 지침을 보면서 몬스터를 클릭해 정보를 보는 것이
    /// 자연스럽고, 그때 지침 창이 닫히면 오히려 방해가 된다.
    ///
    /// ⚠ 일러스트가 없는 종(웨이브 몬스터·넥서스·포탑)은 <b>그림 없이 이름·칭호만</b> 보여준다.
    ///   표에 <c>illust</c> 칸이 생기면 그대로 뜬다 — 이 코드는 안 바뀐다.
    /// </summary>
    public class UnitPortraitPanel : MonoBehaviour
    {
        [Header("문구")]
        [Tooltip("일러스트가 없는 유닛의 그림 자리에 대신 띄울 한 줄")]
        [SerializeField] string noArtText = "일러스트 없음";

        [Tooltip("{0} = 소속 이름. 이름 아래 칭호가 없을 때 대신 보여준다")]
        [SerializeField] string factionFormat = "{0}";

        [Header("색")]
        [SerializeField] Color titleColor = new Color(0.84f, 0.64f, 1f, 1f);

        [Header("일러스트 채우기 (2026-08-17)")]
        [Tooltip("★ <b>캐릭터</b> 그림이 세로로 잘릴 때 남길 위치 (0=아래 · 0.5=가운데 · 1=위).\n" +
                 "인물화는 위쪽을 남겨야 <b>얼굴</b>이 들어온다 — 가운데를 남기면 가슴이 남는다")]
        [Range(0f, 1f)] [SerializeField] float characterVerticalAnchor = 0.86f;

        [Tooltip("<b>몬스터</b> 그림이 세로로 잘릴 때 남길 위치. " +
                 "전신 실루엣 자체가 정보라 가운데가 맞다")]
        [Range(0f, 1f)] [SerializeField] float monsterVerticalAnchor = 0.5f;

        Image _art;
        TMP_Text _nameText;
        TMP_Text _titleText;
        TMP_Text _noArtLabel;

        UnitSelector _selector;
        DamageableUnit _shown;
        bool _bound;

        /// <summary>
        /// 강화 서비스를 구독해 뒀는지. 서비스는 <c>GameSystems</c> 에 상주하지만
        /// <b>이 패널보다 늦게 살아날 수 있어</b> 한 번에 못 걸릴 때가 있다 —
        /// 그래서 <see cref="Subscribe"/> 를 여러 곳에서 다시 부르고 이 칸으로 중복을 막는다.
        /// </summary>
        bool _upgradeHooked;

        /// <summary>
        /// 비활성으로 시작하므로 <c>Awake</c> 가 안 돌 수 있다 — <c>SkillDetailPanel</c> 이 겪은
        /// 함정 그대로다(49-6절·36-4절). 비활성까지 포함해 찾고 배선을 보장한다.
        /// </summary>
        public static UnitPortraitPanel Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<UnitPortraitPanel>(FindObjectsInactive.Include);
                if (_instance != null) _instance.EnsureBound();
                return _instance;
            }
            private set => _instance = value;
        }

        static UnitPortraitPanel _instance;

        void Awake()
        {
            Instance = this;
            EnsureBound();

            // ⚠⚠ <b>여기서 gameObject.SetActive(false) 를 부르면 안 된다.</b>
            //
            //   이 창은 씬에 <b>비활성으로 저장</b>돼 있다. 비활성 오브젝트의 Awake 는
            //   씬 로드 때 <b>안 돌고</b>, 나중에 <c>SetActive(true)</c> 로 켜지는 순간
            //   <b>그 호출 안에서 동기적으로</b> 돈다. 그래서 Awake 에서 자기를 끄면
            //   <b>열리는 바로 그 순간 스스로 닫혀</b> 창이 영영 안 뜬다
            //   (유저 리포트 2026-08-15: "지금 일러스트 연동 ui 작동안함").
            //
            //   "게임 시작 시 비활성"은 <b>씬에 그렇게 저장해서</b> 지킨다. 혹시 켜진 채로
            //   저장됐더라도 항상 살아 있는 <see cref="UnitSelector"/> 가
            //   <see cref="Bind"/> 에서 한 번 닫아준다.
            //
            //   ⚠ 같은 코드가 <c>SkillDetailPanel</c>·<c>SubjugationPanel</c> 에도 있었다 —
            //     셋 다 같은 이유로 고쳤다.
        }

        void OnEnable() => Subscribe();

        /// <summary>
        /// ⚠⚠ <b>여기서 구독을 끊으면 안 된다</b> (2026-08-16, 유저 리포트:
        /// <i>"땅을 클릭하면 영원히 다시 활성화가 안되는 게 아니라 ... 또 다른 캐릭터나
        /// 몬스터 클릭하면 일러스트 연동되서 나오게"</i>).
        ///
        /// 예전에는 여기서 <c>OnUnitSelectionChanged -= …</c> 를 했다. 그런데 이 패널은
        /// <b>닫힐 때 비활성이 된다</b>(빈 땅 클릭 → <see cref="Close"/>). 그 순간 구독이
        /// 끊기므로 <b>그다음 유닛 클릭이 이 패널에 영영 도달하지 못한다</b> — 한 번 닫으면
        /// 다시는 안 열렸다.
        ///
        /// 구독은 <b>델리게이트</b>라 컴포넌트가 꺼져 있어도 살아 있다. 그래서 이 패널은
        /// <b>꺼진 채로 선택 이벤트를 기다리다가</b> 값이 오면 스스로 켜진다 — 그것이
        /// 이 창의 동작 방식이다. 정리는 <see cref="OnDestroy"/> 에서만 한다.
        /// </summary>
        void OnDisable()
        {
            // 일부러 비워 둔다 — 위 주석 참조.
        }

        void OnDestroy()
        {
            if (_selector != null) _selector.OnUnitSelectionChanged -= HandleUnitSelected;

            Units.CharacterUpgradeService service = Units.CharacterUpgradeService.Instance;
            if (service != null) service.OnUpgraded -= HandleUpgraded;
            _upgradeHooked = false;

            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// ⚠ 이 패널은 <b>꺼진 채로 시작</b>하므로 <c>OnEnable</c> 만으로는 구독이 안 걸린다
        /// (꺼져 있으면 아예 안 불린다). 그래서 <b>선택 이벤트를 놓치지 않도록</b>
        /// 씬에 상주하는 중계자가 필요한데, 여기서는 <see cref="UnitSelector"/> 쪽에서
        /// 이 패널을 <b>깨워주는</b> 대신 — 아래 <see cref="Bind"/> 를 <c>UnitSelector</c> 가
        /// 시작할 때 한 번 부르게 한다.
        /// </summary>
        void Subscribe()
        {
            if (_selector != null || (_selector = UnitSelector.Instance) != null)
            {
                _selector.OnUnitSelectionChanged -= HandleUnitSelected;
                _selector.OnUnitSelectionChanged += HandleUnitSelected;
            }

            HookUpgrades();
        }

        /// <summary>
        /// ★ <b>강화하면 레벨이 즉시 이 카드에 반영되게</b> 한다 (유저 지시 2026-08-18:
        /// <i>"캐릭터 강화 시에 레벨 단계가 즉시 일러스트 ui 에 연동되게"</i>).
        ///
        /// <b>왜 필요했나</b> — 이름 줄은 예전부터 <c>Lv.N</c> 을 같이 적고 있었다
        /// (<see cref="NameLineOf"/>, 86-8절). 그런데 그 글자를 쓰는 곳이
        /// <see cref="Show"/> <b>하나뿐</b>이고 <see cref="Show"/> 는 <b>선택이 바뀔 때만</b>
        /// 불린다. 그래서 카드를 띄워 둔 채로 강화하면 <b>레벨이 옛 값 그대로 남았다</b> —
        /// 다른 캐릭터를 클릭했다 돌아와야 갱신됐다.
        ///
        /// ⚠ <b>매 프레임 다시 그리지 않는다.</b> 이 카드는 값이 거의 안 바뀌는 정적 카드라
        /// (로스터·성장 창처럼 주기 갱신이 없다) 폴링을 넣으면 그것만으로 상시 비용이 생긴다.
        /// <b>바뀌는 순간에만</b> 알림을 받는 편이 이 창의 성격에 맞는다.
        ///
        /// ⚠ 구독은 <b>델리게이트</b>라 이 패널이 꺼져 있어도 살아 있다 —
        /// <see cref="OnDisable"/> 주석의 그 성질을 여기서도 그대로 쓴다.
        /// 그래서 카드를 닫아 둔 동안 강화가 일어나도, 다시 열면 이미 맞는 값이 그려진다.
        /// </summary>
        void HookUpgrades()
        {
            if (_upgradeHooked) return;

            Units.CharacterUpgradeService service = Units.CharacterUpgradeService.Instance;
            if (service == null) return;          // 아직 안 살아났다 — 다음 기회에 다시 시도한다

            service.OnUpgraded -= HandleUpgraded;
            service.OnUpgraded += HandleUpgraded;
            _upgradeHooked = true;
        }

        /// <summary>
        /// 강화가 성사됐다. <b>지금 이 카드에 떠 있는 그 캐릭터</b>일 때만 다시 그린다 —
        /// 다른 캐릭터를 강화했다고 이 카드가 흔들릴 이유가 없다.
        ///
        /// ⚠ 정신 이상 「고조」의 무료 강화(<c>GrowFree</c>)도 같은 이벤트를 쏜다 —
        /// 그쪽도 레벨이 오르므로 <b>같이 반영되는 것이 맞다</b>.
        /// </summary>
        void HandleUpgraded(Units.CharacterUnit unit, int cost)
        {
            if (unit == null || !ReferenceEquals(unit, _shown)) return;
            Show(unit);
        }

        /// <summary>
        /// <see cref="UnitSelector"/> 가 <c>Start</c> 에서 한 번 부른다 — <b>꺼져 있는 패널이
        /// 스스로 구독할 수 없기 때문</b>이다. 여기서 건 구독은 패널이 꺼져 있어도 살아 있다
        /// (구독 주체는 컴포넌트가 아니라 델리게이트다).
        /// </summary>
        public void Bind(UnitSelector selector)
        {
            EnsureBound();

            // "게임 시작 시 비활성" — 씬에 켜진 채 저장됐더라도 여기서 한 번 닫는다.
            // (Awake 에서 닫으면 안 되는 이유는 그쪽 주석 참조.)
            if (gameObject.activeSelf) Close();

            _selector = selector;
            if (_selector != null)
            {
                _selector.OnUnitSelectionChanged -= HandleUnitSelected;
                _selector.OnUnitSelectionChanged += HandleUnitSelected;
            }

            // 강화 서비스도 같이 건다 — 이 시점(UnitSelector.Start)이면 GameSystems 는 이미 깨어 있다.
            HookUpgrades();
        }

        // ------------------------------------------------------------------

        void HandleUnitSelected(DamageableUnit unit)
        {
            if (unit == null) { Close(); return; }
            Show(unit);
        }

        /// <summary>지금 보여주고 있는 유닛. 없으면 null.</summary>
        public DamageableUnit Shown => _shown;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>패널을 닫는다 — 빈 땅 클릭이 이 경로로 들어온다.</summary>
        public void Close()
        {
            _shown = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>유닛 하나를 보여준다.</summary>
        public void Show(DamageableUnit unit)
        {
            if (unit == null) { Close(); return; }

            EnsureBound();
            HookUpgrades();      // 서비스가 늦게 살아난 경우를 대비 — 이미 걸려 있으면 즉시 반환한다
            _shown = unit;
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            Sprite art = unit.Portrait;
            if (_art != null)
            {
                _art.sprite = art;
                // 그림이 없을 때 흰 사각형이 남지 않도록 알파로 지운다
                // (Image 를 끄면 레이아웃이 흔들린다).
                _art.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);

                // ★ 액자를 꽉 채운다 (2026-08-17) — preserveAspect 는 '맞춰 넣기' 라
                //   세로형 인물화가 가로 액자에서 폭의 46% 만 쓰고 나머지가 빈 공간이었다.
                //   자세한 계산은 PortraitFit 클래스 주석 참조.
                //
                //   ⚠ 사람과 몬스터는 남길 곳이 다르다 — 인물은 위(얼굴), 전신 몬스터는
                //     가운데(실루엣 전체). 세로로 잘릴 때만 차이가 난다.
                PortraitFit.Cover(_art,
                    unit is CharacterUnit ? characterVerticalAnchor : monsterVerticalAnchor);
            }
            if (_noArtLabel != null)
            {
                _noArtLabel.text = noArtText;
                _noArtLabel.gameObject.SetActive(art == null);
            }

            if (_nameText != null) _nameText.text = NameLineOf(unit);

            if (_titleText != null)
            {
                string title = unit.Title;
                // 칭호가 없으면 그 자리에 소속을 적는다 — 줄이 통째로 비면 카드가 어색하다.
                _titleText.text = string.IsNullOrWhiteSpace(title)
                    ? string.Format(factionFormat, FactionLabel(unit))
                    : title;
                _titleText.color = string.IsNullOrWhiteSpace(title) ? HudTheme.TextDim : titleColor;
            }
        }

        /// <summary>
        /// 이름 줄. 캐릭터면 <b>레벨</b>을 같이 적는다 — 로스터와 같은 규칙이다
        /// (유저 지시 2026-08-15: 강화 횟수를 Lv 로).
        /// </summary>
        static string NameLineOf(DamageableUnit unit)
        {
            if (unit is CharacterUnit c)
            {
                string lv = ColorUtility.ToHtmlStringRGB(HudTheme.TextAccent);
                return $"{c.DisplayName} <size=78%><color=#{lv}>Lv.{c.UpgradeCount}</color></size>";
            }
            return unit.DisplayName;
        }

        static string FactionLabel(DamageableUnit unit)
        {
            if (unit.Faction == Faction.Neutral) return "중립 몬스터";
            if (unit.Faction == Faction.Angel)
                return unit.Kind == UnitKind.Nexus ? "중앙 건물" : "아군";
            return "웨이브 몬스터";
        }

        // ------------------------------------------------------------------

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            // ★ 2026-08-17 구조 변경 — 액자(Art, RectMask2D) 안에 그림(Art/Sprite)이 들어간다.
            //   전술 지침·성장 창이 이미 쓰던 "Portrait/Sprite" 와 <b>같은 모양</b>으로 맞춘 것이다.
            //   꽉 채운 그림은 액자 밖으로 넘치므로 <b>가릴 층이 반드시 하나 더</b> 필요하다.
            //   예전 구조(그림이 곧 Art)로 되돌아간 씬에서도 죽지 않게 폴백을 둔다.
            _art = transform.Find("Art/Sprite") != null
                ? Find<Image>("Art/Sprite")
                : Find<Image>("Art");

            _nameText = Find<TMP_Text>("Name");
            _titleText = Find<TMP_Text>("Title");
            _noArtLabel = Find<TMP_Text>("NoArt");
        }

        T Find<T>(string path) where T : Component
        {
            Transform t = transform.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[Portrait] 하이라키에서 '{path}' 를 찾지 못했습니다.", this);
                return null;
            }
            return t.GetComponent<T>();
        }
    }
}
