using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Relics
{
    /// <summary>
    /// <b>보유한 유물</b>과 <b>누가 무엇을 끼고 있는가</b>를 들고 있는 곳.
    ///
    /// <b>왜 서비스 하나로 모으나</b> — 유물은 «캐릭터의 것» 처럼 보이지만 실제로는
    /// <b>성역 전체의 소유물</b>이다(캐릭터가 죽어도 남고, 다른 캐릭터에게 옮겨 낀다).
    /// 캐릭터 컴포넌트에 들고 있으면 «죽으면 사라지는» 문제와 «누가 몇 개 가졌는지 세기»
    /// 문제가 동시에 생긴다. 그래서 <see cref="LastSanctuary.Buildings.BuildService"/> 처럼
    /// <c>GameSystems</c> 아래 서비스 하나로 둔다.
    ///
    /// ══════════════════════════════════════════════════════════════════════
    ///  ★★★ <b>칸이 셋이다</b> (2026-08-26 · 유저 지시: *"유물 장착 인벤토리 3칸으로 변경"*)
    /// ══════════════════════════════════════════════════════════════════════
    /// 예전에는 «캐릭터 정의 ID → 유물 ID» 한 칸이었다. 이제 <b>칸 배열</b>이다
    /// (<see cref="equipSlots"/> · 기본 3). 빈 칸은 <c>0</c> 이다.
    ///
    /// ★ <b>«칸 번호» 를 열쇠에 섞지 않았다.</b> 사전의 <b>값</b>을 배열로 바꿨을 뿐이라
    ///   위의 «캐릭터 정의 ID 가 열쇠» 라는 규칙(아래 ⚠)이 그대로 유지된다.
    /// ★ <b>세이브 형식은 안 바뀐다</b> — <see cref="CaptureEquipped"/> 가 «한 칸 = 한 쌍»
    ///   으로 내보내므로 (캐릭터, 유물) 쌍이 캐릭터마다 여러 개 나올 뿐이다.
    ///   <b>옛 세이브(캐릭터당 한 쌍)는 첫 칸으로 들어온다</b>.
    /// ⚠ 칸 수를 <b>줄이면</b> 넘치는 칸은 «벗은 것» 이 된다(<see cref="Restore"/> 가 버린다).
    ///   늘리는 것은 언제나 안전하다.
    ///
    /// <b>규약</b> (표 Info 시트 «장착 규약»)
    /// <code>
    ///   · 캐릭터 한 명당 유물 <b>세 개</b>  (2026-08-26 · 그전에는 하나였다)
    ///   · 같은 유물을 두 명이 나눠 낄 수 없다 — 보유 수량만큼만 동시에 장착된다
    ///   · 같은 유물을 한 명이 <b>두 칸에</b> 낄 수도 없다 (수량이 1 이므로 자연히 막힌다)
    ///   · 소환수(골렘)에게는 장착할 수 없다
    ///   · 캐릭터가 죽어도 유물은 보관함으로 돌아온다
    /// </code>
    ///
    /// ⚠ <b>«누가 끼고 있는가» 의 열쇠는 캐릭터 정의 ID</b>다(<see cref="CharacterUnit"/> 인스턴스가
    ///   아니라). 캐릭터는 죽고 다시 생성될 수 있고, 세이브를 거치면 인스턴스가 통째로 바뀐다 —
    ///   정의 ID 라야 «같은 인물» 이 이어진다. 이 프로젝트의 다른 «인물 단위» 기록
    ///   (<c>CharacterDefinitionRegistry</c> 의 등장 기록)도 같은 열쇠를 쓴다.
    /// </summary>
    public class RelicInventory : MonoBehaviour
    {
        public static RelicInventory Instance { get; private set; }

        [Header("장착")]
        [Tooltip("★ 캐릭터 한 명이 낄 수 있는 유물 칸 수. 2026-08-26 에 1 → 3 이 됐다. " +
                 "★ 칸을 늘리면 유물 효과가 그만큼 겹쳐서 붙는다(유저 확정: 수치는 그대로 두고 3배)")]
        [Min(1)] [SerializeField] int equipSlots = 3;

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        /// <summary>유물 ID → 보유 개수. 같은 유물을 여러 개 가질 수 있다.</summary>
        readonly Dictionary<int, int> _owned = new Dictionary<int, int>();

        /// <summary>
        /// 캐릭터 정의 ID → <b>칸 배열</b>(길이 <see cref="equipSlots"/>). 빈 칸은 0.
        /// 항목 자체가 없으면 «아무것도 안 낀 캐릭터» 다.
        /// </summary>
        readonly Dictionary<int, int[]> _equipped = new Dictionary<int, int[]>();

        /// <summary>보유 목록이나 장착이 바뀔 때. UI 가 이걸 듣고 다시 그린다.</summary>
        public event System.Action OnChanged;

        /// <summary>
        /// ★ 유물을 <b>새로</b> 얻었다 (2026-08-26 신설). 중복이라 안 준 경우에는 오지 않는다.
        ///
        /// <b>왜 필요했나</b> — 중대 사건 배너(<see cref="UI.HudNoticeBanner"/>)가
        /// «유물 획득» 을 알려면 <b>주는 통로 넷</b>(발굴·드랍·보스 고유·사건 보상)을 전부
        /// 알아야 한다. <see cref="Grant"/> 가 이미 그 넷이 모이는 «들어오는 문» 이므로
        /// 여기에 이벤트를 하나 열어 두면 <b>배너가 통로를 하나도 몰라도 된다</b>.
        ///
        /// ⚠ <c>static</c> 이라 도메인 리로드를 꺼두면 구독이 남는다 —
        ///   <see cref="ResetStatics"/> 에서 비운다(이 프로젝트의 정적 이벤트 관례).
        /// </summary>
        public static event System.Action<RelicDefinitionSO> OnGranted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OnGranted = null;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>캐릭터 한 명의 유물 칸 수. UI 가 칸을 그릴 때 읽는다.</summary>
        public int EquipSlots => Mathf.Max(1, equipSlots);

        // ==================================================================
        // 보유
        // ==================================================================

        /// <summary>보유한 유물 종류와 개수. UI 가 목록을 그릴 때 쓴다.</summary>
        public IEnumerable<KeyValuePair<int, int>> Owned => _owned;

        public int OwnedCount(int relicId) =>
            _owned.TryGetValue(relicId, out int n) ? n : 0;

        /// <summary>보유한 <b>종류</b> 수(개수 합이 아니다).</summary>
        public int OwnedKinds => _owned.Count;

        /// <summary>이 유물을 지금 <b>새로 장착할 수 있는</b> 개수 — 보유 − 이미 낀 것.</summary>
        public int FreeCount(int relicId)
        {
            int used = 0;
            foreach (var kv in _equipped)
            {
                int[] slots = kv.Value;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] == relicId) used++;
            }
            return OwnedCount(relicId) - used;
        }

        /// <summary>
        /// 유물을 하나 얻는다. 획득 로그는 부르는 쪽이 남긴다(문맥이 다르므로).
        ///
        /// ★★★ <b>같은 유물은 두 번 주지 않는다</b> (2026-08-25 · 유저 지시:
        /// *"유물 중복 획득 안되게 수정해줘"*).
        ///
        /// ★ <b>추첨에서 이미 걸렀는데 왜 여기서 또 보는가</b> — 주는 통로가 <b>넷</b>이기
        ///   때문이다: 발굴 · 일반 처치 드랍 · <b>보스 고유 드랍</b> · <b>사건 보상</b>.
        ///   뒤의 둘은 추첨을 거치지 않고 <b>정해진 유물을 곧바로</b> 준다. 통로마다 검사를
        ///   흩어 놓으면 다섯 번째 통로가 생기는 날 반드시 빠뜨린다 —
        ///   <b>«들어오는 문» 한 곳에서 막는다</b>(HudExclusive 가 창 배타에서 택한 것과 같은 결론).
        /// ⚠ 그래도 추첨 쪽 <see cref="RelicRegistry.RollGrade"/> 의 거르기는 <b>남긴다</b>.
        ///   거기서 안 거르면 «뽑았는데 거절당함» 이 되어 <b>체감 확률이 조용히 떨어진다</b>.
        /// </summary>
        /// <returns>실제로 새로 얻었으면 <c>true</c>. 이미 가지고 있었으면 <c>false</c>.</returns>
        public bool Grant(RelicDefinitionSO relic)
        {
            if (relic == null || relic.relicId <= 0) return false;

            if (_owned.TryGetValue(relic.relicId, out int n) && n > 0)
            {
                if (logChanges)
                    Debug.Log($"[유물] 중복 — {relic.DisplayName} 은(는) 이미 가지고 있어 주지 않았습니다");
                return false;
            }

            _owned[relic.relicId] = 1;
            if (logChanges)
                Debug.Log($"[유물] 획득 — {relic.DisplayName} ({RelicDefinitionSO.NameOf(relic.grade)})");
            OnChanged?.Invoke();
            OnGranted?.Invoke(relic);      // ★ 위 OnGranted 주석 — «들어오는 문» 은 여기 하나다
            return true;
        }

        /// <summary>이 유물을 이미 가지고 있는가 (2026-08-25 · 중복 금지).</summary>
        public bool Owns(int relicId) => OwnedCount(relicId) > 0;

        // ==================================================================
        // 장착
        // ==================================================================

        /// <summary>
        /// 이 캐릭터의 칸 배열. 없으면 <c>null</c> — <b>만들지 않는다</b>
        /// (읽기만 하는 쪽이 빈 배열을 사전에 심어 두면 «아무것도 안 낀 캐릭터» 가 장부에 쌓인다).
        /// </summary>
        int[] SlotsOf(int key) => key > 0 && _equipped.TryGetValue(key, out int[] s) ? s : null;

        /// <summary>이 캐릭터의 칸 배열을 <b>없으면 만들어서</b> 돌려준다.</summary>
        int[] EnsureSlots(int key)
        {
            if (_equipped.TryGetValue(key, out int[] s))
            {
                // 인스펙터에서 칸 수를 늘렸을 수 있다 — 그때 배열을 키운다(값은 보존).
                if (s.Length >= EquipSlots) return s;
                var grown = new int[EquipSlots];
                System.Array.Copy(s, grown, s.Length);
                _equipped[key] = grown;
                return grown;
            }

            var made = new int[EquipSlots];
            _equipped[key] = made;
            return made;
        }

        /// <summary>
        /// 이 캐릭터가 낀 <b>첫 유물</b>. 없으면 null.
        /// ⚠ 칸이 셋이 된 뒤로 이것은 «대표 하나» 일 뿐이다 — 전부가 필요하면
        ///   <see cref="CollectEquipped"/> 를 쓸 것.
        /// </summary>
        public RelicDefinitionSO EquippedOn(CharacterUnit unit)
        {
            int[] slots = SlotsOf(KeyOf(unit));
            if (slots == null) return null;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] > 0) return RelicRegistry.ById(slots[i]);
            return null;
        }

        /// <summary>이 캐릭터의 <paramref name="slot"/> 번 칸. 비었거나 칸 밖이면 null.</summary>
        public RelicDefinitionSO EquippedOn(CharacterUnit unit, int slot)
        {
            int[] slots = SlotsOf(KeyOf(unit));
            if (slots == null || slot < 0 || slot >= slots.Length) return null;
            return slots[slot] > 0 ? RelicRegistry.ById(slots[slot]) : null;
        }

        /// <summary>
        /// 이 캐릭터가 낀 유물을 <b>칸 순서대로</b> 모은다(빈 칸은 건너뛴다).
        /// ★ 목록을 <b>새로 만들지 않는다</b> — 로스터·초상화가 0.2초마다 부르므로
        ///   호출부가 준 목록을 재사용해야 GC 가 안 돈다(이 프로젝트의 UI 관례).
        /// </summary>
        public void CollectEquipped(CharacterUnit unit, List<RelicDefinitionSO> into)
        {
            if (into == null) return;
            into.Clear();

            int[] slots = SlotsOf(KeyOf(unit));
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] <= 0) continue;
                RelicDefinitionSO r = RelicRegistry.ById(slots[i]);
                if (r != null) into.Add(r);
            }
        }

        /// <summary>이 캐릭터가 <b>지금 이 유물을</b> 끼고 있는가.</summary>
        public bool IsEquippedOn(CharacterUnit unit, RelicDefinitionSO relic)
        {
            if (relic == null) return false;
            int[] slots = SlotsOf(KeyOf(unit));
            if (slots == null) return false;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == relic.relicId) return true;
            return false;
        }

        /// <summary>이 캐릭터가 지금 쓰고 있는 칸 수 / 전체 칸 수 — UI 가 «2/3» 으로 그린다.</summary>
        public int UsedSlots(CharacterUnit unit)
        {
            int[] slots = SlotsOf(KeyOf(unit));
            if (slots == null) return 0;
            int n = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i] > 0) n++;
            return n;
        }

        /// <summary>이 유물을 낀 캐릭터가 있으면 그 정의 ID. 없으면 0.</summary>
        public int WearerOf(int relicId)
        {
            foreach (var kv in _equipped)
            {
                int[] slots = kv.Value;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] == relicId) return kv.Key;
            }
            return 0;
        }

        /// <summary>
        /// ★★ 이 유물을 낀 <b>캐릭터 전부</b>의 정의 ID (2026-08-26 · 유저 지시:
        /// *"유물 목록 버튼 오른쪽 끝에 장착하고 있는 캐릭터 나오게"*).
        ///
        /// <b>왜 <see cref="WearerOf"/> 로는 모자라나</b> — 같은 유물을 <b>둘 이상 가질 수</b> 있고
        /// (발굴이 같은 것을 또 준다) 그러면 <b>두 캐릭터가 같이 낀다</b>. 그 경우
        /// <c>WearerOf</c> 는 사전 순서상 <b>먼저 걸린 하나</b>만 돌려주므로, 목록 칸에 그것만
        /// 적으면 «분명히 내가 다른 애한테도 끼웠는데» 가 된다.
        ///
        /// ⚠ <b>같은 캐릭터가 두 칸에 같은 유물을 낀 경우는 한 번만 담는다</b> — 칸이 셋이라
        ///   막혀 있지 않다(<see cref="TryEquip"/> 는 «이미 꼈으면 true» 로 돌아 나가므로 실제로는
        ///   생기지 않지만, 저장을 되살릴 때(<see cref="Restore"/>) 들어올 수 있다).
        /// </summary>
        /// <param name="relicId">유물 정의 ID.</param>
        /// <param name="into">여기에 <b>덧붙인다</b>(먼저 비우지 않는다 — 부르는 쪽이 정한다).</param>
        public void CollectWearers(int relicId, List<int> into)
        {
            if (into == null || relicId <= 0) return;
            foreach (var kv in _equipped)
            {
                int[] slots = kv.Value;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != relicId) continue;
                    if (!into.Contains(kv.Key)) into.Add(kv.Key);
                    break;
                }
            }
        }

        /// <summary>
        /// 장착. <b>빈 칸에</b> 꽂는다 — 칸이 셋이므로 «먼저 벗고 낀다» 가 아니다
        /// (그것이 칸 하나 시절의 규칙이었다). 빈 칸이 없으면 이유를 돌려준다.
        /// 못 끼면 <c>false</c> 와 이유를 돌려준다.
        /// </summary>
        public bool TryEquip(CharacterUnit unit, RelicDefinitionSO relic, out string reason)
        {
            reason = "";
            if (unit == null || relic == null) { reason = "대상이 없습니다."; return false; }
            if (unit.IsSummoned) { reason = "소환수에게는 장착할 수 없습니다."; return false; }

            int key = KeyOf(unit);
            if (key <= 0) { reason = "이 캐릭터에는 정의가 없습니다."; return false; }

            // 이미 그걸 끼고 있으면 아무 일도 하지 않는다(눌러도 값이 안 흔들리게).
            if (IsEquippedOn(unit, relic)) return true;

            // ⚠ <b>수량 검사가 먼저다</b> — 칸이 셋이 된 뒤로는 «먼저 벗고 센다» 가 필요 없다.
            //   자기 것이 점유로 잡히는 문제는 바로 위 «이미 끼고 있으면 그냥 성공» 이 막는다.
            if (FreeCount(relic.relicId) <= 0)
            {
                reason = "남은 수량이 없습니다(다른 캐릭터가 끼고 있습니다).";
                return false;
            }

            int[] slots = EnsureSlots(key);
            int free = -1;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] <= 0) { free = i; break; }

            if (free < 0)
            {
                reason = $"유물 칸이 가득 찼습니다({slots.Length}칸). 먼저 하나를 벗기세요.";
                return false;
            }

            slots[free] = relic.relicId;
            RelicEffectService.OnEquipped(unit, relic);
            if (logChanges)
                Debug.Log($"[유물] {unit.DisplayName} ← {relic.DisplayName} ({free + 1}번 칸)");
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// <b>그 유물 하나만</b> 벗는다. 끼고 있지 않으면 아무 일도 하지 않는다.
        /// ★ 칸이 셋이 된 뒤로는 «무엇을 벗을지» 를 반드시 말해야 한다 —
        ///   그래서 인자 없는 <see cref="UnequipAll"/> 과 이름을 갈랐다.
        /// </summary>
        public void Unequip(CharacterUnit unit, RelicDefinitionSO relic)
        {
            if (relic == null) return;
            int key = KeyOf(unit);
            int[] slots = SlotsOf(key);
            if (slots == null) return;

            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != relic.relicId) continue;
                slots[i] = 0;
                changed = true;
            }
            if (!changed) return;

            RelicEffectService.OnUnequipped(unit, relic);
            OnChanged?.Invoke();
        }

        /// <summary>이 캐릭터의 <paramref name="slot"/> 번 칸을 비운다.</summary>
        public void UnequipSlot(CharacterUnit unit, int slot)
        {
            int[] slots = SlotsOf(KeyOf(unit));
            if (slots == null || slot < 0 || slot >= slots.Length || slots[slot] <= 0) return;

            RelicDefinitionSO relic = RelicRegistry.ById(slots[slot]);
            slots[slot] = 0;
            RelicEffectService.OnUnequipped(unit, relic);
            OnChanged?.Invoke();
        }

        /// <summary>이 캐릭터의 칸을 <b>전부</b> 비운다.</summary>
        public void UnequipAll(CharacterUnit unit)
        {
            int[] slots = SlotsOf(KeyOf(unit));
            if (slots == null) return;

            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] <= 0) continue;
                RelicDefinitionSO relic = RelicRegistry.ById(slots[i]);
                slots[i] = 0;
                RelicEffectService.OnUnequipped(unit, relic);
                changed = true;
            }
            if (changed) OnChanged?.Invoke();
        }

        // ==================================================================
        // 판 갈아엎기 · 세이브
        // ==================================================================

        /// <summary>
        /// <b>새 판</b>. 보유·장착을 전부 비운다.
        /// ⚠ 이어하기는 이 문을 지나지 않는다 — <see cref="Restore"/> 가 값을 되살린다
        ///   (<see cref="LastSanctuary.Save.RunResetService"/> 의 규칙과 같다).
        /// </summary>
        public void ResetRun()
        {
            _owned.Clear();
            _equipped.Clear();
            RelicEffectService.ClearAll();
            OnChanged?.Invoke();
        }

        /// <summary>세이브용 — 보유 목록. (유물 ID, 개수) 쌍.</summary>
        public List<Vector2Int> CaptureOwned()
        {
            var list = new List<Vector2Int>(_owned.Count);
            foreach (var kv in _owned) list.Add(new Vector2Int(kv.Key, kv.Value));
            return list;
        }

        /// <summary>
        /// 세이브용 — 장착 목록. <b>(캐릭터 정의 ID, 유물 ID) 쌍이 «낀 칸마다» 하나씩</b>.
        ///
        /// ★ 칸이 셋이 되어도 <b>형식이 안 바뀌는</b> 이유가 이것이다 — 한 캐릭터가 쌍 여러 개로
        ///   나올 뿐이고 <see cref="Save.SaveData"/> 는 손댈 것이 없다. 옛 세이브(캐릭터당 한 쌍)는
        ///   자연히 첫 칸으로 들어온다.
        /// ⚠ <b>칸 순서를 지킨다</b> — 되돌릴 때 같은 자리에 꽂히도록.
        /// </summary>
        public List<Vector2Int> CaptureEquipped()
        {
            var list = new List<Vector2Int>(_equipped.Count * EquipSlots);
            foreach (var kv in _equipped)
            {
                int[] slots = kv.Value;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] > 0) list.Add(new Vector2Int(kv.Key, slots[i]));
            }
            return list;
        }

        /// <summary>
        /// 이어하기 — 값을 되살린다.
        /// ⚠ <b>효과는 여기서 걸지 않는다</b> — 이 시점에는 캐릭터 인스턴스가 아직
        ///   다 생성되지 않았을 수 있다. <see cref="ReapplyAll"/> 을 «캐릭터를 다 세운 뒤»
        ///   부르는 것이 규칙이다.
        /// ⚠ 칸보다 많은 쌍이 들어오면(칸 수를 줄인 뒤 옛 세이브를 열면) <b>넘치는 것은 버린다</b> —
        ///   장부에 «칸 밖의 유물» 을 남기면 벗을 방법이 없다.
        /// </summary>
        public void Restore(IEnumerable<Vector2Int> owned, IEnumerable<Vector2Int> equipped)
        {
            _owned.Clear();
            _equipped.Clear();

            // ★★★ <b>되살릴 때도 «한 종류에 하나» 를 지킨다</b> (2026-08-26 · 유저 지시:
            //   *"유물 중복 획득 안되고 중복 장착도 안 되게 해"*).
            //
            //   들어오는 문(<see cref="Grant"/>)과 장착(<see cref="TryEquip"/>)은 이미 막고 있었다.
            //   남은 구멍이 <b>이 함수</b>다 — 저장은 «그때의 장부» 라서, 중복 금지가 없던
            //   판(2026-08-25 이전)의 저장이나 손으로 고친 저장이 들어오면 <b>수량 2</b> 나
            //   <b>두 캐릭터가 같은 유물</b>이 그대로 되살아난다. 규칙은 «장부에 들어오는 모든
            //   길» 에서 같아야 하므로 여기서도 자른다.
            if (owned != null)
                foreach (var p in owned)
                    if (p.x > 0 && p.y > 0) _owned[p.x] = 1;      // ⚠ 수량은 <b>항상 1</b>

            if (equipped != null)
            {
                // 이미 누군가에게 꽂은 유물 — 두 번째부터는 버린다(장부에 하나뿐이므로).
                var placed = new HashSet<int>();
                foreach (var p in equipped)
                {
                    if (p.x <= 0 || p.y <= 0) continue;
                    if (!placed.Add(p.y))
                    {
                        Debug.LogWarning($"[유물] 저장에 중복 장착이 있었습니다 — 유물 {p.y} 를 " +
                                         $"캐릭터 {p.x} 에서 뺐습니다(한 유물은 한 명만 낄 수 있습니다)");
                        continue;
                    }
                    int[] slots = EnsureSlots(p.x);
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] != 0) continue;
                        slots[i] = p.y;
                        break;
                    }
                }
            }
            OnChanged?.Invoke();
        }

        /// <summary>지금 살아 있는 캐릭터들에게 «끼고 있는 것» 의 효과를 다시 건다.</summary>
        public void ReapplyAll()
        {
            RelicEffectService.ClearAll();
            var all = Combat.UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CharacterUnit c) || !c.IsAlive) continue;

                int[] slots = SlotsOf(KeyOf(c));
                if (slots == null) continue;

                for (int s = 0; s < slots.Length; s++)
                {
                    if (slots[s] <= 0) continue;
                    RelicDefinitionSO r = RelicRegistry.ById(slots[s]);
                    if (r != null) RelicEffectService.OnEquipped(c, r);
                }
            }
        }

        /// <summary>
        /// «누구» 의 열쇠 — <b>캐릭터 정의 ID</b>. 정의가 없으면 0(장착 불가).
        /// 맨 위 ⚠ 참조.
        /// </summary>
        public static int KeyOf(CharacterUnit unit) =>
            unit != null && unit.Definition != null ? unit.Definition.characterId : 0;
    }
}
