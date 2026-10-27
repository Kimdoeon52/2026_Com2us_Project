using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class BossAiBase : AllAiBase
{
    [Header("Npc 골드 데이터")]
    public MainCharacterGold data;

    //람다식으로 삽입.
    public override string NPCName => data != null ? data.mainCharacterName : "No Name";
    public override int Gold => data != null ? data.mainCharacterGold : 0;
    public override int ThinkDelay => UnityEngine.Random.Range(4000, 8000); //보스는 생각 하는 시간 짧게
    public override bool IsGiveUp { get; set; } = false;

    public static event Func<string, string, Sprite, Sprite, Color, UniTask> OnBossBigRaiseCutscene; //컷씬 이벤트

    [Header("컷씬 배너 이미지")]
    [SerializeField] private Sprite cutsceneBannerSprite; //이 보스 전용 컷씬 배너 (비우면 기본 배너 유지)
    [SerializeField] private Color cutsceneBGcolor; //컷씬 백그라운드 색상
    [SerializeField] private Sprite cutsceneCharacter; //배너 캐릭터 모습

    [SerializeField] public int bigRaiseGold = 1000; //큰 레이즈가 생기면 얼마를 올릴 것인지.

    [SerializeField] private int bigRaisetimes = 1; // 횟수
    //================================기본 세팅=================================
    public override void Start()
    {
        base.Start();
    }

    public override void ReadyForAction()
    {
        IsGiveUp = false;
        RaiseGold = 200;
    }

    //==================================판단===================================
    public override bool RaiseThink(int actionPrise, int actionRealPrise)
    { //현재는 단순히 골드보유로만
        if (IsGiveUp || data == null) return false;

        int NextPrice = actionPrise + RaiseGold;

        // GlobalGold 매니저를 통해 안전하게 잔고 검사
        if (!GlobalGold.Instance.CanUseGold(NPCName, NextPrice))
        {
            IsGiveUp = true; // 예산 부족으로 포기
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
        if (!GlobalGold.Instance.CanUseGold(NPCName, currentPrice + bigRaiseGold) || bigRaisetimes < 1) //체크한번 해주고
        {
            return false;
        }
        bigRaisetimes--; // 횟수 1회 감소
        return currentPrice >= (int)(baseCost * 1.5f);
    }
    public virtual async UniTask TriggerCutscene(string bossName, string skill)
    {
       if (OnBossBigRaiseCutscene != null)
       {
           await OnBossBigRaiseCutscene.Invoke(bossName, skill, cutsceneBannerSprite, cutsceneCharacter, cutsceneBGcolor);
       }
    }
    public virtual void UseBigSkill()
    {
        // 여기에 스킬 작성.
        Debug.LogWarning($"나는 {data.mainCharacterName}!!!!!!!");
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