using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실린더를 드론으로 표현하는 허브. 플레이어 로봇(ActionExecutor가 붙은 오브젝트)에 붙인다.
///
/// 흐름
///  1) 전투 내내 드론은 실린더 최대치(MaxCylinder)만큼 등 뒤 슬롯에 떠 있다.
///     장전된 발수만큼은 원래 색, 나머지는 검정 반투명(빈 탄). CombatDataHub의 currentCylinder에 맞춘다
///  2) 실린더 소모 스킬 시작: ActionExecutor.OnCylinderSpent(action, cost) → 장전된 드론 cost기가 출격
///  3) 선딜 동안: 가장 먼저 켜지는 Hit박스 중심으로 틱 단위 이동 (Hit박스가 없는 행동은 총구 / 기본 자리)
///  4) Hit박스가 실제로 켜진 틱: 검정 반투명으로 바뀐다 (자리는 그대로 유지, 플레이어를 따라감)
///  5) 액티브가 끝나면(후딜 진입) 등 뒤 슬롯으로 복귀. 이후 D 적중으로 재장전되면 원래 색으로 돌아온다
///  6) 피격·사망으로 행동이 끊기면: 탄은 이미 소모됐으므로 바로 빈 탄으로 바꾸고 복귀
///
/// 홀드형 스킬은 탄을 쓴 뒤에도 행동이 바뀌거나(차지 샷 강화 → 단계 판정) 홀드가 이어질 수 있어서,
/// "끊김"은 행동 비교만이 아니라 그 행동을 돌리던 스킬 러너가 아직 도는지로도 판단하고,
/// "빈 탄 전환"은 계산한 프레임이 아니라 실제로 Hit박스가 켜졌는지로 판단한다.
///
/// 판정·데미지·탄 차감은 기존 시스템이 담당하고, 이 클래스는 표시만 한다.
/// </summary>
[RequireComponent(typeof(ActionExecutor))]
public class UnitControllHub : MonoBehaviour
{
    [Header("드론")]
    [SerializeField] private Unit dronePrefab;

    [Header("대기 (등 뒤)")]
    [Tooltip("슬롯별 대기 자리. 피벗 기준 로컬, 오른쪽을 볼 때 기준(x 음수 = 등 뒤). z = 깊이 오프셋. 개수는 MaxCylinder에 맞춘다")]
    [SerializeField]
    private List<Vector3> idleSlots = new List<Vector3>
    {
        new Vector3(-0.45f, 0.65f, 0.05f),
        new Vector3(-0.60f, 0.45f, 0.05f),
        new Vector3(-0.45f, 0.25f, 0.05f),
    };
    [Tooltip("대기 중 위아래로 흔들리는 폭 (0이면 정지)")]
    [SerializeField] private float idleBobAmplitude = 0.03f;
    [Tooltip("흔들림 속도 (초당 주기 수)")]
    [SerializeField] private float idleBobFrequency = 1.2f;
    [Tooltip("대기 슬롯을 따라가는 부드러움(초). 방향 전환·복귀 때 순간이동하지 않게 한다")]
    [SerializeField] private float idleSmoothTime = 0.1f;

    [Header("출격")]
    [Tooltip("Hit박스도 투사체도 없는 행동일 때의 자리. 피벗 기준, 오른쪽 볼 때 기준")]
    [SerializeField] private Vector2 fallbackLocalTarget = new Vector2(0.05f, 0.3f);
    [Tooltip("Hit박스 배치일 때 한 박스에 여러 기가 몰리면 벌어지는 간격 (로컬 Y)")]
    [SerializeField] private float multiDroneSpacing = 0.3f;
    [Tooltip("Hit박스 배치일 때 깊이 오프셋 (카메라 쪽이면 음수)")]
    [SerializeField] private float hitBoxZOffset = -0.1f;
    [Tooltip("이동 진행도(틱 기준 0~1) → 위치 보간값(0~1). 기본은 처음 빠르게 나가서 도착 직전 감속")]
    [SerializeField]
    private AnimationCurve sortieMoveCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));

    // ---- 참조 ----
    private ActionExecutor executor;
    private ActionState state;
    private RobotMover mover;

    // ---- 드론 (슬롯 순서 고정) ----
    private readonly List<Unit> drones = new List<Unit>(CombatantSnapshot.MaxCylinder);

    // ---- 진행 중인 출격 1건 ----
    // ActionState는 한 번에 행동 하나만 돌리므로 출격도 동시에 1건만 존재한다
    private class Sortie
    {
        public ActionData action;      // 탄을 쓴 행동
        public SkillRunner runner;     // 그 행동을 돌리는 스킬 러너 (일반 ActionData면 null)
        public int ticks;              // 출격 후 지난 틱 수 (이동 진행도)
        public int travelFrames;       // 이동에 쓰는 틱 수 (히트 직전 틱에 도착)
        public bool spent;             // 이미 빈 탄으로 바뀌었는지
        public readonly List<Unit> drones = new List<Unit>(CombatantSnapshot.MaxCylinder);
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
        executor = TryGetComponent<ActionExecutor>(out var exec) ? exec : null;
        mover = TryGetComponent<RobotMover>(out var mov) ? mov : null;
        state = executor.State;

        if (state == null)
        {
            Debug.LogWarning($"[UnitControllHub] {name}: ActionExecutor.State가 없음 — 드론 비활성");
            enabled = false;
            return;
        }
        if (dronePrefab == null)
        {
            Debug.LogWarning($"[UnitControllHub] {name}: dronePrefab이 비어 있음 — 드론 비활성");
            enabled = false;
            return;
        }

        executor.OnCylinderSpent += OnCylinderSpent;
        state.OnDead += OnDead;

        for (int i = 0; i < CombatantSnapshot.MaxCylinder; i++)
        {
            // 플레이어 자식으로 두지 않는다 — 방향 전환 시 스케일 반전에 같이 뒤집히지 않게 월드에 둔다
            Unit d = Instantiate(dronePrefab, transform.position, Quaternion.identity);
            d.name = $"{name}_Drone_{i}";
            d.slotIndex = i;
            d.Place(IdleSlotWorld(i));
            drones.Add(d);
        }
        SyncCharge();
    }

    private void OnDestroy()
    {
        if (executor != null) executor.OnCylinderSpent -= OnCylinderSpent;
        if (state != null) state.OnDead -= OnDead;

        foreach (var d in drones)
            if (d != null) Destroy(d.gameObject);
        drones.Clear();
    }


    // ========================================================================
    // 이벤트 (전투 틱 안에서 호출됨)
    // ========================================================================

    // 호출: ActionExecutor.SpendCylinder (state.Begin 직후, 같은 틱). 이 시점엔 state.Timeline이 이미 확정돼 있다
    private void OnCylinderSpent(ActionData action, int cost)
    {
        if (action == null || cost <= 0) return;

        // 이전 출격이 정리 안 됐으면(이론상 없음) 먼저 정리
        if (sortie != null) ReleaseSortie();

        var s = new Sortie { action = action };

        // 장전된 대기 드론 중 슬롯 번호가 큰 것부터 보낸다 (리볼버처럼 마지막에 장전된 탄부터 쓰는 느낌)
        for (int i = drones.Count - 1; i >= 0 && s.drones.Count < cost; i--)
        {
            Unit d = drones[i];
            if (d.IsCharged && !d.inSortie) s.drones.Add(d);
        }
        if (s.drones.Count < cost)
            Debug.LogWarning($"[UnitControllHub] '{action.ActionName}' 실린더 {cost}발 소모인데 장전된 드론은 {s.drones.Count}기");

        // 출격 드론 배정 순서: 앞에서부터 0,1,2 — 슬롯 번호 작은 순으로 다시 정렬해 자리 배정 순서를 안정적으로
        s.drones.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));

        ResolveTargets(action, s);
        CaptureRunner(s);
        sortie = s;
    }

    // 탭형은 러너가 SkillDriver에 등록되기 전에 탄을 내므로(Begin 안에서 비용 확정) 다음 틱에 다시 잡는다.
    // 러너가 지금 돌리는 행동이 탄을 쓴 행동일 때만 잡는다 — 체인 후속 행동(연속 펀치 피니시)을 이전 러너에 묶지 않으려고
    private void CaptureRunner(Sortie s)
    {
        if (s.runner != null) return;
        SkillRunner running = executor.SkillDriver != null ? executor.SkillDriver.CurrentRunner : null;
        if (running != null && running.Current == s.action) s.runner = running;
    }

    // CombatClock 구독 순서상 ActionExecutor보다 먼저/나중에 불릴 수 있다 (최대 1틱 차이)
    private void OnCombatTick()
    {
        if (sortie == null || state == null) return;

        sortie.ticks++;
        CaptureRunner(sortie);

        // 끊김: 탄을 쓴 행동도 아니고, 그 행동을 돌리던 러너도 끝났다 (피격 Interrupt / 사망).
        // 러너가 도는 중에 행동만 바뀐 것(차지 샷 강화 → 단계 판정)은 끊김이 아니다
        bool sameAction = state.CurrentAction == sortie.action;
        bool runnerAlive = sortie.runner != null && sortie.runner.IsRunning;
        bool interrupted = state.IsDead || !(sameAction || runnerAlive);
        bool recovering = state.Phase == ActionPhase.Recovery;

        // Hit박스가 실제로 켜진 틱 → 빈 탄으로. 끊기거나 후딜까지 왔으면 탄은 이미 소모됐으니 무조건 빈 탄
        if (!sortie.spent && (interrupted || recovering || IsHitFrameNow()))
        {
            foreach (var d in sortie.drones) d.SetCharged(false);
            sortie.spent = true;
        }

        // 액티브가 끝나면(후딜 진입) 또는 끊기면 등 뒤로 복귀
        if (interrupted || recovering)
            ReleaseSortie();
    }

    // 지금 Hit박스가 켜져 있는가. Hit박스가 아예 없는 행동(투사체 등)은 활성 진입을 히트로 본다.
    // 차지(홀드) 중에는 러너가 Hit박스를 빼고 돌려주므로 홀드 시간이 길어도 일찍 바뀌지 않는다
    private bool IsHitFrameNow()
    {
        foreach (FrameBox box in state.GetActiveBoxes())
            if (box.type == BoxType.Hit) return true;

        if (state.Phase != ActionPhase.Active) return false;

        ActionData action = state.CurrentAction;
        if (action == null) return true;
        foreach (FrameBox box in action.FrameBoxes)
            if (box.type == BoxType.Hit) return false; // Hit박스가 있는데 아직 안 켜졌다
        return true;
    }

    private void OnDead()
    {
        if (sortie != null) ReleaseSortie();
        foreach (var d in drones) d.SetCharged(false);
    }

    private void ReleaseSortie()
    {
        if (sortie == null) return;
        foreach (var d in sortie.drones) d.EndSortie();
        sortie = null;
    }

    // ========================================================================
    // 표시 (렌더 프레임)
    // ========================================================================

    private void LateUpdate()
    {
        if (state == null) return;

        SyncCharge();
        UpdateSortiePositions();
        UpdateIdlePositions();
    }

    // 장전 색 드론 수를 스냅샷의 currentCylinder에 맞춘다. 이벤트 대신 폴링하는 이유:
    // 실린더가 바뀌는 길이 D 적중(+1), 소모(-n), 전투 리셋(=1) 등 여러 갈래라 값을 직접 보는 쪽이 빠뜨릴 일이 없다.
    // 출격 중(아직 히트 전) 드론은 이미 탄이 차감된 상태라 "장전 수"에서 뺀다 — 히트 프레임까지는 원래 색을 유지해야 하므로
    private void SyncCharge()
    {
        if (state.IsDead) return;

        var hub = CombatDataHub.Instance;
        var snapshot = hub != null ? hub.GetSnapshot(executor.FighterId) : null;
        if (snapshot == null) return;

        int desired = Mathf.Clamp(snapshot.currentCylinder, 0, drones.Count);

        int charged = 0;
        foreach (var d in drones)
            if (d.IsCharged && !d.inSortie) charged++;

        // 재장전: 대기 중인 빈 탄 드론을 슬롯 번호 작은 것부터 원래 색으로
        for (int i = 0; i < drones.Count && charged < desired; i++)
        {
            Unit d = drones[i];
            if (!d.IsCharged && !d.inSortie)
            {
                d.SetCharged(true);
                charged++;
            }
        }

        // 초과(전투 리셋 등): 대기 중인 장전 드론을 슬롯 번호 큰 것부터 빈 탄으로
        for (int i = drones.Count - 1; i >= 0 && charged > desired; i--)
        {
            Unit d = drones[i];
            if (d.IsCharged && !d.inSortie)
            {
                d.SetCharged(false);
                charged--;
            }
        }
    }

    private void UpdateIdlePositions()
    {
        foreach (var d in drones)
        {
            if (d.inSortie) continue;

            Vector3 slot = IdleSlotWorld(d.slotIndex);
            if (idleBobAmplitude > 0f)
            {
                // 슬롯마다 위상을 어긋나게 해서 같이 출렁이지 않게 한다
                float phase = d.slotIndex * 0.33f;
                slot.y += Mathf.Sin((Time.time * idleBobFrequency + phase) * Mathf.PI * 2f) * idleBobAmplitude;
            }
            d.transform.position = Vector3.SmoothDamp(d.transform.position, slot, ref d.velocity, idleSmoothTime);
        }
    }

    // 틱 단위 이동: 진행도를 시간이 아니라 출격 후 지난 틱 수(정수)로만 계산한다.
    // state.GlobalFrame을 안 쓰는 이유: 홀드형은 홀드 시간이 GlobalFrame에 섞여 있어 이동이 앞당겨진다.
    // LateUpdate에서 읽는 이유: CombatClock 구독 순서와 무관하게 그 렌더 프레임의 모든 틱이 끝난 최신 값을 보기 위해.
    // 히트 이후(액티브 동안)는 t=1로 스킬 자리에 붙어 플레이어를 따라간다
    private void UpdateSortiePositions()
    {
        if (sortie == null) return;

        float t = sortie.spent ? 1f : sortieMoveCurve.Evaluate(Mathf.Clamp01((float)sortie.ticks / sortie.travelFrames));
        bool facingRight = mover == null || mover.FacingRight;
        Vector3 pivot = transform.position;

        foreach (var d in sortie.drones)
        {
            Vector3 target = LocalToWorld(pivot, d.sortieLocalTarget, facingRight);
            d.transform.position = Vector3.LerpUnclamped(d.sortieStartPos, target, t);
        }
    }

    // ========================================================================
    // 출격 자리 / 히트 프레임 계산
    // ========================================================================

    // 도착 시점은 "가장 먼저 켜지는 Hit박스"로 정한다 (BoxDrawer·HitDetection과 같은 ResolvedAction.GetWindow 기준).
    // 자리는 그 Hit박스 중심 (실제 빈 탄 전환은 IsHitFrameNow가 판단한다)
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

        // Hit박스가 없는 행동(투사체 등)은 활성 진입 프레임을 히트 프레임으로 본다
        int hitStartFrame = hitBoxBuffer.Count > 0 ? earliest : Mathf.Max(1, tl.StartupFrames);
        // 히트 직전 틱에 도착하게 한다. 선딜이 1틱 이하면 1틱만에 도착
        s.travelFrames = Mathf.Max(1, hitStartFrame - 1);

        if (hitBoxBuffer.Count > 0)
        {
            AssignToBoxes(s);
        }
        else
        {
            Vector2 baseTarget = action.Projectile != null
                ? new Vector2(action.ProjectileMuzzle.x, action.ProjectileMuzzle.y)
                : fallbackLocalTarget;
            for (int i = 0; i < s.drones.Count; i++)
                s.drones[i].BeginSortie(baseTarget + SpreadOffset(i, s.drones.Count));
        }
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
            s.drones[i].BeginSortie(new Vector3(local.x, local.y, hitBoxZOffset));
        }
    }

    private Vector2 SpreadOffset(int index, int total)
    {
        if (total <= 1) return Vector2.zero;
        float mid = (total - 1) * 0.5f;
        return new Vector2(0f, (index - mid) * multiDroneSpacing);
    }

    // ========================================================================
    // 유틸
    // ========================================================================

    // x,y는 BoxResolver 규칙으로 좌우 반전, z는 깊이 오프셋으로 그대로 더한다
    private static Vector3 LocalToWorld(Vector3 pivot, Vector3 local, bool facingRight)
    {
        Vector3 world = BoxResolver.ToWorldPoint(pivot, new Vector2(local.x, local.y), facingRight);
        world.z += local.z;
        return world;
    }

    private Vector3 IdleSlotWorld(int slotIndex)
    {
        Vector3 local = idleSlots.Count > 0
            ? idleSlots[Mathf.Min(slotIndex, idleSlots.Count - 1)]
            : new Vector3(-0.5f, 0.5f, 0f);
        bool facingRight = mover == null || mover.FacingRight;
        return LocalToWorld(transform.position, local, facingRight);
    }
}