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
    /// <b>규약</b> (표 Info 시트 «장착 규약»)
    /// <code>
    ///   · 캐릭터 한 명당 유물 하나  (유저 지시 9번)
    ///   · 같은 유물을 두 명이 나눠 낄 수 없다 — 보유 수량만큼만 동시에 장착된다
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

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        /// <summary>유물 ID → 보유 개수. 같은 유물을 여러 개 가질 수 있다.</summary>
        readonly Dictionary<int, int> _owned = new Dictionary<int, int>();

        /// <summary>캐릭터 정의 ID → 장착한 유물 ID. 없으면 항목 자체가 없다.</summary>
        readonly Dictionary<int, int> _equipped = new Dictionary<int, int>();

        /// <summary>보유 목록이나 장착이 바뀔 때. UI 가 이걸 듣고 다시 그린다.</summary>
        public event System.Action OnChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

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
            foreach (var kv in _equipped) if (kv.Value == relicId) used++;
            return OwnedCount(relicId) - used;
        }

        /// <summary>유물을 하나 얻는다. 획득 로그는 부르는 쪽이 남긴다(문맥이 다르므로).</summary>
        public void Grant(RelicDefinitionSO relic)
        {
            if (relic == null || relic.relicId <= 0) return;
            _owned.TryGetValue(relic.relicId, out int n);
            _owned[relic.relicId] = n + 1;
            if (logChanges)
                Debug.Log($"[유물] 획득 — {relic.DisplayName} ({RelicDefinitionSO.NameOf(relic.grade)}) x{n + 1}");
            OnChanged?.Invoke();
        }

        // ==================================================================
        // 장착
        // ==================================================================

        /// <summary>이 캐릭터가 낀 유물. 없으면 null.</summary>
        public RelicDefinitionSO EquippedOn(CharacterUnit unit)
        {
            int key = KeyOf(unit);
            if (key <= 0) return null;
            return _equipped.TryGetValue(key, out int id) ? RelicRegistry.ById(id) : null;
        }

        /// <summary>이 유물을 낀 캐릭터가 있으면 그 정의 ID. 없으면 0.</summary>
        public int WearerOf(int relicId)
        {
            foreach (var kv in _equipped) if (kv.Value == relicId) return kv.Key;
            return 0;
        }

        /// <summary>
        /// 장착. 이미 다른 유물을 끼고 있으면 <b>먼저 벗는다</b>(한 명당 하나).
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
            if (_equipped.TryGetValue(key, out int now) && now == relic.relicId) return true;

            // ⚠ <b>먼저 벗고</b> 나서 남은 개수를 센다 — 바꿔 끼는 경우에 «자기 것» 이
            //   점유로 잡혀 «수량 부족» 이 되는 것을 막는다.
            Unequip(unit);

            if (FreeCount(relic.relicId) <= 0)
            {
                reason = "남은 수량이 없습니다(다른 캐릭터가 끼고 있습니다).";
                return false;
            }

            _equipped[key] = relic.relicId;
            RelicEffectService.OnEquipped(unit, relic);
            if (logChanges) Debug.Log($"[유물] {unit.DisplayName} ← {relic.DisplayName}");
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>해제. 끼고 있지 않으면 아무 일도 하지 않는다.</summary>
        public void Unequip(CharacterUnit unit)
        {
            int key = KeyOf(unit);
            if (key <= 0 || !_equipped.TryGetValue(key, out int id)) return;

            _equipped.Remove(key);
            RelicEffectService.OnUnequipped(unit, RelicRegistry.ById(id));
            OnChanged?.Invoke();
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

        /// <summary>세이브용 — 장착 목록. (캐릭터 정의 ID, 유물 ID) 쌍.</summary>
        public List<Vector2Int> CaptureEquipped()
        {
            var list = new List<Vector2Int>(_equipped.Count);
            foreach (var kv in _equipped) list.Add(new Vector2Int(kv.Key, kv.Value));
            return list;
        }

        /// <summary>
        /// 이어하기 — 값을 되살린다.
        /// ⚠ <b>효과는 여기서 걸지 않는다</b> — 이 시점에는 캐릭터 인스턴스가 아직
        ///   다 생성되지 않았을 수 있다. <see cref="ReapplyAll"/> 을 «캐릭터를 다 세운 뒤»
        ///   부르는 것이 규칙이다.
        /// </summary>
        public void Restore(IEnumerable<Vector2Int> owned, IEnumerable<Vector2Int> equipped)
        {
            _owned.Clear();
            _equipped.Clear();
            if (owned != null)
                foreach (var p in owned) if (p.x > 0 && p.y > 0) _owned[p.x] = p.y;
            if (equipped != null)
                foreach (var p in equipped) if (p.x > 0 && p.y > 0) _equipped[p.x] = p.y;
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
                RelicDefinitionSO r = EquippedOn(c);
                if (r != null) RelicEffectService.OnEquipped(c, r);
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
