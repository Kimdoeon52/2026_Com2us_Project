using UnityEngine;
/// <summary>
/// 경기 시작 직전 NPC의 베팅 판단용 인터페이스, 그리고 성향별 클래스 혼합 코드
/// </summary>
public interface INpcBettingLogic
{
    // 플레이어의 제시액 수락 여부 판정
    bool ThinkPlayerBet(int playerBet, int npcWealth, int basePrice);

    // 수락 거절 시, NPC의 역제안 금액
    int NpcCounterBet(int playerBet, int npcWealth, int basePrice);
}

// 공격적인 NPC
public class AggressiveNpcLogic : INpcBettingLogic
{
    public bool ThinkPlayerBet(int playerBet, int npcWealth, int basePrice)
    {
        float acceptThreshold = npcWealth * 0.8f;
        return playerBet >= acceptThreshold;
    }

    public int NpcCounterBet(int playerBet, int npcWealth, int basePrice)
    {
        float multiplier = Random.Range(1.10f, 1.25f); // +10~25% 상향
        return Mathf.Min(Mathf.RoundToInt(playerBet * multiplier), npcWealth);
    }
}

// 신중한 NPC
public class CautiousNpcLogic : INpcBettingLogic
{
    public bool ThinkPlayerBet(int playerBet, int npcWealth, int basePrice)
    {
        float acceptThreshold = basePrice * 1.1f;
        return playerBet <= acceptThreshold;
    }

    public int NpcCounterBet(int playerBet, int npcWealth, int basePrice)
    {
        float multiplier = Random.Range(0.75f, 0.95f); // -5~25% 하향
        int counterOffer = Mathf.RoundToInt(playerBet * multiplier);
        int minBetLimit = Mathf.RoundToInt(basePrice * 0.7f); // 최소 기준가 보장

        return Mathf.Max(counterOffer, minBetLimit);
    }
}

// 평범한 NPC
public class NormalNpcLogic : INpcBettingLogic
{
    public bool ThinkPlayerBet(int playerBet, int npcWealth, int basePrice)
    {
        int minAccept = Mathf.RoundToInt(basePrice * 0.8f);
        int maxAccept = Mathf.RoundToInt(basePrice * 1.5f);
        return playerBet >= minAccept && playerBet <= maxAccept && playerBet <= npcWealth;
    }

    public int NpcCounterBet(int playerBet, int npcWealth, int basePrice)
    {
        float multiplier = Random.Range(0.95f, 1.10f);
        int counterOffer = Mathf.RoundToInt(playerBet * multiplier);
        int minBetLimit = Mathf.RoundToInt(basePrice * 0.7f);

        return Mathf.Clamp(counterOffer, minBetLimit, npcWealth);
    }
}
