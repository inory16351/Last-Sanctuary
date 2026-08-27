using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Data;

namespace LastSanctuary.Units
{
    /// <summary>
    /// ★★★ <b>두 번째로 등장하는 인물에게 «다른 이름» 을 준다</b> (2026-08-26 신설 · 유저 지시:
    /// *"같은 캐릭터가 두번째로 등장할때는 랜덤한 다른 이름을 가지고 태어나게 해 다른 인물처럼
    /// 보이도록. 그리고 엔딩에 이름이 다르게 실릴 수 있도록"*).
    ///
    /// <b>왜 «정의» 를 새로 만들지 않는가</b> — 인물 정의(<see cref="CharacterDefinitionSO"/>)는
    /// 표가 정본이고 능력치·스킨·초상화·얼굴 초점까지 묶여 있다. «이름만 다른 사람» 을 위해
    /// 정의를 복제하면 표가 두 벌이 된다. 그래서 <b>이름만</b> 갈아 끼운다 —
    /// 화면·로그·엔딩은 전부 <see cref="Combat.DamageableUnit.DisplayName"/> 하나를 보므로
    /// (2026-08-15 에 그렇게 모아 뒀다) 그 한 곳만 덮으면 «다른 인물» 로 보인다.
    ///
    /// <b>이름의 정본</b> — <c>데이터 테이블/대체 이름 테이블.xlsx</c> 이고, 문구는
    /// <b>스트링 키 테이블</b>의 <c>character_altname_N</c> 키(한국어·영어)다.
    /// 그래서 <b>언어를 바꾸면 대체 이름도 그 언어로</b> 나온다 — 이름을 문자열로 들고 있으면
    /// 그렇게 안 된다. 그래서 이 서비스는 <b>이름이 아니라 «키» 를 배정</b>한다.
    ///
    /// <b>몇 개인지 코드에 적지 않는다</b> — <c>character_altname_1</c> 부터 <b>빈 번호가
    /// 나올 때까지</b> 훑어 목록을 만든다(<see cref="Keys"/>). 표에 이름을 더하면 코드를
    /// 고치지 않아도 늘어난다.
    ///
    /// ★★ <b>성별을 가린다</b> (2026-08-27 · 유저 지시: *"남캐는 남자 이름 여캐는 여자이름으로
    ///   들어가는 시스템으로"*) — 이름 주머니의 성별은 <c>대체 이름 테이블.xlsx</c> 가 정본이고,
    ///   <c>Tools/gen_alt_name_table.py</c> 가 <c>Resources/Data/AltNameGender.txt</c>(TSV)로
    ///   내보낸다. <b>스트링 표에 넣지 않은 이유</b> — 성별은 화면에 나가는 «문구» 가 아니다.
    ///   번역할 것이 없는 값을 스트링 표에 넣으면 «영어 빈칸» 검사가 매번 걸리고,
    ///   죽은 키인지 산 키인지도 구분이 안 된다(182-5절에서 겪은 종류의 함정이다).
    ///
    /// ⚠ <b>성별을 모르면 안 가린다</b> — 인물의 성별이 <see cref="CharacterGender.Unknown"/>
    ///   이거나 성별 파일이 아예 없으면 <b>예전처럼</b> 아무 이름이나 뽑는다. 이름이 없어서
    ///   원래 이름으로 태어나는 것보다, 성별만 못 맞추는 편이 눈에 덜 띈다.
    ///
    /// ⚠ <b>같은 판에서 이름이 겹치지 않는다</b> — 한 번 쓴 키는 <see cref="_used"/> 에 남는다.
    ///   이름이 다 떨어지면 «정의의 원래 이름» 으로 돌아간다(억지로 겹치게 하지 않는다).
    /// ⚠ <b>세이브를 건넌다</b> — 배정된 키는 <c>CharacterSave.altNameKey</c> 로 저장되고
    ///   복원할 때 그대로 돌아온다. 안 그러면 이어하기에서 이름이 다시 바뀌어
    ///   <b>엔딩 명단과 화면이 어긋난다</b>.
    /// </summary>
    public static class CharacterAltNames
    {
        /// <summary>스트링 키의 앞머리. 뒤에 1부터의 번호가 붙는다.</summary>
        public const string KeyPrefix = "character_altname_";

        /// <summary>
        /// 이름별 성별표의 <c>Resources.Load</c> 경로(확장자 없음).
        /// <c>Tools/gen_alt_name_table.py</c> 가 <b>대체 이름 테이블에서</b> 내보낸다.
        /// </summary>
        public const string GenderResourcePath = "Data/AltNameGender";

        /// <summary>표를 훑을 때의 상한 — 빈 번호가 나오면 거기서 멈추므로 사실상 안전판이다.</summary>
        const int MaxProbe = 500;

        static List<string> _keys;

        /// <summary>인물 정의 ID → 이번 판에 <b>몇 번째</b> 등장인가(1 부터).</summary>
        static readonly Dictionary<int, int> _appeared = new Dictionary<int, int>();

        /// <summary>이번 판에 이미 쓴 대체 이름 키.</summary>
        static readonly HashSet<string> _used = new HashSet<string>();

        /// <summary>대체 이름 키 → 그 이름의 성별. 파일이 없으면 <b>비어 있고</b>, 그때는 안 가린다.</summary>
        static Dictionary<string, CharacterGender> _genderByKey;

        /// <summary>
        /// 표에 있는 대체 이름 키 전부. 처음 불릴 때 한 번 훑고 캐시한다.
        /// 스트링 표가 없으면 빈 목록이라 <b>예전 동작</b>(늘 정의의 이름)이 된다.
        /// </summary>
        public static IReadOnlyList<string> Keys
        {
            get
            {
                if (_keys != null) return _keys;

                _keys = new List<string>(64);
                for (int i = 1; i <= MaxProbe; i++)
                {
                    string key = KeyPrefix + i;
                    if (!StringTable.Has(key)) break;      // 빈 번호 = 목록 끝
                    _keys.Add(key);
                }

                if (_keys.Count == 0)
                    Debug.LogWarning($"[이름] 스트링 표에 '{KeyPrefix}1' 이 없습니다 — " +
                                     "두 번째 등장 인물도 원래 이름으로 태어납니다. " +
                                     "python Tools/gen_alt_name_table.py 를 돌리세요.");
                return _keys;
            }
        }

        /// <summary>
        /// 이 인물이 <b>이번 판에 몇 번째로</b> 등장하는지 세고, <b>두 번째부터</b>는
        /// 아직 안 쓴 대체 이름 키를 무작위로 하나 준다. 첫 등장이거나 이름이 다 떨어지면
        /// <c>null</c>(= 정의의 원래 이름).
        /// </summary>
        /// <param name="gender">
        /// 인물의 성별. <b>같은 성별의 이름만</b> 뽑는다(2026-08-27 유저 지시).
        /// <see cref="CharacterGender.Unknown"/> 이면 안 가린다 — 표에 칸이 비었을 때의 안전판이다.
        /// </param>
        public static string RegisterAppearance(int definitionId,
                                                CharacterGender gender = CharacterGender.Unknown)
        {
            if (definitionId <= 0) return null;

            _appeared.TryGetValue(definitionId, out int count);
            count++;
            _appeared[definitionId] = count;

            if (count <= 1) return null;                   // 첫 등장은 본래 이름

            string key = PickUnused(gender);
            if (key == null)
            {
                // ★ 성별 때문에 못 뽑은 것인지 이름이 동난 것인지를 갈라 적는다 —
                //   전자는 «표에 그 성별 이름을 더하세요» 이고 후자는 «판이 길었다» 라서
                //   할 일이 다르다.
                bool anyLeft = PickUnused(CharacterGender.Unknown) != null;
                Debug.Log($"[이름] 인물 {definitionId} 의 {count}번째 등장인데 " +
                          (anyLeft && gender != CharacterGender.Unknown
                              ? $"{gender} 이름이 남지 않아 원래 이름으로 태어납니다 " +
                                "(대체 이름 테이블에 그 성별 이름을 더하세요)."
                              : "쓸 수 있는 대체 이름이 남지 않아 원래 이름으로 태어납니다."));
                return null;
            }

            _used.Add(key);
            return key;
        }

        /// <summary>이 대체 이름 키의 성별. 성별표에 없으면 <see cref="CharacterGender.Unknown"/>.</summary>
        public static CharacterGender GenderOf(string altNameKey)
        {
            EnsureGenders();
            if (string.IsNullOrEmpty(altNameKey)) return CharacterGender.Unknown;
            return _genderByKey.TryGetValue(altNameKey, out CharacterGender g)
                ? g : CharacterGender.Unknown;
        }

        /// <summary>
        /// 세이브에서 되살릴 때 쓴다 — <b>세지 않고</b> «이 키는 이미 쓰였다» 만 기록한다.
        /// (복원은 등장 순서를 다시 밟는 것이 아니므로 <see cref="RegisterAppearance"/> 를
        /// 부르면 <b>이름이 새로 배정</b>되어 엔딩 명단과 어긋난다.)
        /// </summary>
        public static void MarkRestored(int definitionId, string altNameKey)
        {
            if (definitionId > 0)
            {
                _appeared.TryGetValue(definitionId, out int count);
                _appeared[definitionId] = Mathf.Max(count, string.IsNullOrEmpty(altNameKey) ? 1 : 2);
            }
            if (!string.IsNullOrEmpty(altNameKey)) _used.Add(altNameKey);
        }

        /// <summary>
        /// 새 판 — 등장 기록과 쓴 이름을 비운다.
        ///
        /// ⚠ <b>2026-08-27 — 부르는 자리가 둘이다.</b> ① <c>RunResetService.ClearRunState</c>(새 판)
        ///   ② <c>GameSnapshot.RestoreCharacters</c>(이어하기). 이어하기는 ①의 문을 지나지 않아
        ///   <b>지난 판의 등장 기록이 그대로 남았고</b>, 그래서 이어한 판에서 <b>처음 만든
        ///   캐릭터가 곧바로 대체 이름</b>을 달고 나왔다(유저 리포트). ②는 비운 뒤
        ///   <see cref="MarkRestored"/> 로 세이브에 든 사실을 다시 채운다 — 자세한 이유는
        ///   그쪽 주석에 적어 뒀다.
        /// </summary>
        public static void ResetRun()
        {
            _appeared.Clear();
            _used.Clear();
        }

        static string PickUnused(CharacterGender want)
        {
            IReadOnlyList<string> keys = Keys;
            if (keys.Count == 0) return null;

            EnsureGenders();

            // ★ 성별표가 통째로 없으면 «가리지 않는다» — 예전(2026-08-26) 동작 그대로다.
            //   여기서 엄격하게 굴면 파일 하나가 빠졌을 때 <b>모든 인물이 원래 이름</b>이 되어
            //   기능이 통째로 죽는데, 그게 화면에서는 «두 번째 등장이 안 되네» 로 보인다.
            bool filter = want != CharacterGender.Unknown && _genderByKey.Count > 0;

            // 안 쓴 것 중에서 고른다. 후보를 새 목록에 담지 않으려고 «몇 개 남았는지» 를
            // 먼저 세고 그중 n 번째를 집는다 — 매 생성마다 할당하지 않기 위한 것이다.
            int free = 0;
            for (int i = 0; i < keys.Count; i++)
                if (Eligible(keys[i], want, filter)) free++;
            if (free == 0) return null;

            int pick = Random.Range(0, free);
            for (int i = 0; i < keys.Count; i++)
            {
                if (!Eligible(keys[i], want, filter)) continue;
                if (pick-- == 0) return keys[i];
            }
            return null;
        }

        static bool Eligible(string key, CharacterGender want, bool filter)
        {
            if (_used.Contains(key)) return false;
            if (!filter) return true;
            return _genderByKey.TryGetValue(key, out CharacterGender g) && g == want;
        }

        /// <summary>
        /// 이름별 성별표를 한 번 읽는다. TSV 두 칸(<c>alt_name_key · gender</c>)이고
        /// 형식·이유는 <see cref="Data.StringTable"/> 과 같다(<c>#</c> 주석 · 헤더 한 줄).
        /// 파일이 없으면 <b>빈 표</b>로 두고 경고만 남긴다 — 그때는 성별을 안 가린다.
        /// </summary>
        static void EnsureGenders()
        {
            if (_genderByKey != null) return;
            _genderByKey = new Dictionary<string, CharacterGender>(64);

            var asset = Resources.Load<TextAsset>(GenderResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[이름] Resources/{GenderResourcePath} 를 찾지 못했습니다 — " +
                                 "두 번째 등장 이름의 성별을 가리지 않습니다. " +
                                 "python Tools/gen_alt_name_table.py 를 돌려 내보내세요.");
                return;
            }

            string[] lines = asset.text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;

                string[] cells = line.Split('\t');
                string key = cells[0].Trim();
                if (key.Length == 0 || key == "alt_name_key") continue;

                CharacterGender g = CharacterGenderText.Parse(cells.Length > 1 ? cells[1] : null);
                if (g == CharacterGender.Unknown)
                {
                    // 뽑기에서 «성별 없는 이름» 은 아무에게도 안 간다 — 조용히 사라지면
                    // «이 이름은 왜 한 번도 안 나오지» 가 되므로 여기서 이름을 찍는다.
                    Debug.LogWarning($"[이름] '{key}' 의 성별 칸이 비었거나 모르는 값입니다 " +
                                     $"('{(cells.Length > 1 ? cells[1] : string.Empty)}') — " +
                                     "이 이름은 뽑히지 않습니다.");
                    continue;
                }
                _genderByKey[key] = g;
            }
        }

        /// <summary>
        /// 도메인 리로드를 꺼도 판마다 초기화되게 한다(이 프로젝트의 static 초기화 규칙 —
        /// <c>StringTable.ResetStatics</c> 와 같은 자리).
        /// ⚠ <see cref="_keys"/> 도 비운다 — 표를 다시 내보낸 뒤 에디터에서 바로 반영되게.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _keys = null;
            _genderByKey = null;
            _appeared.Clear();
            _used.Clear();
        }
    }
}
