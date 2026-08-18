using UnityEngine;
using UnityEngine.Tilemaps;
using LastSanctuary.Map;

namespace LastSanctuary.Units
{
    /// <summary>
    /// <b>넥서스 둘레에 「성역」을 깐다</b> (2026-08-18, 유저 지시).
    ///
    /// <i>"넥서스 주변도 중립 몬스터 청크 처럼 일정 범위의 청크 넣어서 생성(불규칙한 저그 점막처럼) ·
    /// 청크 에셋은 볼트 리소스에 있어 · 청크 색이나 배열은 너가 알아서 적절하게 수정해줘
    /// 좀 확실하게 다른 공간이랑 분리되어서 보이게"</i>
    ///
    /// ★★ <b>모양 만드는 코드를 새로 안 짰다</b>
    /// ---------------------------------------
    /// 지시가 <i>"중립 몬스터 청크 처럼"</i> 이므로, 카르시노스 서식지가 쓰는
    /// <see cref="NeutralHabitat"/> 를 <b>그대로 붙여서</b> 쓴다 — 노이즈로 경계를 흔들고,
    /// 셀룰러 오토마타로 다듬고, 중심과 이어진 덩어리만 남기는 그 레시피가 곧
    /// "불규칙한 저그 점막" 이다. 같은 것을 두 벌 만들지 않는다.
    ///
    /// ⚠ 그 컴포넌트 이름이 <c>Neutral</c> 로 시작하지만 <b>중립 전용 코드가 한 줄도 없다</b>
    ///   (타일맵과 좌표만 다룬다). 이름을 바꾸지 <b>않는</b> 것은 씬 YAML 이 클래스 이름으로
    ///   컴포넌트를 찾기 때문이다 — 바꾸면 카르시노스 쪽 배선이 끊긴다.
    ///
    /// ★ <b>서식지와 다른 점 — 되돌리지 않는다</b>
    /// 서식지는 개체가 죽으면 걷힌다. 성역은 <b>넥서스가 죽으면 게임이 끝나므로</b>
    /// 되돌릴 이유가 없다. <see cref="NeutralHabitat"/> 의 <c>restoreOnDestroy</c> 는
    /// 인스펙터 값이고 기본이 켜짐인데, 넥서스는 패배할 때까지 안 사라지니 실질적으로
    /// 차이가 없다 — 그래서 그 값을 건드리지 않는다.
    ///
    /// ⚠ <b>플레이 모드에서만 그려진다.</b> 타일맵 변경은 플레이를 나가면 사라진다 —
    ///   서식지와 같고, <b>그게 의도다</b>(씬 파일이 더러워지지 않는다).
    /// </summary>
    [RequireComponent(typeof(Nexus))]
    [DisallowMultipleComponent]
    public class NexusSanctuary : MonoBehaviour
    {
        [Header("범위")]
        [Tooltip("★ <b>성역의 지름</b>(타일). 유저 확정 2026-08-18: <b>15</b> " +
                 "(처음 지시는 10 이었고, 눈으로 보고 15 로 올렸다). " +
                 "<b>왜 반지름이 아니라 지름인가</b> — 지시가 「지름 10의 원 범위」 였다. " +
                 "반지름 칸에 지름 값을 넣으면 <b>지시의 두 배</b>가 된다. " +
                 "값의 뜻을 칸 이름이 말하게 해서 그 착각이 생길 자리를 없앤다. " +
                 "⚠ 1차(2026-08-18)에는 <b>반지름 20 = 지름 40</b> 이었다. 유저 리포트: " +
                 "「중앙건물 청크 구역이 너무 큼」 — 맵이 320타일이지만 화면에 보이는 범위에 " +
                 "비하면 지름 40 은 시야를 거의 채운다. 지름 15 면 넥서스 발판(3x3)을 " +
                 "여유 있게 두르는 정도다")]
        [Min(2f)] [SerializeField] float diameterTiles = 15f;

        [Tooltip("★ <b>생성할 때마다 지름을 이 비율만큼 흔든다</b> (유저 지시: " +
                 "「해당 원 범위와 비슷한 범위내에서 생성 시마다 불규칙」). " +
                 "0.2 면 지름이 매 게임 <b>−20% ~ +20%</b> 안에서 뽑힌다(지름 15 → 12~18). " +
                 "0 이면 언제나 위 지름 그대로다. " +
                 "⚠ <b>모양</b>의 불규칙함은 이 값과 별개다 — 그쪽은 NeutralHabitat 의 " +
                 "노이즈(Edge Wobble)가 만들고, 씨앗이 매 게임 달라 이미 매번 다른 덩어리가 " +
                 "나온다. 이 칸은 거기에 <b>크기의 편차</b>를 더한다")]
        [Range(0f, 0.5f)] [SerializeField] float diameterJitter = 0.2f;

        [Header("타일 묶음 (Resources/HabitatTiles/ 아래)")]
        [Tooltip("바닥 타일 폴더 이름. Tools/gen_sanctuary_tiles.py 가 굽는다.\n" +
                 "가장자리·데코는 이 이름에 Edge/Props 를 붙여 찾는다 — 서식지와 같은 규약")]
        [SerializeField] string tileSetName = "Sanctuary";

        [Header("씨앗")]
        [Tooltip("★ 0 이면 <b>매 게임 다른 모양</b>이 나온다(실행할 때마다 새로 뽑는다).\n" +
                 "0 이 아니면 그 값을 고정으로 써서 <b>항상 같은 모양</b>이 된다 — " +
                 "모양을 눈으로 비교하며 값을 조정할 때 쓴다")]
        [SerializeField] int fixedSeed;

        [Header("디버그")]
        [SerializeField] bool logSanctuary = true;

        void Start()
        {
            var map = FindAnyObjectByType<MapGenerator>();
            if (map == null) return;

            // 바닥이 없으면 아무것도 안 그린다. 가장자리·데코는 없어도 된다
            // (없으면 각각 바닥 타일로 대체 / 데코 생략 — NeutralHabitat.Paint 참조).
            TileBase[] ground = Load(tileSetName, required: true);
            if (ground == null || ground.Length == 0) return;

            TileBase[] edge = Load(tileSetName + "Edge", required: false);
            TileBase[] props = Load(tileSetName + "Props", required: false);

            var habitat = GetComponent<NeutralHabitat>();
            if (habitat == null) habitat = gameObject.AddComponent<NeutralHabitat>();

            int seed = fixedSeed != 0 ? fixedSeed : Random.Range(int.MinValue, int.MaxValue);
            Vector3Int center = map.WorldToCell(transform.position);

            // ★ 크기도 <b>씨앗에서</b> 뽑는다 — 모양과 같은 씨앗을 쓰므로 씨앗을 고정하면
            //   크기까지 함께 재현된다(fixedSeed 로 모양을 눈으로 비교할 때 필요하다).
            float diameter = RollDiameter(seed);
            habitat.Paint(map, ground, edge, props, center, diameter * 0.5f, seed);

            if (logSanctuary)
                Debug.Log($"[성역] 넥서스 둘레 {habitat.PaintedCells}칸 · 데코 {habitat.PropCells}개 " +
                          $"(지름 {diameter:0.##}타일 — 기준 {diameterTiles} ±{diameterJitter:P0} · " +
                          $"바닥 {ground.Length}종 · " +
                          $"가장자리 {(edge != null ? edge.Length : 0)}종 · " +
                          $"데코 {(props != null ? props.Length : 0)}종 · 씨앗 {seed})", this);
        }

        /// <summary>
        /// 이번 게임의 지름. 기준 지름을 <see cref="diameterJitter"/> 비율 안에서 흔든다.
        ///
        /// ⚠ <b>씨앗에서 뽑는다</b>(<c>System.Random</c>) — <c>UnityEngine.Random</c> 을 쓰면
        ///   씨앗을 고정해도 크기만 매번 달라져 "같은 씨앗 = 같은 성역" 이 깨진다.
        ///
        /// ⚠ <b>씨앗을 그대로 쓰지 않고 한 번 비튼다</b>(<c>seed ^ 0x5F3759DF</c>) —
        ///   <c>NeutralHabitat</c> 의 마스크 생성이 <b>같은 씨앗의 첫 두 난수</b>를 노이즈 위치로
        ///   쓴다. 여기서 첫 난수를 크기로 쓰면 크기와 노이즈 위치가 한 값에 묶여, 지름이
        ///   클 때마다 <b>같은 방향으로</b> 울퉁불퉁해진다.
        /// </summary>
        float RollDiameter(int seed)
        {
            float baseDiameter = Mathf.Max(2f, diameterTiles);
            if (diameterJitter <= 0f) return baseDiameter;

            var rng = new System.Random(seed ^ 0x5F3759DF);
            float t = (float)rng.NextDouble() * 2f - 1f;         // −1~1
            return Mathf.Max(2f, baseDiameter * (1f + t * diameterJitter));
        }

        /// <summary>타일 폴더 하나를 통째로 읽는다. 없으면 null.</summary>
        TileBase[] Load(string folder, bool required)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;

            TileBase[] tiles = Resources.LoadAll<TileBase>("HabitatTiles/" + folder);
            if ((tiles == null || tiles.Length == 0) && required)
                Debug.LogWarning($"[성역] 'Resources/HabitatTiles/{folder}' 에 타일이 없습니다 — " +
                                 "Tools/gen_sanctuary_tiles.py 를 돌려주세요.", this);
            return tiles;
        }
    }
}
