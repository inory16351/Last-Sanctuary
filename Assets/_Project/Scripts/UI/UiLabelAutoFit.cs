using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Data;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>번역 때문에 잘리는 라벨을 자동으로 줄여 넣는다</b>
    /// (2026-09-01 신설 · 유저 지시: *"잘리는 라벨들 개선해주고"*).
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// <b>왜 필요한가 — 한국어가 유난히 짧다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 이 게임의 UI 는 한국어 문구에 맞춰 폭이 잡혀 있다. 그런데 한국어는
    /// <b>두 글자로 끝나는 라벨</b>이 아주 많고(전방 · 후퇴 · 사냥 · 확인 · 장착),
    /// 같은 뜻을 다른 말로 옮기면 <b>훨씬 길어진다</b>. 실측(글자 폭 기준, CJK 는 2배):
    ///
    /// <list type="bullet">
    /// <item>「만렙」 → <b>Максимальный уровень</b> — <b>×5.0</b></item>
    /// <item>「탐색」 → <b>Reconnaissance</b> · 「도망」 → <b>Auf der Flucht</b> — ×3.5</item>
    /// <item>「기본값으로」 → <b>Rétablir les valeurs par défaut</b> — ×3.1</item>
    /// </list>
    ///
    /// 폭이 고정된 버튼에서 이 문구들은 <b>잘리거나 밖으로 삐져나온다</b>.
    ///
    /// ★ <b>두 갈래로 푼다</b>
    ///   ① 여기 — <b>칸에 맞게 글자를 줄인다</b>(TMP 자동 크기). 어떤 언어가 들어와도 먹는다.
    ///   ② 스트링 테이블 — <b>너무 긴 번역은 짧은 말로 바꾼다</b>. ①만으로 버티면
    ///     러시아어 버튼만 글자가 절반 크기가 되어 «저 버튼만 이상하다» 가 된다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// <b>어디에 거는가 — 아무 데나 걸면 안 된다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// ⚠ <b>본문(여러 줄로 흐르는 글)에는 걸지 않는다.</b> 그런 글은 «잘리는» 것이 아니라
    ///   <b>줄바꿈으로 흘러내린다</b> — 자동 크기를 켜면 문단 길이에 따라 글자 크기가
    ///   출렁여서 오히려 읽기 나빠진다. 그래서 <b>한 줄짜리 라벨</b>만 고른다
    ///   (칸 높이가 글자 크기의 2.2배 미만).
    ///
    /// ⚠ <b>사람이 이미 켜 둔 자동 크기는 건드리지 않는다</b>
    ///   (<c>EndingDirector</c> 의 엔딩 롤이 그렇다). 그쪽은 의도한 설정이다.
    ///
    /// ⚠ <b>바닥을 둔다</b>(<see cref="MinScale"/> · 절대 하한 <see cref="AbsoluteMin"/>).
    ///   바닥이 없으면 TMP 가 글자를 4pt 까지 줄여서 «안 잘리지만 읽을 수도 없는» 상태가 된다.
    ///   그 지점을 넘어가면 <b>말줄임표</b>로 끝내는 편이 정직하다.
    ///
    /// ★ <b>언제 도는가</b> — 씬이 뜬 다음 한 번, 그리고 <b>언어가 바뀔 때마다</b>.
    ///   HUD 는 코드로 만들어지므로(<c>HudBootstrap</c>) 첫 프레임에는 아직 없다.
    ///   그래서 한 프레임 기다렸다 돈다.
    /// </summary>
    public static class UiLabelAutoFit
    {
        /// <summary>원래 크기의 몇 배까지 줄일 수 있는가.</summary>
        const float MinScale = 0.55f;

        /// <summary>이보다 작게는 안 줄인다(pt). 읽을 수 없는 크기로 가느니 말줄임이 낫다.</summary>
        const float AbsoluteMin = 10f;

        /// <summary>이 배수보다 칸이 높으면 «여러 줄 본문» 으로 보고 건드리지 않는다.</summary>
        const float SingleLineHeightRatio = 2.2f;

        static readonly List<TMP_Text> _scratch = new List<TMP_Text>(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            // ⚠ 정적 이벤트라 판마다 쌓인다 — 빼고 더한다.
            StringTable.OnLanguageChanged -= ApplyAllDeferred;
            StringTable.OnLanguageChanged += ApplyAllDeferred;
            ApplyAllDeferred();
        }

        /// <summary>
        /// 한 프레임 뒤에 돈다. HUD 가 코드로 만들어지므로 «지금» 훑으면 아직 아무것도 없다.
        /// 언어 변경 때도 마찬가지다 — 각 창이 자기 문구를 다시 적는 것이 <b>먼저</b>여야 한다.
        /// </summary>
        public static void ApplyAllDeferred()
        {
            Runner.Ensure().StartCoroutine(NextFrame());
        }

        static IEnumerator NextFrame()
        {
            yield return null;      // 창들이 문구를 다시 적을 틈을 준다
            ApplyAll();
        }

        /// <summary>지금 씬의 모든 UI 라벨을 훑어 칸에 맞게 맞춘다.</summary>
        public static void ApplyAll()
        {
            _scratch.Clear();
            TMP_Text[] all = Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int fitted = 0;
            for (int i = 0; i < all.Length; i++)
                if (Apply(all[i])) fitted++;
        }

        /// <summary>이 라벨 하나를 맞춘다. 손댔으면 <c>true</c>.</summary>
        public static bool Apply(TMP_Text text)
        {
            if (text == null) return false;

            // 월드 공간 글씨(데미지 숫자 등)는 칸이 없다 — 잘릴 일도 없다.
            var rt = text.rectTransform;
            if (rt == null) return false;
            if (text.GetComponentInParent<Canvas>() == null) return false;

            // 사람이 이미 정한 설정은 존중한다.
            if (text.enableAutoSizing) return false;

            float size = text.fontSize;
            if (size <= 0f) return false;

            // 여러 줄 본문은 «흘러내리는» 글이라 자동 크기가 오히려 해롭다.
            float h = rt.rect.height;
            if (h <= 0f || h > size * SingleLineHeightRatio) return false;

            float min = Mathf.Max(AbsoluteMin, size * MinScale);
            if (min >= size) return false;          // 원래도 작은 글씨라 줄일 여지가 없다

            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = min;

            // ★ 바닥까지 줄여도 안 들어가면 <b>말줄임표</b>로 끝낸다 — 칸 밖으로
            //   삐져나가 옆 버튼을 덮는 것보다 «…» 이 정직하다.
            if (text.overflowMode == TextOverflowModes.Overflow)
                text.overflowMode = TextOverflowModes.Ellipsis;

            return true;
        }

        /// <summary>
        /// 코루틴을 돌릴 자리. 정적 클래스는 <c>StartCoroutine</c> 을 못 하므로
        /// 숨은 오브젝트 하나를 만들어 쓴다(<c>HideFlags</c> 로 하이라키에도 안 보인다).
        /// </summary>
        class Runner : MonoBehaviour
        {
            static Runner _instance;

            public static Runner Ensure()
            {
                if (_instance != null) return _instance;

                var go = new GameObject("~UiLabelAutoFit") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<Runner>();
                return _instance;
            }
        }
    }
}
