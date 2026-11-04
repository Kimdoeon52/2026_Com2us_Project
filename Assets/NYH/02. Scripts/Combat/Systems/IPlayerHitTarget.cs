using UnityEngine;

/// <summary>
/// 플레이어 공격에 맞는 보스 쪽 "자기 체력이 따로 있는" 대상 (2026-11-04, 쿄다이 촉수).
///
/// HitDetection이 플레이어 Hit박스·투사체 자리에서 이 컴포넌트가 붙은 콜라이더(또는 그 부모)를 찾으면,
/// 보스 HP(허브 ProcessHit)로 보내지 않고 여기로 데미지를 넘긴다. 보스 밑에 있어도 보스는 안 깎인다.
/// 데미지는 공격 ActionData.Damage 그대로(방어력·배율 없음 — 대상 체력이 기획 수치가 아니라 임시값이라 단순하게).
/// </summary>
public interface IPlayerHitTarget
{
    /// <summary>지금 맞을 수 있는지 (나오는 중·죽는 중이면 false)</summary>
    bool IsHittable { get; }

    /// <summary>플레이어 공격에 맞음. point = 맞은 대략의 자리(월드)</summary>
    void TakePlayerHit(float damage, Vector2 point);
}
