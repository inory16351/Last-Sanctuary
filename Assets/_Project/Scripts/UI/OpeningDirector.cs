using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LastSanctuary.Save;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 새 게임을 시작할 때 나오는 <b>오프닝 연출</b> (2026-08-24 신설 — 유저 지시
    /// <i>"오프닝 씬 만들 거야 새 게임 시작하면 나오는 거고 … 각 배경 화면 넘어갈때마다 노래
    /// 타이밍 바뀌고 텍스트에 맞춰서 자막도 차례대로 타자로 치는것처럼 등장하게 해줘. 영어에
    /// 맞춰서. 배경 화면 넘어갈때는 페이드 인 페이드 아웃 … 폰트는 네오 둥근모 …
    /// 이 오프닝은 하드 코딩 해도 됨"</i>).
    ///
    /// 쓰는 에셋 (볼트 <c>리소스/</c> 에서 가져와 <c>Resources/</c> 로 넣었다) —
    /// <code>
    ///   Resources/Opening/BG_01 ~ BG_04      ← 볼트의 opening_BG.png 를 2×2 로 잘라낸 넉 장
    ///   Resources/Opening/VO_01 ~ VO_04      ← 볼트의 voice/Scene_01~04.mp3
    ///   Resources/Bgm/The Fall of the Sanctuary  ← 오프닝 브금 (1분 59초)
    /// </code>
    ///
    /// <b>㉠ 브금이 시계다</b> — 이 연출의 뼈대가 되는 판단.
    /// «배경이 넘어갈 때마다 노래 타이밍이 바뀐다» 를 지키는 방법은 둘이다.
    /// <code>
    ///   ① 장면마다 «몇 초 보여줄지» 를 적고 시간이 쌓이면 넘긴다        — (안 씀)
    ///   ② 장면마다 «브금의 몇 초에 시작할지» 를 적고 재생 위치를 본다   — (씀)
    /// </code>
    /// ①은 한 장면의 길이를 0.2초 고치면 <b>그 뒤의 모든 장면이 노래에서 밀린다</b>. 로딩이
    /// 한 프레임 튀거나 프레임이 떨어져도 조금씩 어긋나 <b>누적</b>된다. ②는 각 장면이
    /// 노래의 <b>절대 시각에 못 박혀</b> 있어서, 한 장면을 손봐도 다른 장면이 밀리지 않고
    /// 프레임이 아무리 튀어도 «그 소절» 에 정확히 배경이 바뀐다.
    /// (<see cref="_clock"/> · <see cref="Slide.atMusicTime"/>)
    ///
    /// <b>㉡ 자막은 음성 길이에 맞춰 «스스로» 속도를 정한다</b>
    /// (<see cref="Slide.fitCaptionsToVoice"/>). 자막을 초 단위로 하나하나 박아 넣으면
    /// 문구를 한 글자 고칠 때마다 그 장면의 시각을 <b>전부 다시 재야</b> 한다. 대신
    /// «이 장면의 글자 수» 를 «이 음성의 길이» 로 나눠 타자 속도를 구하면, 문구를 고쳐도
    /// 자막은 여전히 <b>내레이션이 끝나기 조금 전에</b> 다 쳐진다. 한 줄 한 줄을 노래에
    /// 정확히 붙여야 할 때만 <see cref="Caption.atMusicTime"/> 에 시각을 적으면 그쪽이 이긴다.
    ///
    /// <b>㉢ 페이드는 «검은 막» 하나로 한다</b> — 배경 두 장을 겹쳐 크로스페이드하지 않는다.
    /// 유저가 요청한 것은 «페이드 인 / 페이드 아웃»(검게 지고 검은 데서 다시 밝아지는 것)이고,
    /// 막 하나면 <b>자막까지 같이</b> 사라진다. 그래서 하이라키를 이렇게 쌓았다 —
    /// <code>
    ///   Background   (액자 + 그림)      ← 맨 아래
    ///   CaptionShade (아래쪽 그늘)
    ///   Caption      (자막)
    ///   Curtain      (검은 막)          ← 자막까지 덮는다
    ///   SkipHint     (건너뛰기 안내)    ← 막 위. 검은 화면에서도 보인다
    /// </code>
    /// 자막을 막 <b>아래</b>에 두었기 때문에 «자막 지우기» 를 따로 할 일이 없다.
    ///
    /// <b>㉣ UI 를 코드로 짓는다</b> — HUD(<see cref="HudTheme"/>)·<see cref="Audio.BgmService"/> 와
    /// 같은 이유다. 스프라이트·폰트·오디오는 전부 <b>에셋 참조</b>라 MCP 로 씬 인스펙터에 넣을 수
    /// 없다(진행상황 8절 4번). 씬에는 이 컴포넌트를 붙인 오브젝트 <b>하나</b>만 있으면 되고
    /// 배선할 것이 없다.
    ///
    /// ⚠ 시간은 전부 <see cref="Time.unscaledDeltaTime"/> 으로 잰다 — 게임에서 배속·일시정지를
    ///   걸어둔 채 새로하기를 누르면 <c>timeScale</c> 이 그대로 넘어와 연출이 멈춘다
    ///   (<see cref="LobbyPanel"/> 이 같은 함정을 같은 방법으로 막는다).
    /// ⚠ 이 프로젝트는 <b>Input System 패키지 전용</b>이다 — <c>UnityEngine.Input</c> 을 쓰면
    ///   실행 시점에 예외가 난다.
    /// </summary>
    public class OpeningDirector : MonoBehaviour
    {
        // ────────────────────────────────────────────────────────────────
        //  대본 — 오프닝의 내용은 전부 이 표에 있다 (유저: "하드 코딩 해도 됨")
        // ────────────────────────────────────────────────────────────────

        /// <summary>자막 한 줄. 타자를 치듯 한 글자씩 나타난다.</summary>
        [Serializable]
        public class Caption
        {
            [Tooltip("자막 문구(영어)")]
            [TextArea(1, 4)] public string text;

            [Tooltip("이 줄을 치기 시작하는 <b>브금의 시각(초)</b>. " +
                     "0 이면 앞 줄을 다 친 뒤 captionGapSeconds 만큼 쉬고 이어 친다")]
            [Min(0f)] public float atMusicTime;
        }

        /// <summary>배경 한 장 + 그 위에 얹히는 자막들 + 그 장면의 음성.</summary>
        [Serializable]
        public class Slide
        {
            [Tooltip("배경 그림 — Resources 아래의 경로(확장자 없이)")]
            public string background;

            [Tooltip("이 장면의 음성 — Resources 아래의 경로. 없으면 비워둔다")]
            public string voice;

            [Tooltip("이 장면이 <b>페이드 인을 시작하는</b> 브금의 시각(초). " +
                     "0 이면 앞 장면이 검게 진 직후 바로 시작한다")]
            [Min(0f)] public float atMusicTime;

            [Tooltip("자막의 타자 속도를 <b>음성 길이에 맞춰 자동으로</b> 정한다. " +
                     "켜두면 내레이션이 끝나기 captionTailSeconds 전에 자막이 다 쳐진다")]
            public bool fitCaptionsToVoice = true;

            [Tooltip("마지막 자막을 다 친 뒤 머무는 시간(초). " +
                     "⚠ 다음 장면에 atMusicTime 이 적혀 있으면 그쪽이 이긴다 — " +
                     "다음 장면이 노래에서 밀리지 않는 것이 우선이다")]
            [Min(0f)] public float holdAfterLastCaption = 2f;

            public Caption[] captions;
        }

        /// <summary>
        /// ★★ <b>오프닝 대본</b>.
        ///
        /// <b>시각(<see cref="Slide.atMusicTime"/>)은 음성 길이에서 뽑았다</b> — 각 장면은
        /// «페이드 인 → 내레이션 → 잠깐 머묾 → 페이드 아웃» 이고, 페이드 아웃이 끝나는 순간이
        /// 곧 다음 장면의 시각이다(<see cref="Run"/> 의 fadeOutAt 계산).
        /// <code>
        ///   장면 1 : 2.0초 시작 · 음성 15.60초 → 17.6초 끝 · 19.8초부터 검게 진다
        ///   장면 2 : 21.0초    · 음성 23.72초 → 44.7초    · 46.8초
        ///   장면 3 : 48.0초    · 음성 19.67초 → 67.7초    · 69.3초
        ///   장면 4 : 70.5초    · 음성 20.09초 → 90.6초    · 93.6초 → 게임 씬으로
        ///   (브금 The Fall of the Sanctuary 는 119.65초 — 넉넉히 남는다)
        /// </code>
        ///
        /// ⚠ <b>자막 문구는 아직 자리표시자다</b> — 볼트에는 배경·음성·브금만 들어왔고
        ///   내레이션 <b>대본(텍스트)</b>은 없었다. 음성에 배경음이 계속 깔려 있어 문장 경계를
        ///   소리로 갈라낼 수도 없다. 영어 문구가 들어오면 이 표의 <c>text</c> 만 채우면 되고,
        ///   타자 속도는 <see cref="Slide.fitCaptionsToVoice"/> 가 알아서 맞춘다.
        ///
        /// ⚠⚠ <b>이 표는 씬에도 복사되어 있다</b>(<c>Opening.unity</c> 의 OpeningDirector).
        ///    <see cref="SerializeField"/> 이므로 <b>씬에 저장된 값이 이 코드보다 이긴다</b> —
        ///    여기만 고치면 게임에는 <b>안 바뀐 채로 보인다</b>. 코드를 고친 뒤에는 둘 중 하나를
        ///    해야 한다: ① 인스펙터에서 값을 같이 고친다, 또는 ② 컴포넌트 톱니바퀴 →
        ///    <b>Reset</b> 으로 씬의 값을 코드 기본값으로 되돌린다(다른 설정도 함께 초기화된다).
        /// </summary>
        [Header("대본 (배경 · 음성 · 자막)")]
        [SerializeField] Slide[] slides =
        {
            new Slide
            {
                background  = "Opening/BG_01",
                voice       = "Opening/VO_01",
                atMusicTime = 2f,
                captions = new[]
                {
                    new Caption { text = "[SCENE 1] Paste the English narration for this scene here." },
                },
            },
            new Slide
            {
                background  = "Opening/BG_02",
                voice       = "Opening/VO_02",
                atMusicTime = 21f,
                captions = new[]
                {
                    new Caption { text = "[SCENE 2] Paste the English narration for this scene here." },
                },
            },
            new Slide
            {
                background  = "Opening/BG_03",
                voice       = "Opening/VO_03",
                atMusicTime = 48f,
                captions = new[]
                {
                    new Caption { text = "[SCENE 3] Paste the English narration for this scene here." },
                },
            },
            new Slide
            {
                background  = "Opening/BG_04",
                voice       = "Opening/VO_04",
                atMusicTime = 70.5f,
                holdAfterLastCaption = 3f,
                captions = new[]
                {
                    new Caption { text = "[SCENE 4] Paste the English narration for this scene here." },
                },
            },
        };

        // ────────────────────────────────────────────────────────────────
        //  설정
        // ────────────────────────────────────────────────────────────────

        [Header("브금 (Resources 아래의 경로 — 확장자 없이)")]
        [SerializeField] string bgmResource = "Bgm/The Fall of the Sanctuary";

        [Range(0f, 1f)] [SerializeField] float bgmVolume = 0.55f;

        [Tooltip("오프닝이 끝나며 브금이 잦아드는 시간(초)")]
        [Min(0f)] [SerializeField] float bgmFadeOutSeconds = 2.5f;

        [Header("음성")]
        [Range(0f, 1f)] [SerializeField] float voiceVolume = 1f;

        [Header("페이드 (배경이 넘어갈 때)")]
        [Tooltip("검은 막이 걷히는 시간(초) — 배경이 드러난다")]
        [Min(0f)] [SerializeField] float fadeInSeconds = 1.6f;

        [Tooltip("검은 막이 덮이는 시간(초) — 배경이 검게 진다.\n" +
                 "다음 장면의 atMusicTime 에서 이 값을 <b>거꾸로 빼서</b> 페이드 아웃을 시작하므로 " +
                 "«완전히 검어지는 순간 = 다음 장면이 밝아지기 시작하는 순간» 이 딱 맞는다")]
        [Min(0f)] [SerializeField] float fadeOutSeconds = 1.2f;

        [Header("자막 (타자 효과)")]
        [Tooltip("1초에 치는 글자 수. ⚠ fitCaptionsToVoice 가 켜진 장면에서는 이 값 대신 " +
                 "음성 길이에서 계산한 속도를 쓴다")]
        [Min(1f)] [SerializeField] float charsPerSecond = 22f;

        [Tooltip("자막이 <b>내레이션보다 이만큼 먼저</b> 끝나게 한다(초). " +
                 "fitCaptionsToVoice 가 켜진 장면에만 쓰인다")]
        [Min(0f)] [SerializeField] float captionTailSeconds = 0.8f;

        [Tooltip("한 줄을 다 친 뒤 다음 줄로 넘어가기까지 쉬는 시간(초). " +
                 "그 줄에 atMusicTime 이 적혀 있으면 그쪽이 이긴다")]
        [Min(0f)] [SerializeField] float captionGapSeconds = 0.9f;

        [SerializeField] float captionFontSize = 40f;

        [Tooltip("자막 뒤에 깔아 밝은 배경에서도 글자가 읽히게 하는 아래쪽 그늘의 진하기. 0 이면 안 깐다")]
        [Range(0f, 1f)] [SerializeField] float captionShadeAlpha = 0.7f;

        [Header("건너뛰기")]
        [Tooltip("이 시간이 지나면 «건너뛰기» 안내가 뜬다(초)")]
        [Min(0f)] [SerializeField] float skipHintDelaySeconds = 3.5f;

        [SerializeField] string skipHintText = "PRESS SPACE TO SKIP";

        [Tooltip("건너뛸 때 검게 지는 시간(초). 0 이면 즉시 넘어간다")]
        [Min(0f)] [SerializeField] float skipFadeSeconds = 0.4f;

        [Header("씬")]
        [Tooltip("오프닝이 끝나면 열 씬. 빌드 세팅에 들어 있어야 한다")]
        [SerializeField] string nextSceneName = "Proto_01";

        // ────────────────────────────────────────────────────────────────

        Image _background;
        AspectRatioFitter _backgroundFit;
        CanvasGroup _curtain;
        TMP_Text _caption;
        CanvasGroup _skipHint;

        AudioSource _bgm;
        AudioSource _voice;

        /// <summary>
        /// 지금이 «노래의 몇 초» 인가.
        ///
        /// 브금이 돌고 있으면 <see cref="AudioSource.time"/> 이 정본이다 — 프레임이 튀어도
        /// 어긋나지 않는다. 브금이 없거나(에셋을 못 찾음) 곡이 끝난 뒤에는 이 값이 스스로
        /// 굴러가므로 <b>브금 없이도 연출은 그대로 돈다</b>.
        ///
        /// ⚠ <see cref="Update"/> 에서 <b>절대 뒤로 가지 않게</b> 큰 값을 취한다. 스트리밍
        ///   재생이 늦게 시작되면 <c>AudioSource.time</c> 이 한동안 0 이라, 그대로 받으면
        ///   시계가 뒤로 튀어 이미 지나간 대기 조건이 다시 잠긴다.
        /// </summary>
        float _clock;

        bool _running;
        bool _leaving;

        // ────────────────────────────────────────────────────────────────

        void Start()
        {
            // 음량은 씬마다 한 번 불러야 한다 — SaveService.ApplyVolume 주석 그대로.
            SaveService.ApplyVolume();

            // 배속·일시정지를 걸어둔 채 새로하기로 들어왔을 수 있다.
            Time.timeScale = 1f;

            BuildUi();
            BuildAudio();

            _running = true;
            StartCoroutine(Run());
            StartCoroutine(ShowSkipHint());
        }

        void Update()
        {
            _clock += Time.unscaledDeltaTime;
            if (_bgm != null && _bgm.isPlaying && _bgm.clip != null)
                _clock = Mathf.Max(_clock, _bgm.time);      // ★ 뒤로 가지 않는다

            if (_running && !_leaving && SkipRequested()) Skip();
        }

        /// <summary><see cref="LobbyPanel"/> 의 «누르면 건너뛰기» 와 같은 입력을 받는다.</summary>
        static bool SkipRequested()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

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

                // ① 이 장면이 시작될 «노래의 시각» 까지 검은 화면으로 기다린다.
                if (slide.atMusicTime > 0f)
                    while (_clock < slide.atMusicTime) yield return null;

                // ② 막이 내려간 동안 배경을 갈아끼운다 — 바뀌는 순간이 보이지 않는다.
                ApplyBackground(slide.background);
                _caption.text = string.Empty;
                float voiceLength = PlayVoice(slide.voice);

                // ③ 페이드 인
                yield return Fade(_curtain, 1f, 0f, fadeInSeconds);

                // ④ 자막을 차례대로 타자로 친다
                yield return TypeCaptions(slide, voiceLength);

                // ⑤ 페이드 아웃을 <b>언제</b> 시작할지 — 다음 장면이 노래에서 밀리지 않는 것이 우선이다.
                float? next = NextSlideMusicTime(i);
                float fadeOutAt = next.HasValue && next.Value > 0f
                    ? next.Value - fadeOutSeconds
                    : _clock + slide.holdAfterLastCaption;

                while (_clock < fadeOutAt) yield return null;

                yield return Fade(_curtain, _curtain.alpha, 1f, fadeOutSeconds);
            }

            yield return Leave();
        }

        /// <summary>다음 장면의 <see cref="Slide.atMusicTime"/>. 마지막 장면이면 <c>null</c>.</summary>
        float? NextSlideMusicTime(int index) =>
            index + 1 < slides.Length && slides[index + 1] != null
                ? slides[index + 1].atMusicTime
                : (float?)null;

        IEnumerator TypeCaptions(Slide slide, float voiceLength)
        {
            Caption[] captions = slide.captions;
            if (captions == null) yield break;

            float speed = SpeedFor(slide, voiceLength);

            for (int i = 0; i < captions.Length; i++)
            {
                Caption caption = captions[i];
                if (caption == null || string.IsNullOrEmpty(caption.text)) continue;

                if (caption.atMusicTime > 0f)
                {
                    // 노래(=음성)의 그 시각에 이 줄이 시작한다
                    while (_clock < caption.atMusicTime) yield return null;
                }
                else if (i > 0)
                {
                    float until = _clock + captionGapSeconds;
                    while (_clock < until) yield return null;
                }

                yield return Type(caption.text, speed);
            }
        }

        /// <summary>
        /// 이 장면의 타자 속도(초당 글자 수).
        ///
        /// <see cref="Slide.fitCaptionsToVoice"/> 가 켜져 있으면 «이 장면 자막의 총 글자 수» 를
        /// «내레이션에 남은 시간» 으로 나눈다 — 자막이 <b>음성보다
        /// <see cref="captionTailSeconds"/> 먼저</b> 끝난다. 문구를 고쳐도 시각을 다시 잴 필요가
        /// 없는 것이 이 방식의 값이다.
        ///
        /// ⚠ 자막은 페이드 인이 <b>끝난 뒤</b> 시작하므로(<see cref="Run"/> 의 ③→④) 예산에서
        ///   <see cref="fadeInSeconds"/> 를 뺀다. 줄 사이 쉬는 시간도 예산에서 빠진다.
        /// ⚠ 극단적인 값(너무 느려 안 읽히거나 순간에 다 떠버리는)은 <see cref="Mathf.Clamp"/>
        ///   으로 막는다 — 문구가 아주 짧거나 아주 길 때를 대비한 안전장치다.
        /// </summary>
        float SpeedFor(Slide slide, float voiceLength)
        {
            if (!slide.fitCaptionsToVoice || voiceLength <= 0f || slide.captions == null)
                return charsPerSecond;

            int chars = 0;
            int lines = 0;
            foreach (Caption caption in slide.captions)
            {
                if (caption == null || string.IsNullOrEmpty(caption.text)) continue;
                chars += caption.text.Length;
                lines++;
            }
            if (chars <= 0) return charsPerSecond;

            float budget = voiceLength
                           - fadeInSeconds
                           - captionTailSeconds
                           - captionGapSeconds * Mathf.Max(0, lines - 1);

            if (budget <= 0.25f) return charsPerSecond;

            return Mathf.Clamp(chars / budget, 6f, 90f);
        }

        /// <summary>
        /// 타자 효과. <b>글자를 하나씩 붙이지 않고</b> 문장을 한 번에 넣은 뒤
        /// <see cref="TMP_Text.maxVisibleCharacters"/> 만 늘린다.
        ///
        /// 붙이는 방식은 글자가 늘 때마다 줄바꿈이 다시 계산돼 <b>이미 나온 글자가 좌우로
        /// 흔들린다</b>(가운데 정렬이라 특히 심하다). 이쪽은 자리를 미리 다 잡아두고
        /// «보이는 개수» 만 바꾸므로 한 글자도 움직이지 않는다.
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
                shown += Time.unscaledDeltaTime * speed;
                _caption.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
        }

        // ── 끝내기 ──────────────────────────────────────────────────────

        void Skip()
        {
            StopAllCoroutines();
            _running = false;
            if (_skipHint != null) _skipHint.alpha = 0f;
            StartCoroutine(Leave(skipFadeSeconds));
        }

        /// <summary>
        /// 검게 지고 브금을 잦아들게 한 뒤 다음 씬을 연다.
        ///
        /// 막과 브금은 <b>길이가 다른</b> 두 페이드다(건너뛸 때는 막이 0.4초, 브금은 2.5초).
        /// 그래서 둘 중 긴 쪽을 한 바퀴로 잡고, 각자의 진행률을 따로 계산한다.
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

            Time.timeScale = 1f;
            SceneManager.LoadScene(nextSceneName);
        }

        // ── 잔심부름 ────────────────────────────────────────────────────

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

        IEnumerator ShowSkipHint()
        {
            if (_skipHint == null) yield break;

            float t = 0f;
            while (t < skipHintDelaySeconds) { t += Time.unscaledDeltaTime; yield return null; }

            yield return Fade(_skipHint, 0f, 1f, 0.8f);
        }

        void ApplyBackground(string resource)
        {
            Sprite sprite = LoadOnce<Sprite>(resource);
            _background.sprite = sprite;
            _background.enabled = sprite != null;

            if (sprite != null && _backgroundFit != null)
                _backgroundFit.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        }

        /// <summary>이 장면의 음성을 틀고 <b>길이(초)</b>를 돌려준다. 없으면 0.</summary>
        float PlayVoice(string resource)
        {
            AudioClip clip = LoadOnce<AudioClip>(resource);
            if (clip == null) return 0f;

            _voice.Stop();
            _voice.clip = clip;
            _voice.volume = voiceVolume;
            _voice.Play();
            return clip.length;
        }

        /// <summary>없는 에셋 경고를 여러 번 쏟지 않게 한 번만 남긴다.</summary>
        readonly HashSet<string> _missingWarned = new HashSet<string>();

        T LoadOnce<T>(string resource) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(resource)) return null;

            T asset = Resources.Load<T>(resource);
            if (asset == null && _missingWarned.Add(resource))
                Debug.LogWarning($"[오프닝] Resources/{resource} 를 찾지 못했습니다.", this);

            return asset;
        }

        // ── UI 짓기 ─────────────────────────────────────────────────────

        void BuildAudio()
        {
            // AudioSource 는 코드로 붙인다 — 씬에 배선할 것을 만들지 않기 위해서다
            // (BgmService.NewSource 와 같은 판단).
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _bgm.loop = false;              // ★ 시계로 쓰므로 감기면 안 된다
            _bgm.spatialBlend = 0f;         // 2D
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
            var canvasGo = new GameObject("OpeningCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;      // 오프닝은 무엇보다 앞이다

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasGo.GetComponent<RectTransform>();

            // ① 배경 — «액자» 로 잘라낸다.
            //    그림의 비율(16:9)이 화면과 다를 수 있으므로 EnvelopeParent(꽉 채우고 넘치는
            //    쪽을 밖으로 밀어냄)를 쓰고, 넘친 부분은 액자(RectMask2D)가 자른다.
            //    ★ preserveAspect 로는 안 된다 — 그쪽은 «안쪽에 맞추기» 라 위아래에 검은 띠가 생긴다.
            //    (LobbyPanel 의 «배경 액자(RectMask2D)» 와 같은 구성)
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

            // ② 자막 뒤 그늘 — 아래로 갈수록 진해지는 세로 그라데이션.
            //    통짜 반투명 띠는 «막대» 로 보여 그림을 가로막는다. 그라데이션은 경계가 없다.
            if (captionShadeAlpha > 0f)
            {
                RectTransform shade = NewRect("CaptionShade", root);
                shade.anchorMin = new Vector2(0f, 0f);
                shade.anchorMax = new Vector2(1f, 0f);
                shade.pivot = new Vector2(0.5f, 0f);
                shade.offsetMin = Vector2.zero;
                shade.offsetMax = new Vector2(0f, 420f);

                var shadeImage = shade.gameObject.AddComponent<Image>();
                shadeImage.sprite = BuildBottomGradient();
                shadeImage.raycastTarget = false;
                shadeImage.color = new Color(0f, 0f, 0f, captionShadeAlpha);
            }

            // ③ 자막
            RectTransform captionRect = NewRect("Caption", root);
            captionRect.anchorMin = new Vector2(0.5f, 0f);
            captionRect.anchorMax = new Vector2(0.5f, 0f);
            captionRect.pivot = new Vector2(0.5f, 0f);
            captionRect.sizeDelta = new Vector2(1560f, 300f);
            captionRect.anchoredPosition = new Vector2(0f, 110f);

            _caption = captionRect.gameObject.AddComponent<TextMeshProUGUI>();
            _caption.font = HudTheme.Font;               // 네오둥근모 (유저 지정)
            _caption.fontSize = captionFontSize;
            _caption.color = HudTheme.TextMain;
            _caption.alignment = TextAlignmentOptions.Bottom;
            _caption.textWrappingMode = TextWrappingModes.Normal;
            _caption.overflowMode = TextOverflowModes.Overflow;
            _caption.raycastTarget = false;
            _caption.text = string.Empty;

            // ④ 검은 막 — 자막까지 덮는다. 처음에는 완전히 검다(막이 걷히며 첫 장면이 뜬다).
            RectTransform curtainRect = NewRect("Curtain", root);
            Stretch(curtainRect);
            var curtainImage = curtainRect.gameObject.AddComponent<Image>();
            curtainImage.color = Color.black;
            curtainImage.raycastTarget = false;
            _curtain = curtainRect.gameObject.AddComponent<CanvasGroup>();
            _curtain.alpha = 1f;
            _curtain.blocksRaycasts = false;

            // ⑤ 건너뛰기 안내 — 막 <b>위</b>에 둔다. 장면 사이의 검은 화면에서도 보여야 한다.
            RectTransform hintRect = NewRect("SkipHint", root);
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.sizeDelta = new Vector2(560f, 48f);
            hintRect.anchoredPosition = new Vector2(-48f, 40f);

            var hint = hintRect.gameObject.AddComponent<TextMeshProUGUI>();
            hint.font = HudTheme.Font;
            hint.fontSize = 22f;
            hint.color = HudTheme.TextDim;
            hint.alignment = TextAlignmentOptions.BottomRight;
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.raycastTarget = false;
            hint.text = skipHintText;

            _skipHint = hintRect.gameObject.AddComponent<CanvasGroup>();
            _skipHint.alpha = 0f;
            _skipHint.blocksRaycasts = false;
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

        /// <summary>
        /// 아래로 갈수록 불투명해지는 1×64 세로 그라데이션을 코드로 굽는다.
        ///
        /// 그림 파일을 프로젝트에 하나 더 넣지 않으려는 것 — 이 그늘은 «자막이 읽히게 하는»
        /// 기능이라 아트가 관리할 대상이 아니고, 1픽셀 폭이라 늘려 써도 흐려지지 않는다.
        /// </summary>
        static Sprite BuildBottomGradient()
        {
            const int height = 64;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = "OpeningCaptionShade",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int y = 0; y < height; y++)
            {
                // y=0 이 아래쪽. 아래가 진하고 위로 갈수록 투명해진다.
                float up = y / (float)(height - 1);
                float alpha = 1f - up;
                alpha *= alpha;             // 제곱 — 위쪽이 더 빨리 사라져 경계가 보이지 않는다
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            }
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f));
        }
    }
}
