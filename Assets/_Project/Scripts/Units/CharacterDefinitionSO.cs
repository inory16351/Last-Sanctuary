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

        [Tooltip("character_name — 한글 이름. UI 에 이걸 보여준다")]
        public string characterName = "";

        [Tooltip("character_name_EG — 영어 이름. 에셋 폴더 이름과 맞춘다")]
        public string characterNameEn = "";

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
