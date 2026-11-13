using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    // 씬의 소품 자리(SLOT_) 오브젝트에 NPC 기능을 붙이는 도구 (작업대 · 길드 접수원)
    // 작업대: E → 대화창 "작업대: ……" → 1. 제작하기 2. 수리하기 3. 나가기
    //   제작 · 수리: NpcChoiceMenu가 씬의 YJW CraftingUIManager를 찾아 그 창을 엶 (없으면 JunkShop_Map에서 복사해 옴)
    // 길드 접수원: E → 1. 토벌 신청 2. 랭킹 보드 3. 포상 수령 4. 대화 5. 나가기
    //   토벌 신청: 대화 시스템 프리팹 안의 HuntDeparture가 난이도 선택 → 보스 진입 대사 → 전투 씬으로 이어 줌
    public static class WorkbenchSetup
    {
        internal sealed class SlotNpcSpec
        {
            public string Title;
            public string Label;
            public string[] Keywords;
            public NpcRole Role;
            public string PoolId;
            public string PoolPath;
            public string SpeakerId;
            public string GreetingPoolId;
            public string Flow;
            public bool ImportCrafting;
        }

        private static readonly SlotNpcSpec WorkbenchSpec = new SlotNpcSpec
        {
            Title = "작업대 연결",
            Label = "작업대",
            Keywords = new[] { "Workbench", "작업대" },
            Role = NpcRole.Workbench,
            PoolId = "workbench",
            PoolPath = "Assets/LHM/03.Data/Dialogue/workbench.json",
            SpeakerId = "workbench",
            GreetingPoolId = "workbench",
            Flow = "E → '작업대: ……' → 1. 제작하기  2. 수리하기  3. 나가기",
            ImportCrafting = true,
        };

        private static readonly SlotNpcSpec ReceptionistSpec = new SlotNpcSpec
        {
            Title = "길드 접수원 연결",
            Label = "접수원",
            Keywords = new[] { "접수원", "Receptionist" },
            Role = NpcRole.GuildReceptionist,
            PoolId = "guild_npc",
            PoolPath = "Assets/LHM/03.Data/Dialogue/guild_npc.json",
            SpeakerId = "receptionist",
            GreetingPoolId = "",
            Flow = "E → 1. 토벌 신청  2. 랭킹 보드  3. 포상 수령  4. 대화  5. 나가기\n" +
                   "  토벌 신청 → 이번 시즌 보스 난이도 선택 → 보스 진입 대사 → 전투 씬 (대화 시스템 프리팹에 포함)\n" +
                   "  랭킹 보드 · 포상 수령은 시스템 연결 전이라 로그만",
            ImportCrafting = false,
        };

        private const string Title = "작업대 배치";
        private const string ObjectName = "작업대";
        // 다른 팀(LJS) 에셋은 참조만 함 (수정 · 이동하지 않음). 없으면 상자 모양으로 대신
        private const string ModelPath = "Assets/LJS/00. Assets/Kenney_suvival_kit/workbench.fbx";

        private static readonly SlotNpcSpec AuctionSpec = new SlotNpcSpec
        {
            Title = "경매장 NPC 연결",
            Label = "등록직원",
            Keywords = new[] { "등록직원", "Registrar" },
            Role = NpcRole.Other,
            PoolId = "auction_npc",
            PoolPath = "Assets/LHM/03.Data/Dialogue/auction_npc.json",
            SpeakerId = "auctioneer",
            GreetingPoolId = "",
            Flow = "E → 1. 대화  2. 나가기 (경매장 NPC 대사: 낙찰 후 · 주말 · 기본 등 상황별)",
            ImportCrafting = false,
        };

        internal static bool Exists() => HasRole(NpcRole.Workbench);
        internal static bool ReceptionistExists() => HasRole(NpcRole.GuildReceptionist);
        internal static bool AuctionExists() => Object.FindObjectsByType<NpcInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(n => n.SpeakerId == AuctionSpec.SpeakerId);
        internal static void LinkAuction() => Link(AuctionSpec);

        private static bool HasRole(NpcRole role)
        {
            return Object.FindObjectsByType<NpcInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(n => n.Role == role);
        }

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, Title)) return;

            var pool = AssetDatabase.LoadAssetAtPath<TextAsset>(WorkbenchSpec.PoolPath);
            DialogueManager manager = ActiveManager(scene);
            GameObject player = JunkShopDialogueSetup.FindPlayer(scene);
            string problem = CheckSystem(pool, WorkbenchSpec.PoolPath, manager, player);
            if (problem != null)
            {
                SetupKit.Dialog(Title, problem, "확인");
                return;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            bool exists = Exists();
            bool hasEventSystem = JunkShopDialogueSetup.FindInScene<UnityEngine.EventSystems.EventSystem>(scene).Any();

            if (!SetupKit.Dialog(Title,
                    $"'{scene.name}' 씬에 작업대를 배치합니다.\n" +
                    "- E로 말 걸면 대화창에 '작업대: ……' → 그 상태로 선택지\n" +
                    "  1. 제작하기  2. 수리하기  3. 나가기 (숫자 키로 바로 선택 가능)\n" +
                    $"- 제작 · 수리 창: {CraftingUiStatus(scene)}\n" +
                    $"- 모양: {(model != null ? "Kenney workbench 모델 (LJS 에셋 참조만)" : "회색 상자 (모델 없음)")}\n" +
                    $"- 위치: {(player != null ? $"플레이어({player.name}) 앞 3m" : "원점 (플레이어 없음)")}\n" +
                    $"- 대화 시스템: {SystemStatus(manager, player)}\n" +
                    (hasEventSystem ? "" : "- EventSystem 없음 → 추가 (선택지 마우스 클릭용)\n") +
                    "- DialogueManager 대사 풀에 workbench 추가\n" +
                    (exists ? "- 이미 작업대가 있어 하나 더 만듭니다\n" : "") +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)",
                    "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(Title);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[{Title}] {scene.path}\n");

            manager = EnsureSystem(scene, manager, pool, hasEventSystem, log);

            var root = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(root, Title);
            SceneManager.MoveGameObjectToScene(root, scene);
            Place(root.transform, player != null ? player.transform : null, log);

            float height = AddVisual(root.transform, model, log);

            Configure(root.AddComponent<NpcInteractable>(), height, WorkbenchSpec, log);


            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → 작업대에 다가가 E → 대사 넘기기(Space) → 1 · 2 · 3 또는 방향키 + E");
            Debug.Log(log.ToString());
        }

        // ── 씬에 이미 있는 작업대 오브젝트에 연결 ─────────────────────────────

        internal static void LinkExisting() => Link(WorkbenchSpec);
        internal static void LinkReceptionist() => Link(ReceptionistSpec);

        // 선택한 오브젝트, 없으면 이름에 키워드가 들어간 오브젝트 (SLOT_ 자리 우선)에 NPC 기능을 붙임
        // 대화 시스템 · EventSystem이 없는 씬이면 함께 배치
        private static void Link(SlotNpcSpec spec)
        {
            string title = spec.Title;
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, title)) return;

            GameObject target = FindTarget(scene, spec.Keywords, out string how);
            if (target == null)
            {
                SetupKit.Dialog(title, $"{spec.Label} 오브젝트를 찾지 못했습니다.\n하이어라키에서 {spec.Label} 오브젝트를 선택한 뒤 다시 누르세요.", "확인");
                return;
            }

            var pool = AssetDatabase.LoadAssetAtPath<TextAsset>(spec.PoolPath);
            DialogueManager manager = ActiveManager(scene);
            GameObject player = JunkShopDialogueSetup.FindPlayer(scene);
            bool hasEventSystem = JunkShopDialogueSetup.FindInScene<UnityEngine.EventSystems.EventSystem>(scene).Any();

            string problem = CheckSystem(pool, spec.PoolPath, manager, player);
            if (problem != null)
            {
                SetupKit.Dialog(title, problem, "확인");
                return;
            }

            bool importCrafting = spec.ImportCrafting && !CraftingSystemImport.HasCraftingUi(scene);
            string craftingLine = !spec.ImportCrafting ? ""
                : !importCrafting ? "- 제작 · 수리 창: 이미 있음 → 1. 제작하기 · 2. 수리하기로 실제 창이 열림\n"
                : $"- 제작 · 수리 창: 없음 → JunkShop_Map에서 복사해 옴: {string.Join(" · ", CraftingSystemImport.RootNames)}\n" +
                  "    (원본 씬은 잠깐 열어 복사만 하고 저장하지 않음 / 창 겹침 관리자는 제외)\n";

            if (!SetupKit.Dialog(title,
                    $"'{scene.name}' 씬의 {spec.Label}에 NPC 기능을 연결합니다.\n" +
                    $"- 대상: {PathOf(target.transform)} ({how})\n" +
                    "  (모양은 그대로, 상호작용 컴포넌트만 추가)\n" +
                    craftingLine +
                    $"- 대화 시스템: {SystemStatus(manager, player)}\n" +
                    $"- EventSystem: {(hasEventSystem ? "이미 있음" : "없음 → 추가 (선택지 마우스 클릭용)")}\n" +
                    $"- {spec.Flow}\n" +
                    "씬은 자동 저장하지 않습니다." +
                    (importCrafting ? "\n복사해 온 창은 Ctrl+Z로 되돌릴 수 없으니, 되돌리려면 저장하지 말고 씬을 다시 여세요." : " (Ctrl+Z로 되돌리기)"),
                    "연결", "취소"))
            {
                return;
            }

            var log = new StringBuilder($"[{title}] {scene.path}\n");

            // 복사는 다른 씬을 잠깐 여는 작업이라 Undo 묶음 밖에서 먼저 함
            if (importCrafting)
            {
                int imported = CraftingSystemImport.Import(scene, log);
                if (imported < 0)
                {
                    Debug.LogWarning(log.ToString());
                    SetupKit.Dialog(title, "제작 · 수리 창을 가져오지 못했습니다. 콘솔을 확인하세요.\n연결은 진행하지 않았습니다.", "확인");
                    return;
                }
                hasEventSystem = JunkShopDialogueSetup.FindInScene<UnityEngine.EventSystems.EventSystem>(scene).Any();
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(title);
            int group = Undo.GetCurrentGroup();

            EnsureSystem(scene, manager, pool, hasEventSystem, log);

            bool existed = target.TryGetComponent(out NpcInteractable npc);
            if (!existed) npc = Undo.AddComponent<NpcInteractable>(target);
            else Undo.RecordObject(npc, title);
            Configure(npc, HeightOf(target), spec, log);
            log.AppendLine($"- 대상: {PathOf(target.transform)} — NpcInteractable {(existed ? "설정 갱신" : "추가")}");

            Selection.activeGameObject = target;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine($"완료. Ctrl+S로 저장 후 Play → {spec.Label}에게 다가가 E → 숫자 키 또는 방향키 + E");
            Debug.Log(log.ToString());
        }

        private static GameObject FindTarget(Scene scene, string[] keywords, out string how)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.scene == scene)
            {
                // 임시 모형을 골랐어도 그 위의 SLOT_ 자리에 붙임 (진짜 모델로 바꿔도 기능이 남도록)
                Transform slot = selected.transform.parent;
                if (selected.name.StartsWith(TempPrefix) && slot != null && slot.name.StartsWith(SlotPrefix))
                {
                    how = "선택한 임시 모형의 소품 자리";
                    return slot.gameObject;
                }
                how = "선택한 오브젝트";
                return selected;
            }

            var candidates = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => keywords.Any(k => t.name.Contains(k)))
                .ToList();
            Transform best = candidates.FirstOrDefault(t => t.name.StartsWith(SlotPrefix))
                             ?? candidates.FirstOrDefault(t => !t.name.StartsWith(TempPrefix))
                             ?? candidates.FirstOrDefault();
            how = best != null && best.name.StartsWith(SlotPrefix) ? "이름으로 찾음 · 소품 자리" : "이름으로 찾음";
            return best != null ? best.gameObject : null;
        }

        private const string SlotPrefix = "SLOT_";
        private const string TempPrefix = "임시_";

        // 소품 자리(RSPropSlot)는 지정 크기, 아니면 보이는 모델 크기 (안내 문구 · 카메라 초점 높이용)
        private static float HeightOf(GameObject target)
        {
            if (target.TryGetComponent(out RealSteel.Build.RSPropSlot slot) && slot.size.y > 0.01f)
            {
                return slot.size.y * Mathf.Abs(target.transform.lossyScale.y);
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 1f;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return Mathf.Max(0.5f, bounds.max.y - target.transform.position.y);
        }

        private static void Configure(NpcInteractable npc, float height, SlotNpcSpec spec, StringBuilder log)
        {
            var so = new SerializedObject(npc);
            so.FindProperty("role").enumValueIndex = (int)spec.Role;
            so.FindProperty("poolId").stringValue = spec.PoolId;
            so.FindProperty("speakerId").stringValue = spec.SpeakerId;
            so.FindProperty("greetingPoolId").stringValue = spec.GreetingPoolId;
            so.FindProperty("interactRadius").floatValue = 2f;
            so.FindProperty("promptOffset").vector3Value = new Vector3(0f, height + 0.4f, 0f);
            so.FindProperty("closeUpFocusOffset").vector3Value = new Vector3(0f, height * 0.8f, 0f);
            so.ApplyModifiedProperties();
            string greeting = string.IsNullOrEmpty(spec.GreetingPoolId) ? "없음 (바로 선택지)" : spec.GreetingPoolId;
            log.AppendLine($"- NpcInteractable: 역할 {spec.Role} / 대사 풀 {spec.PoolId} / 화자 {spec.SpeakerId} / 인사 대사 {greeting} / 반경 2m / 안내 높이 {height + 0.4f:0.0}m");
        }

        private static string PathOf(Transform t)
        {
            return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        }

        // ── 새로 배치 ─────────────────────────────

        // ── 대화 시스템 확인 · 배치 (새로 배치 · 기존 연결 공용) ─────────────────────────────

        // 켜져 있는 DialogueManager만 (예전 대화 오브젝트가 꺼진 채 남아 있는 씬이 있음)
        private static DialogueManager ActiveManager(Scene scene)
        {
            return JunkShopDialogueSetup.FindInScene<DialogueManager>(scene).FirstOrDefault(m => m.gameObject.activeInHierarchy && m.enabled);
        }

        private static string CheckSystem(TextAsset pool, string poolPath, DialogueManager manager, GameObject player)
        {
            if (pool == null) return $"데이터 파일을 찾지 못했습니다.\n{poolPath}";
            if (manager != null) return null;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefabBuilder.SystemPrefabPath) == null) return "켜진 대화 시스템이 없고, 대화 시스템 프리팹도 없습니다. 대화 툴의 '프리팹 만들기'를 먼저 실행하세요.";
            if (player == null) return "켜진 대화 시스템이 없고, 플레이어(RSSimpleCharacter 또는 Player 태그)도 찾지 못해 대화 시스템을 연결할 수 없습니다.";
            return null;
        }

        private static string SystemStatus(DialogueManager manager, GameObject player)
        {
            return manager != null ? "이미 있음" : $"켜진 것 없음 → 프리팹 배치 + 씬 연결 (플레이어: {player.name}, 꺼진 예전 대화 오브젝트는 그대로 둠)";
        }

        // 제작 · 수리는 NpcChoiceMenu가 실행 중에 YJW CraftingUIManager를 이름으로 찾아 엶 — 여기선 있는지만 알려 줌
        private static string CraftingUiStatus(Scene scene)
        {
            bool found = JunkShopDialogueSetup.FindInScene<MonoBehaviour>(scene).Any(b => b != null && b.GetType().Name == "CraftingUIManager");
            return found ? "있음 (CraftingUIManager) → 1. 제작하기 · 2. 수리하기로 실제 창이 열림" : "없음 → 제작 · 수리를 골라도 콘솔 안내만 나오고 닫힘";
        }

        private static DialogueManager EnsureSystem(Scene scene, DialogueManager manager, TextAsset pool, bool hasEventSystem, StringBuilder log)
        {
            if (manager == null)
            {
                var systemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefabBuilder.SystemPrefabPath);
                GameObject system = DialogueSceneLinker.PlaceSystem(systemPrefab, scene, log);
                manager = system.GetComponentInChildren<DialogueManager>(true);
            }

            Undo.RecordObject(manager, Title);
            SetupKit.AddPool(manager, pool, log);

            if (!hasEventSystem)
            {
                var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, Title);
                SceneManager.MoveGameObjectToScene(eventSystem, scene);
                log.AppendLine("- EventSystem 추가 (Input System UI 모듈)");
            }

            return manager;
        }

        private static void Place(Transform root, Transform player, StringBuilder log)
        {
            if (player == null)
            {
                root.position = Vector3.zero;
                log.AppendLine("- 위치: 원점 (손으로 옮기세요)");
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            float ground = player.position.y;
            if (player.TryGetComponent(out CharacterController controller))
            {
                ground = player.TransformPoint(controller.center).y - controller.height * 0.5f * player.lossyScale.y;
            }

            Vector3 position = player.position + forward * 3f;
            root.position = new Vector3(position.x, ground, position.z);
            root.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            log.AppendLine("- 위치: 플레이어 앞 3m, 플레이어를 바라봄 (손으로 옮기면 됨)");
        }

        // 모양을 자식으로 붙이고 바닥에 맞춤. 반환값은 높이 (안내 문구 · 카메라 초점 위치용)
        private static float AddVisual(Transform root, GameObject model, StringBuilder log)
        {
            GameObject visual;
            if (model != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                visual.name = "Model";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                log.AppendLine($"- 모양: {ModelPath}");
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Model";
                visual.transform.SetParent(root, false);
                visual.transform.localScale = new Vector3(1.4f, 0.9f, 0.7f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                log.AppendLine("- 모양: 회색 상자 (Kenney 모델을 찾지 못함)");
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 1f;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

            // 모델 원점이 바닥이 아닐 수 있어 아래 면을 루트 높이에 맞춤
            visual.transform.position += Vector3.up * (root.position.y - bounds.min.y);
            return Mathf.Max(0.5f, bounds.size.y);
        }
    }
}
