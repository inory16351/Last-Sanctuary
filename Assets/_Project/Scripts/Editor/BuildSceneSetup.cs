using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// 빌드 씬 목록을 <b>정본 순서</b>로 맞춘다 (2026-08-18, 99절).
    ///
    /// <b>순서가 곧 규칙이다</b> — 빌드를 실행하면 <b>0번 씬</b>이 열린다. 유저 지시가
    /// <i>"게임 시작하면 로비화면에서 시작"</i> 이므로 <see cref="LobbyScene"/> 이 반드시 0번이어야 한다.
    ///
    /// <b>왜 메뉴로 만들었나</b> — 빌드 세팅은 <c>ProjectSettings/EditorBuildSettings.asset</c> 에
    /// 있고, 에디터가 그것을 <b>메모리에 들고 있다</b>. 파일을 직접 고치면 에디터가 자기 사본으로
    /// 덮어써 되돌아간다. 그리고 이 목록은 씬을 새로 만들 때마다 어긋날 수 있으므로,
    /// 한 번 쓰고 버리는 것보다 <b>언제든 다시 맞출 수 있는 버튼</b>이 낫다.
    /// </summary>
    public static class BuildSceneSetup
    {
        const string LobbyScene = "Assets/Scenes/Lobby.unity";
        const string GameScene = "Assets/Scenes/Proto_01.unity";

        /// <summary>정본 순서. 0번이 게임을 켰을 때 열리는 씬이다.</summary>
        static readonly string[] Order = { LobbyScene, GameScene };

        [MenuItem("LastSanctuary/빌드/씬 목록을 정본 순서로 맞추기 (로비 → 게임)", priority = 200)]
        public static void Apply()
        {
            var scenes = new List<EditorBuildSettingsScene>();

            foreach (string path in Order)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogError($"[빌드] 씬을 찾을 수 없습니다: {path}. 목록을 바꾸지 않았습니다.");
                    return;
                }
                scenes.Add(new EditorBuildSettingsScene(path, enabled: true));
            }

            // 정본에 없는 씬(예: SampleScene)은 <b>끈 채로 뒤에 남긴다</b> — 목록에서 아예 빼면
            // 누군가 쓰고 있던 씬이 조용히 사라진다. 끄기만 하면 빌드에는 안 들어가고 흔적은 남는다.
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing == null) continue;
                if (System.Array.IndexOf(Order, existing.path) >= 0) continue;

                scenes.Add(new EditorBuildSettingsScene(existing.path, enabled: false));
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[빌드] 씬 목록을 맞췄습니다 — 0번 {LobbyScene} · 1번 {GameScene} " +
                      $"(그 외 {scenes.Count - Order.Length}개는 비활성으로 유지)");
        }
    }
}
