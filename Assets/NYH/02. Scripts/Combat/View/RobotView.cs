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

    public void Init(ActionState state)
    {
        state.OnActionBegin += PlayAction;
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
}
