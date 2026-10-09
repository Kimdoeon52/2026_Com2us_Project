using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반 적(몹) AI 입력원 (§8, §11-9). 플레이어와 같은 로봇 본체에 "키보드 대신" 꽂히는 두뇌다 —
/// ActionExecutor/RobotMover/RobotView는 이게 사람인지 AI인지 모른다(§8 "입력만 바꿔 끼운다").
///
/// 하는 일은 딱 하나: 판단할 때가 되면 EnemyAIProfile의 선택지 중 하나를 가중치로 뽑는다.
///   · 기술   — 사거리 안 + 쿨타임 끝 + 부위 파손으로 봉인 안 됨(RuntimeRobot) + 상대 위치 조건
///   · 접근   — 상대가 preferredRange보다 멀 때
///   · 대기   — 한 템포 쉬기
///   · 점프   — 상대 쪽으로 뛰기
///
/// "지금 공격 중인가 / 경직 중인가 / 공중인가 / 죽었는가"는 따로 들고 있지 않고 ActionState·RobotMover에
/// 매번 물어본다 — AI가 자기 상태기계를 따로 가지면 실제 몸 상태와 어긋나기 때문이다.
///
/// 판단(쿨타임·템포)은 CombatClock 틱에서만 센다(§3). GetMoveInput/GetJumpInput은 RobotMover가
/// 렌더 프레임마다 부르므로, 틱에서 정해둔 의도를 읽어가기만 한다.
/// </summary>
public class AIInputSource : MonoBehaviour, IInputSource
{
    private enum MoveIntent { Hold, Approach }

    private enum ChoiceKind { Skill, Approach, Wait, Jump }

    private struct Choice
    {
        public ChoiceKind kind;
        public int skillIndex;
        public float weight;
    }

    [Tooltip("쫓아가고 때릴 대상. 보통 Player의 Transform")]
    [SerializeField] private Transform opponent;

    [Tooltip("이 적의 성격(기술별 사거리·가중치·쿨타임, 접근·대기·점프 성향)")]
    [SerializeField] private EnemyAIProfile profile;

    [Tooltip("0이면 판마다 다른 판단. 0이 아니면 같은 시드로 같은 판단 순서가 재현된다(버그 재현용)")]
    [SerializeField] private int randomSeed;

    // 자기 몸 — 상태(행동/사망)·부위 파손 목록·바라보는 방향을 묻는 용도
    private ActionExecutor executor;
    private RobotMover mover;
    // 상대 몸 — 사망 여부·공중 여부를 묻는 용도
    private ActionExecutor opponentExecutor;
    private RobotMover opponentMover;

    private System.Random rng;
    private int[] skillCooldowns = new int[0];
    private int jumpCooldown;
    private int thinkTimer;

    // 틱에서 정한 의도. 입력 함수들이 읽어간다
    private ActionData pendingAction;
    private bool pendingJump;
    private MoveIntent moveIntent = MoveIntent.Hold;

    // 매 판단마다 새로 만들지 않으려고 재사용하는 후보 목록
    private readonly List<Choice> choices = new List<Choice>();

    private void Awake()
    {
        executor = GetComponent<ActionExecutor>();
        mover = GetComponent<RobotMover>();
        if (opponent != null)
        {
            opponentExecutor = opponent.GetComponent<ActionExecutor>();
            opponentMover = opponent.GetComponent<RobotMover>();
        }

        rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
        if (profile != null) skillCooldowns = new int[profile.Skills.Length];
        if (profile == null) Debug.LogWarning($"[AIInputSource] {name}에 EnemyAIProfile이 없음 — 아무 판단도 안 함");
    }

    // 프로필 기술이 ActionExecutor.allActions에 안 들어 있으면 RuntimeRobot이 "못 쓰는 기술"로 걸러서
    // AI가 공격을 아예 안 하고 대기·점프만 반복한다 — 에러 없이 조용히 그렇게 되니까 시작할 때 한 번 알려준다.
    // Start에서 하는 이유: Robot은 PlayerRobotBootstrap.Awake에서 만들어져서, Awake 시점엔 아직 없을 수 있다
    private void Start()
    {
        RuntimeRobot robot = executor != null ? executor.Robot : null;
        if (profile == null || robot == null) return;

        foreach (var entry in profile.Skills)
        {
            if (entry?.action == null) continue;
            if (!robot.IsAvailable(entry.action))
                Debug.LogWarning($"[AIInputSource] {name}: {entry.action.ActionName}이(가) ActionExecutor.allActions에 없음 — AI가 이 기술을 절대 쓰지 않음");
        }
    }

    // OnEnable/OnDisable 짝으로 구독하는 이유는 PlayerRobotBootstrap과 같다
    private void OnEnable() => CombatClock.Instance.OnCombatTick += Think;

    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= Think;
    }

    // 호출: CombatClock.OnCombatTick (1/60초마다). 쿨타임을 줄이고, 판단할 때가 되면 선택지 하나를 고른다
    private void Think()
    {
        for (int i = 0; i < skillCooldowns.Length; i++)
            if (skillCooldowns[i] > 0) skillCooldowns[i]--;
        if (jumpCooldown > 0) jumpCooldown--;

        if (profile == null || opponent == null || executor == null || executor.State == null) return;

        ActionState state = executor.State;
        if (state.IsDead || IsOpponentDead())
        {
            pendingAction = null;
            moveIntent = MoveIntent.Hold;
            return;
        }

        // 공격·경직 중엔 판단하지 않는다. 끝난 뒤 thinkInterval만큼 기다렸다가 판단 — 이게 "반응 속도"다
        if (!state.CanAcceptNewAction)
        {
            moveIntent = MoveIntent.Hold;
            thinkTimer = profile.ThinkIntervalFrames;
            return;
        }

        // 공중에선 착지할 때까지 아무것도 안 고른다 (공중 행동은 아직 설계 미정 — CLAUDE_1.md §15)
        if (mover != null && !mover.IsGrounded) return;

        // 접근하다가 목표 거리에 닿으면 템포를 기다리지 않고 바로 다음 판단으로 — 붙자마자 때려야 자연스럽다
        if (moveIntent == MoveIntent.Approach && Distance() <= profile.PreferredRange)
        {
            moveIntent = MoveIntent.Hold;
            thinkTimer = 0;
        }

        if (pendingAction != null) return; // 고른 기술을 ActionExecutor가 아직 안 가져감

        if (thinkTimer > 0)
        {
            thinkTimer--;
            return;
        }
        thinkTimer = profile.ThinkIntervalFrames;

        Decide();
    }

    // 지금 가능한 선택지를 모아 가중치 비율로 하나를 뽑는다
    private void Decide()
    {
        choices.Clear();
        float distance = Distance();
        bool targetAirborne = opponentMover != null && !opponentMover.IsGrounded;
        RuntimeRobot robot = executor.Robot;

        // 등을 돌리고 있으면 기술은 후보에서 뺀다 — 먼저 돌아서야 한다(GetMoveInput이 처리)
        if (IsFacingOpponent())
        {
            var skills = profile.Skills;
            for (int i = 0; i < skills.Length; i++)
            {
                var entry = skills[i];
                if (entry == null || entry.action == null || skillCooldowns[i] > 0) continue;
                if (distance < entry.minRange || distance > entry.maxRange) continue;
                if (entry.targetCondition == AITargetCondition.Grounded && targetAirborne) continue;
                if (entry.targetCondition == AITargetCondition.Airborne && !targetAirborne) continue;
                // 부위가 파괴돼 봉인된 기술은 고르지 않는다 (§5 "입력·UI·AI가 같은 목록 하나를 본다")
                if (robot != null && !robot.IsAvailable(entry.action)) continue;

                float weight = entry.weight * (targetAirborne ? entry.airborneTargetWeightMultiplier : 1f);
                AddChoice(ChoiceKind.Skill, i, weight);
            }
        }

        if (distance > profile.PreferredRange) AddChoice(ChoiceKind.Approach, -1, profile.ApproachWeight);
        AddChoice(ChoiceKind.Wait, -1, profile.WaitWeight);
        if (jumpCooldown <= 0) AddChoice(ChoiceKind.Jump, -1, profile.JumpWeight);

        if (!TryPick(out Choice picked)) return;

        switch (picked.kind)
        {
            case ChoiceKind.Skill:
                var entry = profile.Skills[picked.skillIndex];
                pendingAction = entry.action;
                skillCooldowns[picked.skillIndex] = entry.cooldownFrames;
                moveIntent = MoveIntent.Hold;
                break;
            case ChoiceKind.Approach:
                moveIntent = MoveIntent.Approach;
                break;
            case ChoiceKind.Wait:
                moveIntent = MoveIntent.Hold;
                break;
            case ChoiceKind.Jump:
                pendingJump = true;
                jumpCooldown = profile.JumpCooldownFrames;
                moveIntent = MoveIntent.Approach; // 상대 쪽으로 뛴다
                break;
        }
    }

    private void AddChoice(ChoiceKind kind, int skillIndex, float weight)
    {
        if (weight <= 0f) return;
        choices.Add(new Choice { kind = kind, skillIndex = skillIndex, weight = weight });
    }

    private bool TryPick(out Choice picked)
    {
        picked = default;
        float total = 0f;
        foreach (var c in choices) total += c.weight;
        if (total <= 0f) return false;

        double roll = rng.NextDouble() * total;
        foreach (var c in choices)
        {
            roll -= c.weight;
            if (roll < 0)
            {
                picked = c;
                return true;
            }
        }
        picked = choices[choices.Count - 1]; // 부동소수 오차로 끝까지 남은 경우
        return true;
    }

    // 호출: ActionExecutor.ExecuteTick (Idle일 때만). 틱에서 골라둔 기술을 한 번만 내준다
    public ActionData GetDesiredAction()
    {
        ActionData action = pendingAction;
        pendingAction = null;
        return action;
    }

    // 호출: RobotMover.Update / RobotView(걷기 그림). 접근 중이면 상대 쪽, 아니면 정지.
    // 단, 상대가 등 뒤에 있으면 그쪽으로 잠깐 민다 — RobotMover는 "이동 입력 방향"으로만 몸을 돌리기 때문에(§12 2026-10-02),
    // 돌아서려면 그 방향 입력이 한 번은 들어가야 한다. 돌아서는 순간 IsFacingOpponent가 참이 되어 바로 0으로 돌아온다
    public float GetMoveInput()
    {
        if (opponent == null || executor == null || executor.State == null) return 0f;
        if (executor.State.IsDead || IsOpponentDead()) return 0f;

        float dx = opponent.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return 0f; // 거의 겹친 상태 — 방향이 매 프레임 뒤집히는 걸 막는다
        float towardOpponent = dx > 0f ? 1f : -1f;

        if (moveIntent == MoveIntent.Approach)
            return Mathf.Abs(dx) > profile.PreferredRange ? towardOpponent : 0f;

        return IsFacingOpponent() ? 0f : towardOpponent;
    }

    // 호출: RobotMover.Update. 틱에서 정한 점프를 한 번만 내준다 (1회성 펄스 — IInputSource 계약)
    public bool GetJumpInput()
    {
        bool jump = pendingJump;
        pendingJump = false;
        return jump;
    }

    // 일반 적은 달리기를 쓰지 않는다 — 필요해지면 프로필에 성향을 추가하고 여기서 반환
    public bool GetDashInput() => false;

    private float Distance() => Mathf.Abs(opponent.position.x - transform.position.x);

    private bool IsFacingOpponent()
    {
        if (mover == null) return true; // 방향을 모르면 막지 않는다
        float dx = opponent.position.x - transform.position.x;
        return Mathf.Abs(dx) < 0.05f || (dx > 0f) == mover.FacingRight;
    }

    private bool IsOpponentDead() =>
        opponentExecutor != null && opponentExecutor.State != null && opponentExecutor.State.IsDead;
}
