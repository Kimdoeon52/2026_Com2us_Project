using UnityEngine;

/// <summary>
/// [보스 FSM & 인터럽트 인터랙티브 테스터 (BossFSMTester)]
/// 유니티 에디터 Play 모드에서 키보드 단축키(1, 2, 3, 4, 5)로 보스 상태 전이 및 인터럽트를 검증함.
/// - [키 1]: 일반 피격 대미지 50 가하기
/// - [키 2]: DPS 체크 강타 (대미지 300 즉시 가하여 3초 스턴 유도)
/// - [키 3]: 기믹 파훼 강제 트리거 (못판 펑크 유도: 4초 그로기 & 대미지 1.5배)
/// - [키 4]: 현재 상태 및 체력 정보 콘솔 출력
/// </summary>
public class BossFSMTester : MonoBehaviour
{
    [SerializeField] private BossController bossController;

    private void Start()
    {
        if (bossController == null)
        {
            bossController = GetComponent<BossController>();
        }

        Debug.Log("<color=cyan><b>[BossFSMTester 준비 완료]</b>\n" +
                  "▶ [1번 키]: 일반 공격 (피해 50)\n" +
                  "▶ [2번 키]: 저지 공격 (피해 300 ➔ 돌진 전조 중이면 3초 스턴)\n" +
                  "▶ [3번 키]: 기믹 파훼 강제 인터럽트 (4초 그로기 ➔ 피해 1.5배)\n" +
                  "▶ [4번 키]: 보스 스냅샷 상태 출력</color>");
    }

    private void Update()
    {
        if (bossController == null) return;

        // 1번: 일반 공격 (50 대미지)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("[Tester] 1번 키 입력 ➔ 일반 공격 (대미지 50)");
            bossController.TakeDamage(50);
        }

        // 2번: 저지 공격 (300 대미지: 전조 중 누적 300 달성 시 스턴)
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("[Tester] 2번 키 입력 ➔ 저지 강타 공격 (대미지 300)");
            bossController.TakeDamage(300);
        }

        // 3번: 기믹 파훼 강제 인터럽트 (못판 펑크 유도)
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("[Tester] 3번 키 입력 ➔ 못판 유도 착지 기믹 파훼! (4초 그로기 인터럽트)");
            bossController.InterruptToGroggy(4.0f, 1.5f);
        }

        // 4번: 상태 정보 출력
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            var s = bossController.Snapshot;
            Debug.Log($"[Tester] 보스: {s.bossName} | HP: {s.currentHp}/{s.maxHp} | " +
                      $"상태: {bossController.StateMachine?.CurrentState?.StateName} | " +
                      $"그로기: {s.isGroggy} (배율: {s.currentDamageMultiplier}배)");
        }
    }
}
