using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// UI 판·버튼 그림의 <b>임포트 설정과 9-슬라이스 경계</b>를 박는다 (2026-08-25 신설).
    ///
    /// ★★ <b>왜 필요한가 — 9-슬라이스는 임포터에만 있다</b>
    /// -----------------------------------------------------
    /// 이 게임의 창은 크기가 제각각이다(창 프레임 하나가 1220×924 부터 520×430 까지
    /// 일곱 크기를 덮고, 「창 안 보통 버튼」은 폭이 열 가지다). 그림 한 장으로 다 쓰려면
    /// <see cref="Sprite.border"/> 가 있어야 하는데, 그 값은 <b>씬이 아니라 텍스처
    /// 임포터</b>에 산다. 그래서 MCP <c>update_component</c> 로는 절대 넣을 수 없고
    /// (컴포넌트가 아니다) 반드시 에디터 코드가 <see cref="TextureImporter"/> 를 만져야 한다.
    ///
    /// ★ <b>경계 값은 여기 적지 않는다.</b> <c>Tools/ui_sprite_cut.py</c> 가 시트를 자르면서
    ///   같이 계산해 <c>Temp/ui_sprite_cut.json</c> 에 적어 둔다 — 자른 사람과 값을 쓰는
    ///   사람이 갈리면 «그림은 새로 뽑았는데 경계는 옛날 것» 이 된다. 그림을 다시 뽑으면
    ///   파이썬을 다시 돌리고 이 메뉴를 누르면 끝이다.
    ///
    /// ⚠ <b>Mesh Type 은 반드시 <see cref="SpriteMeshType.FullRect"/></b> — 기본값
    ///   <c>Tight</c> 는 투명한 부분을 잘라낸 다각형 메시라 9-슬라이스가 어긋난다.
    ///   (<see cref="SpriteImportFixer"/> 는 이걸 안 건드린다 — 거기는 일러스트·아이콘용이라
    ///   늘릴 일이 없어서 Tight 가 오히려 낫다. 그래서 파일을 나눴다.)
    ///
    /// ⚠ <b>Filter Mode 는 반드시 <see cref="FilterMode.Point"/></b> (2026-08-25 2차 ·
    ///   픽셀 아트로 갈아엎으면서). 기본값 <c>Bilinear</c> 는 이웃 픽셀을 섞어서 <b>1픽셀
    ///   테두리를 뭉갠다</b> — 이 그림들은 테두리가 1픽셀이고 옆에 놓이는 글자가
    ///   비트맵 폰트(네오둥근모)라, 하나만 흐려도 «UI 만 따로 노는» 그 느낌이 된다.
    ///
    /// ⚠ <b>PPU 100</b>: 이제 그림을 <b>표시 크기 그대로</b>(1배) 내보낸다. 그림 1픽셀 =
    ///   화면 1픽셀 = 폰트 1픽셀이어야 한다. 지난 painted 세트는 2배로 뽑아 PPU 200 이었는데,
    ///   픽셀 아트에 그걸 하면 <b>격자가 절반으로 리샘플링되어</b> 통째로 뭉갠다.
    ///
    /// ⚠ <b>압축 없음</b>: UI 는 압축하면 테두리 그라데이션에 블록 얼룩이 뜬다.
    ///   장수가 36 장뿐이라 메모리도 문제되지 않는다.
    /// </summary>
    public static class UiSpriteImporter
    {
        /// <summary>파이썬이 적어 둔 경계표. 프로젝트 루트 기준.</summary>
        const string BorderTablePath = "Temp/ui_sprite_cut.json";

        /// <summary>
        /// 가로로 <b>이어 붙여 반복</b>하는 그림. 게이지 채움만 해당한다 —
        /// 끝과 끝이 맞물려야 해서 <see cref="TextureWrapMode.Clamp"/> 면 이음매가 보인다.
        /// </summary>
        static readonly string[] Repeating = { "Bar_Fill" };

        [System.Serializable]
        class Entry
        {
            public string path;
            public int[] border;   // [L, B, R, T] — Unity spriteBorder = (x=L, y=B, z=R, w=T)
        }

        [System.Serializable]
        class Table
        {
            public Entry[] items;
        }

        [MenuItem("LastSanctuary/UI/임포트", priority = 40)]
        public static void Apply()
        {
            string json = Path.Combine(Directory.GetCurrentDirectory(), BorderTablePath);
            if (!File.Exists(json))
            {
                Debug.LogError($"[UI] 경계표가 없습니다: {BorderTablePath}\n" +
                               "먼저 `python Tools/ui_sprite_cut.py` 를 돌려 시트를 자르세요.");
                return;
            }

            Table table = JsonUtility.FromJson<Table>(File.ReadAllText(json));
            if (table?.items == null || table.items.Length == 0)
            {
                Debug.LogError($"[UI] 경계표가 비었습니다: {BorderTablePath}");
                return;
            }

            var done = new List<string>();
            var missing = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (Entry e in table.items)
                {
                    var im = AssetImporter.GetAtPath(e.path) as TextureImporter;
                    if (im == null) { missing.Add(e.path); continue; }

                    im.textureType = TextureImporterType.Sprite;
                    im.spriteImportMode = SpriteImportMode.Single;
                    im.spritePixelsPerUnit = 100f;              // 1배(네이티브)로 뽑았으므로
                    im.mipmapEnabled = false;
                    im.filterMode = FilterMode.Point;           // ★ 픽셀 아트 — 절대 Bilinear 금지
                    im.alphaIsTransparency = true;
                    im.textureCompression = TextureImporterCompression.Uncompressed;
                    im.maxTextureSize = 2048;
                    im.npotScale = TextureImporterNPOTScale.None;

                    string name = Path.GetFileNameWithoutExtension(e.path);
                    im.wrapMode = System.Array.IndexOf(Repeating, name) >= 0
                        ? TextureWrapMode.Repeat
                        : TextureWrapMode.Clamp;

                    // ★ 경계와 FullRect 는 세팅 구조체로만 들어간다 — 위 프로퍼티들과
                    //   달리 TextureImporter 에 직접 뚫린 필드가 없다.
                    var s = new TextureImporterSettings();
                    im.ReadTextureSettings(s);
                    s.spriteMeshType = SpriteMeshType.FullRect;
                    s.spriteAlignment = (int)SpriteAlignment.Center;
                    s.spriteBorder = new Vector4(e.border[0], e.border[1], e.border[2], e.border[3]);
                    im.SetTextureSettings(s);

                    im.SaveAndReimport();
                    done.Add($"{name} 경계({e.border[0]},{e.border[1]},{e.border[2]},{e.border[3]})");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[UI] 임포트 설정 완료 {done.Count}장 / 못 찾음 {missing.Count}장\n" +
                      string.Join("\n", done));
            if (missing.Count > 0)
                Debug.LogWarning("[UI] 파일이 없습니다:\n" + string.Join("\n", missing));
        }
    }
}
