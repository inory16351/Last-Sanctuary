using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.Resource
{
    /// <summary>
    /// 플레이어(천사 진영)가 보유한 에너지 총량. 캐릭터 개인이 아니라 진영 전체가
    /// 공유하는 자원 풀이다 — 뒤에 붙을 캐릭터 강화 시스템이 이 풀에서 소비한다.
    /// 씬의 루트 오브젝트 하나(<c>WaveManager</c>와 같은 자리)에 부착한다.
    ///
    /// 일반 몬스터를 처치하면 자동으로 에너지를 얻는다 — <see cref="DamageableUnit.OnAnyDied"/>
    /// 정적 이벤트를 구독해서 잡는다(WaveManager가 넥서스 파괴를 잡는 방식과 동일).
    /// 중간보스/메인보스 처치 보상은 아직 정해지지 않아 지금은 대상에서 뺐다.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        [Header("획득량")]
        [Tooltip("일반 몬스터 한 마리를 처치할 때 얻는 에너지. 밸런싱 중이므로 에디터에서 바로 조정한다")]
        [Min(0)] [SerializeField] int energyPerMonsterKill = 10;

        [Header("시작값")]
        [Min(0)] [SerializeField] int startingEnergy = 0;

        [Header("디버그")]
        [Tooltip("획득/소비 로그를 콘솔에 남긴다. UI가 아직 없어 확인 수단이 이것뿐이다")]
        [SerializeField] bool logChanges = true;

        int _energy;

        /// <summary>지금 보유한 에너지 총량.</summary>
        public int Energy => _energy;

        /// <summary>총량이 바뀔 때마다 (변화량, 변화 후 총량). UI/강화 시스템이 구독하면 된다.</summary>
        public event System.Action<int, int> OnEnergyChanged;

        public static ResourceManager Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            _energy = Mathf.Max(0, startingEnergy);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() => DamageableUnit.OnAnyDied += HandleDeath;
        void OnDisable() => DamageableUnit.OnAnyDied -= HandleDeath;

        void HandleDeath(DamageableUnit unit)
        {
            // 중립 몬스터는 정의 테이블의 min~max_energy 범위에서 무작위 지급 (정찰 중 사냥 보상).
            if (unit is NeutralMonsterUnit neutral)
            {
                AddEnergy(neutral.RollEnergyReward());
                return;
            }

            // 몬스터(암세포 진영) 중에서도 일반 등급만 지금 보상 대상이다.
            if (unit.Faction != Faction.Cancer || unit.Kind != UnitKind.Monster) return;
            if (unit is MonsterUnit monster && monster.Tier != MonsterTier.Normal) return;

            AddEnergy(energyPerMonsterKill);
        }

        /// <summary>에너지를 더한다. 0 이하는 무시한다.</summary>
        public void AddEnergy(int amount)
        {
            if (amount <= 0) return;
            _energy += amount;
            if (logChanges) Debug.Log($"[Resource] 에너지 +{amount} → 총 {_energy}");
            OnEnergyChanged?.Invoke(amount, _energy);
        }

        /// <summary>보유량이 충분한지. 소비 전에 버튼 활성화 여부를 판단할 때 쓴다.</summary>
        public bool CanAfford(int amount) => amount <= 0 || _energy >= amount;

        /// <summary>
        /// 에너지를 소비한다. 부족하면 <b>아무것도 깎지 않고</b> false 를 돌려준다 —
        /// 호출한 쪽이 성공 여부만 보고 처리하면 되므로 잔액 검사가 흩어지지 않는다.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0) return false;
            if (amount == 0) return true;
            if (_energy < amount) return false;

            _energy -= amount;
            if (logChanges) Debug.Log($"[Resource] 에너지 -{amount} → 총 {_energy}");
            OnEnergyChanged?.Invoke(-amount, _energy);
            return true;
        }
    }
}
