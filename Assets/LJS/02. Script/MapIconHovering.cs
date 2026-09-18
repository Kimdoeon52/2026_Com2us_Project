using TMPro;
using UnityEngine;

public class MapIconHovering : MonoBehaviour
{
    [Header("Hover UI References")]
    public CanvasGroup hoverGroup;
    public TextMeshProUGUI hoverText;

    [Header("Position Settings")]
    public float yOffset = 50f;

    private void Awake()
    {
        hoverGroup.alpha = 0f;
        hoverGroup.interactable = false;
        hoverGroup.blocksRaycasts = false;
    }

    // 스크립트가 켜질 때 이벤트 구독
    private void OnEnable()
    {
        MapElement.OnHovered += ShowTooltip;
        MapElement.OnUnhovered += HideTooltip;
        MapElement.OnClicked += HideTooltipOnPopup;
        MapSystemManager.OnMapClosed += HideTooltip;
    }

    // 스크립트가 꺼질 때 메모리 누수 방지를 위해 이벤트 구독 해제
    private void OnDisable()
    {
        MapElement.OnHovered -= ShowTooltip;
        MapElement.OnUnhovered -= HideTooltip;
    }

    // 이벤트 발생 시 자동으로 실행될 함수
    private void ShowTooltip(MapElement element, Vector3 iconPosition)
    {
        hoverText.text = $"지역 '{element.elementName}'\n({element.useHours}시간 소요)";
        hoverGroup.transform.position = iconPosition + new Vector3(0, yOffset, 0);
        hoverGroup.alpha = 1f;
    }

    private void HideTooltip() { hoverGroup.alpha = 0f; }
    private void HideTooltipOnPopup(MapElement element) { HideTooltip(); } // 이벤트용 래퍼함수
}
