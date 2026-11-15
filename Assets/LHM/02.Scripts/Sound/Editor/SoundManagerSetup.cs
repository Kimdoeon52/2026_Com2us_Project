using System.IO;
using System.Linq;
using System.Text;
using Michsky.UI.Heat;
using RealSteel.Dialogue.EditorTools;
using RealSteel.MainMenu.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace RealSteel.Sound.EditorTools
{
    // 공용 사운드 매니저 프리팹 만들기 + 현재 씬에 넣기 (+ 메인 메뉴면 설정에 BGM · 효과음 음량 슬라이더 추가)
    public static class SoundManagerSetup
    {
        private const string Title = "사운드 매니저";
        internal const string PrefabPath = "Assets/LHM/05.Prefab/Sound/SoundManager.prefab";
        private const string LibraryPath = "Assets/LHM/03.Data/Sound/SoundLibrary.asset";
        // 다른 팀(공용 Sound 폴더) 믹서는 참조만 함. 없으면 믹서 없이 동작
        private const string MixerPath = "Assets/Sound/Master.mixer";
        private const string SliderPrefab = "UI Elements/Settings/Settings Element (Slider).prefab";

        internal static bool Exists()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool inScene = Object.FindObjectsByType<SoundManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(m => m.gameObject.scene == scene);
            return inScene && !NeedsSliders(scene);
        }

        private static bool NeedsSliders(Scene scene) => FindMasterSlider(scene) != null && FindSlider(scene, SoundManager.BgmVolumeKey) == null;

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool hasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            bool inScene = Exists();
            SliderManager master = FindMasterSlider(scene);
            bool needSliders = NeedsSliders(scene);

            if (hasPrefab && inScene && !needSliders)
            {
                Selection.activeObject = Object.FindObjectsByType<SoundManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(m => m.gameObject.scene == scene);
                SetupKit.Dialog(Title, "이 씬에 이미 사운드 매니저가 있습니다. 선택해 두었으니 인스펙터에서 '이 씬 BGM'을 정하세요.", "확인");
                return;
            }

            string current = string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path;
            if (!SetupKit.Dialog(Title,
                    $"'{scene.name}' 씬에 공용 사운드 매니저를 넣습니다.\n" +
                    (SetupKit.IsLhmScene(scene) ? "" : $"※ LHM 밖의 씬입니다: {current}\n  다른 팀 씬이면 저장 전에 담당자와 확인하세요.\n") +
                    $"- 프리팹: {(hasPrefab ? "이미 있음" : "새로 만듦")} → {PrefabPath}\n" +
                    $"- 효과음 목록: {LibraryPath} (id로 부를 소리를 여기에 등록)\n" +
                    $"- 씬: {(inScene ? "이미 있음" : "프리팹 배치")}\n" +
                    (needSliders ? "- 메인 메뉴 설정 > 사운드: 'BGM 음량' · '효과음 음량' 슬라이더 추가 (전체 음량 아래)\n" : "") +
                    "\n다른 씬에서는 프리팹을 끌어다 놓거나 이 버튼을 누르고, '이 씬 BGM'만 바꾸면 됩니다.\n" +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 씬 변경 되돌리기)",
                    "넣기", "취소"))
            {
                return;
            }

            var log = new StringBuilder($"[{Title}] {current}\n");
            GameObject prefab = EnsurePrefab(log);
            if (prefab == null)
            {
                Debug.LogWarning(log.ToString());
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(Title);
            int group = Undo.GetCurrentGroup();

            SoundManager manager = Object.FindObjectsByType<SoundManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(m => m.gameObject.scene == scene);
            if (manager == null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(go, Title);
                manager = go.GetComponent<SoundManager>();
                log.AppendLine($"- 씬에 {go.name} 배치");
            }

            if (needSliders) AddVolumeSliders(master, manager, log);

            Selection.activeGameObject = manager.gameObject;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. 인스펙터에서 '이 씬 BGM'을 정하고 Ctrl+S로 저장하세요.");
            Debug.Log(log.ToString());
        }

        private static GameObject EnsurePrefab(StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));

            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
                log.AppendLine($"- 효과음 목록 생성: {LibraryPath}");
            }

            var go = new GameObject("SoundManager");
            try
            {
                var manager = go.AddComponent<SoundManager>();
                var so = new SerializedObject(manager);
                so.FindProperty("library").objectReferenceValue = library;

                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                if (mixer != null)
                {
                    so.FindProperty("bgmGroup").objectReferenceValue = mixer.FindMatchingGroups("BGM").FirstOrDefault();
                    so.FindProperty("sfxGroup").objectReferenceValue = mixer.FindMatchingGroups("SFX").FirstOrDefault();
                    log.AppendLine($"- 믹서 그룹 연결: {MixerPath} (BGM · SFX)");
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                log.AppendLine($"- 프리팹 생성: {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── 메인 메뉴 설정 슬라이더 ─────────────────────────────

        private static SliderManager FindMasterSlider(Scene scene) => FindSlider(scene, SoundManager.MasterVolumeKey);

        private static SliderManager FindSlider(Scene scene, string saveKey)
        {
            if (!scene.IsValid()) return null;
            return scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<SliderManager>(true))
                .FirstOrDefault(s => s.saveKey == saveKey);
        }

        private static void AddVolumeSliders(SliderManager master, SoundManager manager, StringBuilder log)
        {
            // 슬라이더 프리팹 한 덩어리(설정 항목) = 목록의 바로 아래 자식
            Transform element = master.transform;
            Transform list = element.parent;
            while (list != null && list.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() == null)
            {
                element = list;
                list = list.parent;
            }
            if (list == null)
            {
                log.AppendLine("- 주의: 전체 음량 슬라이더가 들어 있는 목록을 찾지 못해 슬라이더를 추가하지 못함");
                return;
            }

            // 메인 메뉴 컨트롤러가 있으면 거기로 (전체 음량과 같은 경로 → 사운드 매니저로 전달)
            var controller = Object.FindObjectsByType<RealSteel.MainMenu.MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.scene == master.gameObject.scene);

            int index = element.GetSiblingIndex();
            SliderManager bgm = AddSlider(list, ++index, "BGM Volume", "BGM 음량", SoundManager.BgmVolumeKey, 0.7f);
            if (bgm != null)
            {
                if (controller != null) HeatKit.Listen(bgm.onValueChanged, controller.SetBgmVolume);
                else HeatKit.Listen(bgm.onValueChanged, manager.SetBgmVolume);
            }
            SliderManager sfx = AddSlider(list, ++index, "SFX Volume", "효과음 음량", SoundManager.SfxVolumeKey, 1f);
            if (sfx != null)
            {
                if (controller != null) HeatKit.Listen(sfx.onValueChanged, controller.SetSfxVolume);
                else HeatKit.Listen(sfx.onValueChanged, manager.SetSfxVolume);
            }
            log.AppendLine("- 설정 > 사운드: 'BGM 음량' · '효과음 음량' 슬라이더 추가 (값은 PlayerPrefs에 저장, 모든 씬에 적용)");
        }

        private static SliderManager AddSlider(Transform list, int siblingIndex, string objectName, string label, string saveKey, float defaultValue)
        {
            GameObject element = HeatKit.Prefab(SliderPrefab, list, objectName);
            Undo.RegisterCreatedObjectUndo(element, Title);
            element.transform.SetSiblingIndex(siblingIndex);

            TextMeshProUGUI text = element.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Text");
            if (text != null) text.text = label;

            // 설명 패널(SettingsDescriptionManager)이 없는 씬에서는 오류가 나므로 뺌 (메인 메뉴 빌더와 같게)
            foreach (SettingsDescription description in element.GetComponentsInChildren<SettingsDescription>(true))
            {
                Object.DestroyImmediate(description);
            }

            var slider = element.GetComponentInChildren<SliderManager>(true);
            if (slider == null || slider.mainSlider == null) return null;

            slider.saveValue = true;
            slider.saveKey = saveKey;
            slider.usePercent = true;
            slider.useRoundValue = false;
            slider.mainSlider.minValue = 0f;
            slider.mainSlider.maxValue = 1f;
            slider.mainSlider.wholeNumbers = false;
            slider.mainSlider.value = defaultValue;
            return slider;
        }
    }
}
