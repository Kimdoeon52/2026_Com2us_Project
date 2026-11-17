/// <summary>
/// 효과의 런타임 인스턴스 (순수 C# 클래스, 로봇당 효과당 1개). 필요한 것만 override한다.
/// </summary>
public abstract class EffectInstance
{
    /// <summary>
    /// 행동이 시작될 때(ActionState.Begin) 호출된다. 그 행동에 해당하면 r에 보정값을 곱한다.
    /// 여기서 받는 action은 대체가 끝난 뒤의 최종 행동이다.
    /// </summary>
    public virtual void Contribute(ActionData action, ResolvedModifiers r) { }

    /// <summary>
    /// 입력원이 요청한 행동을 다른 행동으로 바꾸고 싶으면 대체할 ActionData를 반환하고, 아니면 null.
    /// 한 번만 적용되며 대체 결과가 다시 대체되지는 않는다 (순환 방지).
    /// </summary>
    public virtual ActionData Replace(ActionData requested) => null;

    /// <summary>같은 행동을 여러 효과가 대체하려 할 때 높은 값이 이긴다. 같으면 나중에 등록된 쪽이 이긴다</summary>
    public virtual int ReplacePriority => 0;
}
