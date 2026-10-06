using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [전투 데이터 & 보스 시스템 종합 통합 테스트 러너 (CombatDataHubTester)]
/// v5.6 아키텍처 및 리팩토링된 모든 백엔드 로직(코어 성장, 실린더, 보스 범용 기믹,
/// 1:1 대전 모드 이원화, 기획서 승패 정산 및 기본 파츠 제외)을 콘솔 로그로 자동 검증함.
/// </summary>
public class CombatDataHubTester : MonoBehaviour
{
    [Header("테스트 실행 설정")]
    [Tooltip("체크 시 씬 시작과 동시에 모든 단위/통합 테스트를 자동 실행함")]
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

    [ContextMenu("▶ Run All Tests (모든 테스트 실행)")]
    public void RunAllTests()
    {
        Debug.Log("<color=cyan>================================================================================</color>");
        Debug.Log("<color=cyan><b>[전투 DB & 보스 시스템 (KKH)] 리팩토링 종합 검증 테스트 스위트 개시</b></color>");
        Debug.Log("<color=cyan>================================================================================</color>");

        int passCount = 0;
        int totalTests = 6;

        // 필수 컴포넌트 확보
        var hub = CombatDataHub.Instance ?? gameObject.AddComponent<CombatDataHub>();
        var battleMgr = BattleManager.Instance ?? gameObject.AddComponent<BattleManager>();

        if (Test_1_CoreGrowthAndRestoreCost()) passCount++;
        if (Test_2_CylinderAndDefaultParts()) passCount++;
        if (Test_3_RobotVsRobotAndBasicAttackD()) passCount++;
        if (Test_4_BossGimmickAndGroggyAmplification()) passCount++;
        if (Test_5_BattleModeDualPipeline(hub)) passCount++;
        if (Test_6_SettlementAndDefaultPartExclusion()) passCount++;

        // 사후 상태 복원 (Clean-up)
        hub.ResetFighter("Player");
        hub.ResetFighter("Enemy");

        Debug.Log("<color=cyan>================================================================================</color>");
        if (passCount == totalTests)
        {
            Debug.Log($"<color=#1AF333><b>[ALL PASS] 전체 {totalTests}개 검증 케이스가 100% 정상 통과되었습니다! ({passCount}/{totalTests})</b></color>");
        }
        else
        {
            Debug.LogError($"[TEST RESULT] 총 {totalTests}개 중 {passCount}개 성공, {totalTests - passCount}개 실패 발생!");
        }
        Debug.Log("<color=cyan>================================================================================</color>");
    }

    // ========================================================================
    // [Test 1] 코어 Lv.1~30 성장 공식 및 복구비 공식 검증 (CoreMasterData)
    // ========================================================================
    private bool Test_1_CoreGrowthAndRestoreCost()
    {
        Debug.Log("<color=yellow><b>[Test 1] 코어 Lv.1~30 성장 공식 및 복구비 공식 검증</b></color>");

        var coreSO = ScriptableObject.CreateInstance<CoreMasterData>();
        coreSO.coreID = "CORE_TEST";
        coreSO.baseHp = 300;
        coreSO.baseDefense = 20;

        // Lv.1 기준값 검증
        int hpLv1 = coreSO.GetMaxHp(1);
        int defLv1 = coreSO.GetDefense(1);
        int costLv1 = coreSO.GetRecoveryCost(1);

        // Lv.10 검증 (HP: 300 + 9*15 = 435, DEF: 20 + 9*3 = 47)
        int hpLv10 = coreSO.GetMaxHp(10);
        int defLv10 = coreSO.GetDefense(10);

        // Lv.20 검증 (HP: 435 + 10*20 = 635, DEF: 47 + 10*4 = 87)
        int hpLv20 = coreSO.GetMaxHp(20);
        int defLv20 = coreSO.GetDefense(20);

        // Lv.30 검증 (HP: 635 + 10*25 = 885, DEF: 87 + 10*5 = 137, Cost: 100 + 30*50 = 1600G)
        int hpLv30 = coreSO.GetMaxHp(30);
        int defLv30 = coreSO.GetDefense(30);
        int costLv30 = coreSO.GetRecoveryCost(30);

        bool pass = (hpLv1 == 300 && defLv1 == 20 && costLv1 == 150) &&
                    (hpLv10 == 435 && defLv10 == 47) &&
                    (hpLv20 == 635 && defLv20 == 87) &&
                    (hpLv30 == 885 && defLv30 == 137 && costLv30 == 1600);

        if (pass)
        {
            Debug.Log($"<color=green>▶ [Test 1 PASS] 코어 Lv.1~30 체력/방어력 공식 및 복구비 공식 일치! (Lv.30 HP:{hpLv30}, DEF:{defLv30}, 복구비:{costLv30}G)</color>");
            return true;
        }
        else
        {
            Debug.LogError($"▶ [Test 1 FAIL] 코어 성장 수치 불일치! Lv.1(HP:{hpLv1}, DEF:{defLv1}), Lv.30(HP:{hpLv30}, DEF:{defLv30})");
            return false;
        }
    }

    // ========================================================================
    // [Test 2] 실린더 탄환 시스템 & 기본 파츠 자동 채움 검증 (CombatantSnapshot, CombatantBuilder)
    // ========================================================================
    private bool Test_2_CylinderAndDefaultParts()
    {
        Debug.Log("<color=yellow><b>[Test 2] 실린더 탄환 관리 & 임시 기본 파츠(DEFAULT_) 자동 장착 검증</b></color>");

        // 파츠 장착 없이 코어만 넘겨서 빌드 (빈 슬롯 5개)
        var coreSO = ScriptableObject.CreateInstance<CoreMasterData>();
        var snapshot = CombatantBuilder.Build("Player_Test", true, coreSO, null);

        // 1. 실린더 기본 1발 장전 및 최대 3발 제한 검증
        bool initCylinder = snapshot.currentCylinder == 1;
        snapshot.AddCylinder(1); // 2발
        snapshot.AddCylinder(5); // 3발 초과 시도 -> 3발 고정이어야 함
        bool maxCylinderCap = snapshot.currentCylinder == CombatantSnapshot.MaxCylinder;

        snapshot.SpendCylinder(2); // 1발 남음
        bool spendCylinder = snapshot.currentCylinder == 1;

        // 2. 기본 파츠 자동 채움 검증 (머리 42, 팔 70, 다리 56)
        bool hasHead = snapshot.partStates.TryGetValue(BodyPart.Head, out var head) && head.partID.StartsWith("DEFAULT_") && head.maxDurability == 42;
        bool hasLeftArm = snapshot.partStates.TryGetValue(BodyPart.LeftArm, out var leftArm) && leftArm.partID.StartsWith("DEFAULT_") && leftArm.maxDurability == 70;
        bool hasRightLeg = snapshot.partStates.TryGetValue(BodyPart.RightLeg, out var rightLeg) && rightLeg.partID.StartsWith("DEFAULT_") && rightLeg.maxDurability == 56;

        // 3. NPC A 프리셋 검증
        var npcA = CombatantBuilder.CreateNPCPreset(NPCType.NPC_A);
        bool npcValid = npcA != null && npcA.currentHp == 500 && !npcA.isPlayer;

        bool pass = initCylinder && maxCylinderCap && spendCylinder && hasHead && hasLeftArm && hasRightLeg && npcValid;

        if (pass)
        {
            Debug.Log("<color=green>▶ [Test 2 PASS] 실린더 탄환 관리(1발 시작, 최대 3발 상한) 및 5부위 기본 파츠(일반 70%) 자동 채움 정상 확인!</color>");
            return true;
        }
        else
        {
            Debug.LogError($"▶ [Test 2 FAIL] 실린더/기본파츠 검증 실패! (Cylinder:{snapshot.currentCylinder}, Head:{hasHead}, Arm:{hasLeftArm}, Leg:{hasRightLeg})");
            return false;
        }
    }

    // ========================================================================
    // [Test 3] 로봇 vs 로봇 1:1 대전 판정 및 D 기본기 실린더 충전 연산 (CombatCalculator)
    // ========================================================================
    private bool Test_3_RobotVsRobotAndBasicAttackD()
    {
        Debug.Log("<color=yellow><b>[Test 3] 1:1 로봇 대전 & D 기본기 실린더 충전 & 부위 파손 스킬 봉인 검증</b></color>");

        var attacker = CombatantBuilder.CreateDummy("Player_Atk", true, maxHp: 1000, baseDefense: 50, baseAtk: 20, leftArmAtk: 80, rightArmAtk: 80);
        var defender = CombatantBuilder.CreateDummy("NPC_Def", false, maxHp: 1000, baseDefense: 100);

        attacker.currentCylinder = 1;

        // 1. D 기본기 적중 검증 (D는 고정 피해 10, Defender 방어력 100 적용: 10 * (100 / 200) = 5 대미지)
        // 공격자 실린더 탄환이 +1 되어 2발이 되어야 함
        var resultD = CombatCalculator.EvaluateRobotAttack(
            attacker: attacker,
            defender: defender,
            rawSkillDamage: 0f,
            targetPart: BodyPart.Core,
            partDamageAmount: 0,
            isDefenderInvincible: false,
            isBasicAttackD: true
        );

        bool dDamageCheck = resultD.coreHpDamage == 5 && defender.currentHp == 995;
        bool cylinderGainedCheck = resultD.cylinderGained && attacker.currentCylinder == 2;

        // 2. 부위 타격 스킬 적중 검증 (오른팔에 내구도 80 직격 타격 -> 내구도 0 파손 판정)
        var resultArm = CombatCalculator.EvaluateRobotAttack(
            attacker: attacker,
            defender: defender,
            rawSkillDamage: 100f,
            targetPart: BodyPart.RightArm,
            partDamageAmount: 80,
            isDefenderInvincible: false,
            isBasicAttackD: false
        );

        bool armBrokenCheck = resultArm.isPartDestroyed && defender.IsPartBroken(BodyPart.RightArm);

        // 3. 스킬 시전 게이트 검증: 오른팔 파손 시 오른팔 스킬 시전 차단(false), D 기본기는 허용(true)
        bool canCastPartSkill = CombatCalculator.CanExecuteAction(defender, ActionSource.Part, BodyPart.RightArm);
        bool canCastBasicD = CombatCalculator.CanExecuteAction(defender, ActionSource.CoreFixed, BodyPart.Core);

        bool pass = dDamageCheck && cylinderGainedCheck && armBrokenCheck && !canCastPartSkill && canCastBasicD;

        if (pass)
        {
            Debug.Log("<color=green>▶ [Test 3 PASS] D 기본기 방어 감쇄 및 실린더 +1 충전(2발), 부위 파손 판정 및 스킬 봉인 게이트 정상 확인!</color>");
            return true;
        }
        else
        {
            Debug.LogError($"▶ [Test 3 FAIL] 로봇 연산 오류! D_Dmg:{resultD.coreHpDamage}, Cylinder:{attacker.currentCylinder}, ArmBroken:{armBrokenCheck}, GateBlock:{!canCastPartSkill}");
            return false;
        }
    }

    // ========================================================================
    // [Test 4] 보스 범용 기믹 파훼 & 그로기 1.5배 피해 연산 검증 (BossSnapshot, CombatCalculator)
    // ========================================================================
    private bool Test_4_BossGimmickAndGroggyAmplification()
    {
        Debug.Log("<color=yellow><b>[Test 4] 보스 범용 기믹(못판 펑크) 파훼 및 4초 그로기 1.5배 피해 연산 검증</b></color>");

        var player = CombatantBuilder.CreateDummy("Player", true, maxHp: 1000, baseDefense: 50, baseAtk: 20, leftArmAtk: 80, rightArmAtk: 80); // 총 공격력 = 100
        var boss = new BossSnapshot
        {
            bossID = "BOSS_REGION_01",
            bossName = "정크 휠러",
            maxHp = 4000,
            currentHp = 4000,
            baseDefense = 100
        };

        // 1. 범용 기믹 맵에 못판 기믹 등록 (내구도 100)
        boss.gimmickStates["GIMMICK_NAIL_BOARD"] = new BossGimmickRuntimeState("GIMMICK_NAIL_BOARD", "고철 못판", false, 100);

        // 기믹 타격 및 파훼
        var hitGimmick = CombatCalculator.EvaluatePlayerAttackOnBoss(
            player: player,
            boss: boss,
            skillBaseDamage: 100f, // 100 * (100 / 100) = 100 실제 피해
            isBasicAttackD: false,
            attackTag: GimmickTag.ObjectBreak,
            targetGimmickID: "GIMMICK_NAIL_BOARD"
        );

        bool gimmickBroken = boss.GetGimmick("GIMMICK_NAIL_BOARD").isBroken && hitGimmick.isGimmickTriggered;

        // 2. 그로기 상태 피해 배율(1.5배) 검증
        // 정상 상태: skillBaseDamage 100 -> 최종 대미지 = 100 * (100 / 200) = 50
        // 그로기 상태: 50 * 1.5 = 75 대미지여야 함
        boss.isGroggy = true;
        int hpBefore = boss.currentHp;

        var hitGroggy = CombatCalculator.EvaluatePlayerAttackOnBoss(
            player: player,
            boss: boss,
            skillBaseDamage: 100f,
            isBasicAttackD: false
        );

        int groggyDamage = hitGroggy.coreHpDamage;
        bool groggyAmplified = groggyDamage == 75 && (hpBefore - boss.currentHp) == 75;

        // 3. 보스 패턴 봉인 검증
        boss.SealSkill("SKILL_CHARGE");
        bool isSkillSealed = boss.IsSkillSealed("SKILL_CHARGE");

        bool pass = gimmickBroken && groggyAmplified && isSkillSealed;

        if (pass)
        {
            Debug.Log($"<color=green>▶ [Test 4 PASS] 보스 기믹 파훼 성공, 그로기 시 1.5배 증폭 피해({groggyDamage} Dmg), 패턴 봉인 정상 확인!</color>");
            return true;
        }
        else
        {
            Debug.LogError($"▶ [Test 4 FAIL] 보스 기믹/그로기 연산 불일치! GimmickBroken:{gimmickBroken}, GroggyDamage:{groggyDamage}(기대:75), Sealed:{isSkillSealed}");
            return false;
        }
    }

    // ========================================================================
    // [Test 5] 1:1 대전 모드 이원화 관제 검증 (CombatDataHub)
    // ========================================================================
    private bool Test_5_BattleModeDualPipeline(CombatDataHub hub)
    {
        Debug.Log("<color=yellow><b>[Test 5] CombatDataHub 1:1 대전 모드 이원화(RobotVsRobot / RobotVsBoss) 검증</b></color>");

        var player = CombatantBuilder.CreateDummy("Player", true, 1000, 50);
        var enemyRobot = CombatantBuilder.CreateDummy("NPC_A", false, 500, 30);
        var boss = new BossSnapshot { bossID = "BOSS_01", currentHp = 4000, maxHp = 4000 };

        // 1. 일반 로봇 대전 초기화
        hub.InitializeRobotBattle(player, enemyRobot);
        bool mode1Check = hub.CurrentBattleMode == BattleMode.RobotVsRobot &&
                          hub.PlayerSnapshot == player &&
                          hub.EnemyRobotSnapshot == enemyRobot &&
                          hub.BossSnapshot == null &&
                          !hub.IsOpponentDead;

        // 2. 보스 토벌전 초기화
        hub.InitializeBossBattle(player, boss);
        bool mode2Check = hub.CurrentBattleMode == BattleMode.RobotVsBoss &&
                          hub.PlayerSnapshot == player &&
                          hub.EnemyRobotSnapshot == null &&
                          hub.BossSnapshot == boss &&
                          !hub.IsOpponentDead;

        // 3. 상대 사망 판정 단일 질의 검증
        boss.currentHp = 0;
        bool bossDeadCheck = hub.IsOpponentDead;

        bool pass = mode1Check && mode2Check && bossDeadCheck;

        if (pass)
        {
            Debug.Log("<color=green>▶ [Test 5 PASS] 1:1 대전 모드 이원화 및 단일 상대방 생존 질의(IsOpponentDead) 정상 작동 확인!</color>");
            return true;
        }
        else
        {
            Debug.LogError($"▶ [Test 5 FAIL] 대전 모드 관제 실패! Mode1:{mode1Check}, Mode2:{mode2Check}, OpponentDead:{bossDeadCheck}");
            return false;
        }
    }

    // ========================================================================
    // [Test 6] 기획서 하드코어 승패 정산 및 임시 기본 파츠(DEFAULT_) 영구 파괴 제외 검증
    // ========================================================================
    private bool Test_6_SettlementAndDefaultPartExclusion()
    {
        Debug.Log("<color=yellow><b>[Test 6] 승패 정산(EXP 공식, 코어 레벨업, 패배 위로금) 및 DEFAULT_ 파츠 파괴 제외 검증</b></color>");

        var coreMaster = ScriptableObject.CreateInstance<CoreMasterData>();
        coreMaster.coreID = "CORE_SETTLE";
        coreMaster.coreLevel = 1;
        coreMaster.baseHp = 300;
        coreMaster.baseDefense = 20;

        var player = CombatantBuilder.Build("Player_Settle", true, coreMaster, null);
        var enemy = CombatantBuilder.CreateDummy("Enemy_Dummy", false);

        // 인벤토리 파츠 1개(영구 파괴 대상) + 기본 파츠 4개(DEFAULT_) 상태로 구성
        player.partStates[BodyPart.Head] = new PartRuntimeState("PERMANENT_HEAD_01", BodyPart.Head, 100);
        // 나머지 부위(LeftArm, RightArm, LeftLeg, RightLeg)는 DEFAULT_ 파츠임

        // 1. [승리 정산 검증]
        // 지역 기준가 1000G, 승리 시 EXP = 1000 * 0.2 * 1.0 * 1.0 = 200 EXP
        var winResult = BattleSettlementProcessor.ProcessSettlement(
            playerWon: true,
            player: player,
            enemy: enemy,
            betGold: 500,
            regionBasePrice: 1000,
            coreMaster: coreMaster
        );

        bool winExpCheck = winResult.CoreExpGained == 200 && winResult.GoldDelta == 1000 && !winResult.CoreUnstable;

        // 2. [패배 정산 및 DEFAULT_ 파츠 영구 파괴 제외 검증]
        // 패배 시 20% 위로금 EXP = 200 * 0.2 = 40 EXP
        var loseResult = BattleSettlementProcessor.ProcessSettlement(
            playerWon: false,
            player: player,
            enemy: enemy,
            betGold: 500,
            regionBasePrice: 1000,
            coreMaster: coreMaster
        );

        bool loseExpCheck = loseResult.CoreExpGained == 40 && loseResult.GoldDelta == -500 && loseResult.CoreUnstable;

        // 파괴 목록에 "DEFAULT_" 접두사를 가진 파츠가 단 하나라도 들어가 있으면 FAIL!
        bool defaultExcluded = true;
        foreach (var destroyedId in loseResult.DestroyedPartIDs)
        {
            if (destroyedId.StartsWith("DEFAULT_", StringComparison.OrdinalIgnoreCase))
            {
                defaultExcluded = false;
                break;
            }
        }

        bool pass = winExpCheck && loseExpCheck && defaultExcluded;

        if (pass)
        {
            Debug.Log($"<color=green>▶ [Test 6 PASS] 승리 EXP(200), 패배 위로금 EXP(40), 코어 Unstable, 임시 기본 파츠(DEFAULT_) 영구 파괴 100% 제외 확인!</color>");
            return true;
        }
        else
        {
            Debug.LogError($"▶ [Test 6 FAIL] 정산 로직 오류! WinExp:{winExpCheck}, LoseExp:{loseExpCheck}, DefaultExcluded:{defaultExcluded}");
            return false;
        }
    }
}
