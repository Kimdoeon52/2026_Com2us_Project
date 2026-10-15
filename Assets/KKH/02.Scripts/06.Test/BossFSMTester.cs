using UnityEngine;

/// <summary>
/// [보스 FSM 인터랙티브 통합 테스터 (BossFSMTester)]
/// HUDInteractiveTester(1~5번)와의 단축키 충돌 방지를 위해 F1~F4 및 숫자패드(Numpad 1~4)로 개편.
/// 화면 우측 상단 GUI 버튼으로도 마우스 원클릭 조작 가능.
/// </summary>
public class BossFSMTester : MonoBehaviour
{
    [SerializeField] private BossController bossController;
    [SerializeField] private bool showOnScreenGui = true;

    private void Start()
    {
        if (bossController == null)
        {
            bossController = GetComponent<BossController>();
        }

        Debug.Log("<color=cyan><b>[BossFSMTester 준비 완료 - 키 충돌 개편]</b>\n" +
                  "▶ [F1 / Num1]: 보스 일반 피격 (-50 HP)\n" +
                  "▶ [F2 / Num2]: 300 저지 공격 (돌진 전조 중이면 3초 스턴)\n" +
                  "▶ [F3 / Num3]: 못판 착지 기믹 파훼 (4초 그로기 & 대미지 1.5배)\n" +
                  "▶ [F4 / Num4]: 보스 현재 상태 콘솔 출력</color>");
    }

    private void Update()
    {
        if (bossController == null) return;

        // F1 또는 키패드 1: 일반 피격 50
        if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            AttackNormal();
        }

        // F2 또는 키패드 2: 300 저지 공격
        if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            AttackInterrupt300();
        }

        // F3 또는 키패드 3: 못판 착지 기믹 파훼
        if (Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            TriggerGimmickGroggy();
        }

        // F4 또는 키패드 4: 상태 조회
        if (Input.GetKeyDown(KeyCode.F4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            PrintBossStatus();
        }
    }

    public void AttackNormal()
    {
        Debug.Log("<color=yellow>[Tester] F1 입력 ➔ 보스 일반 피격 (피해 50)</color>");
        bossController.TakeDamage(50);
    }

    public void AttackInterrupt300()
    {
        Debug.Log("<color=cyan>[Tester] F2 입력 ➔ 저지 강타 공격 (피해 300! 전조 중이면 3초 스턴)</color>");
        bossController.TakeDamage(300);
    }

    public void TriggerGimmickGroggy()
    {
        Debug.Log("<color=magenta>[Tester] F3 입력 ➔ 못판 유도 착지 파훼! (4초 그로기 & 대미지 1.5배)</color>");
        bossController.InterruptToGroggy(4.0f, 1.5f);
    }

    public void PrintBossStatus()
    {
        var s = bossController.Snapshot;
        if (s == null) return;
        Debug.Log($"<color=white>[Tester] 보스: {s.bossName} | HP: {s.currentHp}/{s.maxHp} | " +
                  $"상태: {bossController.StateMachine?.CurrentState?.StateName} | " +
                  $"그로기: {s.isGroggy} (피해배율: {s.currentDamageMultiplier}배)</color>");
    }

    private void OnGUI()
    {
        if (!showOnScreenGui || bossController == null) return;

        // 화면 우측 상단 조작 GUI 박스
        GUILayout.BeginArea(new Rect(Screen.width - 240, 20, 220, 230), "<b>[보스 FSM 테스터]</b>", GUI.skin.window);
        GUILayout.Space(6);

        var curState = bossController.StateMachine?.CurrentState?.StateName ?? "None";
        GUILayout.Label($"<size=10><b>상태:</b> {curState}</size>");
        GUILayout.Space(4);

        if (GUILayout.Button("[F1] 일반 공격 (-50)", GUILayout.Height(30))) AttackNormal();
        if (GUILayout.Button("[F2] 300 저지 공격 (스턴)", GUILayout.Height(30))) AttackInterrupt300();
        if (GUILayout.Button("[F3] 못판 그로기 (4초 1.5배)", GUILayout.Height(30))) TriggerGimmickGroggy();
        if (GUILayout.Button("[F4] 보스 스탯 출력", GUILayout.Height(25))) PrintBossStatus();

        GUILayout.EndArea();
    }
}
