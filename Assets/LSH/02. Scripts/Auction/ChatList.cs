using System.Collections.Generic;
using UnityEngine;

// 경매 진행자 대사 모음
// - 상황(key)마다 여러 '대사 묶음'을 넣어두고, 부를 때마다 그중 하나를 랜덤으로 골라 같은 말만 반복하지 않게 함
// - {0}, {1}, {2} ... 자리에는 GetLines에 넘긴 값이 순서대로 들어감
public class ChatList : Singleton<ChatList>
{
    private readonly Dictionary<string, List<string[]>> chatDictionary = new Dictionary<string, List<string[]>>();
    private readonly Dictionary<string, int> lastPicked = new Dictionary<string, int>(); // 직전에 쓴 묶음 (연속 중복 방지)

    protected override void Awake()
    {
        base.Awake();
        AddChat();
    }

    private void Add(string key, params string[] lines)
    {
        if (!chatDictionary.TryGetValue(key, out List<string[]> list))
        {
            list = new List<string[]>();
            chatDictionary[key] = list;
        }
        list.Add(lines);
    }

    void AddChat()
    {
        // ===== 첫 경매 시작 (커튼 열리기 전) ===== {0}=총 회차
        Add("개장",
            "신사 숙녀 여러분, 오늘도 저희 경매장을 찾아주셔서 감사합니다!",
            "오늘은 총 {0}점의 물품이 준비되어 있습니다.",
            "그럼 첫 번째 경매 물품부터 만나보시죠!");
        Add("개장",
            "오래 기다리셨습니다! 지금부터 경매를 시작하겠습니다.",
            "오늘 준비된 물품은 모두 {0}점! 놓치면 후회하실 겁니다.",
            "첫 번째 물품, 막을 올려주세요!");

        // ===== 중간 회차 시작 ===== {0}=지금 회차, {1}=총 회차
        Add("다음경매",
            "다음 경매 물품입니다!",
            "{0}번째 물품, 막을 올려주세요!");
        Add("다음경매",
            "자, 분위기가 뜨거워지고 있네요!",
            "이어서 {0}번째 물품을 소개해 드리겠습니다!");
        Add("다음경매",
            "아직 끝나지 않았습니다, 여러분!",
            "다음 경매 물품입니다!");

        // ===== 마지막 회차 시작 =====
        Add("마지막경매",
            "드디어 오늘의 마지막 경매 물품입니다!",
            "마지막 기회, 놓치지 마세요!");
        Add("마지막경매",
            "벌써 마지막 물품이군요.",
            "오늘의 대미를 장식할 물품, 공개합니다!");

        // ===== 물품 소개 (커튼 열린 뒤) ===== {0}=물품 이름, {1}=등급, {2}=시작가
        Add("물건소개",
            "이번 경매 물품은 [{0}]입니다!",
            "등급은 {1}! 상태도 아주 훌륭하군요.",
            "시작가는 {2}G입니다.");
        Add("물건소개",
            "보시는 물품은 바로 [{0}]!",
            "{1} 등급으로, 쉽게 구할 수 없는 물건이죠.",
            "{2}G부터 시작하겠습니다!");
        Add("물건소개",
            "[{0}], {1} 등급입니다.",
            "이 물품의 새 주인은 과연 누가 될까요?",
            "시작가 {2}G! 자, 준비되셨습니까?");

        // ===== 입찰 시작 신호 (일반 경매) =====
        Add("입찰시작", "그럼, 입찰을 시작하겠습니다!");
        Add("입찰시작", "자, 입찰 시작합니다!");
        Add("입찰시작", "지금부터 입찰을 받겠습니다!");

        // ===== 낙찰 (일반 경매) ===== {0}=낙찰자, {1}=낙찰가
        Add("낙찰",
            "{1}G! 더 없으십니까?",
            "...낙찰! {0}님께 낙찰되었습니다!",
            "축하드립니다!");
        Add("낙찰",
            "{1}G, 더 이상 없습니까? 셋, 둘, 하나...",
            "낙찰! {0}님, 축하드립니다!");

        // ===== 낙찰 (주먹 경매: 결과 발표는 이미 했으니 축하만) ===== {0}=낙찰자
        Add("낙찰_주먹", "{0}님, 축하드립니다! 좋은 물건 가져가시네요.");
        Add("낙찰_주먹", "치열한 눈치 싸움의 승자는 {0}님! 축하드립니다!");

        // ===== 유찰 =====
        Add("유찰",
            "아쉽게도 이번 물품은 주인을 찾지 못했군요.",
            "이번 물품은 유찰되었습니다.");
        Add("유찰_주먹", "이번 물품은 다음 기회에 다시 선보이겠습니다.");

        // ===== 회차 마무리 (다음 물품이 남았을 때) =====
        Add("회차마무리", "잠시 후 다음 물품이 이어집니다!");
        Add("회차마무리", "다음 물품을 준비하겠습니다. 잠시만 기다려 주세요!");

        // ===== 모든 경매 종료 =====
        Add("폐장",
            "이것으로 오늘의 경매를 모두 마치겠습니다.",
            "참여해 주신 여러분, 감사합니다! 다음에 또 뵙겠습니다!");
        Add("폐장",
            "오늘 준비한 물품은 여기까지입니다!",
            "좋은 거래 되셨길 바랍니다. 조심히 들어가세요!");

        // ===== 예전 키 (다른 곳에서 부를 수 있어 유지) =====
        Add("경매시작", "경매를 시작하겠습니다.", "물건을 가져와 주시기 바랍니다.");
        Add("경매완료", "{0}님 축하드립니다.", "경매가 종료되었습니다.");
    }

    // 상황(key)에 맞는 대사 묶음 하나를 랜덤으로 골라 {0},{1}..에 값을 채워서 돌려줌
    public List<string> GetLines(string key, params object[] args)
    {
        if (!chatDictionary.TryGetValue(key, out List<string[]> variants) || variants.Count == 0)
        {
            Debug.LogWarning($"[ChatList] '{key}' 대사가 없습니다.");
            return new List<string>();
        }

        int index = Random.Range(0, variants.Count);
        if (variants.Count > 1 && lastPicked.TryGetValue(key, out int last) && index == last)
            index = (index + 1) % variants.Count; // 바로 전에 쓴 대사는 피함
        lastPicked[key] = index;

        List<string> result = new List<string>();
        foreach (string line in variants[index])
        {
            result.Add(args != null && args.Length > 0 ? string.Format(line, args) : line);
        }
        return result;
    }

    // 예전 방식 호환: {0}에 하나만 넣음
    public List<string> GetChat(string key, string stuffName = "")
    {
        return string.IsNullOrEmpty(stuffName) ? GetLines(key) : GetLines(key, stuffName);
    }

    // 등급을 한글로 (대사용)
    public static string GradeName(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Common: return "노말";
            case PartGrade.Rare: return "레어";
            case PartGrade.Epic: return "에픽";
            case PartGrade.Legendary: return "레전더리";
            case PartGrade.Prototype: return "프로토타입";
            default: return "기본";
        }
    }
}
