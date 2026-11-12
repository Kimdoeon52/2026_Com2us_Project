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
    public override int NPCID => data != null ? data.npcID : -1;
    public override PartGrade[] Parts => data != null ? data.parts : null;

    //public override bool IsGiveUp { get; set; } = false;

    //================================기본 세팅=================================
    public override void ReadyForAction()
    {
        IsGiveUp = false;
        RaiseGold = 10;
        ResetThinkBubble();
    }

    //==================================판단===================================
    public override bool RaiseThink(int actionPrise, int actionRealPrise, PartGrade stuffGrade)
    { //현재는 단순히 골드보유로만
        if (IsGiveUp || data == null) return false;

        int NextPrice = actionPrise + RaiseGold;

        // 일반 NPC는 GlobalGold에 등록돼 있지 않으므로 자기 NpcGold 데이터의 예산으로 판단
        if (!CanUseGold(NextPrice))
        {
            IsGiveUp = true; // 예산 부족으로 포기
            return false;
        }
        return true;
    }

    // NPC 예산 검사 (NpcGold.npcGold 기준)
    protected bool CanUseGold(int price) => data != null && data.npcGold >= price;

    // 일반 NPC(조무레기)는 골드/장비가 저장되지 않음 (기획: 골드 상관X).
    // NpcGold는 ScriptableObject라 에디터에서 값을 깎으면 에셋 자체가 바뀌므로 차감하지 않고 기록만 남김
    public override void OnWinAuction(int finalPrice, PartGrade stuffGrade)
    {
        Debug.Log($"[낙찰] {NPCName} : {finalPrice}G (일반 NPC라 골드는 차감하지 않음)");
    }
    
}
