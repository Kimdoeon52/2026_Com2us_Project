using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 인벤토리 데이터 보유. 씬 전환에도 유지되며 UI는 Instance로 Grid/Save를 읽어간다
public class InventoryHost : PersistentSingleton<InventoryHost>, IEquipmentSource
{
    // 첫 씬에 없을 때 자동 생성할 Resources 경로
    private const string ResourcePath = "InventoryHost";

    [Tooltip("그리드 규격")]
    [SerializeField] private InventoryDefinition _inventoryDefinition;

    [Tooltip("파츠 목록")]
    [SerializeField] private PartsCatalog _catalog;

    [Tooltip("부품 목록")]
    [SerializeField] private ComponentCatalog _componentCatalog;

    [Tooltip("코어 목록")]
    [SerializeField] private CoreCatalog _coreCatalog;

    [Tooltip("난수 시드")]
    [SerializeField] private int _seed = 1;

    [Tooltip("시작 시 자동 배치할 파츠 수")]
    [Min(0)]
    [SerializeField] private int _startingParts;

    private Rng _rng;
    private InventoryGrid _grid;
    private InventorySaveData _save;
    private string _snapshot;
    private bool _saveDirty;

    private static string SavePath => Path.Combine(Application.persistentDataPath, "inventory.json");

    public InventoryDefinition Definition => _inventoryDefinition;
    public InventoryGrid Grid => _grid;
    public InventorySaveData Save => _save;
    public PartsCatalog Catalog => _catalog;
    public ComponentCatalog ComponentCatalog => _componentCatalog;
    public CoreCatalog CoreCatalog => _coreCatalog;

    // 장착 코어. 코어 칸은 비지 않는다
    public CoreDefinition Core => _coreCatalog != null ? _coreCatalog.Find(_save.Core.Id) : null;
    public int CoreLevel => _save.Core.Level;

    // 불안정한 코어는 복구 전까지 출전할 수 없다
    public bool CanSortie => !_save.Core.IsUnstable;

    // 데이터가 바뀌면 UI가 전체 리빌드한다
    public event Action Changed;

    // 첫 씬에 배치된 게 없으면 Resources 프리팹으로 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        InventoryHost prefab = Resources.Load<InventoryHost>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"Resources/{ResourcePath} 프리팹이 없다.");
            return;
        }

        Instantiate(prefab).name = prefab.name;
    }

    protected override void Awake()
    {
        // DontDestroyOnLoad는 루트 오브젝트에만 걸린다
        if (Instance == null && transform.parent != null)
            transform.SetParent(null);

        base.Awake();

        // 중복이면 파괴 예정이라 초기화하지 않는다
        if (Instance != this)
            return;

        _rng = new Rng(_seed);

        if (!LoadFromFile())
        {
            _save = new InventorySaveData();
            _grid = new InventoryGrid(_inventoryDefinition, _save.Components, _componentCatalog);
            EnsureCore();
            EquipStarting();

            for (int i = 0; i < _startingParts; i++)
            {
                GiveRandomPart();
            }
        }

        Changed += MarkSaveDirty;
        gameObject.GetOrAddComponent<SkillLevelSystem>();
    }

    private void Start()
    {
        if (Instance != this)
            return;

        BattleSettlementProcessor.OnSettlementCompleted += ApplySettlement;

        // 첫 씬에서 자동 생성되면 전투 데이터 허브의 Awake보다 늦는다
        if (CombatDataHub.Instance != null)
            InventoryBattleBridge.ApplyTo(CombatDataHub.Instance.PlayerSnapshot);
    }

    protected override void OnDestroy()
    {
        BattleSettlementProcessor.OnSettlementCompleted -= ApplySettlement;
        base.OnDestroy();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            GiveDebugComponents();
    }

    // 한 프레임에 여러 번 바뀌어도 파일은 한 번만 쓴다
    private void LateUpdate()
    {
        if (_saveDirty)
            SaveToFile();
    }

    private void OnApplicationQuit()
    {
        if (Instance == this && _saveDirty)
            SaveToFile();
    }

    private void MarkSaveDirty() => _saveDirty = true;

    private bool LoadFromFile()
    {
        if (!File.Exists(SavePath))
            return false;

        try
        {
            InventorySaveData loaded = JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(SavePath));
            if (loaded == null)
                return false;

            _save = loaded;
            _grid = new InventoryGrid(_inventoryDefinition, _save.Components, _componentCatalog);
            _save.RestoreTo(_grid, _catalog);
            EnsureCore();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"인벤토리 세이브를 읽지 못해 새로 시작한다. {SavePath}\n{e.Message}", this);
            return false;
        }
    }

    private void SaveToFile()
    {
        _saveDirty = false;

        try
        {
            _save.CaptureFrom(_grid);
            File.WriteAllText(SavePath, JsonUtility.ToJson(_save, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"인벤토리 세이브에 실패했다. {SavePath}\n{e.Message}", this);
        }
    }

    [ContextMenu("세이브 파일 삭제")]
    private void DeleteSaveFile()
    {
        _saveDirty = false;
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    // 전투 결과를 장착 파츠 내구도, 잔해, 코어 상태에 반영한다
    private void ApplySettlement(BattleSettlementResult result)
    {
        for (int i = 0; i < EquipmentSaveData.SlotCount; i++)
        {
            string id = _save.Equipment.Get((Slot)i);
            if (id != null && result.FinalPartDurabilities.TryGetValue(id, out int durability))
                SetDurability((Slot)i, durability);
        }

        if (result.CoreUnstable)
            _save.Core.IsUnstable = true;

        RemoveDestroyed(result.DestroyedPartIDs);
        Changed?.Invoke();
    }

    public int GetDurability(Slot slot)
    {
        PartsDefinition equipped = GetEquipped(slot);
        return equipped != null ? Mathf.Max(0, equipped.Durability - _save.Equipment.GetLostDurability(slot)) : 0;
    }

    public void SetDurability(Slot slot, int durability)
    {
        PartsDefinition equipped = GetEquipped(slot);
        if (equipped != null)
            _save.Equipment.SetLostDurability(slot, equipped.Durability - Mathf.Clamp(durability, 0, equipped.Durability));

        _saveDirty = true;
    }

    // 수리. 비용과 시간은 호출하는 쪽에서 처리한다
    public void RepairSlot(Slot slot)
    {
        _save.Equipment.SetLostDurability(slot, 0);
        Changed?.Invoke();
    }

    // 잔해를 내구도 100%로 되돌린다. 해당 부위가 비었으면 장착, 아니면 그리드로. 둘 다 안 되면 false
    public bool RestoreWreck(string partId)
    {
        PartsDefinition definition = _catalog != null ? _catalog.Find(partId) : null;
        if (definition == null || !_save.Wrecks.Contains(partId))
            return false;

        if (GetEquipped(definition.Slot) == null)
            _save.Equipment.Set(definition.Slot, partId);
        else if (!_grid.TryAutoPlaceParts(definition, out int _))
            return false;

        _save.Wrecks.Remove(partId);
        Changed?.Invoke();
        return true;
    }

    public void DiscardWreck(string partId)
    {
        if (_save.Wrecks.Remove(partId))
            Changed?.Invoke();
    }

    public void RestoreCore()
    {
        _save.Core.IsUnstable = false;
        Changed?.Invoke();
    }

    // 특정 등급 부품의 총 보유 수
    public int CountComponents(PartGrade grade)
    {
        if (_componentCatalog == null || _componentCatalog.Components == null)
            return 0;

        int count = 0;
        foreach (ComponentDefinition component in _componentCatalog.Components)
        {
            if (component != null && component.Grade == grade)
                count += _save.Components.Get(component.Id);
        }
        return count;
    }

    // 특정 등급 부품을 보유한 것부터 소모한다
    public bool ConsumeComponents(PartGrade grade, int amount)
    {
        if (CountComponents(grade) < amount)
            return false;

        foreach (ComponentDefinition component in _componentCatalog.Components)
        {
            if (amount <= 0)
                break;
            if (component == null || component.Grade != grade)
                continue;

            int use = Mathf.Min(amount, _save.Components.Get(component.Id));
            if (use > 0 && ConsumeComponent(component.Id, use))
                amount -= use;
        }
        return amount <= 0;
    }

    // 부위를 비운 내구도 그대로 그리드에 놓는다
    private bool PlaceWithLost(PartsDefinition definition, Vector2Int? origin, int lost)
    {
        bool placed;
        int index;
        if (origin.HasValue)
            placed = _grid.TryPlaceParts(definition, origin.Value, out index);
        else
            placed = _grid.TryAutoPlaceParts(definition, out index);

        if (placed && _grid.TryGetEntry(index, out InventoryGrid.Entry entry) && entry.Parts != null)
            entry.Parts.LostDurability = lost;

        return placed;
    }

    [ContextMenu("테스트: 장착 파츠 손상")]
    private void DebugDamageEquipped()
    {
        for (int i = 0; i < EquipmentSaveData.SlotCount; i++)
        {
            PartsDefinition equipped = GetEquipped((Slot)i);
            if (equipped != null)
                SetDurability((Slot)i, Mathf.Max(1, equipped.Durability * (i + 1) / (EquipmentSaveData.SlotCount + 1)));
        }
        Changed?.Invoke();
    }

    [ContextMenu("테스트: 패배 (첫 장착 파츠 잔해, 코어 불안정)")]
    private void DebugLose()
    {
        _save.Core.IsUnstable = true;
        IReadOnlyList<string> ids = GetEquippedPartIds();
        RemoveDestroyed(ids.Count > 0 ? new[] { ids[0] } : new string[0]);
        Changed?.Invoke();
    }

    // 지정 파츠를 빈자리에 배치
    public bool GivePart(PartsDefinition definition)
    {
        if (_grid == null || definition == null)
            return false;

        bool placed = _grid.TryAutoPlaceParts(definition, out int _);
        if (placed)
            Changed?.Invoke();

        return placed;
    }

    // 세이브 데이터를 밖에서 고친 뒤 저장·화면 갱신을 요청한다
    public void NotifyChanged() => Changed?.Invoke();

    // 인벤토리에서 해당 파츠 1개 제거. 없으면 false. fullOnly면 내구도가 가득 찬 것만
    public bool RemovePart(PartsDefinition definition, bool fullOnly = false)
    {
        if (_grid == null || definition == null)
            return false;

        for (int i = 0; i < _grid.Entries.Count; i++)
        {
            var entry = _grid.Entries[i];
            if (entry.IsComponent || entry.Definition != definition)
                continue;

            if (entry.Parts == null || entry.Parts.IsDestroyed)   // 파괴된 파츠는 판매 대상에서 제외
                continue;

            if (fullOnly && entry.Parts.LostDurability > 0)
                continue;

            _grid.Remove(i);
            Changed?.Invoke();
            return true;
        }

        return false;
    }

    // 지정 파츠를 원하는 칸에 배치. 그 칸이 막혀있거나 범위 밖이면 실패
    public bool TryPlacePartAt(PartsDefinition definition, Vector2Int origin)
    {
        if (_grid == null || definition == null)
            return false;

        bool placed = _grid.TryPlaceParts(definition, origin, out int _);
        if (placed)
            Changed?.Invoke();

        return placed;
    }

    [ContextMenu("파츠 지급")]
    public bool GiveRandomPart()
    {
        if (_catalog == null || _catalog.Parts == null || _catalog.Parts.Length == 0)
            return false;

        return GivePart(_catalog.Parts[_rng.Range(0, _catalog.Parts.Length)]);
    }

    // 드래그 드롭으로 엔트리를 옮긴다
    public bool MoveEntry(int index, Vector2Int origin)
    {
        if (_grid == null || !_grid.TryMove(index, origin))
            return false;

        Changed?.Invoke();
        return true;
    }

    // 최대 소지 개수를 넘는 양은 버려진다. 실제 지급량 반환
    public int GiveComponent(ComponentDefinition component, int amount)
    {
        int added = _save.Components.Add(component, amount);
        _grid.SyncComponents();
        Changed?.Invoke();
        return added;
    }

    // 그리드의 파츠를 부위에 장착. 이미 끼워진 파츠는 그리드로 돌려보낸다
    public bool Equip(int index, Slot slot)
    {
        if (_grid == null || !_grid.TryGetEntry(index, out InventoryGrid.Entry entry))
            return false;

        if (entry.IsComponent || entry.Definition == null || entry.Definition.Slot != slot)
            return false;

        PartsDefinition previous = GetEquipped(slot);
        int previousLost = _save.Equipment.GetLostDurability(slot);
        int lost = entry.Parts != null ? entry.Parts.LostDurability : 0;
        _grid.Remove(index);

        // 기존 파츠를 둘 자리가 없으면 되돌린다
        if (previous != null && !PlaceWithLost(previous, entry.Origin, previousLost) && !PlaceWithLost(previous, null, previousLost))
        {
            PlaceWithLost(entry.Definition, entry.Origin, lost);
            return false;
        }

        _save.Equipment.Set(slot, entry.Definition.Id);
        _save.Equipment.SetLostDurability(slot, lost);
        Changed?.Invoke();
        return true;
    }

    // 장착 해제. 그리드에 빈자리가 없으면 실패
    public bool Unequip(Slot slot) => ReturnToGrid(slot, null);

    // 지정 칸으로 장착 해제. 막혀 있으면 실패
    public bool UnequipAt(Slot slot, Vector2Int origin) => ReturnToGrid(slot, origin);

    private bool ReturnToGrid(Slot slot, Vector2Int? origin)
    {
        PartsDefinition equipped = GetEquipped(slot);
        if (equipped == null || _grid == null)
            return false;

        if (!PlaceWithLost(equipped, origin, _save.Equipment.GetLostDurability(slot)))
            return false;

        _save.Equipment.Set(slot, null);
        Changed?.Invoke();
        return true;
    }

    // 코어가 비었거나 목록에 없는 ID면 기본 코어를 끼운다
    private void EnsureCore()
    {
        if (_coreCatalog == null || _coreCatalog.DefaultCore == null)
        {
            Debug.LogWarning("코어 목록이나 기본 코어가 비었다.", this);
            return;
        }

        if (Core == null)
            _save.Core.Set(_coreCatalog.DefaultCore.Id);
    }

    // 새 게임에서 시작 파츠를 각 부위에 바로 장착한다
    private void EquipStarting()
    {
        PartsDefinition[] starting = _catalog != null ? _catalog.StartingEquipment : null;
        if (starting == null)
            return;

        for (int i = 0; i < starting.Length; i++)
        {
            PartsDefinition part = starting[i];
            if (part == null)
                continue;

            if (_save.Equipment.Get(part.Slot) != null)
                Debug.LogWarning($"시작 파츠 {part.Id}: {part.Slot} 부위가 겹친다.", this);

            _save.Equipment.Set(part.Slot, part.Id);
        }
    }

    public PartsDefinition GetEquipped(Slot slot)
    {
        return _catalog != null ? _catalog.Find(_save.Equipment.Get(slot)) : null;
    }

    public IReadOnlyList<string> GetEquippedPartIds()
    {
        var ids = new List<string>(EquipmentSaveData.SlotCount);
        for (int i = 0; i < EquipmentSaveData.SlotCount; i++)
        {
            string id = _save.Equipment.Get((Slot)i);
            if (id != null)
                ids.Add(id);
        }

        return ids;
    }

    public void RemoveDestroyed(IEnumerable<string> partIds)
    {
        if (partIds == null)
            return;

        bool removed = false;
        foreach (string partId in partIds)
        {
            for (int i = 0; i < EquipmentSaveData.SlotCount; i++)
            {
                if (_save.Equipment.Get((Slot)i) != partId)
                    continue;

                _save.Equipment.Set((Slot)i, null);
                _save.Wrecks.Add(partId);
                removed = true;
                break;
            }
        }

        if (removed)
            Changed?.Invoke();
    }

    [ContextMenu("장착 ID 출력")]
    public void LogEquipped()
    {
        Debug.Log($"장착: [{string.Join(", ", GetEquippedPartIds())}]");
    }

    [ContextMenu("부품 지급")]
    public void GiveSampleComponents()
    {
        if (_componentCatalog == null)
            return;

        GiveRandomComponent(PartGrade.Common, 10);
        GiveRandomComponent(PartGrade.Rare, 3);
        GiveRandomComponent(PartGrade.Epic, 1);
    }

    public void GiveRandomComponent(PartGrade grade, int amount)
    {
        if (_componentCatalog != null && _componentCatalog.TryPick(grade, _rng, out ComponentDefinition component))
            GiveComponent(component, amount);
    }

    // 수량이 0이 되면 칸도 사라진다
    public bool ConsumeComponent(string id, int amount)
    {
        bool consumed = _save.Components.TryConsume(id, amount);
        if (!consumed)
            return false;

        _grid.SyncComponents();
        Changed?.Invoke();
        return true;
    }

    // 지급해도 버려지거나 칸이 없어 안 보이게 되지 않는지. 크래프팅 슬롯에서 돌려주기 전에 확인한다
    public bool CanGiveComponent(ComponentDefinition component, int amount = 1)
    {
        if (component == null || _grid == null)
            return false;

        if (component.RemainingCapacity(_save.Components.Get(component.Id)) < amount)
            return false;

        // 이미 칸이 있으면 수량만 늘어난다
        var entries = _grid.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsComponent && entries[i].Component.Id == component.Id)
                return true;
        }

        // 새 칸이 필요하면 빈칸이 하나는 있어야 한다
        for (int y = 0; y < _grid.Height; y++)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                if (_grid.GetIndexAt(new Vector2Int(x, y)) < 0)
                    return true;
            }
        }

        return false;
    }

    [ContextMenu("첫 부품 1개 소모")]
    public void ConsumeSampleComponent()
    {
        var slots = _save.Components.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Count <= 0)
                continue;

            ConsumeComponent(slots[i].Id, 1);
            return;
        }
    }

    [ContextMenu("전체 비우기")]
    public void ClearAll()
    {
        _save.Components.Clear();
        _save.Equipment.Clear();
        _save.Core.Clear();
        _save.Wrecks.Clear();
        EnsureCore();
        _grid.Clear();
        Changed?.Invoke();
    }

    // 직렬화 왕복 테스트. 저장 → 비우기 → 복원 후 배치가 같아야 한다
    [ContextMenu("세이브 캡처")]
    public void CaptureSnapshot()
    {
        _save.CaptureFrom(_grid);
        _snapshot = JsonUtility.ToJson(_save, true);
        Debug.Log(_snapshot);
    }

    [ContextMenu("세이브 복원")]
    public void RestoreSnapshot()
    {
        if (string.IsNullOrEmpty(_snapshot))
        {
            Debug.LogWarning("캡처된 세이브가 없다.");
            return;
        }

        _save = JsonUtility.FromJson<InventorySaveData>(_snapshot);
        _grid = new InventoryGrid(_inventoryDefinition, _save.Components, _componentCatalog);
        _save.RestoreTo(_grid, _catalog);
        EnsureCore();
        Changed?.Invoke();
    }

    // 현재 점유 상태를 문자 그리드로 출력
    [ContextMenu("그리드 출력")]
    public void LogGrid()
    {
        var text = new System.Text.StringBuilder();
        for (int y = _grid.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                int index = _grid.GetIndexAt(new Vector2Int(x, y));
                text.Append(index < 0 ? ". " : $"{index % 10} ");
            }

            text.AppendLine();
        }

        Debug.Log(text.ToString());
    }

    ///////////////////////////
    /// 크래프팅 디버그용
    ///////////////////////////
    [Header("디버그 지급")]
    [Tooltip("지급할 부품 ID 목록")]
    [SerializeField] private string[] _debugComponentIds;

    [Tooltip("각 부품당 지급 수량")]
    [SerializeField] private int _debugAmount = 1;

    [ContextMenu("지정 부품 지급")]
    public void GiveDebugComponents()
    {
        if (_componentCatalog == null || _debugComponentIds == null)
            return;

        for (int i = 0; i < _debugComponentIds.Length; i++)
        {
            ComponentDefinition component = _componentCatalog.Find(_debugComponentIds[i]);
            if (component == null)
            {
                Debug.LogWarning($"'{_debugComponentIds[i]}' 부품을 찾을 수 없다.");
                continue;
            }

            GiveComponent(component, _debugAmount);
        }
    }
}
