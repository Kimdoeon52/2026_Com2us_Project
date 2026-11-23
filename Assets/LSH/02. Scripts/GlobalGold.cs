using System.Collections.Generic;
using System.IO;
using UnityEngine;

//// 1. 저장할 데이터 구조
//[System.Serializable]
//public class GameData
//{
//    public int gold;
//}
public class GlobalGold: PersistentSingleton<GlobalGold>, IGoldSource
{
    // 현재 게임 데이터
    public List<MainCharacterGold> mainCharacterdata;
    // 저장 파일 경로
    private string mainSavePath; //메인 캐릭터들 세이브 경로
                                 // 플레이어는 0번
    public int PlayerGold => mainCharacterdata != null && mainCharacterdata.Count > 0 ? mainCharacterdata[0].mainCharacterGold : 0;

    [System.Serializable]
    private class StageProgress // 스테이지 진행도 저장용
    {
        public int currentStage = 1;
    }

    private string stageSavePath; //스테이지 진행도 세이브 경로
    public int CurrentStage { get; private set; } = 1; //현재 스테이지 (1부터 시작)

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

        stageSavePath = Path.GetFullPath(Path.Combine(basePath, "stageProgress.json"));

        Debug.Log($"[저장 경로] {mainSavePath}");
        LoadGame();
        LoadStage();
    }

    void Start()
    {
        LoadGame(); //저장하면 로드
        // 게임 시작 시 기존 데이터 불러오기
        if (mainCharacterdata == null || mainCharacterdata.Count == 0)
        {
            Debug.LogWarning("[GlobalGold] mainCharacterdata가 비어있습니다. 인스펙터를 확인하세요.");
            return;
        }
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
        if (Input.GetKeyDown(KeyCode.S) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
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

    public void GetGold(int id, int goldplus) //골드 추가하는 함수임
    {
        foreach (var goldData in mainCharacterdata) //모든 데이터를 돌면서 같은 id가 있는 npc나 플레이어에게 골드를 추가해줌
        {
            if (goldData.mainCharacterID == id)
            {
                goldData.mainCharacterGold += goldplus;
                Debug.Log($"NPC {goldData.mainCharacterName}의 골드추가했다능~ {goldData.mainCharacterGold}");
                return;
            }
        }
    }

    public bool UseGold(int id, int goldminus) //골드 사용했다는 함수임 일부러 bool을 리턴해서 골드 사용 가능 불가능 여부 전달.
    {
        foreach (var goldData in mainCharacterdata) //모든 데이터를 돌면서 같은 id가 있는 npc나 플레이어에게 골드를 사용해줌
        {
            if (goldData.mainCharacterID == id)
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
    public bool CanUseGold(int id, int requiredGold) //골드 사용 가능 여부 판단하는 함수임
    {
        foreach (var goldData in mainCharacterdata)
        {
            if (goldData.mainCharacterID == id)
            {
                return goldData.mainCharacterGold >= requiredGold;
            }
        }
        return false;
    }
    //==========================================부품 수집 → 장비 강화==========================================================
    public const int ComponentsPerUpgrade = 3; // 같은 부품을 이만큼 모으면 강화

    // 랭커가 부품 1개를 얻음. 같은 부품이 3개 모이면 장비 중 그 등급보다 낮은 칸 하나를 그 등급으로 올리고 3개 소모.
    // 강화됐으면 true (upgradedGrade에 올라간 등급)
    public bool AddComponent(int id, ComponentDefinition component, out PartGrade upgradedGrade)
    {
        upgradedGrade = PartGrade.Basic;
        MainCharacterGold data = FindCharacter(id);
        if (data == null || component == null) return false;
        if (data.components == null) data.components = new List<OwnedComponent>();

        OwnedComponent owned = data.components.Find(c => c.id == component.Id);
        if (owned == null)
        {
            owned = new OwnedComponent { id = component.Id, displayName = component.DisplayName, grade = component.Grade, count = 0 };
            data.components.Add(owned);
        }
        owned.count++;
        Debug.Log($"[부품] {data.mainCharacterName} : {component.DisplayName} {owned.count}/{ComponentsPerUpgrade}");

        if (owned.count < ComponentsPerUpgrade) return false;
        if (!UpgradeLowestPart(data, owned.grade)) return false; // 올릴 칸이 없으면(전부 같거나 높음) 부품은 그대로 보관

        owned.count -= ComponentsPerUpgrade;
        upgradedGrade = owned.grade;
        Debug.Log($"[강화] {data.mainCharacterName} : {component.DisplayName} {ComponentsPerUpgrade}개 → 장비 한 칸이 {owned.grade}(으)로 강화 (남은 개수 {owned.count})");
        return true;
    }

    // 이 랭커가 가진 특정 부품 개수
    public int GetComponentCount(int id, string componentId)
    {
        MainCharacterGold data = FindCharacter(id);
        if (data == null || data.components == null) return 0;
        OwnedComponent owned = data.components.Find(c => c.id == componentId);
        return owned != null ? owned.count : 0;
    }

    // 장비 칸 중 grade보다 낮은 칸들 가운데 가장 낮은 칸을 grade로 올림
    private static bool UpgradeLowestPart(MainCharacterGold data, PartGrade grade)
    {
        if (data.parts == null || data.parts.Length == 0) return false;
        int lowest = -1;
        for (int i = 0; i < data.parts.Length; i++)
        {
            if (data.parts[i] >= grade) continue;
            if (lowest < 0 || data.parts[i] < data.parts[lowest]) lowest = i;
        }
        if (lowest < 0) return false;
        data.parts[lowest] = grade;
        return true;
    }

    private MainCharacterGold FindCharacter(int id)
    {
        if (mainCharacterdata == null) return null;
        return mainCharacterdata.Find(d => d != null && d.mainCharacterID == id);
    }

    //==========================================장비 등급 교체 (구 방식: 낙찰 즉시 강화 → AddComponent로 대체)==========================================================
    public void ChangePartsGrade(int id, PartGrade stuffGrade) //장비 등급 교체 함수
    {
        foreach (var data in mainCharacterdata) //모든 데이터를 돌면서 같은 id가 있는 npc나 플레이어에게 골드를 추가해줌
        {
            if (data.mainCharacterID == id)
            {
                for (int i = 0; i < data.parts.Length; i++)
                {
                    if(data.parts[i] < stuffGrade)
                    {
                        data.parts[i] = stuffGrade;
                        break; //등급 하나만 바꾸고 탈출
                    }
                }
            }
        }
    }
    //==========================================스테이지(지역)==========================================================
    // 2026-11-16: 경매 세션이 끝나면 자동으로 다음 스테이지로 넘기던 AdvanceStage는 없앴다 — 지역 이동은 나중에 다른 곳이 담당한다.
    // 지역을 바꾸는 쪽은 SetStage를 부르면 된다(파일에 저장). 경매장은 들어올 때 ReloadStage로 지금 지역을 다시 읽는다.

    /// <summary>지역(스테이지)을 정하고 저장한다 (1부터). 지역 이동을 담당하는 쪽이 부른다</summary>
    public void SetStage(int stage)
    {
        CurrentStage = Mathf.Max(1, stage);
        File.WriteAllText(stageSavePath, JsonUtility.ToJson(new StageProgress { currentStage = CurrentStage }));
        Debug.Log($"[스테이지] 현재 스테이지: {CurrentStage}");
    }

    /// <summary>저장된 지역을 다시 읽는다 — GlobalGold는 씬을 넘어 살아 있어서, 다른 곳이 바꾼 값을 쓰려면 다시 읽어야 한다</summary>
    public void ReloadStage() => LoadStage();

    private void LoadStage()
    {
        if (File.Exists(stageSavePath))
        {
            StageProgress progress = JsonUtility.FromJson<StageProgress>(File.ReadAllText(stageSavePath));
            CurrentStage = Mathf.Max(1, progress.currentStage);
        }
        else CurrentStage = 1; // 저장 파일이 없으면 1지역
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
