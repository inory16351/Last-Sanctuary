using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LastSanctuary.Save;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 목표 웨이브를 클리어하면 나오는 <b>엔딩 연출</b> (2026-08-25 신설 — 유저 지시
    /// <i>"엔딩도 만들어줘 필요한건 다 넣었음"</i>).
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★★★ <see cref="OpeningDirector"/> 와 <b>일부러 같은 구조</b>다
    /// ══════════════════════════════════════════════════════════════════
    /// 두 연출은 «브금이 시계 · 컷마다 배경 · 조각마다 자막+음성 · 페이드 · 건너뛰기 ·
    /// 다음 씬» 이 완전히 같다. 그래서 <b>구조를 갈라놓지 않았다</b> —
    /// <see cref="VictoryPanel"/> 과 <see cref="DefeatPanel"/> 을 같은 모양으로 둔 것과
    /// 같은 판단이고, 한쪽을 고칠 때 다른 쪽의 <b>같은 자리</b>를 보면 된다.
    ///
    /// ⚠ <b>«재생기» 를 공통 부모로 뽑지 않았다.</b> 뽑는 것이 옳지만 지금 하지 않은 이유는
    ///   <see cref="OpeningDirector"/> 의 표가 <b>씬에 직렬화</b>되어 있어서다
    ///   (그 클래스의 ⚠⚠). 필드를 부모로 옮기면 <c>Opening.unity</c> 의 저장된 대본이
    ///   살아 있는지 <b>씬 YAML 로 확인</b>해야 하고, 그것은 검증된 연출을 건드리는 일이다.
    ///   엔딩이 돌아가는 것을 본 뒤에 따로 할 일로 남긴다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  오프닝과 <b>다른</b> 것 넷
    /// ══════════════════════════════════════════════════════════════════
    /// <code>
    ///   ① 자막이 «스트링 키» 로 온다      — Caption.textKey (유저 지시: 스트링 테이블 연동)
    ///   ② 컷 2 에 «전사자 명단» 이 얹힌다 — RunRecord (Caption.showRoll)
    ///   ③ 도착지가 로비다                 — 오프닝은 본편으로 갔다
    ///   ④ 끝나면 저장을 지운다            — clearSaveOnFinish
    /// </code>
    ///
    /// ★ ④의 이유 — 이 판은 <b>끝났다</b>. 저장을 남기면 로비의 «이어하기» 가 <b>이미 이긴
    ///   판</b>을 다시 열어 승리 판정이 또 뜬다(20웨이브 이후가 없다 — 진행상황 미결 105번).
    ///   그래서 지운다. ⚠ 그래도 «클리어 뒤에도 계속 하고 싶다» 가 되면 이 값을 끄면 된다 —
    ///   <b>지우는 것이 기본인 이유는 «고장난 상태» 를 만들지 않는 쪽이기 때문</b>이다.
    ///
    /// 쓰는 에셋 (<c>Tools/import_ending_assets.py</c> 가 볼트에서 들여왔다) —
    /// <code>
    ///   Resources/Ending/BG_01 ~ BG_04            ← Ending_bg.png 를 2×2 로 자른 넉 장
    ///                                                ★ 자른 순서 = 이야기 순서 (오프닝과 다르다)
    ///   Resources/Ending/VO_01_1 ~ VO_04_5        ← 유저가 문장별로 나눠 준 17개
    ///   Resources/Bgm/The Unspoken Oath           ← 엔딩 브금 (119.77초)
    ///   Resources/UI/Lobby/LobbyButtonSkip        ← 건너뛰기 버튼 판 (오프닝과 같은 것)
    /// </code>
    /// </summary>
    public class EndingDirector : MonoBehaviour
    {
        // ────────────────────────────────────────────────────────────────
        //  대본의 모양
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 한 조각 = 자막 + 음성 + 시각. <see cref="OpeningDirector.Caption"/> 과 같고
        /// <see cref="textKey"/>·<see cref="showRoll"/> 둘이 더 있다.
        /// </summary>
        [Serializable]
        public class Caption
        {
            [Tooltip("스트링 키 (ending_caption_<컷>_<조각>). 비어 있거나 못 찾으면 아래 text 를 쓴다")]
            public string textKey = "";

            [Tooltip("자막 문구(한글) — 스트링 키를 못 찾을 때의 <b>폴백</b>")]
            [TextArea(1, 4)] public string text;

            [Tooltip("이 문장의 음성 — Resources 아래의 경로(확장자 없이)")]
            public string voice;

            [Tooltip("이 문장이 <b>말을 시작하는 브금의 시각(초)</b>. " +
                     "Tools/gen_ending_schedule.py 가 박자 격자에 맞춰 계산해 준다")]
            [Min(0f)] public float atMusicTime;

            [Tooltip("이 조각이 <b>다 쳐진 뒤</b> 전사자 명단을 띄운다. 컷 2 의 4번째 조각" +
                     "(«그 이름은 성역에 새겨질 것이다») 하나만 켜져 있다")]
            public bool showRoll;

            /// <summary>화면에 뜰 글. 스트링 테이블이 정본이고 <see cref="text"/> 가 폴백이다.</summary>
            public string Text => Data.StringTable.Get(textKey, text);
        }

        /// <summary>한 컷 = 배경 + 조각들. <see cref="OpeningDirector.Slide"/> 와 같다.</summary>
        [Serializable]
        public class Slide
        {
            [Tooltip("배경 그림 — Resources 아래의 경로(확장자 없이)")]
            public string background;

            [Tooltip("이 컷이 <b>페이드 인을 시작하는</b> 브금의 시각(초)")]
            [Min(0f)] public float atMusicTime;

            [Tooltip("자막의 타자 속도를 그 문장 음성의 길이에 맞춘다")]
            public bool fitCaptionsToVoice = true;

            [Tooltip("<b>말이 끝난 뒤</b> 머무는 시간(초). 다음 컷의 시각이 있으면 그쪽이 이긴다 — " +
                     "이 값이 실제로 쓰이는 것은 <b>마지막 컷</b>뿐이다")]
            [Min(0f)] public float holdAfterLastCaption = 2.4f;

            public Caption[] captions;
        }

        // ────────────────────────────────────────────────────────────────
        //  대본
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// ★★ <b>엔딩 대본</b> (2026-08-25 · 유저가 여러 번 고쳐 확정한 문구).
        ///
        /// ★★ <b>시각은 전부 <c>Tools/gen_ending_schedule.py</c> 가 계산해 찍어준다.</b>
        /// 손으로 재지 말 것 — 조각이 17개라 하나를 고치면 뒤가 다 밀린다.
        ///
        /// 실측한 격자 — <b>75.45 BPM · 박 0.7952초 · 첫 박 0.308초 · 격자 선명도 1.35</b>.
        /// (오프닝은 63.80 BPM · 선명도 1.94 였다. 이 곡은 격자가 조금 흐리지만 맞출 만하다.)
        /// <code>
        ///   컷 전환(slide.atMusicTime)     = <b>박</b> 위
        ///   조각 시작(caption.atMusicTime) = <b>박의 1/2</b> 위
        /// </code>
        ///
        /// ★ <b>오프닝보다 헐렁하다.</b> 내레이션이 67.50초인데 브금이 119.77초다 —
        /// 오프닿의 최소 텀(문장 0.35 · 절 0.15)을 그대로 쓰면 <b>88초에 다 끝나고 30초가
        /// 무음</b>으로 남는다. 그래서 텀을 <b>박 단위로 크게</b> 잡았다(문장 2박 = 1.59초 ·
        /// 절 1박 = 0.80초). 느린 낭독이 엔딩의 결에도 맞다.
        ///
        /// ★ 최소 텀이 어느 쪽인지는 <b>앞 자막의 «끝 글자» 가 정한다</b> — 마침표면 문장,
        /// 쉼표·줄표(—)·말줄임(…)이면 절이다. 오프닝과 같은 규칙이다.
        ///
        /// 시각표 —
        /// <code>
        ///   컷 1  1.90 :  4.28 · 8.26 · 13.83                        → 23.91 끝
        ///   컷 2 25.75 : 28.14 · 35.70 · 40.47 · 45.64 · 51.20        → 58.55
        ///                ★ 45.64 조각이 끝나면 명단이 뜬다 (약 9초 보인다)
        ///   컷 3 59.95 : 62.34 · 66.71 · 74.26 · 80.63               → 86.16
        ///   컷 4 87.78 : 90.17 · 94.94 · 98.92 · 104.09 · 108.06     → 113.92
        ///                그 뒤 검게 져 115.11초, 브금 페이드까지 117.6초
        ///   (브금 The Unspoken Oath 는 119.77초 — 넘지 않는다)
        /// </code>
        ///
        /// ⚠⚠ <b>이 표는 씬에도 복사된다</b>(<c>Ending.unity</c>). <see cref="SerializeField"/>
        ///    이므로 <b>씬에 저장된 값이 이 코드보다 이긴다</b> — 코드만 고치면 게임에는
        ///    안 바뀐 채로 보인다. 인스펙터에서 같이 고치거나 컴포넌트 <b>Reset</b> 을 할 것.
        /// </summary>
        [Header("대본 (배경 · 조각 = 자막 + 음성 + 시각)")]
        [SerializeField] Slide[] slides =
        {
            new Slide
            {
                background  = "Ending/BG_01",
                atMusicTime = 1.90f,
                captions = new[]
                {
                    new Caption
                    {
                        textKey     = "ending_caption_01_1",
                        text        = "울음이 그쳤다.",
                        voice       = "Ending/VO_01_1",     // 2.09초
                        atMusicTime = 4.28f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_01_2",
                        text        = "전장의 열기가 잦아든다.",
                        voice       = "Ending/VO_01_2",     // 3.89초
                        atMusicTime = 8.26f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_01_3",
                        text        = "떠나간 이들도 남은 이들도, 그 잔열에 몸을 떨고 있을 뿐이다.",
                        voice       = "Ending/VO_01_3",     // 8.49초
                        atMusicTime = 13.83f,
                    },
                },
            },
            new Slide
            {
                background  = "Ending/BG_02",
                atMusicTime = 25.75f,
                captions = new[]
                {
                    new Caption
                    {
                        textKey     = "ending_caption_02_1",
                        text        = "성문 앞에 마지막까지 서 있던 이들이 있다.",
                        voice       = "Ending/VO_02_1",     // 5.67초
                        atMusicTime = 28.14f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_02_2",
                        text        = "누구도 제 몸을 아끼지 않고 던졌고,",
                        voice       = "Ending/VO_02_2",     // 3.58초
                        atMusicTime = 35.70f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_02_3",
                        text        = "때로는 그 목숨까지 희생했다.",
                        voice       = "Ending/VO_02_3",     // 3.29초
                        atMusicTime = 40.47f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_02_4",
                        text        = "그 이름은 성역에 새겨질 것이다.",
                        voice       = "Ending/VO_02_4",     // 3.94초
                        atMusicTime = 45.64f,
                        showRoll    = true,                 // ★ 여기서 명단이 뜬다
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_02_5",
                        text        = "잊혀지지 않은채로, 영원히…",
                        voice       = "Ending/VO_02_5",     // 3.37초
                        atMusicTime = 51.20f,
                    },
                },
            },
            new Slide
            {
                background  = "Ending/BG_03",
                atMusicTime = 59.95f,
                captions = new[]
                {
                    new Caption
                    {
                        textKey     = "ending_caption_03_1",
                        text        = "찢어진 자리에 새 살이 돋는다.",
                        voice       = "Ending/VO_03_1",     // 3.34초
                        atMusicTime = 62.34f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_03_2",
                        text        = "보기 흉해도, 비 온 뒤에 땅은 더 굳어지는 법이다.",
                        voice       = "Ending/VO_03_2",     // 5.93초
                        atMusicTime = 66.71f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_03_3",
                        text        = "성벽은 예전 모양으로 돌아가지 않는다.",
                        voice       = "Ending/VO_03_3",     // 4.49초
                        atMusicTime = 74.26f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_03_4",
                        text        = "돌아가지 않은 채로, 그 자리를 지킬 뿐…",
                        voice       = "Ending/VO_03_4",     // 3.94초
                        atMusicTime = 80.63f,
                    },
                },
            },
            new Slide
            {
                background  = "Ending/BG_04",
                atMusicTime = 87.78f,
                holdAfterLastCaption = 2.39f,
                captions = new[]
                {
                    new Caption
                    {
                        textKey     = "ending_caption_04_1",
                        text        = "어둠은 그저 물러갔을 뿐이다.",
                        voice       = "Ending/VO_04_1",     // 3.00초
                        atMusicTime = 90.17f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_04_2",
                        text        = "그 뿌리는 그대로 남아 있다.",
                        voice       = "Ending/VO_04_2",     // 2.35초
                        atMusicTime = 94.94f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_04_3",
                        text        = "언제 다시 하늘이 붉어질지는 아무도 모른다.",
                        voice       = "Ending/VO_04_3",     // 4.36초
                        atMusicTime = 98.92f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_04_4",
                        text        = "그대여, 우리는 그때에도 —",
                        voice       = "Ending/VO_04_4",     // 2.27초
                        atMusicTime = 104.09f,
                    },
                    new Caption
                    {
                        textKey     = "ending_caption_04_5",
                        text        = "이 땅과 당신을 지킬 것이다.",
                        voice       = "Ending/VO_04_5",     // 3.47초
                        atMusicTime = 108.06f,
                    },
                },
            },
        };

        // ────────────────────────────────────────────────────────────────
        //  설정
        // ────────────────────────────────────────────────────────────────

        [Header("브금 (Resources 아래의 경로 — 확장자 없이)")]
        [SerializeField] string bgmResource = "Bgm/The Unspoken Oath";

        [Range(0f, 1f)] [SerializeField] float bgmVolume = 0.55f;

        [Min(0f)] [SerializeField] float bgmFadeOutSeconds = 2.5f;

        [Header("음성")]
        [Range(0f, 1f)] [SerializeField] float voiceVolume = 1f;

        [Header("페이드 (오프닝과 같은 값 — 두 연출이 한 벌로 보여야 한다)")]
        [Min(0f)] [SerializeField] float fadeInSeconds = 1.59f;
        [Min(0f)] [SerializeField] float fadeOutSeconds = 1.19f;

        [Header("자막 (타자 효과)")]
        [Min(1f)] [SerializeField] float charsPerSecond = 22f;

        [Tooltip("자막이 내레이션보다 이만큼 먼저 끝나게 한다(초)")]
        [Min(0f)] [SerializeField] float captionTailSeconds = 0.8f;

        [Min(0f)] [SerializeField] float captionGapSeconds = 0.9f;

        [SerializeField] float captionFontSize = 40f;
        [SerializeField] Vector2 captionSize = new Vector2(1440f, 320f);
        [SerializeField] Vector2 captionMargin = new Vector2(180f, 110f);
        [SerializeField] float captionLineSpacing = 12f;
        [Range(0f, 1f)] [SerializeField] float captionShadeAlpha = 0.7f;
        [Min(0f)] [SerializeField] float captionShadeHeight = 480f;

        [Header("전사자 명단 (컷 2)")]
        [Tooltip("명단이 떠오르는 시간(초)")]
        [Min(0f)] [SerializeField] float rollFadeSeconds = 1.2f;

        [SerializeField] float rollFontSize = 26f;

        [Tooltip("명단 칸의 크기(px · 1920x1080 기준). 화면 <b>가운데</b>에 놓는다 — " +
                 "배경 그림의 우상단 칸을 «가운데를 비워» 주문한 것이 이 자리 때문이다")]
        [SerializeField] Vector2 rollSize = new Vector2(900f, 460f);

        [Tooltip("명단 칸을 화면 가운데에서 위로 이만큼 올린다(px). " +
                 "자막이 왼쪽 아래에 있으므로 조금 올려야 겹치지 않는다")]
        [SerializeField] float rollRaise = 90f;

        [Tooltip("이름을 이만큼까지만 적는다. 넘으면 «… 그리고 N명» 으로 접는다 — " +
                 "한 판에 스무 명이 죽으면 칸을 넘겨 화면 밖으로 흐른다")]
        [Min(1)] [SerializeField] int rollMaxNames = 8;

        [SerializeField] string rollFallenHeader = "돌아오지 못한 이들";
        [SerializeField] string rollSurvivorHeader = "끝까지 남은 이들";

        [Tooltip("{0}=이름 · {1}=칭호 · {2}=레벨 · {3}=쓰러진 웨이브")]
        [SerializeField] string rollFallenFormat = "{0}  <size=80%>{1} · Lv.{2} · {3}웨이브</size>";

        [Tooltip("{0}=이름 · {1}=칭호 · {2}=레벨")]
        [SerializeField] string rollSurvivorFormat = "{0}  <size=80%>{1} · Lv.{2}</size>";

        [Tooltip("{0}=더 있는 인원 수")]
        [SerializeField] string rollMoreFormat = "<size=80%>… 그리고 {0}명</size>";

        [Header("건너뛰기 버튼")]
        [Min(0f)] [SerializeField] float skipButtonDelaySeconds = 3.5f;
        [SerializeField] string skipButtonText = "건너뛰기";
        [SerializeField] string skipButtonResource = "UI/Lobby/LobbyButtonSkip";
        [SerializeField] Vector2 skipButtonSize = new Vector2(200f, 70f);
        [SerializeField] Vector2 skipButtonMargin = new Vector2(40f, 24f);
        [Min(1f)] [SerializeField] float skipButtonFontSize = 20f;
        [Min(0f)] [SerializeField] float skipFadeSeconds = 0.4f;

        [Header("씬")]
        [Tooltip("엔딩이 끝나면 열 씬. 빌드 세팅에 들어 있어야 한다")]
        [SerializeField] string nextSceneName = "Lobby";

        [Tooltip("★ 끝나면 저장을 지운다 — 이 판은 끝났다(클래스 doc ④). " +
                 "끄면 로비의 «이어하기» 가 이미 이긴 판을 다시 연다")]
        [SerializeField] bool clearSaveOnFinish = true;

        // ────────────────────────────────────────────────────────────────

        Image _background;
        AspectRatioFitter _backgroundFit;
        CanvasGroup _curtain;
        TMP_Text _caption;
        CanvasGroup _roll;
        TMP_Text _rollText;
        CanvasGroup _skipButton;

        AudioSource _bgm;
        AudioSource _voice;

        /// <summary>
        /// 지금이 «노래의 몇 초» 인가 — <see cref="OpeningDirector"/> 와 같은 시계다.
        /// 브금이 돌고 있으면 <see cref="AudioSource.time"/> 이 정본이고, 없거나 끝난 뒤에는
        /// 스스로 굴러간다. <b>절대 뒤로 가지 않는다</b>(스트리밍이 늦게 시작될 때를 막는다).
        /// </summary>
        float _clock;

        /// <summary>지금 틀고 있는 음성이 끝나는 브금 시각. 마지막 컷의 «머묾» 기준이다.</summary>
        float _voiceEndsAt;

        bool _running;
        bool _leaving;

        /// <summary>«이 컷은 다 봤다» — 화면을 누르면 선다. 진행은 코루틴이 스스로 끊는다.</summary>
        bool _cutRequested;

        // ────────────────────────────────────────────────────────────────

        void Start()
        {
            SaveService.ApplyVolume();

            // 승리 화면이 timeScale 을 0 으로 만들어 두었을 수 있다 — 그대로면 연출이 멈춘다.
            Time.timeScale = 1f;

            BuildUi();
            BuildAudio();

            _running = true;
            StartCoroutine(Run());
            StartCoroutine(ShowSkipButton());
        }

        void Update()
        {
            _clock += Time.unscaledDeltaTime;
            if (_bgm != null && _bgm.isPlaying && _bgm.clip != null)
                _clock = Mathf.Max(_clock, _bgm.time);

            if (_running && !_leaving && SkipRequested()) Skip();
            if (_running && !_leaving && NextCutRequested()) _cutRequested = true;
        }

        /// <summary>화면 아무 곳이나 눌렀는가 — 건너뛰기 버튼 위는 세지 않는다.</summary>
        static bool NextCutRequested()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return false;

            EventSystem events = EventSystem.current;
            return events == null || !events.IsPointerOverGameObject();
        }

        static bool SkipRequested()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.spaceKey.wasPressedThisFrame ||
                    keyboard.enterKey.wasPressedThisFrame ||
                    keyboard.escapeKey.wasPressedThisFrame);
        }

        // ── 연출 본체 ────────────────────────────────────────────────────

        IEnumerator Run()
        {
            for (int i = 0; i < slides.Length; i++)
            {
                Slide slide = slides[i];
                if (slide == null) continue;

                _cutRequested = false;

                // ① 이 컷이 시작될 «노래의 시각» 까지 검은 화면으로 기다린다
                if (slide.atMusicTime > 0f)
                    while (_clock < slide.atMusicTime) yield return null;

                // ② 막이 내려간 동안 배경을 갈아끼운다
                ApplyBackground(slide.background);
                _caption.text = string.Empty;
                HideRoll();
                _voiceEndsAt = _clock;

                // ③ 페이드 인
                yield return Fade(_curtain, 1f, 0f, fadeInSeconds);

                // ④ 조각을 차례대로
                yield return TypeCaptions(slide);

                // ⑤ 페이드 아웃을 언제 시작할지 — 다음 컷이 노래에서 밀리지 않는 것이 우선이다
                float? next = NextSlideMusicTime(i);
                float fadeOutAt = next.HasValue && next.Value > 0f
                    ? next.Value - fadeOutSeconds
                    : Mathf.Max(_clock, _voiceEndsAt) + slide.holdAfterLastCaption;

                while (_clock < fadeOutAt && !_cutRequested) yield return null;

                if (_cutRequested)
                {
                    _cutRequested = false;
                    if (_voice != null) _voice.Stop();
                    SeekToCut(next);
                }

                yield return Fade(_curtain, _curtain.alpha, 1f, fadeOutSeconds);
            }

            yield return Leave();
        }

        float? NextSlideMusicTime(int index) =>
            index + 1 < slides.Length && slides[index + 1] != null
                ? slides[index + 1].atMusicTime
                : (float?)null;

        /// <summary>
        /// 노래를 다음 컷의 자리로 민다 — 컷을 클릭으로 넘길 때 부른다.
        /// 화면만 앞질러 보내면 자막·음성이 <b>지나간 박자</b>를 기다려 연출이 통째로 어긋난다.
        /// </summary>
        void SeekToCut(float? nextCutMusicTime)
        {
            if (!nextCutMusicTime.HasValue || nextCutMusicTime.Value <= 0f) return;

            float target = Mathf.Max(0f, nextCutMusicTime.Value - fadeOutSeconds);
            if (target <= _clock) return;

            if (_bgm != null && _bgm.clip != null && target < _bgm.clip.length - 0.05f)
                _bgm.time = target;

            _clock = target;
        }

        IEnumerator TypeCaptions(Slide slide)
        {
            Caption[] captions = slide.captions;
            if (captions == null) yield break;

            for (int i = 0; i < captions.Length; i++)
            {
                if (_cutRequested) yield break;

                Caption caption = captions[i];
                string text = caption != null ? caption.Text : null;
                if (string.IsNullOrEmpty(text)) continue;

                if (caption.atMusicTime > 0f)
                {
                    while (_clock < caption.atMusicTime && !_cutRequested) yield return null;
                }
                else if (i > 0)
                {
                    float until = _clock + captionGapSeconds;
                    while (_clock < until && !_cutRequested) yield return null;
                }

                if (_cutRequested) yield break;

                float voiceLength = PlayVoice(caption.voice);
                yield return Type(text, SpeedFor(slide, text, voiceLength));

                // ★ 이 조각이 «명단을 여는» 조각이면 여기서 띄운다 — 자막을 다 친 뒤다.
                //   ⚠ 자막보다 먼저 띄우면 «이름은 성역에 새겨질 것이다» 를 <b>읽기 전에</b>
                //     명단이 떠서 문장이 명단의 설명이 아니라 뒷북이 된다.
                if (caption.showRoll) ShowRoll();
            }
        }

        /// <summary>이 문장의 타자 속도(초당 글자 수) — 음성 길이에서 역산한다.</summary>
        float SpeedFor(Slide slide, string text, float voiceLength)
        {
            if (!slide.fitCaptionsToVoice || voiceLength <= 0f || string.IsNullOrEmpty(text))
                return charsPerSecond;

            float budget = voiceLength - captionTailSeconds;
            if (budget <= 0.25f) return charsPerSecond;

            return Mathf.Clamp(text.Length / budget, 6f, 90f);
        }

        /// <summary>
        /// 타자 효과 — 글자를 붙이지 않고 <see cref="TMP_Text.maxVisibleCharacters"/> 만 늘린다.
        /// 붙이는 방식은 줄바꿈이 매 프레임 다시 계산돼 이미 나온 글자가 흔들린다.
        /// </summary>
        IEnumerator Type(string text, float speed)
        {
            _caption.text = text;
            _caption.maxVisibleCharacters = 0;
            _caption.ForceMeshUpdate();

            int total = _caption.textInfo.characterCount;
            if (total <= 0) yield break;

            float shown = 0f;
            while (_caption.maxVisibleCharacters < total)
            {
                if (_cutRequested) { _caption.maxVisibleCharacters = total; yield break; }

                shown += Time.unscaledDeltaTime * speed;
                _caption.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
        }

        // ── 전사자 명단 ──────────────────────────────────────────────────

        /// <summary>
        /// <b>이 판에서 누가 쓰러지고 누가 남았는지</b>를 화면 가운데에 띄운다
        /// (<see cref="RunRecord"/>).
        ///
        /// ★ <b>기록이 비어 있으면 아무것도 하지 않는다</b> — 엔딩만 따로 열어 보거나
        ///   («[테스트] 즉시 승리» 로 확인할 때) 기록 없이 들어올 수 있다. 그때 빈 상자가
        ///   떠오르면 <b>고장으로 보인다</b>.
        /// ⚠ 이름이 너무 많으면 칸을 넘겨 화면 밖으로 흐른다 — <see cref="rollMaxNames"/>
        ///   까지만 적고 나머지는 «… 그리고 N명» 으로 접는다.
        /// </summary>
        void ShowRoll()
        {
            if (_roll == null || _rollText == null) return;
            if (!RunRecord.HasAny) return;

            var sb = new StringBuilder();

            AppendGroup(sb, rollFallenHeader, RunRecord.Fallen, fallen: true);
            if (sb.Length > 0) sb.AppendLine();
            AppendGroup(sb, rollSurvivorHeader, RunRecord.Survivors, fallen: false);

            string body = sb.ToString().TrimEnd();
            if (string.IsNullOrEmpty(body)) return;

            _rollText.text = body;
            StartCoroutine(Fade(_roll, 0f, 1f, rollFadeSeconds));
        }

        void AppendGroup(StringBuilder sb, string header,
                         IReadOnlyList<RunRecord.Entry> rows, bool fallen)
        {
            if (rows == null || rows.Count == 0) return;

            sb.Append("<color=#8E9AA6>").Append(header).Append("</color>").AppendLine();

            int shown = Mathf.Min(rows.Count, rollMaxNames);
            for (int i = 0; i < shown; i++)
            {
                RunRecord.Entry e = rows[i];
                sb.AppendLine(fallen
                    ? string.Format(rollFallenFormat, e.name, e.title, e.level, e.wave)
                    : string.Format(rollSurvivorFormat, e.name, e.title, e.level));
            }

            int more = rows.Count - shown;
            if (more > 0) sb.AppendLine(string.Format(rollMoreFormat, more));
        }

        void HideRoll()
        {
            if (_roll == null) return;
            _roll.alpha = 0f;
        }

        // ── 끝내기 ──────────────────────────────────────────────────────

        void Skip()
        {
            if (_leaving) return;

            StopAllCoroutines();
            _running = false;

            if (_skipButton != null)
            {
                _skipButton.alpha = 0f;
                _skipButton.blocksRaycasts = false;
            }

            StartCoroutine(Leave(skipFadeSeconds));
        }

        /// <summary>
        /// 검게 지고 브금을 잦아들게 한 뒤 로비를 연다.
        ///
        /// ★ <b>저장을 여기서 지운다</b> — 씬을 넘기기 <b>직전</b>이다. 더 앞에서 지우면
        ///   연출 도중에 게임을 끄는 유저가 «이겼는데 저장도 없다» 를 겪는다.
        /// </summary>
        IEnumerator Leave(float curtainSeconds = -1f)
        {
            if (_leaving) yield break;
            _leaving = true;
            _running = false;

            float curtainFade = curtainSeconds >= 0f ? curtainSeconds : fadeOutSeconds;
            float span = Mathf.Max(0.01f, Mathf.Max(curtainFade, bgmFadeOutSeconds));

            float curtainFrom = _curtain.alpha;
            float bgmFrom = _bgm != null ? _bgm.volume : 0f;
            float voiceFrom = _voice != null ? _voice.volume : 0f;

            float t = 0f;
            while (t < span)
            {
                t += Time.unscaledDeltaTime;

                _curtain.alpha = Mathf.Lerp(curtainFrom, 1f,
                    curtainFade <= 0f ? 1f : Mathf.Clamp01(t / curtainFade));

                float quiet = bgmFadeOutSeconds <= 0f ? 1f : Mathf.Clamp01(t / bgmFadeOutSeconds);
                if (_bgm != null) _bgm.volume = Mathf.Lerp(bgmFrom, 0f, quiet);
                if (_voice != null) _voice.volume = Mathf.Lerp(voiceFrom, 0f, quiet);

                yield return null;
            }

            _curtain.alpha = 1f;

            if (clearSaveOnFinish)
            {
                SaveService.Delete();
                SaveService.PendingLoad = null;   // 남아 있으면 다음 판이 이것으로 덮인다
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(nextSceneName);
        }

        // ── 잔심부름 (오프닝과 같다) ─────────────────────────────────────

        IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
        {
            if (group == null) yield break;

            if (seconds <= 0f)
            {
                group.alpha = to;
                yield break;
            }

            group.alpha = from;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }
            group.alpha = to;
        }

        IEnumerator ShowSkipButton()
        {
            if (_skipButton == null) yield break;

            float t = 0f;
            while (t < skipButtonDelaySeconds) { t += Time.unscaledDeltaTime; yield return null; }

            yield return Fade(_skipButton, 0f, 1f, 0.8f);
            _skipButton.blocksRaycasts = true;
        }

        void ApplyBackground(string resource)
        {
            Sprite sprite = LoadOnce<Sprite>(resource);
            _background.sprite = sprite;
            _background.enabled = sprite != null;

            if (sprite != null && _backgroundFit != null)
                _backgroundFit.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        }

        float PlayVoice(string resource)
        {
            AudioClip clip = LoadOnce<AudioClip>(resource);
            if (clip == null) return 0f;

            _voice.Stop();
            _voice.clip = clip;
            _voice.volume = voiceVolume;
            _voice.Play();

            _voiceEndsAt = _clock + clip.length;
            return clip.length;
        }

        readonly HashSet<string> _missingWarned = new HashSet<string>();

        T LoadOnce<T>(string resource) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(resource)) return null;

            T asset = Resources.Load<T>(resource);
            if (asset == null && _missingWarned.Add(resource))
                Debug.LogWarning($"[엔딩] Resources/{resource} 를 찾지 못했습니다.", this);

            return asset;
        }

        // ── UI 짓기 ─────────────────────────────────────────────────────

        void BuildAudio()
        {
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _bgm.loop = false;              // ★ 시계로 쓰므로 감기면 안 된다
            _bgm.spatialBlend = 0f;
            _bgm.volume = bgmVolume;
            _bgm.clip = LoadOnce<AudioClip>(bgmResource);
            if (_bgm.clip != null) _bgm.Play();

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.loop = false;
            _voice.spatialBlend = 0f;
            _voice.volume = voiceVolume;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("EndingCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasGo.GetComponent<RectTransform>();

            // ① 배경 — 액자(RectMask2D) + EnvelopeParent. preserveAspect 로는 검은 띠가 생긴다.
            RectTransform frame = NewRect("Background", root);
            Stretch(frame);
            frame.gameObject.AddComponent<RectMask2D>();

            RectTransform bg = NewRect("Sprite", frame);
            Stretch(bg);
            _background = bg.gameObject.AddComponent<Image>();
            _background.raycastTarget = false;
            _background.preserveAspect = false;
            _backgroundFit = bg.gameObject.AddComponent<AspectRatioFitter>();
            _backgroundFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            // ② 자막 뒤 그늘
            if (captionShadeAlpha > 0f)
            {
                RectTransform shade = NewRect("CaptionShade", root);
                shade.anchorMin = new Vector2(0f, 0f);
                shade.anchorMax = new Vector2(1f, 0f);
                shade.pivot = new Vector2(0.5f, 0f);
                shade.offsetMin = Vector2.zero;
                shade.offsetMax = new Vector2(0f, captionShadeHeight);

                var shadeImage = shade.gameObject.AddComponent<Image>();
                shadeImage.sprite = BuildBottomGradient();
                shadeImage.raycastTarget = false;
                shadeImage.color = new Color(0f, 0f, 0f, captionShadeAlpha);
            }

            // ③ 전사자 명단 — 화면 <b>가운데</b>. 자막(왼쪽 아래)보다 <b>먼저</b> 그린다.
            BuildRoll(root);

            // ④ 자막 — 왼쪽 아래, 왼쪽 정렬
            RectTransform captionRect = NewRect("Caption", root);
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(0f, 0f);
            captionRect.pivot = new Vector2(0f, 0f);
            captionRect.sizeDelta = captionSize;
            captionRect.anchoredPosition = captionMargin;

            _caption = captionRect.gameObject.AddComponent<TextMeshProUGUI>();
            _caption.font = HudTheme.Font;
            _caption.fontSize = captionFontSize;
            _caption.color = HudTheme.TextMain;
            _caption.alignment = TextAlignmentOptions.BottomLeft;
            _caption.lineSpacing = captionLineSpacing;
            _caption.textWrappingMode = TextWrappingModes.Normal;
            _caption.overflowMode = TextOverflowModes.Overflow;
            _caption.raycastTarget = false;
            _caption.text = string.Empty;

            // ⑤ 검은 막
            RectTransform curtainRect = NewRect("Curtain", root);
            Stretch(curtainRect);
            var curtainImage = curtainRect.gameObject.AddComponent<Image>();
            curtainImage.color = Color.black;
            curtainImage.raycastTarget = false;
            _curtain = curtainRect.gameObject.AddComponent<CanvasGroup>();
            _curtain.alpha = 1f;
            _curtain.blocksRaycasts = false;

            // ⑥ 건너뛰기 버튼 — 막 위에 둔다(형제 중 마지막 = 가장 앞)
            BuildSkipButton(root);
        }

        void BuildRoll(RectTransform root)
        {
            RectTransform rect = NewRect("Roll", root);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = rollSize;
            rect.anchoredPosition = new Vector2(0f, rollRaise);

            _rollText = rect.gameObject.AddComponent<TextMeshProUGUI>();
            _rollText.font = HudTheme.Font;
            _rollText.fontSize = rollFontSize;
            _rollText.color = HudTheme.TextMain;
            _rollText.alignment = TextAlignmentOptions.Center;
            _rollText.lineSpacing = 16f;
            _rollText.textWrappingMode = TextWrappingModes.NoWrap;
            _rollText.overflowMode = TextOverflowModes.Overflow;
            _rollText.raycastTarget = false;
            _rollText.richText = true;
            _rollText.text = string.Empty;

            _roll = rect.gameObject.AddComponent<CanvasGroup>();
            _roll.alpha = 0f;
            _roll.blocksRaycasts = false;
        }

        void BuildSkipButton(RectTransform root)
        {
            EnsureEventSystem();

            RectTransform rect = NewRect("SkipButton", root);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = skipButtonSize;
            rect.anchoredPosition = new Vector2(-skipButtonMargin.x, -skipButtonMargin.y);

            var plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = LoadOnce<Sprite>(skipButtonResource);
            plate.type = Image.Type.Simple;
            plate.preserveAspect = false;
            plate.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor      = new Color(0.90f, 0.90f, 0.90f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor     = new Color(0.66f, 0.66f, 0.66f, 1f);
            colors.selectedColor    = new Color(0.90f, 0.90f, 0.90f, 1f);
            colors.disabledColor    = new Color(0.45f, 0.45f, 0.45f, 0.55f);
            colors.colorMultiplier  = 1f;
            colors.fadeDuration     = 0.12f;
            button.colors = colors;

            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            button.onClick.AddListener(Skip);

            RectTransform labelRect = NewRect("Label", rect);
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);

            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = HudTheme.Font;
            label.fontSize = skipButtonFontSize;
            label.color = HudTheme.TextMain;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = skipButtonText;

            _skipButton = rect.gameObject.AddComponent<CanvasGroup>();
            _skipButton.alpha = 0f;
            _skipButton.blocksRaycasts = false;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(null, false);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Sprite BuildBottomGradient()
        {
            const int height = 64;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = "EndingCaptionShade",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int y = 0; y < height; y++)
            {
                float up = y / (float)(height - 1);
                float alpha = 1f - up;
                alpha *= alpha;
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            }
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f));
        }
    }
}
