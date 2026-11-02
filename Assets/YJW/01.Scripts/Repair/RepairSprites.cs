using UnityEngine;

// 수리/복구 화면에서 상태에 따라 바꿔 끼우는 이미지 모음
public class RepairSprites : Singleton<RepairSprites>
{
    [Header("상태 아이콘")]
    [SerializeField] private Sprite _statusCool;
    [SerializeField] private Sprite _statusWarm;
    [SerializeField] private Sprite _statusHot;

    [Header("내구도 칸")]
    [SerializeField] private Sprite _cellGood;
    [SerializeField] private Sprite _cellWarning;
    [SerializeField] private Sprite _cellBad;
    public Sprite cellEmpty;

    [Header("뱃지")]
    public Sprite badgeNeutral;
    public Sprite badgeCyan;
    public Sprite badgeOrange;
    public Sprite badgeYellow;
    public Sprite badgeRed;
    public Sprite badgeGreen;

    [Header("선택 표시")]
    public Sprite rowNormal;
    public Sprite rowSelected;
    public Sprite slotNormal;
    public Sprite slotSelected;

    public Sprite StatusIcon(RepairPartState state)
    {
        switch (state)
        {
            case RepairPartState.Normal:
                return _statusCool;
            case RepairPartState.Damaged:
                return _statusWarm;
        }
        return _statusHot;
    }

    public Sprite Cell(RepairPartState state)
    {
        switch (state)
        {
            case RepairPartState.Normal:
                return _cellGood;
            case RepairPartState.Damaged:
                return _cellWarning;
        }
        return _cellBad;
    }
}
