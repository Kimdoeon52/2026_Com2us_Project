using System;

/// <summary>
/// [타격 판정 결과 DTO (HitResolutionResult)]
/// 1:1 대전(Robot vs Robot / Robot vs Boss) 타격 적중 시 교환하는 단일 표준 결과 DTO임.
/// NYH(전투 행동) 파트의 HitDetection 및 KnockbackSystem과의 100% 호환성을 보장함.
/// </summary>
[Serializable]
public class HitResolutionResult
{
    // ========================================================================
    // 1. 핵심 수치 판정 (NYH HitDetection / KnockbackSystem 연동)
    // ========================================================================
    /// <summary>코어 본체 또는 보스 HP에 가해진 실질 피해량 (NYH HitDetection.cs 연동)</summary>
    public int DamageToHp;

    /// <summary>피격 시 밀려나는 넉백 거리 (NYH KnockbackSystem.Apply 연동)</summary>
    public float KnockbackDistance;

    // ========================================================================
    // 2. 방어 및 회피 상태 플래그
    // ========================================================================
    /// <summary>Space 회피 무적(0.3s) 회피 성공 여부</summary>
    public bool IsEvaded;

    /// <summary>가드 성공 여부 (현재 2.5D 액션에서는 기본 false, 향후 스킬 대비)</summary>
    public bool IsGuarded;

    /// <summary>치명타(크리티컬) 적중 여부</summary>
    public bool IsCritical;

    // ========================================================================
    // 3. 부위 타격 스킬 판정 (보스/특정 스킬 전용)
    // ========================================================================
    /// <summary>피격 대상 부위 (일반 공격은 Core)</summary>
    public BodyPart TargetBodyPart = BodyPart.Core;

    /// <summary>부위 타격 스킬로 인한 파츠 내구도 피해량 (일반 공격 시 0)</summary>
    public int PartDurabilityDamage;

    /// <summary>해당 피격으로 부위 내구도가 0에 도달하여 스킬이 봉인되었는지 여부</summary>
    public bool IsPartDestroyed;

    // ========================================================================
    // 4. 실린더 및 보스 기믹 상태 플래그
    // ========================================================================
    /// <summary>D 기본기 적중으로 공격자 실린더 탄환이 +1 충전되었는지 여부</summary>
    public bool CylinderGained;

    /// <summary>보스 기믹(못판 펑크/약점 파훼 등)이 발동되었는지 여부</summary>
    public bool IsGimmickTriggered;

    // ========================================================================
    // 5. 하위 호환 헬퍼 (기존 KKH 내부 소문자 참조 및 구버전 호환)
    // ========================================================================
    public int coreHpDamage { get => DamageToHp; set => DamageToHp = value; }
    public bool isEvaded { get => IsEvaded; set => IsEvaded = value; }
    public bool isGuarded { get => IsGuarded; set => IsGuarded = value; }
    public bool isCritical { get => IsCritical; set => IsCritical = value; }
    public BodyPart targetBodyPart { get => TargetBodyPart; set => TargetBodyPart = value; }
    public int partDurabilityDamage { get => PartDurabilityDamage; set => PartDurabilityDamage = value; }
    public bool isPartDestroyed { get => IsPartDestroyed; set => IsPartDestroyed = value; }
    public bool cylinderGained { get => CylinderGained; set => CylinderGained = value; }
    public bool isGimmickTriggered { get => IsGimmickTriggered; set => IsGimmickTriggered = value; }

    // 구버전 복싱 레거시 필드 (테스터 호환)
    public int StaggerAdded;
    public int LeftArmDurabilityDamage;
    public int RightArmDurabilityDamage;
    public bool CausesKnockdown;
}
