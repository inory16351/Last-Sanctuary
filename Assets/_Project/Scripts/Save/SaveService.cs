using System.IO;
using UnityEngine;

namespace LastSanctuary.Save
{
    /// <summary>
    /// 저장 파일과 환경 설정의 <b>보관 담당</b>. 게임 상태를 읽고 쓰는 것은
    /// <see cref="GameSnapshot"/> 이 하고, 이 클래스는 <b>파일 · 슬롯 · 씬 전환</b>만 다룬다.
    ///
    /// <b>정적 클래스인 이유</b> — 씬을 넘나들며 살아 있어야 한다. 로비에서 "이어하기" 를 누르면
    /// 저장 파일을 읽어두고 게임 씬을 불러오는데, 그 사이에 <c>MonoBehaviour</c> 는 전부 파괴된다.
    /// <c>DontDestroyOnLoad</c> 오브젝트를 만드는 방법도 있지만 그러면 <b>씬에 배선할 것이 늘고</b>
    /// (MCP 제약, 진행상황 8절 4번) 로비 씬에도 같은 오브젝트를 놔야 한다.
    ///
    /// <b>슬롯은 하나다</b>(유저 확정 2026-08-18) — 기본이 자동 저장이라 슬롯을 고르는 순간이 없다.
    /// </summary>
    public static class SaveService
    {
        const string FileName = "slot01.json";

        /// <summary>음량은 저장 파일이 아니라 <c>PlayerPrefs</c> 에 둔다 — 새로하기로 저장을 지워도
        /// 유지돼야 하는 값이고, 로비에서도 게임 씬에서도 똑같이 읽혀야 한다.</summary>
        const string VolumeKey = "LastSanctuary.Volume";

        /// <summary>
        /// 씬을 넘길 때 들고 가는 저장 데이터. 로비의 "이어하기" 가 채우고,
        /// 게임 씬의 <see cref="GameSnapshot"/> 이 첫 프레임에 꺼내 쓴 뒤 비운다.
        /// </summary>
        public static SaveData PendingLoad { get; set; }

        /// <summary>마지막 저장/불러오기 결과 메시지. HUD 로그·로비 표시에 쓴다.</summary>
        public static string LastMessage { get; private set; } = string.Empty;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // 도메인 리로드를 꺼도 static 이 남는다 — 플레이를 다시 시작할 때 옛 데이터가
            // 남아 있으면 "새로하기" 가 조용히 이어하기가 된다.
            PendingLoad = null;
            LastMessage = string.Empty;
        }

        // ==================================================================
        // 파일
        // ==================================================================

        public static bool HasSave => File.Exists(FilePath);

        /// <summary>저장 파일을 읽는다. 없거나 깨졌거나 판이 다르면 null.</summary>
        public static SaveData Load()
        {
            if (!HasSave) return null;

            try
            {
                string json = File.ReadAllText(FilePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    LastMessage = "저장 파일을 읽지 못했습니다.";
                    Debug.LogWarning($"[저장] 파싱 실패 — {FilePath}");
                    return null;
                }

                // ★ 판이 다르면 <b>거부한다</b>. 구조가 바뀐 파일을 억지로 읽으면 빈 목록으로
                //   복원되어 "캐릭터가 사라진 세이브" 같은 조용한 손상이 된다.
                if (data.version != SaveData.CurrentVersion)
                {
                    LastMessage = $"저장 형식이 다릅니다 (파일 {data.version} · 지금 {SaveData.CurrentVersion}).";
                    Debug.LogWarning($"[저장] {LastMessage}");
                    return null;
                }

                return data;
            }
            catch (System.Exception e)
            {
                LastMessage = "저장 파일을 읽지 못했습니다.";
                Debug.LogError($"[저장] 읽기 실패 — {e.Message}");
                return null;
            }
        }

        /// <summary>저장 파일을 쓴다. 성공 여부를 돌려준다.</summary>
        public static bool Write(SaveData data)
        {
            if (data == null) return false;

            try
            {
                data.version = SaveData.CurrentVersion;
                data.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // ⚠ <b>임시 파일에 먼저 쓰고 바꿔친다.</b> 저장 도중에 게임이 죽으면
                //   반쯤 쓰인 파일이 남아 다음 실행에서 세이브가 통째로 날아간다 —
                //   자동 저장이 웨이브 중에도 도는 만큼 그 확률이 낮지 않다.
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, prettyPrint: false));

                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);

                LastMessage = $"저장했습니다 ({data.savedAt})";
                return true;
            }
            catch (System.Exception e)
            {
                LastMessage = "저장하지 못했습니다.";
                Debug.LogError($"[저장] 쓰기 실패 — {e.Message}");
                return false;
            }
        }

        /// <summary>저장 파일을 지운다 ("새로하기").</summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                PendingLoad = null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[저장] 삭제 실패 — {e.Message}");
            }
        }

        /// <summary>저장 시각만 읽어 온다 — 로비의 "이어하기" 설명에 쓴다. 없으면 빈 문자열.</summary>
        public static string SavedAtLabel()
        {
            SaveData data = Load();
            return data != null ? data.savedAt : string.Empty;
        }

        // ==================================================================
        // 음량 (환경 설정)
        // ==================================================================

        /// <summary>
        /// 전체 음량 0~1. <see cref="AudioListener.volume"/> 하나로 다룬다 —
        /// 지금 이 프로젝트에 소리는 BGM 뿐이라(효과음 시스템이 아직 없다) 채널을 나눌 것이 없고,
        /// 나중에 효과음이 생겨도 이 값이 <b>전체 음량</b>으로 그대로 유효하다.
        /// </summary>
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            set
            {
                float v = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, v);
                PlayerPrefs.Save();
                AudioListener.volume = v;
            }
        }

        /// <summary>
        /// 저장된 음량을 <see cref="AudioListener"/> 에 실제로 반영한다.
        /// 씬마다 한 번 불러야 한다 — <c>AudioListener.volume</c> 은 씬을 넘겨도 유지되지만,
        /// 빌드를 새로 켰을 때는 기본값 1 이라 저장된 값을 한 번 밀어 넣어야 한다.
        /// </summary>
        public static void ApplyVolume() => AudioListener.volume = Volume;
    }
}
