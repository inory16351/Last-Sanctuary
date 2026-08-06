using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.CameraControl;
using LastSanctuary.Combat;
using LastSanctuary.Units;
using LastSanctuary.Wave;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 좌측 상단 캐릭터 로스터. 캐릭터마다 한 줄씩 상태(이름·HP·능력치·현재 행동)를 보여주고,
    /// 줄을 누르면 그 캐릭터가 선택되며, 줄 안의 버튼으로 바로 강화할 수 있다.
    ///
    /// <b>행은 모체 하나를 복제해서 만든다</b>(<see cref="rowTemplate"/>) — 오브젝트는 하이라키에
    /// 만들되 개수가 런타임에 정해지는 반복 요소만 스크립트가 복제한다는 규칙
    /// (Docs/UI 브렌치 준수사항.md §10 H-2). 유닛 템플릿 복제 패턴(진행상황 5절)과 같은 모양이다.
    ///
    /// <b>죽어도 행이 즉시 사라지지 않는다</b> — 유저 요청: 사망한 캐릭터는 회색으로 표시해
    /// "죽었음"을 명확히 보여주고, 실제로 목록에서 지우는 건 웨이브가 끝난 뒤(<see cref="WaveManager.OnWaveEnded"/>)
    /// 로 미룬다. 그래서 캐릭터 목록(<see cref="_characters"/>)은 사망으로 줄어들지 않고
    /// <see cref="HandleWaveEnded"/> 에서만 정리된다. `CharacterUnit.OnDeath()` 가 오브젝트를
    /// 파괴하므로(<c>Destroy(gameObject)</c>), 죽는 순간(<see cref="DamageableUnit.OnDied"/>)에
    /// 이름·능력치를 미리 스냅샷해두고, 그 뒤로는 살아있는 멤버(Stats·transform 등)를 다시 읽지 않는다.
    ///
    /// 갱신은 세 갈래다:
    ///   - <b>HP 바</b>: <see cref="DamageableUnit.OnHpChanged"/> 를 행마다 구독해 <b>즉시</b> 반영한다.
    ///   - <b>사망 처리</b>: <see cref="DamageableUnit.OnDied"/> 를 행마다 구독해 <b>즉시</b> 회색으로 바꾼다.
    ///   - <b>그 외(이름·능력치·행동·선택 표시·강화 가능 여부)</b>: <see cref="refreshInterval"/> 마다
    ///     한 번씩, 살아있는 행만 — 매 프레임 전수 조회를 피하기 위한 것이다(준수사항 U-D10).
    ///
    /// <b>정렬(유저 요청)</b>: 목록은 항상 <b>현재 체력 %가 낮은 캐릭터가 위, 높은 캐릭터가
    /// 아래</b>로 정렬된다 — 지금 신경 써야 할 캐릭터가 스크롤 없이 바로 보이게 하기 위함이다.
    /// 사망한 캐릭터는 체력 0%지만 "지금 신경 쓸 대상"이 아니라서 예외로 **항상 맨 아래**에
    /// 둔다(살아있는 캐릭터들보다 뒤). <see cref="Row"/> 객체와 캐릭터의 실제 매칭
    /// (구독·데이터)은 전혀 안 바뀐다 — <see cref="ReorderRows"/> 가 화면에 보이는 **순서만**
    /// (`SetSiblingIndex`) 매 갱신마다 다시 계산한다.
    ///
    /// <b>체력바 애니메이션(유저 요청)</b>: "몬스터에게 맞으면 실제로 깎이는 게 보였으면
    /// 좋겠다"(격투 게임 체력바처럼) — 그래서 HP 가 바뀌어도 <c>fillAmount</c> 를 즉시
    /// 스냅하지 않고, 목표값(<see cref="Row.HpRatioTarget"/>)과 화면에 보이는 값
    /// (<see cref="Row.HpRatioDisplayed"/>)을 나눠서 <see cref="AnimateHpBars"/> 가 매
    /// 프레임(폴링 주기와 무관하게) <c>MoveTowards</c> 로 서서히 따라가게 한다. 능력치 표시
    /// (근접해서 보는 상세 스탯)와 캐릭터별 강화 버튼은 이 카드에서 뺐다 — 능력치 텍스트는
    /// 체력바가 커진 자리를 대신 채우고(숫자 % 를 더 잘 보이게), 강화는 추후 별도 UI로
    /// 다시 만든다는 전제로 제거했다(전체 강화는 HUD_Actions 의 기존 버튼으로 여전히 가능).
    /// </summary>
    public class CharacterRosterPanel : MonoBehaviour
    {
        [Header("하이라키 연결 (비워두면 자식에서 이름으로 찾는다)")]
        [Tooltip("행이 쌓이는 컨테이너 (VerticalLayoutGroup). 비우면 \"ScrollView/Viewport/List\"")]
        [SerializeField] RectTransform listRoot;

        [Tooltip("복제할 행의 원본. 비우면 자식 \"RowTemplate\". 비활성으로 둘 것")]
        [SerializeField] RectTransform rowTemplate;

        [Header("갱신")]
        [Tooltip("HP·행동·비용을 다시 읽는 주기(초). 0 이면 매 프레임")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.2f;

        [Header("색")]
        [SerializeField] Color rowNormal = new Color(0.10f, 0.12f, 0.16f, 0.70f);
        [SerializeField] Color rowSelected = new Color(0.13f, 0.28f, 0.26f, 0.90f);

        [Tooltip("체력 막대 색이 이 비율에서 중간(노랑)이 되고, 위/아래로 초록/빨강 쪽으로 부드럽게 바뀐다. " +
                 "칸이 좁아 막대 길이만으로는 조금씩 줄어드는 게 눈에 잘 안 띄어서, 색으로도 남은 %를 " +
                 "가늠할 수 있게 한다(유저 피드백: 체력바가 게이지로 안 줄어드는 것처럼 보인다)")]
        [Range(0.01f, 0.99f)] [SerializeField] float lowHpRatio = 0.35f;

        [Header("사망 표시 (웨이브가 끝날 때까지 행을 남겨둔다)")]
        [Tooltip("사망한 캐릭터의 행 배경색")]
        [SerializeField] Color rowDead = new Color(0.08f, 0.08f, 0.09f, 0.55f);

        [Tooltip("사망한 캐릭터의 체력바 색. 비어서(투명) 안 보이는 것보다 " +
                 "꽉 찬 회색 막대가 '사망'을 훨씬 눈에 띄게 알려준다")]
        [SerializeField] Color deadBarColor = new Color(0.42f, 0.42f, 0.45f, 0.9f);

        [Tooltip("사망한 캐릭터의 이름 글자색")]
        [SerializeField] Color deadTextColor = new Color(0.5f, 0.5f, 0.52f, 1f);

        [Header("체력바 애니메이션 (유저 요청 — 실제로 깎이는 게 보이게)")]
        [Tooltip("체력바가 목표 값(실제 체력)을 따라가는 속도(비율/초, 1.0 = 가득 찬 막대가 " +
                 "1초에 다 빈다). 맞는 순간 즉시 스냅되지 않고 서서히 줄어드는 걸 보여주려는 것이라 " +
                 "너무 크게 잡으면(예: 10 이상) 스냅과 차이가 안 느껴진다. 0이면 애니메이션 없이 즉시 반영")]
        [Min(0f)] [SerializeField] float hpDrainSpeed = 1.6f;

        [Header("카메라")]
        [Tooltip("행을 누르면 카메라를 그 캐릭터 위치로 옮긴다")]
        [SerializeField] bool focusCameraOnSelect = true;

        [Tooltip("켜면 감쇠 없이 즉시 이동(SnapTo), 끄면 부드럽게 이동(FocusOn)")]
        [SerializeField] bool snapCamera = false;

        /// <summary>복제된 행 하나가 참조하는 조각들. 매번 GetComponent 하지 않으려고 캐시한다.</summary>
        class Row
        {
            public GameObject Root;
            public Image Background;
            public Button SelectButton;
            public TMP_Text Name;
            public TMP_Text Duty;
            public Image HpFill;
            public TMP_Text HpPercentLabel;

            /// <summary>살아있는 동안만 유효. 죽은 뒤에는 멤버를 다시 읽지 않는다(파괴된 오브젝트라서).</summary>
            public CharacterUnit Unit;

            /// <summary>지금 <see cref="HpHandler"/>/<see cref="DiedHandler"/> 를 구독하고 있는 대상.
            /// 행이 재활용되어 다른 캐릭터로 바뀔 때 이전 구독을 정확히 끊기 위해 <see cref="Unit"/> 과 따로 든다.</summary>
            public DamageableUnit SubscribedUnit;

            /// <summary>이 행에 고정된 핸들러들. 구독/해제에 매번 같은 델리게이트가 필요하다.</summary>
            public System.Action<int, int> HpHandler;
            public System.Action<DamageableUnit> DiedHandler;

            /// <summary>사망 확정 여부. true 가 되면 폴링 갱신(RefreshValues)에서 건드리지 않는다.</summary>
            public bool IsDead;

            /// <summary>죽기 직전에 찍어둔 이름 — 죽은 뒤에는 이 값만 쓴다(Unit.name 을 다시 못 읽어서).</summary>
            public string CachedName;

            /// <summary>실제 최신 체력 비율. <see cref="ApplyHp"/>(이벤트 콜백)가 즉시 갱신한다.</summary>
            public float HpRatioTarget;

            /// <summary>화면에 지금 보여주고 있는 체력 비율. <see cref="AnimateHpBars"/> 가 매 프레임
            /// <see cref="HpRatioTarget"/> 을 향해 서서히 따라간다 — "실제로 깎이는" 느낌을 만든다.</summary>
            public float HpRatioDisplayed;
        }

        readonly List<Row> _rows = new List<Row>();

        /// <summary>로스터에 한 번이라도 올라온 캐릭터 전부. 죽어도 여기서 안 빠진다 —
        /// <see cref="HandleWaveEnded"/> 에서만 정리한다.</summary>
        readonly List<CharacterUnit> _characters = new List<CharacterUnit>();

        readonly List<CharacterUnit> _aliveScratch = new List<CharacterUnit>();

        /// <summary>이번 웨이브에서 죽은 캐릭터 집합. 웨이브가 끝나면 이 집합 기준으로 정리하고 비운다.</summary>
        readonly HashSet<CharacterUnit> _dead = new HashSet<CharacterUnit>();

        UnitSelector _selector;
        CameraRigController _cameraRig;
        WaveManager _waveManager;
        float _nextRefresh;

        void Start()
        {
            _selector = UnitSelector.Instance;
            _cameraRig = FindAnyObjectByType<CameraRigController>();
            _waveManager = FindAnyObjectByType<WaveManager>();

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다. 인스펙터에서 직접 넣으면 그 값이 우선이다.
            // "List" 는 스크롤(ScrollView/Viewport) 안으로 옮겨졌으므로 경로가 한 단계 깊다.
            if (listRoot == null) listRoot = transform.Find("ScrollView/Viewport/List") as RectTransform;
            if (rowTemplate == null) rowTemplate = transform.Find("RowTemplate") as RectTransform;

            if (rowTemplate == null || listRoot == null)
            {
                Debug.LogError("[Roster] listRoot / rowTemplate 이 연결되지 않았습니다. " +
                               "HUD_Roster 의 ScrollView/Viewport/List 와 RowTemplate 을 인스펙터에 넣어주세요.", this);
                enabled = false;
                return;
            }

            rowTemplate.gameObject.SetActive(false);
            BindScrollRect();

            if (_waveManager != null) _waveManager.OnWaveEnded += HandleWaveEnded;

            AppendNewCharacters();
        }

        /// <summary>
        /// ScrollRect·Scrollbar 의 object-참조 필드(content/viewport/handleRect 등)는
        /// MCP 로 넣을 수 없다(진행상황 8절 4번) — 그래서 이름으로 찾아 코드에서 직접 연결한다.
        /// 인스펙터에서 이미 연결돼 있으면 건드리지 않는다(사람이 직접 맞춘 값이 우선).
        /// </summary>
        void BindScrollRect()
        {
            var scrollRect = transform.Find("ScrollView")?.GetComponent<ScrollRect>();
            if (scrollRect == null) return;

            if (scrollRect.content == null) scrollRect.content = listRoot;
            if (scrollRect.viewport == null)
                scrollRect.viewport = transform.Find("ScrollView/Viewport") as RectTransform;

            if (scrollRect.verticalScrollbar == null)
            {
                var scrollbar = transform.Find("Scrollbar")?.GetComponent<Scrollbar>();
                if (scrollbar != null)
                {
                    scrollRect.verticalScrollbar = scrollbar;

                    if (scrollbar.handleRect == null)
                        scrollbar.handleRect = transform.Find("Scrollbar/Handle") as RectTransform;
                    if (scrollbar.targetGraphic == null)
                        scrollbar.targetGraphic = transform.Find("Scrollbar/Handle")?.GetComponent<Image>();
                }
            }
        }

        void OnDestroy()
        {
            if (_waveManager != null) _waveManager.OnWaveEnded -= HandleWaveEnded;
            UnsubscribeAll();
        }

        void UnsubscribeAll()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row.SubscribedUnit == null) continue;
                row.SubscribedUnit.OnHpChanged -= row.HpHandler;
                row.SubscribedUnit.OnDied -= row.DiedHandler;
            }
        }

        void Update()
        {
            // 체력바 애니메이션은 폴링 주기(refreshInterval)와 무관하게 매 프레임 진행해야
            // 부드럽다 — "실제로 깎이는" 느낌이 목적이라 0.2초 단위로 뚝뚝 끊기면 의미가 없다.
            AnimateHpBars(Time.unscaledDeltaTime);

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            if (_selector == null) _selector = UnitSelector.Instance;
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager>();
                if (_waveManager != null) _waveManager.OnWaveEnded += HandleWaveEnded;
            }

            AppendNewCharacters();
            RefreshValues();
            ReorderRows();
        }

        // ------------------------------------------------------------------
        // 캐릭터 목록 — 죽어도 안 줄어든다. 웨이브 종료 때만 정리한다.
        // ------------------------------------------------------------------

        /// <summary>지금 살아있는 캐릭터를 모은다 — "새로 생긴 캐릭터"를 찾는 용도로만 쓴다.</summary>
        void CollectAliveCharacters(List<CharacterUnit> into)
        {
            into.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is CharacterUnit c && c.IsAlive) into.Add(c);
        }

        /// <summary>아직 로스터에 없는 살아있는 캐릭터를 찾아 뒤에 추가하고 행을 물린다.</summary>
        void AppendNewCharacters()
        {
            CollectAliveCharacters(_aliveScratch);

            bool added = false;
            for (int i = 0; i < _aliveScratch.Count; i++)
            {
                CharacterUnit c = _aliveScratch[i];
                if (_characters.Contains(c)) continue;
                _characters.Add(c);
                added = true;
            }
            if (!added) return;

            while (_rows.Count < _characters.Count)
                _rows.Add(CreateRow(_rows.Count));

            for (int i = 0; i < _characters.Count; i++)
            {
                Row row = _rows[i];
                if (!row.Root.activeSelf) row.Root.SetActive(true);
                if (ReferenceEquals(row.Unit, _characters[i])) continue;   // 이미 이 캐릭터를 보여주는 중
                BindRowToUnit(row, _characters[i]);
            }
        }

        /// <summary>웨이브가 끝나면 죽은 캐릭터를 로스터에서 실제로 지운다(유저 요청).</summary>
        void HandleWaveEnded(int wave)
        {
            if (_dead.Count == 0) return;

            _characters.RemoveAll(c => _dead.Contains(c));
            _dead.Clear();

            ReassignAllRows();
        }

        /// <summary>죽은 캐릭터가 빠져 인덱스가 밀렸을 수 있으니 행 전체를 다시 배정한다.</summary>
        void ReassignAllRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool used = i < _characters.Count;

                if (!used)
                {
                    if (row.SubscribedUnit != null)
                    {
                        row.SubscribedUnit.OnHpChanged -= row.HpHandler;
                        row.SubscribedUnit.OnDied -= row.DiedHandler;
                        row.SubscribedUnit = null;
                    }
                    row.Unit = null;
                    row.IsDead = false;
                    if (row.Root.activeSelf) row.Root.SetActive(false);
                    continue;
                }

                if (!row.Root.activeSelf) row.Root.SetActive(true);

                CharacterUnit newUnit = _characters[i];
                if (ReferenceEquals(row.Unit, newUnit)) continue;   // 죽지 않고 그대로 남은 자리

                if (row.SubscribedUnit != null)
                {
                    row.SubscribedUnit.OnHpChanged -= row.HpHandler;
                    row.SubscribedUnit.OnDied -= row.DiedHandler;
                }
                BindRowToUnit(row, newUnit);
            }
        }

        /// <summary>행에 캐릭터를 물리고 구독을 건다. "살아있음" 상태로 시각을 되돌린다(재활용 대비).</summary>
        void BindRowToUnit(Row row, CharacterUnit unit)
        {
            row.Unit = unit;
            row.SubscribedUnit = unit;
            row.IsDead = false;

            unit.OnHpChanged += row.HpHandler;
            unit.OnDied += row.DiedHandler;

            // 재구성/재활용 직후엔 애니메이션 없이 바로 스냅한다 — 안 그러면 새로 물린
            // 캐릭터의 체력바가 0에서부터 차오르는 것처럼 보인다.
            row.HpRatioTarget = row.HpRatioDisplayed =
                unit.MaxHp > 0 ? (float)unit.CurrentHp / unit.MaxHp : 0f;
            ApplyDisplayedHp(row);

            ApplyAliveAppearance(row);
        }

        Row CreateRow(int index)
        {
            RectTransform clone = Instantiate(rowTemplate, listRoot);
            clone.name = $"Row_{index + 1}";
            clone.gameObject.SetActive(true);

            var row = new Row
            {
                Root = clone.gameObject,
                Background = clone.GetComponent<Image>(),
                SelectButton = clone.GetComponent<Button>(),
                Name = FindText(clone, "Name"),
                Duty = FindText(clone, "Duty"),
            };

            Transform hpBack = clone.Find("HpBack");
            if (hpBack != null)
            {
                Transform fill = hpBack.Find("HpFill");
                if (fill != null) row.HpFill = fill.GetComponent<Image>();

                Transform percentLabel = hpBack.Find("HpPercentLabel");
                if (percentLabel != null) row.HpPercentLabel = percentLabel.GetComponent<TMP_Text>();
            }

            // 람다가 row 를 잡아두므로 행이 다른 캐릭터로 바뀌어도 항상 지금 물린 캐릭터를 쓴다.
            if (row.SelectButton != null)
                row.SelectButton.onClick.AddListener(() => SelectRow(row));

            // 핸들러도 같은 이유로 row 를 닫아 두고 한 번만 만든다 — 구독/해제할 때마다 새
            // 델리게이트를 만들면 구독 해제(-=)가 다른 인스턴스를 지우려다 실패한다
            // (C# 이벤트는 참조가 같아야 -= 가 먹는다).
            row.HpHandler = (current, max) => ApplyHp(row, current, max);
            row.DiedHandler = unit => HandleUnitDied(row, unit);

            return row;
        }

        /// <summary>
        /// 목표 체력 비율만 갱신한다. <see cref="DamageableUnit.OnHpChanged"/> 구독 콜백 —
        /// 여기서 <c>fillAmount</c> 를 바로 바꾸지 않는다. 즉시 스냅하면 "몬스터에게 맞을 때마다
        /// 실제로 깎이는" 느낌이 안 나고 순간이동처럼 보인다(유저 피드백) — 화면에 보이는 값은
        /// <see cref="AnimateHpBars"/> 가 매 프레임 이 목표를 향해 서서히 따라간다.
        /// </summary>
        void ApplyHp(Row row, int current, int max)
        {
            if (row.IsDead) return;   // 사망 처리 후에는 건드리지 않는다
            row.HpRatioTarget = max > 0 ? (float)current / max : 0f;
        }

        /// <summary>
        /// 화면에 보이는 체력 비율을 목표치로 서서히 이동시킨다. 폴링 주기(refreshInterval)와
        /// 무관하게 매 프레임 불러야 애니메이션이 부드럽다 — <see cref="Update"/> 맨 앞에서 호출.
        /// </summary>
        void AnimateHpBars(float dt)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row.IsDead || row.HpFill == null) continue;
                if (Mathf.Approximately(row.HpRatioDisplayed, row.HpRatioTarget)) continue;

                row.HpRatioDisplayed = hpDrainSpeed > 0f
                    ? Mathf.MoveTowards(row.HpRatioDisplayed, row.HpRatioTarget, hpDrainSpeed * dt)
                    : row.HpRatioTarget;

                ApplyDisplayedHp(row);
            }
        }

        /// <summary>지금 <see cref="Row.HpRatioDisplayed"/> 값 그대로 막대 채움·색·숫자 %를 그린다.</summary>
        void ApplyDisplayedHp(Row row)
        {
            if (row.HpFill == null) return;

            row.HpFill.fillAmount = row.HpRatioDisplayed;
            row.HpFill.color = HpGaugeColor(row.HpRatioDisplayed);

            // 막대 길이만으로는 몇 % 줄었는지 눈으로 정확히 재기 어렵다는 피드백 —
            // 현재 체력을 0~100% 정수로 환산해 막대 위에 숫자로도 그대로 보여준다.
            if (row.HpPercentLabel != null)
                row.HpPercentLabel.text = $"{Mathf.RoundToInt(row.HpRatioDisplayed * 100f)}%";
        }

        /// <summary>
        /// 체력 비율에 맞춰 초록 → 노랑 → 빨강으로 부드럽게 보간한다.
        /// 막대 자체는 항상 <see cref="ApplyHp"/> 에서 fillAmount 로 줄어들고 있었지만,
        /// 로스터 칸이 좁아 몇 % 줄어든 게 눈에 잘 안 띄어서 "그냥 맞을 때 빨갛게 반짝인다"로
        /// 보인다는 피드백이 있었다 — 색으로도 남은 %를 가늠할 수 있게 3단 그라디언트로 바꿨다.
        /// </summary>
        Color HpGaugeColor(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            float mid = lowHpRatio;
            return ratio >= mid
                ? Color.Lerp(HudTheme.BarHpMid, HudTheme.BarHp, (ratio - mid) / (1f - mid))
                : Color.Lerp(HudTheme.BarHpLow, HudTheme.BarHpMid, ratio / mid);
        }

        /// <summary>
        /// 사망 확정. <see cref="DamageableUnit.OnDied"/> 구독 콜백 — <c>CharacterUnit.OnDeath()</c>
        /// 가 <c>Destroy(gameObject)</c> 를 부른 바로 다음에 같은 프레임에서 호출된다. Unity 의
        /// Destroy 는 프레임 끝에 처리되므로(DamageableUnit.ApplyDamage 주석 참조) <b>지금은 아직</b>
        /// <c>row.Unit</c> 의 멤버를 안전하게 읽을 수 있는 마지막 순간이다 — 표시에 필요한 값을
        /// 전부 스냅샷해두고, 이후로는 다시 읽지 않는다.
        /// </summary>
        void HandleUnitDied(Row row, DamageableUnit deadUnit)
        {
            if (row.IsDead) return;
            row.IsDead = true;

            if (row.Unit != null)
            {
                _dead.Add(row.Unit);
                row.CachedName = row.Unit.name;
            }

            ApplyDeadAppearance(row);

            // 선택 대상에서 확실히 빼둔다. UnitSelector 도 다음 프레임에 스스로 선택을
            // 놓지만(죽은 유닛은 IsAlive 가 false), 그 전에 행이라도 눌리지 않게 즉시 막는다.
            if (row.SelectButton != null) row.SelectButton.interactable = false;

            // 사망은 맨 아래로 내려가야 하는 순서 변경이라, 다음 폴링(최대 refreshInterval)
            // 까지 기다리지 않고 그 자리에서 바로 다시 정렬한다.
            ReorderRows();
        }

        /// <summary>죽은 캐릭터의 행을 회색으로 — "확실하게 죽었다"는 걸 알아볼 수 있게 한다.</summary>
        void ApplyDeadAppearance(Row row)
        {
            if (row.Background != null) row.Background.color = rowDead;
            if (row.Name != null) { row.Name.text = row.CachedName; row.Name.color = deadTextColor; }
            if (row.Duty != null) { row.Duty.text = "사망"; row.Duty.color = deadTextColor; }

            // 비어서(투명) 안 보이는 것보다, 꽉 찬 회색 막대가 "사망"을 훨씬 눈에 띄게 알려준다.
            // 죽음은 애니메이션으로 서서히 보여줄 상태가 아니라 즉시 확정이라 목표·표시값을
            // 둘 다 여기서 바로 맞춘다(AnimateHpBars 는 어차피 IsDead 행을 건너뛴다).
            row.HpRatioTarget = row.HpRatioDisplayed = 1f;
            if (row.HpFill != null)
            {
                row.HpFill.fillAmount = 1f;
                row.HpFill.color = deadBarColor;
            }
            if (row.HpPercentLabel != null) row.HpPercentLabel.text = string.Empty;
        }

        /// <summary>행이 재활용될 때 이전 사망 표시(회색)를 지우고 정상 색으로 되돌린다.</summary>
        void ApplyAliveAppearance(Row row)
        {
            if (row.Background != null) row.Background.color = rowNormal;
            if (row.Name != null) row.Name.color = HudTheme.TextMain;
            if (row.Duty != null) row.Duty.color = HudTheme.TextDim;
            if (row.SelectButton != null) row.SelectButton.interactable = true;
        }

        static TMP_Text FindText(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        void SelectRow(Row row)
        {
            if (row.IsDead || row.Unit == null || !row.Unit.IsAlive) return;
            if (_selector == null) _selector = UnitSelector.Instance;
            _selector?.Select(row.Unit);
            FocusCameraOn(row.Unit);
            RefreshValues();
        }

        /// <summary>
        /// 카메라를 그 캐릭터로 옮긴다. 카메라는 <c>CameraAnchor</c> 오브젝트가 움직이고
        /// 시네머신이 따라오는 구조라(진행상황 1·7절), 여기서도 카메라가 아니라 리그를 부른다.
        /// 맵 경계 밖으로는 <c>CameraRigController</c> 가 알아서 잘라준다.
        /// </summary>
        void FocusCameraOn(CharacterUnit unit)
        {
            if (!focusCameraOnSelect || unit == null) return;
            if (_cameraRig == null) _cameraRig = FindAnyObjectByType<CameraRigController>();
            if (_cameraRig == null) return;

            if (snapCamera) _cameraRig.SnapTo(unit.transform.position);
            else _cameraRig.FocusOn(unit.transform.position);
        }

        // ------------------------------------------------------------------

        void RefreshValues()
        {
            CharacterUnit selected = _selector != null ? _selector.Selected : null;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (!row.Root.activeSelf || row.Unit == null) continue;
                if (row.IsDead) continue;   // ApplyDeadAppearance 가 이미 확정한 값을 그대로 둔다

                CharacterUnit unit = row.Unit;

                if (row.Name != null) row.Name.text = unit.name;

                // HP 바는 여기서 건드리지 않는다 — ApplyHp(즉시)+AnimateHpBars(매 프레임)가 반영한다.
                // 폴링과 애니메이션이 같은 값을 이중으로 쓰면 순서에 따라 잠깐 어긋나 보일 수 있다.

                if (row.Duty != null) row.Duty.text = DutyTextOf(unit);

                if (row.Background != null)
                    row.Background.color = ReferenceEquals(unit, selected) ? rowSelected : rowNormal;
            }
        }

        /// <summary>
        /// 화면에 보이는 행 순서를 "체력 % 낮은 순(사망은 맨 아래)"으로 다시 맞춘다.
        /// <see cref="Row"/> 와 캐릭터의 매칭(구독·데이터)은 그대로 두고 <c>SetSiblingIndex</c>
        /// 로 <see cref="listRoot"/> 안의 표시 순서만 바꾼다 — <c>VerticalLayoutGroup</c> 이
        /// 형제 인덱스 순으로 배치하므로 이것만으로 목록이 다시 정렬된다.
        ///
        /// HP 비율은 새로 계산하지 않고 <see cref="Row.HpFill"/> 의 <c>fillAmount</c> 를 그대로
        /// 읽는다 — <see cref="ApplyHp"/> 가 이미 그 값을 최신 체력 비율로 유지하고 있어서
        /// 중복 계산이 필요 없다. 동률(같은 %)일 때는 <c>GetInstanceID()</c> 로 순서를 고정해서,
        /// 매 갱신마다 동률 캐릭터들의 순서가 이유 없이 뒤바뀌며 깜빡이는 것을 막는다.
        /// </summary>
        void ReorderRows()
        {
            var active = _rows.Where(r => r.Root.activeSelf)
                              .OrderBy(r => r.IsDead ? 1 : 0)                                   // 산 사람 먼저
                              .ThenBy(r => r.IsDead ? 0f : (r.HpFill != null ? r.HpFill.fillAmount : 0f))
                              .ThenBy(r => r.Unit != null ? r.Unit.GetInstanceID() : 0)         // 동률 순서 고정
                              .ToList();

            for (int i = 0; i < active.Count; i++)
                active[i].Root.transform.SetSiblingIndex(i);
        }

        /// <summary>"지금 뭐 하는 중인지" 한 단어. 전투가 자율 이동보다 우선이라 먼저 검사한다.</summary>
        static string DutyTextOf(CharacterUnit unit)
        {
            var combat = unit.GetComponent<UnitCombat>();
            if (combat != null && combat.Target != null && combat.Target.IsAlive)
                return combat.IsHunting ? "사냥" : "교전";

            var behavior = unit.GetComponent<CharacterBehavior>();
            if (behavior == null) return "-";

            return behavior.Duty switch
            {
                CharacterDuty.Scout => "정찰",
                CharacterDuty.Rally => "집결",
                _                   => "방어",
            };
        }
    }
}
