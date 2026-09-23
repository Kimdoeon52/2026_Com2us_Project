using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [전투 데이터 허브 (CombatDataHub)]
/// 전투 행동 파트(NYH)에 로봇의 종합 스탯(이동속도, 공격력, 체력 등)을 공급하고,
/// 실시간 대미지 감쇄, 가드 내구도 소모, 위빙 페널티를 계산해주는 중앙 데이터 허브임.
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

    [Header("전투 참가자 런타임 스냅샷")]
    [Tooltip("플레이어 로봇 스냅샷 (스탯 및 부위 내구도)")]
    public CombatantSnapshot PlayerSnapshot;

    [Tooltip("적/상대 로봇 스냅샷 (스탯 및 부위 내구도)")]
    public CombatantSnapshot EnemySnapshot;

    // 부품 마스터 데이터 룩업 캐시 (ID -> PartMasterData)
    private Dictionary<string, PartMasterData> partMasterLookup = new Dictionary<string, PartMasterData>();

    // ========================================================================
    // 실시간 이벤트 (UI, 사운드, 연출, 타 파트 구독용)
    // ========================================================================
    /// <summary>체력 변경 통보 이벤트: (fighterId, currentHp, maxHp)</summary>
    public event Action<string, int, int> OnHpChanged;

    /// <summary>부위 내구도 변경 통보 이벤트: (fighterId, BodyPart, curDurability, maxDurability)</summary>
    public event Action<string, BodyPart, int, int> OnPartDurabilityChanged;

    /// <summary>타격 판정 연산 완료 통보 이벤트임</summary>
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
        }
    }

    /// <summary>
    /// 허브에 전투 참가자 스냅샷 등록함
    /// </summary>
    public void RegisterCombatants(CombatantSnapshot player, CombatantSnapshot enemy)
    {
        PlayerSnapshot = player;
        EnemySnapshot = enemy;

        Debug.Log($"[CombatDataHub] 참가자 스냅샷 등록 완료 - " +
                  $"Player({player?.fighterID}): HP={player?.currentHp}, Atk={player?.totalAttackPower}, Spd={player?.finalMoveSpeed} | " +
                  $"Enemy({enemy?.fighterID}): HP={enemy?.currentHp}, Atk={enemy?.totalAttackPower}, Spd={enemy?.finalMoveSpeed}");
    }

    /// <summary>
    /// 부품 마스터 에셋 룩업 테이블 등록함
    /// </summary>
    public void RegisterMasterData(IEnumerable<PartMasterData> masterDatas)
    {
        if (masterDatas == null) return;
        foreach (var data in masterDatas)
        {
            if (data != null && !string.IsNullOrEmpty(data.partID))
            {
                partMasterLookup[data.partID] = data;
            }
        }
    }

    public PartMasterData GetMasterData(string partId)
    {
        if (partMasterLookup.TryGetValue(partId, out var data))
            return data;
        return null;
    }

    /// <summary>
    /// 식별자("Player", "Enemy" 등)를 통한 스냅샷 조회함
    /// </summary>
    public CombatantSnapshot GetSnapshot(string fighterId)
    {
        if (PlayerSnapshot != null && PlayerSnapshot.fighterID.Equals(fighterId, StringComparison.OrdinalIgnoreCase))
            return PlayerSnapshot;
        if (EnemySnapshot != null && EnemySnapshot.fighterID.Equals(fighterId, StringComparison.OrdinalIgnoreCase))
            return EnemySnapshot;

        // 기본 매핑 폴백: 대소문자 무관 검색함
        if (fighterId.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
            return PlayerSnapshot;
        if (fighterId.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0)
            return EnemySnapshot;

        return null;
    }

    // ========================================================================
    // 1. 남윤호(NYH) 파트 스탯 조회 API
    // ========================================================================

    /// <summary>기획서 공식으로 계산된 최종 이동속도 반환: (왼다리 + 오른다리) / 2</summary>
    public float GetFinalMoveSpeed(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.finalMoveSpeed : 3f;
    }

    /// <summary>기획서 공식으로 계산된 총 공격력 반환: 기본Atk + (왼팔 + 오른팔) / 2</summary>
    public int GetTotalAttackPower(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.totalAttackPower : 100;
    }

    /// <summary>현재 코어 체력(HP) 반환함</summary>
    public int GetCurrentHp(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.currentHp : 0;
    }

    /// <summary>최대 코어 체력(HP) 반환함</summary>
    public int GetMaxHp(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.maxHp : 1000;
    }

    /// <summary>코어 본체 방어력 반환함</summary>
    public int GetBaseDefense(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.baseDefense : 50;
    }

    /// <summary>가드 시 팔 파츠가 제공하는 추가 방어력 보정치 반환함</summary>
    public int GetGuardDefBonus(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.guardDefBonus : 0;
    }

    /// <summary>위빙 성공 시 다리 파츠가 제공하는 추가 무적 보정 반환함</summary>
    public float GetInvincibleBonus(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        return s != null ? s.invincibleBonus : 0f;
    }

    /// <summary>특정 부위의 현재 실시간 내구도 반환함</summary>
    public int GetPartDurability(string fighterId, BodyPart part)
    {
        var s = GetSnapshot(fighterId);
        var p = s?.GetPartRuntimeState(part);
        return p != null ? p.currentDurability : 0;
    }

    /// <summary>특정 부위의 최대 내구도 반환함</summary>
    public int GetPartMaxDurability(string fighterId, BodyPart part)
    {
        var s = GetSnapshot(fighterId);
        var p = s?.GetPartRuntimeState(part);
        return p != null ? p.maxDurability : 0;
    }

    /// <summary>특정 부위가 파손(내구도 0)되었거나 미장착 상태인지 확인함</summary>
    public bool IsPartBroken(string fighterId, BodyPart part)
    {
        var s = GetSnapshot(fighterId);
        return s == null || s.IsPartBroken(part);
    }

    // ========================================================================
    // 2. 남윤호(NYH) 파트 실시간 전투 연동 인터페이스
    // ========================================================================

    /// <summary>
    /// [행동 가능 여부 질의 - CanExecuteAction]
    /// ActionExecutor에서 틱마다 입력을 받아 기술을 시작하기 직전에 호출함.
    /// - CoreFixed(잽, 가드, 위빙, 이동): 항상 true임
    /// - Part(훅, 스트레이트, 어퍼컷, 백스핀 등): 요구 부위 파손 시 false 반환해서 시전 차단함
    /// </summary>
    public bool CanExecuteAction(string fighterId, ActionData action)
    {
        var actor = GetSnapshot(fighterId);
        if (actor == null)
            return true; // 스냅샷이 없으면 기본 허용함

        return CombatCalculator.CanExecuteAction(actor, action);
    }

    /// <summary>
    /// [타격 적중 시 대미지 감쇄 연산 - ProcessHit]
    /// 공격 히트박스가 상대 허트박스에 닿았을 때 호출함.
    /// 기획서 방어 감쇄 공식, 가드 시 양팔 내구도 5:5 분산 소모, 위빙 무적 회피, 크리티컬 등을 연산해서 반환함.
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

        if (attacker == null || defender == null)
        {
            Debug.LogWarning($"[CombatDataHub] ProcessHit 실패: 참가자 스냅샷을 찾을 수 없음 (Attacker: {attackerId}, Defender: {defenderId})");
            return new HitResolutionResult();
        }

        // 순수 수치 판정기 호출함 (피격 부위 전달)
        var result = CombatCalculator.EvaluateHit(attacker, defender, attackAction, isGuarding, isWeaving, hitPart);

        // 이벤트 알림 처리함
        if (result.DamageToHp > 0)
        {
            OnHpChanged?.Invoke(defender.fighterID, defender.currentHp, defender.maxHp);
        }

        // 가드로 인한 양팔 내구도 차감 이벤트
        if (result.LeftArmDurabilityDamage > 0)
        {
            var arm = defender.GetPartRuntimeState(BodyPart.LeftArm);
            if (arm != null)
                OnPartDurabilityChanged?.Invoke(defender.fighterID, BodyPart.LeftArm, arm.currentDurability, arm.maxDurability);
        }

        if (result.RightArmDurabilityDamage > 0)
        {
            var arm = defender.GetPartRuntimeState(BodyPart.RightArm);
            if (arm != null)
                OnPartDurabilityChanged?.Invoke(defender.fighterID, BodyPart.RightArm, arm.currentDurability, arm.maxDurability);
        }

        // 직접 피격된 파츠(머리, 다리 등) 내구도 차감 이벤트
        if (hitPart != BodyPart.Core && hitPart != BodyPart.LeftArm && hitPart != BodyPart.RightArm)
        {
            var state = defender.GetPartRuntimeState(hitPart);
            if (state != null)
                OnPartDurabilityChanged?.Invoke(defender.fighterID, hitPart, state.currentDurability, state.maxDurability);
        }

        OnHitResolved?.Invoke(result);

        // 코어 HP가 0 이하로 떨어지면 BattleManager에 통보함
        if (defender.currentHp <= 0 && BattleManager.Instance != null)
        {
            BattleManager.Instance.OnFighterKilled(defender.fighterID);
        }

        return result;
    }

    /// <summary>
    /// [위빙(회피) 시도 처리 - ProcessWeavingAttempt]
    /// 플레이어 또는 AI가 위빙을 시도할 때 호출함.
    /// 좌/우 다리 중 무작위 1개 내구도 5를 차감하며, 다리 1개 파손 시 50% 확률로 실패를 반환함.
    /// </summary>
    public bool ProcessWeavingAttempt(string fighterId)
    {
        var actor = GetSnapshot(fighterId);
        if (actor == null) return true;

        bool success = CombatCalculator.EvaluateWeavingAttempt(actor);

        // 다리 내구도 변경 이벤트 알림 처리함
        var leftLeg = actor.GetPartRuntimeState(BodyPart.LeftLeg);
        if (leftLeg != null)
            OnPartDurabilityChanged?.Invoke(actor.fighterID, BodyPart.LeftLeg, leftLeg.currentDurability, leftLeg.maxDurability);

        var rightLeg = actor.GetPartRuntimeState(BodyPart.RightLeg);
        if (rightLeg != null)
            OnPartDurabilityChanged?.Invoke(actor.fighterID, BodyPart.RightLeg, rightLeg.currentDurability, rightLeg.maxDurability);

        return success;
    }

    // ========================================================================
    // 3. 직접 수치 조작 및 실시간 이벤트 발생 API (테스트 및 특수 피격용)
    // ========================================================================

    /// <summary>
    /// [코어 체력 직접 차감]
    /// 유효타 피격, 크리티컬 추가타 또는 HUD 테스트 시 코어 체력을 직접 삭감하고 OnHpChanged 이벤트를 호출함.
    /// </summary>
    public void ApplyCoreDamage(string fighterId, int damage)
    {
        var s = GetSnapshot(fighterId);
        if (s == null) return;

        s.currentHp = Mathf.Max(0, s.currentHp - damage);
        OnHpChanged?.Invoke(s.fighterID, s.currentHp, s.maxHp);

        if (s.currentHp <= 0 && BattleManager.Instance != null)
        {
            BattleManager.Instance.OnFighterKilled(s.fighterID);
        }
    }

    /// <summary>
    /// [특정 부위 내구도 직접 차감]
    /// 가드/치명타/특수 상태이상 또는 HUD 테스트 시 해당 부위 내구도를 차감하고 OnPartDurabilityChanged 이벤트를 호출함.
    /// </summary>
    public void ConsumePartDurability(string fighterId, BodyPart part, int amount)
    {
        var s = GetSnapshot(fighterId);
        if (s == null) return;

        s.ConsumePartDurability(part, amount);
        var state = s.GetPartRuntimeState(part);
        if (state != null)
        {
            OnPartDurabilityChanged?.Invoke(s.fighterID, part, state.currentDurability, state.maxDurability);
        }
    }

    /// <summary>
    /// [파츠 유효타 피격 처리: 파츠 내구도 + 코어 체력 동시 차감]
    /// 특정 부위 피격 시 해당 파츠 내구도를 깎음과 동시에, 기체 본체인 코어 체력(HP)에도 피해를 전달함.
    /// </summary>
    public void ApplyPartHit(string fighterId, BodyPart part, int partDamage, int coreDamage)
    {
        var s = GetSnapshot(fighterId);
        if (s == null) return;

        // 1. 파츠 내구도 차감
        if (partDamage > 0)
        {
            s.ConsumePartDurability(part, partDamage);
            var state = s.GetPartRuntimeState(part);
            if (state != null)
            {
                OnPartDurabilityChanged?.Invoke(s.fighterID, part, state.currentDurability, state.maxDurability);
            }
        }

        // 2. 코어 체력 동시 차감 (기획서: 파츠 피격 시 본체 생명력 동반 감소)
        if (coreDamage > 0)
        {
            s.currentHp = Mathf.Max(0, s.currentHp - coreDamage);
            OnHpChanged?.Invoke(s.fighterID, s.currentHp, s.maxHp);

            if (s.currentHp <= 0 && BattleManager.Instance != null)
            {
                BattleManager.Instance.OnFighterKilled(s.fighterID);
            }
        }
    }

    /// <summary>
    /// [전투 참가자 상태 전체 초기화]
    /// 코어 체력 및 5개 파츠 내구도를 최대치로 완전 복구하고 UI를 갱신함.
    /// </summary>
    public void ResetFighter(string fighterId)
    {
        var s = GetSnapshot(fighterId);
        if (s == null) return;

        s.currentHp = s.maxHp;
        OnHpChanged?.Invoke(s.fighterID, s.currentHp, s.maxHp);

        foreach (var kvp in s.partStates)
        {
            kvp.Value.currentDurability = kvp.Value.maxDurability;
            OnPartDurabilityChanged?.Invoke(s.fighterID, kvp.Key, kvp.Value.currentDurability, kvp.Value.maxDurability);
        }
    }
}

