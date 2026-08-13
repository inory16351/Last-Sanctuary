using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Resource;
using LastSanctuary.Units;

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
        [Tooltip("화면에 남겨둘 최대 줄 수. 넘으면 가장 오래된 줄부터 밀려난다")]
        [Min(1)] [SerializeField] int maxLines = 10;

        [Header("구독할 이벤트")]
        [SerializeField] bool logKills = true;
        [SerializeField] bool logEnergy = true;
        [SerializeField] bool logUpgrades = true;

        [Tooltip("아군(캐릭터·넥서스) 사망은 처치 로그와 별개로 항상 남긴다")]
        [SerializeField] bool logAllyDeaths = true;

        readonly List<TMP_Text> _lines = new List<TMP_Text>();

        ResourceManager _resources;
        CharacterUpgradeService _upgrades;
        UnitSpawner _spawner;

        void Start()
        {
            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            if (linesRoot == null) linesRoot = transform.Find("Lines") as RectTransform;
            if (lineTemplate == null) lineTemplate = transform.Find("LineTemplate") as RectTransform;

            if (linesRoot == null || lineTemplate == null)
            {
                Debug.LogError("[Log] linesRoot / lineTemplate 이 연결되지 않았습니다. " +
                               "HUD_Log 의 Lines 와 LineTemplate 을 인스펙터에 넣어주세요.", this);
                enabled = false;
                return;
            }

            lineTemplate.gameObject.SetActive(false);

            HudLog.OnLine += Append;

            if (logKills || logAllyDeaths) DamageableUnit.OnAnyDied += HandleDied;

            _resources = ResourceManager.Instance;
            if (logEnergy && _resources != null) _resources.OnEnergyChanged += HandleEnergyChanged;

            _upgrades = CharacterUpgradeService.Instance;
            if (logUpgrades && _upgrades != null) _upgrades.OnUpgraded += HandleUpgraded;

            _spawner = FindAnyObjectByType<UnitSpawner>();
            if (_spawner != null) _spawner.OnCharacterSpawned += HandleCharacterSpawned;

            Append("전투 로그 준비 완료", HudLogKind.Info);
        }

        void OnDestroy()
        {
            HudLog.OnLine -= Append;
            DamageableUnit.OnAnyDied -= HandleDied;

            if (_resources != null) _resources.OnEnergyChanged -= HandleEnergyChanged;
            if (_upgrades != null) _upgrades.OnUpgraded -= HandleUpgraded;
            if (_spawner != null) _spawner.OnCharacterSpawned -= HandleCharacterSpawned;
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
                Append(unit.Kind == UnitKind.Nexus ? "넥서스 파괴" : $"{NameOf(unit)} 사망",
                       HudLogKind.Danger);
                return;
            }

            if (!logKills) return;

            string what = unit is NeutralMonsterUnit ? "중립 몬스터" : "몬스터";
            Append($"{what} 처치 — {NameOf(unit)}", HudLogKind.Good);
        }

        /// <summary>
        /// 로그에 쓸 이름 — <b>표의 이름</b>이 먼저다.
        ///
        /// 예전에는 <c>unit.name</c>(오브젝트 이름)을 그대로 찍었다. 몬스터는 스포너가
        /// 복제할 때마다 이름 뒤에 일련번호를 붙였기 때문에 로그가 "지옥 송곳니_7 처치"
        /// 처럼 나왔다(유저 지시 2026-08-13: "몬스터 뒤에 번호 붙는 거 없애줘 캐릭터랑
        /// 동일하게 그냥 이름으로"). 스포너 쪽에서 번호를 뗐고, 여기서도 표시 이름을
        /// 명시적으로 읽어 <b>하이라키 이름이 어떻든 로그가 흔들리지 않게</b> 한다.
        /// </summary>
        static string NameOf(DamageableUnit unit)
        {
            if (unit == null) return string.Empty;
            if (unit is CharacterUnit c) return c.DisplayName;
            if (unit is MonsterUnit m) return m.DisplayName;
            return unit.name;
        }

        void HandleEnergyChanged(int delta, int total)
        {
            // 소비는 각 기능(생성·강화)이 이미 자기 문장으로 남기므로 획득만 적는다.
            if (delta <= 0) return;
            Append($"에너지 +{delta} (총 {total})", HudLogKind.Good);
        }

        void HandleUpgraded(CharacterUnit unit, int cost)
        {
            if (unit == null) return;
            Append($"{unit.name} 강화 {unit.UpgradeCount}회 (−{cost})", HudLogKind.Good);
        }

        void HandleCharacterSpawned(CharacterUnit unit)
        {
            if (unit == null) return;
            Append($"{unit.name} 합류", HudLogKind.Good);
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

            line.text = message;
            line.color = HudLog.ColorOf(kind);
            line.transform.SetAsLastSibling();   // 항상 맨 아래가 최신
        }
    }
}
