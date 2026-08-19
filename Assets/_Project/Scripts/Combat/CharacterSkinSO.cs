using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 스킨이 재생할 공격 계열 모션.
    ///
    /// ★ <b>2026-08-19 부터 <see cref="Magic"/> 이 따로 있다.</b> 예전에는 셋뿐이었고
    /// 마법이 <see cref="Ranged"/> 모션을 같이 썼다 — "둘 다 떨어져서 시전하는 동작"이라는
    /// 이유였고, 그때는 <b>마법 전용 원화를 가진 캐릭터가 없었다.</b>
    /// 엘린 모션 시트가 「원거리 공격 — 기도 후 쇠사슬 솟구침」과 「마법 공격 — 기도 후
    /// 쇠사슬 마법」을 <b>다른 동작으로</b> 그려 오면서 그 전제가 깨졌다(같은 기도 자세가
    /// 아니다: 5번 프레임이 65x62 vs 55x74 · 평균 화소차 26~66 — 실측).
    ///
    /// ⚠ <b>기존 스킨은 아무것도 안 바뀐다</b> — 마법 칸이 비어 있으면
    /// <see cref="CharacterSkinSO.Attack"/> 가 예전처럼 원거리 모션으로 떨어진다.
    /// </summary>
    public enum SkinAttackMotion
    {
        /// <summary>붙어서 휘두르는 동작.</summary>
        Melee,

        /// <summary>원거리 — 던지거나 쏘는 동작.</summary>
        Ranged,

        /// <summary>회복 — 아군을 살리는 동작.</summary>
        Heal,

        /// <summary>마법 — 시전하는 동작. 전용 원화가 없으면 <see cref="Ranged"/> 로 떨어진다.</summary>
        Magic,
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

        [Header("마법 (Magic — 없으면 원거리 → 근접 공격 모션을 재사용한다)")]
        [Tooltip("★ 2026-08-19 신설. 전술 공격 유형이 '마법' 인 캐릭터가 시전할 때 쓰는 모션.\n\n" +
                 "<b>왜 원거리 칸과 나눴나</b> — 예전에는 마법이 원거리 모션을 같이 썼다. " +
                 "그 전제는 <b>마법 전용 원화가 없었기 때문</b>이지 둘이 같은 동작이어서가 " +
                 "아니다. 엘린 모션 시트가 「원거리 — 기도 후 쇠사슬 솟구침」과 " +
                 "「마법 — 기도 후 쇠사슬 마법」을 <b>다른 동작으로</b> 그려 왔고 " +
                 "(5번 프레임 65x62 vs 55x74 · 평균 화소차 26~66), 받을 칸이 없으면 " +
                 "한쪽 원화를 버려야 했다.\n" +
                 "⚠ 비워두면 <b>예전과 완전히 같다</b> — 원거리 모션으로 떨어진다")]
        public Sprite[] magicRight;
        public Sprite[] magicLeft;

        [Header("회복 (Heal — 없으면 원거리 → 근접 공격 모션을 재사용한다)")]
        [Tooltip("전술 지침을 '회복'으로 둔 캐릭터가 아군을 살릴 때 쓰는 모션. " +
                 "전용 원화가 있는 캐릭터가 아직 없어서 지금은 전부 공격 모션으로 대체된다 " +
                 "(유저 지시 2026-08-11: '없으면 공격 모션 사용')")]
        public Sprite[] healRight;
        public Sprite[] healLeft;

        [Tooltip("★ 2026-08-19 신설. 회복이 <b>실제로 들어간 순간 맞는 쪽 발밑에</b> 깔리는 " +
                 "연출(엘린 시트의 「회복 이펙트」 = 초록 십자가가 솟는 7장).\n\n" +
                 "<b>왜 impactFrames 로 못 쓰나</b> — 그 칸은 <b>투사체가 닿는 순간</b>에만 " +
                 "돌아가고(CombatProjectileFx.HandleAttack 이 원거리·마법에서만 부른다), " +
                 "회복은 투사체가 없어서 그 경로를 아예 타지 않는다. 그래서 회복에는 " +
                 "<b>연출이 하나도 없었다</b>(UnitCombat.PerformHeal 이 체력만 올렸다).\n" +
                 "방향이 없어 한 벌만 둔다 — 바닥에 깔리는 그림이다")]
        public Sprite[] healFxFrames;

        /// <summary>회복 지면 연출. 없으면 null — 그러면 예전처럼 연출 없이 체력만 오른다.</summary>
        public Sprite[] HealFx() => HasFrames(healFxFrames) ? healFxFrames : null;

        // ------------------------------------------------------------------
        // 부활 (2026-08-14 — 히스톤)
        //
        // 히스톤의 원화 시트에 <b>부활 모션</b>과 <b>부활 시 주변 이펙트</b> 두 행이 들어 있다.
        // 표의 패시브 스킬이 그 근거다:
        //   Rage_on(80014) — 분노 100 에서 죽으면 경직 뒤 <b>부활</b>한다  → reviveRight/Left
        //   Reaver (80015) — 부활할 때 반경 범위에 피해 + 아군 회복        → reviveFx
        //
        // ★ <b>이제 실제로 재생된다</b> (2026-08-15, 미결 177·178번 해소):
        //   <see cref="CharacterAnimator.PlayReviveMotion"/> 이 경직 구간에 reviveRight/Left 를 돌리고,
        //   <see cref="CharacterPassives"/> 의 「복수자」가 되살아나는 순간 reviveFx 를 범위 크기로 깐다.
        //
        // ⚠ 33-6절("캐릭터는 죽으면 다시 등장하지 않는다")과 <b>충돌하지 않는다</b> — 그 규칙은
        //   <b>새로 생성될 때 그 id 를 다시 뽑지 않는다</b>는 뜻이고, 여기 부활은 죽은 자리에서
        //   같은 개체가 일어나는 것이다. 히스톤은 파괴되지 않으므로 등장 장부에 손대지 않는다.
        //   히스톤 외의 스킨은 이 칸이 비어 있고, 비어 있으면 부활 연출은 통째로 생략된다.
        // ------------------------------------------------------------------

        [Header("부활 (Revive — 「분노」 80014 의 경직 구간에 재생된다)")]
        public Sprite[] reviveRight;
        public Sprite[] reviveLeft;

        [Tooltip("부활할 때 발밑에 퍼지는 범위 연출. 방향이 없어 한 벌만 둔다")]
        public Sprite[] reviveFx;

        /// <summary>
        /// 부활 모션. <b>없으면 null</b> — 평타로 대체하지 않는다
        /// (<see cref="CharacterAnimator.PlayReviveMotion"/> 주석 참조).
        /// 히스톤 외의 스킨은 이 칸이 비어 있다.
        /// </summary>
        public Sprite[] Revive(bool facingRight)
        {
            Sprite[] frames = Pick(reviveRight, reviveLeft, facingRight);
            return HasFrames(frames) ? frames : null;
        }

        /// <summary>부활 모션 한 바퀴에 걸리는 시간(초). 없으면 0.</summary>
        public float ReviveClipSeconds(bool facingRight)
        {
            Sprite[] frames = Revive(facingRight);
            return frames == null || attackFramesPerSecond <= 0f
                ? 0f
                : frames.Length / attackFramesPerSecond;
        }

        /// <summary>부활 지면 연출. 없으면 null.</summary>
        public Sprite[] ReviveFx() => HasFrames(reviveFx) ? reviveFx : null;

        // ------------------------------------------------------------------
        // 보스 스킬 모션 (2026-08-13)
        //
        // <b>왜 슬롯 번호인가</b> — 스킬 종류(enum)로 칸을 나누면 표에 스킬이 하나 늘 때마다
        // 이 클래스에 필드를 두 개씩 더해야 한다. 몬스터 정의의 <c>bossSkillIds</c> 순서가
        // 곧 슬롯이라, <b>표의 boss_skill_1 → 슬롯 0 · boss_skill_2 → 슬롯 1</b> 로 그대로 맞다.
        // 단탈리온은 두 개뿐이므로 두 벌만 둔다(<see cref="SkillMotion"/> 가 범위 밖을 null 로 돌려준다).
        //
        // ⚠ 원화는 이미 임포트돼 있었다(<c>SpecialShockwave</c> · <c>SpecialBeam</c> · <c>Fx</c>,
        //   59-3절). 스킬이 미구현이라 스킨에 배선만 안 돼 있던 것을 이제 연결한다.
        // ------------------------------------------------------------------

        [Header("보스 스킬 — 시전 모션 (슬롯 0 = 표의 boss_skill_1)")]
        [Tooltip("슬롯 0 시전 동작. 단탈리온은 SpecialShockwave(타락한 무덤)")]
        public Sprite[] skill1Right;
        public Sprite[] skill1Left;

        [Tooltip("슬롯 1 시전 동작. 단탈리온은 SpecialBeam(공허의 광선)")]
        public Sprite[] skill2Right;
        public Sprite[] skill2Left;

        [Header("보스 스킬 — 지면 연출 (피해 범위 표시)")]
        [Tooltip("슬롯 0 의 범위 연출. 범위(가로 x 세로 타일)에 맞춰 늘려 그린다 — " +
                 "마법 착탄과 같은 원칙으로 <b>보이는 범위 = 맞는 범위</b> 가 된다")]
        public Sprite[] skill1Fx;

        [Tooltip("슬롯 1 의 범위 연출")]
        public Sprite[] skill2Fx;

        [Header("보스 스킬 — 날아가는 탄환 (2026-08-18 신설)")]
        [Tooltip("★ <b>스킬 전용 탄환</b>. 말파스 「구속탄」이 첫 사용자다.\n\n" +
                 "<b>왜 projectileFrames 를 못 쓰나</b> — 그쪽은 <b>평타</b> 탄환이고 " +
                 "말파스는 평타가 검은 구체·구속탄이 초록 구체로 그림 자체가 다르다. " +
                 "슬롯을 나눠 두면 표에 스킬을 추가할 때 그림만 넣으면 된다.\n" +
                 "비워두면 평타 탄환으로 떨어진다 — 탄환이 아예 없는 스킬은 그냥 비워둘 것")]
        public Sprite[] skill1Projectile;

        [Tooltip("슬롯 1 의 스킬 전용 탄환")]
        public Sprite[] skill2Projectile;

        /// <summary>슬롯의 스킬 전용 탄환. 없으면 null — 호출부가 평타 탄환으로 떨어진다.</summary>
        public Sprite[] SkillProjectile(int slot)
        {
            switch (slot)
            {
                case 0: return HasFrames(skill1Projectile) ? skill1Projectile : null;
                case 1: return HasFrames(skill2Projectile) ? skill2Projectile : null;
                default: return null;
            }
        }

        /// <summary>슬롯의 시전 모션. 없으면 null — 그러면 평타 모션으로 대체된다.</summary>
        public Sprite[] SkillMotion(int slot, bool facingRight)
        {
            switch (slot)
            {
                case 0: return Pick(skill1Right, skill1Left, facingRight);
                case 1: return Pick(skill2Right, skill2Left, facingRight);
                default: return null;
            }
        }

        /// <summary>슬롯의 지면 연출. 없으면 null — 그러면 범위 표시 없이 피해만 들어간다.</summary>
        public Sprite[] SkillFx(int slot)
        {
            switch (slot)
            {
                case 0: return HasFrames(skill1Fx) ? skill1Fx : null;
                case 1: return HasFrames(skill2Fx) ? skill2Fx : null;
                default: return null;
            }
        }

        /// <summary>시전 모션 한 바퀴에 걸리는 시간(초). 없으면 0.</summary>
        public float SkillClipSeconds(int slot, bool facingRight)
        {
            Sprite[] frames = SkillMotion(slot, facingRight);
            if (!HasFrames(frames) || attackFramesPerSecond <= 0f) return 0f;
            return frames.Length / attackFramesPerSecond;
        }

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

        [Tooltip("★ 2026-08-19 신설 — <b>마법</b> 공격의 착탄 연출. 비워두면 위 " +
                 "impactFrames 를 그대로 쓴다(기존 스킨은 아무것도 안 바뀐다).\n\n" +
                 "<b>왜 갈랐나</b> — 엘린 시트가 적중 이펙트를 <b>두 벌</b>로 그렸다: " +
                 "원거리는 갈색 쇠사슬, 마법은 보라 쇠사슬. 한 칸으로는 한쪽을 버려야 한다")]
        public Sprite[] magicImpactFrames;

        /// <summary>
        /// 이 공격 유형에 쓸 착탄 연출. 마법이면 전용 칸이 먼저고, 없으면 공용 칸.
        /// </summary>
        public Sprite[] ImpactFor(TacticalAttackType attackType) =>
            attackType == TacticalAttackType.Magic && HasFrames(magicImpactFrames)
                ? magicImpactFrames
                : impactFrames;

        // ------------------------------------------------------------------
        // ★★ 투사체 없는 원거리·마법 (2026-08-19)
        //
        // 유저 지시: <i>"엘린의 마법/원거리 공격은 투사체 없이 적중대상 땅바닥에서 사슬이
        // 올라오는 걸로 해줘. 온힛 판정이니까 상관없을듯"</i> — 맞다, 피해는 예전부터
        // <b>발사 즉시 히트 스캔</b>이라 탄환은 순수 연출이다(<c>CombatProjectileFx</c> 맨 위).
        //
        // ⚠ <b><c>projectileFrames</c> 를 비우는 것만으로는 안 된다.</b> 비어 있으면
        //   <c>CombatProjectileFx.ArtFor</c> 가 <c>FallbackArt</c> 로 내려가 <b>진영 기본
        //   탄환</b>(회색 화살)을 띄운다 — "스킨에 탄환이 없는 유닛" 을 위한 폴백이라
        //   "탄환이 없어야 하는 유닛" 과 구분이 안 된다. 그래서 <b>의도</b>를 적는 칸이 필요하다.
        // ------------------------------------------------------------------

        // ------------------------------------------------------------------
        // ★★ 근접 공격에도 «날아가는» 연출 (2026-08-20)
        //
        // 유저 지시: <i>"라린길 돌진 이펙트를 투사체의 형태로 만들어서 날아가는 모습
        // 연출할 수 있으면 그렇게 해봐"</i>
        //
        // 라린길 원화 시트의 「3. 근거리 공격 이펙트」는 <b>날아가는 그림</b>이다 —
        // 첫 장이 발톱이 박힌 자리의 튄 피고, 뒤 세 장이 <b>초승달 참격이 앞으로 뻗는</b>
        // 모양이다. 그런데 그동안 <b>배선할 칸이 없어서</b> 뽑아만 두고 버려 뒀다
        // (115절: "「평타 이펙트」 칸이 없다").
        //
        // ⚠ <c>projectileFrames</c> 에 넣으면 안 된다 — 그 칸을 읽는
        //   <see cref="CombatProjectileFx.HandleAttack"/> 은 <b>원거리·마법에서만</b> 돌고,
        //   라린길은 표에서 <c>atk_type = Melee</c> 다. 공격 유형을 바꾸면 사거리·명중·치명
        //   판정이 전부 달라지므로(전투 규칙이 유형으로 갈린다) <b>연출만</b> 얹을 칸이 필요하다.
        //
        // ★ 피해 판정은 아무것도 안 바뀐다 — 근접 평타는 예전처럼 즉시 히트 스캔이다.
        //   이 그림은 «맞았다» 를 눈에 보이게만 한다.
        // ------------------------------------------------------------------

        [Header("근접 공격의 날아가는 연출 (2026-08-20)")]
        [Tooltip("★ 근접 평타가 성사될 때 <b>공격자 → 대상</b> 으로 날아가는 그림. " +
                 "라린길의 「근거리 공격 이펙트」(발톱 참격)가 첫 사용자다.\n\n" +
                 "비워두면 아무 일도 없다(예전 동작 그대로). 원거리·마법 유형에서는 " +
                 "이 칸을 <b>보지 않는다</b> — 그쪽은 projectileFrames 가 정본이다.\n" +
                 "그림은 <b>+X(오른쪽)를 향한 한 벌</b>만 두면 코드가 방향으로 돌린다")]
        public Sprite[] meleeTravelFrames;

        [Tooltip("그 연출을 가로 몇 타일로 그릴지. 0 이면 원화 크기 그대로.\n" +
                 "⚠ 근접은 거리가 짧으니 크게 잡으면 화면을 덮는다 — 1~2타일이 적당하다")]
        [Min(0f)] public float meleeTravelWidthTiles;

        /// <summary>근접 평타에 날아가는 연출이 있는지.</summary>
        public bool HasMeleeTravel => HasFrames(meleeTravelFrames);

        [Header("투사체를 쓰지 않는 원거리·마법 (2026-08-19)")]
        [Tooltip("★ 켜면 <b>날아가는 탄환과 발사 섬광을 통째로 건너뛰고</b> 착탄 연출만 " +
                 "대상 발밑에 바로 깐다. 엘린의 「쇠사슬 솟구침」이 첫 사용자다.\n\n" +
                 "⚠ 이 칸이 필요한 이유 — projectileFrames 를 비우기만 하면 " +
                 "CombatProjectileFx 가 <b>진영 기본 탄환</b>으로 떨어진다(스킨에 탄환이 " +
                 "없는 유닛용 폴백). 「그림이 없다」와 「탄환이 없어야 한다」는 다른 뜻이다.\n" +
                 "피해 판정은 아무것도 안 바뀐다 — 원래도 발사 즉시 히트 스캔이다")]
        public bool groundImpactOnly;

        [Tooltip("⚠ 구식(픽셀 기준) — 원화 크기에 맞춰 손으로 고른 배율이다. " +
                 "아래 projectileWidthTiles 가 0 일 때만 폴백으로 쓰인다")]
        [Min(0.05f)] public float projectileScale = 0.55f;

        [Tooltip("⚠ 구식(픽셀 기준) — impactWidthTiles 가 0 일 때만 폴백으로 쓰인다")]
        public Vector2 impactScale = Vector2.one;

        // ------------------------------------------------------------------
        // 크기 기준은 <b>타일</b>이다 (유저 확정 2026-08-13)
        //
        // 예전에는 크기를 <b>배율</b>(projectileScale 0.55 · 몬스터 spriteScale 0.75)로 적었다.
        // 그 숫자는 "원화가 몇 픽셀인지 / PPU 가 얼마인지"를 보고 손으로 고른 값이라,
        // <b>원화가 바뀌면 게임 안 크기가 같이 흔들린다.</b> 실제로 그래서 보스(단탈리온)가
        // 잡몹보다 작아졌다 — 원화 가로가 넓어 0.75 를 곱했더니 세로가 1.4타일이 됐다.
        //
        // 그래서 <b>"몇 타일로 보이고 싶은지"만 적고</b>, 배율은 아래 실측값으로 코드가 계산한다.
        // 실측값은 <c>Tools/measure_skin_tiles.py</c> 가 원화의 알파 경계를 재서 채운다 —
        // 손으로 적지 말 것.
        // ------------------------------------------------------------------

        [Header("실측 크기 (타일) — Tools/measure_skin_tiles.py 가 채운다. 손으로 고치지 말 것")]
        [Tooltip("스케일 1 일 때 대기(Idle) 원화가 실제로 차지하는 크기(타일). " +
                 "캔버스가 아니라 <b>알파 경계</b> 기준이라 여백이 큰 원화도 정확하다")]
        public Vector2 contentSizeTiles;

        [Tooltip("스케일 1 일 때 탄환 원화의 실제 크기(타일)")]
        public Vector2 projectileSizeTiles;

        [Tooltip("스케일 1 일 때 착탄 원화의 실제 크기(타일)")]
        public Vector2 impactSizeTiles;

        [Header("표시 크기 (타일 기준)")]
        [Tooltip("탄환을 가로 몇 타일로 그릴지. 0 이면 구식 projectileScale 을 쓴다")]
        [Min(0f)] public float projectileWidthTiles;

        [Tooltip("착탄 연출을 가로 몇 타일로 그릴지. 0 이면 구식 impactScale 을 쓴다.\n" +
                 "⚠ <b>마법 공격은 이 값을 안 쓴다</b> — 실제 피해 범위(UnitCombat.MagicAreaTiles)를 " +
                 "그대로 그려서 '보이는 범위 = 맞는 범위' 가 되게 한다")]
        [Min(0f)] public float impactWidthTiles;

        [Tooltip("착탄 연출을 바닥에 눕히는 세로 비율. 원화가 측면 시점(위로 솟는 폭발)이면 " +
                 "1 보다 작게 준다. 크기가 아니라 <b>시점 보정</b>이라 타일이 아닌 비율이다")]
        [Range(0.1f, 1f)] public float impactFlattenY = 1f;

        /// <summary>
        /// 이 외형을 <paramref name="heightTiles"/> 타일 높이로 그리기 위한 <b>균등</b> 배율.
        /// 실측값이 없거나(0) 목표가 0 이면 1 — 크기를 건드리지 않는다.
        /// </summary>
        public float ScaleForHeightTiles(float heightTiles) =>
            heightTiles > 0f && contentSizeTiles.y > 0.0001f ? heightTiles / contentSizeTiles.y : 1f;

        /// <summary>그 배율로 그렸을 때 화면에 보이는 크기(타일). 발판·근접 거리 판정이 이걸 읽는다.</summary>
        public Vector2 RenderedSizeTiles(float heightTiles)
        {
            float s = ScaleForHeightTiles(heightTiles);
            return new Vector2(contentSizeTiles.x * s, contentSizeTiles.y * s);
        }

        /// <summary>탄환에 곱할 배율. 타일 값이 있으면 그걸로, 없으면 구식 배율.</summary>
        public float ProjectileScale =>
            projectileWidthTiles > 0f && projectileSizeTiles.x > 0.0001f
                ? projectileWidthTiles / projectileSizeTiles.x
                : projectileScale;

        /// <summary>
        /// 착탄 연출에 곱할 배율. <paramref name="areaTiles"/> 가 0 보다 크면
        /// (마법의 실제 피해 범위) 그 크기에 맞추고, 아니면 스킨의 표시 크기를 쓴다.
        /// </summary>
        public Vector2 ImpactScaleFor(float areaTiles)
        {
            float wanted = areaTiles > 0f ? areaTiles : impactWidthTiles;
            if (wanted <= 0f || impactSizeTiles.x <= 0.0001f)
                return impactScale == Vector2.zero ? Vector2.one : impactScale;

            float s = wanted / impactSizeTiles.x;
            return new Vector2(s, s * Mathf.Clamp(impactFlattenY, 0.1f, 1f));
        }

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

            // ★ 마법 전용 모션 (2026-08-19). 비어 있으면 아래 원거리로 떨어진다 —
            //   그것이 이 칸이 생기기 전의 동작이라 기존 스킨은 아무것도 안 바뀐다.
            if (motion == SkinAttackMotion.Magic)
            {
                Sprite[] m = Pick(magicRight, magicLeft, facingRight);
                if (HasFrames(m)) return m;
            }

            // 회복·마법도 전용 모션이 없으면 원거리 동작으로 대체한다 (붙어서 휘두르는 동작이 아니다).
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
