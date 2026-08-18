using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using LastSanctuary.Map;

namespace LastSanctuary.MapEditorTools
{
    /// <summary>
    /// MapGenerator 인스펙터. 맵 크기를 여기서 바로 편집하고 생성까지 실행한다.
    /// 크기 값 자체는 Config(ScriptableObject)에 저장되므로 데이터 출처는 하나로 유지된다.
    /// </summary>
    [CustomEditor(typeof(MapGenerator))]
    public class MapGeneratorEditor : Editor
    {
        /// <summary>
        /// 오브젝트를 선택하지 않고도 맵을 다시 만들 수 있는 메뉴.
        /// 타일셋을 다시 읽은 뒤에는 씬에 남아 있는 타일 데이터가 옛 타일을 가리키므로
        /// 반드시 한 번 재생성해야 한다.
        /// </summary>
        [MenuItem("LastSanctuary/맵/새 시드로 맵 생성", priority = 201)]
        static void RegenerateWithNewSeed()
        {
            var generator = Object.FindFirstObjectByType<MapGenerator>();
            if (generator == null)
            {
                Debug.LogError("[MapGenerator] 씬에서 MapGenerator 를 찾지 못했습니다.");
                return;
            }

            MapGenerationConfigSO config = generator.Config;
            if (config == null)
            {
                Debug.LogError("[MapGenerator] Config 가 연결되지 않았습니다.", generator);
                return;
            }

            int newSeed = Random.Range(int.MinValue, int.MaxValue);
            Undo.RecordObject(config, "Randomize Map Seed");
            config.seed = newSeed;
            EditorUtility.SetDirty(config);

            generator.Generate(newSeed);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        }

        /// <summary>
        /// 시드를 바꾸지 않고 <b>지금 시드 그대로</b> 다시 만든다.
        ///
        /// 왜 따로 필요했나 (2026-08-18) — 벽 앞면이 덮은 칸을 이동 불가로 바꾸면서
        /// (<see cref="MapGenerator.IsWallSkirt"/>) <b>통로 굴착·연결성 검사가 함께 바뀌었다.</b>
        /// 이미 저장된 맵은 그 규칙 없이 만들어진 것이라 한 칸 높이 통로가 통째로 막혀 있을 수
        /// 있다. 지형을 통째로 갈아엎지 않고 <b>같은 시드로</b> 다시 돌리는 길이 필요했다.
        /// </summary>
        [MenuItem("LastSanctuary/맵/현재 시드로 맵 다시 생성", priority = 200)]
        static void RegenerateWithSameSeed()
        {
            if (!Resolve(out MapGenerator generator, out MapGenerationConfigSO config)) return;

            generator.Generate(config.seed);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        }

        /// <summary>
        /// <b>지금 씬에 깔려 있는 맵</b>에 벽 앞면(치마) 규칙을 적용하면 어떻게 되는지만
        /// 보고한다 — <b>아무것도 바꾸지 않는다.</b> 다시 생성할지 판단하는 근거다.
        ///
        /// ★★ <b>봐야 할 것은 마지막 줄 하나뿐이다</b> — <c>못 가는 칸</c>.
        ///
        /// ⚠ 처음에는 「넥서스에서 도달 : A → B」의 <b>차이</b>를 「고립되는 칸」이라고 찍었는데,
        ///   그 차이의 대부분은 <b>치마 칸 자신</b>이다(원래 못 가게 만든 칸이므로 줄어드는 것이
        ///   정상이다). 그 숫자를 보고 "3,400칸이 끊겼다" 로 읽으면 멀쩡한 맵을 계속 다시 만들게
        ///   된다 — 2026-08-18 에 실제로 그렇게 읽을 뻔했다.
        ///
        /// <b>진짜 판정은 「통행 가능(치마 적용) == 넥서스에서 도달(치마 적용)」</b> 이다.
        /// 둘이 같으면 <b>갈 수 있는 칸이 하나도 안 끊겼다</b>는 뜻이다.
        /// </summary>
        [MenuItem("LastSanctuary/맵/벽 앞면 이동불가 영향 점검 (변경 없음)", priority = 202)]
        static void ReportSkirtImpact()
        {
            if (!Resolve(out MapGenerator generator, out MapGenerationConfigSO config)) return;

            Vector2Int size = config.MapSize;
            Vector2Int org = config.Origin;
            int w = size.x, h = size.y;

            Tilemap obstacles = generator.ObstacleTilemap;
            if (obstacles == null)
            {
                Debug.LogError("[맵 점검] 장애물 타일맵이 연결되지 않았습니다.", generator);
                return;
            }

            var isWall = new bool[w * h];
            int walls = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (obstacles.HasTile(new Vector3Int(x + org.x, y + org.y, 0)))
                    {
                        isWall[x + y * w] = true;
                        walls++;
                    }

            bool Skirt(int i) => i / w + 1 < h && isWall[i + w];

            int before = 0, after = 0;
            for (int i = 0; i < isWall.Length; i++)
            {
                if (!isWall[i]) before++;
                if (!isWall[i] && !Skirt(i)) after++;
            }

            // 넥서스에서 4방향 BFS — 규칙 적용 전/후로 각각 센다.
            int start = (w / 2) + (h / 2) * w;
            int reachBefore = Flood(isWall, w, h, start, false);
            int reachAfter  = Flood(isWall, w, h, start, true);

            // ★ 이것이 진짜 「끊긴 칸」이다 — 갈 수 있어야 하는데(치마도 아닌데) 못 가는 칸.
            int stranded = after - reachAfter;

            Debug.Log($"[맵 점검] {w}x{h} · 벽 {walls}칸\n" +
                      $"  통행 가능 : {before} → {after} " +
                      $"(줄어든 {before - after}칸이 벽 앞면이다 — 정상)\n" +
                      $"  넥서스에서 도달 : {reachBefore} → {reachAfter}\n" +
                      $"  ★ 못 가는 칸 : {stranded}  " +
                      (stranded == 0
                          ? "→ 갈 수 있는 칸이 하나도 안 끊겼다. 다시 생성할 필요 없다."
                          : "→ 한 칸 높이 통로가 끊겼다. 「현재 시드로 맵 다시 생성」을 실행할 것."),
                      generator);
        }

        /// <summary>넥서스에서 4방향으로 퍼진 칸 수. <paramref name="useSkirt"/> 면 벽 앞면도 막힌 것으로 본다.</summary>
        static int Flood(bool[] isWall, int w, int h, int start, bool useSkirt)
        {
            bool Blocked(int i) => isWall[i] || (useSkirt && i / w + 1 < h && isWall[i + w]);
            if (Blocked(start)) return 0;

            var seen = new bool[isWall.Length];
            var queue = new System.Collections.Generic.Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);
            int n = 0;

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                n++;
                int x = i % w, y = i / w;

                Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);

                void Visit(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
                    int ni = nx + ny * w;
                    if (seen[ni] || Blocked(ni)) return;
                    seen[ni] = true;
                    queue.Enqueue(ni);
                }
            }
            return n;
        }

        /// <summary>두 메뉴가 공유하는 씬 조회 — 없으면 이유를 찍고 false.</summary>
        static bool Resolve(out MapGenerator generator, out MapGenerationConfigSO config)
        {
            generator = Object.FindFirstObjectByType<MapGenerator>();
            config = null;

            if (generator == null)
            {
                Debug.LogError("[MapGenerator] 씬에서 MapGenerator 를 찾지 못했습니다.");
                return false;
            }

            config = generator.Config;
            if (config == null)
            {
                Debug.LogError("[MapGenerator] Config 가 연결되지 않았습니다.", generator);
                return false;
            }
            return true;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var generator = (MapGenerator)target;
            var config = generator.Config;

            EditorGUILayout.Space(10);

            if (config == null)
            {
                EditorGUILayout.HelpBox("Config 를 연결하면 맵 크기 설정과 생성 버튼이 나타납니다.",
                                        MessageType.Info);
                return;
            }

            DrawSizeSection(config);
            EditorGUILayout.Space(6);
            DrawBoundsSection(generator, config);
            EditorGUILayout.Space(6);
            DrawGenerateSection(generator, config);
        }

        // ------------------------------------------------------------------ 맵 크기

        void DrawSizeSection(MapGenerationConfigSO config)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("맵 크기", EditorStyles.boldLabel);

                var cfg = new SerializedObject(config);
                var pMode   = cfg.FindProperty("sizeMode");
                var pChunks = cfg.FindProperty("chunkCount");
                var pTiles  = cfg.FindProperty("mapSizeTiles");
                var pSize   = cfg.FindProperty("chunkSize");

                EditorGUILayout.PropertyField(pMode, new GUIContent("지정 방식"));

                if (config.sizeMode == MapSizeMode.Chunks)
                    EditorGUILayout.PropertyField(pChunks, new GUIContent("청크 개수"));
                else
                    EditorGUILayout.PropertyField(pTiles, new GUIContent("전체 타일 수"));

                EditorGUILayout.PropertyField(pSize, new GUIContent("청크 크기"));

                if (cfg.ApplyModifiedProperties())
                    EditorUtility.SetDirty(config);

                Vector2Int actual = config.MapSize;
                Vector2Int cc = config.ChunkCount;

                EditorGUILayout.LabelField("실제 크기",
                    $"{actual.x} x {actual.y} 타일   ({cc.x}x{cc.y} 청크 · {actual.x * actual.y}칸)");

                if (config.IsCropped)
                {
                    Vector2Int covered = config.CoveredSize;
                    Vector2Int crop = config.CroppedAmount;
                    EditorGUILayout.HelpBox(
                        $"청크 {cc.x}x{cc.y} = {covered.x} x {covered.y} 를 생성한 뒤 " +
                        $"경계에 걸친 {crop.x} x {crop.y} 타일을 잘라내 " +
                        $"정확히 {actual.x} x {actual.y} 로 만듭니다.",
                        MessageType.Info);
                }

                // 흔히 쓰는 크기 프리셋
                EditorGUILayout.LabelField("프리셋", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SizeButton("소형 60x40", 60, 40, config)) { }
                    if (SizeButton("중형 100x60", 100, 60, config)) { }
                    if (SizeButton("대형 160x100", 160, 100, config)) { }
                }
            }
        }

        static bool SizeButton(string label, int w, int h, MapGenerationConfigSO config)
        {
            if (!GUILayout.Button(label)) return false;

            Undo.RecordObject(config, "Set Map Size");
            config.sizeMode = MapSizeMode.Tiles;
            config.mapSizeTiles = new Vector2Int(w, h);
            EditorUtility.SetDirty(config);
            return true;
        }

        // ------------------------------------------------------------------ 경계 동기화

        void DrawBoundsSection(MapGenerator generator, MapGenerationConfigSO config)
        {
            Collider2D bounds = generator.BoundsShape;
            if (bounds == null)
            {
                EditorGUILayout.HelpBox(
                    "Bounds Shape 에 MapBounds 를 넣으면 생성할 때마다 카메라 경계가 " +
                    "맵 크기에 자동으로 맞춰집니다.", MessageType.Warning);
                return;
            }

            Vector2Int map = config.MapSize;
            Bounds b = bounds.bounds;   // 월드 공간 실측 — 스케일/점 방식 모두 반영
            bool matches = Mathf.Approximately(Mathf.Round(b.size.x), map.x)
                        && Mathf.Approximately(Mathf.Round(b.size.y), map.y);

            string state = matches ? "일치" : "불일치";
            EditorGUILayout.LabelField("카메라 경계",
                $"{b.size.x:0.#} x {b.size.y:0.#}  ({state} · 맵 {map.x} x {map.y})");

            if (!generator.AutoResizeBounds)
            {
                EditorGUILayout.HelpBox(
                    "Auto Resize Bounds 가 꺼져 있어 생성 시 경계가 자동으로 맞춰지지 않습니다.",
                    MessageType.Info);
            }

            if (!matches)
            {
                EditorGUILayout.HelpBox(
                    "경계가 맵과 다릅니다. 이대로면 카메라가 맵 끝까지 못 가거나 맵 밖을 봅니다.",
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(matches))
            {
                if (GUILayout.Button($"경계를 {map.x} x {map.y} 로 지금 맞추기"))
                {
                    RegisterBoundsUndo(bounds, "Sync Map Bounds");
                    generator.SyncBoundsToMap();
                    EditorSceneManager.MarkSceneDirty(bounds.gameObject.scene);
                }
            }
        }

        static void RegisterBoundsUndo(Collider2D bounds, string label)
        {
            Undo.RegisterCompleteObjectUndo(
                new Object[] { bounds, bounds.transform, bounds.gameObject }, label);
        }

        // ------------------------------------------------------------------ 생성

        void DrawGenerateSection(MapGenerator generator, MapGenerationConfigSO config)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 시드로 생성", GUILayout.Height(28)))
                    RunGenerate(generator, config.seed);

                if (GUILayout.Button("새 시드로 생성", GUILayout.Height(28)))
                {
                    int newSeed = Random.Range(int.MinValue, int.MaxValue);
                    Undo.RecordObject(config, "Randomize Map Seed");
                    config.seed = newSeed;
                    EditorUtility.SetDirty(config);
                    RunGenerate(generator, newSeed);
                }
            }

            if (GUILayout.Button("전부 지우기"))
            {
                RegisterUndo(generator, "Clear Map");
                generator.ClearAll();
                MarkDirty(generator);
            }

            EditorGUILayout.HelpBox(
                "같은 시드는 항상 같은 맵을 만듭니다. 마음에 드는 맵이 나오면 " +
                "Config 의 seed 값을 기록해두세요.  Ctrl+Z 로 되돌릴 수 있습니다.",
                MessageType.None);
        }

        void RunGenerate(MapGenerator generator, int seed)
        {
            RegisterUndo(generator, "Generate Map");
            generator.Generate(seed);
            MarkDirty(generator);
        }

        /// <summary>
        /// 타일맵의 타일 데이터까지 되돌릴 수 있도록 세 타일맵 모두 등록한다.
        /// 직렬화 프로퍼티로 접근해야 private 필드를 읽을 수 있다.
        /// </summary>
        void RegisterUndo(MapGenerator generator, string label)
        {
            var objects = new System.Collections.Generic.List<Object> { generator };

            foreach (string prop in new[] { "groundTilemap", "decoTilemap", "obstacleTilemap" })
            {
                var p = serializedObject.FindProperty(prop);
                if (p != null && p.objectReferenceValue is Tilemap tm)
                {
                    objects.Add(tm);
                    objects.Add(tm.gameObject);
                }
            }

            // 생성 시 경계 콜라이더도 함께 바뀌므로 같이 등록해야 Ctrl+Z 가 온전히 되돌린다.
            Collider2D bounds = generator.BoundsShape;
            if (bounds != null)
            {
                objects.Add(bounds);
                objects.Add(bounds.transform);
                objects.Add(bounds.gameObject);
            }

            Undo.RegisterCompleteObjectUndo(objects.ToArray(), label);
        }

        static void MarkDirty(MapGenerator generator)
        {
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        }
    }
}
