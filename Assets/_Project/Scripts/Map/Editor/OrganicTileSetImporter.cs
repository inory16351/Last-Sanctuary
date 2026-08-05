using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using LastSanctuary.Map;

namespace LastSanctuary.MapEditorTools
{
    /// <summary>
    /// <c>TileMapCatalog.json</c> 을 읽어 <see cref="OrganicTileSetSO"/> 를 채우고,
    /// 같은 패스에서 타일 에셋의 끊어진 스프라이트 참조까지 복구한다.
    ///
    /// <b>카탈로그를 유일한 기준으로 삼는다.</b> 타일 팩이 갱신되면 시트 구성·타일 이름
    /// 규칙이 통째로 바뀌는데(실제로 벽 시트가 obstacles 1장 → Wall_Inner/Wall_Outer 2장으로
    /// 바뀌었다), 파일 이름에서 용도를 유추하면 그때마다 코드가 깨진다. 카탈로그에는
    /// 시트·category·rule 이 전부 적혀 있으므로 그것만 믿는다.
    ///
    /// 타일 id ↔ 에셋 파일명 규칙이 시트마다 다른 것도 여기서 흡수한다.
    ///   · 벽:            id <c>Wall_Outer_North_01</c> = 에셋 파일명 그대로
    ///   · 그 외:         id <c>terrain_07</c> → 에셋 <c>OrganicTerrain_20px_07</c>
    /// 두 경우 모두 <b>스프라이트 이름 = 에셋 파일명</b> 이라서 복구는 이름으로 이어붙인다.
    /// </summary>
    public static class OrganicTileSetImporter
    {
        const string DefaultAssetPath = "Assets/_Project/Data/Map/OrganicTileSet.asset";
        const string CatalogFileName = "TileMapCatalog.json";

        [MenuItem("LastSanctuary/맵/OrganicTilemap 타일셋 다시 읽기", priority = 200)]
        public static void ReimportMenu()
        {
            OrganicTileSetSO set = LoadOrCreate();
            if (set == null) return;

            if (Reimport(set))
            {
                Selection.activeObject = set;
                EditorGUIUtility.PingObject(set);
            }
        }

        static OrganicTileSetSO LoadOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<OrganicTileSetSO>(DefaultAssetPath);
            if (existing != null) return existing;

            string dir = Path.GetDirectoryName(DefaultAssetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Debug.LogError($"[TileSet] 폴더가 없습니다: {dir}");
                return null;
            }

            var created = ScriptableObject.CreateInstance<OrganicTileSetSO>();
            AssetDatabase.CreateAsset(created, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TileSet] 타일셋 에셋을 새로 만들었습니다: {DefaultAssetPath}");
            return created;
        }

        // ------------------------------------------------------------------

        public static bool Reimport(OrganicTileSetSO set)
        {
            if (!ResolveFolders(set, out string catalogPath)) return false;

            Catalog catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(catalogPath));
            if (catalog?.sheets == null || catalog.sheets.Length == 0)
            {
                Debug.LogError($"[TileSet] 카탈로그를 해석하지 못했습니다: {catalogPath}");
                return false;
            }

            var buckets = new Dictionary<string, List<WeightedTile>>();
            int loaded = 0, repaired = 0, missingTile = 0, missingSprite = 0;

            foreach (Sheet sheet in catalog.sheets)
            {
                if (sheet?.tiles == null) continue;

                string sheetPrefix = Path.GetFileNameWithoutExtension(sheet.sheet);
                string pngPath = $"{set.sourceFolder.TrimEnd('/')}/{sheet.sheet}";
                Dictionary<string, Sprite> sprites = LoadSheetSprites(pngPath);

                foreach (TileEntry entry in sheet.tiles)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id)) continue;

                    if (!TryResolveTile(set.tilesFolder, sheetPrefix, entry.id,
                                        out Tile tile, out string tileName))
                    {
                        if (missingTile < 5)
                            Debug.LogWarning($"[TileSet] 타일 에셋 없음: {entry.id} " +
                                             $"(시트 {sheetPrefix})");
                        missingTile++;
                        continue;
                    }

                    // 스프라이트 복구 — 팩의 .asset 은 만든 쪽 프로젝트의 PNG GUID 를
                    // 가리키므로 이 프로젝트로 오면 참조가 끊긴다(에러 없이 안 보이기만 한다).
                    if (sprites != null && sprites.TryGetValue(tileName, out Sprite sprite))
                    {
                        if (tile.sprite != sprite)
                        {
                            tile.sprite = sprite;
                            EditorUtility.SetDirty(tile);
                            repaired++;
                        }
                    }
                    else
                    {
                        if (missingSprite < 5)
                            Debug.LogWarning($"[TileSet] '{tileName}' 스프라이트를 " +
                                             $"{sheet.sheet} 에서 못 찾았습니다.");
                        missingSprite++;
                    }

                    string key = BucketKey(entry.category, entry.rule);
                    if (key == null) continue;

                    if (!buckets.TryGetValue(key, out List<WeightedTile> list))
                        buckets[key] = list = new List<WeightedTile>();

                    list.Add(new WeightedTile
                    {
                        tile = tile,
                        weight = entry.weight > 0f ? entry.weight : 1f,
                    });
                    loaded++;
                }
            }

            Undo.RecordObject(set, "Reimport Organic Tile Set");

            set.ground        = Take(buckets, "ground");
            set.groundCracked = Take(buckets, "ground_cracked");
            set.props         = Take(buckets, "prop");

            set.walls = new WallTileSet
            {
                innerFill    = Take(buckets, "wall_inner"),
                exposedNorth = Take(buckets, "wall_north"),
                exposedSouth = Take(buckets, "wall_south"),
                exposedWest  = Take(buckets, "wall_west"),
                exposedEast  = Take(buckets, "wall_east"),
                cornerNW     = Take(buckets, "wall_nw"),
                cornerNE     = Take(buckets, "wall_ne"),
                cornerSW     = Take(buckets, "wall_sw"),
                cornerSE     = Take(buckets, "wall_se"),
            };

            set.bloodEdge = TakeDirectional(buckets, "blood_edge");
            set.chasmEdge = TakeDirectional(buckets, "chasm_edge");

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            WallTileSet w = set.walls;
            string problems = "";
            if (missingTile > 0) problems += $" · 타일 누락 {missingTile}";
            if (missingSprite > 0) problems += $" · 스프라이트 누락 {missingSprite}";

            Debug.Log($"[TileSet] {loaded}개 분류 · 스프라이트 복구 {repaired}개{problems}\n" +
                      $"  바닥 {Len(set.ground)} / 갈라진바닥 {Len(set.groundCracked)} / 프롭 {Len(set.props)}\n" +
                      $"  벽 내부채움 {Len(w.innerFill)} · 노출 N{Len(w.exposedNorth)} " +
                      $"S{Len(w.exposedSouth)} W{Len(w.exposedWest)} E{Len(w.exposedEast)} " +
                      $"· 모서리 NW{Len(w.cornerNW)} NE{Len(w.cornerNE)} " +
                      $"SW{Len(w.cornerSW)} SE{Len(w.cornerSE)}\n" +
                      $"  전이 blood {(set.bloodEdge.HasAny ? "O" : "X")} / " +
                      $"chasm {(set.chasmEdge.HasAny ? "O" : "X")}", set);

            if (!set.IsUsable)
                Debug.LogError("[TileSet] 바닥 또는 벽 타일이 비어 있어 맵을 생성할 수 없습니다.", set);

            LinkIntoConfigs(set);
            return true;
        }

        /// <summary>
        /// 카탈로그 위치를 확정한다. 지정된 폴더에 없으면 프로젝트 전체를 뒤져 찾고
        /// 타일셋의 경로 필드를 고쳐 준다 — 폴더를 옮기거나 다시 임포트해도 동작하게.
        /// </summary>
        static bool ResolveFolders(OrganicTileSetSO set, out string catalogPath)
        {
            catalogPath = $"{set.sourceFolder.TrimEnd('/')}/{CatalogFileName}";
            if (File.Exists(catalogPath) && HasTiles(set.tilesFolder)) return true;

            string[] found = Directory.GetFiles("Assets", CatalogFileName,
                                                SearchOption.AllDirectories);
            if (found.Length == 0)
            {
                Debug.LogError($"[TileSet] 프로젝트에서 {CatalogFileName} 을 찾지 못했습니다.");
                return false;
            }

            catalogPath = found[0].Replace('\\', '/');
            string folder = Path.GetDirectoryName(catalogPath).Replace('\\', '/');

            Undo.RecordObject(set, "Fix Tile Set Folders");
            set.sourceFolder = folder;
            set.tilesFolder = folder + "/Tiles";
            EditorUtility.SetDirty(set);

            Debug.Log($"[TileSet] 카탈로그 위치가 바뀌어 경로를 갱신했습니다 → {folder}", set);
            if (found.Length > 1)
                Debug.LogWarning($"[TileSet] {CatalogFileName} 이 {found.Length}곳에 있습니다. " +
                                 $"'{catalogPath}' 를 사용합니다.");
            return true;
        }

        static bool HasTiles(string tilesFolder) =>
            !string.IsNullOrEmpty(tilesFolder) && Directory.Exists(tilesFolder) &&
            Directory.GetFiles(tilesFolder, "*.asset").Length > 0;

        /// <summary>
        /// 카탈로그의 타일 id 로 실제 Tile 에셋을 찾는다.
        /// 벽 시트는 id 가 곧 파일명이고, 나머지는 "시트이름_번호" 규칙이다.
        /// </summary>
        static bool TryResolveTile(string tilesFolder, string sheetPrefix, string id,
                                   out Tile tile, out string tileName)
        {
            tilesFolder = tilesFolder.TrimEnd('/');

            // 1) id 가 그대로 파일명인 경우 (Wall_Outer_North_01 등)
            tile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesFolder}/{id}.asset");
            if (tile != null) { tileName = id; return true; }

            // 2) "시트이름_번호" 로 조립 (terrain_07 → OrganicTerrain_20px_07)
            int underscore = id.LastIndexOf('_');
            if (underscore >= 0)
            {
                tileName = $"{sheetPrefix}_{id.Substring(underscore + 1)}";
                tile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesFolder}/{tileName}.asset");
                if (tile != null) return true;
            }

            tileName = id;
            return false;
        }

        static Dictionary<string, Sprite> LoadSheetSprites(string pngPath)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(pngPath);
            if (all == null || all.Length == 0)
            {
                Debug.LogError($"[TileSet] 시트를 찾지 못했습니다: {pngPath}");
                return null;
            }

            var map = new Dictionary<string, Sprite>();
            foreach (Object o in all)
                if (o is Sprite s) map[s.name] = s;

            if (map.Count == 0)
                Debug.LogError($"[TileSet] {pngPath} 에 슬라이스된 스프라이트가 없습니다. " +
                               "Sprite Mode 가 Multiple 인지 확인하세요.");
            return map;
        }

        /// <summary>
        /// category(+rule) → 타일셋의 어느 칸에 넣을지.
        /// 벽은 rule 의 <c>exposed_*</c> 토큰이 방향을 알려준다
        /// (예: <c>"solid_collider; exposed_northwest_corner"</c>).
        /// </summary>
        static string BucketKey(string category, string rule)
        {
            if (string.IsNullOrEmpty(category)) return null;

            switch (category)
            {
                case "ground":
                case "ground_cracked":
                    return category;

                case "wall_inner":
                    return "wall_inner";

                case "wall_outer":
                    return WallKeyFromRule(rule);

                case "blood_edge":
                case "chasm_edge":
                    return string.IsNullOrEmpty(rule) ? null : category + ":" + rule;

                default:
                    // root/bone/egg_sac/spike/tentacle/fungus/pit 등은 전부 프롭이다.
                    return "prop";
            }
        }

        static string WallKeyFromRule(string rule)
        {
            if (string.IsNullOrEmpty(rule)) return null;

            foreach (string raw in rule.Split(';'))
            {
                string token = raw.Trim();
                if (!token.StartsWith("exposed_")) continue;

                switch (token.Substring("exposed_".Length))
                {
                    case "north": return "wall_north";
                    case "south": return "wall_south";
                    case "west":  return "wall_west";
                    case "east":  return "wall_east";
                    case "northwest_corner": return "wall_nw";
                    case "northeast_corner": return "wall_ne";
                    case "southwest_corner": return "wall_sw";
                    case "southeast_corner": return "wall_se";
                }
            }
            return null;
        }

        static WeightedTile[] Take(Dictionary<string, List<WeightedTile>> buckets, string key) =>
            buckets.TryGetValue(key, out List<WeightedTile> list)
                ? list.ToArray()
                : System.Array.Empty<WeightedTile>();

        static DirectionalTileSet TakeDirectional(Dictionary<string, List<WeightedTile>> buckets,
                                                  string family) =>
            new DirectionalTileSet
            {
                north    = Take(buckets, family + ":north"),
                south    = Take(buckets, family + ":south"),
                west     = Take(buckets, family + ":west"),
                east     = Take(buckets, family + ":east"),
                cornerNW = Take(buckets, family + ":nw_corner"),
                cornerNE = Take(buckets, family + ":ne_corner"),
                cornerSW = Take(buckets, family + ":sw_corner"),
                cornerSE = Take(buckets, family + ":se_corner"),
            };

        static int Len(WeightedTile[] a) => a != null ? a.Length : 0;

        /// <summary>
        /// 맵 생성 Config 에 타일셋을 연결한다. 에셋 참조는 MCP 로 채울 수 없어서
        /// (진행상황 8절 1항) 여기서 처리한다.
        /// </summary>
        static void LinkIntoConfigs(OrganicTileSetSO set)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(MapGenerationConfigSO)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<MapGenerationConfigSO>(path);
                if (config == null || config.tileSet == set) continue;

                Undo.RecordObject(config, "Link Tile Set");
                config.tileSet = set;
                EditorUtility.SetDirty(config);
                Debug.Log($"[TileSet] {config.name} 에 타일셋을 연결했습니다.", config);
            }
            AssetDatabase.SaveAssets();
        }

        // ---- TileMapCatalog.json 스키마 ----------------------------------

        [System.Serializable]
        class Catalog
        {
            public int tileSizePx;
            public int pixelsPerUnit;
            public Sheet[] sheets;
        }

        [System.Serializable]
        class Sheet
        {
            public string sheet;
            public TileEntry[] tiles;
        }

        [System.Serializable]
        class TileEntry
        {
            public string id;
            public int[] cell;
            public string category;
            public float weight;
            public string rule;
        }
    }
}
