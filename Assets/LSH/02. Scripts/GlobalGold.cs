using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 1. 저장할 데이터 구조
[System.Serializable]
public class GameData
{
    public int gold;
}
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
            data[0].gold += 100;
            Debug.Log($"골드 증가! 현재 골드: {data[0].gold}");
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            foreach (var goldData in data)
            {
                goldData.gold += 100;
            }
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
            //JsonUtility.FromJsonOverwrite(json, data); // 기존 data 객체를 유지하면서 json 데이터를 덮어씀 //SO나 MONO머시기는 덮어써서 사용,
            if (data == null)
            {
                Debug.Log("data is null");
                //data = ScriptableObject.CreateInstance<Gold>(); 
            }
            //Debug.Log($"[불러오기 완료] 현재 골드: {data.gold}");
        }
        //else
        //{
        //    Debug.LogWarning("저장된 데이터 파일이 없어 기본값(0)으로 시작합니다.");
        //    data = new GameData();
        //}
    }
}
