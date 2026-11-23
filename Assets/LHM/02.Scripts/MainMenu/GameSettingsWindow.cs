using System.Collections.Generic;
using System.Linq;
using RealSteel.Sound;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealSteel.MainMenu
{
    public class GameSettingsWindow : MonoBehaviour
    {
        public const string VSyncKey = "Switch_RealSteel VSync";

        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2 panelSize = new Vector2(760f, 600f);

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color PanelColor = new Color(0.045f, 0.048f, 0.052f, 0.97f);
        private static readonly Color Accent = new Color(1f, 0.86f, 0.04f, 1f);
        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color SubColor = new Color(1f, 1f, 1f, 0.55f);

        private const float RowHeight = 62f;
        private const float RowGap = 10f;
        private const float HeaderHeight = 96f;
        private const float Padding = 36f;

        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private readonly FullScreenMode[] screenModes = { FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        private readonly string[] screenModeNames = { "전체 화면", "창 모드" };

        private bool built;
        private int resolutionIndex;
        private int screenModeIndex;
        private TMP_Text resolutionText;
        private TMP_Text screenModeText;
        private TMP_Text vsyncText;
        private Slider masterSlider;
        private Slider bgmSlider;
        private Slider sfxSlider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedVSync()
        {
            if (PlayerPrefs.HasKey(VSyncKey)) QualitySettings.vSyncCount = PlayerPrefs.GetString(VSyncKey) == "true" ? 1 : 0;
        }

        private void Awake()
        {
            Build();
        }

        private void OnEnable()
        {
            Build();
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            SoundManager sound = SoundManager.Instance;
            masterSlider.SetValueWithoutNotify(AudioListener.volume);
            bgmSlider.SetValueWithoutNotify(sound != null ? sound.BgmVolume : PlayerPrefs.GetFloat("Slider_" + SoundManager.BgmVolumeKey, 0.7f));
            sfxSlider.SetValueWithoutNotify(sound != null ? sound.SfxVolume : PlayerPrefs.GetFloat("Slider_" + SoundManager.SfxVolumeKey, 1f));
            UpdatePercent(masterSlider);
            UpdatePercent(bgmSlider);
            UpdatePercent(sfxSlider);

            resolutions.Clear();
            resolutions.AddRange(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct());
            if (resolutions.Any(r => ResolutionLimit.IsAllowed(r.x, r.y))) resolutions.RemoveAll(r => !ResolutionLimit.IsAllowed(r.x, r.y));
            resolutions.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
            resolutionIndex = resolutions.FindIndex(r => r.x == Screen.width && r.y == Screen.height);
            resolutionText.text = resolutionIndex >= 0 ? Label(resolutions[resolutionIndex]) : $"{Screen.width} × {Screen.height}";

            screenModeIndex = Screen.fullScreenMode == FullScreenMode.Windowed ? 1 : 0;
            screenModeText.text = screenModeNames[screenModeIndex];
            vsyncText.text = QualitySettings.vSyncCount > 0 ? "켜짐" : "꺼짐";
        }

        private void StepResolution(int step)
        {
            if (resolutions.Count == 0) return;
            resolutionIndex = resolutionIndex < 0 ? resolutions.Count - 1 : (resolutionIndex + step + resolutions.Count) % resolutions.Count;
            Vector2Int r = resolutions[resolutionIndex];
            Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
            resolutionText.text = Label(r);
        }

        private void StepScreenMode(int step)
        {
            screenModeIndex = (screenModeIndex + step + screenModes.Length) % screenModes.Length;
            Screen.fullScreenMode = screenModes[screenModeIndex];
            screenModeText.text = screenModeNames[screenModeIndex];
        }

        private void ToggleVSync(int step)
        {
            bool on = QualitySettings.vSyncCount == 0;
            QualitySettings.vSyncCount = on ? 1 : 0;
            PlayerPrefs.SetString(VSyncKey, on ? "true" : "false");
            vsyncText.text = on ? "켜짐" : "꺼짐";
        }

        private static void SetMaster(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("Slider_" + SoundManager.MasterVolumeKey, AudioListener.volume);
            SoundManager.Instance?.SetMasterVolume(value);
        }

        private static void SetBgm(float value)
        {
            PlayerPrefs.SetFloat("Slider_" + SoundManager.BgmVolumeKey, Mathf.Clamp01(value));
            SoundManager.Instance?.SetBgmVolume(value);
        }

        private static void SetSfx(float value)
        {
            PlayerPrefs.SetFloat("Slider_" + SoundManager.SfxVolumeKey, Mathf.Clamp01(value));
            SoundManager.Instance?.SetSfxVolume(value);
        }

        private static string Label(Vector2Int r) => $"{r.x} × {r.y}";

        private void Build()
        {
            if (built) return;
            built = true;

            var root = (RectTransform)transform;
            Stretch(root, Vector2.zero, Vector2.zero);
            if (!root.TryGetComponent(out Image dim)) dim = root.gameObject.AddComponent<Image>();
            dim.color = Dim;
            dim.raycastTarget = true;

            RectTransform panel = Rect("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = panelSize;
            AddImage(panel, PanelColor);

            RectTransform bar = Rect("TopBar", panel);
            Band(bar, 0f, 6f);
            AddImage(bar, Accent);

            RectTransform title = Rect("Title", panel);
            Band(title, 22f, 44f, Padding, Padding);
            TMP_Text titleText = AddText(title, "환경설정", 34f, TextColor, TextAlignmentOptions.Left);
            titleText.fontStyle = FontStyles.Bold;

            RectTransform english = Rect("English", panel);
            Band(english, 30f, 32f, Padding, Padding);
            AddText(english, "SETTINGS", 18f, Accent, TextAlignmentOptions.Right).characterSpacing = 6f;

            float y = HeaderHeight;
            masterSlider = SliderRow(panel, "전체 음량", ref y, SetMaster);
            bgmSlider = SliderRow(panel, "배경음", ref y, SetBgm);
            sfxSlider = SliderRow(panel, "효과음", ref y, SetSfx);
            screenModeText = SelectorRow(panel, "화면 모드", ref y, StepScreenMode);
            resolutionText = SelectorRow(panel, "해상도", ref y, StepResolution);
            vsyncText = SelectorRow(panel, "수직 동기화", ref y, ToggleVSync);

            RectTransform close = Rect("Close", panel);
            close.anchorMin = close.anchorMax = new Vector2(0.5f, 0f);
            close.pivot = new Vector2(0.5f, 0f);
            close.anchoredPosition = new Vector2(0f, 24f);
            close.sizeDelta = new Vector2(220f, 52f);
            Button closeButton = AddButton(close, "닫기  (Esc)", 22f);
            closeButton.onClick.AddListener(Close);
            closeButton.GetComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.18f);
        }

        private Slider SliderRow(RectTransform panel, string label, ref float y, UnityAction<float> onChanged)
        {
            RectTransform row = Row(panel, label, ref y);

            RectTransform area = Rect("Slider", row);
            area.anchorMin = new Vector2(0.38f, 0.5f);
            area.anchorMax = new Vector2(0.86f, 0.5f);
            area.sizeDelta = new Vector2(0f, 22f);

            RectTransform track = Rect("Track", area);
            Stretch(track, new Vector2(0f, 7f), new Vector2(0f, -7f));
            AddImage(track, TrackColor);

            RectTransform fillArea = Rect("Fill Area", area);
            Stretch(fillArea, new Vector2(0f, 7f), new Vector2(0f, -7f));
            RectTransform fill = Rect("Fill", fillArea);
            Stretch(fill, Vector2.zero, Vector2.zero);
            AddImage(fill, Accent);

            RectTransform handleArea = Rect("Handle Area", area);
            Stretch(handleArea, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            RectTransform handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(16f, 0f);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            Image handleImage = AddImage(handle, TextColor);
            handleImage.raycastTarget = true;

            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            RectTransform percent = Rect("Percent", row);
            percent.anchorMin = new Vector2(0.88f, 0f);
            percent.anchorMax = new Vector2(1f, 1f);
            percent.offsetMin = Vector2.zero;
            percent.offsetMax = new Vector2(-16f, 0f);
            TMP_Text percentText = AddText(percent, "100%", 20f, SubColor, TextAlignmentOptions.Right);

            slider.onValueChanged.AddListener(onChanged);
            slider.onValueChanged.AddListener(v => percentText.text = $"{Mathf.RoundToInt(v * 100f)}%");
            return slider;
        }

        private TMP_Text SelectorRow(RectTransform panel, string label, ref float y, UnityAction<int> onStep)
        {
            RectTransform row = Row(panel, label, ref y);

            RectTransform left = Rect("Prev", row);
            left.anchorMin = left.anchorMax = new Vector2(0.38f, 0.5f);
            left.pivot = new Vector2(0f, 0.5f);
            left.sizeDelta = new Vector2(44f, 40f);
            AddButton(left, "◀", 18f).onClick.AddListener(() => onStep(-1));

            RectTransform right = Rect("Next", row);
            right.anchorMin = right.anchorMax = new Vector2(1f, 0.5f);
            right.pivot = new Vector2(1f, 0.5f);
            right.anchoredPosition = new Vector2(-16f, 0f);
            right.sizeDelta = new Vector2(44f, 40f);
            AddButton(right, "▶", 18f).onClick.AddListener(() => onStep(1));

            RectTransform value = Rect("Value", row);
            value.anchorMin = new Vector2(0.38f, 0f);
            value.anchorMax = new Vector2(1f, 1f);
            value.offsetMin = new Vector2(52f, 0f);
            value.offsetMax = new Vector2(-68f, 0f);
            return AddText(value, "", 22f, TextColor, TextAlignmentOptions.Center);
        }

        private RectTransform Row(RectTransform panel, string label, ref float y)
        {
            RectTransform row = Rect(label, panel);
            Band(row, y, RowHeight, Padding, Padding);
            AddImage(row, RowColor);
            y += RowHeight + RowGap;

            RectTransform mark = Rect("Mark", row);
            mark.anchorMin = new Vector2(0f, 0f);
            mark.anchorMax = new Vector2(0f, 1f);
            mark.pivot = new Vector2(0f, 0.5f);
            mark.sizeDelta = new Vector2(4f, 0f);
            AddImage(mark, Accent);

            RectTransform text = Rect("Label", row);
            text.anchorMin = Vector2.zero;
            text.anchorMax = new Vector2(0.36f, 1f);
            text.offsetMin = new Vector2(22f, 0f);
            text.offsetMax = Vector2.zero;
            AddText(text, label, 24f, TextColor, TextAlignmentOptions.Left);
            return row;
        }

        private Button AddButton(RectTransform rect, string label, float size)
        {
            Image image = AddImage(rect, ButtonColor);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.6f, 1f);
            colors.pressedColor = Accent;
            button.colors = colors;

            RectTransform text = Rect("Text", rect);
            Stretch(text, Vector2.zero, Vector2.zero);
            AddText(text, label, size, TextColor, TextAlignmentOptions.Center);
            return button;
        }

        private static void UpdatePercent(Slider slider)
        {
            var text = slider.transform.parent.Find("Percent")?.GetComponent<TMP_Text>();
            if (text != null) text.text = $"{Mathf.RoundToInt(slider.value * 100f)}%";
        }

        private RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Band(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text AddText(RectTransform rect, string value, float size, Color color, TextAlignmentOptions alignment)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
