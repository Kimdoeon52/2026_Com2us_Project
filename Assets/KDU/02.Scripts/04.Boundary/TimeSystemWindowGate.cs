// LJS 시간 상태로 창을 열어도 되는지 판단한다. 이 파일 밖에서는 TimeSystemManager를 직접 참조하지 않는다
public static class TimeSystemWindowGate
{
    // 시간이 흐르는 중이거나, 다른 창이 시간을 멈춘 상태면 연다. 전투 중에는 막는다
    public static bool CanOpen(bool otherWindowOpen)
    {
        TimeSystemManager clock = TimeSystemManager.Instance;
        if (clock == null)
            return true;

        if (clock.currentState == TimeSystemManager.TimeState.Running)
            return true;

        return otherWindowOpen && clock.currentState == TimeSystemManager.TimeState.UIOpen;
    }
}
