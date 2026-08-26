using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>버튼 글자가 칸에 꽉 차 가리는 것을 한 곳에서 쓸어 담는다</b>
    /// (2026-08-26 · 유저 리포트: *"버튼에 텍스트가 너무 꽉차게 들어가서 가리는 문제가
    /// 가끔 있으니 수정 좀"*).
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  왜 «가끔» 인가 — <b>고치는 도구는 있었고 부르는 곳이 없었다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// <see cref="HudTheme.FitText"/> 는 2026-08-24 에 이미 만들어져 있었다. 그런데 그것은
    /// <b>부르는 쪽이 기억할 때만</b> 적용된다 — 실측하면 성장 창의 유물 띠 · 로스터 이름 ·
    /// 도움말 카드 <b>몇 군데뿐</b>이고, <b>버튼 라벨은 한 번도 지나가지 않았다</b>.
    /// 그래서 «어떤 버튼은 멀쩡하고 어떤 버튼은 넘친다» 가 됐다. 그것이 «가끔» 의 정체다.
    ///
    /// ★ <b>그래서 «부르는 곳» 을 없앴다</b> — 이 컴포넌트가 <c>UI_Root</c> 아래 <b>모든</b>
    ///   <see cref="Button"/> 을 한 번 훑어 라벨을 손본다. 새 창이 생겨도, 버튼이 늘어도
    ///   여기도 그쪽도 안 고쳐도 된다(<c>HudTheme.PaintButton</c> 이 «계열을 그림이 말해
    ///   준다» 로 얻은 것과 같은 성질이다).
    ///
    /// <b>무엇을 하나</b> — 버튼마다
    /// <list type="number">
    ///   <item>라벨에 <b>좌우 여백</b>을 준다 — 글자가 버튼 테두리에 닿아 있으면 안 잘려도
    ///         «꽉 찬» 것으로 보인다. 여백은 <b>버튼 너비에 비례</b>한다(작은 배속 버튼에
    ///         고정 8px 을 주면 그쪽이 되레 뭉개진다).</item>
    ///   <item><see cref="HudTheme.FitText"/> 로 <b>자동 크기</b>를 켠다 — 지금 크기가 최대,
    ///         <see cref="minScale"/> 배가 최소. <b>들어가는 글자는 그대로 두고</b> 넘치는
    ///         것만 줄어든다.</item>
    ///   <item>줄바꿈을 <b>끈다</b> — 버튼은 대개 한 줄 높이라, 줄바꿈이 켜져 있으면 둘째 줄이
    ///         칸 아래로 흘러 «글자가 반만 보이는» 그 모습이 된다. 이것이 실제로 가장 흔한
    ///         원인이다(<c>HudTheme.FitText</c> 의 ⚠ 가 가리키는 바로 그 함정).</item>
    /// </list>
    ///
    /// ⚠ <b>비활성 오브젝트까지 훑는다</b>(<c>GetComponentsInChildren(true)</c>) — 창은 대개
    ///   꺼진 채로 씬에 있고, 목록의 <b>모체</b>도 꺼져 있다. 모체를 고쳐 두면 런타임에
    ///   복제되는 줄들이 <b>고쳐진 채로</b> 태어난다(그래서 복제까지 따라다닐 필요가 없다).
    /// ⚠ <b>스스로 칸 크기를 정하는 라벨은 건드리지 않는다</b> — <see cref="ContentSizeFitter"/>
    ///   가 붙어 있으면 «칸이 글자를 따라가는» 쪽이라, 여기서 글자를 줄이면 칸이 같이 줄어
    ///   레이아웃이 흔들린다.
    /// ⚠ <b>Start 에서 돈다</b> — <c>Awake</c> 면 창들이 아직 자기 라벨을 만들기 전일 수 있다
    ///   (런타임에 UI 를 짓는 패널이 여럿이다). 88-1절의 «창이 열리는 순간 스스로 닫혔다» 와
    ///   같은 «누가 먼저 깨어나는가» 문제라 <b>한 박자 늦게</b> 잡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiButtonLabelFit : MonoBehaviour
    {
        [Header("여백")]
        [Tooltip("버튼 너비의 몇 배를 좌우 여백으로 줄지")]
        [Range(0f, 0.2f)] [SerializeField] float paddingRatio = 0.08f;

        [Tooltip("여백의 최소·최대 픽셀")]
        [Min(0f)] [SerializeField] float paddingMin = 3f;
        [Min(0f)] [SerializeField] float paddingMax = 12f;

        [Header("자동 크기")]
        [Tooltip("여기까지만 줄인다 — 지금 글자 크기의 몇 배")]
        [Range(0.4f, 1f)] [SerializeField] float minScale = 0.62f;

        [Tooltip("이 크기 밑으로는 안 줄인다(픽셀). 너무 줄면 읽을 수 없다")]
        [Min(6f)] [SerializeField] float minSizeFloor = 10f;

        [Header("진단")]
        [Tooltip("몇 개를 손봤는지 로그로 알린다")]
        [SerializeField] bool logChanges = true;

        void Start()
        {
            int n = FitAll(transform);
            if (logChanges && n > 0)
                Debug.Log($"[UI] 버튼 라벨 {n}개의 넘침 규칙을 맞췄습니다.", this);
        }

        /// <summary>
        /// <paramref name="root"/> 아래 모든 버튼의 라벨을 손본다. 손본 개수를 돌려준다.
        /// ★ 런타임에 <b>새로 지은</b> 창이 있으면 그 창의 루트를 주고 다시 부르면 된다.
        /// </summary>
        public int FitAll(Transform root)
        {
            if (root == null) return 0;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            int changed = 0;
            for (int i = 0; i < buttons.Length; i++)
                if (Fit(buttons[i])) changed++;

            return changed;
        }

        /// <summary>버튼 하나. 손볼 것이 없으면 <c>false</c>.</summary>
        public bool Fit(Button button)
        {
            if (button == null) return false;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return false;

            // ⚠ 칸이 글자를 따라가는 라벨은 건드리지 않는다 — 줄이면 칸이 같이 줄어든다.
            if (label.GetComponent<ContentSizeFitter>() != null) return false;

            var rect = button.transform as RectTransform;
            float width = rect != null ? rect.rect.width : 0f;

            // ★ 여백은 버튼 너비에 비례한다 — 작은 버튼에 고정값을 주면 그쪽이 되레 뭉개진다.
            float pad = Mathf.Clamp(width * paddingRatio, paddingMin, paddingMax);

            Vector4 m = label.margin;
            label.margin = new Vector4(pad, m.y, pad, m.w);

            // ★ 줄바꿈을 끄고 자동 크기를 켠다 — 줄이는 것이지 자르는 것이 아니다.
            //   (자르면 «방어력이 8, 체력이 5 증가합니다» 의 뒤쪽 숫자가 사라진다 —
            //    HudTheme.FitText 가 Ellipsis 를 안 쓰는 그 이유 그대로다.)
            float size = label.fontSize > 0f ? label.fontSize : HudTheme.FontBody;
            float min = Mathf.Max(minSizeFloor, size * minScale);

            HudTheme.FitText(label, Mathf.Min(min, size), wrap: false);
            return true;
        }
    }
}
