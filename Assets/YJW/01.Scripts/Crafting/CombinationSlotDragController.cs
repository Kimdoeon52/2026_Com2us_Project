using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 크래프팅 슬롯 9칸끼리 부품을 드래그로 재배치한다.
// 인벤토리 격자와 달리 칸 크기가 정해진 그리드가 아니라서, 셀 스냅 없이 포인터를 그대로 따라가는 고스트를 쓴다.
// 이 스크립트는 슬롯 1개당 1개씩 붙는다 (9개 슬롯 = 9개 인스턴스, 각자 자기 슬롯만 안다).
public class CombinationSlotDragController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("이 컨트롤러가 속한 슬롯. 비우면 Reset에서 같은 오브젝트의 CombinationSlot을 자동으로 채운다")]
    [SerializeField] private CombinationSlot _slot;

    [Tooltip("드래그 중 보여줄 고스트 프리팹. 비우면 고스트 없이(안 보이는 채로) 드래그된다")]
    [SerializeField] private GameObject _ghostPrefab;

    [Tooltip("고스트를 담을 부모. 슬롯들의 형제 레이어(예: craftingWindow)여야 슬롯들 위에 그려진다. GridLayoutGroup이 붙은 오브젝트(CombinePanel 등)에 연결하면 고스트도 그리드 칸으로 취급되어 위치가 깨지니 주의")]
    [SerializeField] private RectTransform _ghostLayer;

    [Tooltip("슬롯 내용이 바뀐 뒤 레시피를 다시 검사시키기 위한 참조")]
    [SerializeField] private CorrectRecipe _recipe;

    // 실제로 화면에 떠 있는 고스트. 슬롯당 하나만 만들고 재사용한다 (드래그마다 새로 Instantiate하지 않음)
    private RectTransform _ghost;
    private Image _ghostImage;

    // 드래그 중 원본 슬롯을 반투명하게 만드는 데 쓰는 CanvasGroup. 드래그가 끝나면 다시 1로 되돌린다
    private CanvasGroup _source;

    // 지금 드래그 중인 부품. 드래그 시작 시점의 슬롯 내용을 캐시해 둔다 (드래그 중 슬롯 내용이 바뀌는 일은 없지만, 매 프레임 _slot.Component를 다시 읽지 않으려는 목적도 있음)
    private ComponentDefinition _dragged;

    // 인스펙터에서 _slot을 안 채우고 컴포넌트를 추가하면 자동으로 같은 오브젝트의 CombinationSlot을 찾아 채운다
    private void Reset()
    {
        if (_slot == null)
            _slot = GetComponent<CombinationSlot>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragged = _slot.Component;   // 비어있으면 null

        // 빈 슬롯을 잡은 거면 드래그할 게 없으니 여기서 끝. OnDrag/OnEndDrag도 _dragged == null 가드로 아무 일 안 함
        if (_dragged == null)
            return;

        // 드래그 중엔 원본이 그 자리에 남아있다는 걸 시각적으로 보여주기 위해 반투명 처리
        _source = _slot.GetOrAddComponent<CanvasGroup>();
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
        if (_dragged != null)
        {
            CombinationSlot target = FindTargetSlot(eventData);

            if (target != null && target != _slot)
            {
                // 다른 슬롯 위에 놓였다. 그 슬롯이 이미 차 있어도 상관없이 서로 내용을 맞바꾼다 (스왑)
                // target이 비어있었다면 other는 null이 되고, 그게 그대로 _slot.SetComponent(null)로 들어가 원래 자리가 빈다
                ComponentDefinition other = target.Component;
                target.SetComponent(_dragged);
                _slot.SetComponent(other);

                // 배치가 바뀌었으니 지금 9칸 조합이 레시피와 맞는지 다시 확인시킨다
                _recipe.PushPartData();
            }
            // target이 null(빈 곳에 놓임)이거나 자기 자신이면 아무것도 안 하고 원래 자리 그대로 둔다
        }

        // 원본 슬롯 다시 선명하게
        if (_source != null)
        {
            _source.alpha = 1f;
            _source = null;
        }

        HideGhost();
        _dragged = null;
    }

    // 포인터 바로 아래(또는 그 부모 쪽)에서 CombinationSlot을 찾는다.
    // 실제로 레이캐스트에 잡히는 건 슬롯의 아이콘 Image 같은 자식이라, GetComponentInParent로 한 단계씩 올라가며 찾는다
    private CombinationSlot FindTargetSlot(PointerEventData eventData)
    {
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        return hit != null ? hit.GetComponentInParent<CombinationSlot>() : null;
    }

    // 고스트는 슬롯당 한 번만 만들어서 재사용한다 (드래그 시작마다 Instantiate/Destroy 안 함)
    private void EnsureGhost()
    {
        if (_ghost != null || _ghostPrefab == null)
            return;

        _ghost = (RectTransform)Instantiate(_ghostPrefab, _ghostLayer).transform;
        _ghostImage = _ghost.GetComponent<Image>();

        if (_ghostImage != null)
        {
            _ghostImage.raycastTarget = false;   // 고스트가 드롭 대상 레이캐스트를 가리면 안 됨
            _ghostImage.sprite = _dragged.Sprite;
            _ghostImage.enabled = true;
        }

        _ghost.gameObject.SetActive(true);
        _ghost.SetAsLastSibling();   // 형제들(다른 패널) 위에 그려지도록 맨 뒤로
    }

    // 고스트를 포인터 위치로 옮긴다. 그리드가 아니므로 스냅 없이 픽셀 단위로 그대로 따라간다
    private void MoveGhost(PointerEventData eventData)
    {
        if (_ghost == null || _ghostLayer == null)
            return;

        // local은 _ghostLayer의 피벗을 (0,0) 기준으로 한 좌표
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_ghostLayer, eventData.position, eventData.pressEventCamera, out Vector2 local);

        // 고스트의 앵커/피벗은 (0,0)(좌하단)인데 _ghostLayer 피벗은 그렇지 않을 수 있어서(예: craftingWindow는 중앙 피벗),
        // 레이어 크기 * 피벗만큼 더해 "레이어 좌하단 기준" 좌표로 보정한다
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
