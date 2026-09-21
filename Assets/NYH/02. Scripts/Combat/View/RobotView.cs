using UnityEngine;

/// <summary>
/// ActionState 이벤트를 구독해서 애니메이션을 재생하는 자리 (§1 View 계층, §10 애니메이션 연동).
/// 지금은 실제 도트 클립이 없어서 임시 클립(네모 애니메이션 등)으로 배선만 먼저 검증한다.
/// 나중에 진짜 클립이 나오면 Animator Controller의 스테이트 이름만
/// ActionData.AnimationClipName과 맞춰주면 되고, 이 코드는 수정할 필요가 없어야 한다 (§10 원칙).
///
/// 검증 대상: speed = 클립길이 / 데이터길이(TotalFrames/60f) 공식.
/// 상태 전환 자체는 여전히 ActionState(§3)가 프레임으로 정한다 — 여기서는 그림만 맞춰 재생할 뿐이다.
/// </summary>
public class RobotView : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string walkStateName = "Walk";

    // 액션이 끝난 뒤 Idle/Walk 중 뭘 재생할지 고르려면 "지금 이동 입력이 있는지"를 알아야 한다 —
    // ActionState는 그 정보를 안 갖고 있어서(순수하게 행동 진행 상태만 담당), 여기서 직접 IInputSource를 참조한다
    private IInputSource inputSource;
    private float actionStartTime; // 실제 재생 시간 측정용

    /// <summary>
    /// state: 재생 트리거를 받는 이벤트 소스. input: 액션 끝나고 Idle/Walk 중 뭘 틀지 판단용
    /// (걷기는 3구간 상태기계 대상이 아니라 이동 입력만 보면 되므로, RobotMover와 별개로 여기서 직접 확인)
    /// </summary>
    // 호출: PlayerRobotBootstrap.Awake. 받음: state(이벤트 구독용), input(액션이 끝난 뒤 Walk/Idle을 고르는 용도)
    public void Init(ActionState state, IInputSource input)
    {
        inputSource = input;
        // 여기서 두 이벤트를 구독하는 것만으로 애니메이션 재생이 자동으로 이루어진다 — ActionState 쪽은
        // "누가 구독하는지" 전혀 모른 채로 이벤트만 쏘고, 반응(애니메이션 재생)은 전부 구독자인 이쪽 책임이다(§13과 같은 패턴)
        state.OnActionBegin += PlayAction;
        state.OnActionEnd += OnActionEnd;
    }

    // 호출: ActionState.OnActionBegin 이벤트. 받음: 시작된 ActionData — AnimationClipName 스테이트를 재생하고 속도를 데이터 길이에 맞춘다
    private void PlayAction(ActionData action)
    {
        actionStartTime = Time.time;
        if (animator == null || string.IsNullOrEmpty(action.AnimationClipName))
        {
            // 조용히 그냥 넘어가지 않고 경고를 남기는 이유: 애니메이터 연결을 깜빡했거나
            // ActionData에 클립 이름을 안 채운 경우, 겉으로는 "캐릭터가 그냥 안 움직이네"로만
            // 보여서 원인을 찾기 어렵다. 로그가 있어야 바로 어느 액션이 문제인지 알 수 있다
            Debug.LogWarning($"[RobotView] {action?.ActionName} — animator 또는 AnimationClipName 미설정");
            return;
        }

        // 길이를 읽기 전에 speed를 1로 되돌리고 Update(0)으로 상태를 확정한다. 안 그러면 Play 직후 이전 상태/속도가 섞여 길이가 틀리게 읽힌다
        // (이전 액션에서 설정해둔 speed가 그대로 남아있는 채로 새 클립의 length를 재면, 실제 원본 클립
        //  길이가 아니라 "이전 speed가 적용된 것처럼 보이는" 잘못된 값을 읽게 될 수 있어서 매번 1로 초기화 후 잰다)
        animator.speed = 1f;
        animator.Play(action.AnimationClipName, 0, 0f);
        // Play()를 호출한 직후에는 아직 실제로 그 상태로 "전이"가 일어나지 않은 상태라, 바로 다음 줄에서
        // GetCurrentAnimatorStateInfo를 불러도 이전 상태 정보가 나올 수 있다. Update(0)을 강제로 한 번
        // 불러서(경과 시간 0이지만) 상태 전이를 즉시 확정시켜야 아래에서 정확한 클립 길이를 읽을 수 있다
        animator.Update(0f);

        float clipLength = animator.GetCurrentAnimatorStateInfo(0).length; // 원본 애니메이션 클립이 실제로 몇 초짜리인지
        float dataLength = action.TotalFrames / 60f; // 프레임표가 정한 "이 기술은 몇 초여야 하는지" (60fps 기준)

        // 데이터가 진실이고 그림이 따라온다(§3, §10 원칙)는 걸 실제로 구현하는 한 줄.
        // 클립 원본 길이가 데이터 길이보다 길면 빠르게(speed>1), 짧으면 느리게(speed<1) 재생해서
        // 항상 "이 기술은 정확히 dataLength초 만에 끝난다"가 보장되게 만든다.
        // dataLength가 0이면 나누기 오류가 나므로 그 경우만 예외로 1(원본 속도)을 쓴다
        animator.speed = dataLength > 0f ? clipLength / dataLength : 1f;

        Debug.Log($"[RobotView] {action.ActionName} 클립길이={clipLength:F3}s 데이터길이={dataLength:F3}s → speed={animator.speed:F2}");
    }

    /// <summary>
    /// FrameStepper(F3)가 정지 상태에서 애니메이터를 정확히 1틱 미는 용도.
    /// Animator는 Update()의 Time.deltaTime으로만 움직이는데 Time.timeScale=0이면 그게 0이라
    /// 자동으로는 전혀 진행하지 않는다 (§3) — 그래서 여기서 직접 시간을 넣어 밀어줘야
    /// F3를 누를 때마다 눈으로 한 프레임씩 확인할 수 있다.
    /// </summary>
    // 호출: FrameStepper.Update (F3, CombatClock.Tick 직후). 논리 프레임과 그림 프레임을 같은 타이밍에 맞춰 전진시킨다
    public void AdvanceOneTick()
    {
        if (animator == null) return;
        // Animator.Update(dt)는 "dt초가 지난 것처럼 애니메이터를 강제로 진행시켜라"는 수동 호출이다.
        // CombatClock.TICK(1/60초)을 그대로 넘겨서, 논리가 1틱 전진한 것과 정확히 같은 시간만큼만 그림도 전진시킨다.
        // 이때도 위 PlayAction에서 맞춰둔 animator.speed가 그대로 곱해져서 적용되므로 배속도 정확히 반영된다
        animator.Update(CombatClock.TICK);
    }

    /// <summary>
    /// Recovery 끝나고 Idle로 복귀하는 순간 — Animator Exit Time에 기대지 않고 여기서 강제로 되돌린다 (§3).
    /// 액션 중 바꿔둔 animator.speed도 1로 원복해야 Idle/Walk이 정상 속도로 재생된다.
    /// </summary>
    // 호출: ActionState.OnActionEnd 이벤트. 받음: 끝난 ActionData — 속도를 1로 되돌리고 Idle/Walk로 전환한다
    private void OnActionEnd(ActionData finishedAction)
    {
        // 데이터가 의도한 소요시간과 실제로 걸린 시간이 얼마나 차이나는지 매 액션마다 로그로 남겨서,
        // speed 계산식이 제대로 동작하는지(§10 검증 대상) 플레이만 해봐도 확인할 수 있게 한다
        Debug.Log($"[진단] {finishedAction.ActionName} 실제 소요시간={Time.time - actionStartTime:F3}s (데이터={finishedAction.TotalFrames / 60f:F3}s)");
        if (animator == null) return;

        // 액션 재생 중 데이터 길이에 맞추려고 바꿔둔 speed를 그대로 두면, Idle/Walk 애니메이션까지
        // 같이 빨라지거나 느려져 버린다 — 액션이 끝났으니 원래 속도(1배)로 반드시 되돌려야 한다
        animator.speed = 1f;

        // 지금 이동 입력이 있는지 물어봐서 Walk/Idle 중 자연스러운 쪽을 고른다.
        // (걷기는 ActionState의 3구간 대상이 아니라서, ActionState 이벤트만으로는 이 판단을 할 수 없다 — 그래서 input을 따로 받아둠)
        bool isMoving = Mathf.Abs(inputSource?.GetMoveInput() ?? 0f) > 0.01f;
        animator.Play(isMoving ? walkStateName : idleStateName, 0, 0f);
    }
}
