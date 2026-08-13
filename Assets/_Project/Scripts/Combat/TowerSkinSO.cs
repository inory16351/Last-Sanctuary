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

        /// <summary>
        /// 이 건물이 쏘는 탄환. <see cref="CharacterSkinSO.projectileFrames"/> 와 같은 규칙이다 —
        /// <b>+X 를 향한 그림 한 벌</b>만 넣으면 <see cref="CombatProjectileFx"/> 가 발사 방향으로
        /// 돌려서 그린다. 포탑 레이저는 원화에 <b>아래-오른쪽 고정</b>으로 구워져 있어서
        /// 오려내 여기로 넘긴 것이다(진행상황 27-11절).
        /// </summary>
        [Header("투사체 (공격 시 날아가는 탄환 — +X 를 향한 그림 한 벌)")]
        public Sprite[] projectileFrames;

        [Tooltip("발사 순간 포구에서 터지는 섬광. 쏘는 쪽에서 반짝인다. " +
                 "비워두면 섬광 없이 탄환만 날아간다")]
        public Sprite[] muzzleFlashFrames;

        [Tooltip("탄환이 닿는 순간 맞는 쪽에 재생한다. 비워두면 착탄 연출이 없다. " +
                 "규칙은 CharacterSkinSO.impactFrames 와 같다")]
        public Sprite[] impactFrames;

        [Tooltip("⚠ 구식(픽셀 기준) — projectileWidthTiles 가 0 일 때만 폴백으로 쓰인다")]
        [Min(0.05f)] public float projectileScale = 0.85f;

        [Tooltip("⚠ 구식(픽셀 기준) — impactWidthTiles 가 0 일 때만 폴백으로 쓰인다")]
        public Vector2 impactScale = Vector2.one;

        // 크기 기준은 타일이다 — 이유와 규칙은 <see cref="CharacterSkinSO"/> 의 같은 절 참조.
        // 실측값은 Tools/measure_skin_tiles.py 가 원화의 알파 경계를 재서 채운다.

        [Header("실측 크기 (타일) — Tools/measure_skin_tiles.py 가 채운다. 손으로 고치지 말 것")]
        [Tooltip("스케일 1 일 때 대기(Idle) 원화가 실제로 차지하는 크기(타일)")]
        public Vector2 contentSizeTiles;

        [Tooltip("스케일 1 일 때 탄환 원화의 실제 크기(타일)")]
        public Vector2 projectileSizeTiles;

        [Tooltip("스케일 1 일 때 착탄 원화의 실제 크기(타일)")]
        public Vector2 impactSizeTiles;

        [Header("표시 크기 (타일 기준)")]
        [Tooltip("탄환을 가로 몇 타일로 그릴지. 0 이면 구식 projectileScale 을 쓴다")]
        [Min(0f)] public float projectileWidthTiles;

        [Tooltip("착탄 연출을 가로 몇 타일로 그릴지. 0 이면 구식 impactScale 을 쓴다.\n" +
                 "⚠ 범위 공격(Splash)이면 이 값 대신 실제 피해 범위를 그린다")]
        [Min(0f)] public float impactWidthTiles;

        [Tooltip("착탄 연출을 바닥에 눕히는 세로 비율(시점 보정이라 타일이 아닌 비율이다)")]
        [Range(0.1f, 1f)] public float impactFlattenY = 1f;

        /// <summary>이 외형을 <paramref name="heightTiles"/> 타일 높이로 그리기 위한 균등 배율.</summary>
        public float ScaleForHeightTiles(float heightTiles) =>
            heightTiles > 0f && contentSizeTiles.y > 0.0001f ? heightTiles / contentSizeTiles.y : 1f;

        /// <summary>탄환에 곱할 배율. 타일 값이 있으면 그걸로, 없으면 구식 배율.</summary>
        public float ProjectileScale =>
            projectileWidthTiles > 0f && projectileSizeTiles.x > 0.0001f
                ? projectileWidthTiles / projectileSizeTiles.x
                : projectileScale;

        /// <summary>착탄 연출 배율. <paramref name="areaTiles"/> 가 0 보다 크면 그 범위에 맞춘다.</summary>
        public Vector2 ImpactScaleFor(float areaTiles)
        {
            float wanted = areaTiles > 0f ? areaTiles : impactWidthTiles;
            if (wanted <= 0f || impactSizeTiles.x <= 0.0001f)
                return impactScale == Vector2.zero ? Vector2.one : impactScale;

            float s = wanted / impactSizeTiles.x;
            return new Vector2(s, s * Mathf.Clamp(impactFlattenY, 0.1f, 1f));
        }

        /// <summary>이 스킨이 자기 탄환을 들고 있는지.</summary>
        public bool HasProjectile => Has(projectileFrames);

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
