/// <summary>
/// 인스턴스 생성 시 효과에 전달되는 정보. 효과가 어느 로봇의 어느 부위 장비에서 왔는지 알려준다.
/// </summary>
public readonly struct EffectContext
{
    public readonly ActionExecutor Executor;
    public readonly BodyPart SourcePart;

    public EffectContext(ActionExecutor executor, BodyPart sourcePart)
    {
        Executor = executor;
        SourcePart = sourcePart;
    }

    public ActionState State => Executor.State;
    public string FighterId => Executor.FighterId;
}
