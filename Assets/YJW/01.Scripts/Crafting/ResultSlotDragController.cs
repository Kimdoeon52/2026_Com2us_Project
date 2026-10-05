using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 크래프팅 결과 슬롯(ResultSlot)에 뜬 완성 파츠를 인벤토리로 드래그해서 넣는다.
// 인벤토리 위에 실제로 배치가 성공했을 때만 재료(부품)를 소모(ConfirmCraft)한다 -
// 그냥 결과창에 떠 있는 것만으로는 재료가 안 없어지고, 인벤토리에 옮겨 담아야 확정된다.
public class ResultSlotDragController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("레시피 판정/재료 소모(ConfirmCraft)를 갖고 있는 쪽")]
    [SerializeField] private CorrectRecipe _recipe;

    [Tooltip("완성 파츠 아이콘을 보여주는 결과 슬롯. 인벤토리에 넣기 성공하면 여기를 비운다")]
    [SerializeField] private ResultSlot _resultSlot;

    [Tooltip("인벤토리 데이터. 실제 배치(GivePart)를 요청하는 대상")]
    [SerializeField] private InventoryHost _host;

    [Tooltip("인벤토리 좌표·레이어 참조. 드롭 위치가 인벤토리 패널인지 판단하는 데 씀")]
    [SerializeField] private InventoryView _view;

    [Tooltip("드래그 중 보여줄 고스트 프리팹")]
    [SerializeField] private GameObject _ghostPrefab;

    [Tooltip("고스트를 담을 부모. craftingWindow처럼 크래프팅 UI 전체를 감싸는 상위 레이어여야 다른 패널 위에 그려진다")]
    [SerializeField] private RectTransform _ghostLayer;

    // 슬롯당 한 번만 만들어서 재사용하는 고스트
    private RectTransform _ghost;
    private Image _ghostImage;

    // 드래그 중 결과 슬롯 자신을 반투명하게 만드는 CanvasGroup
    private CanvasGroup _source;

    // 지금 드래그 중인 완성 파츠. CorrectRecipe.resultPart를 드래그 시작 시점에 캐시해 둔다
    private PartsDefinition _dragged;

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragged = _recipe.resultPart;

        // 조합이 안 맞아서 결과가 없으면 드래그할 게 없다. OnDrag/OnEndDrag도 _dragged == null이면 아무 일 안 함
        if (_dragged == null)
            return;

        _source = GetComponent<CanvasGroup>();
        if (_source == null)
            _source = gameObject.AddComponent<CanvasGroup>();
        _source.alpha = 0.35f;

        EnsureGhost();
        MoveGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragged == null)
            return;

        MoveGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 자동으로 빈자리를 찾아 넣는 게 아니라, 놓은 위치의 칸에 그대로 배치를 시도한다.
        // 그 칸이 막혀있거나 인벤토리 격자 범위 밖이면 TryPlacePartAt이 false를 반환하고,
        // 이 경우 재료 소모도 안 일어나고 결과 슬롯도 그대로 남는다 (원하는 칸을 다시 골라 드래그하면 됨)
        if (_dragged != null && TryGetDropOrigin(eventData, out Vector2Int origin) && _host.TryPlacePartAt(_dragged, origin))
        {
            _recipe.ConfirmCraft();      // 여기서 실제로 부품 재료가 소모되고 크래프팅 슬롯 9칸이 비워짐
            _resultSlot.SetPart(null);   // 결과 슬롯 아이콘도 비움 (인벤토리로 옮겨졌으니)
        }

        // 성공/실패 상관없이 드래그는 여기서 끝나므로 시각 상태는 항상 복구한다
        if (_source != null)
        {
            _source.alpha = 1f;
            _source = null;
        }

        HideGhost();
        _dragged = null;
    }

    // 포인터 위치를 인벤토리 파츠 레이어 기준 좌표로 바꾼 뒤, 칸 좌표(원점)로 변환한다.
    // InventoryDragController.ToCell과 같은 계산이다 - 파츠 레이어는 격자 크기에 맞춰져 있고 피벗이 좌하단이라
    // 셀 크기로 나눠 내림하면 바로 그 칸의 좌표가 나온다.
    // 인벤토리와 전혀 다른 화면 위치(크래프팅 존 등)에 놓아도 이 계산 자체는 항상 값을 내놓지만,
    // 그 좌표가 그리드 범위 밖이면 뒤이어 호출하는 TryPlacePartAt이 알아서 실패 처리한다
    private bool TryGetDropOrigin(PointerEventData eventData, out Vector2Int origin)
    {
        origin = default;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_view.PartsLayer, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return false;

        float size = Mathf.Max(1f, _view.CellSize);
        origin = new Vector2Int(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.y / size));
        return true;
    }

    private void EnsureGhost()
    {
        if (_ghost != null || _ghostPrefab == null)
            return;

        _ghost = (RectTransform)Instantiate(_ghostPrefab, _ghostLayer).transform;
        _ghostImage = _ghost.GetComponent<Image>();

        if (_ghostImage != null)
        {
            _ghostImage.raycastTarget = false;   // 고스트가 드롭 판정용 레이캐스트를 가리면 안 됨
            _ghostImage.sprite = _dragged.Sprite;
            _ghostImage.enabled = true;
        }

        _ghost.gameObject.SetActive(true);
        _ghost.SetAsLastSibling();   // 다른 패널들 위에 그려지도록 맨 뒤로
    }

    // 고스트를 포인터 위치로 옮긴다. 크래프팅 존/인벤토리 모두 격자 스냅 없이 픽셀 단위로 그대로 따라간다
    private void MoveGhost(PointerEventData eventData)
    {
        if (_ghost == null || _ghostLayer == null)
            return;

        // local은 _ghostLayer 피벗 기준 좌표
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_ghostLayer, eventData.position, eventData.pressEventCamera, out Vector2 local);

        // 고스트는 (0,0) 앵커/피벗인데 _ghostLayer는 그렇지 않을 수 있어서(예: 중앙 피벗),
        // 레이어 크기 * 피벗만큼 더해 "레이어 좌하단 기준" 좌표로 맞춘다
        Vector2 pivotOffset = new Vector2(
            _ghostLayer.rect.width * _ghostLayer.pivot.x,
            _ghostLayer.rect.height * _ghostLayer.pivot.y);

        // 고스트 크기만큼 왼쪽·아래로 밀어서, 마우스가 고스트의 오른쪽 위 모서리에 오게 함
        _ghost.anchoredPosition = local + pivotOffset - _ghost.rect.size;
    }

    private void HideGhost()
    {
        if (_ghost != null)
            _ghost.gameObject.SetActive(false);
    }
}
