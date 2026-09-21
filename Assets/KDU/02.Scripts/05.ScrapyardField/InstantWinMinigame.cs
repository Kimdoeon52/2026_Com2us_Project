using System;
using UnityEngine;

// 미니게임 구현 전 자리표시자. 즉시 성공 처리한다
public class InstantWinMinigame : MonoBehaviour, IScrapMinigame
{
    public void Begin(MinigameRewardTable table, Action<bool> onResult)
    {
        onResult?.Invoke(true);
    }
}
