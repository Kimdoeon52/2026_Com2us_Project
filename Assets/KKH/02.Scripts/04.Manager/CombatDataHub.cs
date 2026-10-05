using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1:1 대전 모드 열거형
/// </summary>
public enum BattleMode
{
    RobotVsRobot, // 일반 적(NPC A·B·C) 1:1 로봇 대전
    RobotVsBoss   // 보스(정크 휠러 등) 1:1 토벌전
}

/// <summary>
/// [전투 데이터 허브 (CombatDataHub)]
/// 전투 행동 파트(NYH), 일반 적 AI(LJS), 스킬 파트(CSH) 및 UI 간의 중앙 데이터 관제 허브임.
/// 1:1 대전 모드(Robot vs Robot / Robot vs Boss)를 통합 관제하고, 실시간 피격 연산 및 브로드캐스팅을 수행함.
/// </summary>
public class CombatDataHub : MonoBehaviour
{
    private static CombatDataHub instance;
    public static CombatDataHub Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CombatDataHub>();
            }
            return instance;
        }
    }

    [Header("1. 대전 모드 및 1:1 참가자 스냅샷")]
    [SerializeField] private BattleMode currentBattleMode = BattleMode.RobotVsRobot;
    public BattleMode CurrentBattleMode => currentBattleMode;

    [Tooltip("플레이어 로봇 스냅샷")]
    public CombatantSnapshot PlayerSnapshot;

    [Tooltip("[모드 1 전용] 일반 적(NPC A·B·C) 로봇 스냅샷")]
    public CombatantSnapshot EnemySnapshot;
    public CombatantSnapshot EnemyRobotSnapshot => EnemySnapshot;

    [Tooltip("[모드 2 전용] 보스 스냅샷 (범용 기믹 DTO)")]
    public BossSnapshot BossSnapshot;

    /// <summary>상대방(Opponent)의 사망 여부 (모드에 따라 적 로봇 또는 보스 생존 판정)</summary>
    public bool IsOpponentDead => currentBattleMode == BattleMode.RobotVsRobot
        ? (EnemySnapshot == null || !EnemySnapshot.IsAlive)
        : (BossSnapshot == null || !BossSnapshot.IsAlive);

    // 부품 마스터 데이터 룩업 캐시 (ID -> PartMasterData)
    private Dictionary<string, PartMasterData> partMasterLookup = new Dictionary<string, PartMasterData>();

    // ========================================================================
    // 브로드캐스팅 이벤트 (UI, 사운드, 연출, 타 파트 구독용)
    // ========================================================================
    // [플레이어 UI 이벤트]
    public event Action<int, int> OnPlayerHpChanged;
    public event Action<int, int> OnPlayerCylinderChanged;
    public event Action<BodyPart, int, int> OnPlayerPartDurabilityChanged;
    public event Action<BodyPart> OnPlayerPartBroken;

    // [일반전 상대 로봇 이벤트]
    public event Action<int, int> OnEnemyHpChanged;
    public event Action<BodyPart, int, int> OnEnemyPartDurabilityChanged;
    public event Action<BodyPart> OnEnemyPartBroken;

    // [보스전 전용 이벤트]
    public event Action<int, int, int> OnBossHpChanged; // curHp, maxHp, phase
    public event Action<string, bool> OnBossGimmickTriggered; // gimmickId, isGroggy
    public event Action<string> OnBossGimmickBroken;          // gimmickId
    public event Action<int, int, int> OnCoreLevelUp;         // newLv, newHp, newDef

    // [레거시 호환 이벤트]
    public event Action<string, int, int> OnHpChanged;
    public event Action<string, BodyPart, int, int> OnPartDurabilityChanged;
    public event Action<HitResolutionResult> OnHitResolved;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ValidateAndRepairSnapshots();
    }

    private void ValidateAndRepairSnapshots()
    {
        RepairSnapshot(PlayerSnapshot, "Player", true);
        RepairSnapshot(EnemySnapshot, "Enemy", false);
    }

    private void RepairSnapshot(CombatantSnapshot s, string defaultId, bool isPlayer)
    {
        if (s == null) return;
        if (string.IsNullOrEmpty(s.fighterID)) s.fighterID = defaultId;
        s.isPlayer = isPlayer;

        if (s.maxHp <= 0) s.maxHp = 1000;
        if (s.currentHp <= 0) s.currentHp = s.maxHp;

        if (s.partStates == null)
        {
            s.partStates = new Dictionary<BodyPart, PartRuntimeState>();
        }

        var parts = new[] { BodyPart.Head, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };
        foreach (var p in parts)
        {
            if (!s.partStates.ContainsKey(p) || s.partStates[p] == null)
            {
                s.partStates[p] = new PartRuntimeState($"DEFAULT_{p}", p, 100);
            }
        }
    }

    // ========================================================================
    // 전투 초기화 (모드별 1:1 대전 진입)
    // ========================================================================
    /// <summary>일반 적(NPC A·B·C)과의 1:1 로봇 대전 초기화</summary>
    public void InitializeRobotBattle(CombatantSnapshot player, CombatantSnapshot enemyRobot)
    {
        currentBattleMode = BattleMode.RobotVsRobot;
        PlayerSnapshot = player;
        EnemySnapshot = enemyRobot;
        BossSnapshot = null;

        Debug.Log($"[CombatDataHub] 1:1 로봇 대전 초기화 완료 - Player({player?.fighterID}) vs Enemy({enemyRobot?.fighterID})");
        BroadcastFullState();
    }

    /// <summary>보스와의 1:1 토벌전 초기화</summary>
    public void InitializeBossBattle(CombatantSnapshot player, BossSnapshot boss)
    {
        currentBattleMode = BattleMode.RobotVsBoss;
        PlayerSnapshot = player;
        BossSnapshot = boss;
        EnemySnapshot = null;

        Debug.Log($"[CombatDataHub] 1:1 보스 토벌전 초기화 완료 - Player({player?.fighterID}) vs Boss({boss?.bossName})");
        BroadcastFullState();
    }

    /// <summary>기존 레거시 등록 함수 호환</summary>
    public void RegisterCombatants(CombatantSnapshot player, CombatantSnapshot enemy)
    {
        InitializeRobotBattle(player, enemy);
    }

    public void RegisterCombatant(CombatantSnapshot combatant)
    {
        if (combatant == null) return;
        if (combatant.isPlayer || combatant.fighterID.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
            PlayerSnapshot = combatant;
        else
            EnemySnapshot = combatant;
    }

    // ========================================================================
    // 실시간 피격 및 연산 질의 (NYH HitDetection 호출 Façade)
    // ========================================================================
    /// <summary>
    /// [NYH HitDetection 전용 1:1 대전 통합 타격 처리]
    /// </summary>
    public HitResolutionResult ProcessHit(
        string attackerId,
        string defenderId,
        ActionData attackAction,
        bool isGuarding,
        bool isWeaving,
        BodyPart hitPart = BodyPart.Core)
    {
        var attacker = GetSnapshot(attackerId);
        var defender = GetSnapshot(defenderId);

        HitResolutionResult result = null;

        // 1. 일반 적 로봇 vs 로봇 1:1 대전
        if (currentBattleMode == BattleMode.RobotVsRobot && attacker != null && defender != null)
        {
            result = CombatCalculator.EvaluateRobotAttack(
                attacker, defender, attackAction, isGuarding, isWeaving, hitPart);

            // 상태 변경 브로드캐스팅
            NotifyHpAndCylinder(attacker, defender);
        }
        // 2. 보스전 (Robot vs Boss)
        else if (currentBattleMode == BattleMode.RobotVsBoss)
        {
            bool isAttackerPlayer = attackerId.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isAttackerPlayer && PlayerSnapshot != null && BossSnapshot != null)
            {
                // 플레이어 -> 보스 타격
                bool isD = attackAction != null && (attackAction.ActionName == "D" || attackAction.Source == ActionSource.CoreFixed);
                float dmg = attackAction != null ? attackAction.Damage : 10f;
                result = CombatCalculator.EvaluatePlayerAttackOnBoss(PlayerSnapshot, BossSnapshot, dmg, isD);
                result.KnockbackDistance = attackAction != null ? attackAction.KnockbackDistance : 0f;

                OnPlayerCylinderChanged?.Invoke(PlayerSnapshot.currentCylinder, CombatantSnapshot.MaxCylinder);
                OnBossHpChanged?.Invoke(BossSnapshot.currentHp, BossSnapshot.maxHp, BossSnapshot.currentPhase);
                OnHpChanged?.Invoke(BossSnapshot.bossID, BossSnapshot.currentHp, BossSnapshot.maxHp);
            }
            else if (!isAttackerPlayer && BossSnapshot != null && PlayerSnapshot != null)
            {
                // 보스 -> 플레이어 타격
                float dmg = attackAction != null ? attackAction.Damage : 30f;
                result = CombatCalculator.EvaluateBossAttack(BossSnapshot, PlayerSnapshot, dmg, hitPart, 15, isWeaving);
                result.KnockbackDistance = attackAction != null ? attackAction.KnockbackDistance : 0f;

                OnPlayerHpChanged?.Invoke(PlayerSnapshot.currentHp, PlayerSnapshot.maxHp);
                OnHpChanged?.Invoke(PlayerSnapshot.fighterID, PlayerSnapshot.currentHp, PlayerSnapshot.maxHp);
            }
        }

        if (result == null)
            result = new HitResolutionResult();

        OnHitResolved?.Invoke(result);

        // 사망 판정 통보
        CheckAndNotifyDeath();

        return result;
    }

    /// <summary>
    /// [수치 직접 전달형 1:1 대전 통합 타격 처리]
    /// </summary>
    public HitResolutionResult ProcessHit(
        string attackerId,
        string defenderId,
        float rawDamage,
        BodyPart targetPart = BodyPart.Core,
        int partDamage = 0,
        bool isBasicAttackD = false,
        GimmickTag tag = GimmickTag.None,
        string targetGimmickId = null)
    {
        var attacker = GetSnapshot(attackerId);
        var defender = GetSnapshot(defenderId);

        HitResolutionResult result = null;

        if (currentBattleMode == BattleMode.RobotVsRobot && attacker != null && defender != null)
        {
            result = CombatCalculator.EvaluateRobotAttack(
                attacker, defender, rawDamage, targetPart, partDamage, false, isBasicAttackD);
            NotifyHpAndCylinder(attacker, defender);
        }
        else if (currentBattleMode == BattleMode.RobotVsBoss)
        {
            bool isAttackerPlayer = attackerId.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isAttackerPlayer && PlayerSnapshot != null && BossSnapshot != null)
            {
                result = CombatCalculator.EvaluatePlayerAttackOnBoss(PlayerSnapshot, BossSnapshot, rawDamage, isBasicAttackD, tag, targetGimmickId);
                OnPlayerCylinderChanged?.Invoke(PlayerSnapshot.currentCylinder, CombatantSnapshot.MaxCylinder);
                OnBossHpChanged?.Invoke(BossSnapshot.currentHp, BossSnapshot.maxHp, BossSnapshot.currentPhase);
            }
            else if (!isAttackerPlayer && BossSnapshot != null && PlayerSnapshot != null)
            {
                result = CombatCalculator.EvaluateBossAttack(BossSnapshot, PlayerSnapshot, rawDamage, targetPart, partDamage, false);
                OnPlayerHpChanged?.Invoke(PlayerSnapshot.currentHp, PlayerSnapshot.maxHp);
            }
        }

        if (result == null)
            result = new HitResolutionResult();

        OnHitResolved?.Invoke(result);
        CheckAndNotifyDeath();
        return result;
    }

    private void NotifyHpAndCylinder(CombatantSnapshot attacker, CombatantSnapshot defender)
    {
        if (attacker.isPlayer)
        {
            OnPlayerCylinderChanged?.Invoke(attacker.currentCylinder, CombatantSnapshot.MaxCylinder);
        }

        if (defender.isPlayer)
        {
            OnPlayerHpChanged?.Invoke(defender.currentHp, defender.maxHp);
            OnHpChanged?.Invoke(defender.fighterID, defender.currentHp, defender.maxHp);
        }
        else
        {
            OnEnemyHpChanged?.Invoke(defender.currentHp, defender.maxHp);
            OnHpChanged?.Invoke(defender.fighterID, defender.currentHp, defender.maxHp);
        }
    }

    private void CheckAndNotifyDeath()
    {
        if (PlayerSnapshot != null && PlayerSnapshot.currentHp <= 0)
        {
            BattleManager.Instance?.OnFighterKilled(PlayerSnapshot.fighterID);
        }
        else if (currentBattleMode == BattleMode.RobotVsRobot && EnemySnapshot != null && EnemySnapshot.currentHp <= 0)
        {
            BattleManager.Instance?.OnFighterKilled(EnemySnapshot.fighterID);
        }
        else if (currentBattleMode == BattleMode.RobotVsBoss && BossSnapshot != null && BossSnapshot.currentHp <= 0)
        {
            BattleManager.Instance?.OnFighterKilled(BossSnapshot.bossID);
        }
    }

    // ========================================================================
    // 행동 실행 가능 게이트 (NYH ActionExecutor 연동)
    // ========================================================================
    public bool CanExecuteAction(string fighterId, ActionData action)
    {
        var actor = GetSnapshot(fighterId);
        return CombatCalculator.CanExecuteAction(actor, action);
    }

    public bool CanExecuteAction(string fighterId, ActionSource source, BodyPart requiredPart, int cylinderCost = 0)
    {
        var actor = GetSnapshot(fighterId);
        return CombatCalculator.CanExecuteAction(actor, source, requiredPart, cylinderCost);
    }

    // ========================================================================
    // 브로드캐스팅 헬퍼 메서드
    // ========================================================================
    public void BroadcastPartBroken(string fighterId, BodyPart brokenPart)
    {
        bool isPlayer = fighterId.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
        if (isPlayer)
            OnPlayerPartBroken?.Invoke(brokenPart);
        else
            OnEnemyPartBroken?.Invoke(brokenPart);

        Debug.Log($"<color=red>[CombatDataHub] {fighterId} {brokenPart} 파손 및 스킬 봉인 브로드캐스팅!</color>");
    }

    public void BroadcastCoreLevelUp(int newLv, int newHp, int newDef)
    {
        OnCoreLevelUp?.Invoke(newLv, newHp, newDef);
    }

    public void BroadcastFullState()
    {
        if (PlayerSnapshot != null)
        {
            OnPlayerHpChanged?.Invoke(PlayerSnapshot.currentHp, PlayerSnapshot.maxHp);
            OnPlayerCylinderChanged?.Invoke(PlayerSnapshot.currentCylinder, CombatantSnapshot.MaxCylinder);
            foreach (var kvp in PlayerSnapshot.partStates)
            {
                OnPlayerPartDurabilityChanged?.Invoke(kvp.Key, kvp.Value.currentDurability, kvp.Value.maxDurability);
            }
        }

        if (currentBattleMode == BattleMode.RobotVsRobot && EnemySnapshot != null)
        {
            OnEnemyHpChanged?.Invoke(EnemySnapshot.currentHp, EnemySnapshot.maxHp);
            foreach (var kvp in EnemySnapshot.partStates)
            {
                OnEnemyPartDurabilityChanged?.Invoke(kvp.Key, kvp.Value.currentDurability, kvp.Value.maxDurability);
            }
        }
        else if (currentBattleMode == BattleMode.RobotVsBoss && BossSnapshot != null)
        {
            OnBossHpChanged?.Invoke(BossSnapshot.currentHp, BossSnapshot.maxHp, BossSnapshot.currentPhase);
        }
    }

    // ========================================================================
    // 참가자 조회 및 스탯 헬퍼
    // ========================================================================
    public CombatantSnapshot GetSnapshot(string fighterId)
    {
        if (PlayerSnapshot != null && (PlayerSnapshot.fighterID.Equals(fighterId, StringComparison.OrdinalIgnoreCase) || fighterId.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0))
            return PlayerSnapshot;
        if (EnemySnapshot != null && (EnemySnapshot.fighterID.Equals(fighterId, StringComparison.OrdinalIgnoreCase) || fighterId.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0 || fighterId.IndexOf("NPC", StringComparison.OrdinalIgnoreCase) >= 0))
            return EnemySnapshot;

        return null;
    }

    public float GetFinalMoveSpeed(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.finalMoveSpeed : 3f;
    }

    public int GetTotalAttackPower(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.totalAttackPower : 20;
    }

    public int GetCurrentHp(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        if (s != null) return s.currentHp;
        if (BossSnapshot != null && fighterId.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
            return BossSnapshot.currentHp;
        return 0;
    }

    public int GetMaxHp(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        if (s != null) return s.maxHp;
        if (BossSnapshot != null && fighterId.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
            return BossSnapshot.maxHp;
        return 1000;
    }

    public int GetPartDurability(string fighterId, BodyPart part)
    {
        var s = GetSnapshot(fighterId);
        var p = s?.GetPartRuntimeState(part);
        return p != null ? p.currentDurability : 0;
    }

    public int GetPartMaxDurability(string fighterId, BodyPart part)
    {
        var s = GetSnapshot(fighterId);
        var p = s?.GetPartRuntimeState(part);
        return p != null ? p.maxDurability : 0;
    }

    public bool IsPartBroken(string fighterId, BodyPart part)
    {
        var s = GetSnapshot(fighterId);
        return s == null || s.IsPartBroken(part);
    }

    // ========================================================================
    // 직접 조작 헬퍼 (테스트 및 HUD용)
    // ========================================================================
    public void ApplyCoreDamage(string fighterId, int damage)
    {
        var s = GetSnapshot(fighterId);
        if (s != null)
        {
            s.currentHp = Mathf.Max(0, s.currentHp - damage);
            OnHpChanged?.Invoke(s.fighterID, s.currentHp, s.maxHp);
            if (s.isPlayer) OnPlayerHpChanged?.Invoke(s.currentHp, s.maxHp);
            else OnEnemyHpChanged?.Invoke(s.currentHp, s.maxHp);

            if (s.currentHp <= 0) BattleManager.Instance?.OnFighterKilled(s.fighterID);
        }
        else if (BossSnapshot != null && fighterId.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            BossSnapshot.currentHp = Mathf.Max(0, BossSnapshot.currentHp - damage);
            OnBossHpChanged?.Invoke(BossSnapshot.currentHp, BossSnapshot.maxHp, BossSnapshot.currentPhase);
            if (BossSnapshot.currentHp <= 0) BattleManager.Instance?.OnFighterKilled(BossSnapshot.bossID);
        }
    }

    public void ApplyPartHit(string fighterId, BodyPart part, int partDamage, int coreDamage)
    {
        var s = GetSnapshot(fighterId);
        if (s == null) return;

        if (partDamage > 0)
        {
            bool isBroken = s.ConsumePartDurability(part, partDamage);
            var state = s.GetPartRuntimeState(part);
            if (state != null)
            {
                OnPartDurabilityChanged?.Invoke(s.fighterID, part, state.currentDurability, state.maxDurability);
                if (s.isPlayer) OnPlayerPartDurabilityChanged?.Invoke(part, state.currentDurability, state.maxDurability);
                else OnEnemyPartDurabilityChanged?.Invoke(part, state.currentDurability, state.maxDurability);
            }
            if (isBroken) BroadcastPartBroken(s.fighterID, part);
        }

        if (coreDamage > 0)
        {
            ApplyCoreDamage(fighterId, coreDamage);
        }
    }

    public void ResetFighter(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        if (s == null) return;

        s.currentHp = s.maxHp;
        s.currentCylinder = 1;
        if (s.isPlayer)
        {
            OnPlayerHpChanged?.Invoke(s.currentHp, s.maxHp);
            OnPlayerCylinderChanged?.Invoke(s.currentCylinder, CombatantSnapshot.MaxCylinder);
        }
        else
        {
            OnEnemyHpChanged?.Invoke(s.currentHp, s.maxHp);
        }

        foreach (var kvp in s.partStates)
        {
            kvp.Value.currentDurability = kvp.Value.maxDurability;
            if (s.isPlayer)
                OnPlayerPartDurabilityChanged?.Invoke(kvp.Key, kvp.Value.currentDurability, kvp.Value.maxDurability);
            else
                OnEnemyPartDurabilityChanged?.Invoke(kvp.Key, kvp.Value.currentDurability, kvp.Value.maxDurability);
        }
    }
}
