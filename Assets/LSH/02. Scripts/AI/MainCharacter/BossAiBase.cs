using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Numerics;

public class BossAiBase : AllAiBase
{
    [Header("Npc 골드 데이터")]
    public MainCharacterGold data;

    //람다식으로 삽입.
    public override string NPCName => data != null ? data.mainCharacterName : "No Name";
    public override int Gold => data != null ? data.mainCharacterGold : 0;

    public override bool IsReady { get; set; } = false;

    public static event Func<string, string, UniTask> OnBossBigRaiseCutscene; //컷씬 이벤트

    [SerializeField] private int bigRaiseGold = 1000; //큰 레이즈가 생기면 얼마를 올릴 것인지.
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
    public virtual bool IsBigRaiseThink(int currentPrice, int baseCost) //큰레이즈 조건 판단
    {
        if (!GlobalGold.Instance.CanUseGold(NPCName, currentPrice + bigRaiseGold)) //체크한번 해주고
        {
            return false;
        }
        return currentPrice >= (int)(baseCost * 1.5f);
    }
    public virtual async UniTask TriggerCutscene(string bossName, string skill)
    {
       if (OnBossBigRaiseCutscene != null)
       {
           await OnBossBigRaiseCutscene.Invoke(bossName, skill);
       }
    }

    //============================= 큰 레이즈가 생기면 얼마를 올릴 것인지, 보스 맨트 ==========================

    public virtual int BigRaiseGold() //큰 레이즈가 생기면 얼마를 올릴 것인지.
    {
        return bigRaiseGold;
    }
    public virtual string BigRaiseSkillName() //큰 레이즈가 생길때 보스 맨트
    {
        return "고작 이정도야? 크게 걸어보자고.";
    }
}