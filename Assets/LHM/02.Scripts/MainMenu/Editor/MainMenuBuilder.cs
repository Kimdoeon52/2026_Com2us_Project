using System.Collections.Generic;
using System.Text;
using Michsky.UI.Heat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using static RealSteel.MainMenu.EditorTools.HeatKit;

namespace RealSteel.MainMenu.EditorTools
{
    internal static class MainMenuBuilder
    {
        internal const string GameTitle = "REAL STEEL";
        private const string Placeholders = Root + "Textures/Placeholders/Background/";
        private const string MixerPath = Root + "Audio/_Mixer.mixer";

        internal const float SideMargin = 75f;
        internal const float BoxMarginY = 190f;
        private const float LeftColumnWidth = 600f;
        private const float ColumnSpacing = 30f;

        internal static readonly Color Dark = new Color(0.098f, 0.137f, 0.176f, 1f);

        internal sealed class Context
        {
            public UIManager ui;
            public Transform canvas;
            public MainMenuController controller;
            public GraphicsManager graphics;
            public PanelManager panels;
            public StringBuilder log;
        }

        internal static GameObject Build(UIManager ui, CreditsPreset creditsPreset, StringBuilder log)
        {
            GameObject canvasGo = Prefab("UI Elements/Canvas/Canvas.prefab", null, "[MainMenu] Canvas");
            var ctx = new Context { ui = ui, canvas = canvasGo.transform, log = log };

            BuildBackground(ctx);
            Transform managers = Node("Managers", ctx.canvas);
            ctx.controller = new GameObject("Main Menu", typeof(MainMenuController)).GetComponent<MainMenuController>();
            ctx.controller.transform.SetParent(managers, false);
            ctx.graphics = new GameObject("Graphics Manager", typeof(GraphicsManager)).GetComponent<GraphicsManager>();
            ctx.graphics.transform.SetParent(managers, false);
            BuildAudio(ctx, managers);

            RectTransform mainContent = Node("Main Content", ctx.canvas);
            ctx.panels = mainContent.gameObject.AddComponent<PanelManager>();

            RectTransform home = Panel("Home Panel", mainContent);
            RectTransform settings = Panel("Settings Panel", mainContent);
            BuildTopBar(ctx, mainContent, out PanelButton homeTab, out PanelButton settingsTab);
            BoxButtonManager settingsBox = BuildHome(ctx, home, out BoxButtonManager continueBox, out Transform homeHotkeys);
            Transform settingsHotkeys = MainMenuSettingsBuilder.Build(ctx, settings);

            ctx.panels.panels = new List<PanelManager.PanelItem>
            {
                new PanelManager.PanelItem { panelName = MainMenuController.HomePanel, panelObject = home.GetComponent<Animator>(), panelButton = homeTab, hotkeyParent = homeHotkeys },
                new PanelManager.PanelItem { panelName = MainMenuController.SettingsPanel, panelObject = settings.GetComponent<Animator>(), panelButton = settingsTab, altBoxButton = settingsBox, hotkeyParent = settingsHotkeys }
            };
            SetEnum(ctx.panels, "panelMode", (int)PanelManager.PanelMode.MainPanel);

            CreditsManager credits = BuildCredits(ctx, creditsPreset);
            ModalWindowManager quit = BuildQuitWindow(ctx);
            Animator splash = BuildSplash(ctx);
            ImageFading init = BuildInitScreen(ctx);

            var menu = new GameObject("Menu Manager", typeof(MenuManager)).GetComponent<MenuManager>();
            menu.transform.SetParent(managers, false);
            menu.UIManagerAsset = ui;
            menu.splashScreen = splash;
            Set(menu, "mainContent", mainContent.gameObject);
            Set(menu, "initPanel", init);
            Listen(Find<HotkeyEvent>(splash.gameObject, "Hotkeys/Press Any Key").onHotkeyPress, menu.DisableSplashScreen);

            Set(ctx.controller, "mainPanels", ctx.panels);
            Set(ctx.controller, "credits", credits);
            Set(ctx.controller, "quitWindow", quit);
            Set(ctx.controller, "continueButton", continueBox);

            log.AppendLine($"- Heat UI Manager 연결: {BindUIManager(canvasGo, ui)}곳, Roboto → 나눔바른고딕: {ReplaceHeatFonts(canvasGo, ui)}곳");
            return canvasGo;
        }

        private static void BuildBackground(Context ctx)
        {
            RectTransform root = Node("Background", ctx.canvas);
            Paint(root.gameObject, Dark);

            RectTransform imageRt = Node("Image", root);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Placeholders + "Placeholder Image.png");
            Paint(imageRt.gameObject, Color.white, sprite);
            if (sprite != null)
            {
                var fitter = imageRt.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }

            Paint(Node("Filter", root).gameObject, new Color(Dark.r, Dark.g, Dark.b, 0.72f));
        }

        private static void BuildAudio(Context ctx, Transform parent)
        {
            var go = new GameObject("Audio Manager", typeof(AudioSource));
            go.transform.SetParent(parent, false);
            var source = go.GetComponent<AudioSource>();
            source.playOnAwake = false;

            var audio = go.AddComponent<UIManagerAudio>();
            audio.UIManagerAsset = ctx.ui;
            audio.audioSource = source;
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer != null) Set(audio, "audioMixer", mixer);
        }

        private static RectTransform Panel(string name, Transform parent)
        {
            RectTransform root = Node(name, parent);
            Add<CanvasGroup>(root.gameObject);
            UseAnimator(root.gameObject, "Panels/Main Panel/MainPanel.controller");
            Node("Content", root);
            return root;
        }

        private static void BuildTopBar(Context ctx, Transform mainContent, out PanelButton homeTab, out PanelButton settingsTab)
        {
            RectTransform bar = Node("Top Panel", mainContent);
            Place(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -SideMargin), new Vector2(-SideMargin * 2, 50));
            var group = Add<CanvasGroup>(bar.gameObject);
            Animator animator = UseAnimator(bar.gameObject, "Panels/Navigation Bar/NavigationBar.controller");
            var nav = bar.gameObject.AddComponent<NavigationBar>();

            RectTransform content = Node("Content", bar);
            var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 30;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            RectTransform logo = Node("Logo", content);
            logo.sizeDelta = new Vector2(320, 50);
            Caption(logo.gameObject, GameTitle, 40, TextAlignmentOptions.MidlineLeft, ctx.ui, UIManagerText.FontType.Bold);

            RectTransform tabs = Node("Panel Buttons", content);
            tabs.sizeDelta = new Vector2(600, 50);
            var tabRow = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabRow.spacing = 15;
            tabRow.childAlignment = TextAnchor.MiddleLeft;
            tabRow.childControlWidth = tabRow.childControlHeight = false;
            tabRow.childForceExpandWidth = tabRow.childForceExpandHeight = false;

            homeTab = Tab(tabs, "Home", "홈");
            settingsTab = Tab(tabs, "Settings", "설정");
            PanelButton creditsTab = Tab(tabs, "Credits", "크레딧");
            Listen(creditsTab.onClick, ctx.controller.OpenCredits);

            GameObject exit = Prefab("UI Elements/Button/Button (Radial Icon Only).prefab", bar, "Exit");
            Place((RectTransform)exit.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(50, 50));
            Listen(exit.GetComponent<ButtonManager>().onClick, ctx.controller.RequestQuit);

            Set(nav, "animator", animator);
            Set(nav, "canvasGroup", group);
            Set(nav, "buttonParent", tabs);
        }

        private static PanelButton Tab(Transform parent, string name, string label)
        {
            var button = Prefab("UI Elements/Button/Button (Panel).prefab", parent, name).GetComponent<PanelButton>();
            button.buttonText = label;
            button.useLocalization = false;
            button.UpdateUI();
            return button;
        }

        private static BoxButtonManager BuildHome(Context ctx, RectTransform home, out BoxButtonManager continueBox, out Transform hotkeys)
        {
            Transform content = home.Find("Content");
            RectTransform container = Node("Box Container", content);
            Stretch(container, SideMargin, SideMargin, BoxMarginY, BoxMarginY);
            container.gameObject.AddComponent<BoxContainer>();

            RectTransform left = Column("Row 1", container);
            Place(left, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(LeftColumnWidth / 2, 0), new Vector2(LeftColumnWidth, 0));
            RectTransform right = Column("Row 2", container);
            Stretch(right, LeftColumnWidth + ColumnSpacing, 0, 0, 0);

            BoxButtonManager newGame = Box(left, "New Game", "새 게임", "오프닝부터 이야기를 시작합니다", "Placeholder Image 2.png");
            Listen(newGame.onClick, ctx.controller.NewGame);
            continueBox = Box(left, "Continue", "이어하기", "저장된 진행이 없습니다", "Placeholder Image 3.png");
            Listen(continueBox.onClick, ctx.controller.Continue);
            continueBox.isInteractable = false;
            continueBox.UpdateUI();

            BoxButtonManager settings = Box(right, "Settings", "설정", "화면 · 사운드", "Placeholder Image 4.png");
            BoxButtonManager credits = Box(right, "Credits", "크레딧", "만든 사람들", "Placeholder Image 5.png");
            Listen(credits.onClick, ctx.controller.OpenCredits);

            hotkeys = Hotkeys(content);
            HotkeyEvent quitKey = Hotkey(hotkeys, "Quit", "종료");
            Listen(quitKey.onHotkeyPress, ctx.controller.RequestQuit);
            return settings;
        }

        private static RectTransform Column(string name, Transform parent)
        {
            RectTransform column = Node(name, parent);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ColumnSpacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            return column;
        }

        private static BoxButtonManager Box(Transform parent, string name, string title, string description, string background)
        {
            var box = Prefab("UI Elements/Button/Button (Box).prefab", parent, name).GetComponent<BoxButtonManager>();
            box.buttonTitle = title;
            box.buttonDescription = description;
            box.useLocalization = false;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Placeholders + background);
            if (sprite != null) box.buttonBackground = sprite;
            box.UpdateUI();
            return box;
        }

        internal static Transform Hotkeys(Transform content)
        {
            RectTransform row = Node("Hotkeys", content);
            Place(row, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, SideMargin), new Vector2(-SideMargin * 2, 50));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 50;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        internal static HotkeyEvent Hotkey(Transform parent, string name, string label)
        {
            var hotkey = Prefab("UI Elements/Input/Hotkey Indicator.prefab", parent, name).GetComponent<HotkeyEvent>();
            hotkey.keyID = "Escape";
            hotkey.hotkeyLabel = label;
            hotkey.useLocalization = false;
            hotkey.hotkey = KeyAction(name, "<Keyboard>/escape", "<Gamepad>/buttonEast");
            hotkey.UpdateUI();
            return hotkey;
        }

        private static CreditsManager BuildCredits(Context ctx, CreditsPreset preset)
        {
            GameObject go = Prefab("Panels/Credits.prefab", ctx.canvas, "Credits");
            var credits = go.GetComponent<CreditsManager>();
            Set(credits, "creditsPreset", preset);
            go.SetActive(false);
            return credits;
        }

        private static ModalWindowManager BuildQuitWindow(Context ctx)
        {
            GameObject go = Prefab("UI Elements/Modal Window/Modal Window.prefab", ctx.canvas, "Quit Window");
            var modal = go.GetComponent<ModalWindowManager>();
            modal.titleText = "게임 종료";
            modal.descriptionText = "게임을 종료할까요?";
            modal.useLocalization = false;
            Label(modal.confirmButton, "종료");
            Label(modal.cancelButton, "취소");
            modal.UpdateUI();
            Listen(modal.onConfirm, ctx.controller.Quit);
            return modal;
        }

        private static void Label(ButtonManager button, string text)
        {
            if (button == null) return;
            button.buttonText = text;
            button.useLocalization = false;
            button.UpdateUI();
        }

        private static Animator BuildSplash(Context ctx)
        {
            RectTransform root = Node("Splash Screen", ctx.canvas);
            Add<CanvasGroup>(root.gameObject);
            Animator animator = UseAnimator(root.gameObject, "Panels/Splash Screen/SplashScreenController.controller");
            var splash = root.gameObject.AddComponent<UIManagerSplashScreen>();

            Paint(Node("Shadow", root).gameObject, new Color(Dark.r, Dark.g, Dark.b, 0.55f));

            RectTransform hotkeys = Node("Hotkeys", root);
            RectTransform anyKey = Node("Press Any Key", hotkeys);
            var press = anyKey.gameObject.AddComponent<HotkeyEvent>();
            press.hotkeyType = HotkeyEvent.HotkeyType.Custom;
            press.useLocalization = false;
            press.hotkey = KeyAction("Press Any Key", "<Keyboard>/anyKey", "<Mouse>/leftButton", "<Gamepad>/start", "<Gamepad>/buttonSouth");

            RectTransform content = Node("Content", root);
            Add<CanvasGroup>(content.gameObject);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 120;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform logoHolder = Node("Logo Holder", content);
            logoHolder.sizeDelta = new Vector2(0, 160);
            RectTransform logo = Node("Game Logo", logoHolder);
            Caption(logo.gameObject, GameTitle, 140, TextAlignmentOptions.Center, ctx.ui, UIManagerText.FontType.Bold);

            GameObject pak = Prefab("Splash Screen/PAK Label.prefab", content, "PAK Label");
            Set(splash, "UIManagerAsset", ctx.ui);
            Set(splash, "PAKStart", Find<TextMeshProUGUI>(pak, "Content/Start Text"));
            Set(splash, "PAKKey", Find<TextMeshProUGUI>(pak, "Content/Key Text/Key Text"));
            Set(splash, "PAKEnd", Find<TextMeshProUGUI>(pak, "Content/End Text"));

            root.gameObject.SetActive(false);
            return animator;
        }

        private static ImageFading BuildInitScreen(Context ctx)
        {
            RectTransform root = Node("Init Screen", ctx.canvas);
            Paint(root.gameObject, Color.black, null, true);
            return root.gameObject.AddComponent<ImageFading>();
        }
    }
}
