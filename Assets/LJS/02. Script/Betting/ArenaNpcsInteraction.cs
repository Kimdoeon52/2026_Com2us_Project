using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArenaNpcsInteraction : MonoBehaviour
{
    [Header("SO 연결")]
    public ArenaNpcInteractionSO npcInteractionSO;

    [Header("UI 구성")]
    public GameObject interactPanel;
    public Button interactButton;
    public TextMeshProUGUI interactText;

    private ArenaNpcs currentNpc;

    private void OnEnable()
    {
        if (npcInteractionSO != null)
        {
            npcInteractionSO.OnInteractableEntered += OnNpcEntered;
            npcInteractionSO.OnInteractableExited += OnNpcExited;
        }
    }

    private void OnDisable()
    {
        npcInteractionSO.OnInteractableEntered -= OnNpcEntered;
        npcInteractionSO.OnInteractableExited -= OnNpcExited;
    }
    
    private void Start()
    {
        interactPanel.SetActive(false);
    }

    private void Update()
    {
        /*if (interactPanel.activeSelf && Input.GetKeyDown(KeyCode.E) && currentNpc != null) 
            ExecuteInteraction();*/
        if (currentNpc != null)
        {
            // 1. 패널이 꺼져있을 때 E키를 누르면 패널 등장
            if (!interactPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    ShowInteractButton(currentNpc);
                }
            }
            // 2. 패널이 켜져있을 때 E키 또는 엔터키를 누르면 베팅 시작
            else
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ExecuteInteraction();
                }
            }
        }
    }


    private void OnNpcEntered(ArenaNpcs npc) { currentNpc = npc; }
    private void OnNpcExited()
    {
        currentNpc = null;
        interactPanel.SetActive(false);
    }

    private void ShowInteractButton(ArenaNpcs npc)
    {
        interactText.text = $"[{npc.npcName}]\n경기 시작 베팅을 하시겠습니까?";

        interactButton.onClick.RemoveAllListeners();
        interactButton.onClick.AddListener(ExecuteInteraction);

        interactPanel.SetActive(true);
    }

    private void ExecuteInteraction()
    {
        if (currentNpc != null)
        {
            currentNpc.BettingStart();
            interactPanel.SetActive(false);
        }
    }
}
