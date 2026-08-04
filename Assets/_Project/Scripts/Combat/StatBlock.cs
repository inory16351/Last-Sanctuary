using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>프로토타입 능력치 4종. 정식 버전에서 14종으로 확장된다.</summary>
    public enum StatType
    {
        Hp = 0,        // 체력
        Attack = 1,    // 공격력
        Defense = 2,   // 방어력
        Regen = 3,     // 체력 회복력
        COUNT = 4,
    }

    /// <summary>
    /// 능력치 묶음. 값은 유저에게 보여주는 값과 동일한 1~100 척도이며,
    /// 실제 게임플레이 수치는 BalanceConfigSO 의 치환 공식을 통해 얻는다.
    /// </summary>
    [System.Serializable]
    public struct StatBlock
    {
        [Min(1)] public int hp;
        [Min(1)] public int attack;
        [Min(1)] public int defense;
        [Min(1)] public int regen;

        public int this[StatType t]
        {
            get => t switch
            {
                StatType.Hp => hp,
                StatType.Attack => attack,
                StatType.Defense => defense,
                StatType.Regen => regen,
                _ => 0,
            };
            set
            {
                switch (t)
                {
                    case StatType.Hp:      hp = value;      break;
                    case StatType.Attack:  attack = value;  break;
                    case StatType.Defense: defense = value; break;
                    case StatType.Regen:   regen = value;   break;
                }
            }
        }

        public int Total => hp + attack + defense + regen;

        /// <summary>모든 능력치를 [min, max] 균등 랜덤으로 채운다. 캐릭터 생성 규칙.</summary>
        public static StatBlock Roll(System.Random rng, int min, int max)
        {
            int Next() => rng.Next(min, max + 1);   // System.Random 은 상한 배타적
            return new StatBlock { hp = Next(), attack = Next(), defense = Next(), regen = Next() };
        }

        public StatBlock Clamped(int min, int max) => new StatBlock
        {
            hp      = Mathf.Clamp(hp, min, max),
            attack  = Mathf.Clamp(attack, min, max),
            defense = Mathf.Clamp(defense, min, max),
            regen   = Mathf.Clamp(regen, min, max),
        };

        public override string ToString() =>
            $"체력{hp} 공격{attack} 방어{defense} 회복{regen} (합 {Total})";
    }
}
