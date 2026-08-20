namespace LastSanctuary.Combat
{
    /// <summary>
    /// 보스 스킬의 <b>분기용 식별자</b>. 웨이브 몬스터 테이블 <c>Skill.skill_type</c> 문자열과
    /// 1:1 이다 — <see cref="PassiveSkillType"/> 과 완전히 같은 구조·같은 이유다
    /// (문자열을 매 프레임 비교하지 않고, 오타가 컴파일에 안 걸리는 것을 막는다).
    ///
    /// 두 스킬의 <b>모양은 같다</b> — 보스 자기 칸에서 한 방향으로 뻗는 직사각형 범위에
    /// "근거리 공격력 × value03%" 를 넣는다. 다른 것은 <b>누구를 향해 뻗는지</b> 뿐이다:
    /// 타락한 무덤은 <b>가장 가까운</b> 적, 공허의 광선은 <b>가장 먼</b> 적.
    /// 그래서 <see cref="BossSkillCaster"/> 는 이 enum 을 조준 규칙에만 쓴다.
    /// </summary>
    public enum BossSkillType
    {
        /// <summary>알 수 없는 값 — 시전하지 않는다(표에 새 스킬이 추가된 경우).</summary>
        None = 0,

        /// <summary>
        /// 타락한 무덤 (130001) — <b>가장 가까운</b> 적을 향해 5 x 3 타일.
        /// 근접한 적을 쓸어내는 기술이라 붙어 있는 전열이 주 대상이다.
        /// </summary>
        FallenTomb,

        /// <summary>
        /// 공허의 광선 (130002) — <b>가장 먼</b> 적을 향해 15 x 3 타일.
        /// 뒤에서 안전하게 쏘던 원거리·치유 캐릭터를 노리는 기술이다.
        /// </summary>
        VoidLaser,

        // ──────────────────────────────────────────────────────────────
        // 카르시노스 (에픽 중립 보스 1004) — 2026-08-15
        //
        // ⚠ <b>값의 뜻이 단탈리온과 다르다.</b> 단탈리온의 두 스킬은 value_04 가 침식이지만,
        //   이 둘은 스트링 테이블의 정의문이 각자 다른 것을 말한다:
        //     할퀴기      value_04 = 방어력 <b>감소 %</b> · value_05 = 지속 <b>초</b>
        //     죽음의 포효 value_01 = <b>반지름</b> 타일 · value_02 = <b>넉백</b> 타일
        //   그래서 <see cref="BossSkillSO"/> 의 프로퍼티도 종류별로 갈라 읽는다 —
        //   칸 번호가 아니라 <b>뜻</b>으로 이름 붙인 프로퍼티를 쓸 것.
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 할퀴기 (2001) — <b>가장 가까운</b> 적을 향해 전방 5 x 3 타일.
        /// 맞은 적은 <b>방어력이 깎인다</b>(value_04 % · value_05 초).
        /// 단탈리온의 「타락한 무덤」과 조준·모양이 같고, 붙는 효과만 다르다.
        /// </summary>
        Scratch,

        /// <summary>
        /// 죽음의 포효 (2002) — 자기 중심 <b>원형</b>. 맞은 적을 <b>뒤로 밀어낸다</b>
        /// (value_02 타일). 붙어서 때리던 전열을 통째로 떼어내는 기술이다.
        /// </summary>
        RoarDeath,

        // ──────────────────────────────────────────────────────────────
        // 말파스 (웨이브 최종보스 120002) — 2026-08-18
        //
        // ⚠ 여기도 <b>값의 뜻이 다르다.</b> 스트링 테이블의 정의문이 근거다:
        //   구속탄   value_01 = 투사체 <b>반지름</b> · value_02 = 폭발 <b>반지름</b>
        //            value_04 = 공격속도 감소 % · value_05 = 「허약」 초 · value_06 = 「구속」 초
        //   저주광선 value_01 = 가로 · value_02 = 세로   (단탈리온의 광선과 같은 뜻)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 구속탄 (130003) — <b>가장 가까운</b> 적을 향해 던지고, 맞은 자리를 중심으로
        /// <c>value_02</c> 반지름 원형 피해. 맞은 적은 <b>「허약」</b>(공격속도 −value_04%,
        /// value_05초)이 되고, <b>허약 상태에서 또 맞으면</b> 허약이 풀리는 대신
        /// <b>「구속」</b>(value_06초 동안 이동·공격 불가)에 걸린다.
        ///
        /// ★ 원형이지만 <b>보스 중심이 아니다</b> — 맞은 적의 자리에서 터진다.
        ///   그래서 <see cref="BossSkillCaster"/> 가 원형 분기를 두 갈래로 나눈다.
        /// </summary>
        BindingOrb,

        /// <summary>
        /// 저주광선 (130004) — <b>가장 먼</b> 적을 향해 10 x 2 타일.
        /// 조준·모양이 단탈리온의 「공허의 광선」과 같고, <b>원거리</b> 공격력을 쓴다.
        /// </summary>
        CurseBeam,

        // ──────────────────────────────────────────────────────────────
        // 카시노마 (웨이브 보스 120003) — 2026-08-18
        //
        // ⚠ 여기는 <b>value_01·02 의 뜻이 또 다르다.</b> 스트링 테이블 정의문이 근거다:
        //   이끌리는 혈취 value_01 = 탐색 <b>지름</b> 타일 · value_02 = <b>피해 %</b>
        //                 (⚠ 다른 스킬은 value_03 이 피해다 — 여기만 한 칸 앞이다)
        //   죽음의 노래   value_01 = 가로 · value_02 = 세로 · value_03 = 피해 %
        //                 · value_04 = <b>타격 횟수</b>
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 이끌리는 혈취 (130005) — <b>가장 가까운</b> 적에게 <b>직접 이동</b>해 붙은 뒤
        /// 한 번 때린다. 원화 시트: *"20x20 타일 범위 내 타겟 1명에게 돌진 후 1회 피해
        /// (근거리 공격의 3배)"*.
        ///
        /// ★ 이 프로젝트에서 <b>시전자를 움직이는 첫 스킬</b>이다 — 다른 보스 스킬은 전부
        ///   제자리에서 범위만 낸다. 그래서 <see cref="BossSkillCaster"/> 에 이동 분기가 있다.
        /// </summary>
        LureBlood,

        /// <summary>
        /// 죽음의 노래 (130006) — <b>가장 가까운</b> 적을 향해 4 x 4 타일 상자를
        /// <c>value_04</c> 번 <b>연달아</b> 때린다. 한 번에 다 넣지 않고 시전 시간에 걸쳐
        /// 나눠 넣는다 — 원화가 6타 연격이고, 한 프레임에 몰면 회복·방어가 끼어들 틈이 없다.
        /// </summary>
        DeathSong,

        // ──────────────────────────────────────────────────────────────
        // 아니사킬 (에픽 중립 보스 1005) — 2026-08-19
        //
        // ⚠ 여기도 <b>칸의 뜻이 다르다.</b> 스트링 테이블 정의문이 근거다:
        //   치명적 꼬리 타격 value_01 = 가로 · value_02 = 세로 · value_03 = 피해 %
        //                    (단탈리온 「타락한 무덤」·카르시노스 「할퀴기」와 같은 배치)
        //   거대한 위협 포효 value_01 = <b>반지름</b> 타일 · value_02 = <b>피해 %</b>
        //                    · value_03 = <b>구속 초</b>
        //                    (⚠ 피해가 value_02 다 — 「이끌리는 혈취」와 같이 한 칸 앞이다)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 치명적 꼬리 타격 (2003) — <b>가장 가까운</b> 적을 향해 3(가로) x 5(세로) 타일에
        /// 근거리 공격력의 <c>value_03</c>%.
        ///
        /// 조준·모양·값 배치가 「타락한 무덤」·「할퀴기」와 <b>완전히 같다</b> — 붙는 효과가
        /// 없다는 점만 다르다. 그래서 <see cref="BossSkillCaster"/> 에 전용 분기가 없고
        /// 기본 직사각형 갈래를 그대로 탄다.
        /// </summary>
        TailStrike,

        /// <summary>
        /// 거대한 위협 포효 (2004) — 자기 중심 <b>원형</b>(반지름 <c>value_01</c>)에
        /// <c>value_02</c>% 피해. 맞은 적은 <c>value_03</c>초 동안 <b>「구속」</b>
        /// (이동·공격 불가 = 기절).
        ///
        /// ★ <b>「구속」은 말파스 구속탄과 같은 상태다</b> — <c>UnitCombat.ApplyBind</c> 를
        ///   그대로 쓴다. 다른 것은 <b>거는 조건</b>뿐이다: 구속탄은 「허약 상태에서 또 맞으면」
        ///   이지만 이쪽은 <b>맞으면 바로</b> 걸린다.
        ///
        /// ★ 정의문에 <i>"구속 상태는 부정적인 정신 이상 상태를 해제하는 효과로 해제 가능하다"</i>
        ///   가 붙어 있다 — 그래서 피올로의 「정신 안정」이 구속도 푼다
        ///   (<c>CharacterPassives.TryCalmDown</c>). 그 규칙은 <b>구속탄의 구속에도 같이</b>
        ///   적용된다: 같은 상태에 해제 규칙이 두 벌 있으면 안 된다.
        /// </summary>
        HugeThreat,

        // ──────────────────────────────────────────────────────────────
        // 라린길 (웨이브 최종보스 120004) — 2026-08-19
        //
        // ⚠ <b>여기도 칸의 뜻이 다르다.</b> 스트링 테이블 정의문이 근거다:
        //   아우성        value_01 = <b>반지름</b> 타일 · value_02 = <b>피해 %</b>
        //                 (⚠ 피해가 value_02 다 — 「이끌리는 혈취」·「거대한 위협 포효」와 같은 자리)
        //   타오르는 숨결 value_01 = 가로 · value_02 = 세로 · value_03 = 피해 %
        //                 · value_04 = <b>대상 최대 체력의 %</b> 만큼 추가 피해
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 아우성 (130007) — 자기 중심 <b>원형</b>(반지름 <c>value_01</c>). 맞은 적에게
        /// <c>value_02</c>% 피해를 주고 <b>침식</b>을 올린다.
        ///
        /// ★ <b>지금 표의 <c>value_02</c> 는 0 이다</b> — 피해가 없고 <b>침식만</b> 올리는
        /// 기술이라는 뜻이다. 침식량(<c>mentalerror_damage</c> 15)은 이 게임의 모든 보스
        /// 스킬 중 가장 크다. 정의문도 *"…{value_02}의 데미지를 입히고 침식수치를
        /// 증가시킨다"* 라 피해 칸이 비어 있는 것이 <b>표가 말하는 것</b>이다.
        ///
        /// ⚠ 그래서 <see cref="BossSkillSO.DamagePercent"/> 의 "비면 100%" 폴백을 이
        /// 종류에만 <b>끈다</b> — 폴백이 돌면 침식용 기술이 평타 한 대를 그대로 얹는
        /// <b>전혀 다른 기술</b>이 된다. 표에 값이 들어오면 그때부터 그 값이 그대로 쓰인다.
        /// </summary>
        Screaming,

        /// <summary>
        /// 타오르는 숨결 (130008) — <b>가장 가까운</b> 적을 향해 <c>value_01</c> x
        /// <c>value_02</c> 타일 상자에 근거리 공격력의 <c>value_03</c>%.
        ///
        /// ★ <b>추가로 대상 최대 체력의 <c>value_04</c>%</b> 를 더 깎는다 — 이 프로젝트에서
        /// <b>방어력을 거치지 않는 피해</b>가 나오는 첫 기술이다. 정의문:
        /// *"…추가로 적 전체 체력의 {value_04}%의 데미지를 준다."*
        /// 방어력이 아무리 높아도 반드시 아픈, 최종보스다운 확정 피해다.
        /// </summary>
        BurningBreath,

        // ──────────────────────────────────────────────────────────────
        // 베일 (웨이브 최종보스 120005) — 2026-08-20
        //
        // ⚠ <b>여기도 칸의 뜻이 다르다.</b> 스트링 테이블의 정의문이 근거다:
        //   담뱃대 강타 value_01 = <b>반지름</b> 타일 · value_02 = <b>대상 수(명)</b>
        //               · value_03 = 피해 % · value_04 = <b>밀쳐내는 타일</b>
        //   담배 연기   value_01 = <b>부채꼴 반지름</b> 타일 · value_02 = <b>연기 지속 초</b>
        //               · value_03 = 피해(=0) · value_04 = <b>중독 초</b>
        //               · value_05 = <b>초당 최대체력 %</b>
        //
        // ★★ <b>value_02 가 「세로」가 아니라 「명」이다</b> — 그래서 이 둘은 기본
        //    직사각형 갈래에 태울 수 없다. 태우면 3x1 상자가 되어 «반지름 3 원형에
        //    1명» 이라는 표의 뜻과 전혀 다른 기술이 된다.
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 담뱃대 강타 (130009) — 자기 중심 <b>반지름 <c>value_01</c></b> 원형 안에서
        /// <b>가장 가까운 <c>value_02</c>명</b>에게 <c>value_03</c>% 피해를 주고
        /// <c>value_04</c> 타일 <b>밀쳐낸다</b>.
        ///
        /// ★ 밀리는 방향이 <b>「보스 반대쪽」이 아니다</b> — 정의문이
        ///   *"캐릭터는 자신이 바라보는 반대 방향으로 밀려납니다"* 라고 못박고 있다.
        ///   그래서 「죽음의 포효」(<see cref="RoarDeath"/>)의 넉백과 <b>기준이 다르다</b>:
        ///   그쪽은 시전자에서 멀어지는 쪽, 이쪽은 <b>맞는 쪽이 보고 있는 반대쪽</b>이다.
        ///   교전 중이면 대개 보스를 보고 있어 결과가 비슷하지만, 도망치던 중에 맞으면
        ///   <b>보스 쪽으로</b> 끌려온다 — 그게 표가 말하는 것이다.
        ///
        /// ⚠ 표의 <c>range_type</c> 은 <c>Line</c> 인데 정의문은 <b>원형</b>이다.
        ///   정의문을 따랐고(칸의 뜻이 그쪽에만 적혀 있다), 그래서 이 종류는
        ///   <see cref="BossSkillShape"/> 를 <b>보지 않는다</b> — 구속탄·이끌리는 혈취와
        ///   같이 <see cref="BossSkillCaster"/> 의 전용 갈래를 탄다.
        /// </summary>
        PipeStrike,

        /// <summary>
        /// 담배 연기 (130010) — <b>정면 부채꼴</b>(반지름 <c>value_01</c>) 안에 캐릭터가
        /// 있으면 <c>value_02</c>초간 연기를 깔고, 닿은 적을 <c>value_04</c>초 동안
        /// <b>「중독」</b>(매초 최대 체력의 <c>value_05</c>%)으로 만든다.
        ///
        /// ★ <b>직접 피해가 0 이다</b>(표의 <c>value_03</c> = 0). 「아우성」과 같은 이유로
        ///   <see cref="BossSkillSO.DamagePercent"/> 의 «비면 100%» 폴백을 <b>끈다</b> —
        ///   폴백이 돌면 중독을 거는 기술이 평타 한 대를 얹는 다른 기술이 된다.
        ///
        /// ★ 이 프로젝트에서 <b>부채꼴 범위를 쓰는 첫 스킬</b>이다
        ///   (<see cref="BossSkillShape.SemiCircle"/> 신설).
        /// </summary>
        PipeSmoke,

        // ──────────────────────────────────────────────────────────────
        // 바리올라 (에픽 중립 보스 1103) — 2026-08-20
        //
        // 스트링 테이블 정의문이 근거다. ★ <b>둘 다 「지름」</b>이라
        // <see cref="BossSkillSO.CircleValueIsRadius"/> 에 넣지 않는다 —
        // 카르시노스·아니사킬·라린길과 반대쪽(단탈리온 계열과 같은 쪽)이다:
        //   소름 끼치는 흉터 "바리올라 + {value_01} <b>지름</b> 타일 범위에 가장 가까운
        //                     적 {value_02}명에게 <b>침식 수치 {value_03}</b>을 증가"
        //   치명적인 독기   "바리올라 + {value_01} <b>지름</b> 타일 범위에 있는 모든 적의
        //                     현재 체력이 <b>최대체력의 {value_02}%</b> 감소"
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 소름 끼치는 흉터 (2005) — 자기 중심 <b>지름 <c>value_01</c></b> 원형 안에서
        /// <b>가장 가까운 <c>value_02</c>명</b>의 <b>침식만</b> <c>value_03</c> 올린다.
        ///
        /// ★ <b>피해가 없다</b> — 정의문에 피해라는 말이 아예 없다. 「아우성」과 같은
        ///   «침식 전용» 기술이라 <see cref="BossSkillSO.DamagePercent"/> 폴백을 끈다.
        ///
        /// ★★ <b>침식량이 전용 칸(<c>mentalerror_damage</c>)에 없다</b> — 그 칸은 0 이고
        ///   정의문이 <c>value_03</c> 을 가리킨다. 그래서 이 종류만
        ///   <see cref="BossSkillSO.ErosionValue"/> 가 <c>value_03</c> 을 읽는다.
        ///   («모든 스킬이 같은 칸을 쓴다»는 2026-08-18 의 정리에 대한 <b>유일한 예외</b>이고,
        ///   근거는 표 자신이다 — 전용 칸이 비어 있다.)
        /// </summary>
        CreepyScar,

        /// <summary>
        /// 치명적인 독기 (2006) — 자기 중심 <b>지름 <c>value_01</c></b> 원형 안의
        /// <b>모든</b> 적에게 <b>최대 체력의 <c>value_02</c>%</b> 피해.
        ///
        /// ★ <b>방어력을 거치지 않는다</b> — 「타오르는 숨결」의 추가 피해와 같은 성질이라
        ///   <see cref="BossSkillSO.MaxHpPercentDamage"/> 를 그대로 쓴다. 다만 숨결은
        ///   <b>평타 피해에 얹는</b> 것이고 이쪽은 <b>그것만</b>이다(<c>value_03</c> = 0).
        /// </summary>
        DeadlyVenom,
    }

    public static class BossSkillTypes
    {
        /// <summary>
        /// 테이블 문자열 → enum. 못 알아보면 <see cref="BossSkillType.None"/>.
        /// 공백·대소문자에 관대하다 — 표는 사람이 손으로 적는 칸이고, 실제로 이 프로젝트의
        /// 표에 앞 공백이 섞여 들어온 적이 있다(<see cref="PassiveSkillTypes.Parse"/> 주석).
        /// </summary>
        public static BossSkillType Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return BossSkillType.None;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "fallen_tomb": return BossSkillType.FallenTomb;
                case "void_laser":  return BossSkillType.VoidLaser;
                case "scratch":     return BossSkillType.Scratch;
                case "roar_death":  return BossSkillType.RoarDeath;
                case "binding_orb": return BossSkillType.BindingOrb;
                case "curse_beam":  return BossSkillType.CurseBeam;
                case "lure_blood":  return BossSkillType.LureBlood;
                case "death_song":  return BossSkillType.DeathSong;
                case "tail_strike": return BossSkillType.TailStrike;
                case "huge_threat": return BossSkillType.HugeThreat;
                case "screaming":   return BossSkillType.Screaming;
                case "burning_breath": return BossSkillType.BurningBreath;

                // ── 베일 120005 (2026-08-20) ──
                case "pipe_strike":    return BossSkillType.PipeStrike;
                case "pipe_smoke":     return BossSkillType.PipeSmoke;

                // ── 바리올라 1103 (2026-08-20) ──
                case "creepy_scar":    return BossSkillType.CreepyScar;
                case "deadly_venom":   return BossSkillType.DeadlyVenom;
                default:            return BossSkillType.None;
            }
        }
    }

    /// <summary>
    /// 스킬 범위의 <b>모양</b>. 표 <c>Skill.range_type</c> 칸이 정본이다(2026-08-13 신설).
    ///
    /// <b>왜 칸을 새로 뒀나</b> — 유저 지시가 "360도 범위 값으로 적용 가능하게 ... 원형으로"
    /// 였는데, 단탈리온의 두 스킬은 원화가 <b>가로로 긴 파동·광선</b>이라 그대로 원형으로 만들면
    /// 그림과 판정이 어긋난다. 그래서 <b>모양을 표에서 고르게</b> 하고, 기본값인
    /// <see cref="Line"/> 은 조준을 4방향에서 <b>자유각(360도)</b>으로 풀어 "대각선 적을 못
    /// 때린다"는 문제만 정확히 해소한다. 진짜 원형이 필요한 스킬은 표에서 <c>Circle</c> 로
    /// 바꾸면 코드 수정 없이 원형으로 돈다.
    /// </summary>
    public enum BossSkillShape
    {
        /// <summary>
        /// 조준 방향으로 뻗는 <b>직사각형</b>(기본). 각도 제한이 없다 —
        /// <see cref="UnitRegistry.CollectEnemiesInOrientedRect"/> 로 상자를 통째로 돌린다.
        /// </summary>
        Line = 0,

        /// <summary>
        /// 보스를 중심으로 한 <b>원형</b>. 반지름 = <c>value_01</c>(지름, 타일) ÷ 2.
        /// 방향이라는 개념이 없으므로 어느 각도의 적이든 거리만 맞으면 전부 맞는다.
        /// </summary>
        Circle = 1,

        /// <summary>
        /// 조준 방향의 <b>부채꼴(반원)</b>. 반지름 = <c>value_01</c>, 각도는 <b>180도</b> —
        /// 「정면」이라는 말이 뜻하는 만큼만 열려 있고 등 뒤는 안 맞는다
        /// (2026-08-20 신설 · 베일 「담배 연기」).
        ///
        /// ★★ <b>왜 새로 필요했나</b> — 표는 이 값을 <c>Semi_Circle</c> 로 적어놨는데
        ///   enum 에 없어서 <see cref="Parse"/> 가 조용히 <see cref="Line"/> 로 떨어뜨리고
        ///   있었다. 그러면 «정면 반지름 5 부채꼴» 이 «5x1 상자» 가 된다 — 표와 화면이
        ///   어긋나는데 <b>경고 한 줄도 안 남는다</b>(폴백이 정상 동작이라서).
        ///
        /// ★ <b>왜 180도인가</b> — 표의 값이 <c>Semi_Circle</c>(반원)이고, 부채꼴의 각도를
        ///   적는 칸이 표에 <b>없다</b>. 각도를 코드에 지어내는 대신 이름이 말하는 값을
        ///   그대로 쓴다. 각도를 정하고 싶어지면 그때 표에 칸을 만드는 것이 맞다
        ///   (범위 값을 하드코딩 상수에서 표 컬럼으로 옮겨 온 이 프로젝트의 규칙 · 118-3절).
        /// </summary>
        SemiCircle = 2,
    }

    public static class BossSkillShapes
    {
        /// <summary>표 문자열 → enum. 비었거나 못 알아보면 <see cref="BossSkillShape.Line"/>.</summary>
        public static BossSkillShape Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return BossSkillShape.Line;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "circle":
                case "round":
                case "radial": return BossSkillShape.Circle;

                // ★ 2026-08-20 — 표의 `Semi_Circle` 이 여기 없어서 Line 으로 떨어지고
                //   있었다(위 SemiCircle 주석). 사람이 적는 칸이라 표기 흔들림을 다 받는다.
                case "semi_circle":
                case "semicircle":
                case "semi circle":
                case "half_circle":
                case "fan":
                case "cone":
                case "sector":  return BossSkillShape.SemiCircle;

                default:       return BossSkillShape.Line;
            }
        }
    }
}
