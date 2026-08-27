using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using LastSanctuary.Data;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>씬에 «구운» 정적 라벨을 스트링 테이블에 잇는다</b> (2026-08-26 신설 · 유저 지시:
    /// *"씬에 구운 것들도 테이블로 스트링 키 써서 영어로 바뀌게 해줘"*).
    ///
    /// <b>무엇이 문제였나</b> — 창 제목·구역 머리·정적 버튼 글자는 <b>씬의 TMP 에 한국어가
    /// 그대로 박혀</b> 있었다(실측 188칸). 코드가 손대지 않는 자리라 언어를 영어로 바꿔도
    /// <b>영원히 한국어</b>다. 데이터에서 오는 문구(몬스터·유물·스킬 이름)와 `LocalizeLabels`
    /// 를 가진 창 일곱만 영어가 됐고, 나머지는 그대로였다.
    ///
    /// <b>왜 창마다 고치지 않고 여기 모았나</b> — 대상 창이 <b>열둘</b>이다
    /// (`HUD_Tactics` 하나만 25칸). 창마다 <c>LocalizeLabels</c> 를 심으면
    /// ① 열두 파일에 같은 배선이 복사되고 ② 새 라벨을 넣을 때 <b>어디에 적어야 하는지</b>
    /// 가 흩어진다. 이 프로젝트가 «모체 하나 · 반복은 코드»(준수사항 §10 H-2)로 가는 것과
    /// 같은 이치로, <b>«경로 → 스트링 키» 지도 한 곳</b>만 둔다.
    ///
    /// ★ <b>UI_Root 에 붙인다.</b> 경로는 그 아래의 상대 경로다.
    /// ★★ <b>씬 하나가 아니다</b> (2026-08-26) — 게임 씬(`Proto_01`)과 <b>로비 씬</b>(`Lobby`)
    ///   양쪽의 `UI_Root` 에 붙고, <b>지도는 하나</b>를 나눠 쓴다. 그 씬에 없는 창의 칸은
    ///   조용히 넘어간다(<see cref="Apply"/>). 문구를 한 곳에서 다듬기 위해서다 —
    ///   «환경 설정»·«음량»·«단축키 설정» 은 두 씬이 <b>같은 키</b>를 본다.
    /// ★★ <b>자기 자식 밖도 찾는다</b> — 도움말 세 창은 `Help_Root` 아래에 산다
    ///   (<see cref="FindWindow"/> 의 세 단계).
    /// ★ <b><see cref="Transform.Find"/> 는 «닫힌 창» 안도 찾는다</b> — 그래서 `HUD_Growth`
    ///   처럼 꺼져 있는 창의 라벨도 부팅 때 한 번에 갈아 끼울 수 있다(창을 열 때
    ///   다시 그릴 필요가 없다).
    /// ⚠ <b>코드가 쓰는 라벨은 여기 적지 않는다.</b> 자리표(<c>RowTemplate</c>·
    ///   <c>StatRow_*</c>·<c>PassiveCard_*</c>)와 패널이 직렬화 문자열로 채우는 칸
    ///   (예: `Info/Name`·`Info/Hint`·`EnhanceButton/Label`)은 <b>그 패널이 정본</b>이다.
    ///   여기에 적으면 언어를 바꿀 때 이 컴포넌트가 <b>패널이 쓴 값을 덮어</b> 버린다.
    /// ⚠ <b>같은 이름의 창이 둘 있으면 «켜져 있는» 쪽을 고른다</b> — 씬에 `HUD_Portrait`
    ///   가 둘이다(하나는 꺼진 잔존물). 첫 자식을 무조건 잡으면 보이는 창이 안 바뀐다.
    /// </summary>
    public class UiLocalizer : MonoBehaviour
    {
        /// <summary>한 칸 — <see cref="Path"/> 의 라벨을 <see cref="Key"/> 문구로 채운다.</summary>
        readonly struct Entry
        {
            public readonly string Path;
            public readonly string Key;
            public Entry(string path, string key) { Path = path; Key = key; }
        }

        static Entry E(string path, string key) => new Entry(path, key);

        /// <summary>
        /// ★ <b>정본 지도.</b> 새 정적 라벨이 생기면 여기 한 줄을 더하고
        /// `Tools/table_update_20260826_scene_labels.py` 로 키를 표에 넣는다
        /// (그 스크립트가 <b>이 파일을 읽어</b> 키가 표에 다 있는지 검산한다).
        /// </summary>
        static readonly Entry[] Map =
        {
            // ── 항상 보이는 HUD ────────────────────────────────────────────
            E("HUD_Log/Title",                        "ui_log_title"),
            E("HUD_Minimap/Title",                    "ui_minimap_title"),
            E("HUD_Portrait/Relic/Label",             "ui_head_relic"),
            E("HUD_Portrait/Skills/Slot0/Label",      "ui_head_skill"),
            E("HUD_Portrait/Skills/Slot1/Label",      "ui_head_skill"),
            E("HUD_Portrait/Skills/Slot2/Label",      "ui_head_skill"),
            // ⚠ 사건 창의 제목·본문·선택지는 사건 데이터가 채운다 — 닫기 버튼만 정적이다
            //   (씬에 «달기» 라는 오타가 박혀 있었다. 키를 붙이면 그것도 함께 고쳐진다).
            E("HUD_Event/CloseButton/Label",          "ui_btn_close"),

            // ── 환경 설정 ─────────────────────────────────────────────────
            E("HUD_Settings/Header/Title",            "ui_action_settings"),
            E("HUD_Settings/Body/SaveButton/Label",   "ui_settings_save"),
            E("HUD_Settings/Body/LobbyButton/Label",  "ui_settings_to_lobby"),
            E("HUD_Settings/Body/QuitButton/Label",   "ui_settings_quit"),
            E("HUD_Settings/Body/RestartButton/Label", "ui_settings_restart"),
            E("HUD_Settings/Body/HotkeyButton/Label", "ui_settings_hotkeys"),
            E("HUD_Settings/Body/HelpResetButton/Label", "ui_settings_help_reset"),
            E("HUD_Settings/Body/Volume/Label",       "ui_settings_volume"),

            // ── 부대 지정 ─────────────────────────────────────────────────
            E("HUD_Squad/Header/Title",               "ui_squad_title"),
            E("HUD_Squad/Header/Subtitle",            "ui_squad_subtitle"),
            E("HUD_Squad/Header/AddButton/Label",     "ui_squad_add"),
            E("HUD_Squad/Footer/CloseButton/Label",   "ui_btn_close"),

            // ── 토벌 지시 ─────────────────────────────────────────────────
            // ⚠ 2026-08-27 — <b>제목 칸이 빠져 있었다</b>. 안쪽 머리글 둘만 이어 두고 정작
            //   창 제목은 아무도 안 건드려 «토벌 지시» 가 영영 한국어였다(유저 리포트의 그 종류).
            //   HUD 액션 버튼과 <b>같은 키</b>를 쓴다 — 같은 창을 가리키는 같은 말이다.
            E("HUD_Subjugate/Header",                 "ui_action_subjugate"),
            E("HUD_Subjugate/Squads/Label",           "ui_head_squads"),
            E("HUD_Subjugate/Targets/Label",          "ui_subj_targets_head"),

            // ── 유물 관리 ─────────────────────────────────────────────────
            // ⚠ 2026-08-27 — 위 「토벌 지시」와 같은 건. 안쪽 문구는 RelicPanel 이
            //   키로 채우는데(ui_relic_hint_*) <b>제목만</b> 씬에 구운 채 남아 있었다.
            E("HUD_Relics/Header",                    "ui_action_relic"),

            // ── 도움말 ───────────────────────────────────────────────────
            E("HUD_Help/Header",                      "ui_help_title"),
            E("HUD_Help/Detail/SeeAlsoButton/Label",  "ui_help_see_also"),
            E("HUD_HelpCard/Card/MoreButton/Label",   "ui_helpcard_more"),
            E("HUD_HelpCard/Card/OkButton/Label",     "ui_helpcard_ok"),
            E("HUD_HelpTour/Bubble/NextButton/Label", "ui_tour_next"),
            E("HUD_HelpTour/Bubble/PrevButton/Label", "ui_tour_prev"),
            E("HUD_HelpTour/Bubble/QuitButton/Label", "ui_tour_quit"),

            // ── 스킬 상세 ─────────────────────────────────────────────────
            E("HUD_SkillDetail/EffectHead",           "ui_skill_effect_head"),

            // ── 발굴 ─────────────────────────────────────────────────────
            E("HUD_Dig/ConfirmButton/Label",          "ui_btn_confirm"),

            // ── 캐릭터 성장 ───────────────────────────────────────────────
            E("HUD_Growth/Header/Title",              "ui_growth_title"),
            E("HUD_Growth/Header/Subtitle",           "ui_growth_subtitle"),
            E("HUD_Growth/Info/CostLabel",            "ui_growth_cost_head"),
            E("HUD_Growth/Info/HpLabel",              "ui_head_hp_now"),
            // ⚠ 2026-08-27 — 초상화 자리의 «캐릭터 선택». 코드는 이 칸을 <b>켜고 끄기만</b>
            //   하고(<c>_portraitHint.SetActive</c>) 글자는 한 번도 안 쓴다 — 그래서 지도가
            //   맡는다. 전술 지침 창에도 <b>같은 칸</b>이 있어 키를 나눠 쓴다.
            E("HUD_Growth/Info/Portrait/Hint",        "ui_portrait_pick_hint"),
            E("HUD_Growth/Stats/Head",                "ui_growth_stats_head"),
            E("HUD_Growth/Stats/GrowthLabel",         "ui_growth_focus_head"),
            E("HUD_Growth/Stats/PassiveHead",         "ui_growth_passive_head"),
            E("HUD_Growth/Stats/RelicSlot/Head",      "ui_head_relic"),
            E("HUD_Growth/RelicBar/Head",             "ui_growth_relic_head"),
            E("HUD_Growth/RelicBar/ChangeButton/Label", "ui_growth_relic_open"),
            E("HUD_Growth/Footer/CloseButton/Label",  "ui_btn_close"),
            E("HUD_Growth/Stats/GrowthTypes/MeleeDps/Label",  "ui_focus_melee_dps"),
            E("HUD_Growth/Stats/GrowthTypes/RangedDps/Label", "ui_focus_ranged_dps"),
            E("HUD_Growth/Stats/GrowthTypes/MagicDps/Label",  "ui_focus_magic_dps"),
            E("HUD_Growth/Stats/GrowthTypes/Tank/Label",      "ui_focus_tank"),
            E("HUD_Growth/Stats/GrowthTypes/Support/Label",   "ui_focus_support"),

            // ── 전술 지침 ─────────────────────────────────────────────────
            E("HUD_Tactics/Header/Title",             "ui_tactics_title"),
            E("HUD_Tactics/Header/Subtitle",          "ui_tactics_subtitle"),
            // ⚠ 성장 창과 <b>같은 칸·같은 키</b>다(위 ui_portrait_pick_hint 의 ⚠).
            E("HUD_Tactics/Info/Portrait/Hint",       "ui_portrait_pick_hint"),
            E("HUD_Tactics/Col1/Head",                "ui_tactics_col1_head"),
            E("HUD_Tactics/Col1/PosLabel",            "ui_tactics_pos_head"),
            E("HUD_Tactics/Col1/PosHint",             "ui_tactics_pos_hint"),
            E("HUD_Tactics/Col1/Pos/Front/Label",     "ui_pos_front"),
            E("HUD_Tactics/Col1/Pos/Mid/Label",       "ui_pos_mid"),
            E("HUD_Tactics/Col1/Pos/Back/Label",      "ui_pos_back"),
            E("HUD_Tactics/Col1/ReactLabel",          "ui_tactics_react_head"),
            E("HUD_Tactics/Col1/React/Chase/Label",   "ui_reaction_chase"),
            E("HUD_Tactics/Col1/React/Hold/Label",    "ui_reaction_hold"),
            E("HUD_Tactics/Col1/TypeLabel",           "ui_tactics_type_head"),
            E("HUD_Tactics/Col1/Type/Melee/Label",    "ui_atk_melee"),
            E("HUD_Tactics/Col1/Type/Ranged/Label",   "ui_atk_ranged"),
            E("HUD_Tactics/Col1/Type/Magic/Label",    "ui_atk_magic"),
            E("HUD_Tactics/Col1/Type/Heal/Label",     "ui_atk_heal"),
            E("HUD_Tactics/Col2/Head",                "ui_tactics_col2_head"),
            E("HUD_Tactics/Col2/TargetLabel",         "ui_tactics_target_head"),
            E("HUD_Tactics/Col2/Target/Nearest/Label",   "ui_target_nearest"),
            E("HUD_Tactics/Col2/Target/Farthest/Label",  "ui_target_farthest"),
            E("HUD_Tactics/Col2/Target/Weakest/Label",   "ui_target_weakest"),
            E("HUD_Tactics/Col2/Target/Strongest/Label", "ui_target_strongest"),
            E("HUD_Tactics/Col2/RetreatLabel",        "ui_tactics_retreat_head"),
            E("HUD_Tactics/Col2/RetreatHint",         "ui_tactics_retreat_hint"),
            E("HUD_Tactics/Col2/RetreatActionLabel",  "ui_tactics_retreat_action_head"),
            E("HUD_Tactics/Col2/RetreatActionHint",   "ui_tactics_retreat_action_hint"),
            E("HUD_Tactics/Col2/RetreatAction/Keep/Label",     "ui_retreat_hold"),
            E("HUD_Tactics/Col2/RetreatAction/WithAlly/Label", "ui_retreat_with_ally"),
            E("HUD_Tactics/Col3/Head",                "ui_tactics_col3_head"),
            E("HUD_Tactics/Col3/NonLabel",            "ui_tactics_scout_head"),
            E("HUD_Tactics/Col3/Non/Explore/Label",   "ui_scout_explore"),
            E("HUD_Tactics/Col3/Non/Hunt/Label",      "ui_scout_hunt"),
            E("HUD_Tactics/Col3/Non/Patrol/Label",    "ui_scout_patrol"),
            E("HUD_Tactics/Col3/RoamLabel",           "ui_tactics_roam_head"),
            E("HUD_Tactics/Col3/Roam/Near/Label",     "ui_roam_near"),
            E("HUD_Tactics/Col3/Roam/Mid/Label",      "ui_roam_mid"),
            E("HUD_Tactics/Col3/Roam/Far/Label",      "ui_roam_far"),
            E("HUD_Tactics/Col3/WaveLabel",           "ui_tactics_wave_head"),
            E("HUD_Tactics/Col3/Wave/Defend/Label",   "ui_wave_defend_now"),
            E("HUD_Tactics/Col3/Wave/Priority/Label", "ui_wave_keep_exploring"),
            E("HUD_Tactics/Info/HpLabel",             "ui_head_hp_now"),
            E("HUD_Tactics/Footer/Note",              "ui_tactics_note"),
            E("HUD_Tactics/Footer/ResetButton/Label", "ui_btn_reset"),
            E("HUD_Tactics/Footer/CloseButton/Label", "ui_btn_close"),

            // ── 로비 씬 (Lobby.unity) ─────────────────────────────────────
            // ★★ 2026-08-26 · 유저 리포트 «로비 버튼들이 영어로 안 바뀐다».
            //   로비는 <b>별개의 씬</b>이라 178절이 게임 씬에만 이 컴포넌트를 붙였고,
            //   로비의 버튼 글자는 <b>아무도 손대지 않는</b> 씬의 정적 라벨이었다.
            //   ⚠ 게임 씬에는 `Lobby` 라는 창이 없다 — 그 경우는 조용히 넘긴다
            //     (<see cref="Apply"/> 의 ★ 를 볼 것).
            //   ⚠ `SettingsWindow/Body/LanguageButton/Label` 은 <b>여기 적지 않는다</b> —
            //     `LobbySettingsWindow` 가 «언어 : {0}» 으로 채우는 자리표다.
            E("Lobby/Menu/ContinueButton/Label",      "ui_lobby_continue"),
            E("Lobby/Menu/NewGameButton/Label",       "ui_lobby_new_game"),
            E("Lobby/Menu/SettingsButton/Label",      "ui_action_settings"),
            E("Lobby/Menu/QuitButton/Label",          "ui_lobby_quit"),
            E("Lobby/SettingsWindow/Header/Title",    "ui_action_settings"),
            E("Lobby/SettingsWindow/Body/Volume/Label",         "ui_settings_volume"),
            E("Lobby/SettingsWindow/Body/HotkeyButton/Label",   "ui_settings_hotkeys"),
            E("Lobby/SettingsWindow/Body/HelpResetButton/Label", "ui_settings_help_reset"),
        };

        /// <summary>이 자리의 «원래 문구» — 표에 키가 없을 때 되돌아갈 값(씬의 첫 문구).</summary>
        readonly Dictionary<string, string> _fallback = new Dictionary<string, string>(128);

        /// <summary>
        /// 이 씬의 최상위 오브젝트 — <see cref="Apply"/> 가 <b>한 번 돌 때만</b> 들고 있는다
        /// (<see cref="FindWindow"/> 의 ②③단계가 쓴다). 계속 들고 있으면 씬이 바뀌었을 때
        /// <b>죽은 오브젝트를 가리킨다</b>.
        /// </summary>
        GameObject[] _roots;

        bool _reported;

        void OnEnable()
        {
            StringTable.OnLanguageChanged -= Apply;
            StringTable.OnLanguageChanged += Apply;
            Apply();
        }

        void OnDisable()
        {
            StringTable.OnLanguageChanged -= Apply;
        }

        /// <summary>지도의 모든 칸을 지금 언어로 채운다. 언어가 바뀔 때마다 다시 돈다.</summary>
        void Apply()
        {
            int done = 0, elsewhere = 0;
            StringBuilder missing = null;
            HashSet<string> absentWindows = _reported ? null : new HashSet<string>();

            // ★ 최상위 목록을 <b>한 번만</b> 뜬다 — `GetRootGameObjects` 는 부를 때마다 배열을
            //   새로 만든다. 지도가 백 칸이라 칸마다 부르면 부팅에 쓸데없는 쓰레기가 쌓인다.
            _roots = gameObject.scene.IsValid() ? gameObject.scene.GetRootGameObjects() : null;

            for (int i = 0; i < Map.Length; i++)
            {
                // ★ 지도 하나를 <b>씬 여럿이 나눠 쓴다</b> — 로비 씬에는 `HUD_*` 창이 없고
                //   게임 씬에는 `Lobby` 가 없다. «이 씬에 없는 창» 은 잘못이 아니므로
                //   조용히 넘긴다. 반대로 <b>창은 있는데 그 안의 경로가 없는 것</b>은
                //   이름이 바뀌었다는 뜻이라 반드시 알린다 — 그것이 이 검산의 값이다.
                Transform window = FindWindow(Head(Map[i].Path));
                if (window == null)
                {
                    elsewhere++;
                    absentWindows?.Add(Head(Map[i].Path));
                    continue;
                }

                TMP_Text label = ResolveIn(window, Map[i].Path);
                if (label == null)
                {
                    if (!_reported)
                    {
                        missing ??= new StringBuilder();
                        missing.Append("\n  · ").Append(Map[i].Path);
                    }
                    continue;
                }

                // ★ 첫 방문에 씬의 문구를 폴백으로 적어 둔다 — 두 번째부터는 이미 바꿔 놓은
                //   글자가 폴백이 되면 «영어를 한국어 폴백으로 되돌리는» 길이 막힌다.
                if (!_fallback.TryGetValue(Map[i].Path, out string fallback))
                {
                    fallback = label.text;
                    _fallback[Map[i].Path] = fallback;
                }

                label.text = StringTable.Get(Map[i].Key, fallback);
                done++;
            }

            if (!_reported)
            {
                _reported = true;
                string other = absentWindows != null && absentWindows.Count > 0
                    ? $" · 이 씬에 없는 창 {absentWindows.Count}개({string.Join(", ", absentWindows)})의 " +
                      $"{elsewhere}칸은 넘겼습니다"
                    : "";

                if (missing != null)
                    Debug.LogWarning($"[Localize] 창은 있는데 못 찾은 라벨 " +
                                     $"{Map.Length - done - elsewhere}칸 " +
                                     $"(경로가 바뀌었는지 볼 것){other}:{missing}", this);
                else
                    Debug.Log($"[Localize] 정적 라벨 {done}칸을 표에 이었습니다 " +
                              $"(언어: {StringTable.Language}){other}.", this);
            }

            _roots = null;      // ★ 들고 있지 않는다 — 위 <see cref="_roots"/> 의 이유
        }

        /// <summary>
        /// 창을 <see cref="FindWindow"/> 로 잡고 나머지는 <see cref="Transform.Find"/> 로
        /// 내려간다(꺼진 창 «안» 도 찾는다 — 그래서 부팅 때 한 번에 갈아 끼울 수 있다).
        /// </summary>
        TMP_Text ResolveIn(Transform window, string path)
        {
            if (window == null) return null;

            int slash = path.IndexOf('/');
            Transform target = slash < 0 ? window : window.Find(path.Substring(slash + 1));
            return target != null ? target.GetComponent<TMP_Text>() : null;
        }

        /// <summary>경로의 첫 칸 — «창» 의 이름.</summary>
        static string Head(string path)
        {
            int slash = path.IndexOf('/');
            return slash < 0 ? path : path.Substring(0, slash);
        }

        /// <summary>
        /// ★★ <b>창을 세 단계로 찾는다</b> (2026-08-26 · 유저 리포트 «「알겠습니다」가 영어로
        /// 안 바뀐다»). 예전에는 <b>자기 자식만</b> 훑었다 — 그런데 도움말 세 창은
        /// <c>UI_Root</c> 가 아니라 <b>씬 최상위의 `Help_Root`</b> 아래에 산다. 그래서 지도의
        /// 일곱 칸(<c>HUD_Help</c>·<c>HUD_HelpCard</c>·<c>HUD_HelpTour</c>)이 <b>한 번도
        /// 적용된 적이 없었다</b>(178절의 검산 도구는 씬 파일을 «전체 검색» 해서 통과시켰다 —
        /// 런타임의 «자기 자식만» 규칙과 어긋났다).
        ///
        /// <code>
        ///   ① 자기 자식           ← 대부분. 이 순서가 <b>먼저</b>인 것이 중요하다
        ///   ② 씬 최상위 오브젝트
        ///   ③ 씬 최상위의 자식     ← Help_Root/HUD_Help …
        /// </code>
        /// ⚠ <b>①을 먼저 보는 것이 규약이다</b> — 씬에 <c>HUD_Portrait</c> 가 둘인데
        ///   (<c>UI_Root</c> 아래의 <b>진짜</b>와 최상위의 <b>꺼진 잔존물</b>) 진짜 쪽은
        ///   창이 닫혀 있어 <c>activeSelf</c> 가 거짓이다. «켜진 쪽 우선» 을 단계 밖으로
        ///   끌어올리면 <b>잔존물이 이긴다</b>. 켜진 쪽 우선은 <b>한 단계 안에서만</b> 쓴다.
        /// </summary>
        Transform FindWindow(string head)
        {
            Transform hit = Pick(transform, head);
            if (hit != null) return hit;

            GameObject[] roots = _roots;
            if (roots == null) return null;

            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == head &&
                    (hit == null || (!hit.gameObject.activeSelf && roots[i].activeSelf)))
                    hit = roots[i].transform;
            if (hit != null) return hit;

            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = Pick(roots[i].transform, head);
                if (found != null &&
                    (hit == null || (!hit.gameObject.activeSelf && found.gameObject.activeSelf)))
                    hit = found;
            }
            return hit;
        }

        /// <summary><paramref name="parent"/> 의 <b>직계 자식</b> 중 그 이름 — 켜진 쪽 우선.</summary>
        static Transform Pick(Transform parent, string name)
        {
            Transform hit = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name != name) continue;
                if (hit == null || (!hit.gameObject.activeSelf && child.gameObject.activeSelf))
                    hit = child;
            }
            return hit;
        }
    }
}
