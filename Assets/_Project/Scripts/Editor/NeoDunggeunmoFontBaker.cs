using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// 네오 둥근모(neodgm.ttf)로 TMP 폰트 에셋을 굽고, 열려 있는 씬의 모든 TMP 텍스트에
    /// 그 폰트를 적용한다.
    ///
    /// 왜 에디터 스크립트로 하는가: MCP 의 <c>update_component</c> 로는 에셋 참조 필드를
    /// 제대로 채울 수 없다(8절 1항). 폰트 에셋 지정은 에셋 참조이므로, 에디터 쪽에서
    /// 한 번에 굽고 붙이는 편이 확실하다. 텍스트를 새로 추가한 뒤 메뉴를 다시 실행하면
    /// 새 텍스트에도 폰트가 적용된다(이미 구운 에셋은 재사용한다).
    /// </summary>
    public static class NeoDunggeunmoFontBaker
    {
        const string SourceFontPath = "Assets/TextMesh Pro/Fonts/neodgm.ttf";

        /// ★ <b>반드시 <c>Resources/Fonts</c> 여야 한다</b> (준수사항 §10 H-4, 2026-08-13 수정).
        ///
        /// 예전 값은 <c>Assets/_Project/Art/Fonts</c> 였다. 그 경로에는 <b>에셋이 없었으므로</b>
        /// 이 메뉴를 실행하면 <c>LoadOrBake</c> 가 "없다"고 판단해 <b>폰트를 새로 굽고</b>,
        /// 이어서 <c>ApplyToLoadedScenes</c> 가 씬의 TMP 텍스트 <b>전부를 그 새 에셋으로
        /// 갈아끼웠다.</b> 실제로 2026-08-13 에 이 메뉴를 한 번 눌러 씬의 폰트 참조 234개가
        /// 통째로 바뀌었다(38MB 씬이 전면 재작성됐다).
        ///
        /// 프로젝트의 정본 폰트는 <c>Resources/Fonts/NeoDunggeunmo SDF.asset</c> 다 —
        /// 런타임에 <c>Resources.Load</c> 로도 읽는 자리다. 여기를 가리키면
        /// <c>LoadOrBake</c> 가 <b>기존 에셋을 그대로 재사용</b>하므로 이 사고가 재발하지 않고,
        /// 새로 추가한 텍스트에만 같은 폰트가 붙는다(이 메뉴의 원래 목적).
        const string OutputFolder = "Assets/_Project/Resources/Fonts";
        const string OutputPath = OutputFolder + "/NeoDunggeunmo SDF.asset";

        // 네오 둥근모는 16px 기준으로 만든 픽셀 폰트다. 그 정수배(32)로 구워야
        // 화면에서 16/32/48 처럼 정수배 크기로 쓸 때 픽셀 격자가 덜 뭉개진다.
        const int SamplingPointSize = 32;
        const int AtlasPadding = 4;
        const int AtlasWidth = 1024;
        const int AtlasHeight = 1024;

        [MenuItem("LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용", priority = 100)]
        public static void BakeAndApply()
        {
            TMP_FontAsset fontAsset = LoadOrBake();
            if (fontAsset == null) return;

            int applied = ApplyToLoadedScenes(fontAsset);
            Debug.Log($"[FontBaker] '{fontAsset.name}' 적용 완료 — TMP 텍스트 {applied}개. " +
                      $"에셋 경로: {OutputPath}", fontAsset);
        }

        // ------------------------------------------------------------------

        static TMP_FontAsset LoadOrBake()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[FontBaker] 원본 폰트를 찾을 수 없습니다: {SourceFontPath}");
                return null;
            }

            EnsureFolder(OutputFolder);

            // Dynamic 으로 굽는 이유: 한글은 음절이 11,172자라 전부 미리 구우면
            // 아틀라스가 감당이 안 된다. Dynamic 은 실제로 쓰인 글자만 아틀라스에
            // 채워 넣으므로 어떤 문장을 넣어도 빠지는 글자가 없다.
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[FontBaker] TMP 폰트 에셋 생성에 실패했습니다.");
                return null;
            }

            fontAsset.name = "NeoDunggeunmo SDF";
            AssetDatabase.CreateAsset(fontAsset, OutputPath);

            // 아틀라스 텍스처와 머티리얼을 폰트 에셋의 서브에셋으로 넣어야
            // 에셋 하나로 옮겨 다닐 수 있다(안 넣으면 참조가 끊긴다).
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
            {
                Texture2D atlas = fontAsset.atlasTextures[0];
                if (atlas != null && !AssetDatabase.IsSubAsset(atlas))
                {
                    atlas.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }

            Material material = fontAsset.material;
            if (material != null && !AssetDatabase.IsSubAsset(material))
            {
                material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(OutputPath);

            Debug.Log($"[FontBaker] TMP 폰트 에셋을 구웠습니다: {OutputPath}");
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
        }

        static int ApplyToLoadedScenes(TMP_FontAsset fontAsset)
        {
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var dirtyScenes = new HashSet<UnityEngine.SceneManagement.Scene>();
            int applied = 0;

            foreach (TMP_Text text in texts)
            {
                if (text == null) continue;
                if (text.font == fontAsset) continue;

                text.font = fontAsset;

                // 폰트를 바꿔도 머티리얼이 옛 폰트의 것으로 남는 경우가 있어 명시적으로 맞춘다.
                text.fontSharedMaterial = fontAsset.material;

                EditorUtility.SetDirty(text);
                dirtyScenes.Add(text.gameObject.scene);
                applied++;
            }

            foreach (var scene in dirtyScenes)
                if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            return applied;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];              // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
