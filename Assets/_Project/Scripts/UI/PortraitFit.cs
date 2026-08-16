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
        public static void Cover(Image image, float verticalAnchor = 0.85f,
                                 float horizontalAnchor = 0.5f)
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

            // 앵커·피벗을 액자 정중앙으로 고정한 뒤 크기와 오프셋을 직접 준다.
            // ⚠ 늘린 앵커(anchorMin != anchorMax)를 남겨두면 sizeDelta 의 뜻이
            //   "액자와의 차이"로 바뀌어 아래 계산이 전부 어긋난다(88-2절의 그 함정과 같은 결).
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            // 넘치는 양의 절반이 한쪽으로 갈 수 있는 최대치다. 앵커 0.5 면 0(가운데 정렬).
            float overflowX = Mathf.Max(0f, size.x - frameSize.x);
            float overflowY = Mathf.Max(0f, size.y - frameSize.y);

            rect.anchoredPosition = new Vector2(
                (0.5f - Mathf.Clamp01(horizontalAnchor)) * overflowX,
                (Mathf.Clamp01(verticalAnchor) - 0.5f) * overflowY);

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
