// 크래프팅 쪽 부품 소모 경계
public interface IComponentSink
{
    int Get(PartGrade grade);

    // 모자라면 아무것도 소모하지 않고 false
    bool TryConsume(PartGrade grade, int amount);
}
