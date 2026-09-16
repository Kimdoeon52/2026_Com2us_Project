using System.Collections.Generic;
using System.IO;
using UnityEngine;

//// 1. 저장할 데이터 구조
//[System.Serializable]
//public class GameData
//{
//    public int gold;
//}
public class GlobalGold: MonoBehaviour
{
    // 현재 게임 데이터
    public List<Gold> data;
    
    // 저장 파일 경로
    private string savePath;

    void Awake()
    {
        // 저장 파일 경로 설정
        savePath = Path.Combine(Application.persistentDataPath, "DorajiGold.json");
    }

    void Start()
    {
        // 게임 시작 시 기존 데이터 불러오기
        LoadGame();
        foreach (var goldData in data)
        {
            Debug.Log($"현재 NPC: {goldData.npcName}, 골드: {goldData.gold}");
        }
    }

    void Update()
    {
        // [테스트용 키 입력]
        // G: 골드 100 증가
        if (Input.GetKeyDown(KeyCode.G))
        {
            GetGold(1, 100);
            GetGold(4, 100);
        }
        if(Input.GetKeyDown(KeyCode.H))
        {

            UseGold(1, 50);
            UseGold(4, 50);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            AddGoldAllNpc(100);
        }
        // S: 수동 저장
        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveGame();
        }

        // L: 수동 불러오기
        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadGame();
        }
    }


    //================================================골드 관련 기능=====================================================

    public void GetGold(int id, int goldplus) //골드 추가하는 함수임
    {
        foreach (var goldData in data) //모든 데이터를 돌면서 같은 id가 있는 npc나 플레이어에게 골드를 추가해줌
        {
            if (goldData.ID == id)
            {
                goldData.gold += goldplus;
                Debug.Log($"NPC {goldData.npcName}의 골드추가했다능~ {goldData.gold}");
                return;
            }
        }
    }

    public bool UseGold(int id, int goldminus) //골드 사용했다는 함수임 일부러 bool을 리턴해서 골드 사용 가능 불가능 여부 전달.
    {
        foreach (var goldData in data) //모든 데이터를 돌면서 같은 id가 있는 npc나 플레이어에게 골드를 사용해줌
        {
            if (goldData.ID == id)
            {
                if (goldData.gold >= goldminus)
                {
                    goldData.gold -= goldminus;
                    Debug.Log($"NPC {goldData.npcName}의 골드사용했다능~ {goldData.gold}");
                    return true;
                }
                else
                {
                    Debug.Log($"NPC {goldData.npcName}의 골드가 부족하다능~ 현재 골드: {goldData.gold}");
                }
                return false;
            }
        }
        return false;
    }

    public void AddGoldAllNpc(int goldplus) //모든 npc에게 골드 추가하는 함수임
    {
        foreach (var goldData in data)
        {
            goldData.gold += goldplus;
            Debug.Log($"NPC {goldData.npcName}의 골드추가했다능~ {goldData.gold}");
        }
    }

    //================================================SAVE&LOAD 기능=====================================================
    // JSON 파일로 저장
    public void SaveGame()
    {
        string json = "";
        foreach (var goldData in data)
        {
            json += JsonUtility.ToJson(goldData) + "\n"; //문자열로 번역하는 기능
        }
        //json +=JsonUtility.ToJson(data, true); //문자열로 번역하는 기능
        File.WriteAllText(savePath, json);
        Debug.Log($"[저장 완료] 경로: {savePath}\n내용: {json}");
    }

    // JSON 파일에서 불러오기
    public void LoadGame()
    {
        if (File.Exists(savePath))
        {
            string[] json = File.ReadAllLines(savePath); //savePath에 있는 json파일을 읽어와서 json변수에 저장
            if (data == null) {
                Debug.Log("data is null");
                //data = ScriptableObject.CreateInstance<Gold>(); 
            }
            //Debug.Log(data.name + " " + json);  
            //data = JsonUtility.FromJson<Gold>(json); //json 문자열을 GameData 객체로 변환 data에는 이제 변경된 골드값이 들어가게 됌! //class는 직접 넣고
            int index = 0;
            foreach (var goldData in data)
            {
                if (index >= json.Length)
                    break;
                JsonUtility.FromJsonOverwrite(json[index], goldData); // 기존 goldData 객체를 유지하면서 json 데이터를 덮어씀
                index++;
            }
        }
        //else
        //{
        //    Debug.LogWarning("저장된 데이터 파일이 없어 기본값(0)으로 시작합니다.");
        //    data = new GameData();
        //}
    }
}
