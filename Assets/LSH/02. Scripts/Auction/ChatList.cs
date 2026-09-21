using System.Collections.Generic;
using UnityEngine;

public class ChatList : PersistentSingleton<ChatList>
{
    private Dictionary<string, List<string>> chatDictionary = new Dictionary<string, List<string>>();

    protected override void Awake()
    {
        base.Awake();
        AddChat();
    }

    void AddChat()
    {
        chatDictionary["경매시작"] = new List<string>
        {
            "경매를 시작하겠습니다.",
            "물건을 가져와 주시기 바랍니다."
        };
        chatDictionary["물건소개"] = new List<string>
        {
            "이번 경매 물품은 {0}입니다.",
            "상당히 귀한 물건으로 보입니다.",
            "이번 물품의 주인은 누가 될지 궁금해지는군요."
        };
        chatDictionary["경매중"] = new List<string>
        {
            "더 없으십니까?",
            "다음 입찰자 계십니까?",
            "입찰하실 분 계십니까?",
            "시간이 흐르고 있습니다."
        };
        chatDictionary["경매완료"] = new List<string>
        {
            "{0}님 축하드립니다.",
            "경매가 종료되었습니다."
        };
    }

    public List<string> GetChat(string key, string stuffName = "")
    {
        if (!chatDictionary.ContainsKey(key))
        {
            Debug.LogWarning($"해당하는 대사가 없다요~!");
            return null;
        }

        List<string> nowChat = chatDictionary[key];
        List<string> addStuffChat = new List<string>();

        foreach (var chat in nowChat)
        {
            // {0}자리에 물건 이름을 넣기 위한거임
            string realChat = string.IsNullOrEmpty(stuffName)
                ? chat
                : string.Format(chat, stuffName);

            addStuffChat.Add(realChat);
        }

        return addStuffChat;
    }
}
