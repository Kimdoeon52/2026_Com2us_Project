using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.Dialogue
{
    public enum NpcAction
    {
        HuntApply,
        RankingBoard,
        RewardClaim,
        Talk,
        Shop,
        Exit,
        Craft,
        Repair,
    }

    public class NpcChoiceMenu : MonoBehaviour
    {
        private static readonly NpcAction[] ReceptionistActions = { NpcAction.HuntApply, NpcAction.RankingBoard, NpcAction.RewardClaim, NpcAction.Talk, NpcAction.Exit };
        private static readonly NpcAction[] JunkDealerActions = { NpcAction.Shop, NpcAction.Talk, NpcAction.Exit };
        private static readonly NpcAction[] OtherActions = { NpcAction.Talk, NpcAction.Exit };
        private static readonly NpcAction[] WorkbenchActions = { NpcAction.Craft, NpcAction.Repair, NpcAction.Exit };

        private enum State
        {
            Closed,
            Greeting,
            Menu,
            Talking,
            Shop,
            Window,
        }

        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button optionTemplate;
        [SerializeField] private GameObject shopPanel;

        [Header("외부 상점 창 (연결하면 임시 상점 대신 이 창을 연다)")]
        [SerializeField] private GameObject shopWindow;

        [Header("제작 · 수리 창 (비우면 씬에서 YJW CraftingUIManager를 찾아 씀)")]
        [Tooltip("craftingWindow · fixWindow를 가진 창 관리자. 그 스크립트의 열기 · 닫기 함수를 그대로 불러 인벤토리 배치 등도 같이 처리됨")]
        [SerializeField] private MonoBehaviour craftingUi;

        [Header("표시")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private string selectedMark = "▶ ";
        [Tooltip("선택지 앞에 1. 2. 3. 번호를 붙이고 숫자 키로 바로 고르게 함")]
        [SerializeField] private bool numbered = true;

        private readonly List<Button> buttons = new List<Button>();
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private State state = State.Closed;
        private NpcInteractable npc;
        private NpcAction[] actions = OtherActions;
        private int selected;
        private int stateFrame = -1;
        private int endedFrame = -1;
        private DialogueBox subscribedBox;
        private GameObject openWindow;
        private MonoBehaviour openWindowOwner;
        private string openWindowClose;

        // YJW 제작 · 수리 창 이름 — 그쪽 코드를 직접 참조하지 않고 이름으로 찾음 (바뀌면 콘솔에 안내)
        private const string CraftingUiTypeName = "CraftingUIManager";
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public bool IsBusy => state != State.Closed;
        public bool IsShopOpen => state == State.Shop;
        public bool EndedThisFrame => endedFrame == Time.frameCount;

        public event Action<NpcInteractable, NpcAction> ActionChosen;
        public event Action Ended;

        private void Awake()
        {
            if (manager == null || panel == null || optionTemplate == null)
            {
                Debug.LogError("[NpcChoiceMenu] Manager, Panel, Option Template 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            optionTemplate.gameObject.SetActive(false);
            panel.SetActive(false);
            if (shopPanel != null) shopPanel.SetActive(false);
        }

        private void Start()
        {
            if (!enabled || manager.Box == null) return;

            subscribedBox = manager.Box;
            subscribedBox.Closed += OnDialogueClosed;
        }

        private void OnDestroy()
        {
            if (subscribedBox != null) subscribedBox.Closed -= OnDialogueClosed;
        }

        private void Update()
        {
            if (Time.frameCount == stateFrame) return;

            if (state == State.Menu) HandleMenuInput();
            else if (state == State.Shop) HandleShopInput();
            else if (state == State.Window) HandleWindowInput();
        }

        public void Open(NpcInteractable target)
        {
            if (!enabled || state != State.Closed || target == null) return;

            npc = target;
            actions = GetActions(target.Role);
            BuildButtons();
            selected = 0;

            if (titleText != null) titleText.text = manager.GetSpeakerName(target.SpeakerId);
            ClearUiSelection();
            UiStack.Push(this);

            if (TryPlayGreeting(target)) return;
            ShowMenu();
        }

        // 대화창에 '작업대: ……' 같은 대사를 띄우고 마지막 줄이 다 나오면 그 상태로 선택지를 엶
        private bool TryPlayGreeting(NpcInteractable target)
        {
            if (string.IsNullOrEmpty(target.GreetingPoolId) || manager.Box == null) return false;

            ChangeState(State.Greeting);
            manager.Box.HoldOnLastLine(ShowMenu);
            if (manager.Talk(target.GreetingPoolId)) return true;

            Debug.LogWarning($"[NPC] {target.name}: 인사 대사 풀 '{target.GreetingPoolId}'를 띄우지 못해 선택지만 엽니다.", target);
            manager.Box.CancelHold();
            return false;
        }

        private void ShowMenu()
        {
            if (npc == null) return;

            panel.SetActive(true);
            ChangeState(State.Menu);
            RefreshHighlight();
            Debug.Log($"[NPC] {npc.name} ({npc.Role}) 선택지 열림: {string.Join(" / ", Array.ConvertAll(actions, GetLabel))}");
        }

        // 인사 대사가 떠 있는 채로 선택지를 골랐으면 대화창을 닫음 (Closed 이벤트는 Menu 상태라 End로 이어지지 않음)
        private void CloseGreeting()
        {
            DialogueBox box = manager.Box;
            if (box == null || !box.IsOpen) return;

            box.CancelHold();
            box.Close();
        }

        private static NpcAction[] GetActions(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.GuildReceptionist: return ReceptionistActions;
                case NpcRole.JunkDealer: return JunkDealerActions;
                case NpcRole.Workbench: return WorkbenchActions;
                default: return OtherActions;
            }
        }

        private string GetLabel(NpcAction action)
        {
            switch (action)
            {
                case NpcAction.HuntApply: return manager.GetText("menu.hunt_apply");
                case NpcAction.RankingBoard: return manager.GetText("menu.ranking_board");
                case NpcAction.RewardClaim: return manager.GetText("menu.reward_claim");
                case NpcAction.Talk: return manager.GetText(npc != null && npc.Role == NpcRole.JunkDealer ? "menu.talk_more" : "menu.talk");
                case NpcAction.Shop: return manager.GetText("menu.shop");
                case NpcAction.Craft: return manager.GetText("menu.craft");
                case NpcAction.Repair: return manager.GetText("menu.repair");
                default: return manager.GetText("menu.exit");
            }
        }

        private void BuildButtons()
        {
            while (buttons.Count < actions.Length)
            {
                Button button = Instantiate(optionTemplate, optionTemplate.transform.parent);
                button.navigation = new Navigation { mode = Navigation.Mode.None };

                int index = buttons.Count;
                button.onClick.AddListener(() => Choose(index));

                buttons.Add(button);
                labels.Add(button.GetComponentInChildren<TMP_Text>(true));
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].gameObject.SetActive(i < actions.Length);
            }
        }

        private void RefreshHighlight()
        {
            for (int i = 0; i < actions.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null) continue;

                bool isSelected = i == selected;
                string number = numbered ? $"{i + 1}. " : "";
                label.text = (isSelected ? selectedMark : "") + number + GetLabel(actions[i]);
                label.color = isSelected ? selectedColor : normalColor;
            }
        }

        private void HandleMenuInput()
        {
            bool up, down, select, cancel;
            InputModeController input = InputModeController.Current;

            if (input != null)
            {
                up = input.WasPressed(InputMode.Dialogue, "Up");
                down = input.WasPressed(InputMode.Dialogue, "Down");
                select = input.WasPressed(InputMode.Dialogue, "Select");
                cancel = input.WasPressed(InputMode.Dialogue, "Cancel");
            }
            else
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null) return;

                up = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
                down = keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
                select = keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                         keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                cancel = keyboard.escapeKey.wasPressedThisFrame;
            }

            int digit = numbered ? PressedDigit() : -1;
            if (digit >= 0 && digit < actions.Length)
            {
                selected = digit;
                RefreshHighlight();
                Choose(digit);
            }
            else if (up) Move(-1);
            else if (down) Move(1);
            else if (select) Choose(selected);
            else if (cancel) Choose(Array.IndexOf(actions, NpcAction.Exit));
        }

        // 숫자 키 1~9 (위 숫자줄 · 키패드) → 0부터 시작하는 번호, 없으면 -1
        private static int PressedDigit()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return -1;

            Key[] row = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            Key[] pad = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };
            for (int i = 0; i < row.Length; i++)
            {
                if (keyboard[row[i]].wasPressedThisFrame || keyboard[pad[i]].wasPressedThisFrame) return i;
            }
            return -1;
        }

        private void Move(int direction)
        {
            selected = (selected + direction + actions.Length) % actions.Length;
            RefreshHighlight();
        }

        private void Choose(int index)
        {
            if (state != State.Menu || index < 0 || index >= actions.Length) return;

            NpcAction action = actions[index];
            Debug.Log($"[NPC] 선택: {GetLabel(action)}");

            ClearUiSelection();
            panel.SetActive(false);
            CloseGreeting();
            ActionChosen?.Invoke(npc, action);

            switch (action)
            {
                case NpcAction.Talk:
                    ChangeState(State.Talking);
                    if (!manager.Talk(npc.PoolId)) End();
                    break;

                case NpcAction.Shop:
                    ChangeState(State.Shop);
                    if (shopWindow != null)
                    {
                        shopWindow.SetActive(true);
                        Debug.Log($"[NPC] 상점 열림 ({shopWindow.name}) — 창의 닫기 버튼 또는 Esc로 닫으면 대화 종료");
                        if (!shopWindow.activeInHierarchy) Debug.LogWarning($"[NPC] '{shopWindow.name}'의 부모가 꺼져 있어 화면에 보이지 않습니다.", shopWindow);
                    }
                    else
                    {
                        if (shopPanel != null) shopPanel.SetActive(true);
                        Debug.Log("[NPC] 상점 열림 (임시) — Esc 또는 E로 닫기");
                    }
                    break;

                case NpcAction.Craft:
                    if (!OpenCraftingUi("craftingWindow", "OpenCraftingWindow", "CloseCraftingWindow", "제작")) End();
                    break;

                case NpcAction.Repair:
                    if (!OpenCraftingUi("fixWindow", "OpenFixWindow", "CloseFixWindow", "수리")) End();
                    break;

                case NpcAction.Exit:
                    End();
                    break;

                default:
                    Debug.Log($"[NPC] '{GetLabel(action)}' → 해당 시스템 UI로 넘김 (임시 로그, 시스템 연결 전)");
                    End();
                    break;
            }
        }

        private void HandleShopInput()
        {
            if (shopWindow != null)
            {
                bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                if (escape && shopWindow.activeSelf) shopWindow.SetActive(false);

                if (!shopWindow.activeSelf)
                {
                    Debug.Log($"[NPC] 상점 닫힘 ({shopWindow.name}) → 대화 종료");
                    End();
                }
                return;
            }

            bool close;
            InputModeController input = InputModeController.Current;

            if (input != null)
            {
                close = input.WasPressed(InputMode.Dialogue, "Cancel") || input.WasPressed(InputMode.Dialogue, "Select");
            }
            else
            {
                Keyboard keyboard = Keyboard.current;
                close = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame);
            }

            if (close)
            {
                if (shopPanel != null) shopPanel.SetActive(false);
                Debug.Log("[NPC] 상점 닫힘 → 대화 종료");
                End();
            }
        }

        // 제작 · 수리 창 열기: 창 관리자의 열기 함수를 부르고, 창이 꺼질 때까지 기다렸다가 상호작용 종료
        private bool OpenCraftingUi(string windowField, string openMethod, string closeMethod, string label)
        {
            MonoBehaviour owner = craftingUi != null ? craftingUi : FindCraftingUi();
            if (owner == null)
            {
                Debug.LogWarning($"[NPC] 이 씬에 {label} 창 관리자({CraftingUiTypeName})가 없어 열 수 없습니다. 제작 · 수리 창이 있는 씬인지 확인하세요.", this);
                return false;
            }

            System.Type type = owner.GetType();
            var window = type.GetField(windowField, AnyInstance)?.GetValue(owner) as GameObject;
            MethodInfo open = type.GetMethod(openMethod, AnyInstance, null, System.Type.EmptyTypes, null);
            if (window == null)
            {
                Debug.LogWarning($"[NPC] {type.Name}의 '{windowField}' 칸이 비어 있거나 이름이 바뀌어 {label} 창을 열 수 없습니다.", owner);
                return false;
            }

            if (open != null) open.Invoke(owner, null);
            else Debug.LogWarning($"[NPC] {type.Name}에 '{openMethod}' 함수가 없어 창만 켭니다.", owner);
            if (!window.activeSelf) window.SetActive(true);

            openWindow = window;
            openWindowOwner = owner;
            openWindowClose = closeMethod;
            ChangeState(State.Window);
            Debug.Log($"[NPC] {label} 창 열림 ({window.name}) — 창의 닫기 버튼 또는 Esc로 닫으면 상호작용 종료");
            if (!window.activeInHierarchy) Debug.LogWarning($"[NPC] '{window.name}'의 부모가 꺼져 있어 화면에 보이지 않습니다.", window);
            return true;
        }

        private MonoBehaviour FindCraftingUi()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().Name == CraftingUiTypeName) return craftingUi = behaviour;
            }
            return null;
        }

        private void HandleWindowInput()
        {
            if (openWindow == null)
            {
                End();
                return;
            }

            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (escape && openWindow.activeSelf) CloseOpenWindow();

            if (!openWindow.activeSelf)
            {
                Debug.Log($"[NPC] 창 닫힘 ({openWindow.name}) → 상호작용 종료");
                End();
            }
        }

        // 창 관리자의 닫기 함수를 우선 사용 (제작창은 올려 둔 재료 되돌리기 · 인벤토리 숨기기까지 해 줌)
        private void CloseOpenWindow()
        {
            MethodInfo close = openWindowOwner != null ? openWindowOwner.GetType().GetMethod(openWindowClose, AnyInstance, null, System.Type.EmptyTypes, null) : null;
            if (close != null) close.Invoke(openWindowOwner, null);
            if (openWindow.activeSelf) openWindow.SetActive(false);
        }

        private void OnDialogueClosed()
        {
            if (state == State.Talking || state == State.Greeting) End();
        }

        private void ChangeState(State next)
        {
            state = next;
            stateFrame = Time.frameCount;
        }

        public GameObject ShopWindow
        {
            get => shopWindow;
            set => shopWindow = value;
        }

        private void End()
        {
            panel.SetActive(false);
            if (shopPanel != null) shopPanel.SetActive(false);
            if (state == State.Shop && shopWindow != null && shopWindow.activeSelf) shopWindow.SetActive(false);
            if (state == State.Window && openWindow != null && openWindow.activeSelf) CloseOpenWindow();
            openWindow = null;
            openWindowOwner = null;

            Debug.Log($"[NPC] 상호작용 종료 ({(npc != null ? npc.name : "-")})");
            npc = null;
            state = State.Closed;
            UiStack.Remove(this);
            endedFrame = Time.frameCount;
            Ended?.Invoke();
        }

        private static void ClearUiSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
