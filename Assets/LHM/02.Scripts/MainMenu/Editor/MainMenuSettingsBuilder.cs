using Michsky.UI.Heat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static RealSteel.MainMenu.EditorTools.HeatKit;
using Dropdown = Michsky.UI.Heat.Dropdown;

namespace RealSteel.MainMenu.EditorTools
{
    // 설정 패널: 화면 (해상도 · 화면 모드 · 수직 동기화) + 사운드 (전체 음량)
    internal static class MainMenuSettingsBuilder
    {
        private const string Elements = "UI Elements/Settings/";
        private const float ListWidth = 900f;
        private const string WindowModeKey = "RealSteel Window Mode";
        private const string VSyncKey = "RealSteel VSync";

        internal static Transform Build(MainMenuBuilder.Context ctx, RectTransform settings)
        {
            Transform content = settings.Find("Content");

            RectTransform list = Node("List", content);
            Place(list, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(MainMenuBuilder.SideMargin, -MainMenuBuilder.BoxMarginY), new Vector2(ListWidth, 0));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Header(list, "화면");
            BuildResolution(ctx, list);
            BuildWindowMode(ctx, list);
            BuildVSync(ctx, list);

            Header(list, "사운드");
            BuildMasterVolume(ctx, list);

            Transform hotkeys = MainMenuBuilder.Hotkeys(content);
            HotkeyEvent back = MainMenuBuilder.Hotkey(hotkeys, "Back", "뒤로");
            Listen(back.onHotkeyPress, ctx.controller.OpenHome);

            ctx.log.AppendLine("- 설정: 해상도 · 화면 모드 · 수직 동기화 · 전체 음량 (값은 PlayerPrefs에 저장)");
            return hotkeys;
        }

        private static void Header(Transform parent, string title)
        {
            GameObject header = Prefab(Elements + "Settings Header.prefab", parent, "Header " + title);
            SetLabel(header.GetComponent<TextMeshProUGUI>(), title);
        }

        private static void BuildResolution(MainMenuBuilder.Context ctx, Transform parent)
        {
            GameObject element = Element(parent, "Settings Element (Dropdown).prefab", "Resolution", "해상도");
            var dropdown = element.GetComponentInChildren<Dropdown>(true);
            if (dropdown == null) return;

            // 목록은 실행할 때 GraphicsManager가 모니터 해상도로 채움 (에디터에서는 선택해도 바뀌지 않음)
            dropdown.items.Clear();
            Set(ctx.graphics, "resolutionDropdown", dropdown);
            Set(ctx.graphics, "initializeResolutions", true);
        }

        private static void BuildWindowMode(MainMenuBuilder.Context ctx, Transform parent)
        {
            GameObject element = Element(parent, "Settings Element (Horizontal Selector).prefab", "Window Mode", "화면 모드");
            var selector = element.GetComponentInChildren<HorizontalSelector>(true);
            if (selector == null) return;

            selector.useLocalization = false;
            selector.saveSelected = true;
            selector.saveKey = WindowModeKey;
            selector.defaultIndex = 1;
            selector.items.Clear();
            selector.items.Add(SelectorItem("전체 화면"));
            selector.items.Add(SelectorItem("테두리 없는 창"));
            selector.items.Add(SelectorItem("창 모드"));
            Listen(selector.items[0].onItemSelect, ctx.graphics.SetWindowFullscreen);
            Listen(selector.items[1].onItemSelect, ctx.graphics.SetWindowBorderless);
            Listen(selector.items[2].onItemSelect, ctx.graphics.SetWindowWindowed);
        }

        private static HorizontalSelector.Item SelectorItem(string title)
        {
            return new HorizontalSelector.Item { itemTitle = title, localizationKey = "" };
        }

        private static void BuildVSync(MainMenuBuilder.Context ctx, Transform parent)
        {
            GameObject element = Element(parent, "Settings Element (Switch).prefab", "VSync", "수직 동기화");
            var toggle = element.GetComponentInChildren<SwitchManager>(true);
            if (toggle == null) return;

            toggle.saveValue = true;
            toggle.saveKey = VSyncKey;
            toggle.isOn = true;
            Listen(toggle.onValueChanged, ctx.controller.SetVSync);
        }

        private static void BuildMasterVolume(MainMenuBuilder.Context ctx, Transform parent)
        {
            GameObject element = Element(parent, "Settings Element (Slider).prefab", "Master Volume", "전체 음량");
            var slider = element.GetComponentInChildren<SliderManager>(true);
            if (slider == null || slider.mainSlider == null) return;

            // 저장 키는 MainMenuController가 게임 시작 때 읽는 키와 같아야 함 ("Slider_" + 키)
            slider.saveValue = true;
            slider.saveKey = MainMenuController.MasterVolumeKey;
            slider.usePercent = true;
            slider.useRoundValue = false;
            slider.mainSlider.minValue = 0f;
            slider.mainSlider.maxValue = 1f;
            slider.mainSlider.wholeNumbers = false;
            slider.mainSlider.value = 1f;
            Listen(slider.onValueChanged, ctx.controller.SetMasterVolume);
        }

        private static GameObject Element(Transform parent, string prefab, string name, string label)
        {
            GameObject element = Prefab(Elements + prefab, parent, name);
            SetLabel(Find<TextMeshProUGUI>(element, "Text"), label);

            // 설명 패널(SettingsDescriptionManager)이 없는 씬에서는 오류가 나므로 뺌
            foreach (SettingsDescription description in element.GetComponentsInChildren<SettingsDescription>(true))
            {
                Object.DestroyImmediate(description);
            }
            return element;
        }

        private static void SetLabel(TextMeshProUGUI text, string value)
        {
            if (text == null) return;
            text.text = value;

            // 로컬라이제이션을 꺼도 키가 남아 있으면 덮어쓸 수 있어 비움
            var localized = text.GetComponent<LocalizedObject>();
            if (localized != null) localized.localizationKey = "";
        }
    }
}
