using UnityEngine;

// 눈에서 나간 선이 플레이어를 천천히 쫓고, 막히는 곳에서 끊긴다
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(LineRenderer))]
public class KyodaiEyeLaser : MonoBehaviour
{
    [Tooltip("따라붙는 시간(초)")]
    [Min(0.01f)] [SerializeField] private float followTime = 0.8f;

    [Tooltip("최대 추적 속도")]
    [Min(0.01f)] [SerializeField] private float maxFollowSpeed = 6f;

    [Tooltip("조준점 너머로 뻗는 길이")]
    [Min(0f)] [SerializeField] private float overshoot = 3f;

    [Tooltip("선을 막는 레이어")]
    [SerializeField] private LayerMask blockMask = Physics.DefaultRaycastLayers;

    private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
    private LineRenderer line;
    private Transform player;
    private Collider2D playerCollider;
    private Vector2 aim;
    private Vector2 aimVelocity;
    private bool hasAim;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
    }

    private void OnEnable()
    {
        hasAim = false;
        aimVelocity = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (player == null)
            FindPlayer();

        line.enabled = player != null;
        if (player == null)
            return;

        Vector2 target = PlayerCenter();
        if (!hasAim)
        {
            aim = target;
            hasAim = true;
        }
        aim = Vector2.SmoothDamp(aim, target, ref aimVelocity, followTime, maxFollowSpeed, Time.deltaTime);

        Vector3 eye = transform.position;
        Vector3 end = FindLineEnd(eye, player.position.z);
        line.SetPosition(0, eye);
        line.SetPosition(1, end);

        // 자식 스포트라이트도 같은 곳을 비춘다
        Vector3 toEnd = end - eye;
        if (toEnd.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toEnd);
    }

    private void FindPlayer()
    {
        var found = GameObject.FindWithTag("Player");
        if (found == null)
            return;

        player = found.transform;
        playerCollider = found.GetComponent<Collider2D>();
    }

    // 콜라이더가 꺼져 있어도 마지막 중심을 쓴다
    private Vector2 PlayerCenter()
    {
        return playerCollider != null ? (Vector2)player.TransformPoint(playerCollider.offset) : (Vector2)player.position;
    }

    // 화면에 보이는 눈 위치를 경기장 평면으로 옮긴다
    private Vector2 EyeOnPlane(Vector3 eye, float planeZ)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return eye;

        Ray ray = cam.ScreenPointToRay(cam.WorldToScreenPoint(eye));
        var plane = new Plane(Vector3.back, new Vector3(0f, 0f, planeZ));
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : eye;
    }

    // 경기장 평면 2D 판정 후 3D 판정으로 선 끝을 찾는다
    private Vector3 FindLineEnd(Vector3 eye, float planeZ)
    {
        // 맞든 빗나가든 눈 → 조준점 경로를 지나게 한다
        Vector3 aimPoint = new Vector3(aim.x, aim.y, planeZ);
        Vector3 toAim = aimPoint - eye;
        Vector3 end = aimPoint + toAim.normalized * overshoot;

        Vector2 origin = EyeOnPlane(eye, planeZ);
        Vector2 direction = aim - origin;
        float range = direction.magnitude + overshoot;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;

        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = blockMask };
        int count = Physics2D.Raycast(origin, direction, filter, hits, range);
        for (int i = 0; i < count; i++)
        {
            // 시작점이 겹친 콜라이더는 건너뛴다
            if (hits[i].fraction <= 0f)
                continue;

            end = new Vector3(hits[i].point.x, hits[i].point.y, planeZ);
            break;
        }

        Vector3 toEnd = end - eye;
        if (Physics.Raycast(eye, toEnd.normalized, out RaycastHit hit, toEnd.magnitude, blockMask, QueryTriggerInteraction.Ignore))
            end = hit.point;

        return end;
    }
}
