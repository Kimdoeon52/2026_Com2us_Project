// 크래프팅 쪽 부품 소모 경계
public interface IComponentSink
{
    int Get(string componentId);

    // 모자라면 아무것도 소모하지 않고 false
    bool TryConsume(string componentId, int amount);
}
