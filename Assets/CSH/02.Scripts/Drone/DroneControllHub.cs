using System.Collections.Generic;
using UnityEngine;

public class DroneControllHub : MonoBehaviour
{
    [Header("드론")]
    [SerializeField] private DroneUnit dronePrefab;

    [Tooltip("시작 시 미리 만들어둘 드론 수. 부족하면 런타임에 추가 생성한다 (출격 중 D로 재장전되는 경우 대비)")]
    [SerializeField] private int prewarmCount = CombatantSnapshot.MaxCylinder + 1;

    /// <summary>공전 궤도 1개의 설정. orbiting 리스트에서 index % 그룹 수 가 같은 드론끼리 이 궤도를 공유한다</summary>
    [System.Serializable]
    public class OrbitGroup
    {
        [Tooltip("플레이어 피벗(발밑) 기준 궤도 중심 (높이는 y)")]
        public Vector3 centerOffset = new Vector3(0f, 1.6f, 0f);

        [Tooltip("궤도 반지름. x = 궤도 장축, y = 궤도 단축 (같으면 원)")]
        public Vector2 radius = new Vector2(0.9f, 0.9f);

        [Tooltip("궤도면 기울기(오일러각). (0,0,0) = 바닥과 평행한 수평 고리. x/z를 기울이면 아톰 모양")]
        public Vector3 tiltEuler = Vector3.zero;

        [Tooltip("초당 회전 각도 (절댓값)")]
        public float speedDeg = 120f;

        [Tooltip("체크하면 시계 방향(위에서 볼 때)")]
        public bool clockwise = false;

        [Tooltip("시작 각도 오프셋. 그룹끼리 같은 위상으로 겹쳐 보이지 않게 어긋나게 한다")]
        public float phaseOffsetDeg = 0f;

        [System.NonSerialized] public float angle; // 런타임 누적 각도
    }
    [Header("공전")]
    [Tooltip("궤도 목록. orbiting 리스트의 0,3,6… → [0], 1,4,7… → [1], 2,5,8… → [2]")]
    [SerializeField]
    private List<OrbitGroup> orbitGroups = new List<OrbitGroup>
    {
        new OrbitGroup { centerOffset = new Vector3(0f, 0.45f, 0f), radius = new Vector2(0.6f, 0.2f), tiltEuler = new Vector3(0f, 0f,  25f), speedDeg = 120f, clockwise = false, phaseOffsetDeg =   0f },
        new OrbitGroup { centerOffset = new Vector3(0f, 0.48f, 0f), radius = new Vector2(0.6f, 0.2f), tiltEuler = new Vector3(0f, 0f, -25f), speedDeg =  90f, clockwise = true,  phaseOffsetDeg = 120f },
        new OrbitGroup { centerOffset = new Vector3(0f, 0.44f, 0f), radius = new Vector2(0.6f, 0.2f), tiltEuler = new Vector3(60f, 0f,  0f), speedDeg = 150f, clockwise = false, phaseOffsetDeg = 240f },
    };
    [Tooltip("공전 슬롯을 따라가는 부드러움(초). 드론 수가 바뀌어 그룹·슬롯이 재배치될 때 순간이동하지 않게 한다")]
    [SerializeField] private float orbitSmoothTime = 0.08f;

    [Tooltip("드론 등장·비상 출격 시 기준점 (플레이어 피벗 기준)")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.6f, 0f);

    [Header("출격")]
    [Tooltip("Hit박스가 없는 행동(투사체 등)일 때 쓸 목표 지점. 플레이어 피벗 기준, 오른쪽을 볼 때 기준 좌표")]
    [SerializeField] private Vector2 fallbackLocalTarget = new Vector2(1.2f, 1.2f);
    [Tooltip("한 박스에 여러 기가 몰릴 때 서로 벌어지는 간격 (로컬 Y 방향)")]
    [SerializeField] private float multiDroneSpacing = 0.35f;
    [Tooltip("공격 위치 Z 오프셋 (캐릭터보다 카메라 쪽으로 빼고 싶을 때 음수)")]
    [SerializeField] private float attackZOffset = -0.1f;
    [Tooltip("이동 진행도(틱 기준 0~1) → 위치 보간값(0~1). 기본은 처음 빠르게 나가서 도착 직전 감속")]
    [SerializeField]
    private AnimationCurve attackMoveCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));

    // ---- 참조 ----
    private ActionExecutor executor;
    private ActionState state;
    private RobotMover mover;

    // ---- 풀 ----
    private readonly List<DroneUnit> pool = new List<DroneUnit>(8);
    private readonly List<DroneUnit> orbiting = new List<DroneUnit>(4);

    // ---- 진행 중인 출격 1건 ----
    // ActionState는 한 번에 행동 하나만 돌리므로 출격도 동시에 1건만 존재한다
    private class Sortie
    {
        public ActionData action;
        public int hitStartFrame;      // 이 GlobalFrame에 도달하면 드론을 끈다
        public int travelFrames;       // 이동에 쓰는 틱 수 (= hitStartFrame - 1, 히트 직전 틱에 도착)
        public readonly List<DroneUnit> drones = new List<DroneUnit>(3);
    }
    private Sortie sortie;
    private readonly List<FrameBox> hitBoxBuffer = new List<FrameBox>(8);

    // ========================================================================
    // 생명주기
    // ========================================================================

    private void OnEnable() => CombatClock.Instance.OnCombatTick += OnCombatTick;

    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= OnCombatTick;
    }

    // Start에서 구독하는 이유: ActionState는 PlayerRobotBootstrap.Awake → ActionExecutor.Init에서 생성되므로
    // 이 컴포넌트의 Awake 시점엔 아직 없을 수 있다 (FlameEmitter와 같은 방식)
    private void Start()
    {
        executor = GetComponent<ActionExecutor>();
        mover = GetComponent<RobotMover>();
        state = executor.State;

        if (state == null)
        {
            Debug.LogWarning($"[DroneControlHub] {name}: ActionExecutor.State가 없음 — 드론 비활성");
            enabled = false;
            return;
        }
        if (dronePrefab == null)
        {
            Debug.LogWarning($"[DroneControlHub] {name}: dronePrefab이 비어 있음 — 드론 비활성");
            enabled = false;
            return;
        }

        executor.OnCylinderSpent += OnCylinderSpent;
        state.OnDead += OnDead;

        for (int i = 0; i < prewarmCount; i++) CreateDrone();
    }

    private void OnDestroy()
    {
        if (executor != null) executor.OnCylinderSpent -= OnCylinderSpent;
        if (state != null) state.OnDead -= OnDead;

        foreach (var d in pool)
            if (d != null) Destroy(d.gameObject);
        pool.Clear();
    }

    // ========================================================================
    // 이벤트 (전투 틱 안에서 호출됨)
    // ========================================================================

    // 호출: ActionExecutor.SpendCylinder (state.Begin 직후, 같은 틱). 이 시점엔 state.Timeline이 이미 확정돼 있다
    private void OnCylinderSpent(ActionData action, int cost)
    {
        if (action == null || cost <= 0) return;

        // 이전 출격이 정리 안 됐으면(이론상 없음) 먼저 정리
        if (sortie != null) FinishSortie(playEffect: false);

        // 실린더가 이미 차감됐으므로 LateUpdate의 동기화보다 먼저 공전 드론을 빼와야 한다
        var s = new Sortie { action = action };
        int take = Mathf.Min(cost, orbiting.Count);
        for (int i = 0; i < take; i++)
        {
            // 앞쪽(공격 방향)에 가까운 드론부터 보내면 동선이 덜 꼬인다
            DroneUnit d = PickFrontMostOrbiting();
            orbiting.Remove(d);
            s.drones.Add(d);
        }
        // 공전 드론이 모자라면(동기화 직전 등) 풀에서 바로 꺼내 플레이어 위치에서 출발시킨다
        for (int i = take; i < cost; i++)
        {
            DroneUnit d = GetFreeDrone();
            d.Appear(OrbitCenterWorld());
            s.drones.Add(d);
        }

        ResolveTargets(action, s);
        sortie = s;
    }

    private void OnCombatTick()
    {
        if (sortie == null || state == null) return;

        // 피격 Interrupt / 사망 / 다른 행동으로 바뀜 → 행동이 끊겼다. 탄은 이미 소모됐으니 드론도 소모 처리
        if (state.CurrentAction != sortie.action || state.IsDead)
        {
            FinishSortie(playEffect: false);
            return;
        }

        // Hit박스가 켜지는 프레임 도달 → 목표 지점에 붙인 뒤 드론 off (이펙트도 정확히 히트 위치에서 터진다).
        // CombatClock 구독 순서상 ActionExecutor보다 먼저/나중에 불릴 수 있어 ">="로 본다 (최대 1틱 차이)
        if (state.GlobalFrame >= sortie.hitStartFrame)
        {
            ApplySortiePositions(1f);
            FinishSortie(playEffect: true);
        }
    }

    private void OnDead()
    {
        if (sortie != null) FinishSortie(playEffect: false);
        foreach (var d in orbiting) d.Hide();
        orbiting.Clear();
    }

    // ========================================================================
    // 표시 (렌더 프레임)
    // ========================================================================

    private void LateUpdate()
    {
        if (state == null) return;

        SyncOrbitCount();
        UpdateOrbit();
        UpdateSortie();
    }

    // 공전 드론 수를 스냅샷의 실린더 수에 맞춘다. 이벤트 구독 대신 폴링하는 이유:
    // 실린더가 바뀌는 길이 D 적중(+1), 소모(-n), 전투 리셋(=1) 등 여러 갈래라 값을 직접 보는 쪽이 빠뜨릴 일이 없다
    private void SyncOrbitCount()
    {
        if (state.IsDead) return;

        var hub = CombatDataHub.Instance;
        var snapshot = hub != null ? hub.GetSnapshot(executor.FighterId) : null;
        if (snapshot == null) return;

        int desired = Mathf.Max(0, snapshot.currentCylinder);

        while (orbiting.Count < desired)
        {
            DroneUnit d = GetFreeDrone();
            d.Appear(OrbitCenterWorld());
            orbiting.Add(d);
        }
        while (orbiting.Count > desired)
        {
            DroneUnit d = orbiting[orbiting.Count - 1];
            orbiting.RemoveAt(orbiting.Count - 1);
            d.Hide();
        }
    }

    // 그룹별 각도는 드론이 없어도 계속 돈다 — 드론이 새로 들어올 때 궤도 위상이 튀지 않게
    private void UpdateOrbit()
    {
        int groupCount = orbitGroups.Count;
        if (groupCount == 0) return;

        foreach (var g in orbitGroups)
        {
            float dir = g.clockwise ? -1f : 1f;
            g.angle = (g.angle + dir * g.speedDeg * Time.deltaTime) % 360f;
        }

        if (orbiting.Count == 0) return;

        for (int i = 0; i < orbiting.Count; i++)
        {
            int groupIndex = i % groupCount;          // 0,3,6… / 1,4,7… / 2,5,8…
            int memberIndex = i / groupCount;         // 그룹 안에서 몇 번째
            int memberCount = GroupMemberCount(groupIndex, groupCount);

            Vector3 slot = OrbitSlotWorld(orbitGroups[groupIndex], memberIndex, memberCount);

            DroneUnit d = orbiting[i];
            d.transform.position = Vector3.SmoothDamp(d.transform.position, slot, ref d.velocity, orbitSmoothTime);
        }
    }

    // 그룹 groupIndex에 속한 공전 드론 수 (orbiting 인덱스 중 i % groupCount == groupIndex 인 것의 개수)
    private int GroupMemberCount(int groupIndex, int groupCount)
    {
        return (orbiting.Count - groupIndex + groupCount - 1) / groupCount;
    }

    // 궤도 위 한 점: 궤도면(XZ 평면)에서 타원 위 점을 구한 뒤 tiltEuler로 기울이고 중심으로 옮긴다.
    // 같은 그룹 안의 드론끼리는 360/memberCount 간격으로 벌린다
    private Vector3 OrbitSlotWorld(OrbitGroup g, int memberIndex, int memberCount)
    {
        float step = memberCount > 0 ? 360f / memberCount : 0f;
        float rad = (g.angle + g.phaseOffsetDeg + step * memberIndex) * Mathf.Deg2Rad;

        Vector3 local = new Vector3(Mathf.Cos(rad) * g.radius.x, 0f, Mathf.Sin(rad) * g.radius.y);
        return transform.position + g.centerOffset + Quaternion.Euler(g.tiltEuler) * local;
    }

    // 틱 단위 이동: 진행도를 시간(deltaTime)이 아니라 state.GlobalFrame(정수 틱)으로만 계산한다.
    // → 위치는 틱이 넘어갈 때만 바뀌고, 일시정지·FrameStepper·프레임 드랍에서도 판정 타이밍과 1:1로 맞는다.
    // 계산을 OnCombatTick이 아니라 LateUpdate에서 "읽기만" 하는 이유: CombatClock 구독 순서상 이 허브가
    // ActionExecutor보다 먼저 불리면 GlobalFrame/로봇 위치가 1틱 늦은 값이 된다. LateUpdate는 그 렌더 프레임의
    // 모든 틱이 끝난 뒤라 순서와 무관하게 항상 최신 틱 값을 본다. 값 자체는 틱으로만 변하므로 결과는 틱 이동과 같다
    private void UpdateSortie()
    {
        if (sortie == null) return;

        float progress = Mathf.Clamp01((float)state.GlobalFrame / sortie.travelFrames);
        ApplySortiePositions(attackMoveCurve.Evaluate(progress));
    }

    private void ApplySortiePositions(float t)
    {
        bool facingRight = mover == null || mover.FacingRight;
        Vector3 pivot = transform.position;

        foreach (var d in sortie.drones)
        {
            // 목표는 매번 다시 계산한다 — 선딜 중 전진(AdvanceDistance)이나 방향 전환이 있어도 Hit박스 위치를 따라간다
            Vector3 target = BoxResolver.ToWorldPoint(pivot, d.attackLocalTarget, facingRight);
            target.z += attackZOffset;
            d.transform.position = Vector3.LerpUnclamped(d.attackStartPos, target, t);
        }
    }

    // ========================================================================
    // 목표 지점 / 히트 프레임 계산
    // ========================================================================

    // 행동의 Hit박스 중 "가장 먼저 켜지는" 것들을 찾아 드론을 배정한다.
    // 창 계산은 ResolvedAction.GetWindow를 그대로 써서 BoxDrawer·HitDetection과 같은 기준을 유지한다 (§4)
    private void ResolveTargets(ActionData action, Sortie s)
    {
        ResolvedAction tl = state.Timeline;
        hitBoxBuffer.Clear();
        int earliest = int.MaxValue;

        foreach (FrameBox box in action.FrameBoxes)
        {
            if (box.type != BoxType.Hit) continue;

            ResolvedAction.GetWindow(box, tl.StartupFrames, tl.ActiveFrames, tl.RecoveryFrames, out int start, out int end);
            if (end < start) continue; // 이번 실행에서 안 켜지는 박스

            if (start < earliest)
            {
                earliest = start;
                hitBoxBuffer.Clear();
            }
            if (start == earliest) hitBoxBuffer.Add(box);
        }

        if (hitBoxBuffer.Count > 0)
        {
            s.hitStartFrame = earliest;
            AssignToBoxes(s);
        }
        else
        {
            // Hit박스가 없는 행동(투사체 등): 활성 진입 프레임에 끄고, 총구 → 지정 오프셋 순으로 목표를 잡는다
            s.hitStartFrame = Mathf.Max(1, tl.StartupFrames);
            Vector2 baseTarget = action.Projectile != null ? action.ProjectileMuzzle : fallbackLocalTarget;
            for (int i = 0; i < s.drones.Count; i++)
                s.drones[i].BeginAttack(baseTarget + SpreadOffset(i, s.drones.Count));
        }

        // 히트 틱에는 꺼지므로 그 직전 틱(hitStartFrame - 1)에 목표에 도착하게 한다. 선딜이 1틱 이하면 1틱만에 도착
        s.travelFrames = Mathf.Max(1, s.hitStartFrame - 1);
    }

    // 박스가 여러 개면(부스터 샷 산탄 등) 드론을 박스마다 나눠 배정, 한 박스에 여러 기가 몰리면 위아래로 벌린다
    private void AssignToBoxes(Sortie s)
    {
        int boxCount = hitBoxBuffer.Count;
        for (int i = 0; i < s.drones.Count; i++)
        {
            FrameBox box = hitBoxBuffer[i % boxCount];
            int sameBoxIndex = i / boxCount;
            int sameBoxTotal = (s.drones.Count + boxCount - 1 - (i % boxCount)) / boxCount;

            Vector2 local = box.rect.center + SpreadOffset(sameBoxIndex, sameBoxTotal);
            s.drones[i].BeginAttack(local);
        }
    }

    private Vector2 SpreadOffset(int index, int total)
    {
        if (total <= 1) return Vector2.zero;
        float mid = (total - 1) * 0.5f;
        return new Vector2(0f, (index - mid) * multiDroneSpacing);
    }

    private void FinishSortie(bool playEffect)
    {
        if (sortie == null) return;
        foreach (var d in sortie.drones) d.Consume(playEffect);
        sortie = null;
    }

    // ========================================================================
    // 풀 / 유틸
    // ========================================================================

    private DroneUnit CreateDrone()
    {
        // 플레이어 자식으로 두지 않는다 — 방향 전환 시 스케일 반전·스프라이트 플립에 같이 뒤집히지 않게 월드에 둔다
        DroneUnit d = Instantiate(dronePrefab, transform.position, Quaternion.identity);
        d.name = $"{name}_Drone_{pool.Count}";
        pool.Add(d);
        return d;
    }

    private DroneUnit GetFreeDrone()
    {
        foreach (var d in pool)
            if (d.State == DroneState.Off) return d;
        return CreateDrone();
    }

    private DroneUnit PickFrontMostOrbiting()
    {
        bool facingRight = mover == null || mover.FacingRight;
        DroneUnit best = orbiting[0];
        float bestX = facingRight ? float.MinValue : float.MaxValue;
        foreach (var d in orbiting)
        {
            float x = d.transform.position.x;
            if (facingRight ? x > bestX : x < bestX)
            {
                bestX = x;
                best = d;
            }
        }
        return best;
    }

    private Vector3 OrbitCenterWorld() => transform.position + spawnOffset;
}
