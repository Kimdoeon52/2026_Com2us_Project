// 탐지 1틱 결과. 연출·사운드·상호작용이 같은 값을 공유한다
public struct ScrapDetectionState
{
    public ScrapNode Node;
    public float Distance;
    public float Intensity01;
    public bool InInteractRange;

    // 손 닿는 거리에 노드는 있지만 이번 달 총량이 바닥나 대상에서 빠졌다
    public bool DepletedNearby;

    public bool HasTarget => Node != null;
}
