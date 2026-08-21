using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Buildings;
using LastSanctuary.Combat;
using LastSanctuary.Fog;
using LastSanctuary.Resource;
using LastSanctuary.UI;
using LastSanctuary.Units;
using LastSanctuary.Wave;

namespace LastSanctuary.Save
{
    /// <summary>
    /// 게임 상태를 <see cref="SaveData"/> 로 <b>담고(Capture)</b> 되돌린다<b>(Restore)</b>.
    /// 파일·슬롯은 <see cref="SaveService"/> 가 맡고, 이쪽은 씬 안의 값만 다룬다.
    ///
    /// <b>복원 방식 — "기본 생성이 끝난 뒤 덮어쓴다"</b>
    /// 씬이 열리면 평소대로 맵이 생성되고 캐릭터 3명이 나오고 웨이브가 시작된다. 이 컴포넌트는
    /// <b>그 뒤에</b> 판을 비우고 저장된 것으로 채운다.
    ///
    /// 반대 방식(스포너들이 "복원 중이니 만들지 마라"를 각자 확인하게 하는 것)도 가능하지만,
    /// 그러려면 <c>UnitSpawner</c>·<c>MonsterSpawner</c>·<c>NeutralMonsterSpawner</c>·
    /// <c>WaveManager</c> 네 곳의 시작 경로에 전부 갈래를 내야 하고, <b>하나라도 빠뜨리면
    /// 복원한 판에 유령 유닛이 섞인다</b>. 덮어쓰기는 그 위험이 없다 — 조금 낭비지만
    /// (한 프레임어치 생성) 틀릴 구석이 훨씬 적다.
    ///
    /// ⚠ <b>한 프레임 기다린 뒤 덮어쓴다</b>(<see cref="RestoreNextFrame"/>). 다른 컴포넌트의
    /// <c>Start</c> 가 아직 안 돌았을 수 있는데, 그 상태에서 지우면 <b>없어진 것을 다시 만드는</b>
    /// 스포너가 생긴다. <c>yield return null</c> 한 번이면 모든 <c>Start</c> 가 끝난 뒤가 보장된다.
    /// </summary>
    public class GameSnapshot : MonoBehaviour
    {
        /// <summary>다른 곳(환경 설정 창·자동 저장)이 부를 수 있게 하나만 둔다.</summary>
        public static GameSnapshot Instance { get; private set; }

        [Header("자동 저장")]
        [Tooltip("되돌리면 이득을 보는 순간마다 저장한다 — 캐릭터 강화 · 캐릭터 사망 · 웨이브 클리어.\n" +
                 "끄면 환경 설정 창의 '저장하기' 로만 저장된다")]
        [SerializeField] bool autoSave = true;

        [Tooltip("자동 저장이 일어났을 때 HUD 로그에 한 줄 남긴다")]
        [SerializeField] bool logAutoSave = true;

        [Tooltip("같은 프레임에 여러 사건이 겹쳐도 이 시간 안에는 한 번만 저장한다(초). " +
                 "웨이브 클리어 순간에는 마지막 몬스터 사망과 클리어가 같이 일어난다")]
        [Min(0f)] [SerializeField] float autoSaveCooldown = 0.5f;

        WaveManager _wave;
        UnitSpawner _unitSpawner;
        MonsterSpawner _monsterSpawner;
        NeutralMonsterSpawner _neutralSpawner;
        FogOfWarService _fog;

        float _nextAutoSaveAllowed;
        bool _restoring;

        void Awake()
        {
            Instance = this;
            SaveService.ApplyVolume();      // 빌드를 새로 켰을 때 저장된 음량을 실제로 반영한다
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // ★ 구독 해제를 <b>전부 여기</b>에 모은다.
            //
            //   ⚠ 처음에는 웨이브·강화 구독을 <c>OnDisable</c> 에서 풀었는데, 그러면
            //   <b>껐다 켜면 그 둘이 영영 안 돌아온다</b> — 다시 거는 곳(<see cref="Start"/>)이
            //   한 번만 도는 <c>Start</c> 라서다. 사망 구독만 <c>OnEnable/OnDisable</c> 짝이었어서
            //   <b>셋 중 하나만 살아 돌아오는</b> 어긋난 상태가 된다.
            //   생애가 오브젝트와 같은 구독은 <c>OnDestroy</c> 에서 푸는 것이 맞다.
            DamageableUnit.OnAnyDied -= HandleAnyDied;
            if (_wave != null) _wave.OnWaveEnded -= HandleWaveEnded;

            CharacterUpgradeService upgrade = CharacterUpgradeService.Instance;
            if (upgrade != null) upgrade.OnUpgraded -= HandleUpgraded;
        }

        void Start()
        {
            _wave = FindAnyObjectByType<WaveManager>();
            _unitSpawner = FindAnyObjectByType<UnitSpawner>();
            _monsterSpawner = FindAnyObjectByType<MonsterSpawner>();
            _neutralSpawner = FindAnyObjectByType<NeutralMonsterSpawner>();
            _fog = FindAnyObjectByType<FogOfWarService>();

            HookAutoSave();

            if (SaveService.PendingLoad != null) StartCoroutine(RestoreNextFrame());
        }

        /// <summary>
        /// 자동 저장 사건 셋을 구독한다. <b>정적 이벤트(<c>OnAnyDied</c>)까지 여기서 건다</b> —
        /// <c>OnEnable</c> 에 두면 <see cref="Start"/> 에서 거는 나머지 둘과 생애가 어긋난다
        /// (<see cref="OnDestroy"/> 주석 참조).
        /// </summary>
        void HookAutoSave()
        {
            DamageableUnit.OnAnyDied += HandleAnyDied;

            if (_wave != null) _wave.OnWaveEnded += HandleWaveEnded;

            CharacterUpgradeService upgrade = CharacterUpgradeService.Instance;
            if (upgrade != null) upgrade.OnUpgraded += HandleUpgraded;
        }

        // ==================================================================
        // 자동 저장 — "되돌리면 이득을 보는 순간"
        //
        // 유저 확정 2026-08-18: <i>"게임에 되돌릴 경우 베네핏을 볼 수 있을 상황이 발생하는
        // 경우엔 자동 저장 되어야 해"</i>. 그래서 <b>나쁜 일이 일어난 직후</b>(사망)에도 저장한다 —
        // 그 저장이 없으면 유저가 게임을 껐다 켜서 죽음을 무를 수 있다.
        // ==================================================================

        void HandleWaveEnded(int wave) => AutoSave($"웨이브 {wave} 클리어");

        void HandleUpgraded(CharacterUnit unit, int cost) =>
            AutoSave($"{(unit != null ? unit.DisplayName : "캐릭터")} 강화");

        /// <summary>
        /// 캐릭터가 죽었을 때만 저장한다. 몬스터 사망은 초당 수십 번 일어나 저장이 의미가 없다.
        ///
        /// ⚠ <b>부활 대기는 사망이 아니다</b> — 「분노」(히스톤)가 되살릴 캐릭터까지 저장하면
        /// 불러왔을 때 <b>부활 코루틴이 없는 시체</b>가 남는다. 그건 되돌리기 이득이 아니라 손해다.
        /// </summary>
        void HandleAnyDied(DamageableUnit unit)
        {
            if (unit is not CharacterUnit character) return;
            if (character.IsRevivePending) return;

            AutoSave($"{character.DisplayName} 사망");
        }

        /// <summary>
        /// 자동 저장 진입점. <b>이벤트가 늘어나면 여기로만 붙인다</b> — 유저가 예고한
        /// "이후에 추가할 이벤트"도 이 한 줄을 부르면 된다.
        /// </summary>
        public void AutoSave(string reason)
        {
            if (!autoSave || _restoring) return;

            // 결과가 확정된 판은 저장하지 않는다 — 패배 화면에서 저장하면 그 세이브는
            // 불러오자마자 다시 패배한다(승리도 같다).
            if (_wave != null && _wave.IsFinished) return;

            if (Time.unscaledTime < _nextAutoSaveAllowed) return;
            _nextAutoSaveAllowed = Time.unscaledTime + autoSaveCooldown;

            if (!SaveNow(reason)) return;
            if (logAutoSave) HudLog.Add($"자동 저장 — {reason}", HudLogKind.Info);
        }

        /// <summary>
        /// 지금 상태를 파일에 쓴다. 환경 설정 창의 "저장하기" 도 이 경로를 쓴다.
        ///
        /// ══════════════════════════════════════════════════════════════════
        ///  ★★ <b>캐릭터가 0명인 판은 저장하지 않는다</b> (2026-08-21)
        /// ══════════════════════════════════════════════════════════════════
        /// 유저 리포트: *"가끔 게임 시작 시 캐릭터가 3마리가 생성이 안되고 캐릭터가 0개로
        /// 시작하는 버그"*.
        ///
        /// <b>어떻게 그런 세이브가 만들어졌나</b> — <see cref="HandleAnyDied"/> 는 죽음
        /// <b>그 순간</b>에 불린다(<c>OnAnyDied</c> 는 피해 처리 안에서 바로 발생한다).
        /// 그런데 «전멸 패배» 는 <c>WaveManager.Update</c> 가 <b>다음 프레임에</b> 폴링해서
        /// 정하고, 그것도 «에너지로 다시 뽑을 수 없을 때» 만 패배다. 그래서 마지막 캐릭터가
        /// 죽는 프레임에는 <c>IsFinished</c> 가 <b>아직 false</b> 이고 위 ⚠ 의 가드를 통과한다
        /// → <see cref="CaptureCharacters"/> 가 죽은 유닛을 전부 건너뛰어 <b>인원 0명짜리
        /// 세이브</b>가 디스크에 쓰인다.
        ///
        /// 그 파일을 「이어하기」로 열면 <see cref="RestoreCharacters"/> 가 시작 캐릭터 3명을
        /// <b>먼저 지우고</b> 저장된 0명을 세우려 하므로 — <b>0명으로 시작</b>한다.
        ///
        /// ★ <b>여기가 맞는 자리다</b> — <see cref="AutoSave"/> 와 환경 설정의 «저장하기»
        ///   (<c>SettingsPanel.TrySave</c>)가 <b>둘 다</b> 이 함수를 지난다. 한 곳에서 막으면
        ///   경로가 늘어도 규칙이 갈리지 않는다.
        /// ★ 위의 <c>IsFinished</c> 가드는 <b>그대로 둔다</b> — 그것은 «결과가 확정된 판은
        ///   저장하지 않는다» 는 <b>정책</b>이고, 이것은 «되돌릴 수 없는 세이브는 만들지
        ///   않는다» 는 <b>유효성</b>이다. 뜻이 달라 합치지 않는다.
        /// ⚠ 「저장하고 로비로 돌아가기」는 저장 실패 시 <b>씬을 넘기지 않는다</b> —
        ///   그래서 이 가드가 걸리면 유저가 그 자리에서 «저장하지 못했습니다» 를 본다.
        ///   그편이 «조용히 못 쓰는 세이브를 만들고 나가는» 것보다 낫다.
        /// </summary>
        public bool SaveNow(string reason)
        {
            SaveData data = Capture(reason);

            if (data.characters.Count == 0)
            {
                Debug.LogWarning($"[저장] 캐릭터가 0명이라 «{reason}» 저장을 건너뜁니다 — " +
                                 "그 세이브를 불러오면 시작 캐릭터가 지워지고 0명으로 시작합니다.", this);
                HudLog.Add("캐릭터가 없어 저장하지 않았습니다", HudLogKind.Warn);
                return false;
            }

            return SaveService.Write(data);
        }

        // ==================================================================
        // 담기
        // ==================================================================

        public SaveData Capture(string reason)
        {
            var data = new SaveData { reason = reason ?? string.Empty };

            if (_wave != null)
            {
                data.waveNumber = _wave.WaveNumber;
                data.wavePhase = (int)_wave.Phase;
                data.phaseRemaining = _wave.PhaseRemaining;
            }

            ResourceManager resources = ResourceManager.Instance;
            if (resources != null) data.energy = resources.Energy;

            if (_unitSpawner != null && _unitSpawner.SpawnedNexus != null)
                data.nexusHp = _unitSpawner.SpawnedNexus.CurrentHp;

            CaptureCharacters(data);
            CaptureSquads(data);
            CaptureTowers(data);
            CaptureMonsters(data);
            CaptureNeutrals(data);
            CaptureSubjugation(data);
            CaptureFog(data);
            CaptureMap(data);

            return data;
        }

        /// <summary>
        /// ★ 이 판의 <b>지형 씨앗</b>을 적는다 (2026-08-19, 맵 랜덤 생성).
        ///
        /// 이 한 칸이 없으면 이어하기가 <b>다른 지형</b>을 만들고 그 위에 저장된 좌표로
        /// 유닛을 되살린다 — 캐릭터가 벽에 박히고 서식지가 엉뚱한 곳에 그려진다.
        /// 그래서 <see cref="SaveData.CurrentVersion"/> 도 같이 올렸다(옛 파일은 거부된다).
        ///
        /// ⚠ 0 이면 <b>런타임 생성을 안 하는 설정</b>(씬에 구운 맵을 그대로 쓰는 예전 방식)이다 —
        ///   그때는 이어하기도 그 구운 맵을 쓰므로 0 을 그대로 적는 것이 맞다.
        /// </summary>
        void CaptureMap(SaveData data)
        {
            var map = FindAnyObjectByType<Map.MapGenerator>();
            if (map != null) data.mapSeed = map.ActiveSeed;
        }

        void CaptureCharacters(SaveData data)
        {
            var all = FindObjectsByType<CharacterUnit>(FindObjectsSortMode.None);
            SquadService squads = SquadService.Instance;

            foreach (CharacterUnit unit in all)
            {
                if (unit == null) continue;

                // ★ <b>소환수(아루의 골렘)는 담지 않는다</b> (2026-08-21).
                //   골렘도 <c>CharacterUnit</c> 이라 그대로 담기고 있었는데, 불러오면
                //   <b>주인 없는 로스터 인원</b>으로 되살아난다(「강림」이 다시 부를 몸이 아니다).
                //   ⚠ 이 프로젝트의 다른 인원 집계는 이미 전부 <c>!IsSummoned</c> 를 쓴다
                //     (<c>WaveManager</c>·<c>DefeatPanel</c>·<c>VictoryPanel</c>·
                //      <c>CharacterCreationService</c>) — 저장만 빠져 있었다.
                if (unit.IsSummoned) continue;

                // 쓰러진 채 부활을 기다리는 캐릭터는 <b>살아있는 것으로</b> 담는다 —
                // 없어진 게 아니라 잠깐 누워 있는 것이다(CharacterUnit.IsRevivePending 주석).
                if (!unit.IsAlive && !unit.IsRevivePending) continue;

                var save = new CharacterSave
                {
                    characterId = unit.Definition != null ? unit.Definition.characterId : 0,
                    stats = unit.Stats,
                    upgradeCount = unit.UpgradeCount,
                    growthFocus = (int)unit.GrowthFocus,
                    currentHp = Mathf.Max(1, unit.CurrentHp),
                    position = unit.transform.position,
                    squadId = squads != null ? squads.SquadIdOf(unit) : 0,
                };

                CharacterTactics tactics = unit.GetComponent<CharacterTactics>();
                if (tactics != null) save.order = tactics.Order;

                CharacterKills kills = CharacterKills.Of(unit);
                if (kills != null)
                {
                    save.kills = kills.Kills;
                    save.awakenings = kills.Awakenings;
                    save.heals = kills.Heals;      // ★ 힐러의 각성 진행도(2026-08-21)

                    // 영웅 각성 보정은 <b>능력치에 안 들어 있다</b>(AddFlatStatBonus 는 상한 밖
                    // 별도 칸이다) — 따로 담지 않으면 불러왔을 때 각성이 통째로 사라진다.
                    // ⚠ StatType.COUNT 는 개수를 세는 표식이지 능력치가 아니다 — 빼고 돈다.
                    for (int i = 0; i < (int)StatType.COUNT; i++)
                    {
                        int bonus = kills.AwakenBonus((StatType)i);
                        if (bonus == 0) continue;
                        save.awakenBonusStats.Add(i);
                        save.awakenBonusAmounts.Add(bonus);
                    }
                }

                CharacterErosion erosion = CharacterErosion.Of(unit);
                if (erosion != null)
                {
                    save.erosion = erosion.Erosion;
                    save.mentalErrorType = (int)erosion.ActiveType;
                }

                data.characters.Add(save);
            }
        }

        void CaptureSquads(SaveData data)
        {
            SquadService service = SquadService.Instance;
            if (service == null) return;

            RallyPointService rally = RallyPointService.Instance;

            foreach (SquadService.Squad squad in service.Squads)
            {
                if (squad == null) continue;

                var save = new SquadSave
                {
                    id = squad.Id,
                    name = squad.Name,
                    coopExpedition = service.IsCoopExpedition(squad.Id),
                };

                RallyPointService.RallyPoint point = rally != null ? rally.FindBySquad(squad.Id) : null;
                if (point != null)
                {
                    save.hasRallyPoint = true;
                    save.rallyPoint = point.World;
                }

                data.squads.Add(save);
            }
        }

        void CaptureTowers(SaveData data)
        {
            BuildService build = BuildService.Instance;
            if (build == null) return;

            foreach (TowerUnit tower in build.AliveTowers())
            {
                if (tower == null || !tower.IsAlive) continue;
                data.towers.Add(new TowerSave
                {
                    minCell = tower.FootprintMinCell,
                    currentHp = tower.CurrentHp,
                });
            }
        }

        void CaptureMonsters(SaveData data)
        {
            if (_monsterSpawner == null) return;

            foreach (MonsterUnit monster in _monsterSpawner.Alive)
            {
                if (monster == null || !monster.IsAlive) continue;
                if (monster.Definition == null) continue;

                data.monsters.Add(new MonsterSave
                {
                    definitionName = monster.Definition.name,
                    position = monster.transform.position,
                    currentHp = monster.CurrentHp,
                    stats = monster.Stats,
                });
            }
        }

        /// <summary>
        /// 중립 몬스터 (유저 지시 2026-08-18 — "소환된 숫자와 서식지 위치는 유지").
        /// 개체를 하나씩 담으므로 마리 수는 저절로 맞고, 서식지는 <b>중심 칸과 씨앗</b>만 담는다.
        /// </summary>
        void CaptureNeutrals(SaveData data)
        {
            if (_neutralSpawner == null) return;

            foreach (NeutralMonsterUnit unit in _neutralSpawner.AliveAll())
            {
                if (unit == null || unit.Definition == null) continue;

                var save = new NeutralSave
                {
                    monId = unit.Definition.monId,
                    position = unit.transform.position,
                    currentHp = unit.CurrentHp,
                    homePosition = unit.transform.position,
                };

                // 에픽(서식지 모드)만 개체마다 "집"이 있다 — 지금 서 있는 자리는 쫓아 나간
                // 도중일 수 있으므로 서식지 중심이 정본이다. 그 외 중립은 넥서스 고리 안을
                // 도는 것이라 개체마다 기억할 집이 없다(태어난 자리를 그대로 쓴다).
                var wander = unit.GetComponent<NeutralMonsterWander>();
                if (wander != null && wander.IsHabitatMode) save.homePosition = wander.HabitatCenter;

                var habitat = unit.GetComponent<NeutralHabitat>();
                if (habitat != null && habitat.HasPainted)
                {
                    save.hasHabitat = true;
                    save.habitatCell = habitat.CenterCell;
                    save.habitatSeed = habitat.Seed;
                }

                save.spawnId = unit.SpawnId;

                data.neutrals.Add(save);
            }

            // 재생성 대기 — <b>죽어 있는 종</b>의 기다림이다. 개체 목록에는 아무 흔적이 없으므로
            // 이걸 안 담으면 "죽은 지 얼마나 됐는지"가 불러올 때 0 으로 되돌아간다.
            _neutralSpawner.ExportRestockDelays(data.neutralRestockMonIds, data.neutralRestockSeconds);
        }

        /// <summary>
        /// 토벌 발견 목록 · 부대 토벌 지시 (2026-08-18).
        ///
        /// ★★ 발견 판정이 <b>"지금 시야에 들어와 있는가"</b> 라서, 저장하지 않으면 불러온 순간
        /// 목록이 비고 부대에 걸어둔 지시도 같이 사라진다
        /// (<c>EpicSubjugationService.RestoreState</c> 주석 참조).
        /// </summary>
        void CaptureSubjugation(SaveData data)
        {
            EpicSubjugationService service = EpicSubjugationService.Instance;
            if (service == null) return;

            service.ExportDiscovered(data.subjugationDiscovered);
            // ★ 2026-08-20 — 개체 번호와 <b>따로</b> 종 번호도 저장한다. 개체는 죽으면
            //   사라지지만 «그 종을 안다» 는 사실은 남아야 한다
            //   (EpicSubjugationService 클래스 주석 ★★).
            service.ExportKnownSpecies(data.subjugationKnownSpecies);
            service.ExportOrders(data.subjugationOrderSquads, data.subjugationOrderTargets);
        }

        void CaptureFog(SaveData data)
        {
            if (_fog == null || !_fog.IsReady) return;

            data.fogExplored = _fog.ExportExplored();
            data.fogWidth = _fog.FogSize.x;
            data.fogHeight = _fog.FogSize.y;
        }

        // ==================================================================
        // 되돌리기
        // ==================================================================

        IEnumerator RestoreNextFrame()
        {
            // 다른 컴포넌트의 Start 가 전부 끝난 뒤에 손대야 한다 (클래스 doc 참조).
            yield return null;

            SaveData data = SaveService.PendingLoad;
            SaveService.PendingLoad = null;
            if (data == null) yield break;

            _restoring = true;
            try { Restore(data); }
            finally { _restoring = false; }

            HudLog.Add($"불러왔습니다 — 웨이브 {data.waveNumber} ({data.savedAt})", HudLogKind.Good);
        }

        void Restore(SaveData data)
        {
            RestoreSquads(data);        // 캐릭터를 배정하려면 부대가 먼저 있어야 한다
            RestoreCharacters(data);
            RestoreTowers(data);
            RestoreMonsters(data);
            RestoreNeutrals(data);
            RestoreSubjugation(data);   // 가리킬 개체가 생긴 뒤여야 한다

            ResourceManager resources = ResourceManager.Instance;
            if (resources != null) resources.RestoreEnergy(data.energy);

            RestoreNexus(data);
            RestoreFog(data);

            // ★ 웨이브는 <b>맨 마지막</b>이다 — 단계를 Battle 로 바꾸는 순간
            //   WaveManager 가 "몬스터가 다 죽었나"를 보기 시작하는데, 몬스터를 아직 안 만들었으면
            //   그 프레임에 웨이브가 클리어된다.
            if (_wave != null)
                _wave.RestoreState(data.waveNumber, (WavePhase)data.wavePhase, data.phaseRemaining);
        }

        void RestoreSquads(SaveData data)
        {
            SquadService service = SquadService.Instance;
            if (service == null) return;

            service.ClearAllForRestore();

            RallyPointService rally = RallyPointService.Instance;

            foreach (SquadSave save in data.squads)
            {
                SquadService.Squad squad = service.CreateSquadWithId(save.id, save.name);
                if (squad == null) continue;

                service.SetCoopExpedition(squad.Id, save.coopExpedition);
                if (save.hasRallyPoint && rally != null)
                    rally.SetRallyPoint(save.rallyPoint, squad.Id);
            }
        }

        void RestoreCharacters(SaveData data)
        {
            if (_unitSpawner == null) return;

            // ★★ <b>지울 것을 지우기 «전에» 넣을 것이 있는지 본다</b> (2026-08-21).
            //
            //   ⚠⚠ 순서가 전부다. 아래 <c>DestroySpawnedCharactersForRestore</c> 는 시작
            //     캐릭터 3명을 <b>확실히</b> 지운다(레지스트리까지 훑는다). 그 뒤에 세울 것이
            //     없으면 <b>0명으로 시작</b>한다 — 유저가 본 «가끔 캐릭터 0개» 다.
            //
            //   ★ <see cref="SaveNow"/> 가 0명짜리 세이브를 <b>더는 만들지 않지만</b>,
            //     이미 디스크에 있는 파일은 그대로다. 그것을 열어도 판이 망가지지 않게
            //     읽는 쪽에도 가드를 둔다(«두 겹» — 이 프로젝트가 잠금에 쓰는 방식과 같다).
            //   ★ 여기서 <b>그냥 돌아간다</b> — 시작 캐릭터 3명이 살아 있는 채로 판이 이어진다.
            //     못 쓰는 세이브 때문에 판을 못 하는 것보다 «불러오기만 실패» 가 낫다.
            if (data.characters.Count == 0)
            {
                Debug.LogError("[불러오기] 저장에 캐릭터가 0명입니다 — 불러오기를 건너뜁니다. " +
                               "시작 캐릭터를 그대로 둡니다(그러지 않으면 0명으로 시작한다).", this);
                HudLog.Add("저장에 캐릭터가 없어 불러오지 않았습니다", HudLogKind.Warn);
                return;
            }

            _unitSpawner.DestroySpawnedCharactersForRestore();

            // ★ <b>등장 인물 기록을 세이브 기준으로 다시 세운다</b> (2026-08-21).
            //   위에서 시작 캐릭터 3명을 지웠는데, 그 셋이 뽑힐 때 «등장했다» 로 <b>기록은
            //   남아 있다</b>(재등장 금지의 핵심이라 죽어도 안 지운다). 그대로 두면 이어하기
            //   한 번에 후보가 3명씩 줄어들고, 인물 11명이라 몇 번이면 «등장할 인물 없음» 이 된다.
            //   ⚠ 아래 루프가 <c>SpawnRestored</c> → <c>MarkAppeared</c> 로 <b>저장된 인원을
            //     다시 표시</b>하므로, 여기서 비워도 재등장 금지는 그대로 지켜진다.
            Units.CharacterDefinitionRegistry.ResetRun();

            SquadService squads = SquadService.Instance;

            foreach (CharacterSave save in data.characters)
            {
                CharacterDefinitionSO def = CharacterDefinitionRegistry.ById(save.characterId);

                CharacterUnit unit = _unitSpawner.SpawnRestored(def, save.stats,
                                                                save.upgradeCount, save.position);
                if (unit == null) continue;

                unit.SetGrowthFocus((StatGrowthFocus)save.growthFocus);

                CharacterTactics tactics = unit.GetComponent<CharacterTactics>();
                if (tactics != null && save.order != null) tactics.SetOrder(save.order);

                // 각성 보정을 먼저 되돌린다 — 체력 상한이 이 보정에 걸려 있어서(AddFlatStatBonus),
                // 체력을 맞추기 전에 넣어야 최대 체력이 저장 당시와 같아진다.
                CharacterKills kills = CharacterKills.EnsureOn(unit);
                if (kills != null)
                {
                    kills.RestoreCounts(save.kills, save.awakenings, save.heals);

                    int pairs = Mathf.Min(save.awakenBonusStats.Count, save.awakenBonusAmounts.Count);
                    for (int i = 0; i < pairs; i++)
                    {
                        var type = (StatType)save.awakenBonusStats[i];
                        int amount = save.awakenBonusAmounts[i];
                        unit.AddFlatStatBonus(type, amount);
                        kills.RecordAwakenBonus(type, amount);
                    }
                }

                CharacterErosion erosion = CharacterErosion.EnsureOn(unit);
                if (erosion != null) erosion.RestoreErosion(save.erosion);

                if (squads != null && save.squadId != 0) squads.Assign(unit, save.squadId);

                // 체력은 최대치로 태어난 뒤 저장된 값까지 깎는다 — 포탑 복원과 같은 이유로
                // 피해 파이프라인 밖에 체력 대입 통로를 만들지 않는다.
                int target = Mathf.Clamp(save.currentHp, 1, unit.MaxHp);
                if (target < unit.CurrentHp) unit.ApplyDamage(unit.CurrentHp - target);
            }
        }

        void RestoreTowers(SaveData data)
        {
            BuildService build = BuildService.Instance;
            if (build == null) return;

            foreach (TowerSave save in data.towers)
                build.RestoreTower(save.minCell, save.currentHp);
        }

        void RestoreMonsters(SaveData data)
        {
            if (_monsterSpawner == null) return;

            _monsterSpawner.ClearAll();

            foreach (MonsterSave save in data.monsters)
            {
                MonsterUnit unit = _monsterSpawner.RestoreMonster(
                    save.definitionName, save.position, save.stats);
                if (unit == null) continue;

                int target = Mathf.Clamp(save.currentHp, 1, unit.MaxHp);
                if (target < unit.CurrentHp) unit.ApplyDamage(unit.CurrentHp - target);
            }
        }

        void RestoreNeutrals(SaveData data)
        {
            _restoredNeutrals.Clear();
            if (_neutralSpawner == null) return;

            // ⚠⚠ 서식지를 <b>즉시</b> 되돌린 뒤에 지운다 — 그냥 파괴하면 사라지는 연출이
            //    7.5초 동안 돌면서 <b>방금 새로 그린 서식지를 뒤에서 지워 나간다</b>
            //    (같은 중심·같은 씨앗이라 칸이 정확히 겹친다). ClearAllForRestore 주석 참조.
            _neutralSpawner.ClearAllForRestore();

            foreach (NeutralSave save in data.neutrals)
            {
                NeutralMonsterUnit unit = _neutralSpawner.RestoreNeutral(
                    save.monId, save.position, save.homePosition,
                    save.hasHabitat, save.habitatCell, save.habitatSeed, save.spawnId);
                if (unit == null) continue;

                // 토벌 목록이 개체를 번호로 가리킨다 — 여기서 짝을 기억해 두면 그쪽에서
                // 씬을 다시 훑지 않아도 된다(복원한 마리만 후보라는 것도 여기서 보장된다).
                if (save.spawnId > 0) _restoredNeutrals[save.spawnId] = unit;

                int target = Mathf.Clamp(save.currentHp, 1, unit.MaxHp);
                if (target < unit.CurrentHp) unit.ApplyDamage(unit.CurrentHp - target);
            }

            // 죽어 있던 종의 남은 재생성 대기. ⚠ <b>Start 가 이미 자기 주기를 잡은 뒤</b>라
            // 덮어쓰는 순서가 맞다(스포너 쪽 ImportRestockDelays 주석).
            _neutralSpawner.ImportRestockDelays(data.neutralRestockMonIds, data.neutralRestockSeconds);
        }

        /// <summary>복원한 중립을 개체 번호로 찾기 위한 짝. 토벌 목록 복원에만 쓴다.</summary>
        readonly Dictionary<int, NeutralMonsterUnit> _restoredNeutrals =
            new Dictionary<int, NeutralMonsterUnit>();

        /// <summary>
        /// 토벌 발견 목록 · 부대 토벌 지시를 되돌린다. <b>중립 복원 뒤에</b> 불러야 한다 —
        /// 가리킬 개체가 그때 생긴다.
        /// </summary>
        void RestoreSubjugation(SaveData data)
        {
            EpicSubjugationService service = EpicSubjugationService.Instance;
            if (service == null) return;

            // ⚠ <b>종 기억을 먼저</b> 되돌린다 — RestoreState 는 이걸 비우지 않고, 개체
            //   목록에서 종을 역산해 <b>보태기만</b> 한다(옛 세이브 호환).
            service.RestoreKnownSpecies(data.subjugationKnownSpecies);

            var discovered = new List<NeutralMonsterUnit>();
            foreach (int spawnId in data.subjugationDiscovered)
                if (_restoredNeutrals.TryGetValue(spawnId, out NeutralMonsterUnit unit))
                    discovered.Add(unit);

            var squadIds = new List<int>();
            var targets = new List<NeutralMonsterUnit>();

            int pairs = Mathf.Min(data.subjugationOrderSquads.Count, data.subjugationOrderTargets.Count);
            for (int i = 0; i < pairs; i++)
            {
                if (!_restoredNeutrals.TryGetValue(data.subjugationOrderTargets[i],
                                                   out NeutralMonsterUnit target)) continue;
                squadIds.Add(data.subjugationOrderSquads[i]);
                targets.Add(target);
            }

            service.RestoreState(discovered, squadIds, targets);
        }

        void RestoreNexus(SaveData data)
        {
            if (_unitSpawner == null || _unitSpawner.SpawnedNexus == null) return;
            if (data.nexusHp <= 0) return;

            Nexus nexus = _unitSpawner.SpawnedNexus;
            int target = Mathf.Clamp(data.nexusHp, 1, nexus.MaxHp);
            if (target < nexus.CurrentHp) nexus.ApplyDamage(nexus.CurrentHp - target);
        }

        void RestoreFog(SaveData data)
        {
            if (_fog == null || !_fog.IsReady) return;
            _fog.ImportExplored(data.fogExplored, data.fogWidth, data.fogHeight);
        }
    }
}
