using UnityEngine;

/// <summary>
/// 두 캐릭터의 Hit박스와 Hurt박스가 겹쳤는지 매 틱 검사하고, 겹치면 KKH의 CombatDataHub.ProcessHit을
/// 호출해서 실제 데미지 계산을 맡기는 3층 시스템 (§4, §11-6).
///
/// 씬을 한 번 훑어서 ActionExecutor(=로봇)를 자동으로 찾는다 — 1v1 전용이라 정확히 2개가 있어야
/// 동작한다. 인스펙터에 Player/Enemy를 수동으로 드래그하는 방식 대신 자동 탐색을 쓴 이유: 로봇을
/// 씬에 새로 추가·교체할 때마다 이 컴포넌트의 참조를 따로 연결해줘야 하는 번거로움을 없애기 위함.
/// </summary>
public class HitDetection : MonoBehaviour
{
    private ActionExecutor fighterA;
    private ActionExecutor fighterB;
    // 좌우 반전 계산(BoxResolver)에 각 로봇이 "지금 어느 방향을 보고 있는지"가 필요해서 같이 캐싱해둔다
    private RobotMover moverA;
    private RobotMover moverB;

    // 호출: Unity(이 컴포넌트가 활성화될 때). 씬에서 로봇 2개를 찾아 캐싱하고 CombatClock에 판정 함수를 등록한다
    private void OnEnable()
    {
        var fighters = FindObjectsByType<ActionExecutor>(FindObjectsSortMode.None);
        if (fighters.Length != 2)
        {
            // 씬에 로봇이 아직 하나뿐이거나(Enemy 준비 전), 실수로 3개 이상 있는 경우를 바로 알아채게
            // 경고로 남긴다 — 조용히 넘어가면 "왜 판정이 아예 안 되지"를 한참 헤매게 된다
            Debug.LogWarning($"[HitDetection] 씬에 ActionExecutor가 {fighters.Length}개임 — 1v1 전용이라 정확히 2개여야 정상 동작함");
            return;
        }

        fighterA = fighters[0];
        fighterB = fighters[1];
        moverA = fighterA.GetComponent<RobotMover>();
        moverB = fighterB.GetComponent<RobotMover>();

        CombatClock.Instance.OnCombatTick += CheckHits;
    }

    // 호출: Unity(비활성화/파괴 시). PlayerRobotBootstrap과 같은 이유로 반드시 구독 해제한다
    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= CheckHits;
    }

    // 호출: CombatClock.OnCombatTick(틱마다). 양쪽 다 서로에게 공격자가 될 수 있으므로 두 방향 다 검사한다.
    // (A가 B를 때릴 수도, B가 A를 때릴 수도 있으니 "누가 공격자/방어자"가 고정이 아니라 매번 둘 다 확인해야 함)
    private void CheckHits()
    {
        if (fighterA == null || fighterB == null) return; // OnEnable에서 못 찾았으면(로봇 2개 아님) 계속 아무것도 안 함

        CheckDirectional(fighterA, moverA, fighterB, moverB);
        CheckDirectional(fighterB, moverB, fighterA, moverA);
        CheckProjectiles();
    }

    // 2026-10-09 추가 (§11-15): 날아가는 투사체 각각의 Hit박스 vs 쏜 쪽이 아닌 로봇의 Hurt박스.
    // 근접과 달리 "행동 1회당 1히트"(HasHitThisAction)를 쓰지 않는다 — 총알마다 따로 한 번씩 맞혀야 하므로
    // 히트 여부는 총알 자신이 들고 있다(맞으면 Explode()로 Live에서 빠짐)
    private void CheckProjectiles()
    {
        var projectiles = Projectile.Live;
        // 뒤에서부터 도는 이유: 맞으면 Explode()가 목록에서 자기를 빼므로, 앞에서부터 돌면 다음 원소를 건너뛴다
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = projectiles[i];
            if (projectile.Owner == fighterA) TryProjectileHit(projectile, fighterB, moverB);
            else if (projectile.Owner == fighterB) TryProjectileHit(projectile, fighterA, moverA);
        }
    }

    private void TryProjectileHit(Projectile projectile, ActionExecutor defender, RobotMover defenderMover)
    {
        // 회피 무적 중이면 총알이 그냥 통과한다 — 근접처럼 ProcessHit(isWeaving=true)으로 넘기면
        // 겹쳐 있는 내내 매 틱 "회피 성공"이 들어가고 총알도 터져버려서, 무적의 의미가 없어진다
        if (IsInvincibleNow(defender)) return;

        Rect worldHit = projectile.GetWorldHitBox();
        bool defenderFacingRight = defenderMover == null || defenderMover.FacingRight;

        foreach (var hurtBox in defender.State.GetActiveBoxes())
        {
            if (hurtBox.type != BoxType.Hurt) continue;
            Rect worldHurt = BoxResolver.ToWorldRect(defender.transform.position, hurtBox.rect, defenderFacingRight);
            if (!Overlap(worldHit, worldHurt)) continue;

            if (CombatDataHub.Instance == null)
            {
                Debug.LogWarning("[HitDetection] CombatDataHub가 씬에 없어서 투사체 히트 처리를 건너뜀");
                return;
            }

            var result = CombatDataHub.Instance.ProcessHit(
                projectile.Owner.FighterId, defender.FighterId, projectile.SourceAction, false, false);

            projectile.Explode(); // 한 발 = 한 번. 같은 틱에 다른 박스와 또 겹쳐도 더 안 맞는다
            ApplyHitConsequences(projectile.transform.position.x, projectile.SourceAction, defender, result);

            Debug.Log($"[HitDetection] {projectile.Owner.FighterId}의 투사체 → {defender.FighterId} : 데미지={result.DamageToHp}");
            return;
        }
    }

    // 맞은 뒤의 후속 처리 순서를 근접·투사체가 똑같이 따르도록 한 곳에 모은다.
    // 경직 → 넉백 순서인 이유는 HitReactionSystem.Apply 주석 참고(띄우기가 먼저면 공중 판정으로 경직이 빠짐)
    private static void ApplyHitConsequences(float sourceX, ActionData sourceAction, ActionExecutor defender, HitResolutionResult result)
    {
        HitReactionSystem.Apply(defender, result);
        KnockbackSystem.Apply(sourceX, sourceAction, defender, result);
        defender.NotifyDamaged(result); // 점멸 등 연출 — 판정에는 영향 없음
    }

    // 회피(구 위닝) 무적 — 기술 이름이 아니라 플래그로 판정한다 (§13)
    private static bool IsInvincibleNow(ActionExecutor defender) =>
        defender.State.CurrentAction != null
        && defender.State.CurrentAction.IsInvincibleDuringActive
        && defender.State.Phase == ActionPhase.Active;

    // attacker의 Hit박스 전부 vs defender의 Hurt박스 전부를 비교해서, 하나라도 겹치면 그 순간 히트 처리하고 끝낸다
    private void CheckDirectional(ActionExecutor attacker, RobotMover attackerMover, ActionExecutor defender, RobotMover defenderMover)
    {
        // 이번 행동에서 이미 한 번 맞혔으면 더 검사할 필요가 없다 — 활성 구간 내내 겹쳐 있어도
        // ProcessHit은 딱 한 번만 불러야 한다 (ActionState.HasHitThisAction 설명 참고)
        if (attacker.State.HasHitThisAction) return;

        // mover가 아직 없으면(배선 전 등) 오른쪽을 본다고 가정 — BoxDrawer와 동일한 안전 기본값
        bool attackerFacingRight = attackerMover == null || attackerMover.FacingRight;
        bool defenderFacingRight = defenderMover == null || defenderMover.FacingRight;

        foreach (var hitBox in attacker.State.GetActiveBoxes())
        {
            if (hitBox.type != BoxType.Hit) continue; // 공격자 쪽에서는 Hit박스만 의미가 있다 — Hurt/Push는 무시
            Rect worldHit = BoxResolver.ToWorldRect(attacker.transform.position, hitBox.rect, attackerFacingRight);

            foreach (var hurtBox in defender.State.GetActiveBoxes())
            {
                if (hurtBox.type != BoxType.Hurt) continue; // 방어자 쪽에서는 Hurt박스만 의미가 있다
                Rect worldHurt = BoxResolver.ToWorldRect(defender.transform.position, hurtBox.rect, defenderFacingRight);

                if (!Overlap(worldHit, worldHurt)) continue;

                // 겹쳤다 — 실제 데미지·가드·크리티컬 계산은 전부 KKH의 CombatDataHub가 담당한다.
                // 여기서는 "누가 누구를 어떤 상태(가드/회피 여부)로 때렸는지"만 넘겨주고 판단은 그쪽에 맡긴다
                ResolveHit(attacker, defender);
                return; // 한 틱에 박스 쌍이 여러 개 겹쳐도 히트는 1번이면 충분 — 더 검사할 이유가 없다
            }
        }
    }

    // 실제로 겹침이 확인된 뒤, KKH API에 필요한 값(가드/회피 여부)을 계산해서 ProcessHit을 호출한다
    private void ResolveHit(ActionExecutor attacker, ActionExecutor defender)
    {
        // CombatDataHub(KKH)는 CombatClock과 달리 씬에 없으면 Instance가 그냥 null을 반환한다
        // (자동 생성 안 함) — 여기서 막아두지 않으면 이 예외 하나가 CombatClock.Tick()의 나머지
        // 구독자(다른 로봇들의 ExecuteTick 등)까지 전부 못 돌게 막아버린다 (C# 이벤트는 한 구독자가
        // 예외를 던지면 그 뒤 구독자는 호출되지 않는다)
        if (CombatDataHub.Instance == null)
        {
            Debug.LogWarning("[HitDetection] CombatDataHub가 씬에 없어서 히트 처리를 건너뜀 — DummyBattleBootstrap이나 CombatDataHubTester가 먼저 실행됐는지 확인할 것");
            return;
        }

        // 가드는 2026-09-29 기동 행동에서 삭제됐다 (CLAUDE_1.md §5·§15) — 항상 false만 전달한다.
        // KKH의 CombatDataHub.ProcessHit 시그니처는 그대로 유지되므로(§0·§14) 값만 고정하고 파라미터는 안 건드린다.
        bool isGuarding = false;

        // 회피(구 위닝) 무적도 마찬가지로 기술 이름이 아니라 플래그로 판정한다 — 나중에 다른 무적기가 생겨도 이 코드는 안 바뀐다
        bool isWeaving = IsInvincibleNow(defender);

        ActionData attackAction = attacker.State.CurrentAction;
        var result = CombatDataHub.Instance.ProcessHit(
            attacker.FighterId, defender.FighterId, attackAction, isGuarding, isWeaving);

        attacker.State.MarkHit(); // 이번 행동으로 이 상대를 다시는 못 맞히게 표시 (중복 데미지 방지)

        // 데미지 계산은 KKH가 끝냈고, 몸이 어떻게 반응하는지(경직·밀려남·띄우기·점멸)는 여기서 처리한다
        ApplyHitConsequences(attacker.transform.position.x, attackAction, defender, result);

        // 결과를 바로 눈으로 확인할 수 있게 로그로 남긴다 — 나중에 UI가 생기면 CombatDataHub.OnHitResolved
        // 이벤트를 구독해서 이 정보로 화면에 데미지 숫자를 띄우면 된다 (지금은 그 UI가 없어서 로그로 대체)
        Debug.Log($"[HitDetection] {attacker.FighterId} → {defender.FighterId} : " +
                  $"데미지={result.DamageToHp} 가드={result.IsGuarded} 회피={result.IsEvaded} 크리티컬={result.IsCritical}");
    }

    // 두 사각형이 겹치는지 검사하는 표준 AABB(축 정렬 바운딩 박스) 겹침 판정.
    // 한쪽의 왼쪽이 상대의 오른쪽보다 왼쪽에 있고 + 오른쪽이 상대의 왼쪽보다 오른쪽에 있고, 위아래도 마찬가지면 겹친 것이다
    private static bool Overlap(Rect a, Rect b) =>
        a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
}
