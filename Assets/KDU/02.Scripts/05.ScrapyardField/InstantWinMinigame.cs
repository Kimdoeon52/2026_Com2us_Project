using System;
using UnityEngine;

// 실제 미니게임 없이 흐름만 확인할 때 쓰는 자리표시자
public class InstantWinMinigame : MonoBehaviour, IScrapMinigame
{
    public void Begin(Action<ScrapMinigameResult> onComplete)
    {
        onComplete?.Invoke(ScrapMinigameResult.Win(1f));
    }
}
