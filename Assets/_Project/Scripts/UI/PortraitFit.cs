using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>일러스트를 액자에 꽉 채운다</b> (2026-08-17 신설, 유저 지시:
    /// <i>"일러스트 ui 나타나는 크기 부자연스러움 — 원본 이미지를 다시 분석해서 적절한 비율과
    /// 크기로 채워 넣을 것. 지금 빈 공간이 너무 많아서 이상함. 스타크래프트 일러스트 ui 느낌"</i>).
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// ★★ <b>빈 공간의 정체 — <c>preserveAspect</c> 는 "채우기"가 아니라 "맞춰 넣기"다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 지금까지 모든 초상화가 <c>Image.preserveAspect = true</c> 하나로 끝냈다. 그런데 이 옵션은
    /// <b>contain</b>(액자 안에 그림이 통째로 들어가게 축소)이라, 액자와 그림의 비율이 다르면
    /// <b>반드시 남는 쪽이 생긴다</b>. 실제 숫자가 그대로 그 증상이었다:
    /// <code>
    ///   HUD_Portrait/Art 액자 : 424 x 262  (가로형 1.618)
    ///   캐릭터 일러스트       : 420 x 568  (세로형 0.739)
    ///   → 높이에 맞춰 194 x 262 로 그려진다 = 액자 <b>가로의 46%</b> 만 씀
    ///     좌우에 각각 115px 씩, 합쳐 <b>액자의 54%</b> 가 빈 공간
    /// </code>
    /// 게다가 몬스터 일러스트는 반대로 <b>가로형</b>(768x512 = 1.5)이라, 액자 비율을 캐릭터에
    /// 맞추면 이번엔 몬스터가 남는다. <b>어느 한 비율로 액자를 고쳐도 다른 쪽이 깨진다.</b>
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// <b>그래서 cover 로 바꿨다 — 스타크래프트 초상화가 하는 그 방식</b>
    /// ══════════════════════════════════════════════════════════════════
    /// <code>
    ///   contain (기존) : 그림 전체가 보인다  · 액자에 빈 곳이 생긴다
    ///   cover   (지금) : 액자가 꽉 찬다      · 그림의 넘치는 쪽이 잘린다
    /// </code>
    /// 스타크래프트의 유닛 초상화는 액자가 언제나 꽉 차 있고, 얼굴이 프레임 밖으로 넘어간다.
    /// "그림을 온전히 감상하는 자리"가 아니라 <b>"지금 누구인지 알려주는 계기판"</b>이기 때문이다.
    /// 이 게임의 초상화도 같은 목적이라 같은 방식이 맞다.
    ///
    /// ⚠ <b>잘라내려면 액자에 <c>RectMask2D</c> 가 있어야 한다.</b> 이 함수는 그림
    /// <b>Image 의 크기만</b> 액자보다 크게 잡는다 — 넘친 부분을 실제로 가리는 것은 마스크다.
    /// 마스크가 없으면 그림이 액자 밖으로 삐져나와 옆 UI 를 덮는다.
    /// (그래서 <see cref="Cover"/> 가 마스크가 없으면 경고를 남긴다.)
    ///
    /// ★ <b>세로 기준점(<paramref name="verticalAnchor"/>)이 이 함수의 핵심 인자다.</b>
    /// 세로로 긴 인물화를 가로 액자에 cover 로 넣으면 위아래가 잘리는데, 가운데를 남기면
    /// <b>얼굴이 아니라 가슴이 남는다.</b> 인물은 위쪽(0.85 정도)을 남겨야 얼굴이 들어온다.
    /// 몬스터 전신화는 가운데(0.5)가 맞다 — 실루엣 전체가 정보이기 때문이다.
    /// </summary>
    public static class PortraitFit
    {
        /// <summary>
        /// <paramref name="image"/> 를 <b>부모 RectTransform(액자)에 꽉 차게</b> 키운다.
        /// 스프라이트가 없으면 아무것도 하지 않는다(꺼진 액자의 크기를 흔들 이유가 없다).
        /// </summary>
        /// <param name="image">그림을 그리는 Image. <b>액자의 자식</b>이어야 한다.</param>
        /// <param name="verticalAnchor">
        /// 세로로 잘릴 때 남길 위치. 0 = 아래, 0.5 = 가운데, 1 = 위.
        /// 인물은 0.85 정도(얼굴), 전신 몬스터는 0.5.
        /// </param>
        /// <param name="horizontalAnchor">가로로 잘릴 때 남길 위치. 보통 0.5(가운데).</param>
        /// <param name="zoom">
        /// ★★ <b>얼굴만 크게 보고 싶을 때 더 키우는 배수</b> (2026-08-26 · 유저 지시:
        /// *"캐릭터 로스터에 초상화 얼굴이 잘리니까 좀더 얼굴에 집중해서 짤라서 만들어
        /// 헤드룸이 좀 있어야 자연스럽게 들어갈듯"*).
        ///
        /// <b>왜 필요한가</b> — cover 는 «액자를 채우는 <b>최소</b> 배율» 이다. 420×568 인물화를
        /// 84×84 정사각 액자에 넣으면 폭이 딱 맞아 <b>세로 420px 이 그대로 보인다</b> — 얼굴
        /// (위쪽 200px 쯤)만 아니라 <b>가슴·허리까지</b> 들어와 작은 칸에서는 얼굴이 알아볼 수
        /// 없이 작아진다. 로스터의 84px 칸이 정확히 그 경우였다.
        ///
        /// <b>무엇을 하나</b> — 계산된 cover 크기에 이 배수를 곱한다. <c>1.6</c> 이면 보이는
        /// 영역이 420 → <b>262px</b> 로 좁아져 얼굴이 칸을 채운다.
        /// ★ <b>1 이면 예전과 완전히 같다</b> — 다른 초상화 자리(상세 카드·몬스터)는 안 건드린다.
        /// ⚠ 배수를 올릴수록 <paramref name="verticalAnchor"/> 를 <b>1 에 붙여야</b> 한다 —
        ///   좁아진 창을 아래로 내리면 «머리 위 여백»(헤드룸)이 아니라 <b>머리가 잘린다</b>.
        /// </param>
        public static void Cover(Image image, float verticalAnchor = 0.85f,
                                 float horizontalAnchor = 0.5f, float zoom = 1f,
                                 float focusX = -1f, float focusY = -1f,
                                 float focusPlacement = 0.35f)
        {
            if (image == null) return;

            var rect = image.rectTransform;
            var frame = rect.parent as RectTransform;
            if (frame == null) return;

            Sprite sprite = image.sprite;
            if (sprite == null) return;

            // ★ cover 는 Image 가 직접 못 한다 — preserveAspect 는 contain 뿐이다.
            //   그래서 preserveAspect 를 끄고(늘려서 채우게 두고) 크기를 우리가 계산한다.
            //   ⚠ 켜둔 채로 크기만 키우면 contain 이 다시 안쪽으로 줄여 원점으로 돌아간다.
            image.preserveAspect = false;
            image.type = Image.Type.Simple;

            Vector2 frameSize = frame.rect.size;
            if (frameSize.x <= 0f || frameSize.y <= 0f) return;

            float spriteAspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            float frameAspect = frameSize.x / frameSize.y;

            Vector2 size = spriteAspect > frameAspect
                // 그림이 액자보다 납작하다 → 높이를 맞추고 좌우가 넘친다
                ? new Vector2(frameSize.y * spriteAspect, frameSize.y)
                // 그림이 액자보다 길쭉하다 → 폭을 맞추고 위아래가 넘친다
                : new Vector2(frameSize.x, frameSize.x / spriteAspect);

            // ★ 얼굴만 크게 볼 때는 «채우는 최소 배율» 위로 한 번 더 키운다.
            //   ⚠ 1 보다 작은 값은 받지 않는다 — 액자에 빈 곳이 생기면 cover 가 아니게 된다.
            size *= Mathf.Max(1f, zoom);

            // 앵커·피벗을 액자 정중앙으로 고정한 뒤 크기와 오프셋을 직접 준다.
            // ⚠ 늘린 앵커(anchorMin != anchorMax)를 남겨두면 sizeDelta 의 뜻이
            //   "액자와의 차이"로 바뀌어 아래 계산이 전부 어긋난다(88-2절의 그 함정과 같은 결).
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            // 넘치는 양의 절반이 한쪽으로 갈 수 있는 최대치다. 앵커 0.5 면 0(가운데 정렬).
            float overflowX = Mathf.Max(0f, size.x - frameSize.x);
            float overflowY = Mathf.Max(0f, size.y - frameSize.y);

            // ══════════════════════════════════════════════════════════════
            //  ★★★ 2026-08-26 — <b>세로 앵커의 부호가 뒤집혀 있었다</b>
            // ══════════════════════════════════════════════════════════════
            // 유니티 UI 는 <b>+y 가 위</b>다. 그림을 위로 밀면 액자에는 그림의 <b>아래쪽</b>이
            // 남는다 — 가로가 그래서 <c>(0.5 − h)</c> 인 것이다(h=0 «왼쪽을 남긴다» → 그림을
            // 오른쪽으로 민다). 그런데 세로만 <c>(v − 0.5)</c> 였다:
            //
            //     v = 1(«맨 위를 남긴다»)  →  +overflowY/2  →  그림이 위로  →  <b>아래쪽이 남는다</b>
            //
            // 즉 <b>뜻과 정반대로</b> 돌고 있었다. 84x84 정사각 액자(로스터 행)에서는
            // 넘침이 75px 이라 <b>허리·다리만 보였다</b> — 유저가 «얼굴이 잘린다» →
            // «엄청 이상하다» 고 세 번 말한 것이 이것이다.
            // ⚠ 다른 호출부(상세 카드 236x302 · 성장 창 226x300)는 세로 넘침이 2px 미만이라
            //   같은 버그를 안고도 <b>눈에 안 띄었다</b>. 그래서 오래 살아남았다.
            // ⚠⚠ <b>검산은 «코드» 로 해야 한다</b> — 이 값을 파이썬으로 미리 그려 확인했는데,
            //   그 모의는 «뜻» 대로(1 = 위) 계산해서 <b>버그를 그대로 통과시켰다</b>.
            float offsetX = (0.5f - Mathf.Clamp01(horizontalAnchor)) * overflowX;
            float offsetY = (0.5f - Mathf.Clamp01(verticalAnchor)) * overflowY;

            // ★★ <b>초점(focus)</b> — 그림의 어느 점을 액자의 어디에 놓을지 (2026-08-26 · 유저
            //   지시: *"캐릭터의 얼굴이 보이는 상체 일러스트 부분만 남기기"*).
            //
            //   앵커는 «위/가운데/아래» 셋 중 하나를 고르는 것뿐이라, <b>캐릭터마다 얼굴 높이가
            //   다른</b> 일러스트에는 맞지 않는다(15장 실측 — 세로 0.19~0.38 · 가로 0.42~0.58).
            //   초점은 «그 캐릭터의 얼굴 좌표» 를 받아 <b>액자의 정해진 자리</b>에 놓는다.
            //   <paramref name="focusPlacement"/> 가 그 자리다(0.35 = 액자 위에서 35% 지점 —
            //   얼굴 위에 머리, 아래에 어깨가 남아 «상체» 가 된다).
            if (focusY >= 0f)
            {
                float want = frameSize.y * 0.5f - frameSize.y * Mathf.Clamp01(focusPlacement)
                             - size.y * 0.5f + Mathf.Clamp01(focusY) * size.y;
                offsetY = Mathf.Clamp(want, -overflowY * 0.5f, overflowY * 0.5f);
            }
            if (focusX >= 0f)
                offsetX = Mathf.Clamp(size.x * (0.5f - Mathf.Clamp01(focusX)),
                                      -overflowX * 0.5f, overflowX * 0.5f);

            rect.anchoredPosition = new Vector2(offsetX, offsetY);

            WarnIfUnmasked(frame, image);
        }

        static void WarnIfUnmasked(RectTransform frame, Image image)
        {
#if UNITY_EDITOR
            if (frame.GetComponent<RectMask2D>() != null) return;
            if (frame.GetComponentInParent<RectMask2D>() != null) return;

            Debug.LogWarning(
                $"[초상화] '{frame.name}' 에 RectMask2D 가 없습니다 — 꽉 채운 그림이 " +
                "액자 밖으로 넘쳐 옆 UI 를 덮습니다. 액자에 RectMask2D 를 붙여주세요.",
                image);
#endif
        }
    }
}
