using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// <b>HUD 에 판·버튼·게이지 그림을 입힌다</b> (2026-08-25 신설).
    ///
    /// ★★ <b>왜 MCP 가 아니라 에디터 코드인가</b>
    /// -----------------------------------------------------
    /// 준수사항 §10 H-1 은 «오브젝트 생성·수정은 MCP 로» 다. 그런데 <b>스프라이트는
    /// MCP 로 넣을 수 없다</b> — <c>update_component</c> 에 <c>m_Sprite</c> 를 경로로도
    /// 이름으로도 줘 봤지만 <c>m_Type</c>(단순 enum)만 반영되고 <c>m_Sprite</c> 는 계속
    /// <c>null</c> 이었다(2026-08-25 실측). 진행상황 8절 4번의 «MCP 는 참조를 못 넣는다» 가
    /// 에셋 참조에도 그대로 걸린다. TMP 폰트만 예외였던 것(UI-1절)은 그 브리지가
    /// <c>m_fontAsset</c> 을 따로 처리하기 때문이다.
    ///
    /// 그래서 <b>참조를 넣는 일만</b> 여기로 가져왔다. 크기·위치·계층은 여전히 MCP 가 맡는다.
    ///
    /// ★ <b>경로를 나열하지 않고 «이름으로» 판단한다.</b> 로스터 행·부대 카드·유물 행은
    ///   <b>템플릿을 복제해</b> 런타임에 생기므로(UI-1절 ★ 템플릿 예외) 경로 목록으로 잡으면
    ///   복제본이 빠진다. 이름 규칙(<c>HpBack</c>·<c>HpFill</c>·<c>*Button</c>)으로 판단하면
    ///   템플릿 하나만 칠해도 복제본이 전부 따라온다.
    ///
    /// ★ <b>버튼의 «종류» 는 렉트 비율로 고른다.</b> 버튼이 마흔 개가 넘는데 폭만 다르고
    ///   모양은 여섯 종뿐이다. 경로마다 손으로 짝지으면 버튼이 하나 늘 때마다 이 파일을
    ///   고쳐야 한다 — 비율로 고르면 안 고쳐도 된다.
    ///
    /// ⚠ <b>채워지는 막대는 <c>type</c> 을 건드리지 않는다</b> — <c>Filled</c> 여야
    ///   <c>fillAmount</c> 가 먹는다(<see cref="UI.UiFillBar"/> 의 설명 그대로). 여기서
    ///   <c>Sliced</c> 로 바꾸면 체력바가 다시 «색만 바뀌고 안 줄어드는» 상태로 돌아간다.
    ///
    /// ⚠ <b>목록의 «행» 은 칠하지 않는다</b>(로스터 행·도움말 행·유물 행·부대 카드).
    ///   그것들은 <see cref="UI.HudTheme"/> 의 색으로 선택 상태를 표시하는데, 그림을 깔면
    ///   그 색이 그림에 곱해져 선택이 안 보인다. 행은 단색이 맞다.
    /// </summary>
    public static class UiSkinApplier
    {
        /// <summary>
        /// ★★ <b>훑는 캔버스가 둘이다</b> (2026-08-25 · 유저 지시: *"도움말에도 ui 이미지 사용해줘"*).
        ///
        /// <b>왜 도움말만 그림이 없었나</b> — 도움말 세 창(<c>HUD_Help</c>·<c>HUD_HelpCard</c>·
        /// <c>HUD_HelpTour</c>)은 <b><c>Help_Root</c></b> 라는 <b>다른 캔버스</b>에 있다
        /// (sortingOrder 20 — 조언 카드가 다른 창 위에 보여야 해서 그렇게 나눴다).
        /// 그런데 이 적용기는 <c>UI_Root</c> <b>하나만</b> 훑고 있었다. 목록에 이름을 적어도
        /// 찾지 못했을 것이고, 실제로는 목록에도 없었다 — 두 겹으로 빠져 있었다.
        /// </summary>
        /// <summary>
        /// ★ <b>글자 여백</b>(<see cref="UiTextInset"/>)도 <b>같은 목록</b>을 쓴다 —
        ///   두 벌로 두면 한쪽에만 캔버스를 더해 «그림은 깔렸는데 글자는 안 밀린» 상태가 된다
        ///   (실제로 2026-08-25 에 그 상태였다: 배선만 Help_Root 를 알고 있었다).
        /// </summary>
        public static readonly string[] Roots = { "UI_Root", "Help_Root" };
        const string SpriteDir = "Assets/_Project/Resources/UI/";

        // ── 어떤 그림을 어디에 ──────────────────────────────────────────

        /// <summary>큰 창 — 크기가 520×430 부터 1220×924 까지라 한 장을 9-슬라이스로 늘려 쓴다.</summary>
        static readonly string[] Windows =
        {
            "HUD_Growth", "HUD_Tactics", "HUD_Squad", "HUD_Subjugate", "HUD_Settings",
            "HUD_Event", "HUD_Relics", "HUD_Dig", "HUD_SkillDetail",

            // ★ 도움말 셋 (2026-08-25) — Help_Root 캔버스에 있다.
            //   ⚠ <c>HUD_HelpCard</c>·<c>HUD_HelpTour</c> <b>자신은 넣지 않는다</b> —
            //     그 둘은 화면 전체를 덮는 «막» 이라 그림을 깔면 전장이 통째로 가려진다.
            //     안쪽의 카드·말풍선만 창으로 본다.
            "HUD_Help", "HUD_HelpCard/Card", "HUD_HelpTour/Bubble",
        };

        /// <summary>상시 떠 있는 판, 그리고 창 안의 속판. 창보다 조용해야 전장이 보인다.</summary>
        static readonly string[] Plates =
        {
            "HUD_Roster", "HUD_Log", "HUD_Energy", "HUD_Actions",
            "HUD_Minimap", "HUD_Speed", "HUD_Portrait",
            "HUD_Boss/Body",
            "HUD_Growth/Info", "HUD_Growth/Stats", "HUD_Growth/RelicBar",
            "HUD_Relics/Detail", "HUD_Relics/List",
            "HUD_Squad/Body",
            "HUD_Subjugate/Squads", "HUD_Subjugate/Targets",
            "HUD_Tactics/Col1", "HUD_Tactics/Col2", "HUD_Tactics/Col3", "HUD_Tactics/Info",
            "HUD_Settings/Header", "HUD_SkillDetail/EffectBack",
            "HUD_Defeat/Body/Panel", "HUD_Victory/Body/Panel",

            // 도움말 창 안의 두 속판 (2026-08-25)
            "HUD_Help/List", "HUD_Help/Detail",
        };

        /// <summary>사건·발굴 창의 선택지 — 그림 배경 위에 얹히므로 더 두껍고 불투명한 판을 쓴다.</summary>
        static readonly string[] Choices =
        {
            "HUD_Event/Choice0", "HUD_Event/Choice1", "HUD_Dig/Choice0", "HUD_Dig/Choice1",
        };

        /// <summary>게이지의 «빈 통». 이름으로 잡는다 — 템플릿 복제본까지 따라오게.</summary>
        static readonly string[] TrackNames = { "HpBack", "ErosionBack", "RageBack" };

        /// <summary>게이지의 «채움». ⚠ <c>type</c> 은 <c>Filled</c> 그대로 두어야 한다.</summary>
        static readonly string[] FillNames = { "HpFill", "HpGhost", "HpShield", "ErosionFill", "RageFill" };

        /// <summary>아이콘이 들어가는 칸.</summary>
        static readonly string[] SlotNames =
        {
            "Slot0", "Slot1", "Slot2", "Slot_00", "Slot_01", "Slot_02", "Slot_03", "RelicSlot",
        };

        /// <summary>
        /// 손대지 않을 것. <b>목록의 행·카드</b>(선택 색이 필요하다) · 스크롤바 · 슬라이더 ·
        /// 이름 입력칸 · 오버레이 표식.
        /// </summary>
        static readonly string[] Skip =
        {
            "Scrollbar", "NameInput", "RowTemplate", "SquadCard_Template",
            "DigMarkerTemplate", "BuildRangeTemplate", "RallyRangeTemplate",
            "Slider", "Handle", "Fill Area", "Viewport", "BgMask",

            // ★ 도움말 투어의 «집중 표시» — 얇은 선 넉 장으로 대상을 두르는 것이다.
            //   판으로 칠하면 <b>가리키려던 것을 덮어</b> 버린다.
            "HUD_HelpTour/Frame",
        };

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Load(string name)
        {
            if (Cache.TryGetValue(name, out Sprite s)) return s;
            string sub = name.StartsWith("Btn_") ? "Buttons/" : "Frames/";
            s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + sub + name + ".png");
            if (s == null) Debug.LogError($"[UI] 스프라이트를 못 찾음: {SpriteDir}{sub}{name}.png");
            Cache[name] = s;
            return s;
        }

        [MenuItem("LastSanctuary/UI/배선", priority = 41)]
        public static void Apply()
        {
            Cache.Clear();

            var log = new List<string>();
            int windows = 0, plates = 0, buttons = 0, bars = 0, slots = 0, frames = 0;
            int found = 0;

            foreach (string rootName in Roots)
            {
            GameObject root = GameObject.Find(rootName);
            if (root == null)
            {
                // ⚠ 없는 것이 <b>정상일 수 있다</b> — Help_Root 는 도움말 UI 를 아직 만들지
                //   않은 씬에는 없다. 그래서 에러가 아니라 기록만 남긴다.
                log.Add($"  (건너뜀) {rootName} 이 이 씬에 없다");
                continue;
            }
            found++;

            // ⚠ 비활성 창(대부분의 HUD 창은 꺼진 채로 저장된다)까지 훑어야 한다 —
            //   GetComponentsInChildren 의 두 번째 인자가 그것이다.
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                string path = PathOf(img.transform, root.transform);
                string name = img.name;

                if (Skip.Any(s => path.Contains(s))) continue;

                // ── 게이지 ────────────────────────────────────────────
                if (FillNames.Contains(name))
                {
                    // type 은 그대로 둔다(Filled). 스프라이트만 갈아끼운다.
                    Set(img, Load("Bar_Fill"), keepType: true);
                    bars++; continue;
                }
                if (TrackNames.Contains(name))
                {
                    Set(img, Load("Bar_Track"), Image.Type.Sliced);
                    bars++; continue;
                }

                // ── 칸 ────────────────────────────────────────────────
                if (SlotNames.Contains(name))
                {
                    // ★★ <b>정사각 소켓을 가로로 늘리면 테두리도 같이 늘어난다</b>
                    //   (2026-08-25 · 유저 보고: *"미해금 된 스킬 아이콘이 배너 UI 에
                    //   미세하게 가려진다"*).
                    //
                    //   <c>Slot_Empty</c> 는 43×44 정사각이고 경계가 <b>0</b>(늘리지 않는
                    //   그림)이다. 그런데 초상화의 스킬 줄은 <b>208×44</b>(비 4.73)라
                    //   <c>Simple</c> 로 4.8배 늘어나면서 6px 짜리 왼쪽 테두리가
                    //   <b>29px</b> 이 됐다 — x 8~40 에 있는 아이콘을 그만큼 덮었다.
                    //
                    // ★ 그래서 <b>모양으로 갈라 준다</b>: 정사각에 가까운 칸(부대 카드의
                    //   초상화 82×72)만 소켓을 쓰고, 가로로 긴 줄은 <c>Bar_Track</c> 을 쓴다.
                    //   그 그림은 경계가 <b>가로에만</b> 있어(L7 R7 · 위아래 0) 늘려도
                    //   위아래 테두리가 안 생기고, 좌우 마개는 7px 이라 아이콘(8부터)을
                    //   건드리지 않는다.
                    Rect rr = img.rectTransform.rect;
                    bool square = rr.height > 0f && rr.width / rr.height < 1.6f;
                    if (square) Set(img, Load("Slot_Empty"), Image.Type.Simple);
                    else Set(img, Load("Bar_Track"), Image.Type.Sliced, 0.95f);
                    slots++; continue;
                }

                if (name == "Divider") { Set(img, Load("Divider_Plain"), Image.Type.Simple); continue; }

                // ── 성장 창의 «칸» ────────────────────────────────────
                // ★ 스탯 12칸(280×66)과 스킬 3칸(280×176)은 <b>버튼이지만 판처럼</b> 보여야
                //   한다 — 비율만 보면 스킬 칸(1.59:1)이 「닫기」 그림으로 떨어져 모서리가
                //   뭉갠다. 이름으로 먼저 걸러 판 그림을 준다.
                // ⚠ <c>transition</c> 은 건드리지 않는다 — 잠김/해금/고른 것을 코드가
                //   <c>Background.color</c> 로 칠하고 있고, 그 색은 흰 계열이라 그림을 안 죽인다.
                if (name.StartsWith("StatRow_") || name.StartsWith("PassiveCard_"))
                {
                    Set(img, Load("Hud_Plate"), Image.Type.Sliced, 0.95f);
                    LayoutCell(img.rectTransform, name.StartsWith("StatRow_"));
                    plates++; continue;
                }

                // ── 버튼 ──────────────────────────────────────────────
                var btn = img.GetComponent<Button>();
                if (btn != null && btn.targetGraphic == img)
                {
                    string kind = ButtonKind(path, img.rectTransform.rect);
                    Sprite face = Load($"Btn_{kind}_Normal");
                    Set(img, face, Image.Type.Sliced);
                    WireSwap(btn, kind);
                    InsetLabel(btn, face);
                    buttons++;
                    log.Add($"  버튼 {kind,-6} {path}");
                    continue;
                }

                // ── 판 ────────────────────────────────────────────────
                // ★ 웨이브 표시는 판이 아니라 <b>매달린 현수막</b>이다 — 화면 정중앙 위라
                //   다른 판과 같은 그림을 쓰면 «또 하나의 창» 으로 보인다.
                if (path == "HUD_Wave") { Set(img, Load("Wave_Banner"), Image.Type.Sliced); plates++; continue; }
                if (Windows.Contains(path)) { Set(img, Load("Win_Frame"), Image.Type.Sliced); windows++; continue; }
                if (Plates.Contains(path)) { Set(img, Load("Hud_Plate"), Image.Type.Sliced, 0.95f); plates++; continue; }
            }

            frames += ApplyThinFrames(root, log);
            }

            if (found == 0)
            {
                Debug.LogError($"[UI] {string.Join(" · ", Roots)} 을 하나도 못 찾았습니다. " +
                               "게임 씬을 열고 실행하세요.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[UI] 배선 완료 — 창 {windows} · 판 {plates} · 버튼 {buttons} · 막대 {bars} · 칸 {slots} · 액자 {frames}\n"
                      + string.Join("\n", log));
        }

        /// <summary>
        /// ★★ <b>얇은 선 네 장으로 만든 테두리를 «액자 한 장» 으로 바꾼다</b>.
        ///
        /// 초상화와 미니맵의 테두리는 <c>Top/Bottom/Left/Right</c> <b>1~2px 짜리 이미지 네 장</b>
        /// 으로 돼 있다(MCP 로 만들 때 이게 유일한 방법이었다). 그림이 생겼으니 그 네 장을
        /// <b>끄고</b>, 그것들을 담고 있던 <b>빈 컨테이너에 이미지를 붙여</b> 액자 한 장을 깐다.
        ///
        /// ★ <b>오브젝트를 새로 만들지 않는다</b> — 컨테이너(<c>ArtFrame</c>·<c>ViewBorder</c>)가
        ///   이미 <b>정확히 액자가 놓일 렉트</b>다. 새로 만들면 앵커를 다시 맞춰야 하고
        ///   계층이 바뀐다(준수사항 §10 H-1 — 계층 변경은 MCP 의 몫이다).
        /// ⚠ <b>포인터를 받지 않게 한다</b> — 액자가 클릭을 먹으면 미니맵을 못 누르고
        ///   초상화 위의 스킬 칸도 안 눌린다.
        /// ⚠ 바깥 <c>Border</c> 네 장도 끈다 — 이제 판 그림(<c>Hud_Plate</c>)이 테두리를
        ///   가지고 있어서 선이 겹쳐 두 겹으로 보인다.
        /// </summary>
        static int ApplyThinFrames(GameObject root, List<string> log)
        {
            (string container, string sprite)[] jobs =
            {
                ("HUD_Portrait/ArtFrame", "Portrait_Frame"),
                ("HUD_Minimap/ViewBorder", "Minimap_Bezel"),
            };
            string[] retire = { "HUD_Portrait/Border", "HUD_Minimap/Border" };

            int n = 0;
            foreach ((string container, string sprite) in jobs)
            {
                Transform t = root.transform.Find(container);
                if (t == null) { Debug.LogWarning($"[UI] 액자 컨테이너 없음: {container}"); continue; }

                foreach (Transform strip in t) if (strip.gameObject.activeSelf)
                {
                    Undo.RecordObject(strip.gameObject, "UI 스킨 배선");
                    strip.gameObject.SetActive(false);
                    EditorUtility.SetDirty(strip.gameObject);
                }

                var img = t.GetComponent<Image>();
                if (img == null)
                {
                    img = Undo.AddComponent<Image>(t.gameObject);
                    log.Add($"  액자 이미지 신설 {container}");
                }
                Set(img, Load(sprite), Image.Type.Sliced);
                img.raycastTarget = false;
                EditorUtility.SetDirty(img);
                log.Add($"  액자 {sprite,-15} {container} (얇은 선 {t.childCount}장 끔)");
                n++;
            }

            foreach (string path in retire)
            {
                Transform t = root.transform.Find(path);
                if (t == null) continue;
                foreach (Transform strip in t) if (strip.gameObject.activeSelf)
                {
                    Undo.RecordObject(strip.gameObject, "UI 스킨 배선");
                    strip.gameObject.SetActive(false);
                    EditorUtility.SetDirty(strip.gameObject);
                }
                log.Add($"  겹치는 테두리 끔 {path}");
            }
            return n;
        }

        /// <summary>
        /// 버튼의 «종류» 를 렉트 비율로 고른다.
        ///
        /// ★ 액션 바와 선택지만 경로로 못 박는다 — 액션 바는 8개가 <b>같은 그림</b>이어야
        ///   묶음으로 보이고, 선택지는 비율만 보면 「창 안 보통 버튼」과 구별이 안 되기 때문이다.
        /// </summary>
        static string ButtonKind(string path, Rect r)
        {
            if (path.StartsWith("HUD_Actions/Buttons/")) return "Action";
            if (Choices.Contains(path)) return "Choice";

            // ★★ <b>분류 탭은 «밝은» 계열을 쓴다</b> (2026-08-26 · 유저 지시:
            //   *"도움말 ui 위 쪽 메뉴 이미지들 밝은 색 이미지로 변경 가시성이 너무 안 좋음"*).
            //
            //   <c>Btn_Panel_Normal</c> 의 속색(<c>#212B38</c>)은 창 판때기와 한 단밖에
            //   차이가 안 나 <b>탭이 배경에 묻힌다</b>. <c>Btn_Tab_*</c> 는 그것을 팔레트
            //   안에서 한 단 올린 것이다(<c>Tools/ui_make_tab_sprites.py</c>).
            // ★ <b>경로에 <c>Tabs/</c> 가 있으면</b> 탭이다 — 이름을 하나하나 적지 않는다.
            //   탭은 <c>TabTemplate</c> 을 복제해 런타임에 생기므로(HelpPanel.MakeTab)
            //   목록으로 잡으면 복제본이 빠진다. 그리고 복제본은 <b>스프라이트를 물려받고</b>
            //   <see cref="UI.HudTheme"/>.PaintButton 이 그 이름에서 계열을 읽으므로
            //   여기 한 줄이면 «고른 탭/안 고른 탭» 까지 저절로 맞는다.
            if (path.Contains("/Tabs/")) return "Tab";
            // ★ 배속·정지(57×40)는 <b>「칩」</b>을 쓴다. 비율만 보면 「닫기」로 떨어지는데
            //   닫기는 정사각이라 가로로 늘리면 모서리가 뭉갠다.
            // ⚠ 전용 그림 `Btn_Speed_*` 도 있지만 <b>안 쓴다</b> — 원화가 3:1 로 나와서
            //   좌우 장식이 13px 씩이라, 57px 폭에 넣으면 글자 자리가 <b>23px</b> 밖에
            //   안 남아 «정지»(20pt · 40px)가 삐져나온다. 칩은 장식이 6px 라 45px 이 남는다.
            if (path.StartsWith("HUD_Speed/")) return "Chip";

            float w = Mathf.Max(1f, r.width), h = Mathf.Max(1f, r.height);
            float ratio = w / h;
            if (ratio >= 5.5f) return "Action";     // 아주 긴 띠
            if (ratio < 1.6f) return "Close";      // 정사각에 가까움 — 늘리지 않는다
            if (w >= 150f) return "Panel";       // 글이 들어가는 보통 버튼
            return "Chip";                          // 좁은 토글 칩
        }

        static void Set(Image img, Sprite sprite, Image.Type type = Image.Type.Simple,
                        float alpha = 1f, bool keepType = false)
        {
            if (sprite == null) return;
            Undo.RecordObject(img, "UI 스킨 배선");
            img.sprite = sprite;
            if (!keepType) img.type = type;
            if (type == Image.Type.Sliced) img.fillCenter = true;
            // ★ 색을 흰색으로 되돌린다 — 그림은 이미 어둡다. 예전의 어두운 색을 남겨 두면
            //   그림에 곱해져 새까매진다(이게 «그림을 넣었는데 안 보인다» 의 정체다).
            img.color = new Color(1f, 1f, 1f, alpha);
            EditorUtility.SetDirty(img);
        }

        /// <summary>
        /// 마우스 올림·누름·잠김을 <b>유니티가</b> 갈아끼우게 한다.
        ///
        /// ★ <c>SpriteSwap</c> 은 <see cref="Image.overrideSprite"/> 를 쓰고, 코드가 상태를
        ///   바꿀 때는 <see cref="Image.sprite"/> 를 쓴다 — <b>서로 안 싸운다</b>.
        ///   그래서 «켜짐»(창이 열림·배속 선택됨)은 코드가 계속 맡을 수 있다
        ///   (<see cref="UI.HudTheme"/>.SetButtonSkin).
        /// </summary>
        static void WireSwap(Button btn, string kind)
        {
            Undo.RecordObject(btn, "UI 스킨 배선");
            btn.transition = Selectable.Transition.SpriteSwap;
            var st = new SpriteState
            {
                highlightedSprite = Load($"Btn_{kind}_Hover"),
                pressedSprite = Load($"Btn_{kind}_On"),
                selectedSprite = Load($"Btn_{kind}_Normal"),
                disabledSprite = Load($"Btn_{kind}_Off"),
            };
            btn.spriteState = st;
            EditorUtility.SetDirty(btn);
        }

        /// <summary>
        /// ★★ <b>버튼 글자를 장식 안쪽으로 밀어 넣는다</b> (2026-08-25 · 유저 지시:
        /// *"그 이미지들에 가려서 텍스트 짤리는 것들 수정 좀"*).
        ///
        /// <b>왜 생겼나</b> — 예전 단색 버튼은 테두리가 1~2px 이라 라벨을 버튼 전체에
        /// 늘려 놓아도 됐다. 그림이 깔리자 좌우 <b>24px 짜리 장식</b>이 생겼는데 라벨은
        /// 그대로 전폭이라, 긴 글(«저장하고 로비로 돌아가기»)이 <b>장식 위로 번졌다</b>.
        ///
        /// ★ <b>여백을 스프라이트에서 읽는다</b>(<see cref="Sprite.border"/>) — 그림을 다시
        ///   뽑아 장식 크기가 바뀌어도 이 코드는 안 고친다. 그래서 배선을 돌릴 때마다
        ///   여백이 <b>자동으로 다시 맞는다</b>.
        /// ⚠ <b>가로로 늘어난 라벨만</b> 건드린다. 아이콘 옆에 붙는 라벨처럼 한쪽에
        ///   고정된 것은 밀면 자리가 어긋난다.
        /// ⚠ 세로는 손대지 않는다 — 버튼 그림의 위아래 경계는 0 이다(가로로만 늘어난다).
        /// </summary>
        static void InsetLabel(Button btn, Sprite face)
        {
            if (face == null) return;
            int pad = 4;                       // 장식에 글자가 «닿는» 것도 막는 최소 숨통
            float l = face.border.x + pad;
            float r = face.border.z + pad;

            foreach (TMP_Text t in btn.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.transform.parent != btn.transform) continue;
                RectTransform lr = t.rectTransform;
                if (!Mathf.Approximately(lr.anchorMin.x, 0f) ||
                    !Mathf.Approximately(lr.anchorMax.x, 1f)) continue;   // 늘어난 라벨만

                Undo.RecordObject(lr, "UI 스킨 배선");
                lr.offsetMin = new Vector2(l, 0f);
                lr.offsetMax = new Vector2(-r, 0f);
                EditorUtility.SetDirty(lr);
            }
        }

        /// <summary>
        /// ★★ <b>성장 창 «칸» 의 속 배치를 판 테두리 안으로 넣는다</b> (2026-08-25).
        ///
        /// <b>왜 여기서 하나</b> — 이 칸들은 <see cref="Button"/> 이라
        /// <see cref="UiTextInset"/> 가 «버튼 라벨» 로 보고 건너뛰고, 그렇다고
        /// <see cref="InsetLabel"/> 이 맡기에는 <b>늘어난 라벨이 아니라</b> 좌상단에
        /// 고정된 칸 넷~다섯이다. 그래서 이 한 곳에서 <b>값으로</b> 못박는다.
        ///
        /// ★ <b>자동으로 «밀기» 를 쓰면 안 되는 자리</b>다 — 이름·값·증감이 위아래로
        ///   붙어 있어서 각자 안쪽으로 밀면 <b>서로 겹친다</b>. 사람이 한 번 재서
        ///   넣는 편이 맞다(판 테두리는 위 10 · 아래 8 이라 안쪽이 y 10~58 뿐이다).
        /// ⚠ 스탯 12칸 · 스킬 3칸이 <b>전부 같은 속</b>이라 이름으로 한 번에 맞춘다.
        /// </summary>
        static void LayoutCell(RectTransform cell, bool isStat)
        {
            // (자식 이름, 왼쪽, 위, 폭, 높이) — 칸 좌상단 기준. 전부 앵커 (0,1) 피벗 (0,1).
            (string n, float x, float y, float w, float h)[] plan = isStat
                ? new (string, float, float, float, float)[]
                {
                    ("Label", 12f, 12f, 256f, 18f),
                    ("Value", 12f, 32f, 150f, 24f),
                    ("Delta", 168f, 32f, 100f, 24f),
                }
                : new (string, float, float, float, float)[]
                {
                    ("Icon",     10f,  10f,  68f, 68f),
                    ("Name",     86f,  12f, 182f, 22f),
                    ("Lock",     86f,  38f, 182f, 18f),
                    ("Desc",     10f,  88f, 260f, 58f),
                    ("Hint",     10f, 148f, 260f, 18f),
                    ("RageBack", 10f, 148f, 260f, 18f),
                };

            foreach ((string n, float x, float y, float w, float h) in plan)
            {
                Transform t = cell.Find(n);
                if (t == null) continue;
                var r = t as RectTransform;
                if (r == null) continue;

                Undo.RecordObject(r, "UI 스킨 배선");
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.sizeDelta = new Vector2(w, h);
                r.anchoredPosition = new Vector2(x, -y);
                EditorUtility.SetDirty(r);
            }
        }

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
