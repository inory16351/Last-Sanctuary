using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Fog
{
    /// <summary>
    /// 전장의 안개를 걷어내는 시야원. 이 컴포넌트가 붙은 오브젝트 주변이 밝혀진다.
    /// 캐릭터 템플릿·넥서스 템플릿에 붙여두면 복제되는 모든 유닛이 물려받는다.
    ///
    /// 시야는 "한 변 몇 타일"로 지정한다. 5 면 5x5 칸(반경 2.5타일)이 보인다.
    /// </summary>
    public class VisionSource : MonoBehaviour
    {
        [Tooltip("시야 범위를 한 변의 타일 수로 지정한다.\n" +
                 "5 = 5x5 칸, 10 = 10x10 칸. 반경은 이 값의 절반")]
        [Min(1f)] [SerializeField] float visionTiles = 5f;

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

        void OnEnable() => _sources.Add(this);
        void OnDisable() => _sources.Remove(this);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.95f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, VisionRadius);
        }
    }
}
