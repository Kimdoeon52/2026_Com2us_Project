using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcAiBase : AllAiBase
{
    [Header("Npc 골드 데이터")]
    public NpcGold data;
    //[Range(0.1f, 1f)]
    //[Tooltip("자신이 골드 %까지 베팅에 사용할지.")]
    //public float actionRate = 0.7f; // 초반형은 70%까지만 배팅 시도

    //람다식으로 삽입.
    public override string NPCName => data != null ? data.npcName : "No Name";
    public override int Gold => data != null ? data.npcGold : 0;
    public override int ThinkDelay => Random.Range(5000, 10000);

    public override bool IsReady { get; set; } = false;

    //================================기본 세팅=================================
    public override void ReadyForAction()
    {
        IsReady = false;
        RaiseGold = 100;
        ResetThinkBubble();
    }

    //==================================판단===================================
    public override bool RaiseThink(int actionPrise, int actionRealPrise)
    { //현재는 단순히 골드보유로만
        if (IsReady || data == null) return false;

        int NextPrice = actionPrise + RaiseGold;

        // GlobalGold 매니저를 통해 안전하게 잔고 검사
        if (!GlobalGold.Instance.CanUseGold(data.npcName, NextPrice))
        {
            IsReady = true; // 예산 부족으로 포기
            ShowThinkChatWithText("쳇 난 포기 하겠어.");
            return false;
        }
        return true;
    }

    public virtual void GetAuction(int finalPrise) //낙찰
    {
        if (data != null)
        {
            data.npcGold -= finalPrise;
            Debug.Log($"{NPCName} : {finalPrise} 골드로 낙찰 받음. 남은 골드 : {data.npcGold}");
        }
    }
   
}
