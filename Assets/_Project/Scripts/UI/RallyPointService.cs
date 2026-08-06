using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using LastSanctuary.Map;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 집결지 지정. "집결지 설정" 버튼을 누르면 지정 모드로 들어가고, 맵을 클릭하면
    /// 그 지점이 집결지가 된다.
    ///
    /// <b>대상 규칙</b>: 선택된 캐릭터가 있으면 그 캐릭터에게만, 없으면 전체에게 건다.
    /// (목업의 "선택된 캐릭터에게만 적용" 과 같은 개념)
    ///
    /// 이동 자체는 새로 짜지 않는다 — <see cref="CharacterBehavior"/> 가 매 프레임
    /// 여기 물어보고, 집결지가 있으면 정찰·순찰 대신 그 지점을 목적지로 삼는다.
    /// 이 프로젝트가 이미 쓰는 "귀환 지점을 옮겨 이동 명령을 대신한다" 방식 그대로다(진행상황 12절).
    ///
    /// 마커는 <see cref="markerTemplate"/> 하나를 복제해서 쓴다 — 오브젝트는 MCP 로
    /// 하이라키에 만들고 반복되는 것만 스크립트가 복제한다는 규칙(준수사항 §10 H-2).
    ///
    /// <b>범위 표시</b>: 집결지는 점이 아니라 <see cref="RallyAreaSize"/> 만큼의 정사각 구역이다
    /// (<see cref="CharacterBehavior"/> 가 그 구역 안에서 경계 순찰한다). 그래서 마커(점)뿐 아니라
    /// 그 구역 크기를 그대로 보여주는 반투명 사각형도 같이 그린다 — 지정 모드로 들어가면 마우스를
    /// 따라 <b>미리보기</b>가 뜨고, 실제로 찍으면 그 자리에 <b>고정</b>된다. 이 클래스가
    /// <see cref="RallyAreaSize"/> 의 정본이다 — <c>CharacterBehavior</c> 는 실제 순찰 반경을 정할 때
    /// 이 값을 그대로 읽어간다(화면에 보이는 범위와 실제 순찰 범위가 항상 같아야 하므로, 값을
    /// 두 곳에 따로 두지 않는다).
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
        [Tooltip("집결지 구역의 한 변 길이(타일). CharacterBehavior 의 실제 순찰 범위도 " +
                 "이 값을 그대로 쓴다 — 화면에 보이는 범위와 실제 동작이 항상 일치하게")]
        [Min(2f)] [SerializeField] float rallyAreaSize = 10f;

        [Header("마커 · 범위 (모체 하나씩 복제해서 쓴다 — 비활성으로 둘 것)")]
        [Tooltip("집결지 위치에 표시할 점 마커의 원본. UI_Root 아래 비활성 오브젝트")]
        [SerializeField] RectTransform markerTemplate;

        [Tooltip("집결지 구역을 나타내는 사각형의 원본. UI_Root 아래 비활성 오브젝트")]
        [SerializeField] RectTransform rangeTemplate;

        [Tooltip("마커·범위를 그릴 캔버스. 비워두면 markerTemplate 의 부모를 쓴다")]
        [SerializeField] RectTransform markerParent;

        [Tooltip("지정 모드 중 미리보기(아직 찍지 않은 것)의 불투명도 배율. 1보다 작으면 " +
                 "확정된 집결지보다 옅게 보여 구분된다")]
        [Range(0.1f, 1f)] [SerializeField] float previewAlphaScale = 0.55f;

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        /// <summary>캐릭터별 집결지. 값이 없으면 <see cref="_globalRally"/> 를 본다.</summary>
        readonly Dictionary<CharacterUnit, Vector3> _perUnit = new Dictionary<CharacterUnit, Vector3>();

        /// <summary>전체 공통 집결지. 선택 없이 지정하면 여기 들어간다.</summary>
        Vector3? _globalRally;

        readonly List<RectTransform> _markers = new List<RectTransform>();
        readonly List<RectTransform> _ranges = new List<RectTransform>();

        /// <summary>이번 프레임에 마커·범위를 그려야 할 월드 위치들. 인덱스 0 은 미리보기일 수 있다.</summary>
        readonly List<Vector3> _activePoints = new List<Vector3>();

        Vector3 _previewWorld;
        bool _hasPreview;

        Camera _camera;
        MapGenerator _map;
        Vector2 _pressPosition;
        bool _pressActive;
        bool _pressStartedOverUI;

        /// <summary>집결지 구역의 한 변 길이(타일). 마커 범위 표시와 실제 순찰 반경이 공유하는 값.</summary>
        public float RallyAreaSize => rallyAreaSize;

        /// <summary>마커·범위를 담는 컨테이너 이름. HUD 보다 뒤에 그려지도록 sortingOrder 를 낮춘 Canvas.</summary>
        const string OverlayName = "RallyOverlay";

        public static RallyPointService Instance { get; private set; }

        /// <summary>지금 맵 클릭을 기다리는 중인지.</summary>
        public bool IsPicking { get; private set; }

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
            // 비어 있으면 UI_Root 아래에서 이름으로 찾는다.
            // (GameObject.Find 는 비활성 오브젝트를 못 찾지만 Transform.Find 는 찾는다 —
            //  마커 모체는 비활성이므로 이 차이가 중요하다.)
            GameObject canvas = GameObject.Find("UI_Root");

            // 마커·범위는 HUD 패널 아래(뒤)에 그려야 해서 별도 컨테이너
            // `UI_Root/RallyOverlay` 안에 있다 — 유저 피드백: "집결지 표시 위에 다른 UI 가
            // 있을 때 그 UI 를 가리면 안 된다". 그 컨테이너는 sortingOrder 를 낮춘 Canvas 라
            // 형제 순서와 무관하게 항상 HUD 보다 뒤에 그려진다.
            // 예전 위치(UI_Root 직속)에 그대로 있는 씬도 계속 돌아가게 두 곳을 다 본다.
            Transform overlay = canvas != null ? canvas.transform.Find(OverlayName) : null;
            Transform lookupRoot = overlay != null ? overlay : canvas?.transform;

            if (markerTemplate == null && lookupRoot != null)
                markerTemplate = lookupRoot.Find("RallyMarkerTemplate") as RectTransform;
            if (rangeTemplate == null && lookupRoot != null)
                rangeTemplate = lookupRoot.Find("RallyRangeTemplate") as RectTransform;

            // 컨테이너가 있으면 복제본도 그 안에 넣는다(그래야 같이 뒤로 깔린다).
            if (markerParent == null && overlay != null) markerParent = overlay as RectTransform;

            if (markerParent == null)
                markerParent = (markerTemplate != null ? markerTemplate.parent
                              : rangeTemplate != null ? rangeTemplate.parent
                              : null) as RectTransform;

            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
            if (rangeTemplate != null) rangeTemplate.gameObject.SetActive(false);
        }

        void Update()
        {
            UpdatePreview();
            if (IsPicking) HandlePicking();
            PruneDeadUnits();
            UpdateOverlay();
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

        /// <summary>지정 모드를 켠다/끈다.</summary>
        public void TogglePicking()
        {
            if (IsPicking) CancelPicking();
            else BeginPicking();
        }

        public void BeginPicking()
        {
            if (IsPicking) return;
            IsPicking = true;
            _pressActive = false;
            OnPickingChanged?.Invoke(true);

            CharacterUnit selected = UnitSelector.Instance != null ? UnitSelector.Instance.Selected : null;
            HudLog.Add(selected != null
                           ? $"집결지 지정 — {selected.name}. 맵을 클릭하세요"
                           : "집결지 지정 — 전체. 맵을 클릭하세요",
                       HudLogKind.Warn);
        }

        public void CancelPicking()
        {
            if (!IsPicking) return;
            IsPicking = false;
            OnPickingChanged?.Invoke(false);
        }

        void HandlePicking()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPicking();
                HudLog.Add("집결지 지정 취소");
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // 우클릭은 해제.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                ClearForCurrentTarget();
                CancelPicking();
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

            SetRallyPoint(Snap(world));
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

        /// <summary>선택된 캐릭터(없으면 전체)의 집결지를 지정한다.</summary>
        public void SetRallyPoint(Vector3 world)
        {
            CharacterUnit selected = UnitSelector.Instance != null ? UnitSelector.Instance.Selected : null;

            if (selected != null)
            {
                _perUnit[selected] = world;
                if (logChanges) Debug.Log($"[Rally] {selected.name} 집결지 → {world}", selected);
                HudLog.Add($"{selected.name} 집결지 지정", HudLogKind.Good);
            }
            else
            {
                _globalRally = world;
                _perUnit.Clear();   // 전체 지정은 개별 지정을 덮는다 — 규칙이 두 벌로 갈리지 않게
                if (logChanges) Debug.Log($"[Rally] 전체 집결지 → {world}");
                HudLog.Add("전체 집결지 지정", HudLogKind.Good);
            }
        }

        /// <summary>지금 대상(선택된 캐릭터 또는 전체)의 집결지를 해제한다.</summary>
        public void ClearForCurrentTarget()
        {
            CharacterUnit selected = UnitSelector.Instance != null ? UnitSelector.Instance.Selected : null;

            if (selected != null)
            {
                if (_perUnit.Remove(selected)) HudLog.Add($"{selected.name} 집결지 해제");
            }
            else
            {
                _globalRally = null;
                _perUnit.Clear();
                HudLog.Add("집결지 전체 해제");
            }
        }

        /// <summary>집결지가 하나라도 걸려 있는지. 버튼 문구를 정할 때 쓴다.</summary>
        public bool HasAnyRally => _globalRally.HasValue || _perUnit.Count > 0;

        /// <summary>
        /// 이 캐릭터가 지금 가야 할 집결지. <see cref="CharacterBehavior"/> 가 매 프레임 물어본다.
        /// 서비스가 없거나 지정된 곳이 없으면 false — 그 경우 캐릭터는 원래 정찰·방어 로직으로 돈다.
        /// </summary>
        public static bool TryGetRallyPoint(CharacterUnit unit, out Vector3 point)
        {
            point = default;
            RallyPointService service = Instance;
            if (service == null || unit == null) return false;

            if (service._perUnit.TryGetValue(unit, out point)) return true;

            if (service._globalRally.HasValue)
            {
                point = service._globalRally.Value;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 마커
        // ------------------------------------------------------------------

        /// <summary>죽은 캐릭터의 개별 집결지는 들고 있어봐야 의미가 없다.</summary>
        void PruneDeadUnits()
        {
            if (_perUnit.Count == 0) return;

            List<CharacterUnit> dead = null;
            foreach (var pair in _perUnit)
            {
                if (pair.Key != null && pair.Key.IsAlive) continue;
                (dead ??= new List<CharacterUnit>()).Add(pair.Key);
            }
            if (dead == null) return;

            for (int i = 0; i < dead.Count; i++) _perUnit.Remove(dead[i]);
        }

        /// <summary>
        /// 표시할 집결지 목록(미리보기 + 확정된 것들)을 모아 마커·범위 오브젝트를 그 개수만큼
        /// 맞추고 화면 좌표로 옮긴다. 미리보기가 있으면 항상 인덱스 0 이다.
        /// </summary>
        void UpdateOverlay()
        {
            if (markerParent == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            _activePoints.Clear();
            if (_hasPreview) _activePoints.Add(_previewWorld);
            if (_globalRally.HasValue) _activePoints.Add(_globalRally.Value);
            foreach (var pair in _perUnit) _activePoints.Add(pair.Value);

            SyncPool(_markers, markerTemplate, "RallyMarker");
            SyncPool(_ranges, rangeTemplate, "RallyRange");

            int slots = Mathf.Max(_markers.Count, _ranges.Count);
            for (int i = 0; i < slots; i++)
            {
                bool used = i < _activePoints.Count;
                // 미리보기(인덱스 0, _hasPreview 일 때)는 옅게 — 확정 전이라는 걸 구분해준다.
                float alphaScale = used && _hasPreview && i == 0 ? previewAlphaScale : 1f;

                PlaceOverlayItem(_markers, i, used, alphaScale, isRange: false);
                PlaceOverlayItem(_ranges, i, used, alphaScale, isRange: true);
            }
        }

        /// <summary>모자라면 모체를 복제해 채운다 (§10 H-2 템플릿 복제). 템플릿이 없으면 아무 것도 안 한다.</summary>
        void SyncPool(List<RectTransform> pool, RectTransform template, string namePrefix)
        {
            if (template == null) return;
            while (pool.Count < _activePoints.Count)
            {
                RectTransform clone = Instantiate(template, markerParent);
                clone.name = $"{namePrefix}_{pool.Count + 1}";
                pool.Add(clone);
            }
        }

        /// <summary>
        /// 마커 하나(점 또는 범위 사각형)를 화면 위치·크기·투명도까지 갱신한다.
        /// 범위 사각형만 <paramref name="isRange"/> 로 표시해 매 프레임 실제 월드 크기로 다시 잰다 —
        /// 카메라 줌이 바뀌면 같은 10타일이 화면에서 차지하는 픽셀 크기도 바뀐다(진행상황 §11,
        /// 월드 공간 UI는 줌에 따라 크기가 튄다). 점 마커는 고정 픽셀 크기라 크기 갱신이 필요 없다.
        /// </summary>
        void PlaceOverlayItem(List<RectTransform> pool, int index, bool used, float alphaScale, bool isRange)
        {
            if (index >= pool.Count) return;
            RectTransform item = pool[index];

            if (item.gameObject.activeSelf != used) item.gameObject.SetActive(used);
            if (!used) return;

            Vector3 world = _activePoints[index];
            Vector3 screen = _camera.WorldToScreenPoint(world);

            // 카메라 뒤로 넘어가면(2D 에선 드물지만) 화면 밖으로 치운다.
            if (screen.z < 0f) { item.gameObject.SetActive(false); return; }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(markerParent, screen, null,
                                                                     out Vector2 local);
            item.anchoredPosition = local;

            if (isRange) item.sizeDelta = WorldSizeToLocalSize(world, rallyAreaSize);

            var graphic = item.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
            {
                Color c = graphic.color;
                c.a = BaseAlphaOf(item) * alphaScale;
                graphic.color = c;
            }
        }

        // 마커·범위는 원본 알파(디자인 시 정한 투명도)를 유지한 채 미리보기일 때만 더 옅게
        // 만들어야 하므로, 매 프레임 원본 알파를 다시 계산하지 않고 오브젝트마다 한 번만 기억해둔다.
        readonly Dictionary<RectTransform, float> _baseAlpha = new Dictionary<RectTransform, float>();

        float BaseAlphaOf(RectTransform item)
        {
            if (_baseAlpha.TryGetValue(item, out float a)) return a;

            var graphic = item.GetComponent<UnityEngine.UI.Graphic>();
            a = graphic != null ? graphic.color.a : 1f;
            _baseAlpha[item] = a;
            return a;
        }

        /// <summary>
        /// 월드 공간에서 <paramref name="worldSize"/>(정사각 한 변, 타일=월드 유닛) 만큼의 크기가
        /// 지금 카메라·캔버스 스케일에서 화면 상 몇 로컬 유닛인지 계산한다. 카메라 줌·해상도·
        /// CanvasScaler 배율을 전부 자동으로 반영한다 — 두 모서리를 각각 스크린 → 캔버스 로컬
        /// 좌표로 변환해서 차이를 재는 것이라, 그 사이의 모든 변환 단계가 자동으로 상쇄된다.
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
