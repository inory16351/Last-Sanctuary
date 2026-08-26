using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 침식 게이지 한 벌(막대 + 숫자)을 그리는 작은 뷰. 캐릭터 체력바가 나오는 <b>모든</b> 곳에
    /// 침식도 같이 나와야 한다는 요구(유저 확정)를 세 패널
    /// (<see cref="CharacterRosterPanel"/> · <see cref="TacticalOrderPanel"/> ·
    /// <see cref="CharacterGrowthPanel"/>)이 각자 구현하지 않도록 여기 한 곳에 모았다 —
    /// 색 규칙이나 문구를 고칠 때 세 군데를 따로 고치다 어긋나는 것을 막는 목적이다
    /// (<see cref="HpGhostBar"/> 를 세 패널이 공유하는 것과 같은 결).
    ///
    /// <b>하이라키 규약</b> — 어느 패널이든 아래 이름을 쓴다. MCP 로 직접 만든 오브젝트이고,
    /// 스크립트는 경로로만 찾는다(진행상황 8절 4번: MCP 로는 인스펙터 참조를 못 넣는다).
    /// <code>
    /// (부모)/ErosionBack        Image — 게이지 배경
    ///   ├─ ErosionFill          Image(Filled/Horizontal) — 채워지는 막대
    ///   └─ ErosionLabel         TMP_Text — "침식 42" 또는 "침식 42 · 혼란"
    /// </code>
    /// </summary>
    public class ErosionGaugeView
    {
        Image _fill;
        TMP_Text _label;
        GameObject _root;

        [System.NonSerialized] bool _bound;

        /// <summary>침식이 이 비율을 넘으면 막대 색이 경고 쪽(자홍)으로 완전히 넘어간다.</summary>
        const float HighRatio = 0.75f;

        // ★ 문구는 스트링 표가 정본이다 (2026-08-26 · 여기 박혀 있던 «침식 …» 은 영어로
        //   바뀌지 않았다. 표의 en 은 유저가 쓰는 낱말 «Corruption» 을 따른다).
        //   표에 키가 없으면 폴백(옛 한국어)이 그대로 나오므로 화면은 지금과 같다.
        const string KeyValue = "ui_erosion_value";
        const string KeyWithState = "ui_erosion_value_state";
        const string KeyNone = "ui_erosion_none";

        /// <summary>이 게이지가 하이라키에 실제로 존재하는지. 없으면 모든 호출이 조용히 무시된다.</summary>
        public bool IsBound => _bound;

        /// <summary>
        /// <paramref name="parent"/> 밑의 <paramref name="backPath"/> 경로에서 게이지 조각을 찾는다.
        /// 못 찾아도 예외를 내지 않는다 — 이 게이지가 아직 없는 패널에서도 호출부가 그대로
        /// 동작하도록(부분 적용 상태에서 콘솔이 에러로 도배되지 않도록) 조용히 비활성 상태가 된다.
        /// </summary>
        public void Bind(Transform parent, string backPath)
        {
            _bound = false;
            _fill = null;
            _label = null;
            _root = null;

            if (parent == null) return;

            Transform back = parent.Find(backPath);
            if (back == null) return;

            _root = back.gameObject;
            _fill = back.Find("ErosionFill")?.GetComponent<Image>();
            _label = back.Find("ErosionLabel")?.GetComponent<TMP_Text>();

            // 스프라이트가 비어 있으면 fillAmount 가 무시되어 게이지가 항상 꽉 찬 것처럼
            // 보인다 — UiFillBar 문서 참조.
            UiFillBar.Prepare(_fill);

            _bound = _fill != null || _label != null;
        }

        /// <summary>
        /// 지금 침식 수치를 그린다. <paramref name="unit"/> 이 null 이면 빈 상태로 그린다
        /// (선택된 캐릭터가 없는 패널 상태).
        /// </summary>
        public void Refresh(CharacterUnit unit)
        {
            if (!_bound) return;

            CharacterErosion erosion = CharacterErosion.Of(unit);

            // ★★ 2026-08-21 — <b>소환수(아루의 골렘)는 침식 칸을 «해당 없음» 으로 둔다</b>
            //   (유저 지시: *"ui에도 아루의 골렘에겐 침식 수치가 보이지 않거나 항상 0으로
            //   고정되게"*). 골렘은 침식이 <b>일어나지 않는</b> 유닛이므로(「강림」 정의문),
            //   «0» 이라는 숫자보다 «칸이 없다» 는 표시가 사실에 가깝다 — 0 은 «아직 안 쌓였다»
            //   로 읽히고, 그러면 «곧 쌓이겠구나» 라는 잘못된 기대를 준다.
            if (unit != null && unit.IsSummoned)
            {
                if (_fill != null) _fill.fillAmount = 0f;
                if (_label != null)
                {
                    _label.text = Data.StringTable.Get(KeyNone, "침식 -");
                    _label.color = HudTheme.TextDim;
                }
                return;
            }

            if (unit == null || erosion == null)
            {
                if (_fill != null) _fill.fillAmount = 0f;
                if (_label != null)
                    _label.text = unit == null
                        ? "-"
                        : string.Format(Data.StringTable.Get(KeyValue, "침식 {0}"), 0);
                return;
            }

            float ratio = erosion.ErosionRatio;

            if (_fill != null)
            {
                _fill.fillAmount = ratio;
                _fill.color = GaugeColor(ratio);
            }

            if (_label != null)
            {
                // 정신 이상이 발동 중이면 수치와 함께 그 이름도 보여준다 — 로스터의 "현재 상태"
                // 칸과 중복되지만, 전술·성장 창에는 그 칸이 없어서 여기 붙여야 한다.
                int shown = Mathf.RoundToInt(erosion.Erosion);
                _label.text = erosion.HasActive
                    ? string.Format(Data.StringTable.Get(KeyWithState, "침식 {0} · {1}"),
                                    shown, erosion.ActiveName)
                    : string.Format(Data.StringTable.Get(KeyValue, "침식 {0}"), shown);
                _label.color = erosion.HasActive ? HudTheme.TextErosion : HudTheme.TextDim;
            }
        }

        /// <summary>사망한 캐릭터의 행처럼 "수치가 의미 없는" 상태로 비운다.</summary>
        public void Clear()
        {
            if (!_bound) return;
            if (_fill != null) _fill.fillAmount = 0f;
            if (_label != null) _label.text = string.Empty;
        }

        /// <summary>이 게이지 전체를 보이거나 숨긴다.</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
        }

        /// <summary>
        /// 침식 비율에 따른 막대 색. 체력바(초록→노랑→빨강)와 겹치지 않는 보라→자홍이다.
        /// 상한이 가까워질수록(= 정신 이상이 임박) 눈에 띄게 만드는 것이 목적이다.
        /// </summary>
        public static Color GaugeColor(float ratio) =>
            Color.Lerp(HudTheme.BarErosion, HudTheme.BarErosionHigh,
                       Mathf.Clamp01(ratio / HighRatio));
    }
}
