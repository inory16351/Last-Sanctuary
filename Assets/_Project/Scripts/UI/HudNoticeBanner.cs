using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Relics;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>중대 사건 배너</b> — 미니맵과 허드 액션 사이에 <b>페이드 인 텍스트</b>로
    /// 한 줄 띄운다 (2026-08-26 신설 · 유저 지시: *"미니맵과 허드 액션 UI 사이에 중대한
    /// 이벤트는 페이드 인 텍스트로 알려주는 기능 추가(유물 획득 / 중립 보스 토벌 등)"*).
    ///
    /// <code>
    ///   나타난다(fadeIn) ─ 머문다(hold) ─ 사라진다(fadeOut)
    ///        0.35초           2.4초           0.7초
    /// </code>
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★ <b>«무엇이 중대한 사건인가» 를 여기서 정한다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 유물·전투 코드에 <c>HudNotice.Show(...)</c> 를 심지 않는다. 그러면 그 시스템들이
    /// <b>배너를 알아야</b> 하게 되고, 알림을 하나 더 넣을 때마다 남의 파일을 고치게 된다.
    /// 대신 <b>이미 있는 public 이벤트</b>에 붙는다 — <see cref="Help.HelpService"/> 가
    /// 튜토리얼에서 택한 것과 <b>같은 방향</b>이고, 그 판단은 여기서도 그대로 맞다.
    ///
    /// <list type="bullet">
    ///   <item><see cref="RelicInventory.OnGranted"/> — 유물을 <b>새로</b> 얻었다.
    ///         ★ 주는 통로가 넷(발굴·드랍·보스 고유·사건 보상)이라 <b>«들어오는 문»</b> 하나에
    ///         붙는다. 통로마다 붙이면 다섯 번째가 생기는 날 반드시 빠뜨린다
    ///         (<see cref="RelicInventory.Grant"/> 의 ★★★ 가 같은 이유로 세운 규칙이다).</item>
    ///   <item><see cref="DamageableUnit.OnAnyDied"/> — 에픽 중립 토벌 · 웨이브 보스 격파.</item>
    /// </list>
    ///
    /// ⚠ <b>로그를 대신하지 않는다.</b> 같은 사건이 <see cref="HudLog"/> 에도 남는다 —
    ///   배너는 지나가고 로그는 남아야 하며, 이제 로그는 50줄을 거슬러 올라가 볼 수 있다.
    ///
    /// ⚠ <b>시간은 <c>unscaledDeltaTime</c> 으로 흐른다</b> — 도움말 카드나 사건 창이
    ///   게임을 멈춰도(<see cref="ReadingPause"/>) 배너는 나타났다 사라져야 한다.
    ///   멈춘 채로 굳어 있으면 «화면이 깨졌다» 로 보인다.
    ///
    /// ⚠ MCP 로는 인스펙터 참조를 넣을 수 없어(진행상황 8절 4번) 자식을 <b>이름으로</b> 찾는다.
    ///   이 프로젝트의 모든 HUD 패널이 쓰는 방식이다.
    /// </summary>
    public class HudNoticeBanner : MonoBehaviour
    {
        [Header("하이라키 연결 (비어 있으면 이름으로 찾는다)")]
        [Tooltip("글자를 그리는 TMP. 기본 경로는 이 오브젝트의 자식 Label")]
        [SerializeField] TMP_Text label;

        [Header("연출")]
        [Tooltip("나타나는 데 걸리는 시간(초)")]
        [Min(0f)] [SerializeField] float fadeInSeconds = 0.35f;

        [Tooltip("다 나타난 뒤 머무는 시간(초)")]
        [Min(0f)] [SerializeField] float holdSeconds = 2.4f;

        [Tooltip("사라지는 데 걸리는 시간(초)")]
        [Min(0f)] [SerializeField] float fadeOutSeconds = 0.7f;

        [Tooltip("나타나는 동안 아래에서 이만큼 떠오른다(픽셀). 0 이면 제자리에서 밝아지기만 한다")]
        [SerializeField] float riseFromPixels = 14f;

        [Tooltip("한 번에 줄 세울 수 있는 알림 수. 넘치면 <b>가장 오래된 것을 버린다</b> — " +
                 "뒤늦게 뜨는 알림은 이미 지나간 사건이라 오히려 헷갈린다")]
        [Min(1)] [SerializeField] int maxQueue = 4;

        [Header("무엇을 알릴 것인가")]
        [Tooltip("유물을 새로 얻었을 때 알린다")]
        [SerializeField] bool noticeRelicGained = true;

        [Tooltip("에픽 중립 몬스터를 토벌했을 때 알린다")]
        [SerializeField] bool noticeEpicSubjugated = true;

        [Tooltip("웨이브 보스를 쓰러뜨렸을 때 알린다")]
        [SerializeField] bool noticeBossDefeated = true;

        [Header("문구")]
        [Tooltip("{0} = 유물 이름")]
        [SerializeField] string relicFormat = "유물 획득 — 「{0}」";
        [Tooltip("{0} = 몬스터 이름")]
        [SerializeField] string epicFormat = "토벌 완료 — {0}";
        [Tooltip("{0} = 보스 이름")]
        [SerializeField] string bossFormat = "{0} 격파!";

        readonly struct Item
        {
            public readonly string Text;
            public readonly HudNoticeKind Kind;
            public Item(string text, HudNoticeKind kind) { Text = text; Kind = kind; }
        }

        readonly List<Item> _queue = new List<Item>();

        CanvasGroup _group;
        RectTransform _labelRect;
        Vector2 _labelHome;

        /// <summary>지금 재생 중인 알림의 경과 시간(초). 음수면 «재생 중이 아니다».</summary>
        float _elapsed = -1f;

        void Awake()
        {
            if (label == null) label = transform.Find("Label")?.GetComponent<TMP_Text>();

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            // ★ 배너는 <b>클릭을 먹지 않는다</b> — 미니맵과 허드 액션 사이에 있어서
            //   레이캐스트를 받으면 그 둘의 조작을 가로막는다.
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            _labelRect = label != null ? label.rectTransform : null;
            if (_labelRect != null) _labelHome = _labelRect.anchoredPosition;
        }

        void OnEnable()
        {
            HudNotice.OnNotice += Enqueue;
            RelicInventory.OnGranted += HandleRelicGranted;
            DamageableUnit.OnAnyDied += HandleDied;
        }

        void OnDisable()
        {
            HudNotice.OnNotice -= Enqueue;
            RelicInventory.OnGranted -= HandleRelicGranted;
            DamageableUnit.OnAnyDied -= HandleDied;
        }

        // ------------------------------------------------------------------
        // 계기 — 이미 있는 public 이벤트에 붙는다 (맨 위 ★ 참조)
        // ------------------------------------------------------------------

        void HandleRelicGranted(RelicDefinitionSO relic)
        {
            if (!noticeRelicGained || relic == null) return;
            Enqueue(string.Format(relicFormat, relic.DisplayName), HudNoticeKind.Relic);
        }

        /// <summary>
        /// 누군가 죽었다. <b>에픽 중립</b>과 <b>웨이브 보스</b>만 알린다 —
        /// 잡몹까지 알리면 배너가 쉬지 않고 떠서 «중대한 사건» 이라는 뜻이 사라진다.
        /// </summary>
        void HandleDied(DamageableUnit unit)
        {
            if (unit == null || unit.Faction == Faction.Angel) return;

            if (noticeEpicSubjugated && unit is NeutralMonsterUnit neutral &&
                neutral.Definition != null && neutral.Definition.epic)
            {
                Enqueue(string.Format(epicFormat, unit.DisplayName), HudNoticeKind.Triumph);
                return;
            }

            if (noticeBossDefeated && unit is MonsterUnit monster && monster.IsBoss)
                Enqueue(string.Format(bossFormat, unit.DisplayName), HudNoticeKind.Triumph);
        }

        // ------------------------------------------------------------------
        // 줄 세우기 · 재생
        // ------------------------------------------------------------------

        void Enqueue(string message, HudNoticeKind kind)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            _queue.Add(new Item(message, kind));
            // 넘치면 <b>앞쪽</b>을 버린다 — 위 maxQueue 주석 참조.
            while (_queue.Count > maxQueue) _queue.RemoveAt(0);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;      // 맨 위 ⚠ — 멈춘 게임에서도 흐른다

            if (_elapsed < 0f)
            {
                if (_queue.Count == 0) return;
                Begin(_queue[0]);
                _queue.RemoveAt(0);
                return;                              // 첫 프레임은 알파 0 에서 시작한다
            }

            _elapsed += dt;

            float total = fadeInSeconds + holdSeconds + fadeOutSeconds;
            if (_elapsed >= total)
            {
                _elapsed = -1f;
                if (_group != null) _group.alpha = 0f;
                return;
            }

            if (_group != null) _group.alpha = AlphaAt(_elapsed);

            // 떠오르기 — 나타나는 동안에만 움직이고 그 뒤로는 제자리에 있는다.
            if (_labelRect != null && riseFromPixels != 0f)
            {
                float t = fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / fadeInSeconds);
                float eased = 1f - (1f - t) * (1f - t);        // ease-out — 끝에서 부드럽게 멈춘다
                _labelRect.anchoredPosition =
                    _labelHome + new Vector2(0f, -riseFromPixels * (1f - eased));
            }
        }

        void Begin(Item item)
        {
            _elapsed = 0f;

            if (label != null)
            {
                label.text = item.Text;
                label.color = HudNotice.ColorOf(item.Kind);
            }

            if (_group != null) _group.alpha = 0f;
            if (_labelRect != null)
                _labelRect.anchoredPosition = _labelHome + new Vector2(0f, -riseFromPixels);
        }

        /// <summary>경과 시간에 맞는 알파. 나타남 → 머묾(1) → 사라짐.</summary>
        float AlphaAt(float t)
        {
            if (t < fadeInSeconds)
                return fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(t / fadeInSeconds);

            float afterHold = t - fadeInSeconds - holdSeconds;
            if (afterHold <= 0f) return 1f;

            return fadeOutSeconds <= 0f ? 0f : Mathf.Clamp01(1f - afterHold / fadeOutSeconds);
        }
    }
}
