using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        const string Root = "UI_Root";
        const string SpriteDir = "Assets/_Project/Resources/UI/";

        // ── 어떤 그림을 어디에 ──────────────────────────────────────────

        /// <summary>큰 창 — 크기가 520×430 부터 1220×924 까지라 한 장을 9-슬라이스로 늘려 쓴다.</summary>
        static readonly string[] Windows =
        {
            "HUD_Growth", "HUD_Tactics", "HUD_Squad", "HUD_Subjugate", "HUD_Settings",
            "HUD_Event", "HUD_Relics", "HUD_Dig", "HUD_SkillDetail",
        };

        /// <summary>상시 떠 있는 판, 그리고 창 안의 속판. 창보다 조용해야 전장이 보인다.</summary>
        static readonly string[] Plates =
        {
            "HUD_Roster", "HUD_Log", "HUD_Wave", "HUD_Energy", "HUD_Actions",
            "HUD_Minimap", "HUD_Speed", "HUD_Portrait",
            "HUD_Boss/Body",
            "HUD_Growth/Info", "HUD_Growth/Stats", "HUD_Growth/RelicBar",
            "HUD_Relics/Detail", "HUD_Relics/List",
            "HUD_Squad/Body",
            "HUD_Subjugate/Squads", "HUD_Subjugate/Targets",
            "HUD_Tactics/Col1", "HUD_Tactics/Col2", "HUD_Tactics/Col3", "HUD_Tactics/Info",
            "HUD_Settings/Header", "HUD_SkillDetail/EffectBack",
            "HUD_Defeat/Body/Panel", "HUD_Victory/Body/Panel",
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
            "Scrollbar", "NameInput", "RowTemplate", "SquadCard_Template", "PassiveCard",
            "DigMarkerTemplate", "BuildRangeTemplate", "RallyRangeTemplate",
            "Slider", "Handle", "Fill Area", "Viewport", "BgMask",
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
            GameObject root = GameObject.Find(Root);
            if (root == null) { Debug.LogError($"[UI] {Root} 을 못 찾았습니다. 게임 씬을 열고 실행하세요."); return; }

            var log = new List<string>();
            int windows = 0, plates = 0, buttons = 0, bars = 0, slots = 0;

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
                    Set(img, Load("Slot_Empty"), Image.Type.Simple);
                    slots++; continue;
                }

                if (name == "Divider") { Set(img, Load("Divider_Plain"), Image.Type.Simple); continue; }

                // ── 버튼 ──────────────────────────────────────────────
                var btn = img.GetComponent<Button>();
                if (btn != null && btn.targetGraphic == img)
                {
                    string kind = ButtonKind(path, img.rectTransform.rect);
                    Set(img, Load($"Btn_{kind}_Normal"), Image.Type.Sliced);
                    WireSwap(btn, kind);
                    buttons++;
                    log.Add($"  버튼 {kind,-6} {path}");
                    continue;
                }

                // ── 판 ────────────────────────────────────────────────
                if (Windows.Contains(path)) { Set(img, Load("Win_Frame"), Image.Type.Sliced); windows++; continue; }
                if (Plates.Contains(path)) { Set(img, Load("Hud_Plate"), Image.Type.Sliced, 0.95f); plates++; continue; }
            }

            int frames = ApplyThinFrames(root, log);

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

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
