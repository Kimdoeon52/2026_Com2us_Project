// 탐지 1틱 결과. 연출·사운드·상호작용이 같은 값을 공유한다
public struct ScrapDetectionState
{
    public ScrapNode Node;
    public float Distance;
    public float Intensity01;
    public bool InInteractRange;

    public bool HasTarget => Node != null;
}
