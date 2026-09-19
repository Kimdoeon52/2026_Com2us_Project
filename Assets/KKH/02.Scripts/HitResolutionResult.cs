using System;

/// <summary>
/// [타격 판정 결과 DTO]
/// 전투 행동 파트(NYH)와 데이터 허브(KKH) 간에 타격 적중 시 교환하는 결과 데이터 객체임.
/// </summary>
[Serializable]
public class HitResolutionResult
{
    /// <summary>코어 본체 HP에 실질적으로 가해진 피해량임</summary>
    public int DamageToHp;

    /// <summary>가드 성공 여부임 (true면 코어 피해 대신 팔 내구도 차감됨)</summary>
    public bool IsGuarded;

    /// <summary>위빙(회피) 성공 여부임 (true면 무적 처리되어 피해 0임)</summary>
    public bool IsEvaded;

    /// <summary>치명타(크리티컬) 적중 여부임</summary>
    public bool IsCritical;

    /// <summary>피격자에게 누적될 경직도(Stagger) 수치임 (ActionData.StaggerValue 전달됨)</summary>
    public int StaggerAdded;

    /// <summary>가드 시 소모된 왼팔 내구도 피해량임</summary>
    public int LeftArmDurabilityDamage;

    /// <summary>가드 시 소모된 오른팔 내구도 피해량임</summary>
    public int RightArmDurabilityDamage;

    /// <summary>다운(Knockdown) 유발 여부임 (ActionData.CausesKnockdown 전달됨)</summary>
    public bool CausesKnockdown;

    /// <summary>넉백 거리임</summary>
    public float KnockbackDistance;
}
