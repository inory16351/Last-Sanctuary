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
            E("HUD_Subjugate/Squads/Label",           "ui_head_squads"),
            E("HUD_Subjugate/Targets/Label",          "ui_subj_targets_head"),

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
        };

        /// <summary>이 자리의 «원래 문구» — 표에 키가 없을 때 되돌아갈 값(씬의 첫 문구).</summary>
        readonly Dictionary<string, string> _fallback = new Dictionary<string, string>(128);

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
            int done = 0;
            StringBuilder missing = null;

            for (int i = 0; i < Map.Length; i++)
            {
                TMP_Text label = Resolve(Map[i].Path);
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
                if (missing != null)
                    Debug.LogWarning($"[Localize] 씬에서 못 찾은 라벨 {Map.Length - done}칸 " +
                                     $"(경로가 바뀌었는지 볼 것):{missing}", this);
                else
                    Debug.Log($"[Localize] 정적 라벨 {done}칸을 표에 이었습니다 " +
                              $"(언어: {StringTable.Language}).", this);
            }
        }

        /// <summary>
        /// 첫 칸(창 이름)은 <b>켜져 있는 것을 먼저</b> 고르고, 나머지는
        /// <see cref="Transform.Find"/> 로 내려간다(꺼진 창 안도 찾는다).
        /// </summary>
        TMP_Text Resolve(string path)
        {
            int slash = path.IndexOf('/');
            string head = slash < 0 ? path : path.Substring(0, slash);

            Transform window = null;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name != head) continue;
                if (window == null || (!window.gameObject.activeSelf && child.gameObject.activeSelf))
                    window = child;
            }
            if (window == null) return null;

            Transform target = slash < 0 ? window : window.Find(path.Substring(slash + 1));
            return target != null ? target.GetComponent<TMP_Text>() : null;
        }
    }
}
