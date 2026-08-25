using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Events
{
    /// <summary>
    /// 이벤트 <b>발동 조건</b> — 표 <c>Condition</c> 시트의 <c>cond_id</c> 3종.
    ///
    /// ★★ <b>Ver013 에서 <c>EventKind</c>(5001/5002/5003) 를 대체했다.</b> 옛 표는
    /// <c>EventType</c> 시트에 «타입 id» 를 두고 그 숫자를 그대로 캐스팅했는데,
    /// Ver013 은 시트를 지우고 <b>조건 이름</b>(<c>wave_end</c> …)을 <c>Event.trigger_cond</c>
    /// 칸에 직접 적는다. 그래서 이 enum 은 <b>숫자가 아니라 이름</b>으로 판다
    /// (<see cref="EventDefinitionSO.Trigger"/>).
    ///
    /// ⚠ <b>표에 없는 이름이 오면 <see cref="None"/> 이다</b> — 그리고 그 이벤트는 절대
    ///   뽑히지 않는다. 조용히 «웨이브» 로 떨어지면 기획이 오타를 냈을 때 알 수 없다.
    /// </summary>
    public enum EventTrigger
    {
        /// <summary>표에 없는/못 읽은 조건. 이 이벤트는 뽑히지 않는다.</summary>
        None = 0,

        /// <summary>
        /// <c>wave_end</c> — 웨이브가 <b>완전히</b> 끝난 순간.
        /// ⚠ 표가 못박은 정의: *"광폭화(Enrage)까지 모두 종료되어 정비 시간으로 넘어가는 프레임"*.
        ///   즉 <b>전투 단계 진입이 아니다</b>(Ver012 구현은 그랬다 — Ver013 이 고친 지점).
        /// </summary>
        WaveEnd = 1,

        /// <summary><c>private_timer</c> — 화면에 안 보이는 180초 타이머가 한 바퀴 돌 때.</summary>
        PrivateTimer = 2,

        /// <summary>
        /// <c>habitat_contact</c> — <c>trigger_value</c> 에 적힌 중립 몬스터의 서식지에
        /// 캐릭터가 <b>최초로</b> 인접한 순간. 확률·가중치를 쓰지 않고 <b>100% 발동</b>한다.
        /// </summary>
        HabitatContact = 3,
    }

    /// <summary>
    /// 선택지 하나 — 표 <c>ChoiceGroup</c> 시트의 한 행.
    ///
    /// ★★ <b>Ver013 에서 <c>EventLine</c>(대사 사슬)을 대체했다.</b> Ver012 는 대사를
    /// «줄 단위 그래프»(<c>next_dialogue_id_01/02</c> 로 이어지는)로 굴렸는데, Ver013 은
    /// 그 구조를 <b>버렸다</b>: 이벤트 본문은 <see cref="EventDefinitionSO.eventScript"/>
    /// 한 칸에 통째로 들어가고, <b>분기는 대사가 아니라 선택지가</b> 담당한다.
    ///
    /// <b>흐름</b>(Info 시트 «화면 흐름»):
    /// <code>
    ///   event_bg 배경 + event_script 본문  →  choice_text 버튼 N개
    ///     → 누르면 reward_type_01/02 적용  →  result_script + result_effect 를 결과창에
    ///     → 결과창을 닫으면 이벤트 종료
    /// </code>
    ///
    /// ★ <b>보상이 «타입 + 수치 + 지속시간» 세 칸</b>인 것이 Ver012 와의 가장 큰 차이다.
    ///   옛 표는 지속시간이 «이벤트가 끝날 때까지» 라는 <b>상대값</b>이었는데, 이벤트가
    ///   웨이브 <b>종료 시</b> 에 뜨게 바뀌면서 그 기준점이 사라졌다. 그래서 <b>초</b>로 못박는다.
    ///
    /// ⚠ 칸 이름은 <b>표의 영문 헤더를 그대로</b> 옮겼다 — 표와 코드를 나란히 놓고 대조할
    ///   수 있어야 한다(이 프로젝트가 스킬 <c>value_01</c> 을 그렇게 쓰고 있다).
    /// </summary>
    [System.Serializable]
    public class EventChoice
    {
        [Tooltip("choice_id — 300001~399999")]
        public int choiceId;

        [Tooltip("choice_order — 버튼을 놓는 순서(1 부터)")]
        public int choiceOrder;

        [Tooltip("choice_text — 버튼에 넣는 문구. <b>반말</b>이고 효과를 수치로 알려주지 않는다")]
        [TextArea(1, 3)] public string choiceText = "";

        [Tooltip("result_script — 고른 뒤 결과창에 뜨는 대사. 존댓말·은유")]
        [TextArea(1, 4)] public string resultScript = "";

        [Tooltip("result_effect — 결과창의 효과 요약. <b>여기서 처음 수치가 보인다</b>")]
        [TextArea(1, 3)] public string resultEffect = "";

        // ★★ 스트링 키 (2026-08-25) — 위 셋은 이제 <b>폴백</b>이다. 아래 doc 참고.
        //   ⚠ 키의 꼬리는 `choice_group_id` 가 아니라 <b>`choice_id`</b> 다 — 그룹 하나에
        //     선택지가 여럿이라 그룹으로 묶으면 서로 덮어쓴다.
        [Tooltip("event_choice_text_<choice_id>")]
        public string choiceTextKey = "";

        [Tooltip("event_result_script_<choice_id>")]
        public string resultScriptKey = "";

        [Tooltip("event_result_effect_<choice_id>")]
        public string resultEffectKey = "";

        /// <summary>버튼 문구 — 스트링 키가 있으면 그쪽이 정본이다.</summary>
        public string ChoiceText => Data.StringTable.Get(choiceTextKey, choiceText);

        /// <summary>결과창 대사.</summary>
        public string ResultScript => Data.StringTable.Get(resultScriptKey, resultScript);

        /// <summary>결과창 효과 요약.</summary>
        public string ResultEffect => Data.StringTable.Get(resultEffectKey, resultEffect);

        [Tooltip("reward_type_01 — RewardType 시트의 enum 이름")]
        public string rewardType01 = "";

        [Tooltip("reward_value_01 — 수치(대개 %, 에너지·침식은 절대값)")]
        public int rewardValue01;

        [Tooltip("reward_duration_01 — 유지 시간(초). 즉시 효과는 0")]
        public int rewardDuration01;

        [Tooltip("reward_type_02 — 둘째 보상. 비면 보상 하나만 적용한다")]
        public string rewardType02 = "";

        [Tooltip("reward_value_02")]
        public int rewardValue02;

        [Tooltip("reward_duration_02")]
        public int rewardDuration02;

        /// <summary>둘째 보상 칸이 채워져 있는가.</summary>
        public bool HasSecondReward => !string.IsNullOrWhiteSpace(rewardType02);
    }

    /// <summary>
    /// 이벤트 하나 — 표 <c>Event</c> 시트의 한 행 + 그 이벤트의 <c>ChoiceGroup</c> 행 전부.
    ///
    /// <b>표가 정본이다.</b> 이 에셋은 <c>Tools/gen_event_assets.py</c> 가 만든다 —
    /// 값을 인스펙터에서 손으로 고치지 말 것(다시 생성하면 지워진다).
    /// <b>사람이 정하는 값</b>(자연 발생 확률 등)은 에셋이 아니라
    /// <see cref="EventService"/> 의 인스펙터에 있다.
    ///
    /// ★★ <b>Ver013 (2026-08-21)</b> — 유저 지시: *"이벤트 테이블 수정된거 읽어보고 다시
    /// 인게임에 구현"*. 표에서 <c>Dialogue</c>·<c>EventType</c>·<c>Switch</c> 시트가 사라지고
    /// <c>ChoiceGroup</c> 이 생겼다. 대응하는 코드 변화:
    ///
    /// | Ver012 | Ver013 |
    /// |---|---|
    /// | <c>eventType</c> 5001/5002/5003 | <see cref="triggerCond"/> 이름 3종 |
    /// | <c>lines</c> (대사 사슬) | <see cref="eventScript"/> 한 칸 + <see cref="choices"/> |
    /// | <c>startSwitch/endSwitch</c> | <see cref="repeatable"/> 불리언 하나 |
    /// | <c>value01</c>(타이머 길이) | 지워짐 — 코드가 아는 값이다 |
    /// | <c>value02</c>(가중치) | <see cref="weight"/> |
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/이벤트 정의", fileName = "Event_")]
    public class EventDefinitionSO : ScriptableObject
    {
        [Header("Event 시트")]
        [Tooltip("event_id — 205001~ wave_end · 206001~ private_timer · 207001~ habitat_contact")]
        public int eventId;

        [Tooltip("event_name")]
        public string eventName = "";

        [Tooltip("trigger_cond — wave_end / private_timer / habitat_contact")]
        public string triggerCond = "";

        [Tooltip("trigger_value — habitat_contact 일 때 <b>중립 몬스터 id</b>" +
                 "(1101 카르시노스 · 1102 아니사킬 · 1103 바리올라 · 1104 폴리르). 그 밖에는 0")]
        public int triggerValue;

        [Tooltip("weight — <b>가중치</b>. 같은 조건끼리 이 비율로 뽑힌다. " +
                 "habitat_contact 는 0 이다(확률을 쓰지 않는다)")]
        public int weight;

        [Tooltip("repeatable — 켜지면 쿨타임 뒤 다시 후보가 된다. 꺼지면 한 판에 한 번뿐")]
        public bool repeatable = true;

        [Tooltip("choice_group_id — ChoiceGroup 시트의 그룹 id (이벤트와 1:1)")]
        public int choiceGroupId;

        [Tooltip("event_bg — 본문 뒤에 깔 배경 이미지 이름. 아직 그림이 없는 키다")]
        public string eventBg = "";

        [Tooltip("event_script — <b>이벤트 본문</b>. 여러 줄이 한 묶음으로 한 번에 출력된다")]
        [TextArea(3, 12)] public string eventScript = "";

        // ══════════════════════════════════════════════════════════════
        //  ★★★ 스트링 키 (2026-08-25 신설 — 유저: *"이벤트랑 유물 테이블도
        //      스트링 키 테이블 연동"*)
        // ══════════════════════════════════════════════════════════════
        // 사건은 <b>이 게임에서 글이 가장 많은 곳</b>이다(본문 43 · 선택지 86 · 결과 172).
        // 그 전부가 스트링 키 테이블 <b>밖</b>에 있었다 — 51절이 «모든 테이블 문구를 한
        // 파일로» 라고 세운 방향에서 이 표만 빠져 있었다(표가 124절에 <b>나중에</b> 생겼다).
        //
        // ⚠ 위 <see cref="eventName"/>·<see cref="eventScript"/> 는 이제 <b>폴백</b>이다.
        //   문구는 <b>이벤트 표</b>에서 고치고 `gen_string_table.py` 를 돌린다.

        [Header("스트링 키")]
        [Tooltip("event_name_<event_id> — 비어 있으면 eventName 을 그대로 쓴다")]
        public string nameKey = "";

        [Tooltip("event_script_<event_id> — 비어 있으면 eventScript 를 그대로 쓴다")]
        public string scriptKey = "";

        /// <summary>사건 이름 — 창의 제목과 전투 기록에 나온다.</summary>
        public string DisplayName => Data.StringTable.Get(nameKey, eventName);

        /// <summary>사건 본문.</summary>
        public string Script => Data.StringTable.Get(scriptKey, eventScript);

        [Header("ChoiceGroup 시트 (이 이벤트 것만 · choice_order 순)")]
        public List<EventChoice> choices = new List<EventChoice>();

        // ------------------------------------------------------------------

        /// <summary>
        /// <see cref="triggerCond"/> 문자열을 enum 으로 판다.
        ///
        /// ★ <b>표의 이름을 그대로</b> 비교한다 — 매핑표를 따로 두면 표에 조건이 늘 때
        ///   두 곳을 고쳐야 한다(이 프로젝트가 스킬 유형에서 겪은 일이다).
        /// ⚠ 못 읽으면 <see cref="EventTrigger.None"/> — 그 이벤트는 뽑히지 않는다.
        /// </summary>
        public EventTrigger Trigger
        {
            get
            {
                switch ((triggerCond ?? "").Trim())
                {
                    case "wave_end": return EventTrigger.WaveEnd;
                    case "private_timer": return EventTrigger.PrivateTimer;
                    case "habitat_contact": return EventTrigger.HabitatContact;
                    default: return EventTrigger.None;
                }
            }
        }

        /// <summary>가중 추첨에 쓰는 무게. 0 이면 <b>가중 추첨으로는 안 뽑힌다</b>.</summary>
        public int Weight => Mathf.Max(0, weight);

        /// <summary>
        /// 쓸 수 있는 정의인가 — id 가 있고, 조건을 읽었고, 선택지가 있어야 한다.
        /// ⚠ 선택지가 없으면 창을 띄워도 <b>닫을 수가 없다</b>(버튼이 안 생긴다).
        /// </summary>
        public bool IsUsable => eventId != 0 && Trigger != EventTrigger.None && choices.Count > 0;

        /// <summary>선택지를 <c>choice_order</c> 순으로 돌려준다. 표가 이미 정렬돼 있어도 한 번 더 본다.</summary>
        public List<EventChoice> OrderedChoices()
        {
            choices.Sort((a, b) => a.choiceOrder.CompareTo(b.choiceOrder));
            return choices;
        }

        /// <summary>번호로 선택지를 찾는다(0 = 첫째). 없으면 null.</summary>
        public EventChoice ChoiceAt(int index)
        {
            List<EventChoice> list = OrderedChoices();
            return index >= 0 && index < list.Count ? list[index] : null;
        }
    }
}
