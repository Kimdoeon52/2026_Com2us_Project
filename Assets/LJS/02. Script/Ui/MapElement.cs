using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 맵UI의 아이콘 이미지마다 삽입하는 코드.
/// </summary>

[RequireComponent(typeof(Image))]
public class MapElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // MapIconHovering이 받는 이벤트 액션
    public static event Action<MapElement, Vector3> OnHovered;
    public static event Action OnUnhovered;
    public static event Action<MapElement> OnClicked; // 해당 아이콘 클릭시 발생 이벤트

    [Header("이동지점 정보")]
    public string elementName; // 이동지점 이름
    public int useHours; // 사용시간. 인스펙터 상에서 작성
    public Transform teleportPoint; // 씬 내의 도착 지점 (빈 게임오브젝트의 Transform)

    private Image locationImage; // 해당 위치 아이콘

    private Vector3 normalScale = Vector3.one; // 마우스 호버링시 커졌다 작아짐
    private Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1.1f);

    private void Awake()
    {
        locationImage = GetComponent<Image>();
        //locationImage.color = Color.white;
    }

    // 마우스를 올렸을 때 - 밝게 하이라이트
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (MapSystemManager.Instance.isConfirmOpen) return;
        transform.localScale = hoverScale;
        OnHovered?.Invoke(this, transform.position);
    }

    // 마우스를 뗐을 때 - 원래 색상 복귀
    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = normalScale;
        OnUnhovered?.Invoke();
    }

    // 아이콘을 클릭했을 때 - 팝업 알림
    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapSystemManager.Instance.isConfirmOpen) return; // 선택지 켜져있으면 리턴

        transform.localScale = normalScale;
        OnUnhovered?.Invoke();

        OnClicked?.Invoke(this);
    }

    private void OnDisable()
    {
        transform.localScale = normalScale; // 크기를 원래대로 복구
        OnUnhovered?.Invoke(); // 혹시 남아있을지 모를 툴팁도 끄라고 방송
    }
}
