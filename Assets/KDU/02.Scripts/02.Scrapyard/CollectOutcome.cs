// 수집 1회 결과
public struct CollectOutcome
{
    public bool IsMinigame;
    public MinigameRewardTable Table;
    public ComponentDefinition Component;
    public int Amount;

    public PartGrade Grade => Component != null ? Component.Grade : PartGrade.Common;

    public static CollectOutcome Instant(ComponentDefinition component, int amount)
    {
        return new CollectOutcome { IsMinigame = false, Component = component, Amount = amount };
    }

    public static CollectOutcome Minigame(MinigameRewardTable table)
    {
        return new CollectOutcome { IsMinigame = true, Table = table };
    }
}
