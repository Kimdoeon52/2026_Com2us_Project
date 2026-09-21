using UnityEngine;

// 미니게임 1회 결과. 보상은 호출부가 정하므로 여기엔 없다
public struct ScrapMinigameResult
{
    public bool Success;

    // 보상 보정용. 아직 쓰는 곳이 없어도 채워서 돌려준다
    public float Performance01;

    public static ScrapMinigameResult Win(float performance01)
    {
        return new ScrapMinigameResult { Success = true, Performance01 = Mathf.Clamp01(performance01) };
    }

    public static ScrapMinigameResult Lose(float performance01)
    {
        return new ScrapMinigameResult { Success = false, Performance01 = Mathf.Clamp01(performance01) };
    }
}
