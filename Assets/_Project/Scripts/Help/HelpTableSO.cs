using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Data;

namespace LastSanctuary.Help
{
    /// <summary>
    /// 조언 카드를 <b>띄우는 계기</b> — 표 <c>Help</c> 시트의 <c>trigger</c> 와 1:1 이다.
    ///
    /// ★★ <b>이름은 «기능을 처음 썼을 때» 를 가리킨다</b> (2026-08-24 유저 지시:
    ///   *"최초로 해당 기능을 눌렀을때 나타나게"*). 그래서 대부분이 <b>이미 있는 이벤트</b>에
    ///   그대로 걸린다 — 캐릭터를 만들면 <c>CanCreateCharacter</c>, 부대를 만들면
    ///   <c>SquadCreated</c>, 강화를 누르면 <c>CharacterUpgraded</c>. «버튼을 눌렀다» 를
    ///   따로 세지 않는 이유는, 누르는 통로가 여러 곳(액션 버튼 · 창 · 단축키)이라
    ///   버튼마다 세면 반드시 하나를 빠뜨리기 때문이다. <b>결과</b>를 보면 통로가 몇이든 잡힌다.
    ///
    /// ⚠ 여기 없는 값이 표에 들어오면 <see cref="HelpTableSO.ParseTrigger"/> 가
    ///   <see cref="None"/> 을 돌려주고 그 항목은 <b>백과에만</b> 남는다(저절로 안 뜬다).
    ///   표에 계기를 더하면 반드시 이 enum 과 <see cref="HelpService"/> 의 배선을 같이 더할 것 —
    ///   유물 대사표의 <c>situation</c> 이 세 번 밟은 그 함정이다.
    /// </summary>
    public enum HelpTrigger
    {
        None = 0,

        /// <summary>판이 시작되어 <b>첫 정비 시간</b>에 들어갔다. 가장 먼저 뜨는 둘이 여기 걸린다.</summary>
        NewRunFirstPreparation = 1,

        /// <summary>에너지가 처음 늘었다.</summary>
        EnergyGained = 2,

        /// <summary>캐릭터를 처음 만들었다 (= 「캐릭터 생성」을 처음 눌렀다).</summary>
        CanCreateCharacter = 3,

        /// <summary>첫 전투가 벌어졌다 (<c>WavePhase.Battle</c>).</summary>
        BattleStarted = 4,

        /// <summary>광폭화로 넘어갔다 (<c>WavePhase.Enrage</c>).</summary>
        EnrageStarted = 5,

        /// <summary>아군 캐릭터가 처음 쓰러졌다.</summary>
        AllyDied = 6,

        /// <summary>공격이 처음 빗나갔다.</summary>
        FirstMiss = 7,

        /// <summary>보스가 나오는 웨이브가 소환됐다.</summary>
        BossWaveSpawned = 8,

        /// <summary>강화를 처음 했다 (= 「강화」를 처음 눌렀다).</summary>
        CharacterUpgraded = 9,

        /// <summary>영웅 각성이 처음 일어났다.</summary>
        HeroAwakened = 10,

        /// <summary>발굴 칸의 느낌표가 처음 떴다.</summary>
        RelicDigMarkAppeared = 11,

        /// <summary>유물을 처음 얻었다.</summary>
        RelicObtained = 12,

        /// <summary>부대를 처음 만들었다 (= 「부대 설정」에서 처음 만들었다).</summary>
        SquadCreated = 13,

        /// <summary>집결지를 처음 찍었다.</summary>
        RallyPointCreated = 14,

        /// <summary>전술 지침을 처음 바꿨다.</summary>
        TacticsChanged = 15,

        // 16 = BuildModeEntered — <b>지웠다</b> (2026-08-25 · 유저: *"도움말에서 포탑 건설
        // 관련 설명 삭제해 해당 기능 없어졌어"*). 번호는 <b>비워 둔다</b> — 다시 쓰면
        // 이미 구워 둔 에셋의 «16» 이 엉뚱한 계기를 가리킨다.

        /// <summary>
        /// 누군가의 침식이 <see cref="HelpEntry.triggerArg"/> 에 <b>닿았다</b>(표에 50 이 들어 있다).
        /// </summary>
        ErosionReached = 17,

        /// <summary>정신 이상이 처음 발현했다.</summary>
        MentalErrorTriggered = 18,

        /// <summary>중립 몬스터를 처음 잡았다.</summary>
        NeutralKilled = 19,

        /// <summary>에픽 중립을 처음 발견했다.</summary>
        EpicNeutralFound = 20,

        /// <summary>사건(이벤트)이 처음 일어났다.</summary>
        EventStarted = 21,

        /// <summary>자동 저장이 처음 돌았다.</summary>
        AutoSaved = 22,

        /// <summary>배속이나 일시정지를 처음 만졌다.</summary>
        GameSpeedChanged = 23,

        // ══════════════════════════════════════════════════════════════
        //  ★★★ 허드 액션 버튼을 <b>처음 눌렀을 때</b> (2026-08-25 신설)
        // ══════════════════════════════════════════════════════════════
        // 유저 지시: *"허드 액션의 각 버튼을 <b>최초로 눌렀을때</b> 해당 기능에 대한 도움말이
        // 등장하는 것으로 진행"*.
        //
        // ⚠ <b>위의 계기들과 성질이 다르다.</b> 위는 «기능이 <b>일어난 뒤</b>» 를 듣고,
        //   이 일곱은 «기능을 <b>쓰려는 순간</b>» 을 가로챈다. 그래서 배선도 다르다 —
        //   <see cref="HelpService.Fire"/> 가 아니라
        //   <see cref="HelpService.InterceptFirstUse"/> 로 들어온다(그쪽 doc 참고).
        //
        // ★ 이 일곱만 «버튼» 에 거는 이유는 144-2 절의 «통로가 여럿이라 빠뜨린다» 가
        //   여기서는 성립하지 않기 때문이다 — 이 버튼들이 그 창을 여는 <b>유일한 통로</b>다.

        /// <summary>「캐릭터 생성」 버튼을 처음 눌렀다.</summary>
        ActionCreate = 24,

        /// <summary>「캐릭터 성장」 버튼을 처음 눌렀다 (강화 · <c>HUD_Growth</c>).</summary>
        ActionUpgrade = 25,

        /// <summary>「부대 설정」 버튼을 처음 눌렀다.</summary>
        ActionSquad = 26,

        /// <summary>「전술 지침」 버튼을 처음 눌렀다.</summary>
        ActionTactics = 27,

        /// <summary>「토벌 지시」 버튼을 처음 눌렀다.</summary>
        ActionSubjugate = 28,

        /// <summary>「유물 관리」 버튼을 처음 눌렀다.</summary>
        ActionRelic = 29,

        /// <summary>「환경 설정」 버튼을 처음 눌렀다.</summary>
        ActionSettings = 30,
    }

    /// <summary>
    /// 도움말 항목 하나 — 표 <c>Help</c> 시트의 한 줄.
    ///
    /// ★ <b>문구는 여기 없다.</b> 이 줄은 <b>구조와 스트링 키</b>만 들고 있고, 실제 글은
    ///   <c>스트링 키 테이블.xlsx</c> → <see cref="StringTable"/> 에 있다(표 <c>읽기</c> 시트
    ///   확정사항 ②). 그래서 문구를 다듬을 때 <b>이 에셋을 다시 굽지 않아도 된다</b>.
    /// </summary>
    [System.Serializable]
    public class HelpEntry
    {
        [Tooltip("표의 help_id. 백과의 항목 키이고 see_also 가 가리키는 이름이다")]
        public string helpId;

        [Tooltip("분류 이름 그대로 (기본 · 전투 · 성장 · 지휘 · 위험 · 운영). 백과의 탭이 된다")]
        public string category;

        [Tooltip("같은 분류 안에서의 순서. 표의 order 그대로")]
        public int order;

        public string titleKey;
        public string summaryKey;
        public string bodyKey;

        public HelpTrigger trigger = HelpTrigger.None;

        [Tooltip("계기에 딸린 값. 지금은 ErosionReached 의 «침식 50» 하나만 쓴다")]
        public int triggerArg;

        [Tooltip("1 = 그 순간 알아야 한다 · 2 = 보통 · 3 = 급하지 않다. " +
                 "같은 순간에 둘이 걸리면 이 값이 순서를 정한다")]
        [Range(1, 3)] public int priority = 2;

        [Tooltip("한 번 보고 나면 다시 뜨지 않는다. 표의 show_once 는 전부 1 이다")]
        public bool showOnce = true;

        [Tooltip("같이 읽으면 좋은 항목의 help_id. 백과 상세에 버튼으로 뜬다")]
        public string seeAlso;

        /// <summary>
        /// ★★★ 「자세히 보기」가 <b>열어야 하는 창</b>의 씬 경로 (2026-08-24 · 유저 지시:
        /// *"자세히 보기를 누르면 … <b>해당 ui를 직접 띄워서</b> 설명하는 방식"*).
        ///
        /// 예 <c>UI_Root/HUD_Tactics</c>. 창을 열고 나서 <see cref="HelpStepRow.targetPath"/> 가
        /// 가리키는 <b>그 창 안의 영역</b>들을 차례로 짚는다.
        ///
        /// ★ <b>비워도 된다</b> — 그때는 창을 열지 않고, 이미 화면에 있는 HUD 를 바로 짚는다
        ///   (에너지 · 웨이브 · 배속처럼 늘 보이는 것들).
        /// ⚠ <b>내가 연 창만 내가 닫는다</b> — 유저가 이미 열어 두었으면 안내가 끝나도 열린 채
        ///   둔다(<see cref="ReadingPause"/> 와 같은 소유권 규칙이다).
        /// </summary>
        [Tooltip("「자세히 보기」가 열어야 하는 창의 씬 경로. 비우면 창을 열지 않고 화면의 HUD 를 짚는다")]
        public string openPanelPath;

        /// <summary>이름표. 표에 키가 없으면 <c>helpId</c> 를 그대로 보여준다(어느 줄인지 알 수 있게).</summary>
        public string Title => StringTable.Get(titleKey, helpId);

        /// <summary>조언 카드에 뜨는 <b>두어 줄</b>.</summary>
        public string Summary => StringTable.Get(summaryKey, "");

        /// <summary>백과에 뜨는 <b>본문</b>. <c>&lt;b&gt;</c> 태그가 들어 있다 — 리치 텍스트를 끄지 말 것.</summary>
        public string Body => StringTable.Get(bodyKey, "");
    }

    /// <summary>
    /// ★★★ <b>화면에서 짚어 주는 한 단계</b> — 표 <c>HelpStep</c> 시트의 한 줄
    /// (2026-08-24 신설 · 유저 지시: *"자세히 보기에서 실제 ui로 연결하고 <b>빨간 테두리 선으로
    /// 하나하나 설명</b>해주는 기능 넣어주고"*).
    ///
    /// <b>글로만 설명하는 것과 무엇이 다른가</b> — 「강화」가 무엇인지 읽어도 <b>어디를 눌러야
    /// 하는지</b>는 모른다. 그래서 이 줄은 «무엇을 말하는가»(<see cref="stepText"/>) 와
    /// «화면의 어디인가»(<see cref="targetPath"/>) 를 <b>함께</b> 들고 있다.
    ///
    /// ★ <see cref="targetPath"/> 는 <b>씬의 경로</b>다(예 <c>UI_Root/HUD_Tactics/Col1/Pos</c>).
    ///   이 프로젝트는 UI 참조를 이름으로 찾는 것이 관례다(MCP 로는 인스펙터 참조를 넣지 못한다).
    /// ★ <b>비워도 된다</b> — 그 단계는 짚을 곳 없이 글만 보여준다(조작법처럼 화면의 칸이 아닌 것).
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★★ 한 항목의 단계는 <b>한 UI 안에서만</b> 머문다
    /// ══════════════════════════════════════════════════════════════════
    /// 2026-08-24 유저 지시: *"듀토리얼 이벤트의 배선이 어수선해 … <b>해당 ui를 직접 띄워서</b>
    /// 설명하는 방식으로 만들어야 하고"* · *"전술 지침을 누르면 … 거기서 자세히 보기를 누르면
    /// <b>실제 전술 지침 ui를 띄워놓고 각 영역에 대해</b> 빨간색 테두리로 설명"*.
    ///
    /// 처음 초안은 단계마다 <b>다른 HUD</b>를 짚었다 — 「전술 지침 버튼」 → 「로스터」 → 「기록창」.
    /// 눈이 화면을 세 번 건너뛰고, 정작 <b>전술 지침 창 안에 무엇이 있는지</b>는 알려주지 않았다.
    /// 그것이 «어수선하다» 의 정체였다.
    ///
    /// → 이제 <see cref="HelpEntry.openPanelPath"/> 가 창을 <b>열고</b>, 단계들은 <b>그 창 안의
    ///   영역</b>만 짚는다. 창이 없는 항목은 <b>늘 보이는 HUD 하나</b> 안에서만 짚는다.
    ///   <c>gen_help_assets.py</c> 가 이 규칙을 <b>검산한다</b>.
    /// </summary>
    [System.Serializable]
    public class HelpStepRow
    {
        [Tooltip("어느 항목의 단계인가 — Help 시트의 help_id")]
        public string helpId;

        [Tooltip("단계 순서(1 부터). 이 순서로 넘어간다")]
        public int stepOrder;

        [Tooltip("짚을 곳의 씬 경로. 비우면 짚지 않고 글만 보여준다")]
        public string targetPath;

        [Tooltip("그 칸이 무엇이고 무엇을 하는지 — 한두 문장")]
        [TextArea(2, 4)] public string stepText;
    }

    /// <summary>
    /// ★★★ <b>도움말(튜토리얼) 표</b> (2026-08-24 신설 · 볼트
    /// <c>데이터 테이블/Last_Sanctuary_도움말테이블_Ver01.xlsx</c> 의 <c>Help</c> 시트).
    ///
    /// <b>왜 이 모양인가</b> — 유저 지시가 *"문명 듀토리얼처럼 도움말처럼 구성"* 이었다.
    /// 문명의 조언자는 <b>같은 글 한 벌</b>을 두 곳에 쓴다:
    /// <code>
    ///   조언 카드      : 그 상황이 처음 왔을 때 게임을 멈추고 «요약» 두어 줄을 보여준다
    ///   백과(도움말 창) : 언제든 F1 로 열어 «본문» 전체를 다시 읽는다
    /// </code>
    /// 그래서 항목마다 <b>요약과 본문을 따로</b> 들고 있다. 별도 씬·별도 진행도를 만들지 않는
    /// 이유도 같다 — 튜토리얼이 «본편과 다른 판» 이 되면 두 벌을 유지해야 한다.
    ///
    /// ⚠ <b>손으로 고치지 말 것</b> — <c>Tools/gen_help_assets.py</c> 가 표에서 다시 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/도움말 표", fileName = "HelpTable")]
    public class HelpTableSO : ScriptableObject
    {
        /// <summary><c>Resources.Load</c> 경로 (확장자 없음).</summary>
        public const string ResourcePath = "Data/Help/HelpTable";

        public List<HelpEntry> entries = new List<HelpEntry>();

        /// <summary>
        /// 화면에서 짚어 주는 단계들 — 표 <c>HelpStep</c> 시트. 항목 하나에 여러 줄이 붙는다.
        /// ★ <b>없는 항목이 있어도 된다</b> — 짚을 곳이 없는 개념(명중·크리티컬 같은 규칙)은
        ///   글로만 설명한다. 그때는 「자세히 보기」가 백과를 여는 예전 동작으로 돌아간다.
        /// </summary>
        public List<HelpStepRow> steps = new List<HelpStepRow>();

        static HelpTableSO _loaded;
        static bool _warned;

        /// <summary>
        /// Resources 에서 한 번만 읽어 들고 있는다.
        /// ★ <see cref="HelpService"/> 와 도움말 창이 <b>같은 표</b>를 봐야 하므로
        ///   각자 로드하지 않고 여기 한 곳에서 받는다.
        /// </summary>
        public static HelpTableSO Load()
        {
            if (_loaded != null) return _loaded;

            _loaded = Resources.Load<HelpTableSO>(ResourcePath);
            if (_loaded == null && !_warned)
            {
                _warned = true;
                Debug.LogWarning($"[도움말] Resources/{ResourcePath} 를 찾지 못했습니다. " +
                                 "py -3 Tools/gen_help_assets.py 를 돌려 구우세요.");
            }
            return _loaded;
        }

        /// <summary>표의 문자열 → enum. 모르는 값은 <see cref="HelpTrigger.None"/>.</summary>
        public static HelpTrigger ParseTrigger(string raw)
        {
            string s = (raw ?? "").Trim();
            if (s.Length == 0) return HelpTrigger.None;

            // 표에는 enum 이름 그대로 들어 있다. 대소문자만 눈감아 준다.
            if (System.Enum.TryParse(s, ignoreCase: true, out HelpTrigger t)) return t;
            return HelpTrigger.None;
        }

        public HelpEntry ById(string helpId)
        {
            if (string.IsNullOrEmpty(helpId)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].helpId == helpId) return entries[i];
            return null;
        }

        /// <summary>그 계기에 걸린 항목들. 급한 것(priority 1)이 먼저 온다.</summary>
        public void CollectByTrigger(HelpTrigger trigger, List<HelpEntry> into)
        {
            if (into == null) return;
            into.Clear();
            if (trigger == HelpTrigger.None) return;

            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].trigger == trigger) into.Add(entries[i]);

            into.Sort((a, b) => a.priority != b.priority
                              ? a.priority.CompareTo(b.priority)
                              : a.order.CompareTo(b.order));
        }

        /// <summary>
        /// 그 항목의 «화면에서 짚어 주기» 단계들을 <c>step_order</c> 순으로. 없으면 빈 목록.
        /// </summary>
        public void CollectSteps(string helpId, List<HelpStepRow> into)
        {
            if (into == null) return;
            into.Clear();
            if (string.IsNullOrEmpty(helpId)) return;

            for (int i = 0; i < steps.Count; i++)
                if (steps[i] != null && steps[i].helpId == helpId) into.Add(steps[i]);
            into.Sort((a, b) => a.stepOrder.CompareTo(b.stepOrder));
        }

        /// <summary>그 항목에 짚어 줄 단계가 하나라도 있는가 — 버튼 문구를 가르는 데 쓴다.</summary>
        public bool HasSteps(string helpId)
        {
            if (string.IsNullOrEmpty(helpId)) return false;
            for (int i = 0; i < steps.Count; i++)
                if (steps[i] != null && steps[i].helpId == helpId) return true;
            return false;
        }

        /// <summary>분류 이름을 <b>표에 나온 순서</b>대로. 탭의 순서가 된다.</summary>
        public List<string> Categories()
        {
            var list = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                string c = entries[i] != null ? entries[i].category : null;
                if (!string.IsNullOrEmpty(c) && !list.Contains(c)) list.Add(c);
            }
            return list;
        }

        /// <summary>그 분류의 항목을 <c>order</c> 순으로.</summary>
        public void CollectByCategory(string category, List<HelpEntry> into)
        {
            if (into == null) return;
            into.Clear();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].category == category) into.Add(entries[i]);
            into.Sort((a, b) => a.order.CompareTo(b.order));
        }

        /// <summary>도메인 리로드가 꺼져 있어도 플레이할 때마다 다시 읽게 한다(이 프로젝트의 static 규칙).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = null;
            _warned = false;
        }
    }
}
