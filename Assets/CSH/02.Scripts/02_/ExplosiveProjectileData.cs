using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 터질 때 범위 피해를 주는 투사체 데이터 (기획서 붐버: 착탄 폭발, 최대 사거리 도달 시 공중 폭발).
/// ProjectileData 상속 — 기존 ActionData.projectile 칸에 그대로 꽂는다. 터지면 ProjectileExplosion.Detonate가 범위 판정.
/// 데미지는 쏜 행동(ActionData.Damage)을 쓴다. 일반/강화는 행동 에셋 2개 + 이 에셋 2개로 나눈다.
/// </summary>
[CreateAssetMenu(menuName = "CSH/Combat/Explosive Projectile Data", fileName = "NewExplosiveProjectileData")]
public class ExplosiveProjectileData : ProjectileData
{
    [field: Header("폭발 범위")]
    [field: Tooltip("폭발 반경(사거리와 같은 단위). 붐버: 일반 1.5 / 강화 2.5")]
    [field: Min(0.01f)] [field: SerializeField] public float ExplosionRadius { get; private set; } = 1.5f;

    [field: Header("폭발 대상")]
    [field: Tooltip("상대 로봇(로봇전)의 Hurt박스를 맞힌다")]
    [field: SerializeField] public bool HitRobots { get; private set; } = true;

    [field: Tooltip("보스 콜라이더(보스전)를 맞힌다. BossHurtZone 부위 배율 적용")]
    [field: SerializeField] public bool HitBoss { get; private set; } = true;

    [field: Tooltip("반경 안의 전장 기물(BossFieldGimmickBase)을 작동시킨다")]
    [field: SerializeField] public bool TriggerFieldGimmicks { get; private set; } = true;

    [Header("연출 — 비워도 판정은 동작한다")]
    [Tooltip("터질 때 폭발 위치에 생성할 이펙트 목록. 스킬 구간 이펙트(ActionData.vfx)와 같은 규칙으로 생성되고, 끝까지 재생 후 자동 제거된다")]
    [SerializeField] private List<ExplosionVfxEntry> explosionVfx = new List<ExplosionVfxEntry>();

    public IReadOnlyList<ExplosionVfxEntry> ExplosionVfx => explosionVfx;
}
