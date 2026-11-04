using System;
using System.Collections.Generic;
using UnityEngine;

// 인벤토리 세이브 묶음
[Serializable]
public class InventorySaveData
{
    [SerializeField] private ComponentStock _components = new ComponentStock();
    [SerializeField] private List<PartsInstance> _parts = new List<PartsInstance>();
    [SerializeField] private EquipmentSaveData _equipment = new EquipmentSaveData();
    [SerializeField] private CoreSaveData _core = new CoreSaveData();
    [SerializeField] private List<string> _wrecks = new List<string>();   // 패배로 파괴된 파츠 ID (잔해)
    [SerializeField] private ConsignmentSaveData _consignment = new ConsignmentSaveData();
    [SerializeField] private SkillLevelSaveData _skillLevels = new SkillLevelSaveData();

    public ComponentStock Components => _components;
    public List<PartsInstance> Parts => _parts;
    public EquipmentSaveData Equipment => _equipment;
    public CoreSaveData Core => _core;
    public List<string> Wrecks => _wrecks;
    public ConsignmentSaveData Consignment => _consignment;
    public SkillLevelSaveData SkillLevels => _skillLevels;

    public void CaptureFrom(InventoryGrid grid)
    {
        if (grid == null)
            return;

        _parts = grid.ToSaveList();
    }

    public void RestoreTo(InventoryGrid grid, PartsCatalog catalog)
    {
        if (grid == null)
            return;

        grid.Load(_parts, catalog);
    }
}
