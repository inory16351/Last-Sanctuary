using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using LastSanctuary.Map;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 집결지. <b>부대마다 하나씩</b> 가진다 — 만들고 지우는 조작은 전부
    /// "부대 설정" 창(<see cref="SquadPanel"/>)의 부대 카드 안에 있다
    /// (유저 확정 2026-08-12: 집결지 생성·해제·부대 지정 세 버튼을 하나로 합쳤다).
    ///
    /// 이동 자체는 새로 짜지 않는다 — <see cref="CharacterBehavior"/> 가 매 프레임
    /// 여기 물어보고, 집결지가 있으면 정찰·순찰 대신 그 지점을 목적지로 삼는다.
    /// 이 프로젝트가 이미 쓰는 "귀환 지점을 옮겨 이동 명령을 대신한다" 방식 그대로다(진행상황 12절).
    ///
    /// <b>화면 표시</b>(2026-08-12 개편 — 예전엔 노란 원이 계속 깔려 있었다):
    /// <list type="bullet">
    /// <item>확정된 집결지는 <b>깃발</b>(<see cref="RallyFlag"/>, 월드 스프라이트)로만 표시한다.
    ///       깃대가 박히는 칸이 집결지 정중앙이다.</item>
    /// <item>깃발 위에 <b>담당 부대 이름</b>이 뜬다(이름은 부대 설정 창에서 유저가 고칠 수 있다).</item>
    /// <item>범위(<see cref="RallyAreaSize"/> 지름의 원)는 <b>평소에 그리지 않는다</b> —
    ///       깃발을 클릭한 동안만 <b>테두리로</b> 보여준다. 지정 모드의 미리보기도 같은 테두리다.</item>
    /// </list>
    /// 이 클래스가 <see cref="RallyAreaSize"/> 의 정본이다 — <c>CharacterBehavior</c> 는
    /// 실제 순찰 반경을 정할 때 이 값을 그대로 읽어간다(화면에 보이는 범위와 실제 순찰 범위가
    /// 항상 같아야 하므로, 값을 두 곳에 따로 두지 않는다).
    /// </summary>
    public class RallyPointService : MonoBehaviour
    {
        [Header("클릭 판정")]
        [Tooltip("누른 지점에서 이 픽셀 이상 움직이면 카메라 드래그로 보고 지정하지 않는다. " +
                 "UnitSelector·CameraRigController 와 같은 값으로 둘 것")]
        [Min(0f)] [SerializeField] float clickThresholdPixels = 4f;

        [Tooltip("벽·구조물 칸을 찍으면 근처의 갈 수 있는 칸으로 밀어준다. 그 탐색 반경(타일)")]
        [Min(0)] [SerializeField] int snapSearchRadius = 6;

        [Header("범위 (임시값 — 기획 확정 전)")]
        [Tooltip("집결지 원형 구역의 지름(타일). CharacterBehavior 의 실제 순찰 범위도 " +
                 "이 값을 그대로 쓴다 — 화면에 보이는 범위와 실제 동작이 항상 일치하게")]
        [Min(2f)] [SerializeField] float rallyAreaSize = 10f;

        [Header("깃발 (모체 하나를 복제해서 쓴다 — 비활성으로 둘 것)")]
        [Tooltip("맵에 꽂을 깃발의 원본. 씬의 RallyFlags 아래 비활성 오브젝트")]
        [SerializeField] RallyFlag flagTemplate;

        [Tooltip("깃발 위에 띄울 부대 이름표의 원본. UI_Root/RallyOverlay 아래 비활성 오브젝트")]
        [SerializeField] RectTransform labelTemplate;

        [Tooltip("이름표를 깃발 꼭대기에서 얼마나 더 띄울지(화면 픽셀)")]
        [SerializeField] float labelScreenOffset = 6f;

        [Header("범위 테두리 (모체 하나를 복제해서 쓴다 — 비활성으로 둘 것)")]
        [Tooltip("집결지 구역을 나타내는 원의 원본. UI_Root/RallyOverlay 아래 비활성 오브젝트")]
        [SerializeField] RectTransform rangeTemplate;

        [Tooltip("범위 원에 씌울 '테두리만 있는 원' 그림의 Resources 경로 (확장자 없이). " +
                 "MCP 로는 씬의 Image 에 Sprite 를 못 넣어서 코드로 꽂는다(진행상황 27-9절)")]
        [SerializeField] string rangeOutlineResource = "UI/RallyRangeOutline";

        [Tooltip("마커·범위를 그릴 캔버스. 비워두면 rangeTemplate 의 부모를 쓴다")]
        [SerializeField] RectTransform markerParent;

        [Header("깃발 끌어 옮기기")]
        [Tooltip("깃발을 이만큼 꾹 누르고 있으면 잔상이 분리되어 따라온다(초)")]
        [Min(0.1f)] [SerializeField] float dragHoldSeconds = 1f;

        [Header("색")]
        [Tooltip("깃발을 눌러 펼쳐둔 범위 테두리")]
        [SerializeField] Color rangeColor = new Color(1f, 0.86f, 0.42f, 0.95f);

        [Tooltip("아직 찍지 않은 미리보기 테두리")]
        [SerializeField] Color previewColor = new Color(0.55f, 0.92f, 0.85f, 0.75f);

        [Tooltip("범위를 펼쳐둔 깃발에 입히는 색")]
        [SerializeField] Color flagHighlight = new Color(1f, 1f, 1f, 1f);

        [Tooltip("끌고 있는 동안 원래 자리에 남는 잔상의 색")]
        [SerializeField] Color dragSourceColor = new Color(1f, 1f, 1f, 0.3f);

        [Tooltip("마우스를 따라다니는 분신 깃발의 색")]
        [SerializeField] Color dragGhostColor = new Color(1f, 0.96f, 0.8f, 0.85f);

        [Tooltip("★ 범위를 펼쳐둔 깃발을 flagHighlight 쪽으로 얼마나 끌어올릴지.\n" +
                 "0 이면 부대 색 그대로(강조 없음) · 1 이면 flagHighlight 그대로(부대 색이 사라진다)")]
        [Range(0f, 1f)] [SerializeField] float expandedLift = 0.45f;

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        /// <summary>
        /// 맵에 찍힌 집결지 하나. <b>부대마다 하나</b>다 — 같은 부대로 다시 찍으면
        /// 새로 생기지 않고 그 자리만 옮긴다.
        /// </summary>
        public class RallyPoint
        {
            public int Id;
            public Vector3 World;

            /// <summary>이 집결지를 쓰는 부대. <b>0 이면 부대 미지정 = 전체 공용</b>이다.</summary>
            public int SquadId;
        }

        readonly List<RallyPoint> _points = new List<RallyPoint>();
        int _nextPointId = 1;

        /// <summary>집결지 목록이 바뀌었다(생성·이동·해제). UI 가 표시를 다시 그린다.</summary>
        public event System.Action OnPointsChanged;

        public IReadOnlyList<RallyPoint> Points => _points;

        readonly List<RallyFlag> _flags = new List<RallyFlag>();
        readonly List<RectTransform> _labels = new List<RectTransform>();
        readonly List<RectTransform> _ranges = new List<RectTransform>();

        /// <summary>끌고 있는 동안 마우스를 따라다니는 분신 깃발. 모체를 한 번만 복제해 재사용한다.</summary>
        RallyFlag _ghostFlag;

        // 꾹 누르기 판정 — 눌린 깃발과 누르기 시작한 시각
        int _pressFlagPointId;
        float _pressStartTime;

        /// <summary>지금 끌고 있는 집결지 id. 0 이면 안 끌고 있다.</summary>
        int _dragPointId;
        Vector3 _dragWorld;

        /// <summary>집은 순간의 (깃발 밑동 − 커서) 차이. 이걸 유지해야 깃발이 손에서 안 튄다.</summary>
        Vector3 _dragGrabOffset;

        Sprite _outlineSprite;
        bool _outlineLoaded;

        Vector3 _previewWorld;
        bool _hasPreview;

        Camera _camera;
        MapGenerator _map;
        Transform _flagParent;
        Vector2 _pressPosition;
        bool _pressActive;
        bool _pressStartedOverUI;

        /// <summary>집결지 구역의 지름(타일). 범위 표시와 실제 순찰 반경이 공유하는 값.</summary>
        public float RallyAreaSize => rallyAreaSize;

        /// <summary>마커·범위를 담는 컨테이너 이름. HUD 보다 뒤에 그려지도록 sortingOrder 를 낮춘 Canvas.</summary>
        const string OverlayName = "RallyOverlay";

        /// <summary>깃발 복제본을 담는 씬 루트 이름.</summary>
        const string FlagRootName = "RallyFlags";

        public static RallyPointService Instance { get; private set; }

        /// <summary>지금 맵 클릭을 기다리는 중인지.</summary>
        public bool IsPicking { get; private set; }

        /// <summary>지정 모드에서 집결지를 받을 부대. 0 이면 부대 미지정(전체 공용).</summary>
        public int PickingSquadId { get; private set; }

        /// <summary>지금 범위 테두리를 펼쳐 둔 집결지 id. 0 이면 아무 것도 안 펼침.</summary>
        public int ExpandedPointId { get; private set; }

        /// <summary>지정 모드가 켜지거나 꺼질 때.</summary>
        public event System.Action<bool> OnPickingChanged;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _camera = Camera.main;
            _map = FindAnyObjectByType<MapGenerator>();

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            // (GameObject.Find 는 비활성 오브젝트를 못 찾지만 Transform.Find 는 찾는다 —
            //  모체는 전부 비활성이므로 이 차이가 중요하다. 그래서 부모는 켜져 있어야 한다.)
            GameObject canvas = GameObject.Find("UI_Root");

            // 이름표·범위는 HUD 패널 아래(뒤)에 그려야 해서 별도 컨테이너
            // `UI_Root/RallyOverlay` 안에 있다 — 유저 피드백: "집결지 표시 위에 다른 UI 가
            // 있을 때 그 UI 를 가리면 안 된다". 그 컨테이너는 sortingOrder 를 낮춘 Canvas 라
            // 형제 순서와 무관하게 항상 HUD 보다 뒤에 그려진다.
            Transform overlay = canvas != null ? canvas.transform.Find(OverlayName) : null;
            Transform lookupRoot = overlay != null ? overlay : canvas?.transform;

            if (rangeTemplate == null && lookupRoot != null)
                rangeTemplate = lookupRoot.Find("RallyRangeTemplate") as RectTransform;
            if (labelTemplate == null && lookupRoot != null)
                labelTemplate = lookupRoot.Find("RallyLabelTemplate") as RectTransform;

            if (markerParent == null && overlay != null) markerParent = overlay as RectTransform;
            if (markerParent == null && rangeTemplate != null) markerParent = rangeTemplate.parent as RectTransform;

            GameObject flagRoot = GameObject.Find(FlagRootName);
            if (flagRoot != null)
            {
                _flagParent = flagRoot.transform;
                if (flagTemplate == null)
                    flagTemplate = flagRoot.transform.Find("RallyFlagTemplate")?.GetComponent<RallyFlag>();
            }
            if (_flagParent == null && flagTemplate != null) _flagParent = flagTemplate.transform.parent;

            if (flagTemplate == null)
                Debug.LogWarning("[Rally] 깃발 모체(RallyFlags/RallyFlagTemplate)를 찾지 못했습니다.", this);

            if (rangeTemplate != null) rangeTemplate.gameObject.SetActive(false);
            if (labelTemplate != null) labelTemplate.gameObject.SetActive(false);
            if (flagTemplate != null) flagTemplate.gameObject.SetActive(false);
        }

        void Update()
        {
            PruneOrphanPoints();
            UpdatePreview();

            if (IsDraggingFlag) HandleFlagDrag();
            else if (IsPicking) HandlePicking();
            else HandleFlagInput();

            UpdateOverlay();
        }

        void OnDisable()
        {
            // 끌던 중에 꺼지면 카메라 패닝 잠금이 영영 남는다.
            if (IsDraggingFlag) CancelFlagDrag();
        }

        // ------------------------------------------------------------------
        // 깃발 클릭 — 범위 테두리 펼치기/접기 · 꾹 눌러 끌어 옮기기
        // ------------------------------------------------------------------

        /// <summary>지금 깃발을 끌고 있는지.</summary>
        public bool IsDraggingFlag => _dragPointId != 0;

        /// <summary>
        /// 지정 모드가 아닐 때의 깃발 조작 두 가지(유저 확정 2026-08-12):
        /// <list type="bullet">
        /// <item><b>짧게 클릭</b> — 그 집결지의 범위를 테두리로 펼친다/접는다.</item>
        /// <item><b><see cref="dragHoldSeconds"/> 만큼 꾹 누르기</b> — 잔상이 분리되어 마우스를
        ///       따라온다. 누른 채로 옮겨 손을 떼면 그 자리로 집결지가 옮겨간다
        ///       ("창에 매번 들어가서 위치 바꾸는 게 불편하다"는 요청).</item>
        /// </list>
        ///
        /// 판정은 깃발의 <see cref="BoxCollider2D"/> 로 한다 — 그림과 정확히 같은 모양이라
        /// "보이는 곳을 눌렀는데 안 눌린다"가 생기지 않는다. 거리 기반 판정(예전 방식)은
        /// 집결지가 붙어 있으면 엉뚱한 것이 잡혔다(진행상황 미결 41번).
        /// </summary>
        void HandleFlagInput()
        {
            // 건설 자리를 찍는 중이면 그 클릭은 건설 것이다 — 두 기능이 같은 좌클릭을
            // 나눠 쓰므로 명시적으로 비켜준다.
            if (Buildings.BuildService.Instance != null && Buildings.BuildService.Instance.IsPicking) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _pressActive = true;
                _pressPosition = mouse.position.ReadValue();
                _pressStartedOverUI = IsPointerOverUI();

                RallyFlag pressed = _pressStartedOverUI ? null : FindFlagAt(ScreenToWorld(_pressPosition));
                _pressFlagPointId = pressed != null ? pressed.PointId : 0;
                _pressStartTime = Time.unscaledTime;
                return;
            }

            // 누르고 있는 동안 — 꾹 누르기 판정
            if (_pressActive && _pressFlagPointId != 0 && mouse.leftButton.isPressed)
            {
                // 임계값을 넘게 움직였으면 카메라 드래그다 — 꾹 누르기 후보에서 뺀다.
                // (안 그러면 화면을 끌다가 1초가 지나는 순간 깃발이 딸려온다.)
                if ((mouse.position.ReadValue() - _pressPosition).magnitude >= clickThresholdPixels)
                    _pressFlagPointId = 0;
                else if (Time.unscaledTime - _pressStartTime >= dragHoldSeconds)
                    BeginFlagDrag(_pressFlagPointId);
                return;
            }

            if (!mouse.leftButton.wasReleasedThisFrame) return;

            bool wasPress = _pressActive;
            _pressActive = false;
            _pressFlagPointId = 0;
            if (!wasPress || _pressStartedOverUI) return;

            // 카메라를 끌었던 것이면 클릭이 아니다 (UnitSelector 와 같은 규칙).
            Vector2 release = mouse.position.ReadValue();
            if ((release - _pressPosition).magnitude >= clickThresholdPixels) return;

            RallyFlag hit = FindFlagAt(ScreenToWorld(release));
            int id = hit != null ? hit.PointId : 0;

            // 같은 깃발을 다시 누르면 접는다. 빈 곳을 누르면 그냥 접힌다.
            ExpandedPointId = (id != 0 && id == ExpandedPointId) ? 0 : id;
        }

        /// <summary>
        /// 잔상을 분리해 끌기 시작한다. <b>카메라 패닝을 잠근다</b> — 좌클릭 드래그를 카메라와
        /// 나눠 쓰기 때문에, 안 막으면 깃발을 옮기는 내내 화면이 같이 밀린다.
        /// </summary>
        void BeginFlagDrag(int pointId)
        {
            RallyPoint point = _points.Find(p => p.Id == pointId);
            if (point == null) return;

            _dragPointId = pointId;
            _dragWorld = point.World;
            _pressActive = false;
            _pressFlagPointId = 0;

            // ⚠️ 커서 위치를 그대로 깃대 밑동으로 쓰면 안 된다 — 깃발은 세로 2타일이라
            // 보통 깃대 위쪽(천 부분)을 누르게 되고, 그러면 집는 순간 깃발이 2타일 아래로
            // 툭 떨어진다. 집은 순간의 차이를 유지해서 손에 붙어 있게 한다.
            _dragGrabOffset = _camera != null
                            ? point.World - ScreenToWorld(_pressPosition)
                            : Vector3.zero;

            ExpandedPointId = pointId;      // 옮기는 동안 범위를 같이 보여준다
            CameraControl.CameraRigController.PanSuppressed = true;

            // ⚠ 자리표 {0} 은 부대 이름이다 — 표에서 지우면 «어느 부대를 옮기는지» 가 사라진다.
            HudLog.Add(string.Format(
                           HudTheme.T("log_rally_drag_begin",
                                      "{0} 집결지 이동 — 원하는 자리에서 손을 떼세요"),
                           SquadLabel(point.SquadId)),
                       HudLogKind.Warn);
        }

        void HandleFlagDrag()
        {
            Mouse mouse = Mouse.current;

            // 끌던 집결지가 사라졌으면(부대 삭제 등) 조용히 끝낸다.
            if (mouse == null || _points.Find(p => p.Id == _dragPointId) == null) { CancelFlagDrag(); return; }

            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || mouse.rightButton.wasPressedThisFrame)
            {
                CancelFlagDrag();
                HudLog.Add(HudTheme.T("log_rally_move_cancel", "집결지 이동 취소"));
                return;
            }

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) { CancelFlagDrag(); return; }

            // UI 위에 있어도 좌표는 계속 따라간다 — 커서가 잠깐 HUD 를 스쳐도 분신이 멈추면
            // 손에 붙은 느낌이 끊긴다. 실제로 놓을 때만 자리가 유효한지 Snap 이 판단한다.
            _dragWorld = Snap(ScreenToWorld(mouse.position.ReadValue()) + _dragGrabOffset);

            // wasReleasedThisFrame 만 보면 창 밖에서 손을 뗐을 때 영영 끌린 채로 남는다.
            if (!mouse.leftButton.isPressed) CommitFlagDrag();
        }

        void CommitFlagDrag()
        {
            int pointId = _dragPointId;
            Vector3 world = _dragWorld;
            EndFlagDrag();
            MovePoint(pointId, world);
        }

        void CancelFlagDrag() => EndFlagDrag();

        void EndFlagDrag()
        {
            _dragPointId = 0;
            _pressActive = false;
            _pressFlagPointId = 0;
            CameraControl.CameraRigController.PanSuppressed = false;

            if (_ghostFlag != null) _ghostFlag.gameObject.SetActive(false);
        }

        Vector3 ScreenToWorld(Vector2 screen)
        {
            Vector3 world = _camera.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }

        /// <summary>월드 한 점에 걸리는 깃발. 콜라이더 판정이라 그림 밖은 안 잡힌다.</summary>
        RallyFlag FindFlagAt(Vector3 world)
        {
            // 장애물 타일맵의 거대한 CompositeCollider2D 도 같이 잡히므로 RallyFlag 로 걸러낸다.
            Collider2D[] hits = Physics2D.OverlapPointAll(world);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;
                var flag = hits[i].GetComponentInParent<RallyFlag>();
                if (flag != null) return flag;
            }
            return null;
        }

        /// <summary>
        /// 지정 모드 중 마우스가 가리키는 위치를 매 프레임 계산해둔다 — 아직 클릭하지 않았어도
        /// "여기 찍으면 이 정도 범위" 를 미리 보여주기 위한 것. 버튼 등 UI 위에 있을 때는
        /// 숨긴다(그 자리에 범위가 뜨면 혼란스럽다).
        /// </summary>
        void UpdatePreview()
        {
            _hasPreview = false;
            if (!IsPicking) return;
            if (IsPointerOverUI()) return;

            if (_camera == null) _camera = Camera.main;
            Mouse mouse = Mouse.current;
            if (_camera == null || mouse == null) return;

            Vector3 world = _camera.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;

            _previewWorld = Snap(world);
            _hasPreview = true;
        }

        // ------------------------------------------------------------------
        // 지정 모드
        // ------------------------------------------------------------------

        /// <summary>
        /// 이 부대의 집결지를 찍는 모드를 켠다/끈다. 같은 부대로 다시 부르면 끈다
        /// (부대 카드의 "집결지 설정" 버튼이 토글로 동작하게).
        /// </summary>
        public void TogglePickingForSquad(int squadId)
        {
            if (IsPicking && PickingSquadId == squadId) { CancelPicking(); return; }
            BeginPickingForSquad(squadId);
        }

        public void BeginPickingForSquad(int squadId)
        {
            PickingSquadId = squadId;
            _pressActive = false;

            if (!IsPicking)
            {
                IsPicking = true;
                OnPickingChanged?.Invoke(true);
            }

            // ⚠ 자리표 {0} 은 부대 이름이다 — 지우지 말 것.
            HudLog.Add(string.Format(
                           HudTheme.T("log_rally_pick_begin",
                                      "{0} 집결지 지정 — 맵을 클릭하세요 (Esc 취소)"),
                           SquadLabel(squadId)),
                       HudLogKind.Warn);
        }

        public void CancelPicking()
        {
            if (!IsPicking) return;
            IsPicking = false;
            PickingSquadId = 0;
            OnPickingChanged?.Invoke(false);
        }

        void HandlePicking()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPicking();
                // Esc·우클릭 두 경로가 같은 문구다 — 키 하나로 묶는다.
                HudLog.Add(HudTheme.T("log_rally_pick_cancel", "집결지 지정 취소"));
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // 우클릭은 지정 취소 (해제는 부대 카드의 "집결지 해제" 버튼이 한다).
            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelPicking();
                // Esc·우클릭 두 경로가 같은 문구다 — 키 하나로 묶는다.
                HudLog.Add(HudTheme.T("log_rally_pick_cancel", "집결지 지정 취소"));
                return;
            }

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

            // 카메라를 끌었던 것이면 지정이 아니다 (UnitSelector 와 같은 규칙).
            Vector2 release = mouse.position.ReadValue();
            if ((release - _pressPosition).magnitude >= clickThresholdPixels) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Vector3 world = _camera.ScreenToWorldPoint(release);
            world.z = 0f;

            SetRallyPoint(Snap(world), PickingSquadId);
            CancelPicking();
        }

        /// <summary>벽·구조물 칸이면 근처의 갈 수 있는 칸으로 옮겨준다.</summary>
        Vector3 Snap(Vector3 world)
        {
            if (_map == null) return world;

            Vector3Int cell = _map.WorldToCell(world);
            if (_map.IsCellPlaceable(cell)) return _map.CellCenterWorld(cell);

            if (_map.TryFindPlaceableNear(cell, snapSearchRadius, null, out Vector3Int found))
                return _map.CellCenterWorld(found);

            return world;   // 근처에 갈 곳이 없으면 찍은 자리 그대로 (캐릭터가 최대한 접근한다)
        }

        // ------------------------------------------------------------------
        // 집결지 설정 / 해제
        // ------------------------------------------------------------------

        /// <summary>
        /// 이 부대의 집결지를 <paramref name="world"/> 에 둔다.
        /// <b>부대마다 하나</b>라 이미 있으면 새로 만들지 않고 자리만 옮긴다 —
        /// 안 그러면 "집결지 설정"을 누를 때마다 깃발이 쌓이고, 어느 것이 유효한지
        /// (<see cref="TryGetRallyPoint"/> 는 먼저 찾은 것을 쓴다) 알 수 없게 된다.
        /// </summary>
        public RallyPoint SetRallyPoint(Vector3 world, int squadId)
        {
            RallyPoint point = squadId != 0 ? FindBySquad(squadId) : null;

            if (point != null)
            {
                point.World = world;
                if (logChanges) Debug.Log($"[Rally] 집결지 #{point.Id} 이동 → {world} ({SquadLabel(squadId)})");
                // ⚠ {0} = 부대 이름. 이 형식은 MovePoint 와 같은 키를 쓴다(문구가 같다).
                HudLog.Add(string.Format(HudTheme.T("log_rally_moved", "{0} 집결지 이동"),
                                         SquadLabel(squadId)),
                           HudLogKind.Good);
            }
            else
            {
                point = new RallyPoint { Id = _nextPointId++, World = world, SquadId = squadId };
                _points.Add(point);

                if (logChanges) Debug.Log($"[Rally] 집결지 #{point.Id} 생성 → {world} ({SquadLabel(squadId)})");
                // ⚠ {0} = 부대 이름. 지우지 말 것.
                HudLog.Add(string.Format(HudTheme.T("log_rally_set", "{0} 집결지 지정"),
                                         SquadLabel(squadId)),
                           HudLogKind.Good);
            }

            OnPointsChanged?.Invoke();
            return point;
        }

        /// <summary>집결지 하나에 담당 부대를 바꿔 단다. 0 이면 전체 공용으로 되돌린다.</summary>
        public void AssignSquad(int pointId, int squadId)
        {
            RallyPoint point = _points.Find(p => p.Id == pointId);
            if (point == null) return;

            point.SquadId = squadId;
            if (logChanges) Debug.Log($"[Rally] 집결지 #{point.Id} → {SquadLabel(squadId)}");
            // ⚠ {0} = 집결지 번호 · {1} = 부대 이름. 순서를 바꾸지 말 것.
            HudLog.Add(string.Format(HudTheme.T("log_rally_assign", "집결지 #{0} → {1}"),
                                     point.Id, SquadLabel(squadId)),
                       HudLogKind.Good);
            OnPointsChanged?.Invoke();
        }

        /// <summary>
        /// 집결지 하나를 그 자리로 옮긴다. 깃발을 끌어 놓았을 때 쓴다 —
        /// <see cref="SetRallyPoint"/> 는 부대 기준이라 부대 미지정(SquadId 0) 집결지를
        /// 옮길 수 없다(새로 만들어버린다). 그래서 id 로 옮기는 경로를 따로 둔다.
        /// </summary>
        public void MovePoint(int pointId, Vector3 world)
        {
            RallyPoint point = _points.Find(p => p.Id == pointId);
            if (point == null) return;
            if (point.World == world) return;

            point.World = world;
            if (logChanges) Debug.Log($"[Rally] 집결지 #{point.Id} 이동 → {world} ({SquadLabel(point.SquadId)})");
            // SetRallyPoint 의 이동 로그와 같은 문구다 — 키 하나로 묶는다({0} = 부대 이름).
            HudLog.Add(string.Format(HudTheme.T("log_rally_moved", "{0} 집결지 이동"),
                                     SquadLabel(point.SquadId)),
                       HudLogKind.Good);
            OnPointsChanged?.Invoke();
        }

        /// <summary>집결지 하나를 없앤다.</summary>
        public void RemovePoint(int pointId)
        {
            int index = _points.FindIndex(p => p.Id == pointId);
            if (index < 0) return;

            _points.RemoveAt(index);
            if (ExpandedPointId == pointId) ExpandedPointId = 0;

            // ⚠ {0} = 집결지 번호. 지우지 말 것.
            HudLog.Add(string.Format(HudTheme.T("log_rally_removed", "집결지 #{0} 해제"), pointId));
            OnPointsChanged?.Invoke();
        }

        /// <summary>이 부대의 집결지를 해제한다. 없으면 아무 일도 하지 않는다.</summary>
        public bool RemoveForSquad(int squadId)
        {
            RallyPoint point = FindBySquad(squadId);
            if (point == null) return false;

            _points.Remove(point);
            if (ExpandedPointId == point.Id) ExpandedPointId = 0;
            if (IsPicking && PickingSquadId == squadId) CancelPicking();

            // ⚠ {0} = 부대 이름. 지우지 말 것.
            HudLog.Add(string.Format(HudTheme.T("log_rally_removed_squad", "{0} 집결지 해제"),
                                     SquadLabel(squadId)));
            OnPointsChanged?.Invoke();
            return true;
        }

        /// <summary>집결지를 전부 지운다.</summary>
        public void ClearAll()
        {
            if (_points.Count == 0) return;

            _points.Clear();
            ExpandedPointId = 0;
            HudLog.Add(HudTheme.T("log_rally_removed_all", "집결지 전체 해제"));
            OnPointsChanged?.Invoke();
        }

        public RallyPoint FindBySquad(int squadId)
        {
            if (squadId == 0) return null;
            for (int i = 0; i < _points.Count; i++)
                if (_points[i].SquadId == squadId) return _points[i];
            return null;
        }

        public bool HasRallyForSquad(int squadId) => FindBySquad(squadId) != null;

        /// <summary>이 id 의 집결지. 없으면 null.</summary>
        RallyPoint FindById(int pointId)
        {
            if (pointId == 0) return null;
            for (int i = 0; i < _points.Count; i++)
                if (_points[i].Id == pointId) return _points[i];
            return null;
        }

        /// <summary>
        /// ★★ 이 집결지의 깃발에 입힐 <b>부대 색</b> (2026-08-26 · 유저 지시: *"다른 색으로 해줘"*).
        ///
        /// ⚠ <b>부대 id 가 아니라 «목록에서의 순번» 으로 색을 고른다</b> —
        ///   <c>CharacterRosterPanel.SquadOrderOf</c> 가 세운 규칙 그대로다. id 는 부대를
        ///   지웠다 만들면 1·2·5 처럼 띄엄띄엄해져 <b>색이 건너뛴다</b>. 순번이면 «위에서 몇
        ///   번째 부대» 와 색이 언제나 같이 가고, <b>로스터 테두리 · 부대 창 카드 · 이 깃발
        ///   셋이 같은 색</b>이 된다. 그것이 이 색의 유일한 쓸모다.
        ///
        /// ★ <b>부대 미지정(<c>SquadId</c> 0 = 전체 공용)이면 깃발 원래 색</b>을 돌려준다 —
        ///   «없음» 을 어느 부대 색으로도 칠하면 그것도 하나의 부대처럼 보인다
        ///   (로스터가 부대 없는 행의 테두리를 <b>투명</b>으로 두는 것과 같은 판단이다).
        /// ⚠ 원화의 <b>천이 회색</b>이라야 이 곱셈이 산다. 천에 색이 구워져 있으면 곱한 결과가
        ///   탁해진다(볼트 <c>로스터 UI 프롬프트.md</c> 와 같은 이유 · <c>Bar_Fill</c> 이 흰
        ///   그라디언트인 것과 같은 규약).
        /// </summary>
        Color SquadTintFor(RallyPoint point, RallyFlag flag)
        {
            Color basis = flag != null ? flag.DefaultTint : Color.white;
            if (point == null || point.SquadId == 0) return basis;

            int order = SquadOrderOf(point.SquadId);
            return order < 0 ? basis : HudTheme.SquadColor(order);
        }

        /// <summary>부대 id → 부대 목록에서의 순번(0부터). 없으면 −1.</summary>
        static int SquadOrderOf(int squadId)
        {
            Units.SquadService squads = Units.SquadService.Instance;
            if (squads == null || squadId == 0) return -1;

            var list = squads.Squads;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].Id == squadId) return i;

            return -1;
        }

        /// <summary>집결지가 하나라도 있는지.</summary>
        public bool HasAnyRally => _points.Count > 0;

        /// <summary>
        /// 없어진 부대의 집결지는 남겨봐야 아무도 안 쓴다 — 부대를 지우면 같이 지운다.
        /// (<see cref="SquadService"/> 가 아직 없는 첫 프레임에는 아무 것도 하지 않는다.)
        /// </summary>
        void PruneOrphanPoints()
        {
            if (_points.Count == 0) return;

            SquadService squads = SquadService.Instance;
            if (squads == null) return;

            bool changed = false;
            for (int i = _points.Count - 1; i >= 0; i--)
            {
                RallyPoint p = _points[i];
                if (p.SquadId == 0 || squads.Find(p.SquadId) != null) continue;

                if (ExpandedPointId == p.Id) ExpandedPointId = 0;
                _points.RemoveAt(i);
                changed = true;
            }
            if (changed) OnPointsChanged?.Invoke();
        }

        /// <summary>
        /// 부대 id → 화면에 적을 이름. 로그와 깃발 이름표가 <b>같은 문구</b>를 쓴다 —
        /// 그래서 <c>log_</c> 가 아니라 <c>ui_</c> 접두사다.
        /// ★ 유저가 지은 부대 이름은 번역하지 않는다(표를 거치지 않고 그대로 나간다).
        /// </summary>
        static string SquadLabel(int squadId)
        {
            if (squadId == 0) return HudTheme.T("ui_squad_all", "전체");
            var squad = SquadService.Instance != null ? SquadService.Instance.Find(squadId) : null;
            // ⚠ {0} = 부대 번호. 이름 없는 부대의 임시 표기라 자리표를 지우면 셋을 구분할 수 없다.
            return squad != null ? squad.Name
                                 : string.Format(HudTheme.T("ui_squad_numbered", "부대 #{0}"), squadId);
        }

        /// <summary>
        /// 이 캐릭터가 지금 가야 할 집결지. <see cref="CharacterBehavior"/> 가 매 프레임 물어본다.
        /// 서비스가 없거나 지정된 곳이 없으면 false — 그 경우 캐릭터는 원래 정찰·방어 로직으로 돈다.
        ///
        /// 우선순위: <b>① 자기 부대에 배정된 집결지 → ② 부대 미지정(전체 공용) 집결지</b>.
        /// 부대에 배정된 집결지가 있으면 전체 공용보다 그쪽이 먼저다 — 부대별로 다른 곳을
        /// 지키게 하려고 만든 기능이라, 전체 지정이 부대 지정을 덮으면 의미가 없다.
        /// </summary>
        public static bool TryGetRallyPoint(CharacterUnit unit, out Vector3 point)
        {
            point = default;
            RallyPointService service = Instance;
            if (service == null || unit == null) return false;

            int squadId = SquadService.Instance != null ? SquadService.Instance.SquadIdOf(unit) : 0;

            RallyPoint fallback = null;
            for (int i = 0; i < service._points.Count; i++)
            {
                RallyPoint p = service._points[i];
                if (squadId != 0 && p.SquadId == squadId)
                {
                    point = p.World;
                    return true;
                }
                if (p.SquadId == 0 && fallback == null) fallback = p;
            }

            if (fallback != null)
            {
                point = fallback.World;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 표시 — 깃발(월드) · 이름표(UI) · 범위 테두리(UI)
        // ------------------------------------------------------------------

        void UpdateOverlay()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            UpdateFlags();
            UpdateLabels();
            UpdateRanges();
        }

        /// <summary>깃발은 집결지 개수만큼. 월드 오브젝트라 줌은 저절로 따라간다.</summary>
        void UpdateFlags()
        {
            if (flagTemplate == null) return;

            while (_flags.Count < _points.Count)
            {
                RallyFlag clone = Instantiate(flagTemplate, _flagParent);
                clone.name = $"RallyFlag_{_flags.Count + 1}";
                _flags.Add(clone);
            }

            for (int i = 0; i < _flags.Count; i++)
            {
                RallyFlag flag = _flags[i];
                if (flag == null) continue;

                bool used = i < _points.Count;
                if (flag.gameObject.activeSelf != used) flag.gameObject.SetActive(used);
                if (!used) continue;

                RallyPoint p = _points[i];
                flag.transform.position = p.World;
                flag.Bind(p.Id);

                // ★★ 깃발은 <b>그 집결지를 쓰는 부대의 색</b>으로 칠한다 (2026-08-26 · 유저 지시).
                //   상태(잔상·펼침)는 그 색 «위에» 얹는다 — 상태가 부대 색을 지우면
                //   «어느 부대의 집결지인가» 가 그 순간 사라진다.
                Color squad = SquadTintFor(p, flag);

                // 끌고 있는 깃발은 원래 자리에 옅은 잔상으로 남는다 — 분신이 어디서 떨어져
                // 나왔는지, 취소하면 어디로 돌아가는지가 보여야 한다.
                flag.SetTint(p.Id == _dragPointId ? squad * dragSourceColor
                           : p.Id == ExpandedPointId ? Color.Lerp(squad, flagHighlight, expandedLift)
                           : squad);
            }

            UpdateGhostFlag();
        }

        /// <summary>
        /// 마우스를 따라다니는 분신 깃발. 모체를 <b>한 번만</b> 복제해 두고 켜고 끈다 —
        /// 끌 때마다 만들고 지우면 GC 가 돈다(다른 오버레이 풀과 같은 방식).
        /// </summary>
        void UpdateGhostFlag()
        {
            if (!IsDraggingFlag)
            {
                if (_ghostFlag != null && _ghostFlag.gameObject.activeSelf)
                    _ghostFlag.gameObject.SetActive(false);
                return;
            }

            if (_ghostFlag == null)
            {
                if (flagTemplate == null) return;
                _ghostFlag = Instantiate(flagTemplate, _flagParent);
                _ghostFlag.name = "RallyFlagGhost";

                // 분신은 클릭 대상이 아니다 — 켜져 있으면 자기 자신을 집어 올리게 된다.
                var col = _ghostFlag.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }

            if (!_ghostFlag.gameObject.activeSelf) _ghostFlag.gameObject.SetActive(true);
            _ghostFlag.transform.position = _dragWorld;

            // ★ 분신도 <b>끌고 있는 그 집결지의 부대 색</b>을 쓴다 — 잔상(원래 자리)과 분신이
            //   같은 색이라야 «이것이 저것이 옮겨간 것» 으로 읽힌다.
            _ghostFlag.SetTint(SquadTintFor(FindById(_dragPointId), _ghostFlag) * dragGhostColor);
        }

        /// <summary>이름표는 깃발 꼭대기 위에 뜬다. UI 라 줌과 무관하게 항상 같은 크기로 읽힌다.</summary>
        void UpdateLabels()
        {
            if (labelTemplate == null || markerParent == null) return;

            while (_labels.Count < _points.Count)
            {
                RectTransform clone = Instantiate(labelTemplate, markerParent);
                clone.name = $"RallyLabel_{_labels.Count + 1}";
                _labels.Add(clone);
            }

            for (int i = 0; i < _labels.Count; i++)
            {
                RectTransform label = _labels[i];
                if (label == null) continue;

                bool used = i < _points.Count && i < _flags.Count && _flags[i] != null;
                if (label.gameObject.activeSelf != used) label.gameObject.SetActive(used);
                if (!used) continue;

                // 끌고 있는 집결지의 이름표는 잔상이 아니라 분신을 따라간다 — 지금 어디로
                // 옮기는 중인지가 이름과 함께 보여야 한다.
                bool followsGhost = _points[i].Id == _dragPointId
                                 && _ghostFlag != null && _ghostFlag.gameObject.activeSelf;

                Vector3 screen = _camera.WorldToScreenPoint(followsGhost ? _ghostFlag.TopWorld
                                                                        : _flags[i].TopWorld);
                if (screen.z < 0f) { label.gameObject.SetActive(false); continue; }

                screen.y += labelScreenOffset;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markerParent, screen, null,
                                                                         out Vector2 local);
                label.anchoredPosition = local;

                var text = label.GetComponent<TMP_Text>();
                if (text != null) text.text = SquadLabel(_points[i].SquadId);
            }
        }

        /// <summary>
        /// 범위 테두리는 <b>최대 2개</b>만 있으면 된다 — 미리보기 하나, 펼쳐둔 집결지 하나.
        /// 예전처럼 집결지마다 원을 깔지 않는다(유저 요청: "노랗게 계속 표시되지 않게").
        /// </summary>
        void UpdateRanges()
        {
            if (rangeTemplate == null || markerParent == null) return;

            while (_ranges.Count < 2)
            {
                RectTransform clone = Instantiate(rangeTemplate, markerParent);
                clone.name = $"RallyRange_{_ranges.Count + 1}";
                ApplyOutlineSprite(clone);
                _ranges.Add(clone);
            }

            PlaceRange(_ranges[0], _hasPreview, _previewWorld, previewColor);

            // 끌고 있는 동안에는 원래 자리가 아니라 <b>지금 놓으려는 자리</b>의 범위를 보여준다 —
            // "여기 놓으면 부대가 어디까지 퍼지는지"가 판단 기준이기 때문.
            RallyPoint expanded = ExpandedPointId != 0 ? _points.Find(p => p.Id == ExpandedPointId) : null;
            Vector3 rangeWorld = IsDraggingFlag && expanded != null && expanded.Id == _dragPointId
                               ? _dragWorld
                               : (expanded?.World ?? Vector3.zero);
            PlaceRange(_ranges[1], expanded != null, rangeWorld, rangeColor);
        }

        /// <summary>
        /// 원본 Image 의 스프라이트를 <b>코드에서</b> 테두리 그림으로 바꾼다 —
        /// MCP 로는 씬의 Image.m_Sprite 를 못 바꾼다(진행상황 27-9절에서 확인된 한계).
        /// </summary>
        void ApplyOutlineSprite(RectTransform item)
        {
            if (!_outlineLoaded)
            {
                _outlineLoaded = true;
                if (!string.IsNullOrEmpty(rangeOutlineResource))
                {
                    _outlineSprite = Resources.Load<Sprite>(rangeOutlineResource);
                    if (_outlineSprite == null)
                        Debug.LogWarning($"[Rally] 범위 테두리 그림 'Resources/{rangeOutlineResource}' 을 " +
                                         "찾지 못했습니다. 예전 원판 그림 그대로 그립니다.", this);
                }
            }
            if (_outlineSprite == null) return;

            var image = item.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.sprite = _outlineSprite;
        }

        void PlaceRange(RectTransform item, bool used, Vector3 world, Color color)
        {
            if (item == null) return;

            if (item.gameObject.activeSelf != used) item.gameObject.SetActive(used);
            if (!used) return;

            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z < 0f) { item.gameObject.SetActive(false); return; }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(markerParent, screen, null,
                                                                     out Vector2 local);
            item.anchoredPosition = local;
            item.sizeDelta = WorldSizeToLocalSize(world, rallyAreaSize);

            var graphic = item.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null) graphic.color = color;
        }

        /// <summary>
        /// 월드 공간에서 <paramref name="worldSize"/>(원의 지름, 타일=월드 유닛) 만큼의 크기가
        /// 지금 카메라·캔버스 스케일에서 화면 상 몇 로컬 유닛인지 계산한다. 카메라 줌·해상도·
        /// CanvasScaler 배율을 전부 자동으로 반영한다 — 정사각형 바운딩 박스의 두 모서리를 각각
        /// 스크린 → 캔버스 로컬 좌표로 변환해서 차이를 재는 것이라, 그 사이의 모든 변환 단계가
        /// 자동으로 상쇄된다. 결과는 원형 스프라이트가 정확히 이 지름으로 그려지도록 sizeDelta 에 쓰인다.
        /// </summary>
        Vector2 WorldSizeToLocalSize(Vector3 centerWorld, float worldSize)
        {
            float half = worldSize * 0.5f;
            Vector3 corner1 = centerWorld + new Vector3(half, half, 0f);
            Vector3 corner2 = centerWorld + new Vector3(-half, -half, 0f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                markerParent, _camera.WorldToScreenPoint(corner1), null, out Vector2 local1);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                markerParent, _camera.WorldToScreenPoint(corner2), null, out Vector2 local2);

            return new Vector2(Mathf.Abs(local1.x - local2.x), Mathf.Abs(local1.y - local2.y));
        }

        static bool IsPointerOverUI()
        {
            EventSystem es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
