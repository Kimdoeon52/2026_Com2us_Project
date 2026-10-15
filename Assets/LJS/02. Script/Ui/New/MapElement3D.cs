using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 각 3D 오브젝트 그룹에 붙는 스크립트.
/// </summary>
public class MapElement3D : MonoBehaviour
{
    public static event Action<MapElement3D, Vector3> OnHovered3D;
    public static event Action OnUnhovered3D;

    [Header("맵 이동지점 정보")]
    public string elementName;
    public int useHours;
    public string targetSceneName;

    [Header("하이라이트")]
    public Color elementHighlight = new Color(1.2f, 1.2f, 1.2f); // 밝아지는 정도 (기본 색상에 곱해짐)

    // 자식들의 메쉬와 원래 색상을 기억할 딕셔너리
    private Dictionary<MeshRenderer, Color> originalColors = new Dictionary<MeshRenderer, Color>();

    private void Awake()
    {
        // 하위에 있는 모든 Box(MeshRenderer)를 찾아서 원래 색상 저장
        MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in childRenderers)
        {
            originalColors[renderer] = renderer.material.color;
        }
    }

    public void HoverOn()
    {
        foreach (var kvp in originalColors)
        {
            kvp.Key.material.color = kvp.Value * elementHighlight; // 원래 색상보다 밝게 조절
        }

        // 툴팁을 띄우기 위해 내 3D 월드 좌표(transform.position)를 방송
        OnHovered3D?.Invoke(this, transform.position);
    }

    public void HoverOff()
    {
        foreach (var kvp in originalColors)
        {
            kvp.Key.material.color = kvp.Value; // 원래 색상으로 복구
        }
        OnUnhovered3D?.Invoke();
    }

    private void OnDisable()
    {
        HoverOff();
    }
}
