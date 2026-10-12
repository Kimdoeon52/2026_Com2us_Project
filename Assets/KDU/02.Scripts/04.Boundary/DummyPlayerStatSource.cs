using System.Collections.Generic;

// 스탯 계산이 붙기 전 진행용 더미. 값은 비워둔다
public class DummyPlayerStatSource : IPlayerStatSource
{
    private static readonly string[] Labels = { "체력", "방어력", "공격력", "이동속도" };

    private readonly List<StatLine> _lines = new List<StatLine>();

    public IReadOnlyList<StatLine> GetStats(IReadOnlyList<string> equippedPartIds)
    {
        _lines.Clear();

        for (int i = 0; i < Labels.Length; i++)
        {
            _lines.Add(new StatLine(Labels[i], "-"));
        }

        _lines.Add(new StatLine("장착 파츠", equippedPartIds != null ? equippedPartIds.Count.ToString() : "0"));
        return _lines;
    }
}
