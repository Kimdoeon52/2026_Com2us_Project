using System.Collections.Generic;
using System.IO;
using UnityEngine;
/// <summary>
/// mainGold.json의 한 줄(NDJSON) 구조를 손실 없이 보존하기 위한 데이터 클래스
/// </summary>
[System.Serializable]
public class MainCharacterGoldData
{
    public int mainCharacterID;
    public string mainCharacterName;
    public int mainCharacterGold;
    public int score;
    public int power;
    public int rank;
    public int[] parts;
}

public class RankingSeasonResult : MonoBehaviour
{
    [Header("JSON File Settings")]
    [Tooltip("실제 프로젝트에서 mainGold.json을 읽고 쓰는 파일 경로에 맞춰 수정하세요.")]
    public string jsonFileName = "mainGold.json";

    // D:\...\Assets\LSH\06. JSon\mainGold.json 경로를 가리킴
    private string FilePath => Path.Combine(Application.dataPath, "LSH", "06. JSon", jsonFileName);

    /// <summary>
    /// 점수가 초기화되기 전 정렬된 랭킹 리스트를 받아 상위 1~10위 중 플레이어에게만 보상을 기록합니다.
    /// 실제 골드 지급은 길드 접수원 '포상 수령'에서 GlobalGold로 처리합니다 (mainGold.json 직접 수정은 GlobalGold 메모리 값에 덮어써짐).
    /// </summary>
    public void ProcessSeasonReward(List<NPC> sortedNpcList)
    {
        if (sortedNpcList == null || sortedNpcList.Count == 0) return;

        int checkCount = Mathf.Min(10, sortedNpcList.Count);

        for (int i = 0; i < checkCount; i++)
        {
            NPC target = sortedNpcList[i];

            // NPC는 보상을 지급하지 않음 (알림은 SystemNewsUiManager의 분기 이벤트 구독으로 별도 출력)
            if (!target.isPlayer) continue;

            // 1등(인덱스 0)은 10,000골드, 2~10등(인덱스 1~9)은 6,000골드
            int rewardGold = (i == 0) ? 10000 : 6000;

            // 바로 지급하지 않고 '받을 포상'으로 기록 → 길드 접수원 '포상 수령'에서 대사와 함께 지급 (LHM SeasonRewardStore)
            RealSteel.Reward.SeasonRewardStore.Add(i + 1, rewardGold);
            SystemNewsUiManager.SendNews(
                $"분기 랭킹 {i + 1}위 달성! 길드 접수원에게서 포상 {rewardGold:N0}골드를 받으세요."
            );
        }
    }

    private bool AddPlayerGoldToJson(int addGold)
    {
        if (!File.Exists(FilePath))
        {
            Debug.LogError($"[RankingSeasonResult] 파일을 찾을 수 없습니다: {FilePath}");
            return false;
        }

        string[] lines = File.ReadAllLines(FilePath);
        bool isUpdated = false;

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            MainCharacterGoldData characterData = JsonUtility.FromJson<MainCharacterGoldData>(lines[i]);

            if (characterData != null && characterData.mainCharacterID == 0)
            {
                characterData.mainCharacterGold += addGold;
                lines[i] = JsonUtility.ToJson(characterData);
                isUpdated = true;
                break;
            }
        }

        if (isUpdated)
        {
            File.WriteAllLines(FilePath, lines);
        }
        else
        {
            Debug.LogWarning("[RankingSeasonResult] mainGold.json에서 mainCharacterID가 0인 플레이어 데이터를 찾지 못했습니다.");
        }

        return isUpdated;
    }
}