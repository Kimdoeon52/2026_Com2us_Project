using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
/*
보스1: 한번에 크게 넣는 능력. 2회 (확률형)
보스2: 다른 한명을 강제로 포기하게 만드는 능력. 1회 (확률형)
보스3: 경매시간을 반으로 줄이는 능력 1회 (확률형)
보스4: 경매 아이템이 안보이는 능력 1회 (시작 확률형)
보스5: 경매 아이템을 교체하는 능력 1회 (확률형)
*/
public class BossAiBase : AllAiBase
{
    [Header("Npc 골드 데이터")]
    public MainCharacterGold data;

    //람다식으로 삽입.
    public override string NPCName => data != null ? data.mainCharacterName : "No Name";
    public override int Gold => data != null ? data.mainCharacterGold : 0;
    public override int ThinkDelay => UnityEngine.Random.Range(4000, 8000); //보스는 생각 하는 시간 짧게
    public override int NPCID => data != null ? data.mainCharacterID : -1;
    public override PartGrade[] Parts => data != null ? data.parts : null;
    //public override bool IsGiveUp { get; set; } = false;

    protected virtual int SkillCount { get; set; } = 0; //스킬 사용 횟수
    protected virtual float SkillChance { get; set; } = 50f; //스킬 사용 확률
    public static event Func<string, string, Sprite, Sprite, Color, UniTask> OnBossBigRaiseCutscene; //컷씬 이벤트

    [Header("컷씬 배너 이미지")]
    [SerializeField] private Sprite cutsceneBannerSprite; //이 보스 전용 컷씬 배너 (비우면 기본 배너 유지)
    [SerializeField] private Color cutsceneBGcolor; //컷씬 백그라운드 색상
    [SerializeField] private Sprite cutsceneCharacter; //배너 캐릭터 모습

    //================================기본 세팅=================================
    public override void ReadyForAction()
    {
        IsGiveUp = false;
        RaiseGold = 10;
    }

    //==================================판단===================================
    public override bool RaiseThink(int actionPrise, int actionRealPrise, PartGrade stuffGrade)
    { // 필요 여부/원가 배수 한도는 NormalAuctionMode에서 공통 판단(PassCommonRaiseRule) 후 호출됨
        if (IsGiveUp || data == null) return false;

        int NextPrice = actionPrise + RaiseGold;

        // GlobalGold 매니저를 통해 안전하게 잔고 검사
        if (!GlobalGold.Instance.CanUseGold(NPCID, NextPrice))
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
        if (!GlobalGold.Instance.CanUseGold(NPCID, currentPrice) || SkillCount < 1) //체크한번 해주고
        {
            return false;
        }
        if (!CanUseSkill())
        {
            return false;
        }
        SkillCount--; //스킬 사용 횟수 차감
        return true;
    }
    public virtual async UniTask TriggerCutscene(string bossName, string skill)
    {
       if (OnBossBigRaiseCutscene != null)
       {
           await OnBossBigRaiseCutscene.Invoke(bossName, skill, cutsceneBannerSprite, cutsceneCharacter, cutsceneBGcolor);
       }
    }
    public virtual void UseBigSkill(Auction manager)
    {
        // 여기에 스킬 작성.
        Debug.LogWarning($"나는 {data.mainCharacterName}!!!!!!!");
    }

    //============================= 큰 레이즈가 생기면 얼마를 올릴 것인지, 보스 맨트 ==========================

    public virtual string BigRaiseSkillName() //큰 레이즈가 생길때 보스 맨트
    {
        return "고작 이정도야? 크게 걸어보자고.";
    }

    //=========================보스 스킬 확률 및 횟수 설정 함수===================
    protected virtual void SetSkillChance(float chance, int count)
    {
        SkillChance = chance;
        SkillCount = count;
    }
    protected virtual bool CanUseSkill()
    {
         bool canUseSkill = UnityEngine.Random.value * 100f < SkillChance; //SkillChance는 0~100 기준
         return canUseSkill && SkillCount > 0;
    }
}