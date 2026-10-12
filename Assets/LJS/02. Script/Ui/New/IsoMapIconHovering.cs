using TMPro;
using UnityEngine;
using DG.Tweening;

public class IsoMapIconHovering : MonoBehaviour
{
    [Header("호버 UI 참조")]
    public CanvasGroup hoverGroup;
    public TextMeshProUGUI hoverText;

    [Header("UI 위치값")]
    public Vector2 offset = new Vector2(0, 50f); // 3D -> 2D 변환 후의 오프셋

    [Header("도트윈 세팅")]
    public float fadeDuration = 0.3f;

    private Tween fadeTween;

    private void Awake()
    {
        hoverGroup.alpha = 0f;
        hoverGroup.interactable = false;
        hoverGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        MapElement3D.OnHovered3D += ShowTooltip;
        MapElement3D.OnUnhovered3D += HideTooltip;
        IsoMapSystemManager.OnMapClosed += HideTooltip;
    }

    private void OnDisable()
    {
        MapElement3D.OnHovered3D -= ShowTooltip;
        MapElement3D.OnUnhovered3D -= HideTooltip;
        IsoMapSystemManager.OnMapClosed -= HideTooltip;
        fadeTween?.Kill();
    }

    private void ShowTooltip(MapElement3D element, Vector3 worldPosition)
    {
        hoverText.text = $"지역 '{element.elementName}'\n({element.useHours}시간 소요)";

        Vector2 screenPosition = IsoMapSystemManager.Instance.mapCamera.WorldToScreenPoint(worldPosition);
        hoverGroup.transform.position = screenPosition + offset;

        fadeTween?.Kill();
        fadeTween = hoverGroup.DOFade(1f, fadeDuration).SetUpdate(true); 
        // 시간 정지 상태에서도 작동하도록 SetUpdate(true)
    }

    private void HideTooltip() 
    {
        fadeTween?.Kill();
        fadeTween = hoverGroup.DOFade(0f, fadeDuration).SetUpdate(true);
    }
}
