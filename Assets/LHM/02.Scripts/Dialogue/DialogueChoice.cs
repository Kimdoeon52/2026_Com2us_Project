using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.Dialogue
{
    public class DialogueChoice : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button[] buttons = new Button[0];
        [SerializeField] private TMP_Text[] labels = new TMP_Text[0];

        [Header("표시")]
        [SerializeField] private Color normalColor = new Color32(0xD8, 0xDA, 0xDE, 0xFF);
        [SerializeField] private Color selectedColor = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        [SerializeField] private string selectedMark = "▶ ";
        [SerializeField, Min(0f)] private float inputDelay = 0.5f;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private string hint = "방향키 고르기 · Enter / Z 결정";

        private string[] options = new string[0];
        private Action<int> onChosen;
        private int selected;
        private int cancelIndex = -1;
        private float readyAt;
        private bool waitRelease;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                if (buttons[i] != null) buttons[i].onClick.AddListener(() => Choose(index));
            }
            if (panel != null) panel.SetActive(false);
            if (hintText == null) CreateHint();
        }

        private void CreateHint()
        {
            if (panel == null || string.IsNullOrEmpty(hint)) return;

            TMP_Text source = labels.Length > 0 ? labels[0] : null;
            var go = new GameObject("Hint", typeof(RectTransform));
            go.layer = panel.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel.transform, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(40f, 18f);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (source != null) text.font = source.font;
            text.fontSize = 11f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(0x9A, 0x9E, 0xA4, 0xFF);
            text.raycastTarget = false;
            hintText = text;
        }

        public bool Show(string[] choices, Action<int> chosen, int cancelOption = -1)
        {
            if (IsOpen || choices == null || choices.Length == 0 || choices.Length > buttons.Length) return false;

            options = choices;
            onChosen = chosen;
            cancelIndex = cancelOption;
            selected = 0;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null) buttons[i].gameObject.SetActive(i < options.Length);
            }

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (panel != null) panel.SetActive(true);
            IsOpen = true;
            readyAt = Time.unscaledTime + inputDelay;
            waitRelease = true;
            if (hintText != null) hintText.text = hint;
            Refresh();
            Debug.Log($"[Choice] 선택지: {string.Join(" / ", options)}");
            return true;
        }

        private void Update()
        {
            if (!IsOpen) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (waitRelease)
            {
                if (keyboard.anyKey.isPressed) return;
                waitRelease = false;
            }

            if (Time.unscaledTime < readyAt) return;

            bool up = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            bool down = keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
            bool select = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                          keyboard.zKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame;
            bool cancel = keyboard.escapeKey.wasPressedThisFrame;

            if (up) Move(-1);
            else if (down) Move(1);
            else if (select) Choose(selected);
            else if (cancel && cancelIndex >= 0) Choose(cancelIndex);
        }

        private void Move(int direction)
        {
            selected = (selected + direction + options.Length) % options.Length;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < options.Length && i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                bool isSelected = i == selected;
                labels[i].text = (isSelected ? selectedMark : "") + options[i];
                labels[i].color = isSelected ? selectedColor : normalColor;
            }
        }

        private void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= options.Length) return;

            IsOpen = false;
            if (panel != null) panel.SetActive(false);
            Debug.Log($"[Choice] 선택: {options[index]}");

            Action<int> callback = onChosen;
            onChosen = null;
            callback?.Invoke(index);
        }
    }
}
