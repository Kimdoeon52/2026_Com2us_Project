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
    // SmoothDamp 함수가 "지금 속도가 얼마인지"를 계속 참조해야 부드러운 감속 곡선을 그릴 수 있어서,
    // 결과값 자체보다는 SmoothDamp 내부 계산용으로 매 프레임 갱신되는 상태값이다(ref로 넘겨줌)
    private Vector3 positionVelocity;
    private float zoomVelocity;

    // ---- 확장 지점 — 나중에 다른 시스템이 호출 ----
    private bool isFrozen;      // 히트스톱 등으로 카메라를 잠깐 멈추고 싶을 때 켜는 플래그
    private float extraZoomOffset; // 필살기 연출 등에서 "기본 계산된 줌 값에 추가로 더 당기고 싶을 때" 쓰는 보정치

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            Debug.LogWarning("[CombatCamera] 같은 오브젝트에 Camera 컴포넌트가 없음 — 줌/이동 제한 불가");
    }

    // 호출: Unity(모든 Update 이후). 이동이 끝난 파이터 위치로 카메라 이동/줌을 정한다
    // Update가 아니라 LateUpdate를 쓰는 이유: RobotMover.Update()에서 캐릭터가 다 이동한 "이후"의
    // 최종 위치를 기준으로 카메라를 맞춰야 한다. Update에서 카메라를 먼저 움직이면, 아직 그 프레임에
    // 이동하지 않은 "한 프레임 전" 캐릭터 위치를 쫓아가게 되어 살짝 밀리는 느낌이 생긴다
    private void LateUpdate()
    {
        if (cam == null || playerA == null || playerB == null || isFrozen) return;

        // 두 파이터가 얼마나 떨어져 있는지, 그리고 그 거리 + 여백(framingPadding)만큼을 화면에
        // 다 담으려면 카메라 중심에서 좌우로 얼마나 넓게 보여야 하는지를 구한다 (필요한 "반쪽 너비")
        float distance = Mathf.Abs(playerA.position.x - playerB.position.x);
        float requiredHalfWidth = distance / 2f + framingPadding;

        // 카메라가 봐야 할 x축 중심은 두 파이터의 정중앙. Clamp로 minX~maxX(맵 경계) 밖으로는 못 나가게 막는다
        float desiredX = Mathf.Clamp((playerA.position.x + playerB.position.x) / 2f, minX, maxX);
        Vector3 desiredPosXY = new Vector3(desiredX, fixedY, transform.position.z);
        // SmoothDamp로 목표 위치를 향해 부드럽게 따라가게 한다 — positionSmoothTime이 짧을수록 즉각 반응
        Vector3 smoothedXY = Vector3.SmoothDamp(transform.position, desiredPosXY, ref positionVelocity, positionSmoothTime);

        // 카메라 설정이 Orthographic(평행 투영)인지 Perspective(원근 투영)인지에 따라 "줌"의 의미가
        // 다르다 — Ortho는 orthographicSize(보이는 세로 절반 높이)를, Perspective는 카메라와 피사체
        // 사이의 거리(Z)를 조절해야 화면에 담기는 범위가 넓어지거나 좁아진다. 그래서 분기 처리함
        if (cam.orthographic)
        {
            // 화면 가로세로 비율(aspect)로 나눠야 "가로로 requiredHalfWidth만큼 보이게 하는 세로 절반 크기"가 나온다
            float targetSize = Mathf.Clamp(requiredHalfWidth / cam.aspect, minOrthoSize, maxOrthoSize) + extraZoomOffset;
            bool isZoomOut = targetSize > cam.orthographicSize; // 목표 크기가 지금보다 크면 = 더 넓게 보여야 함 = 줌아웃

            cam.orthographicSize = FollowZoom(cam.orthographicSize, targetSize, isZoomOut);
            transform.position = smoothedXY;
        }
        else
        {
            // Perspective 카메라에서 "카메라로부터 거리 d만큼 떨어진 곳에서 화면에 보이는 절반 너비"를
            // 구하는 공식: tan(수직 FOV의 절반) * 거리 * 화면비. 여기서는 역으로 "원하는 절반 너비를
            // 보여주려면 거리가 얼마나 필요한가"를 구하기 위해 이 비율(halfWidthPerUnit, 거리 1당 보이는 절반 너비)을 먼저 구한다
            float halfWidthPerUnit = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            float requiredDistance = requiredHalfWidth / halfWidthPerUnit; // 필요한 절반 너비를, 단위 거리당 너비로 나누면 필요한 실제 거리가 나옴
            // 카메라는 Z축 음의 방향으로 멀어질수록 캐릭터에서 멀어진다고 가정 — 그래서 playerA.position.z에서 requiredDistance만큼 뺀 값을 목표 Z로 삼는다
            float targetZ = Mathf.Clamp(playerA.position.z - requiredDistance, farZ, closeZ) + extraZoomOffset;
            bool isZoomOut = targetZ < transform.position.z; // Z가 더 작아진다(더 음수 방향) = 카메라가 더 멀어진다 = 줌아웃

            float zoomedZ = FollowZoom(transform.position.z, targetZ, isZoomOut);
            transform.position = new Vector3(smoothedXY.x, smoothedXY.y, zoomedZ);
        }
    }

    /// <summary>줌아웃은 즉시 따라가고(파이터가 화면에 막히지 않게), 줌인만 부드럽게 따라간다</summary>
    // 왜 줌아웃과 줌인의 속도를 다르게 하는가: 줌아웃이 느리면(부드럽게 따라가면), 두 캐릭터가
    // 갑자기 멀어졌을 때(백스핀 엘보우 넉백 등) 화면이 못 따라가서 한쪽 캐릭터가 화면 밖으로
    // 잘려 보이는 상황이 생길 수 있다 — 그건 격투 게임에서 치명적인 UX 문제라 줌아웃만큼은 무조건 즉시 반영한다.
    // 반대로 줌인은 즉시 하면 화면이 너무 정신없이 확대·축소를 반복해서 어지러워 보이므로 부드럽게 둔다
    private float FollowZoom(float current, float target, bool isZoomOut)
    {
        if (isZoomOut)
        {
            zoomVelocity = 0f; // 다음에 줌인으로 바뀔 때 이전 줌아웃 관성이 안 남게 속도를 리셋
            return target; // 보간 없이 목표값을 그대로 반환 = 즉시 적용
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
    // 이 값이 필요한 이유: 캐릭터가 화면 밖으로 나가지 못하게 막으려면(ClampFighterX), "지금 화면에
    // 실제로 보이는 범위가 어디까지인지"부터 알아야 한다. 카메라 설정(Ortho/Perspective)에 따라
    // 계산 방식이 다른 건 위 LateUpdate의 줌 계산과 같은 이유
    private float CurrentVisibleHalfWidth()
    {
        if (cam.orthographic) return cam.orthographicSize * cam.aspect;

        // 카메라와 캐릭터가 있는 평면(Z) 사이의 실제 거리를 구해서, 그 거리에서 보이는 절반 너비를 계산
        float distance = Mathf.Abs(transform.position.z - playerA.position.z);
        return Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance * cam.aspect;
    }

    /// <summary>이동하려는 x를 현재 화면 좌우 끝선 안으로 보정해서 돌려준다</summary>
    // 호출: RobotMover.Update. 받음: 가고 싶은 x. 반환: 현재 화면 좌우 끝선 안으로 보정된 x
    public float ClampFighterX(float desiredX)
    {
        if (cam == null || playerA == null) return desiredX; // 카메라가 없으면 제한할 방법이 없으니 원하는 값 그대로 통과

        float halfWidth = CurrentVisibleHalfWidth();
        float centerX = transform.position.x;
        // fighterEdgeInset만큼 화면 끝에서 안쪽으로 들여서 제한한다 — 0으로 두면 캐릭터의 "중심점"이
        // 화면 끝까지 가버려서, 캐릭터 몸(스프라이트 절반 폭)이 화면 밖으로 삐져나가 보이게 된다
        return Mathf.Clamp(desiredX, centerX - halfWidth + fighterEdgeInset, centerX + halfWidth - fighterEdgeInset);
    }
}
