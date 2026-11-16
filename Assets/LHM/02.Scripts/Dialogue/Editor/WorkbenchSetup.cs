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
            // true면 LHM 밖의 씬(다른 팀 씬)에서도 실행 가능
            public bool AllowOutsideLhm;
            // 이름에 이 키워드가 든 오브젝트(데스크 등) 쪽을 NPC의 정면으로 보고, 그쪽에 클로즈업 카메라 자리를 만듦. 비우면 만들지 않음
            public string[] FrontKeywords;
            // true면 상점 창(ShopWindow)을 NpcChoiceMenu · Binder에 연결
            public bool BindShop;
            // 함께 등록할 대사 풀 (혼잣말 등)
            public string[] ExtraPoolPaths;
            // true면 필드 카메라가 보던 쪽에서, 오브젝트 크기에 맞춘 거리로 클로즈업 자리를 만듦 (큰 소품용)
            public bool CloseUpFromCamera;
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
            AllowOutsideLhm = true,
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
            Role = NpcRole.Auction,
            PoolId = "auction_npc",
            PoolPath = "Assets/LHM/03.Data/Dialogue/auction_npc.json",
            SpeakerId = "auctioneer",
            GreetingPoolId = "",
            Flow = "E → 1. 경매 (→ 1. 일반 경매  2. 주먹 경매  3. 뒤로)  2. 옵션 (경매장 판매 창)  3. 대화하기  4. 나가기\n" +
                   "  경매 · 옵션은 씬의 경매 관리자(LSH Auction)를 그대로 씀 — 경매 시작 후 모드 선택 창에서 고른 모드를 대신 누름",
            ImportCrafting = false,
            AllowOutsideLhm = true,
            FrontKeywords = new[] { "등록데스크", "Reception" },
        };

        // 고철 매입 기계 (압축기 등): E → '기계: ...' → 1. 상점 열기 2. 나가기. 근처를 지나가면 "헐값... 매입중.." 혼잣말
        // 상점 창(YJW ShopWindow)이 없는 씬이면 제작 · 수리 창 묶음째 복사해 옴. 덩치가 커서 클로즈업은 멀리서 (필드 카메라 쪽)
        private static readonly SlotNpcSpec CompressorSpec = new SlotNpcSpec
        {
            Title = "고물상 판매 기계 연결",
            Label = "판매 기계 (압축기 등)",
            Keywords = new[] { "압축기", "Compressor" },
            Role = NpcRole.Compressor,
            PoolId = "compressor",
            PoolPath = "Assets/LHM/03.Data/Dialogue/compressor.json",
            ExtraPoolPaths = new[] { "Assets/LHM/03.Data/Dialogue/mono_compressor.json" },
            SpeakerId = "compressor",
            GreetingPoolId = "compressor",
            Flow = "E → '기계: ...' → 1. 상점 열기 (YJW 상점 창, 닫으면 종료)  2. 나가기\n" +
                   "  근처를 지나가면 머리 위 혼잣말 \"헐값... 매입중..\"",
            ImportCrafting = true,
            AllowOutsideLhm = true,
            BindShop = true,
            CloseUpFromCamera = true,
        };

        internal static bool CompressorExists() => HasRole(NpcRole.Compressor);
        internal static void LinkCompressor() => Link(CompressorSpec);

        internal static bool Exists() => HasRole(NpcRole.Workbench);
        internal static bool ReceptionistExists() => HasRole(NpcRole.GuildReceptionist);
        internal static bool AuctionExists() => Object.FindObjectsByType<NpcInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(n => n.SpeakerId == AuctionSpec.SpeakerId && n.Role == AuctionSpec.Role);
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
            if (!spec.AllowOutsideLhm && !SetupKit.RequireLhmScene(scene, title)) return;
            bool outside = !SetupKit.IsLhmScene(scene);

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

            // 같은 NPC 기능이 다른 오브젝트에도 붙어 있으면 '[E] 이름' 안내가 두 번 뜸 → 대상 하나만 남기고 지움
            NpcInteractable[] duplicates = FindDuplicates(scene, spec, target);
            string duplicateLine = duplicates.Length == 0 ? ""
                : $"- 중복 정리: 같은 NPC 기능이 {duplicates.Length}곳에 더 붙어 있어 지움 (안내가 두 번 뜨던 원인)\n" +
                  string.Concat(duplicates.Select(d => $"    {PathOf(d.transform)}\n"));

            bool importCrafting = spec.ImportCrafting && !CraftingSystemImport.HasCraftingUi(scene);
            string craftingLine = !spec.ImportCrafting ? ""
                : !importCrafting ? "- 제작 · 수리 창: 이미 있음 → 1. 제작하기 · 2. 수리하기로 실제 창이 열림\n"
                : $"- 제작 · 수리 창: 없음 → JunkShop_Map에서 복사해 옴: {string.Join(" · ", CraftingSystemImport.RootNames)}\n" +
                  "    (원본 씬은 잠깐 열어 복사만 하고 저장하지 않음 / 창 겹침 관리자는 제외)\n";

            if (!SetupKit.Dialog(title,
                    $"'{scene.name}' 씬의 {spec.Label}에 NPC 기능을 연결합니다.\n" +
                    (outside ? $"※ LHM 밖의 씬입니다: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}\n  다른 팀 씬이면 저장 전에 담당자와 확인하세요.\n" : "") +
                    $"- 대상: {PathOf(target.transform)} ({how})\n" +
                    "  (모양은 그대로, 상호작용 컴포넌트만 추가)\n" +
                    craftingLine +
                    $"- 대화 시스템: {SystemStatus(manager, player)}\n" +
                    $"- EventSystem: {(hasEventSystem ? "이미 있음" : "없음 → 추가 (선택지 마우스 클릭용)")}\n" +
                    duplicateLine +
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

            manager = EnsureSystem(scene, manager, pool, hasEventSystem, log);
            foreach (string extraPath in spec.ExtraPoolPaths ?? new string[0])
            {
                var extra = AssetDatabase.LoadAssetAtPath<TextAsset>(extraPath);
                if (extra != null) SetupKit.AddPool(manager, extra, log);
                else log.AppendLine($"- 주의: 대사 풀 파일 없음 {extraPath}");
            }
            if (spec.BindShop) JunkShopDialogueSetup.BindShopWindow(scene, log);

            foreach (NpcInteractable duplicate in duplicates)
            {
                string path = PathOf(duplicate.transform);
                Undo.DestroyObjectImmediate(duplicate);
                log.AppendLine($"- 중복 삭제: {path}의 NpcInteractable");
            }

            bool existed = target.TryGetComponent(out NpcInteractable npc);
            if (!existed) npc = Undo.AddComponent<NpcInteractable>(target);
            else Undo.RecordObject(npc, title);
            Configure(npc, HeightOf(target), spec, log);
            if (spec.CloseUpFromCamera) SetupWideCloseUpPoint(npc, scene, spec, player, log);
            else if (spec.FrontKeywords != null && spec.FrontKeywords.Length > 0) SetupCloseUpPoint(npc, scene, spec, HeightOf(target), log);
            log.AppendLine($"- 대상: {PathOf(target.transform)} — NpcInteractable {(existed ? "설정 갱신" : "추가")}");

            Selection.activeGameObject = target;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine($"완료. Ctrl+S로 저장 후 Play → {spec.Label}에게 다가가 E → 숫자 키 또는 방향키 + E");
            Debug.Log(log.ToString());
        }

        // 대상이 아닌 오브젝트에 붙은 같은 NPC(같은 화자, 접수원 · 작업대는 같은 역할) 기능
        private static NpcInteractable[] FindDuplicates(Scene scene, SlotNpcSpec spec, GameObject target)
        {
            return JunkShopDialogueSetup.FindInScene<NpcInteractable>(scene)
                .Where(n => n.gameObject != target)
                .Where(n => n.SpeakerId == spec.SpeakerId || (spec.Role != NpcRole.Other && n.Role == spec.Role))
                .ToArray();
        }

        private static GameObject FindTarget(Scene scene, string[] keywords, out string how)
        {
            var candidates = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => keywords.Any(k => t.name.Contains(k)))
                .ToList();

            // 플레이어(또는 그 자식)를 골라 둔 채 누르면 플레이어가 NPC가 되어 버림 → 선택 무시
            // 이름이 맞는 오브젝트가 씬에 있으면 이름과 상관없는 선택보다 그쪽을 우선
            GameObject selected = Selection.activeGameObject;
            GameObject player = JunkShopDialogueSetup.FindPlayer(scene);
            bool selectedIsPlayer = selected != null && player != null && selected.transform.IsChildOf(player.transform);
            bool selectedMatches = selected != null && keywords.Any(k => selected.name.Contains(k));
            if (selected != null && selected.scene == scene && !selectedIsPlayer && (selectedMatches || candidates.Count == 0))
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

            Transform best = candidates.FirstOrDefault(t => t.name.StartsWith(SlotPrefix))
                             ?? candidates.FirstOrDefault(t => !t.name.StartsWith(TempPrefix))
                             ?? candidates.FirstOrDefault();
            how = best != null && best.name.StartsWith(SlotPrefix) ? "이름으로 찾음 · 소품 자리" : "이름으로 찾음";
            if (selectedIsPlayer) how += " (선택한 플레이어는 대상에서 제외)";
            else if (selected != null && !selectedMatches && best != null) how += $" (선택한 '{selected.name}'은 이름이 달라 제외)";
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

        private const string CloseUpPointName = "CloseUpPoint";
        private const float CloseUpDistance = 2.2f;
        private const float CloseUpCameraHeightRatio = 1.05f;
        private const float CloseUpLookHeightRatio = 0.85f;

        // NPC 정면(데스크 쪽)에 카메라 자리를 만들어 클로즈업이 등 뒤가 아니라 얼굴 쪽에서, 머리까지 보이게 함
        // 큰 소품(압축기 등): 필드 카메라가 보던 방향에서, 전체가 화면에 들어오도록 크기에 맞춘 거리 · 살짝 위에서 내려다봄
        private static void SetupWideCloseUpPoint(NpcInteractable npc, Scene scene, SlotNpcSpec spec, GameObject player, StringBuilder log)
        {
            Transform npcTransform = npc.transform;
            Renderer[] renderers = npcTransform.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(npcTransform.position, Vector3.one * 2f);
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

            Camera camera = JunkShopDialogueSetup.FindGameplayCamera(scene, player);
            Vector3 toCamera = camera != null ? Vector3.ProjectOnPlane(-camera.transform.forward, Vector3.up) : Vector3.zero;
            if (toCamera.sqrMagnitude < 0.0001f) toCamera = Vector3.ProjectOnPlane(-npcTransform.forward, Vector3.up);
            if (toCamera.sqrMagnitude < 0.0001f) toCamera = Vector3.back;
            toCamera.Normalize();

            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float distance = Mathf.Max(4f, size * 1.8f);
            Vector3 lookAt = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.45f, bounds.center.z);
            Vector3 cameraPosition = lookAt + toCamera * distance + Vector3.up * Mathf.Max(1.5f, bounds.size.y * 0.6f);

            Transform point = npcTransform.Find(CloseUpPointName);
            if (point == null)
            {
                var go = new GameObject(CloseUpPointName);
                Undo.RegisterCreatedObjectUndo(go, spec.Title);
                Undo.SetTransformParent(go.transform, npcTransform, spec.Title);
                point = go.transform;
            }
            else
            {
                Undo.RecordObject(point, spec.Title);
            }
            point.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(lookAt - cameraPosition, Vector3.up));

            var so = new SerializedObject(npc);
            so.FindProperty("closeUpPoint").objectReferenceValue = point;
            so.FindProperty("closeUpFocusOffset").vector3Value = lookAt - npcTransform.position;
            so.ApplyModifiedProperties();

            log.AppendLine($"- 클로즈업 카메라: {(camera != null ? $"필드 카메라({camera.name})가 보던 쪽" : "오브젝트 앞쪽")}에서 {distance:0.0}m 떨어져 전체가 보이게 ({PathOf(point)}, 위치는 직접 옮겨도 됨)");
        }

        private static void SetupCloseUpPoint(NpcInteractable npc, Scene scene, SlotNpcSpec spec, float height, StringBuilder log)
        {
            Transform npcTransform = npc.transform;
            Transform front = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t != npcTransform && !t.IsChildOf(npcTransform) && spec.FrontKeywords.Any(k => t.name.Contains(k)))
                .OrderBy(t => t.name.StartsWith(SlotPrefix) ? 0 : 1)
                .ThenBy(t => (t.position - npcTransform.position).sqrMagnitude)
                .FirstOrDefault();

            Vector3 forward = front != null ? Vector3.ProjectOnPlane(front.position - npcTransform.position, Vector3.up) : Vector3.zero;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(npcTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.back;
            forward.Normalize();

            Transform point = npcTransform.Find(CloseUpPointName);
            if (point == null)
            {
                var go = new GameObject(CloseUpPointName);
                Undo.RegisterCreatedObjectUndo(go, spec.Title);
                Undo.SetTransformParent(go.transform, npcTransform, spec.Title);
                point = go.transform;
            }
            else
            {
                Undo.RecordObject(point, spec.Title);
            }

            Vector3 basePosition = npcTransform.position;
            Vector3 cameraPosition = basePosition + forward * CloseUpDistance + Vector3.up * (height * CloseUpCameraHeightRatio);
            Vector3 lookAt = basePosition + Vector3.up * (height * CloseUpLookHeightRatio);
            point.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(lookAt - cameraPosition, Vector3.up));

            var so = new SerializedObject(npc);
            so.FindProperty("closeUpPoint").objectReferenceValue = point;
            so.ApplyModifiedProperties();

            string basis = front != null ? $"'{front.name}' 쪽을 정면으로" : "오브젝트 앞쪽을 정면으로";
            log.AppendLine($"- 클로즈업 카메라: {basis}, {CloseUpDistance}m 앞 · 높이 {height * CloseUpCameraHeightRatio:0.00}m에서 얼굴 쪽을 봄 ({PathOf(point)}, 위치는 직접 옮겨도 됨)");
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
