using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering.Universal;

namespace LastSanctuary.CameraControl
{
    /// <summary>
    /// 붙잡아 끄는 방식(grab-pan)의 RTS 카메라.
    /// 이 컴포넌트는 자기 Transform(CameraAnchor)만 움직이고, 실제 카메라는 Cinemachine 이 따라온다.
    ///
    /// 조작
    ///   · 좌클릭 또는 휠클릭 드래그  → 월드를 붙잡고 끌기 (마우스에 1:1 추종 + 관성)
    ///   · WASD / 방향키              → 패닝
    ///   · 마우스 휠 스크롤           → 확대/축소
    ///   · Space                      → 넥서스(원점)로 복귀
    ///
    /// 가장자리 패닝은 기본 비활성이다. 이 게임은 UI 패널이 화면 하단·좌상단·우측에 붙어 있고
    /// 자동 전투라 마우스가 UI 에 머무는 시간이 길어서, 가장자리 패닝을 켜면
    /// UI 를 누르려는 동작마다 화면이 스크롤되어 서로 충돌한다.
    /// </summary>
    public class CameraRigController : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("줌(Lens.OrthographicSize)을 제어할 Cinemachine 카메라")]
        [SerializeField] CinemachineCamera cineCamera;

        [Tooltip("카메라가 벗어나지 못할 맵 경계. MapBounds 의 Collider2D")]
        [SerializeField] Collider2D boundsShape;

        [Header("드래그 패닝 (주 조작)")]
        [SerializeField] DragButton dragButton = DragButton.LeftOrMiddle;

        [Tooltip("끄면 마우스 방향과 같은 쪽으로 이동 (지도를 미는 느낌)")]
        [SerializeField] bool dragGrabsWorld = true;

        [Tooltip("이 픽셀 이상 움직여야 드래그로 인정. 그 미만은 클릭으로 남겨둔다 " +
                 "(캐릭터 선택 등 클릭 조작과 공존시키기 위함)")]
        [Min(0f)] [SerializeField] float dragThresholdPixels = 4f;

        [Tooltip("UI 위에서 시작한 드래그는 무시. 하단 HUD·캐릭터 관리 창과 충돌 방지")]
        [SerializeField] bool ignoreDragOverUI = true;

        [Header("관성")]
        [Tooltip("손을 뗀 뒤 미끄러지는 느낌. 0 이면 즉시 정지")]
        [Range(0f, 20f)] [SerializeField] float inertiaDamping = 6f;

        [Tooltip("관성 최대 속도 (월드 유닛/초). 0 이면 관성 없음")]
        [Min(0f)] [SerializeField] float inertiaMaxSpeed = 40f;

        [Header("키보드 패닝")]
        [SerializeField] bool keyboardPanEnabled = true;
        [Min(0f)] [SerializeField] float keyboardPanSpeed = 32f;

        [Header("가장자리 패닝 (기본 비활성)")]
        [Tooltip("UI 패널과 충돌하므로 권장하지 않음")]
        [SerializeField] bool edgePanEnabled = false;
        [Min(1f)] [SerializeField] float edgeThickness = 24f;
        [Min(0f)] [SerializeField] float edgePanSpeed = 28f;

        [Header("줌 — 보이는 세로 타일 수로 지정")]
        [Tooltip("가장 확대했을 때 화면에 보이는 세로 타일 수")]
        [Min(2f)] [SerializeField] float minViewTiles = 11f;

        [Tooltip("가장 축소했을 때 화면에 보이는 세로 타일 수. " +
                 "시야가 맵보다 커지면 패닝이 불가능해지므로 맵 크기로 한 번 더 제한된다")]
        [Min(4f)] [SerializeField] float maxViewTiles = 31f;

        [Tooltip("픽셀 퍼펙트 한계(텍스처 1px = 화면 1px)를 넘어서까지 축소할 수 있게 한다.\n" +
                 "이 구간에서는 도트가 축소 샘플링되어 다소 거칠어지는 대신 넓은 전장을 볼 수 있다.\n" +
                 "축소 배율도 정수(2배, 3배...)로만 움직여 최대한 깔끔하게 유지한다")]
        [SerializeField] bool allowZoomOutBeyondPixelPerfect = true;

        [Tooltip("Pixel Perfect 가 없을 때만 사용하는 연속 줌 증분")]
        [Min(0f)] [SerializeField] float zoomStep = 1.6f;

        [Min(0f)] [SerializeField] float zoomSmoothing = 12f;

        [Header("이동 감쇠")]
        [Tooltip("키보드/가장자리 패닝의 보간 속도. 드래그는 감쇠 없이 즉시 반영된다")]
        [Min(0f)] [SerializeField] float panSmoothing = 18f;

        public enum DragButton { Left, Middle, Right, LeftOrMiddle, None }

        Camera _outputCamera;
        CinemachinePositionComposer _composer;
        PixelPerfectCamera _pixelPerfectCamera;
        bool _steppedZoom;      // 픽셀 퍼펙트가 걸려 있으면 정수 배율 단위로만 줌

        // 줌 단계 인덱스 s.  s=1 이 텍스처 1px = 화면 1px (픽셀 퍼펙트 기준선)
        //   s >= 1 → ortho = Base1x / s        확대 방향, 정수 업스케일 = 도트 선명
        //   s <= 0 → ortho = Base1x * (2 - s)  축소 방향, 정수 다운샘플 = 다소 거칠어짐
        int _zoomStepIndex = 3;

        Vector3 _targetPosition;
        float _targetZoom;

        bool _dragPending;      // 버튼은 눌렸지만 아직 임계값 미달
        bool _dragging;         // 임계값을 넘어 실제 드래그 중
        Vector2 _dragOrigin;
        Vector2 _dragPrevMouse;
        Vector2 _inertia;

        // ------------------------------------------------------------------

        void Awake()
        {
            _targetPosition = transform.position;

            if (cineCamera != null)
            {
                _composer = cineCamera.GetComponent<CinemachinePositionComposer>();
                _targetZoom = cineCamera.Lens.OrthographicSize;
            }
            else
            {
                Debug.LogError("[CameraRig] Cine Camera 가 연결되지 않아 줌이 동작하지 않습니다. " +
                               "CameraAnchor 인스펙터에 CM_Camera 를 넣어주세요.", this);
                _targetZoom = 11.25f;
            }

            _outputCamera = Camera.main;
            if (_outputCamera == null)
                Debug.LogWarning("[CameraRig] MainCamera 태그가 붙은 카메라를 찾지 못했습니다.", this);
            else
                _pixelPerfectCamera = _outputCamera.GetComponent<PixelPerfectCamera>();

            // CinemachinePixelPerfect 가 붙어 있으면 실제 표시 크기가 기준크기/정수N 으로
            // 스냅된다. 그 경우 연속값을 넣으면 스크롤 여러 칸이 같은 N 으로 반올림되어
            // 아무 변화도 없으므로, N 자체를 단계로 움직여야 한다.
            _steppedZoom = _pixelPerfectCamera != null
                        && cineCamera != null
                        && cineCamera.GetComponent<CinemachinePixelPerfect>() != null;

            if (_steppedZoom)
            {
                _zoomStepIndex = StepFromOrtho(_targetZoom);
                _targetZoom = OrthoFromStep(_zoomStepIndex);
                UpdatePixelPerfectState();
            }
        }

        void Update()
        {
            if (!Application.isFocused)
            {
                CancelDrag();
                ApplySmoothing();
                return;
            }

            float dt = Time.unscaledDeltaTime;   // 일시정지/배속과 무관하게 카메라는 항상 반응

            HandleZoomInput();
            HandleDragPan(dt);

            if (!_dragging)
            {
                ApplyInertia(dt);
                HandleKeyboardPan(dt);
                HandleEdgePan(dt);
            }
            HandleRecenter();

            ClampTargetToBounds();
            ApplySmoothing();
        }

        // ------------------------------------------------------------------ 드래그

        void HandleDragPan(float dt)
        {
            var mouse = Mouse.current;
            if (mouse == null || dragButton == DragButton.None) return;

            bool pressed = IsDragButtonPressed(mouse, out bool wasPressed, out bool wasReleased);
            Vector2 pos = mouse.position.ReadValue();

            if (wasPressed)
            {
                // UI 위에서 시작한 드래그는 카메라가 가져가지 않는다.
                if (ignoreDragOverUI && IsPointerOverUI()) return;

                _dragPending = true;
                _dragging = false;
                _dragOrigin = pos;
                _dragPrevMouse = pos;
                _inertia = Vector2.zero;
                return;
            }

            if (wasReleased || !pressed)
            {
                // 손을 뗄 때의 속도를 관성으로 넘긴다.
                _dragPending = false;
                _dragging = false;
                return;
            }

            if (!_dragPending && !_dragging) return;

            // 임계값을 넘기 전에는 클릭일 수 있으므로 카메라를 움직이지 않는다.
            if (!_dragging && (pos - _dragOrigin).magnitude < dragThresholdPixels) return;
            _dragging = true;

            Vector2 deltaPx = pos - _dragPrevMouse;
            _dragPrevMouse = pos;
            if (deltaPx.sqrMagnitude <= 0f) { _inertia = Vector2.zero; return; }

            // 화면 픽셀 → 월드 유닛. 현재 줌 기준으로 계산해야 마우스에 1:1 로 붙는다.
            float worldPerPixel = (2f * CurrentZoom) / Mathf.Max(1, Screen.height);
            Vector2 worldDelta = deltaPx * worldPerPixel;
            if (dragGrabsWorld) worldDelta = -worldDelta;

            // 드래그는 감쇠 없이 즉시 반영 — 손에 붙는 느낌을 위해
            _targetPosition += (Vector3)worldDelta;
            transform.position = _targetPosition;

            if (inertiaMaxSpeed > 0f && dt > 0f)
                _inertia = Vector2.ClampMagnitude(worldDelta / dt, inertiaMaxSpeed);
        }

        bool IsDragButtonPressed(Mouse mouse, out bool wasPressed, out bool wasReleased)
        {
            switch (dragButton)
            {
                case DragButton.Left:
                    return Read(mouse.leftButton, out wasPressed, out wasReleased);
                case DragButton.Middle:
                    return Read(mouse.middleButton, out wasPressed, out wasReleased);
                case DragButton.Right:
                    return Read(mouse.rightButton, out wasPressed, out wasReleased);
                case DragButton.LeftOrMiddle:
                {
                    bool lp = Read(mouse.leftButton, out bool lPress, out bool lRel);
                    bool mp = Read(mouse.middleButton, out bool mPress, out bool mRel);
                    wasPressed = lPress || mPress;
                    wasReleased = (lRel || mRel) && !(lp || mp);
                    return lp || mp;
                }
                default:
                    wasPressed = wasReleased = false;
                    return false;
            }

            static bool Read(ButtonControl b, out bool press, out bool release)
            {
                press = b.wasPressedThisFrame;
                release = b.wasReleasedThisFrame;
                return b.isPressed;
            }
        }

        void CancelDrag()
        {
            _dragPending = false;
            _dragging = false;
            _inertia = Vector2.zero;
        }

        void ApplyInertia(float dt)
        {
            if (_inertia.sqrMagnitude <= 0.0001f) { _inertia = Vector2.zero; return; }

            _targetPosition += (Vector3)(_inertia * dt);
            _inertia *= Mathf.Exp(-inertiaDamping * dt);
        }

        static bool IsPointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------ 그 외 입력

        void HandleZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float raw = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(raw, 0f)) return;

            // 플랫폼에 따라 한 칸이 120 또는 1 로 들어온다. 둘 다 처리.
            float notches = raw / 120f;
            if (Mathf.Abs(notches) < 0.01f) notches = Mathf.Sign(raw);

            if (_steppedZoom)
            {
                // 정수 배율을 직접 움직인다 → 스크롤 한 칸이 반드시 한 단계 변화
                int step = Mathf.RoundToInt(Mathf.Sign(notches) * Mathf.Max(1f, Mathf.Abs(notches)));
                _zoomStepIndex = Mathf.Clamp(_zoomStepIndex + step, MinStepIndex, MaxStepIndex);
                _targetZoom = OrthoFromStep(_zoomStepIndex);
                UpdatePixelPerfectState();
            }
            else
            {
                _targetZoom = Mathf.Clamp(_targetZoom - notches * zoomStep, MinOrtho, MaxOrtho);
            }
        }

        /// <summary>
        /// 픽셀 퍼펙트 기준선보다 축소한 구간에서는 PixelPerfectCamera 를 끈다.
        ///
        /// PPC 는 CorrectCinemachineOrthoSize() 가 호출되면 Cinemachine 호환 모드로 들어가
        /// orthographicSize 를 건드리지 않지만, 그 보정 함수가 N >= 1 로 하드 클램프하기
        /// 때문에 기준선보다 축소하는 것이 원천적으로 불가능하다. 그 구간만 PPC 를 비활성화해
        /// Cinemachine 이 크기를 온전히 결정하게 한다.
        /// </summary>
        void UpdatePixelPerfectState()
        {
            if (_pixelPerfectCamera == null) return;

            bool wantPixelPerfect = _zoomStepIndex >= 1;
            if (_pixelPerfectCamera.enabled != wantPixelPerfect)
                _pixelPerfectCamera.enabled = wantPixelPerfect;
        }

        // ---- 줌 범위 계산 -------------------------------------------------

        /// <summary>가장 확대했을 때의 orthographicSize.</summary>
        float MinOrtho => minViewTiles * 0.5f;

        /// <summary>
        /// 가장 축소했을 때의 orthographicSize.
        /// 시야가 맵보다 커지면 경계 제한이 성립하지 않아 카메라가 중앙에 못 박히므로,
        /// 맵 안에 들어오는 크기로 한 번 더 잘라낸다.
        /// </summary>
        float MaxOrtho
        {
            get
            {
                float o = maxViewTiles * 0.5f;

                if (boundsShape != null)
                {
                    Bounds b = boundsShape.bounds;
                    float aspect = CurrentAspect;
                    o = Mathf.Min(o, b.size.y * 0.5f, b.size.x * 0.5f / Mathf.Max(0.01f, aspect));
                }
                return Mathf.Max(MinOrtho, o);
            }
        }

        float CurrentAspect => _outputCamera != null
            ? _outputCamera.aspect
            : (float)Screen.width / Mathf.Max(1, Screen.height);

        /// <summary>
        /// 픽셀 퍼펙트 1배(N=1) 기준 orthographicSize = 출력 세로픽셀 / (2 × PPU).
        /// 게임뷰 크기가 바뀌면 값도 바뀌므로 매번 계산한다.
        /// </summary>
        float Base1xOrtho
        {
            get
            {
                if (_pixelPerfectCamera == null || _outputCamera == null) return MaxOrtho;
                int ppu = Mathf.Max(1, _pixelPerfectCamera.assetsPPU);
                return Mathf.Max(0.01f, _outputCamera.pixelHeight / (2f * ppu));
            }
        }

        /// <summary>단계 인덱스 → orthographicSize. s 가 작아질수록 축소된다.</summary>
        float OrthoFromStep(int s) =>
            s >= 1 ? Base1xOrtho / s
                   : Base1xOrtho * (2 - s);

        /// <summary>가장 축소된 단계. 옵션이 꺼져 있으면 픽셀 퍼펙트 기준선(s=1)이 하한.</summary>
        int MinStepIndex
        {
            get
            {
                if (!allowZoomOutBeyondPixelPerfect) return 1;

                // Base1x * (2 - s) <= MaxOrtho  →  s >= 2 - MaxOrtho / Base1x
                float s = 2f - MaxOrtho / Mathf.Max(0.01f, Base1xOrtho);
                return Mathf.Min(1, Mathf.CeilToInt(s));
            }
        }

        /// <summary>가장 확대된 단계.</summary>
        int MaxStepIndex => Mathf.Max(MinStepIndex,
            Mathf.Max(1, Mathf.FloorToInt(Base1xOrtho / Mathf.Max(0.01f, MinOrtho))));

        int StepFromOrtho(float ortho)
        {
            float b = Base1xOrtho;
            int s = ortho <= b
                ? Mathf.Max(1, Mathf.RoundToInt(b / Mathf.Max(0.01f, ortho)))   // 확대 구간
                : 2 - Mathf.RoundToInt(ortho / b);                              // 축소 구간
            return Mathf.Clamp(s, MinStepIndex, MaxStepIndex);
        }

        void HandleKeyboardPan(float dt)
        {
            if (!keyboardPanEnabled) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dir.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dir.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;

            if (dir.sqrMagnitude > 0f)
            {
                _targetPosition += (Vector3)(dir.normalized * keyboardPanSpeed * dt);
                _inertia = Vector2.zero;
            }
        }

        void HandleEdgePan(float dt)
        {
            if (!edgePanEnabled) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 m = mouse.position.ReadValue();
            if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return;

            Vector2 dir = Vector2.zero;
            if (m.x <= edgeThickness)                       dir.x = -Ramp(m.x);
            else if (m.x >= Screen.width - edgeThickness)    dir.x =  Ramp(Screen.width - m.x);
            if (m.y <= edgeThickness)                       dir.y = -Ramp(m.y);
            else if (m.y >= Screen.height - edgeThickness)   dir.y =  Ramp(Screen.height - m.y);

            if (dir.sqrMagnitude > 0f) _targetPosition += (Vector3)(dir * edgePanSpeed * dt);

            float Ramp(float distFromEdge) => Mathf.Clamp01(1f - distFromEdge / edgeThickness);
        }

        void HandleRecenter()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                _inertia = Vector2.zero;
                FocusOn(Vector3.zero);
            }
        }

        // ------------------------------------------------------------------ 적용

        float CurrentZoom => cineCamera != null ? cineCamera.Lens.OrthographicSize : _targetZoom;

        void ApplySmoothing()
        {
            float dt = Time.unscaledDeltaTime;

            // 드래그 중에는 HandleDragPan 이 직접 위치를 맞췄으므로 보간을 건너뛴다.
            if (!_dragging)
            {
                transform.position = panSmoothing > 0f
                    ? Vector3.Lerp(transform.position, _targetPosition, 1f - Mathf.Exp(-panSmoothing * dt))
                    : _targetPosition;
            }

            if (cineCamera == null) return;

            LensSettings lens = cineCamera.Lens;
            lens.OrthographicSize = zoomSmoothing > 0f
                ? Mathf.Lerp(lens.OrthographicSize, _targetZoom, 1f - Mathf.Exp(-zoomSmoothing * dt))
                : _targetZoom;
            cineCamera.Lens = lens;
        }

        /// <summary>
        /// 카메라 시야가 맵 밖으로 나가지 않도록 목표 위치를 제한한다.
        /// Confiner2D 가 카메라를 최종적으로 붙잡지만, 앵커까지 막지 않으면
        /// 앵커만 맵 밖으로 멀어져서 "드래그해도 화면이 안 움직이는" 구간이 생긴다.
        /// </summary>
        void ClampTargetToBounds()
        {
            if (boundsShape == null) return;

            Bounds b = boundsShape.bounds;
            float halfH = CurrentZoom;
            float halfW = halfH * CurrentAspect;

            // PositionComposer 의 ScreenPosition.y 만큼 카메라가 앵커보다 위에 놓인다.
            float offsetY = _composer != null ? 2f * halfH * _composer.Composition.ScreenPosition.y : 0f;

            Vector3 before = _targetPosition;

            _targetPosition.x = ClampAxis(_targetPosition.x, b.min.x + halfW, b.max.x - halfW, b.center.x);
            _targetPosition.y = ClampAxis(_targetPosition.y,
                                          b.min.y + halfH - offsetY,
                                          b.max.y - halfH - offsetY,
                                          b.center.y - offsetY);

            // 경계에 부딪히면 관성을 죽여 벽에 비비는 느낌을 없앤다.
            if (!Mathf.Approximately(before.x, _targetPosition.x)) _inertia.x = 0f;
            if (!Mathf.Approximately(before.y, _targetPosition.y)) _inertia.y = 0f;

            if (_dragging) transform.position = _targetPosition;

            // 맵이 화면보다 작으면 가둘 수 없으니 중앙에 고정한다.
            static float ClampAxis(float v, float min, float max, float fallback) =>
                min > max ? fallback : Mathf.Clamp(v, min, max);
        }

        // ------------------------------------------------------------------ 공개 API

        /// <summary>지정 월드 위치로 카메라를 이동시킨다. (미니맵 클릭, 캐릭터 선택 등)</summary>
        public void FocusOn(Vector3 worldPosition)
        {
            _targetPosition = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        }

        /// <summary>감쇠 없이 즉시 이동. 씬 시작이나 컷 전환용.</summary>
        public void SnapTo(Vector3 worldPosition)
        {
            FocusOn(worldPosition);
            ClampTargetToBounds();
            transform.position = _targetPosition;
        }

        /// <summary>줌 목표를 직접 설정한다. 픽셀 퍼펙트일 때는 가장 가까운 유효 배율로 스냅된다.</summary>
        public void SetZoom(float orthographicSize)
        {
            float clamped = Mathf.Clamp(orthographicSize, MinOrtho, MaxOrtho);
            if (_steppedZoom)
            {
                _zoomStepIndex = StepFromOrtho(clamped);
                _targetZoom = OrthoFromStep(_zoomStepIndex);
                UpdatePixelPerfectState();
            }
            else _targetZoom = clamped;
        }

        /// <summary>보이는 세로 타일 수로 줌을 설정한다.</summary>
        public void SetZoomByViewTiles(float verticalTiles) => SetZoom(verticalTiles * 0.5f);

        /// <summary>현재 화면에 보이는 세로 타일 수.</summary>
        public float CurrentViewTiles => CurrentZoom * 2f;

        /// <summary>드래그가 진행 중인지. 클릭 판정을 하는 다른 시스템이 참고할 수 있다.</summary>
        public bool IsDragging => _dragging;

        void OnValidate()
        {
            if (maxViewTiles < minViewTiles) maxViewTiles = minViewTiles;
        }
    }
}
