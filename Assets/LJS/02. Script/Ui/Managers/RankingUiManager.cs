using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class RankingUiManager : MonoBehaviour
{
    public static RankingUiManager Instance;

    [Header("UI Object")]
    public GameObject rankingCanvas;

    [Header("Dependencies")]
    public RankingSystemManager rankingManager;
    public Transform contentParent;
    public GameObject npcLayPrefab;

    [Header("Animation Settings")]
    public float itemHeight = 80f;    // NpcLay 프리팹의 높이 (UI에 맞게 조절)
    public float spacing = 10f;       // 항목 간의 간격 (UI에 맞게 조절)
    public float animDuration = 0.6f; // 이동 애니메이션 시간

    // NPC 데이터와 UI 객체를 1:1로 매칭하여 추적하는 딕셔너리 (핵심 변경점)
    private Dictionary<NPC, NpcUiItem> npcUiMap = new Dictionary<NPC, NpcUiItem>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (rankingCanvas != null) rankingCanvas.SetActive(false);
    }

    void Start()
    {
        rankingManager.OnRankingUpdated += OnRankingUpdated;
    }

    void OnDestroy()
    {
        if (rankingManager != null)
        {
            rankingManager.OnRankingUpdated -= OnRankingUpdated;
        }
    }

    public void ToggleRankingUI()
    {
        bool isActive = !rankingCanvas.activeSelf;
        rankingCanvas.SetActive(isActive);

        if (isActive)
        {
            //TimeSystemManager.Instance.ChangeTimeState(TimeSystemManager.TimeState.UIOpen);
            // 창을 열 때는 애니메이션 없이 즉시 화면 배치 (true 전달)
            RefreshUI(true);
        }
        else
        {
            TimeSystemManager.Instance.ChangeTimeState(TimeSystemManager.TimeState.Running);
        }
    }

    // 매니저에서 랭킹이 갱신되었을 때 호출되는 이벤트용 (애니메이션 켜짐)
    private void OnRankingUpdated()
    {
        // UI가 열려있을 때만 애니메이션을 보여주거나 갱신합니다.
        if (rankingCanvas.activeSelf)
        {
            RefreshUI(false);
        }
    }

    // 실제 UI 생성 및 좌표 이동 로직
    private void RefreshUI(bool instantSnap)
    {
        List<NPC> currentList = rankingManager.npcList;

        // 1. 필요한 UI 아이템 생성 및 딕셔너리 등록 (최초 1회에만 Instantiate 발생)
        foreach (var npc in currentList)
        {
            if (!npcUiMap.ContainsKey(npc))
            {
                GameObject go = Instantiate(npcLayPrefab, contentParent);
                NpcUiItem uiItem = go.GetComponent<NpcUiItem>();
                npcUiMap.Add(npc, uiItem);
            }
        }

        // 2. 스크롤 뷰의 Content 전체 높이 수동 조절 (Layout Group을 지웠으므로 코드로 크기 설정 필요)
        float totalHeight = currentList.Count * (itemHeight + spacing);
        RectTransform contentRect = contentParent.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, totalHeight);

        // 3. 정렬된 NPC 리스트를 바탕으로 목표 위치 계산 및 이동
        for (int i = 0; i < currentList.Count; i++)
        {
            NPC npc = currentList[i];
            NpcUiItem uiItem = npcUiMap[npc];

            // 데이터 최신화 (점수, 전투력, 순위 텍스트 갱신)
            uiItem.SetData(npc);

            // 목표 Y 좌표 계산: 1위는 0, 2위는 -(높이+간격), 3위는 -(높이+간격)*2
            float targetY = -(i * (itemHeight + spacing));

            RectTransform rect = uiItem.GetComponent<RectTransform>();

            // DOTween이 진행 중이면 겹치지 않게 중지
            rect.DOKill();

            if (instantSnap)
            {
                // UI를 방금 켰을 때는 애니메이션 없이 즉시 이동
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, targetY);
            }
            else
            {
                // 랭킹 갱신 시에는 부드럽게 목표 Y좌표로 날아감 (DOAnchorPosY)
                rect.DOAnchorPosY(targetY, animDuration).SetEase(Ease.OutQuart);
            }
        }
    }
}

/*
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
 */