using UnityEngine;

// SYS/UI 리소스에서 뽑은 글자색
public static class RepairColors
{
    public static readonly Color Cyan = new Color32(53, 208, 222, 255);
    public static readonly Color Bad = new Color32(255, 77, 46, 255);
    public static readonly Color Good = new Color32(82, 224, 143, 255);
    public static readonly Color Warning = new Color32(255, 138, 30, 255);
    public static readonly Color Bone = new Color32(238, 237, 232, 255);
    public static readonly Color Neutral = new Color32(180, 185, 192, 255);
    public static readonly Color Ink = new Color32(26, 27, 29, 255);

    public static Color State(RepairPartState state)
    {
        switch (state)
        {
            case RepairPartState.Normal:
                return Good;
            case RepairPartState.Damaged:
                return Warning;
            case RepairPartState.Emergency or RepairPartState.Broken:
                return Bad;
        }
        return Neutral;
    }
}
