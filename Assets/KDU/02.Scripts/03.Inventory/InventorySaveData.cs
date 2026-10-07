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

    public ComponentStock Components => _components;
    public List<PartsInstance> Parts => _parts;
    public EquipmentSaveData Equipment => _equipment;

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
