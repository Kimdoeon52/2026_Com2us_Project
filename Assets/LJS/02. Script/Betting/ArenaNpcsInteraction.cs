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
            npcInteractionSO.OnInteractableEntered += ShowInteractButton;
            npcInteractionSO.OnInteractableExited += HideInteractButton;
        }
    }

    private void OnDisable()
    {
        npcInteractionSO.OnInteractableEntered -= ShowInteractButton;
        npcInteractionSO.OnInteractableExited -= HideInteractButton;
    }
    
    private void Start()
    {
        interactPanel.SetActive(false);
    }

    private void Update()
    {
        if (interactPanel.activeSelf && Input.GetKeyDown(KeyCode.E) && currentNpc != null) ExecuteInteraction();
    }

    private void ShowInteractButton(ArenaNpcs npc)
    {
        currentNpc = npc;
        interactText.text = $"[{npc.npcName}]와(과) 경기 시작 베팅을 하시겠습니까?";

        interactButton.onClick.RemoveAllListeners();
        interactButton.onClick.AddListener(ExecuteInteraction);

        interactPanel.SetActive(true);
    }

    private void HideInteractButton()
    {
        currentNpc = null;
        interactPanel.SetActive(false);
    }

    private void ExecuteInteraction()
    {
        if (currentNpc != null)
        {
            currentNpc.BettingStart();
            HideInteractButton();
        }
    }
}
