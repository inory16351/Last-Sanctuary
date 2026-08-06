using UnityEngine;
using UnityEngine.EventSystems;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 가로 막대를 눌러/끌어서 0~1 값을 고르게 해주는 최소 구현.
    ///
    /// <b>왜 <c>Slider</c> 를 안 쓰는가</b> — 유니티 <c>Slider</c> 는 <c>fillRect</c>/<c>handleRect</c>/
    /// <c>targetGraphic</c> 이 전부 <b>오브젝트 참조</b>라 MCP 로 씬에 값을 넣을 수 없다
    /// (진행상황 8절 4번). 이 컴포넌트는 자기 <c>RectTransform</c> 안에서의 마우스 위치만 보므로
    /// 참조가 하나도 필요 없고, MCP 로 붙이기만 하면 바로 동작한다.
    ///
    /// 값을 실제로 어디에 반영할지는 <see cref="OnValueChanged"/> 를 구독하는 쪽이 정한다
    /// (전술 지침 창의 후퇴 판단 기준 → 1% 단위).
    ///
    /// ⚠️ 이 오브젝트의 <c>Graphic.raycastTarget</c> 이 켜져 있어야 포인터 이벤트가 들어온다.
    /// </summary>
    public class UiDragBar : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        /// <summary>막대 왼쪽 끝 0, 오른쪽 끝 1. 누르는 순간과 끄는 동안 계속 발생한다.</summary>
        public event System.Action<float> OnValueChanged;

        public void OnPointerDown(PointerEventData eventData) => Apply(eventData);

        public void OnDrag(PointerEventData eventData) => Apply(eventData);

        void Apply(PointerEventData eventData)
        {
            var rect = transform as RectTransform;
            if (rect == null || rect.rect.width <= 0f) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            // rect.xMin 은 피벗 기준 좌측 끝이다 — 피벗이 어디든 이 식이 그대로 성립한다.
            float t = Mathf.Clamp01((local.x - rect.rect.xMin) / rect.rect.width);
            OnValueChanged?.Invoke(t);
        }
    }
}
