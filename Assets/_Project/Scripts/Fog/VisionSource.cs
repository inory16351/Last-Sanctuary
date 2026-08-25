using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Fog
{
    /// <summary>
    /// 전장의 안개를 걷어내는 시야원. 이 컴포넌트가 붙은 오브젝트 주변이 밝혀진다.
    /// 캐릭터 템플릿·성역 템플릿에 붙여두면 복제되는 모든 유닛이 물려받는다.
    ///
    /// 시야는 "한 변 몇 타일"로 지정한다. 5 면 5x5 칸(반경 2.5타일)이 보인다.
    /// </summary>
    public class VisionSource : MonoBehaviour
    {
        [Tooltip("시야 범위를 한 변의 타일 수로 지정한다.\n" +
                 "5 = 5x5 칸, 10 = 10x10 칸. 반경은 이 값의 절반")]
        [Min(1f)] [SerializeField] float visionTiles = 5f;

        [Tooltip("0 이 아니면 원형 대신 이 크기(타일)의 <b>사각형</b>으로 밝힌다.\n" +
                 "그림만 딱 덮어야 하는 경우에 쓴다 — 원형은 모서리까지 닿는 반경이 필요해서 " +
                 "같은 그림을 덮으려면 넓이가 3~4배가 된다")]
        [SerializeField] Vector2 visionBoxTiles = Vector2.zero;

        [Tooltip("사각 시야의 중심을 오브젝트 위치에서 얼마나 옮길지(타일). " +
                 "캐릭터는 피벗이 발밑이라 그림이 위로만 솟으므로 y 에 높이의 절반을 넣는다")]
        [SerializeField] Vector2 visionBoxOffsetTiles = Vector2.zero;

        static readonly List<VisionSource> _sources = new List<VisionSource>(64);

        public static IReadOnlyList<VisionSource> All => _sources;

        /// <summary>플레이 모드를 다시 시작할 때 정적 목록이 남지 않게 초기화.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _sources.Clear();

        /// <summary>시야 한 변의 타일 수.</summary>
        public float VisionTiles => visionTiles;

        /// <summary>시야 반경(타일).</summary>
        public float VisionRadius => visionTiles * 0.5f;

        /// <summary>런타임에 시야를 바꾼다 (성장·버프 등).</summary>
        public void SetVision(float tiles) => visionTiles = Mathf.Max(1f, tiles);

        /// <summary>
        /// ★ <b>사각 시야를 쓰는지.</b> 켜져 있으면 <see cref="FogOfWarService"/> 가
        /// 원형(<see cref="VisionRadius"/>) 대신 이 상자만 밝힌다.
        ///
        /// <b>왜 원형만으로는 부족한가</b> — 그림을 딱 덮으려면 원의 반경이 그림의
        /// <b>모서리</b>까지 닿아야 하는데, 그러면 밝히는 넓이가 그림의 3~4배가 된다
        /// (엘린 실측: 그림 5.6타일² vs 반경 2.52 원 19.9타일²).
        /// 「타고난 섬세함」처럼 <b>"자기 그림만"</b> 이 요구사항이면 사각형이어야 맞다.
        /// </summary>
        public bool UsesBox => visionBoxTiles.x > 0.01f && visionBoxTiles.y > 0.01f;

        /// <summary>사각 시야의 크기(타일).</summary>
        public Vector2 VisionBoxTiles => visionBoxTiles;

        /// <summary>사각 시야 중심의 오브젝트 기준 오프셋(타일).</summary>
        public Vector2 VisionBoxOffsetTiles => visionBoxOffsetTiles;

        /// <summary>사각 시야를 지정한다. 크기를 0 으로 주면 원형으로 되돌아간다.</summary>
        public void SetVisionBox(Vector2 sizeTiles, Vector2 offsetTiles)
        {
            visionBoxTiles = new Vector2(Mathf.Max(0f, sizeTiles.x), Mathf.Max(0f, sizeTiles.y));
            visionBoxOffsetTiles = offsetTiles;
        }

        void OnEnable() => _sources.Add(this);
        void OnDisable() => _sources.Remove(this);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.95f, 0.6f, 0.5f);

            if (UsesBox)
            {
                Gizmos.DrawWireCube(transform.position + (Vector3)visionBoxOffsetTiles,
                                    new Vector3(visionBoxTiles.x, visionBoxTiles.y, 0f));
                return;
            }

            Gizmos.DrawWireSphere(transform.position, VisionRadius);
        }
    }
}
