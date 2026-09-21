using UnityEngine;
using UnityEngine.AI;

// 동작 실험용 임시 플레이어. 팀원 플레이어로 교체되면 이 폴더째 삭제한다.
// WASD 이동과 NavMesh 제약만 담당하고 탐색 시스템을 전혀 모른다
public class TEMP_JunkShopPlayerMover : MonoBehaviour
{
    [Tooltip("초당 이동 거리")]
    [Min(0f)]
    [SerializeField] private float _moveSpeed = 4f;

    [Tooltip("NavMesh 밖으로 나갔을 때 되돌릴 최대 탐색 거리")]
    [Min(0.01f)]
    [SerializeField] private float _sampleDistance = 0.6f;

    [Tooltip("시작할 때 바닥을 찾는 탐색 거리. 피벗이 바닥에서 떨어져 있어도 잡히도록 넉넉하게 준다")]
    [Min(0.01f)]
    [SerializeField] private float _groundSnapDistance = 5f;

    [Tooltip("2.5D 시점이라 카메라 기준으로 이동시킨다")]
    [SerializeField] private bool _cameraRelative = true;

    // 이동은 발밑 기준으로 계산한다. 피벗이 중심인 캡슐이어도 바닥에 파묻히지 않는다
    private Vector3 _footPosition;
    private float _pivotHeight;
    private bool _grounded;

    private void Start()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, _groundSnapDistance, NavMesh.AllAreas))
        {
            Debug.LogWarning($"{name}: 근처에서 NavMesh를 찾지 못했다. 베이크 여부와 시작 위치를 확인해라.", this);
            return;
        }

        _footPosition = hit.position;
        _pivotHeight = Mathf.Max(0f, transform.position.y - hit.position.y);
        _grounded = true;
        transform.position = _footPosition + (Vector3.up * _pivotHeight);
    }

    private void Update()
    {
        if (!_grounded || ScrapInputGate.IsBlocked)
            return;

        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude < 0.0001f)
            return;

        Vector3 direction = _cameraRelative ? ToCameraSpace(input) : input;
        Vector3 target = _footPosition + (direction.normalized * (_moveSpeed * Time.deltaTime));

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, _sampleDistance, NavMesh.AllAreas))
            return;

        _footPosition = hit.position;
        transform.position = _footPosition + (Vector3.up * _pivotHeight);
    }

    private Vector3 ToCameraSpace(Vector3 input)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return input;

        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;

        return (forward * input.z) + (right * input.x);
    }
}
