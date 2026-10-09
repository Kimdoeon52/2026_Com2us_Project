using UnityEngine;

public class Npc1 : NpcAiBase //후반형
{
    [Range(0.1f, 1f)]
    [Tooltip("자신이 골드 %까지 베팅에 사용할지.")]
    public float actionRate = 1f; // 후반형은 올인까지 배팅 시도
    public override void Start()
    {
        base.Start();
    }
    public override void ReadyForAction()
    {
        base.ReadyForAction();
        RaiseGold = 200;
    }
    public override bool RaiseThink(int actionPrice, int actionRealPrice)
    {
        // 10초 초과 시 관망 대사만 내보내고 지켜봄
        if (Auction.Instance.RemainingTime > 10f)
        {
            ShowThinkChatWithText("흐음... 아직은 때가 아니야.");
            return false;
        }

        // 10초 이하가 되었을 때의 예산 판단
        return base.RaiseThink(actionPrice, actionRealPrice);
    }
}
