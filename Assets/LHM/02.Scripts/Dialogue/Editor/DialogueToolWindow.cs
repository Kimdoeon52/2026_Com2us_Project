using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RealSteel.BossRace;
using RealSteel.BossRace.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RealSteel.Dialogue.EditorTools
{
    public sealed class DialogueToolWindow : EditorWindow
    {
        private const string WindowTitle = "대화 툴";
        private const float ButtonWidth = 96f;
        private const float StatusWidth = 52f;
        private const string AppliedText = "<color=#6FCF6F>적용됨</color>";
        private const string MissingText = "<color=#E0A040>없음</color>";

        private sealed class ToolItem
        {
            public readonly string Label;
            public readonly Action Run;
            public readonly Func<bool> IsApplied;
            public readonly bool InBatch;
            public readonly string BatchWarning;

            public ToolItem(string label, Action run, Func<bool> isApplied, bool inBatch = false, string batchWarning = null)
            {
                Label = label;
                Run = run;
                IsApplied = isApplied;
                InBatch = inBatch;
                BatchWarning = batchWarning;
            }
        }

        private sealed class ToolGroup
        {
            public readonly string Title;
            public readonly ToolItem[] Items;

            public ToolGroup(string title, params ToolItem[] items)
            {
                Title = title;
                Items = items;
            }
        }

        private static readonly ToolGroup[] Groups =
        {
            new ToolGroup("프리팹",
                new ToolItem("프리팹 만들기 (DialogueTest 기준)", DialoguePrefabBuilder.Build, DialoguePrefabBuilder.Exists),
                new ToolItem("씬에 프리팹 배치 + 씬 연결", DialogueSceneLinker.PlaceAndLink, Has<DialogueSceneBinder>),
                new ToolItem("씬 연결 다시 실행", DialogueSceneLinker.LinkActiveScene, null)),
            new ToolGroup("기본 대화",
                new ToolItem("대화 코어 (9단계)", Step09_SceneSetup.Setup, () => Has<DialogueManager>() && Has<NpcChoiceMenu>(), true),
                new ToolItem("LJS 시간 (10-1단계)", TimeSystemSetup.Setup, () => Has<TimeSystemUiStack>() && !Has<DummyUiStack>(), true,
                    "LJS 프리팹 배치, 이 씬의 F1~F5 씬 전환 끔, DummyUiStack · ClockText 삭제"),
                new ToolItem("입력 상태 (10-2단계)", Step10_SceneSetup.SetupInput, Has<InputModeController>, true),
                new ToolItem("보스 진입 (11단계)", Step11_SceneSetup.Setup, Has<BossEntryDialogue>, true),
                new ToolItem("00시 트리거 · 이벤트 큐 (11-2단계)", Step112_SceneSetup.Setup, Has<DialogueEventQueue>, true),
                new ToolItem("스킵 · 오토 · 빨리감기 (12단계)", Step12_SceneSetup.Setup, () => DialogueBoxField("statusText") != null && DialogueBoxField("skipConfirm") != null, true),
                new ToolItem("백로그 (13단계)", Step13_SceneSetup.Setup, Has<DialogueBacklog>, true),
                new ToolItem("전투 바크 (14단계)", Step14_SceneSetup.Setup, Has<BattleBarkPlayer>, true),
                new ToolItem("비강제 말풍선 (15단계)", Step15_SceneSetup.Setup, Has<BubbleManager>, true),
                new ToolItem("튜토리얼 (18단계)", Step18_SceneSetup.Setup, () => HasPool("tutorial"), true),
                new ToolItem("패킹 데이터 연결 (16단계)", Step16_SceneSetup.Setup, HasPackedData, true)),
            new ToolGroup("보스 레이스",
                new ToolItem("보스 레이스 선택 UI", BossRaceSetup.Setup, Has<BossRaceSelectUI>, true),
                new ToolItem("RS-OS 스타일 지역 보스 선택 (프리팹 수정)", RsOsRegionStyleSetup.Setup, RsOsRegionStyleSetup.IsApplied),
                new ToolItem("난이도 선택 UI", DifficultySetup.Setup, Has<BossDifficultySelectUI>, true),
                new ToolItem("RS-OS 스타일 난이도 선택 (프리팹 수정)", RsOsDifficultyStyleSetup.Setup, RsOsDifficultyStyleSetup.IsApplied),
                new ToolItem("시즌 시작 전화", SeasonCallSetup.Setup, Has<SeasonRaceCall>, true),
                new ToolItem("토벌 출발 (실제 전투 씬으로 이동)", HuntDepartureSetup.Setup, Has<HuntDeparture>)),
            new ToolGroup("연출 · NPC",
                new ToolItem("NPC 클로즈업 카메라", NpcTalkCameraSetup.Setup, Has<NpcTalkCamera>),
                new ToolItem("대화창 모션 (MTP-2)", DialogueBoxMotionSetup.Setup, Has<DialogueBoxMotion>),
                new ToolItem("하데스 스타일 대화창", HadesDialogueStyleSetup.Setup, HasHadesStyle),
                new ToolItem("RS-OS 스타일 대화창 (프리팹 수정)", RsOsDialogueStyleSetup.Setup, RsOsDialogueStyleSetup.IsApplied),
                new ToolItem("NPC 스프라이트", NpcSpriteSetup.Setup, NpcSpriteSetup.IsApplied),
                new ToolItem("단축키 정리 (Home 안내)", JunkShopDialogueSetup.ApplyShortcutsMenu, Has<ShortcutHelpOverlay>)),
            new ToolGroup("고물상 맵",
                new ToolItem("고물상 맵 배치", JunkShopDialogueSetup.Setup, Has<FieldInputBridge>),
                new ToolItem("연결 복구 (카메라 · 상점 창)", JunkShopDialogueSetup.RepairLinks, null)),
        };

        private static readonly ToolItem[] DataTools =
        {
            new ToolItem("데이터 검사", DialogueDataTools.CheckMenu, null),
            new ToolItem("데이터 패킹 (빌드용 파일 만들기)", DialogueDataTools.PackMenu, null),
            new ToolItem("CSV 내보내기 (JSON → CSV)", DialogueSheetTools.ExportMenu, null),
            new ToolItem("CSV 가져오기 (CSV → JSON, 전체)", DialogueSheetTools.ImportAllMenu, null),
            new ToolItem("UI 이미지 다시 만들기 (보스 · 하데스)", RegenerateImages, null),
            new ToolItem("패킹 파일 손상시키기 (폴백 테스트)", Step16_SceneSetup.CorruptPack, null),
        };

        [SerializeField] private List<string> openGroups = new List<string>();
        [NonSerialized] private GUIStyle statusStyle;
        [NonSerialized] private bool isStatusDirty = true;
        private readonly Dictionary<ToolItem, bool> statusCache = new Dictionary<ToolItem, bool>();
        private Vector2 scroll;

        [MenuItem("Tools/Dialogue/대화 툴 열기", priority = 0)]
        private static void Open()
        {
            GetWindow<DialogueToolWindow>(WindowTitle).Show();
        }

        private void OnEnable()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Undo.undoRedoPerformed += RefreshStatus;
        }

        private void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Undo.undoRedoPerformed -= RefreshStatus;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            RefreshStatus();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            RefreshStatus();
        }

        private void OnHierarchyChange()
        {
            RefreshStatus();
        }

        private void OnFocus()
        {
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            isStatusDirty = true;
            Repaint();
        }

        private bool StatusOf(ToolItem item)
        {
            if (isStatusDirty)
            {
                statusCache.Clear();
                isStatusDirty = false;
            }

            if (!statusCache.TryGetValue(item, out bool applied))
            {
                applied = item.IsApplied();
                statusCache[item] = applied;
            }

            return applied;
        }

        private void OnGUI()
        {
            statusStyle ??= new GUIStyle(EditorStyles.label) { richText = true };

            Scene scene = SceneManager.GetActiveScene();
            DrawHeader(scene);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                foreach (ToolGroup group in Groups) DrawGroup(group, scene);
                DrawGroup(new ToolGroup("데이터", DataTools), scene);
            }
        }

        private static void DrawHeader(Scene scene)
        {
            string path = string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path;
            EditorGUILayout.LabelField("현재 씬", path, EditorStyles.boldLabel);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Play 중에는 실행할 수 없습니다.", MessageType.Info);
            }
            else if (!SetupKit.IsLhmScene(scene))
            {
                EditorGUILayout.HelpBox("LHM 밖의 씬입니다. 프리팹, 토벌 출발, NPC 스프라이트, 단축키 정리, 고물상 맵, 데이터 항목만 실행됩니다.", MessageType.Warning);
            }
        }

        private void DrawGroup(ToolGroup group, Scene scene)
        {
            ToolItem[] tracked = group.Items.Where(i => i.IsApplied != null).ToArray();
            int applied = tracked.Count(StatusOf);
            string title = tracked.Length > 0 ? $"{group.Title}  ({applied}/{tracked.Length})" : group.Title;

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool wasOpen = openGroups.Contains(group.Title);
                bool isOpen = EditorGUILayout.Foldout(wasOpen, title, true);
                if (isOpen != wasOpen) SetOpen(group.Title, isOpen);

                if (group.Items.Any(i => i.InBatch))
                {
                    bool canRun = SetupKit.IsLhmScene(scene) && group.Items.Any(i => i.InBatch && !StatusOf(i));
                    using (new EditorGUI.DisabledScope(!canRun))
                    {
                        if (GUILayout.Button("빠진 것 적용", GUILayout.Width(ButtonWidth))) Defer(() => RunMissing(group));
                    }
                }
            }

            if (!openGroups.Contains(group.Title)) return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (ToolItem item in group.Items) DrawItem(item);
            }
        }

        private void DrawItem(ToolItem item)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(item.Label);
                string status = item.IsApplied == null ? "" : StatusOf(item) ? AppliedText : MissingText;
                GUILayout.Label(status, statusStyle, GUILayout.Width(StatusWidth));
                if (GUILayout.Button("실행", GUILayout.Width(ButtonWidth))) Defer(item.Run);
            }
        }

        private void SetOpen(string groupTitle, bool isOpen)
        {
            openGroups = isOpen
                ? openGroups.Append(groupTitle).ToList()
                : openGroups.Where(t => t != groupTitle).ToList();
        }

        private void Defer(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                try
                {
                    action();
                }
                finally
                {
                    if (this != null) RefreshStatus();
                }
            };
        }

        private static void RunMissing(ToolGroup group)
        {
            Scene scene = SceneManager.GetActiveScene();
            ToolItem[] missing = group.Items.Where(i => i.InBatch && !i.IsApplied()).ToArray();
            if (missing.Length == 0) return;

            string names = string.Join("\n", missing.Select(i => i.BatchWarning == null ? $"- {i.Label}" : $"- {i.Label}\n    {i.BatchWarning}"));
            if (!EditorUtility.DisplayDialog(WindowTitle,
                    $"'{scene.name}' 씬에 '{group.Title}'의 빠진 항목을 순서대로 적용합니다.\n{names}\n\n" +
                    "실패하면 그 자리에서 멈춥니다. 항목별 내용은 콘솔에 남습니다.\n" +
                    "씬은 자동 저장하지 않습니다. Ctrl+Z 한 번으로 씬 변경을 되돌립니다 (만들어진 이미지 · 패킹 파일은 남음).", "적용", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            string summary = ApplyAll(missing);
            Undo.SetCurrentGroupName($"{WindowTitle} — {group.Title}");
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[{WindowTitle}] {scene.path}\n{summary}");
            EditorUtility.DisplayDialog(WindowTitle, summary, "확인");
        }

        private static string ApplyAll(IEnumerable<ToolItem> items)
        {
            var summary = new StringBuilder();
            foreach (ToolItem item in items)
            {
                bool completed = RunInBatch(item, out List<string> notes);
                if (!completed || !item.IsApplied())
                {
                    summary.AppendLine($"실패: {item.Label}");
                    foreach (string note in notes) summary.AppendLine($"  {note}");
                    summary.AppendLine("여기서 멈췄습니다. 콘솔 로그를 확인하세요.");
                    return summary.ToString();
                }

                summary.AppendLine($"적용: {item.Label}");
            }

            summary.AppendLine("완료. Ctrl+S로 저장하세요.");
            return summary.ToString();
        }

        private static bool RunInBatch(ToolItem item, out List<string> notes)
        {
            bool completed = true;
            SetupKit.BeginBatch();
            try
            {
                item.Run();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                completed = false;
            }

            notes = SetupKit.EndBatch();
            return completed;
        }

        private static void RegenerateImages()
        {
            BossRaceArt.Generate();
            HadesDialogueStyleSetup.Rebuild();
        }

        private static bool Has<T>() where T : Object
        {
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include) != null;
        }

        private static DialogueManager Manager()
        {
            return Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        private static Object DialogueBoxField(string fieldName)
        {
            DialogueManager manager = Manager();
            if (manager == null) return null;

            using var managerSo = new SerializedObject(manager);
            Object box = managerSo.FindProperty("dialogueBox").objectReferenceValue;
            if (box == null) return null;

            using var boxSo = new SerializedObject(box);
            return boxSo.FindProperty(fieldName).objectReferenceValue;
        }

        private static bool HasPool(string poolName)
        {
            DialogueManager manager = Manager();
            if (manager == null) return false;

            using var so = new SerializedObject(manager);
            SerializedProperty pools = so.FindProperty("poolJsons");
            for (int i = 0; i < pools.arraySize; i++)
            {
                Object pool = pools.GetArrayElementAtIndex(i).objectReferenceValue;
                if (pool != null && pool.name == poolName) return true;
            }

            return false;
        }

        private static bool HasPackedData()
        {
            DialogueManager manager = Manager();
            if (manager == null) return false;

            using var so = new SerializedObject(manager);
            return so.FindProperty("packedData").objectReferenceValue != null;
        }

        private static bool HasHadesStyle()
        {
            var box = DialogueBoxField("box") as GameObject;
            return box != null && box.transform.Find(HadesDialogueStyleSetup.StyleRootName) != null;
        }
    }
}
