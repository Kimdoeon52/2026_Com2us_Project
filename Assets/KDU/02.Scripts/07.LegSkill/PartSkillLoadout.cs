using UnityEngine;

// 장착 파츠의 스킬 ID로 슬롯 행동을 채운다. 매핑이 없으면 기존 로드아웃으로 넘긴다
// 같은 스킬이라도 슬롯마다 복제본을 써서 쿨타임·부위 파손·레벨을 따로 둔다
public class PartSkillLoadout : MonoBehaviour, ISkillLoadout
{
    private static readonly (Slot part, ActionSlot action, BodyPart body)[] SlotMap =
    {
        (Slot.LeftArm, ActionSlot.LeftArm, BodyPart.LeftArm),
        (Slot.RightArm, ActionSlot.RightArm, BodyPart.RightArm),
        (Slot.LeftLeg, ActionSlot.LeftLeg, BodyPart.LeftLeg),
        (Slot.RightLeg, ActionSlot.RightLeg, BodyPart.RightLeg),
    };

    [Tooltip("스킬 매핑")]
    [SerializeField] private PartSkillCatalog _catalog;

    [Tooltip("기본 로드아웃")]
    [SerializeField] private MonoBehaviour _fallback;

    private readonly ActionData[] _sources = new ActionData[SlotMap.Length];
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
        Hook();
    }

    // 씬 시작 시 OnEnable이 InventoryHost.Awake보다 먼저 불릴 수 있다
    private void Start()
    {
        Hook();
    }

    private void OnDisable()
    {
        if (_host != null)
            _host.Changed -= Rebuild;

        _host = null;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < SlotMap.Length; i++)
        {
            ReleaseClone(i);
        }
    }

    private void Hook()
    {
        if (_host != null || InventoryHost.Instance == null)
            return;

        _host = InventoryHost.Instance;
        _host.Changed += Rebuild;
        Rebuild();
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

    // 장착 변경 시 슬롯별 행동 재계산. 같은 행동이면 복제본을 유지해 쿨타임이 초기화되지 않는다
    private void Rebuild()
    {
        for (int i = 0; i < SlotMap.Length; i++)
        {
            ActionData source = FindAction(SlotMap[i].part);
            if (source != _sources[i])
            {
                ReleaseClone(i);
                _sources[i] = source;
                _cache[i] = source != null ? source.CreateSlotInstance(SlotMap[i].body) : null;
            }

            if (_cache[i] != null)
                _cache[i].SetRuntimeDamageScale(SkillLevelSystem.Instance != null ? SkillLevelSystem.Instance.GetDamageScale(SlotMap[i].part) : 1f);
        }
    }

    private ActionData FindAction(Slot slot)
    {
        PartsDefinition part = _host != null ? _host.GetEquipped(slot) : null;
        if (part == null || string.IsNullOrEmpty(part.ActiveSkillId) || _catalog == null)
            return null;

        if (_catalog.TryFind(part.ActiveSkillId, out PartSkillCatalog.Entry entry))
            return entry.Action;

        Debug.LogWarning($"[PartSkillLoadout] {part.Id}: 스킬 ID {part.ActiveSkillId} 매핑 없음", this);
        return null;
    }

    private void ReleaseClone(int index)
    {
        if (_cache[index] != null)
            Destroy(_cache[index]);

        _cache[index] = null;
        _sources[index] = null;
    }
}
