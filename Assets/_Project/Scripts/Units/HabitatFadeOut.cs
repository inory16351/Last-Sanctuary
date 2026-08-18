using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LastSanctuary.Units
{
    /// <summary>
    /// <b>서식지가 바깥에서 안쪽으로 서서히 걷힌다</b> (유저 지시 2026-08-18:
    /// <i>"보스 몹 처치 시 서식지 청크가 즉시 사라지지 말고 중앙에서 먼곳에서부터 서서히
    /// 페이드 아웃되며 시간이 지남에 따라 없어지는 걸로 해줘 저그 해처리 점막 없어지는 거처럼"</i>).
    ///
    /// ★★ <b>왜 별도 오브젝트인가 — 주인이 이미 죽었기 때문이다.</b>
    /// <see cref="NeutralHabitat"/> 는 몬스터에 붙어 있고, 몬스터는 죽는 즉시
    /// <c>Destroy(gameObject)</c> 된다(<see cref="NeutralMonsterUnit.OnDeath"/>).
    /// 파괴되는 오브젝트에서는 코루틴도 <c>Update</c> 도 <b>그 프레임에 멈춘다</b> —
    /// 거기서 연출을 돌리면 첫 프레임만 나오고 끊긴다. 그래서 <b>연출만 들고 나오는
    /// 빈 오브젝트</b>를 하나 띄우고, 끝나면 스스로 사라진다.
    ///
    /// ★ <b>바깥부터 걷힌다</b> — 칸마다 <b>시작 시각</b>이 다르다.
    /// 중심에서 먼 칸일수록 먼저 시작하므로 경계가 안쪽으로 오므라드는 것처럼 보인다
    /// (저그 점막이 걷히는 그림). 칸 하나가 사라지는 데 걸리는 시간
    /// (<see cref="_cellSeconds"/>)은 모두 같아서, 걷히는 <b>띠의 두께</b>가 일정하다.
    ///
    /// ⚠ <b>타일맵은 칸마다 색을 따로 줄 수 있다</b> — 단 <c>TileFlags.LockColor</c> 가
    /// 걸려 있으면 무시된다. 그래서 칸이 사라지기 시작할 때 <c>TileFlags.None</c> 으로
    /// 풀어준다. 다 사라진 칸은 <b>원래 타일과 원래 색</b>을 되돌린다 — 색을 안 되돌리면
    /// 그 자리에 투명해진 지형이 남는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HabitatFadeOut : MonoBehaviour
    {
        /// <summary>사라지는 중인 칸 하나. 원래 타일·색과 <b>언제 사라지기 시작하는지</b>를 들고 있다.</summary>
        struct Cell
        {
            public Vector3Int Pos;
            public TileBase Original;
            public Color OriginalColor;
            public float StartAt;
            public bool Unlocked;     // TileFlags 를 이미 풀었는가 (한 번만 하면 된다)
        }

        Tilemap _ground;
        Tilemap _deco;
        readonly List<Cell> _groundCells = new List<Cell>();
        readonly List<Cell> _decoCells = new List<Cell>();

        float _startTime;
        float _cellSeconds;
        float _endTime;

        /// <summary>
        /// 연출을 시작한다. <b>새 오브젝트를 만들어</b> 거기서 돌린다 (클래스 주석 참조).
        ///
        /// <paramref name="spreadSeconds"/> 는 <b>가장 먼 칸이 시작해서 중심 칸이 시작할
        /// 때까지</b> 걸리는 시간이고, <paramref name="cellSeconds"/> 는 <b>칸 하나</b>가
        /// 완전히 사라지는 데 걸리는 시간이다. 전체 길이는 둘의 합이다.
        /// </summary>
        public static void Begin(Tilemap ground, Tilemap deco,
                                 IReadOnlyList<NeutralHabitat.PaintedCell> groundCells,
                                 IReadOnlyList<NeutralHabitat.PaintedCell> decoCells,
                                 Vector3Int centerCell,
                                 float spreadSeconds, float cellSeconds)
        {
            bool anything = (ground != null && groundCells != null && groundCells.Count > 0)
                         || (deco != null && decoCells != null && decoCells.Count > 0);
            if (!anything) return;

            var go = new GameObject("HabitatFadeOut");
            var fx = go.AddComponent<HabitatFadeOut>();
            fx.Setup(ground, deco, groundCells, decoCells, centerCell,
                     Mathf.Max(0f, spreadSeconds), Mathf.Max(0.05f, cellSeconds));
        }

        void Setup(Tilemap ground, Tilemap deco,
                   IReadOnlyList<NeutralHabitat.PaintedCell> groundCells,
                   IReadOnlyList<NeutralHabitat.PaintedCell> decoCells,
                   Vector3Int centerCell, float spreadSeconds, float cellSeconds)
        {
            _ground = ground;
            _deco = deco;
            _cellSeconds = cellSeconds;
            _startTime = Time.time;

            // ★ 가장 먼 칸을 기준으로 정규화한다 — 서식지 반지름을 따로 받지 않아도
            //   "제일 바깥"이 항상 0 초에 시작하도록 맞춰진다(모양이 울퉁불퉁해도 성립).
            float maxDist = 0.01f;
            maxDist = Mathf.Max(maxDist, FarthestFrom(groundCells, centerCell));
            maxDist = Mathf.Max(maxDist, FarthestFrom(decoCells, centerCell));

            Fill(_groundCells, groundCells, centerCell, maxDist, spreadSeconds);
            Fill(_decoCells, decoCells, centerCell, maxDist, spreadSeconds);

            _endTime = _startTime + spreadSeconds + cellSeconds;
        }

        static float FarthestFrom(IReadOnlyList<NeutralHabitat.PaintedCell> cells, Vector3Int center)
        {
            float best = 0f;
            if (cells == null) return best;
            for (int i = 0; i < cells.Count; i++)
            {
                float dx = cells[i].Cell.x - center.x;
                float dy = cells[i].Cell.y - center.y;
                best = Mathf.Max(best, Mathf.Sqrt(dx * dx + dy * dy));
            }
            return best;
        }

        void Fill(List<Cell> into, IReadOnlyList<NeutralHabitat.PaintedCell> from,
                  Vector3Int center, float maxDist, float spreadSeconds)
        {
            if (from == null) return;
            for (int i = 0; i < from.Count; i++)
            {
                float dx = from[i].Cell.x - center.x;
                float dy = from[i].Cell.y - center.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                into.Add(new Cell
                {
                    Pos = from[i].Cell,
                    Original = from[i].Original,
                    OriginalColor = from[i].OriginalColor,
                    // 먼 칸(d = maxDist)이 0 초, 중심 칸이 spreadSeconds 에 시작한다.
                    StartAt = (1f - Mathf.Clamp01(d / maxDist)) * spreadSeconds,
                });
            }
        }

        void Update()
        {
            float t = Time.time - _startTime;

            Step(_ground, _groundCells, t);
            Step(_deco, _decoCells, t);

            if (_groundCells.Count == 0 && _decoCells.Count == 0) Destroy(gameObject);
            else if (Time.time > _endTime + 5f) { FinishAll(); Destroy(gameObject); }   // 안전장치
        }

        /// <summary>
        /// 한 층을 한 프레임 진행시킨다. <b>다 사라진 칸은 목록에서 빼고</b>(마지막 칸과 맞바꿔
        /// 제거) 원래 타일을 되돌린다 — 남겨두면 매 프레임 다시 계산하게 된다.
        /// </summary>
        void Step(Tilemap map, List<Cell> cells, float t)
        {
            if (map == null) { cells.Clear(); return; }

            for (int i = cells.Count - 1; i >= 0; i--)
            {
                Cell c = cells[i];
                if (t < c.StartAt) continue;                  // 아직 차례가 아니다 (알파 1 그대로)

                float a = 1f - Mathf.Clamp01((t - c.StartAt) / _cellSeconds);

                if (a <= 0f)
                {
                    RestoreCell(map, c);
                    cells[i] = cells[cells.Count - 1];
                    cells.RemoveAt(cells.Count - 1);
                    continue;
                }

                if (!c.Unlocked)
                {
                    // LockColor 가 걸려 있으면 SetColor 가 무시된다 — 한 번만 풀어준다.
                    map.SetTileFlags(c.Pos, TileFlags.None);
                    c.Unlocked = true;
                }

                Color col = c.OriginalColor;
                col.a *= a;
                map.SetColor(c.Pos, col);
                cells[i] = c;
            }
        }

        void RestoreCell(Tilemap map, Cell c)
        {
            map.SetTile(c.Pos, c.Original);
            // ⚠ 색은 타일이 아니라 <b>칸</b>에 붙는다 — 되돌리지 않으면 새로 깔린
            //   지형까지 투명해진 채로 남는다.
            map.SetTileFlags(c.Pos, TileFlags.None);
            map.SetColor(c.Pos, c.OriginalColor);
        }

        /// <summary>남은 칸을 한 번에 되돌린다 (안전장치 · 씬을 떠날 때).</summary>
        void FinishAll()
        {
            if (_ground != null)
                for (int i = 0; i < _groundCells.Count; i++) RestoreCell(_ground, _groundCells[i]);
            _groundCells.Clear();

            if (_deco != null)
                for (int i = 0; i < _decoCells.Count; i++) RestoreCell(_deco, _decoCells[i]);
            _decoCells.Clear();
        }
    }
}
