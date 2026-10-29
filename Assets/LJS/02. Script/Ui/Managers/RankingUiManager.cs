using System.Collections.Generic;
using UnityEngine;

public class RankingUiManager : MonoBehaviour
{
    public static RankingUiManager Instance;

    [Header("UI Object")]
    public GameObject rankingCanvas;

    [Header("Dependencies")]
    public RankingSystemManager rankingManager; // 데이터 매니저 참조
    public Transform contentParent;             // 하이어라키의 Content 객체 할당
    public GameObject npcLayPrefab;             // NpcLay 프리팹 할당[cite: 5]

    private List<NpcUiItem> spawnedItems = new List<NpcUiItem>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else return;
        // 시작 시 Ui들 숨기기
        if (rankingCanvas != null) rankingCanvas.SetActive(false);
    }

    void Start()
    {
        // Manager의 정렬 완료 이벤트에 UI 갱신 함수를 구독(연결)[cite: 6]
        rankingManager.OnRankingUpdated += RefreshUI;
    }

    void OnDestroy()
    {
        if (rankingManager != null)
        {
            rankingManager.OnRankingUpdated -= RefreshUI;
        }
    }

    public void ToggleRankingUI()
    {
        bool isActive = !rankingCanvas.activeSelf;
        rankingCanvas.SetActive(isActive);

        if (isActive)
        {
            // UI를 열 때 게임 시간을 정지 상태(UIOpen)로 변경
            TimeSystemManager.Instance.ChangeTimeState(TimeSystemManager.TimeState.UIOpen);
            // 필요 시 UI를 열 때 최신 데이터로 즉시 화면 갱신
            RefreshUI();
        }
        else
        {
            // UI를 닫을 때 게임 시간 다시 흐름(Running) 상태로 복구
            TimeSystemManager.Instance.ChangeTimeState(TimeSystemManager.TimeState.Running);
        }
    }

    // OnRankingUpdated 이벤트가 발생할 때마다 자동으로 실행됨
    private void RefreshUI()
    {
        // 1. 기존에 생성된 UI 리스트가 부족하면 새로 생성 (초기화 및 동적 확장)
        int dataCount = rankingManager.npcList.Count; //[cite: 6]

        while (spawnedItems.Count < dataCount)
        {
            GameObject go = Instantiate(npcLayPrefab, contentParent);
            NpcUiItem uiItem = go.GetComponent<NpcUiItem>();
            spawnedItems.Add(uiItem);
        }

        // 2. 정렬된 npcList의 데이터를 순서대로 UI에 덮어씌움
        for (int i = 0; i < dataCount; i++)
        {
            spawnedItems[i].gameObject.SetActive(true);
            spawnedItems[i].SetData(rankingManager.npcList[i]); //[cite: 6]
        }

        // 3. 만약 데이터 개수보다 생성된 UI가 많다면 남는 UI는 비활성화
        for (int i = dataCount; i < spawnedItems.Count; i++)
        {
            spawnedItems[i].gameObject.SetActive(false);
        }
    }
}
