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
    public override bool RaiseThink(int actionPrise, int actionRealPrise)
    {
        // 10초 초과일 때는 이번 턴만 입찰을 '스킵'하는 것이므로 IsReady를 true로 만들지 않고 false만 리턴
        if (Auction.Instance.RemainingTime > 10f)
        {
            // 아직 관망 중이라는 대사를 띄워주면 기획 의도에 완벽히 부합함!
            ShowThinkChatWithText("흐음... 아직은 때가 아니야.");
            return false;
        }

        // 10초 이하가 되면 정상적으로 예산 체크 후 입찰
        return base.RaiseThink(actionPrise, actionRealPrise);
    }
}
