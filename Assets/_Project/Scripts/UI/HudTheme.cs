using TMPro;
using UnityEngine;

namespace LastSanctuary.UI
{
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
    }
}
