using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 건물(포탑) 외형 한 벌. <see cref="TowerAnimator"/> 가 이걸 읽어 재생한다.
    ///
    /// <b>왜 <see cref="CharacterSkinSO"/> 를 재사용하지 않는가</b> — 건물은 캐릭터와 모션 구성이
    /// 아예 다르다. 건물은 <b>이동하지 않으므로 걷기가 없고, 좌·우 방향도 없다</b>(고정 구조물이라
    /// 늘 같은 면을 보인다). 대신 캐릭터에는 없는 <b>파괴(Destroy)</b> 모션이 있다 — 한 번만
    /// 재생하고 끝나는 종류라 반복 재생하는 캐릭터 모션과 재생 규칙 자체가 다르다.
    /// 방향·걷기 필드를 비워둔 채 캐릭터 스킨을 쓰면 "왜 비어 있나"를 다음 세션이 다시 조사해야
    /// 하고, 캐릭터 쪽 규칙(좌우 대체 폴백 등)이 건물에 잘못 적용될 여지도 남는다.
    ///
    /// <b>왜 <c>Resources</c> 에 두는가</b> — 스프라이트 배열은 오브젝트 참조라서 MCP 로 씬의
    /// 템플릿에 넣을 수 없다(진행상황 8절 4번). 에셋을 폴더에 넣기만 하면 되는 구조로 만들면
    /// 씬에 배선할 참조가 하나도 없다. <see cref="CharacterSkinSO"/> 와 같은 이유·같은 방식이다.
    ///
    /// <b>폴더를 캐릭터·몬스터와 나누는 이유</b> — <see cref="CharacterAnimator"/> 가 자기 폴더의
    /// 스킨 중 하나를 무작위로 고르기 때문에, 같은 폴더에 넣으면 캐릭터가 포탑 모습으로 뽑힌다
    /// (진행상황 27-1절에서 늑대 스킨을 <c>MonsterSkins</c> 로 나눈 것과 같은 이유).
    /// </summary>
    [CreateAssetMenu(fileName = "Skin_", menuName = "Last Sanctuary/Tower Skin")]
    public class TowerSkinSO : ScriptableObject
    {
        [Header("표시 이름 (로그·디버그용)")]
        public string displayName = "";

        [Header("재생 속도")]
        [Tooltip("대기 모션의 초당 재생 수")]
        [Min(1f)] public float framesPerSecond = 10f;

        [Tooltip("공격 모션의 초당 재생 수")]
        [Min(1f)] public float attackFramesPerSecond = 14f;

        [Tooltip("파괴 모션의 초당 재생 수. 이 값이 파괴 연출 길이를 정한다 " +
                 "(프레임 수 ÷ 이 값 = 초)")]
        [Min(1f)] public float destroyFramesPerSecond = 12f;

        [Header("대기 (Idle) — 창의 불빛이 흔들리는 정지 모션")]
        public Sprite[] idle;

        [Header("공격 (Attack) — 빔은 원화에서 오려냈다. 창의 문양만 빛난다")]
        public Sprite[] attack;

        [Header("파괴 (Destroy) — 한 번만 재생한다. 마지막 프레임에서 오브젝트가 사라진다")]
        public Sprite[] destroy;

        public bool HasIdle => Has(idle);
        public bool HasAttack => Has(attack);
        public bool HasDestroy => Has(destroy);

        /// <summary>이 스킨이 쓸 만한지 (대기 프레임이 하나라도 있는지).</summary>
        public bool IsUsable => HasIdle;

        /// <summary>공격 모션 한 바퀴에 걸리는 시간(초). 이 시간만큼 공격 모션을 유지한다.</summary>
        public float AttackClipSeconds =>
            !HasAttack || attackFramesPerSecond <= 0f ? 0f : attack.Length / attackFramesPerSecond;

        /// <summary>파괴 모션 전체 길이(초). <see cref="TowerAnimator"/> 가 이 시간 뒤에 끝났다고 알린다.</summary>
        public float DestroyClipSeconds =>
            !HasDestroy || destroyFramesPerSecond <= 0f ? 0f : destroy.Length / destroyFramesPerSecond;

        static bool Has(Sprite[] frames) => frames != null && frames.Length > 0;
    }
}
