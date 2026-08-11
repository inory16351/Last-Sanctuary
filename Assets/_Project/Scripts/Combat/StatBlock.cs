using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 능력치 12종. 캐릭터 테이블(<c>캐릭터 테이블.xlsx</c> / first_Stat 시트)의 컬럼과 1:1 대응한다.
    ///
    /// <b>0~3 번은 절대 재배치하지 말 것</b> — 확장 이전부터 쓰던 값이라
    /// 이 순서를 바꾸면 기존에 직렬화된 값이 다른 능력치로 읽힌다. 새 능력치는 항상 뒤에 붙인다.
    /// 화면에 보이는 순서는 이 enum 이 아니라 UI 쪽 슬롯 배열이 정한다.
    ///
    /// 시야 · 사거리는 <b>모든 캐릭터가 같은 고정값을 쓰고 패시브 스킬로만 바뀌므로</b>
    /// 능력치에 넣지 않는다(유저 확정 2026-08-11). 그래서 테이블에도 컬럼이 없다.
    /// </summary>
    public enum StatType
    {
        Hp = 0,             // 체력            hp
        Attack = 1,         // 근거리 공격력   melee_atk   (몬스터·중립은 이 값 하나만 쓴다)
        Defense = 2,        // 방어력          def
        Regen = 3,          // 체력 재생       hp_recovery

        RangedAttack = 4,   // 원거리 공격력   ranged_atk
        Magic = 5,          // 마법            magic
        Cure = 6,           // 회복력          cure
        Accuracy = 7,       // 명중률          accuracy
        Critical = 8,       // 크리티컬 확률   critical
        AttackSpeed = 9,    // 공격 속도       atk_speed
        MoveSpeed = 10,     // 이동속도        movement_speed
        Resistance = 11,    // 저항력          resistance   ← 캐릭터 고유 고정값, 강화로 오르지 않는다

        COUNT = 12,
    }

    /// <summary>
    /// 능력치 묶음. 값은 유저에게 보여주는 값과 동일한 1~100 척도이며,
    /// 실제 게임플레이 수치는 BalanceConfigSO 의 치환 공식을 통해 얻는다.
    ///
    /// 몬스터·중립 몬스터는 <see cref="hp"/> / <see cref="attack"/> / <see cref="defense"/> /
    /// <see cref="regen"/> 네 개만 채워 쓴다 — 확장 전과 동작이 완전히 같다.
    /// </summary>
    [System.Serializable]
    public struct StatBlock
    {
        [Min(0)] public int hp;
        [Min(0)] public int attack;      // 근거리 공격력
        [Min(0)] public int defense;
        [Min(0)] public int regen;

        [Min(0)] public int rangedAttack;
        [Min(0)] public int magic;
        [Min(0)] public int cure;
        [Min(0)] public int accuracy;
        [Min(0)] public int critical;
        [Min(0)] public int attackSpeed;
        [Min(0)] public int moveSpeed;
        [Min(0)] public int resistance;

        public int this[StatType t]
        {
            get => t switch
            {
                StatType.Hp => hp,
                StatType.Attack => attack,
                StatType.Defense => defense,
                StatType.Regen => regen,
                StatType.RangedAttack => rangedAttack,
                StatType.Magic => magic,
                StatType.Cure => cure,
                StatType.Accuracy => accuracy,
                StatType.Critical => critical,
                StatType.AttackSpeed => attackSpeed,
                StatType.MoveSpeed => moveSpeed,
                StatType.Resistance => resistance,
                _ => 0,
            };
            set
            {
                switch (t)
                {
                    case StatType.Hp:           hp = value;           break;
                    case StatType.Attack:       attack = value;       break;
                    case StatType.Defense:      defense = value;      break;
                    case StatType.Regen:        regen = value;        break;
                    case StatType.RangedAttack: rangedAttack = value; break;
                    case StatType.Magic:        magic = value;        break;
                    case StatType.Cure:         cure = value;         break;
                    case StatType.Accuracy:     accuracy = value;     break;
                    case StatType.Critical:     critical = value;     break;
                    case StatType.AttackSpeed:  attackSpeed = value;  break;
                    case StatType.MoveSpeed:    moveSpeed = value;    break;
                    case StatType.Resistance:   resistance = value;   break;
                }
            }
        }

        /// <summary>
        /// 강화로 오르는 능력치인가. 저항력은 <b>캐릭터 고유의 고정 능력치</b>라 제외한다
        /// (캐릭터 가이드 p5 — "특히 저항력 등의 고정되는 캐릭터 고유의 능력치는 더욱 신중하게 결정").
        /// </summary>
        public static bool IsGrowable(StatType t) => t != StatType.Resistance;

        /// <summary>표시 이름. UI 라벨과 로그가 같은 문자열을 쓰게 한 곳에 모아둔다.</summary>
        public static string DisplayName(StatType t) => t switch
        {
            StatType.Hp => "체력",
            StatType.Attack => "근거리 공격력",
            StatType.Defense => "방어력",
            StatType.Regen => "체력 재생",
            StatType.RangedAttack => "원거리 공격력",
            StatType.Magic => "마법",
            StatType.Cure => "회복력",
            StatType.Accuracy => "명중률",
            StatType.Critical => "크리티컬 확률",
            StatType.AttackSpeed => "공격 속도",
            StatType.MoveSpeed => "이동속도",
            StatType.Resistance => "저항력",
            _ => t.ToString(),
        };

        public int Total => hp + attack + defense + regen;

        /// <summary>
        /// 프로토타입 4종만 [min, max] 균등 랜덤으로 채운다.
        /// 테이블 기반 캐릭터(<see cref="Units.CharacterDefinitionSO"/>)는 이 경로를 쓰지 않고
        /// 정의된 고정값을 그대로 받는다 — 이 메서드는 정의가 하나도 없을 때의 폴백용이다.
        /// </summary>
        public static StatBlock Roll(System.Random rng, int min, int max)
        {
            int Next() => rng.Next(min, max + 1);   // System.Random 은 상한 배타적
            return new StatBlock
            {
                hp = Next(), attack = Next(), defense = Next(), regen = Next(),
                // 확장 능력치도 같은 대역으로 채워야 신규 공식이 0으로 떨어지지 않는다
                rangedAttack = Next(), magic = Next(), cure = Next(),
                accuracy = Next(), critical = Next(), attackSpeed = Next(), moveSpeed = Next(),
                resistance = 50,   // 기준점 = 침식 배율 100%. 랜덤 캐릭터는 중립으로 둔다
            };
        }

        public StatBlock Clamped(int min, int max)
        {
            StatBlock r = this;
            for (int i = 0; i < (int)StatType.COUNT; i++)
            {
                var t = (StatType)i;
                r[t] = Mathf.Clamp(this[t], min, max);
            }
            return r;
        }

        public override string ToString() =>
            $"체력{hp} 근접{attack} 원거리{rangedAttack} 마법{magic} 회복{cure} 방어{defense} " +
            $"재생{regen} 명중{accuracy} 치명{critical} 공속{attackSpeed} 이속{moveSpeed} 저항{resistance}";
    }
}
