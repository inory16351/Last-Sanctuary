using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LastSanctuary.Map
{
    /// <summary>
    /// 고정 크기 아레나를 청크 단위로 절차적 생성한다.
    ///
    /// 파이프라인
    ///   1. 청크별 바이옴 추첨 (시드 결정적)
    ///   2. 바닥 채우기
    ///   3. 장애물 마스크 (펄린 노이즈 → 셀룰러 오토마타 스무딩)
    ///   4. 성역 반경 / 경계 벽 / 스폰 게이트 강제 반영
    ///   5. 스폰 게이트 → 성역 통로 굴착
    ///   6. 연결성 검사(BFS) — 도달 불가 지역은 벽으로 메움
    ///   7. 타일맵에 일괄 기록 (SetTilesBlock)
    ///
    /// 6번이 중요하다. 몬스터는 플로우 필드로 성역을 향해 이동하므로
    /// 고립된 구역이 남으면 스폰 지점이 도달 불가가 되어 웨이브가 진행되지 않는다.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [Header("생성 설정")]
        [SerializeField] MapGenerationConfigSO config;

        [Header("대상 타일맵")]
        [SerializeField] Tilemap groundTilemap;
        [SerializeField] Tilemap decoTilemap;
        [SerializeField] Tilemap obstacleTilemap;

        [Header("경계")]
        [Tooltip("MapBounds 의 Collider2D. 카메라 Confiner2D 와 CameraRigController 가 이것을 경계로 쓴다")]
        [SerializeField] Collider2D boundsShape;

        [Tooltip("생성할 때마다 경계 콜라이더를 맵 크기에 자동으로 맞춘다. " +
                 "끄면 맵 크기를 바꿔도 카메라 경계는 그대로 남는다")]
        [SerializeField] bool autoResizeBounds = true;

        /// <summary>에디터에서 경계 동기화에 사용.</summary>
        public Collider2D BoundsShape => boundsShape;

        /// <summary>자동 리사이즈 설정. 에디터 인스펙터 표시용.</summary>
        public bool AutoResizeBounds => autoResizeBounds;

        [Header("런타임")]
        [Tooltip("체크하면 게임 시작 시 자동 생성. 끄면 에디터 버튼으로만 생성")]
        [SerializeField] bool generateOnAwake = false;

        [Tooltip("★ 켜면 <b>판마다 다른 맵</b>이 나온다 (씨앗을 매번 새로 뽑는다).\n" +
                 "끄면 config 의 고정 seed 를 써서 언제나 같은 맵이 나온다 — 지형 자체를 " +
                 "비교해야 하는 디버깅에는 끄는 편이 낫다.\n\n" +
                 "⚠ <b>이어하기는 이 값과 무관하게 저장된 씨앗을 쓴다</b> — 안 그러면 불러올 때마다 " +
                 "지형이 바뀌어 캐릭터가 벽에 박힌다(ResolveStartupSeed 주석).")]
        [SerializeField] bool randomizeSeedOnAwake = true;

        /// <summary>
        /// 지금 깔려 있는 지형을 만든 씨앗. <b>저장이 이 값을 적어야</b> 이어하기가 같은 맵을
        /// 다시 만든다(<see cref="LastSanctuary.Save.SaveData.mapSeed"/>).
        /// 한 번도 생성하지 않았으면 0 이다.
        /// </summary>
        public int ActiveSeed { get; private set; }

        // 생성 결과 — 다른 시스템(플로우 필드, 스폰 배치)이 참조한다.
        public MapGenerationConfigSO Config => config;
        public bool[] Walkable { get; private set; }

        /// <summary>
        /// ★★★ <b>맵의 칸 수</b> — <b>생성 전에도 옳은 값</b>을 돌려준다 (2026-08-25).
        ///
        /// ══════════════════════════════════════════════════════════════
        ///  ⚠⚠ 무슨 일이 있었나 — <b>발굴 기능이 통째로 죽어 있었다</b>
        /// ══════════════════════════════════════════════════════════════
        /// 유저 리포트: *"발굴 기능이 구현이 안된거같은데 한 번 확인해줘"*.
        ///
        /// 예전에는 이 둘이 <c>{ get; private set; }</c> 였고 <see cref="Generate"/> 안에서만
        /// 채워졌다. 그런데 씬의 <see cref="generateOnAwake"/> 는 <b>꺼져 있다</b> — 이 판의
        /// 지형은 에디터에서 미리 구워 타일맵에 <b>직렬화돼</b> 있고 런타임에는 만들지 않는다.
        /// 즉 <b><c>MapSize</c> 가 영원히 (0,0)</b> 이었다.
        ///
        /// <c>RelicDigService.PickSites</c> 만 이 값을 읽고 있었고
        /// (다른 다섯 곳은 전부 <c>Config.MapSize</c> 를 읽는다), 거기서
        /// <c>if (size.x &lt;= 0 || size.y &lt;= 0) return;</c> 로 <b>조용히</b> 빠져나갔다:
        ///
        /// <code>
        ///   발굴 칸 0개  →  표식(느낌표) 0개  →  누를 것이 없다  →  «기능이 없다»
        ///   ⚠ 그 return 이 <b>로그보다 앞</b>이라 콘솔에 <b>한 줄도</b> 남지 않았다.
        /// </code>
        ///
        /// → <b>값의 정본은 언제나 <see cref="config"/> 다.</b> <see cref="Generate"/> 도
        ///   <c>config.MapSize</c> 를 그대로 옮겨 담을 뿐이므로 <b>두 값은 늘 같다</b> —
        ///   생성 전이면 config 를 그대로 돌려주는 것이 «다른 값» 이 아니라 «같은 값» 이다.
        /// ★ 이렇게 두면 <b>앞으로 이 값을 읽는 코드가 늘어도</b> 같은 함정에 안 빠진다.
        ///   «Config 를 읽어라» 를 사람이 기억하는 대신 <b>속성이 스스로 맞는다</b>.
        /// </summary>
        public Vector2Int MapSize =>
            _mapSize.x > 0 && _mapSize.y > 0 ? _mapSize
            : config != null ? config.MapSize : Vector2Int.zero;

        /// <summary>맵의 왼쪽 아래 셀 좌표. <see cref="MapSize"/> 와 같은 이유로 config 폴백이 있다.</summary>
        public Vector2Int Origin =>
            _mapSize.x > 0 && _mapSize.y > 0 ? _origin
            : config != null ? config.Origin : Vector2Int.zero;

        /// <summary>실제로 <see cref="Generate"/> 가 만든 값. 만든 적이 없으면 (0,0) 이다.</summary>
        Vector2Int _mapSize, _origin;

        /// <summary>
        /// 성역 등 고정 구조물이 차지한 칸. 장애물 타일맵과 별개로 관리해서
        /// (타일맵은 절차적 생성이 통째로 다시 쓰므로) 구조물이 스스로 등록/해제한다.
        /// </summary>
        readonly HashSet<Vector3Int> _structureBlockedCells = new HashSet<Vector3Int>();

        /// <summary>스폰 게이트의 로컬 좌표 4개 (하, 상, 좌, 우 순).</summary>
        public List<Vector2Int> SpawnGates { get; } = new List<Vector2Int>();

        void Awake()
        {
            if (generateOnAwake) Generate(ResolveStartupSeed());
        }

        /// <summary>
        /// 이번 판에 쓸 씨앗을 정한다 — <b>이어하기 &gt; 무작위 &gt; 고정</b> 순.
        ///
        /// ★★ <b>왜 여기서 저장을 들여다보는가(밀지 않고 당기는가)</b> — 이 결정은 반드시
        /// <see cref="Awake"/> 안에서 끝나야 한다. 맵을 읽는 쪽이 전부 <c>Start</c> 에서 도는데
        /// (<c>FogOfWarService.Build</c> · <c>FlowFieldService</c> · <c>NexusSanctuary</c> ·
        /// 스포너 셋), 유니티는 <b>모든 Awake 가 모든 Start 보다 먼저</b> 돈다는 것만 보장하고
        /// 오브젝트 사이의 Awake 순서는 보장하지 않는다. 그래서 "누군가 씨앗을 넣어 준 뒤에
        /// 생성한다" 는 구조를 만들 수가 없다 — <b>스스로 물어보는 것</b>이 유일하게 확실하다.
        ///
        /// ⚠ <b>복원 시점에 맵을 다시 만드는 방법은 못 쓴다.</b> <c>GameSnapshot</c> 의 복원은
        /// <c>Start</c> 다음 프레임인데, 그때는 이미 안개가 만들어졌고 스포너가 초기 개체를
        /// 뿌렸고 <b>서식지가 칠해져 있다</b>. 거기서 지형을 갈아치우면 그 전부가 어긋난다.
        ///
        /// ⚠ <see cref="SaveService.PendingLoad"/> 는 <c>GameSnapshot</c> 이 <b>복원할 때</b>
        /// 비우므로(그쪽 <c>RestoreNextFrame</c>), Awake 에서 읽는 것은 안전하다.
        ///
        /// ⚠ 무작위 씨앗을 <b>양수로만</b> 뽑는다 — <c>new System.Random(int.MinValue)</c> 는
        /// 런타임 구현에 따라 예외를 던진다(내부에서 절댓값을 취한다). 20억 가지면 충분하다.
        /// </summary>
        int ResolveStartupSeed()
        {
            // ① 이어하기 — 저장된 지형을 그대로 되살린다.
            Save.SaveData pending = Save.SaveService.PendingLoad;
            if (pending != null) return pending.mapSeed;

            // ② 새 판 — 매번 다른 지형.
            if (randomizeSeedOnAwake) return Random.Range(1, int.MaxValue);

            // ③ 고정 — 지형을 비교해야 할 때.
            return config != null ? config.seed : 0;
        }

        // ------------------------------------------------------------------
        // 공개 API
        // ------------------------------------------------------------------

        /// <summary>지정 시드로 맵을 생성한다.</summary>
        public void Generate(int seed)
        {
            if (!Validate()) return;

            // 저장이 읽어갈 값 — 지금 깔린 지형이 어느 씨앗에서 나왔는지.
            ActiveSeed = seed;

            _mapSize = config.MapSize;
            _origin  = config.Origin;
            int w = _mapSize.x, h = _mapSize.y;
            Vector2Int cc = config.ChunkCount;

            // 1. 청크별 팔레트 — 전체 타일 풀에서 매번 새로 뽑는다
            ChunkPalette[] palettes = BuildChunkPalettes(seed);

            // 셀 하나가 속한 청크 팔레트를 빠르게 얻기 위한 헬퍼.
            // Tiles 모드에서 맵이 청크 배수보다 작으면(= 잘린 경우) 마지막 청크는
            // 일부만 사용되며, Clamp 가 인덱스 초과를 막는다.
            int csx = Mathf.Max(1, config.chunkSize.x);
            int csy = Mathf.Max(1, config.chunkSize.y);
            int ChunkIndexAt(int x, int y)
            {
                int cx = Mathf.Clamp(x / csx, 0, cc.x - 1);
                int cy = Mathf.Clamp(y / csy, 0, cc.y - 1);
                return cx + cy * cc.x;
            }
            var rng = new System.Random(seed);

            // 2~6. 마스크 계산
            bool[] isWall = BuildObstacleMask(seed, w, h, palettes, ChunkIndexAt);
            ApplyNexusClear(isWall, w, h);
            ApplyBorderAndGates(isWall, w, h);
            CarveCorridors(isWall, w, h, seed);
            SealUnreachable(isWall, w, h);

            // 7. 타일 배열 구성 후 일괄 기록
            var ground   = new TileBase[w * h];
            var deco     = new TileBase[w * h];
            var obstacle = new TileBase[w * h];

            OrganicTileSetSO set = config.tileSet;

            // 맵 밖은 벽으로 취급 — 최외곽 벽이 정면/모서리 판정에서 끊기지 않는다.
            bool WallAt(int x, int y) =>
                x < 0 || y < 0 || x >= w || y >= h || isWall[x + y * w];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = x + y * w;
                    ChunkPalette p = palettes[ChunkIndexAt(x, y)];

                    ground[i] = PickWeighted(p.ground, rng);

                    if (isWall[i])
                    {
                        obstacle[i] = PickWallTile(set, x, y, WallAt, rng);
                        continue;
                    }

                    // 벽에 닿은 바닥칸은 경계 타일로 정리하고, 그 외에는 프롭을 얹는다.
                    // 둘 다 Deco 타일맵을 쓰므로 한 칸에 하나만 놓일 수 있다.
                    TileBase edge = PickEdgeTile(p.edges, x, y, WallAt, rng);
                    if (edge != null) { deco[i] = edge; continue; }

                    if (p.props != null && p.props.Length > 0 && rng.NextDouble() < p.propChance)
                        deco[i] = PickWeighted(p.props, rng);
                }
            }

            var bounds = new BoundsInt(Origin.x, Origin.y, 0, w, h, 1);
            WriteBlock(groundTilemap,   bounds, ground);
            WriteBlock(decoTilemap,     bounds, deco);
            WriteBlock(obstacleTilemap, bounds, obstacle);

            if (autoResizeBounds) SyncBoundsToMap();

            // 다른 시스템이 쓸 통행 가능 맵 (벽 = false, <b>벽 앞면에 덮인 칸도 false</b>).
            // 판정 규칙은 런타임의 <see cref="IsCellBlocked"/> 와 반드시 같아야 한다 —
            // 하나만 고치면 "생성 때는 길이 있는데 게임에서는 막혀 있다"가 된다.
            Walkable = new bool[w * h];
            for (int i = 0; i < isWall.Length; i++)
                Walkable[i] = !isWall[i] && !IsSkirt(isWall, w, h, i);

            Vector2Int cropped = config.CroppedAmount;
            string cropInfo = config.IsCropped
                ? $" · 청크 영역 {config.CoveredSize.x}x{config.CoveredSize.y} 에서 " +
                  $"{cropped.x}x{cropped.y} 잘라냄"
                : "";

            Debug.Log($"[MapGenerator] {w}x{h} 생성 완료 · seed={seed} · " +
                      $"청크 {cc.x}x{cc.y} (청크당 {config.chunkSize.x}x{config.chunkSize.y})" +
                      $"{cropInfo} · 통행 가능 {CountTrue(Walkable)}/{w * h} · " +
                      $"타일셋 {(set != null ? set.TotalTiles : 0)}종");
        }

        /// <summary>세 타일맵을 모두 비운다.</summary>
        public void ClearAll()
        {
            if (groundTilemap   != null) groundTilemap.ClearAllTiles();
            if (decoTilemap     != null) decoTilemap.ClearAllTiles();
            if (obstacleTilemap != null) obstacleTilemap.ClearAllTiles();
            Walkable = null;
            SpawnGates.Clear();
        }

        /// <summary>로컬 그리드 좌표 → 월드 타일 좌표.</summary>
        public Vector3Int LocalToCell(Vector2Int local) =>
            new Vector3Int(local.x + Origin.x, local.y + Origin.y, 0);

        // ------------------------------------------------------------------
        // 다른 시스템이 맵을 조회하는 API
        // Walkable[] 은 런타임 배열이라 씬을 다시 열면 사라지므로,
        // 위치 판정은 직렬화되어 남아 있는 장애물 타일맵을 기준으로 한다.
        // ------------------------------------------------------------------

        public Tilemap GroundTilemap => groundTilemap;
        public Tilemap ObstacleTilemap => obstacleTilemap;

        /// <summary>
        /// 바닥 위에 얹는 장식 타일맵(경계·프롭). <b>런타임에 덧그리는 연출</b>이 여기를 쓴다 —
        /// 지금은 에픽 중립 몬스터의 서식지(<see cref="Units.NeutralHabitat"/>)가 유일하다.
        /// Ground 가 아니라 여기에 그리면 <b>원래 타일을 기억했다가 되돌릴 수</b> 있다.
        /// </summary>
        public Tilemap DecoTilemap => decoTilemap;

        /// <summary>맵 중앙 셀. Origin = -MapSize/2 이므로 항상 (0,0) 이다.</summary>
        public Vector3Int CenterCell => Vector3Int.zero;

        /// <summary>셀 중심의 월드 좌표.</summary>
        public Vector3 CellCenterWorld(Vector3Int cell) =>
            groundTilemap != null
                ? groundTilemap.GetCellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        /// <summary>
        /// 해당 셀이 장애물인지 (지형 장애물 타일 + <b>벽 앞면이 덮은 칸</b> +
        /// 성역 등 구조물 점유 칸).
        /// </summary>
        public bool IsCellBlocked(Vector3Int cell) =>
            (obstacleTilemap != null &&
             (obstacleTilemap.HasTile(cell) || obstacleTilemap.HasTile(cell + Vector3Int.up))) ||
            _structureBlockedCells.Contains(cell);

        /// <summary>
        /// ★ 이 칸에 <b>벽 타일 자체가 깔려 있는가</b> (2026-08-18 신설).
        ///
        /// <b><see cref="IsCellBlocked"/> 와 다르다.</b> 그쪽은 「유닛이 못 지나가는가」이고
        /// 세 가지를 한데 묶는다:
        /// <code>
        ///   ① 이 칸의 벽 타일          ← 그림이 실제로 여기 있다
        ///   ② 바로 북쪽 벽의 앞면(치마) ← 그림은 북쪽 칸에 있고 이 칸을 덮는다 (102-5절)
        ///   ③ 구조물 발판(성역·타워)  ← 그림은 스프라이트고 <b>바닥은 평범한 지형이다</b>
        /// </code>
        /// <b>바닥을 칠하는 쪽</b>(<see cref="LastSanctuary.Units.NeutralHabitat"/>)이 알고 싶은
        /// 것은 ①뿐이다 — ①은 칠하면 <b>벽 그림이 지워지지만</b>, ②·③은 Ground 타일맵에
        /// 평범한 바닥이 깔려 있고 그 위를 다른 그림이 덮고 있을 뿐이다.
        ///
        /// ⚠ <b>이 구분이 없어서 성역에 구멍이 났다</b> (유저 리포트 2026-08-18:
        /// <i>"중앙 건물 바로 아래 타일은 왜 기본 타일 그대로지"</i>). 성역이 발판 3x3 을
        /// ③으로 등록하는데 <see cref="IsCellBlocked"/> 로 걸러서, 성역이 <b>건물 발판만
        /// 남기고</b> 그려졌다. 건물 스프라이트가 그 위를 덮으니 <b>스프라이트 아래로 삐져나온
        /// 한 줄</b>만 원래 바닥으로 보인 것이다.
        /// </summary>
        public bool HasWallTile(Vector3Int cell) =>
            obstacleTilemap != null && obstacleTilemap.HasTile(cell);

        /// <summary>
        /// ★ <b>벽 앞면(치마)이 덮은 칸</b>인가 — 자기 칸엔 벽이 없는데 <b>바로 북쪽</b>에
        /// 벽이 있는 칸이다 (2026-08-18, 유저 리포트: *"벽 이미지 입체감 있게 만드려고 한
        /// 아래쪽 타일이 이동 불가 판정이 없어서 어색함"*).
        ///
        /// <b>왜 정확히 한 칸인가</b> — 21·22절에서 벽을 2칸 높이로 새로 그렸다.
        /// <c>Wall_Outer</c> 스프라이트는 <b>20x40</b>(1x2 타일)에 피벗 <c>{0.5, 0.75}</c> 라,
        /// 타일맵이 칸 중심에 피벗을 놓으면 그림이 위로 0.5칸·아래로 1.5칸 뻗는다:
        /// <code>
        ///   자기 칸(중심±0.5) 전부 + 바로 아래 칸(중심-1.5 ~ -0.5) 전부
        /// </code>
        /// 즉 아래 칸을 <b>정확히 하나, 빈틈없이(알파 100%)</b> 덮는다 — 실측했다.
        /// 그 칸은 화면상 완전한 「벽의 정면」인데 이동 판정이 없어서 유닛이 벽을 뚫고
        /// 걸어 들어가는 것처럼 보였다.
        ///
        /// <b>왜 북쪽 타일의 종류를 안 따지는가</b> — 20x20 짜리 <c>Wall_Inner_Fill</c> 은
        /// 사방이 벽일 때만 깔린다(<see cref="PickWallTile"/>). 내 칸이 벽이 아니면 북쪽 칸은
        /// 반드시 남쪽이 열린 <c>Wall_Outer</c>(20x40) 다 — 그래서 「북쪽에 벽 타일이 있다」와
        /// 「내 칸이 벽 앞면에 덮였다」가 완전히 같은 조건이 된다.
        ///
        /// <b>왜 벽 타일을 깔지 않는가</b> — 이 칸에 장애물 타일을 넣으면 그 칸도 다시 자기
        /// 아래를 덮는 20x40 벽이 되어 <b>남쪽으로 끝없이 번진다</b>. 그림은 그대로 두고
        /// 판정만 막는 것이 맞다.
        /// </summary>
        public bool IsWallSkirt(Vector3Int cell) =>
            obstacleTilemap != null &&
            !obstacleTilemap.HasTile(cell) &&
            obstacleTilemap.HasTile(cell + Vector3Int.up);

        /// <summary>
        /// ★★ 이 칸이 <b>시야선을 막는가</b> — 원거리·마법의 「벽 너머는 못 때린다」 판정에만 쓴다
        /// (2026-08-27 신설).
        ///
        /// <b><see cref="IsCellBlocked"/> 에서 구조물 발판(③)을 뺀 것</b>이다. 그 함수의 위
        /// 주석이 셋을 구분해 놓았는데(① 벽 타일 · ② 벽 앞면(치마) · ③ 성역·포탑 발판),
        /// <b>시야를 막는 것은 ①·②뿐</b>이다. ③은 벽이 아니라 <b>유닛 자신</b>이고,
        /// 이 프로젝트의 유닛에는 애초에 콜라이더가 없다(준수사항 U-D9) — 즉 유닛은
        /// 서로의 시야를 막지 않는다는 것이 이미 규칙이다.
        ///
        /// ⚠⚠ <b>이 구분이 없어서 원거리 웨이브 몬스터가 성역을 영영 못 때렸다</b>
        /// (유저 리포트 2026-08-27: *"원거리 웨이브 몬스터가 성역 심장부를 공격하지 않는다"*).
        /// <see cref="LastSanctuary.Units.Nexus"/> 가 자기 발판 3x3 을 ③으로 등록하는데,
        /// <c>UnitCombat.BuildTargetFilter</c> 의 시야선 검사가
        /// <c>IsCellPlaceable</c>(= ①②③ 전부)로 <b>성역 중심까지 선을 훑었다</b>.
        /// 선의 끝점이 정의상 성역 발판 안이므로 <b>어디서 쏘든 무조건 false</b> —
        /// 성역이 후보에서 통째로 탈락하고, 타겟이 없으니 몬스터는
        /// <c>CombatState.Advance</c> 로 성역에 몸만 부비고 서 있었다.
        /// 근거리는 <c>needLos</c> 가 false 라 이 함정을 안 밟아서 «원거리만» 안 때린 것이다.
        /// 같은 이유로 <b>포탑</b>(발판 2x2)도 원거리 몬스터의 후보가 아니었다.
        ///
        /// ⚠ <b>이동 판정에는 쓰지 말 것.</b> 이동은 발판을 반드시 막힌 것으로 봐야 한다 —
        /// 안 그러면 유닛이 성역·포탑을 뚫고 걸어가려 한다. 그래서
        /// <see cref="GridPathfinder.HasLineOfSight"/>(이동·경로 평활화)는 그대로 두고
        /// <see cref="GridPathfinder.HasAttackLineOfSight"/> 만 이 판정을 쓴다.
        /// </summary>
        public bool BlocksSight(Vector3Int cell) =>
            !IsCellInsideMap(cell) ||
            (obstacleTilemap != null &&
             (obstacleTilemap.HasTile(cell) || obstacleTilemap.HasTile(cell + Vector3Int.up)));

        /// <summary>
        /// 성역 등 구조물이 자기 발판 칸을 등록한다. 등록된 칸은 벽과 똑같이
        /// <see cref="IsCellBlocked"/> / 이동 충돌 / 배치 판정에서 막힌 것으로 취급된다.
        /// </summary>
        public void RegisterStructureFootprint(IEnumerable<Vector3Int> cells)
        {
            foreach (Vector3Int c in cells) _structureBlockedCells.Add(c);
        }

        /// <summary>구조물이 파괴되거나 비활성화될 때 자기 발판 칸을 반납한다.</summary>
        public void UnregisterStructureFootprint(IEnumerable<Vector3Int> cells)
        {
            foreach (Vector3Int c in cells) _structureBlockedCells.Remove(c);
        }

        /// <summary>
        /// 중심 셀 기준 한 변 footprintTiles 칸의 정사각 영역을 나열한다.
        /// <b>홀수 발판 전용</b>이다 — 짝수(2x2 등)는 중심이 칸이 아니라 네 칸이 만나는
        /// 꼭짓점이라 "중심 칸" 이라는 개념 자체가 없다. 짝수 발판은
        /// <see cref="FootprintCellsFrom"/> 로 좌하단 칸을 기준으로 나열할 것.
        /// </summary>
        public static IEnumerable<Vector3Int> FootprintCells(Vector3Int center, int footprintTiles)
        {
            int half = Mathf.Max(1, footprintTiles) / 2;
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                    yield return new Vector3Int(center.x + dx, center.y + dy, 0);
        }

        /// <summary>
        /// <paramref name="minCell"/>(좌하단 칸)에서 시작하는 한 변 <paramref name="size"/> 칸의
        /// 정사각 영역. 짝·홀 발판 모두 정확하다 — 2x2 포탑처럼 짝수 발판을 쓰는 건물은
        /// 이쪽을 쓴다(<see cref="LastSanctuary.Buildings.BuildService"/>).
        /// </summary>
        public static IEnumerable<Vector3Int> FootprintCellsFrom(Vector3Int minCell, int size)
        {
            int n = Mathf.Max(1, size);
            for (int dy = 0; dy < n; dy++)
                for (int dx = 0; dx < n; dx++)
                    yield return new Vector3Int(minCell.x + dx, minCell.y + dy, 0);
        }

        /// <summary>
        /// 좌하단 칸이 <paramref name="minCell"/> 인 <paramref name="size"/>x<paramref name="size"/>
        /// 영역의 <b>월드 중심</b>. 짝수 발판이면 칸 중심이 아니라 칸 경계 위에 놓인다.
        /// </summary>
        public Vector3 FootprintCenterWorld(Vector3Int minCell, int size)
        {
            int n = Mathf.Max(1, size);
            Vector3 a = CellCenterWorld(minCell);
            Vector3 b = CellCenterWorld(new Vector3Int(minCell.x + n - 1, minCell.y + n - 1, 0));
            return (a + b) * 0.5f;
        }

        /// <summary>월드 좌표 → 셀. 이동 충돌 판정처럼 위치 기반 조회가 필요한 곳에서 쓴다.</summary>
        public Vector3Int WorldToCell(Vector3 world) =>
            groundTilemap != null
                ? groundTilemap.WorldToCell(world)
                : new Vector3Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), 0);

        /// <summary>해당 셀이 맵 범위 안인지. config 기준이라 생성 전에도 동작한다.</summary>
        public bool IsCellInsideMap(Vector3Int cell)
        {
            if (config == null) return false;
            Vector2Int size = config.MapSize;
            Vector2Int org = config.Origin;
            return cell.x >= org.x && cell.x < org.x + size.x
                && cell.y >= org.y && cell.y < org.y + size.y;
        }

        /// <summary>배치 가능한 셀인지 (맵 안 + 장애물 아님).</summary>
        public bool IsCellPlaceable(Vector3Int cell) =>
            IsCellInsideMap(cell) && !IsCellBlocked(cell);

        /// <summary>
        /// 지정 셀에서 바깥으로 링을 넓혀가며 배치 가능한 셀을 찾는다.
        /// exclude 로 이미 사용한 칸을 제외할 수 있다.
        /// </summary>
        public bool TryFindPlaceableNear(Vector3Int center, int maxRadius,
                                         System.Predicate<Vector3Int> exclude,
                                         out Vector3Int found)
        {
            for (int r = 0; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        // 링의 테두리만 검사 (r=0 은 중심 한 칸)
                        if (r > 0 && Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;

                        var c = new Vector3Int(center.x + dx, center.y + dy, 0);
                        if (!IsCellPlaceable(c)) continue;
                        if (exclude != null && exclude(c)) continue;

                        found = c;
                        return true;
                    }
                }
            }
            found = center;
            return false;
        }

        /// <summary>
        /// 경계 콜라이더를 현재 맵 크기에 맞춘다.
        ///
        /// 트랜스폼 스케일로 늘리는 대신 콜라이더의 점을 직접 지정한다.
        /// 스케일 방식은 콜라이더 원본 크기가 1x1 이라는 가정에 의존해서,
        /// 나중에 모양이 바뀌면 조용히 어긋난다.
        ///
        /// Confiner2D 는 경계 모양을 베이크해 캐시하므로, 모양을 바꾼 뒤
        /// InvalidateBoundingShapeCache() 를 부르지 않으면 낡은 경계를 계속 쓴다.
        /// </summary>
        public void SyncBoundsToMap()
        {
            if (boundsShape == null || config == null) return;

            Vector2Int size = config.MapSize;
            Transform t = boundsShape.transform;

            // 점을 직접 지정하므로 스케일은 1 로 되돌리고 원점에 맞춘다.
            t.localScale = Vector3.one;
            t.position = new Vector3(0f, 0f, t.position.z);

            float hw = size.x * 0.5f;
            float hh = size.y * 0.5f;

            switch (boundsShape)
            {
                case PolygonCollider2D poly:
                    poly.pathCount = 1;
                    poly.SetPath(0, new[]
                    {
                        new Vector2(-hw, -hh), new Vector2( hw, -hh),
                        new Vector2( hw,  hh), new Vector2(-hw,  hh),
                    });
                    break;

                case BoxCollider2D box:
                    box.offset = Vector2.zero;
                    box.size = new Vector2(size.x, size.y);
                    break;

                default:
                    // 지원하지 않는 콜라이더는 스케일로 근사한다.
                    Debug.LogWarning($"[MapGenerator] {boundsShape.GetType().Name} 는 점 지정을 " +
                                     "지원하지 않아 트랜스폼 스케일로 맞춥니다. " +
                                     "PolygonCollider2D 또는 BoxCollider2D 를 권장합니다.", boundsShape);
                    t.localScale = new Vector3(size.x, size.y, 1f);
                    break;
            }

            InvalidateConfinerCaches();
        }

        /// <summary>
        /// 이 경계 콜라이더를 쓰는 Cinemachine Confiner2D 의 베이크 캐시를 무효화한다.
        /// 참조를 직접 들고 있으면 맵 시스템이 카메라에 결합되므로, 생성 시점에만
        /// 한 번 훑어서 찾는다.
        /// </summary>
        void InvalidateConfinerCaches()
        {
            var confiners = FindObjectsByType<CinemachineConfiner2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int hit = 0;
            foreach (var c in confiners)
            {
                if (c.BoundingShape2D != boundsShape) continue;
                c.InvalidateBoundingShapeCache();
                hit++;
            }

            if (hit == 0 && confiners.Length > 0)
                Debug.LogWarning("[MapGenerator] Confiner2D 를 찾았지만 Bounding Shape 2D 가 " +
                                 $"{boundsShape.name} 를 가리키지 않습니다. 카메라 경계가 " +
                                 "맵과 다르게 동작할 수 있습니다.", this);
        }

        // ------------------------------------------------------------------
        // 단계별 구현
        // ------------------------------------------------------------------

        bool Validate()
        {
            if (config == null)
            {
                Debug.LogError("[MapGenerator] Config 가 비어 있습니다.", this);
                return false;
            }
            if (config.tileSet == null)
            {
                Debug.LogError("[MapGenerator] Config 에 Tile Set 이 비어 있습니다. " +
                               "메뉴 'LastSanctuary > 맵 > OrganicTilemap 타일셋 다시 읽기' 로 " +
                               "타일셋을 만든 뒤 Config 에 연결하세요.", config);
                return false;
            }
            if (!config.tileSet.IsUsable)
            {
                Debug.LogError("[MapGenerator] Tile Set 에 바닥 또는 벽 타일이 없습니다. " +
                               "타일셋을 다시 읽어주세요.", config.tileSet);
                return false;
            }
            if (groundTilemap == null || decoTilemap == null || obstacleTilemap == null)
            {
                Debug.LogError("[MapGenerator] 타일맵 3개를 모두 연결해야 합니다.", this);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 청크 하나가 쓸 타일 조합과 지형 파라미터. 에셋이 아니라 <b>생성할 때마다
        /// 런타임에 뽑는다</b> — 그래서 시드를 바꾸면 모든 청크의 타일 구성이 새로 섞인다.
        /// </summary>
        class ChunkPalette
        {
            public WeightedTile[] ground;
            public WeightedTile[] props;
            public DirectionalTileSet edges;
            public float propChance;
            public float obstacleDensity;
            public float noiseScale;
            public int smoothPasses;
        }

        /// <summary>
        /// 청크마다 전체 타일 풀에서 조합을 새로 뽑는다.
        ///
        /// 청크별 독립 RNG(<see cref="Hash"/>)를 쓰기 때문에 같은 시드는 항상 같은 맵을
        /// 만들고(디버깅·재현에 필요), 시드를 바꾸면 256개 청크 전부가 다른 조합을 받는다.
        /// </summary>
        ChunkPalette[] BuildChunkPalettes(int seed)
        {
            Vector2Int cc = config.ChunkCount;
            int cw = cc.x, ch = cc.y;
            var result = new ChunkPalette[cw * ch];
            OrganicTileSetSO set = config.tileSet;

            for (int cy = 0; cy < ch; cy++)
            {
                for (int cx = 0; cx < cw; cx++)
                {
                    var r = new System.Random(Hash(seed, cx, cy));
                    result[cx + cy * cw] = BuildPalette(set, r);
                }
            }
            return result;
        }

        ChunkPalette BuildPalette(OrganicTileSetSO set, System.Random r)
        {
            var p = new ChunkPalette
            {
                obstacleDensity = RandRange(r, config.obstacleDensityRange),
                noiseScale      = RandRange(r, config.noiseScaleRange),
                smoothPasses    = RandRange(r, config.smoothPassesRange),
                propChance      = RandRange(r, config.propChanceRange),
                ground          = System.Array.Empty<WeightedTile>(),
                props           = System.Array.Empty<WeightedTile>(),
            };

            if (set == null) return p;

            int groundCount = RandRange(r, config.groundTilesPerChunk);
            p.ground = ShuffleGround(set.ground, set.groundCracked,
                                     config.crackedGroundRatio, groundCount, r);
            p.props = ShufflePick(set.props, RandRange(r, config.propTilesPerChunk), r);

            // 경계 장식 계열도 청크마다 갈라 준다 — 피 웅덩이 지대와 균열 지대가 섞인다.
            bool blood = r.NextDouble() < 0.5;
            DirectionalTileSet chosen = blood ? set.bloodEdge : set.chasmEdge;
            if (chosen == null || !chosen.HasAny)
                chosen = blood ? set.chasmEdge : set.bloodEdge;
            p.edges = chosen;

            return p;
        }

        /// <summary>바닥 팔레트 — 일반 바닥과 갈라진 바닥을 비율대로 섞는다.</summary>
        static WeightedTile[] ShuffleGround(WeightedTile[] normal, WeightedTile[] cracked,
                                            float crackedRatio, int count, System.Random r)
        {
            count = Mathf.Max(1, count);
            int crackedCount = Mathf.RoundToInt(count * Mathf.Clamp01(crackedRatio));
            int normalCount = Mathf.Max(1, count - crackedCount);

            WeightedTile[] a = ShufflePick(normal, normalCount, r);
            WeightedTile[] b = crackedCount > 0
                ? ShufflePick(cracked, crackedCount, r)
                : System.Array.Empty<WeightedTile>();

            if (a.Length == 0) return b;
            if (b.Length == 0) return a;

            var merged = new WeightedTile[a.Length + b.Length];
            a.CopyTo(merged, 0);
            b.CopyTo(merged, a.Length);
            return merged;
        }

        /// <summary>
        /// 풀에서 서로 다른 타일 <paramref name="count"/> 개를 골라 가중치를 새로 굴린다.
        /// 가중치까지 다시 굴리는 이유: 같은 타일 조합이 나와도 등장 비율이 달라져
        /// 청크마다 다른 지대처럼 보인다.
        /// </summary>
        static WeightedTile[] ShufflePick(WeightedTile[] pool, int count, System.Random r)
        {
            if (pool == null || pool.Length == 0) return System.Array.Empty<WeightedTile>();
            count = Mathf.Clamp(count, 1, pool.Length);

            // 부분 피셔-예이츠 — 앞쪽 count 개만 섞으면 되므로 전체를 섞지 않는다.
            var idx = new int[pool.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            for (int i = 0; i < count; i++)
            {
                int j = i + r.Next(idx.Length - i);
                (idx[i], idx[j]) = (idx[j], idx[i]);
            }

            var picked = new WeightedTile[count];
            for (int i = 0; i < count; i++)
            {
                picked[i] = pool[idx[i]];
                picked[i].weight = 1f + (float)r.NextDouble() * 3f;
            }
            return picked;
        }

        static float RandRange(System.Random r, Vector2 range) =>
            range.x + (float)r.NextDouble() * Mathf.Max(0f, range.y - range.x);

        static int RandRange(System.Random r, Vector2Int range) =>
            range.x + r.Next(Mathf.Max(1, range.y - range.x + 1));

        // ------------------------------------------------------------------
        // 방향에 맞는 타일 선택 (3/4 뷰)
        // ------------------------------------------------------------------

        /// <summary>
        /// 벽칸 하나에 쓸 타일을 고른다. <b>입체감은 전적으로 이 판정에서 나온다</b> —
        /// 벽 덩어리의 어느 면이 바닥에 노출됐는지에 따라 다른 타일을 깔아야 두께가 보인다.
        ///
        /// 타일 팩이 <c>Wall_Inner</c>(사방이 벽인 내부 채움) + <c>Wall_Outer</c>(노출
        /// 방향별 8종)로 나뉘어 있어서, 노출된 이웃 방향을 그대로 타일 종류에 대응시킨다.
        ///
        /// 마주보는 두 면이 열린 경우(N+S 또는 W+E)나 세 면 이상 열린 경우에는 딱 맞는
        /// 타일이 없다. 그때는 <b>남쪽 노출을 우선</b>한다 — 3/4 뷰에서 카메라를 향한
        /// 남쪽면이 가장 크게 보이므로 그쪽을 맞추는 것이 가장 자연스럽다.
        /// </summary>
        TileBase PickWallTile(OrganicTileSetSO set, int x, int y,
                              System.Func<int, int, bool> wallAt, System.Random rng)
        {
            if (set == null) return null;

            bool openN = !wallAt(x, y + 1);
            bool openS = !wallAt(x, y - 1);
            bool openW = !wallAt(x - 1, y);
            bool openE = !wallAt(x + 1, y);

            WallTileSet w = set.walls;

            // 사방이 벽 — 덩어리 내부. 윗면만 보이는 채움 타일.
            if (!openN && !openS && !openW && !openE)
                return PickWeighted2(w.innerFill, w.exposedSouth, rng);

            // 두 면이 동시에 노출된 모서리를 먼저 잡는다. 남쪽 조합을 앞에 둬서
            // 세 면 이상 열린 칸도 남쪽면 기준으로 정리된다.
            if (openS && openW) return PickWeighted2(w.cornerSW, w.exposedSouth, rng);
            if (openS && openE) return PickWeighted2(w.cornerSE, w.exposedSouth, rng);
            if (openN && openW) return PickWeighted2(w.cornerNW, w.exposedNorth, rng);
            if (openN && openE) return PickWeighted2(w.cornerNE, w.exposedNorth, rng);

            // 한 면만 노출 (또는 마주보는 두 면 — 남쪽을 우선).
            if (openS) return PickWeighted2(w.exposedSouth, w.innerFill, rng);
            if (openN) return PickWeighted2(w.exposedNorth, w.innerFill, rng);
            if (openW) return PickWeighted2(w.exposedWest,  w.innerFill, rng);
            return PickWeighted2(w.exposedEast, w.innerFill, rng);
        }

        /// <summary>
        /// 벽에 닿은 바닥칸에 얹을 경계 타일. 벽이 어느 쪽에 있는지로 방향을 정한다.
        /// 벽에 닿지 않았거나 확률에서 떨어지면 null (그 자리엔 프롭이 놓일 수 있다).
        /// </summary>
        TileBase PickEdgeTile(DirectionalTileSet edges, int x, int y,
                              System.Func<int, int, bool> wallAt, System.Random rng)
        {
            if (!config.useTransitionEdges || edges == null) return null;

            bool wN = wallAt(x, y + 1);
            bool wS = wallAt(x, y - 1);
            bool wW = wallAt(x - 1, y);
            bool wE = wallAt(x + 1, y);
            if (!wN && !wS && !wW && !wE) return null;

            if (rng.NextDouble() >= config.transitionChance) return null;

            // 아트의 방향 규약이 코드 가정과 반대일 때를 위한 스위치.
            if (config.invertTransitionDirection)
            {
                (wN, wS) = (wS, wN);
                (wW, wE) = (wE, wW);
            }

            if (wN && wW) return PickWeighted2(edges.cornerNW, edges.north, rng);
            if (wN && wE) return PickWeighted2(edges.cornerNE, edges.north, rng);
            if (wS && wW) return PickWeighted2(edges.cornerSW, edges.south, rng);
            if (wS && wE) return PickWeighted2(edges.cornerSE, edges.south, rng);
            if (wN) return PickWeighted(edges.north, rng);
            if (wS) return PickWeighted(edges.south, rng);
            if (wW) return PickWeighted(edges.west, rng);
            return PickWeighted(edges.east, rng);
        }

        /// <summary>주 풀이 비어 있으면 대체 풀에서 뽑는다. 임포트가 불완전할 때의 안전장치.</summary>
        static TileBase PickWeighted2(WeightedTile[] primary, WeightedTile[] fallback,
                                      System.Random rng)
        {
            TileBase t = PickWeighted(primary, rng);
            return t != null ? t : PickWeighted(fallback, rng);
        }

        /// <summary>
        /// 펄린 노이즈로 벽 후보를 만들고 셀룰러 오토마타로 덩어리화.
        ///
        /// 임계값은 고정값이 아니라 청크별 "백분위"로 구한다.
        /// Mathf.PerlinNoise 는 값이 0.5 근처에 몰려 있어(실질 0.2~0.8) 고정 임계값을
        /// 쓰면 density 를 0.18 로 줘도 벽이 거의 생기지 않는다. 해당 청크 셀들의
        /// 노이즈 값을 정렬해 density 위치의 값을 임계값으로 삼으면
        /// obstacleDensity 가 "그 청크 면적 중 벽이 될 비율" 이라는 뜻이 된다.
        ///
        /// 예전에는 바이옴 단위로 묶었는데, 이제 청크마다 밀도를 따로 뽑으므로
        /// <b>청크 단위로 묶는다</b> — 청크마다 벽이 빽빽한 곳과 트인 곳이 갈린다.
        /// </summary>
        bool[] BuildObstacleMask(int seed, int w, int h,
                                 ChunkPalette[] palettes,
                                 System.Func<int, int, int> chunkIndexAt)
        {
            var mask = new bool[w * h];

            // 시드에 따라 노이즈 샘플 위치를 옮겨 서로 다른 지형이 나오게 한다.
            var offRng = new System.Random(seed);
            float offX = (float)offRng.NextDouble() * 10000f;
            float offY = (float)offRng.NextDouble() * 10000f;

            // 1) 셀별 노이즈 값 계산 + 청크별로 인덱스 모으기
            var noise = new float[w * h];
            var cellsByChunk = new Dictionary<int, List<int>>();
            int maxSmooth = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int chunk = chunkIndexAt(x, y);
                    ChunkPalette p = palettes[chunk];

                    int i = x + y * w;
                    // 노이즈는 월드 좌표로 샘플 — 청크 경계에서 무늬가 끊기지 않는다.
                    noise[i] = Mathf.PerlinNoise((x + offX) * p.noiseScale,
                                                 (y + offY) * p.noiseScale);

                    if (!cellsByChunk.TryGetValue(chunk, out var list))
                        cellsByChunk[chunk] = list = new List<int>();
                    list.Add(i);

                    if (p.smoothPasses > maxSmooth) maxSmooth = p.smoothPasses;
                }
            }

            // 2) 청크별 백분위 임계값으로 벽 후보 결정
            foreach (var kv in cellsByChunk)
            {
                ChunkPalette p = palettes[kv.Key];
                List<int> cells = kv.Value;
                if (p.obstacleDensity <= 0f || cells.Count == 0) continue;

                var values = new float[cells.Count];
                for (int k = 0; k < cells.Count; k++) values[k] = noise[cells[k]];
                System.Array.Sort(values);

                int cut = Mathf.Clamp(Mathf.RoundToInt(p.obstacleDensity * (cells.Count - 1)),
                                      0, cells.Count - 1);
                float threshold = values[cut];

                for (int k = 0; k < cells.Count; k++)
                    if (noise[cells[k]] <= threshold) mask[cells[k]] = true;
            }

            // 3) 스무딩은 맵 전체에 적용 — 청크별로 하면 경계에 직선 흔적이 남는다.
            for (int pass = 0; pass < maxSmooth; pass++)
                mask = SmoothOnce(mask, w, h);

            return mask;
        }

        /// <summary>주변 8칸 중 벽이 5개 이상이면 벽, 3개 이하면 빈칸. 고전 셀룰러 오토마타.</summary>
        static bool[] SmoothOnce(bool[] src, int w, int h)
        {
            var dst = new bool[src.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int wallCount = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            // 맵 밖은 벽으로 취급 — 외곽이 자연스럽게 닫힌다.
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) { wallCount++; continue; }
                            if (src[nx + ny * w]) wallCount++;
                        }
                    }

                    int i = x + y * w;
                    if (wallCount >= 5)      dst[i] = true;
                    else if (wallCount <= 3) dst[i] = false;
                    else                     dst[i] = src[i];
                }
            }
            return dst;
        }

        /// <summary>맵 중앙(성역)을 원형으로 비운다.</summary>
        void ApplyNexusClear(bool[] mask, int w, int h)
        {
            int cx = w / 2, cy = h / 2;
            int r = config.nexusClearRadius;
            if (r <= 0) return;

            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2) mask[x + y * w] = false;
                }
            }
        }

        /// <summary>외곽을 벽으로 두르고, 각 변 중앙에 스폰 통로를 뚫는다.</summary>
        void ApplyBorderAndGates(bool[] mask, int w, int h)
        {
            int t = config.borderWallThickness;

            if (t > 0)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (x < t || y < t || x >= w - t || y >= h - t)
                            mask[x + y * w] = true;
                    }
                }
            }

            SpawnGates.Clear();
            int half = config.spawnGateWidth / 2;
            if (config.spawnGateWidth <= 0) return;

            int mx = w / 2, my = h / 2;

            // 하 / 상 — 가로 방향으로 열린 통로
            for (int x = mx - half; x <= mx + half; x++)
            {
                if (x < 0 || x >= w) continue;
                for (int y = 0; y < Mathf.Max(t, 1); y++)         mask[x + y * w] = false;
                for (int y = h - Mathf.Max(t, 1); y < h; y++)     mask[x + y * w] = false;
            }
            // 좌 / 우
            for (int y = my - half; y <= my + half; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = 0; x < Mathf.Max(t, 1); x++)         mask[x + y * w] = false;
                for (int x = w - Mathf.Max(t, 1); x < w; x++)     mask[x + y * w] = false;
            }

            SpawnGates.Add(new Vector2Int(mx, 0));
            SpawnGates.Add(new Vector2Int(mx, h - 1));
            SpawnGates.Add(new Vector2Int(0, my));
            SpawnGates.Add(new Vector2Int(w - 1, my));
        }

        /// <summary>
        /// 각 스폰 게이트에서 성역까지 통로를 굴착한다.
        /// 웨이브 몬스터의 진입 경로를 물리적으로 보장하는 단계.
        /// </summary>
        void CarveCorridors(bool[] mask, int w, int h, int seed)
        {
            var target = new Vector2Int(w / 2, h / 2);
            int radius = Mathf.Max(1, config.corridorWidth / 2);

            for (int g = 0; g < SpawnGates.Count; g++)
            {
                var rng = new System.Random(Hash(seed, 777, g));
                Vector2 pos = SpawnGates[g];

                // 안전 상한 — 무한 루프 방지
                int maxSteps = (w + h) * 3;

                for (int step = 0; step < maxSteps; step++)
                {
                    CarveDisc(mask, w, h, Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), radius);

                    Vector2 dir = ((Vector2)target - pos);
                    if (dir.sqrMagnitude <= 1f) break;
                    dir.Normalize();

                    // 좌우 흔들림을 섞어 통로가 자연스럽게 휘게 한다.
                    if (config.corridorWobble > 0f)
                    {
                        var perp = new Vector2(-dir.y, dir.x);
                        float wob = (float)(rng.NextDouble() * 2.0 - 1.0) * config.corridorWobble;
                        dir = (dir + perp * wob).normalized;
                    }

                    pos += dir;
                    pos.x = Mathf.Clamp(pos.x, 0, w - 1);
                    pos.y = Mathf.Clamp(pos.y, 0, h - 1);
                }
            }
        }

        /// <summary>
        /// 통로를 원반 모양으로 판다.
        ///
        /// ★ <b>판 칸의 바로 위 칸도 같이 판다</b> (2026-08-18). 벽 앞면이 아래 칸을 통째로
        /// 덮어 이동 불가가 되었기 때문이다(<see cref="IsWallSkirt"/>) — 그 규칙 아래에서는
        /// 「위가 벽인 칸」이 곧 막힌 칸이라, 한 칸 높이로 판 가로 통로는 <b>전부 막힌
        /// 통로</b>가 된다. 위로 한 줄 더 파면 원반 안쪽 칸들은 전부 위가 트여 살아남는다
        /// (맨 윗줄만 치마가 되는데, 그 줄은 통행에 쓰이지 않아도 된다).
        /// </summary>
        static void CarveDisc(bool[] mask, int w, int h, int cx, int cy, int r)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > r2) continue;

                    mask[x + y * w] = false;
                    if (y + 1 < h) mask[x + (y + 1) * w] = false;   // 치마 여유 한 줄
                }
            }
        }

        /// <summary>
        /// 생성 단계에서 쓰는 <see cref="IsWallSkirt"/> — 마스크 배열판이다.
        /// 맵 <b>밖</b>은 벽으로 치지 않는다(그 자리엔 그릴 스프라이트가 없다) —
        /// 타일 종류를 고르는 <c>WallAt</c> 이 맵 밖을 벽으로 보는 것과 <b>일부러 다르다.</b>
        /// </summary>
        static bool IsSkirt(bool[] isWall, int w, int h, int i)
        {
            int y = i / w;
            return y + 1 < h && isWall[i + w];
        }

        /// <summary>
        /// 성역에서 BFS 로 도달 가능한 구역을 찾고, 나머지 빈칸은 벽으로 메운다.
        /// 결과적으로 통행 가능 영역이 항상 하나로 연결된다 → 플로우 필드가 안전해진다.
        ///
        /// ★ <b>2026-08-18 — 「벽 앞면에 덮인 칸」(치마)을 막힌 것으로 보고 돈다</b>
        /// (<see cref="IsWallSkirt"/>). 그래서 <b>여러 번 돌아야 한다</b>:
        /// 메우면 새 벽이 생기고, 새 벽은 <b>자기 아래 칸에 새 치마</b>를 만들어 그 칸이
        /// 다시 막히기 때문이다. 더 이상 메울 것이 없을 때까지 반복한다.
        ///
        /// ⚠ <b>치마 칸은 절대 벽으로 메우지 않는다.</b> 메우면 그 칸이 다시 아래 칸을
        /// 덮는 벽이 되어 <b>남쪽으로 끝없이 번진다</b>. 치마는 「벽이 아닌데 못 지나가는 칸」
        /// 으로 남겨두는 것이 정본이다 — 바닥은 그대로 그려지고 그 위를 벽 정면이 덮는다.
        /// </summary>
        void SealUnreachable(bool[] mask, int w, int h)
        {
            int start = (w / 2) + (h / 2) * w;

            // 성역 자리가 막혔으면 강제로 뚫는다 (정상적으로는 발생하지 않음).
            // 위 칸까지 뚫어야 성역 칸 자신이 치마가 되지 않는다.
            mask[start] = false;
            if (h / 2 + 1 < h) mask[start + w] = false;

            var reached = new bool[w * h];
            var queue = new Queue<int>();
            int total = 0;

            // 새로 메운 벽이 또 새 치마를 만든다 — 안정될 때까지 반복.
            // 상한은 안전장치일 뿐이다(실측상 2~3회면 끝난다).
            for (int pass = 0; pass < 8; pass++)
            {
                System.Array.Clear(reached, 0, reached.Length);
                queue.Clear();

                reached[start] = true;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    int x = i % w, y = i / w;

                    // 4방향 — 대각 이동을 허용하지 않으므로 벽 사이로 새지 않는다.
                    TryVisit(x + 1, y); TryVisit(x - 1, y);
                    TryVisit(x, y + 1); TryVisit(x, y - 1);

                    void TryVisit(int nx, int ny)
                    {
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
                        int ni = nx + ny * w;
                        if (reached[ni] || mask[ni] || IsSkirt(mask, w, h, ni)) return;
                        reached[ni] = true;
                        queue.Enqueue(ni);
                    }
                }

                int sealedNow = 0;
                for (int i = 0; i < mask.Length; i++)
                {
                    if (mask[i] || reached[i]) continue;
                    if (IsSkirt(mask, w, h, i)) continue;   // ← 치마는 벽으로 만들지 않는다
                    mask[i] = true;
                    sealedNow++;
                }

                total += sealedNow;
                if (sealedNow == 0) break;
            }

            if (total > 0)
                Debug.Log($"[MapGenerator] 고립 구역 {total}칸을 벽으로 메웠습니다.");
        }

        // ------------------------------------------------------------------
        // 유틸
        // ------------------------------------------------------------------

        static void WriteBlock(Tilemap map, BoundsInt bounds, TileBase[] tiles)
        {
            map.ClearAllTiles();
            // 셀 단위 SetTile 을 반복하면 매우 느리다. 블록 단위 한 번이 정답.
            map.SetTilesBlock(bounds, tiles);
        }

        static TileBase PickWeighted(WeightedTile[] pool, System.Random rng)
        {
            if (pool == null || pool.Length == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].tile != null) total += Mathf.Max(0f, pool[i].weight);

            if (total <= 0f) return null;

            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].tile == null) continue;
                roll -= Mathf.Max(0f, pool[i].weight);
                if (roll <= 0f) return pool[i].tile;
            }
            return pool[pool.Length - 1].tile;
        }

        /// <summary>좌표와 시드를 섞어 결정적 해시를 만든다.</summary>
        static int Hash(int seed, int x, int y)
        {
            unchecked
            {
                int h = seed * 73856093;
                h ^= x * 19349663;
                h ^= y * 83492791;
                return h;
            }
        }

        static int CountTrue(bool[] a)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (a[i]) n++;
            return n;
        }

        // ------------------------------------------------------------------
        // 시각화 — 씬 뷰에서 맵 경계와 스폰 게이트 확인
        // ------------------------------------------------------------------

        void OnDrawGizmosSelected()
        {
            if (config == null) return;

            Vector2Int size = config.MapSize;
            Vector2Int org = config.Origin;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Vector3.zero, config.nexusClearRadius);

            Gizmos.color = Color.red;
            foreach (var g in SpawnGates)
                Gizmos.DrawWireSphere(new Vector3(g.x + org.x + 0.5f, g.y + org.y + 0.5f, 0f), 1.5f);
        }
    }
}
