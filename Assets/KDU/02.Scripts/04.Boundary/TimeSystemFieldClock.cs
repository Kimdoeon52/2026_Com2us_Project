// LJS 시간 시스템 어댑터. 이 파일 밖에서는 TimeSystemManager를 직접 참조하지 않는다
public class TimeSystemFieldClock : IFieldClock
{
    public static bool IsAvailable => TimeSystemManager.Instance != null;

    public int Month => TimeSystemManager.Instance != null ? TimeSystemManager.Instance.month : 0;
    public int Day => TimeSystemManager.Instance != null ? TimeSystemManager.Instance.day : 0;
    public int Hour => TimeSystemManager.Instance != null ? TimeSystemManager.Instance.hour : 0;
}
