using UnityEngine;
using UnityEngine.EventSystems;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>창을 눌러 끌어 옮긴다</b> (유저 지시 2026-08-18:
    /// <i>"부대설정 / 전술지침 / 캐릭터 성장 / 토벌 지시 ui들 클릭해서 드래그로 옮길 수 있게"</i>).
    ///
    /// <b>왜 창 본체에 붙이나 — 잡을 손잡이(제목 표시줄)가 없다.</b>
    /// 네 창의 <c>Header</c> 는 <see cref="UnityEngine.UI.Image"/> 가 없어서
    /// 레이캐스트를 받지 못한다(글자만 있다). 손잡이용 오브젝트를 새로 만들 수도 있지만,
    /// 창 본체에는 이미 배경 <c>Image</c> 가 있고 <c>raycastTarget</c> 도 켜져 있다 —
    /// <b>빈 배경 아무 데나 잡아 끌면</b> 되는 편이 손잡이를 찾는 것보다 쓰기 쉽다.
    ///
    /// ★ <b>버튼·슬라이더를 방해하지 않는다.</b> 유니티의 드래그 이벤트는 <b>가장 안쪽에서
    /// 처리기를 가진 오브젝트</b>가 가져간다 — 버튼 위에서 시작한 드래그는 버튼이,
    /// 후퇴 기준 슬라이더 위에서 시작한 드래그는 그 <c>Slider</c> 가 먹는다.
    /// 그래서 이 컴포넌트는 <b>아무 위젯도 없는 배경</b>에서 시작한 드래그만 받는다.
    ///
    /// ⚠ <b>창을 화면 밖으로 완전히 내보내지 않는다.</b> 한 번 놓치면 다시 잡을 수 없으므로
    /// <see cref="keepVisibleMargin"/> 만큼은 항상 화면 안에 남긴다.
    ///
    /// ⚠ <b>좌표는 부모 기준으로 계산한다</b>(<c>ScreenPointToLocalPointInRectangle</c>) —
    /// 스크린 좌표 델타를 그대로 더하면 <c>CanvasScaler</c> 의 해상도 보정(1920x1080 기준)이
    /// 무시되어, 창이 <b>마우스보다 느리게/빠르게</b> 따라온다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [Tooltip("끌어 옮길 대상. 비우면 이 오브젝트 자신이다 — " +
                 "손잡이를 따로 만들면 그때 창 본체를 여기 넣는다")]
        [SerializeField] RectTransform target;

        [Tooltip("창이 화면 밖으로 나가도 최소한 이만큼(px)은 남긴다. " +
                 "0 이면 완전히 내보낼 수 있어 다시 잡지 못하게 된다")]
        [Min(0f)] [SerializeField] float keepVisibleMargin = 80f;

        [Tooltip("드래그를 시작할 때 이 창을 <b>맨 앞</b>으로 올린다 " +
                 "(형제 순서 마지막 = 가장 위에 그려진다)")]
        [SerializeField] bool bringToFrontOnDrag = true;

        RectTransform _self;
        RectTransform _parent;

        /// <summary>누른 지점과 창 원점의 차이. 이걸 유지해야 창이 마우스에 "붙어" 따라온다.</summary>
        Vector2 _grabOffset;

        RectTransform Target
        {
            get
            {
                if (target != null) return target;
                if (_self == null) _self = transform as RectTransform;
                return _self;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            RectTransform t = Target;
            if (t == null) return;

            _parent = t.parent as RectTransform;
            if (_parent == null) return;

            if (bringToFrontOnDrag) t.SetAsLastSibling();

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            _grabOffset = t.anchoredPosition - local;
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransform t = Target;
            if (t == null || _parent == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            t.anchoredPosition = Clamp(t, local + _grabOffset);
        }

        /// <summary>
        /// 창이 화면 밖으로 완전히 빠지지 않게 자른다.
        ///
        /// 창의 네 모서리를 부모 좌표로 구해 <b>실제로 차지하는 사각형</b>을 얻는다 —
        /// 앵커·피벗이 창마다 달라서(가운데 고정인 것도, 좌상단 고정인 것도 있다)
        /// <c>anchoredPosition</c> 만으로는 어디에 있는지 알 수 없다.
        /// </summary>
        Vector2 Clamp(RectTransform t, Vector2 wanted)
        {
            Rect parentRect = _parent.rect;
            Rect selfRect = t.rect;

            // 지금 위치에서 창의 좌하단이 부모 좌표 어디인지 → wanted 로 옮겼을 때의 사각형
            Vector2 delta = wanted - t.anchoredPosition;
            Vector3[] corners = new Vector3[4];
            t.GetLocalCorners(corners);

            // 부모 좌표계에서의 좌하단·우상단
            Vector2 min = (Vector2)_parent.InverseTransformPoint(t.TransformPoint(corners[0])) + delta;
            Vector2 max = min + new Vector2(selfRect.width, selfRect.height);

            float m = keepVisibleMargin;
            Vector2 fix = Vector2.zero;

            if (max.x < parentRect.xMin + m) fix.x = parentRect.xMin + m - max.x;
            else if (min.x > parentRect.xMax - m) fix.x = parentRect.xMax - m - min.x;

            if (max.y < parentRect.yMin + m) fix.y = parentRect.yMin + m - max.y;
            else if (min.y > parentRect.yMax - m) fix.y = parentRect.yMax - m - min.y;

            return wanted + fix;
        }
    }
}
