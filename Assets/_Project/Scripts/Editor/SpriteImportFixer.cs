using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// <c>Resources/</c> 아래의 PNG 를 <b>전부 Sprite 로</b> 맞춘다 (2026-08-20 신설).
    ///
    /// ★★ <b>왜 생겼나 — 시그리드 일러스트가 안 뜨던 버그</b>
    /// -----------------------------------------------------
    /// <c>Resources/Illust/illust_Sigrid.png</c> 가 <b>textureType 0(Default)</b> 로
    /// 들어와 있었다. 파일도 있고 표의 <c>illust</c> 칸도 맞는데
    /// <see cref="Resources.Load{T}"/>(<c>Sprite</c>) 가 <b>null</b> 을 돌려준다 —
    /// Default 텍스처에는 Sprite 서브에셋이 없기 때문이다.
    ///
    /// ★ 이 실패가 <b>거의 안 보인다</b>: <see cref="Units.CharacterDefinitionSO.Illust"/> 는
    ///   경고를 한 줄 남기지만 <b>초상화를 처음 여는 순간에만</b> 뜨고, 그 뒤에는
    ///   <c>_illustLoaded</c> 캐시에 걸려 두 번 다시 안 뜬다. 그래서 콘솔을 나중에 보면
    ///   아무 흔적이 없고 «일러스트가 그냥 안 나온다» 로만 보인다.
    ///
    /// ★★ <b>왜 .meta 를 손으로 안 고치나</b> (유저 지시: <i>"모든 객체 생성 및 수정은
    ///   템플릿 슬롯 복제 하는 경우를 제외하고는 하드 코딩을 하지말고 mcp 연결해서
    ///   직접 생성 및 수정"</i>)
    /// -----------------------------------------------------
    /// <c>Tools/import_monster_illust.py</c> 는 .meta YAML 을 <b>문자열 템플릿으로 엮어</b>
    /// 쓴다. 그 방식은 ① 유니티 버전이 <c>serializedVersion</c> 을 올리면 조용히 어긋나고
    /// ② 새 파일을 <b>넣을 때</b>만 돈다(이미 들어와 있는 파일은 손대지 않는다) —
    /// 시그리드가 정확히 그 구멍으로 빠졌다. 여기서는 <see cref="TextureImporter"/> 를
    /// 그대로 쓰므로 유니티가 자기 형식으로 저장한다.
    ///
    /// ★ <b>한 파일을 고치는 도구가 아니라 규칙</b>이다 — <c>Resources/</c> 아래 PNG 는
    ///   전부 코드가 <c>Resources.Load&lt;Sprite&gt;</c> 로 읽는 그림이므로
    ///   (일러스트 · 스킬 아이콘 · 스킨 프레임 · 타일) <b>Sprite 가 아닐 이유가 없다</b>.
    ///   그래서 대상을 이름으로 나열하지 않고 <b>폴더로</b> 정한다. 캐릭터·몬스터가
    ///   늘어도 이 파일은 안 바뀐다.
    ///
    /// ⚠ <b>이미 Sprite 인 파일은 건드리지 않는다</b> — 손댄 파일만 재임포트한다.
    ///   전부 다시 임포트하면 스킨 프레임 수천 장이 도는 데다, 사람이 인스펙터에서
    ///   따로 맞춰 둔 값(피벗 등)을 되돌릴 위험이 있다.
    ///
    /// ⚠ 메뉴 이름을 <b>짧게</b> 둔다 — MCP 의 <c>execute_menu_item</c> 은 이름 문자열이
    ///   정확히 맞아야 찾는다(괄호·밑줄이 섞인 긴 이름은 실제로 못 찾았다 · 2026-08-19).
    /// </summary>
    public static class SpriteImportFixer
    {
        /// <summary>
        /// 검사할 폴더. <b>여기 아래 PNG 는 전부 코드가 이름으로 읽는 그림</b>이라는 것이
        /// 이 목록의 뜻이다(<c>Resources.Load</c> 는 이 폴더 밖을 못 읽는다).
        /// </summary>
        static readonly string[] Roots = { "Assets/_Project/Resources" };

        [MenuItem("LastSanctuary/스킨/Resources 그림을 Sprite 로 맞추기", priority = 60)]
        public static void FixAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", Roots);
            var fixedPaths = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }

                // ── 아래 넷은 <b>Sprite 로 바꾼 파일에만</b> 맞춘다 ──────────────
                //    이미 Sprite 였던 파일에는 손대지 않는다(위 ⚠) — 그쪽은 정본
                //    임포트 설정(Tools/import_monster_illust.py 의 META)으로 들어온
                //    것이고, 여기서 다시 쓰면 사람이 조정한 값까지 되돌린다.
                if (!changed) continue;

                // 배경 투명이 안 켜져 있으면 반투명 경계가 검게 뜬다.
                importer.alphaIsTransparency = true;

                // 한 장 = 한 스프라이트. 이 프로젝트의 그림은 전부 낱장으로 잘라 넣는다
                // (시트 슬라이싱은 파이썬이 미리 끝낸다).
                importer.spriteImportMode = SpriteImportMode.Single;

                // 도트 그림이 밉맵으로 뭉개지면 안 된다. 정본 .meta 도 0 이다.
                importer.mipmapEnabled = false;

                // 타일·프레임이 이어 붙을 때 반대쪽 가장자리가 새지 않게.
                importer.wrapMode = TextureWrapMode.Clamp;

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                fixedPaths.Add(path);
            }

            if (fixedPaths.Count == 0)
            {
                Debug.Log($"[Sprite] {guids.Length}장을 확인했고 <b>고칠 것이 없었습니다</b> " +
                          "— Resources 아래 PNG 가 전부 Sprite 입니다.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[Sprite] {fixedPaths.Count}장을 Sprite 로 맞췄습니다 " +
                          $"(확인 {guids.Length}장):");
            foreach (string p in fixedPaths) sb.AppendLine("  · " + p);
            Debug.Log(sb.ToString());

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 정의 에셋들이 가리키는 <b>일러스트가 실제로 Sprite 로 읽히는지</b> 점검한다
        /// (2026-08-20 신설).
        ///
        /// ★★ <b>왜 별도 점검이 필요한가</b> — 위 <see cref="FixAll"/> 는 «임포트 설정이
        /// Sprite 인가» 를 본다. 그런데 그림이 화면에 안 뜨는 원인은 그것 하나가 아니다:
        /// 표의 이름이 파일명과 다를 수도 있고(대소문자·오타), 파일이 <c>Resources/</c> 밖에
        /// 있을 수도 있다. 그 셋을 한꺼번에 잡는 유일한 방법은 <b>실제로 읽어 보는 것</b>이다.
        ///
        /// ★ 런타임 경고에 기대면 안 된다 — <c>CharacterDefinitionSO.Illust</c> 는 실패를
        ///   <b>초상화를 처음 여는 순간 한 번만</b> 경고하고 <c>_illustLoaded</c> 캐시에 걸려
        ///   두 번 다시 안 뜬다. 그래서 «그냥 안 나온다» 로만 보인다(시그리드가 그랬다).
        /// </summary>
        [MenuItem("LastSanctuary/스킨/일러스트 로드 점검", priority = 61)]
        public static void VerifyIllusts()
        {
            var sb = new StringBuilder();
            int ok = 0, bad = 0;

            // 표에서 온 정의 에셋 전부. 종류별로 필드 이름이 달라 <b>리플렉션</b>으로 읽는다 —
            // 종이 늘어도 이 파일은 안 바뀐다(CharacterSkinBuilder 의 _skin_spec 처리와 같은 취지).
            foreach (ScriptableObject so in Resources.LoadAll<ScriptableObject>(""))
            {
                if (so == null) continue;

                var field = so.GetType().GetField("illustName");
                if (field == null || field.FieldType != typeof(string)) continue;

                string n = (field.GetValue(so) as string)?.Trim();
                if (string.IsNullOrEmpty(n)) continue;   // 빈 칸은 정상(일러스트 없는 종)

                if (Resources.Load<Sprite>("Illust/" + n) != null) { ok++; continue; }

                bad++;
                bool asTexture = Resources.Load<Texture2D>("Illust/" + n) != null;
                sb.AppendLine($"  · {so.name}: 'Illust/{n}' 을 Sprite 로 읽지 못했습니다" +
                              (asTexture
                                  ? " — <b>파일은 있는데 Sprite 가 아닙니다</b>(위 메뉴로 고칠 수 있습니다)."
                                  : " — <b>파일 자체가 없습니다</b>(표의 이름과 파일명을 맞춰 주세요)."));
            }

            if (bad == 0) Debug.Log($"[일러스트] {ok}개 전부 Sprite 로 읽힙니다.");
            else Debug.LogError($"[일러스트] {bad}개를 읽지 못했습니다 (정상 {ok}개):\n{sb}");
        }
    }
}
