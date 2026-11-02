using UnityEngine;

// 기획서의 수리·복구 공식. 수치는 임시
public static class RepairRules
{
    public const int RepairHours = 1;
    public const int RestoreHours = 4;

    public static int BaseRepairCost(Slot slot)
    {
        switch (slot)
        {
            case Slot.Head: return 40;
            case Slot.LeftArm:
            case Slot.RightArm: return 30;
            default: return 25;
        }
    }

    public static float GradeRate(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Rare: return 1.5f;
            case PartGrade.Epic: return 2.5f;
            case PartGrade.Legendary: return 4f;
            case PartGrade.Prototype: return 6f;
            default: return 1f;
        }
    }

    // 부위 기본 수리비 x 등급 배율 x 잃은 내구도 비율. 소수점은 반올림
    public static int RepairCost(PartsDefinition part, int current)
    {
        if (part == null || part.Durability <= 0 || current >= part.Durability)
            return 0;

        float lost = (part.Durability - current) / (float)part.Durability;
        return Mathf.FloorToInt(BaseRepairCost(part.Slot) * GradeRate(part.Grade) * lost + 0.5f);
    }

    // 일반 등급은 복구할 수 없다
    public static bool CanRestore(PartGrade grade) => grade != PartGrade.Common;

    public static int RestoreCost(PartsDefinition part)
    {
        int[] costs;
        switch (part.Grade)
        {
            case PartGrade.Rare: costs = new[] { 150, 120, 100 }; break;
            case PartGrade.Epic: costs = new[] { 300, 240, 200 }; break;
            case PartGrade.Legendary: costs = new[] { 600, 480, 400 }; break;
            case PartGrade.Prototype: costs = new[] { 900, 720, 600 }; break;
            default: return 0;
        }

        switch (part.Slot)
        {
            case Slot.Head: return costs[0];
            case Slot.LeftArm:
            case Slot.RightArm: return costs[1];
            default: return costs[2];
        }
    }

    // 에픽, 전설만 같은 등급 부품 1개가 더 든다
    public static int RestoreMaterial(PartGrade grade) => grade == PartGrade.Epic || grade == PartGrade.Legendary ? 1 : 0;

    // 100G + 코어 레벨 x 50G
    public static int CoreRestoreCost(int level) => 100 + level * 50;

    public static string GradeName(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Rare: return "레어";
            case PartGrade.Epic: return "에픽";
            case PartGrade.Legendary: return "전설";
            case PartGrade.Prototype: return "프로토타입";
            default: return "일반";
        }
    }

    public static string SlotName(Slot slot)
    {
        switch (slot)
        {
            case Slot.Head: return "머리";
            case Slot.LeftArm: return "왼팔";
            case Slot.RightArm: return "오른팔";
            case Slot.LeftLeg: return "왼다리";
            case Slot.RightLeg: return "오른다리";
            default: return "코어";
        }
    }

    public static string SlotKey(Slot slot)
    {
        switch (slot)
        {
            case Slot.Head: return "패시브";
            case Slot.LeftArm: return "Q";
            case Slot.RightArm: return "W";
            case Slot.LeftLeg: return "E";
            case Slot.RightLeg: return "R";
            default: return "";
        }
    }
}
