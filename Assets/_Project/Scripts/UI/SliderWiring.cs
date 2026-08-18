using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 유니티 <see cref="Slider"/> 의 <b>오브젝트 참조 세 칸</b>을 코드가 꽂아준다.
    ///
    /// ★★ <b>왜 필요한가</b> — <c>fillRect</c> · <c>handleRect</c> · <c>targetGraphic</c> 은 전부
    /// <b>씬 오브젝트 참조</b>라 MCP 로 인스펙터에 넣을 수 없다(진행상황 8절 4번). 그래서
    /// 이 프로젝트는 <b>구조는 MCP 로 만들고 참조는 코드가 이름으로 찾아 꽂는다</b> —
    /// 그 마지막 한 걸음이 이 클래스다. "MCP 로 못 한다"가 "만들 수 없다"는 아니었다.
    ///
    /// <b>왜 따로 뒀나</b> — 음량(<see cref="VolumeSlider"/>)과 전술 지침의 후퇴 기준
    /// (<see cref="TacticalOrderPanel"/>) 이 <b>같은 배선</b>을 한다. 두 벌 적으면
    /// 한쪽만 고치는 날이 온다(준수사항 §10 H-3).
    /// </summary>
    public static class SliderWiring
    {
        /// <summary>
        /// 비어 있는 참조만 꽂는다.
        ///
        /// ⚠ <c>fillRect</c>/<c>handleRect</c> 를 대입하면 유니티가 그 <see cref="RectTransform"/> 의
        /// 앵커를 <b>스스로 몰기 시작한다</b>(driven). 실행 중에 씬에서 잡아둔 앵커 값이 바뀌어
        /// 보이는 것이 정상이다 — 손으로 다시 맞추려 들면 안 된다.
        ///
        /// ⚠ <b>이미 꽂혀 있으면 건드리지 않는다</b> — 나중에 프리팹으로 승격해 인스펙터에
        /// 제대로 배선하면 그쪽이 정본이어야 한다.
        /// </summary>
        public static void Wire(Slider slider, RectTransform fill, RectTransform handle)
        {
            if (slider == null) return;

            if (slider.fillRect == null && fill != null) slider.fillRect = fill;
            if (slider.handleRect == null && handle != null) slider.handleRect = handle;

            // 손잡이가 눌린 상태를 보여줄 그래픽. 없으면 클릭해도 색이 안 변할 뿐 동작은 한다.
            if (slider.targetGraphic == null && slider.handleRect != null)
            {
                var graphic = slider.handleRect.GetComponent<Graphic>();
                if (graphic != null) slider.targetGraphic = graphic;
            }
        }
    }
}
