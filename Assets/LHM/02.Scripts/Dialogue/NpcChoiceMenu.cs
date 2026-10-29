using System;
using System.Collections.Generic;
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
    }

    public class NpcChoiceMenu : MonoBehaviour
    {
        private static readonly NpcAction[] ReceptionistActions = { NpcAction.HuntApply, NpcAction.RankingBoard, NpcAction.RewardClaim, NpcAction.Talk, NpcAction.Exit };
        private static readonly NpcAction[] JunkDealerActions = { NpcAction.Shop, NpcAction.Talk, NpcAction.Exit };
        private static readonly NpcAction[] OtherActions = { NpcAction.Talk, NpcAction.Exit };

        private enum State
        {
            Closed,
            Menu,
            Talking,
            Shop,
        }

        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button optionTemplate;
        [SerializeField] private GameObject shopPanel;

        [Header("표시")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private string selectedMark = "▶ ";

        private readonly List<Button> buttons = new List<Button>();
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private State state = State.Closed;
        private NpcInteractable npc;
        private NpcAction[] actions = OtherActions;
        private int selected;
        private int stateFrame = -1;
        private int endedFrame = -1;
        private DialogueBox subscribedBox;

        public bool IsBusy => state != State.Closed;
        public bool EndedThisFrame => endedFrame == Time.frameCount;

        public event Action<NpcInteractable, NpcAction> ActionChosen;

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
            panel.SetActive(true);
            ChangeState(State.Menu);
            RefreshHighlight();

            Debug.Log($"[NPC] {target.name} ({target.Role}) 선택지 열림: {string.Join(" / ", Array.ConvertAll(actions, GetLabel))}");
        }

        private static NpcAction[] GetActions(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.GuildReceptionist: return ReceptionistActions;
                case NpcRole.JunkDealer: return JunkDealerActions;
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
                label.text = (isSelected ? selectedMark : "") + GetLabel(actions[i]);
                label.color = isSelected ? selectedColor : normalColor;
            }
        }

        private void HandleMenuInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                Move(-1);
            }
            else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                Move(1);
            }
            else if (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                     keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Choose(selected);
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Choose(Array.IndexOf(actions, NpcAction.Exit));
            }
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
            ActionChosen?.Invoke(npc, action);

            switch (action)
            {
                case NpcAction.Talk:
                    ChangeState(State.Talking);
                    if (!manager.Talk(npc.PoolId)) End();
                    break;

                case NpcAction.Shop:
                    ChangeState(State.Shop);
                    if (shopPanel != null) shopPanel.SetActive(true);
                    Debug.Log("[NPC] 상점 열림 (임시) — Esc 또는 E로 닫기");
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
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                if (shopPanel != null) shopPanel.SetActive(false);
                Debug.Log("[NPC] 상점 닫힘 → 대화 종료");
                End();
            }
        }

        private void OnDialogueClosed()
        {
            if (state == State.Talking) End();
        }

        private void ChangeState(State next)
        {
            state = next;
            stateFrame = Time.frameCount;
        }

        private void End()
        {
            panel.SetActive(false);
            if (shopPanel != null) shopPanel.SetActive(false);

            Debug.Log($"[NPC] 상호작용 종료 ({(npc != null ? npc.name : "-")})");
            npc = null;
            state = State.Closed;
            endedFrame = Time.frameCount;
        }

        private static void ClearUiSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
