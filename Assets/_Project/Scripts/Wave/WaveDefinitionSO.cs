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

        [Tooltip("이 웨이브의 몬스터 능력치 배율(%, 정수). 표의 wave_mon_abil_per(0.6~2.63) × 100")]
        [Min(0)] public int statPercent;
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
