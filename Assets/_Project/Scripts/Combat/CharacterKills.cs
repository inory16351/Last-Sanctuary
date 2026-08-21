using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 캐릭터 한 명의 <b>처치 기록과 영웅 각성 상태</b>. 유닛에 붙는 <b>상태 보관용</b>이고,
    /// 규칙과 수치는 전부 <see cref="HeroAwakeningService"/>(씬의 <c>GameSystems</c>)에 있다 —
    /// <see cref="CharacterErosion"/> ↔ <see cref="ErosionService"/> 와 완전히 같은 갈래다.
    ///
    /// <b>왜 유닛에 두나</b> — 캐릭터마다 따로 쌓이는 값이고, 캐릭터가 사라지면 같이 사라져야
    /// 한다. 서비스 쪽 <c>Dictionary</c> 에 들고 있으면 죽은 캐릭터의 기록이 남아 새는데,
    /// 컴포넌트로 두면 <c>Destroy</c> 가 알아서 치운다.
    ///
    /// ⚠ <b>능력치 칸(<see cref="StatBlock"/>)에는 넣지 않는다</b> — 그쪽은 캐릭터 테이블과
    /// 1:1 이라 표에 없는 값을 끼우면 파이프라인(<c>sync_tables_to_assets.py</c>)이 어긋난다.
    /// 「분노」(<see cref="CharacterPassives.Rage"/>)를 능력치가 아니라 패시브 쪽에 둔 것과 같은 판단이다.
    /// </summary>
    public class CharacterKills : MonoBehaviour
    {
        [Header("현재 상태 (읽기 전용 — 규칙과 수치는 GameSystems/HeroAwakeningService 에 있다)")]
        [Tooltip("이 캐릭터가 인정받은 처치 수. 같은 적을 여럿이 때렸으면 그 전원이 각각 1 을 받는다")]
        [SerializeField] int kills;

        // ★★ <b>회복 횟수</b> (2026-08-21 · 유저 지시: *"힐러는 회복 횟수를 카운트해서
        //   회복을 200번 사용하면 영웅 각성이 가능한 상태로 만들어줘"*).
        //
        //   <b>왜 처치와 따로 세나</b> — 힐러는 처치를 거의 못 한다. 각성 조건이 처치뿐이면
        //   회복 유형 캐릭터는 <b>영웅이 될 길이 없다</b>. 그래서 «회복도 같은 방식으로 쌓이는
        //   또 하나의 길» 로 뒀다.
        //   ★ «힐러인가» 를 <b>묻지 않는다</b> — 회복을 쓰면 세고, 처치를 하면 센다.
        //     인물이나 유형으로 갈래를 나누면 유형을 바꿀 때마다 규칙이 흔들린다
        //     (이 프로젝트가 스킬을 «슬롯 번호가 아니라 종류» 로 판정하는 것과 같은 원칙).
        [Tooltip("이 캐릭터가 <b>회복 공격을 성공시킨</b> 횟수. 영웅 각성의 두 번째 길이다")]
        [SerializeField] int heals;

        [Tooltip("영웅 각성 횟수. 1 이상이면 이 캐릭터는 '영웅' 이다")]
        [SerializeField] int awakenings;

        [Tooltip("각성으로 <b>영구히</b> 오른 능력치. StatType 순서와 1:1 이다. " +
                 "화면 표시가 아니라 <b>밸런싱용 장부</b>다 — 자세한 이유는 아래 주석 참조")]
        [SerializeField] int[] awakenBonus;

        CharacterUnit _unit;

        /// <summary>이 기록의 주인.</summary>
        public CharacterUnit Unit
        {
            get
            {
                if (_unit == null) _unit = GetComponent<CharacterUnit>();
                return _unit;
            }
        }

        /// <summary>지금까지 인정받은 처치 수.</summary>
        public int Kills => kills;

        /// <summary>지금까지 성공시킨 회복 횟수 (영웅 각성의 두 번째 길).</summary>
        public int Heals => heals;

        /// <summary>영웅 각성 횟수. 0 이면 아직 평범한 캐릭터다.</summary>
        public int Awakenings => awakenings;

        /// <summary>이 캐릭터가 영웅인가.</summary>
        public bool IsHero => awakenings > 0;

        /// <summary>
        /// 기록 컴포넌트를 보장한다. 없으면 붙여서 돌려준다.
        ///
        /// <b>템플릿에 미리 넣지 않고 여기서 붙이는 이유</b> — 씬의 캐릭터 템플릿이 브랜치
        /// 재동기화로 되돌아가는 사고가 이 프로젝트에서 두 번 있었고(진행상황 28-3·28-4절),
        /// 그때 이 컴포넌트가 빠지면 처치 기록이 <b>조용히 0 으로 고정</b>된다.
        /// <see cref="CharacterErosion.EnsureOn"/> 이 같은 이유로 같은 안전망을 쓴다.
        /// </summary>
        public static CharacterKills EnsureOn(CharacterUnit unit)
        {
            if (unit == null) return null;
            if (unit.TryGetComponent(out CharacterKills existing)) return existing;
            return unit.gameObject.AddComponent<CharacterKills>();
        }

        /// <summary>붙어 있으면 돌려주고, 없으면 null — 표시용 조회에 쓴다(붙이지 않는다).</summary>
        public static CharacterKills Of(CharacterUnit unit) =>
            unit != null && unit.TryGetComponent(out CharacterKills k) ? k : null;

        /// <summary>처치 하나를 인정한다. 부르는 곳은 <see cref="HeroAwakeningService"/> 하나다.</summary>
        public void AddKill() => kills++;

        /// <summary>회복 하나를 인정한다. 부르는 곳은 <see cref="HeroAwakeningService"/> 하나다.</summary>
        public void AddHeal() => heals++;

        /// <summary>각성 횟수를 1 올린다. 능력치를 실제로 올리는 것은 서비스가 한다.</summary>
        public void RegisterAwakening() => awakenings++;

        /// <summary>
        /// <b>각성으로 영구히 오른 양</b>을 기록한다. 능력치를 실제로 올리는 것은
        /// <see cref="CharacterUnit.AddFlatStatBonus"/> 이고, 여기 적는 것은 <b>장부</b>다.
        ///
        /// <b>화면에는 쓰지 않는다</b> — 성장 창은 <see cref="CharacterUnit.EffectiveStat"/>
        /// (지금 실제로 적용되는 값)를 그린다(유저 확정 2026-08-18). 이 장부는
        /// <b>"각성이 실제로 얼마를 줬나"</b> 를 답하는 유일한 기록이라 남긴다 —
        /// <c>EffectiveStat</c> 는 임시 보정(「광란」·「희열」·정신 이상 「각성」)이 섞여 있어
        /// 그 질문에 답하지 못하고, 밸런싱할 때 반드시 필요한 값이다. 인스펙터에서 바로 보인다.
        ///
        /// ⚠ 서비스의 설정값(<c>awakenStatBonus</c>)에서 <b>다시 계산하면 안 된다.</b>
        /// 성장 유형은 각성 뒤에도 바뀔 수 있고 인스펙터 값도 도중에 바뀔 수 있다 —
        /// 그러면 장부가 사실과 어긋난다. <b>걸던 순간의 값</b>을 그대로 적는다.
        /// </summary>
        public void RecordAwakenBonus(StatType type, int amount)
        {
            if (amount == 0) return;

            awakenBonus ??= new int[(int)StatType.COUNT];
            if (awakenBonus.Length < (int)StatType.COUNT)
                System.Array.Resize(ref awakenBonus, (int)StatType.COUNT);

            awakenBonus[(int)type] += amount;
        }

        /// <summary>
        /// 저장에서 처치 수·각성 횟수를 되돌린다 (98절).
        /// <see cref="AddKill"/>·<see cref="RegisterAwakening"/> 는 "하나 늘린다"는 게임 규칙이고,
        /// 복원은 "그때 그 값이었다"를 재현하는 것이라 규칙을 타면 안 된다.
        ///
        /// ⚠ 각성으로 오른 <b>능력치</b>는 여기서 되돌리지 않는다 —
        /// <see cref="CharacterUnit.AddFlatStatBonus"/> 로 실제 보정을 걸고
        /// <see cref="RecordAwakenBonus"/> 로 장부를 채우는 것은 부르는 쪽 책임이다.
        /// </summary>
        public void RestoreCounts(int killCount, int awakenCount)
        {
            kills = Mathf.Max(0, killCount);
            awakenings = Mathf.Max(0, awakenCount);
        }

        /// <summary>
        /// 저장에서 <b>회복 횟수</b>까지 되돌린다 (2026-08-21).
        ///
        /// ★ 오버로드를 <b>따로</b> 뒀다 — 옛 세이브에는 회복 칸이 없으므로 위 두 인자
        ///   버전을 그대로 남겨 둔다(부르는 쪽이 안 바뀌면 옛 동작 그대로다).
        /// </summary>
        public void RestoreCounts(int killCount, int awakenCount, int healCount)
        {
            RestoreCounts(killCount, awakenCount);
            heals = Mathf.Max(0, healCount);
        }

        /// <summary>각성으로 영구히 오른 양. 각성한 적이 없으면 0.</summary>
        public int AwakenBonus(StatType type)
        {
            int i = (int)type;
            return awakenBonus != null && i >= 0 && i < awakenBonus.Length ? awakenBonus[i] : 0;
        }
    }
}
