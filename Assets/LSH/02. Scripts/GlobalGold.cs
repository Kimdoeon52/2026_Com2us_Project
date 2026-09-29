using System.Collections.Generic;
using System.IO;
using UnityEngine;

//// 1. 저장할 데이터 구조
//[System.Serializable]
//public class GameData
//{
//    public int gold;
//}
public class GlobalGold: PersistentSingleton<GlobalGold>
{
    // 현재 게임 데이터
    public List<MainCharacterGold> mainCharacterdata;
    // 저장 파일 경로
    private string mainSavePath; //메인 캐릭터들 세이브 경로

    protected override void Awake()
    {
        base.Awake();
        // 저장할 디렉터리 경로 및 파일 경로 설정
        string basePath;
#if UNITY_EDITOR
        basePath = Path.Combine(Application.dataPath, "LSH", "06. JSon");
#else
        basePath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "JSonGoldData"); //빌드하면 저장경로에 저거 이름으로 만들어짐
#endif
        // 해당 디렉터리(폴더)가 없으면 생성임. 그냥 호오오오옥시 모를때
        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
        }

        // 최종 파일 경로: LSH/06.JSon/mainGold.json
        mainSavePath = Path.GetFullPath(Path.Combine(basePath, "mainGold.json"));//Path.GetFullPath은 역슬래시로 통일시켜줌.안쓰면 뒤죽박죽...

        Debug.Log($"[저장 경로] {mainSavePath}");
        LoadGame();
    }

    void Start()
    {
        LoadGame(); //저장하면 로드
        // 게임 시작 시 기존 데이터 불러오기
        foreach (var goldData in mainCharacterdata)
        {
            Debug.Log($"현재 Main캐릭터들: {goldData.mainCharacterName}, 골드: {goldData.mainCharacterGold}");
        }
    }

    void Update()
    {
        //// [테스트용 키 입력]
        //// G: 골드 100 증가
        //if (Input.GetKeyDown(KeyCode.G))
        //{
        //    GetGold(1, 100);
        //    GetGold(4, 100);
        //}
        //if(Input.GetKeyDown(KeyCode.H))
        //{
        //    UseGold(1, 50);
        //    UseGold(4, 50);
        //}
        //if (Input.GetKeyDown(KeyCode.E))
        //{
        //    AddGoldAllNpc(100);
        //}
        //// S: 수동 저장
        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveGame();
        }

        //// L: 수동 불러오기
        //if (Input.GetKeyDown(KeyCode.L))
        //{
        //    LoadGame();
        //}
    }


    //================================================골드 관련 기능=====================================================

    public void GetGold(string name, int goldplus) //골드 추가하는 함수임
    {
        foreach (var goldData in mainCharacterdata) //모든 데이터를 돌면서 같은 id가 있는 npc나 플레이어에게 골드를 추가해줌
        {
            if (goldData.mainCharacterName == name)
            {
                goldData.mainCharacterGold += goldplus;
                Debug.Log($"NPC {goldData.mainCharacterName}의 골드추가했다능~ {goldData.mainCharacterGold}");
                return;
            }
        }
    }

    public bool UseGold(string name, int goldminus) //골드 사용했다는 함수임 일부러 bool을 리턴해서 골드 사용 가능 불가능 여부 전달.
    {
        foreach (var goldData in mainCharacterdata) //모든 데이터를 돌면서 같은 id가 있는 npc나 플레이어에게 골드를 사용해줌
        {
            if (goldData.mainCharacterName == name)
            {
                if (goldData.mainCharacterGold >= goldminus)
                {
                    goldData.mainCharacterGold -= goldminus;
                    Debug.Log($"NPC {goldData.mainCharacterName}의 골드사용했다능~ {goldData.mainCharacterGold}");
                    return true;
                }
                else
                {
                    Debug.Log($"NPC {goldData.mainCharacterName}의 골드가 부족하다능~ 현재 골드: {goldData.mainCharacterGold}");
                }
                return false;
            }
        }
        return false;
    }

    public void AddGoldAllNpc(int goldplus) //모든 npc에게 골드 추가하는 함수임
    {
        foreach (var goldData in mainCharacterdata)
        {
            goldData.mainCharacterGold += goldplus;
            Debug.Log($"NPC {goldData.mainCharacterName}의 골드추가했다능~ {goldData.mainCharacterGold}");
        }
    }
    //====================================골드 사용 가능 불가능 판단 여부===============================================
    public bool CanUseGold(string name, int requiredGold) //골드 사용 가능 여부 판단하는 함수임
    {
        foreach (var goldData in mainCharacterdata)
        {
            if (goldData.mainCharacterName == name)
            {
                return goldData.mainCharacterGold >= requiredGold;
            }
        }
        return false;
    }
    //================================================SAVE&LOAD 기능=====================================================
    // JSON 파일로 저장
    public void SaveGame()
    {
        string json = "";
        foreach (var goldData in mainCharacterdata)
        {
            json += JsonUtility.ToJson(goldData) + "\n"; //문자열로 번역하는 기능
        }
        //json +=JsonUtility.ToJson(data, true); //문자열로 번역하는 기능
        File.WriteAllText(mainSavePath, json);
        Debug.Log($"[저장 완료] 경로: {mainSavePath}\n내용: {json}");
    }

    // JSON 파일에서 불러오기
    public void LoadGame()
    {
        if (File.Exists(mainSavePath))
        {
            string[] mainJson = File.ReadAllLines(mainSavePath); //savePath에 있는 json파일을 읽어와서 json변수에 저장
            if (mainCharacterdata == null) 
            {
                Debug.Log("data is null");
            }
            //Debug.Log(data.name + " " + json);  
            //data = JsonUtility.FromJson<Gold>(json); //json 문자열을 GameData 객체로 변환 data에는 이제 변경된 골드값이 들어가게 됌! //class는 직접 넣고
            int index = 0;

            //===============================main 캐릭터 데이터 불러오기=========================================
            foreach (var goldData in mainCharacterdata)
            {
                if (index >= mainJson.Length)
                    break;
                JsonUtility.FromJsonOverwrite(mainJson[index], goldData); // 기존 goldData 객체를 유지하면서 json 데이터를 덮어씀
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
