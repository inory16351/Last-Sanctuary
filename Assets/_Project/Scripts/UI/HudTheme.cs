using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 버튼이 지금 어떤 상태인지. <see cref="HudTheme.PaintButton"/> 가 받는다.
    ///
    /// ⚠ <b>마우스 올림·누름은 여기 없다</b> — 그건 유니티의 <c>Button.transition =
    /// SpriteSwap</c> 이 <see cref="Image.overrideSprite"/> 로 알아서 한다. 여기 있는
    /// 것은 <b>게임이 판단하는</b> 상태다: 창이 열려 있음(<see cref="On"/>) ·
    /// 에너지가 모자람(<see cref="Off"/>).
    /// </summary>
    public enum ButtonState
    {
        Normal,
        Hover,
        On,
        Off,
    }

    /// <summary>
    /// HUD 의 색·크기·폰트를 한 곳에 모아둔 곳.
    ///
    /// HUD 는 씬이 아니라 <b>코드로 런타임 생성</b>하기 때문에(<see cref="HudBootstrap"/>),
    /// 인스펙터에서 스타일을 만질 수 없다. 대신 값을 전부 여기 모아 한 파일만 고치면
    /// 전체 톤이 바뀌도록 했다. 패널마다 색을 직접 적어넣지 말 것.
    /// </summary>
    public static class HudTheme
    {
        // ── 색 ──────────────────────────────────────────────────────────
        // 게임 화면이 어두운 유기체 톤(배경 0.04,0.04,0.05)이라 패널은 반투명 검정,
        // 강조는 청록 계열로 잡았다. 비주얼 가이드가 확정되면 이 값들만 바꾸면 된다.

        public static readonly Color PanelBg      = new Color(0.05f, 0.06f, 0.08f, 0.82f);
        public static readonly Color PanelBgSoft  = new Color(0.07f, 0.08f, 0.11f, 0.66f);
        public static readonly Color RowBg        = new Color(0.10f, 0.12f, 0.16f, 0.70f);
        public static readonly Color RowBgOn      = new Color(0.13f, 0.28f, 0.26f, 0.90f);

        public static readonly Color TextMain     = new Color(0.88f, 0.92f, 0.94f, 1f);
        public static readonly Color TextDim      = new Color(0.58f, 0.64f, 0.70f, 1f);
        public static readonly Color TextAccent   = new Color(0.45f, 0.95f, 0.78f, 1f);
        public static readonly Color TextWarn     = new Color(0.98f, 0.72f, 0.35f, 1f);
        public static readonly Color TextDanger   = new Color(0.96f, 0.42f, 0.42f, 1f);

        public static readonly Color BarBack      = new Color(0.16f, 0.05f, 0.07f, 0.90f);
        public static readonly Color BarHp        = new Color(0.40f, 0.85f, 0.52f, 1f);
        public static readonly Color BarHpMid     = new Color(0.90f, 0.78f, 0.32f, 1f);
        public static readonly Color BarHpLow     = new Color(0.92f, 0.38f, 0.38f, 1f);

        // 침식(Erosion) 게이지 — 체력바 바로 아래에 나란히 놓이므로 색으로 확실히 갈라야 한다.
        // 체력이 초록→노랑→빨강 계열이라, 침식은 겹치지 않는 보라→자홍 계열로 잡았다
        // (침식이 차오르는 것 자체가 "정신이 잠식된다"는 표현이라 톤도 맞는다).
        public static readonly Color BarErosionBack = new Color(0.08f, 0.05f, 0.12f, 0.90f);
        public static readonly Color BarErosion     = new Color(0.55f, 0.36f, 0.86f, 1f);
        public static readonly Color BarErosionHigh = new Color(0.95f, 0.30f, 0.78f, 1f);

        /// <summary>정신 이상 상태 이름을 표시할 때의 글자색.</summary>
        public static readonly Color TextErosion    = new Color(0.84f, 0.64f, 1f, 1f);

        /// <summary>
        /// ★★ <b>영웅 각성한 캐릭터의 이름 색</b> (2026-08-21 · 유저 지시: *"영웅 각성 시
        /// 캐릭터 상세 UI에 영웅 이름을 황금색으로 … 캐릭터 그리드에도 황금색으로"*).
        ///
        /// ★ 값이 <see cref="Combat.DamageNumberFx"/> 의 <c>heroAwakenColor</c>(1, 0.72, 0.22)와
        ///   <b>같다</b> — 각성하는 순간 화면에 뜨는 «영웅 각성!» 글자와 그 뒤로 계속 남는
        ///   이름 색이 <b>같은 금색</b>이어야 «그때 그 일» 과 «이 캐릭터» 가 이어져 보인다.
        /// ⚠ 색을 여기 둔 이유는 <b>두 창이 쓰기</b> 때문이다(상세 UI · 로스터). 패널마다
        ///   적으면 한쪽만 바뀐다 — 이 파일의 맨 위 규칙 그대로다.
        /// </summary>
        public static readonly Color TextHero       = new Color(1f, 0.72f, 0.22f, 1f);

        /// <summary>
        /// ★★ <b>부대 색</b> (2026-08-24 · 유저 지시: <i>"같은 부대인 캐릭터를 각기 다른 색의
        /// 아웃라인으로 묶어서 보여줘"</i>).
        ///
        /// <see cref="Units.SquadService"/> 의 부대 상한이 6 이라 여섯 색이면 충분하다.
        /// ★ <b>색상환에서 고르게 벌렸다</b>(청록 · 주황 · 보라 · 연두 · 하늘 · 분홍) —
        ///   이웃한 두 부대가 «비슷한 색» 이면 묶음이 안 보인다.
        /// ⚠ 여기 없는 색을 쓰지 말 것 — 로스터·부대 창·집결지 깃발이 <b>같은 색</b>을
        ///   써야 «저 색이 저 부대» 가 성립한다.
        /// ⚠ 체력바(초록·노랑·빨강)·침식(보라·자홍)·각성(금색)과 겹치지 않게 골랐다.
        /// </summary>
        static readonly Color[] SquadColors =
        {
            new Color(0.35f, 0.85f, 0.95f, 1f),   // 1 청록
            new Color(0.98f, 0.65f, 0.28f, 1f),   // 2 주황
            new Color(0.72f, 0.55f, 0.98f, 1f),   // 3 보라
            new Color(0.62f, 0.90f, 0.42f, 1f),   // 4 연두
            new Color(0.45f, 0.62f, 0.98f, 1f),   // 5 하늘
            new Color(0.98f, 0.55f, 0.75f, 1f),   // 6 분홍
        };

        /// <summary>부대 순번(0부터)의 색. 범위를 넘으면 <b>돌려 쓴다</b>(색이 없어서 안 보이는 것보다 낫다).</summary>
        public static Color SquadColor(int order) =>
            SquadColors[((order % SquadColors.Length) + SquadColors.Length) % SquadColors.Length];

        public static readonly Color ButtonNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        public static readonly Color ButtonHover  = new Color(0.18f, 0.26f, 0.32f, 0.98f);
        public static readonly Color ButtonOn     = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        public static readonly Color ButtonOff    = new Color(0.10f, 0.11f, 0.13f, 0.85f);

        // 미니맵 — 미탐사(안개) → 탐사됐지만 지금 시야 밖(캐릭터가 지나간 곳) → 지금 시야 안(가장 밝음)
        // 3단계가 한눈에 구분되도록 밝기 격차를 크게 뒀다. 예전엔 지형 원색 자체가 어두워서
        // (바닥 38,44,52) "탐사됨" 상태가 곱연산으로 한 번 더 죽으면 미탐사와 거의 안 구별됐다
        // (유저 피드백: "캐릭터가 지나간 곳이 안 밝아 보인다") — 지형 원색을 밝게 올리고,
        // "탐사됐지만 시야 밖"은 곱연산이 아니라 미탐사 색과 지형 원색 사이를 보간해서 항상
        // 미탐사보다 확실히 밝은 중간 밝기를 보장하도록 바꿨다.
        public static readonly Color32 MapFloor      = new Color32(145, 160, 170, 255);
        public static readonly Color32 MapWall       = new Color32( 55,  62,  72, 255);
        public static readonly Color32 MapUnexplored = new Color32( 10,  10,  13, 255);

        /// <summary>탐사됐지만 지금 시야 밖인 칸의 밝기 — 미탐사(0)와 지금 시야 안(1) 사이의 보간 비율.</summary>
        public const float MapExploredBrightness = 0.5f;
        public static readonly Color32 MapNexus      = new Color32(120, 235, 200, 255);
        public static readonly Color32 MapAlly       = new Color32( 90, 200, 255, 255);
        public static readonly Color32 MapEnemy      = new Color32(240,  90,  90, 255);
        public static readonly Color32 MapNeutral    = new Color32(225, 200, 110, 255);
        public static readonly Color32 MapRally      = new Color32(255, 240, 130, 255);

        // ── 크기 (CanvasScaler 기준 해상도 1920x1080) ────────────────────
        public const int FontTitle  = 20;
        public const int FontBody   = 17;
        public const int FontSmall  = 14;
        public const int FontBig    = 30;

        public const float Margin   = 16f;   // 화면 가장자리 여백
        public const float Gap      = 8f;    // 패널 사이 간격
        public const float Pad      = 10f;   // 패널 안쪽 여백

        // ── 폰트 — 네오둥근모 고정 ───────────────────────────────────────
        // 런타임 생성 UI 는 인스펙터 참조를 가질 수 없어서 Resources 로 읽는다.
        // 그래서 폰트 에셋을 Art/Fonts → Resources/Fonts 로 옮겨두었다
        // (.meta 를 같이 옮겨 GUID 유지 → 씬의 기존 TMP 참조도 그대로 산다).
        public const string FontResourcePath = "Fonts/NeoDunggeunmo SDF";

        static TMP_FontAsset _font;
        static bool _fontWarned;

        /// <summary>HUD 전용 폰트(네오둥근모). 못 찾으면 TMP 기본 폰트로 떨어진다(한글이 깨진다).</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.Load<TMP_FontAsset>(FontResourcePath);
                if (_font == null && !_fontWarned)
                {
                    _fontWarned = true;
                    Debug.LogError($"[HUD] 폰트를 찾지 못했습니다: Resources/{FontResourcePath}. " +
                                   "네오둥근모 SDF 에셋이 Assets/_Project/Resources/Fonts/ 에 있는지 확인하세요. " +
                                   "기본 폰트로 대체하면 한글이 표시되지 않습니다.");
                    _font = TMP_Settings.defaultFontAsset;
                }
                return _font;
            }
        }

        // ── 버튼 상태를 «색» 이 아니라 «그림» 으로 ────────────────────────

        static readonly Dictionary<string, Sprite> ButtonSprites = new Dictionary<string, Sprite>();

        /// <summary>
        /// ★★ <b>버튼의 상태를 칠한다</b> (2026-08-25 · 버튼 그림 도입).
        ///
        /// <b>왜 생겼나</b> — 예전에는 패널마다 <c>background.color = buttonOn</c> 처럼
        /// <b>색을 직접</b> 칠했다. 그 자리에 그림(<c>Btn_Action_Normal</c> 등)이 깔리자
        /// 그 어두운 색이 그림에 <b>곱해져</b> 버튼이 새까매졌다 — «그림을 넣었는데
        /// 안 보인다» 의 정체다.
        ///
        /// <b>무엇을 하나</b> — 지금 붙어 있는 스프라이트 이름에서 <b>계열</b>을 읽어
        /// (<c>Btn_Action_Normal</c> → <c>Btn_Action</c>) 같은 계열의 다른 상태 그림으로
        /// 갈아끼우고 색은 흰색으로 되돌린다.
        ///
        /// ★ <b>계열을 부르는 쪽이 몰라도 된다</b> — 액션 바 버튼인지 창 안 버튼인지는
        ///   <b>씬에 이미 붙은 그림</b>이 말해 준다. 그래서 버튼 종류가 늘어도 이 파일도,
        ///   부르는 쪽도 안 바뀐다.
        /// ⚠ <b>그림이 없으면 예전처럼 색을 칠한다</b>(<paramref name="fallback"/>).
        ///   목록의 행처럼 그림을 안 깔 자리도 있고, 그림을 뽑기 전 상태로 돌려도
        ///   화면이 멀쩡해야 한다.
        /// </summary>
        /// <param name="img">버튼의 배경 <see cref="Image"/>. null 이면 아무 일도 하지 않는다.</param>
        /// <param name="state">게임이 판단한 상태.</param>
        /// <param name="fallback">그림이 없을 때 칠할 색.</param>
        public static void PaintButton(Image img, ButtonState state, Color fallback)
        {
            if (img == null) return;

            Sprite cur = img.sprite;
            if (cur != null && cur.name.StartsWith("Btn_"))
            {
                int cut = cur.name.LastIndexOf('_');
                if (cut > 0)
                {
                    string key = cur.name.Substring(0, cut + 1) + state;
                    if (!ButtonSprites.TryGetValue(key, out Sprite next))
                    {
                        next = Resources.Load<Sprite>("UI/Buttons/" + key);
                        ButtonSprites[key] = next;
                    }
                    if (next != null)
                    {
                        // ⚠ 같은 그림이면 손대지 않는다 — Image.sprite 에 대입하면
                        //   같은 값이어도 메시를 다시 굽는다(글자 색과 같은 이유).
                        if (!ReferenceEquals(img.sprite, next)) img.sprite = next;
                        if (img.color != Color.white) img.color = Color.white;
                        return;
                    }
                }
            }

            if (img.color != fallback) img.color = fallback;
        }

        // ── 글자가 칸을 넘지 않게 ────────────────────────────────────────

        /// <summary>
        /// ★★ <b>글자가 칸 밖으로 나가지 않게 만든다</b> (2026-08-24 · 유저 지시:
        /// *"유물 ui안에 텍스트 배치할때 텍스트가 짤리지 않도록"*).
        ///
        /// <b>왜 코드에서 하나</b> — 이 게임의 창은 MCP 로 만든다. 그런데 TMP 의
        /// 줄바꿈·자동 크기 칸(<c>m_TextWrappingMode</c> · <c>m_enableAutoSizing</c>)은
        /// MCP 브리지가 넘기지 못한다(진행상황 8절 4번 · UI-50 실측). 그래서 <b>칸의
        /// 크기는 MCP 가</b>, <b>넘침 규칙은 코드가</b> 맡는다. 창을 배선하는 자리
        /// (<c>EnsureBound</c>/<c>Bind</c>)에서 한 번만 부르면 된다.
        ///
        /// <b>무엇을 하나</b> — ① 줄바꿈을 켠다 ② <b>자동 크기</b>를 켠다
        /// (지금 글자 크기가 최대, <paramref name="minSize"/> 가 최소) ③ 넘침 모드는
        /// <c>Overflow</c> 로 둔다.
        ///
        /// ★ <b>왜 <c>Ellipsis</c>(…)나 <c>Truncate</c> 가 아닌가</b> — 그 둘은 «잘라서»
        ///   맞춘다. 유물 설명은 «방어력이 8, 체력이 5 증가합니다» 처럼 <b>뒤쪽에 숫자가
        ///   있는</b> 문장이라 뒤를 자르면 정보가 사라진다. 자동 크기는 <b>줄여서</b>
        ///   맞추므로 아무것도 잃지 않는다.
        /// ⚠ 그래도 안 들어가는 극단(칸이 지나치게 작을 때)에는 <c>Overflow</c> 라
        ///   <b>넘쳐서라도 보인다</b> — «안 보이는 것» 보다 «삐져나온 것» 이 낫다.
        ///   그 상태가 눈에 띄면 그때 칸을 키우면 된다(칸 크기는 MCP 스크립트에 있다).
        /// </summary>
        /// <param name="text">대상. null 이면 아무 일도 하지 않는다.</param>
        /// <param name="minSize">여기까지만 줄인다. 0 이하면 지금 크기의 70%.</param>
        /// <param name="wrap">
        /// 줄바꿈 허용 여부. <b>한 줄짜리 띠</b>(성장 창의 유물 띠처럼 높이가 한 줄인 칸)는
        /// <c>false</c> 로 줘야 한다 — 줄바꿈을 켜면 두 번째 줄이 칸 아래로 흘러버린다.
        /// </param>
        public static void FitText(TMP_Text text, float minSize = 0f, bool wrap = true)
        {
            if (text == null) return;

            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            // ⚠ 자동 크기를 켜기 <b>전에</b> 지금 크기를 최대값으로 잡아 둔다 —
            //   켜고 나면 fontSize 가 «계산된 값» 으로 덮여서 원래 크기를 잃는다.
            float max = text.fontSize;
            if (max <= 0f) max = FontBody;

            text.fontSizeMax = max;
            text.fontSizeMin = minSize > 0f ? Mathf.Min(minSize, max) : max * 0.7f;
            text.enableAutoSizing = true;
        }
    }
}
