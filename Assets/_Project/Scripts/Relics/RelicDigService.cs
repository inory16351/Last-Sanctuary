using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Combat;
using LastSanctuary.Fog;
using LastSanctuary.Map;
using LastSanctuary.UI;
using LastSanctuary.Units;

namespace LastSanctuary.Relics
{
    /// <summary>발굴 가능 칸 하나. <see cref="LastSanctuary.Buildings.BuildSite"/> 와 같은 모양이다.</summary>
    public class DigSite
    {
        public Vector3Int Cell;
        public Vector3 Center;

        /// <summary>한 번이라도 캐릭터 시야에 들어왔는가 — <b>기억</b>이다(표 Info 시트 «보이기»).</summary>
        public bool Revealed;

        /// <summary>유저가 «파라» 고 눌렀는가. 누르기 전에는 캐릭터가 가지 않는다.</summary>
        public bool Ordered;

        /// <summary>지금까지 쌓인 발굴 시간(초).</summary>
        public float Progress;

        /// <summary>이 자리를 맡은 캐릭터. <b>한 자리에 한 명</b>(건설과 같은 규칙).</summary>
        public CharacterBehavior Digger;

        /// <summary>
        /// ★★ 이 자리가 쓸 <b>대사 묶음</b> (2026-08-24 · 표 Ver02 <c>Dialogue</c> 시트).
        ///
        /// 자리를 만들 때 한 번 뽑아 <b>여기에 기억해 둔다</b> — 창을 다시 열 때마다 말투가
        /// 바뀌면 «다른 자리를 보고 있나» 싶어진다. 발견의 말투와 결과의 말투는 이어져야 한다.
        /// </summary>
        public int DialogueGroup;

        public float Ratio(float required) => required > 0f ? Mathf.Clamp01(Progress / required) : 0f;
    }

    /// <summary>
    /// ★★★ <b>유물 발굴</b> (2026-08-23 신설 · 유저 지시).
    ///
    /// <code>
    ///   ② 맵이 생성(게임 재시작)될 때마다 맵 전체에 랜덤한 칸이 총 N개 발굴 가능 칸으로 선정
    ///   ③ 그 칸이 캐릭터의 시야에 보이면 느낌표가 뜨고 클릭 가능해진다.
    ///      클릭하면 «기존에 삭제된 건설처럼» 가장 가까운 캐릭터가 15초에 걸쳐 발굴하고,
    ///      일정 확률에 따라 에너지·체력 회복/감소·유물 획득 등이 일어난다
    /// </code>
    ///
    /// <b>왜 건설과 «같은 구조» 인가</b> — 유저 지시가 그렇게 못박았고("기존에 삭제된 건설처럼"),
    /// 실제로 필요한 것이 같다: «자리 목록 · 한 자리에 한 명 · 걸어가서 시간을 채운다 ·
    /// 진행도를 화면에 겹쳐 그린다». <see cref="LastSanctuary.Buildings.BuildService"/> 가
    /// 이미 그 네 가지를 다 갖고 있으므로 <b>그 구조를 그대로 옮겼다</b> — 두 기능이 서로 다른
    /// 조작감을 갖지 않게 하려는 것이다(그 클래스가 집결지에서 구조를 가져온 것과 같은 이유).
    ///
    /// ★ <b>다른 점 하나 — 클릭을 «월드» 가 아니라 «UI» 로 받는다.</b>
    ///   건설은 «배치 모드» 로 들어가 맵을 클릭하지만, 발굴은 모드가 없고 <b>칸에 뜬 느낌표</b>를
    ///   직접 누른다(지시 3번). 월드 클릭으로 받으면 <see cref="UnitSelector"/>·집결지와
    ///   <b>같은 클릭을 두고 다툰다</b>. 느낌표를 <b>UI 버튼</b>으로 두면 그쪽들이 이미
    ///   «포인터가 UI 위면 무시» 하므로 다툼이 아예 생기지 않는다.
    ///
    /// ★ <b>자리는 <see cref="Start"/> 에서 고른다 — 그것이 곧 «맵이 생성될 때마다» 다.</b>
    ///   이 프로젝트의 맵은 <see cref="MapGenerator.Awake"/> 가 <b>판마다 새 씨앗으로</b>
    ///   다시 만든다(그쪽 <c>ResolveStartupSeed</c>). 유니티는 «모든 Awake 가 모든 Start 보다
    ///   먼저» 를 보장하므로, <see cref="Start"/> 에 오면 <see cref="MapGenerator.MapSize"/> ·
    ///   걸을 수 있는 칸이 이미 확정돼 있다 — 안개·흐름장·스포너가 전부 같은 이유로
    ///   <c>Start</c> 에서 맵을 읽는다.
    /// ⚠ <b>이어하기는 새로 고르지 않는다</b> — 저장된 자리를 <see cref="Restore"/> 가 넣는다.
    ///   안 그러면 이어할 때마다 자리가 바뀌어 «가던 캐릭터가 허공을 판다».
    /// </summary>
    public class RelicDigService : MonoBehaviour
    {
        public static RelicDigService Instance { get; private set; }

        // ------------------------------------------------------------------
        // 인스펙터 — 유저 지시: *"발굴 가능 칸 갯수는 너가 정하고 에딧에서 수정 가능하게"*
        // ------------------------------------------------------------------

        [Header("발굴 가능 칸")]
        [Tooltip("맵 전체에 뿌릴 발굴 가능 칸 수.\n" +
                 "★ 기본 24 — 맵이 넓어 한 판에 다 밝히기 어렵고, 이 정도면 «탐험하다 가끔 만난다» " +
                 "가 된다. 너무 많으면 발굴이 주 수입원이 되어 웨이브를 미루는 것이 최적 전략이 된다")]
        [Min(0)] [SerializeField] int digSiteCount = 24;

        [Tooltip("성역에서 이만큼(타일) 떨어진 곳에만 둔다 — 시작하자마자 다 캐지 않게")]
        [Min(0f)] [SerializeField] float minDistanceFromNexus = 14f;

        [Tooltip("발굴 칸끼리 이만큼(타일)은 떨어뜨린다 — 한곳에 몰리면 «한 번 가서 다 캔다» 가 된다")]
        [Min(0f)] [SerializeField] float minSpacing = 10f;

        [Tooltip("자리를 고를 때 시도할 최대 횟수. 이만큼 굴려도 자리가 안 나오면 포기한다")]
        [Min(1)] [SerializeField] int placementAttempts = 4000;

        [Header("발굴")]
        [Tooltip("한 칸을 파는 데 걸리는 시간(초). 유저 지시: 15초")]
        [Min(1f)] [SerializeField] float digSeconds = 15f;

        [Tooltip("캐릭터가 이 거리(타일) 안에 들어와야 발굴이 진행된다")]
        [Min(0.5f)] [SerializeField] float digWorkRange = 1.6f;

        [Header("오버레이 (모체 하나를 복제해서 쓴다 — 비활성으로 둘 것)")]
        [Tooltip("느낌표 표식의 원본. UI_Root/DigOverlay 아래. 비어 있으면 이름으로 찾는다")]
        [SerializeField] RectTransform markerTemplate;

        [SerializeField] RectTransform overlayParent;

        [SerializeField] Color idleColor = new Color(0.98f, 0.85f, 0.35f, 0.95f);
        [SerializeField] Color orderedColor = new Color(0.45f, 0.95f, 0.78f, 0.95f);

        [Tooltip("표식의 화면 크기(픽셀). 카메라 줌과 무관하게 일정하게 둔다 — 작아지면 못 누른다")]
        [Min(8f)] [SerializeField] float markerPixels = 34f;

        [Header("표식 그림 (2026-08-25)")]
        [Tooltip("느낌표 원화의 Resources 경로. 비우거나 못 찾으면 <b>글자 느낌표</b>로 돌아간다.\n" +
                 "굽는 스크립트: Tools/dig_marker_build.py")]
        [SerializeField] string markerSpriteResource = "DigMarker/dig_marker";

        [Tooltip("평소 표식의 색. ★ 원화를 <b>그려진 대로</b> 보여주려면 흰색이어야 한다 — " +
                 "다른 색을 주면 곱연산으로 물든다")]
        [SerializeField] Color markerSpriteIdle = Color.white;

        [Tooltip("파는 중일 때의 색. 진행도가 오르면 평소 색으로 돌아온다 — " +
                 "«지금 누가 파고 있다» 를 색이 옅어지는 것으로 알린다")]
        [SerializeField] Color markerSpriteDigging = new Color(0.52f, 0.56f, 0.62f, 0.95f);

        [Header("통통 튀기 (2026-08-25 · 주의를 끌기 위해)")]
        [Tooltip("튀어오르는 높이(픽셀). 0 이면 튀지 않는다")]
        [Min(0f)] [SerializeField] float bounceHeight = 9f;

        [Tooltip("한 번 튀고 다음에 튀기까지의 주기(초)")]
        [Min(0.05f)] [SerializeField] float bouncePeriod = 0.95f;

        [Tooltip("주기 중 <b>공중에 있는</b> 비율. 1 보다 작아야 바닥에서 잠깐 쉰다 — " +
                 "그 «쉼» 이 있어야 통통 튀는 것으로 보인다. 1 이면 계속 흐물거린다")]
        [Range(0.15f, 1f)] [SerializeField] float bounceAirRatio = 0.55f;

        [Tooltip("표식마다 튀는 때를 이만큼 어긋나게 한다(초). 0 이면 전부 <b>한꺼번에</b> 튄다")]
        [Min(0f)] [SerializeField] float bouncePhaseStep = 0.19f;

        /// <summary>느낌표 원화. 못 찾으면 null 이고, 그때는 글자 느낌표를 그대로 쓴다.</summary>
        Sprite _markerSprite;

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        readonly List<DigSite> _sites = new List<DigSite>();
        readonly List<RectTransform> _markers = new List<RectTransform>();
        readonly List<CharacterBehavior> _freeWorkers = new List<CharacterBehavior>();

        int _assignFrame = -1;
        Camera _camera;
        MapGenerator _map;
        FogOfWarService _fog;
        RelicDigTableSO _table;

        /// <summary>지금 남아 있는 발굴 칸. <see cref="CharacterBehavior"/> 가 읽어간다.</summary>
        public IReadOnlyList<DigSite> Sites => _sites;

        /// <summary>한 칸을 파는 데 필요한 시간(초). UI 가 진행도를 그릴 때도 쓴다.</summary>
        public float DigSeconds => Mathf.Max(1f, digSeconds);

        /// <summary>지금 화면에 보이는(= 발견한) 칸 수. 액션 버튼의 뱃지가 읽는다.</summary>
        public int RevealedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _sites.Count; i++) if (_sites[i].Revealed) n++;
                return n;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // ★ 드랍 판정은 상태가 없는 정적 클래스다 — <b>이 서비스의 생애에 맞춰</b>
            //   붙였다 뗀다(그 클래스의 ⚠: 안 떼면 다음 판에 두 배로 걸린다).
            RelicDropService.Hook();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // ⚠ 정적 이벤트를 붙여 둔 것이 있으므로 판이 끝날 때 반드시 뗀다(그 클래스들의 ⚠).
            RelicDropService.Unhook();
            RelicEffectService.Unhook();
        }

        /// <summary>발굴 대사표(표 Ver02). 없으면 창이 <c>Fallback</c> 한 줄로 뜬다.</summary>
        RelicDialogueTableSO _dialogue;

        /// <summary>대사 묶음 번호들 — 자리를 만들 때 여기서 하나 뽑는다.</summary>
        readonly List<int> _dialogueGroups = new List<int>();

        /// <summary>마지막으로 본 웨이브 번호. 바뀌는 순간이 «웨이브가 넘어갔다» 다.</summary>
        int _seenWave = -1;

        Wave.WaveManager _wave;

        void Start()
        {
            _camera = Camera.main;
            _map = FindAnyObjectByType<MapGenerator>();
            _fog = FindAnyObjectByType<FogOfWarService>();
            _table = RelicDigTableSO.Load();
            _dialogue = Resources.Load<RelicDialogueTableSO>("Relics/RelicDialogueTable");
            if (_dialogue != null) _dialogueGroups.AddRange(_dialogue.GroupIds());
            else Debug.LogWarning("[유물] Resources/Relics/RelicDialogueTable 이 없습니다 — "
                                  + "발굴 창이 기본 문구로 뜹니다. gen_relic_assets.py 를 돌려주세요.", this);

            ResolveOverlay();

            // ★ 이어하기가 예약돼 있으면 자리를 새로 고르지 않는다 — 저장된 자리를
            //   <see cref="Restore"/> 가 넣어 준다(그쪽이 늦게 도착해도 되도록 비워 둔다).
            if (Save.SaveService.PendingLoad != null) return;

            PickSites();
        }

        void ResolveOverlay()
        {
            GameObject canvas = GameObject.Find("UI_Root");
            Transform overlay = canvas != null ? canvas.transform.Find("DigOverlay") : null;

            if (markerTemplate == null && overlay != null)
                markerTemplate = overlay.Find("DigMarkerTemplate") as RectTransform;
            if (overlayParent == null) overlayParent = overlay as RectTransform;
            if (overlayParent == null && markerTemplate != null)
                overlayParent = markerTemplate.parent as RectTransform;

            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
            else Debug.LogWarning("[유물] DigOverlay/DigMarkerTemplate 을 찾지 못했습니다 — " +
                                  "발굴 칸이 화면에 안 보입니다.", this);

            LoadMarkerSprite();
        }

        /// <summary>
        /// ★★ <b>느낌표를 글자에서 원화로 바꾼다</b> (2026-08-25 · 유저 지시:
        /// *"느낌표 스프라이트 볼트에 넣어놨으니까 <b>텍스트 대신 주황색 느낌표 짤라서 써</b>
        /// 발굴칸에"*).
        ///
        /// ★ 원화를 <b>못 찾으면 글자 느낌표를 그대로 둔다.</b> 표식이 아예 안 보이는 것보다
        ///   «옛 모양으로 보이는» 편이 낫고, 무엇을 굽지 않았는지 경고로 알린다.
        /// ⚠ <b>원본(템플릿)의 글자를 끈다</b> — 복제는 템플릿을 그대로 베끼므로 여기서 한 번
        ///   끄면 앞으로 만들어지는 표식 전부에 적용된다. 표식마다 끄면 프레임마다 일이 생긴다.
        /// ⚠ 스프라이트 «참조» 는 MCP 로 넣을 수 없다(진행상황 8절 4번) — 그래서 씬이 아니라
        ///   <b>코드가 Resources 에서 읽어</b> 꽂는다(<c>HudTheme.Font</c> 와 같은 방식).
        /// </summary>
        void LoadMarkerSprite()
        {
            if (string.IsNullOrWhiteSpace(markerSpriteResource)) return;

            _markerSprite = Resources.Load<Sprite>(markerSpriteResource);
            if (_markerSprite == null)
            {
                Debug.LogWarning($"[유물] 느낌표 원화를 찾지 못했습니다: Resources/{markerSpriteResource} " +
                                 "— py -3 Tools/dig_marker_build.py 를 돌리세요. " +
                                 "그때까지는 글자 느낌표로 보입니다.", this);
                return;
            }

            if (markerTemplate == null) return;

            Transform label = markerTemplate.Find("Label");
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);

            // ★ 원화의 가로세로가 1:1 이 아니다(느낌표는 세로로 길다). preserveAspect 를 켜지
            //   않으면 <b>납작하게 눌린다</b>. 칸은 정사각으로 두어 누르는 넓이를 지킨다.
            if (markerTemplate.TryGetComponent(out Image templateImage))
            {
                templateImage.sprite = _markerSprite;
                templateImage.preserveAspect = true;
                templateImage.color = markerSpriteIdle;
            }
        }

        // ==================================================================
        // 자리 고르기
        // ==================================================================

        /// <summary>
        /// 맵 전체에서 발굴 가능 칸을 <see cref="digSiteCount"/> 개 고른다.
        ///
        /// 조건 셋(표 Info 시트 «자리 고르기»):
        /// <code>
        ///   ① 걸을 수 있고 벽·구조물이 없는 칸       (IsCellPlaceable)
        ///   ② 성역에서 minDistanceFromNexus 밖
        ///   ③ 다른 발굴 칸과 minSpacing 이상 떨어짐
        /// </code>
        /// ⚠ 조건을 만족하는 자리가 모자라면 <b>있는 만큼만</b> 둔다 — 무한 루프를 돌지 않는다.
        /// </summary>
        public void PickSites()
        {
            _sites.Clear();
            if (_map == null || digSiteCount <= 0) return;

            Vector3 nexus = NexusPosition();
            float nexusSqr = minDistanceFromNexus * minDistanceFromNexus;
            float spaceSqr = minSpacing * minSpacing;

            Vector2Int size = _map.MapSize;
            Vector2Int origin = _map.Origin;
            if (size.x <= 0 || size.y <= 0) return;

            int tries = 0;
            while (_sites.Count < digSiteCount && tries < placementAttempts)
            {
                tries++;
                var local = new Vector2Int(Random.Range(0, size.x), Random.Range(0, size.y));
                Vector3Int cell = _map.LocalToCell(local);
                if (!_map.IsCellPlaceable(cell)) continue;

                Vector3 world = _map.CellCenterWorld(cell);
                if (((Vector2)(world - nexus)).sqrMagnitude < nexusSqr) continue;

                bool tooClose = false;
                for (int i = 0; i < _sites.Count; i++)
                    if (((Vector2)(world - _sites[i].Center)).sqrMagnitude < spaceSqr) { tooClose = true; break; }
                if (tooClose) continue;

                _sites.Add(new DigSite { Cell = cell, Center = world,
                                         DialogueGroup = RollDialogueGroup() });
            }

            if (logChanges)
                Debug.Log($"[유물] 발굴 가능 칸 {_sites.Count}개 배치 (목표 {digSiteCount} · 시도 {tries})", this);
            if (_sites.Count < digSiteCount)
                Debug.LogWarning($"[유물] 조건에 맞는 자리가 모자라 {_sites.Count}개만 두었습니다 — " +
                                 "minSpacing / minDistanceFromNexus 를 줄여보세요.", this);
        }

        Vector3 NexusPosition()
        {
            var nexus = FindAnyObjectByType<Nexus>();
            return nexus != null ? nexus.transform.position : Vector3.zero;
        }

        // ==================================================================
        // 매 프레임
        // ==================================================================

        /// <summary>대사 묶음 하나를 고른다. 표가 없으면 0(창이 Fallback 문구로 뜬다).</summary>
        int RollDialogueGroup() =>
            _dialogueGroups.Count > 0 ? _dialogueGroups[Random.Range(0, _dialogueGroups.Count)] : 0;

        void Update()
        {
            UpdateReveal();
            UpdateMarkers();
            WatchWave();

            // ★ 문턱형 유물(「두꺼워진 가피」)을 여기서 함께 재운다 — 그 하나 때문에
            //   MonoBehaviour 를 새로 두지 않는다(RelicEffectService.Tick 의 주석).
            RelicEffectService.Tick();
        }

        /// <summary>
        /// 시야에 들어온 칸을 <b>발견</b>으로 표시한다.
        ///
        /// ★ <b>안개가 걷힌 것만으로는 안 된다</b> — 유저 지시가 «캐릭터의 시야에 보일 경우» 다.
        ///   그래서 <see cref="FogOfWarService.IsVisible"/>(지금 누군가의 시야 안인가)를 본다.
        ///   ⚠ 한 번 본 칸은 <b>계속 보인다</b>(기억) — 시야를 벗어날 때마다 느낌표가
        ///   사라지면 «분명 봤는데 없어졌다» 가 된다.
        /// </summary>
        /// <summary>
        /// ★★ <b>웨이브 계열 유물의 통로</b> (2026-08-24 · 표 Ver02 의
        /// <c>relic_wave_shield</c> · <c>relic_wave_energy</c> · <c>relic_wave_heal</c>).
        ///
        /// <b>왜 여기서 보나</b> — <see cref="Wave.WaveManager"/> 의 이벤트는 <b>정적이 아니라
        /// 인스턴스</b> 것이라 <see cref="RelicEffectService"/>(정적 클래스)가 붙을 수 없다.
        /// 매니저가 유물을 알아야 하게 만드는 것도 방향이 거꾸로다(유물은 나중에 생긴 것이다).
        /// 그래서 <b>이미 매 프레임 도는 이 서비스</b>가 웨이브 번호가 바뀌는 것을 보고
        /// 알려준다 — 「두꺼워진 가피」를 이 <c>Update</c> 에 얹은 것과 <b>같은 이유</b>다.
        ///
        /// ⚠ 첫 프레임(<c>_seenWave &lt; 0</c>)에는 «바뀌었다» 로 보지 않는다 —
        ///   판을 켜자마자 보호막이 공짜로 걸리면 안 된다.
        /// </summary>
        void WatchWave()
        {
            // ⚠ WaveManager 에는 static Instance 가 없다 — 한 번 찾아서 들고 있는다.
            if (_wave == null)
            {
                _wave = FindAnyObjectByType<Wave.WaveManager>();
                if (_wave == null) return;
            }

            int now = _wave.WaveNumber;
            if (now == _seenWave) return;

            bool first = _seenWave < 0;
            _seenWave = now;
            if (first) return;

            RelicEffectService.OnWaveEnded();     // 지난 웨이브가 끝났고
            RelicEffectService.OnWaveSpawned();   // 새 웨이브가 시작된다
        }

        void UpdateReveal()
        {
            if (_fog == null || !_fog.IsReady) return;
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (s.Revealed) continue;
                if (!_fog.IsVisible(s.Cell)) continue;

                s.Revealed = true;
                HudLog.Add("발굴할 수 있는 자리를 찾았습니다", HudLogKind.Good);
            }
        }

        // ==================================================================
        // 표식 (UI 버튼)
        // ==================================================================

        void UpdateMarkers()
        {
            if (overlayParent == null || markerTemplate == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            int need = 0;
            for (int i = 0; i < _sites.Count; i++) if (_sites[i].Revealed) need++;

            while (_markers.Count < need)
            {
                RectTransform clone = Instantiate(markerTemplate, overlayParent);
                clone.name = $"DigMarker_{_markers.Count + 1}";
                _markers.Add(clone);
            }

            int slot = 0;
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (!s.Revealed) continue;

                RectTransform item = _markers[slot++];
                Vector3 screen = _camera.WorldToScreenPoint(s.Center);
                if (screen.z < 0f)
                {
                    if (item.gameObject.activeSelf) item.gameObject.SetActive(false);
                    continue;
                }

                if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayParent, screen, null, out Vector2 local);
                // ★ 파는 중에는 튀지 않는다 — 이미 사람이 가고 있으니 <b>주의를 끌 일이 끝났다</b>.
                item.anchoredPosition = local + new Vector2(0f, s.Ordered ? 0f : BounceOffset(slot - 1));
                item.sizeDelta = new Vector2(markerPixels, markerPixels);

                var img = item.GetComponent<Image>();
                if (img != null)
                {
                    if (_markerSprite != null)
                    {
                        // ⚠ 복제가 템플릿보다 먼저 만들어졌을 수 있다(저장된 씬의 값) —
                        //   그래서 여기서도 한 번 확인한다. 같으면 아무 일도 하지 않는다.
                        if (img.sprite != _markerSprite)
                        {
                            img.sprite = _markerSprite;
                            img.preserveAspect = true;
                        }
                        img.color = s.Ordered
                            ? Color.Lerp(markerSpriteDigging, markerSpriteIdle, s.Ratio(DigSeconds))
                            : markerSpriteIdle;
                    }
                    else
                    {
                        // 원화가 없을 때의 옛 모양 — 파는 중이면 진행도를 색으로 보여준다.
                        img.color = s.Ordered
                            ? Color.Lerp(orderedColor, Color.white, s.Ratio(DigSeconds))
                            : idleColor;
                    }
                }

                // ⚠ 버튼은 <b>매 프레임 다시 배선한다</b> — 표식은 자리마다 재사용되므로
                //   («다섯 번째 표식» 이 프레임마다 다른 자리를 가리킬 수 있다) 지난 프레임의
                //   람다가 남아 있으면 <b>엉뚱한 자리를 판다</b>.
                var button = item.GetComponent<Button>();
                if (button != null)
                {
                    DigSite captured = s;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => Open(captured));
                    button.interactable = !captured.Ordered;
                }
            }

            for (int i = slot; i < _markers.Count; i++)
                if (_markers[i].gameObject.activeSelf) _markers[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// ★★ <b>통통 튀는 높이</b> (2026-08-25 · 유저 지시: *"통통 튀게 해줘 주의를 끌 수 있도록"*).
        ///
        /// <b>왜 사인 곡선이 아닌가</b> — <c>Sin</c> 은 위아래로 <b>고르게 흐물거린다</b>.
        /// 통통 튀는 것으로 보이려면 <b>바닥에서 쉬는 시간</b>이 있어야 한다. 실제로 튀는 공이
        /// 그렇다 — 잠깐 솟았다가 내려와 <b>머문다</b>. 그래서 «공중에 있는 동안» 만
        /// 포물선(<c>4x(1-x)</c>)을 그리고 나머지 시간은 <b>0</b>으로 둔다.
        ///
        /// <code>
        ///   |    ▁▄█▄▁          ▁▄█▄▁          ▁▄█▄▁
        ///   |____      __________     __________     ____   ← 이 «쉼» 이 통통의 정체다
        ///        공중         바닥
        /// </code>
        ///
        /// ★ 표식마다 <b>때를 어긋나게</b> 한다(<see cref="bouncePhaseStep"/>) — 여럿이
        ///   한꺼번에 튀면 기계처럼 보이고, 어긋나면 살아 있는 것처럼 보인다.
        /// ⚠ <see cref="Time.unscaledTime"/> 을 쓴다 — 조언 카드나 일시정지로 시간이 멈춰도
        ///   표식은 계속 튀어야 «누를 수 있는 것» 으로 읽힌다(멈춘 동안에도 누를 수 있다).
        /// </summary>
        float BounceOffset(int index)
        {
            if (bounceHeight <= 0f || bouncePeriod <= 0f) return 0f;

            float t = Mathf.Repeat(Time.unscaledTime + index * bouncePhaseStep, bouncePeriod)
                    / bouncePeriod;
            if (t >= bounceAirRatio) return 0f;          // 바닥에서 쉬는 구간

            float x = t / bounceAirRatio;                 // 공중에 있는 동안을 0~1 로
            return 4f * x * (1f - x) * bounceHeight;      // 포물선 — 올랐다 내린다
        }

        /// <summary>
        /// ★★ <b>느낌표를 눌렀다 — 곧바로 파지 않고 «묻는다»</b> (2026-08-24 · 유저 지시:
        /// <i>"유물 자동 발굴 되게 하지말고 … 해당 칸을 누를 경우 발굴 ui가 나와서
        /// 발굴하기를 누르면 가장 가까운 캐릭터가 가서 발굴하게"</i>).
        ///
        /// 창이 없으면(씬을 아직 안 만들었으면) <b>예전처럼 곧바로 지시한다</b> —
        /// UI 하나 때문에 기능이 통째로 죽으면 안 된다.
        /// </summary>
        public void Open(DigSite site)
        {
            if (site == null || site.Ordered) return;

            var panel = UI.RelicDigPanel.Instance;
            if (panel == null) { Confirm(site); return; }

            panel.PresentSite(site, _dialogue);
        }

        /// <summary>창에서 «파러 간다» 를 골랐다 — 이제야 지시가 나간다.</summary>
        public void Confirm(DigSite site)
        {
            if (site == null || site.Ordered) return;
            site.Ordered = true;
            HudLog.Add("발굴을 지시했습니다 — 가장 가까운 캐릭터가 갑니다", HudLogKind.Good);
        }

        // ==================================================================
        // 배정 · 진행 (건설과 같은 규칙)
        // ==================================================================

        /// <summary>
        /// 이 캐릭터가 맡은 발굴 자리. 없으면 null.
        /// <see cref="LastSanctuary.Buildings.BuildService.AssignedSiteFor"/> 와 같은 구조다 —
        /// 배정은 <b>프레임당 한 번</b> 전체를 보고 정한다(캐릭터마다 스스로 고르면 한 자리에 몰린다).
        /// </summary>
        public DigSite AssignedSiteFor(CharacterBehavior worker)
        {
            if (worker == null || _sites.Count == 0) return null;

            if (_assignFrame != Time.frameCount)
            {
                _assignFrame = Time.frameCount;
                AssignDiggers();
            }

            for (int i = 0; i < _sites.Count; i++)
                if (_sites[i].Digger == worker) return _sites[i];
            return null;
        }

        void AssignDiggers()
        {
            _freeWorkers.Clear();
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                DamageableUnit u = units[i];
                if (u == null || !u.IsAlive || u.Kind != UnitKind.Character) continue;
                var worker = u.GetComponent<CharacterBehavior>();
                if (worker != null && worker.CanTakeBuildOrder) _freeWorkers.Add(worker);
            }

            // 유지되는 배정은 후보에서 뺀다(한 명이 두 자리를 맡지 않게).
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (s.Digger == null) continue;
                if (!s.Ordered || !_freeWorkers.Remove(s.Digger)) s.Digger = null;
            }

            // 남은 자리 × 남은 후보 중 <b>가장 가까운 짝</b>부터 (유저 지시: «가장 가까운 캐릭터»).
            while (_freeWorkers.Count > 0)
            {
                DigSite bestSite = null;
                CharacterBehavior bestWorker = null;
                float bestSqr = float.PositiveInfinity;

                for (int i = 0; i < _sites.Count; i++)
                {
                    DigSite s = _sites[i];
                    if (!s.Ordered || s.Digger != null) continue;

                    for (int w = 0; w < _freeWorkers.Count; w++)
                    {
                        CharacterBehavior worker = _freeWorkers[w];
                        float sqr = ((Vector2)(s.Center - worker.transform.position)).sqrMagnitude;
                        if (sqr >= bestSqr) continue;
                        bestSite = s;
                        bestWorker = worker;
                        bestSqr = sqr;
                    }
                }

                if (bestWorker == null) break;
                bestSite.Digger = bestWorker;
                _freeWorkers.Remove(bestWorker);
            }
        }

        /// <summary>캐릭터가 이 거리 안에 들어와야 삽질이 진행된다.</summary>
        public float WorkRange => digWorkRange;

        /// <summary>
        /// 현장에서 <paramref name="seconds"/> 만큼 판다. 한 자리에 한 명이므로 사람이 늘어도
        /// 빨라지지 않는다 — 대신 <see cref="RelicEffectType.DigSpeed"/> 유물이 빠르게 한다.
        /// </summary>
        public void Contribute(DigSite site, float seconds, CharacterUnit digger)
        {
            if (site == null || seconds <= 0f || !_sites.Contains(site)) return;

            site.Progress += seconds * RelicEffectService.DigSpeedMultiplier(digger);
            if (site.Progress < DigSeconds) return;

            Complete(site, digger);
        }

        // ==================================================================
        // 결과
        // ==================================================================

        void Complete(DigSite site, CharacterUnit digger)
        {
            _sites.Remove(site);

            DigOutcomeRow row = _table != null ? _table.Roll() : null;
            if (row == null)
            {
                HudLog.Add("발굴을 마쳤지만 아무것도 나오지 않았습니다");
                return;
            }

            string what = ApplyOutcome(row, digger);
            string who = digger != null ? digger.DisplayName : "누군가";
            HudLog.Add(string.IsNullOrEmpty(what)
                           ? $"{who} — 발굴: {row.Script}"
                           : $"{who} — 발굴: {what}",
                       what.StartsWith("−") || row.outcomeType == "dig_hurt" ||
                       row.outcomeType == "dig_erosion_up"
                           ? HudLogKind.Warn
                           : HudLogKind.Good);

            // ★ 결과 창 — 같은 대사 묶음의 result 줄 + 표의 결과 문구(2026-08-24).
            var panel = UI.RelicDigPanel.Instance;
            if (panel != null)
            {
                string flavor = _dialogue != null
                    ? _dialogue.Roll(site.DialogueGroup, RelicDialogueSituation.Result) : "";
                if (string.IsNullOrWhiteSpace(flavor))
                    flavor = RelicDialogueTableSO.Fallback(RelicDialogueSituation.Result);

                string line = row.Script;
                if (!string.IsNullOrEmpty(what)) line += "\n" + what;
                panel.PresentResult(flavor, line, _lastGrantedIcon);
            }
            _lastGrantedIcon = null;

            if (logChanges) Debug.Log($"[유물] 발굴 완료 {site.Cell} → {row.outcomeType}", this);
        }

        /// <summary>
        /// 발굴 결과 한 줄을 적용한다 — 표 <c>DigOutcome</c> 시트의 <c>outcome_type</c> 과 1:1.
        ///
        /// ⚠ <b>여기 없는 타입은 «아무 일도 안 일어난다»</b>. 표에 새 결과를 더하면
        ///   반드시 가지도 함께 더할 것(등록기가 유물 효과에 대해 하는 경고와 같은 규칙).
        /// </summary>
        string ApplyOutcome(DigOutcomeRow row, CharacterUnit digger)
        {
            switch (row.outcomeType)
            {
                case "dig_energy":
                case "dig_energy_big":
                    Resource.ResourceManager.Instance?.AddEnergy(row.value01);
                    return $"에너지 +{row.value01}";

                case "dig_heal":
                {
                    if (digger == null || !digger.IsAlive) return "";
                    int amount = Mathf.Max(1, Mathf.RoundToInt(digger.MaxHp * row.value01 * 0.01f));
                    digger.Heal(amount);
                    return $"체력 +{row.value01}%";
                }

                case "dig_hurt":
                {
                    if (digger == null || !digger.IsAlive) return "";
                    int amount = Mathf.Max(1, Mathf.RoundToInt(digger.MaxHp * row.value01 * 0.01f));
                    // ⚠ 표: *"이 피해로 죽지 않습니다"* — 체력 1 을 남긴다(이벤트 보상과 같은 규칙).
                    int safe = Mathf.Min(amount, Mathf.Max(0, digger.CurrentHp - 1));
                    if (safe > 0) digger.ApplyDamage(safe);
                    return $"체력 −{row.value01}%";
                }

                case "dig_erosion_up":
                case "dig_erosion_down":
                {
                    var er = digger != null ? CharacterErosion.Of(digger) : null;
                    if (er == null) return "";
                    bool up = row.outcomeType == "dig_erosion_up";
                    er.AddErosion(up ? row.value01 : -row.value01);
                    return up ? $"침식 +{row.value01}" : $"침식 −{row.value01}";
                }

                case "dig_nothing":
                    return "";

                case "dig_relic_common": return GrantRelic(RelicGrade.Common, false);
                case "dig_relic_rare":   return GrantRelic(RelicGrade.Rare, false);
                case "dig_relic_epic":   return GrantRelic(RelicGrade.Epic, true);
            }
            return "";
        }

        /// <summary>방금 발굴로 얻은 유물의 아이콘 — 결과 창이 그린다. 없으면 null.</summary>
        Sprite _lastGrantedIcon;

        string GrantRelic(RelicGrade grade, bool digOnly)
        {
            RelicDefinitionSO relic = RelicRegistry.RollGrade(grade, digOnly);
            if (relic == null) return "";
            RelicInventory.Instance?.Grant(relic);
            _lastGrantedIcon = relic.icon;
            return $"유물 「{relic.DisplayName}」 ({RelicDefinitionSO.NameOf(grade)})";
        }

        // ==================================================================
        // 세이브
        // ==================================================================

        /// <summary>세이브용 — (칸 x, 칸 y, 발견 여부, 진행도 ×100) 로 접는다.</summary>
        public List<Vector4> Capture()
        {
            var list = new List<Vector4>(_sites.Count);
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                list.Add(new Vector4(s.Cell.x, s.Cell.y,
                                     (s.Revealed ? 1 : 0) + (s.Ordered ? 2 : 0),
                                     s.Progress));
            }
            return list;
        }

        /// <summary>이어하기 — 저장된 자리를 되살린다. 없으면 새로 고른다.</summary>
        public void Restore(List<Vector4> saved)
        {
            if (saved == null || saved.Count == 0)
            {
                if (_sites.Count == 0) PickSites();
                return;
            }

            _sites.Clear();
            if (_map == null) _map = FindAnyObjectByType<MapGenerator>();
            for (int i = 0; i < saved.Count; i++)
            {
                Vector4 v = saved[i];
                var cell = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), 0);
                int flags = Mathf.RoundToInt(v.z);
                _sites.Add(new DigSite
                {
                    Cell = cell,
                    Center = _map != null ? _map.CellCenterWorld(cell) : Vector3.zero,
                    Revealed = (flags & 1) != 0,
                    Ordered = (flags & 2) != 0,
                    Progress = v.w,
                    // ⚠ 대사 묶음은 <b>저장하지 않는다</b> — 세이브 형식(Vector4)에 칸이 없고,
                    //   말투가 이어하기 뒤에 달라지는 것은 «틀린» 것이 아니다.
                    //   자리·진행도처럼 «맞아야 하는» 값과 구분한다.
                    DialogueGroup = RollDialogueGroup(),
                });
            }
            if (logChanges) Debug.Log($"[유물] 발굴 칸 {_sites.Count}개 복원", this);
        }
    }
}
