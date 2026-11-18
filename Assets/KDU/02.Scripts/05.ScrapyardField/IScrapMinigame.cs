using System;

// 미니게임 경계. 성패와 성과도를 돌려주고, 보상 부품을 직접 정한 경우만 그 Id를 담는다
public interface IScrapMinigame
{
    // 끝나면 결과를 콜백으로 전달한다. 수십 초가 걸려도 된다
    void Begin(Action<ScrapMinigameResult> onComplete);
}
