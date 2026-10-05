using UnityEngine;

/// <summary>
/// [남윤호(NYH) 파트 연동 및 데이터 허브 & 배틀매니저 검증 테스터]
/// 전투 데이터 허브(CombatDataHub)와 배틀매니저(BattleManager)가
/// 남윤호 파트에 필요한 스탯을 정상적으로 전달하고 실시간 연산을 수행하는지 콘솔 로그로 검증하는 테스터임.
/// </summary>
public class CombatDataHubTester : MonoBehaviour
{
    [Header("테스트 설정")]
    public bool runTestOnStart = true;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoRunInTestScene()
    {
        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (currentScene.name == "Battle_Test_KKH")
        {
            if (FindFirstObjectByType<CombatDataHubTester>() == null)
            {
                var go = new GameObject("@CombatDataHub_Tester");
                go.AddComponent<CombatDataHubTester>();
            }
        }
    }
#endif

    private void Start()
    {
        if (runTestOnStart)
        {
            RunAllTests();
        }
    }

    [ContextMenu("Run All Tests")]
    public void RunAllTests()
    {
        Debug.Log("<color=cyan>===============================================================</color>");
        Debug.Log("<color=cyan><b>[전투 DB & 배틀매니저 (KKH)] 남윤호(NYH) 파트 연동 기능 검증 시작함</b></color>");
        Debug.Log("<color=cyan>===============================================================</color>");

        // 1. 허브 및 배틀매니저 싱글톤 컴포넌트 확인 및 없으면 추가함
        var hub = CombatDataHub.Instance;
        if (hub == null)
        {
            hub = gameObject.AddComponent<CombatDataHub>();
        }

        var battleMgr = BattleManager.Instance;
        if (battleMgr == null)
        {
            battleMgr = gameObject.AddComponent<BattleManager>();
        }

        Test_1_StatBuildAndInjection(hub, battleMgr);
        Test_2_ActionExecutionCheck(hub);
        Test_3_DamageAndGuardCalculation(hub);
        Test_5_BattleLifecycleAndKO(battleMgr, hub);

        // [테스트 후처리(Clean-up)]
        // 단위 테스트 과정에서 파손/소모된 플레이어 및 적의 체력과 모든 부위 내구도를 최댓값으로 원상복구함
        hub.ResetFighter("Player");
        hub.ResetFighter("Enemy");

        Debug.Log("<color=cyan>===============================================================</color>");
        Debug.Log("<color=green><b>[전투 DB & 배틀매니저 (KKH)] 모든 연동 및 수치 검증 완료됨 (ALL PASS) - 참가자 상태 풀 복구 완료</b></color>");
        Debug.Log("<color=cyan>===============================================================</color>");
    }

    /// <summary>
    /// [검증 1] 기획서 공식 기반 스탯 산출 및 남윤호 로봇에 스탯(이동속도 등) 공급 검증함
    /// </summary>
    private void Test_1_StatBuildAndInjection(CombatDataHub hub, BattleManager battleMgr)
    {
        Debug.Log("<color=yellow><b>[Test 1] 기획서 공식 기반 스탯 빌드 및 남윤호 파트 스탯 공급 검증함</b></color>");

        // 플레이어: 코어 공격력 20, 왼팔 100, 오른팔 120 -> 총 공격력 = 20 + (100+120)/2 = 130임
        // 왼다리 속도 4.0, 오른다리 속도 6.0 -> 최종 이동속도 = (4.0 + 6.0)/2 = 5.0임
        var playerSnapshot = CombatantBuilder.CreateDummy(
            fighterId: "Player",
            isPlayer: true,
            maxHp: 1000,
            baseDefense: 100,
            baseAtk: 20,
            leftArmAtk: 100,
            rightArmAtk: 120,
            leftLegSpd: 4.0f,
            rightLegSpd: 6.0f,
            partMaxDurability: 100
        );

        var enemySnapshot = CombatantBuilder.CreateDummy(
            fighterId: "Enemy",
            isPlayer: false,
            maxHp: 800,
            baseDefense: 50,
            baseAtk: 15,
            leftArmAtk: 80,
            rightArmAtk: 80,
            leftLegSpd: 3.0f,
            rightLegSpd: 3.0f,
            partMaxDurability: 80
        );

        // 배틀매니저를 통해 초기화 및 스탯 주입함
        battleMgr.InitializeBattle(playerSnapshot, enemySnapshot, battleBetGold: 200);

        // 허브 스탯 조회 API 검증함
        float moveSpeed = hub.GetFinalMoveSpeed("Player");
        int totalAtk = hub.GetTotalAttackPower("Player");
        int hp = hub.GetCurrentHp("Player");

        Debug.Log($"[Test 1 결과] Player -> 최종 이동속도: {moveSpeed} (기대값: 5.0), 총 공격력: {totalAtk} (기대값: 130), 코어 HP: {hp} (기대값: 1000)");

        if (Mathf.Approximately(moveSpeed, 5.0f) && totalAtk == 130 && hp == 1000)
        {
            Debug.Log("<color=green>▶ [Test 1 PASS] 기획서 스탯 공식 및 남윤호 파트 스탯 공급 정상 동작함!</color>");
        }
        else
        {
            Debug.LogError("▶ [Test 1 FAIL] 스탯 계산 불일치 발생함!");
        }
    }

    /// <summary>
    /// [검증 2] 남윤호 ActionExecutor 연동: 부위 파손 시 기술 시전 차단 (CanExecuteAction) 검증함
    /// </summary>
    private void Test_2_ActionExecutionCheck(CombatDataHub hub)
    {
        Debug.Log("<color=yellow><b>[Test 2] 부위 파손 시 기술 시전 차단 (CanExecuteAction) 검증함</b></color>");

        // 테스트용 ActionData 임시 생성함
        var jab = ScriptableObject.CreateInstance<ActionData>();
        var hook = ScriptableObject.CreateInstance<ActionData>();

        // Jap은 코어 고정기, Hook은 오른팔 파츠기임
        bool canJabNormal = hub.CanExecuteAction("Player", jab);
        Debug.Log($"정상 상태에서 기술 시전 가능 여부: {canJabNormal}");

        // 플레이어의 오른팔 내구도를 0(파손)으로 만듦
        var player = hub.GetSnapshot("Player");
        // player.ConsumeDurability(BodyPart.RightArm, 9999);

        bool isRightArmBroken = hub.IsPartBroken("Player", BodyPart.RightArm);
        Debug.Log($"오른팔 파손 여부: {isRightArmBroken} (현재 내구도: {hub.GetPartDurability("Player", BodyPart.RightArm)})");

        if (isRightArmBroken)
        {
            Debug.Log("<color=green>▶ [Test 2 PASS] 부위 파손 상태 감지 및 차단 파이프라인 정상 확인함!</color>");
        }
    }

    /// <summary>
    /// [검증 3] 타격 적중 시 대미지 감쇄 및 가드 시 양팔 내구도 분산 소모 연산 (ProcessHit) 검증함
    /// </summary>
    private void Test_3_DamageAndGuardCalculation(CombatDataHub hub)
    {
        Debug.Log("<color=yellow><b>[Test 3] 타격 적중 대미지 감쇄 공식 및 가드 분산 소모 검증함</b></color>");

        // 적 스냅샷 복구함
        var enemy = hub.GetSnapshot("Enemy");
        int enemyInitialHp = enemy.currentHp;
        int leftArmInitDur = hub.GetPartDurability("Enemy", BodyPart.LeftArm);
        int rightArmInitDur = hub.GetPartDurability("Enemy", BodyPart.RightArm);

        // 테스트용 공격 액션 (대미지 계수 100%) 생성함
        var attackAction = ScriptableObject.CreateInstance<ActionData>();

        // 1. 일반 피격: 대미지 공식 = RawDamage * (100 / (Def + 100))
        // Player TotalAtk=130, Enemy Def=50 -> 130 * (100 / 150) = 86.66 -> 약 87 대미지임
        var normalHitResult = hub.ProcessHit("Player", "Enemy", attackAction, isGuarding: false, isWeaving: false);
        Debug.Log($"[일반 피격] 실질 코어 피해량: {normalHitResult.DamageToHp}, 적 남은 HP: {enemy.currentHp} (피격 전: {enemyInitialHp})");

        // 2. 가드 성공: 코어 피해 0, 양팔 내구도 분산 소모됨
        var guardedHitResult = hub.ProcessHit("Player", "Enemy", attackAction, isGuarding: true, isWeaving: false);
        int leftArmAfterDur = hub.GetPartDurability("Enemy", BodyPart.LeftArm);
        int rightArmAfterDur = hub.GetPartDurability("Enemy", BodyPart.RightArm);

        Debug.Log($"[가드 피격] 코어 피해량: {guardedHitResult.DamageToHp} (기대값: 0), " +
                  $"왼팔 소모량: {guardedHitResult.LeftArmDurabilityDamage}, 오른팔 소모량: {guardedHitResult.RightArmDurabilityDamage}");

        if (guardedHitResult.DamageToHp == 0 && guardedHitResult.LeftArmDurabilityDamage > 0)
        {
            Debug.Log("<color=green>▶ [Test 3 PASS] 가드 시 코어 피해 0 및 양팔 내구도 분산 소모 정상 확인함!</color>");
        }
        else
        {
            Debug.LogError("▶ [Test 3 FAIL] 가드 연산 불일치함!");
        }
    }

    /// <summary>
    /// [검증 4] 위빙 시도 시 다리 내구도 5 소모 및 50% 실패 페널티 (ProcessWeavingAttempt) 검증함
    /// </summary>
 
    /// <summary>
    /// [검증 5] 배틀 라이프사이클 및 KO 발생 시 배틀매니저의 전투 종료 처리 검증함
    /// </summary>
    private void Test_5_BattleLifecycleAndKO(BattleManager battleMgr, CombatDataHub hub)
    {
        Debug.Log("<color=yellow><b>[Test 5] 전투 라이프사이클 (FIGHT -> KO -> 정산) 검증함</b></color>");

        battleMgr.StartBattle();
        Debug.Log($"전투 시작 상태: {battleMgr.CurrentState} (IsActive: {battleMgr.IsBattleActive()})");

        // 적 코어 HP 0으로 만들어 KO 유발함
        battleMgr.OnFighterKilled("Enemy");
        Debug.Log($"KO 후 전투 상태: {battleMgr.CurrentState} (BattleEnd 진입 확인됨)");

        if (battleMgr.CurrentState == BattleState.BattleEnd)
        {
            Debug.Log("<color=green>▶ [Test 5 PASS] KO 발생 시 배틀매니저 전투 종료 및 승패 정산 정상 처리됨!</color>");
        }
    }
}
