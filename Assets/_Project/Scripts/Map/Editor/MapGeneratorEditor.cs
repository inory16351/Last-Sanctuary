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
