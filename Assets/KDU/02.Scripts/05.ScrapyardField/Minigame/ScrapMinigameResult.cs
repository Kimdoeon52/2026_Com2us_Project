using UnityEngine;

// 미니게임 1회 결과. 보상은 보통 호출부가 정한다
public struct ScrapMinigameResult
{
    public bool Success;

    // 보상 보정용. 아직 쓰는 곳이 없어도 채워서 돌려준다
    public float Performance01;

    // 미니게임이 보상 부품을 직접 정했을 때만 채운다. 비어 있으면 보상 테이블로 뽑는다
    public string RewardId;

    public static ScrapMinigameResult Win(float performance01, string rewardId = null)
    {
        return new ScrapMinigameResult { Success = true, Performance01 = Mathf.Clamp01(performance01), RewardId = rewardId };
    }

    public static ScrapMinigameResult Lose(float performance01)
    {
        return new ScrapMinigameResult { Success = false, Performance01 = Mathf.Clamp01(performance01) };
    }
}
