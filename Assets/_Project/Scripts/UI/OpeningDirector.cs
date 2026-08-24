using System;
using System.Collections;
using System.Collections.Generic;
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
    /// 새 게임을 시작할 때 나오는 <b>오프닝 연출</b> (2026-08-24 신설 — 유저 지시
    /// <i>"오프닝 씬 만들 거야 새 게임 시작하면 나오는 거고 … 각 배경 화면 넘어갈때마다 노래
    /// 타이밍 바뀌고 텍스트에 맞춰서 자막도 차례대로 타자로 치는것처럼 등장하게 해줘. 영어에
    /// 맞춰서. 배경 화면 넘어갈때는 페이드 인 페이드 아웃 … 폰트는 네오 둥근모 …
    /// 이 오프닝은 하드 코딩 해도 됨"</i>).
    ///
    /// 쓰는 에셋 (볼트 <c>리소스/</c> 에서 가져와 <c>Resources/</c> 로 넣었다) —
    /// <code>
    ///   Resources/Opening/BG_01 ~ BG_04      ← 볼트의 opening_BG.png 를 2×2 로 잘라낸 넉 장
    ///                                          ⚠ 자른 순서 ≠ 이야기 순서 (<see cref="slides"/>)
    ///   Resources/Opening/VO_01_1 ~ VO_04_4  ← 볼트 voice/ 의 <b>유저가 문장별로 나눠 준</b>
    ///                                          16개(01-01 … 04-04) (Tools/import_opening_voice.py)
    ///   Resources/Bgm/The Fall of the Sanctuary  ← 오프닝 브금 (1분 59초)
    ///   Resources/UI/Lobby/LobbyButtonSkip   ← 건너뛰기 버튼 판 (로비 버튼을 가로만 잘라낸 것)
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
    /// <b>㉡ 한 조각 = 자막 + 음성 + 시각</b> (2026-08-24 개편 — <see cref="Caption"/> 의 설명).
    /// 음성이 조각으로 나뉘어 있으므로 조각마다 «브금의 몇 초에 말을 시작할지»
    /// (<see cref="Caption.atMusicTime"/>)를 적는다. <b>조각 사이의 텀이 데이터가 된다</b> —
    /// 앞 조각이 끝나도 다음 조각의 시각까지 기다리므로 그 차이가 곧 텀이고, 텀을 바꾸려면
    /// 숫자만 밀면 된다(오디오를 다시 굽지 않는다).
    /// 타자 속도는 <b>그 조각 음성의 길이</b>에서 계산하므로(<see cref="SpeedFor"/>) 문구를
    /// 고쳐도 자막은 여전히 <b>그 조각을 말하는 동안</b> 다 쳐진다.
    ///
    /// <b>㉢ 페이드는 «검은 막» 하나로 한다</b> — 배경 두 장을 겹쳐 크로스페이드하지 않는다.
    /// 유저가 요청한 것은 «페이드 인 / 페이드 아웃»(검게 지고 검은 데서 다시 밝아지는 것)이고,
    /// 막 하나면 <b>자막까지 같이</b> 사라진다. 그래서 하이라키를 이렇게 쌓았다 —
    /// <code>
    ///   Background   (액자 + 그림)      ← 맨 아래
    ///   CaptionShade (아래쪽 그늘)
    ///   Caption      (자막)
    ///   Curtain      (검은 막)          ← 자막까지 덮는다
    ///   SkipButton   (건너뛰기 버튼)    ← 막 위. 검은 화면에서도 보이고 눌린다
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

        /// <summary>
        /// ★★ <b>문장 하나</b> — 자막 · 그 문장의 음성 · 말을 시작하는 시각이 <b>한 덩어리</b>다
        /// (2026-08-24 개편. 유저 지시: <i>"문장 별로 너무 빨리 이어져서 … 문장 별로 나눠서 좀 더
        /// 텀을 두고 말하게 만들 수 있음?"</i> → <i>"음성 파일 자체를 짤라서 하면 되자나"</i>).
        ///
        /// <b>예전에는 음성이 «장면»에 붙어 있었다</b>(<c>Slide.voice</c> 한 개 = 문장 두세 개가
        /// 이어 붙은 통짜 파일). 그래서 문장 사이의 텀이 <b>녹음에 구워져 있었고</b> 연출로는
        /// 손댈 수 없었다 — 유저가 «너무 빨리 이어진다» 고 한 것이 그것이다.
        ///
        /// 지금은 음성이 조각으로 나뉘어 있고, 조각마다 <see cref="atMusicTime"/> 에 «브금의 몇 초에
        /// 말을 시작할지» 를 적는다. 그러면 <b>텀이 데이터가 된다</b> — 숫자를 고치면 텀이 바뀌고,
        /// 오디오는 다시 굽지 않는다.
        ///
        /// ★★ 2026-08-24 <b>조각은 유저가 직접 나눠 준다</b> (유저 지시: <i>"오프닝 목소리 수정한거
        /// 반영 좀 문장별로 다 분리해서 파일 만들어 놨으니까 이거 바탕으로 다시 배치해줘"</i>).
        /// 처음에는 통짜 음성 4개를 <b>내가 소리로 분석해</b> 11조각으로 갈랐다(경계를 찾느라
        /// 방법을 세 번 틀렸다 — 진행상황 139-1절). 이제 볼트에 <b>16개</b>가 나뉘어 들어오므로
        /// 그 추측이 필요 없다 — 경계는 «추정» 이 아니라 «주어진 것» 이다.
        ///
        /// ⚠ 조각은 <b>문장이 아니라 절(clause) 단위</b>다(컷 2가 6조각). 그래서 텀도 두 단계다 —
        ///   <see cref="slides"/> 의 설명 참조.
        /// </summary>
        [Serializable]
        public class Caption
        {
            [Tooltip("자막 문구(한글). 줄바꿈은 \\n 으로 <b>손으로</b> 나눈다 — slides 의 설명 참조")]
            [TextArea(1, 4)] public string text;

            [Tooltip("이 문장의 음성 — Resources 아래의 경로(확장자 없이). 없으면 비워둔다.\n" +
                     "예: Opening/VO_02_3 (컷 2의 셋째 문장)")]
            public string voice;

            [Tooltip("이 문장이 <b>말을 시작하는 브금의 시각(초)</b>. 음성과 자막이 함께 시작한다.\n" +
                     "0 이면 앞 문장을 다 친 뒤 captionGapSeconds 만큼 쉬고 이어 친다")]
            [Min(0f)] public float atMusicTime;
        }

        /// <summary>배경 한 장 + 그 위에 차례로 지나가는 문장들.</summary>
        [Serializable]
        public class Slide
        {
            [Tooltip("배경 그림 — Resources 아래의 경로(확장자 없이)")]
            public string background;

            [Tooltip("이 장면이 <b>페이드 인을 시작하는</b> 브금의 시각(초). " +
                     "0 이면 앞 장면이 검게 진 직후 바로 시작한다")]
            [Min(0f)] public float atMusicTime;

            [Tooltip("자막의 타자 속도를 <b>그 문장 음성의 길이에 맞춰</b> 정한다. " +
                     "켜두면 문장을 말하는 동안 자막이 다 쳐진다")]
            public bool fitCaptionsToVoice = true;

            [Tooltip("<b>말이 끝난 뒤</b> 머무는 시간(초).\n" +
                     "⚠ 자막이 끝난 뒤가 아니라 <b>음성이 끝난 뒤</b>부터 잰다 — 자막은 말보다 " +
                     "먼저 끝나므로 자막 기준으로 재면 마지막 문장이 잘린다.\n" +
                     "⚠ 다음 장면에 atMusicTime 이 적혀 있으면 그쪽이 이긴다")]
            [Min(0f)] public float holdAfterLastCaption = 2f;

            public Caption[] captions;
        }

        /// <summary>
        /// ★★ <b>오프닝 대본</b>.
        ///
        /// ★★ <b>시각은 전부 <c>Tools/import_opening_voice.py</c> 가 계산해 찍어준다</b>.
        /// 손으로 재지 말 것 — 조각이 16개라 하나를 고치면 뒤가 다 밀린다. 그 스크립트에서
        /// 값을 고쳐 다시 돌리면 <b>이 표에 그대로 옮길 시각표</b>를 찍어 준다.
        ///
        /// ★★★ <b>시각이 노래의 «박» 에 맞춰져 있다</b> (2026-08-24 유저 지시: <i>"텀은 노래
        /// 타이밍에 맞춰서 전환 해주면 베스트 불가능하면 현재로 유지"</i>).
        ///
        /// 브금을 분석해 격자를 <b>실측</b>했다 — <b>63.80 BPM · 4/4 · 박 0.9404초 · 마디 3.7616초 ·
        /// 첫 마디 1.590초</b>. 격자 선명도 1.94(1.0 이면 «격자가 무의미»)이고 다운비트 대비가
        /// 2.30(3박자 후보는 1.16)이라 <b>맞출 만한 곡</b>이다. 박의 3분할(191.4 = 63.8 x 3)이
        /// 뚜렷해 잘게 맞출 때는 박을 <b>셋으로</b> 나눈다(0.3135초).
        ///
        /// 그래서 지금 —
        /// <code>
        ///   컷 전환(slide.atMusicTime)  = <b>박</b> 위에 놓인다
        ///   조각 시작(caption.atMusicTime) = <b>박의 1/3</b> 위에 놓인다
        /// </code>
        /// <b>텀은 «격자까지의 거리» 로 저절로 정해진다</b> — 최소 텀(문장 0.35 · 절 0.15)만 지나면
        /// 다음 격자점으로 <b>올려</b> 붙이므로, 실제 텀은 0.18~0.63초 사이에서 <b>노래에 맞게</b>
        /// 들쭉날쭉하다. 손으로 정한 균일한 텀보다 이것이 «음악에 맞춰 읽는» 소리에 가깝다.
        ///
        /// ⚠ <b>마디(다운비트)에 맞추는 것은 못 했다</b> — 1.33초가 모자란다. 내레이션 16조각이
        ///   93.07초이고 페이드·머묾을 더하면 브금 119.65초를 거의 다 쓴다. 마디 격자(3.76초)로
        ///   올리면 컷 셋에서 평균 1.9초씩 버려져 브금을 넘긴다. 그래서 <b>한 단계 잘게</b>
        ///   («박») 맞췄다. 마디까지 맞추려면 페이드나 머묾을 1.5초쯤 줄여야 한다 —
        ///   그것은 연출의 <b>모양</b>을 바꾸는 결정이므로 하지 않았다.
        ///   (스크립트가 격자 후보 다섯을 전부 시험해 «브금 안에 들어오는 가장 센 격자» 를 고른다)
        ///
        /// ★ 최소 텀이 어느 쪽인지는 <b>앞 자막의 «끝 글자» 가 정한다</b> — 마침표로 끝나면 문장,
        /// 쉼표나 줄표(—)로 끝나면 절이다. 대본을 눈으로 읽으면 바로 보이고, 따로 관리하는 표가
        /// 없으니 어긋날 일도 없다.
        ///
        /// 그 규칙으로 나온 지금의 시각표 (조각 3 · 6 · 3 · 4 = 16) —
        /// <code>
        ///   컷 1  1.59 시작 :  3.47(6.16초) · 10.05(4.73) · 15.07(5.38)              → 20.45 끝
        ///   컷 2 23.22      : 25.10(3.84) · 29.49(3.71) · 33.56(7.52) ·
        ///                     41.71(2.77) · 44.85(5.46) · 50.49(6.45)                → 56.94
        ///   컷 3 59.90      : 61.78(5.25) · 67.42(9.27) · 77.14(9.43)                → 86.57
        ///   컷 4 89.99      : 91.87(5.85) · 98.14(6.27) · 105.03(8.39) · 113.81(2.59) → 116.40
        ///                     그 뒤 1.5초 머물고 검게 져 119.10초에 게임 씬으로
        ///   (브금 The Fall of the Sanctuary 는 119.65초 — 0.55초 남는다)
        /// </code>
        /// 컷 1 은 <b>첫 마디</b>(1.590)에서 시작하고, 컷 3·4 의 첫 조각(61.78 · 91.87)은
        /// 우연히 <b>마디 위</b>에 떨어졌다.
        /// ⚠ 브금보다 길어져도 <b>멈추지는 않는다</b> — <see cref="_clock"/> 은 브금이 끝나도
        ///   <see cref="Time.unscaledDeltaTime"/> 으로 계속 흐른다. 다만 마지막이 무음에서
        ///   끝나므로 스크립트가 «브금 안에 들어오는지» 를 찍어 확인해 준다.
        ///
        /// ★★ <b>줄바꿈은 손으로 나눈다</b> (2026-08-24 유저 지시: <i>"오프닝 줄 바꿈 좀 깔끔하게
        /// 해줘 지금 너무 지저분 함"</i>).
        ///
        /// 문구를 한 덩어리로 넣고 <see cref="TMP_Text"/> 의 자동 줄바꿈에 맡기면, 줄이 <b>칸 폭이
        /// 다하는 곳에서</b> 끊긴다 — 「하늘은 순식간에 핏빛으로 / 물들었고」처럼 <b>구(句) 가운데를
        /// 가른다</b>. 한글은 어절이 길어 이 사고가 특히 잦고, 왼쪽 정렬이라 오른쪽 끝의 들쭉날쭉함이
        /// 그대로 눈에 남는다. 게다가 타자 효과가 그 자리에서 <b>한 글자씩</b> 지나가므로 «어색한
        /// 지점»을 유저가 오래 본다.
        ///
        /// 그래서 <c>\n</c> 을 <b>대본에 직접</b> 넣어 «한 줄 = 한 구» 로 맞췄다. 소스의 줄 모양이
        /// 곧 화면의 줄 모양이다.
        ///
        /// ★★ 2026-08-24 조각을 16개로 나눈 뒤에는 <b>줄바꿈이 하나도 없다</b> — 조각이 절 단위라
        /// 자막이 죄다 <b>한 줄</b>에 들어간다(가장 긴 것이 30자). 손으로 나눌 일이 없어졌지만
        /// 규칙은 남겨 둔다 — 문구를 길게 고치면 다시 필요해진다.
        /// 칸(<see cref="captionSize"/> 1440px · 40pt)은 <b>40자쯤</b>이 들어간다 —
        /// 자동 줄바꿈은 <b>끄지 않았다</b>. 해상도 비율이 달라져 한 줄이 넘칠 때 화면 밖으로
        /// 삐져나가는 것보다 <b>접히는</b> 편이 안전하다(평소에는 여유 7자 덕에 발동하지 않는다).
        ///
        /// ⚠ <b>문구를 고치면 줄도 다시 나눠야 한다.</b> 이것이 손으로 나누는 값의 대가다 —
        ///   한 줄이 <b>35자</b>를 넘지 않게, 쉼표·줄표(—) 뒤에서 끊는 것을 기준으로 삼을 것.
        ///
        /// <b>자막은 한글이다</b> (2026-08-24 유저 지시 — <i>"이렇게 한글로만 넣고"</i>).
        /// 음성은 영어 내레이션 그대로지만 화면에 뜨는 글은 유저가 준 한글 문구다. 길이가
        /// 영어와 달라도 타자 속도는 <see cref="Slide.fitCaptionsToVoice"/> 가 음성 길이에서
        /// 다시 계산하므로 <b>시각을 손댈 일이 없다</b>(㉡).
        ///
        /// ★★ <b>배경을 대사에 맞춰 다시 짝지었다</b> (같은 지시 — <i>"이미지 배경이 컷이랑
        /// 안 맞아 한글대사 읽어보고 해당 대사에 맞춰서 해줘"</i>). 넉 장은 볼트 그림을 2×2 로
        /// 자른 순서(BG_01~04)일 뿐이고 <b>이야기 순서가 아니었다</b> —
        /// <code>
        ///   1컷 «순백으로 빛나던 시절 · 천사들의 노래»  → BG_04 (흰 성역 · 맑은 하늘)
        ///   2컷 «빛이 꺼지고 하늘이 핏빛 · 갑주를 여미고» → BG_01 (붉은 일식 · 진군하는 기사들)
        ///   3컷 «성문이 무너지고 짐승이 울부짖는다»      → BG_03 (불타는 성 · 용과 마군)
        ///   4컷 «쓰러진 이들 · 지켜지지 못한 맹세»        → BG_02 (주저앉은 천사 · 잿더미)
        /// </code>
        /// ⚠ <b>음성(VO)의 순서는 그대로다</b> — 내레이션은 이미 이야기 순서로 녹음되어 있다.
        ///   바꾼 것은 «어느 그림을 어느 컷에 쓸지» 뿐이다.
        ///
        /// ⚠⚠ <b>이 표는 씬에도 복사되어 있다</b>(<c>Opening.unity</c> 의 OpeningDirector).
        ///    <see cref="SerializeField"/> 이므로 <b>씬에 저장된 값이 이 코드보다 이긴다</b> —
        ///    여기만 고치면 게임에는 <b>안 바뀐 채로 보인다</b>. 코드를 고친 뒤에는 둘 중 하나를
        ///    해야 한다: ① 인스펙터에서 값을 같이 고친다, 또는 ② 컴포넌트 톱니바퀴 →
        ///    <b>Reset</b> 으로 씬의 값을 코드 기본값으로 되돌린다(다른 설정도 함께 초기화된다).
        /// </summary>
        [Header("대본 (배경 · 조각 = 자막 + 음성 + 시각)")]
        [SerializeField] Slide[] slides =
        {
            new Slide
            {
                background  = "Opening/BG_04",
                atMusicTime = 1.59f,
                captions = new[]
                {
                    new Caption
                    {
                        voice       = "Opening/VO_01_1",     // 6.16초
                        atMusicTime = 3.47f,
                        text = "기억한다 — 이 성역이 순백으로 빛나던 시절을.",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_01_2",     // 4.73초
                        atMusicTime = 10.05f,
                        text = "천사들의 노래가 첨탑마다 울려 퍼졌고,",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_01_3",     // 5.38초
                        atMusicTime = 15.07f,
                        text = "그 어떤 어둠도 이 문턱을 넘지 못했다.",
                    },
                },
            },
            new Slide
            {
                background  = "Opening/BG_01",
                atMusicTime = 23.22f,
                captions = new[]
                {
                    new Caption
                    {
                        voice       = "Opening/VO_02_1",     // 3.84초
                        atMusicTime = 25.10f,
                        text = "그 빛은 꺼졌다.",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_02_2",     // 3.71초
                        atMusicTime = 29.49f,
                        text = "하늘은 순식간에 핏빛으로 물들었고,",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_02_3",     // 7.52초
                        atMusicTime = 33.56f,
                        text = "노도와 같은 어둠은 그들을 집어삼켰다.",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_02_4",     // 2.77초
                        atMusicTime = 41.71f,
                        text = "터전을 지키는 데에,",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_02_5",     // 5.46초
                        atMusicTime = 44.85f,
                        text = "그 이유는 중요치 않으리 — 그들은 영문도 모른 채,",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_02_6",     // 6.45초
                        atMusicTime = 50.49f,
                        text = "갑주를 여미고, 짙어지는 어둠을 향해 나아갈 뿐이었다.",
                    },
                },
            },
            new Slide
            {
                background  = "Opening/BG_03",
                atMusicTime = 59.90f,
                captions = new[]
                {
                    new Caption
                    {
                        voice       = "Opening/VO_03_1",     // 5.25초
                        atMusicTime = 61.78f,
                        text = "성문은 무너졌고, 하늘에서는 짐승이 울부짖는다.",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_03_2",     // 9.27초
                        atMusicTime = 67.42f,
                        text = "불길은 자비를 모르고, 어둠은 뿌리처럼 번져간다.",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_03_3",     // 9.43초
                        atMusicTime = 77.14f,
                        text = "남은 것은 잿더미와, 지켜지지 못한 맹세뿐.",
                    },
                },
            },
            new Slide
            {
                background  = "Opening/BG_02",
                atMusicTime = 89.99f,
                holdAfterLastCaption = 1.5f,
                captions = new[]
                {
                    new Caption
                    {
                        voice       = "Opening/VO_04_1",     // 5.85초
                        atMusicTime = 91.87f,
                        text = "쓰러진 이들의 이름을 나는 다 기억하지 못한다.",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_04_2",     // 6.27초
                        atMusicTime = 98.14f,
                        text = "그러나 그들이 지키려 했던 것만은 잊지 않았다.",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_04_3",     // 8.39초
                        atMusicTime = 105.03f,
                        text = "그대여, 마지막 성역이 완전히 저물기 전에 —",
                    },
                    new Caption
                    {
                        voice       = "Opening/VO_04_4",     // 2.59초
                        atMusicTime = 113.81f,
                        text = "나서라.",
                    },
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

        /// <summary>
        /// ★ <b>자막은 왼쪽 정렬이다</b> (2026-08-24 유저 지시: <i>"자막 위치를 중앙정열로
        /// 하지말고 왼쪽 정렬로 해서 깔끔하게 다듬어줘"</i>).
        ///
        /// 가운데 정렬은 <b>줄마다 시작 x 가 달라진다</b> — 줄이 넘어갈 때마다 눈이 좌우로
        /// 튀고, 타자 효과가 «왼쪽에서 오른쪽으로 쳐 나가는» 것과도 어긋난다(글자가 늘 때마다
        /// 줄 전체가 옮겨 앉는 것처럼 보인다). 왼쪽 정렬은 모든 줄의 시작이 <b>한 선</b>에
        /// 맞아서 여러 줄을 읽어도 자리를 잃지 않는다.
        ///
        /// ⚠ 칸을 <b>화면 폭보다 좁게</b> 잡는다(1440 &lt; 1920). 왼쪽 정렬은 오른쪽 끝이
        ///   들쭉날쭉해서, 칸이 화면 폭에 가까우면 한 줄이 너무 길어져 오히려 읽기 어렵다.
        ///   40pt 한글로 한 줄에 35자쯤 — 4컷 중 가장 긴 2컷(116자)이 4줄이다.
        /// </summary>
        [Tooltip("자막 칸의 크기(px · 1920x1080 기준). " +
                 "한 줄이 너무 길어지지 않게 <b>화면 폭보다 좁다</b>")]
        [SerializeField] Vector2 captionSize = new Vector2(1440f, 320f);

        [Tooltip("자막 칸을 화면 <b>왼쪽 아래</b> 모서리에서 띄우는 여백(px). " +
                 "x 가 곧 «모든 줄이 시작하는 선» 이다")]
        [SerializeField] Vector2 captionMargin = new Vector2(180f, 110f);

        [Tooltip("줄 사이를 조금 벌린다(글꼴 크기 대비 %). " +
                 "여러 줄이 붙어 보이면 왼쪽 정렬의 «시작선» 이 덩어리로 뭉쳐 보인다")]
        [SerializeField] float captionLineSpacing = 12f;

        [Tooltip("자막 뒤에 깔아 밝은 배경에서도 글자가 읽히게 하는 아래쪽 그늘의 진하기. 0 이면 안 깐다")]
        [Range(0f, 1f)] [SerializeField] float captionShadeAlpha = 0.7f;

        [Tooltip("아래쪽 그늘의 높이(px). 자막 칸의 위끝보다 넉넉히 높아야 " +
                 "글자 윗줄이 그늘 밖으로 삐져나오지 않는다")]
        [Min(0f)] [SerializeField] float captionShadeHeight = 480f;

        /// <summary>
        /// ★★ <b>건너뛰기는 «버튼» 으로만 한다</b> (2026-08-24 유저 지시: <i>"오프닝은 클릭으로
        /// 스킵되지 말고 클릭하면 스킵되어 버리는데 오른쪽 위에 스킵 버튼 만들어서 해당 버튼
        /// 누르면 스킵되게 만들어줘"</i>).
        ///
        /// 처음에는 «화면 아무 곳이나 누르면 건너뛴다» 였다(<see cref="LobbyPanel"/> 의 «누르면
        /// 넘어가기» 를 그대로 가져온 것). 그런데 오프닝은 <b>2분을 보는 연출</b>이라 손이
        /// 미끄러진 한 번의 클릭으로 <b>전부 날아간다</b>. 되돌릴 방법도 없다 — 그래서 건너뛰기는
        /// «누를 곳이 정해진 버튼» 이어야 한다.
        ///
        /// ⚠ 키보드(스페이스·엔터·ESC)는 <b>남겨 둔다</b> — 실수로 눌릴 위험이 있는 것은
        ///   «화면 아무 곳이나» 인 마우스 클릭이고, 그것만 뺐다(<see cref="SkipRequested"/>).
        /// </summary>
        [Header("건너뛰기 버튼")]
        [Tooltip("이 시간이 지나면 «건너뛰기» 버튼이 떠오른다(초)")]
        [Min(0f)] [SerializeField] float skipButtonDelaySeconds = 3.5f;

        [SerializeField] string skipButtonText = "건너뛰기";

        [Tooltip("버튼 판 그림 — Resources 아래의 경로(확장자 없이). " +
                 "로비 버튼 판을 <b>가로만 잘라낸</b> 그림이다 (Tools/make_skip_button_art.py)")]
        [SerializeField] string skipButtonResource = "UI/Lobby/LobbyButtonSkip";

        [Tooltip("버튼 칸의 크기(px · 1920x1080 기준). " +
                 "로비 버튼 칸(360x70)과 <b>같은 픽셀 축척</b>이라 장식이 같은 두께로 보인다")]
        [SerializeField] Vector2 skipButtonSize = new Vector2(200f, 70f);

        [Tooltip("화면 <b>오른쪽 위</b> 모서리에서 띄우는 여백(px)")]
        [SerializeField] Vector2 skipButtonMargin = new Vector2(40f, 24f);

        [Tooltip("버튼 글자 크기 — 로비 버튼 라벨과 같은 20")]
        [Min(1f)] [SerializeField] float skipButtonFontSize = 20f;

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
        CanvasGroup _skipButton;

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

        /// <summary>
        /// 지금 틀고 있는 음성이 <b>끝나는 브금 시각</b>. 마지막 장면이 «말이 끝난 뒤» 머무는
        /// 시간을 재는 근거다(<see cref="Run"/> 의 ⑤).
        ///
        /// ⚠ 자막이 끝난 시각(<see cref="_clock"/>)으로 재면 안 된다 — 자막은 말보다
        ///   <see cref="captionTailSeconds"/> 먼저 끝나도록 속도를 정하므로, 그 기준으로 재면
        ///   <b>마지막 문장을 말하는 중에</b> 화면이 검게 진다.
        /// </summary>
        float _voiceEndsAt;

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
            StartCoroutine(ShowSkipButton());
        }

        void Update()
        {
            _clock += Time.unscaledDeltaTime;
            if (_bgm != null && _bgm.isPlaying && _bgm.clip != null)
                _clock = Mathf.Max(_clock, _bgm.time);      // ★ 뒤로 가지 않는다

            if (_running && !_leaving && SkipRequested()) Skip();
        }

        /// <summary>
        /// 건너뛰기 <b>키</b>. 마우스는 여기 없다 — 화면 아무 곳이나 눌러 건너뛰는 것을 막는 것이
        /// 이 연출의 요구사항이고(<see cref="skipButtonDelaySeconds"/> 의 설명), 클릭은
        /// <b>버튼만</b> 받는다.
        /// </summary>
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

                // ① 이 장면이 시작될 «노래의 시각» 까지 검은 화면으로 기다린다.
                if (slide.atMusicTime > 0f)
                    while (_clock < slide.atMusicTime) yield return null;

                // ② 막이 내려간 동안 배경을 갈아끼운다 — 바뀌는 순간이 보이지 않는다.
                //    ★ 음성은 여기서 틀지 않는다 — 문장마다 <b>제 시각에</b> 제 음성을 튼다(④).
                ApplyBackground(slide.background);
                _caption.text = string.Empty;
                _voiceEndsAt = _clock;

                // ③ 페이드 인
                yield return Fade(_curtain, 1f, 0f, fadeInSeconds);

                // ④ 문장을 차례대로 — 각 문장이 «제 시각에» 말을 시작하고 자막이 같이 쳐진다
                yield return TypeCaptions(slide);

                // ⑤ 페이드 아웃을 <b>언제</b> 시작할지 — 다음 장면이 노래에서 밀리지 않는 것이 우선이다.
                //    ⚠ 마지막 장면은 «말이 끝난 뒤»(_voiceEndsAt)부터 머문다 — 자막은 말보다 먼저
                //      끝나므로 _clock 만 보면 <b>마지막 문장이 잘린 채</b> 검게 진다.
                float? next = NextSlideMusicTime(i);
                float fadeOutAt = next.HasValue && next.Value > 0f
                    ? next.Value - fadeOutSeconds
                    : Mathf.Max(_clock, _voiceEndsAt) + slide.holdAfterLastCaption;

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

        /// <summary>
        /// 문장을 차례대로 — <b>제 시각에 · 제 음성으로 · 제 자막으로</b>.
        ///
        /// ★ <b>조각 사이의 텀은 여기서 «저절로» 생긴다.</b> 앞 조각의 음성이 끝나도 다음 조각의
        /// <see cref="Caption.atMusicTime"/> 까지는 기다리므로, 그 차이가 곧 텀이다. 텀을 늘리려면
        /// 표의 시각만 밀면 되고 <b>오디오는 손대지 않는다</b> — 그 시각은
        /// <c>Tools/import_opening_voice.py</c> 가 <b>노래의 박자 격자에 맞춰</b> 계산해 준다
        /// (<see cref="slides"/> 의 설명).
        ///
        /// ★ <b>앞 문장의 자막은 지우지 않는다</b> — 텀 동안 화면에 그대로 남아 있고, 다음 문장이
        /// 쳐지기 시작할 때 <see cref="Type"/> 이 갈아끼운다. 텀마다 화면이 비면 «끊긴» 느낌이 난다.
        /// </summary>
        IEnumerator TypeCaptions(Slide slide)
        {
            Caption[] captions = slide.captions;
            if (captions == null) yield break;

            for (int i = 0; i < captions.Length; i++)
            {
                Caption caption = captions[i];
                if (caption == null || string.IsNullOrEmpty(caption.text)) continue;

                if (caption.atMusicTime > 0f)
                {
                    // 노래의 그 시각에 이 문장이 말을 시작한다
                    while (_clock < caption.atMusicTime) yield return null;
                }
                else if (i > 0)
                {
                    float until = _clock + captionGapSeconds;
                    while (_clock < until) yield return null;
                }

                float voiceLength = PlayVoice(caption.voice);
                yield return Type(caption.text, SpeedFor(slide, caption, voiceLength));
            }
        }

        /// <summary>
        /// <b>이 문장</b>의 타자 속도(초당 글자 수).
        ///
        /// <see cref="Slide.fitCaptionsToVoice"/> 가 켜져 있으면 «이 문장의 글자 수» 를
        /// «이 문장 음성의 길이» 로 나눈다 — 자막이 <b>그 문장을 말하는 동안</b>
        /// (끝나기 <see cref="captionTailSeconds"/> 전에) 다 쳐진다.
        ///
        /// ★ <b>장면 단위가 아니라 문장 단위로 잰다</b>(2026-08-24 개편). 예전에는 «장면의 총
        /// 글자 수 ÷ 통짜 음성 길이» 였다 — 문장마다 길이가 다르면 어떤 문장은 말보다 한참
        /// 먼저 끝나고 어떤 문장은 말이 끝난 뒤에도 계속 쳐졌다. 이제 음성이 문장별로 잘려
        /// 있으므로(<see cref="Caption.voice"/>) 문장마다 제 예산으로 잰다.
        ///
        /// ⚠ 페이드 인·줄 사이 쉬는 시간을 예산에서 뺄 일이 <b>없어졌다</b> — 문장의 시작이
        ///   <see cref="Caption.atMusicTime"/> 으로 못박혀 있어 예산이 곧 음성 길이다.
        /// ⚠ 극단적인 값(너무 느려 안 읽히거나 순간에 다 떠버리는)은 <see cref="Mathf.Clamp"/>
        ///   으로 막는다 — 문구가 아주 짧거나 아주 길 때를 대비한 안전장치다.
        /// </summary>
        float SpeedFor(Slide slide, Caption caption, float voiceLength)
        {
            if (!slide.fitCaptionsToVoice || voiceLength <= 0f
                || caption == null || string.IsNullOrEmpty(caption.text))
                return charsPerSecond;

            float budget = voiceLength - captionTailSeconds;
            if (budget <= 0.25f) return charsPerSecond;

            return Mathf.Clamp(caption.text.Length / budget, 6f, 90f);
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

        /// <summary>
        /// 건너뛴다. 버튼의 <c>onClick</c> 과 <see cref="SkipRequested"/> 가 함께 부른다.
        ///
        /// ⚠ <b>두 번 눌리는 것을 막는다</b> — 버튼을 연타하면 <see cref="Leave"/> 가 두 번 돌아
        ///   씬을 두 번 열려 한다. 막이 지는 0.4초는 연타하기에 충분한 시간이다.
        ///   (<see cref="Leave"/> 의 <c>_leaving</c> 도 같은 사고를 막지만, 버튼을 아예
        ///   먹통으로 만들어 <b>눌린 표시조차 나지 않게</b> 하는 편이 손에 맞다.)
        /// </summary>
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

        /// <summary>
        /// 건너뛰기 버튼을 <see cref="skipButtonDelaySeconds"/> 뒤에 떠오르게 한다.
        ///
        /// ⚠ <b>다 뜬 뒤에야 눌린다</b>(<c>blocksRaycasts</c>) — 반투명하게 떠오르는 중에
        ///   눌리면 «안 보이는 버튼을 눌렀다» 가 된다. 로비가 버튼을 하나씩 띄울 때 내린
        ///   결론과 같다(<see cref="LobbyPanel"/> 의 FadeInButton).
        /// </summary>
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

        /// <summary>이 장면의 음성을 틀고 <b>길이(초)</b>를 돌려준다. 없으면 0.</summary>
        float PlayVoice(string resource)
        {
            AudioClip clip = LoadOnce<AudioClip>(resource);
            if (clip == null) return 0f;

            _voice.Stop();
            _voice.clip = clip;
            _voice.volume = voiceVolume;
            _voice.Play();

            _voiceEndsAt = _clock + clip.length;    // ★ «말이 끝나는 시각» 을 남긴다 (Run 의 ⑤)
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
                shade.offsetMax = new Vector2(0f, captionShadeHeight);

                var shadeImage = shade.gameObject.AddComponent<Image>();
                shadeImage.sprite = BuildBottomGradient();
                shadeImage.raycastTarget = false;
                shadeImage.color = new Color(0f, 0f, 0f, captionShadeAlpha);
            }

            // ③ 자막 — 화면 <b>왼쪽 아래</b>에 붙이고 <b>왼쪽 정렬</b>한다 (captionSize 의 설명).
            //    ★ 칸을 왼쪽 아래 모서리에 앵커하므로 여백(captionMargin)이 그대로
            //      «글자가 시작하는 선» 이 된다 — 화면 비율이 달라져도 그 선은 안 움직인다.
            RectTransform captionRect = NewRect("Caption", root);
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(0f, 0f);
            captionRect.pivot = new Vector2(0f, 0f);
            captionRect.sizeDelta = captionSize;
            captionRect.anchoredPosition = captionMargin;

            _caption = captionRect.gameObject.AddComponent<TextMeshProUGUI>();
            _caption.font = HudTheme.Font;               // 네오둥근모 (유저 지정)
            _caption.fontSize = captionFontSize;
            _caption.color = HudTheme.TextMain;
            _caption.alignment = TextAlignmentOptions.BottomLeft;
            _caption.lineSpacing = captionLineSpacing;
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

            // ⑤ 건너뛰기 버튼 — <b>오른쪽 위</b>. 막 <b>위</b>에 둔다(형제 중 마지막 = 가장 앞).
            //    장면 사이의 검은 화면에서도 보여야 하고, 그때도 눌려야 한다.
            BuildSkipButton(root);
        }

        /// <summary>
        /// «건너뛰기» 버튼을 짓는다 — <b>로비 버튼과 같은 디자인</b>
        /// (유저 지시: <i>"스킵 버튼 디자인은 로비 버튼 디자인이랑 똑같게 가로 길이만 이미지
        /// 짤라서 줄여주고"</i>).
        ///
        /// ★ <b>판을 가로로 «눌러» 줄이지 않았다.</b> <c>LobbyButton</c> 을 200px 칸에 그대로
        /// 넣으면 양끝의 창끝 장식이 찌그러져 로비 버튼과 같은 디자인으로 보이지 않는다.
        /// 그래서 그림의 <b>가운데 평평한 띠만 도려낸</b> 새 그림을 쓴다
        /// (<c>Tools/make_skip_button_art.py</c> → <c>LobbyButtonSkip</c>).
        /// 픽셀 축척(그림 폭÷칸 폭)이 로비와 같으므로 장식의 두께·그림자가 똑같이 보인다.
        ///
        /// ★ <b>상태 색은 로비 씬의 버튼에서 그대로 가져왔다</b>(정상 0.9 · 강조 1.0 · 눌림 0.66 ·
        /// 페이드 0.12초). 로비는 그 값이 <b>씬</b>에 있어서 코드에 없다 — 오프닝은 UI 를 코드로
        /// 짓기 때문에 여기 적는다. 두 곳의 값이 갈리면 «같은 버튼» 으로 보이지 않는다.
        ///
        /// ⚠ <see cref="EventSystem"/> 이 없으면 <b>버튼이 눌리지 않는다</b>. 오프닝 씬에는
        ///   원래 없었다(연출이 클릭을 받지 않았으니 필요가 없었다) — 그래서 없으면
        ///   <see cref="EnsureEventSystem"/> 이 만든다.
        /// </summary>
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
            plate.preserveAspect = false;       // 로비와 같다 — 판은 가로로 늘어나도 되는 그림이다
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

            // ⚠ 키보드 포커스를 받지 않게 한다 — 스페이스로 건너뛸 때 «버튼이 눌린 것» 으로도
            //   처리되어 Skip() 이 두 번 불릴 수 있다(막아 두었지만 애초에 안 겹치는 편이 낫다).
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            button.onClick.AddListener(Skip);

            // 라벨 — 로비 버튼과 같은 폰트·크기·색·여백(좌우 8px)이다.
            RectTransform labelRect = NewRect("Label", rect);
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);

            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = HudTheme.Font;                  // 네오둥근모
            label.fontSize = skipButtonFontSize;
            label.color = HudTheme.TextMain;             // 로비 라벨과 같은 (0.88, 0.92, 0.94)
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = skipButtonText;

            _skipButton = rect.gameObject.AddComponent<CanvasGroup>();
            _skipButton.alpha = 0f;
            _skipButton.blocksRaycasts = false;          // 다 떠오른 뒤에 열린다 (ShowSkipButton)
        }

        /// <summary>
        /// 클릭을 받을 <see cref="EventSystem"/> 이 없으면 만든다.
        ///
        /// ⚠ 이 프로젝트는 <b>Input System 패키지 전용</b>이라 입력 모듈도
        ///   <see cref="InputSystemUIInputModule"/> 여야 한다 — 기본 <c>StandaloneInputModule</c>
        ///   은 실행 시점에 «옛 입력 백엔드가 꺼져 있다» 며 예외를 던진다.
        ///   액션은 모듈이 <b>스스로</b> 기본값을 넣는다(패키지의 <c>OnEnable</c> →
        ///   <c>HasNoActions()</c> → <c>AssignDefaultActions()</c>) — 씬에 배선할 것이 없다.
        /// </summary>
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
