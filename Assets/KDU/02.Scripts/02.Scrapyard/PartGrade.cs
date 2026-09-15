// 부품 등급. 개별 ID 없이 등급으로만 구분한다
public enum PartGrade
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3,
    Prototype = 4,
}

public static class PartGrades
{
    public const int Count = 5;

    public static readonly PartGrade[] All =
    {
        PartGrade.Common,
        PartGrade.Rare,
        PartGrade.Epic,
        PartGrade.Legendary,
        PartGrade.Prototype,
    };

    // 인스펙터·에디터 출력용 한글 라벨
    public static string ToLabel(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Common:
                return "일반";
            case PartGrade.Rare:
                return "레어";
            case PartGrade.Epic:
                return "에픽";
            case PartGrade.Legendary:
                return "전설";
            case PartGrade.Prototype:
                return "프로토타입";
            default:
                return grade.ToString();
        }
    }

    public static bool IsValid(PartGrade grade)
    {
        return (int)grade >= 0 && (int)grade < Count;
    }
}
