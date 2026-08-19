using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using LastSanctuary.Combat;
using UnityEditor;
using UnityEngine;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// 원화 폴더 → <see cref="CharacterSkinSO"/> 에셋을 <b>유니티가 직접</b> 만든다 (2026-08-19).
    ///
    /// ★★ <b>왜 생겼나</b> (유저 지시 2026-08-19: <i>"하드코딩 하지 말고 스킨 에셋 만들어서
    /// mcp 로 직접 넣어줘"</i>)
    /// ----------------------------------------------------------------------------------
    /// 지금까지 스킨 에셋은 <c>Tools/gen_*_skin.py</c> 가 <b>YAML 을 손으로 엮어서</b>
    /// 만들었다. 그 방식의 대가가 컸다:
    /// <list type="bullet">
    /// <item>스크립트가 <b>guid 를 직접 들고 다녔다</b> — 프레임 .meta 를 열어 guid 를 읽고
    ///       그 문자열을 YAML 에 박는다. 캐릭터마다 스크립트가 한 벌씩 생겼다
    ///       (<c>gen_kasinoma_skin.py</c> · <c>gen_malphas_skin.py</c> · <c>gen_laryngeal_skin.py</c> …
    ///       거의 같은 코드가 여섯 벌).</item>
    /// <item><b>빈 줄 하나에 조용히 깨진다</b> — .asset YAML 에 빈 줄이 들어가면 유니티가
    ///       그 뒤 필드를 <b>전부 무시</b>한다(진행상황 8절 3번). 사람이 엮는 동안 계속 밟은 함정이다.</item>
    /// <item>필드를 하나 추가하면 <b>여섯 스크립트를 다 고쳐야</b> 했다.</item>
    /// </list>
    /// ⚠ <c>Tools/measure_skin_tiles.py</c> 의 주석은 <i>"MCP 에는 SO 에셋을 다루는 도구가
    /// 없다"</i> 고 적어놨다(59-2절). 맞는 말이었고, <b>이 파일이 그 도구다</b> — MCP 의
    /// <c>execute_menu_item</c> 으로 부를 수 있는 메뉴가 되면 SO 에셋도 MCP 로 다뤄진다.
    ///
    /// 규칙 — <b>폴더 이름이 곧 스킨 칸</b>
    /// -------------------------------------
    /// <c>Art/Char_Asset/Char_Asset_&lt;이름&gt;/Char/</c> 아래의 <b>폴더 하나가 칸 하나</b>다.
    /// <see cref="Slots"/> 표가 그 대응이고, <b>이 표 하나뿐</b>이다 — 캐릭터가 늘어도
    /// 이 파일은 안 바뀐다.
    ///
    /// 원화만 봐서는 알 수 없는 값(재생 속도·투사체를 쓰는지)은 같은 폴더의
    /// <c>_skin_spec.txt</c> 에 <c>키=값</c> 으로 적혀 있다 — 분해 스크립트가 써 둔 것이고,
    /// 여기서는 <b>리플렉션으로</b> 같은 이름의 필드에 넣는다. 키가 늘어도 이 파일은 안 바뀐다.
    ///
    /// ⚠ <b><c>_skin_spec.txt</c> 가 있는 폴더만</b> 건드린다. 그 파일이 없는 캐릭터·몬스터의
    ///   스킨(예전 파이썬으로 만든 것)은 <b>손대지 않는다</b> — 아직 옮기지 않은 것을
    ///   조용히 비우면 안 된다.
    ///
    /// ⚠ 대상 폴더의 스킨은 <b>표에 있는 칸을 전부 먼저 비운다.</b> 안 그러면 폴더를 지워도
    ///   옛 프레임이 남는다 — 엘린의 <c>projectileFrames</c>(투사체 없음)가 정확히 그 경우다.
    ///
    /// 다음 단계: <c>python Tools/measure_skin_tiles.py</c> — 실측 크기(<c>contentSizeTiles</c> 등)를
    /// 채운다. 그건 알파 경계를 재는 일이라 파이썬 쪽에 그대로 둔다.
    /// </summary>
    public static class CharacterSkinBuilder
    {
        const string ArtRoot = "Assets/_Project/Art/Char_Asset";
        const string SpecFileName = "_skin_spec.txt";

        /// <summary>기본 출력 폴더. <c>_skin_spec.txt</c> 의 <c>outputFolder</c> 로 바꿀 수 있다.</summary>
        const string DefaultOutputFolder = "Assets/_Project/Resources/Skins";

        /// <summary>미배선 원화를 담아두는 폴더의 접두사 — 표에 없어도 경고하지 않는다.</summary>
        const string UnusedPrefix = "Unused_";

        /// <summary>
        /// <b>폴더 이름 → 스킨 필드</b>. 방향이 있는 모션은 <c>Right</c>/<c>Left</c> 두 칸,
        /// 방향이 없는 연출은 <c>Right</c> 칸만 쓰고 <c>Left</c> 는 <c>null</c> 이다.
        ///
        /// ⚠ 폴더 이름을 필드 이름과 <b>같게 맞추지 않았다</b> — 원화 폴더 이름
        /// (<c>MeleeAttack</c>·<c>RangedAttack</c>)은 이미 다른 캐릭터·몬스터와
        /// <c>Tools/import_char_asset.py</c> 가 쓰는 정본 규칙이다. 그쪽을 바꾸는 대신
        /// 대응을 여기 한 줄로 적는다.
        /// </summary>
        static readonly (string Folder, string Right, string Left)[] Slots =
        {
            // ── 이동·평타 ────────────────────────────────────────────────
            ("Idle",         "idleRight",         "idleLeft"),
            ("Walk",         "walkRight",         "walkLeft"),
            // ⚠ 몬스터 원화는 이동 폴더를 `Move` 로 쓴다(라린길·말파스). 둘 다 받는다 —
            //   원화 폴더 이름 규칙이 캐릭터/몬스터로 갈려 있고, 그쪽을 통일하는 것이
            //   이 표에 한 줄 더하는 것보다 위험하다(import_char_asset.py 가 쓰는 규칙이다).
            ("Move",         "walkRight",         "walkLeft"),
            ("MeleeAttack",  "attackRight",       "attackLeft"),
            ("RangedAttack", "rangedRight",       "rangedLeft"),
            ("MagicAttack",  "magicRight",        "magicLeft"),
            ("Heal",         "healRight",         "healLeft"),
            ("Revive",       "reviveRight",       "reviveLeft"),
            ("ReviveFx",     "reviveFx",          null),

            // ── 투사체·착탄 ──────────────────────────────────────────────
            ("Projectile",     "projectileFrames",   null),
            ("MuzzleFlash",    "muzzleFlashFrames",  null),
            ("Impact",         "impactFrames",       null),
            ("ImpactMagic",    "magicImpactFrames",  null),
            ("HealFx",         "healFxFrames",       null),
            // ★ 2026-08-20 — 근접 평타의 날아가는 연출(라린길 발톱 참격).
            ("MeleeTravelFx",  "meleeTravelFrames",  null),

            // ── 보스 스킬 (슬롯 0 = 표의 boss_skill_1) ───────────────────
            ("Skill1",           "skill1Right",       "skill1Left"),
            ("Skill2",           "skill2Right",       "skill2Left"),
            ("Skill1Fx",         "skill1Fx",          null),
            ("Skill2Fx",         "skill2Fx",          null),
            ("Skill1Projectile", "skill1Projectile",  null),
            ("Skill2Projectile", "skill2Projectile",  null),
        };

        // ⚠ 메뉴 이름을 짧게 둔다 — MCP 의 execute_menu_item 은 <b>이름 문자열이 정확히</b>
        //   맞아야 찾는다. 괄호·밑줄이 섞인 긴 이름은 실제로 못 찾았다(2026-08-19).
        //   설명은 이 주석과 클래스 요약에 있고, 메뉴에는 짧은 이름만 남긴다.
        [MenuItem("LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기", priority = 50)]
        public static void BuildAll()
        {
            string[] specs = Directory
                .GetDirectories(ArtRoot)
                .Select(d => Path.Combine(d, "Char", SpecFileName).Replace("\\", "/"))
                .Where(File.Exists)
                .ToArray();

            if (specs.Length == 0)
            {
                Debug.LogWarning($"[스킨] {ArtRoot} 아래에 {SpecFileName} 을 가진 원화 폴더가 " +
                                 "없습니다. 분해 스크립트(Tools/*_skin_build.py)를 먼저 돌려주세요.");
                return;
            }

            int built = 0;
            foreach (string spec in specs)
                if (BuildOne(spec)) built++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[스킨] 스킨 에셋 {built}개를 만들었습니다 " +
                      "(다음: python Tools/measure_skin_tiles.py — 실측 크기 채우기)");
        }

        static bool BuildOne(string specPath)
        {
            Dictionary<string, string> spec = ReadSpec(specPath);
            string charFolder = Path.GetDirectoryName(specPath).Replace("\\", "/");

            if (!spec.TryGetValue("skinAssetName", out string assetName) ||
                string.IsNullOrWhiteSpace(assetName))
            {
                Debug.LogError($"[스킨] {specPath} 에 skinAssetName 이 없습니다.");
                return false;
            }

            string outFolder = spec.TryGetValue("outputFolder", out string of) &&
                               !string.IsNullOrWhiteSpace(of) ? of : DefaultOutputFolder;
            if (!AssetDatabase.IsValidFolder(outFolder))
            {
                Debug.LogError($"[스킨] 출력 폴더가 없습니다: {outFolder}");
                return false;
            }

            string outPath = $"{outFolder}/{assetName}.asset";

            // ★ 있으면 <b>같은 에셋을 고친다</b> — 지우고 다시 만들면 guid 가 바뀌어
            //   이 스킨을 가리키는 참조가 끊긴다(캐릭터 정의는 이름으로 찾지만,
            //   씬·프리팹이 직접 참조하게 되는 날 조용히 깨진다).
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinSO>(outPath);
            bool created = skin == null;
            if (created)
            {
                skin = ScriptableObject.CreateInstance<CharacterSkinSO>();
                AssetDatabase.CreateAsset(skin, outPath);
            }

            ClearSlots(skin);

            var filled = new List<string>();
            var skipped = new List<string>();
            foreach (string dir in Directory.GetDirectories(charFolder).OrderBy(d => d))
            {
                string folder = Path.GetFileName(dir);
                int index = Array.FindIndex(Slots, s => s.Folder == folder);
                if (index < 0)
                {
                    if (!folder.StartsWith(UnusedPrefix, StringComparison.Ordinal))
                        Debug.LogWarning($"[스킨] {assetName}: 폴더 '{folder}' 에 대응하는 " +
                                         "스킨 칸이 없어 건너뜁니다. 배선할 뜻이 없으면 " +
                                         $"이름을 '{UnusedPrefix}{folder}' 로 바꿔주세요.");
                    else
                        skipped.Add(folder);
                    continue;
                }

                (string _, string right, string left) = Slots[index];
                Sprite[] all = LoadSprites(dir);
                if (all.Length == 0)
                {
                    Debug.LogWarning($"[스킨] {assetName}: {folder} 폴더가 비어 있습니다.");
                    continue;
                }

                if (left == null)
                {
                    // 방향 없는 연출 — 파일 이름에 방향이 없다.
                    Assign(skin, right, all);
                    filled.Add($"{folder}({all.Length})");
                    continue;
                }

                Sprite[] r = all.Where(s => s.name.Contains("_Right_")).ToArray();
                Sprite[] l = all.Where(s => s.name.Contains("_Left_")).ToArray();

                // 방향이 아예 안 적힌 폴더는 양쪽에 같은 프레임을 넣는다 — 정면 모션용.
                if (r.Length == 0 && l.Length == 0) { r = all; l = all; }

                Assign(skin, right, r);
                Assign(skin, left, l);
                filled.Add($"{folder}({r.Length}/{l.Length})");
            }

            ApplyScalars(skin, spec, assetName);

            EditorUtility.SetDirty(skin);
            Debug.Log($"[스킨] {(created ? "새로 만듦" : "갱신")} {outPath}\n" +
                      $"  칸: {string.Join(" ", filled)}\n" +
                      (skipped.Count == 0 ? "" : $"  미배선(Unused_): {string.Join(" ", skipped)}\n"));
            return true;
        }

        /// <summary>표에 있는 칸을 전부 빈 배열로 만든다 (맨 위 ⚠ 두 번째).</summary>
        static void ClearSlots(CharacterSkinSO skin)
        {
            foreach ((string _, string right, string left) in Slots)
            {
                Assign(skin, right, Array.Empty<Sprite>());
                if (left != null) Assign(skin, left, Array.Empty<Sprite>());
            }
        }

        static void Assign(CharacterSkinSO skin, string fieldName, Sprite[] frames)
        {
            FieldInfo f = typeof(CharacterSkinSO).GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            if (f == null)
            {
                Debug.LogError($"[스킨] CharacterSkinSO 에 '{fieldName}' 필드가 없습니다 — " +
                               "Slots 표를 고쳐주세요.");
                return;
            }
            f.SetValue(skin, frames);
        }

        /// <summary>
        /// <c>_skin_spec.txt</c> 의 나머지 키를 <b>같은 이름의 필드</b>에 넣는다.
        /// 키가 늘어도 이 파일은 안 바뀌는 것이 요점이다.
        /// </summary>
        static void ApplyScalars(CharacterSkinSO skin, Dictionary<string, string> spec,
                                 string assetName)
        {
            foreach (KeyValuePair<string, string> kv in spec)
            {
                if (kv.Key == "skinAssetName" || kv.Key == "outputFolder") continue;

                FieldInfo f = typeof(CharacterSkinSO).GetField(kv.Key,
                    BindingFlags.Public | BindingFlags.Instance);
                if (f == null)
                {
                    Debug.LogWarning($"[스킨] {assetName}: _skin_spec.txt 의 '{kv.Key}' 에 " +
                                     "해당하는 CharacterSkinSO 필드가 없습니다 — 무시합니다.");
                    continue;
                }

                try
                {
                    if (f.FieldType == typeof(string)) f.SetValue(skin, kv.Value);
                    else if (f.FieldType == typeof(bool))
                        f.SetValue(skin, kv.Value == "1" ||
                                         kv.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
                    else if (f.FieldType == typeof(int))
                        f.SetValue(skin, int.Parse(kv.Value, CultureInfo.InvariantCulture));
                    else if (f.FieldType == typeof(float))
                        f.SetValue(skin, float.Parse(kv.Value, CultureInfo.InvariantCulture));
                    else
                        Debug.LogWarning($"[스킨] {assetName}: '{kv.Key}' 는 " +
                                         $"{f.FieldType.Name} 타입이라 여기서 넣을 수 없습니다.");
                }
                catch (FormatException)
                {
                    Debug.LogError($"[스킨] {assetName}: '{kv.Key}={kv.Value}' 를 " +
                                   $"{f.FieldType.Name} 로 읽을 수 없습니다.");
                }
            }
        }

        static Dictionary<string, string> ReadSpec(string path)
        {
            var spec = new Dictionary<string, string>();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                spec[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return spec;
        }

        /// <summary>
        /// 폴더 안의 스프라이트를 <b>파일 이름의 번호 순</b>으로 읽는다.
        /// 문자열 정렬로는 <c>_10</c> 이 <c>_2</c> 앞에 오므로 번호를 숫자로 읽어 정렬한다
        /// (지금 원화는 <c>_00</c> 처럼 두 자리라 문자열 정렬로도 맞지만,
        /// 프레임이 100장을 넘는 시트가 오면 조용히 어긋난다).
        /// </summary>
        static Sprite[] LoadSprites(string dir)
        {
            return Directory.GetFiles(dir, "*.png")
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace("\\", "/")))
                .Where(s => s != null)
                .OrderBy(s => Side(s.name))
                .ThenBy(s => Number(s.name))
                .ToArray();
        }

        static int Side(string name) =>
            name.Contains("_Left_") ? 1 : 0;

        static int Number(string name)
        {
            int i = name.Length;
            while (i > 0 && char.IsDigit(name[i - 1])) i--;
            return i < name.Length &&
                   int.TryParse(name.Substring(i), out int n) ? n : 0;
        }
    }
}
