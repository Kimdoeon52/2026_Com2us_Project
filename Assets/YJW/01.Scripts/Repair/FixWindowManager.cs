using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 수리/복구 창 전체를 관리한다. 값이 바뀌면 각 패널에 다시 그리라고 알린다
public class FixWindowManager : Singleton<FixWindowManager>
{
    private const int PlayerId = 0;

    private static readonly Slot[] RepairSlots = { Slot.Head, Slot.LeftArm, Slot.RightArm, Slot.LeftLeg, Slot.RightLeg };

    [Header("상단")]
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private TextMeshProUGUI _dateText;

    [Header("수리 화면")]
    [SerializeField] private RepairPartRow[] _rows;
    [SerializeField] private RepairDiagnosisSlot[] _diagnosisSlots;
    [SerializeField] private RepairDetailPanel _detailPanel;
    [SerializeField] private RepairSummaryBar _summaryBar;

    [Header("복구 화면")]
    [SerializeField] private CoreRestorePanel _corePanel;
    [SerializeField] private PushWreckData _wreckData;

    [Tooltip("이 씬만 단독 실행할 때 쓰는 시간 시스템")]
    [SerializeField] private GameObject _localTimeSystem;

    private HashSet<Slot> _checked = new HashSet<Slot>();
    private int _lastHour = -1;
    private bool _started;

    public Slot SelectedSlot { get; private set; } = Slot.Head;
    public int CheckedCount => _checked.Count;
    public int PlayerGold => GlobalGold.Instance != null ? GlobalGold.Instance.PlayerGold : 0;

    protected override void Awake()
    {
        base.Awake();

        // 다른 씬에서 넘어온 시간 시스템이 있으면 그것을 쓴다
        _localTimeSystem.SetActive(TimeSystemManager.Instance == null);
    }

    // InventoryHost.Instance는 InventoryHost.Awake에서 채워지므로 Awake가 아니라 Start에서 구독
    private void Start()
    {
        if (InventoryHost.Instance != null)
            InventoryHost.Instance.Changed += Refresh;

        _started = true;
        CheckAllDamaged();
        Refresh();
    }

    private void OnEnable()
    {
        if (_started == false) return;

        CheckAllDamaged();
        Refresh();
    }

    protected override void OnDestroy()
    {
        if (InventoryHost.Instance != null)
            InventoryHost.Instance.Changed -= Refresh;

        base.OnDestroy();
    }

    private void Update()
    {
        // 창을 열어둔 동안 시간이 흐르면 날짜와 완료 예정을 맞춘다
        TimeSystemManager time = TimeSystemManager.Instance;
        if (time == null || time.hour == _lastHour) return;

        _lastHour = time.hour;
        Refresh();
    }

    public void Refresh()
    {
        if (InventoryHost.Instance == null) return;

        _goldText.text = $"{PlayerGold:N0}G";

        TimeSystemManager time = TimeSystemManager.Instance;
        if (time != null)
            _dateText.text = $"{time.year}년 {time.month:D2}월 {time.day:D2}일 {time.hour:D2}시";

        foreach (Slot slot in RepairSlots)
        {
            if (!NeedRepair(slot)) _checked.Remove(slot);
        }

        foreach (var row in _rows)
        {
            row.Refresh();
        }

        foreach (var slot in _diagnosisSlots)
        {
            slot.Refresh();
        }

        _detailPanel.Refresh();
        _summaryBar.Refresh();
        _corePanel.Refresh();
        _wreckData.Refresh();
    }

    public void Select(Slot slot)
    {
        SelectedSlot = slot;
        Refresh();
    }

    public bool IsChecked(Slot slot) => _checked.Contains(slot);

    public void SetChecked(Slot slot, bool isOn)
    {
        if (isOn) _checked.Add(slot);
        else _checked.Remove(slot);

        _summaryBar.Refresh();
    }

    private void CheckAllDamaged()
    {
        _checked.Clear();

        foreach (Slot slot in RepairSlots)
        {
            if (NeedRepair(slot)) _checked.Add(slot);
        }
    }

    public bool NeedRepair(Slot slot)
    {
        InventoryHost host = InventoryHost.Instance;
        if (host == null) return false;

        PartsDefinition part = host.GetEquipped(slot);
        return RepairRules.CanRepair(part) && host.GetDurability(slot) < part.Durability;
    }

    public int CheckedCost()
    {
        int total = 0;

        foreach (Slot slot in _checked)
        {
            total += RepairRules.RepairCost(InventoryHost.Instance.GetEquipped(slot), InventoryHost.Instance.GetDurability(slot));
        }
        return total;
    }

    public bool HasWreck(Slot slot)
    {
        foreach (string id in InventoryHost.Instance.Save.Wrecks)
        {
            PartsDefinition part = InventoryHost.Instance.Catalog.Find(id);
            if (part != null && part.Slot == slot) return true;
        }
        return false;
    }

    public bool CanRestore(PartsDefinition part)
    {
        return RepairRules.CanRestore(part)
            && PlayerGold >= RepairRules.RestoreCost(part)
            && RepairRules.OwnedMaterial(part) >= RepairRules.RestoreMaterial(part);
    }

    // 달력은 한 달 28일, 1년 12개월이다
    public string FinishTimeText(int hours)
    {
        TimeSystemManager time = TimeSystemManager.Instance;
        if (time == null) return "-";

        int total = time.hour + hours;
        int day = time.day + total / 24;
        int month = time.month;

        while (day > 28)
        {
            day -= 28;
            month = month % 12 + 1;
        }
        return $"{month:D2}월 {day:D2}일 {total % 24:D2}시";
    }

    //================================ 버튼 ================================
    public void OnClickClearChecks()
    {
        _checked.Clear();
        Refresh();
    }

    public void OnClickStartRepair()
    {
        int cost = CheckedCost();
        int hours = CheckedCount * RepairRules.RepairHours;

        if (CheckedCount == 0 || !PayGold(cost)) return;

        foreach (Slot slot in new List<Slot>(_checked))
        {
            InventoryHost.Instance.RepairSlot(slot);
        }

        _checked.Clear();
        FinishAction(hours);
    }

    public void OnClickStartCoreRestore()
    {
        InventoryHost host = InventoryHost.Instance;

        if (!host.Save.Core.IsUnstable || !PayGold(RepairRules.CoreRestoreCost(host.CoreLevel))) return;

        host.RestoreCore();
        FinishAction(RepairRules.RestoreHours);
    }

    public void RestoreWreck(PartsDefinition part)
    {
        if (!CanRestore(part)) return;

        // 놓을 자리가 없으면 비용을 받지 않는다
        if (!InventoryHost.Instance.RestoreWreck(part.Id))
        {
            Debug.LogWarning($"{part.DisplayName}을 놓을 자리가 없다.");
            return;
        }

        int material = RepairRules.RestoreMaterial(part);
        if (material > 0) InventoryHost.Instance.ConsumeComponent(part.RestoreComponentId, material);
        PayGold(RepairRules.RestoreCost(part));
        FinishAction(RepairRules.RestoreHours);
    }

    private bool PayGold(int cost)
    {
        if (cost <= 0) return true;

        return GlobalGold.Instance != null && GlobalGold.Instance.UseGold(PlayerId, cost);
    }

    private void FinishAction(int hours)
    {
        if (GlobalGold.Instance != null)
            GlobalGold.Instance.SaveGame();

        if (TimeSystemManager.Instance != null)
            TimeSystemManager.Instance.PassTime(hours);

        Refresh();
    }
}
