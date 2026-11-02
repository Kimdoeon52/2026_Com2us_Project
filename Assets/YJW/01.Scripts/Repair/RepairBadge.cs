using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RepairBadge : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TMP_Text _label;

    public void SetGrade(PartGrade grade)
    {
        RepairSprites sprites = RepairSprites.Instance;

        switch (grade)
        {
            case PartGrade.Rare:
                Set(sprites.badgeCyan, "레어", RepairColors.Cyan);
                break;
            case PartGrade.Epic:
                Set(sprites.badgeOrange, "에픽", RepairColors.Ink);
                break;
            case PartGrade.Legendary:
                Set(sprites.badgeYellow, "전설", RepairColors.Ink);
                break;
            case PartGrade.Prototype:
                Set(sprites.badgeRed, "프로토타입", RepairColors.Ink);
                break;
            default:
                Set(sprites.badgeNeutral, "일반", RepairColors.Ink);
                break;
        }
    }

    public void SetState(RepairPartState state)
    {
        RepairSprites sprites = RepairSprites.Instance;

        switch (state)
        {
            case RepairPartState.Normal:
                Set(sprites.badgeGreen, RepairRules.StateName(state), RepairColors.Ink);
                break;
            case RepairPartState.Damaged:
                Set(sprites.badgeOrange, RepairRules.StateName(state), RepairColors.Ink);
                break;
            default:
                Set(sprites.badgeRed, RepairRules.StateName(state), RepairColors.Ink);
                break;
        }
    }

    public void SetCore(bool unstable)
    {
        RepairSprites sprites = RepairSprites.Instance;

        if (unstable) Set(sprites.badgeRed, "불안정한 코어", RepairColors.Ink);
        else Set(sprites.badgeGreen, "정상 코어", RepairColors.Ink);
    }

    private void Set(Sprite sprite, string text, Color color)
    {
        _image.sprite = sprite;
        _label.text = text;
        _label.color = color;
    }
}
