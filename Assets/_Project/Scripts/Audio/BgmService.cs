using UnityEngine;
using LastSanctuary.Units;
using LastSanctuary.Wave;

namespace LastSanctuary.Audio
{
    /// <summary>
    /// 상황별 배경음악. <see cref="WaveManager"/> 의 단계를 그대로 따라가며 곡을 갈아끼운다.
    ///
    /// <b>규칙 (유저 확정)</b> — 한 상황에는 그 상황의 곡 <b>하나만</b> 나오고, 그 상황이
    /// 이어지는 동안 <b>끊기지 않고 계속</b> 재생된다.
    ///
    /// | 상황 | 곡 |
    /// |---|---|
    /// | 대기(정비)시간 | Safe Haven |
    /// | 진군 중        | The Quiet Advance |
    /// | 웨이브(전투)   | Endless Wave |
    /// | 광폭화         | Corruption Rising |
    /// | 보스 등장 웨이브 | The Last Sanctuary |
    ///
    /// "끊기지 않는다" 를 위해 두 가지를 지킨다.
    ///   1. <c>AudioSource.loop = true</c> — 곡이 상황보다 짧아도 이어서 돈다.
    ///   2. <b>같은 곡이면 다시 시작하지 않는다</b> — 웨이브 번호가 올라 다시 대기시간에
    ///      들어가도 Safe Haven 이 처음부터 튀지 않고 그대로 이어진다. 단계가 여러 번
    ///      바뀌어도 <b>목표 곡이 바뀔 때만</b> 전환이 일어난다.
    ///
    /// <b>전환은 크로스페이드</b>다. <see cref="AudioSource"/> 두 개를 번갈아 쓰면서 하나는
    /// 올리고 하나는 내린다 — 곡이 뚝 끊기고 다음 곡이 시작하는 것보다 훨씬 자연스럽다.
    ///
    /// <b>왜 곡을 이름(문자열)으로 읽는가</b> — <c>AudioClip</c> 은 오브젝트 참조라 MCP 로
    /// 씬 인스펙터에 넣을 수 없다(진행상황 8절 4번). <c>Resources/Bgm/</c> 아래에 두고
    /// 파일명 문자열로 읽으면 씬에 연결할 참조가 하나도 없다 —
    /// <see cref="LastSanctuary.Combat.CharacterSkinSO"/> · HUD 폰트 · 탄환 스프라이트가
    /// 전부 같은 이유로 <c>Resources</c> 를 쓴다. 곡을 바꾸려면 이 문자열만 고치면 된다.
    /// </summary>
    public class BgmService : MonoBehaviour
    {
        [Header("곡 (Resources/Bgm 아래의 파일 이름 — 확장자 없이)")]
        [SerializeField] string resourceFolder = "Bgm";
        [SerializeField] string preparationClip = "Safe Haven";
        [SerializeField] string marchClip = "The Quiet Advance";
        [SerializeField] string battleClip = "Endless Wave";
        [SerializeField] string enrageClip = "Corruption Rising";
        [SerializeField] string bossClip = "The Last Sanctuary";

        [Header("보스 웨이브 처리")]
        [Tooltip("보스 웨이브의 진군 구간에도 보스 곡을 쓸지. 끄면 진군은 평소대로 The Quiet Advance")]
        [SerializeField] bool bossTrackCoversMarch = false;

        [Tooltip("보스 웨이브의 광폭화 구간에도 보스 곡을 쓸지. 끄면 광폭화는 Corruption Rising")]
        [SerializeField] bool bossTrackCoversEnrage = false;

        [Header("재생")]
        [Range(0f, 1f)] [SerializeField] float volume = 0.6f;

        [Tooltip("곡을 바꿀 때 겹쳐서 넘기는 시간(초). 0 이면 즉시 전환")]
        [Min(0f)] [SerializeField] float crossfadeSeconds = 1.5f;

        [Tooltip("패배 시 페이드 아웃하고 멈춘다")]
        [SerializeField] bool stopOnDefeat = true;

        [Header("디버그")]
        [SerializeField] bool logTrackChanges = true;

        WaveManager _wave;
        MonsterSpawner _spawner;

        // 크로스페이드용 두 채널. 하나가 재생 중이면 다른 하나가 다음 곡을 받는다.
        AudioSource _a;
        AudioSource _b;
        bool _useB;

        AudioClip _current;
        float _fade;                 // 0 → 현재 채널만, 1 → 다음 채널로 완전히 넘어감
        bool _fading;

        AudioSource Active => _useB ? _b : _a;
        AudioSource Idle => _useB ? _a : _b;

        void Awake()
        {
            // AudioSource 는 코드로 붙인다 — 씬에 배선할 것을 만들지 않기 위해서다.
            _a = NewSource();
            _b = NewSource();
        }

        AudioSource NewSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;            // 상황이 곡보다 길어도 끊기지 않게
            src.volume = 0f;
            src.spatialBlend = 0f;      // 2D — 카메라 위치와 무관하게 같은 크기로 들린다
            return src;
        }

        void Start()
        {
            _wave = FindAnyObjectByType<WaveManager>();
            _spawner = FindAnyObjectByType<MonsterSpawner>();

            if (_wave == null)
            {
                Debug.LogWarning("[BGM] WaveManager 를 찾지 못했습니다. 배경음악이 전환되지 않습니다.", this);
                return;
            }

            _wave.OnPhaseChanged += HandlePhaseChanged;
            Apply(ClipForPhase(_wave.Phase), instant: true);
        }

        void OnDestroy()
        {
            if (_wave != null) _wave.OnPhaseChanged -= HandlePhaseChanged;
        }

        void HandlePhaseChanged(WavePhase phase) => Apply(ClipForPhase(phase), instant: false);

        void Update()
        {
            // 단계가 안 바뀌어도 보스 등장 여부가 늦게 확정될 수 있으므로(예: 표를 못 읽는
            // 구성) 매 프레임 목표 곡을 한 번 더 확인한다. 목표가 그대로면 아무 일도 안 한다.
            if (_wave != null) Apply(ClipForPhase(_wave.Phase), instant: false);

            TickCrossfade();
        }

        /// <summary>지금 단계에서 나와야 할 곡. 보스 웨이브면 해당 구간을 보스 곡으로 바꾼다.</summary>
        AudioClip ClipForPhase(WavePhase phase)
        {
            bool boss = IsBossWave();

            switch (phase)
            {
                case WavePhase.Preparation:
                    return Load(preparationClip);

                case WavePhase.Marching:
                    return Load(boss && bossTrackCoversMarch ? bossClip : marchClip);

                case WavePhase.Battle:
                    return Load(boss ? bossClip : battleClip);

                case WavePhase.Enrage:
                    return Load(boss && bossTrackCoversEnrage ? bossClip : enrageClip);

                case WavePhase.Defeat:
                    return stopOnDefeat ? null : _current;

                default:
                    return null;    // Idle — 아직 게임이 시작되지 않았다
            }
        }

        /// <summary>
        /// 이번 웨이브에 보스가 나오는지. <b>웨이브 표를 보고 판단한다</b> — 살아있는 보스를
        /// 찾는 방식은 소환이 전투 내내 나눠 이뤄지므로(27절 소환 주기 개편) 보스가 늦게
        /// 등장하면 곡이 도중에 바뀌어버린다.
        /// </summary>
        bool IsBossWave() =>
            _spawner != null && _wave != null && _spawner.IsBossWave(_wave.WaveNumber);

        // ------------------------------------------------------------------

        void Apply(AudioClip next, bool instant)
        {
            if (next == _current) return;       // ★ 같은 곡이면 절대 다시 시작하지 않는다
            _current = next;

            if (logTrackChanges)
                Debug.Log($"[BGM] → {(next != null ? next.name : "(정지)")}", this);

            if (instant || crossfadeSeconds <= 0f)
            {
                Idle.Stop();
                Active.clip = next;
                Active.volume = next != null ? volume : 0f;
                if (next != null) Active.Play();
                else Active.Stop();
                _fading = false;
                return;
            }

            // 다음 곡을 놀고 있는 채널에 얹고, 두 채널의 볼륨을 서로 반대로 굴린다.
            AudioSource incoming = Idle;
            incoming.clip = next;
            incoming.volume = 0f;
            if (next != null) incoming.Play();

            _useB = !_useB;                     // 이제 incoming 이 Active 가 된다
            _fade = 0f;
            _fading = true;
        }

        void TickCrossfade()
        {
            if (!_fading) return;

            _fade += Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeSeconds);
            float t = Mathf.Clamp01(_fade);

            Active.volume = volume * t;
            Idle.volume = volume * (1f - t);

            if (t < 1f) return;

            Idle.Stop();
            Idle.clip = null;
            Idle.volume = 0f;
            _fading = false;
        }

        AudioClip Load(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;

            string path = string.IsNullOrEmpty(resourceFolder)
                ? clipName
                : $"{resourceFolder}/{clipName}";

            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip == null && !_missingWarned.Contains(path))
            {
                _missingWarned.Add(path);
                Debug.LogWarning($"[BGM] Resources/{path} 를 찾지 못했습니다.", this);
            }
            return clip;
        }

        /// <summary>없는 곡 경고를 매 프레임 쏟지 않게 한 번만 남긴다.</summary>
        readonly System.Collections.Generic.HashSet<string> _missingWarned =
            new System.Collections.Generic.HashSet<string>();
    }
}
