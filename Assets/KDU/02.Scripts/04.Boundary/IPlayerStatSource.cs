using System.Collections.Generic;

// 표시용 스탯 한 줄
public struct StatLine
{
    public string Label;
    public string Value;

    public StatLine(string label, string value)
    {
        Label = label;
        Value = value;
    }
}

// 플레이어 스탯 계산 경계
public interface IPlayerStatSource
{
    // 장착 파츠 ID로 계산한 표시용 스탯
    IReadOnlyList<StatLine> GetStats(IReadOnlyList<string> equippedPartIds);
}
