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
            LocalizeLabels();
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
                // ★ 2026-08-20 — <b>「상세 설명」이 있으면 그것을 보여준다</b>
                //   (유저 지시: *"밸류 타입보단 덜 상세하게"*). 규칙은 SO 안에 있다 —
                //   <see cref="PassiveSkillSO.EffectDisplayText"/> 의 긴 주석 참조.
                //   ★ 2026-08-25 — <b>그 문장이 이제 수치를 품는다</b>. 자리표를 채우는
                //     일은 SO 가 하므로(`DetailText`) 이 줄은 그대로 두면 된다.
                string effect = skill.EffectDisplayText();
                _effectText.text = string.IsNullOrWhiteSpace(effect) ? noEffectText : effect;
            }

            if (_valuesText != null) _valuesText.text = BuildValuesLine(skill);
        }

        /// <summary>
        /// ★★★ <b>재사용 대기시간만</b> 남긴다 (2026-08-25 · 유저 지시: *"스킬 설명도 자세한
        /// 수치가 상세 설명에 들어가는 방식으로 … 지금 수치가 아래에 따로 빠져있어서
        /// <b>각 수치가 멀 의미하는건지 모를 가능성이 높음</b>. 메이플 스킬 설명 참고"*).
        ///
        /// <b>예전에 여기서 무엇을 했나</b> — <c>① 30   ② 5   ③ 12   쿨타임 8초</c> 처럼
        /// 수치를 <b>번호만 붙여</b> 늘어놓았다. 정보는 다 있었지만 <b>이름이 없었다</b> —
        /// «30이 무엇의 30인가» 를 알려면 표를 봐야 한다. 유저가 지적한 것이 정확히 이것이다.
        ///
        /// ★ 이제 수치는 <b>문장 안</b>으로 들어갔다(<see cref="PassiveSkillSO.DetailText"/> 가
        ///   자리표를 채운다). 숫자마다 바로 옆에 «무엇의» 가 붙으므로 이 줄은 할 일이 없다.
        /// ★ <b>쿨타임만 남긴 이유</b> — 그것은 문장이 설명하는 «효과» 가 아니라 <b>사용 조건</b>이라
        ///   본문에 섞으면 오히려 읽기 나쁘다. 메이플도 「재사용 대기시간」을 <b>따로</b> 적는다.
        /// ⚠ 쿨타임이 0 인 상시 발동 스킬은 <b>빈 줄</b>이다 — «쿨타임 0초» 는 «곧바로 다시
        ///   쓸 수 있다» 로 잘못 읽힌다(그런 스킬은 아예 발동하는 것이 아니다).
        /// </summary>
        string BuildValuesLine(PassiveSkillSO skill)
        {
            if (skill.coolTime <= 0f) return "";
            // ★ 「재사용 대기시간 N초」 는 <b>어순이 언어마다 다르다</b> — 그래서 문장을
            //   조각으로 이어 붙이지 않고 <b>자리표 하나짜리 형식</b>을 표에서 가져온다.
            string cool = string.Format(
                HudTheme.T("ui_skill_cooltime_format", "재사용 대기시간 {0}초"),
                Num(skill.coolTime));
            return string.Format(valuesFormat, cool);
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
    
        /// <summary>
        /// ★ 이 창의 문구를 <b>스트링 표</b>에서 가져온다 (2026-08-26 · 178-5절).
        /// 인스펙터 값은 <b>폴백</b>이다 — 표에 키가 없으면 화면은 지금과 같다.
        /// </summary>
        void LocalizeLabels()
        {
            ownerFormat = HudTheme.T("ui_skill_owner_format", ownerFormat);
            valuesFormat = HudTheme.T("ui_skill_values_format", valuesFormat);
            noEffectText = HudTheme.T("ui_skill_no_effect", noEffectText);
        }
}
}
