using UnityEngine;

// 장착 파츠의 스킬 ID로 슬롯 행동을 채운다. 매핑이 없으면 기존 로드아웃으로 넘긴다
public class PartSkillLoadout : MonoBehaviour, ISkillLoadout
{
    private static readonly (Slot part, ActionSlot action)[] SlotMap =
    {
        (Slot.LeftArm, ActionSlot.LeftArm),
        (Slot.RightArm, ActionSlot.RightArm),
        (Slot.LeftLeg, ActionSlot.LeftLeg),
        (Slot.RightLeg, ActionSlot.RightLeg),
    };

    [Tooltip("스킬 매핑")]
    [SerializeField] private PartSkillCatalog _catalog;

    [Tooltip("기본 로드아웃 (다른 오브젝트에 둘 것)")]
    [SerializeField] private MonoBehaviour _fallback;

    private readonly ActionData[] _cache = new ActionData[SlotMap.Length];
    private ISkillLoadout _fallbackLoadout;
    private InventoryHost _host;

    private void Awake()
    {
        _fallbackLoadout = _fallback as ISkillLoadout;
        if (_fallback != null && _fallbackLoadout == null)
            Debug.LogWarning($"[PartSkillLoadout] {_fallback.name}: ISkillLoadout 아님", this);
    }

    private void OnEnable()
    {
        _host = InventoryHost.Instance;
        if (_host != null)
            _host.Changed += Rebuild;

        Rebuild();
    }

    private void OnDisable()
    {
        if (_host != null)
            _host.Changed -= Rebuild;
    }

    public ActionData GetSlotAction(ActionSlot slot)
    {
        for (int i = 0; i < SlotMap.Length; i++)
        {
            if (SlotMap[i].action == slot && _cache[i] != null)
                return _cache[i];
        }

        return _fallbackLoadout?.GetSlotAction(slot);
    }

    // 장착 변경 시 슬롯별 행동 재계산
    private void Rebuild()
    {
        for (int i = 0; i < SlotMap.Length; i++)
        {
            _cache[i] = null;

            PartsDefinition part = _host != null ? _host.GetEquipped(SlotMap[i].part) : null;
            if (part == null || string.IsNullOrEmpty(part.ActiveSkillId) || _catalog == null)
                continue;

            _cache[i] = _catalog.Find(part.ActiveSkillId);
            if (_cache[i] == null)
                Debug.LogWarning($"[PartSkillLoadout] {part.Id}: 스킬 ID {part.ActiveSkillId} 매핑 없음", this);
        }
    }
}
