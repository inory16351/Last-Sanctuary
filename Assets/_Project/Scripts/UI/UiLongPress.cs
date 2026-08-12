using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>꾹 누르기(long-press) 판정.</b> <c>Button</c> 과 <b>같은 오브젝트</b>에 붙여 쓴다 —
    /// 짧게 누르면 Button 의 <c>onClick</c> 이 그대로 돌고, <see cref="holdSeconds"/> 를 넘겨
    /// 누르고 있으면 <see cref="OnLongPress"/> 가 <b>한 번</b> 발생한다.
    ///
    /// <b>클릭과의 충돌을 어떻게 피하는가</b> — 유니티는 손을 뗄 때 <c>pointerUp</c> → <c>pointerClick</c>
    /// 순서로 이벤트를 보내므로, 이 컴포넌트가 <b>먼저</b> 처리된다. 꾹 누르기가 발동했으면
    /// <see cref="ConsumedThisPress"/> 를 켜 두고, <c>onClick</c> 을 받는 쪽이 그 값을 보고
    /// 자기 처리를 건너뛴다(플래그는 <b>다음 누름</b>에서 초기화되므로 그사이 클릭까지 안전하게 덮는다).
    /// Button 을 잠깐 <c>interactable = false</c> 로 바꾸는 편법보다 부작용이 없다.
    ///
    /// <b>왜 <c>Update</c> 로 재는가</b> — <c>EventSystem</c> 에는 "누른 채 유지 중" 이벤트가 없다.
    /// 누른 시각만 기록해 두고 매 프레임 경과를 보면, 손을 떼지 않아도 정해진 시간에 정확히 발동한다.
    /// 시간은 <c>unscaledTime</c> 으로 잰다 — UI 조작은 게임 정지(패배 화면 등)와 무관해야 한다.
    ///
    /// ⚠️ <b>드래그하면 취소된다</b>(<see cref="cancelDragPixels"/>) — 스크롤 목록 안의 행에 붙는
    /// 컴포넌트라, 스크롤하려고 끌었을 뿐인데 창이 열리면 조작이 망가진다.
    /// </summary>
    public class UiLongPress : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("이 시간 동안 누르고 있으면 꾹 누르기로 판정한다(초). 짧으면 평소 클릭이 " +
                 "자꾸 꾹 누르기로 새고, 길면 답답하다")]
        [Min(0.1f)] [SerializeField] float holdSeconds = 0.5f;

        [Tooltip("누른 뒤 이 픽셀보다 많이 움직이면 꾹 누르기 후보에서 뺀다. " +
                 "스크롤하려고 끈 것을 꾹 누르기로 잡지 않기 위한 값")]
        [Min(1f)] [SerializeField] float cancelDragPixels = 12f;

        /// <summary>꾹 누르기가 성립한 순간. 손을 떼기 전에 발생한다.</summary>
        public event System.Action OnLongPress;

        /// <summary>
        /// 이번 누름이 꾹 누르기로 처리됐는지. <c>onClick</c> 쪽에서 이 값을 보고 자기 처리를
        /// 건너뛴다. <b>다음 <see cref="OnPointerDown"/> 에서 꺼진다</b>.
        /// </summary>
        public bool ConsumedThisPress { get; private set; }

        bool _pressed;
        float _pressTime;
        Vector2 _pressPosition;

        void OnDisable() => Reset();

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            ConsumedThisPress = false;
            _pressTime = Time.unscaledTime;
            _pressPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;

        /// <summary>커서가 행 밖으로 나가면 후보에서 뺀다 — 누른 곳에서 손을 떼야 성립한다.</summary>
        public void OnPointerExit(PointerEventData eventData) => _pressed = false;

        void Update()
        {
            if (!_pressed) return;

            // 끌었으면 꾹 누르기가 아니다(스크롤로 해석한다).
            //
            // ⚠️ <c>IDragHandler</c> 를 구현하면 안 된다 — 이 행은 <c>ScrollRect</c> 안에 있고,
            //    드래그 이벤트는 계층에서 처음 받는 컴포넌트가 가져가므로 스크롤이 죽는다.
            //    그래서 이벤트 대신 포인터 위치를 직접 읽는다.
            //    <c>Pointer</c> 는 마우스·터치·펜의 공통 상위 장치라 셋 다 이 한 줄로 커버된다.
            Vector2 now = Pointer.current != null
                ? Pointer.current.position.ReadValue()
                : _pressPosition;
            if ((now - _pressPosition).sqrMagnitude > cancelDragPixels * cancelDragPixels)
            {
                _pressed = false;
                return;
            }

            if (Time.unscaledTime - _pressTime < holdSeconds) return;

            _pressed = false;                 // 한 번만 발동한다
            ConsumedThisPress = true;
            OnLongPress?.Invoke();
        }

        void Reset()
        {
            _pressed = false;
            ConsumedThisPress = false;
        }
    }
}
