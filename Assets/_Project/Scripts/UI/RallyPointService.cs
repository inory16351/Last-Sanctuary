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
    /// </summary>
    public class RallyPointService : MonoBehaviour
    {
        [Header("클릭 판정")]
        [Tooltip("누른 지점에서 이 픽셀 이상 움직이면 카메라 드래그로 보고 지정하지 않는다. " +
                 "UnitSelector·CameraRigController 와 같은 값으로 둘 것")]
        [Min(0f)] [SerializeField] float clickThresholdPixels = 4f;

        [Tooltip("벽·구조물 칸을 찍으면 근처의 갈 수 있는 칸으로 밀어준다. 그 탐색 반경(타일)")]
        [Min(0)] [SerializeField] int snapSearchRadius = 6;

        [Header("마커 (모체 하나를 복제해서 쓴다 — 비활성으로 둘 것)")]
        [Tooltip("집결지 위치에 표시할 마커의 원본. UI_Root 아래 비활성 오브젝트")]
        [SerializeField] RectTransform markerTemplate;

        [Tooltip("마커를 그릴 캔버스. 비워두면 markerTemplate 의 부모를 쓴다")]
        [SerializeField] RectTransform markerParent;

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        /// <summary>캐릭터별 집결지. 값이 없으면 <see cref="_globalRally"/> 를 본다.</summary>
        readonly Dictionary<CharacterUnit, Vector3> _perUnit = new Dictionary<CharacterUnit, Vector3>();

        /// <summary>전체 공통 집결지. 선택 없이 지정하면 여기 들어간다.</summary>
        Vector3? _globalRally;

        readonly List<RectTransform> _markers = new List<RectTransform>();
        readonly List<Vector3> _markerWorld = new List<Vector3>();

        Camera _camera;
        MapGenerator _map;
        Vector2 _pressPosition;
        bool _pressActive;
        bool _pressStartedOverUI;

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
            if (markerTemplate == null)
            {
                GameObject canvas = GameObject.Find("UI_Root");
                if (canvas != null)
                    markerTemplate = canvas.transform.Find("RallyMarkerTemplate") as RectTransform;
            }

            if (markerTemplate != null)
            {
                if (markerParent == null) markerParent = markerTemplate.parent as RectTransform;
                markerTemplate.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (IsPicking) HandlePicking();
            PruneDeadUnits();
            UpdateMarkers();
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

        /// <summary>표시할 집결지 목록을 모아 마커를 그 개수만큼 맞춘 뒤 화면 좌표로 옮긴다.</summary>
        void UpdateMarkers()
        {
            if (markerTemplate == null || markerParent == null) return;
            if (_camera == null) _camera = Camera.main;

            _markerWorld.Clear();
            if (_globalRally.HasValue) _markerWorld.Add(_globalRally.Value);
            foreach (var pair in _perUnit) _markerWorld.Add(pair.Value);

            // 모자라면 모체를 복제해 채운다 (§10 H-2 템플릿 복제).
            while (_markers.Count < _markerWorld.Count)
            {
                RectTransform clone = Instantiate(markerTemplate, markerParent);
                clone.name = $"RallyMarker_{_markers.Count + 1}";
                _markers.Add(clone);
            }

            for (int i = 0; i < _markers.Count; i++)
            {
                bool used = i < _markerWorld.Count && _camera != null;
                if (_markers[i].gameObject.activeSelf != used) _markers[i].gameObject.SetActive(used);
                if (!used) continue;

                Vector3 screen = _camera.WorldToScreenPoint(_markerWorld[i]);
                // 카메라 뒤로 넘어가면(2D 에선 드물지만) 화면 밖으로 치운다.
                if (screen.z < 0f) { _markers[i].gameObject.SetActive(false); continue; }

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    markerParent, screen, null, out Vector2 local);
                _markers[i].anchoredPosition = local;
            }
        }

        static bool IsPointerOverUI()
        {
            EventSystem es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
