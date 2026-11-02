using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 경매장 위탁 창. 왼쪽 보유 파츠, 가운데 선택 정보, 오른쪽 위탁 슬롯
public class ConsignmentView : MonoBehaviour
{
    [Serializable]
    private struct SlotWidget
    {
        [Tooltip("슬롯 문구")]
        public TextMeshProUGUI Label;

        [Tooltip("취소 버튼")]
        public Button Cancel;
    }

    [Header("규칙")]
    [Tooltip("위탁 규칙")]
    [SerializeField] private ConsignmentDefinition _definition;

    [Tooltip("난수 시드. 0이면 매번 다르게")]
    [SerializeField] private int _seed;

    [Header("보유 파츠")]
    [Tooltip("목록 부모")]
    [SerializeField] private RectTransform _rowRoot;

    [Tooltip("목록 한 줄 원본")]
    [SerializeField] private ConsignmentRow _rowTemplate;

    [Tooltip("등록 가능 개수")]
    [SerializeField] private TextMeshProUGUI _sellableLabel;

    [Tooltip("전체 필터")]
    [SerializeField] private Button _allButton;

    [Tooltip("일반 필터")]
    [SerializeField] private Button _commonButton;

    [Tooltip("레어 필터")]
    [SerializeField] private Button _rareButton;

    [Tooltip("에픽 필터")]
    [SerializeField] private Button _epicButton;

    [Header("선택 정보")]
    [SerializeField] private Image _partImage;
    [SerializeField] private TextMeshProUGUI _gradeLabel;
    [SerializeField] private TextMeshProUGUI _slotLabel;
    [SerializeField] private TextMeshProUGUI _nameLabel;
    [SerializeField] private TextMeshProUGUI _durabilityLabel;
    [SerializeField] private TextMeshProUGUI _baseCostLabel;
    [SerializeField] private TextMeshProUGUI _priceLabel;

    [Tooltip("위탁 등록 버튼")]
    [SerializeField] private Button _registerButton;

    [Header("위탁 슬롯")]
    [SerializeField] private SlotWidget[] _slots;

    [Tooltip("사용 슬롯 수")]
    [SerializeField] private TextMeshProUGUI _countLabel;

    [Tooltip("기대 정산액")]
    [SerializeField] private TextMeshProUGUI _totalLabel;

    [Tooltip("보유 골드")]
    [SerializeField] private TextMeshProUGUI _goldLabel;

    [Header("문구")]
    [Tooltip("부위 이름 (Slot 순서)")]
    [SerializeField] private string[] _slotNames = { "왼팔", "오른팔", "왼다리", "오른다리", "머리", "코어" };

    [SerializeField] private string _emptySlotText = "빈슬롯";
    [SerializeField] private string _noSelectionText = "파츠를 선택하세요";

    private readonly List<PartsInstance> _parts = new List<PartsInstance>();
    private InventoryHost _host;
    private IGoldSource _gold;
    private Rng _rng;
    private PartsInstance _selected;
    private Sprite _placeholder;
    private int _shownGold = -1;
    private bool _filterAll = true;
    private PartGrade _filter;

    private void Awake()
    {
        _rng = new Rng(_seed != 0 ? _seed : Environment.TickCount);

        if (_partImage != null)
            _placeholder = _partImage.sprite;

        if (_rowTemplate != null)
            _rowTemplate.gameObject.SetActive(false);

        Bind(_allButton, () => SetFilter(true, default));
        Bind(_commonButton, () => SetFilter(false, PartGrade.Common));
        Bind(_rareButton, () => SetFilter(false, PartGrade.Rare));
        Bind(_epicButton, () => SetFilter(false, PartGrade.Epic));
        Bind(_registerButton, Register);

        for (int i = 0; i < _slots.Length; i++)
        {
            int index = i;
            Bind(_slots[i].Cancel, () => Cancel(index));
        }
    }

    private void OnEnable()
    {
        Hook();
    }

    // 씬 시작 시 InventoryHost.Awake보다 먼저 불릴 수 있다
    private void Start()
    {
        Hook();

        if (_host == null)
            Debug.LogWarning("InventoryHost가 없다.");
    }

    private void OnDisable()
    {
        if (_host != null)
            _host.Changed -= Refresh;

        _host = null;
    }

    private void Hook()
    {
        if (!GoldSources.IsAlive(_gold))
            _gold = GoldSources.Find();

        if (_host != null || InventoryHost.Instance == null)
            return;

        _host = InventoryHost.Instance;
        _host.Changed += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (_host == null || _host.Grid == null)
            return;

        CollectParts();
        DrawList();
        DrawDetail();
        DrawSlots();

        DrawGold(true);
    }

    // 골드는 변경 이벤트가 없어 값이 바뀔 때만 다시 쓴다
    private void Update()
    {
        DrawGold(false);
    }

    private void DrawGold(bool force)
    {
        if (_goldLabel == null)
            return;

        // 매 프레임 씬 검색은 무거워서 찾기는 Hook에서만 한다
        int gold = GoldSources.IsAlive(_gold) ? _gold.PlayerGold : 0;
        if (!force && gold == _shownGold)
            return;

        _shownGold = gold;
        _goldLabel.text = $"{gold:N0}G";
    }

    private void CollectParts()
    {
        _parts.Clear();
        var entries = _host.Grid.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].IsComponent && entries[i].Parts != null && entries[i].Definition != null)
                _parts.Add(entries[i].Parts);
        }

        if (_selected != null && !_parts.Contains(_selected))
            _selected = null;
    }

    private void DrawList()
    {
        int sellable = 0;
        int shown = 0;

        for (int i = 0; i < _parts.Count; i++)
        {
            PartsInstance part = _parts[i];
            bool canSell = IsSellable(part);
            if (canSell)
                sellable++;

            if (!_filterAll && part.Definition.Grade != _filter)
                continue;

            var row = (RectTransform)_rowRoot.GetOrCreateChild(_rowTemplate.gameObject, shown + 1);
            row.gameObject.SetActive(true);
            row.GetComponent<ConsignmentRow>().Show(part.Definition.DisplayName, RowInfo(part), part == _selected, canSell, () => Select(part));
            shown++;
        }

        // 0번은 원본이다
        for (int i = shown + 1; i < _rowRoot.childCount; i++)
        {
            _rowRoot.GetChild(i).gameObject.SetActive(false);
        }

        if (_sellableLabel != null)
            _sellableLabel.text = $"등록가능 {sellable}개";
    }

    private string RowInfo(PartsInstance part)
    {
        string state = part.Definition.Grade == PartGrade.Basic ? "판매 불가" : part.IsDestroyed ? "파괴됨" : $"내구도 {part.CurrentDurability}/{part.Definition.Durability}";
        return $"{PartGrades.ToLabel(part.Definition.Grade)} · {SlotName(part.Definition.Slot)} · {state}";
    }

    private void DrawDetail()
    {
        PartsDefinition def = _selected != null ? _selected.Definition : null;

        if (_partImage != null)
        {
            // 이미지가 없는 파츠는 기본 아이콘을 둔다
            _partImage.sprite = def != null && def.Sprite != null ? def.Sprite : _placeholder;
            _partImage.preserveAspect = true;
        }

        SetText(_gradeLabel, def != null ? PartGrades.ToLabel(def.Grade) : "-");
        SetText(_slotLabel, def != null ? SlotName(def.Slot) : "-");
        SetText(_nameLabel, def != null ? def.DisplayName : _noSelectionText);
        SetText(_durabilityLabel, def != null ? $"내구도 {_selected.CurrentDurability}/{def.Durability}" : "내구도 -");
        SetText(_baseCostLabel, def != null ? $"기준가 {def.Cost:N0}G" : "기준가 -");
        SetText(_priceLabel, def != null ? PriceText(def) : "-");

        if (_registerButton != null)
            _registerButton.interactable = def != null && IsSellable(_selected) && HasFreeSlot();
    }

    // 이번 주 이미 정해진 가격이 있으면 그 값을 보여준다
    private string PriceText(PartsDefinition def)
    {
        if (_host.Save.Consignment.TryGetLockedPrice(def.Id, out int locked))
            return $"{locked:N0}G";

        if (_definition == null)
            return $"{def.Cost:N0}G";

        return $"{_definition.MinPrice(def.Cost):N0} ~{_definition.MaxPrice(def.Cost):N0}G";
    }

    private void DrawSlots()
    {
        ConsignmentSaveData save = _host.Save.Consignment;
        IReadOnlyList<ConsignedPart> consigned = save.Slots;

        for (int i = 0; i < _slots.Length; i++)
        {
            bool used = i < consigned.Count;
            bool open = i < SlotCount;

            if (_slots[i].Label != null)
            {
                PartsDefinition def = used ? _host.Catalog.Find(consigned[i].PartId) : null;
                _slots[i].Label.text = used ? $"{(def != null ? def.DisplayName : consigned[i].PartId)}  {consigned[i].Price:N0}G" : _emptySlotText;
            }

            if (_slots[i].Cancel != null)
                _slots[i].Cancel.interactable = used;

            if (_slots[i].Label != null)
                _slots[i].Label.transform.parent.gameObject.SetActive(open);
        }

        SetText(_countLabel, $"{consigned.Count} / {SlotCount}");
        SetText(_totalLabel, $"약 {save.TotalPrice():N0}G");
    }

    private void Select(PartsInstance part)
    {
        _selected = part;
        DrawList();
        DrawDetail();
    }

    private void SetFilter(bool all, PartGrade grade)
    {
        _filterAll = all;
        _filter = grade;
        DrawList();
    }

    private void Register()
    {
        if (_selected == null || !IsSellable(_selected) || !HasFreeSlot())
            return;

        PartsDefinition def = _selected.Definition;
        ConsignmentSaveData save = _host.Save.Consignment;

        // 등록 순간 이번 주 가격이 고정된다
        if (!save.TryGetLockedPrice(def.Id, out int price))
        {
            price = _definition != null ? _definition.RollPrice(def.Cost, _rng) : def.Cost;
            save.LockPrice(def.Id, price);
        }

        _selected = null;
        save.Add(new ConsignedPart(def.Id, price));
        if (!_host.RemovePart(def, true))
            save.RemoveAt(save.Slots.Count - 1);

        Refresh();
    }

    private void Cancel(int slotIndex)
    {
        ConsignmentSaveData save = _host.Save.Consignment;
        if (slotIndex >= save.Slots.Count)
            return;

        ConsignedPart consigned = save.Slots[slotIndex];
        PartsDefinition def = _host.Catalog.Find(consigned.PartId);
        if (def == null)
            return;

        // 지급 시 세이브가 같이 저장되므로 슬롯을 먼저 뺀다
        save.RemoveAt(slotIndex);
        if (!_host.GivePart(def))
        {
            save.Insert(slotIndex, consigned);
            Debug.LogWarning("인벤토리에 빈자리가 없어 위탁을 취소하지 못했다.");
        }

        Refresh();
    }

    // 기본 파츠가 아니고, 파괴되지 않고 내구도가 가득 찬 파츠만 판매 가능
    private static bool IsSellable(PartsInstance part)
    {
        return part != null && part.Definition.Grade != PartGrade.Basic && !part.IsDestroyed && part.LostDurability == 0;
    }

    private bool HasFreeSlot() => _host.Save.Consignment.Slots.Count < SlotCount;

    private int SlotCount => Mathf.Min(_definition != null ? _definition.SlotCount : _slots.Length, _slots.Length);

    private string SlotName(Slot slot)
    {
        int index = (int)slot;
        return _slotNames != null && index < _slotNames.Length ? _slotNames[index] : slot.ToString();
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value;
    }
}
