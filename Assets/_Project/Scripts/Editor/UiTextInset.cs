using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// <b>프레임 테두리에 먹히는 글자를 안쪽으로 밀어 넣는다</b> (2026-08-25 신설).
    ///
    /// ★★ <b>왜 생겼나</b>
    /// -----------------------------------------------------
    /// 예전 판은 테두리가 <b>1~2px 단색 선</b>이라 글자를 판 가장자리 14~18px 에 놓아도
    /// 됐다. 픽셀 UI 를 깔면서 창은 <b>23px</b>, 판은 <b>10px</b>, 게이지는 <b>7px</b> 짜리
    /// 테두리가 생겼고, 그 안쪽에 있던 제목·힌트·본문이 <b>테두리 밑으로 들어갔다</b>
    /// (유저 지시: *"그 이미지들에 가려서 텍스트 짤리는 것들 수정 좀"*).
    ///
    /// ★ <b>여백을 스프라이트에서 읽는다</b>(<see cref="Sprite.border"/>) — 그림을 다시
    ///   뽑아 테두리 굵기가 바뀌어도 이 파일은 안 고친다. 다시 돌리면 다시 맞는다.
    ///   버튼 라벨은 <see cref="UiSkinApplier"/> 가 배선할 때 같은 방식으로 맡는다.
    ///
    /// ⚠ <b>레이아웃 그룹이 정하는 렉트는 건드리지 않는다</b> — 로스터 행·액션 버튼처럼
    ///   <c>VerticalLayoutGroup</c> 이 매 프레임 덮어쓰는 자리는 여기서 옮겨도 되돌아간다.
    /// ⚠ <b>중첩된 판은 «가장 가까운» 것만 본다</b> — 창 안의 속판 안의 글자를 창 기준으로
    ///   재면 이미 충분히 안쪽인데도 또 민다.
    /// ⚠ 늘어난 축은 <b>여백(offset)을 좁히고</b>, 고정된 축은 <b>위치를 민다</b>.
    ///   고정된 글자의 폭을 줄이면 가운데 정렬이 틀어진다.
    /// </summary>
    public static class UiTextInset
    {
        /// <summary>테두리에 «닿는» 것도 막는 최소 숨통.</summary>
        const float Pad = 4f;

        /// <summary>
        /// ★★ <b>훑는 캔버스가 둘이다</b> (2026-08-26 · 유저 지시:
        /// *"도움말 ui에 ui 이미지들 적용하고 텍스트 위치 안 가리게 맞추기"*).
        ///
        /// <b>왜 도움말 글자만 안 밀렸나</b> — 배선(<see cref="UiSkinApplier"/>)은
        /// 2026-08-25 에 <c>Help_Root</c> 를 목록에 넣었는데 <b>이 파일은 안 넣었다</b>.
        /// 그래서 도움말 창에는 그림이 깔릴 준비가 됐는데 글자는 <b>테두리 밑에 남는</b>
        /// 짝짝이 상태가 됐다. 이제 <b>같은 목록</b>을 쓴다 — 캔버스가 하나 더 늘어도
        /// 한 곳만 고치면 둘 다 따라온다.
        /// </summary>
        [MenuItem("LastSanctuary/UI/글자 여백", priority = 42)]
        public static void Apply()
        {
            var log = new List<string>();
            int moved = 0, framedTotal = 0, found = 0;

            foreach (string rootName in UiSkinApplier.Roots)
            {
                GameObject root = GameObject.Find(rootName);
                if (root == null)
                {
                    // ⚠ 없는 것이 <b>정상일 수 있다</b>(배선과 같은 판단).
                    log.Add($"  (건너뜀) {rootName} 이 이 씬에 없다");
                    continue;
                }
                found++;
                moved += ApplyTo(root, log, out int framedHere);
                framedTotal += framedHere;
            }

            if (found == 0)
            {
                Debug.LogError($"[UI] {string.Join(" · ", UiSkinApplier.Roots)} 을 하나도 " +
                               "못 찾았습니다. 게임 씬을 열고 실행하세요.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[UI] 글자 여백 — 판 {framedTotal}개 · 민 글자 {moved}개\n" + string.Join("\n", log));
        }

        static int ApplyTo(GameObject root, List<string> log, out int framedCount)
        {
            // 그림이 깔린 판 = 경계가 있는 스프라이트를 쓰는 Image
            var framed = new Dictionary<Transform, Vector4>();
            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                Sprite s = img.sprite;
                if (s == null) continue;
                Vector4 b = s.border;
                if (b == Vector4.zero) continue;
                framed[img.transform] = b;
            }
            framedCount = framed.Count;

            int moved = 0;

            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                // ⚠ <b>비활성 부모까지 찾아야 한다</b>(인자 true). HUD 창은 대부분 꺼진 채로
                //   저장되는데, 기본 <c>GetComponentInParent</c> 는 <b>꺼진 부모를 건너뛴다</b> —
                //   그래서 버튼 라벨이 «버튼 밑이 아닌 것» 으로 읽혀 두 번 밀렸다(실측).
                if (t.GetComponentInParent<Button>(true) != null) continue;   // 버튼 라벨은 배선이 맡는다
                if (IsLayoutDriven(t.transform)) continue;

                Transform panel = NearestFramed(t.transform, framed);
                if (panel == null) continue;

                var pr = panel as RectTransform ?? panel.GetComponent<RectTransform>();
                var lr = t.rectTransform;
                if (pr == null || lr == null) continue;

                Vector4 b = framed[panel];
                if (Fit(lr, pr, b, out string what))
                {
                    moved++;
                    log.Add($"  {root.name}/{Path(t.transform, root.transform)}  {what}");
                }
            }

            return moved;
        }

        /// <summary>
        /// 글자 렉트를 판의 «안전한 안쪽» 으로 밀어 넣는다. 실제로 옮겼으면 true.
        /// </summary>
        static bool Fit(RectTransform lr, RectTransform pr, Vector4 border, out string what)
        {
            what = null;
            // 판 기준 좌표로 글자의 네 모서리를 가져온다 (UI 는 회전이 없어 축이 그대로다).
            Vector3[] pc = new Vector3[4], tc = new Vector3[4];
            pr.GetWorldCorners(pc);
            lr.GetWorldCorners(tc);
            float scale = pr.lossyScale.x;
            if (Mathf.Approximately(scale, 0f)) return false;

            // ⚠ 경계가 <b>0 인 축에는 숨통을 주지 않는다</b> — 버튼처럼 가로로만 테두리가
            //   있는 그림에서 위아래까지 4px 씩 깎으면 글자만 작아 보인다.
            float left = pc[0].x + (border.x + (border.x > 0f ? Pad : 0f)) * scale;
            float right = pc[2].x - (border.z + (border.z > 0f ? Pad : 0f)) * scale;
            float bottom = pc[0].y + (border.y + (border.y > 0f ? Pad : 0f)) * scale;
            float top = pc[1].y - (border.w + (border.w > 0f ? Pad : 0f)) * scale;

            float dl = left - tc[0].x;        // 왼쪽으로 삐져나온 양
            float dr = tc[2].x - right;
            float db = bottom - tc[0].y;
            float dt = tc[1].y - top;

            bool anyX = dl > 0.5f || dr > 0.5f;
            bool anyY = db > 0.5f || dt > 0.5f;
            if (!anyX && !anyY) return false;

            // ⚠ <b>말이 안 되는 크기면 손대지 않는다.</b> 꺼진 창 안의 렉트나 레이아웃이
            //   아직 안 돈 자리는 월드 모서리가 엉뚱하게 나온다 — 실측에서 «가로 -48504»
            //   가 나왔다. 테두리 굵기는 아무리 커도 수십 픽셀이므로, 그 몇 배를 넘으면
            //   계산이 깨진 것이지 «많이 삐져나온» 것이 아니다.
            // ⚠ <b>안전 영역보다 «넓은» 칸은 밀지 않는다.</b> 밀면 반대쪽이 그만큼 나가서
            //   돌릴 때마다 좌우로 <b>핑퐁</b>한다(로그 줄 템플릿이 +6 / -6 을 반복했다).
            //   그런 칸은 위치가 아니라 <b>크기</b>가 문제라 사람이 정해야 한다.
            bool fitsX = (tc[2].x - tc[0].x) <= (right - left) + 0.5f;
            bool fitsY = (tc[1].y - tc[0].y) <= (top - bottom) + 0.5f;

            const float Sane = 200f;
            if (Mathf.Abs(dl) > Sane * scale || Mathf.Abs(dr) > Sane * scale ||
                Mathf.Abs(db) > Sane * scale || Mathf.Abs(dt) > Sane * scale)
            {
                what = "건너뜀 (계산이 깨짐)";
                return false;
            }

            Undo.RecordObject(lr, "UI 글자 여백");
            var parts = new List<string>();

            bool stretchX = Mathf.Approximately(lr.anchorMin.x, 0f) && Mathf.Approximately(lr.anchorMax.x, 1f);
            bool stretchY = Mathf.Approximately(lr.anchorMin.y, 0f) && Mathf.Approximately(lr.anchorMax.y, 1f);

            if (anyX)
            {
                if (stretchX)
                {
                    // 늘어난 축 — 여백을 좁힌다
                    Vector2 mn = lr.offsetMin, mx = lr.offsetMax;
                    if (dl > 0.5f) { mn.x += dl / scale; parts.Add($"왼+{dl / scale:0}"); }
                    if (dr > 0.5f) { mx.x -= dr / scale; parts.Add($"오+{dr / scale:0}"); }
                    lr.offsetMin = mn; lr.offsetMax = mx;
                }
                else if (fitsX)
                {
                    // 고정된 축 — 위치를 민다 (폭을 줄이면 가운데 정렬이 틀어진다)
                    float shift = (dl > dr ? dl : -dr) / scale;
                    lr.anchoredPosition += new Vector2(shift, 0f);
                    parts.Add($"가로 {shift:0}");
                }
                // ⚠ <b>양쪽 다 삐져나오면 옮겨도 소용없다</b> — 칸이 안전 영역보다 넓은
                //   것이라, 밀면 반대쪽이 그만큼 더 나간다(게이지 라벨이 22px 옆으로
                //   밀려 바에서 어긋났다). 그런 칸은 사람이 크기를 줄여야 한다.
            }
            if (anyY)
            {
                if (stretchY)
                {
                    Vector2 mn = lr.offsetMin, mx = lr.offsetMax;
                    if (db > 0.5f) { mn.y += db / scale; parts.Add($"아래+{db / scale:0}"); }
                    if (dt > 0.5f) { mx.y -= dt / scale; parts.Add($"위+{dt / scale:0}"); }
                    lr.offsetMin = mn; lr.offsetMax = mx;
                }
                else if (fitsY)
                {
                    float shift = (dt > db ? -dt : db) / scale;
                    lr.anchoredPosition += new Vector2(0f, shift);
                    parts.Add($"세로 {shift:0}");
                }
            }

            EditorUtility.SetDirty(lr);
            what = string.Join(" ", parts);
            return true;
        }

        static Transform NearestFramed(Transform t, Dictionary<Transform, Vector4> framed)
        {
            for (Transform c = t.parent; c != null; c = c.parent)
                if (framed.ContainsKey(c)) return c;
            return null;
        }

        /// <summary>레이아웃 그룹이 자리를 정하는가 — 그렇다면 여기서 옮겨도 되돌아간다.</summary>
        static bool IsLayoutDriven(Transform t)
        {
            for (Transform c = t.parent; c != null; c = c.parent)
                if (c.GetComponent<LayoutGroup>() != null) return true;
            return false;
        }

        static string Path(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
