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

    private IInputSource inputSource;

    /// <summary>
    /// state: 재생 트리거를 받는 이벤트 소스. input: 액션 끝나고 Idle/Walk 중 뭘 틀지 판단용
    /// (걷기는 3구간 상태기계 대상이 아니라 이동 입력만 보면 되므로, RobotMover와 별개로 여기서 직접 확인)
    /// </summary>
    public void Init(ActionState state, IInputSource input)
    {
        inputSource = input;
        state.OnActionBegin += PlayAction;
        state.OnActionEnd += OnActionEnd;
    }

    private void PlayAction(ActionData action)
    {
        if (animator == null || string.IsNullOrEmpty(action.AnimationClipName))
        {
            Debug.LogWarning($"[RobotView] {action?.ActionName} — animator 또는 AnimationClipName 미설정");
            return;
        }

        animator.Play(action.AnimationClipName, 0, 0f);

        float clipLength = animator.GetCurrentAnimatorStateInfo(0).length;
        float dataLength = action.TotalFrames / 60f;

        animator.speed = dataLength > 0f ? clipLength / dataLength : 1f;

        Debug.Log($"[RobotView] {action.ActionName} 클립길이={clipLength:F3}s 데이터길이={dataLength:F3}s → speed={animator.speed:F2}");
    }

    /// <summary>
    /// Recovery 끝나고 Idle로 복귀하는 순간 — Animator Exit Time에 기대지 않고 여기서 강제로 되돌린다 (§3).
    /// 액션 중 바꿔둔 animator.speed도 1로 원복해야 Idle/Walk이 정상 속도로 재생된다.
    /// </summary>
    private void OnActionEnd(ActionData finishedAction)
    {
        if (animator == null) return;

        animator.speed = 1f;

        bool isMoving = Mathf.Abs(inputSource?.GetMoveInput() ?? 0f) > 0.01f;
        animator.Play(isMoving ? walkStateName : idleStateName, 0, 0f);
    }
}
