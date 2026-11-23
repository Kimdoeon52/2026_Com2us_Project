using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue
{
    [DefaultExecutionOrder(-200)]
    public class DialogueSceneBinder : MonoBehaviour
    {
        private static readonly List<DialogueSceneBinder> live = new List<DialogueSceneBinder>();

        private static readonly string[] MovementTypes = { "RSSimpleCharacter" };
        private static readonly string[] InteractionTypes = { "ScrapInteractor" };
        private static readonly string[] PromptTypes = { "ScrapInteractPrompt" };
        private static readonly string[] UiHotkeyTypes = { "InventoryToggle", "CraftingUIManager" };
        private const string CraftingOwnerType = "CraftingUIManager";
        private const string ShopWindowName = "ShopWindow";
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Header("씬 연결 — 비우면 실행할 때 자동으로 찾음")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform player;
        [SerializeField] private GameObject shopWindow;
        [SerializeField] private Behaviour[] movement = new Behaviour[0];
        [SerializeField] private Behaviour[] interactions = new Behaviour[0];
        [SerializeField] private GameObject[] hiddenPrompts = new GameObject[0];
        [SerializeField] private Behaviour[] uiHotkeys = new Behaviour[0];

        [Header("프리팹 내부")]
        [SerializeField] private TMP_Text interactPrompt;

        private bool registered;

        private void Awake()
        {
            DialogueSceneBinder other = live.FirstOrDefault(b => b != null && b != this && b.gameObject.scene == gameObject.scene);
            if (other != null)
            {
                Debug.LogWarning($"[SceneBinder] '{gameObject.scene.name}' 씬에 대화 시스템이 이미 있어 '{name}'을 지웁니다. 씬에는 대화 시스템 프리팹을 하나만 두세요.", other);
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            live.Add(this);
            registered = true;

            FindSceneObjects();
            if (gameplayCamera == null || player == null)
            {
                Debug.LogError($"[SceneBinder] 플레이어 카메라({NameOf(gameplayCamera)}) 또는 플레이어({NameOf(player)})를 찾지 못했습니다. 플레이어에 RSSimpleCharacter 또는 'Player' 태그가 있는지 확인하세요. 찾지 못하면 Binder 칸에 직접 넣으세요.", this);
            }

            BindCamera();
            BindPlayer();
            BindShopWindow();
        }

        private void OnDestroy()
        {
            if (registered) live.Remove(this);
        }

        private void FindSceneObjects()
        {
            Behaviour[] behaviours = FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (player == null) player = FindPlayer(behaviours);
            if (gameplayCamera == null) gameplayCamera = FindCamera(player);
            if (shopWindow == null) shopWindow = FindShopWindow(behaviours);

            if (movement.Length == 0 && player != null) movement = player.GetComponents<Behaviour>().Where(b => IsType(b, MovementTypes)).ToArray();

            Behaviour[] prompts = behaviours.Where(b => IsType(b, PromptTypes)).ToArray();
            if (interactions.Length == 0) interactions = behaviours.Where(b => IsType(b, InteractionTypes)).Concat(prompts).ToArray();
            if (hiddenPrompts.Length == 0) hiddenPrompts = prompts.Select(p => ReadField(p, "_root") as GameObject).Where(g => g != null).ToArray();
            if (uiHotkeys.Length == 0) uiHotkeys = behaviours.Where(b => IsType(b, UiHotkeyTypes)).ToArray();
        }

        private static Transform FindPlayer(Behaviour[] behaviours)
        {
            Behaviour mover = behaviours.FirstOrDefault(b => IsType(b, MovementTypes) && b.gameObject.activeInHierarchy)
                              ?? behaviours.FirstOrDefault(b => IsType(b, MovementTypes));
            if (mover != null) return mover.transform;

            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            return tagged != null ? tagged.transform : null;
        }

        private Camera FindCamera(Transform target)
        {
            if (target != null)
            {
                foreach (Behaviour mover in target.GetComponents<Behaviour>().Where(b => IsType(b, MovementTypes)))
                {
                    if (ReadField(mover, "cam") is Camera cam && cam != null) return cam;
                }
            }

            var closeUps = new HashSet<Camera>(GetComponentsInChildren<Camera>(true));
            int playerMask = target != null ? 1 << target.gameObject.layer : 1;
            Camera best = FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c.targetTexture == null && c.enabled && !closeUps.Contains(c))
                .OrderByDescending(c => (c.cullingMask & playerMask) != 0)
                .ThenByDescending(c => c.CompareTag("MainCamera"))
                .ThenByDescending(c => c.depth)
                .FirstOrDefault();
            return best != null ? best : Camera.main;
        }

        private static GameObject FindShopWindow(Behaviour[] behaviours)
        {
            foreach (Behaviour behaviour in behaviours.Where(b => IsType(b, new[] { CraftingOwnerType })))
            {
                if (ReadField(behaviour, "shopWindow") is GameObject window && window != null) return window;
            }

            return behaviours.Select(b => b.transform.root).Distinct()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .FirstOrDefault(g => g.name == ShopWindowName);
        }

        private static bool IsType(Behaviour behaviour, string[] typeNames)
        {
            return behaviour != null && typeNames.Contains(behaviour.GetType().Name);
        }

        private static object ReadField(object owner, string field)
        {
            return owner?.GetType().GetField(field, AnyInstance)?.GetValue(owner);
        }

        private static string NameOf(Object value)
        {
            return value != null ? value.name : "없음";
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
