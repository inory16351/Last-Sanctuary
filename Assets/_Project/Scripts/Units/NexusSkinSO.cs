using UnityEngine;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 넥서스(중앙 건물)의 <b>외형 한 벌</b>. 원본은 볼트의
    /// <c>리소스/asset/Tower_Asset/Nexus_Spr.png</c> 이고 <c>Tools/nexus_skin_build.py</c> →
    /// <c>Tools/gen_nexus_skin.py</c> 가 그대로 옮긴다 — <b>손으로 배선하지 않는다.</b>
    ///
    /// <b>왜 <see cref="Combat.CharacterSkinSO"/> 를 안 쓰나</b>
    /// ------------------------------------------------------
    /// 그쪽은 <b>싸우는 유닛</b>의 외형이다 — 좌/우 두 벌, 대기·이동·공격·스킬 슬롯,
    /// 투사체·착탄. 넥서스는 <b>움직이지도 공격하지도 않고</b>, 대신 그쪽에 없는 축이 있다:
    /// <b>체력 구간에 따라 대기 모션이 통째로 바뀐다</b>(원화 시트가 그렇게 그려져 있다).
    /// 남는 칸이 스무 개인 자료형을 물려받으면 "이 칸은 왜 비었지"가 계속 생긴다.
    ///
    /// <b>왜 <c>Resources</c> 폴더인가</b> — MCP 로는 씬에 오브젝트 참조를 써넣을 수 없어서
    /// (진행상황 8절 4번), 이 프로젝트는 스킨·BGM·스킬을 전부 <c>Resources.Load</c> 로
    /// 배선해 왔다. <see cref="NexusAnimator"/> 도 <b>폴더 이름 문자열</b>만 들고 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Units/Nexus Skin", fileName = "Skin_Nexus")]
    public class NexusSkinSO : ScriptableObject
    {
        [Tooltip("표시용 이름(디버그). 게임 문구는 스트링 테이블이 정한다")]
        public string displayName = "";

        [Tooltip("초당 프레임. 원화 시트의 지시가 \"Unity에서 프레임 속도 6~8 FPS 권장\" 이다")]
        [Min(1f)] public float framesPerSecond = 7f;

        [Header("대기 — 체력 구간별 (원화 시트 그대로)")]
        [Tooltip("체력 50% 이상 — \"심장 박동 강함\"")]
        public Sprite[] idleHigh;

        [Tooltip("체력 10~50% — \"심장 박동 약화 · 균열 및 손상\"")]
        public Sprite[] idleMid;

        [Tooltip("체력 10% 이하 — \"심장 박동 불규칙 · 심각한 손상 및 붕괴\"")]
        public Sprite[] idleLow;

        [Header("파괴")]
        [Tooltip("파괴 모션. <b>순환하지 않는다</b> — 마지막 프레임에서 멈춘다" +
                 "(시트의 \"완전 파괴 후 정지\")")]
        public Sprite[] destroy;

        /// <summary>
        /// 체력 비율(0~1)에 맞는 대기 모션. 비어 있는 구간은 <b>위 단계</b>로 떨어진다 —
        /// 원화가 덜 그려졌을 때 아무것도 안 나오는 것보다 낫다.
        /// </summary>
        public Sprite[] IdleFor(float hpRatio)
        {
            if (hpRatio <= 0.10f && Has(idleLow)) return idleLow;
            if (hpRatio <= 0.50f && Has(idleMid)) return idleMid;
            if (Has(idleHigh)) return idleHigh;
            return Has(idleMid) ? idleMid : idleLow;
        }

        /// <summary>파괴 모션이 그려져 있는지.</summary>
        public bool HasDestroy => Has(destroy);

        /// <summary>이 스킨이 쓸 만한지 — 대기 모션이 하나라도 있어야 한다.</summary>
        public bool IsUsable => Has(idleHigh) || Has(idleMid) || Has(idleLow);

        static bool Has(Sprite[] frames)
        {
            if (frames == null) return false;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) return true;
            return false;
        }
    }
}
