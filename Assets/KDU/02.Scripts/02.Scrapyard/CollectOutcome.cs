// 수집 1회 결과
public struct CollectOutcome
{
    public bool IsMinigame;
    public MinigameRewardTable Table;
    public PartGrade Grade;
    public int Amount;

    public static CollectOutcome Instant(PartGrade grade, int amount)
    {
        return new CollectOutcome { IsMinigame = false, Grade = grade, Amount = amount };
    }

    public static CollectOutcome Minigame(MinigameRewardTable table)
    {
        return new CollectOutcome { IsMinigame = true, Table = table };
    }
}
