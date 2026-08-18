using System.Collections;
using UnityEngine;

namespace LastSanctuary.Audio
{
    /// <summary>
    /// 로비 화면의 배경음악 (2026-08-18 신설 — 유저 지시 <i>"로비화면에서는 The Last SANCTUARY
    /// 재생해줘 로비화면 들어갈 때마다 브금 페이드 인 해서 시작하고"</i>).
    ///
    /// <b>왜 <see cref="BgmService"/> 를 안 쓰나</b> — 그쪽은 <c>WaveManager</c> 의 <b>단계</b>를
    /// 따라 곡을 고르는 것이 전부다(<c>ClipForPhase</c>). 로비에는 <c>WaveManager</c> 가 없어서
    /// 경고를 남기고 <b>아무 곡도 틀지 않는다</b>. 거기에 "로비 모드" 갈래를 내면
    /// 그 클래스가 두 가지 일을 하게 되고, 곡 표(상황 5개)에 로비가 끼어들어 표가 흐려진다.
    ///
    /// 여기서 하는 일은 <b>한 곡을 페이드 인 해서 반복 재생</b>하는 것뿐이라 훨씬 작다.
    ///
    /// <b>"들어갈 때마다"</b> — 로비 씬이 열릴 때마다 이 컴포넌트가 새로 생기므로
    /// (<c>DontDestroyOnLoad</c> 를 쓰지 않는다) 페이드 인이 저절로 매번 일어난다.
    ///
    /// ⚠ 시간은 <see cref="Time.unscaledDeltaTime"/> 으로 잰다 — 게임에서 일시정지
    /// (<c>timeScale = 0</c>)한 채 로비로 나오면 페이드가 영영 멈춘다.
    ///
    /// ⚠ 곡은 <b>이름으로</b> 읽는다 — <c>AudioClip</c> 은 에셋 참조라 MCP 로 인스펙터에 넣을 수
    /// 없다(진행상황 8절 4번). <see cref="BgmService"/> 가 같은 이유로 같은 방식을 쓴다.
    /// </summary>
    public class LobbyBgm : MonoBehaviour
    {
        [Header("곡 (Resources/Bgm 아래의 파일 이름 — 확장자 없이)")]
        [SerializeField] string resourceFolder = "Bgm";
        [SerializeField] string clipName = "The Last Sanctuary";

        [Header("재생")]
        [Range(0f, 1f)] [SerializeField] float volume = 0.6f;

        [Tooltip("무음에서 이 음량까지 올라오는 시간(초)")]
        [Min(0f)] [SerializeField] float fadeInSeconds = 2f;

        AudioSource _source;

        void Start()
        {
            AudioClip clip = Load();
            if (clip == null) return;

            // AudioSource 는 코드로 붙인다 — 씬에 배선할 것을 만들지 않기 위해서다
            // (BgmService.NewSource 와 같은 판단).
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;          // 로비에 오래 머물러도 끊기지 않는다
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;    // 2D — 카메라 위치와 무관하게 같은 크기로 들린다
            _source.volume = 0f;
            _source.Play();

            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            if (fadeInSeconds <= 0f)
            {
                _source.volume = volume;
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / fadeInSeconds;
                _source.volume = volume * Mathf.Clamp01(t);
                yield return null;
            }
            _source.volume = volume;
        }

        AudioClip Load()
        {
            if (string.IsNullOrWhiteSpace(clipName)) return null;

            string path = string.IsNullOrEmpty(resourceFolder)
                ? clipName
                : $"{resourceFolder}/{clipName}";

            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip == null)
                Debug.LogWarning($"[로비 BGM] Resources/{path} 를 찾지 못했습니다.", this);

            return clip;
        }
    }
}
