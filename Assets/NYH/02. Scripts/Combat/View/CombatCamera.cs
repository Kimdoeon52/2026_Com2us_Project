using UnityEngine;

/// <summary>
/// 사이드뷰 격투 게임 카메라 (View 계층 — §1 최초 계획엔 없던 항목, 연출 담당으로 추가).
///
/// 격투 게임 표준 방식 — 카메라 중심은 항상 "두 파이터의 중간 지점".
/// 둘 사이 거리에 따라 자동으로 줌인/줌아웃한다.
///   - 서로 멀어지면 카메라가 뒤로 빠지며(줌아웃) 둘 다 화면에 담는다
///   - 서로 붙으면 다시 줌인해서 타격감을 살린다
/// (SF6 등 참고 — 유저 제공 레퍼런스)
///
/// 확장 지점(히트스톱/슬로우 등)은 아직 없는 시스템(StunSystem 등, §11-7)에 의존하므로 훅만 열어둔다.
/// </summary>
public class CombatCamera : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform playerA;
    [SerializeField] private Transform playerB;

    [Header("맵 경계 — 카메라 중심만 멈춤, 캐릭터는 계속 이동 가능")]
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 10f;

    [Header("듀얼 타겟 프레이밍")]
    [Tooltip("두 파이터 좌우로 남길 여백")]
    [SerializeField] private float framingPadding = 2f;
    [Tooltip("이 거리 이상 벌어지면 Perspective 줌이 최대치(farZ)에 도달")]
    [SerializeField] private float maxFramingDistance = 8f;

    [Header("줌 범위 — Perspective (거리 dolly, '뒤로 빠지는' 연출)")]
    [SerializeField] private float closeZ = -6f;
    [SerializeField] private float farZ = -14f;

    [Header("줌 범위 — Orthographic (Size 기반, 둘 다 정확히 화면에 담기게 계산)")]
    [SerializeField] private float minOrthoSize = 3f;
    [SerializeField] private float maxOrthoSize = 8f;

    [Header("부드러움")]
    [SerializeField] private float positionSmoothTime = 0.2f;
    [SerializeField] private float zoomSmoothTime = 0.5f;

    [SerializeField] private float fixedY = 5f;

    private Camera cam;
    private Vector3 positionVelocity;
    private float zVelocity;
    private float orthoSizeVelocity;

    // ---- 확장 지점 — 나중에 다른 시스템이 호출 ----
    private bool isFrozen;
    private float extraZoomOffset;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            Debug.LogWarning("[CombatCamera] 같은 오브젝트에 Camera 컴포넌트가 없음 — 줌 자동 적용 불가");
    }

    private void LateUpdate()
    {
        if (playerA == null || playerB == null || isFrozen) return;

        float desiredX = Mathf.Clamp((playerA.position.x + playerB.position.x) / 2f, minX, maxX);
        float distance = Mathf.Abs(playerA.position.x - playerB.position.x);

        Vector3 desiredPosXY = new Vector3(desiredX, fixedY, transform.position.z);
        Vector3 smoothedXY = Vector3.SmoothDamp(transform.position, desiredPosXY, ref positionVelocity, positionSmoothTime);

        if (cam != null && cam.orthographic)
        {
            float requiredHalfWidth = (distance / 2f) + framingPadding;
            float targetSize = cam.aspect > 0.01f ? requiredHalfWidth / cam.aspect : requiredHalfWidth;
            targetSize = Mathf.Clamp(targetSize, minOrthoSize, maxOrthoSize) + extraZoomOffset;

            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref orthoSizeVelocity, zoomSmoothTime);
            transform.position = smoothedXY;
        }
        else
        {
            float zoomT = Mathf.InverseLerp(0f, maxFramingDistance, distance);
            float targetZ = Mathf.Lerp(closeZ, farZ, zoomT) + extraZoomOffset;

            float smoothedZ = Mathf.SmoothDamp(transform.position.z, targetZ, ref zVelocity, zoomSmoothTime);
            transform.position = new Vector3(smoothedXY.x, smoothedXY.y, smoothedZ);
        }
    }

    /// <summary>히트스톱 — 카메라 갱신 정지. §3 원칙대로 timeScale이 아니라 이 플래그로 처리.
    /// StunSystem/HitDetection이 히트 순간 true→(정지 프레임 수 지나면)false로 호출하게 될 자리.</summary>
    public void SetFrozen(bool frozen) => isFrozen = frozen;

    /// <summary>추가 줌 보정 — Orthographic이면 Size에, Perspective면 Z에 더해진다.
    /// 나중에 콤보/필살기 연출 등에서 호출해 확장 가능.</summary>
    public void SetZoomOffset(float offset) => extraZoomOffset = offset;
}
