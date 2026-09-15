// 시간 시스템 경계
public interface IGameClock
{
    // 현재 인게임 월
    int CurrentMonth { get; }

    // 시간 소모 요청. 모자라면 false
    bool TryConsume(float amount);
}
