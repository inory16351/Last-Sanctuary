using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 패시브 스킬 <b>상세 효과</b> 창. 캐릭터 성장 창의 스킬 칸을 클릭하면 열린다.
    ///
    /// 성장 창의 카드에는 <b>플레이버 문장</b>(캐릭터 테이블 <c>skill_explain</c>)만 짧게 보여주고,
    /// 실제로 무슨 일이 일어나는지(<c>Skill_Type</c> 시트의 정의문, 수치가 채워진 상태)는 이 창이 맡는다.
    ///
    /// <b>미해금 스킬은 열리지 않는다</b> — 카드 자체가 실루엣 + "???" 이므로
    /// 여기까지 오지 않지만, 안전하게 한 번 더 막는다(<see cref="Open"/>).
    ///
    /// 다른 패널(<c>TacticalOrderPanel</c> · <c>CharacterGrowthPanel</c>)과 같은 API 모양을 쓴다:
    /// <c>Instance</c> / <c>IsOpen</c> / <c>Toggle</c> / <c>SetOpen</c> / <c>Close</c>.
    /// 단 이 창은 성장 창 <b>위에</b> 뜨는 것이므로 성장 창을 닫지 않는다.
    /// </summary>
    public class SkillDetailPanel : MonoBehaviour
    {
        public static SkillDetailPanel Instance { get; private set; }

        [Header("문구")]
        [SerializeField] string ownerFormat = "{0} · 패시브 {1}";
        [SerializeField] string valuesFormat = "수치  {0}";
        [SerializeField] string noEffectText = "효과 정의문이 비어 있습니다. 캐릭터 테이블의 Skill_Type 시트를 확인하세요.";

        Image _icon;
        TMP_Text _nameText;
        TMP_Text _ownerText;
        TMP_Text _flavorText;
        TMP_Text _effectText;
        TMP_Text _valuesText;

        void Awake()
        {
            Instance = this;
            BuildBindings();
            gameObject.SetActive(false);   // 항상 닫힌 채로 시작
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsOpen => gameObject.activeSelf;
        public void Close() => gameObject.SetActive(false);

        /// <summary>
        /// 스킬 하나를 보여준다. 해금되지 않았거나 스킬이 없으면 아무것도 하지 않는다 —
        /// 미해금 스킬의 내용이 이 경로로 새어나가지 않게 한다.
        /// </summary>
        public void Open(PassiveSkillSO skill, CharacterUnit owner, int slot, bool unlocked)
        {
            if (skill == null || !unlocked) return;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();   // 성장 창 위에 그린다

            if (_icon != null)
            {
                Sprite s = skill.Icon;
                _icon.sprite = s;
                _icon.color = s != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (_nameText != null) _nameText.text = skill.skillName;

            if (_ownerText != null)
                _ownerText.text = owner != null
                    ? string.Format(ownerFormat, owner.DisplayName, slot + 1)
                    : string.Format(ownerFormat, "-", slot + 1);

            if (_flavorText != null) _flavorText.text = skill.flavorText;

            if (_effectText != null)
            {
                string effect = skill.EffectText();
                _effectText.text = string.IsNullOrWhiteSpace(effect) ? noEffectText : effect;
            }

            if (_valuesText != null) _valuesText.text = BuildValuesLine(skill);
        }

        /// <summary>
        /// 정의문에 실제로 쓰인 수치만 나열한다. 0 이고 정의문에도 안 나오는 값은 의미가 없으므로 뺀다 —
        /// 안 그러면 대부분의 스킬이 "value_02 0 · value_03 0" 을 달고 나온다.
        /// </summary>
        string BuildValuesLine(PassiveSkillSO skill)
        {
            string template = skill.effectTemplate ?? "";
            var parts = new System.Collections.Generic.List<string>(4);

            void Add(string token, string label, float v)
            {
                if (template.Contains(token) || v != 0f) parts.Add($"{label} {Num(v)}");
            }

            Add("{value_01}", "①", skill.value01);
            Add("{value_02}", "②", skill.value02);
            Add("{value_03}", "③", skill.value03);
            if (skill.coolTime > 0f) parts.Add($"쿨타임 {Num(skill.coolTime)}초");

            if (parts.Count == 0) return "";
            return string.Format(valuesFormat, string.Join("   ", parts));
        }

        static string Num(float v) =>
            Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");

        void BuildBindings()
        {
            _icon = Find<Image>("Icon");
            _nameText = Find<TMP_Text>("Name");
            _ownerText = Find<TMP_Text>("Owner");
            _flavorText = Find<TMP_Text>("Flavor");
            _effectText = Find<TMP_Text>("EffectBack/Effect");
            _valuesText = Find<TMP_Text>("Values");

            var close = Find<Button>("CloseButton");
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(Close);
            }
        }

        T Find<T>(string path) where T : Component
        {
            Transform t = transform.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[SkillDetail] 하이라키에서 '{path}' 를 찾지 못했습니다.", this);
                return null;
            }
            return t.GetComponent<T>();
        }
    }
}
