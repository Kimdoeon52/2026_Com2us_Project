// LSH GlobalGold로 플레이어 골드를 지급한다. 골드 쪽 클래스를 아는 곳은 여기뿐이다
public static class GlobalGoldWallet
{
    public static bool IsAvailable => GlobalGold.Instance != null && GlobalGold.Instance.mainCharacterdata != null && GlobalGold.Instance.mainCharacterdata.Count > 0;

    // 0번이 플레이어다
    public static bool AddPlayerGold(int amount)
    {
        if (!IsAvailable)
            return false;

        GlobalGold gold = GlobalGold.Instance;
        gold.GetGold(gold.mainCharacterdata[0].mainCharacterID, amount);
        gold.SaveGame();
        return true;
    }
}
