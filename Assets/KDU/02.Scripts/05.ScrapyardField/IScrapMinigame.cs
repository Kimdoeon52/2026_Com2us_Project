using System;

// 미니게임 경계. 실제 구현은 아직 없다
public interface IScrapMinigame
{
    // 끝나면 성공 여부를 콜백으로 돌려준다
    void Begin(MinigameRewardTable table, Action<bool> onResult);
}
