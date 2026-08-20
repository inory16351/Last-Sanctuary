using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 소환 모션이 도는 동안 <b>싸우지도 움직이지도 않게</b> 잡아 두는 한 번짜리 부품.
    ///
    /// <b>왜 코루틴이 아닌가</b> — 이 프로젝트는 «캐릭터가 프레임 중간에 Instantiate 된다»는
    /// 이유로 유닛의 <c>Update</c> 를 서비스로 올려 두었다(<see cref="PassiveSkillService"/>
    /// 주석). 그 규칙을 깨지 않으면서 «시간이 지나면 스스로 사라지는» 것을 표현하는 가장
    /// 작은 방법이 이 컴포넌트다 — 다 하면 자기 자신을 지운다.
    /// </summary>
    public class SummonDelay : MonoBehaviour
    {
        float _until;
        UnitCombat _combat;

        public void Begin(float seconds)
        {
            _combat = GetComponent<UnitCombat>();
            _until = Time.time + seconds;
            if (_combat != null)
            {
                _combat.SetCombatSuppressed(true);
                _combat.SetImmobile(true);
            }
        }

        void Update()
        {
            if (Time.time < _until) return;
            if (_combat != null)
            {
                _combat.SetCombatSuppressed(false);
                _combat.SetImmobile(false);
            }
            Destroy(this);
        }
    }
}
