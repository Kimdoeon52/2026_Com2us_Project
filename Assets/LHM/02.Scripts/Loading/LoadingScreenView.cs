using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.Loading
{
    public class LoadingScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite ticks;
        [SerializeField] private Sprite chamfer;
        [SerializeField] private Vector2 referenceSize = new Vector2(1920f, 1080f);
        [SerializeField, Min(0.1f)] private float progressSpeed = 1.5f;
        [SerializeField] private bool waitForAnyKey = true;
        [SerializeField, Min(0.1f)] private float blinkSeconds = 1f;
        [SerializeField, Min(0f)] private float inputDelay = 0.25f;

        [SerializeField] private string[] tips =
        {
            "시즌이 끝나면 길드 접수원에게서 랭킹 포상을 받을 수 있습니다.",
            "같은 보스에게 세 번 지면, 다음 도전 때 공략 힌트를 들을 수 있습니다.",
            "고철 더미 근처에서 E를 눌러 부품을 모으세요.",
            "작업대에서 모은 부품으로 파츠를 제작하고 수리할 수 있습니다.",
            "Esc로 일시정지, Home으로 지금 씬의 단축키를 볼 수 있습니다.",
            "파츠 내구도가 바닥나면 전투 중 부서집니다. 출발 전에 정비하세요.",
        };

        private static readonly Dictionary<string, string> SceneLabels = new Dictionary<string, string>
        {
            { "Main_Home_Workshop", "거점" }, { "Home_Workshop", "거점" },
            { "Main_Junk_Shop", "고물상" }, { "Map_Test", "고물상" }, { "JunkShop_Map", "고물상" },
            { "Main_Auction_Hall", "경매장" }, { "Auction", "경매장" },
            { "Main_League_Lobby", "길드" }, { "League_Lobby", "길드" },
            { "Main_Fight_Map_1", "경기장" }, { "Main_Fight_Map_2", "경기장" },
            { "Main_Boss1", "보스전" }, { "Battle", "보스전" },
            { "MainMenu", "메인 메뉴" }, { "OpeningCutscene", "프롤로그" },
        };

        private static readonly Color Background = new Color(0.045f, 0.048f, 0.052f, 1f);
        private static readonly Color Accent = new Color(1f, 0.86f, 0.04f, 1f);
        private static readonly Color Cyan = new Color(0.31f, 0.76f, 0.97f, 1f);
        private static readonly Color Sub = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color Track = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.06f);

        private RectTransform root;
        private RectTransform design;
        private RectTransform fill;
        private RectTransform spinner;
        private TMP_Text percentText;
        private TMP_Text statusText;
        private TMP_Text destinationText;
        private TMP_Text tipText;
        private TMP_Text promptText;
        private float shown;
        private float time;
        private float readyTime = -1f;
        private bool continued;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            LoadingManager.HoldUntilContinue = false;
        }

        private void Awake()
        {
            Build();
            LoadingManager.HoldUntilContinue = waitForAnyKey;
        }

        private void OnEnable()
        {
            Build();
            LoadingManager.HoldUntilContinue = waitForAnyKey;
            shown = 0f;
            time = 0f;
            readyTime = -1f;
            continued = false;
            if (tips != null && tips.Length > 0) tipText.text = tips[Random.Range(0, tips.Length)];
            Refresh();
        }

        private void OnDestroy()
        {
            LoadingManager.HoldUntilContinue = false;
            LoadingManager.Continue();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            shown = Mathf.MoveTowards(shown, Mathf.Max(shown, LoadingManager.Progress), dt * progressSpeed);

            bool ready = shown >= 1f && LoadingManager.IsWaitingForContinue && !continued;
            if (ready && readyTime < 0f) readyTime = time;
            if (ready && time - readyTime >= inputDelay && AnyInput())
            {
                continued = true;
                LoadingManager.Continue();
            }

            Refresh();
        }

        private static bool AnyInput()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
            Gamepad pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        private void Refresh()
        {
            FitDesign();

            bool waiting = readyTime >= 0f && !continued;
            fill.anchorMax = new Vector2(Mathf.Clamp01(shown), 1f);
            percentText.text = $"{Mathf.RoundToInt(shown * 100f):000}<size=50%>%</size>";
            statusText.text = waiting ? "준비 완료" : continued ? "이동 중" + Dots() : "불러오는 중" + Dots();
            spinner.localRotation = Quaternion.Euler(0f, 0f, 45f + (waiting ? 0f : time * 180f));

            promptText.gameObject.SetActive(waiting);
            if (waiting)
            {
                float phase = (time - readyTime) / blinkSeconds * Mathf.PI * 2f;
                promptText.alpha = 0.2f + 0.8f * (0.5f + 0.5f * Mathf.Cos(phase));
            }

            string target = LoadingManager.TargetScene;
            string label = !string.IsNullOrEmpty(target) && SceneLabels.TryGetValue(target, out string name) ? name : target;
            destinationText.text = string.IsNullOrEmpty(label) ? "" : $"→  {label}";
        }

        private string Dots() => new string('.', 1 + (int)(time * 3f) % 3);

        private void FitDesign()
        {
            Rect area = root.rect;
            float scale = Mathf.Min(area.width / referenceSize.x, area.height / referenceSize.y);
            if (scale > 0f) design.localScale = new Vector3(scale, scale, 1f);
        }

        private void Build()
        {
            if (root != null) return;

            root = Rect("[LoadingScreen]", transform);
            Stretch(root);
            root.SetAsLastSibling();
            AddImage(root, Background, null);

            design = Rect("Design", root);
            design.anchorMin = design.anchorMax = new Vector2(0.5f, 0.5f);
            design.sizeDelta = referenceSize;

            RectTransform watermark = Rect("Watermark", design);
            watermark.anchorMin = watermark.anchorMax = watermark.pivot = new Vector2(1f, 0f);
            watermark.sizeDelta = new Vector2(1900f, 380f);
            watermark.anchoredPosition = new Vector2(-20f, -40f);
            TMP_Text mark = AddText(watermark, "LOADING", 360f, Faint, TextAlignmentOptions.BottomRight);
            mark.fontStyle = FontStyles.Bold;

            RectTransform side = Place(Rect("SideBar", design), 96f, 120f, 6f, 840f);
            AddImage(side, Accent, null);

            RectTransform brand = Place(Rect("Brand", design), 130f, 110f, 900f, 36f);
            AddText(brand, "RE:AL STEEL   /   SYSTEM", 22f, Sub, TextAlignmentOptions.Left).characterSpacing = 6f;

            RectTransform title = Place(Rect("Title", design), 122f, 150f, 1200f, 170f);
            TMP_Text titleText = AddText(title, "LOADING", 150f, Color.white, TextAlignmentOptions.Left);
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 4f;

            RectTransform underline = Place(Rect("Underline", design), 130f, 330f, 190f, 10f);
            AddImage(underline, Accent, null);

            spinner = Place(Rect("Spinner", design), 148f, 400f, 26f, 26f, new Vector2(0.5f, 0.5f));
            AddImage(spinner, Accent, null);

            RectTransform status = Place(Rect("Status", design), 186f, 372f, 420f, 56f);
            statusText = AddText(status, "불러오는 중", 44f, Color.white, TextAlignmentOptions.Left);

            RectTransform destination = Place(Rect("Destination", design), 520f, 378f, 700f, 48f);
            destinationText = AddText(destination, "", 34f, Cyan, TextAlignmentOptions.Left);

            RectTransform tip = Place(Rect("TipPanel", design), 130f, 690f, 1000f, 120f);
            Image tipImage = AddImage(tip, PanelColor, chamfer);
            if (chamfer != null) tipImage.type = Image.Type.Sliced;
            RectTransform tipLabel = Place(Rect("TipLabel", tip), 28f, 18f, 200f, 30f);
            AddText(tipLabel, "TIP_", 22f, Accent, TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            RectTransform tipBody = Place(Rect("TipBody", tip), 28f, 52f, 944f, 56f);
            tipText = AddText(tipBody, "", 26f, Color.white, TextAlignmentOptions.TopLeft);
            tipText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform nowLoading = Place(Rect("NowLoading", design), 130f, 846f, 600f, 34f);
            AddText(nowLoading, "NOW LOADING", 22f, Sub, TextAlignmentOptions.Left).characterSpacing = 6f;

            RectTransform percent = Place(Rect("Percent", design), 1290f, 800f, 500f, 80f);
            percentText = AddText(percent, "000%", 72f, Color.white, TextAlignmentOptions.BottomRight);
            percentText.fontStyle = FontStyles.Bold;

            if (ticks != null)
            {
                RectTransform tickRow = Place(Rect("Ticks", design), 130f, 890f, 1660f, 24f);
                Image tickImage = AddImage(tickRow, new Color(1f, 1f, 1f, 0.35f), ticks);
                tickImage.type = Image.Type.Tiled;
            }

            RectTransform track = Place(Rect("Track", design), 130f, 924f, 1660f, 12f);
            AddImage(track, Track, null);
            fill = Rect("Fill", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            AddImage(fill, Accent, null);

            RectTransform prompt = Place(Rect("Prompt", design), 360f, 960f, 1200f, 50f);
            promptText = AddText(prompt, "아무 키나 눌러 계속하기   <size=60%><color=#FFDB0A>PRESS ANY KEY</color></size>", 34f, Color.white, TextAlignmentOptions.Center);
            promptText.characterSpacing = 2f;
            prompt.gameObject.SetActive(false);

            RectTransform footer = Place(Rect("Footer", design), 1290f, 1030f, 500f, 30f);
            AddText(footer, $"BUILD {Application.version}", 18f, Sub, TextAlignmentOptions.Right).characterSpacing = 4f;
        }

        private static RectTransform Place(RectTransform rect, float x, float top, float width, float height, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = pivot ?? new Vector2(0f, 1f);
            Vector2 p = rect.pivot;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x + width * p.x, -top - height * (1f - p.y));
            return rect;
        }

        private RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
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
