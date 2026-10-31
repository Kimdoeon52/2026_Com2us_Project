using System.Collections.Generic;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 두 캐릭터의 Hit박스와 Hurt박스가 겹쳤는지 매 틱 검사하고, 겹치면 KKH의 CombatDataHub.ProcessHit을
/// 호출해서 실제 데미지 계산을 맡기는 3층 시스템 (§4, §11-6).
///
/// 씬을 한 번 훑어서 ActionExecutor(=로봇)를 자동으로 찾는다 — 로봇끼리 판정은 정확히 2개일 때만 돈다.
/// 인스펙터에 Player/Enemy를 수동으로 드래그하는 방식 대신 자동 탐색을 쓴 이유: 로봇을
/// 씬에 새로 추가·교체할 때마다 이 컴포넌트의 참조를 따로 연결해줘야 하는 번거로움을 없애기 위함.
///
/// 2026-10-28 보스전(KKH BossControllerBase) 연동 — KKH 방식(콜라이더·태그)에 맞춘다:
///   · 플레이어 → 보스: 우리 Hit박스 자리에 Physics2D.OverlapBox를 던져 보스 콜라이더를 찾고, 허브 ProcessHit(보스 모드)에 넘긴다.
///     보스 HP는 허브가 깎는다 — NotifyDamageFromHub는 같은 BossSnapshot을 또 깎아서(이중 차감) 부르지 않는다
///   · 보스 → 플레이어: KKH가 허브에서 HP를 직접 깎으므로, 허브의 플레이어 HP 감소 이벤트를 받아 경직·점멸·숫자·히트스톱을 건다.
///     KKH가 플레이어를 찾는 콜라이더는 HurtboxColliderSync가 Hurt박스에 맞춰둔다
/// </summary>
public class HitDetection : MonoBehaviour
{
    public static HitDetection Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[HitDetection] Duplicate HitDetection component detected on {gameObject.name}. Disabling and destroying.");
            enabled = false;
            DestroyImmediate(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; lastPlayerHp = -1; }
    }
    // 2026-10-28 추가: 히트스톱 공식. 비우면 히트스톱 없음 (HitStopSystem)
    [Tooltip("히트스톱 공식 에셋(NYH/Combat/Hit Stop Settings). 비우면 맞아도 안 멈춘다")]
    [SerializeField] private HitStopSettings hitStopSettings;

    private ActionExecutor fighterA;
    private ActionExecutor fighterB;
    // 좌우 반전 계산(BoxResolver)에 각 로봇이 "지금 어느 방향을 보고 있는지"가 필요해서 같이 캐싱해둔다
    private RobotMover moverA;
    private RobotMover moverB;

    // 보스 판정용 OverlapBox 결과 버퍼 (틱마다 새로 할당하지 않으려고 재사용)
    private static readonly List<Collider2D> overlapBuffer = new List<Collider2D>(16);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    // 씬의 로봇 전부 — 보스전에선 Player 하나뿐이다
    private readonly List<ActionExecutor> fighters = new List<ActionExecutor>(4);

    // 보스 공격(허브가 플레이어 HP를 직접 깎음)을 알아채려고 구독 중인 허브와, 직전 플레이어 HP
    private CombatDataHub subscribedHub;
    private static int lastPlayerHp = -1;

    // 다중 박스 판정에서 방어자 Hurt박스를 한 번만 월드로 바꿔 담는 임시 버퍼 (틱마다 새로 할당하지 않으려고 재사용) 최.추가
    private static readonly List<WorldBox> hurtBuffer = new List<WorldBox>(8);

    // 호출: Unity(이 컴포넌트가 활성화될 때). 씬에서 로봇 2개를 찾아 캐싱하고 CombatClock에 판정 함수를 등록한다
    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        fighters.Clear();
        fighters.AddRange(FindObjectsByType<ActionExecutor>(FindObjectsSortMode.None));
        fighterA = fighterB = null;

        if (fighters.Count == 2)
        {
            fighterA = fighters[0];
            fighterB = fighters[1];
            moverA = fighterA.GetComponent<RobotMover>();
            moverB = fighterB.GetComponent<RobotMover>();
        }
        else if (fighters.Count != 1 || FindAnyObjectByType<BossControllerBase>() == null)
        {
            // 로봇 1개는 보스전이면 정상(로봇끼리 판정만 꺼짐). 그 밖의 개수는 바로 알아채게 경고로 남긴다 —
            // 조용히 넘어가면 "왜 판정이 아예 안 되지"를 한참 헤매게 된다
            Debug.LogWarning($"[HitDetection] 씬에 ActionExecutor가 {fighters.Count}개임 — 로봇끼리 판정은 정확히 2개일 때만 동작함");
        }

        CombatClock.Instance.OnCombatTick += CheckHits;
    }

    // 호출: Unity(비활성화/파괴 시). PlayerRobotBootstrap과 같은 이유로 반드시 구독 해제한다
    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= CheckHits;
        SubscribeHub(null);
    }

    // 호출: CombatClock.OnCombatTick(틱마다). 양쪽 다 서로에게 공격자가 될 수 있으므로 두 방향 다 검사한다.
    // (A가 B를 때릴 수도, B가 A를 때릴 수도 있으니 "누가 공격자/방어자"가 고정이 아니라 매번 둘 다 확인해야 함)
    private void CheckHits()
    {
        // 허브는 DummyBattleBootstrap.Start 등에서 늦게 생길 수 있어서 틱마다 확인해 붙는다
        SubscribeHub(CombatDataHub.Instance);

        if (fighterA != null && fighterB != null)
        {
            CheckDirectional(fighterA, moverA, fighterB, moverB);
            CheckDirectional(fighterB, moverB, fighterA, moverA);
        }

        if (IsBossBattle)
        {
            foreach (var fighter in fighters)
                if (fighter != null) CheckBossHit(fighter);
        }

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
            bool hit = false;
            if (fighterA != null && fighterB != null)
            {
                if (projectile.Owner == fighterA) hit = TryProjectileHit(projectile, fighterB, moverB);
                else if (projectile.Owner == fighterB) hit = TryProjectileHit(projectile, fighterA, moverA);
            }
            if (!hit && IsBossBattle) TryProjectileHitBoss(projectile);
        }
    }

    // 반환: 맞혔는지(총알이 터졌는지)
    private bool TryProjectileHit(Projectile projectile, ActionExecutor defender, RobotMover defenderMover)
    {
        // 회피 무적 중이면 총알이 그냥 통과한다 — 근접처럼 ProcessHit(isWeaving=true)으로 넘기면
        // 겹쳐 있는 내내 매 틱 "회피 성공"이 들어가고 총알도 터져버려서, 무적의 의미가 없어진다
        if (IsInvincibleNow(defender)) return false;

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
                return false;
            }

            var result = CombatDataHub.Instance.ProcessHit(
                projectile.Owner.FighterId, defender.FighterId, projectile.SourceAction, false, false);

            projectile.Explode(); // 한 발 = 한 번. 같은 틱에 다른 박스와 또 겹쳐도 더 안 맞는다
            bool reacted = ApplyHitConsequences(projectile.transform.position.x, projectile.SourceAction, defender, result);
            HitStopSystem.Apply(hitStopSettings, projectile.Owner, defender, projectile.SourceAction, result,
                                boxScale: 1f, fromProjectile: true, isRepeatHit: false, defenderReacted: reacted);

            Debug.Log($"[HitDetection] {projectile.Owner.FighterId}의 투사체 → {defender.FighterId} : 데미지={result.DamageToHp}");
            return true;
        }
        return false;
    }

    // 맞은 뒤의 후속 처리 순서를 근접·투사체가 똑같이 따르도록 한 곳에 모은다.
    // 경직 → 넉백 순서인 이유는 HitReactionSystem.Apply 주석 참고(띄우기가 먼저면 공중 판정으로 경직이 빠짐)
    // 반환: 맞은 쪽이 피격 모션(경직)에 들어갔는지 — 히트스톱이 이걸 보고 멈출지 정한다
    private static bool ApplyHitConsequences(float sourceX, ActionData sourceAction, ActionExecutor defender, HitResolutionResult result)
    {
        bool reacted = HitReactionSystem.Apply(defender, result);
        KnockbackSystem.Apply(sourceX, sourceAction, defender, result);
        defender.NotifyDamaged(result); // 점멸 등 연출 — 판정에는 영향 없음
        return reacted;
    }

    // 회피(구 위닝) 무적 — 기술 이름이 아니라 플래그로 판정한다 (§13). HurtboxColliderSync도 이걸 본다
    public static bool IsInvincibleNow(ActionExecutor defender) =>
        defender.State.CurrentAction != null
        && defender.State.CurrentAction.IsInvincibleDuringActive
        && defender.State.Phase == ActionPhase.Active;

    // attacker의 Hit박스 전부 vs defender의 Hurt박스 전부를 비교해서, 하나라도 겹치면 그 순간 히트 처리하고 끝낸다
    private void CheckDirectional(ActionExecutor attacker, RobotMover attackerMover, ActionExecutor defender, RobotMover defenderMover)
    {
        // 이번 행동에서 이미 한 번 맞혔으면 더 검사할 필요가 없다 — 활성 구간 내내 겹쳐 있어도
        // ProcessHit은 딱 한 번만 불러야 한다 (ActionState.HasHitThisAction 설명 참고).
        // 2026-10-11: 다단히트 기술(화염 등)은 HitIntervalFrames가 지나면 다시 맞힐 수 있다 (ActionState.CanHitNow)
        if (!attacker.State.CanHitNow) return;

        // mover가 아직 없으면(배선 전 등) 오른쪽을 본다고 가정 — BoxDrawer와 동일한 안전 기본값
        bool attackerFacingRight = attackerMover == null || attackerMover.FacingRight;
        bool defenderFacingRight = defenderMover == null || defenderMover.FacingRight;

        // 2026-10-15: 부채꼴 산탄처럼 "겹친 Hit박스 수만큼 피해가 쌓이는" 행동은 별도 경로로 처리한다  최.추가
        ActionData action = attacker.State.CurrentAction;
        if (action != null && action.PerBoxDamage)
        {
            CheckMultiBox(attacker, attackerFacingRight, defender, defenderFacingRight);
            return;
        }

        foreach (var hitBox in attacker.State.GetActiveBoxes())
        {
            if (hitBox.type != BoxType.Hit) continue; // 공격자 쪽에서는 Hit박스만 의미가 있다 — Hurt/Push는 무시
            // 2026-10-16: 회전 Hit박스를 쓰는 일반 행동도 BoxDrawer와 같은 WorldBox로 판정한다 (회전이 없으면 예전 AABB 검사와 같은 식)
            WorldBox worldHit = BoxResolver.ToWorldBox(attacker.transform.position, hitBox, attackerFacingRight);

            foreach (var hurtBox in defender.State.GetActiveBoxes())
            {
                if (hurtBox.type != BoxType.Hurt) continue; // 방어자 쪽에서는 Hurt박스만 의미가 있다
                WorldBox worldHurt = BoxResolver.ToWorldBox(defender.transform.position, hurtBox, defenderFacingRight);

                if (!BoxResolver.Overlap(worldHit, worldHurt)) continue;

                // 겹쳤다 — 실제 데미지·가드·크리티컬 계산은 전부 KKH의 CombatDataHub가 담당한다.
                // 여기서는 "누가 누구를 어떤 상태(가드/회피 여부)로 때렸는지"만 넘겨주고 판단은 그쪽에 맡긴다
                // 맞힌 박스의 계수를 히트스톱에도 넘긴다 — 칼끝처럼 박스마다 손맛을 다르게 주는 자리(FrameBox.damageScale)
                ResolveHit(attacker, defender, hitStopBoxScale: hitBox.DamageScaleOrOne);
                return; // 한 틱에 박스 쌍이 여러 개 겹쳐도 히트는 1번이면 충분 — 더 검사할 이유가 없다
            }
        }
    }
    // 2026-10-15: 박스당 피해 행동. 이번 틱에 Hurt박스와 겹친 Hit박스마다 damageScale을 더해서
    // ProcessHit을 "1번만" 부른다. 박스 하나는 Hurt박스가 몇 개와 겹쳐도 1발로만 센다.
    // 하나라도 겹치는 순간 MarkHit으로 행동이 끝나므로, 산탄의 박스는 활성 구간 첫 프레임에만 켜지도록(length=1)
    // 만드는 걸 권장한다 — 그래야 "발사한 순간의 스냅샷"으로 판정된다  최.추가
    private void CheckMultiBox(ActionExecutor attacker, bool attackerFacingRight, ActionExecutor defender, bool defenderFacingRight)
    {
        hurtBuffer.Clear();
        foreach (var hurtBox in defender.State.GetActiveBoxes())
        {
            if (hurtBox.type == BoxType.Hurt)
                hurtBuffer.Add(BoxResolver.ToWorldBox(defender.transform.position, hurtBox, defenderFacingRight));
        }
        if (hurtBuffer.Count == 0) return;

        float weight = 0f;
        foreach (var hitBox in attacker.State.GetActiveBoxes())
        {
            if (hitBox.type != BoxType.Hit) continue;
            WorldBox worldHit = BoxResolver.ToWorldBox(attacker.transform.position, hitBox, attackerFacingRight);

            for (int i = 0; i < hurtBuffer.Count; i++)
            {
                if (!BoxResolver.Overlap(worldHit, hurtBuffer[i])) continue;
                weight += hitBox.DamageScaleOrOne;
                break; // 이 Hit박스(=발)는 한 번만 센다
            }
        }

        if (weight <= 0f) return; // 한 발도 안 맞았다 — 이번 틱은 아무 일도 없었던 걸로 (MarkHit도 안 한다)

        // 히트스톱도 겹친 박스 계수의 합으로 — 산탄이 많이 맞을수록 묵직하게
        ResolveHit(attacker, defender, weight, hitStopBoxScale: weight);
    }

    // 실제로 겹침이 확인된 뒤, KKH API에 필요한 값(가드/회피 여부)을 계산해서 ProcessHit을 호출한다
    // damageWeight: 음수면 기존 경로(ActionData.Damage 그대로), 0 이상이면 "Damage × damageWeight"를 수치 직접 전달형으로 넘긴다  최.추가
    // hitStopBoxScale: 히트스톱 박스 계수(2026-10-28) — 맞힌 Hit박스의 damageScale(박스당 피해면 합)
    private void ResolveHit(ActionExecutor attacker, ActionExecutor defender, float damageWeight = -1f, float hitStopBoxScale = 1f)
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
        /* var result = CombatDataHub.Instance.ProcessHit(
             attacker.FighterId, defender.FighterId, attackAction, isGuarding, isWeaving);*/

        HitResolutionResult result;
        if (damageWeight < 0f || isWeaving)
        {
            // 기존 경로. 박스당 피해 행동도 회피 무적 중이면 여기로 온다 — 수치 직접 전달형(float) 오버로드는
            // 무적 여부를 안 받으므로, 회피 판정(isEvaded)은 ActionData 오버로드에 맡긴다
            result = CombatDataHub.Instance.ProcessHit(
                attacker.FighterId, defender.FighterId, attackAction, isGuarding, isWeaving);
        }
        else
        {
            // KKH의 ActionData 오버로드와 똑같이 Damage가 0이면 10으로 본다
            float perBox = attackAction.Damage > 0f ? attackAction.Damage : 10f;
            result = CombatDataHub.Instance.ProcessHit(attacker.FighterId, defender.FighterId, perBox * damageWeight);
            // float 오버로드는 넉백 거리를 채워주지 않는다 (ActionData 오버로드만 채움) — 여기서 같은 값을 넣는다
            result.KnockbackDistance = attackAction.KnockbackDistance;
        }

        // MarkHit 전에 봐야 "이번 행동에서 이미 맞힌 적이 있나(= 다단히트 두 번째 이후)"를 알 수 있다
        bool isRepeatHit = attacker.State.HasHitThisAction;
        attacker.State.MarkHit(); // 이번 행동으로 이 상대를 다시는 못 맞히게 표시 (중복 데미지 방지)

        // 데미지 계산은 KKH가 끝냈고, 몸이 어떻게 반응하는지(경직·밀려남·띄우기·점멸)는 여기서 처리한다.
        // 히트스톱은 경직 뒤에 건다 — 피격 행동이 먼저 시작돼야 "피격 첫 장에서 멈춘" 그림이 된다
        bool reacted = ApplyHitConsequences(attacker.transform.position.x, attackAction, defender, result);
        HitStopSystem.Apply(hitStopSettings, attacker, defender, attackAction, result,
                            hitStopBoxScale, fromProjectile: false, isRepeatHit, defenderReacted: reacted);

        // 결과를 바로 눈으로 확인할 수 있게 로그로 남긴다 — 나중에 UI가 생기면 CombatDataHub.OnHitResolved
        // 이벤트를 구독해서 이 정보로 화면에 데미지 숫자를 띄우면 된다 (지금은 그 UI가 없어서 로그로 대체)
        Debug.Log($"[HitDetection] {attacker.FighterId} → {defender.FighterId} : " +
                  $"데미지={result.DamageToHp} 가드={result.IsGuarded} 회피={result.IsEvaded} 크리티컬={result.IsCritical}");
    }

    // ========================================================================
    // 2026-10-28 보스전 (KKH BossControllerBase)
    // ========================================================================

    // 허브가 보스 모드로 초기화돼 있을 때만 보스 판정을 한다 — 로봇전 씬엔 영향 없음
    private static bool IsBossBattle
    {
        get
        {
            var hub = CombatDataHub.Instance;
            return hub != null && hub.CurrentBattleMode == BattleMode.RobotVsBoss && hub.BossSnapshot != null;
        }
    }

    // 플레이어 근접 Hit박스 vs 보스 콜라이더. 판정 모양은 우리 Hit박스(회전 포함) 그대로, 상대 찾기만 KKH 방식(Physics2D)
    private void CheckBossHit(ActionExecutor attacker)
    {
        if (attacker.State == null || !attacker.State.CanHitNow) return;
        ActionData action = attacker.State.CurrentAction;
        if (action == null) return;

        bool facingRight = attacker.Mover == null || attacker.Mover.FacingRight;
        BossControllerBase target = null;
        BossHurtZone targetZone = null;
        float weight = 0f;

        foreach (var hitBox in attacker.State.GetActiveBoxes())
        {
            if (hitBox.type != BoxType.Hit) continue;
            WorldBox worldHit = BoxResolver.ToWorldBox(attacker.transform.position, hitBox, facingRight);
            BossControllerBase boss = FindBoss(worldHit, out BossHurtZone zone);
            if (boss == null || (target != null && boss != target)) continue;

            target = boss;
            if (targetZone == null) targetZone = zone; // 여러 부위에 걸치면 처음 찾은 부위 배율
            weight += hitBox.DamageScaleOrOne;
            if (!action.PerBoxDamage) break; // 일반 행동은 박스 하나만 맞아도 1히트 — 로봇전과 같은 규칙
        }

        if (target != null) ResolveBossHit(attacker, target, targetZone, action, weight);
    }

    // 박스 자리에 겹친 콜라이더 중 살아있는 보스를 찾는다. 보스 자식 콜라이더(부위)여도 부모의 BossControllerBase로 올라간다.
    // zone: 그 콜라이더가 BossHurtZone(쿄다이 부위)이면 함께 돌려준다 — 부위 배율을 데미지에 곱한다
    private static BossControllerBase FindBoss(in WorldBox box, out BossHurtZone zone)
    {
        zone = null;
        float angle = box.IsRotated ? Mathf.Atan2(box.AxisX.y, box.AxisX.x) * Mathf.Rad2Deg : 0f;
        int count = Physics2D.OverlapBox(box.Center, box.Half * 2f, angle, overlapFilter, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            var boss = overlapBuffer[i].GetComponentInParent<BossControllerBase>();
            if (boss == null || boss.Snapshot == null || !boss.Snapshot.IsAlive) continue;
            zone = overlapBuffer[i].GetComponent<BossHurtZone>();
            return boss;
        }
        return null;
    }

    // weight: 맞힌 Hit박스 계수의 합. 박스당 피해 행동이면 데미지에도 곱하고, 아니면 히트스톱에만 쓴다
    private void ResolveBossHit(ActionExecutor attacker, BossControllerBase boss, BossHurtZone zone, ActionData action, float weight)
    {
        var hub = CombatDataHub.Instance;
        string bossId = boss.Snapshot.bossID;
        float zoneScale = zone != null ? zone.DamageScale : 1f;

        // HP는 허브가 깎는다(보스 모드: 그로기 배율, D 적중 시 탄 +1 포함). NotifyDamageFromHub는 부르지 않는다 —
        // 허브와 보스 컨트롤러가 같은 BossSnapshot을 들고 있어서 또 깎인다(이중 차감)
        HitResolutionResult result;
        if (action.PerBoxDamage || !Mathf.Approximately(zoneScale, 1f))
        {
            // 수치 직접 전달형 — 박스 수·부위 배율을 데미지에 곱한다. D 판정은 ActionData 오버로드와 같은 규칙
            float baseDamage = action.Damage > 0f ? action.Damage : 10f; // ResolveHit과 같은 규칙
            float boxes = action.PerBoxDamage ? weight : 1f;
            result = hub.ProcessHit(attacker.FighterId, bossId, baseDamage * boxes * zoneScale,
                                    isBasicAttackD: IsBasicAttackD(action));
        }
        else
        {
            result = hub.ProcessHit(attacker.FighterId, bossId, action, false, false);
        }
        if (zone != null) zone.NotifyHit(result.DamageToHp);

        bool isRepeatHit = attacker.State.HasHitThisAction;
        attacker.State.MarkHit();
        HitStopSystem.ApplyAttackerOnly(hitStopSettings, attacker, action, result, weight, fromProjectile: false, isRepeatHit);

        string part = zone != null ? $"({zone.PartId} ×{zoneScale})" : "";
        Debug.Log($"[HitDetection] {attacker.FighterId} → 보스 {bossId}{part} : 데미지={result.DamageToHp} 크리티컬={result.IsCritical} 보스HP={boss.Snapshot.currentHp}");
    }

    // KKH ProcessHit(ActionData) 보스 분기의 D 판정과 같은 식 — 수치 직접 전달형으로 넘길 때도 탄 +1이 빠지지 않게
    private static bool IsBasicAttackD(ActionData action) =>
        action != null && (action.ActionName == "D" || action.Source == ActionSource.CoreFixed);

    private void TryProjectileHitBoss(Projectile projectile)
    {
        if (projectile.Owner == null || projectile.SourceAction == null) return;
        BossControllerBase boss = FindBoss(new WorldBox(projectile.GetWorldHitBox(), 0f), out BossHurtZone zone);
        if (boss == null) return;

        string bossId = boss.Snapshot.bossID;
        ActionData action = projectile.SourceAction;
        HitResolutionResult result;
        if (zone != null && !Mathf.Approximately(zone.DamageScale, 1f))
        {
            float baseDamage = action.Damage > 0f ? action.Damage : 10f;
            result = CombatDataHub.Instance.ProcessHit(projectile.Owner.FighterId, bossId, baseDamage * zone.DamageScale,
                                                       isBasicAttackD: IsBasicAttackD(action));
        }
        else
        {
            result = CombatDataHub.Instance.ProcessHit(projectile.Owner.FighterId, bossId, action, false, false);
        }
        if (zone != null) zone.NotifyHit(result.DamageToHp);

        projectile.Explode();
        HitStopSystem.ApplyAttackerOnly(hitStopSettings, projectile.Owner, action, result,
                                        1f, fromProjectile: true, isRepeatHit: false);

        Debug.Log($"[HitDetection] {projectile.Owner.FighterId}의 투사체 → 보스 {bossId} : 데미지={result.DamageToHp}");
    }

    private void SubscribeHub(CombatDataHub hub)
    {
        if (hub == subscribedHub) return;
        if (subscribedHub != null) subscribedHub.OnPlayerHpChanged -= OnPlayerHpChanged;
        subscribedHub = hub;
        lastPlayerHp = (hub != null && hub.PlayerSnapshot != null) ? hub.PlayerSnapshot.currentHp : -1;
        if (hub != null) hub.OnPlayerHpChanged += OnPlayerHpChanged;
    }

    // 호출: CombatDataHub.OnPlayerHpChanged. 보스전에서 플레이어 HP가 줄면 = KKH 보스 공격(ApplyCoreDamage/ApplyPartHit)에 맞은 것.
    // 로봇전에선 ResolveHit이 이미 경직·점멸을 처리하므로 건너뛴다(두 번 반응 방지).
    // KKH 코드를 고치지 않고 피격 연출을 붙이려고 이벤트로 받는다 — 단, 때린 위치를 몰라서 넉백은 없다
    private void OnPlayerHpChanged(int currentHp, int maxHp)
    {
        if (Instance != null && Instance != this) return;
        if (!isActiveAndEnabled) return;

        int previous = lastPlayerHp;
        lastPlayerHp = currentHp;
        if (previous < 0 || currentHp >= previous || !IsBossBattle) return;

        ActionExecutor player = fighters.Find(f => f != null && f.FighterId == "Player");
        if (player == null || player.State == null) return;

        var result = new HitResolutionResult { DamageToHp = previous - currentHp };
        bool reacted = HitReactionSystem.Apply(player, result);
        player.NotifyDamaged(result);
        HitStopSystem.ApplyVictimOnly(hitStopSettings, player, result.DamageToHp, reacted);
    }

    // 두 사각형이 겹치는지 검사하는 표준 AABB(축 정렬 바운딩 박스) 겹침 판정.
    // 한쪽의 왼쪽이 상대의 오른쪽보다 왼쪽에 있고 + 오른쪽이 상대의 왼쪽보다 오른쪽에 있고, 위아래도 마찬가지면 겹친 것이다
    private static bool Overlap(Rect a, Rect b) =>
        a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
}
