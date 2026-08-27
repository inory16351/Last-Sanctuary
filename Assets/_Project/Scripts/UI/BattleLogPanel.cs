using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Resource;
using LastSanctuary.Units;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 좌측 로그라인. 최근 일어난 일을 몇 줄 보여준다 — UI 가 없던 동안 콘솔 로그로만
    /// 확인하던 것들(자원 획득, 처치, 생성)을 화면에서 바로 보기 위한 것이다.
    ///
    /// <b>줄은 모체 하나를 복제해서 만든다</b>(<see cref="lineTemplate"/>) — 준수사항 §10 H-2.
    /// 최대 줄 수에 도달하면 새로 만들지 않고 <b>가장 오래된 줄을 재사용</b>해 맨 아래로 옮긴다.
    /// 매 이벤트마다 Instantiate/Destroy 하면 전투 중에 GC 가 계속 돈다.
    ///
    /// 로그 출처는 두 갈래다:
    ///   - 게임 시스템의 기존 이벤트를 직접 구독 (처치·에너지·생성·강화)
    ///   - <see cref="HudLog"/> 정적 이벤트 (UI 쪽에서 임의로 남기는 줄)
    /// 후자를 둔 이유는 로그를 남기려고 UI 참조를 여기저기 들고 다니지 않기 위해서다.
    /// </summary>
    public class BattleLogPanel : MonoBehaviour
    {
        [Header("하이라키 연결")]
        [Tooltip("줄이 쌓이는 컨테이너 (VerticalLayoutGroup)")]
        [SerializeField] RectTransform linesRoot;

        [Tooltip("복제할 줄의 원본. 비활성으로 둘 것")]
        [SerializeField] RectTransform lineTemplate;

        [Header("표시")]
        // ★★★ <b>줄 수를 50 으로 늘리고 «올려서 보게» 했다</b> (2026-08-26 · 유저 지시:
        //   *"로그 50개 까지 저장해서 올려서 볼 수 있도록 스크롤 바 추가"*).
        //
        //   예전에는 10줄이었고 <b>틀에 보이는 만큼만</b> 이 전부였다 — 난전에서는 한 웨이브가
        //   지나가는 동안 «누가 죽었는지» 가 이미 밀려나 있었다. 이제 <b>가장 오래된 줄을
        //   재사용하는 그 구조 그대로</b> 상한만 50 으로 올리고, 틀보다 길어진 만큼은
        //   <see cref="ScrollRect"/> 가 잘라서 보여준다.
        //
        // ⚠ <b>줄 재사용은 그대로 둔다</b>(맨 위 클래스 주석) — 50줄이 되어도 Instantiate 는
        //   최대 50번뿐이고 그 뒤로는 한 번도 일어나지 않는다.
        [Tooltip("보관할 최대 줄 수. 넘으면 가장 오래된 줄부터 밀려난다. " +
                 "★ 틀에 보이는 줄 수가 아니라 <b>스크롤로 올려 볼 수 있는</b> 줄 수다")]
        [Min(1)] [SerializeField] int maxLines = 50;

        [Tooltip("새 줄이 붙으면 <b>맨 아래로 따라 내려간다</b>. " +
                 "★ 유저가 위로 올려 둔 동안에는 따라가지 않는다 — 읽고 있는 자리를 뺏지 않으려는 것이다")]
        [SerializeField] bool followNewLines = true;

        [Tooltip("맨 아래에서 이만큼 안쪽이면 «바닥에 있다» 로 본다(0~1). " +
                 "정확히 0 일 때만 따라가면 한 픽셀 스크롤에도 따라가기가 끊긴다")]
        [Range(0f, 0.5f)] [SerializeField] float bottomEpsilon = 0.02f;

        [Header("구독할 이벤트")]
        [SerializeField] bool logKills = true;
        [SerializeField] bool logEnergy = true;
        [SerializeField] bool logUpgrades = true;

        [Tooltip("아군(캐릭터·성역) 사망은 처치 로그와 별개로 항상 남긴다")]
        [SerializeField] bool logAllyDeaths = true;

        readonly List<TMP_Text> _lines = new List<TMP_Text>();

        /// <summary>줄이 틀보다 길어졌을 때 올려 보는 통로. 씬에 없으면 null 이고 예전처럼 동작한다.</summary>
        ScrollRect _scroll;

        ResourceManager _resources;
        CharacterUpgradeService _upgrades;
        UnitSpawner _spawner;

        void Start()
        {
            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            // ★ 스크롤 틀이 생기면서 «줄이 쌓이는 곳» 이 한 겹 안으로 들어갔다.
            //   ⚠ <b>옛 경로를 폴백으로 남긴다</b> — 씬을 아직 안 고친 상태에서도 로그가
            //     그대로 나와야 한다(유물 창이 스크롤을 얻을 때 쓴 그 방법 · 135-3절).
            if (linesRoot == null)
                linesRoot = transform.Find("ScrollView/Viewport/Lines") as RectTransform
                         ?? transform.Find("Lines") as RectTransform;
            if (lineTemplate == null) lineTemplate = transform.Find("LineTemplate") as RectTransform;

            if (linesRoot == null || lineTemplate == null)
            {
                Debug.LogError("[Log] linesRoot / lineTemplate 이 연결되지 않았습니다. " +
                               "HUD_Log 의 Lines 와 LineTemplate 을 인스펙터에 넣어주세요.", this);
                enabled = false;
                return;
            }

            lineTemplate.gameObject.SetActive(false);

            BindScrollRect();

            HudLog.OnLine += Append;

            if (logKills || logAllyDeaths) DamageableUnit.OnAnyDied += HandleDied;

            _resources = ResourceManager.Instance;
            if (logEnergy && _resources != null) _resources.OnEnergyChanged += HandleEnergyChanged;

            _upgrades = CharacterUpgradeService.Instance;
            if (logUpgrades && _upgrades != null) _upgrades.OnUpgraded += HandleUpgraded;

            _spawner = FindAnyObjectByType<UnitSpawner>();
            if (_spawner != null) _spawner.OnCharacterSpawned += HandleCharacterSpawned;

            Append(HudTheme.T("log_battlelog_ready", "전투 로그 준비 완료"), HudLogKind.Info);
        }

        void OnDestroy()
        {
            HudLog.OnLine -= Append;
            DamageableUnit.OnAnyDied -= HandleDied;

            if (_resources != null) _resources.OnEnergyChanged -= HandleEnergyChanged;
            if (_upgrades != null) _upgrades.OnUpgraded -= HandleUpgraded;
            if (_spawner != null) _spawner.OnCharacterSpawned -= HandleCharacterSpawned;
        }

        /// <summary>
        /// <see cref="ScrollRect"/>·<see cref="Scrollbar"/> 의 object-참조 필드
        /// (content/viewport/handleRect 등)는 MCP 로 넣을 수 없다(진행상황 8절 4번) —
        /// 이름으로 찾아 코드가 꽂는다. 인스펙터에 이미 들어 있으면 건드리지 않는다.
        ///
        /// ★ <see cref="CharacterRosterPanel.BindScrollRect"/> · <c>RelicPanel</c> 과
        ///   <b>같은 함수 모양</b>이다 — 창이 늘 때마다 배선 규칙이 갈리지 않게.
        /// ⚠ 스크롤 틀이 씬에 없으면 <b>조용히 넘어간다</b>. 그 경우 로그는 예전처럼
        ///   «보이는 만큼만» 나오고 기능이 깨지지는 않는다.
        /// </summary>
        void BindScrollRect()
        {
            _scroll = transform.Find("ScrollView")?.GetComponent<ScrollRect>();
            if (_scroll == null) return;

            if (_scroll.content == null) _scroll.content = linesRoot;
            if (_scroll.viewport == null)
                _scroll.viewport = transform.Find("ScrollView/Viewport") as RectTransform;

            if (_scroll.verticalScrollbar == null)
            {
                var scrollbar = transform.Find("Scrollbar")?.GetComponent<Scrollbar>();
                if (scrollbar != null)
                {
                    _scroll.verticalScrollbar = scrollbar;
                    if (scrollbar.handleRect == null)
                        scrollbar.handleRect = transform.Find("Scrollbar/Handle") as RectTransform;
                    if (scrollbar.targetGraphic == null)
                        scrollbar.targetGraphic = transform.Find("Scrollbar/Handle")?.GetComponent<Image>();
                }
            }
        }

        /// <summary>
        /// 지금 <b>맨 아래를 보고 있는가</b>. 스크롤 틀이 없으면 언제나 참이다
        /// (그때는 «따라간다» 는 개념 자체가 없다).
        ///
        /// ⚠ 세로 스크롤에서 <c>verticalNormalizedPosition</c> 은 <b>0 이 바닥</b>이다.
        ///   내용이 틀보다 짧으면 이 값이 1 로 튀므로 «넘치는가» 를 먼저 본다 —
        ///   안 그러면 로그가 몇 줄 없을 때 따라가기가 꺼진 것처럼 보인다.
        /// </summary>
        bool IsAtBottom()
        {
            if (_scroll == null || _scroll.content == null || _scroll.viewport == null) return true;
            if (_scroll.content.rect.height <= _scroll.viewport.rect.height) return true;
            return _scroll.verticalNormalizedPosition <= bottomEpsilon;
        }

        // ------------------------------------------------------------------
        // 이벤트 → 로그 문장
        // ------------------------------------------------------------------

        void HandleDied(DamageableUnit unit)
        {
            if (unit == null) return;

            if (unit.Faction == Faction.Angel)
            {
                if (!logAllyDeaths) return;
                // ⚠ {0} = 죽은 아군 이름. 자리표를 지우면 누가 죽었는지가 사라진다.
                Append(unit.Kind == UnitKind.Nexus
                           ? HudTheme.T("log_nexus_destroyed", "성역 파괴")
                           : string.Format(HudTheme.T("log_ally_died", "{0} 사망"), NameOf(unit)),
                       HudLogKind.Danger);
                return;
            }

            if (!logKills) return;

            // ⚠ 「중립 몬스터/몬스터」를 문장에 이어 붙이지 않는다 — 어순이 다른 언어에서
            //   옮길 수 없다. 종류마다 «형식 하나»를 따로 둔다({0} = 처치된 몬스터 이름).
            Append(string.Format(
                       unit is NeutralMonsterUnit
                           ? HudTheme.T("log_kill_neutral", "중립 몬스터 처치 — {0}")
                           : HudTheme.T("log_kill_monster", "몬스터 처치 — {0}"),
                       NameOf(unit)),
                   HudLogKind.Good);
        }

        /// <summary>
        /// 로그에 쓸 이름 — <b>표의 이름</b>이 먼저다.
        ///
        /// 예전에는 <c>unit.name</c>(오브젝트 이름)을 그대로 찍었다. 몬스터는 스포너가
        /// 복제할 때마다 이름 뒤에 일련번호를 붙였기 때문에 로그가 "지옥 송곳니_7 처치"
        /// 처럼 나왔다(유저 지시 2026-08-13: "몬스터 뒤에 번호 붙는 거 없애줘 캐릭터랑
        /// 동일하게 그냥 이름으로").
        ///
        /// ★ 2026-08-15 — 여기 있던 <c>is CharacterUnit</c> / <c>is MonsterUnit</c> 갈래를
        /// 지웠다. <b>중립 몬스터가 그 갈래에 없어서</b> 하이라키 이름으로 떨어졌고, 같은
        /// 갈래가 <c>CharacterPassives</c> 에도 한 벌 더 있어 종류가 늘 때마다 한쪽을
        /// 빠뜨렸다. 이제는 유닛 자신에게 물어본다
        /// (<see cref="DamageableUnit.DisplayName"/>).
        /// </summary>
        static string NameOf(DamageableUnit unit) =>
            unit != null ? unit.DisplayName : string.Empty;

        void HandleEnergyChanged(int delta, int total)
        {
            // 소비는 각 기능(생성·강화)이 이미 자기 문장으로 남기므로 획득만 적는다.
            if (delta <= 0) return;
            // ⚠ {0} = 이번에 들어온 양 · {1} = 보유 총량. 순서를 바꾸지 말 것.
            Append(string.Format(HudTheme.T("log_energy_gain", "에너지 +{0} (총 {1})"), delta, total),
                   HudLogKind.Good);
        }

        void HandleUpgraded(CharacterUnit unit, int cost)
        {
            if (unit == null) return;
            // ★ 2026-08-15 — "강화 N회" 표기를 <b>Lv.N</b> 으로 통일했다(유저 지시).
            //   값 자체는 그대로 UpgradeCount 다 — 화면에 쓰는 이름만 바뀌었다.
            // ⚠ {0} = 캐릭터 이름 · {1} = 오른 레벨 · {2} = 쓴 에너지. 셋 다 지우지 말 것.
            Append(string.Format(HudTheme.T("log_upgraded", "{0} Lv.{1} (−{2})"),
                                 NameOf(unit), unit.UpgradeCount, cost),
                   HudLogKind.Good);
        }

        void HandleCharacterSpawned(CharacterUnit unit)
        {
            if (unit == null) return;
            // ⚠ {0} = 합류한 캐릭터 이름. 지우지 말 것.
            Append(string.Format(HudTheme.T("log_char_joined", "{0} 합류"), NameOf(unit)),
                   HudLogKind.Good);
        }

        // ------------------------------------------------------------------

        /// <summary>줄 하나를 맨 아래에 붙인다. 가득 찼으면 맨 위 줄을 재활용한다.</summary>
        public void Append(string message, HudLogKind kind = HudLogKind.Info)
        {
            if (string.IsNullOrEmpty(message) || linesRoot == null) return;

            TMP_Text line;
            if (_lines.Count < maxLines)
            {
                RectTransform clone = Instantiate(lineTemplate, linesRoot);
                clone.gameObject.SetActive(true);
                line = clone.GetComponent<TMP_Text>();
                if (line == null) return;
                _lines.Add(line);
            }
            else
            {
                line = _lines[0];
                _lines.RemoveAt(0);
                _lines.Add(line);
            }

            bool follow = followNewLines && IsAtBottom();

            line.text = message;
            line.color = HudLog.ColorOf(kind);
            line.transform.SetAsLastSibling();   // 항상 맨 아래가 최신

            // ★ <b>레이아웃이 돌기 전에는 내려갈 곳을 모른다</b> — 줄을 하나 붙인 «그 프레임» 의
            //   content 높이는 아직 옛값이다. 강제로 다시 계산한 뒤 내린다.
            //   ⚠ 유저가 위로 올려 둔 상태(follow == false)면 <b>건드리지 않는다</b>.
            if (follow && _scroll != null && _scroll.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.content);
                _scroll.verticalNormalizedPosition = 0f;
            }
        }
    }
}
