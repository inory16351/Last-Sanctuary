using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 캐릭터 한 명의 정의. 캐릭터 테이블(<c>캐릭터 테이블.xlsx</c>)의
    /// <b>Character</b> 시트 한 줄 + <b>first_Stat</b> 시트 한 줄을 합친 데이터다.
    ///
    /// 지금까지 캐릭터는 <see cref="StatBlock.Roll"/> 로 능력치를 무작위로 굴려 만들었지만,
    /// 이제 <b>정해진 인물</b>(엘린 · 비기오르 · 프레이야)을 뽑아 쓴다.
    ///
    /// ⚠ <b>등장 규칙 (유저 확정 2026-08-11)</b>: 캐릭터는 <b>한 판에 한 번만 등장</b>한다 —
    /// <b>사망하더라도 다시 등장할 수 없다.</b> "살아있는 인물 제외"가 아니라
    /// "이 판에 한 번이라도 등장한 인물 제외"다. 캐릭터가 더 추가될 예정이라
    /// 후보 풀이 커지면 이 규칙이 실제로 의미를 갖는다.
    ///
    /// 지금은 유저 지시대로 <b>무작위 등장(중복 허용)</b> 상태로 두고,
    /// 규칙이 들어갈 자리는 <see cref="CharacterDefinitionRegistry"/> 한 곳에 모아뒀다 —
    /// <c>preventReappearance</c> 를 켜기만 하면 동작한다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Units/Character Definition", fileName = "Character_")]
    public class CharacterDefinitionSO : ScriptableObject
    {
        [Header("식별 — Character 시트")]
        [Tooltip("character_id")]
        public int characterId;

        [Tooltip("스트링 키 (스트링 키 테이블.xlsx). 예: character_name_9001\n" +
                 "★ 화면에 뜨는 이름의 정본은 이 키다 — DisplayName 이 스트링 테이블에서 읽는다.\n" +
                 "비워두면 아래 characterName 리터럴을 그대로 쓴다(하위 호환)")]
        public string nameKey = "";

        [Tooltip("character_name — 한글 이름. ⚠ 스트링 테이블 도입 이후로는 " +
                 "nameKey 를 못 찾았을 때의 폴백일 뿐이다. 문구는 스트링 키 테이블에서 고칠 것")]
        public string characterName = "";

        [Tooltip("character_name_EG — 영어 이름. 에셋 폴더·스킨 이름을 맞추는 용도로 남겨둔다. " +
                 "화면 표시용 영어는 스트링 테이블의 en 컬럼이 정본이다")]
        public string characterNameEn = "";

        /// <summary>
        /// 화면에 보여줄 이름. <b>스트링 테이블이 먼저다</b>(유저 지시 2026-08-12 —
        /// 모든 테이블 문자열을 스트링 키로 관리한다).
        /// 키가 비었거나 표에 없으면 <see cref="characterName"/> 리터럴로 폴백하므로,
        /// 키를 아직 안 붙인 에셋도 그대로 동작한다.
        /// </summary>
        public string DisplayName => Data.StringTable.Get(nameKey, characterName);

        // ------------------------------------------------------------------
        // 칭호 (2026-08-19) — 표의 character_title / character_title_EG
        //
        // 상세 카드(112절)에 칭호 칸을 만들어 뒀는데 <b>캐릭터에는 칭호 데이터가 아예
        // 없어서</b> 항상 빈칸이었다(몬스터·넥서스만 표에 칭호가 있었다). 그 칸을 채운다.
        // 구조는 몬스터 쪽(<c>MonsterDefinitionSO.Title</c>)과 완전히 같다 — 키가 정본,
        // 리터럴은 폴백.
        // ------------------------------------------------------------------

        [Header("칭호 — Character 시트의 character_title")]
        [Tooltip("스트링 키 (예: character_title_9001). ★ 화면에 뜨는 칭호의 정본은 이 키다.\n" +
                 "⚠ 비워두면 칭호 칸이 <b>빈칸</b>으로 남는다 — 유저 확정: " +
                 "\"칭호 해금이 되지 않았을 때는 칭호칸 비워놔\"")]
        public string titleKey = "";

        [Tooltip("⚠ titleKey 를 못 찾았을 때의 폴백. 문구는 스트링 키 테이블에서 고칠 것")]
        public string title = "";

        /// <summary>
        /// 화면에 보여줄 칭호. 없으면 <b>빈 문자열</b>이고, 상세 카드는 그 줄을 비워 둔다
        /// (<c>UnitPortraitPanel.Show</c>).
        /// </summary>
        public string Title => Data.StringTable.Get(titleKey, title);

        [Header("외형")]
        [Tooltip("illust — Resources/Illust/ 아래의 파일 이름 (확장자 없이). " +
                 "전술 지침 · 캐릭터 성장 창의 초상화에 쓰인다")]
        public string illustName = "";

        [Tooltip("ingame_asset — Resources/Skins/ 아래의 CharacterSkinSO 에셋 이름. " +
                 "비우면 CharacterAnimator 가 기존처럼 무작위 스킨을 고른다")]
        public string skinAssetName = "";

        [Header("초기 능력치 — first_Stat 시트 (1~100)")]
        [Tooltip("테이블의 12컬럼을 그대로 옮긴 값. 생성 시 이 값이 그대로 들어간다(랜덤 롤 없음)")]
        public StatBlock stats = new StatBlock
        {
            hp = 5, attack = 5, defense = 5, regen = 5,
            rangedAttack = 5, magic = 5, cure = 5,
            accuracy = 5, critical = 5, attackSpeed = 5, moveSpeed = 5,
            resistance = 50,
        };

        [Header("역할 (생성 시 전술 지침 기본값)")]
        [Tooltip("이 인물이 태어날 때 갖는 공격 유형. Auto 면 능력치에서 역산한다 " +
                 "(네 공격 계열 중 가장 높은 것 — CharacterRole 참조).\n" +
                 "역산 결과가 마음에 안 드는 인물만 여기서 못 박으면 된다")]
        public RoleAttackPreset attackPreset = RoleAttackPreset.Auto;

        [Tooltip("이 인물이 태어날 때 갖는 전열 위치. Auto 면 공격 유형과 맷집(체력+방어력)에서 " +
                 "역산한다 — 근거리이고 튼튼하면 전방, 무르면 중위, 원거리·마법은 후방, 치유는 중위")]
        public RolePositionPreset positionPreset = RolePositionPreset.Auto;

        [Header("패시브 스킬 3종 — Character 시트의 skill_01~03")]
        [Tooltip("순서가 곧 해금 순서다. 0번은 생성 시 즉시 해금, " +
                 "1·2번은 강화 횟수가 조건에 도달하면 해금된다 (캐릭터 가이드 p6).\n" +
                 "해금에 필요한 강화 횟수는 이 에셋이 아니라 씬의 GameSystems > PassiveUnlockConfig 에 있다")]
        public PassiveSkillSO[] passives = new PassiveSkillSO[3];

        [Header("내러티브")]
        [Tooltip("캐릭터 가이드 p7 — 겉은 천사, 실체는 백혈구라는 컨셉이 드러나게")]
        [TextArea(2, 5)] public string narrative = "";

        Sprite _illust;
        bool _illustLoaded;

        /// <summary>초상화 일러스트. <c>Resources/Illust/</c> 에서 이름으로 읽어 캐시한다.</summary>
        public Sprite Illust
        {
            get
            {
                if (_illustLoaded) return _illust;
                _illustLoaded = true;
                if (!string.IsNullOrWhiteSpace(illustName))
                {
                    _illust = Resources.Load<Sprite>("Illust/" + illustName.Trim());
                    if (_illust == null)
                        Debug.LogWarning($"[Character] 일러스트 'Resources/Illust/{illustName}' 을 찾지 못했습니다. ({characterName})", this);
                }
                return _illust;
            }
        }

        /// <summary>
        /// 강화 횟수 기준으로 이 슬롯의 패시브가 해금됐는지.
        /// 조건 자체는 씬의 <see cref="PassiveUnlockConfig"/> 가 들고 있다 —
        /// 캐릭터마다 다른 값이 아니라 게임 전체의 성장 곡선이라서 한 곳에 모아뒀다.
        /// </summary>
        public bool IsPassiveUnlocked(int slot, int upgradeCount) =>
            PassiveUnlockConfig.IsUnlocked(slot, upgradeCount);

        /// <summary>이 슬롯이 해금되기까지 필요한 강화 횟수. 미해금 안내 문구에 쓴다.</summary>
        public int UnlockUpgradesFor(int slot) => PassiveUnlockConfig.RequiredUpgrades(slot);

        public PassiveSkillSO PassiveAt(int slot) =>
            passives != null && slot >= 0 && slot < passives.Length ? passives[slot] : null;

        public bool IsUsable => characterId != 0 && !string.IsNullOrWhiteSpace(characterName);
    }
}
