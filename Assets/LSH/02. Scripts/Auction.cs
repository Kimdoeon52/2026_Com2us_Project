using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Auction : MonoBehaviour
{
    [SerializeField] public Text auctionName; // 이름 나오는 Text공간
    [SerializeField] public Text auctionCost; // 가격 나오는 Text공간
    [SerializeField] public Text chat; // 채팅 나오는 Text공간
    public List<TestStuff> testStuff; //나중에 TestStuff를 바꾸기 지금은 실험용임
    public List<TextDialogue> dialogue;
    private int currentCost = 0; // 현재 가격
    private TestStuff stuff;
    int stuffCost = 0; // TestStuff의 cost값
    private void Start()
    {
        stuff = testStuff[Random.Range(0, testStuff.Count)];
        currentCost = stuff.cost;
    }
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
           StartAuction().Forget();
        }
    }
    private async UniTask StartAuction() //경매 시작 부분.
    {
        auctionName.text = stuff.name;
        auctionCost.text = currentCost.ToString();
        await UniTask.Delay(1000); // 1초 대기
        chat.text = "경매 시작!";
    }

    //======================채팅====================================
    private async UniTask Chatting()
    {
        await UniTask.Delay(1000); // 1초 대기
        chat.text = "현재 가격: " + currentCost.ToString();
    }
}
