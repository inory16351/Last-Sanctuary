using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 마우스 클릭으로 캐릭터를 선택한다. 선택된 캐릭터는 강화 버튼 등 UI 가 참조한다.
    ///
    /// <b>물리 레이캐스트를 쓰지 않는다</b> — 이 프로젝트의 유닛에는 Collider2D 가 없고
    /// 이동 충돌조차 타일 기준으로 판정한다(13절). 그 방식을 유지해서, 마우스 월드 좌표가
    /// 어느 캐릭터의 스프라이트 경계 안에 있는지를 직접 검사한다. 덕분에 템플릿에
    /// 콜라이더를 새로 붙이지 않아도 되고, 콜라이더가 전투/이동 판정에 끼어들 여지도 없다.
    ///
    /// 카메라 드래그(<c>CameraRigController</c>)와 좌클릭을 공유하므로, 누른 지점에서
    /// 임계값 이상 움직였으면 드래그로 보고 선택하지 않는다.
    /// </summary>
    public class UnitSelector : MonoBehaviour
    {
        [Header("클릭 판정")]
        [Tooltip("누른 지점에서 이 픽셀 이상 움직이면 카메라 드래그로 보고 선택하지 않는다. " +
                 "CameraRigController 의 드래그 임계값과 같은 값으로 두는 게 좋다")]
        [Min(0f)] [SerializeField] float clickThresholdPixels = 4f;

        [Tooltip("스프라이트 경계 안에 아무도 없을 때, 이 반경(타일) 안의 가장 가까운 " +
                 "캐릭터를 잡아준다. 도트가 작아 정확히 찍기 어려운 것을 보정한다")]
        [Min(0f)] [SerializeField] float pickAssistRadiusTiles = 0.75f;

        [Tooltip("빈 땅을 클릭하면 선택을 해제한다")]
        [SerializeField] bool clearOnEmptyClick = true;

        [Header("선택 표시")]
        [Tooltip("선택된 캐릭터의 스프라이트에 곱해지는 색. UI 가 아직 없어 이게 유일한 표시다")]
        [SerializeField] Color selectedTint = new Color(0.55f, 1f, 0.7f, 1f);

        [Tooltip("끄면 색을 바꾸지 않는다(외곽선 등 다른 연출을 붙일 때)")]
        [SerializeField] bool tintSelected = true;

        [Header("디버그")]
        [SerializeField] bool logSelection = true;

        CharacterUnit _selected;
        SpriteRenderer _selectedRenderer;
        Color _originalColor;

        Camera _camera;
        Vector2 _pressPosition;
        bool _pressStartedOverUI;
        bool _pressActive;

        /// <summary>지금 선택된 캐릭터. 없으면 null.</summary>
        public CharacterUnit Selected => _selected;

        /// <summary>선택이 바뀔 때마다 발생 (새 선택, 없으면 null).</summary>
        public event System.Action<CharacterUnit> OnSelectionChanged;

        public static UnitSelector Instance { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => _camera = Camera.main;

        void Update()
        {
            // 선택된 캐릭터가 죽거나 파괴되면 선택을 놓는다.
            if (_selected != null && !_selected.IsAlive) Clear();
            if (_selected == null && _selectedRenderer != null) Clear();

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

            CharacterUnit hit = PickAt(release);
            if (hit != null) Select(hit);
            else if (clearOnEmptyClick) Clear();
        }

        /// <summary>화면 좌표 아래에 있는 캐릭터를 찾는다.</summary>
        CharacterUnit PickAt(Vector2 screenPosition)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return null;

            Vector3 world = _camera.ScreenToWorldPoint(screenPosition);
            world.z = 0f;

            CharacterUnit nearest = null;
            float nearestSqr = float.MaxValue;
            float assistSqr = pickAssistRadiusTiles * pickAssistRadiusTiles;

            // 캐릭터 수가 적어(3명 수준) 전수 검사로 충분하다.
            CharacterUnit[] all = FindObjectsByType<CharacterUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                CharacterUnit c = all[i];
                if (c == null || !c.IsAlive) continue;

                // 스프라이트 경계 안이면 그 자리에서 확정한다.
                var sr = c.GetComponent<SpriteRenderer>();
                if (sr != null && sr.enabled)
                {
                    Bounds b = sr.bounds;
                    if (world.x >= b.min.x && world.x <= b.max.x &&
                        world.y >= b.min.y && world.y <= b.max.y)
                        return c;
                }

                float sqr = ((Vector2)(c.transform.position - world)).sqrMagnitude;
                if (sqr <= assistSqr && sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = c;
                }
            }
            return nearest;
        }

        static bool IsPointerOverUI()
        {
            EventSystem es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------

        /// <summary>캐릭터를 선택한다. 같은 대상을 다시 넣으면 아무 일도 하지 않는다.</summary>
        public void Select(CharacterUnit unit)
        {
            if (unit == null) { Clear(); return; }
            if (ReferenceEquals(unit, _selected)) return;

            RestoreTint();

            _selected = unit;
            _selectedRenderer = unit.GetComponent<SpriteRenderer>();
            if (tintSelected && _selectedRenderer != null)
            {
                _originalColor = _selectedRenderer.color;
                _selectedRenderer.color = selectedTint;
            }

            if (logSelection) Debug.Log($"[Select] {unit.name} 선택 · {unit.DebugSummary()}", unit);
            OnSelectionChanged?.Invoke(_selected);
        }

        /// <summary>선택을 해제한다.</summary>
        public void Clear()
        {
            if (_selected == null && _selectedRenderer == null) return;

            RestoreTint();
            _selected = null;
            _selectedRenderer = null;
            OnSelectionChanged?.Invoke(null);
        }

        void RestoreTint()
        {
            if (tintSelected && _selectedRenderer != null) _selectedRenderer.color = _originalColor;
            _selectedRenderer = null;
        }
    }
}
