using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LastSanctuary.UI;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// <b>연출 대본(slides)을 코드 기본값으로 씬에 밀어 넣는다</b> (2026-08-28 신설).
    ///
    /// ★★★ <b>왜 필요한가</b> — <see cref="OpeningDirector"/>·<see cref="EndingDirector"/> 의
    /// <c>slides</c> 는 <c>[SerializeField]</c> 라 <b>씬에 저장된 값이 코드보다 이긴다</b>.
    /// 그래서 코드의 표를 아무리 고쳐도 <c>Opening.unity</c>·<c>Ending.unity</c> 를 열어 보면
    /// <b>옛 표가 그대로</b> 있고 게임에서도 옛 연출이 나온다.
    ///
    /// ⚠ 컴포넌트 톱니바퀴 → <b>Reset</b> 은 이것을 고치지만 <b>다른 인스펙터 값도 전부</b>
    ///   코드 기본값으로 돌려 버린다(페이드·자막 크기·명단 색 …). 손으로 맞춰 둔 값이 날아간다.
    ///
    /// ★ 그래서 이 도구는 <b>정해진 몇 칸만</b> 옮긴다(<see cref="Fields"/>). 빈
    ///   <see cref="GameObject"/> 에 컴포넌트를 새로 붙여 «코드 기본값 그대로인 인스턴스» 를
    ///   만들고, 그 직렬화 값을 <see cref="SerializedObject.CopyFromSerializedProperty"/> 로
    ///   씬의 것에 복사한다 — 중첩 배열(컷 안의 조각들)까지 통째로 따라온다.
    ///
    /// ⚠ <b>페이드도 같이 옮긴다</b> — 컷 수를 바꾸면 시각표와 페이드는 <b>한 벌</b>이다.
    ///   표만 밀어 넣고 페이드를 옛 값으로 두면 컷이 브금을 넘긴다(2026-08-28 여덟 컷).
    /// </summary>
    static class CutsceneTablePusher
    {
        const string OpeningScene = "Assets/Scenes/Opening.unity";
        const string EndingScene  = "Assets/Scenes/Ending.unity";

        /// <summary>씬으로 밀어 넣을 칸. 없는 칸은 조용히 건너뛴다.</summary>
        static readonly string[] Fields =
        {
            "slides",
            "fadeInSeconds",
            "fadeOutSeconds",
            "dissolveSeconds",
        };

        [MenuItem("LastSanctuary/연출/오프닝·엔딩 대본을 코드 기본값으로 씬에 밀어 넣기")]
        static void PushBoth()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int changed = 0;
            changed += Push<OpeningDirector>(OpeningScene) ? 1 : 0;
            changed += Push<EndingDirector>(EndingScene) ? 1 : 0;

            Debug.Log($"[연출] 대본 밀어 넣기 끝 — 씬 {changed}개를 고쳤다.");
        }

        [MenuItem("LastSanctuary/연출/대본 점검 (씬과 코드가 같은지)")]
        static void CheckBoth()
        {
            Check<OpeningDirector>(OpeningScene);
            Check<EndingDirector>(EndingScene);
        }

        // ── 밀어 넣기 ────────────────────────────────────────────────────

        static bool Push<T>(string scenePath) where T : MonoBehaviour
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            T target = FindDirector<T>(scene);
            if (target == null)
            {
                Debug.LogError($"[연출] {scenePath} 에 {typeof(T).Name} 가 없다 — 건너뛴다.");
                return false;
            }

            int before = CountSlides(target);

            var probe = new GameObject("~cutscene-default-probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                T fresh = probe.AddComponent<T>();          // 코드 기본값 그대로인 인스턴스

                var from = new SerializedObject(fresh);
                var to   = new SerializedObject(target);

                foreach (string field in Fields)
                {
                    SerializedProperty source = from.FindProperty(field);
                    if (source == null)
                    {
                        if (field == "slides")
                        {
                            Debug.LogError($"[연출] {typeof(T).Name} 에 slides 필드를 못 찾았다.");
                            return false;
                        }
                        continue;                            // 그 연출에 없는 칸이면 넘어간다
                    }

                    to.CopyFromSerializedProperty(source);   // 중첩 배열까지 통째로
                }

                to.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }

            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[연출] {scenePath} — 컷 {before}개 → {CountSlides(target)}개 로 갈아 끼우고 저장했다.");
            return true;
        }

        static void Check<T>(string scenePath) where T : MonoBehaviour
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            T target = FindDirector<T>(scene);
            if (target == null)
            {
                Debug.LogError($"[연출] {scenePath} 에 {typeof(T).Name} 가 없다.");
                return;
            }

            var so = new SerializedObject(target);
            SerializedProperty slides = so.FindProperty("slides");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[연출] {scenePath} — 씬에 저장된 컷 {slides.arraySize}개  " +
                          $"(페이드인 {so.FindProperty("fadeInSeconds")?.floatValue:0.00} · " +
                          $"아웃 {so.FindProperty("fadeOutSeconds")?.floatValue:0.00})");

            for (int i = 0; i < slides.arraySize; i++)
            {
                SerializedProperty s = slides.GetArrayElementAtIndex(i);
                SerializedProperty dissolve = s.FindPropertyRelative("dissolve");
                SerializedProperty caps = s.FindPropertyRelative("captions");
                sb.AppendLine(string.Format("  {0}컷  {1,-16} {2,7:0.00}  {3}  조각 {4}개",
                    i + 1,
                    s.FindPropertyRelative("background").stringValue,
                    s.FindPropertyRelative("atMusicTime").floatValue,
                    dissolve != null && dissolve.boolValue ? "겹치기 " : "검은전환",
                    caps != null ? caps.arraySize : 0));
            }

            Debug.Log(sb.ToString());
        }

        // ── 잔심부름 ─────────────────────────────────────────────────────

        static T FindDirector<T>(Scene scene) where T : MonoBehaviour
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        static int CountSlides(MonoBehaviour target)
        {
            FieldInfo field = target.GetType()
                .GetField("slides", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Array value = field?.GetValue(target) as Array;
            return value?.Length ?? -1;
        }
    }
}
