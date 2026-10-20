using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    void Update()
    {
        // 1. 키보드 방향키 또는 WASD 입력 받기 (-1, 0, 1 중 하나)
        float xInput = Input.GetAxisRaw("Horizontal");
        float yInput = Input.GetAxisRaw("Vertical");

        // 2. 이동 방향 벡터 생성 및 정규화 (대각선 이동 시 속도 빨라짐 방지)
        Vector3 moveDirection = new Vector3(xInput, yInput, 0f).normalized;

        // 3. 실제 이동 적용 (방향 * 속도 * 프레임 보정)
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);
    }
}
