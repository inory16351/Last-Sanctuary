using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LastSanctuary.Events;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// ★★ <b>사건 배경 그림 점검</b> (2026-08-25 신설 · 유저: *"이미지 넣어서 줄테니까 연동해"*).
    ///
    /// 표의 <c>event_bg</c> 키마다 <c>Resources/EventBg/</c> 에 그림이 있는지 확인한다.
    ///
    /// <b>왜 이 메뉴가 필요한가</b> — 그림이 없거나 임포트 설정이 틀리면
    /// <see cref="Resources.Load{T}"/> 가 <b>조용히 null</b> 을 돌려주고, 게임에서는
    /// «배경이 그냥 안 나온다» 로만 보인다(84-8절 ②가 히스톤에서 겪은 그 함정).
    /// <b>그림을 넣은 직후에 여기서 한 번 보면</b> 무엇이 비었고 무엇이 잘못됐는지 바로 안다.
    ///
    /// ⚠ 특히 <b><c>textureType</c> 이 Sprite(8) 여야 한다</b> — PNG 를 그냥 끌어다 놓으면
    ///   프로젝트 설정에 따라 Default 로 들어올 수 있고, 그러면 <b>파일은 있는데 안 나온다</b>.
    ///   이 메뉴가 그것을 <b>따로 세어</b> 알려준다.
    /// </summary>
    static class EventBgMenu
    {
        const string Folder = "Assets/_Project/Resources/EventBg";

        [MenuItem("LastSanctuary/사건/배경 그림 점검", priority = 300)]
        static void Check()
        {
            // ── 표가 요구하는 키 모으기 ────────────────────────────────
            var wanted = new SortedDictionary<string, List<string>>();
            foreach (EventDefinitionSO def in Resources.LoadAll<EventDefinitionSO>("Events"))
            {
                string key = (def.eventBg ?? "").Trim();
                if (key.Length == 0) continue;
                if (!wanted.TryGetValue(key, out var names))
                    wanted[key] = names = new List<string>();
                names.Add(def.DisplayName);
            }

            if (wanted.Count == 0)
            {
                Debug.LogWarning("[사건 배경] 이벤트 에셋을 찾지 못했습니다 " +
                                 "(Resources/Events 확인).");
                return;
            }

            // ── 실제로 있는 그림 ──────────────────────────────────────
            var have = new Dictionary<string, string>();     // 키 → 에셋 경로
            if (Directory.Exists(Folder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    have[Path.GetFileNameWithoutExtension(path)] = path;
                }
            }

            var lines = new List<string>();
            int ok = 0, missing = 0, badType = 0;

            foreach (var pair in wanted)
            {
                string key = pair.Key;
                int users = pair.Value.Count;

                if (!have.TryGetValue(key, out string path))
                {
                    missing++;
                    lines.Add($"  ✗ {key,-18} 없음            (사건 {users}개가 기다린다)");
                    continue;
                }

                // ⚠ 파일이 있어도 Sprite 가 아니면 Resources.Load<Sprite> 는 null 이다.
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                bool isSprite = importer != null && importer.textureType == TextureImporterType.Sprite;

                if (!isSprite)
                {
                    badType++;
                    lines.Add($"  ⚠ {key,-18} <b>Sprite 가 아니다</b> — 임포트 설정을 " +
                              $"Sprite(2D and UI) 로 바꿀 것  ({Path.GetFileName(path)})");
                    continue;
                }

                // 마지막 확인 — 실제로 읽히는가(이것이 게임이 하는 것과 같은 길이다)
                if (Resources.Load<Sprite>($"EventBg/{key}") == null)
                {
                    badType++;
                    lines.Add($"  ⚠ {key,-18} 파일은 있는데 <b>Resources.Load 가 못 읽는다</b>");
                    continue;
                }

                ok++;
                lines.Add($"  ✓ {key,-18} 사건 {users}개");
            }

            // ── 표가 안 쓰는 그림 ─────────────────────────────────────
            var unused = have.Keys.Where(k => !wanted.ContainsKey(k)).OrderBy(k => k).ToList();

            int totalEvents = wanted.Sum(p => p.Value.Count);
            int coveredEvents = wanted.Where(p => have.ContainsKey(p.Key)).Sum(p => p.Value.Count);

            string report =
                $"[사건 배경] 키 {wanted.Count}종 — 있음 {ok} · 없음 {missing} · 설정 틀림 {badType}\n" +
                $"  배경이 붙는 사건 {coveredEvents}/{totalEvents}개\n" +
                string.Join("\n", lines);

            if (unused.Count > 0)
                report += $"\n  · 표가 안 쓰는 그림 {unused.Count}장: {string.Join(", ", unused)}";

            if (badType > 0) Debug.LogWarning(report);
            else if (missing > 0) Debug.Log(report);
            else Debug.Log(report + "\n  ✓ 전부 붙었습니다.");
        }

        /// <summary>
        /// ★ <b>넣자마자 고쳐 준다</b> — 끌어다 놓은 PNG 가 Default 로 들어왔으면 Sprite 로 바꾼다.
        /// ⚠ 손으로 하나씩 임포트 설정을 바꾸는 일은 <b>반드시 하나를 빠뜨린다</b>.
        /// </summary>
        [MenuItem("LastSanctuary/사건/배경 그림 임포트 설정 고치기", priority = 301)]
        static void FixImport()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogWarning($"[사건 배경] 폴더가 없습니다: {Folder}");
                return;
            }

            int fixedCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureType == TextureImporterType.Sprite) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                fixedCount++;
                Debug.Log($"[사건 배경] Sprite 로 바꿨습니다: {Path.GetFileName(path)}");
            }

            Debug.Log(fixedCount == 0
                ? "[사건 배경] 고칠 것이 없었습니다 — 전부 이미 Sprite 입니다."
                : $"[사건 배경] {fixedCount}장을 Sprite 로 바꿨습니다.");
        }
    }
}
