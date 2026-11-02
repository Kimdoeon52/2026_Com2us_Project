using UnityEngine;

public enum RepairPartState
{
    Empty,
    Normal,
    Damaged,
    Emergency,
    Broken
}

// 기획서의 수리/복구 공식 (수치는 임시)
public static class RepairRules
{
    public const int RepairHours = 1;
    public const int RestoreHours = 4;

    public static RepairPartState GetState(PartsDefinition part, int current)
    {
        if (part == null) return RepairPartState.Empty;
        if (current <= 0) return RepairPartState.Broken;
        if (current >= part.Durability) return RepairPartState.Normal;
        return current == 1 ? RepairPartState.Emergency : RepairPartState.Damaged;
    }

    public static int BaseRepairCost(Slot slot)
    {
        switch (slot)
        {
            case Slot.Head:
                return 40;
            case Slot.LeftArm or Slot.RightArm:
                return 30;
        }
        return 25;
    }

    public static float GradeRate(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Rare:
                return 1.5f;
            case PartGrade.Epic:
                return 2.5f;
            case PartGrade.Legendary:
                return 4f;
            case PartGrade.Prototype:
                return 6f;
        }
        return 1f;
    }

    // 부위 기본 수리비 x 등급 배율 x 잃은 내구도 비율 (소수점은 반올림)
    public static int RepairCost(PartsDefinition part, int current)
    {
        if (part == null || current >= part.Durability) return 0;

        float lost = (part.Durability - current) / (float)part.Durability;
        return Mathf.FloorToInt(BaseRepairCost(part.Slot) * GradeRate(part.Grade) * lost + 0.5f);
    }

    public static bool CanRestore(PartGrade grade) => grade != PartGrade.Common;   // 일반 등급은 복구 불가

    public static int RestoreCost(PartsDefinition part)
    {
        int headCost = RestoreHeadCost(part.Grade);

        switch (part.Slot)
        {
            case Slot.Head:
                return headCost;
            case Slot.LeftArm or Slot.RightArm:
                return headCost * 4 / 5;
        }
        return headCost * 2 / 3;
    }

    private static int RestoreHeadCost(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Rare:
                return 150;
            case PartGrade.Epic:
                return 300;
            case PartGrade.Legendary:
                return 600;
            case PartGrade.Prototype:
                return 900;
        }
        return 0;
    }

    // 에픽, 전설만 같은 등급 부품 1개가 더 든다
    public static int RestoreMaterial(PartGrade grade) => grade == PartGrade.Epic || grade == PartGrade.Legendary ? 1 : 0;

    public static int CoreRestoreCost(int level) => 100 + level * 50;

    public static string GradeName(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Rare:
                return "레어";
            case PartGrade.Epic:
                return "에픽";
            case PartGrade.Legendary:
                return "전설";
            case PartGrade.Prototype:
                return "프로토타입";
        }
        return "일반";
    }

    public static string SlotName(Slot slot)
    {
        switch (slot)
        {
            case Slot.Head:
                return "머리";
            case Slot.LeftArm:
                return "왼팔";
            case Slot.RightArm:
                return "오른팔";
            case Slot.LeftLeg:
                return "왼다리";
            case Slot.RightLeg:
                return "오른다리";
        }
        return "코어";
    }

    public static string SlotKey(Slot slot)
    {
        switch (slot)
        {
            case Slot.Head:
                return "패시브";
            case Slot.LeftArm:
                return "Q";
            case Slot.RightArm:
                return "W";
            case Slot.LeftLeg:
                return "E";
            case Slot.RightLeg:
                return "R";
        }
        return "";
    }

    public static string StateName(RepairPartState state)
    {
        switch (state)
        {
            case RepairPartState.Normal:
                return "정상";
            case RepairPartState.Damaged:
                return "손상";
            case RepairPartState.Emergency:
                return "응급 복구";
            case RepairPartState.Broken:
                return "파손";
        }
        return "빈 슬롯";
    }
}
