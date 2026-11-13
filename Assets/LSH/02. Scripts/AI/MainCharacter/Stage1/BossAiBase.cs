using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/*
 보스 공통 베이스
 - 스킬은 레이즈와 별개로 매 고민(ThinkDelay)마다 굴림: 발동 조건(WantsSkill) + 확률(SkillChance) + 횟수(SkillCount)
 - 스킬 사용 흐름(NormalAuctionMode): 스킬 대사 → 컷씬 → UseSkillAsync(효과) → 가격/최고입찰자가 바뀌었으면 입찰로 인정
 - 보스마다 대사(고민/레이즈/스킬/포기)와 스킬을 오버라이드해서 개성을 줌

 Stage1  1:큰손(거액 레이즈)  2:강제 퇴장  3:초읽기(시간 절반)  4:연막탄(정보 가리기)  5:눈물의 흥정(가격 깎고 선두)
 Stage2  6:경매세(플레이어 레이즈 비용 증가)  7:입찰 봉쇄(플레이어 레이즈 잠금)  8:공포 분위기(잡몹 대량 포기)
         9:연장전(시간 추가 + 레이즈)  10:바꿔치기(경매 물품 교체)
 Stage3  11:광란의 연타(3연속 레이즈)  12:시간 동결(입찰해도 시간 안 늘어남)  13:막판 저격(종료 직전 탈취)
         14:가짜 호가(가격표 교란)  15:절대 권력(크게 올리고 다른 AI 전원 퇴장)
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

    protected virtual int SkillCount { get; set; } = 0; //남은 스킬 사용 횟수
    protected virtual float SkillChance { get; set; } = 50f; //스킬 사용 확률 (0~100)
    public bool HasSkillLeft => SkillCount > 0;
    public static event Func<string, string, Sprite, Sprite, Color, CutsceneStyle, UniTask> OnBossBigRaiseCutscene; //컷씬 이벤트

    [Header("컷씬 배너 이미지")]
    [SerializeField] private Sprite cutsceneBannerSprite; //이 보스 전용 컷씬 배너 (비우면 기본 배너 유지)
    [SerializeField] private Color cutsceneBGcolor; //컷씬 백그라운드 색상
    [SerializeField] private Sprite cutsceneCharacter; //배너 캐릭터 모습

    //================================보스 개성 (대사 & 스킬 이름)=================================
    public virtual string SkillName => "큰손"; // 컷씬 배너에 나오는 스킬 이름
    public virtual string SkillLine => "고작 이정도야? 크게 걸어보자고."; // 스킬 쓸 때 말풍선
    protected virtual string[] ThinkLines => new[] { "흠...", "어디 보자..." }; // 고민 중 말풍선
    protected virtual string[] RaiseLines => new[] { "{0}G!" }; // 레이즈 말풍선 ({0} = 가격)
    protected virtual string GiveUpLine => "쳇 난 포기 하겠어."; // 포기 말풍선
    protected virtual string[] OutbidLines => new[] { "뭐야, 앞질렀다고?" }; // 내가 선두였는데 누가 앞질렀을 때
    protected virtual string[] WinLines => new[] { "내 거다." }; // 낙찰받았을 때
    public virtual CutsceneStyle CutsceneStyle => CutsceneStyle.SlideLeft; // 컷씬 배너 등장 방식
    protected virtual Color SkillColor => new Color(1f, 0.85f, 0.2f); // 스킬 연출 대표 색 (화면 플래시 등)

    protected static string Pick(string[] lines) => lines == null || lines.Length == 0 ? string.Empty : lines[UnityEngine.Random.Range(0, lines.Length)];
    public string GetOutbidLine() => Pick(OutbidLines);
    public override string GetWinLine() => Pick(WinLines);

    //================================연출 헬퍼=================================
    // 내가 앉아 있는 좌석의 초상화 (AI는 좌석 '랭커자리'의 자식으로 생성됨)
    protected SpriteRenderer SeatPortrait => transform.parent != null ? transform.parent.GetComponent<SpriteRenderer>() : null;
    protected static SpriteRenderer PortraitOf(AllAiBase ai) => ai != null && ai.transform.parent != null ? ai.transform.parent.GetComponent<SpriteRenderer>() : null;

    // 스킬 발동 직후(컷씬 끝나고 효과 직전) 공통 기합 연출: 내 초상화가 들썩 + 내 색으로 물듦
    public virtual void PlaySkillIntroFx()
    {
        SpriteRenderer me = SeatPortrait;
        if (me == null) return;
        SkillFx.Punch(me.transform, 0.2f);
        SkillFx.Tint(me, SkillColor, 0.8f);
    }

    //================================기본 세팅=================================
    public override void Start()
    {
        base.Start();
        SetupSkill();
    }

    protected virtual void SetupSkill() // 보스별 확률/횟수/레이즈 단위 설정
    {
        SetSkillChance(30, 1);
    }

    public override void ReadyForAction()
    {
        IsGiveUp = false;
        RaiseGold = 10;
    }

    //================================대사=================================
    public override string GetRandomThinkChat()
    {
        // 프리팹에 적어둔 대사 + 코드 기본 대사 중 랜덤
        List<string> lines = new List<string>(ThinkLines);
        if (thinkChatList != null)
        {
            foreach (var chat in thinkChatList)
                if (!string.IsNullOrWhiteSpace(chat.thinkChat)) lines.Add(chat.thinkChat);
        }
        return lines.Count > 0 ? lines[UnityEngine.Random.Range(0, lines.Count)] : string.Empty;
    }

    public override string GetRaiseLine(int cost)
    {
        string[] lines = RaiseLines;
        if (lines == null || lines.Length == 0) return base.GetRaiseLine(cost);
        return string.Format(lines[UnityEngine.Random.Range(0, lines.Length)], cost);
    }

    protected override void HandleGiveUp()
    {
        ShowThinkChatWithText(GiveUpLine);
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

    // 낙찰 정산: GlobalGold로 골드 차감 + 장비 등급 교체 후 저장
    public override void OnWinAuction(int finalPrice, PartGrade stuffGrade)
    {
        if (GlobalGold.Instance == null) return;
        if (!GlobalGold.Instance.UseGold(NPCID, finalPrice))
            Debug.LogWarning($"[낙찰] {NPCName} 골드 부족으로 차감 실패 ({finalPrice}G)");
        GlobalGold.Instance.ChangePartsGrade(NPCID, stuffGrade);
        GlobalGold.Instance.SaveGame();
    }

    //==================================스킬===================================
    // 이번 고민에서 스킬을 쓸지 결정 (조건 → 확률 → 횟수 차감)
    public bool TryRollSkill(Auction manager)
    {
        if (IsGiveUp || data == null || SkillCount <= 0) return false;
        if (!WantsSkill(manager)) return false;
        if (UnityEngine.Random.value * 100f >= SkillChance) return false;
        SkillCount--;
        return true;
    }

    // 보스별 스킬 발동 조건 (성격). 기본: 아무 때나
    protected virtual bool WantsSkill(Auction manager) => true;

    // 스킬 효과. 가격을 올리고 선두가 되려면 manager.CurrentCost / manager.WinnerID를 바꾸면 됨 (입찰 처리는 NormalAuctionMode가 함)
    public virtual UniTask UseSkillAsync(Auction manager)
    {
        return UniTask.CompletedTask;
    }

    // 스킬로 입찰했을 때 남은 시간을 10초로 되돌릴지 (막판 저격처럼 시간 안 늘리는 스킬은 false)
    public virtual bool SkillRefreshesTimer => true;

    public virtual async UniTask TriggerCutscene(string bossName, string skill)
    {
        if (OnBossBigRaiseCutscene != null)
        {
            await OnBossBigRaiseCutscene.Invoke(bossName, skill, cutsceneBannerSprite, cutsceneCharacter, cutsceneBGcolor, CutsceneStyle);
        }
    }

    //==================================스킬 헬퍼===================================
    protected bool IsLeading(Auction manager) => manager.WinnerID == NPCID;
    protected bool IsPlayerLeading(Auction manager) => manager.IfPlayerWin && manager.WinnerID == manager.PlayerID;

    // 이 가격에 입찰해도 되는지 (필요한 물건 + 원가 배수 한도 + 보유 골드)
    protected bool CanAffordBid(Auction manager, int price)
    {
        if (manager.stuff == null || data == null) return false;
        if (!NeedsItem(manager.stuff.Grade)) return false;
        if (price > MaxBidPrice(manager.stuff.Cost)) return false;
        return GlobalGold.Instance != null && GlobalGold.Instance.CanUseGold(NPCID, price);
    }

    // 선두로 입찰 (가격 설정 + 최고 입찰자 등록)
    protected void BidAt(Auction manager, int price)
    {
        manager.CurrentCost = price;
        manager.WinnerID = NPCID;
        manager.IfPlayerWin = false;
    }

    // 아직 경매에 남아 있는 다른 AI들
    protected List<AllAiBase> ActiveRivals(Auction manager, bool includeWinner = true)
    {
        List<AllAiBase> rivals = new List<AllAiBase>();
        foreach (var ai in manager.auctionAiList)
        {
            if (ai == null || ai == this || ai.IsGiveUp) continue;
            if (!includeWinner && ai.NPCID == manager.WinnerID) continue;
            rivals.Add(ai);
        }
        return rivals;
    }

    //=========================보스 스킬 확률 및 횟수 설정 함수===================
    protected virtual void SetSkillChance(float chance, int count)
    {
        SkillChance = chance;
        SkillCount = count;
    }
}
