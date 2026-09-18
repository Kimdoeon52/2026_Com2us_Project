using UnityEngine;

/// <summary>
/// [임시/테스트용] IInputSource의 이동 입력을 실제 transform 이동으로 옮기는 자리.
/// 3층 구조 목록(CLAUDE.md §1)에 정식으로 없는 클래스라 나중에 팀 구조에 맞춰
/// 위치가 바뀌거나 다른 시스템에 흡수될 수 있음 — 지금은 "키 누르면 움직인다"를
/// 눈으로 확인하기 위한 최소 스캐폴드.
///
/// CombatClock이 아직 없어서 임시로 Update()에서 직접 프레임을 센다.
/// CombatClock 완성되면 이 이동 로직도 CombatTick()로 옮겨야 한다 (§3 원칙).
/// </summary>
public class RobotMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f; // TODO: 임시값. 프레임표/기획 확정 전
    [SerializeField] private Transform opponent;

    private IInputSource inputSource;
    private bool facingRight = true;

    public void Init(IInputSource source)
    {
        inputSource = source;
    }

    private void Update()
    {
        float moveInput = inputSource?.GetMoveInput() ?? 0f;
        transform.Translate(Vector3.right * moveInput * moveSpeed * Time.deltaTime, Space.World);

        if (opponent != null)
        {
            // 정식 방식 — 상대 위치 기준 (§ 격겜 컨벤션: 뒤로 물러나도 상대를 계속 바라봄)
            facingRight = opponent.position.x > transform.position.x;
        }
        else if (Mathf.Abs(moveInput) > 0.01f)
        {
            // [임시 폴백] 상대가 아직 씬에 없을 때(혼자 테스트)만 이동 방향 기준으로 뒤집음.
            // opponent 연결되는 순간 이 분기는 안 타고 위쪽(정식 방식)으로 넘어간다.
            facingRight = moveInput > 0f;
        }

        float sign = facingRight ? 1f : -1f;
        transform.localScale = new Vector3(sign * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }
}
