using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Relics;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>장착 유물 아이콘 «띠»</b> — 칸 하나짜리 <see cref="Image"/> 를 받아
    /// <b>칸 수만큼 복제</b>해 늘어놓고, 그 캐릭터가 낀 유물을 순서대로 그린다
    /// (2026-08-26 · 유저 지시: *"유물 장착 인벤토리 3칸으로 변경 / 초상화UI(아이콘만) /
    /// 캐릭터 로스터에도 연동"*).
    ///
    /// <b>왜 «복제» 인가</b> — 로스터 행과 상세 카드에는 <b>이미 아이콘 하나가 씬에 있다</b>
    /// (166·135-4절이 좌표를 실측해서 잡아 둔 것이다). 칸이 셋이 됐다고 씬에 두 개를 더
    /// 손으로 만들면 <b>세 곳(행 모체·상세 카드·앞으로 생길 곳)의 좌표가 따로 논다</b>.
    /// 원본 하나를 «칸 0» 으로 두고 나머지를 <b>코드가 같은 크기로 옆에 복제</b>하면
    /// 자리 규칙이 한 곳에만 있다 — 이 프로젝트의 «모체 하나만 만들고 반복은 코드가»
    /// (준수사항 §10 H-2) 와 같은 방식이다.
    ///
    /// ★ <b>등급 색으로 칠하지 않는다</b> — 아이콘은 원화라 색을 곱하면 탁해진다
    ///   (<c>CharacterRosterPanel.ApplyRelicIcon</c> 이 세운 규칙 그대로다).
    ///   빈 칸은 <b>알파 0</b> 으로 지운다 — 흰 사각형이 남지 않게.
    ///
    /// ⚠ <b>복제는 한 번만 한다.</b> <see cref="Refresh"/> 는 매 갱신마다 불리므로
    ///   그때마다 <c>Instantiate</c> 하면 로스터가 0.2초마다 GC 를 돌린다.
    /// </summary>
    public class RelicIconStrip
    {
        readonly List<Image> _icons = new List<Image>(3);
        readonly List<RelicDefinitionSO> _scratch = new List<RelicDefinitionSO>(3);

        Image _origin;
        float _step;
        bool _built;

        /// <summary>이 띠가 실제로 그릴 수 있는 칸 수. 아직 안 만들었으면 0.</summary>
        public int Count => _icons.Count;

        /// <summary>
        /// 원본 아이콘을 받아 칸을 갖춘다. <paramref name="slots"/> 가 1 이면 복제하지 않는다.
        ///
        /// <paramref name="stepPixels"/> 가 0 이면 <b>아이콘 폭 + 2px</b> 를 쓴다 —
        /// 씬에서 크기를 바꾸면 간격이 따라오도록. 음수면 <b>왼쪽으로</b> 늘어놓는다
        /// (로스터 행처럼 오른쪽 끝에 붙어 있는 아이콘은 왼쪽으로 자라야 칸 밖으로 안 나간다).
        /// </summary>
        public void Build(Image origin, int slots, float stepPixels = 0f)
        {
            if (_built || origin == null) return;
            _built = true;

            _origin = origin;
            _icons.Add(origin);

            var rect = origin.rectTransform;
            float width = rect.rect.width;
            _step = Mathf.Approximately(stepPixels, 0f) ? width + 2f : stepPixels;

            for (int i = 1; i < Mathf.Max(1, slots); i++)
            {
                Image clone = Object.Instantiate(origin, origin.transform.parent);
                clone.name = origin.name + (i + 1);

                var cr = clone.rectTransform;
                cr.anchorMin = rect.anchorMin;
                cr.anchorMax = rect.anchorMax;
                cr.pivot = rect.pivot;
                cr.sizeDelta = rect.sizeDelta;
                cr.anchoredPosition = rect.anchoredPosition + new Vector2(_step * i, 0f);
                cr.localScale = rect.localScale;

                clone.sprite = null;
                clone.color = Color.clear;
                clone.raycastTarget = false;

                _icons.Add(clone);
            }
        }

        /// <summary>
        /// 이 캐릭터가 낀 유물을 칸에 그린다. 캐릭터가 없거나 장부가 없으면 전부 지운다.
        /// </summary>
        /// <returns>실제로 그린 유물 수 — 부르는 쪽이 «줄을 통째로 숨길지» 를 정할 때 쓴다.</returns>
        public int Refresh(CharacterUnit unit)
        {
            if (_icons.Count == 0) return 0;

            RelicInventory inv = RelicInventory.Instance;
            _scratch.Clear();
            if (inv != null && unit != null) inv.CollectEquipped(unit, _scratch);

            for (int i = 0; i < _icons.Count; i++)
            {
                Image img = _icons[i];
                if (img == null) continue;

                Sprite icon = i < _scratch.Count ? _scratch[i].icon : null;
                if (!ReferenceEquals(img.sprite, icon)) img.sprite = icon;
                img.color = icon != null ? Color.white : Color.clear;
            }

            return _scratch.Count;
        }

        /// <summary>칸을 전부 지운다 (캐릭터가 아닌 것을 골랐을 때).</summary>
        public void Clear()
        {
            for (int i = 0; i < _icons.Count; i++)
                if (_icons[i] != null) _icons[i].color = Color.clear;
        }
    }
}
