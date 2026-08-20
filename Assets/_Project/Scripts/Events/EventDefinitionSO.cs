using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Events
{
    /// <summary>
    /// 이벤트 타입 — 표 <c>EventType</c> 시트의 <c>event_type_id</c> 를 그대로 쓴다.
    ///
    /// ★ 값을 표와 <b>같은 숫자</b>로 두는 이유 — 정의 에셋에는 표 값(5001…)이 그대로
    ///   들어오고, 코드는 그것을 <b>변환 없이</b> 캐스팅한다. 매핑표를 따로 두면
    ///   표에 타입이 늘 때 두 곳을 고쳐야 한다(이 프로젝트가 스킬 유형에서 겪은 일이다).
    /// </summary>
    public enum EventKind
    {
        None = 0,
        Wave = 5001,        // ev_wave     — 웨이브 타이머가 시작될 때
        Private = 5002,     // ev_private  — 비공개 타이머
        Raid = 5003,        // ev_raid     — 몬스터 토벌
    }

    /// <summary>
    /// 이벤트 <b>대사 한 줄</b> — 표 <c>Dialogue</c> 시트의 한 행.
    ///
    /// ★ <b>왜 별도 에셋이 아니라 중첩 클래스인가</b> — 표에는 대사가 168행 있고, 그 전부가
    ///   <c>dialogue_group_id</c> 로 <b>어느 이벤트의 것인지</b> 정해져 있다. 에셋을 168개
    ///   만들면 «어느 이벤트의 대사인가» 를 찾는 일이 매번 전수 검색이 된다.
    ///   그래서 이벤트 에셋 <b>안에</b> 그 이벤트의 대사만 넣는다(42개 에셋).
    ///
    /// ⚠ 칸 이름은 <b>표의 영문 헤더를 그대로</b> 옮겼다 — 표와 코드를 나란히 놓고
    ///   대조할 수 있어야 한다(이 프로젝트가 스킬 <c>value_01</c> 을 그렇게 쓰고 있다).
    /// </summary>
    [System.Serializable]
    public class EventLine
    {
        [Tooltip("dialogue_id")]
        public int dialogueId;

        [Tooltip("dialogue — 화면에 뿌리는 한 줄")]
        [TextArea(1, 4)] public string dialogue = "";

        [Tooltip("dialogue_start — active / wave_done / private_done / raid_done. " +
                 "빈 칸이면 «앞 줄이 이어 준다»는 뜻이다")]
        public string dialogueStart = "";

        [Tooltip("end_switch — 500005(재수락 가능) / 500002(재수락 불가). 0 이면 안 끝난다")]
        public int endSwitch;

        [Tooltip("next_dialogue_id_01 — 선택지 1(또는 그냥 다음 줄)")]
        public int nextDialogueId01;

        [Tooltip("reward_proceed_cond — choice_proceed(유저 선택) / random_proceed(확률)")]
        public string rewardProceedCond = "";

        [Tooltip("reward_proceed_value_01 — random_proceed 일 때 선택지 1이 뽑힐 확률(%)")]
        public int rewardProceedValue01;

        [Tooltip("reward_value_01 — 선택지 1의 보상 타입(enum 문자열)")]
        public string rewardValue01 = "";

        [Tooltip("reward_value_02 — 선택지 1의 보상 수치")]
        public int rewardValue02;

        [Tooltip("next_dialogue_id_02 — 선택지 2")]
        public int nextDialogueId02;

        [Tooltip("reward_value_03 — 선택지 2의 보상 타입(enum 문자열)")]
        public string rewardValue03 = "";

        [Tooltip("reward_value_04 — 선택지 2의 보상 수치")]
        public int rewardValue04;

        /// <summary>이 줄이 <b>선택지를 내는</b> 줄인가 (유저 입력을 기다린다).</summary>
        public bool IsChoice => rewardProceedCond == "choice_proceed";

        /// <summary>이 줄이 <b>확률로 갈리는</b> 줄인가 (유저 입력 없이 하나가 뽑힌다).</summary>
        public bool IsRandom => rewardProceedCond == "random_proceed";

        /// <summary>이 줄에서 이벤트가 끝나는가.</summary>
        public bool Ends => endSwitch != 0;
    }

    /// <summary>
    /// 이벤트 하나 — 표 <c>Event</c> 시트의 한 행 + 그 이벤트의 <c>Dialogue</c> 행 전부.
    ///
    /// <b>표가 정본이다.</b> 이 에셋은 <c>Tools/gen_event_assets.py</c> 가 만든다 —
    /// 값을 인스펙터에서 손으로 고치지 말 것(다시 생성하면 지워진다).
    /// <b>사람이 정하는 값</b>(자연 발생 확률 등)은 에셋이 아니라
    /// <see cref="EventService"/> 의 인스펙터에 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/이벤트 정의", fileName = "Event_")]
    public class EventDefinitionSO : ScriptableObject
    {
        [Header("Event 시트")]
        [Tooltip("event_id — 205001~ 웨이브 · 206001~ 비공개 타이머 · 207001~ 토벌")]
        public int eventId;

        [Tooltip("event_name")]
        public string eventName = "";

        [Tooltip("event_type — 5001 ev_wave · 5002 ev_private · 5003 ev_raid")]
        public int eventType;

        [Tooltip("event_value_01 — 이 이벤트가 붙는 타이머의 길이(초). 표 기준 웨이브 120 · 비공개 180")]
        public int value01;

        [Tooltip("event_value_02 — <b>가중치</b>. 같은 타입끼리 이 값의 비율로 뽑힌다")]
        public int value02;

        [Tooltip("script_group_id — Dialogue 시트의 dialogue_group_id")]
        public int scriptGroupId;

        [Tooltip("event_desc — 작업자용 요약. 화면에 쓰지 않는다")]
        [TextArea(2, 8)] public string eventDesc = "";

        [Tooltip("start_switch / end_switch — Switch 시트")]
        public int startSwitch;
        public int endSwitch;

        [Tooltip("event_bg — 대사 뒤에 깔 배경 이미지 이름")]
        public string eventBg = "";

        [Tooltip("cond / cond_value_01 / cond_value_02 — 종료 전이 조건")]
        public string cond = "";
        public int condValue01;
        public int condValue02;

        [Header("Dialogue 시트 (이 이벤트 것만)")]
        public List<EventLine> lines = new List<EventLine>();

        // ------------------------------------------------------------------

        public EventKind Kind => (EventKind)eventType;

        /// <summary>가중 추첨에 쓰는 무게. 0 이면 <b>안 뽑힌다</b>(표의 빈 칸이 그렇다).</summary>
        public int Weight => Mathf.Max(0, value02);

        /// <summary>이 이벤트의 효과가 유지되는 시간(초). 0 이면 «웨이브가 끝날 때까지».</summary>
        public float DurationSeconds => Mathf.Max(0, value01);

        public bool IsUsable => eventId != 0 && lines.Count > 0;

        /// <summary>대사 id 로 찾는다. 없으면 null.</summary>
        public EventLine Find(int dialogueId)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].dialogueId == dialogueId) return lines[i];
            return null;
        }

        /// <summary>
        /// <b>시작 줄</b> — <c>dialogue_start</c> 가 <c>active</c> 인 첫 줄.
        /// 표의 규칙이고(«발동 조건을 충족하여 진행 중인 상태»), 그룹마다 정확히 하나다.
        /// </summary>
        public EventLine FirstLine()
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].dialogueStart == "active") return lines[i];
            return lines.Count > 0 ? lines[0] : null;
        }
    }
}
