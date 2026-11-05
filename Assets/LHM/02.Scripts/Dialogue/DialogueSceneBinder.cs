using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue
{
    [DefaultExecutionOrder(-200)]
    public class DialogueSceneBinder : MonoBehaviour
    {
        [Header("씬 연결 — 대화 툴의 '씬 연결'이 채운다")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform player;
        [SerializeField] private GameObject shopWindow;
        [SerializeField] private Behaviour[] movement = new Behaviour[0];
        [SerializeField] private Behaviour[] interactions = new Behaviour[0];
        [SerializeField] private GameObject[] hiddenPrompts = new GameObject[0];
        [SerializeField] private Behaviour[] uiHotkeys = new Behaviour[0];

        [Header("프리팹 내부")]
        [SerializeField] private TMP_Text interactPrompt;

        private void Awake()
        {
            if (gameplayCamera == null || player == null)
            {
                Debug.LogError("[SceneBinder] 플레이어 카메라 또는 플레이어가 비어 있습니다. 대화 툴의 '씬 연결'을 실행하세요.", this);
            }

            BindCamera();
            BindPlayer();
            BindShopWindow();
        }

        private void BindCamera()
        {
            if (gameplayCamera == null) return;

            BubbleManager bubbles = GetComponentInChildren<BubbleManager>(true);
            if (bubbles != null) bubbles.BindCamera(gameplayCamera);

            DialogueDirector director = GetComponentInChildren<DialogueDirector>(true);
            if (director != null) director.BindCamera(gameplayCamera);

            NpcTalkCamera talkCamera = GetComponentInChildren<NpcTalkCamera>(true);
            if (talkCamera != null) talkCamera.BindCamera(gameplayCamera);
        }

        private void BindPlayer()
        {
            if (player == null) return;

            NpcBubbles npcBubbles = GetComponentInChildren<NpcBubbles>(true);
            if (npcBubbles != null) npcBubbles.BindPlayer(player);

            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null) interactor = player.gameObject.AddComponent<PlayerInteractor>();

            interactor.Bind(
                GetComponentInChildren<DialogueManager>(true),
                GetComponentInChildren<NpcChoiceMenu>(true),
                interactPrompt,
                gameplayCamera,
                GetComponentInChildren<NpcTalkCamera>(true));

            FieldInputBridge bridge = GetComponentInChildren<FieldInputBridge>(true);
            if (bridge != null) bridge.BindScene(interactor, movement, interactions, hiddenPrompts, uiHotkeys);
        }

        private void BindShopWindow()
        {
            if (shopWindow == null) return;

            NpcChoiceMenu menu = GetComponentInChildren<NpcChoiceMenu>(true);
            if (menu != null) menu.ShopWindow = shopWindow;
        }
    }
}
