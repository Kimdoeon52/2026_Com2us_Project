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

    [Tooltip("2.5D 시점이라 카메라 기준으로 이동시킨다")]
    [SerializeField] private bool _cameraRelative = true;

    private void Update()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude < 0.0001f)
            return;

        Vector3 direction = _cameraRelative ? ToCameraSpace(input) : input;
        Vector3 target = transform.position + direction.normalized * (_moveSpeed * Time.deltaTime);

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, _sampleDistance, NavMesh.AllAreas))
            transform.position = hit.position;
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
