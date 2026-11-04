using System;
using UnityEngine;

// 부위별 스킬 레벨. 남은 포인트는 저장하지 않고 코어 레벨과 찍은 레벨로 계산한다. InventoryHost와 같은 오브젝트에 붙는다
public class SkillLevelSystem : MonoBehaviour
{
    private const string ResourcePath = "SkillLevelDefinition";

    // 스킬 창에 표시할 부위와 순서
    public static readonly Slot[] SkillSlots = { Slot.LeftArm, Slot.RightArm, Slot.LeftLeg, Slot.RightLeg };

    public static SkillLevelSystem Instance { get; private set; }

    private InventoryHost _host;
    private SkillLevelDefinition _definition;

    public event Action Changed;

    public int MaxLevel => _definition != null ? _definition.MaxLevel : 1;
    public int GrantedPoints => _definition != null ? _definition.GetGrantedPoints(_host.CoreLevel) : 0;
    public int AvailablePoints => GrantedPoints - SpentPoints;

    public int SpentPoints
    {
        get
        {
            int spent = 0;
            for (int i = 0; i < SkillSlots.Length; i++)
            {
                spent += GetLevel(SkillSlots[i]) - 1;
            }

            return spent;
        }
    }

    private void Awake()
    {
        Instance = this;
        _host = GetComponent<InventoryHost>();
        _definition = Resources.Load<SkillLevelDefinition>(ResourcePath);

        if (_definition == null)
            Debug.LogWarning($"Resources/{ResourcePath} 에셋이 없다.", this);
    }

    private void OnEnable()
    {
        _host.Changed += Reconcile;
        Reconcile();
    }

    private void OnDisable()
    {
        _host.Changed -= Reconcile;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 장착한 파츠의 스킬이 레벨업 대상일 때만 true
    public bool TryGetSkill(Slot slot, out PartSkillCatalog.Entry entry)
    {
        entry = default;
        PartsDefinition part = _host.GetEquipped(slot);
        if (part == null || _definition == null || _definition.Catalog == null)
            return false;

        return _definition.Catalog.TryFind(part.ActiveSkillId, out entry) && entry.CanLevelUp;
    }

    // 지금 장착한 스킬의 기록이 아니면 1레벨
    public int GetLevel(Slot slot)
    {
        if (!TryGetSkill(slot, out PartSkillCatalog.Entry entry))
            return 1;

        SkillLevelSaveData save = _host.Save.SkillLevels;
        if (save.GetSkillId(slot) != entry.Id)
            return 1;

        return Mathf.Clamp(save.GetLevel(slot), 1, MaxLevel);
    }

    public float GetDamageScale(Slot slot)
    {
        return _definition != null ? _definition.GetDamageScale(GetLevel(slot)) : 1f;
    }

    public bool CanLevelUp(Slot slot)
    {
        return TryGetSkill(slot, out PartSkillCatalog.Entry _) && GetLevel(slot) < MaxLevel && AvailablePoints > 0;
    }

    public bool CanLevelDown(Slot slot)
    {
        return GetLevel(slot) > 1;
    }

    public bool TryLevelUp(Slot slot)
    {
        if (!CanLevelUp(slot))
            return false;

        SetLevel(slot, GetLevel(slot) + 1);
        return true;
    }

    public bool TryLevelDown(Slot slot)
    {
        if (!CanLevelDown(slot))
            return false;

        SetLevel(slot, GetLevel(slot) - 1);
        return true;
    }

    private void SetLevel(Slot slot, int level)
    {
        SkillLevelSaveData save = _host.Save.SkillLevels;
        if (level > 1 && TryGetSkill(slot, out PartSkillCatalog.Entry entry))
            save.Set(slot, entry.Id, level);
        else
            save.Clear(slot);

        _host.NotifyChanged();
    }

    // 빠진 스킬의 기록을 지운다. 포인트가 모자라면 전부 초기화
    private void Reconcile()
    {
        SkillLevelSaveData save = _host.Save.SkillLevels;
        for (int i = 0; i < SkillSlots.Length; i++)
        {
            Slot slot = SkillSlots[i];
            string id = save.GetSkillId(slot);
            if (id == null)
                continue;

            if (!TryGetSkill(slot, out PartSkillCatalog.Entry entry) || entry.Id != id)
                save.Clear(slot);
        }

        if (AvailablePoints < 0)
        {
            for (int i = 0; i < SkillSlots.Length; i++)
            {
                save.Clear(SkillSlots[i]);
            }
        }

        Changed?.Invoke();
    }
}
