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
                  "▶ [F4 / Num4]: 보스 현재 상태 콘솔 출력\n" +
                  "▶ [F5]: 전투 승리 결과 UI 테스트\n" +
                  "▶ [F6]: 전투 패배 결과 UI 테스트\n" +
                  "▶ [K / F12]: 보스 즉시 처치 (Instant Kill & 승리 연동)</color>");
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

        // F5: 전투 승리 결과창 테스트
        if (Input.GetKeyDown(KeyCode.F5))
        {
            TestVictoryResult();
        }

        // F6: 전투 패배 결과창 테스트
        if (Input.GetKeyDown(KeyCode.F6))
        {
            TestDefeatResult();
        }

        // K 또는 F12: 보스 즉시 처치 (Instant Kill)
        if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.F12))
        {
            InstantKillBoss();
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

    public void TestVictoryResult()
    {
        Debug.Log("<color=cyan>[Tester] F5 입력 ➔ 전투 승리 정산 및 결과 UI 테스트 호출</color>");
        EnsureBattleResultUI();
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.EndBattle(true, "Player");
        }
        else
        {
            var dummyResult = new BattleSettlementResult
            {
                PlayerWon = true,
                GoldDelta = 150,
                CoreExpGained = 120,
                SummaryMessage = "테스트: 보스 격파 승리 정산"
            };
            FindAnyObjectByType<RealSteel.UI.BattleResultUI>()?.ShowResult(dummyResult, true);
        }
    }

    public void TestDefeatResult()
    {
        Debug.Log("<color=red>[Tester] F6 입력 ➔ 전투 패배 정산 및 결과 UI 테스트 호출</color>");
        EnsureBattleResultUI();
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.EndBattle(false, "Boss");
        }
        else
        {
            var dummyResult = new BattleSettlementResult
            {
                PlayerWon = false,
                GoldDelta = -100,
                CoreExpGained = 20,
                CoreUnstable = true,
                DestroyedPartIDs = new System.Collections.Generic.List<string> { "PART_ARM_L_01" },
                SummaryMessage = "테스트: 기체 대파 패배 정산 (왼팔 영구 파괴)"
            };
            FindAnyObjectByType<RealSteel.UI.BattleResultUI>()?.ShowResult(dummyResult, false);
        }
    }

    [ContextMenu("▶ Instant Kill Boss (보스 즉시 처치)")]
    public void InstantKillBoss()
    {
        if (bossController == null)
        {
            bossController = FindFirstObjectByType<BossController>();
        }

        if (bossController == null) return;

        Debug.Log("<color=red><b>[Tester] K 키 입력 ➔ 보스 치명적 일격 즉사! (HP: 0)</b></color>");
        bossController.TakeDamage(999999);
        if (bossController.Snapshot != null)
        {
            bossController.Snapshot.currentHp = 0;
        }

        EnsureBattleResultUI();

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.EndBattle(true, "Player");
        }
        else
        {
            TestVictoryResult();
        }
    }

    private void EnsureBattleResultUI()
    {
        var existing = RealSteel.UI.BattleResultUI.Instance ?? FindAnyObjectByType<RealSteel.UI.BattleResultUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("@BattleResultUI");
            go.AddComponent<RealSteel.UI.BattleResultUI>();
        }
    }

    private void OnGUI()
    {
        if (!showOnScreenGui || bossController == null) return;

        // 화면 우측 상단 조작 GUI 박스
        GUILayout.BeginArea(new Rect(Screen.width - 240, 20, 220, 350), "<b>[보스 FSM 테스터]</b>", GUI.skin.window);
        GUILayout.Space(6);

        var curState = bossController.StateMachine?.CurrentState?.StateName ?? "None";
        GUILayout.Label($"<size=10><b>상태:</b> {curState}</size>");
        GUILayout.Space(4);

        if (GUILayout.Button("[F1] 일반 공격 (-50)", GUILayout.Height(26))) AttackNormal();
        if (GUILayout.Button("[F2] 300 저지 공격 (스턴)", GUILayout.Height(26))) AttackInterrupt300();
        if (GUILayout.Button("[F3] 못판 그로기 (4초 1.5배)", GUILayout.Height(26))) TriggerGimmickGroggy();
        if (GUILayout.Button("[F4] 보스 스탯 출력", GUILayout.Height(22))) PrintBossStatus();
        GUILayout.Space(4);
        if (GUILayout.Button("[F5] 승리 결과 UI 테스트", GUILayout.Height(26))) TestVictoryResult();
        if (GUILayout.Button("[F6] 패배 결과 UI 테스트", GUILayout.Height(26))) TestDefeatResult();
        GUILayout.Space(4);
        GUI.color = new Color(1f, 0.4f, 0.4f, 1f);
        if (GUILayout.Button("☠ [K] 보스 즉사 킬 (Kill)", GUILayout.Height(28))) InstantKillBoss();
        GUI.color = Color.white;

        GUILayout.EndArea();
    }
}
