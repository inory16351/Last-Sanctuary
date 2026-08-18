using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using LastSanctuary.Map;

namespace LastSanctuary.Units
{
    /// <summary>
    /// <b>에픽 중립 몬스터의 서식지를 바닥에 그린다</b> (2026-08-15, 유저 지시).
    ///
    /// <i>"서식지 제너레이터 맵과 동일한 방식으로 만들어서 매 게임 시작 카르시노스가 소환 될때마다
    /// 새로운 서식지 타일 에셋들이 섞여서 서식지 디자인이 매 게임마다 조금씩 달라지도록.
    /// 서식지의 크기가 조금 불규칙하게 형성되어서 완전한 원형으로 안 보이는게 더 자연스러울 거 같아."</i>
    ///
    /// ★ <b>맵 생성기와 같은 레시피</b>
    /// -------------------------------
    /// <see cref="MapGenerator.BuildObstacleMask"/> 이 쓰는 것과 <b>같은 순서·같은 판정</b>이다:
    /// <code>
    ///   ① Perlin 노이즈를 월드 좌표로 샘플 (시드로 샘플 위치를 옮긴다)
    ///   ② 임계값으로 채울 칸 결정  ← 여기만 다르다: 맵은 백분위, 여기는 「중심에서의 거리 + 노이즈」
    ///   ③ 셀룰러 오토마타로 다듬기 (주변 8칸 중 5개 이상이면 채움, 3개 이하면 비움)
    /// </code>
    /// ②에서 <b>거리를 노이즈로 흔드는 것</b>이 "완전한 원형으로 안 보이게" 의 실체다 —
    /// 반지름을 칸마다 ±<see cref="edgeWobble"/> 만큼 밀고 당기므로 경계가 울퉁불퉁해진다.
    /// ③이 그 울퉁불퉁함에서 <b>한 칸짜리 뾰족한 돌기와 구멍</b>을 없애 덩어리로 만든다.
    /// 마지막으로 <b>중심과 이어진 덩어리만</b> 남겨 멀리 떨어진 섬은 버린다.
    ///
    /// ★ <b>매 게임 달라지는 이유</b> — 시드를 스포너의 난수(<c>_rng</c>)에서 받는다.
    /// 그 난수는 게임을 켤 때마다 새 씨앗으로 시작하므로, 같은 자리에 소환돼도 모양이 다르다.
    /// 타일 고르기도 같은 시드의 난수라 <b>어느 칸에 어느 타일이 깔리는지</b>까지 달라진다.
    ///
    /// ★ <b>어디에 그리나 — 두 층을 쓴다</b> (2026-08-16 개정)
    /// <code>
    ///   Ground 타일맵 : 서식지 바닥 (안쪽은 바닥 타일 · 테두리 한 칸은 가장자리 타일)
    ///   Deco   타일맵 : 그 위에 드문드문 얹는 데코 (종양 군집·촉수·뼈·포자)
    /// </code>
    /// 처음에는 바닥까지 Deco 에 덮었는데(2026-08-15), 그러면 <b>데코를 얹을 층이 남지 않는다.</b>
    /// 유저 지시가 <i>"서식지의 색만 바꾸지 말고 데코도 추가하거나 더 만들어서 더 색다르게"</i>
    /// 라 층을 하나 더 썼다.
    ///
    /// ⚠ <b>두 층 모두 원래 타일을 기억했다가 되돌린다</b> — 개체가 죽으면 지형이 원상복구된다.
    /// Ground 를 건드리는 것이 더 위험해 보이지만, 어차피 <b>플레이 모드에서만</b> 일어나고
    /// (타일맵 변경은 플레이 모드를 나가면 사라진다 — 씬 파일이 더러워지지 않는다)
    /// 되돌리는 코드도 Deco 와 완전히 같다.
    ///
    /// 벽칸(장애물)은 건드리지 않는다 — 서식지는 바닥 장식이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NeutralHabitat : MonoBehaviour
    {
        [Header("모양")]
        [Tooltip("경계를 이만큼(반지름 대비 비율) 밀고 당긴다. 0 이면 정확한 원, " +
                 "클수록 울퉁불퉁해진다. 0.35 면 반지름이 칸마다 ±35% 로 흔들린다")]
        [Range(0f, 0.8f)] [SerializeField] float edgeWobble = 0.32f;

        [Tooltip("노이즈 배율. 작을수록 큰 덩어리로 뭉치고, 클수록 잘게 부서진다. " +
                 "맵 생성기의 noiseScaleRange(0.08~0.16)와 같은 대역")]
        [Min(0.01f)] [SerializeField] float noiseScale = 0.11f;

        [Tooltip("셀룰러 오토마타 다듬기 횟수. 맵 생성기의 smoothPassesRange(2~4)와 같은 대역. " +
                 "많을수록 매끈해지고 적을수록 거칠다")]
        [Range(0, 6)] [SerializeField] int smoothPasses = 3;

        [Tooltip("서식지 안쪽 이 비율까지는 노이즈와 무관하게 <b>반드시</b> 채운다. " +
                 "가운데가 뚫려 보이지 않게 하는 안전장치")]
        [Range(0f, 1f)] [SerializeField] float solidCoreRatio = 0.45f;

        [Header("데코")]
        [Tooltip("서식지 안쪽 칸에 데코를 얹을 확률. 너무 높으면 바닥이 안 보이고 " +
                 "너무 낮으면 색만 바꾼 것처럼 보인다")]
        [Range(0f, 0.6f)] [SerializeField] float propChance = 0.16f;

        [Header("되돌리기")]
        [Tooltip("개체가 죽거나 사라질 때 원래 타일로 되돌린다(바닥·데코 둘 다). " +
                 "끄면 서식지가 맵에 남는다(잡은 흔적이 남는 연출)")]
        [SerializeField] bool restoreOnDestroy = true;

        [Header("사라지는 연출 (유저 지시 2026-08-18)")]
        [Tooltip("★ <b>가장 바깥 칸이 사라지기 시작해서 중심 칸이 시작할 때까지</b> 걸리는 시간(초). " +
                 "저그 점막이 걷히듯 경계가 안쪽으로 오므라든다. 0 이면 예전처럼 즉시 사라진다")]
        [Min(0f)] [SerializeField] float fadeSpreadSeconds = 6f;

        [Tooltip("<b>칸 하나</b>가 완전히 투명해지는 데 걸리는 시간(초). " +
                 "이 값이 곧 걷혀 나가는 <b>띠의 두께</b>다 — 크면 넓게 흐려지며 사라지고 " +
                 "작으면 선명한 경계가 지나간다. 전체 길이 = 위 값 + 이 값")]
        [Min(0.05f)] [SerializeField] float fadeCellSeconds = 1.5f;

        /// <summary>그린 칸 하나 — <b>원래 타일과 원래 색</b>을 같이 기억한다.</summary>
        public struct PaintedCell
        {
            public Vector3Int Cell;
            public TileBase Original;

            /// <summary>
            /// 원래 색. ⚠ 타일맵의 색은 <b>타일이 아니라 칸</b>에 붙는다 — 연출이 알파를 낮춘 뒤
            /// 이 값으로 되돌리지 않으면 <b>새로 깔린 지형까지 투명한 채로</b> 남는다.
            /// </summary>
            public Color OriginalColor;
        }

        readonly List<PaintedCell> _paintedGround = new List<PaintedCell>();
        readonly List<PaintedCell> _paintedDeco = new List<PaintedCell>();

        Tilemap _ground;
        Tilemap _deco;

        /// <summary>서식지 중심 칸. 연출이 "먼 곳부터" 를 재는 기준점이다.</summary>
        Vector3Int _centerCell;

        /// <summary>지금 이 서식지가 차지한 칸 수. 로그·디버그용.</summary>
        public int PaintedCells => _paintedGround.Count;

        /// <summary>얹은 데코 수. 로그·디버그용.</summary>
        public int PropCells => _paintedDeco.Count;

        /// <summary>
        /// 서식지를 그린다. 이미 그려져 있으면 먼저 지운다(같은 개체를 다시 배치하는 경우).
        /// </summary>
        /// <param name="map">타일맵 주인. 없으면 아무것도 하지 않는다.</param>
        /// <param name="ground">안쪽 바닥 타일 후보. 비어 있으면 아무것도 하지 않는다.</param>
        /// <param name="edge">테두리 한 칸에 쓸 타일 후보. 비어 있으면 바닥 타일로 대체한다.</param>
        /// <param name="props">바닥 위에 얹을 데코 후보. 비어 있으면 데코를 안 얹는다.</param>
        /// <param name="centerCell">서식지 중심 칸 (보통 개체가 태어난 자리).</param>
        /// <param name="radiusTiles">서식지 반지름(타일). 정의의 habitatRadiusTiles 를 쓴다.</param>
        /// <param name="seed">모양과 타일 배치를 정하는 씨앗. 게임마다 달라야 한다.</param>
        public void Paint(MapGenerator map, TileBase[] ground, TileBase[] edge, TileBase[] props,
                          Vector3Int centerCell, float radiusTiles, int seed)
        {
            Restore();

            if (map == null || ground == null || ground.Length == 0 || radiusTiles <= 0.5f) return;

            _ground = map.GroundTilemap;
            _deco = map.DecoTilemap;
            if (_ground == null) return;

            _centerCell = centerCell;      // 사라지는 연출이 "먼 곳부터" 를 재는 기준점

            bool[] mask = BuildMask(centerCell, radiusTiles, seed, out int w, out int h,
                                    out Vector3Int min);
            if (mask == null) return;

            bool hasEdge = edge != null && edge.Length > 0;
            bool hasProps = props != null && props.Length > 0 && _deco != null;
            var rng = new System.Random(seed);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!mask[x + y * w]) continue;

                    var cell = new Vector3Int(min.x + x, min.y + y, 0);

                    // 맵 밖 · 벽칸은 건드리지 않는다 — 서식지는 바닥 장식이다.
                    if (!map.IsCellInsideMap(cell) || map.IsCellBlocked(cell)) continue;

                    // ── 바닥 ────────────────────────────────────────────
                    bool onEdge = IsBoundary(mask, w, h, x, y);
                    TileBase[] set = onEdge && hasEdge ? edge : ground;

                    _paintedGround.Add(new PaintedCell
                    {
                        Cell = cell,
                        Original = _ground.GetTile(cell),
                        OriginalColor = _ground.GetColor(cell),
                    });
                    _ground.SetTile(cell, set[rng.Next(set.Length)]);

                    // ── 데코 ────────────────────────────────────────────
                    // ⚠ 테두리에는 안 얹는다 — 경계가 잦아드는 인상을 데코가 도로 진하게 만든다.
                    if (!hasProps || onEdge || rng.NextDouble() >= propChance) continue;

                    _paintedDeco.Add(new PaintedCell
                    {
                        Cell = cell,
                        Original = _deco.GetTile(cell),
                        OriginalColor = _deco.GetColor(cell),
                    });
                    _deco.SetTile(cell, props[rng.Next(props.Length)]);
                }
            }
        }

        /// <summary>
        /// 이 칸이 서식지 <b>테두리</b>인가 — 4방향 중 하나라도 서식지 밖이면 테두리다.
        /// (대각선까지 보면 거의 모든 칸이 테두리가 되어 가장자리 띠가 두꺼워진다.)
        /// </summary>
        static bool IsBoundary(bool[] mask, int w, int h, int x, int y)
        {
            return Out(mask, w, h, x - 1, y) || Out(mask, w, h, x + 1, y)
                || Out(mask, w, h, x, y - 1) || Out(mask, w, h, x, y + 1);
        }

        static bool Out(bool[] mask, int w, int h, int x, int y) =>
            x < 0 || y < 0 || x >= w || y >= h || !mask[x + y * w];

        /// <summary>
        /// 그려둔 칸을 <b>즉시</b> 원래 타일로 되돌린다 (바닥·데코 둘 다).
        /// 서식지를 다시 그릴 때(<see cref="Paint"/> 첫 줄)와, 연출을 끈 경우에 쓴다.
        /// </summary>
        public void Restore()
        {
            RestoreAll(_ground, _paintedGround);
            RestoreAll(_deco, _paintedDeco);
        }

        static void RestoreAll(Tilemap map, List<PaintedCell> cells)
        {
            if (map != null)
                for (int i = 0; i < cells.Count; i++)
                {
                    map.SetTile(cells[i].Cell, cells[i].Original);
                    // 색도 되돌린다 — 연출 도중에 불릴 수 있어 알파가 낮아진 칸이 있을 수 있다.
                    map.SetTileFlags(cells[i].Cell, TileFlags.None);
                    map.SetColor(cells[i].Cell, cells[i].OriginalColor);
                }
            cells.Clear();
        }

        /// <summary>
        /// ★ <b>바깥에서 안쪽으로 서서히 걷힌다</b> (유저 지시 2026-08-18:
        /// <i>"저그 해처리 점막 없어지는 거처럼"</i>).
        ///
        /// ⚠ <b>연출은 이 컴포넌트에서 돌릴 수 없다</b> — 이 컴포넌트는 몬스터에 붙어 있고
        /// 몬스터는 죽는 즉시 <c>Destroy</c> 된다. 파괴되는 오브젝트의 <c>Update</c>·코루틴은
        /// 그 프레임에 멈추므로 첫 프레임만 보이고 끊긴다. 그래서 <see cref="HabitatFadeOut"/>
        /// 이라는 <b>독립 오브젝트</b>에 칸 목록을 통째로 넘기고 이쪽은 손을 뗀다.
        ///
        /// 넘긴 뒤 목록을 비우는 것이 중요하다 — 안 비우면 곧이어 도는 <see cref="OnDestroy"/> 가
        /// <b>같은 칸을 즉시 되돌려</b> 연출이 시작하자마자 사라진다.
        /// </summary>
        public void FadeOutAndRestore()
        {
            if (fadeSpreadSeconds <= 0f && fadeCellSeconds <= 0f) { Restore(); return; }

            HabitatFadeOut.Begin(_ground, _deco, _paintedGround, _paintedDeco,
                                 _centerCell, fadeSpreadSeconds, fadeCellSeconds);

            _paintedGround.Clear();
            _paintedDeco.Clear();
        }

        /// <summary>
        /// 씬을 떠나거나 플레이 모드를 끝낼 때는 <b>연출을 시작하지 않는다</b> —
        /// 그 순간 새 오브젝트를 만들면 유니티가 경고를 내고, 어차피 타일맵 변경은
        /// 플레이 모드를 나가면 통째로 사라진다.
        /// </summary>
        static bool _quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _quitting = false;

        void OnApplicationQuit() => _quitting = true;

        void OnDestroy()
        {
            if (!restoreOnDestroy) return;

            if (_quitting || !Application.isPlaying || !gameObject.scene.isLoaded) { Restore(); return; }

            FadeOutAndRestore();
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 서식지 모양 마스크. 맨 위 주석의 ①~③ 과 「중심과 이어진 덩어리만 남기기」까지.
        /// 좌표는 <paramref name="min"/> 을 원점으로 한 로컬 격자다.
        /// </summary>
        bool[] BuildMask(Vector3Int center, float radius, int seed,
                         out int w, out int h, out Vector3Int min)
        {
            // 경계가 바깥으로 밀릴 수 있으므로 여유를 두고 상자를 잡는다.
            int reach = Mathf.CeilToInt(radius * (1f + edgeWobble)) + 2;
            w = h = reach * 2 + 1;
            min = new Vector3Int(center.x - reach, center.y - reach, 0);

            // ① 노이즈 — 시드로 샘플 위치를 옮긴다(맵 생성기와 같은 방법).
            var offRng = new System.Random(seed);
            float offX = (float)offRng.NextDouble() * 10000f;
            float offY = (float)offRng.NextDouble() * 10000f;

            var mask = new bool[w * h];
            float core = Mathf.Clamp01(solidCoreRatio);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int cx = min.x + x, cy = min.y + y;
                    float d = Mathf.Sqrt((cx - center.x) * (cx - center.x) +
                                         (cy - center.y) * (cy - center.y)) / radius;

                    if (d <= core) { mask[x + y * w] = true; continue; }

                    // ② 거리에 노이즈를 더해 경계를 흔든다. n 은 -1~1 로 옮겨 쓴다.
                    float n = Mathf.PerlinNoise((cx + offX) * noiseScale,
                                                (cy + offY) * noiseScale) * 2f - 1f;
                    mask[x + y * w] = d + n * edgeWobble < 1f;
                }
            }

            // ③ 다듬기 — 한 칸짜리 돌기·구멍을 없앤다.
            for (int pass = 0; pass < smoothPasses; pass++) mask = SmoothOnce(mask, w, h);

            KeepCenterBlob(mask, w, h, reach, reach);
            return mask;
        }

        /// <summary>
        /// 주변 8칸 중 채워진 칸이 5개 이상이면 채우고 3개 이하면 비운다.
        /// <see cref="MapGenerator"/> 의 같은 이름 함수와 규칙이 같다 — 다만
        /// <b>상자 밖은 「비어 있음」으로 본다</b>(맵은 벽으로 봤다). 서식지는 상자 가장자리에
        /// 닿으면 안 되는 물건이라, 밖을 채워진 것으로 보면 경계가 상자에 붙어 잘린다.
        /// </summary>
        static bool[] SmoothOnce(bool[] src, int w, int h)
        {
            var dst = new bool[src.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (src[nx + ny * w]) n++;
                        }

                    int i = x + y * w;
                    if (n >= 5)      dst[i] = true;
                    else if (n <= 3) dst[i] = false;
                    else             dst[i] = src[i];
                }
            }
            return dst;
        }

        /// <summary>
        /// 중심과 이어진 덩어리만 남긴다. 노이즈가 만든 <b>동떨어진 섬</b>을 버리는 것 —
        /// 서식지가 여러 조각으로 흩어져 보이면 "저 개체의 자리" 라는 뜻이 흐려진다.
        /// </summary>
        static void KeepCenterBlob(bool[] mask, int w, int h, int sx, int sy)
        {
            int start = sx + sy * w;
            if (!mask[start]) return;

            var keep = new bool[mask.Length];
            var queue = new Queue<int>();
            queue.Enqueue(start);
            keep[start] = true;

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;

                // 4방향으로만 잇는다 — 대각선만 닿은 조각은 별개로 본다.
                TryPush(mask, keep, queue, w, h, x - 1, y);
                TryPush(mask, keep, queue, w, h, x + 1, y);
                TryPush(mask, keep, queue, w, h, x, y - 1);
                TryPush(mask, keep, queue, w, h, x, y + 1);
            }

            for (int i = 0; i < mask.Length; i++) mask[i] = keep[i];
        }

        static void TryPush(bool[] mask, bool[] keep, Queue<int> queue, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = x + y * w;
            if (!mask[i] || keep[i]) return;
            keep[i] = true;
            queue.Enqueue(i);
        }
    }
}
