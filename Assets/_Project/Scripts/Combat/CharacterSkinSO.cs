using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 스킨이 재생할 공격 계열 모션. <see cref="TacticalAttackType"/> 와 1:1 이 아니다 —
    /// 마법은 원거리 모션을 같이 쓰기 때문에 스킨 쪽에는 세 종류만 있으면 된다.
    /// </summary>
    public enum SkinAttackMotion
    {
        /// <summary>붙어서 휘두르는 동작.</summary>
        Melee,

        /// <summary>원거리·마법 — 던지거나 시전하는 동작.</summary>
        Ranged,

        /// <summary>회복 — 아군을 살리는 동작.</summary>
        Heal,
    }

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

        [Header("회복 (Heal — 없으면 원거리 → 근접 공격 모션을 재사용한다)")]
        [Tooltip("전술 지침을 '회복'으로 둔 캐릭터가 아군을 살릴 때 쓰는 모션. " +
                 "전용 원화가 있는 캐릭터가 아직 없어서 지금은 전부 공격 모션으로 대체된다 " +
                 "(유저 지시 2026-08-11: '없으면 공격 모션 사용')")]
        public Sprite[] healRight;
        public Sprite[] healLeft;

        // ------------------------------------------------------------------
        // 투사체 — 객체(스킨)마다 따로 관리한다
        // ------------------------------------------------------------------

        /// <summary>
        /// 이 외형이 쏘는 탄환. <see cref="CombatProjectileFx"/> 가 원거리·마법 공격이
        /// 성사될 때 읽어서 날린다.
        ///
        /// <b>왜 스킨이 들고 있나</b> — 예전에는 <c>CombatProjectileFx</c> 안에서
        /// "진영이 암세포면 이 그림 · 종류가 포탑이면 저 그림" 으로 분기했다. 그래서
        /// <b>같은 진영이면 누가 쏘든 같은 탄환</b>이 날아갔고, 캐릭터를 추가할 때마다
        /// 연출 코드에 분기를 하나 더 넣어야 했다. 외형과 탄환은 같이 그려지는 한 벌이므로
        /// 외형 에셋이 들고 있는 것이 맞다 — 이제 <b>스킨 파일에 프레임을 넣기만 하면</b>
        /// 그 유닛만의 탄환이 된다.
        ///
        /// <b>방향별 원화를 두지 않는 이유</b> — 탄환은 진행 방향으로 회전시켜 그린다
        /// (<c>CombatProjectileFx.AimAt</c>). 그림은 <b>+X(오른쪽)를 향한 한 벌</b>만 있으면
        /// 모든 방향이 나오고, 방향별로 넣으면 왼쪽으로 갈 때 두 번 뒤집혀 거꾸로 날아간다.
        ///
        /// 여러 장이면 비행 시간 전체에 고르게 펼쳐 재생한다(분비형 암세포의 침처럼
        /// 날아가면서 부서지는 연출이 된다). 비워두면 <c>CombatProjectileFx</c> 의
        /// 기본 탄환으로 넘어간다.
        /// </summary>
        [Header("투사체 (원거리·마법 공격 시 날아가는 탄환 — +X 를 향한 그림 한 벌)")]
        public Sprite[] projectileFrames;

        [Tooltip("발사 순간 손끝·총구에서 터지는 섬광. 쏘는 쪽에서 한 번 반짝이고 사라진다. " +
                 "비워두면 섬광 없이 탄환만 날아간다")]
        public Sprite[] muzzleFlashFrames;

        /// <summary>
        /// <b>착탄 효과</b> — 탄환이 목표에 닿는 순간 <b>맞는 쪽 자리에</b> 재생된다.
        /// 마법 공격이면 이것이 곧 <b>피해 범위 표시</b> 역할을 한다.
        ///
        /// ⚠ <b>발사 섬광(<see cref="muzzleFlashFrames"/>)과 다르다.</b> 처음에는 프레이야의
        /// <c>ProjectileBurst</c> 원화를 손끝 섬광으로 붙였는데, 그림을 보면 창이 꽂히고
        /// 사방으로 터지는 <b>맞은 자리</b>의 연출이었다(유저 지적 2026-08-11).
        /// 쏘는 쪽에 붙이면 자기 발밑에서 폭발이 터지는 그림이 된다.
        ///
        /// ⚠ <b>원화가 측면 시점이다</b> — 폭발이 위로 솟는 그림이라 3/4 탑뷰 바닥에 그대로
        /// 놓으면 서 있는 것처럼 보인다. <see cref="impactScale"/> 의 y 를 줄여 눕히거나
        /// 원화를 탑뷰로 다시 그려야 한다(유저가 이미 인지 — 다음 작업 후보).
        /// </summary>
        [Tooltip("탄환이 닿는 순간 맞는 쪽에 재생한다. 마법이면 피해 범위 표시가 된다. " +
                 "비워두면 착탄 연출이 없다")]
        public Sprite[] impactFrames;

        [Tooltip("탄환 원화를 이 배율로 줄여 그린다. 원화가 큰 편이라 1 로 두면 화면을 덮는다")]
        [Min(0.05f)] public float projectileScale = 0.55f;

        [Tooltip("착탄 효과 배율. 3/4 탑뷰라 원화가 세로로 서 있는 폭발이면 y 를 줄여 바닥에 눕힌다")]
        public Vector2 impactScale = Vector2.one;

        /// <summary>이 스킨이 자기 탄환을 들고 있는지. 없으면 연출 쪽 기본 탄환을 쓴다.</summary>
        public bool HasProjectile => HasFrames(projectileFrames);

        public Sprite[] Idle(bool facingRight) => Pick(idleRight, idleLeft, facingRight);
        public Sprite[] Walk(bool facingRight) => Pick(walkRight, walkLeft, facingRight);

        /// <summary>
        /// 공격 프레임. 폴백 순서는 <b>회복 → 원거리 → 근접</b> 이다.
        /// 회복 원화가 없는 스킨은 원거리를, 원거리도 없으면 근접 모션을 쓴다 —
        /// 그림이 아예 안 나오는 것보다는 낫고, 유저가 지시한 규칙이기도 하다.
        /// </summary>
        public Sprite[] Attack(bool facingRight, SkinAttackMotion motion)
        {
            if (motion == SkinAttackMotion.Heal)
            {
                Sprite[] h = Pick(healRight, healLeft, facingRight);
                if (HasFrames(h)) return h;
            }

            // 회복도 전용 모션이 없으면 원거리 동작으로 대체한다 (붙어서 휘두르는 동작이 아니다).
            if (motion != SkinAttackMotion.Melee)
            {
                Sprite[] r = Pick(rangedRight, rangedLeft, facingRight);
                if (HasFrames(r)) return r;
            }

            return Pick(attackRight, attackLeft, facingRight);
        }

        /// <summary>공격 모션 한 바퀴에 걸리는 시간(초). 애니메이션이 끊기지 않게 이 시간만큼 유지한다.</summary>
        public float AttackClipSeconds(bool facingRight, SkinAttackMotion motion)
        {
            Sprite[] frames = Attack(facingRight, motion);
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
