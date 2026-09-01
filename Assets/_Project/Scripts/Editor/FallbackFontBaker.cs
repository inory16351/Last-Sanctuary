using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// ★★★ <b>정본 폰트(네오둥근모)가 못 그리는 글자를 맡을 폴백 폰트를 굽고 연결한다</b>
    /// (2026-09-01 신설 · 유저 지시로 7개 언어를 추가하면서).
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// <b>왜 필요했나 — 번역만 넣으면 세 언어가 «네모» 로 나온다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 정본 폰트 <c>neodgm.ttf</c>(NeoDunggeunmo)의 <c>cmap</c> 을 직접 뜯어 확인했다:
    ///
    /// <list type="bullet">
    /// <item>ASCII · 라틴-1(á é ñ ü ç ã ß) — <b>있다</b>. 스페인 · 프랑스 · 독일 ·
    ///       포르투갈어는 그대로 나온다.</item>
    /// <item>라틴 확장-A(ł ż ą ę ś ć ń ź) — <b>128자 중 1자</b>. 폴란드어가 깨진다.</item>
    /// <item>키릴 — <b>0자</b>. 러시아어가 통째로 깨진다.</item>
    /// <item>가나 · 한자 — <b>0자</b>. 일본어가 통째로 깨진다.</item>
    /// </list>
    ///
    /// ⚠ <b>이 종류의 사고는 «번역이 안 됐다» 로 안 보인다</b> — 표에는 번역이 멀쩡히
    ///   들어 있고 코드도 정상인데 화면에만 □ 가 뜬다. 그래서 번역을 채우기 <b>전에</b>
    ///   폰트부터 확인했다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// ★★ <b>2026-09-01 개정 — 폴백을 «노토 산스» 로 통일했다</b>
    ///   (유저 지시: *"네오둥근모 적용 안되는 언어는 노토 산스로 하셈"*)
    /// ══════════════════════════════════════════════════════════════════
    /// 처음에는 라틴 확장·키릴을 <b>이미 프로젝트에 있던</b> <c>LiberationSans</c>(TMP 기본
    /// 폰트)에 맡겼다 — 받을 것이 없어서였다. 유저 지시로 <b>Noto Sans 본체</b>를 받아
    /// 그 자리를 대신하게 했다. 폴백 글꼴이 두 벌(노토 · 리버레이션)로 섞이지 않는다.
    ///
    /// ⚠ <b>«노토 산스 JP» 하나로는 안 된다</b> — cmap 을 세어 확인했다. Noto Sans JP 에는
    ///   <b>폴란드어 글자(Ą Ć Ę Ł Ś Ź Ż)가 없다</b>(라틴 확장-A 128자 중 30자뿐).
    ///   윈도우에 깔린 <c>NotoSansKR</c> 도 마찬가지다. 그래서 <b>라틴/키릴용 Noto Sans 와
    ///   가나/한자용 Noto Sans JP 를 둘 다</b> 둔다.
    ///
    /// ★ <b>LiberationSans 를 완전히 뺐다 — 빼도 잃는 글자가 없음을 세어 확인했다.</b>
    ///   스트링 테이블에 실제로 쓰인 기호를 전부 뽑아 세 폰트에 대조했다:
    ///   <c>。 、 「 」 （ ） ！ ？ ： 〜</c> 는 Noto Sans JP 가, <c>★ → 〜</c> 도 Noto Sans JP 가,
    ///   <c>− — … „ ” “ ’ – № × •</c> 는 Noto Sans 나 네오둥근모가 갖고 있다.
    ///   <b>리버레이션만 갖고 있던 글자는 하나도 없었다.</b>
    ///
    /// ★ <b>순서 — 가벼운 것 먼저.</b> 폴백은 앞에서부터 찾는다. 라틴·키릴이 훨씬 흔하고
    ///   Noto Sans(2MB)가 Noto Sans JP(9.6MB)보다 가벼우므로 그쪽을 앞에 둔다.
    ///
    /// ⚠ <b>Dynamic 으로 굽는다.</b> 한자는 12,747자라 전부 미리 구우면 아틀라스가
    ///   감당이 안 된다(<see cref="NeoDunggeunmoFontBaker"/> 가 한글 11,172자에 대해
    ///   내린 것과 같은 판단). Dynamic 은 실제로 쓰인 글자만 아틀라스에 채운다.
    ///
    /// ⚠⚠ <b>빌드 용량</b> — Dynamic 폰트 에셋은 원본 TTF 를 빌드에 <b>같이 넣는다</b>
    ///   (JP 9.6MB + 본체 2MB). itch.io 웹빌드에는 부담이다. 번역이 다 끝나면
    ///   <b>실제로 쓰인 글자만</b> Static 으로 다시 구워 원본을 빼는 것이 정석이다 —
    ///   그때 쓸 «쓰인 글자 모으기» 는 스트링 테이블의 언어 칸을 훑으면 된다(미결 304).
    ///
    /// ⚠ 라이선스 — 두 폰트 모두 SIL OFL 1.1 이고 전문을
    ///   <c>Assets/_Project/Art/Fonts/OFL.txt</c> 에 뒀다. OFL 은 재배포 시 동봉을 요구한다.
    /// </summary>
    public static class FallbackFontBaker
    {
        const string FontFolder = "Assets/_Project/Art/Fonts";
        const string OutputFolder = "Assets/_Project/Resources/Fonts";
        const string MainFontPath = OutputFolder + "/NeoDunggeunmo SDF.asset";

        /// <summary>라틴 확장-A · 키릴 — 폴란드어 · 러시아어 · 프랑스어 Ÿ 를 맡는다.</summary>
        const string LatinSourcePath = FontFolder + "/NotoSans-VF.ttf";
        const string LatinOutputPath = OutputFolder + "/NotoSans SDF.asset";

        /// <summary>가나 · 한자 · CJK 문장부호 — 일본어를 맡는다.</summary>
        const string CjkSourcePath = FontFolder + "/NotoSansJP-VF.ttf";
        const string CjkOutputPath = OutputFolder + "/NotoSansJP SDF.asset";

        const int SamplingPointSize = 36;
        const int AtlasPadding = 5;
        const int AtlasWidth = 1024;
        const int AtlasHeight = 1024;

        [MenuItem("LastSanctuary/스킨/폴백 폰트 굽기 (노토 산스)", priority = 61)]
        public static void BakeAndLink()
        {
            TMP_FontAsset latin = LoadOrBake(LatinSourcePath, LatinOutputPath, "NotoSans SDF");
            TMP_FontAsset cjk = LoadOrBake(CjkSourcePath, CjkOutputPath, "NotoSansJP SDF");
            if (latin == null || cjk == null) return;

            var main = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);
            if (main == null)
            {
                Debug.LogError($"[FontBaker] 정본 폰트를 찾지 못했습니다: {MainFontPath}");
                return;
            }

            // ⚠ <b>더하지 않고 «정해진 사슬로 맞춘다».</b> 예전 판이 걸어 둔
            //   LiberationSans 가 남아 있으면 폴백 글꼴이 두 벌로 섞인다 —
            //   그것을 빼는 것이 이번 개정의 요점이므로 «없는 것만 추가» 로는 안 된다.
            var want = new List<TMP_FontAsset> { latin, cjk };
            var before = main.fallbackFontAssetTable;
            bool same = before != null && before.Count == want.Count;
            if (same)
            {
                for (int i = 0; i < want.Count; i++)
                    if (before[i] != want[i]) { same = false; break; }
            }

            if (same)
            {
                Debug.Log("[FontBaker] 폴백 사슬이 이미 맞습니다 — 바꾸지 않았습니다.", main);
                return;
            }

            main.fallbackFontAssetTable = want;
            EditorUtility.SetDirty(main);
            AssetDatabase.SaveAssets();

            Debug.Log("[FontBaker] 폴백 사슬을 맞췄습니다: " + main.name + " → " +
                      string.Join(" → ", want.ConvertAll(f => f.name)), main);
        }

        static TMP_FontAsset LoadOrBake(string sourcePath, string outputPath, string assetName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"[FontBaker] 원본 폰트를 찾을 수 없습니다: {sourcePath}");
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError($"[FontBaker] TMP 폰트 에셋 생성에 실패했습니다: {assetName}");
                return null;
            }

            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, outputPath);

            // 아틀라스·머티리얼을 서브에셋으로 — 안 넣으면 참조가 끊긴다
            // (NeoDunggeunmoFontBaker 가 같은 함정을 적어 뒀다).
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
            AssetDatabase.ImportAsset(outputPath);

            Debug.Log($"[FontBaker] 폴백 폰트를 구웠습니다: {outputPath}");
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
        }
    }
}
