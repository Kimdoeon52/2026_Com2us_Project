using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 인벤토리·장착 칸 툴팁. 인벤토리 패널 루트에 두고 포인터 아래 칸의 엔트리를 보여준다.
// 격자 칸을 우클릭하면 '버리기' 메뉴를 띄운다
public class InventoryTooltip : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
{
    // 씬 전환 유지 싱글톤이라 인스펙터로 연결하지 않는다
    private static InventoryHost Host => InventoryHost.Instance;

    private const float MenuWidth = 220f;
    private const float ButtonHeight = 52f;

    [Tooltip("좌표 변환")]
    [SerializeField] private InventoryView _view;

    [Tooltip("툴팁 박스")]
    [SerializeField] private GameObject _box;

    [Tooltip("툴팁 텍스트")]
    [SerializeField] private TextMeshProUGUI _text;

    [Tooltip("툴팁 정렬 순서. 다른 캔버스 팝업보다 높게")]
    [SerializeField] private int _sortingOrder = 100;

    [Header("우클릭 메뉴")]
    [Tooltip("메뉴 배경")]
    [SerializeField] private Sprite _menuPanelSprite;

    [Tooltip("메뉴 버튼 배경")]
    [SerializeField] private Sprite _menuButtonSprite;

    [Tooltip("'버리기' 아이콘")]
    [SerializeField] private Sprite _discardIcon;

    [Tooltip("메뉴 정렬 순서. 툴팁보다 위")]
    [SerializeField] private int _menuSortingOrder = 110;

    // 격자에서 띄운 툴팁인지. 장착 칸이 띄운 건 격자 밖 이동으로 끄지 않는다
    private bool _fromGrid;

    // 메뉴를 연 엔트리. 열린 사이에 배치가 바뀌었는지 확인하려고 원점도 같이 둔다
    private int _menuIndex = -1;
    private Vector2Int _menuOrigin;

    private GameObject _menuLayer;
    private Canvas _menuCanvas;
    private RectTransform _menuPanel;

    private static readonly Color DiscardColor = new Color(1f, 0.42f, 0.36f, 1f);

    // 컴포넌트를 붙일 때 씬에서 찾아 채운다
    private void Reset()
    {
        if (_view == null)
            _view = FindAnyObjectByType<InventoryView>();
    }

    private void OnDisable()
    {
        Hide();
        CloseMenu();
    }

    public void Show(string text)
    {
        if (_box == null || string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        if (_text != null)
            _text.text = text;

        _box.SetActive(true);
        _fromGrid = false;

        // 장착 팝업 위에 그린다. 켠 뒤에 건다
        Canvas canvas = _box.GetOrAddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = _sortingOrder;
    }

    public void Hide()
    {
        if (_text != null)
            _text.text = string.Empty;

        if (_box != null)
            _box.SetActive(false);

        _fromGrid = false;
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (eventData.dragging)
        {
            Hide();
            CloseMenu();
            return;
        }

        if (!TryGetCell(eventData, out Vector2Int cell))
        {
            if (_fromGrid)
                Hide();
            return;
        }

        Show(GetEntryText(Host.Grid.GetIndexAt(cell)));
        _fromGrid = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_fromGrid)
            Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleClick(eventData);
    }

    // 메뉴가 열려 있으면 화면 전체 막이 클릭을 받으므로 막도 여기로 넘긴다.
    // 우클릭은 그 자리 아이템으로 메뉴를 다시 열고, 빈 칸이나 좌클릭이면 닫는다
    private void HandleClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || !TryGetCell(eventData, out Vector2Int cell))
        {
            CloseMenu();
            return;
        }

        int index = Host.Grid.GetIndexAt(cell);
        if (index < 0 || !Host.Grid.TryGetEntry(index, out InventoryGrid.Entry entry))
        {
            CloseMenu();
            return;
        }

        Hide();
        OpenMenu(index, entry.Origin, eventData.position);
    }

    private string GetEntryText(int index)
    {
        if (!Host.Grid.TryGetEntry(index, out InventoryGrid.Entry entry))
            return null;

        if (entry.IsComponent)
            return entry.Component != null ? entry.Component.GetTooltip() : null;

        return entry.Definition != null ? entry.Definition.GetTooltip() : null;
    }

    // 격자 밖이면 false
    private bool TryGetCell(PointerEventData eventData, out Vector2Int cell)
    {
        cell = default;

        if (Host == null || Host.Grid == null || _view == null || _view.PartsLayer == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_view.PartsLayer, eventData.position, eventData.enterEventCamera, out Vector2 local))
            return false;

        float size = Mathf.Max(1f, _view.CellSize);
        cell = new Vector2Int(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.y / size));
        return cell.x >= 0 && cell.y >= 0 && cell.x < Host.Grid.Width && cell.y < Host.Grid.Height;
    }

    // ---------- 우클릭 메뉴 ----------

    private void OpenMenu(int index, Vector2Int origin, Vector2 screenPosition)
    {
        EnsureMenu();
        if (_menuLayer == null)
            return;

        _menuIndex = index;
        _menuOrigin = origin;

        _menuLayer.SetActive(true);

        // 다른 캔버스 팝업 위에 그린다. 켠 뒤에 건다
        _menuCanvas.overrideSorting = true;
        _menuCanvas.sortingOrder = _menuSortingOrder;

        // 포인터 오른쪽 아래로 펼치고, 화면 밖으로 나가면 안쪽으로 당긴다
        var layer = (RectTransform)_menuLayer.transform;
        Camera camera = _menuCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _menuCanvas.rootCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPosition, camera, out Vector2 local);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_menuPanel);
        Rect bounds = layer.rect;
        Vector2 size = _menuPanel.rect.size;
        local.x = Mathf.Clamp(local.x + 8f, bounds.xMin, bounds.xMax - size.x);
        local.y = Mathf.Clamp(local.y - 8f, bounds.yMin + size.y, bounds.yMax);
        _menuPanel.anchoredPosition = local;
    }

    private void CloseMenu()
    {
        if (_menuLayer != null)
            _menuLayer.SetActive(false);

        _menuIndex = -1;
    }

    private void OnDiscardClicked()
    {
        int index = MenuEntryIndex();
        CloseMenu();
        if (index < 0)
            return;

        Hide();
        Host.DiscardEntry(index);
    }

    // 메뉴가 열린 사이에 배치가 바뀌었으면 엉뚱한 칸을 건드리지 않는다
    private int MenuEntryIndex()
    {
        if (_menuIndex < 0 || Host == null || Host.Grid == null)
            return -1;

        if (!Host.Grid.TryGetEntry(_menuIndex, out InventoryGrid.Entry entry) || entry.Origin != _menuOrigin)
            return -1;

        return _menuIndex;
    }

    // 메뉴는 처음 열 때 코드로 만든다. 화면 전체를 덮는 투명 막으로 바깥 클릭을 받아 닫는다
    private void EnsureMenu()
    {
        if (_menuLayer != null)
            return;

        Canvas root = GetComponentInParent<Canvas>();
        if (root == null)
            return;

        _menuLayer = new GameObject("ItemMenu", typeof(RectTransform));
        var layer = (RectTransform)_menuLayer.transform;
        layer.SetParent(root.rootCanvas.transform, false);
        Stretch(layer);

        _menuLayer.SetActive(false);
        _menuCanvas = _menuLayer.AddComponent<Canvas>();
        _menuLayer.AddComponent<GraphicRaycaster>();

        var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image), typeof(MenuBlocker));
        blocker.transform.SetParent(layer, false);
        Stretch((RectTransform)blocker.transform);
        blocker.GetComponent<Image>().color = Color.clear;
        blocker.GetComponent<MenuBlocker>().Clicked = HandleClick;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(layer, false);
        _menuPanel = (RectTransform)panel.transform;
        _menuPanel.anchorMin = _menuPanel.anchorMax = new Vector2(0.5f, 0.5f);
        _menuPanel.pivot = new Vector2(0f, 1f);
        _menuPanel.sizeDelta = new Vector2(MenuWidth, 0f);

        SetSprite(panel.GetComponent<Image>(), _menuPanelSprite, new Color(0.12f, 0.12f, 0.13f, 0.96f));

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddMenuButton(_menuPanel, "버리기", _discardIcon, DiscardColor, OnDiscardClicked);
    }

    private void AddMenuButton(Transform parent, string label, Sprite icon, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var buttonGo = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttonGo.transform.SetParent(parent, false);

        // 버튼 이미지는 위아래 테두리가 높아서 낮은 버튼에 맞게 줄인다
        Image background = buttonGo.GetComponent<Image>();
        SetSprite(background, _menuButtonSprite, new Color(0.22f, 0.22f, 0.24f, 1f));
        background.pixelsPerUnitMultiplier = 1.6f;
        buttonGo.GetComponent<Button>().onClick.AddListener(onClick);
        buttonGo.GetComponent<LayoutElement>().preferredHeight = ButtonHeight;

        var row = buttonGo.GetComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(16, 12, 0, 0);
        row.spacing = 10f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        if (icon != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(buttonGo.transform, false);

            var image = iconGo.GetComponent<Image>();
            image.sprite = icon;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var size = iconGo.GetComponent<LayoutElement>();
            size.preferredWidth = 24f;
            size.preferredHeight = 24f;
        }

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(buttonGo.transform, false);

        var text = textGo.GetComponent<TextMeshProUGUI>();
        if (_text != null)
            text.font = _text.font;
        text.text = label;
        text.fontSize = 24f;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
    }

    private static void SetSprite(Image image, Sprite sprite, Color fallback)
    {
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = sprite != null ? Color.white : fallback;
    }

    // 메뉴 바깥 클릭을 좌·우 구분해서 넘겨준다 (Button은 좌클릭만 받는다)
    private class MenuBlocker : MonoBehaviour, IPointerClickHandler
    {
        public System.Action<PointerEventData> Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(eventData);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
