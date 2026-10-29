using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 100명과 랭킹 시스템을 관리하는 코드.
/// NPC는 데이터 상으로만 존재하므로 이대로 둔다.
/// </summary>

[System.Serializable]
public class NPC : IComparable<NPC>
{
    public string name;    // 이름
    public int rank;         // 순위
    public int score;        // 점수
    public int cp;           // 전투력 (Combat Power)

    // int 배열 대신 PartGrade Enum 배열 사용
    public PartGrade[] parts = 
        new PartGrade[4] 
        {
            PartGrade.Common,
            PartGrade.Common,
            PartGrade.Common,
            PartGrade.Common 
        };

    // 20위 단위로 부여되는 변수 (기본 0, 최대 4)
    public int groupVar = 0;
    public bool isPlayer = false;

    public int CompareTo(NPC other)
    {
        if (other == null) return 1;

        // 1순위: 점수 내림차순
        int scoreComparison = other.score.CompareTo(this.score);
        if (scoreComparison != 0) return scoreComparison;

        // 2순위: 전투력 내림차순
        int cpComparison = other.cp.CompareTo(this.cp);
        if (cpComparison != 0) return cpComparison;

        // 3순위: 이전 순위 오름차순 (숫자가 작을수록 우선 배정)
        return this.rank.CompareTo(other.rank);
    }
}

public class RankingSystemManager : MonoBehaviour
{
    public static RankingSystemManager Instance;

    [Header("NPC Data")]
    public NpcNameData npcNameData; // 스크립터블 오브젝트용

    public List<NPC> npcList = new List<NPC>(101); // Npc랭킹 리스트. 플레이어까지 101명
    private NPC playerRef;

    // 다른 스크립트에서 참조용 리스트
    public List<PartGrade[]> top5NpcParts = new List<PartGrade[]>(5);

    public event Action OnRankingUpdated; // 외부 알림 이벤트. 필요하면 사용
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else return;
    }

    void Start()
    {
        InitializeNPCs();
    }

    private void InitializeNPCs() // npc 생성
    {
        npcList.Clear();
        top5NpcParts.Clear();
        for (int i = 0; i < 5; i++)
        {
            top5NpcParts.Add(new PartGrade[4]);
        }

        // 플레이어 객체 미리 생성 및 리스트 등록
        playerRef = new NPC();
        playerRef.isPlayer = true;
        npcList.Add(playerRef);

        // 이름 리스트 가져오기 및 섞기 (Fisher-Yates 알고리즘)
        List<string> availableNames = new List<string>();
        bool hasEnoughNames = (npcNameData != null && npcNameData.npcNames.Count >= 100);

        if (hasEnoughNames)
        {
            // 원본 SO 데이터를 건드리지 않기 위해 복사본 생성
            availableNames = new List<string>(npcNameData.npcNames);

            for (int i = 0; i < availableNames.Count; i++)
            {
                string temp = availableNames[i];
                int randomIndex = UnityEngine.Random.Range(i, availableNames.Count);
                availableNames[i] = availableNames[randomIndex];
                availableNames[randomIndex] = temp;
            }
        }
        else
        {
            Debug.LogWarning("[RankingSystem] NpcNameData가 없거나 이름 개수가 100개 미만. 기본 이름으로 대체.");
        }

        for (int i = 0; i < 100; i++)
        {
            NPC newNpc = new NPC();

            // 데이터가 충분하면 섞인 리스트에서 순서대로 뽑아 배정 (중복 절대 없음)
            if (hasEnoughNames)
            {
                newNpc.name = availableNames[i];
            }
            else
            {
                newNpc.name = "NPC" + i.ToString("D2"); // 실패 시 기존 방식 유지
            }
            newNpc.cp = 72;
            newNpc.rank = i + 1;
            npcList.Add(newNpc);
        }
    }

    public void UpdatePlayerRecord(string playerName, int playerCp, int playerScore)
    {
        if (playerRef == null)
        {
            playerRef = new NPC();
            playerRef.name = playerName;
            playerRef.isPlayer = true;
            playerRef.rank = int.MaxValue; // 최초 진입 시 가장 낮은 순위로 가정
            npcList.Add(playerRef);
        }

        playerRef.cp = playerCp;
        playerRef.score = playerScore;

        SortAndRank();
    }

    public void OnEventTriggered()
    {
        UpdateCombatPower();
        UpdateScores();
        SortAndRank();
        UpdateGroupVariables();
    }
    private void OnEnable()
    {
        TimeSystemManager.OnDayRankingRestore += OnEventTriggered;
    }
    private void OnDisable()
    {
        TimeSystemManager.OnDayRankingRestore -= OnEventTriggered;
    }

    private void UpdateCombatPower()
    {
        foreach (var npc in npcList)
        {
            if (npc.isPlayer) continue;

            int pCommon = 60 - (3 * npc.groupVar);
            int pRare = 30 - (2 * npc.groupVar);
            int pEpic = 7 + (3 * npc.groupVar);
            int pLegendary = 3 + (2 * npc.groupVar);

            int roll = UnityEngine.Random.Range(1, 101);
            PartGrade rolledGrade = PartGrade.Common;

            if (roll <= pCommon) rolledGrade = PartGrade.Common;
            else if (roll <= pCommon + pRare) rolledGrade = PartGrade.Rare;
            else if (roll <= pCommon + pRare + pEpic) rolledGrade = PartGrade.Epic;
            else rolledGrade = PartGrade.Legendary;

            int randomPart = UnityEngine.Random.Range(0, 4);

            // Enum은 내부적으로 정수이므로 캐스팅하여 크기 비교 가능[cite: 2]
            if ((int)rolledGrade > (int)npc.parts[randomPart])
            {
                npc.parts[randomPart] = rolledGrade;
            }

            // 등급별 고정 수치를 참조하여 전투력 합산
            npc.cp = GetPartPower(npc.parts[0]) + GetPartPower(npc.parts[1]) +
                     GetPartPower(npc.parts[2]) + GetPartPower(npc.parts[3]);
        }
    }
    private int GetPartPower(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Common: return 18;
            case PartGrade.Rare: return 38;
            case PartGrade.Epic: return 60;
            case PartGrade.Legendary: return 90;
            default: return 18;
        }
    }

    private void UpdateScores() // 점수 최신화
    {
        if (npcList.Count == 0) return;

        int minCp = int.MaxValue;
        int maxCp = int.MinValue;

        for (int i = 0; i < npcList.Count; i++)
        {
            if (npcList[i].isPlayer) continue;
            int currentCp = npcList[i].cp;
            if (currentCp < minCp) minCp = currentCp;
            if (currentCp > maxCp) maxCp = currentCp;
        }

        int cpDifference = maxCp - minCp;

        foreach (var npc in npcList)
        {
            if (npc.isPlayer) continue;
            float powerRatio = 0f;
            if (cpDifference > 0)
            {
                powerRatio = (float)(npc.cp - minCp) / cpDifference;
            }

            npc.score += GetWeightedScore(powerRatio);
        }
    }

    private void SortAndRank()
    {
        npcList.Sort();

        for (int i = 0; i < npcList.Count; i++)
        {
            npcList[i].rank = i + 1;
        }
        UpdateTop5Parts();
        OnRankingUpdated?.Invoke();
    }

    private void UpdateTop5Parts()
    {
        //top5NpcParts.Clear();
        int count = Mathf.Min(5, npcList.Count);

        for (int i = 0; i < count; i++)
        {
            // 배열은 참조형이므로 의도치 않은 변조 방지를 위해 복사본 제공
            //top5NpcParts.Add((PartGrade[])npcList[i].parts.Clone());
            Array.Copy(npcList[i].parts, top5NpcParts[i], 4);
        }
    }

    private void UpdateGroupVariables()
    {
        foreach (var npc in npcList)
        {
            if (npc.isPlayer) continue;
            int groupIndex = (npc.rank - 1) / 20;
            npc.groupVar = Mathf.Clamp(4 - groupIndex, 0, 4);
        }
    }

    private int GetWeightedScore(float powerRatio)
    {
        float[] weights = { 20f, 20f, 20f, 20f, 20f };

        weights[0] += (1f - powerRatio) * 50f;
        weights[1] += (1f - powerRatio) * 25f;
        weights[3] += powerRatio * 25f;
        weights[4] += powerRatio * 50f;

        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            totalWeight += weights[i];
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return (i + 1) * 10;
            }
        }
        return 10;
    }
}
