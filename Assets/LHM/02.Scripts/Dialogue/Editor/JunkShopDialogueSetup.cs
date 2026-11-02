using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class JunkShopDialogueSetup
    {
        private const string UndoName = "고물상 맵 대화 시스템 배치";
        private const string SourceScenePath = "Assets/LHM/01.Scene/DialogueTest.unity";
        private const string JunkShopScenePath = "Assets/Scenes/Copys_1102/JunkShop_Map.unity";
        private const string BridgeName = "[Dialogue] FieldInputBridge";

        private static readonly string[] CopyRoots =
        {
            "DialogueCanvas",
            "DialogueDirector",
            "[Step09] DialogueManager",
            "[Step10] Input",
            "[Step10] UiStack (LJS 시간)",
            "[Step11-2] EventQueue",
            "[Step11] BossEntry",
            "[Step15] BubbleSystem",
            "[TalkCam] NpcTalkCamera",
            "[BossRace] Canvas",
            "[Difficulty] Canvas",
            "[SeasonCall] Canvas",
            "[SeasonCall] SeasonRaceCall",
        };

        private static readonly string[] NpcSpeakers = { "receptionist", "junkdealer" };
        private static readonly string[] MovementTypes = { "RSSimpleCharacter" };
        private static readonly string[] InteractionTypes = { "ScrapInteractor" };
        private static readonly string[] PromptTypes = { "ScrapInteractPrompt" };

        [MenuItem("Tools/Dialogue/고물상 맵 배치 (길드 접수원 · 고물상 아저씨 + 대화 시스템)")]
        private static void Setup()
        {
            Scene target = SceneManager.GetActiveScene();

            if (target.path != JunkShopScenePath)
            {
                int choice = EditorUtility.DisplayDialogComplex(UndoName,
                    $"지금 씬이 고물상 맵이 아닙니다.\n현재: {(string.IsNullOrEmpty(target.path) ? "(저장 안 된 씬)" : target.path)}\n고물상 맵: {JunkShopScenePath}",
                    "고물상 맵 열기", "취소", "현재 씬에 배치");
                if (choice == 1) return;
                if (choice == 0)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    target = EditorSceneManager.OpenScene(JunkShopScenePath, OpenSceneMode.Single);
                }
            }

            if (target.path == SourceScenePath)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueTest 씬 자체에는 배치할 수 없습니다. 원본 씬입니다.", "확인");
                return;
            }

            bool replace = false;
            if (FindInScene<DialogueManager>(target).Any())
            {
                if (!EditorUtility.DisplayDialog(UndoName,
                        "이 씬에 이미 대화 시스템이 배치되어 있습니다.\n" +
                        "이 툴이 만든 것(대화 시스템 오브젝트, FieldInputBridge, 길드 접수원·고물상 아저씨, 플레이어의 PlayerInteractor)을 지우고\n" +
                        "지금의 DialogueTest 기준으로 다시 배치할까요? NPC를 옮겨 둔 위치는 유지합니다.",
                        "지우고 다시 배치", "취소"))
                {
                    return;
                }
                replace = true;
            }

            GameObject player = FindPlayer(target);
            Camera targetCamera = FindGameplayCamera(target, player);
            if (targetCamera == null || player == null)
            {
                EditorUtility.DisplayDialog(UndoName, $"이 씬에서 {(targetCamera == null ? "메인 카메라(MainCamera 태그)" : "")}{(targetCamera == null && player == null ? ", " : "")}{(player == null ? $"플레이어({string.Join("/", MovementTypes)} 또는 Player 태그)" : "")}를 찾지 못했습니다.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(UndoName,
                    $"'{target.name}' 씬에 대화 시스템과 NPC를 배치합니다.\n" +
                    $"- 원본: {SourceScenePath} (잠깐 함께 열었다가 저장하지 않고 닫음)\n" +
                    $"- 복사: 대화창 UI, 매니저, 입력, UI 스택, 이벤트 큐, 말풍선, 클로즈업 카메라, 보스·난이도 선택, 시즌 전화 ({CopyRoots.Length}개)\n" +
                    $"- NPC: 길드 접수원, 고물상 아저씨 (플레이어 '{player.name}' 앞, 스프라이트 적용)\n" +
                    $"- 플레이어에 PlayerInteractor 추가, {BridgeName}: 대화 중 이동 잠금(ScrapInputGate), NPC 근처 고철 수집 E 끔\n" +
                    "- GameSceneManager F1~F5 씬 전환 단축키를 이 씬에서만 끔\n" +
                    "- KDU·LJS 스크립트와 원본 씬은 수정하지 않음\n" +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Scene source = SceneManager.GetSceneByPath(SourceScenePath);
            bool openedSource = false;
            if (!source.isLoaded)
            {
                source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
                openedSource = true;
            }
            else if (source.isDirty)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueTest 씬이 열려 있고 저장되지 않은 변경이 있습니다. 저장하거나 닫은 뒤 다시 실행하세요.", "확인");
                return;
            }

            SceneManager.SetActiveScene(target);
            var log = new StringBuilder($"[고물상 배치] {target.path}\n");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();

            var keptPoses = new Dictionary<string, Pose>();

            try
            {
                if (replace) RemovePrevious(target, player, keptPoses, log);
                var map = new Dictionary<Object, Object>();
                var copies = new List<GameObject>();

                GameObject[] sourceRoots = source.GetRootGameObjects();
                foreach (string rootName in CopyRoots)
                {
                    GameObject original = sourceRoots.FirstOrDefault(r => r.name == rootName);
                    if (original == null)
                    {
                        log.AppendLine($"- 건너뜀: 원본에 '{rootName}' 없음");
                        continue;
                    }
                    copies.Add(CopyInto(original, target, map));
                }

                var npcCopies = new List<GameObject>();
                foreach (string speaker in NpcSpeakers)
                {
                    NpcInteractable original = FindInScene<NpcInteractable>(source).FirstOrDefault(n => n.SpeakerId == speaker);
                    if (original == null)
                    {
                        log.AppendLine($"- 건너뜀: 원본에 Speaker Id '{speaker}' NPC 없음");
                        continue;
                    }
                    GameObject copy = CopyInto(original.gameObject, target, map);
                    copy.name = original.name;
                    npcCopies.Add(copy);
                }
                log.AppendLine($"- 복사: {copies.Count}개 + NPC {npcCopies.Count}명");

                PlayerInteractor sourceInteractor = FindInScene<PlayerInteractor>(source).FirstOrDefault();
                PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
                if (interactor == null)
                {
                    interactor = Undo.AddComponent<PlayerInteractor>(player);
                    if (sourceInteractor != null) EditorUtility.CopySerialized(sourceInteractor, interactor);
                    log.AppendLine($"- {player.name}: PlayerInteractor 추가");
                }
                if (sourceInteractor != null) map[sourceInteractor] = interactor;

                var context = new RemapContext
                {
                    map = map,
                    source = source,
                    target = target,
                    sourcePlayer = sourceInteractor != null ? sourceInteractor.gameObject : null,
                    player = player,
                    camera = targetCamera,
                };

                var unresolved = new HashSet<string>();
                foreach (GameObject copy in copies.Concat(npcCopies))
                {
                    foreach (Component component in copy.GetComponentsInChildren<Component>(true)) Remap(component, context, unresolved);
                }
                Remap(interactor, context, unresolved);
                if (unresolved.Count > 0) log.AppendLine($"- 원본 씬에만 있어 비운 연결: {string.Join(", ", unresolved)}");

                MatchCloseUpCamera(copies, targetCamera, log);
                PlaceNpcs(npcCopies, player, keptPoses, log);
                RaiseDialogueCanvas(copies, target, log);
                BindShopWindow(target, log);
                RealSteel.BossRace.EditorTools.HuntDepartureSetup.Create(target, log);
                AddBridge(interactor, player, target, log);
                ApplyShortcuts(target, log);
                DisableSceneShortcuts(target, log);
            }
            finally
            {
                if (openedSource) EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(target);
            }

            NpcSpriteSetup.Apply(log);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(target);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → NPC 앞 E. 처음 Play 때 세이브 경고가 나오면 Shift+F12.");
            Debug.Log(log.ToString());
        }

        private class RemapContext
        {
            public Dictionary<Object, Object> map;
            public Scene source;
            public Scene target;
            public GameObject sourcePlayer;
            public GameObject player;
            public Camera camera;
        }

        private static GameObject CopyInto(GameObject original, Scene target, Dictionary<Object, Object> map)
        {
            GameObject copy = Object.Instantiate(original);
            copy.name = original.name;
            SceneManager.MoveGameObjectToScene(copy, target);
            Undo.RegisterCreatedObjectUndo(copy, UndoName);

            Transform[] from = original.GetComponentsInChildren<Transform>(true);
            Transform[] to = copy.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < from.Length && i < to.Length; i++)
            {
                map[from[i].gameObject] = to[i].gameObject;
                Component[] a = from[i].GetComponents<Component>();
                Component[] b = to[i].GetComponents<Component>();
                for (int j = 0; j < a.Length && j < b.Length; j++)
                {
                    if (a[j] != null && b[j] != null) map[a[j]] = b[j];
                }
            }
            return copy;
        }

        private static void Remap(Component component, RemapContext context, HashSet<string> unresolved)
        {
            if (component == null) return;

            var so = new SerializedObject(component);
            SerializedProperty property = so.GetIterator();
            bool changed = false;

            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object value = property.objectReferenceValue;
                if (value == null) continue;

                Object mapped = Resolve(value, context, out bool external);
                if (mapped == value) continue;

                property.objectReferenceValue = mapped;
                changed = true;
                if (mapped == null && external) unresolved.Add($"{component.GetType().Name}.{property.name}");
            }

            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Object Resolve(Object value, RemapContext context, out bool external)
        {
            external = false;
            if (context.map.TryGetValue(value, out Object mapped)) return mapped;

            GameObject go = value as GameObject;
            if (go == null && value is Component c) go = c.gameObject;
            if (go == null || go.scene != context.source) return value;

            external = true;
            if (go.GetComponent<Camera>() != null) return Equivalent(value, context.camera.gameObject);
            if (context.sourcePlayer != null && go == context.sourcePlayer) return Equivalent(value, context.player);
            if (value is TimeSystemManager) return FindInScene<TimeSystemManager>(context.target).FirstOrDefault();
            return null;
        }

        private static Object Equivalent(Object value, GameObject target)
        {
            if (value is GameObject) return target;
            if (value is Transform) return target.transform;
            return value is Component component ? target.GetComponent(component.GetType()) : null;
        }

        private static void MatchCloseUpCamera(List<GameObject> copies, Camera mainCamera, StringBuilder log)
        {
            foreach (GameObject copy in copies)
            {
                NpcTalkCamera talk = copy.GetComponentInChildren<NpcTalkCamera>(true);
                if (talk == null) continue;

                var so = new SerializedObject(talk);
                var closeUp = so.FindProperty("closeUpCamera").objectReferenceValue as Camera;
                if (closeUp == null) continue;

                EditorUtility.CopySerialized(mainCamera, closeUp);
                closeUp.depth = mainCamera.depth + 1f;
                closeUp.nearClipPlane = Mathf.Min(closeUp.nearClipPlane, 0.1f);
                closeUp.gameObject.layer = mainCamera.gameObject.layer;
                foreach (Component component in mainCamera.GetComponents<Component>())
                {
                    if (component == null || component.GetType().Name != "UniversalAdditionalCameraData") continue;
                    Component data = closeUp.GetComponent(component.GetType());
                    if (data == null) data = closeUp.gameObject.AddComponent(component.GetType());
                    EditorUtility.CopySerialized(component, data);
                }

                so.FindProperty("approach").enumValueIndex = (int)TalkCameraApproach.PlayerSide;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine($"- 클로즈업 카메라: '{mainCamera.name}' 설정(URP·안개 등) 복사, near clip {closeUp.nearClipPlane:0.##}m ('{mainCamera.name}'은 {mainCamera.nearClipPlane:0.##}m), 접근 방향 플레이어 쪽");
            }
        }

        private const string HelpName = "[Help] ShortcutHelp";
        private static readonly string[] UiHotkeyTypes = { "InventoryToggle", "CraftingUIManager" };

        [MenuItem("Tools/Dialogue/단축키 정리 적용 (Home 안내 · 대화 중 UI 키 잠금)")]
        private static void ApplyShortcutsMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            var log = new StringBuilder($"[단축키 정리] {scene.path}\n");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("단축키 정리 적용");
            int group = Undo.GetCurrentGroup();

            ApplyShortcuts(scene, log);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → Home.");
            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog("단축키 정리 적용", log.ToString(), "확인");
        }

        internal static void ApplyShortcuts(Scene scene, StringBuilder log)
        {
            if (!FindInScene<ShortcutHelpOverlay>(scene).Any())
            {
                var go = new GameObject(HelpName);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "단축키 정리 적용");
                go.AddComponent<ShortcutHelpOverlay>();
                log.AppendLine($"- {HelpName} 생성 (Home: 지금 씬의 단축키 안내)");
            }
            else
            {
                log.AppendLine($"- {HelpName} 이미 있음");
            }

            var hotkeys = FindInScene<Behaviour>(scene).Where(b => UiHotkeyTypes.Contains(b.GetType().Name)).ToArray();
            var menu = FindInScene<NpcChoiceMenu>(scene).FirstOrDefault();
            foreach (FieldInputBridge bridge in FindInScene<FieldInputBridge>(scene))
            {
                var so = new SerializedObject(bridge);
                Undo.RecordObject(bridge, "단축키 정리 적용");
                so.FindProperty("menu").objectReferenceValue = menu;
                SetArray(so.FindProperty("uiHotkeys"), hotkeys);
                so.ApplyModifiedProperties();
                log.AppendLine($"- {bridge.name}: 대화 중 잠글 UI 단축키 {hotkeys.Length}개 ({string.Join(", ", hotkeys.Select(h => h.GetType().Name))}), 상점 창이 열린 동안은 잠그지 않음");
            }
        }

        private const string ShopWindowName = "ShopWindow";

        private static GameObject FindShopWindow(Scene scene)
        {
            foreach (MonoBehaviour behaviour in FindInScene<MonoBehaviour>(scene))
            {
                if (behaviour == null || behaviour.GetType().Name != "CraftingUIManager") continue;
                var window = new SerializedObject(behaviour).FindProperty("shopWindow")?.objectReferenceValue as GameObject;
                if (window != null) return window;
            }

            return scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .FirstOrDefault(g => g.name == ShopWindowName);
        }

        private static bool BindShopWindow(Scene scene, StringBuilder log)
        {
            GameObject window = FindShopWindow(scene);
            var menus = FindInScene<NpcChoiceMenu>(scene).ToList();
            if (window == null || menus.Count == 0)
            {
                log.AppendLine($"- 주의: {(window == null ? $"'{ShopWindowName}'(YJW 상점 창)을 찾지 못해" : "NpcChoiceMenu가 없어")} 상점은 임시 패널로 열림");
                return false;
            }

            foreach (NpcChoiceMenu menu in menus)
            {
                var so = new SerializedObject(menu);
                Undo.RecordObject(menu, UndoName);
                so.FindProperty("shopWindow").objectReferenceValue = window;
                so.ApplyModifiedProperties();
            }

            log.AppendLine($"- 고물상 아저씨 '상점 열기' → '{window.name}' 창 (닫기 버튼·Esc로 닫으면 대화 종료)");
            return true;
        }

        [MenuItem("Tools/Dialogue/고물상 맵 — 상점 창(ShopWindow) 연결")]
        private static void BindShopWindowMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            var log = new StringBuilder($"[상점 창 연결] {scene.path}\n");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("상점 창 연결");
            int group = Undo.GetCurrentGroup();

            bool ok = BindShopWindow(scene, log);

            Undo.CollapseUndoOperations(group);
            if (ok) EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("상점 창 연결", log.ToString(), "확인");
            Debug.Log(log.ToString());
        }

        private static void RemovePrevious(Scene target, GameObject player, Dictionary<string, Pose> keptPoses, StringBuilder log)
        {
            int removed = 0;
            foreach (GameObject root in target.GetRootGameObjects())
            {
                NpcInteractable npc = root.GetComponent<NpcInteractable>();
                bool ours = CopyRoots.Contains(root.name) || root.name == BridgeName || root.name == HelpName || root.name == RealSteel.BossRace.EditorTools.HuntDepartureSetup.RootName ||
                            (npc != null && NpcSpeakers.Contains(npc.SpeakerId));
                if (!ours) continue;

                if (npc != null) keptPoses[npc.SpeakerId] = new Pose(root.transform.position, root.transform.rotation);
                Undo.DestroyObjectImmediate(root);
                removed++;
            }

            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            if (interactor != null)
            {
                Undo.DestroyObjectImmediate(interactor);
                removed++;
            }

            log.AppendLine($"- 기존 배치 {removed}개 삭제 (NPC 위치 {keptPoses.Count}개 기억)");
        }

        private static void RaiseDialogueCanvas(List<GameObject> copies, Scene target, StringBuilder log)
        {
            var ours = new HashSet<Canvas>(copies.SelectMany(c => c.GetComponentsInChildren<Canvas>(true)));
            int highest = FindInScene<Canvas>(target)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && !ours.Contains(c))
                .Select(c => c.sortingOrder)
                .DefaultIfEmpty(0)
                .Max();

            foreach (GameObject copy in copies)
            {
                Canvas canvas = copy.GetComponent<Canvas>();
                if (canvas == null || copy.name != "DialogueCanvas") continue;

                int order = Mathf.Max(canvas.sortingOrder, highest + 10);
                canvas.sortingOrder = order;
                log.AppendLine($"- DialogueCanvas 그리기 순서 {order} (이 씬 UI의 가장 높은 순서 {highest}보다 위). 보스·난이도 선택 {Mathf.Max(40, order + 1)}~, 시즌 전화 {Mathf.Max(45, order + 5)}");

                foreach (GameObject other in copies)
                {
                    Canvas otherCanvas = other.GetComponent<Canvas>();
                    if (otherCanvas == null || other == copy) continue;
                    int minimum = other.name.StartsWith("[SeasonCall]") ? order + 5 : order + 1;
                    if (otherCanvas.sortingOrder < minimum) otherCanvas.sortingOrder = minimum;
                }
            }
        }

        private static void PlaceNpcs(List<GameObject> npcs, GameObject player, Dictionary<string, Pose> keptPoses, StringBuilder log)
        {
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float ground = player.transform.position.y;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) ground = player.transform.TransformPoint(controller.center).y - controller.height * 0.5f * player.transform.lossyScale.y;

            for (int i = 0; i < npcs.Count; i++)
            {
                GameObject npc = npcs[i];
                NpcInteractable interactable = npc.GetComponent<NpcInteractable>();

                var npcSo = new SerializedObject(interactable);
                SerializedProperty radius = npcSo.FindProperty("interactRadius");
                if (radius.floatValue < 2f) radius.floatValue = 2f;
                npcSo.ApplyModifiedPropertiesWithoutUndo();

                if (keptPoses.TryGetValue(interactable.SpeakerId, out Pose kept))
                {
                    npc.transform.SetPositionAndRotation(kept.position, kept.rotation);
                    log.AppendLine($"- {npc.name}: 이전 위치 유지, 상호작용 반경 2m");
                    continue;
                }

                float side = npcs.Count == 1 ? 0f : (i == 0 ? -1.5f : 1.5f);
                Vector3 position = player.transform.position + forward * 3f + right * side;
                npc.transform.position = new Vector3(position.x, ground, position.z);
                npc.transform.position += Vector3.up * (ground - NpcSpriteSetup.FeetOf(npc.transform).y);
                npc.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

                log.AppendLine($"- {npc.name}: 플레이어 앞 3m {(side < 0 ? "왼쪽" : side > 0 ? "오른쪽" : "")} 배치 (위치는 손으로 옮기면 됨, 상호작용 반경 2m)");
            }
        }

        private static void AddBridge(PlayerInteractor interactor, GameObject player, Scene target, StringBuilder log)
        {
            var go = new GameObject(BridgeName);
            SceneManager.MoveGameObjectToScene(go, target);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            var bridge = go.AddComponent<FieldInputBridge>();

            var movement = player.GetComponents<Behaviour>().Where(b => MovementTypes.Contains(b.GetType().Name)).ToArray();
            var interactions = FindInScene<Behaviour>(target).Where(b => InteractionTypes.Contains(b.GetType().Name)).ToArray();
            var prompts = FindInScene<Behaviour>(target).Where(b => PromptTypes.Contains(b.GetType().Name)).ToArray();
            var promptRoots = prompts
                .Select(p => new SerializedObject(p).FindProperty("_root")?.objectReferenceValue as GameObject)
                .Where(g => g != null)
                .ToArray();

            var so = new SerializedObject(bridge);
            so.FindProperty("interactor").objectReferenceValue = interactor;
            SetArray(so.FindProperty("movement"), movement);
            SetArray(so.FindProperty("interactions"), interactions.Concat(prompts).ToArray());
            SetArray(so.FindProperty("hiddenPrompts"), promptRoots);
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"- {BridgeName}: 대화 중 끌 이동 {movement.Length}개({string.Join(", ", movement.Select(m => m.GetType().Name))}), NPC 근처에서 끌 상호작용 {interactions.Length}개 + 안내 {promptRoots.Length}개, 미니게임 중 NPC 상호작용 끔");
        }

        private static void DisableSceneShortcuts(Scene target, StringBuilder log)
        {
            foreach (GameSceneManager manager in FindInScene<GameSceneManager>(target))
            {
                var so = new SerializedObject(manager);
                SerializedProperty enabled = so.FindProperty("_shortcutEnabled");
                if (enabled == null || !enabled.boolValue) continue;
                Undo.RecordObject(manager, UndoName);
                enabled.boolValue = false;
                so.ApplyModifiedProperties();
                log.AppendLine($"- {manager.name}: F1~F5 씬 전환 단축키 끔 (대화 테스트 키와 겹침)");
            }
        }

        private static Camera FindGameplayCamera(Scene scene, GameObject player)
        {
            if (player != null)
            {
                foreach (Behaviour movement in player.GetComponents<Behaviour>().Where(b => MovementTypes.Contains(b.GetType().Name)))
                {
                    var cam = new SerializedObject(movement).FindProperty("cam")?.objectReferenceValue as Camera;
                    if (cam != null) return cam;
                }
            }

            var closeUps = new HashSet<Camera>(FindInScene<NpcTalkCamera>(scene)
                .Select(t => new SerializedObject(t).FindProperty("closeUpCamera").objectReferenceValue as Camera)
                .Where(c => c != null));

            return FindInScene<Camera>(scene)
                       .Where(c => c.targetTexture == null && c.enabled && c.gameObject.activeInHierarchy && !closeUps.Contains(c))
                       .OrderByDescending(c => c.depth)
                       .FirstOrDefault() ??
                   FindInScene<Camera>(scene).FirstOrDefault(c => c.CompareTag("MainCamera"));
        }

        [MenuItem("Tools/Dialogue/고물상 맵 — 대화 카메라 다시 연결")]
        private static void RebindCameras()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject player = FindPlayer(scene);
            Camera gameplay = FindGameplayCamera(scene, player);
            if (gameplay == null || !FindInScene<DialogueManager>(scene).Any())
            {
                EditorUtility.DisplayDialog("대화 카메라 다시 연결", "이 씬에 대화 시스템이 없거나, 플레이어가 쓰는 카메라를 찾지 못했습니다.", "확인");
                return;
            }

            var talkCameras = FindInScene<NpcTalkCamera>(scene).ToList();
            var closeUps = new HashSet<Camera>(talkCameras
                .Select(t => new SerializedObject(t).FindProperty("closeUpCamera").objectReferenceValue as Camera)
                .Where(c => c != null));
            var wrong = new HashSet<Camera>(FindInScene<Camera>(scene).Where(c => c != gameplay && !closeUps.Contains(c)));

            if (!EditorUtility.DisplayDialog("대화 카메라 다시 연결",
                    $"플레이어가 쓰는 카메라: '{gameplay.name}' (depth {gameplay.depth})\n" +
                    $"대화 시스템이 '{string.Join(", ", wrong.Select(c => c.name))}'를 가리키는 연결을 모두 '{gameplay.name}'으로 바꾸고,\n" +
                    "클로즈업 카메라 설정·그리는 순서를 다시 맞춥니다.\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "다시 연결", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("대화 카메라 다시 연결");
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[대화 카메라 다시 연결] {scene.path} → '{gameplay.name}'\n");

            foreach (MonoBehaviour behaviour in FindInScene<MonoBehaviour>(scene))
            {
                if (behaviour == null || behaviour.GetType().Namespace == null || !behaviour.GetType().Namespace.StartsWith("RealSteel")) continue;

                var so = new SerializedObject(behaviour);
                SerializedProperty property = so.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_GameObject") continue;
                    Object value = property.objectReferenceValue;
                    GameObject go = value as GameObject ?? (value as Component)?.gameObject;
                    Camera cam = go != null ? go.GetComponent<Camera>() : null;
                    if (cam == null || !wrong.Contains(cam)) continue;

                    property.objectReferenceValue = Equivalent(value, gameplay.gameObject);
                    changed = true;
                    log.AppendLine($"- {behaviour.name}.{behaviour.GetType().Name}.{property.name}: {cam.name} → {gameplay.name}");
                }

                if (changed)
                {
                    Undo.RecordObject(behaviour, "대화 카메라 다시 연결");
                    so.ApplyModifiedProperties();
                }
            }

            foreach (NpcTalkCamera talk in talkCameras) Undo.RecordObject(talk, "대화 카메라 다시 연결");
            foreach (Camera closeUp in closeUps) Undo.RecordObject(closeUp, "대화 카메라 다시 연결");
            MatchCloseUpCamera(talkCameras.Select(t => t.gameObject).ToList(), gameplay, log);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → NPC 앞 E.");
            Debug.Log(log.ToString());
        }

        private static GameObject FindPlayer(Scene scene)
        {
            Behaviour movement = FindInScene<Behaviour>(scene).FirstOrDefault(b => MovementTypes.Contains(b.GetType().Name));
            if (movement != null) return movement.gameObject;
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).FirstOrDefault(g => g.CompareTag("Player"));
        }

        private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
