using UnityEngine;

// 말풍선/명패가 항상 카메라 화면과 평행하게 보이도록 회전 (좌석이 부채꼴로 돌아가 있어도 정면으로 보임)
// 위치/크기 연출(DOTween)은 건드리지 않고 회전만 덮어씀
public class BillboardToCamera : MonoBehaviour
{
    private Camera cachedCamera;

    private void LateUpdate()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
        if (cachedCamera == null) return;
        transform.rotation = cachedCamera.transform.rotation; // 카메라와 같은 방향 = 화면에 딱 붙어 보임
    }
}
