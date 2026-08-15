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
        static SkillDetailPanel _instance;

        /// <summary>
        /// ★ <b>버그 수정 (유저 리포트: "스킬 상세 설명 UI 가 안 나온다")</b>
        ///
        /// 예전에는 <c>Awake</c> 에서만 채우는 순수 static 필드였다. 그런데 이 창은 씬에서
        /// <b>비활성</b>(<c>HUD_SkillDetail.activeSelf = false</c>)으로 시작하고,
        /// <b>비활성 오브젝트의 <c>Awake</c> 는 아예 돌지 않는다.</b> 그래서
        /// <see cref="Instance"/> 가 <b>영원히 null</b> 이었고, 호출부가
        /// <c>SkillDetailPanel.Instance?.Open(...)</c> 로 물음표를 붙여 놓았기 때문에
        /// <b>에러도 로그도 없이 조용히 아무 일도 일어나지 않았다</b> — 스킬 카드를 눌러도
        /// 창이 뜰 수가 없었다.
        ///
        /// 이 프로젝트가 <b>같은 함정을 이미 두 번 밟았다</b>(36-4절 <c>SquadPanel</c>,
        /// 49-6절 <c>CharacterGrowthPanel</c>). 그때는 <b>부르는 쪽</b>에서
        /// <c>FindAnyObjectByType(FindObjectsInactive.Include)</c> 로 우회했는데,
        /// 그러면 <b>새 호출부가 생길 때마다 같은 우회를 기억해야 한다</b> — 이번 버그가 정확히
        /// 그래서 났다. 그래서 이번에는 <b>우회를 프로퍼티 안으로 넣어</b> 호출부가 아무것도
        /// 몰라도 되게 했다. 이 창을 부르는 코드는 그대로 두면 된다.
        /// </summary>
        public static SkillDetailPanel Instance
        {
            get
            {
                if (_instance != null) return _instance;

                // 비활성이라 Awake 가 안 돌았을 수 있다 — 비활성까지 포함해 찾는다.
                _instance = FindAnyObjectByType<SkillDetailPanel>(FindObjectsInactive.Include);
                if (_instance != null) _instance.EnsureBound();
                return _instance;
            }
            private set => _instance = value;
        }

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

        bool _bound;

        void Awake()
        {
            Instance = this;
            EnsureBound();

            // ⚠⚠ <b>2026-08-15 — 여기 있던 <c>gameObject.SetActive(false)</c> 를 지웠다.</b>
            //
            //   이 창은 씬에 <b>비활성으로 저장</b>돼 있어 Awake 가 씬 로드 때 안 돌고,
            //   <see cref="Open"/> 의 <c>SetActive(true)</c> <b>안에서</b> 처음 돈다.
            //   그 자리에서 자기를 끄면 <b>창이 열리는 순간 닫힌다</b> — 위의
            //   <see cref="Instance"/> 주석이 고친 "Instance 가 null" 버그 <b>바로 다음에
            //   숨어 있던 두 번째 원인</b>이다(같은 증상: 눌러도 아무 일도 안 일어난다).
            //
            //   "닫힌 채로 시작"은 씬이 그렇게 저장돼 있는 것으로 이미 지켜진다.
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 하이라키 배선을 한 번만 한다. <see cref="Awake"/> 가 안 돌았을 수도 있으므로
        /// (비활성 시작 — 위 <see cref="Instance"/> 주석) <see cref="Open"/> 쪽에서도 부른다.
        /// </summary>
        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;
            BuildBindings();
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

            EnsureBound();   // Awake 가 안 돌았을 수 있다 (비활성 시작)
            gameObject.SetActive(true);
            transform.SetAsLastSibling();   // 성장 창 위에 그린다

            if (_icon != null)
            {
                Sprite s = skill.Icon;
                _icon.sprite = s;
                _icon.color = s != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (_nameText != null) _nameText.text = skill.DisplayName;

            if (_ownerText != null)
                _ownerText.text = owner != null
                    ? string.Format(ownerFormat, owner.DisplayName, slot + 1)
                    : string.Format(ownerFormat, "-", slot + 1);

            if (_flavorText != null) _flavorText.text = skill.FlavorText;

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
