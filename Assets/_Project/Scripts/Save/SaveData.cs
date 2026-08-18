using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Save
{
    /// <summary>
    /// 저장 파일 한 벌. <b>슬롯은 하나뿐</b>이다(유저 확정 2026-08-18) — 기본이 자동 저장이라
    /// 유저가 슬롯을 고르는 순간이 없다.
    ///
    /// <b>왜 통째로 저장하는가</b>(유저 확정) — 자동 저장 시점이 「되돌리면 이득을 보는 순간」
    /// (강화 · 사망 · 웨이브 클리어)이라, 웨이브 <b>도중</b>에도 저장이 일어난다. 진행도만
    /// 저장하고 웨이브 시작 지점으로 되돌리면 <b>죽은 그 웨이브를 처음부터 다시</b> 하게 되어
    /// 자동 저장이 오히려 되돌리기 수단이 된다. 그래서 살아있는 몬스터 · 유닛 위치 · 체력 ·
    /// 안개까지 담는다.
    ///
    /// <b>JsonUtility 로 직렬화한다</b> — 이 프로젝트는 외부 의존이 없고, 저장할 것이
    /// 전부 <c>[System.Serializable]</c> 로 표현 가능한 값 타입이라 그것으로 충분하다.
    /// ⚠ JsonUtility 는 <c>Dictionary</c> 와 <c>null</c> 필드를 못 다룬다 — 사전이 필요한 곳은
    /// <b>키 목록 + 값 목록 두 배열</b>로 편다(<see cref="CharacterSave.awakenBonusStats"/>).
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        /// <summary>
        /// 저장 형식 판(版). 구조를 바꾸면 올린다 — 옛 파일을 읽었을 때
        /// <see cref="SaveService"/> 가 조용히 깨진 상태로 복원하지 않고 거부하기 위한 칸이다.
        /// </summary>
        public int version = CurrentVersion;

        public const int CurrentVersion = 1;

        /// <summary>사람이 읽을 수 있는 저장 시각. 로비의 "이어하기" 설명에 쓴다.</summary>
        public string savedAt = string.Empty;

        /// <summary>무엇이 저장을 일으켰는지(강화 / 사망 / 웨이브 클리어 / 수동). 디버깅용.</summary>
        public string reason = string.Empty;

        // ── 진행 ──
        public int waveNumber = 1;

        /// <summary><c>(int)WavePhase</c>. enum 을 그대로 넣으면 판이 바뀔 때 값이 밀린다.</summary>
        public int wavePhase;

        public float phaseRemaining;
        public int energy;

        // ── 유닛 ──
        public List<CharacterSave> characters = new List<CharacterSave>();
        public List<SquadSave> squads = new List<SquadSave>();
        public List<TowerSave> towers = new List<TowerSave>();
        public List<MonsterSave> monsters = new List<MonsterSave>();
        public List<NeutralSave> neutrals = new List<NeutralSave>();

        // ── 월드 ──
        // 밝혀진 칸은 칸 하나에 <b>비트 하나</b>를 쓰고 base64 로 적는다 — 맵이 320x320 이면
        // 102,400칸이라 칸마다 숫자를 적으면 저장 파일이 수백 KB가 된다.
        public string fogExplored = string.Empty;

        public int fogWidth;
        public int fogHeight;

        /// <summary>넥서스 체력. 0 이하로 저장되는 일은 없다(그 판은 이미 패배다).</summary>
        public int nexusHp;

        // ── 토벌 (2026-08-18) ──
        //
        // ★★ <b>발견은 "지금 보이는가"로 판정되므로 저장하지 않으면 불러온 순간 비어버린다.</b>
        // 안개의 <b>밝힌 칸</b>은 복원되지만 <b>지금 시야</b>는 유닛 위치로 매 프레임 다시
        // 계산되는 값이다 — <c>EpicSubjugationService.RestoreState</c> 주석 참조.

        /// <summary>발견한 에픽들의 <c>NeutralMonsterUnit.SpawnId</c>.</summary>
        public List<int> subjugationDiscovered = new List<int>();

        /// <summary>토벌 지시를 걸어둔 부대 id. 아래 목록과 <b>같은 순서로 짝</b>지어진다.</summary>
        public List<int> subjugationOrderSquads = new List<int>();

        /// <summary>위 부대가 노리던 개체의 <c>SpawnId</c>.</summary>
        public List<int> subjugationOrderTargets = new List<int>();

        // ── 중립 재생성 대기 (2026-08-18, 유저 지시 "타이머 넣어서") ──
        //
        // ⚠ <b>남은 초</b>로 담는다. <c>Time.time</c> 은 씬을 새로 부르면 0 부터 다시 시작하므로
        // 절대 시각을 적으면 아무 뜻이 없다 — <c>NeutralMonsterSpawner.ExportRestockDelays</c> 참조.

        /// <summary>재생성 대기가 걸려 있는 종의 <c>monId</c>.</summary>
        public List<int> neutralRestockMonIds = new List<int>();

        /// <summary>위 종의 남은 대기 시간(초). 같은 순서로 짝지어진다.</summary>
        public List<float> neutralRestockSeconds = new List<float>();
    }

    /// <summary>캐릭터 한 명. 능력치·성장·상태를 전부 담는다.</summary>
    [System.Serializable]
    public class CharacterSave
    {
        /// <summary>
        /// 캐릭터 테이블의 <c>characterId</c>. <b>에셋 이름이 아니라 id 로 적는다</b> —
        /// 에셋 파일명은 언제든 바뀔 수 있지만 id 는 표가 정한 정본이다.
        /// 0 이면 정의 없이 능력치만 굴려 만든 캐릭터다(<see cref="StatBlock.Roll"/> 폴백).
        /// </summary>
        public int characterId;

        /// <summary>정의가 없는 캐릭터를 되살리기 위한 원시 능력치. 정의가 있어도 그대로 쓴다 —
        /// 강화로 올라간 값이 정의의 기본값과 다르기 때문이다.</summary>
        public StatBlock stats;

        public int upgradeCount;

        /// <summary><c>(int)StatGrowthFocus</c>.</summary>
        public int growthFocus;

        public int currentHp;
        public Vector3 position;

        /// <summary>전술 지침 한 벌. <c>TacticalOrder</c> 는 이미 <c>[System.Serializable]</c> 이다.</summary>
        public TacticalOrder order = new TacticalOrder();

        /// <summary>소속 부대 id. 0 이면 무소속.</summary>
        public int squadId;

        // ── 성장 기록 ──
        public int kills;
        public int awakenings;

        /// <summary>영웅 각성으로 붙은 고정 보정 — 능력치 종류(<c>(int)StatType</c>).</summary>
        public List<int> awakenBonusStats = new List<int>();

        /// <summary>위 목록과 <b>같은 순서로 짝</b>지어지는 보정값.</summary>
        public List<int> awakenBonusAmounts = new List<int>();

        // ── 침식 ──
        public float erosion;

        /// <summary>지금 걸려 있는 정신 이상의 <c>(int)MentalErrorType</c>. 0(None) 이면 없음.</summary>
        public int mentalErrorType;
    }

    /// <summary>부대 한 개. 집결지는 부대마다 하나다(47절).</summary>
    [System.Serializable]
    public class SquadSave
    {
        public int id;
        public string name = string.Empty;
        public bool coopExpedition = true;

        /// <summary>집결지가 설정돼 있는가. <c>Vector3</c> 는 null 이 될 수 없어 별도 칸이 필요하다.</summary>
        public bool hasRallyPoint;
        public Vector3 rallyPoint;
    }

    /// <summary>완성된 포탑 하나. 건설 <b>중</b>인 자리는 저장하지 않는다 — 89-x절의 건설 진행도는
    /// 캐릭터의 작업 배정과 얽혀 있어 되살리면 배정이 어긋난다. 진행 중이던 건설은 취소된다.</summary>
    [System.Serializable]
    public class TowerSave
    {
        public Vector3Int minCell;
        public int currentHp;
    }

    /// <summary>
    /// 웨이브 몬스터 한 마리.
    ///
    /// ⚠ <b>정의를 에셋 이름으로 가리킨다</b> — 캐릭터와 달리 <c>MonsterDefinitionSO</c> 에는
    /// id 칸이 없다(표의 몬스터는 웨이브 표의 <b>슬롯</b>으로 지목되지 한 마리씩 번호를 갖지 않는다).
    /// 에셋 이름을 바꾸면 그 이름으로 저장된 세이브의 그 몬스터만 복원되지 않는다 —
    /// 몬스터 에셋 이름을 바꿀 일이 생기면 <see cref="SaveData.CurrentVersion"/> 을 올릴 것.
    /// </summary>
    [System.Serializable]
    public class MonsterSave
    {
        public string definitionName = string.Empty;
        public Vector3 position;
        public int currentHp;

        /// <summary>
        /// <b>웨이브 배율이 이미 반영된</b> 능력치. 배율(%)이 아니라 결과값을 담는 이유 —
        /// 광폭화가 배율을 계속 올리고 있어서(<c>MonsterSpawner.SetEnragePercent</c>),
        /// 배율만 적으면 "그때 몇 %였는지"를 다시 계산해야 하고 그 계산이 표 개정마다 흔들린다.
        /// </summary>
        public StatBlock stats;
    }

    /// <summary>
    /// 중립 몬스터 한 마리 (유저 지시 2026-08-18: <i>"중립 몬스터의 소환된 숫자와 서식지
    /// 위치는 유지하는 로직으로 만들어줘"</i>).
    ///
    /// ★ <b>개체를 하나씩 담으므로 "소환된 숫자"가 저절로 유지된다.</b> 마리 수만 세어 담고
    /// 복원 때 그만큼 새로 뽑는 방법도 있지만, 그러면 <b>있던 자리가 아니라 아무 데나</b>
    /// 다시 태어나 같이 요구된 "서식지 위치 유지"와 어긋난다.
    ///
    /// ★ <b>서식지는 칸을 담지 않는다.</b> 모양이 (중심 칸 · 반지름 · 씨앗) 셋으로 완전히
    /// 결정되므로(<c>NeutralHabitat</c>), 그 셋만 담으면 수천 칸이 같은 모양으로 다시 그려진다.
    /// 반지름은 표에 있으니 실제로 담는 것은 <b>중심 칸과 씨앗 둘</b>이다.
    /// </summary>
    [System.Serializable]
    public class NeutralSave
    {
        /// <summary>중립 몬스터 표의 <c>monId</c>. 웨이브 몬스터와 달리 중립 정의에는 id 칸이 있다.</summary>
        public int monId;

        public Vector3 position;
        public int currentHp;

        /// <summary>배회의 기준이 되는 자리. 여기서 얼마나 멀어질 수 있는지가 표로 정해진다.</summary>
        public Vector3 homePosition;

        /// <summary>서식지를 가진 개체인가 (에픽만 그린다).</summary>
        public bool hasHabitat;

        /// <summary>서식지 중심 칸. ⚠ <b>지금 서 있는 자리가 아니다</b> — 에픽은 맞으면 서식지
        /// 밖까지 쫓아 나가므로, 그때 자리를 중심으로 삼으면 불러올 때마다 서식지가 밀려난다.</summary>
        public Vector3Int habitatCell;

        /// <summary>서식지 모양을 만든 씨앗.</summary>
        public int habitatSeed;

        /// <summary>
        /// 개체를 가리키는 런타임 번호(<c>NeutralMonsterUnit.SpawnId</c>). 토벌 발견 목록·
        /// 토벌 지시가 이 번호로 <b>같은 마리</b>를 다시 찾는다.
        ///
        /// ⚠ 0 이면 이 칸이 생기기 전(2026-08-18 이전)에 저장된 파일이다 — 복원할 때 스포너가
        /// 새 번호를 매기고, 그 세이브의 토벌 목록은 아무 개체와도 안 이어져 비게 된다.
        /// 조용히 빈 목록이 되는 것이 잘못된 마리를 가리키는 것보다 낫다.
        /// </summary>
        public int spawnId;
    }
}
