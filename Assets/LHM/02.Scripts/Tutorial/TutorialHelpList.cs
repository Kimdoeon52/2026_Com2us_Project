using System;
using System.Collections.Generic;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.Tutorial
{
    // 도움말: 본 튜토리얼 목록에서 골라 다시 보기 (안 본 것은 '???'로 잠김)
    // 프리팹 없이 코드로 만듦 — 색은 RS-OS 톤 (어두운 판 + 노란 강조)
    public class TutorialHelpList : MonoBehaviour
    {
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color Panel = new Color(0.07f, 0.075f, 0.085f, 0.97f);
        private static readonly Color Row = new Color(0.14f, 0.15f, 0.17f, 1f);
        private static readonly Color RowSelected = new Color(0.22f, 0.23f, 0.26f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.898f, 0f, 1f);
        private static readonly Color Text = new Color(0.91f, 0.914f, 0.92f, 1f);
        private static readonly Color Locked = new Color(0.42f, 0.43f, 0.45f, 1f);

        private const float Width = 640f;
        private const float RowHeight = 52f;
        private const float RowGap = 8f;

        private RectTransform listRoot;
        private TMP_FontAsset font;
        private readonly List<(Image back, TMP_Text label, TutorialGuide guide, bool seen)> rows = new List<(Image, TMP_Text, TutorialGuide, bool)>();
        private Action<TutorialGuide> chosen;
        private int selected;
        private float openedAt;

        public bool IsOpen { get; private set; }

        public static TutorialHelpList Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("TutorialHelp", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = Mathf.Max(0, LayerMask.NameToLayer("UI"));

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 245;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var help = go.AddComponent<TutorialHelpList>();
            help.font = font;
            help.Build((RectTransform)go.transform);
            go.SetActive(false);
            return help;
        }

        private void Build(RectTransform root)
        {
            Image dim = Rect("Dim", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            dim.color = Dim;

            RectTransform window = Rect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, 200f));
            window.gameObject.AddComponent<Image>().color = Panel;
            var layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 26, 22);
            layout.spacing = 14f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            window.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform bar = Rect("AccentBar", window, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 4f));
            bar.gameObject.AddComponent<Image>().color = Yellow;
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.pivot = new Vector2(0.5f, 1f);

            Label(window, "도움말 · 튜토리얼 다시 보기", 26f, Yellow, FontStyles.Bold, 36f);

            listRoot = Rect("List", window, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var list = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = RowGap;
            list.childControlHeight = true;
            list.childControlWidth = true;
            list.childForceExpandHeight = false;

            Label(window, "↑↓ 고르기 · Enter 보기 · 클릭 · Esc 닫기", 15f, Locked, FontStyles.Normal, 22f);
        }

        public void Open(List<TutorialGuide> guides, Action<TutorialGuide> onChosen)
        {
            chosen = onChosen;
            BuildRows(guides);
            selected = Mathf.Max(0, rows.FindIndex(r => r.seen));
            gameObject.SetActive(true);
            IsOpen = true;
            openedAt = Time.unscaledTime;
            UiStack.Push(this);
            GamePause.Push(this);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            gameObject.SetActive(false);
            UiStack.Remove(this);
            GamePause.Remove(this);
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        private void BuildRows(List<TutorialGuide> guides)
        {
            foreach (Transform child in listRoot) Destroy(child.gameObject);
            rows.Clear();

            for (int i = 0; i < guides.Count; i++)
            {
                TutorialGuide guide = guides[i];
                bool seen = TutorialPanel.HasSeen(guide.guideId);

                RectTransform row = Rect($"Row {i + 1}", listRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
                Image back = row.gameObject.AddComponent<Image>();
                back.color = Row;

                var button = row.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.interactable = seen;
                int index = i;
                button.onClick.AddListener(() => Choose(index));

                TMP_Text label = Label(row, "", 21f, seen ? Text : Locked, FontStyles.Normal, RowHeight);
                label.rectTransform.offsetMin = new Vector2(20f, 0f);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                rows.Add((back, label, guide, seen));
            }
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime - openedAt < 0.15f) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) Close();
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Move(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Move(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame) Choose(selected);
        }

        private void Move(int direction)
        {
            if (rows.Count == 0) return;
            for (int step = 1; step <= rows.Count; step++)
            {
                int next = (selected + direction * step + rows.Count * step) % rows.Count;
                if (!rows[next].seen) continue;
                selected = next;
                break;
            }
            Refresh();
        }

        private void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= rows.Count || !rows[index].seen) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            TutorialGuide guide = rows[index].guide;
            Close();
            chosen?.Invoke(guide);
        }

        private void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                bool isSelected = i == selected && row.seen;
                row.back.color = isSelected ? RowSelected : Row;
                string name = row.seen ? row.guide.title : "???  (아직 보지 않음)";
                row.label.text = $"{(isSelected ? "▶ " : "   ")}{i + 1}. {name}";
                row.label.color = !row.seen ? Locked : isSelected ? Yellow : Text;
            }
        }

        private TMP_Text Label(RectTransform parent, string value, float size, Color color, FontStyles style, float height)
        {
            RectTransform rect = Rect("Text", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
