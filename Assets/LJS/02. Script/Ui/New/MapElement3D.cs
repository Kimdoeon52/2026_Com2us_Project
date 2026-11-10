using System;
using UnityEngine;

/// <summary>
/// 각 3D 오브젝트에 붙는 스크립트.
/// </summary>
public class MapElement3D : MonoBehaviour
{
    public static event Action<MapElement3D, Vector3> OnHovered3D;
    public static event Action OnUnhovered3D;

    [Header("맵 이동지점 정보")]
    public string elementName;
    public int useHours;
    public string targetSceneName;

    [Header("하이라이트 메테리얼")]
    public Material hoverMaterial;

    // 자신의 렌더러와 원래 메테리얼을 기억할 변수
    private MeshRenderer myRenderer;
    private Material originalMaterial;

    private void Awake()
    {
        // 자신의 오브젝트에 있는 MeshRenderer만 가져옵니다.
        myRenderer = GetComponent<MeshRenderer>();

        if (myRenderer != null)
        {
            // 원래 메테리얼 저장 (sharedMaterial을 사용하여 복제 방지)
            originalMaterial = myRenderer.sharedMaterial;
        }
    }

    public void HoverOn()
    {
        // 내 렌더러의 메테리얼을 교체
        if (myRenderer != null && hoverMaterial != null)
        {
            myRenderer.material = hoverMaterial;
        }

        // 툴팁을 띄우기 위해 내 3D 월드 좌표(transform.position)를 방송
        OnHovered3D?.Invoke(this, transform.position);
    }

    public void HoverOff()
    {
        // 내 렌더러의 메테리얼을 원래대로 복구
        if (myRenderer != null && originalMaterial != null)
        {
            myRenderer.material = originalMaterial;
        }

        OnUnhovered3D?.Invoke();
    }

    private void OnDisable()
    {
        HoverOff();
    }
}