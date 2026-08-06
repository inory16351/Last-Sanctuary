using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 캐릭터 외형 한 벌(스킨). 모션별·방향별 스프라이트 프레임 목록을 들고 있고,
    /// <see cref="CharacterAnimator"/> 가 이걸 읽어 재생한다.
    ///
    /// <b>왜 <c>Resources</c> 에 두는가</b> — 스킨 목록을 컴포넌트의 배열 필드로 두면
    /// 오브젝트 참조라서 MCP 로 씬에 넣을 수 없다(진행상황 8절 4번). <c>Resources.LoadAll</c> 로
    /// 폴더째 읽으면 참조가 하나도 필요 없고, <b>스킨을 추가하는 것도 에셋 파일을 폴더에
    /// 넣기만 하면 끝</b>이다. HUD 폰트가 이미 같은 이유로 <c>Resources</c> 를 쓴다
    /// (<see cref="LastSanctuary.UI.HudTheme.FontResourcePath"/>).
    ///
    /// <b>왜 Combat 폴더인가</b> — 애니메이션은 전투 로직이 아니지만, UI 브랜치가 통째로
    /// 소유하는 경로가 <c>Scripts/Combat/**</c> 와 <c>Scripts/UI/**</c> 뿐이다
    /// (준수사항 §2). 머지 충돌을 안 만들려고 여기 뒀다.
    ///
    /// <b>지금은 생성 시 무작위로 하나를 고른다</b>(유저 확정: 프로토타입 단계). 나중에
    /// 캐릭터별 테이블을 파싱하게 되면 <see cref="CharacterAnimator.SetSkin"/> 으로
    /// 지정만 해주면 되고, 이 클래스는 안 바뀐다.
    /// </summary>
    [CreateAssetMenu(fileName = "Skin_", menuName = "Last Sanctuary/Character Skin")]
    public class CharacterSkinSO : ScriptableObject
    {
        [Header("표시 이름 (로그·디버그용)")]
        public string displayName = "";

        [Header("재생 속도")]
        [Tooltip("대기·이동 프레임의 초당 재생 수")]
        [Min(1f)] public float framesPerSecond = 10f;

        [Tooltip("공격 모션의 초당 재생 수. 공격은 조금 빠른 편이 타격감이 산다")]
        [Min(1f)] public float attackFramesPerSecond = 14f;

        [Header("대기 (Idle)")]
        public Sprite[] idleRight;
        public Sprite[] idleLeft;

        [Header("이동 (Walk)")]
        public Sprite[] walkRight;
        public Sprite[] walkLeft;

        [Header("근접 공격 (Attack / MeleeAttack)")]
        public Sprite[] attackRight;
        public Sprite[] attackLeft;

        [Header("원거리 공격 (RangedAttack — 없으면 근접 모션을 재사용한다)")]
        public Sprite[] rangedRight;
        public Sprite[] rangedLeft;

        public Sprite[] Idle(bool facingRight) => Pick(idleRight, idleLeft, facingRight);
        public Sprite[] Walk(bool facingRight) => Pick(walkRight, walkLeft, facingRight);

        /// <summary>공격 프레임. 원거리 프레임이 없는 스킨은 근접 모션으로 대체한다.</summary>
        public Sprite[] Attack(bool facingRight, bool ranged)
        {
            if (ranged)
            {
                Sprite[] r = Pick(rangedRight, rangedLeft, facingRight);
                if (HasFrames(r)) return r;
            }
            return Pick(attackRight, attackLeft, facingRight);
        }

        /// <summary>공격 모션 한 바퀴에 걸리는 시간(초). 애니메이션이 끊기지 않게 이 시간만큼 유지한다.</summary>
        public float AttackClipSeconds(bool facingRight, bool ranged)
        {
            Sprite[] frames = Attack(facingRight, ranged);
            if (!HasFrames(frames) || attackFramesPerSecond <= 0f) return 0f;
            return frames.Length / attackFramesPerSecond;
        }

        /// <summary>이 스킨이 쓸 만한지 (프레임이 하나라도 있는지).</summary>
        public bool IsUsable => HasFrames(idleRight) || HasFrames(idleLeft);

        static Sprite[] Pick(Sprite[] right, Sprite[] left, bool facingRight)
        {
            // 한쪽 방향만 있는 스킨도 그림이 안 나오는 것보다는 나으므로 반대쪽으로 대체한다.
            if (facingRight) return HasFrames(right) ? right : left;
            return HasFrames(left) ? left : right;
        }

        static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;
    }
}
