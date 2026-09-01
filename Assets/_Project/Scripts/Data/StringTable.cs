using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Data
{
    /// <summary>
    /// 표시 언어. <b>값이 곧 스트링 테이블의 컬럼 번호</b>다
    /// (<c>Tools/gen_string_table.py</c> 의 <c>LANGS</c> 순서와 1:1 이어야 한다).
    ///
    /// ⚠⚠ <b>가운데에 끼워 넣지 말 것 — 항상 뒤에 붙인다.</b> 이 값은 유저의
    ///   <see cref="PlayerPrefs"/> 에 <b>정수로</b> 저장돼 있다(<c>ls_language</c>).
    ///   중간에 하나를 끼우면 이미 «English(1)» 를 고른 사람이 다음 실행에서
    ///   <b>엉뚱한 언어</b>로 시작한다 — 되돌릴 방법이 없는 종류의 사고다.
    ///   같은 이유로 <c>StatType</c> 의 0~3 번도 재배치를 금지해 뒀다.
    ///
    /// ★ 2026-09-01 — 유저 지시로 <b>일곱 언어를 뒤에 붙였다</b>
    ///   (스페인어 · 프랑스어 · 독일어 · 일본어 · 러시아어 · 포르투갈어 · 폴란드어).
    /// </summary>
    public enum GameLanguage
    {
        Korean = 0,
        English = 1,
        Spanish = 2,
        French = 3,
        German = 4,
        Japanese = 5,
        Russian = 6,
        Portuguese = 7,
        Polish = 8,

        COUNT = 9,
    }

    /// <summary>
    /// <b>스트링 테이블</b> — 게임에 뜨는 모든 문자열의 정본
    /// (유저 지시 2026-08-12: "string key 테이블 따로 빼서 모든 테이블 스트링 따로 관리").
    ///
    /// 원본은 <c>데이터 테이블/스트링 키 테이블.xlsx</c> 이고,
    /// <c>Tools/gen_string_table.py</c> 가 그것을
    /// <c>Assets/_Project/Resources/Data/StringTable.txt</c>(TSV)로 내보낸다.
    /// 이 클래스는 그 파일을 읽어 <b>키 → 지금 언어의 문자열</b>만 돌려준다.
    ///
    /// <b>왜 static 인가</b> — 문구를 읽는 곳이 UI·로그·유닛 이름까지 흩어져 있어서
    /// 서비스 오브젝트를 찾아 들고 다니게 하면 호출부마다 null 검사가 붙는다.
    /// 데이터가 읽기 전용이고 씬마다 달라질 이유도 없으므로 static 이 맞다.
    /// (같은 이유로 <c>UnitRegistry</c> 도 static 이다.)
    ///
    /// <b>왜 TSV 인가</b> — 한국어 문구에는 쉼표가 흔해서 CSV 면 인용부호 처리가 필요하다.
    /// 탭은 문구에 나올 일이 없다. 확장자가 <c>.txt</c> 인 이유는 Unity 가 <c>.tsv</c> 를
    /// <c>TextAsset</c> 으로 임포트하지 않기 때문이다.
    ///
    /// <b>★ 폴백 사슬</b> — 요청한 언어 → <b>한국어</b> → 호출부가 준 기본값 → <b>키 문자열 자체</b>.
    /// 절대 빈 문자열이나 null 을 돌려주지 않는다. 번역이 안 된 칸이 화면에서 사라지면
    /// "왜 아무것도 안 뜨지"로 보이는데, 키가 그대로 보이면 <b>어느 키를 채워야 하는지</b>
    /// 바로 알 수 있다. 영어 칸은 지금 대부분 비어 있으므로 이 사슬이 실제로 쓰인다.
    /// </summary>
    public static class StringTable
    {
        /// <summary><c>Resources.Load</c> 경로 (확장자 없음).</summary>
        public const string ResourcePath = "Data/StringTable";

        /// <summary>key → 언어별 칸. 길이는 <see cref="GameLanguage.COUNT"/> 로 맞춘다.</summary>
        static Dictionary<string, string[]> _rows;
        static GameLanguage _language = GameLanguage.Korean;

        /// <summary>언어가 바뀐 직후 발생. 이미 그려둔 UI 를 다시 그리는 데 쓴다.</summary>
        public static event System.Action OnLanguageChanged;

        /// <summary>지금 표시 언어. 바꾸면 <see cref="OnLanguageChanged"/> 가 발생한다.</summary>
        public static GameLanguage Language
        {
            get => _language;
            set
            {
                if (_language == value) return;
                _language = value;
                OnLanguageChanged?.Invoke();
            }
        }

        /// <summary>불려온 키 개수 (0 이면 아직 안 읽었거나 파일이 없다).</summary>
        public static int Count
        {
            get { EnsureLoaded(); return _rows.Count; }
        }

        /// <summary>그 키가 표에 있는지. <b>번역이 비어 있어도 true</b> — 행의 존재만 본다.</summary>
        public static bool Has(string key)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(key) && _rows.ContainsKey(key);
        }

        /// <summary>
        /// 키에 해당하는 문자열. 위 폴백 사슬을 그대로 따른다.
        /// <paramref name="fallback"/> 은 <b>기존 리터럴</b>을 넘기는 용도다 —
        /// 스트링 키를 아직 안 붙인 에셋이 그대로 동작하게 하려는 하위 호환 장치다.
        /// </summary>
        public static string Get(string key, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback ?? string.Empty;

            EnsureLoaded();

            if (_rows.TryGetValue(key, out string[] cells))
            {
                int column = (int)_language;
                if (column < cells.Length && !string.IsNullOrEmpty(cells[column]))
                    return cells[column];

                // ★ 요청한 언어가 비었으면 <b>영어 → 한국어</b> 순으로 내려간다.
                //   영어를 한국어보다 먼저 보는 이유 — 새 언어가 덜 채워졌을 때
                //   그 말을 쓰는 사람에게 한국어보다 영어가 읽을 확률이 높다.
                int en = (int)GameLanguage.English;
                if (column != en && en < cells.Length && !string.IsNullOrEmpty(cells[en]))
                    return cells[en];

                if (cells.Length > 0 && !string.IsNullOrEmpty(cells[0]))
                    return cells[0];
            }

            if (!string.IsNullOrWhiteSpace(fallback)) return fallback;
            return key;   // 키를 그대로 보여준다 — 뭘 채워야 하는지 화면에서 바로 보이게
        }

        /// <summary>
        /// 키의 문구에 값을 끼워 넣는다. 테이블 문구는 <c>{value_01}</c> 같은 자리표를 쓰므로
        /// <see cref="string.Format"/> 의 <c>{0}</c> 형식과 다르다 —
        /// 이름 있는 자리표를 바꿀 때는 <see cref="Replace"/> 를 쓸 것.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            string text = Get(key);
            if (args == null || args.Length == 0) return text;

            try { return string.Format(text, args); }
            catch (System.FormatException) { return text; }   // 자리표가 안 맞아도 죽지 않는다
        }

        /// <summary>
        /// <c>{value_01}</c> 처럼 <b>이름 있는 자리표</b>를 바꾼다 — 테이블 문구의 규약이다
        /// (<c>PassiveSkillSO.EffectText</c> 가 쓰는 방식과 같다).
        /// </summary>
        public static string Replace(string key, params (string placeholder, object value)[] pairs)
        {
            string text = Get(key);
            if (pairs == null) return text;

            for (int i = 0; i < pairs.Length; i++)
            {
                if (string.IsNullOrEmpty(pairs[i].placeholder)) continue;
                text = text.Replace("{" + pairs[i].placeholder + "}",
                                    pairs[i].value != null ? pairs[i].value.ToString() : string.Empty);
            }
            return text;
        }

        /// <summary>표를 다시 읽는다 (엑셀을 다시 내보낸 뒤 에디터에서 확인할 때).</summary>
        public static void Reload()
        {
            _rows = null;
            EnsureLoaded();
            OnLanguageChanged?.Invoke();
        }

        // ------------------------------------------------------------------

        static void EnsureLoaded()
        {
            if (_rows != null) return;
            _rows = new Dictionary<string, string[]>(256);

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                // 경고 한 번만 — Get 이 프레임마다 불리므로 여기서 로그를 반복하면 콘솔이 잠긴다.
                Debug.LogWarning($"[String] Resources/{ResourcePath} 를 찾지 못했습니다. " +
                                 "python Tools/gen_string_table.py 를 돌려 내보내세요. " +
                                 "그때까지는 각 에셋의 기존 리터럴로 표시됩니다.");
                return;
            }

            Parse(asset.text);
        }

        /// <summary>
        /// TSV 파싱. <c>#</c> 로 시작하는 줄과 헤더 줄(<c>string_key</c>)은 건너뛴다.
        /// 내보내기가 줄바꿈을 <c>\n</c> 리터럴로 접어 넣으므로 여기서 되돌린다
        /// (한 키가 여러 줄을 차지하면 TSV 가 아니게 된다).
        /// </summary>
        static void Parse(string text)
        {
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;

                string[] cells = line.Split('\t');
                string key = cells[0].Trim();
                if (key.Length == 0 || key == "string_key") continue;

                // ★ 언어 칸을 <b>전부</b> 읽는다. 표에 아직 칸이 없는 언어는 빈 문자열이 되고,
                //   그러면 아래 폴백 사슬이 «한국어» 로 내려간다 — 화면이 비지 않는다.
                //   ⚠ 배열 길이를 COUNT 로 <b>고정</b>한다. 줄마다 길이가 다르면
                //     Get 이 매번 길이 검사를 해야 하고, 한 줄이 짧을 때 조용히 폴백해서
                //     «어떤 키만 번역이 안 되는» 것처럼 보인다.
                var cell = new string[(int)GameLanguage.COUNT];
                for (int c = 0; c < cell.Length; c++)
                    cell[c] = cells.Length > c + 1 ? Unfold(cells[c + 1]) : string.Empty;

                // 같은 키가 두 번 나오면 먼저 나온 것을 남긴다 — 엑셀에서 실수로 중복시켰을 때
                // 조용히 뒤 값으로 바뀌면 원인을 찾기 어렵다.
                if (_rows.ContainsKey(key))
                {
                    Debug.LogWarning($"[String] 키가 중복됐습니다: '{key}' — 먼저 나온 값을 씁니다.");
                    continue;
                }
                _rows[key] = cell;
            }
        }

        static string Unfold(string cell) => cell.Replace("\\n", "\n");

        /// <summary>
        /// 도메인 리로드가 꺼져 있어도 플레이할 때마다 다시 읽게 한다
        /// (이 프로젝트의 static 초기화 규칙 — <c>SquadService.ResetStatics</c> 와 같다).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _rows = null;
            OnLanguageChanged = null;
            _language = GameLanguage.Korean;
        }
    }
}
