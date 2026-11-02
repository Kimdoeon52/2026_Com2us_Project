using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// FixWindow의 수리·복구 화면을 InventoryHost, GlobalGold, TimeSystemManager와 연결한다
// 씬 오브젝트는 FixWindowBuilder가 만든 이름으로 찾는다
public class FixWindowController : MonoBehaviour
{
    private const int PlayerId = 0;

    private static readonly Slot[] RepairSlots = { Slot.Head, Slot.LeftArm, Slot.RightArm, Slot.LeftLeg, Slot.RightLeg };

    private static readonly Color Yellow = new Color32(255, 232, 0, 255);
    private static readonly Color Cyan = new Color32(53, 208, 222, 255);
    private static readonly Color Bad = new Color32(255, 77, 46, 255);
    private static readonly Color Good = new Color32(82, 224, 143, 255);
    private static readonly Color Warning = new Color32(255, 138, 30, 255);
    private static readonly Color Bone = new Color32(238, 237, 232, 255);
    private static readonly Color Neutral = new Color32(180, 185, 192, 255);
    private static readonly Color Ink = new Color32(26, 27, 29, 255);

    [Header("상태 아이콘")]
    [SerializeField] private Sprite _statusCool;
    [SerializeField] private Sprite _statusWarm;
    [SerializeField] private Sprite _statusHot;

    [Header("내구도 칸")]
    [SerializeField] private Sprite _cellGood;
    [SerializeField] private Sprite _cellWarning;
    [SerializeField] private Sprite _cellBad;
    [SerializeField] private Sprite _cellEmpty;

    [Header("뱃지")]
    [SerializeField] private Sprite _badgeNeutral;
    [SerializeField] private Sprite _badgeCyan;
    [SerializeField] private Sprite _badgeOrange;
    [SerializeField] private Sprite _badgeYellow;
    [SerializeField] private Sprite _badgeRed;
    [SerializeField] private Sprite _badgeGreen;

    [Header("선택 표시")]
    [SerializeField] private Sprite _rowNormal;
    [SerializeField] private Sprite _rowSelected;
    [SerializeField] private Sprite _slotNormal;
    [SerializeField] private Sprite _slotSelected;

    [Header("이 씬만 단독 실행할 때 쓰는 시간 시스템")]
    [SerializeField] private GameObject _localTimeSystem;

    private enum PartState { Empty, Normal, Damaged, Emergency, Broken }

    private class Row
    {
        public Slot Slot;
        public Image Background;
        public Toggle Check;
        public TMP_Text Name;
        public TMP_Text DurabilityText;
        public TMP_Text State;
        public TMP_Text Cost;
        public TMP_Text Time;
        public Image StateMark;
        public Image[] Cells;
        public Transform Badge;
        public GameObject SelectedBar;
        public Image DiagnosisImage;
        public TMP_Text DiagnosisLabel;
        public Image DiagnosisStatus;
    }

    private readonly List<Row> _rows = new List<Row>();
    private readonly HashSet<Slot> _checked = new HashSet<Slot>();
    private readonly List<GameObject> _cards = new List<GameObject>();
    private Slot _selected = Slot.Head;
    private bool _initialized;
    private bool _started;
    private int _lastHour = -1;

    private Transform _repair;
    private Transform _restore;
    private Transform _detail;
    private Transform _summary;
    private Transform _corePanel;
    private Transform _cardContent;
    private GameObject _cardTemplate;
    private GameObject _guideCardTemplate;

    private InventoryHost Host => InventoryHost.Instance;
    private int PlayerGold => GlobalGold.Instance != null ? GlobalGold.Instance.PlayerGold : 0;

    private void Awake()
    {
        // 다른 씬에서 넘어온 시간 시스템이 있으면 그것을 쓴다
        if (_localTimeSystem != null)
            _localTimeSystem.SetActive(TimeSystemManager.Instance == null);

        _repair = transform.Find("RepairPanel");
        _restore = transform.Find("RestorePanel");
        if (_repair == null || _restore == null)
        {
            Debug.LogError("[FixWindow] RepairPanel, RestorePanel을 찾을 수 없다. FixWindow 배치 메뉴를 먼저 실행해야 한다.", this);
            return;
        }

        _detail = _repair.Find("DetailPanel");
        _summary = _repair.Find("SummaryBar");
        _corePanel = _restore.Find("CoreRestorePanel");
        _cardContent = _restore.Find("DestroyedPartsPanel/DestroyedScrollView/Viewport/Content");

        BuildRows();
        BindCardTemplates();
        BindButton(_repair.Find("PartsPanel/ClearAllButton"), ClearChecks);
        BindButton(_summary.Find("StartRepairButton"), StartRepair);
        BindButton(_corePanel.Find("StartCoreRestoreButton"), StartCoreRestore);

        _initialized = true;
    }

    // InventoryHost.Instance는 InventoryHost.Awake에서 채워지므로 Awake가 아니라 Start에서 구독
    private void Start()
    {
        if (!_initialized)
            return;

        if (Host != null)
            Host.Changed += Refresh;

        _started = true;
        CheckAllDamaged();
        Refresh();
    }

    private void OnEnable()
    {
        if (!_started)
            return;

        CheckAllDamaged();
        Refresh();
    }

    private void OnDestroy()
    {
        if (Host != null)
            Host.Changed -= Refresh;
    }

    // 창을 열어둔 동안 시간이 흐르면 날짜와 완료 예정을 맞춘다
    private void Update()
    {
        TimeSystemManager time = TimeSystemManager.Instance;
        if (!_started || time == null || time.hour == _lastHour)
            return;

        _lastHour = time.hour;
        Refresh();
    }

    //================================ 오브젝트 찾기 ================================
    private void BuildRows()
    {
        Transform content = _repair.Find("PartsPanel/PartsScrollView/Viewport/Content");
        Transform head = content.Find($"Row_{RepairRules.SlotName(Slot.Head)}");
        Transform diagnosis = _repair.Find("DiagnosisPanel");

        foreach (Slot slot in RepairSlots)
        {
            Transform root = content.Find($"Row_{RepairRules.SlotName(slot)}");
            if (root == null)
            {
                Debug.LogError($"[FixWindow] {RepairRules.SlotName(slot)} 행을 찾을 수 없다.", this);
                continue;
            }

            var row = new Row
            {
                Slot = slot,
                Background = root.GetComponent<Image>(),
                Check = root.Find("CheckBox").GetComponent<Toggle>(),
                Name = Text(root, "Name"),
                DurabilityText = Text(root, "DurabilityText"),
                State = Text(root, "State"),
                Cost = Text(root, "Cost"),
                Time = Text(root, "Time"),
                Cells = root.Find("Durability").GetComponentsInChildren<Image>(true),
            };

            // 빈 슬롯 행과 선택 안 된 행에는 없는 부품을 머리 행에서 복제한다
            row.StateMark = EnsureChild(root, head, "StateMark").GetComponent<Image>();
            row.Badge = FindBadge(root);
            if (row.Badge == null)
            {
                row.Badge = Instantiate(FindBadge(head), root);
                Vector2 position = row.Badge.GetComponent<RectTransform>().anchoredPosition;
                row.Badge.GetComponent<RectTransform>().anchoredPosition = new Vector2(462, position.y);
            }
            row.SelectedBar = EnsureChild(root, head, "SelectedBar").gameObject;

            row.Background.raycastTarget = true;
            Button rowButton = root.gameObject.AddComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            rowButton.onClick.AddListener(() => Select(slot));

            row.Check.onValueChanged.AddListener(isOn => OnCheckChanged(slot, isOn));

            Transform slotButton = diagnosis.Find($"Slot_{slot}");
            slotButton.GetComponent<Button>().transition = Selectable.Transition.None;
            slotButton.GetComponent<Button>().interactable = true;   // 빈 슬롯도 선택해서 상세를 볼 수 있다
            BindButton(slotButton, () => Select(slot));
            row.DiagnosisImage = slotButton.GetComponent<Image>();
            row.DiagnosisLabel = Text(slotButton, "Label");
            row.DiagnosisStatus = EnsureChild(slotButton, diagnosis.Find("Slot_Head"), "Status").GetComponent<Image>();

            _rows.Add(row);
        }
    }

    private static Transform EnsureChild(Transform root, Transform source, string childName)
    {
        Transform found = root.Find(childName);
        if (found != null)
            return found;

        Transform clone = Instantiate(source.Find(childName), root);
        clone.name = childName;
        return clone;
    }

    private void BindCardTemplates()
    {
        foreach (Transform card in _cardContent)
        {
            if (card.Find("Guide") != null)
                _guideCardTemplate = card.gameObject;
            else if (_cardTemplate == null)
                _cardTemplate = card.gameObject;

            card.gameObject.SetActive(false);
        }
    }

    private static Transform FindBadge(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.name.StartsWith("Badge_"))
                return child;
        }
        return null;
    }

    private static TMP_Text Text(Transform root, string path)
    {
        Transform found = root.Find(path);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private static void SetText(Transform root, string path, string value)
    {
        TMP_Text text = Text(root, path);
        if (text != null)
            text.text = value;
    }

    private static void BindButton(Transform target, UnityEngine.Events.UnityAction action)
    {
        Button button = target.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    //================================ 입력 ================================
    private void Select(Slot slot)
    {
        _selected = slot;
        Refresh();
    }

    private void OnCheckChanged(Slot slot, bool isOn)
    {
        if (isOn)
            _checked.Add(slot);
        else
            _checked.Remove(slot);

        RefreshSummary();
    }

    private void ClearChecks()
    {
        _checked.Clear();
        Refresh();
    }

    private void CheckAllDamaged()
    {
        _checked.Clear();
        foreach (Slot slot in RepairSlots)
        {
            if (NeedsRepair(slot))
                _checked.Add(slot);
        }
    }

    private void StartRepair()
    {
        int cost = CheckedCost();
        int count = _checked.Count;
        if (count == 0 || !PayGold(cost))
            return;

        foreach (Slot slot in new List<Slot>(_checked))
            Host.RepairSlot(slot);

        _checked.Clear();
        FinishAction(count * RepairRules.RepairHours);
    }

    private void StartCoreRestore()
    {
        if (Host == null || !Host.Save.Core.IsUnstable || !PayGold(RepairRules.CoreRestoreCost(Host.CoreLevel)))
            return;

        Host.RestoreCore();
        FinishAction(RepairRules.RestoreHours);
    }

    private void RestoreWreck(PartsDefinition part)
    {
        int cost = RepairRules.RestoreCost(part);
        int material = RepairRules.RestoreMaterial(part.Grade);
        if (PlayerGold < cost || Host.CountComponents(part.Grade) < material)
            return;

        // 놓을 자리가 없으면 비용을 받지 않는다
        if (!Host.RestoreWreck(part.Id))
        {
            Debug.LogWarning($"[FixWindow] {part.DisplayName}을 놓을 자리가 없다.", this);
            return;
        }

        if (material > 0)
            Host.ConsumeComponents(part.Grade, material);

        PayGold(cost);
        FinishAction(RepairRules.RestoreHours);
    }

    private bool PayGold(int cost)
    {
        if (cost <= 0)
            return true;

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

    //================================ 화면 갱신 ================================
    private void Refresh()
    {
        if (!_initialized || Host == null)
            return;

        RefreshHeader();
        foreach (Row row in _rows)
            RefreshRow(row);
        RefreshDetail();
        RefreshSummary();
        RefreshCore();
        RefreshCards();
    }

    private void RefreshHeader()
    {
        SetText(transform, "Gold/Text (TMP)", $"{PlayerGold:N0}G");

        TimeSystemManager time = TimeSystemManager.Instance;
        if (time != null)
            SetText(transform, "Date/Text (TMP)", $"{time.year}년 {time.month:D2}월 {time.day:D2}일 {time.hour:D2}시");
    }

    private void RefreshRow(Row row)
    {
        PartsDefinition part = Host.GetEquipped(row.Slot);
        PartState state = GetState(row.Slot);
        int current = Host.GetDurability(row.Slot);
        bool needsRepair = NeedsRepair(row.Slot);
        bool selected = row.Slot == _selected;

        if (!needsRepair)
            _checked.Remove(row.Slot);

        row.Background.sprite = selected ? _rowSelected : _rowNormal;
        row.SelectedBar.SetActive(selected);
        row.Name.text = part != null ? part.DisplayName : "빈 슬롯";
        row.DurabilityText.text = part != null ? $"{current}/{part.Durability}" : "-";
        row.State.text = StateName(state);
        row.State.color = StateColor(state);
        row.StateMark.gameObject.SetActive(part != null);
        row.StateMark.sprite = StatusIcon(state);

        row.Badge.gameObject.SetActive(part != null);
        if (part != null)
            SetGradeBadge(row.Badge, part.Grade);

        SetCells(row.Cells, part != null ? current : 0, part != null ? part.Durability : 0, state);

        row.Cost.text = part != null ? $"{RepairRules.RepairCost(part, current)}G" : "-";
        row.Time.text = RowTimeText(row.Slot, part, needsRepair);

        row.Check.SetIsOnWithoutNotify(_checked.Contains(row.Slot));
        row.Check.interactable = needsRepair;

        row.DiagnosisImage.sprite = selected ? _slotSelected : _slotNormal;
        row.DiagnosisLabel.text = $"{RepairRules.SlotKey(row.Slot)}\n{(part != null ? RepairRules.SlotName(row.Slot) : "빈 슬롯")}";
        row.DiagnosisLabel.color = selected ? Ink : part == null ? Neutral : Bone;
        row.DiagnosisStatus.gameObject.SetActive(part != null);
        row.DiagnosisStatus.sprite = StatusIcon(state);
    }

    private string RowTimeText(Slot slot, PartsDefinition part, bool needsRepair)
    {
        if (part == null)
            return HasWreck(slot) ? "잔해 보관 중" : "-";

        return needsRepair ? $"{RepairRules.RepairHours}시간" : "수리 불필요";
    }

    private void RefreshDetail()
    {
        PartsDefinition part = Host.GetEquipped(_selected);
        PartState state = GetState(_selected);
        int current = Host.GetDurability(_selected);
        int max = part != null ? part.Durability : 0;
        int cost = RepairRules.RepairCost(part, current);
        string slotName = RepairRules.SlotName(_selected);

        SetText(_detail, "Description", $"{slotName} · {RepairRules.SlotKey(_selected)}");
        SetText(_detail, "PartName", part != null ? part.DisplayName : "빈 슬롯");
        SetText(_detail, "DurabilityValue", part != null ? $"{current} / {max}" : "-");
        SetCells(_detail.Find("DurabilityBar").GetComponentsInChildren<Image>(true), current, max, state);

        Transform gradeBadge = _detail.Find("Badge_에픽");
        Transform stateBadge = _detail.Find("Badge_손상");
        gradeBadge.gameObject.SetActive(part != null);
        stateBadge.gameObject.SetActive(part != null);
        if (part != null)
        {
            SetGradeBadge(gradeBadge, part.Grade);
            SetBadge(stateBadge, StateBadge(state), StateName(state), Ink);
        }

        Transform box = _detail.Find("CostBox");
        SetText(box, "BaseCostLabel", $"부위 기본 수리비 ({slotName})");
        SetText(box, "BaseCostValue", $"{RepairRules.BaseRepairCost(_selected)}G");
        SetText(box, "GradeRateLabel", part != null ? $"× 등급 배율 ({RepairRules.GradeName(part.Grade)})" : "× 등급 배율");
        SetText(box, "GradeRateValue", part != null ? RepairRules.GradeRate(part.Grade).ToString("0.#") : "-");
        SetText(box, "LostDurabilityValue", max > 0 ? $"{Mathf.RoundToInt((max - current) * 100f / max)}%" : "-");
        SetText(box, "TotalValue", part != null ? $"{cost}G" : "-");

        SetText(_detail, "TimeValue", NeedsRepair(_selected) ? $"{RepairRules.RepairHours}시간" : "-");
        SetText(_detail, "ZeroDurabilityValue", _selected == Slot.Head ? "패시브 스킬 해제" : "액티브 스킬 봉인");
        SetText(_detail, "BreakChanceValue", part != null ? $"{BattleSettlementProcessor.GetDestructionProbability(part.Grade):P0} (등급 기준)" : "-");
    }

    private void RefreshSummary()
    {
        if (!_initialized || Host == null)
            return;

        int count = _checked.Count;
        int cost = CheckedCost();
        int hours = count * RepairRules.RepairHours;
        int balance = PlayerGold - cost;

        SetText(_summary, "SelectedValue", $"{count} <size=60%>부위</size>");
        SetText(_summary, "CostValue", $"{cost:N0} <size=60%>G</size>");
        SetText(_summary, "TimeValue", $"{hours} <size=60%>시간</size>");
        SetText(_summary, "BalanceValue", $"{balance:N0} <size=60%>G</size>");
        Text(_summary, "BalanceValue").color = balance < 0 ? Bad : Bone;
        SetText(_summary, "FinishTime", count > 0 ? $"완료 예정 {FinishTimeText(hours)}" : "수리할 부위를 선택하세요");

        _summary.Find("StartRepairButton").GetComponent<Button>().interactable = count > 0 && balance >= 0;
    }

    private void RefreshCore()
    {
        bool unstable = Host.Save.Core.IsUnstable;
        int level = Host.CoreLevel;
        int cost = RepairRules.CoreRestoreCost(level);
        int balance = PlayerGold - cost;

        Transform slotCore = _repair.Find("DiagnosisPanel/Slot_Core");
        SetText(slotCore, "CoreLevel", $"CORE Lv.{level}");
        SetText(slotCore, "CoreState", unstable ? "불안정" : "정상");
        Text(slotCore, "CoreState").color = unstable ? Bad : Good;
        slotCore.Find("CoreIcon").GetComponent<Image>().color = unstable ? Bad : Cyan;

        _restore.Find("WarningBanner").gameObject.SetActive(unstable);

        SetBadge(_corePanel.Find("Badge_불안정한 코어"), unstable ? _badgeRed : _badgeGreen, unstable ? "불안정한 코어" : "정상 코어", Ink);
        SetText(_corePanel, "CoreLevel", $"CORE Lv.{level}");
        SetText(_corePanel, "CoreGuide", unstable ? "복구 전에는 로봇을 조립할 수 없습니다." : "코어가 정상입니다.");
        _corePanel.Find("CoreIcon").GetComponent<Image>().color = unstable ? Bad : Good;
        _corePanel.Find("CoreIcon/Inner").GetComponent<Image>().color = unstable ? Bad : Good;

        Transform box = _corePanel.Find("CostBox");
        SetText(box, "LevelCostLabel", $"+ 코어 레벨 {level} × 50G");
        SetText(box, "LevelCostValue", $"{level * 50}G");
        SetText(box, "TotalValue", $"{cost}G");

        SetText(_corePanel, "TimeValue", unstable ? $"{RepairRules.RestoreHours}시간 · 완료 {FinishTimeText(RepairRules.RestoreHours)}" : "-");
        SetText(_corePanel, "BalanceValue", unstable ? $"{balance:N0}G" : "-");
        Text(_corePanel, "BalanceValue").color = unstable && balance < 0 ? Bad : Bone;

        Transform start = _corePanel.Find("StartCoreRestoreButton");
        start.GetComponent<Button>().interactable = unstable && balance >= 0;
        SetText(start, "Label", !unstable ? "복구 불필요" : balance < 0 ? "골드 부족" : "코어 복구 시작");
    }

    private void RefreshCards()
    {
        foreach (GameObject card in _cards)
            Destroy(card);
        _cards.Clear();

        int restorable = 0;
        int lacking = 0;
        int impossible = 0;

        foreach (string id in Host.Save.Wrecks)
        {
            PartsDefinition part = Host.Catalog != null ? Host.Catalog.Find(id) : null;
            if (part == null)
                continue;

            if (!RepairRules.CanRestore(part.Grade))
                impossible++;
            else if (CanAfford(part))
                restorable++;
            else
                lacking++;

            BuildCard(part);
        }

        string description = _cards.Count > 0
            ? $"복구 가능 {restorable} · 재료 부족 {lacking} · 복구 불가 {impossible} — 복구 시 {RepairRules.RestoreHours}시간, 내구도 100%"
            : "파괴된 파츠가 없습니다";
        SetText(_restore, "DestroyedPartsPanel/Description", description);
    }

    private void BuildCard(PartsDefinition part)
    {
        bool canRestore = RepairRules.CanRestore(part.Grade);
        GameObject template = canRestore ? _cardTemplate : _guideCardTemplate;
        if (template == null)
            return;

        GameObject card = Instantiate(template, _cardContent);
        card.SetActive(true);
        _cards.Add(card);

        Transform root = card.transform;
        string slotName = RepairRules.SlotName(part.Slot);
        SetText(root, "Icon/Label", RepairRules.SlotKey(part.Slot));
        SetText(root, "Slot", $"{slotName} · 잔해");
        SetText(root, "Name", part.DisplayName);
        SetGradeBadge(FindBadge(root), part.Grade);
        BindButton(root.Find("DiscardButton"), () => Host.DiscardWreck(part.Id));

        if (!canRestore)
            return;

        int cost = RepairRules.RestoreCost(part);
        int material = RepairRules.RestoreMaterial(part.Grade);
        int owned = Host.CountComponents(part.Grade);
        bool hasMaterial = owned >= material;
        bool hasGold = PlayerGold >= cost;

        SetText(root, "CostValue", $"{cost:N0}G");
        SetText(root, "MaterialValue", material > 0
            ? $"{RepairRules.GradeName(part.Grade)} 부품 {material}개 <color=#{ColorUtility.ToHtmlStringRGB(hasMaterial ? Good : Bad)}>보유 {owned}</color>"
            : "없음");

        Transform restore = root.Find("RestoreButton");
        bool available = hasMaterial && hasGold;
        restore.GetComponent<Button>().interactable = available;
        BindButton(restore, () => RestoreWreck(part));

        string label = !hasMaterial ? "재료 부족"
            : !hasGold ? "골드 부족"
            : material > 0 ? $"복구 · {cost:N0}G + 부품 {material}"
            : $"복구 · {cost:N0}G";
        SetText(restore, "Label", label);
        Text(restore, "Label").color = available ? Cyan : Neutral;
    }

    //================================ 계산 ================================
    private bool NeedsRepair(Slot slot)
    {
        if (Host == null)
            return false;

        PartsDefinition part = Host.GetEquipped(slot);
        return part != null && Host.GetDurability(slot) < part.Durability;
    }

    private int CheckedCost()
    {
        int total = 0;
        foreach (Slot slot in _checked)
            total += RepairRules.RepairCost(Host.GetEquipped(slot), Host.GetDurability(slot));
        return total;
    }

    private bool HasWreck(Slot slot)
    {
        foreach (string id in Host.Save.Wrecks)
        {
            PartsDefinition part = Host.Catalog != null ? Host.Catalog.Find(id) : null;
            if (part != null && part.Slot == slot)
                return true;
        }
        return false;
    }

    private bool CanAfford(PartsDefinition part)
    {
        return PlayerGold >= RepairRules.RestoreCost(part)
            && Host.CountComponents(part.Grade) >= RepairRules.RestoreMaterial(part.Grade);
    }

    private PartState GetState(Slot slot)
    {
        PartsDefinition part = Host.GetEquipped(slot);
        if (part == null)
            return PartState.Empty;

        int current = Host.GetDurability(slot);
        if (current <= 0)
            return PartState.Broken;
        if (current >= part.Durability)
            return PartState.Normal;
        return current == 1 ? PartState.Emergency : PartState.Damaged;
    }

    // 달력은 한 달 28일, 1년 12개월이다
    private static string FinishTimeText(int hours)
    {
        TimeSystemManager time = TimeSystemManager.Instance;
        if (time == null)
            return "-";

        int total = time.hour + hours;
        int hour = total % 24;
        int day = time.day + total / 24;
        int month = time.month;
        while (day > 28)
        {
            day -= 28;
            month = month % 12 + 1;
        }
        return $"{month:D2}월 {day:D2}일 {hour:D2}시";
    }

    //================================ 표시 ================================
    private static string StateName(PartState state)
    {
        switch (state)
        {
            case PartState.Normal: return "정상";
            case PartState.Damaged: return "손상";
            case PartState.Emergency: return "응급 복구";
            case PartState.Broken: return "파손";
            default: return "빈 슬롯";
        }
    }

    private static Color StateColor(PartState state)
    {
        switch (state)
        {
            case PartState.Normal: return Good;
            case PartState.Damaged: return Warning;
            case PartState.Emergency:
            case PartState.Broken: return Bad;
            default: return Neutral;
        }
    }

    private Sprite StatusIcon(PartState state)
    {
        switch (state)
        {
            case PartState.Normal: return _statusCool;
            case PartState.Damaged: return _statusWarm;
            default: return _statusHot;
        }
    }

    private Sprite StateBadge(PartState state)
    {
        switch (state)
        {
            case PartState.Normal: return _badgeGreen;
            case PartState.Damaged: return _badgeOrange;
            default: return _badgeRed;
        }
    }

    private void SetCells(Image[] cells, int current, int max, PartState state)
    {
        Sprite filledSprite = state == PartState.Normal ? _cellGood
            : state == PartState.Damaged ? _cellWarning
            : _cellBad;

        // 값이 1이라도 있으면 한 칸은 보이게 한다
        int filled = max > 0 ? Mathf.Clamp(Mathf.CeilToInt((float)current / max * cells.Length), 0, cells.Length) : 0;
        for (int i = 0; i < cells.Length; i++)
            cells[i].sprite = i < filled ? filledSprite : _cellEmpty;
    }

    private void SetGradeBadge(Transform badge, PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Rare: SetBadge(badge, _badgeCyan, "레어", Cyan); break;
            case PartGrade.Epic: SetBadge(badge, _badgeOrange, "에픽", Ink); break;
            case PartGrade.Legendary: SetBadge(badge, _badgeYellow, "전설", Ink); break;
            case PartGrade.Prototype: SetBadge(badge, _badgeRed, "프로토타입", Ink); break;
            default: SetBadge(badge, _badgeNeutral, "일반", Ink); break;
        }
    }

    private static void SetBadge(Transform badge, Sprite sprite, string text, Color color)
    {
        badge.GetComponent<Image>().sprite = sprite;

        TMP_Text label = badge.GetComponentInChildren<TMP_Text>(true);
        label.text = text;
        label.color = color;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10;
        label.fontSizeMax = 18;
    }
}
