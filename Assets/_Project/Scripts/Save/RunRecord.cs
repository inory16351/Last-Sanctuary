using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.Save
{
    /// <summary>
    /// ★★ <b>한 판의 «누가 있었나» 기록</b> (2026-08-25 신설 — 엔딩의 전사자 명단을 위해).
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  왜 새로 만드나 — <b>죽은 이름을 아무도 안 들고 있었다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 엔딩 컷 2 의 자막이 «그 이름은 성역에 새겨질 것이다» 다. 그 뒤에 <b>실제 이름</b>이
    /// 떠야 그 문장이 빈말이 아니게 된다. 그런데 프로젝트 어디에도 «이 판에서 죽은 인물의
    /// 이름» 을 남기는 자리가 없었다 —
    ///
    /// * <see cref="CharacterDefinitionRegistry"/> 는 «지금 살아 있는 id» 만 센다
    ///   (죽으면 후보로 <b>돌려보내는</b> 것이 그 클래스의 목적이다).
    /// * <c>SaveData</c> 는 <b>살아 있는</b> 유닛만 담는다(죽은 것을 담으면 복원 때
    ///   시체가 되살아난다).
    /// * <c>HudLog</c> 는 줄 수 상한이 있어 <b>오래된 죽음이 밀려 사라진다</b>.
    ///
    /// 그래서 «기록» 이라는 일을 맡는 자리를 하나 새로 뒀다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ⚠⚠ <b>static 이므로 판이 끝나면 반드시 비워야 한다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 이 프로젝트가 이미 한 번 크게 데인 자리다 — <see cref="RunResetService"/> 가
    /// 생긴 이유가 «판 전역 static 기록이 씬을 다시 열어도 살아남는다» 였다
    /// («캐릭터 생성이 안 되는» 버그). 여기서 같은 사고가 나면 <b>지난 판의 전사자가
    /// 다음 판 엔딩에 섞인다</b>.
    ///
    /// 그래서 <see cref="Clear"/> 를 부르는 곳을 <b>한 곳으로</b> 못박았다 —
    /// <see cref="RunResetService.BeginNewRun"/> 이다. 그 함수가 «새 판을 시작한다» 의
    /// 유일한 정의이므로, 새 경로가 하나 더 생겨도 거기를 지나면 자동으로 비워진다.
    ///
    /// ⚠ <see cref="RuntimeInitializeOnLoadMethod"/> 로도 한 번 비운다 — 에디터에서
    ///   도메인 리로드 없이 플레이를 반복할 때를 위한 안전망이다. 그것만으로는
    ///   <b>부족하다</b>(프로세스마다 한 번만 돈다 — RunResetService 주석의 그 함정).
    ///
    /// ★ <b>씬을 건너서 살아남는 것이 목적이다.</b> 엔딩은 <b>다른 씬</b>이므로
    ///   (Proto_01 → Ending) 컴포넌트에 담으면 씬 전환에서 사라진다. static 인 것은
    ///   게으름이 아니라 요구사항이다.
    /// </summary>
    public static class RunRecord
    {
        /// <summary>이름 한 줄 — 명단에 그릴 최소한의 것.</summary>
        public struct Entry
        {
            public string name;
            public string title;
            public int level;

            /// <summary>쓰러진 웨이브. 생존자는 0.</summary>
            public int wave;
        }

        static readonly List<Entry> _fallen = new List<Entry>();
        static readonly List<Entry> _survivors = new List<Entry>();

        /// <summary>쓰러진 이들 — 죽은 순서대로.</summary>
        public static IReadOnlyList<Entry> Fallen => _fallen;

        /// <summary>끝까지 남은 이들 — <see cref="CaptureSurvivors"/> 를 부른 시점의 것.</summary>
        public static IReadOnlyList<Entry> Survivors => _survivors;

        /// <summary>클리어한 웨이브. 승리 판정이 넣는다.</summary>
        public static int ClearedWave { get; private set; }

        /// <summary>판이 흐른 시간(초). 승리 판정이 넣는다.</summary>
        public static float Seconds { get; private set; }

        /// <summary>기록이 하나라도 있는가 — 엔딩이 «명단을 그릴지» 를 이걸로 판단한다.</summary>
        public static bool HasAny => _fallen.Count > 0 || _survivors.Count > 0;

        // ------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();

        /// <summary>
        /// 기록을 비운다. <b>부르는 곳은 «판이 시작되는 자리» 뿐이다</b> — 위 doc 의 ⚠⚠ 참조.
        ///
        /// ⚠ <b>2026-08-27 — 자리가 둘이 됐다.</b> 예전 규약은 «<see cref="RunResetService"/>
        ///   하나» 였는데, <b>이어하기가 그 문을 지나지 않는다</b>는 것이 드러났다
        ///   (<c>GameSnapshot.Restore</c>). 로비로 나갔다 이어하기를 누르면 같은 프로세스라
        ///   static 이 살아 있어 <b>지난 판의 전사자가 이 판의 엔딩에 실렸다</b>.
        ///   그래서 <c>GameSnapshot.Restore</c> 가 <b>맨 앞에서</b> 한 번 더 부른다.
        ///   <b>그 둘 말고는 부르지 말 것</b> — 판 도중에 부르면 전사자가 통째로 사라진다.
        /// </summary>
        public static void Clear()
        {
            _fallen.Clear();
            _survivors.Clear();
            ClearedWave = 0;
            Seconds = 0f;
        }

        /// <summary>
        /// 캐릭터가 죽었다. <c>DamageableUnit.OnAnyDied</c> 를 듣는
        /// <see cref="GameSnapshot"/> 이 넘겨준다 — <b>새 구독을 만들지 않았다</b>.
        /// 그쪽이 이미 «캐릭터가 죽었을 때만» 을 가려내고 있어서, 여기서 또 구독하면
        /// 같은 판단이 두 벌이 된다.
        ///
        /// ⚠ <b>소환수(아루의 골렘)는 세지 않는다</b> — 인물이 아니다.
        ///   <see cref="CharacterUnit.IsSummoned"/> 의 긴 주석과 같은 판단이다.
        /// ⚠ <b>같은 인물이 두 번 들어갈 수 있다.</b> 죽었던 인물이 다시 뽑혀
        ///   («살아 있는 동안만 중복 금지») 또 죽으면 이름이 두 줄이 된다.
        ///   그것이 <b>사실이므로</b> 지우지 않는다 — 두 번 쓰러진 것이 맞다.
        /// </summary>
        public static void NoteDeath(CharacterUnit unit, int wave)
        {
            if (unit == null || unit.IsSummoned) return;
            _fallen.Add(Describe(unit, wave));
        }

        /// <summary>
        /// 지금 살아 있는 인물을 명단에 찍는다. 승리한 <b>그 순간</b>에 불러야 한다 —
        /// 엔딩 씬으로 넘어간 뒤에는 유닛이 이미 없다.
        /// </summary>
        public static void CaptureSurvivors(int clearedWave, float seconds)
        {
            ClearedWave = clearedWave;
            Seconds = seconds;

            _survivors.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is CharacterUnit c && c.IsAlive && !c.IsSummoned)
                    _survivors.Add(Describe(c, 0));
            }
        }

        static Entry Describe(CharacterUnit unit, int wave) => new Entry
        {
            name = unit.DisplayName,
            title = unit.Definition != null ? unit.Definition.Title : string.Empty,
            level = unit.UpgradeCount,
            wave = wave,
        };
    }
}
