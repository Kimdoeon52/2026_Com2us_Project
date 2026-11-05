using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RealSteel.BossRace;
using RealSteel.BossRace.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RealSteel.Dialogue.EditorTools
{
    internal static class DialoguePrefabBuilder
    {
        internal const string PrefabFolder = "Assets/LHM/05.Prefab/Dialogue";
        internal const string SystemPrefabPath = PrefabFolder + "/DialogueSystem.prefab";
        internal const string TestVariantPath = PrefabFolder + "/DialogueSystem_Test.prefab";

        private const string Title = "대화 시스템 프리팹 만들기";
        private const string SourceScenePath = "Assets/LHM/01.Scene/DialogueTest.unity";
        private const string OutputScenePath = "Assets/LHM/01.Scene/DialogueTest_Prefab.unity";

        private static readonly (string source, string name, int sortOrder)[] Canvases =
        {
            ("DialogueCanvas", "DialogueCanvas", 110),
            ("[BossRace] Canvas", "BossRaceCanvas", 111),
            ("[Difficulty] Canvas", "DifficultyCanvas", 111),
            ("[SeasonCall] Canvas", "SeasonCallCanvas", 115),
        };

        private static readonly (string source, string name)[] CoreRoots =
        {
            ("DialogueDirector", "DialogueDirector"),
            ("[Step09] DialogueManager", "DialogueManager"),
            ("[Step10] Input", "Input"),
            ("[Step10] UiStack (LJS 시간)", "UiStack"),
            ("[Step11-2] EventQueue", "EventQueue"),
            ("[Step11] BossEntry", "BossEntry"),
            ("[Step15] BubbleSystem", "BubbleSystem"),
            ("[SeasonCall] SeasonRaceCall", "SeasonRaceCall"),
        };

        private const string TalkCameraRoot = "[TalkCam] NpcTalkCamera";
        private static readonly string[] TestRoots = { "[Step14] BattleBark", "[Step15] Crowd", "[Step18] TutorialTest" };
        private static readonly string[] NpcSpeakers = { "receptionist", "junkdealer" };

        private static readonly Type[] TestTypes =
        {
            typeof(Step09_DialogueDebug), typeof(Step10_InputTest), typeof(Step11_BossEntryTest), typeof(Step112_MidnightTest), typeof(Step15_BubbleTest),
        };

        private sealed class TestPart
        {
            public GameObject host;
            public Type type;
            public string json;
        }

        private sealed class Context
        {
            public Scene scene;
            public GameObject root;
            public Transform core;
            public Camera camera;
            public Transform player;
            public TMP_Text prompt;
            public int sourceApproach;
            public StringBuilder log;
            public readonly List<TestPart> tests = new List<TestPart>();
        }

        internal static bool Exists()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(SystemPrefabPath) != null;
        }

        internal static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string overwrite = Exists() ? "\n\n※ 프리팹과 검증 씬이 이미 있습니다. 직접 고친 내용은 덮어써집니다." : "";
            if (!EditorUtility.DisplayDialog(Title,
                    $"저장된 {SourceScenePath}를 기준으로 프리팹을 만듭니다.\n" +
                    $"- 프리팹: {PrefabFolder}\n- 검증 씬: {OutputScenePath}\n" +
                    $"- 원본 씬은 수정하지 않습니다. 작업 후 지금 열린 씬으로 돌아옵니다.{overwrite}", "만들기", "취소"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (HasUnsavedScene())
            {
                EditorUtility.DisplayDialog(Title, "저장되지 않은 씬이 있어 중단했습니다. 진행하면 그 변경이 사라집니다. 씬을 저장한 뒤 다시 실행하세요.", "확인");
                return;
            }

            bool hadPrefab = Exists();
            bool ok = RunBuild(out string report);
            Debug.Log($"[{Title}] {(ok ? "완료" : "실패")}\n{report}");

            string failure = hadPrefab ? "기존 프리팹이 일부 덮어써졌을 수 있습니다. git으로 되돌리세요.\n\n" : "";
            EditorUtility.DisplayDialog(Title, ok ? "완료. 콘솔 로그를 확인하고 검증 씬에서 Play 하세요." : $"실패했습니다. 콘솔 로그를 확인하세요.\n{failure}\n{report}", "확인");
        }

        private static bool HasUnsavedScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty) return true;
            }

            return false;
        }

        internal static bool RunBuild(out string report)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            var log = new StringBuilder();
            bool ok = false;

            try
            {
                ok = BuildFromSource(log);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                log.AppendLine($"예외: {exception.Message}");
            }
            finally
            {
                RestoreScenes(setup);
            }

            report = log.ToString();
            return ok;
        }

        private static void RestoreScenes(SceneSetup[] setup)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SceneSetup[] saved = setup.Where(s => !string.IsNullOrEmpty(s.path)).ToArray();
            if (saved.Length == 0) return;

            if (!saved.Any(s => s.isActive)) saved[0].isActive = true;
            EditorSceneManager.RestoreSceneManagerSetup(saved);
        }

        private static bool BuildFromSource(StringBuilder log)
        {
            var context = new Context { log = log, scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single) };

            if (!CheckSource(context) || !CaptureSceneValues(context)) return false;

            ArrangeHierarchy(context);
            ConfigureCanvases(context);
            AddRuntimeParts(context);
            RemoveTests(context);
            if (!AddHuntDeparture(context)) return false;
            ClearSceneReferences(context);

            if (!SaveSystemPrefab(context)) return false;
            if (!SaveTestVariant(context)) return false;

            SaveNpcPrefabs(context);
            return SaveValidationScene(context);
        }

        private static bool CheckSource(Context context)
        {
            string[] rootNames = context.scene.GetRootGameObjects().Select(r => r.name).ToArray();
            string[] required = Canvases.Select(c => c.source).Concat(CoreRoots.Select(c => c.source)).Append(TalkCameraRoot).ToArray();
            string[] missing = required.Where(name => !rootNames.Contains(name)).ToArray();
            if (missing.Length > 0)
            {
                context.log.AppendLine($"원본 씬에 없는 루트: {string.Join(", ", missing)}");
                return false;
            }

            if (JunkShopDialogueSetup.FindInScene<Step09_DialogueDebug>(context.scene).Any() && JunkShopDialogueSetup.FindInScene<Step112_MidnightTest>(context.scene).Any()) return true;

            context.log.AppendLine("원본 씬에 Step09_DialogueDebug 또는 Step112_MidnightTest가 없습니다.");
            return false;
        }

        private static bool CaptureSceneValues(Context context)
        {
            PlayerInteractor interactor = JunkShopDialogueSetup.FindInScene<PlayerInteractor>(context.scene).FirstOrDefault();
            NpcTalkCamera talkCamera = JunkShopDialogueSetup.FindInScene<NpcTalkCamera>(context.scene).FirstOrDefault();
            if (interactor == null || talkCamera == null)
            {
                context.log.AppendLine("원본 씬에서 PlayerInteractor 또는 NpcTalkCamera를 찾지 못했습니다.");
                return false;
            }

            using (var so = new SerializedObject(interactor))
            {
                context.camera = so.FindProperty("worldCamera").objectReferenceValue as Camera;
                context.prompt = so.FindProperty("prompt").objectReferenceValue as TMP_Text;
            }

            using (var so = new SerializedObject(talkCamera))
            {
                if (context.camera == null) context.camera = so.FindProperty("mainCamera").objectReferenceValue as Camera;
                context.sourceApproach = so.FindProperty("approach").enumValueIndex;
            }

            context.player = interactor.transform;
            if (context.camera != null && context.prompt != null) return true;

            context.log.AppendLine("원본 씬의 PlayerInteractor에서 카메라 또는 InteractPrompt를 찾지 못했습니다.");
            return false;
        }

        private static void ArrangeHierarchy(Context context)
        {
            GameObject[] roots = context.scene.GetRootGameObjects();
            GameObject Root(string name) => roots.First(r => r.name == name);

            context.root = new GameObject("DialogueSystem");
            context.core = new GameObject("Core").transform;
            context.core.SetParent(context.root.transform, false);

            foreach ((string source, string name) in CoreRoots) Move(Root(source), context.core, name);
            Move(Root(TalkCameraRoot), context.root.transform, "TalkCamera");
            foreach ((string source, string name, int _) in Canvases) Move(Root(source), context.root.transform, name);
        }

        private static void Move(GameObject target, Transform parent, string name)
        {
            target.transform.SetParent(parent, true);
            target.name = name;
        }

        private static void ConfigureCanvases(Context context)
        {
            foreach ((string _, string name, int sortOrder) in Canvases)
            {
                Canvas canvas = context.root.transform.Find(name).GetComponent<Canvas>();
                if (canvas != null) canvas.sortingOrder = sortOrder;
            }
        }

        private static T Get<T>(Context context) where T : Component
        {
            return context.root.GetComponentInChildren<T>(true);
        }

        private static void AddRuntimeParts(Context context)
        {
            DialogueManager manager = Get<DialogueManager>(context);
            SeasonRaceCall seasonCall = Get<SeasonRaceCall>(context);
            Step09_DialogueDebug debug = Get<Step09_DialogueDebug>(context);
            Step112_MidnightTest midnight = Get<Step112_MidnightTest>(context);

            var store = manager.gameObject.AddComponent<BossProgressStore>();
            using (var so = new SerializedObject(debug))
            {
                SerializedProperty durability = so.FindProperty("partDurability");
                float[] values = Enumerable.Range(0, durability.arraySize).Select(i => durability.GetArrayElementAtIndex(i).floatValue).ToArray();
                store.Configure(manager, so.FindProperty("seasonLength").intValue, values);
            }

            var processor = midnight.gameObject.AddComponent<DialogueDayProcessor>();
            using (var so = new SerializedObject(midnight))
            {
                processor.Configure(manager, Get<DialogueEventQueue>(context), Get<TimeSystemUiStack>(context), seasonCall, so.FindProperty("seasonLength").intValue);
            }

            SetReference(seasonCall, "progressSource", store);

            var binder = context.root.AddComponent<DialogueSceneBinder>();
            SetReference(binder, "interactPrompt", context.prompt);

            var bridge = new GameObject("FieldInputBridge").AddComponent<FieldInputBridge>();
            bridge.transform.SetParent(context.core, false);
            SetReference(bridge, "menu", Get<NpcChoiceMenu>(context));

            new GameObject("ShortcutHelp", typeof(ShortcutHelpOverlay)).transform.SetParent(context.core, false);
            context.log.AppendLine("- 추가: DialogueSceneBinder, BossProgressStore, DialogueDayProcessor, FieldInputBridge, ShortcutHelp");
        }

        private static void RemoveTests(Context context)
        {
            foreach (Type type in TestTypes)
            {
                Component component = context.root.GetComponentInChildren(type, true);
                if (component == null)
                {
                    context.log.AppendLine($"- 주의: 원본에 {type.Name} 없음");
                    continue;
                }

                context.tests.Add(new TestPart { host = component.gameObject, type = type, json = EditorJsonUtility.ToJson(component) });
                Object.DestroyImmediate(component);
            }
        }

        private static bool AddHuntDeparture(Context context)
        {
            if (!HuntDepartureSetup.Create(context.scene, context.log))
            {
                context.log.AppendLine("HuntDeparture를 만들지 못했습니다.");
                return false;
            }

            GameObject hunt = context.scene.GetRootGameObjects().First(r => r.name == HuntDepartureSetup.RootName);
            Move(hunt, context.core, "HuntDeparture");
            return true;
        }

        private static void ClearSceneReferences(Context context)
        {
            SetReference(Get<BubbleManager>(context), "worldCamera", null);
            SetReference(Get<DialogueDirector>(context), "targetCamera", null);
            SetReference(Get<NpcTalkCamera>(context), "mainCamera", null);
            SetReference(Get<NpcBubbles>(context), "player", null);
            SetReference(Get<TimeSystemUiStack>(context), "timeSystem", null);
            SetReference(Get<NpcChoiceMenu>(context), "shopWindow", null);
            SetEnum(Get<NpcTalkCamera>(context), "approach", (int)TalkCameraApproach.PlayerSide);
        }

        private static bool SaveSystemPrefab(Context context)
        {
            EnsureFolder(PrefabFolder);

            var external = new List<string>();
            HashSet<string> expected = CollectReferences(context.root, external);
            foreach (string item in external) context.log.AppendLine($"- 주의: 프리팹 밖을 가리켜 비워지는 참조: {item}");

            foreach ((string _, string name, int _) in Canvases)
            {
                GameObject canvas = context.root.transform.Find(name).gameObject;
                PrefabUtility.SaveAsPrefabAssetAndConnect(canvas, $"{PrefabFolder}/{name}.prefab", InteractionMode.AutomatedAction, out bool saved);
                if (saved) continue;

                context.log.AppendLine($"{name} 프리팹 저장 실패");
                return false;
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(context.root, SystemPrefabPath, InteractionMode.AutomatedAction, out bool success);
            if (!success)
            {
                context.log.AppendLine("DialogueSystem 프리팹 저장 실패");
                return false;
            }

            return ValidateSaved(context, expected);
        }

        private static bool ValidateSaved(Context context, HashSet<string> expected)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(SystemPrefabPath);
            try
            {
                HashSet<string> actual = CollectReferences(contents, new List<string>());
                string[] lost = expected.Where(key => !actual.Contains(key)).ToArray();
                context.log.AppendLine($"- 참조 대조: 저장 전 {expected.Count}개, 끊어진 참조 {lost.Length}개");
                foreach (string key in lost) context.log.AppendLine($"  끊어짐: {key}");
                return lost.Length == 0;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static HashSet<string> CollectReferences(GameObject root, List<string> external)
        {
            var keys = new HashSet<string>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform) continue;

                string owner = ComponentKey(component, root.transform);
                using var so = new SerializedObject(component);
                SerializedProperty property = so.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_GameObject" || property.name == "m_Script") continue;

                    Object value = property.objectReferenceValue;
                    if (value == null) continue;

                    GameObject target = value as GameObject;
                    if (target == null && value is Component other) target = other.gameObject;

                    if (EditorUtility.IsPersistent(value) || target == null || target.transform.IsChildOf(root.transform)) keys.Add($"{owner}|{property.propertyPath}");
                    else external.Add($"{owner}.{property.propertyPath} → {target.name}");
                }
            }

            return keys;
        }

        private static string ComponentKey(Component component, Transform root)
        {
            var path = new List<string>();
            for (Transform t = component.transform; t != root && t != null; t = t.parent) path.Insert(0, t.GetSiblingIndex().ToString());

            Component[] sameType = component.GetComponents(component.GetType());
            return $"{string.Join("/", path)}|{component.GetType().Name}|{Array.IndexOf(sameType, component)}";
        }

        private static bool SaveTestVariant(Context context)
        {
            RestoreTests(context);
            MoveTestRoots(context);

            SetEnum(Get<NpcTalkCamera>(context), "approach", context.sourceApproach);
            Get<HuntDeparture>(context).enabled = false;

            PrefabUtility.SaveAsPrefabAssetAndConnect(context.root, TestVariantPath, InteractionMode.AutomatedAction, out bool success);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(TestVariantPath);
            if (!success || asset == null || PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
            {
                context.log.AppendLine("DialogueSystem_Test Variant 저장 실패");
                return false;
            }

            context.log.AppendLine($"- 저장: {SystemPrefabPath}, {TestVariantPath}");
            return true;
        }

        private static void RestoreTests(Context context)
        {
            foreach (TestPart part in context.tests)
            {
                Component component = part.host.AddComponent(part.type);
                EditorJsonUtility.FromJsonOverwrite(part.json, component);
            }

            DialogueManager manager = Get<DialogueManager>(context);
            DialogueEventQueue queue = Get<DialogueEventQueue>(context);

            Step09_DialogueDebug debug = Get<Step09_DialogueDebug>(context);
            if (debug != null) SetReference(debug, "manager", manager);

            Step11_BossEntryTest entryTest = Get<Step11_BossEntryTest>(context);
            if (entryTest != null)
            {
                SetReference(entryTest, "manager", manager);
                SetReference(entryTest, "entry", Get<BossEntryDialogue>(context));
                SetReference(entryTest, "debug", debug);
                SetReference(entryTest, "menu", Get<NpcChoiceMenu>(context));
                SetReference(entryTest, "queue", queue);
                SetReference(entryTest, "bossSelect", Get<BossRaceSelectUI>(context));
                SetReference(entryTest, "difficultySelect", Get<BossDifficultySelectUI>(context));
            }

            Step112_MidnightTest midnight = Get<Step112_MidnightTest>(context);
            if (midnight != null)
            {
                SetReference(midnight, "manager", manager);
                SetReference(midnight, "queue", queue);
                SetReference(midnight, "clock", Get<TimeSystemUiStack>(context));
                SetReference(midnight, "seasonCall", Get<SeasonRaceCall>(context));
            }

            Step15_BubbleTest bubbleTest = Get<Step15_BubbleTest>(context);
            if (bubbleTest != null)
            {
                SetReference(bubbleTest, "manager", manager);
                SetReference(bubbleTest, "queue", queue);
                SetReference(bubbleTest, "npcBubbles", Get<NpcBubbles>(context));
            }
        }

        private static void MoveTestRoots(Context context)
        {
            foreach (GameObject root in context.scene.GetRootGameObjects())
            {
                if (TestRoots.Contains(root.name)) root.transform.SetParent(context.root.transform, true);
            }

            Step11_BossEntryTest entryTest = Get<Step11_BossEntryTest>(context);
            if (entryTest != null) SetReference(entryTest, "barks", Get<BattleBarkPlayer>(context));

            Step15_BubbleTest bubbleTest = Get<Step15_BubbleTest>(context);
            if (bubbleTest != null) SetReference(bubbleTest, "crowd", Get<CrowdBubbles>(context));
        }

        private static void SaveNpcPrefabs(Context context)
        {
            foreach (string speaker in NpcSpeakers)
            {
                NpcInteractable npc = JunkShopDialogueSetup.FindInScene<NpcInteractable>(context.scene).FirstOrDefault(n => n.SpeakerId == speaker);
                if (npc == null)
                {
                    context.log.AppendLine($"- 주의: Speaker Id '{speaker}' NPC가 없어 프리팹을 만들지 않음");
                    continue;
                }

                string path = NpcPrefabPath(speaker);
                PrefabUtility.SaveAsPrefabAssetAndConnect(npc.gameObject, path, InteractionMode.AutomatedAction, out bool saved);
                context.log.AppendLine(saved ? $"- 저장: {path}" : $"- 주의: {path} 저장 실패");
            }
        }

        internal static string NpcPrefabPath(string speakerId)
        {
            return $"{PrefabFolder}/NPC_{speakerId}.prefab";
        }

        private static bool SaveValidationScene(Context context)
        {
            DialogueSceneBinder binder = context.root.GetComponent<DialogueSceneBinder>();
            SetReference(binder, "gameplayCamera", context.camera);
            SetReference(binder, "player", context.player);

            if (!EditorSceneManager.SaveScene(context.scene, OutputScenePath, true))
            {
                context.log.AppendLine($"{OutputScenePath} 저장 실패");
                return false;
            }

            context.log.AppendLine($"- 검증 씬: {OutputScenePath} (카메라 {context.camera.name}, 플레이어 {context.player.name})");
            return true;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            using var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException($"{target.GetType().Name}에 '{field}' 칸이 없습니다.");

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, int value)
        {
            using var so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
