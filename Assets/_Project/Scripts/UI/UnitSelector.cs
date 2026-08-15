using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 마우스 클릭으로 유닛을 선택한다. 선택된 캐릭터는 강화 버튼 등 UI 가 참조한다.
    ///
    /// <b>물리 레이캐스트를 쓰지 않는다</b> — 이 프로젝트의 유닛에는 Collider2D 가 없고
    /// 이동 충돌조차 타일 기준으로 판정한다(13절). 그 방식을 유지해서, 마우스 월드 좌표가
    /// 어느 유닛의 스프라이트 경계 안에 있는지를 직접 검사한다. 덕분에 템플릿에
    /// 콜라이더를 새로 붙이지 않아도 되고, 콜라이더가 전투/이동 판정에 끼어들 여지도 없다.
    ///
    /// 카메라 드래그(<c>CameraRigController</c>)와 좌클릭을 공유하므로, 누른 지점에서
    /// 임계값 이상 움직였으면 드래그로 보고 선택하지 않는다.
    ///
    /// ★ <b>2026-08-15 — 캐릭터 전용에서 「유닛 전반」으로 넓혔다</b>
    /// (유저 지시: <i>"캐릭터 뿐만 아니라 몬스터 들도 클릭 가능하게 만들고"</i>).
    /// 선택 대상이 <b>두 갈래</b>가 된 것이 이 클래스의 핵심 변화다:
    /// <code>
    ///   SelectedUnit  (DamageableUnit)  클릭한 것 <b>전부</b> — 몬스터·중립·넥서스·포탑 포함
    ///   Selected      (CharacterUnit)   그중 <b>캐릭터일 때만</b> 채워진다
    /// </code>
    /// <b>왜 갈랐나</b> — 기존 UI(강화창·전술 지침창·로스터)는 전부 "선택 = 조작할 캐릭터"
    /// 라는 뜻으로 <see cref="Selected"/> 를 쓴다. 몬스터를 클릭했을 때 그 값이 몬스터로
    /// 채워지면 <b>강화창이 몬스터를 강화하려 든다.</b> 그래서 <b>기존 이름의 뜻은 한 글자도
    /// 안 바꾸고</b>, 보여주기 전용(초상화)을 위한 넓은 값을 옆에 새로 뒀다.
    /// 몬스터를 클릭하면 <see cref="Selected"/> 는 <b>null 로 비워진다</b> — 캐릭터를 조작하다
    /// 몬스터를 눌렀는데 조작 대상이 그대로 남아 있으면 그게 더 위험하다.
    /// </summary>
    public class UnitSelector : MonoBehaviour
    {
        [Header("클릭 판정")]
        [Tooltip("누른 지점에서 이 픽셀 이상 움직이면 카메라 드래그로 보고 선택하지 않는다. " +
                 "CameraRigController 의 드래그 임계값과 같은 값으로 두는 게 좋다")]
        [Min(0f)] [SerializeField] float clickThresholdPixels = 4f;

        [Tooltip("스프라이트 경계 안에 아무도 없을 때, 이 반경(타일) 안의 가장 가까운 " +
                 "유닛을 잡아준다. 도트가 작아 정확히 찍기 어려운 것을 보정한다")]
        [Min(0f)] [SerializeField] float pickAssistRadiusTiles = 0.75f;

        [Tooltip("빈 땅을 클릭하면 선택을 해제한다")]
        [SerializeField] bool clearOnEmptyClick = true;

        [Tooltip("★ 캐릭터가 아닌 유닛(몬스터·중립·넥서스·포탑)도 클릭으로 고를 수 있게 한다.\n" +
                 "끄면 2026-08-15 이전처럼 캐릭터만 잡힌다.\n" +
                 "⚠ 켜져 있어도 <b>조작 대상</b>(Selected)은 여전히 캐릭터뿐이다 — " +
                 "몬스터는 초상화 같은 <b>보여주기 전용</b>으로만 쓰인다")]
        [SerializeField] bool pickNonCharacters = true;

        [Tooltip("<b>안개에 가려진</b> 유닛은 클릭으로 잡히지 않게 한다. " +
                 "보이지도 않는 몬스터의 정보가 뜨면 안개가 무의미해진다")]
        [SerializeField] bool respectFogOfWar = true;

        [Header("선택 표시")]
        [Tooltip("선택된 캐릭터의 스프라이트에 곱해지는 색. UI 가 아직 없어 이게 유일한 표시다")]
        [SerializeField] Color selectedTint = new Color(0.55f, 1f, 0.7f, 1f);

        [Tooltip("끄면 색을 바꾸지 않는다(외곽선 등 다른 연출을 붙일 때)")]
        [SerializeField] bool tintSelected = true;

        [Header("디버그")]
        [SerializeField] bool logSelection = true;

        CharacterUnit _selected;
        DamageableUnit _selectedUnit;
        SpriteRenderer _selectedRenderer;
        Color _originalColor;

        Camera _camera;
        Vector2 _pressPosition;
        bool _pressStartedOverUI;
        bool _pressActive;

        /// <summary>
        /// 지금 선택된 <b>캐릭터</b>. 없으면 null.
        /// ⚠ 몬스터를 클릭하면 이 값은 <b>null 이 된다</b> — 조작 대상은 캐릭터뿐이다.
        /// </summary>
        public CharacterUnit Selected => _selected;

        /// <summary>
        /// 지금 선택된 <b>유닛 전반</b> — 몬스터·중립·넥서스·포탑도 들어온다. 없으면 null.
        /// 초상화(<see cref="UnitPortraitPanel"/>) 처럼 <b>보여주기만</b> 하는 UI 가 쓴다.
        /// </summary>
        public DamageableUnit SelectedUnit => _selectedUnit;

        /// <summary>캐릭터 선택이 바뀔 때마다 발생 (새 선택, 없으면 null).</summary>
        public event System.Action<CharacterUnit> OnSelectionChanged;

        /// <summary>
        /// <b>유닛</b> 선택이 바뀔 때마다 발생 (새 선택, 없으면 null).
        /// 캐릭터를 골랐을 때도 발생한다 — 초상화는 캐릭터·몬스터를 가리지 않는다.
        /// </summary>
        public event System.Action<DamageableUnit> OnUnitSelectionChanged;

        public static UnitSelector Instance { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _camera = Camera.main;

            // ★ 초상화 패널은 <b>꺼진 채로 시작</b>하므로 스스로 구독할 수 없다
            //   (비활성 오브젝트는 OnEnable 이 안 돈다 — 49-6절의 그 함정).
            //   여기서 한 번 깨워 구독을 걸어준다. 구독은 델리게이트라 패널이 꺼져 있어도 산다.
            UnitPortraitPanel.Instance?.Bind(this);
        }

        void Update()
        {
            // 선택된 유닛이 죽거나 파괴되면 선택을 놓는다.
            // ⚠ `== null` 은 Unity 의 오버로드라 <b>파괴된 오브젝트</b>도 true 다 — 그래서
            //   몬스터가 죽어 Destroy 된 경우도 여기서 같이 걸린다.
            if (_selectedUnit != null && !_selectedUnit.IsAlive) Clear();
            if (_selectedUnit == null && _selectedRenderer != null) Clear();

            HandleClick();
        }

        // ------------------------------------------------------------------

        void HandleClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _pressActive = true;
                _pressPosition = mouse.position.ReadValue();
                _pressStartedOverUI = IsPointerOverUI();
                return;
            }

            if (!mouse.leftButton.wasReleasedThisFrame) return;

            bool wasPress = _pressActive;
            _pressActive = false;
            if (!wasPress || _pressStartedOverUI) return;

            // 카메라를 끌었던 것이면 선택이 아니다.
            Vector2 release = mouse.position.ReadValue();
            if ((release - _pressPosition).magnitude >= clickThresholdPixels) return;

            DamageableUnit hit = PickAt(release);
            if (hit != null) Select(hit);
            else if (clearOnEmptyClick) Clear();
        }

        /// <summary>
        /// 화면 좌표 아래에 있는 유닛을 찾는다.
        ///
        /// ★ <b>겹쳤을 때는 캐릭터가 이긴다.</b> 캐릭터는 조작 대상이고 몬스터는 정보 표시일
        /// 뿐이라, 난전에서 몬스터가 잡혀 조작을 놓치는 편이 훨씬 나쁘다. 그래서 스프라이트
        /// 경계 안에 든 후보 중 <b>캐릭터를 먼저</b> 돌려준다.
        /// </summary>
        DamageableUnit PickAt(Vector2 screenPosition)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return null;

            Vector3 world = _camera.ScreenToWorldPoint(screenPosition);
            world.z = 0f;

            DamageableUnit boxedOther = null;      // 경계 안에 든 캐릭터 아닌 유닛
            DamageableUnit nearest = null;         // 경계 밖이지만 보정 반경 안
            float nearestSqr = float.MaxValue;
            float assistSqr = pickAssistRadiusTiles * pickAssistRadiusTiles;

            // 유닛 수가 많지 않아(수십 마리) 전수 검사로 충분하다 — 클릭할 때만 도는 경로다.
            DamageableUnit[] all = FindObjectsByType<DamageableUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;

                bool isCharacter = u is CharacterUnit;
                if (!isCharacter && !pickNonCharacters) continue;
                if (!isCharacter && !IsVisible(u)) continue;

                // 스프라이트 경계 안이면 후보다. 캐릭터면 그 자리에서 확정한다.
                var sr = u.GetComponent<SpriteRenderer>();
                if (sr != null && sr.enabled)
                {
                    Bounds b = sr.bounds;
                    if (world.x >= b.min.x && world.x <= b.max.x &&
                        world.y >= b.min.y && world.y <= b.max.y)
                    {
                        if (isCharacter) return u;
                        if (boxedOther == null) boxedOther = u;
                        continue;
                    }
                }

                float sqr = ((Vector2)(u.transform.position - world)).sqrMagnitude;
                if (sqr <= assistSqr && sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = u;
                }
            }

            // 경계 안에 든 것이 우선, 없으면 보정 반경 안의 가장 가까운 것.
            return boxedOther != null ? boxedOther : nearest;
        }

        /// <summary>
        /// 안개에 가려져 있지 않은가. <see cref="respectFogOfWar"/> 가 꺼져 있으면 항상 true.
        ///
        /// 아군(캐릭터·넥서스·포탑)은 이 검사를 아예 거치지 않는다 — 위치를 원래 알고 있다
        /// (미니맵이 아군만 안개와 무관하게 그리는 것과 같은 규칙, UI-1 절).
        /// </summary>
        bool IsVisible(DamageableUnit unit)
        {
            if (!respectFogOfWar) return true;
            if (unit.Faction == Faction.Angel) return true;

            // 싱글턴이 없는 서비스라 다른 곳(UnitCombat·MinimapPanel)과 같은 방식으로 찾아 캐시한다.
            if (_fog == null) _fog = FindAnyObjectByType<Fog.FogOfWarService>();
            return _fog == null || _fog.IsVisibleWorld(unit.transform.position);
        }

        Fog.FogOfWarService _fog;

        static bool IsPointerOverUI()
        {
            EventSystem es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 유닛을 선택한다. 같은 대상을 다시 넣으면 아무 일도 하지 않는다.
        ///
        /// 캐릭터면 <see cref="Selected"/> 도 같이 채워지고, 아니면 그 값은 <b>비워진다</b>
        /// (클래스 주석의 "두 갈래" 참조).
        /// </summary>
        public void Select(DamageableUnit unit)
        {
            if (unit == null) { Clear(); return; }
            if (ReferenceEquals(unit, _selectedUnit)) return;

            RestoreTint();

            _selectedUnit = unit;
            CharacterUnit asCharacter = unit as CharacterUnit;

            _selectedRenderer = unit.GetComponent<SpriteRenderer>();
            if (tintSelected && _selectedRenderer != null)
            {
                _originalColor = _selectedRenderer.color;
                _selectedRenderer.color = selectedTint;
            }

            if (logSelection) Debug.Log($"[Select] {unit.DisplayName} 선택", unit);

            // 캐릭터 선택이 실제로 바뀐 경우에만 그쪽 이벤트를 쏜다 — 몬스터를 연달아
            // 클릭할 때마다 강화창·전술창이 "선택 없음"으로 다시 그려지면 낭비다.
            bool characterChanged = !ReferenceEquals(asCharacter, _selected);
            _selected = asCharacter;

            OnUnitSelectionChanged?.Invoke(_selectedUnit);
            if (characterChanged) OnSelectionChanged?.Invoke(_selected);
        }

        /// <summary>선택을 해제한다.</summary>
        public void Clear()
        {
            if (_selectedUnit == null && _selected == null && _selectedRenderer == null) return;

            RestoreTint();

            bool hadCharacter = _selected != null;
            _selected = null;
            _selectedUnit = null;
            _selectedRenderer = null;

            OnUnitSelectionChanged?.Invoke(null);
            if (hadCharacter) OnSelectionChanged?.Invoke(null);
        }

        void RestoreTint()
        {
            if (tintSelected && _selectedRenderer != null) _selectedRenderer.color = _originalColor;
            _selectedRenderer = null;
        }
    }
}
