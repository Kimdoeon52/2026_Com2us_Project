using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 드론 3기의 장전/소모(자원 유무) 상태를 3개의 UI Image 색상으로 표시하는 컴포넌트.
/// DroneUI 빈 오브젝트에 부착한다.
/// </summary>
public class DroneUI : MonoBehaviour
{
    [Header("연결 대상")]
    [Tooltip("플레이어 UnitControllHub(동적 생성 시 Bind()를 호출해 연결)")]
    [SerializeField] private UnitControllHub targetHub;

    [Header("드론 UI 이미지")]
    [SerializeField] private Image[] droneImages;

    [Header("상태별 색상")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    private UnitControllHub boundHub;

    private void Start()
    {
        if (droneImages == null || droneImages.Length != CombatantSnapshot.MaxCylinder)
            Debug.LogWarning($"[DroneUI] {name}: droneImages 배열 크기({droneImages?.Length ?? 0})가 MaxCylinder({CombatantSnapshot.MaxCylinder})와 다릅니다.");

        if (targetHub == null)
            targetHub = FindFirstObjectByType<UnitControllHub>();

        if (targetHub != null)
            Bind(targetHub);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void Bind(UnitControllHub hub)
    {
        if (hub == null) return;

        Unbind();
        boundHub = hub;
        boundHub.OnDroneChargeChanged += HandleDroneChargeChanged;

        RefreshAll();
    }

    private void Unbind()
    {
        if (boundHub != null)
        {
            boundHub.OnDroneChargeChanged -= HandleDroneChargeChanged;
            boundHub = null;
        }
    }

    private void HandleDroneChargeChanged(int slotIndex, bool isCharged)
    {
        ApplyColor(slotIndex, isCharged);
    }

    public void RefreshAll()
    {
        if (droneImages == null) return;

        for (int i = 0; i < droneImages.Length; i++)
        {
            bool isCharged = boundHub != null
                             && i < boundHub.Drones.Count
                             && boundHub.Drones[i] != null
                             && boundHub.Drones[i].IsCharged;

            ApplyColor(i, isCharged);
        }
    }

    private void ApplyColor(int slotIndex, bool isCharged)
    {
        if (droneImages == null || slotIndex < 0 || slotIndex >= droneImages.Length) return;
        if (droneImages[slotIndex] == null) return;

        droneImages[slotIndex].color = isCharged ? activeColor : inactiveColor;
    }
}