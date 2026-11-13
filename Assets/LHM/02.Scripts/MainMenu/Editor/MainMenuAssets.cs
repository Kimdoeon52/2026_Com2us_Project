using System.Collections.Generic;
using System.Text;
using Michsky.UI.Heat;
using RealSteel.Cutscene.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RealSteel.MainMenu.EditorTools
{
    internal static class MainMenuAssets
    {
        internal const string FontFolder = "Assets/LHM/04.Fonts";
        internal const string DataFolder = "Assets/LHM/03.Data/MainMenu";
        internal const string UIManagerPath = DataFolder + "/MainMenu_UIManager.asset";
        internal const string CreditsPath = DataFolder + "/MainMenu_Credits.asset";

        private const string SourceFontFolder = "Assets/LJS/03. Asset/Fonts/";
        private const string HeatUIManagerPath = HeatKit.Root + "Resources/Heat UI Manager.asset";
        private const string HeatCreditsPath = HeatKit.Root + "Presets/Credits.asset";
        private const string PressAnyKeyText = "{아무 키나} 눌러서 시작";

        private const int SamplingSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 2048;

        internal static UIManager EnsureUIManager(StringBuilder log)
        {
            CutsceneArt.EnsureFolder(DataFolder);
            CutsceneArt.EnsureFolder(FontFolder);

            TMP_FontAsset light = EnsureFont("NanumBarunGothicLight", log);
            TMP_FontAsset regular = EnsureFont("NanumBarunGothic", log);
            TMP_FontAsset bold = EnsureFont("NanumBarunGothicBold", log);

            var manager = AssetDatabase.LoadAssetAtPath<UIManager>(UIManagerPath);
            if (manager == null)
            {
                if (!AssetDatabase.CopyAsset(HeatUIManagerPath, UIManagerPath))
                    throw new System.IO.IOException($"UI Manager를 복사하지 못했습니다: {HeatUIManagerPath}");
                manager = AssetDatabase.LoadAssetAtPath<UIManager>(UIManagerPath);
                log.AppendLine($"- UI Manager 복사본 만듦: {UIManagerPath} (Heat 원본은 그대로)");
            }

            manager.fontLight = light;
            manager.fontRegular = regular;
            manager.fontMedium = regular;
            manager.fontSemiBold = bold;
            manager.fontBold = bold;
            manager.customFont = bold;
            manager.enableLocalization = false;
            manager.enableSplashScreen = true;
            manager.showSplashScreenOnce = true;
            manager.pakType = UIManager.PressAnyKeyTextType.Default;
            manager.pakText = PressAnyKeyText;
            EditorUtility.SetDirty(manager);
            log.AppendLine("- UI Manager: 나눔바른고딕, 로컬라이제이션 끔, 스플래시 1회");
            return manager;
        }

        internal static CreditsPreset EnsureCredits(StringBuilder log)
        {
            var preset = AssetDatabase.LoadAssetAtPath<CreditsPreset>(CreditsPath);
            if (preset != null)
            {
                log.AppendLine($"- 크레딧 데이터 있음, 그대로 씀: {CreditsPath}");
                return preset;
            }

            if (!AssetDatabase.CopyAsset(HeatCreditsPath, CreditsPath))
                throw new System.IO.IOException($"크레딧 데이터를 복사하지 못했습니다: {HeatCreditsPath}");

            preset = AssetDatabase.LoadAssetAtPath<CreditsPreset>(CreditsPath);
            preset.credits = new List<CreditsPreset.CreditsSection>
            {
                Section("기획", "이름 입력"),
                Section("프로그래밍", "이름 입력"),
                Section("아트", "이름 입력"),
                Section("사운드", "이름 입력"),
                Section("사용 에셋", "Heat - Complete Modern UI (Michsky)", "Motion Titles Pack (Michsky)")
            };
            preset.mentions = new List<CreditsPreset.MentionSection>();
            EditorUtility.SetDirty(preset);
            log.AppendLine($"- 크레딧 데이터 만듦: {CreditsPath} (팀원 이름은 직접 입력)");
            return preset;
        }

        private static CreditsPreset.CreditsSection Section(string header, params string[] names)
        {
            return new CreditsPreset.CreditsSection
            {
                headerTitle = header,
                headerTitleKey = "",
                items = new List<string>(names)
            };
        }

        private static TMP_FontAsset EnsureFont(string fontName, StringBuilder log)
        {
            string assetPath = $"{FontFolder}/{fontName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            string ttfPath = $"{FontFolder}/{fontName}.ttf";
            if (AssetDatabase.LoadAssetAtPath<Font>(ttfPath) == null)
            {
                string source = SourceFontFolder + fontName + ".ttf";
                if (!AssetDatabase.CopyAsset(source, ttfPath))
                    throw new System.IO.FileNotFoundException($"한글 폰트를 복사하지 못했습니다: {source}");
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, AtlasPadding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = fontName + " SDF";
            AssetDatabase.CreateAsset(asset, assetPath);

            asset.atlasTextures[0].name = fontName + " SDF Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = fontName + " SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            var so = new SerializedObject(asset);
            SerializedProperty clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null)
            {
                clear.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            log.AppendLine($"- 한글 폰트 만듦: {assetPath} (동적, 원본 {SourceFontFolder}{fontName}.ttf 복사)");
            return asset;
        }
    }
}
