using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 맵UI의 아이콘 이미지마다 삽입하는 코드.
/// </summary>

public class MapElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("이동지점 정보")]
    public string elementName; // 이동지점 이름
    public int useHours; // 사용시간. 인스펙터 상에서 작성
    public Transform teleportPoint; // 씬 내의 도착 지점 (빈 게임오브젝트의 Transform)

    [Header("위치 아이콘")]
    private Image locationImage; // 해당 위치 아이콘
    public Color highlightColor = new Color(1f, 1f, 1f, 1f); // 밝은 하이라이트 색상
    private Color normalColor = new Color(0.7f, 0.7f, 0.7f, 1f); // 평상시 약간 어두운 색상

    private void Awake()
    {
        locationImage = GetComponent<Image>();
        locationImage.color = normalColor;
    }

    // 마우스를 올렸을 때 (호버링) - 밝게 하이라이트
    public void OnPointerEnter(PointerEventData eventData)
    {
        locationImage.color = highlightColor;
    }

    // 마우스를 뗐을 때 - 원래 색상 복귀
    public void OnPointerExit(PointerEventData eventData)
    {
        locationImage.color = normalColor;
    }

    // 아이콘을 클릭했을 때 - 확인 팝업 호출
    public void OnPointerClick(PointerEventData eventData)
    {
        MapSystemManager.Instance.OpenConfirmPopup(this);
    }
}
