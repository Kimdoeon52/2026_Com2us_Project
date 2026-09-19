using UnityEngine;

/// <summary>
/// 사이드뷰 격투 게임 카메라 (View 계층 — §1 최초 계획엔 없던 항목, 연출 담당으로 추가).
///
/// 격투 게임 표준 방식 — 카메라 중심은 항상 "두 파이터의 중간 지점".
/// closeZ(가장 가까움) ~ farZ(가장 멂) 사이에서 두 파이터가 화면에 딱 맞도록 줌한다.
///   - 서로 멀어지면 줌아웃 (즉시 — 파이터가 화면에 막히지 않게)
///   - 서로 붙으면 줌인 (부드럽게)
/// 최대 줌아웃에 도달하면 화면 좌우 끝선이 그대로 벽이 된다. ClampFighterX가 그 벽을 만든다.
///
/// 확장 지점(히트스톱/슬로우 등)은 아직 없는 시스템(StunSystem 등, §11-7)에 의존하므로 훅만 열어둔다.
/// </summary>
public class CombatCamera : MonoBehaviour
{
    [Header("추적 대상")]
    [Tooltip("카메라가 화면에 담을 첫 번째 파이터")]
    [SerializeField] private Transform playerA;
    [Tooltip("카메라가 화면에 담을 두 번째 파이터")]
    [SerializeField] private Transform playerB;

    [Header("맵 경계 — 카메라 중심이 여기서 멈춤")]
    [Tooltip("카메라 중심의 왼쪽 한계. 올리면(오른쪽으로) 카메라가 왼쪽 끝까지 덜 가서 왼쪽 맵 끝이 좁아지고, 내리면 넓어진다")]
    [SerializeField] private float minX = -10f;
    [Tooltip("카메라 중심의 오른쪽 한계. 올리면 오른쪽 맵 끝이 넓어지고, 내리면 좁아진다")]
    [SerializeField] private float maxX = 10f;

    [Header("프레임 — 카메라 줌 계산용")]
    [Tooltip("두 파이터 바깥 좌우로 남길 여백. 올리면 캐릭터가 화면 끝에서 멀어지고 줌아웃이 빨라져 캐릭터가 작게 보인다. 내리면 캐릭터가 화면 끝에 붙고 더 크게 보인다")]
    [SerializeField] private float framingPadding = 2f;

    [Header("줌 범위 — Perspective (카메라 Z. 0에 가까울수록 가깝고, 더 음수일수록 멀다)")]
    [Tooltip("가장 가까운 줌(최대 줌인) 카메라 Z. 0에 가깝게 올리면 더 확대되고, 더 음수로 내리면 덜 확대된다. farZ보다 커야 한다")]
    [SerializeField] private float closeZ = -6f;
    [Tooltip("가장 먼 줌(최대 줌아웃) 카메라 Z. 더 음수로 내리면 화면이 넓어져 캐릭터가 더 멀리 벌어질 수 있고, 0에 가깝게 올리면 화면이 좁아져 활동 폭(벽)이 줄어든다")]
    [SerializeField] private float farZ = -14f;

    [Header("줌 범위 — Orthographic (Size 기반. Perspective 카메라에서는 쓰이지 않음)")]
    [Tooltip("최대 줌인 크기. 작을수록 더 확대된다")]
    [SerializeField] private float minOrthoSize = 3f;
    [Tooltip("최대 줌아웃 크기. 클수록 화면이 넓어져 활동 폭(벽)이 늘어난다")]
    [SerializeField] private float maxOrthoSize = 8f;

    [Header("부드러움 — 줌아웃은 항상 즉시")]
    [Tooltip("카메라 좌우 이동 지연(초). 올리면 카메라가 느리게 따라가서 캐릭터가 화면 끝에서 카메라를 기다린다. 0에 가까우면 즉시 따라간다")]
    [SerializeField] private float positionSmoothTime = 0.05f;
    [Tooltip("줌인(가까워질 때 확대) 지연(초). 올리면 확대가 천천히 이루어진다. 줌아웃에는 영향 없음")]
    [SerializeField] private float zoomInSmoothTime = 0.2f;

    [Tooltip("카메라 높이. 올리면 카메라가 위로 이동해 캐릭터가 화면 아래쪽에 보이고, 내리면 캐릭터가 화면 위쪽으로 올라간다")]
    [SerializeField] private float fixedY = 5f;

    [Header("이동 제한")]
    [Tooltip("화면 좌우 끝선에서 파이터 중심이 멈추는 거리. 올리면 캐릭터가 화면 끝에서 안쪽에서 멈추고, 0이면 캐릭터 중심이 끝선까지 간다. 몸 반폭 정도가 적당")]
    [SerializeField] private float fighterEdgeInset = 0.25f;

    private Camera cam;
    private Vector3 positionVelocity;
    private float zoomVelocity;

    // ---- 확장 지점 — 나중에 다른 시스템이 호출 ----
    private bool isFrozen;
    private float extraZoomOffset;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            Debug.LogWarning("[CombatCamera] 같은 오브젝트에 Camera 컴포넌트가 없음 — 줌/이동 제한 불가");
    }

    // 호출: Unity(모든 Update 이후). 이동이 끝난 파이터 위치로 카메라 이동/줌을 정한다
    private void LateUpdate()
    {
        if (cam == null || playerA == null || playerB == null || isFrozen) return;

        float distance = Mathf.Abs(playerA.position.x - playerB.position.x);
        float requiredHalfWidth = distance / 2f + framingPadding;

        float desiredX = Mathf.Clamp((playerA.position.x + playerB.position.x) / 2f, minX, maxX);
        Vector3 desiredPosXY = new Vector3(desiredX, fixedY, transform.position.z);
        Vector3 smoothedXY = Vector3.SmoothDamp(transform.position, desiredPosXY, ref positionVelocity, positionSmoothTime);

        if (cam.orthographic)
        {
            float targetSize = Mathf.Clamp(requiredHalfWidth / cam.aspect, minOrthoSize, maxOrthoSize) + extraZoomOffset;
            bool isZoomOut = targetSize > cam.orthographicSize;

            cam.orthographicSize = FollowZoom(cam.orthographicSize, targetSize, isZoomOut);
            transform.position = smoothedXY;
        }
        else
        {
            float halfWidthPerUnit = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            float requiredDistance = requiredHalfWidth / halfWidthPerUnit;
            float targetZ = Mathf.Clamp(playerA.position.z - requiredDistance, farZ, closeZ) + extraZoomOffset;
            bool isZoomOut = targetZ < transform.position.z;

            float zoomedZ = FollowZoom(transform.position.z, targetZ, isZoomOut);
            transform.position = new Vector3(smoothedXY.x, smoothedXY.y, zoomedZ);
        }
    }

    /// <summary>줌아웃은 즉시 따라가고(파이터가 화면에 막히지 않게), 줌인만 부드럽게 따라간다</summary>
    private float FollowZoom(float current, float target, bool isZoomOut)
    {
        if (isZoomOut)
        {
            zoomVelocity = 0f;
            return target;
        }
        return Mathf.SmoothDamp(current, target, ref zoomVelocity, zoomInSmoothTime);
    }

    /// <summary>히트스톱 — 카메라 갱신 정지. §3 원칙대로 timeScale이 아니라 이 플래그로 처리.
    /// StunSystem/HitDetection이 히트 순간 true→(정지 프레임 수 지나면)false로 호출하게 될 자리.</summary>
    public void SetFrozen(bool frozen) => isFrozen = frozen;

    /// <summary>추가 줌 보정 — Orthographic이면 Size에, Perspective면 Z에 더해진다.
    /// 나중에 콤보/필살기 연출 등에서 호출해 확장 가능.</summary>
    public void SetZoomOffset(float offset) => extraZoomOffset = offset;

    // ---- 캐릭터 이동 제한 — 카메라에 지금 보이는 화면의 좌우 끝선이 그대로 투명 벽이다 ----

    /// <summary>지금 이 순간 카메라가 파이터 평면에서 보여 주는 화면 반폭</summary>
    private float CurrentVisibleHalfWidth()
    {
        if (cam.orthographic) return cam.orthographicSize * cam.aspect;

        float distance = Mathf.Abs(transform.position.z - playerA.position.z);
        return Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance * cam.aspect;
    }

    /// <summary>이동하려는 x를 현재 화면 좌우 끝선 안으로 보정해서 돌려준다</summary>
    // 호출: RobotMover.Update. 받음: 가고 싶은 x. 반환: 현재 화면 좌우 끝선 안으로 보정된 x
    public float ClampFighterX(float desiredX)
    {
        if (cam == null || playerA == null) return desiredX;

        float halfWidth = CurrentVisibleHalfWidth();
        float centerX = transform.position.x;
        return Mathf.Clamp(desiredX, centerX - halfWidth + fighterEdgeInset, centerX + halfWidth - fighterEdgeInset);
    }
}
