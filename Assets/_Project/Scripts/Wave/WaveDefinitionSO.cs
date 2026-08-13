using UnityEngine;

namespace LastSanctuary.Wave
{
    /// <summary>웨이브 하나의 구성 — `데이터 테이블/웨이브테이블.xlsx` 한 행 그대로.</summary>
    [System.Serializable]
    public struct WaveMonsterComposition
    {
        [Min(1)] public int waveNumber;
        [Min(0)] public int meleeCount;
        [Min(0)] public int rangedCount;
        [Min(0)] public int bossCount;

        [Tooltip("중간보스 마리 수. 표(`웨이브테이블.xlsx` Sheet2 의 `mid_boss_mon_num`)의 값이며 " +
                 "5·15웨이브가 1이다.\n" +
                 "★ 어느 중간보스가 나오는지는 이 값이 정하지 않는다 — " +
                 "`웨이브 몬스터 테이블.xlsx` 의 `wave_mid_boss.spawn_percent`(0.5/0.5)가 정하고, " +
                 "게임에서는 `MonsterSpawner.midBossSlots` 의 가중치가 그 역할을 한다.\n" +
                 "웨이브 기획서 p4 의 \"5번째 웨이브 – 중간 보스 등장\" 이 23절부터 미구현으로 " +
                 "남아 있던 항목이다(진행상황 54-5절에서 표에 컬럼이 신설됐다)")]
        [Min(0)] public int midBossCount;

        [Tooltip("이 웨이브의 몬스터 능력치 배율(%, 정수). 표의 wave_mon_abil_per(0.6~2.63) × 100")]
        [Min(0)] public int statPercent;

        [Tooltip("전투 중 증원이 오는 간격(초). 표에 없던 값 — \"광폭화가 거의 안 걸린다\" 는 " +
                 "피드백으로 새로 추가했다. meleeCount+rangedCount+bossCount(기본 마리 수)의 20%를 " +
                 "ceil 해서 증원 수로, 28-웨이브번호를 15~28 사이로 clamp 해서 간격으로 임의 계산했다 " +
                 "(웨이브가 오를수록 더 자주·많이 온다) — 밸런스가 안 맞으면 이 표만 고치면 된다")]
        [Min(0)] public float reinforceIntervalSeconds;

        [Tooltip("증원 한 번에 오는 마리 수(근거리/원거리 절반씩, 보스는 포함하지 않는다). " +
                 "0이면 이 웨이브는 증원이 오지 않는다")]
        [Min(0)] public int reinforceCount;

        [Tooltip("★ <b>포탈 한 곳에서 한 번에 튀어나오는 마리 수</b> " +
                 "(표 `웨이브테이블.xlsx` Sheet2 의 `spawn_group_size`, 2026-08-13 신설).\n" +
                 "유저 지시: \"포탈에서 몬스터 등장 시 여러 마리 나오게 — 각개 격파가 너무 " +
                 "잘돼서 디펜스 느낌이 안 남\".\n" +
                 "예전에는 한 마리씩, 그것도 <b>포탈을 돌아가며</b> 내보내서 어느 순간에도 " +
                 "화면에는 서로 다른 방향에서 온 한 마리씩만 있었다 — 캐릭터가 차례로 하나씩 " +
                 "처리하면 끝이었다. 이제 무리 하나가 <b>같은 포탈에서 통째로</b> 나와 같이 " +
                 "걸어온다.\n" +
                 "총 마리 수·능력치는 그대로다 — 나오는 <b>방식</b>만 바뀐다. " +
                 "0·1 이면 예전과 완전히 같은 동작이다")]
        [Min(0)] public int spawnGroupSize;
    }

    /// <summary>
    /// 웨이브별 몬스터 구성표 — `테이블/웨이브테이블.xlsx` 를 그대로 옮긴 데이터.
    /// <see cref="LastSanctuary.Units.MonsterSpawner"/> 가 웨이브 번호로 조회해서 쓴다.
    ///
    /// 표 이전엔 "웨이브 번호를 바꿔도 몬스터 구성은 안 바뀌고 스탯 배율(선형 공식)만 커졌다"
    /// (진행상황 6절) — 이 표가 그 구성표 역할을 실제로 채운다. 근거리·원거리 수량은 웨이브마다
    /// 다르고, 능력치 배율도 표에 적힌 값(선형이 아니다: 60% → 70% → 80% … → 263%)을 그대로 쓴다.
    ///
    /// ⚠️ 표는 20웨이브까지만 있다. 그 이후(21웨이브~)의 규칙은 기획 미확정이라
    /// <see cref="GetWaveOrExtrapolate"/> 는 일단 마지막 행(20웨이브)을 그대로 반복한다 —
    /// 진행상황 10절의 "총 스테이지 수 미확정" 항목과 이어지는 임시 처리다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Wave/Wave Definitions", fileName = "WaveDefinitions")]
    public class WaveDefinitionSO : ScriptableObject
    {
        public WaveMonsterComposition[] waves;

        /// <summary>정확히 그 웨이브 번호의 행이 있으면 돌려준다.</summary>
        public bool TryGetWave(int waveNumber, out WaveMonsterComposition composition)
        {
            if (waves != null)
            {
                for (int i = 0; i < waves.Length; i++)
                {
                    if (waves[i].waveNumber != waveNumber) continue;
                    composition = waves[i];
                    return true;
                }
            }
            composition = default;
            return false;
        }

        /// <summary>
        /// 표에 있으면 그 행, 표 밖(마지막 웨이브보다 큼)이면 마지막 행을 그대로 돌려준다.
        /// 표가 비어 있으면 기본값(전부 0)을 돌려준다 — 호출부가 "표 없음"과 구분해야 하면
        /// <see cref="TryGetWave"/> 를 직접 쓸 것.
        /// </summary>
        public WaveMonsterComposition GetWaveOrExtrapolate(int waveNumber)
        {
            if (waves == null || waves.Length == 0) return default;
            if (TryGetWave(waveNumber, out var exact)) return exact;

            WaveMonsterComposition last = waves[0];
            for (int i = 1; i < waves.Length; i++)
                if (waves[i].waveNumber > last.waveNumber) last = waves[i];
            return last;
        }
    }
}
