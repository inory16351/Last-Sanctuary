using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>맞은 자리에 피해량을 숫자로 띄운다</b> (2026-08-16, 유저 지시).
    ///
    /// <i>"캐릭터들 몬스터 공격할 때랑 맞을 때 피격 데미지랑 공격해서 들어가는 데미지 시각적으로
    /// 데미지 뜨게 만들어줘. 맞는 데미지랑 공격하는 데미지랑 숫자 다르게 해서 시각화 해주고
    /// 크리티컬도 색 다르게 하고"</i>
    ///
    /// ★ <b>세 가지를 색으로 가른다</b> — 플레이어 입장에서 뜻이 다른 숫자이기 때문이다:
    /// <code>
    ///   가한 피해   (맞은 쪽이 적)    흰색 · 보통 크기      "내가 얼마나 넣고 있나"
    ///   받은 피해   (맞은 쪽이 아군)  붉은색 · 크게 · 굵게  "지금 누가 위험한가"
    ///   치명타                        주황금색 · 더 크게    "운이 터졌다"
    /// </code>
    /// <b>받은 피해를 더 크게</b> 두는 것이 핵심이다. 난전에서 숫자가 쏟아질 때 <b>눈이 먼저
    /// 가야 하는 쪽</b>은 내가 맞고 있다는 사실이지, 내가 넣는 딜이 아니다.
    ///
    /// ★ <b>어디에 붙어 있나 — <c>GameSystems</c></b> (2026-08-16 이관)
    /// -------------------------------------------------------------
    /// 처음에는 <see cref="CombatProjectileFx"/> 처럼 <see cref="Bootstrap"/> 이 만드는
    /// <b>숨은 오브젝트</b>에만 붙였다. 그런데 그러면 <c>[SerializeField]</c> 를 달아놔도
    /// <b>인스펙터에서 고칠 자리가 없다</b> — 값이 코드 기본값으로 굳는다
    /// (유저 지적 2026-08-16: <i>"딜 타일이랑 테이큰 타일 어디서 고침"</i>).
    ///
    /// 크기·색·수명은 <b>화면에서 보고 맞추는 값</b>이라 반드시 에딧 모드에서 만질 수 있어야
    /// 한다(유저가 서식지 값에 대해 이미 확정한 원칙 — "타일 계산 값들은 에딧에서 수정할 수
    /// 있도록"). 그래서 <c>GameSystems</c> 오브젝트에 올렸다 —
    /// <c>ErosionService</c> · <c>CharacterUpgradeService</c> 와 같은 자리다.
    ///
    /// <see cref="Bootstrap"/> 은 <b>안전망으로 남겨둔다</b>: 씬에서 컴포넌트가 빠지는 사고가
    /// 이 저장소에서 실제로 두 번 있었다(28-3·28-4절 브랜치 재동기화). 씬에 있으면 그쪽
    /// <c>Awake</c> 가 먼저 돌아 <c>_instance</c> 를 채우므로 Bootstrap 은 조용히 물러난다.
    ///
    /// 폰트는 <c>Resources</c> 에서 읽는다(MCP 로는 씬에 오브젝트 참조를 넣을 수 없다 — 8절 4번).
    ///
    /// ★ <b>월드 공간 TMP</b>(UGUI 가 아니라 <see cref="TextMeshPro"/>)를 쓴다. 캔버스에 그리면
    /// 매 프레임 월드→스크린 변환을 해야 하고 유닛과의 앞뒤 관계가 깨진다. 월드에 두면
    /// 카메라가 움직여도 알아서 따라오고 정렬도 스프라이트와 같은 규칙으로 맞출 수 있다.
    ///
    /// ⚠ <b>같은 프레임에 여러 번 맞으면 숫자가 겹친다</b> — 그래서 좌우로 조금씩 흩고
    /// (<see cref="spreadTiles"/>), 같은 대상에게 연달아 뜨면 위로 조금 더 올려 시작한다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// <b>2026-08-17 확장 — 네 가지가 더 붙었다</b> (유저 지시)
    /// ══════════════════════════════════════════════════════════════════
    /// <code>
    ///   회복      초록 · "+N"      DamageableUnit.OnAnyHealed
    ///   빗나감    회색 · "빗나감"  DamageableUnit.OnAnyMissed
    ///   정신이상  빨강 · 흔들림    ErosionService.OnMentalErrorTriggered  (나쁜 효과)
    ///   정신이상  노랑 · 떠오름    ErosionService.OnMentalErrorTriggered  (좋은 효과)
    ///   영웅각성  금빛 · 떠오름    HeroAwakeningService.OnAwakened
    /// </code>
    ///
    /// ★ <b>왜 새 컴포넌트를 만들지 않았나</b> — 넷 다 "월드 좌표에 글자를 잠깐 띄운다"로
    /// 똑같다. 따로 만들면 <b>폰트 로드·풀링·개수 상한·정렬 레이어·외곽선</b>을 벌 수만큼
    /// 복제하게 되고, 난전에서 상한이 각자 놀아 프레임 관리가 무너진다. 이 저장소가
    /// <c>ErosionGaugeView</c> 를 세 패널이 공유하게 만든 것과 같은 판단이다(29-4절).
    /// 달라지는 것은 <b>움직이는 방식뿐</b>이라 <see cref="PopupStyle"/> 하나로 갈랐다.
    ///
    /// ⚠ <b>크기는 2026-08-17 에 전부 절반 가까이 줄였다</b>
    /// (유저 지시: <i>"숫자 크기 너무 크니까 적절하게 조절"</i>).
    /// 이전 값(가한 1.1 · 받은 1.45타일)은 캐릭터(약 2.2타일)의 <b>절반~2/3</b> 이라
    /// 난전에서 유닛이 숫자에 파묻혔다. 지금은 1/4~1/3 이다.
    /// <b>글자를 줄이면 <see cref="stackTiles"/>·<see cref="spreadTiles"/>·
    /// <see cref="riseTilesPerSecond"/> 도 같이 줄여야 한다</b> — 안 그러면 작은 글자가
    /// 큰 간격으로 흩어져 어느 유닛의 숫자인지 알 수 없게 된다.
    /// </summary>
    public class DamageNumberFx : MonoBehaviour
    {
        /// <summary>
        /// 떠오르는 방식. <b>뜻이 다른 알림은 움직임도 달라야</b> 곁눈으로도 구분된다 —
        /// 색만 다르면 난전에서 색을 읽을 새가 없다.
        /// </summary>
        enum PopupStyle
        {
            /// <summary>위로 곧게 올라가며 끝에서 사라진다. 피해·회복·빗나감.</summary>
            Float,

            /// <summary>제자리에서 <b>부르르 떨린다</b>. 나쁜 정신 이상 — "지금 이상하다".</summary>
            Shake,

            /// <summary>아래에서 <b>천천히 떠오르며 페이드 인</b>. 좋은 정신 이상 — 잔잔하게.</summary>
            RiseIn,
        }

        // ── 표시 규칙 (인스펙터가 없으므로 상수로 둔다 — Bootstrap 이 만드는 오브젝트다) ──

        [Header("색")]
        [Tooltip("적에게 <b>가한</b> 피해")]
        [SerializeField] Color dealtColor = new Color(0.96f, 0.96f, 0.92f, 1f);

        [Tooltip("아군이 <b>받은</b> 피해 — 가장 먼저 눈에 들어와야 한다")]
        [SerializeField] Color takenColor = new Color(1f, 0.34f, 0.34f, 1f);

        [Tooltip("치명타 — 가한 쪽에서 터졌을 때")]
        [SerializeField] Color criticalColor = new Color(1f, 0.78f, 0.24f, 1f);

        [Tooltip("치명타를 <b>맞았을</b> 때. 받은 피해보다 더 밝게 해서 '아팠다'가 보이게")]
        [SerializeField] Color criticalTakenColor = new Color(1f, 0.52f, 0.68f, 1f);

        [Tooltip("<b>회복</b>량 (유저 지시 2026-08-17: \"힐 들어가는 숫자도 초록색으로\"). " +
                 "피해와 반대 방향의 사건이므로 색도 반대쪽(초록)이다")]
        [SerializeField] Color healColor = new Color(0.36f, 1f, 0.46f, 1f);

        [Tooltip("<b>빗나감</b>. 아무 일도 일어나지 않았다는 뜻이므로 채도를 죽인 회색 — " +
                 "피해·회복보다 눈에 덜 띄어야 맞다")]
        [SerializeField] Color missColor = new Color(0.78f, 0.82f, 0.88f, 1f);

        [Tooltip("<b>나쁜</b> 정신 이상 이름 (혼란·공포·광분·자해·피학·이기심·역겨움·우울)")]
        [SerializeField] Color mentalBadColor = new Color(1f, 0.24f, 0.24f, 1f);

        [Tooltip("<b>좋은</b> 정신 이상 이름 (진정·각성·고조). " +
                 "MentalErrorTypes.IsGood 이 이 셋을 가른다")]
        [SerializeField] Color mentalGoodColor = new Color(1f, 0.86f, 0.30f, 1f);

        // ──────────────────────────────────────────────────────────────────
        // 크기 — ★ <b>타일</b>로 적는다 (2026-08-16 수정)
        //
        // ⚠⚠ 처음에는 이 값을 TMP 의 <c>fontSize</c> 에 <b>그대로</b> 넣었다(1.5 / 2.1).
        //   그런데 <b>3D TextMeshPro 의 fontSize 는 월드 단위가 아니다</b> — 대략
        //   <c>fontSize / 10</c> 이 월드 높이가 된다. 그래서 실제로는 <b>0.15 타일</b>,
        //   캐릭터(2.1~2.3타일)의 <b>7%</b> 짜리 글자가 떴다. 화면에서 거의 안 보였다
        //   (유저 리포트: "데미지 숫자가 너무 작아서 안 보임 ... 진짜 너무 작음").
        //
        //   숫자를 손으로 키우면 다음에 또 같은 실수를 한다. <b>「몇 타일로 보이고 싶은지」만
        //   적고</b> 변환은 코드가 하게 했다 — 이 저장소가 유닛 크기·스킬 범위에서 이미
        //   쓰는 방식과 같다(61·66절 "크기 기준은 전부 타일").
        // ──────────────────────────────────────────────────────────────────

        [Header("크기 (타일 — 캐릭터가 약 2.2타일이다)")]
        [Tooltip("적에게 가한 피해의 글자 높이(타일). ⚠ 2026-08-17 에 1.1 → 0.55 로 줄였다 " +
                 "(유저 지시: \"숫자 크기 너무 크니까 적절하게 조절\")")]
        [Min(0.1f)] [SerializeField] float dealtTiles = 0.55f;

        [Tooltip("아군이 받은 피해. 가한 것보다 크게 — 눈이 먼저 가야 하는 쪽이다. " +
                 "⚠ 2026-08-17 에 1.45 → 0.75")]
        [Min(0.1f)] [SerializeField] float takenTiles = 0.75f;

        [Tooltip("치명타일 때 곱하는 배수")]
        [Min(1f)] [SerializeField] float criticalScale = 1.3f;

        [Tooltip("<b>회복</b>량 글자 높이(타일). 가한 피해와 같은 급 — 둘 다 '내가 잘하고 있다' 쪽이다")]
        [Min(0.1f)] [SerializeField] float healTiles = 0.6f;

        [Tooltip("<b>빗나감</b> 글자 높이(타일). 글자 수가 많으므로(3자) 숫자보다 작게 잡는다")]
        [Min(0.1f)] [SerializeField] float missTiles = 0.5f;

        [Tooltip("<b>정신 이상 이름</b> 글자 높이(타일). 드물게 뜨는 대신 " +
                 "떴을 때는 확실히 읽혀야 하므로 피해 숫자보다 크다")]
        [Min(0.1f)] [SerializeField] float mentalTiles = 0.85f;

        [Tooltip("검은 외곽선 두께(0~1). 어두운 지형 위에서도 숫자가 읽히게 한다. " +
                 "0 이면 외곽선 없음")]
        [Range(0f, 0.5f)] [SerializeField] float outlineWidth = 0.22f;

        /// <summary>
        /// 타일 → TMP <c>fontSize</c> 환산 계수. <b>실제 숫자 획의 높이</b>가 기준이다.
        ///
        /// ★ <b>어떻게 나온 값인가</b> (짐작이 아니라 폰트 에셋을 실측했다) —
        /// 3D TMP 는 글자를 이렇게 그린다:
        /// <code>
        ///   월드 높이 = 글리프 높이 / pointSize x fontSize x 0.1
        /// </code>
        /// <c>Resources/Fonts/NeoDunggeunmo SDF</c> 의 face 는
        /// <b>pointSize 32 · CapLine 20</b> 이므로 숫자(대문자 높이)는
        /// <code>
        ///   20 / 32 x fontSize x 0.1 = fontSize x 0.0625 타일
        /// </code>
        /// 즉 <b>1 타일 = fontSize 16</b>. 맵 한 칸이 1 월드 단위라 그대로 타일이 된다.
        ///
        /// ⚠ 처음에 이 계수를 <b>10</b> 으로 잡고 값도 1.5/2.1 을 <c>fontSize</c> 에 그대로
        ///   넣어서, 실제 글자가 <b>0.09 타일</b>(캐릭터의 4%)로 나왔다. 두 실수가 겹친 것이다.
        ///
        /// ⚠ <c>TMP_Text.isOrthographic</c> 을 켜면 위 식의 <c>0.1</c> 이 <b>1</b> 이 되어
        ///   글자가 <b>10배</b>가 된다. 이 프로젝트의 카메라는 직교지만 그 플래그는
        ///   <b>끈 채로 둔다</b>(기본값) — 켜면 이 계수도 같이 고쳐야 한다.
        /// </summary>
        const float FontSizePerTile = 16f;

        // ⚠ 아래 값들은 <b>글자 크기에 맞춰 같이 커져야 한다</b>. 2026-08-16 에 글자를
        //   0.15타일 → 1.1~2.0타일로 키우면서 전부 다시 잡았다 — 안 그러면 숫자끼리
        //   겹쳐 붙고 유닛 머리를 덮는다.
        [Header("움직임")]
        [Tooltip("떠 있는 시간(초). 글자가 커진 만큼 조금 길게 — 읽을 시간이 필요하다")]
        [Min(0.1f)] [SerializeField] float lifeSeconds = 0.9f;

        [Tooltip("초당 위로 올라가는 거리(타일)")]
        [SerializeField] float riseTilesPerSecond = 0.9f;

        [Tooltip("좌우로 흩는 폭(타일). 같은 프레임에 여러 숫자가 겹치는 것을 줄인다")]
        [Min(0f)] [SerializeField] float spreadTiles = 0.5f;

        [Tooltip("유닛 <b>머리 위</b> 어디에서 시작할지(타일). 유닛이 2.2타일쯤이라 " +
                 "그보다 위에서 시작해야 그림을 안 덮는다")]
        [SerializeField] float baseHeightTiles = 1.9f;

        [Tooltip("같은 대상에게 연달아 뜰 때 위로 더 올리는 간격(타일). " +
                 "글자 높이만큼 벌려야 겹쳐 읽히지 않는다")]
        [SerializeField] float stackTiles = 0.7f;

        [Header("정신 이상 문구")]
        [Tooltip("정신 이상 이름이 떠 있는 시간(초). 좋고 나쁨 <b>상관없이</b> 같은 시간이다. " +
                 "★ 유저 지시 2026-08-18: \"뭔지 유저가 확실하게 읽을 수 있도록\" — " +
                 "피해 숫자(lifeSeconds 0.9)의 <b>4배</b>다. 숫자는 못 읽어도 되지만 " +
                 "상태 이름은 반드시 읽혀야 한다")]
        [Min(0.2f)] [SerializeField] float mentalLifeSeconds = 3.6f;

        [Tooltip("<b>나쁜</b> 효과의 흔들림 폭(타일). 0 이면 안 흔들린다")]
        [Min(0f)] [SerializeField] float mentalShakeTiles = 0.16f;

        [Tooltip("<b>나쁜</b> 효과의 초당 흔들림 횟수")]
        [Min(0f)] [SerializeField] float mentalShakeHz = 13f;

        [Tooltip("떠 있는 시간 중 <b>앞쪽 몇 할</b>까지 흔들리는지 (0~1). " +
                 "★ 유저 확정 2026-08-18: <b>1 = 전 구간</b>(뜰 때부터 사라질 때까지 계속 떤다). " +
                 "진폭은 그 구간 안에서 서서히 잦아든다 — 값을 줄이면 앞쪽에서만 떨고 " +
                 "나머지는 가만히 서 있게 된다")]
        [Range(0.05f, 1f)] [SerializeField] float mentalShakeRatio = 1f;

        [Tooltip("<b>좋은</b> 효과가 아래에서 떠오르는 거리(타일). " +
                 "이만큼 아래에서 시작해 제자리까지 올라온다")]
        [Min(0f)] [SerializeField] float mentalRiseFromTiles = 0.9f;

        [Header("영웅 각성 문구 (유저 지시 2026-08-18)")]
        [Tooltip("영웅으로 각성한 순간 캐릭터 머리 위에 뜨는 문구. " +
                 "{0} = 몇 번째 각성인지 (최대 각성 횟수가 1 이면 안 쓰인다)")]
        [SerializeField] string heroAwakenText = "영웅 각성!";

        [Tooltip("각성이 2회 이상 가능할 때의 문구. {0} = 몇 번째 각성인지")]
        [SerializeField] string heroAwakenStageFormat = "영웅 각성! {0}단계";

        [Tooltip("각성 문구 색. 좋은 정신 이상(노랑)과 구분되게 <b>금빛</b> 쪽으로 잡았다 — " +
                 "같은 노랑이면 '또 각성 걸렸네' 로 흘려보게 된다")]
        [SerializeField] Color heroAwakenColor = new Color(1f, 0.72f, 0.22f, 1f);

        [Tooltip("각성 문구의 글자 높이(타일). 정신 이상 문구보다 크다 — " +
                 "한 판에 몇 번 없는 사건이라 눈에 확실히 띄어야 한다")]
        [Min(0.1f)] [SerializeField] float heroAwakenTiles = 1.15f;

        [Tooltip("각성 문구가 떠 있는 시간(초). 정신 이상 문구보다 <b>더</b> 길게 둔다")]
        [Min(0.2f)] [SerializeField] float heroAwakenLifeSeconds = 5f;

        [Header("동작")]
        [Tooltip("동시에 화면에 띄울 수 있는 최대 개수. 넘으면 가장 오래된 것부터 재활용한다 — " +
                 "난전에서 수백 개가 쌓여 프레임이 튀는 것을 막는다")]
        [Min(8)] [SerializeField] int maxLive = 64;

        [Tooltip("끄면 숫자를 아예 안 띄운다")]
        [SerializeField] bool enableNumbers = true;

        // ------------------------------------------------------------------

        class Number
        {
            public Transform Root;
            public TextMeshPro Text;
            public Vector3 From;
            public float Born;
            public float Life;
            public Color Base;
            public PopupStyle Style;

            /// <summary>
            /// 따라다닐 대상. <b>정신 이상 문구만</b> 쓴다 — "캐릭터 주변에 나타나기"라는
            /// 요구는 캐릭터가 걸어가면 문구도 같이 가야 성립한다. 피해 숫자는 반대로
            /// <b>맞은 자리에 남는 것</b>이 맞아서(어디서 맞았는지가 정보다) null 이다.
            /// </summary>
            public Transform Follow;

            /// <summary>따라다닐 때 대상으로부터의 오프셋(월드).</summary>
            public Vector3 FollowOffset;

            /// <summary>흔들림 위상. 같은 순간에 여럿이 뜨면 같은 박자로 떨려서 어색하다.</summary>
            public float Phase;
        }

        readonly List<Number> _live = new List<Number>();
        readonly Stack<Number> _pool = new Stack<Number>();

        /// <summary>대상별 "마지막으로 숫자를 띄운 시각 + 그때의 쌓임 단계".</summary>
        readonly Dictionary<DamageableUnit, (float time, int step)> _stack =
            new Dictionary<DamageableUnit, (float, int)>();

        TMP_FontAsset _font;
        System.Random _rng;

        static DamageNumberFx _instance;

        /// <summary>
        /// <b>안전망 걸기</b> — 씬이 열릴 때마다 "전투 씬인데 이 컴포넌트가 없는가"를 본다.
        ///
        /// ★★ <b>왜 씬마다 다시 보는가 — 로비 씬이 생기면서 안전망이 오작동했다</b>
        /// (2026-08-18, 유저 리포트: 로비에서 시작하자마자 아래 경고가 떴다).
        ///
        /// 예전에는 이 함수가 <c>AfterSceneLoad</c> 에 <b>한 번만</b> 돌면서 "지금 <c>_instance</c> 가
        /// 없으면 만든다"였다. 그 판단은 <b>첫 씬이 전투 씬</b>일 때만 옳다. 빌드 0번이
        /// <c>Lobby</c> 가 된 뒤로는(99-6절) 그 시점에 컴포넌트가 없는 것이 <b>정상</b>인데도
        /// 폴백을 만들고, <c>DontDestroyOnLoad</c> 라서 <b>게임 씬까지 따라갔다.</b>
        ///
        /// 그러면 <c>Proto_01</c> 의 <c>GameSystems</c> 에 붙은 진짜 컴포넌트와 <b>둘이 함께
        /// 살아남는다</b>. 둘 다 <c>Awake</c> 에서 정적 이벤트를 구독하므로 — 그리고 그 구독은
        /// <c>-=</c> 로 못 지운다(대상이 서로 다른 인스턴스다) — <b>피해 숫자가 두 번 뜬다.</b>
        /// 하나는 씬 값, 하나는 코드 기본값이라 <b>크기가 다른 숫자가 겹쳐 보인다.</b>
        ///
        /// 그래서 두 가지를 바꿨다:
        ///   ① <b>전투 씬일 때만</b> 만든다(<see cref="Wave.WaveManager"/> 가 있는가로 판정) —
        ///      로비에는 피해 숫자를 띄울 대상이 아예 없다.
        ///   ② <b><c>DontDestroyOnLoad</c> 를 쓰지 않는다</b> — 폴백은 자기가 태어난 씬과
        ///      생애를 같이 해야 한다. 씬을 넘길 때 사라져 다음 씬의 진짜 컴포넌트와 겹치지 않는다.
        ///
        /// ⚠ <c>sceneLoaded</c> 는 새 씬 오브젝트의 <c>Awake</c> <b>뒤</b> · <c>Start</c> <b>앞</b>에
        /// 돈다. 그래서 씬에 컴포넌트가 있으면 그쪽이 이미 <see cref="_instance"/> 를 채워
        /// 여기서 조용히 물러난다 — <b>인스펙터에서 조정한 값이 폴백에 덮이지 않는다.</b>
        ///
        /// 안전망 자체를 남겨두는 이유는 컴포넌트가 씬에서 빠지는 사고가 실제로 두 번
        /// 있었기 때문이다(28-3·28-4절) — 그때 데미지 숫자가 통째로 사라지는 것보다는
        /// 기본값이 낫다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // ⚠ 먼저 빼고 더한다 — 도메인 리로드를 끈 상태에서 두 번 걸리는 것을 막는다(Awake 와 같은 규칙).
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;

            // 첫 씬은 이벤트가 이미 지나갔으므로 여기서 직접 본다.
            EnsureFallbackForBattleScene();
        }

        static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                      UnityEngine.SceneManagement.LoadSceneMode mode) =>
            EnsureFallbackForBattleScene();

        static void EnsureFallbackForBattleScene()
        {
            if (_instance != null) return;

            // ★ 전투 씬이 아니면 만들지 않는다. 로비에서 만들면 그 폴백이 게임 씬까지 따라가
            //   진짜 컴포넌트와 <b>둘이</b> 구독한다(위 주석 참조).
            if (FindAnyObjectByType<Wave.WaveManager>() == null) return;

            var go = new GameObject("~DamageNumberFx");
            go.hideFlags = HideFlags.DontSave;
            _instance = go.AddComponent<DamageNumberFx>();

            Debug.LogWarning("[데미지숫자] 씬(GameSystems)에 DamageNumberFx 가 없어 " +
                             "코드 기본값으로 임시 생성했습니다 — 인스펙터에서 크기를 조정하려면 " +
                             "GameSystems 에 컴포넌트를 붙여주세요.");
        }

        void Awake()
        {
            _instance = this;
            _font = HudTheme.Font;
            _rng = new System.Random(12345);

            // ⚠ 먼저 빼고 더한다 — 도메인 리로드를 끈 상태에서 두 번 붙는 것을 막는다.
            DamageableUnit.OnAnyDamaged -= HandleDamaged;
            DamageableUnit.OnAnyDamaged += HandleDamaged;

            DamageableUnit.OnAnyHealed -= HandleHealed;
            DamageableUnit.OnAnyHealed += HandleHealed;

            DamageableUnit.OnAnyMissed -= HandleMissed;
            DamageableUnit.OnAnyMissed += HandleMissed;

            ErosionService.OnMentalErrorTriggered -= HandleMentalError;
            ErosionService.OnMentalErrorTriggered += HandleMentalError;

            HeroAwakeningService.OnAwakened -= HandleHeroAwakened;
            HeroAwakeningService.OnAwakened += HandleHeroAwakened;
        }

        void OnDestroy()
        {
            DamageableUnit.OnAnyDamaged -= HandleDamaged;
            DamageableUnit.OnAnyHealed -= HandleHealed;
            DamageableUnit.OnAnyMissed -= HandleMissed;
            ErosionService.OnMentalErrorTriggered -= HandleMentalError;
            HeroAwakeningService.OnAwakened -= HandleHeroAwakened;
            if (_instance == this) _instance = null;
        }

        // ------------------------------------------------------------------

        void HandleDamaged(DamageableUnit attacker, DamageableUnit victim, int amount, bool critical)
        {
            if (!enableNumbers || victim == null || amount <= 0) return;

            // ★ "가한 / 받은" 은 <b>맞은 쪽이 누구인지</b>로 가른다 — 플레이어 시점에서
            //   아군이 맞았으면 받은 피해고, 그 외(웨이브·중립)가 맞았으면 가한 피해다.
            //   공격자를 보지 않는 이유: 지속 피해처럼 공격자가 없는 경로가 있다.
            bool taken = victim.Faction == Faction.Angel;

            Color color = critical
                ? (taken ? criticalTakenColor : criticalColor)
                : (taken ? takenColor : dealtColor);

            float tiles = (taken ? takenTiles : dealtTiles) * (critical ? criticalScale : 1f);

            // ★ 2026-08-21 — <b>치명타에 느낌표를 붙이지 않는다</b> (유저 지시:
            //   *"크리티컬 데미지 이펙트옆에 ! 빼기"*). 치명타는 <b>색과 크기</b>로만 구분한다
            //   (criticalColor · criticalScale) — 숫자 옆의 기호는 자리만 넓히고 읽기를 방해했다.
            Show(victim, amount.ToString(), color, tiles, PopupStyle.Float, lifeSeconds);
        }

        /// <summary>
        /// <b>회복 숫자</b> — 초록 <c>+N</c> (유저 지시 2026-08-17).
        ///
        /// 진영을 가르지 않는다: 몬스터가 회복해도 초록으로 뜬다. "체력이 찼다"는 사실 자체가
        /// 플레이어에게 필요한 정보이고, 색으로 <b>피해와 반대</b>임만 알면 되기 때문이다.
        /// ⚠ 체력 재생은 여기까지 오지 않는다 — <c>DamageableUnit.HealSilently</c> 가 거른다.
        /// </summary>
        void HandleHealed(DamageableUnit unit, int amount)
        {
            if (!enableNumbers || unit == null || amount <= 0) return;

            // ★ 치명타 회복은 <b>피해 치명타와 같은 표시 규칙</b>을 쓴다 (2026-08-20 ·
            //   아르세니아 「불안정성」). 색은 «회복» 을 지키고 <b>크기와 느낌표</b>만
            //   치명타 쪽을 빌린다 — 색까지 바꾸면 «회복인지 피해인지» 가 헷갈린다.
            //   판정값은 이벤트 처리 중에만 유효하다(<see cref="DamageableUnit.PendingHealCritical"/>).
            if (unit.PendingHealCritical)
            {
                // ⚠ 느낌표는 <b>피해 쪽과 같이</b> 뺐다(위 ★) — 한쪽만 남기면 «회복 치명타만
                //   기호가 붙는» 어긋난 표시가 된다. 치명타는 크기로만 보인다.
                Show(unit, $"+{amount}", healColor, healTiles * criticalScale,
                     PopupStyle.Float, lifeSeconds);
                return;
            }
            Show(unit, $"+{amount}", healColor, healTiles, PopupStyle.Float, lifeSeconds);
        }

        /// <summary>
        /// <b>빗나감</b> — 유저 지시 2026-08-17: <i>"원거리 공격 빗나갈 경우 '빗나감' 표기"</i>.
        ///
        /// ★ 이 이벤트(<c>OnAnyMissed</c>)는 <b>2026-08-16 부터 이미 있었지만 구독자가
        /// 하나도 없었다</b> — 그래서 33-11절이 적어둔 <i>"빗나가면 화면에 아무 표시가 없어
        /// 공격이 안 먹는 버그로 보인다"</i> 가 그대로 남아 있었다. 여기가 그 자리다.
        ///
        /// ⚠ <b>실제로 빗나갈 수 있는 것은 원거리뿐</b>이다 —
        /// <c>CharacterUnit.HitChancePercent</c> 가 <c>Ranged</c> 가 아니면 100 을 돌려주므로
        /// 근거리·마법·치유는 명중 판정 자체를 타지 않는다. 즉 유저가 말한 "원거리 공격"이
        /// <b>코드에서도 이미 유일한 경우</b>라 별도의 유형 검사를 넣지 않았다.
        ///
        /// 표시 위치는 <b>피한 쪽</b>이다 — 피해 숫자와 같은 자리에 떠야 "여기서 뭔가
        /// 일어났다"가 한 줄로 읽힌다.
        /// </summary>
        void HandleMissed(DamageableUnit attacker, DamageableUnit target)
        {
            if (!enableNumbers || target == null) return;
            Show(target, "빗나감", missColor, missTiles, PopupStyle.Float, lifeSeconds);
        }

        /// <summary>
        /// <b>정신 이상 발동 문구</b> (유저 지시 2026-08-17).
        /// <code>
        ///   나쁜 효과 → 빨강 · 캐릭터 주변에서 <b>흔들린다</b>
        ///   좋은 효과 → 노랑 · 아래에서 <b>페이드 인 하며 떠오른다</b>
        /// </code>
        /// 좋고 나쁨은 <see cref="MentalErrorTypes.IsGood"/> 이 정한다(진정·각성·고조 셋이
        /// 좋은 효과다). 데이터 테이블에는 이 구분이 컬럼으로 없다 — 그쪽 주석 참조.
        ///
        /// 문구는 스트링 키 테이블에서 온다(<c>MentalErrorDefinitionSO.DisplayName</c>) —
        /// 로스터·게이지·로그가 쓰는 그 이름과 <b>같은 값</b>이라야 같은 상태로 읽힌다.
        /// </summary>
        void HandleMentalError(CharacterUnit unit, MentalErrorDefinitionSO def)
        {
            if (!enableNumbers || unit == null || def == null) return;

            bool good = MentalErrorTypes.IsGood(def.type);

            Show(unit, def.DisplayName,
                 good ? mentalGoodColor : mentalBadColor,
                 mentalTiles,
                 good ? PopupStyle.RiseIn : PopupStyle.Shake,
                 mentalLifeSeconds,
                 follow: unit.transform);
        }

        /// <summary>
        /// <b>영웅 각성 문구</b> (유저 지시 2026-08-18: <i>"영웅 각성도 정신 이상 긍정적 효과처럼
        /// 화면에 나오게"</i>).
        ///
        /// 좋은 정신 이상과 <b>같은 연출</b>(<see cref="PopupStyle.RiseIn"/> — 아래에서
        /// 잔잔하게 떠오르며 페이드 인)을 쓰되, <b>색과 크기와 시간을 따로</b> 둔다.
        /// 좋은 정신 이상은 한 판에 수십 번 뜨지만 각성은 <b>캐릭터당 한 번</b>이라,
        /// 같은 노랑·같은 크기로 뜨면 "또 그거네" 로 흘려보게 된다.
        ///
        /// ⚠ 이 이벤트는 <see cref="HeroAwakeningService"/> 가 능력치를 <b>이미 올린 뒤</b>에
        /// 쏜다 — 문구가 뜨는 순간 성장 창의 숫자도 이미 올라가 있다.
        ///
        /// ⚠ 각성은 <b>처치 판정 도중</b>(적이 죽는 그 프레임)에 일어난다. 같은 프레임에
        /// 데미지 숫자도 뜨는데, <see cref="Show"/> 의 단 쌓기가 알아서 위로 밀어준다.
        /// </summary>
        void HandleHeroAwakened(CharacterUnit unit, int awakenings)
        {
            if (!enableNumbers || unit == null) return;

            string text = awakenings > 1
                ? string.Format(heroAwakenStageFormat, awakenings)
                : heroAwakenText;

            Show(unit, text, heroAwakenColor, heroAwakenTiles,
                 PopupStyle.RiseIn, heroAwakenLifeSeconds, follow: unit.transform);
        }

        void Show(DamageableUnit victim, string text, Color color, float tiles,
                  PopupStyle style, float life, Transform follow = null)
        {
            Number n = Rent();
            if (n == null) return;

            // ── 시작 위치 ────────────────────────────────────────────────
            // 같은 대상에게 짧은 간격으로 또 뜨면 한 단 위에서 시작한다(겹쳐 읽히지 않게).
            // ⚠ 단은 3 까지만 — 4단이면 글자가 화면 위로 지나치게 솟는다.
            //
            // ★ 정신 이상 문구는 이 쌓임에 참여하지 않는다 — 드물게 한 번 뜨는 알림이라
            //   "연달아 맞았다"는 뜻의 계단을 만들 이유가 없고, 오히려 캐릭터에서 멀어진다.
            int step = 0;
            bool stacks = style == PopupStyle.Float;
            if (stacks)
            {
                if (_stack.TryGetValue(victim, out var last) &&
                    Time.time - last.time < lifeSeconds * 0.5f)
                    step = (last.step + 1) % 3;
                _stack[victim] = (Time.time, step);
            }

            float jitter = stacks
                ? (float)(_rng.NextDouble() * 2.0 - 1.0) * spreadTiles
                : 0f;

            Vector3 offset = new Vector3(jitter, baseHeightTiles + step * stackTiles, 0f);

            n.Style = style;
            n.Follow = follow;
            n.FollowOffset = offset;
            n.From = (follow != null ? follow.position : victim.transform.position) + offset;
            n.Phase = (float)(_rng.NextDouble() * Mathf.PI * 2.0);

            n.Born = Time.time;
            n.Life = Mathf.Max(0.05f, life);
            n.Base = color;

            n.Text.text = text;
            n.Text.fontSize = tiles * FontSizePerTile;
            n.Text.color = color;

            n.Root.position = n.From;
            n.Root.gameObject.SetActive(true);
            _live.Add(n);
        }

        void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Number n = _live[i];
                float t = (Time.time - n.Born) / n.Life;

                if (t >= 1f)
                {
                    n.Root.gameObject.SetActive(false);
                    n.Follow = null;                 // ⚠ 풀에 남기면 죽은 Transform 을 붙잡는다
                    _pool.Push(n);
                    _live.RemoveAt(i);
                    continue;
                }

                // 따라다니는 문구(정신 이상)는 매 프레임 대상 위치를 다시 읽는다.
                // ⚠ 대상이 파괴되면 Unity 의 == 오버로드가 null 로 답한다 — 그때는 마지막
                //   자리에 그대로 남겨둔다(중간에 사라지면 "무슨 상태였지"가 안 남는다).
                Vector3 anchor = n.Follow != null ? n.Follow.position + n.FollowOffset : n.From;
                if (n.Follow != null) n.From = anchor;

                Vector3 pos;
                float fade;

                switch (n.Style)
                {
                    case PopupStyle.Shake:
                        // 나쁜 정신 이상 — 제자리에서 좌우로 떨고 아주 조금만 뜬다.
                        // 세로 흔들림은 가로의 절반이다(같으면 원을 그려서 '떨림'이 아니라
                        // '돈다'로 보인다). 진폭은 끝으로 갈수록 줄어 자연스럽게 잦아든다.
                        {
                            // 흔들림은 <b>앞쪽 mentalShakeRatio 구간</b>에서 살아 있고 그 뒤로는 0 이다.
                            // ★ 기본값 1 = <b>전 구간</b>(유저 확정 2026-08-18) — 뜰 때부터
                            //   사라질 때까지 계속 떤다. 이때 식은 원래의 (1 - t) 와 같아진다.
                            float shakeSpan = Mathf.Max(0.01f, mentalShakeRatio);
                            float decay = Mathf.Clamp01(1f - t / shakeSpan);
                            float w = (n.Phase + Time.time * mentalShakeHz * Mathf.PI * 2f);
                            pos = anchor + new Vector3(
                                Mathf.Sin(w) * mentalShakeTiles * decay,
                                Mathf.Sin(w * 1.7f) * mentalShakeTiles * 0.5f * decay +
                                    riseTilesPerSecond * 0.25f * n.Life * t,
                                0f);
                            fade = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                        }
                        break;

                    case PopupStyle.RiseIn:
                        // 좋은 정신 이상 — 아래에서 잔잔하게 떠오르며 페이드 인.
                        // 올라오는 속도는 뒤로 갈수록 느려진다(EaseOut) — 등속으로 올리면
                        // "튀어오른다"로 보여서 '잔잔하게' 라는 요구와 어긋난다.
                        {
                            float ease = 1f - (1f - t) * (1f - t);
                            pos = anchor + new Vector3(
                                0f,
                                -mentalRiseFromTiles * (1f - ease) +
                                    riseTilesPerSecond * 0.35f * n.Life * t,
                                0f);

                            // 앞 15% 페이드 인, 뒤 25% 페이드 아웃 — 나머지 60% 는 <b>완전히
                            // 또렷하게</b> 서 있는다. 표시 시간이 3.6초로 늘면서 예전 비율(25%)
                            // 로는 페이드 인에만 0.9초가 걸려 "떠오르는 중"이 너무 길었다.
                            fade = t < 0.15f ? t / 0.15f
                                 : t > 0.75f ? 1f - (t - 0.75f) / 0.25f
                                 : 1f;
                        }
                        break;

                    default:
                        pos = n.From + new Vector3(0f, riseTilesPerSecond * n.Life * t, 0f);
                        // 뒤쪽 40% 구간에서만 사라진다 — 처음부터 흐려지면 읽기 전에 없어진다.
                        fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                        break;
                }

                n.Root.position = pos;

                Color c = n.Base;
                c.a = fade;
                n.Text.color = c;
            }
        }

        // ------------------------------------------------------------------

        Number Rent()
        {
            if (_pool.Count > 0) return _pool.Pop();

            if (_live.Count >= maxLive)
            {
                // 상한을 넘었다 — 가장 오래된 것을 뺏어 쓴다(새 숫자가 안 보이는 것보다 낫다).
                Number oldest = _live[0];
                _live.RemoveAt(0);
                return oldest;
            }

            var go = new GameObject("DamageNumber");
            go.transform.SetParent(transform, false);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = _font;
            tmp.alignment = TextAlignmentOptions.Center;
            // ⚠ enableWordWrapping 은 이 Unity 버전에서 obsolete 다 — textWrappingMode 를 쓴다.
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.autoSizeTextContainer = true;

            // ★ 가시성 (2026-08-16) — 크기만 키워서는 부족했다.
            //   · 굵게: 어두운 지형·유닛 위에서 획이 가늘면 뭉개진다
            //   · 검은 외곽선: 배경이 어두우면 붉은 글자가, 밝으면 흰 글자가 묻힌다.
            //     외곽선이 있으면 <b>배경이 무엇이든</b> 읽힌다.
            //   ⚠ outlineWidth 를 주면 TMP 가 <b>머티리얼 인스턴스</b>를 만든다. 풀에서
            //     재사용하는 64개뿐이고 만들어질 때 한 번만 설정하므로 비용이 늘지 않는다.
            tmp.fontStyle = FontStyles.Bold;
            if (outlineWidth > 0f)
            {
                tmp.outlineWidth = outlineWidth;
                tmp.outlineColor = new Color32(8, 6, 10, 255);
            }

            // 유닛보다 앞에 그린다 — 몬스터 그림에 숫자가 가리면 읽을 수가 없다.
            tmp.sortingLayerID = SortingLayer.NameToID("Overhead");
            tmp.sortingOrder = 200;

            // ⚠ 글자가 커졌으므로 담는 상자도 키운다 — 좁으면 TMP 가 줄을 접거나 잘라낸다.
            //   최대 크기(치명타로 받은 피해 = 1.45 x 1.4 ≈ 2타일)에 네 자리가 들어갈 만큼.
            var rect = go.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(16f, 5f);

            go.SetActive(false);
            return new Number { Root = go.transform, Text = tmp };
        }
    }
}
