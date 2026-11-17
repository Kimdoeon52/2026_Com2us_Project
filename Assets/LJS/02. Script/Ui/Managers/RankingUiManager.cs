using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
/// <summary>
/// 랭킹 UI를 관리해주는 매니저. 랭킹 따라 이동 이벤트 나오는 것도 여기 있음.
/// </summary>
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
