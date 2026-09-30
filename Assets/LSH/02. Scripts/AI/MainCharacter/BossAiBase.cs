using System;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class BossAiBase : AllAiBase
{
    [Header("Npc 골드 데이터")]
    public MainCharacterGold data;

    //람다식으로 삽입.
    public override string NPCName => data != null ? data.mainCharacterName : "No Name";
    public override int Gold => data != null ? data.mainCharacterGold : 0;

    public override bool IsReady { get; set; } = false;

    public static event Func<string, int, UniTask> OnBossBigRaiseCutscene; //컷씬 이벤트

    [SerializeField] private int bigRaiseThreshold = 1000; // 큰 금액 인상 기준

    public override void ReadyForAction()
    {
        IsReady = false;
    }

    //==================================판단===================================
    public override bool RaiseThink(int actionPrise, int actionRealPrise)
    { //현재는 단순히 골드보유로만
        if (IsReady || data == null) return false;

        int NextPrice = actionPrise + 100;

        // GlobalGold 매니저를 통해 안전하게 잔고 검사
        if (!GlobalGold.Instance.CanUseGold(NPCName, NextPrice))
        {
            IsReady = true; // 예산 부족으로 포기
            return false;
        }
        return true;
    }

    public virtual void GetAuction(int finalPrise) //낙찰
    {
        if (data != null)
        {
            data.mainCharacterGold -= finalPrise;
            Debug.Log($"{NPCName} : {finalPrise} 골드로 낙찰 받음. 남은 골드 : {data.mainCharacterGold}");
        }
    }
    public bool IsBigRaiseThink(int currentPrice, int baseCost, int raiseAmount) //큰레이즈 조건 판단
    {
        return raiseAmount >= bigRaiseThreshold || currentPrice >= (int)(baseCost * 1.5f);
    }
    public async UniTask TriggerCutscene(string bossName, int amount)
    {
       if (OnBossBigRaiseCutscene != null)
       {
           await OnBossBigRaiseCutscene.Invoke(bossName, amount);
       }
    }
}