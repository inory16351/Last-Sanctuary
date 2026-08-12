using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>채워지는 막대(Image.type = Filled)가 실제로 줄어들게 보장하는 보정 헬퍼.</b>
    ///
    /// <b>왜 필요한가 — 유저 리포트: "모든 체력바가 색만 바뀌고 시각적으로 줄어드는 게 안 보인다"</b>
    ///
    /// 원인은 코드가 아니라 <b>스프라이트가 비어 있다는 것</b>이었다. 유니티의
    /// <c>Image.OnPopulateMesh</c> 는 맨 앞에서 이렇게 갈린다:
    /// <code>
    ///   if (activeSprite == null) { base.OnPopulateMesh(toFill); return; }   // ← 사각형 전체를 그린다
    ///   switch (type) { ... case Type.Filled: GenerateFilledSprite(...); }
    /// </code>
    /// 즉 <b>스프라이트가 null 이면 <c>type</c> 이 Filled 여도 <c>fillAmount</c> 를 아예 보지 않고</b>
    /// 렉트 전체를 단색으로 칠한다. 씬의 막대 11개(<c>HpFill</c>·<c>HpGhost</c>·<c>ErosionFill</c>·
    /// 후퇴 기준 막대)가 전부 <c>m_Sprite: {fileID: 0}</c> 이었고, 그래서 <c>fillAmount</c> 를
    /// 제대로 넣는 코드(<see cref="CharacterRosterPanel"/> 등)와 무관하게 <b>색만 바뀌고 길이는
    /// 한 번도 변한 적이 없었다.</b>
    ///
    /// 스프라이트는 씬에도 MCP 로 직접 꽂아 두지만(정본은 인스펙터), 이 헬퍼를 <b>안전망</b>으로
    /// 같이 둔다. 이유 두 가지:
    ///   · 로스터 행·부대 카드처럼 <b>템플릿을 복제해 만드는 막대</b>는 템플릿의 스프라이트가
    ///     빠지는 순간 전부 같이 고장난다(25-2절에서 앵커가 깨져 막대가 안 보인 것과 같은 결).
    ///   · 앞으로 MCP 로 막대를 새로 만들면 스프라이트를 잊기 쉽다 — 잊어도 동작은 유지된다.
    /// 오브젝트를 만드는 것이 아니라 <b>값을 보정</b>하는 것이므로 준수사항 §10 H-1 위반이 아니다
    /// (폰트 안전망 H-4 와 같은 성격).
    /// </summary>
    public static class UiFillBar
    {
        /// <summary>막대에 쓸 흰색 스프라이트. 색은 각 <see cref="Image.color"/> 가 정한다.</summary>
        const string FillSpritePath = "UI/BarFill";

        static Sprite _fillSprite;
        static bool _loadFailed;

        /// <summary>
        /// 막대용 흰 스프라이트. 없으면 <c>Resources</c> 에서 한 번만 읽고 캐시한다.
        /// 읽기에 실패해도 매 프레임 다시 시도하지 않는다(경고 1회).
        /// </summary>
        public static Sprite FillSprite
        {
            get
            {
                if (_fillSprite != null) return _fillSprite;
                if (_loadFailed) return null;

                _fillSprite = Resources.Load<Sprite>(FillSpritePath);
                if (_fillSprite == null)
                {
                    _loadFailed = true;
                    Debug.LogWarning($"[UI] Resources/{FillSpritePath} 를 찾지 못했습니다. " +
                                     "채워지는 막대가 줄어들지 않고 렉트 전체로 보입니다.");
                }
                return _fillSprite;
            }
        }

        /// <summary>
        /// 이 <see cref="Image"/> 를 "가로로 채워지는 막대" 로 확정한다.
        /// 스프라이트가 이미 꽂혀 있으면 <b>건드리지 않는다</b> — 유저가 에디터에서 넣은
        /// 그림(9-slice 프레임 등)을 존중한다. 막대 바인딩 직후 한 번만 부르면 된다.
        /// </summary>
        public static void Prepare(Image bar)
        {
            if (bar == null) return;

            if (bar.sprite == null)
            {
                Sprite sprite = FillSprite;
                if (sprite == null) return;
                bar.sprite = sprite;
            }

            // 채움 방식까지 여기서 확정한다 — 스프라이트만 꽂고 type 이 Simple 이면
            // 역시 fillAmount 가 무시된다(같은 증상이 다른 이유로 재발한다).
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillOrigin = (int)Image.OriginHorizontal.Left;
            bar.preserveAspect = false;
        }

        /// <summary>여러 개를 한 번에 — 바인딩 코드가 한 줄로 끝나게 하는 편의 오버로드.</summary>
        public static void Prepare(params Image[] bars)
        {
            if (bars == null) return;
            for (int i = 0; i < bars.Length; i++) Prepare(bars[i]);
        }
    }
}
