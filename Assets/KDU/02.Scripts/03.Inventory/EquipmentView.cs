using UnityEngine;

// 장착 칸 렌더. 자식의 EquipmentSlotView를 모아 데이터가 바뀔 때마다 갱신한다
public class EquipmentView : MonoBehaviour
{
    private EquipmentSlotView[] _slots;
    private InventoryHost _host;

    private void Awake()
    {
        _slots = GetComponentsInChildren<EquipmentSlotView>(true);
    }

    private void Start()
    {
        _host = InventoryHost.Instance;
        if (_host == null)
        {
            Debug.LogWarning("InventoryHost가 없다.");
            return;
        }

        _host.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_host != null)
            _host.Changed -= Refresh;
    }

    public void Refresh()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Show(_host.GetEquipped(_slots[i].Slot));
        }
    }
}
