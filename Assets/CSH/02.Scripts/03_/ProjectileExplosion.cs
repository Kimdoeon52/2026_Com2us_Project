using System.Collections.Generic;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 폭발형 투사체의 범위 판정. Projectile.Explode()(명중 / 사거리 끝 공통)에서 한 번 불린다.
/// 규칙은 HitDetection 투사체 판정과 같다: 데미지 = 쏜 행동, 경직 → 넉백 → 점멸 → 히트스톱,
/// 회피 무적·쏜 로봇 제외. 폭발 1회에 대상 하나는 한 번만 맞는다.
/// </summary>
public static class ProjectileExplosion
{
    private static readonly List<Collider2D> overlapBuffer = new List<Collider2D>(32);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };
    private static readonly HashSet<object> handled = new HashSet<object>(); // 보스·촉수·기물 중복 방지 (콜라이더 여러 개)

    public static void Detonate(Projectile projectile, ExplosiveProjectileData data)
    {
        if (projectile == null || data == null) return;

        Vector3 pos = projectile.transform.position;
        Vector2 center = pos;
        float radius = data.ExplosionRadius;

        ActionExecutor owner = projectile.Owner;
        SpawnVfx(data, pos, projectile.FacingRight, owner);

        ActionData action = projectile.SourceAction;
        var hub = CombatDataHub.Instance;
        if (owner == null || action == null) return;
        if (hub == null) { Debug.LogWarning("[ProjectileExplosion] CombatDataHub 없음 — 폭발 피해 건너뜀"); return; }


        if (data.HitRobots)
        {
            foreach (var defender in Object.FindObjectsByType<ActionExecutor>(FindObjectsSortMode.None))
            {
                if (defender == owner || defender.State == null || HitDetection.IsInvincibleNow(defender)) continue;
                if (!OverlapsAnyHurtBox(defender, center, radius)) continue;

                var result = hub.ProcessHit(owner.FighterId, defender.FighterId, action, false, false);
                bool reacted = HitReactionSystem.Apply(defender, result); // 경직이 넉백보다 먼저 (HitDetection과 같음)
                KnockbackSystem.Apply(center.x, action, defender, result);
                defender.NotifyDamaged(result);
                Log(owner, defender.FighterId, result);
            }
        }

        bool hitBoss = data.HitBoss && hub.CurrentBattleMode == BattleMode.RobotVsBoss && hub.BossSnapshot != null;
        if (!hitBoss && !data.TriggerFieldGimmicks) return;

        handled.Clear();
        Physics2D.OverlapCircle(center, radius, overlapFilter, overlapBuffer);
        foreach (var col in overlapBuffer)
        {
            if (col == null) continue;

            // 촉수 등 체력이 따로 있는 대상 — 보스 밑에 있어도 보스 대신 이쪽이 맞는다 (HitDetection.FindBoss와 같은 규칙)
            var target = hitBoss ? col.GetComponentInParent<IPlayerHitTarget>() : null;
            if (target != null)
            {
                if (target.IsHittable && handled.Add(target))
                    target.TakePlayerHit(action.Damage > 0f ? action.Damage : 10f, center); // HitDetection.ResolveTargetHits와 같은 규칙
                continue;
            }

            var boss = hitBoss ? col.GetComponentInParent<BossControllerBase>() : null;
            if (boss != null && boss.Snapshot != null && boss.Snapshot.IsAlive && handled.Add(boss))
            {
                var zone = col.GetComponent<BossHurtZone>(); // 처음 찾은 부위 배율 (HitDetection.FindBoss와 같음)
                float scale = zone != null ? zone.DamageScale : 1f;
                string bossId = boss.Snapshot.bossID;
                var result = Mathf.Approximately(scale, 1f)
                    ? hub.ProcessHit(owner.FighterId, bossId, action, false, false)
                    : hub.ProcessHit(owner.FighterId, bossId, (action.Damage > 0f ? action.Damage : 10f) * scale);
                if (zone != null) zone.NotifyHit(result.DamageToHp);
                Log(owner, $"보스 {bossId}", result);
            }

            // 기물엔 HP가 없어서 상호작용을 건다. 작동 조건은 기물의 CanInteract가 판단
            var gimmick = data.TriggerFieldGimmicks ? col.GetComponentInParent<BossFieldGimmickBase>() : null;
            if (gimmick != null && gimmick.IsInteractable && handled.Add(gimmick))
                gimmick.TriggerInteraction(owner.gameObject);
        }
    }

    // Hurt박스 월드 변환은 HitDetection.TryProjectileHit과 같은 방식
    private static bool OverlapsAnyHurtBox(ActionExecutor defender, Vector2 c, float radius)
    {
        bool facingRight = defender.Mover == null || defender.Mover.FacingRight;
        foreach (var box in defender.State.GetActiveBoxes())
        {
            if (box.type != BoxType.Hurt) continue;
            Rect r = BoxResolver.ToWorldRect(defender.transform.position, box.rect, facingRight);
            // 원 vs 사각형: 사각형 위 최근접점까지 거리 ≤ 반경
            float dx = c.x - Mathf.Clamp(c.x, r.xMin, r.xMax);
            float dy = c.y - Mathf.Clamp(c.y, r.yMin, r.yMax);
            if (dx * dx + dy * dy <= radius * radius) return true;
        }
        return false;
    }

    // 스킬 구간 이펙트와 같은 생성 규칙(ActionVfxPlayer.SpawnVisual). 기준점 = 폭발 위치, 방향 = 투사체 진행 방향,
    // 정렬 기준 = 쏜 로봇 그림. 터지는 순간 1번 만들고 끝까지 재생 후 자동 제거 (순수 연출 — 판정과 무관)
    private static void SpawnVfx(ExplosiveProjectileData data, Vector3 pos, bool facingRight, ActionExecutor owner)
    {
        var entries = data.ExplosionVfx;
        if (entries == null || entries.Count == 0) return;

        Renderer sortBase = owner != null ? RobotView.FindRenderer(owner) : null;
        foreach (var e in entries)
        {
            if (e == null || e.prefab == null) continue;
            var go = ActionVfxPlayer.SpawnVisual(e.prefab, pos, e.offset, e.rotation, facingRight,
                                                 e.flipWithFacing, ActionVfxPlayer.CombineScale(e.scale, e.scaleXY), e.playbackSpeed, sortBase, e.sortingOrderOffset);
            ActionVfxPlayer.DestroyAfterNaturalLifetime(go);
        }
    }

    private static void Log(ActionExecutor owner, string target, HitResolutionResult result) =>
        Debug.Log($"[ProjectileExplosion] {owner.FighterId}의 폭발 → {target} : 데미지={result.DamageToHp}");
}
