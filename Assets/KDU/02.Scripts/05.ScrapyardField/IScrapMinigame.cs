using System;

// 미니게임 경계. 미니게임은 보상을 모르고 성패와 성과도만 돌려준다
public interface IScrapMinigame
{
    // 끝나면 결과를 콜백으로 전달한다. 수십 초가 걸려도 된다
    void Begin(Action<ScrapMinigameResult> onComplete);
}
