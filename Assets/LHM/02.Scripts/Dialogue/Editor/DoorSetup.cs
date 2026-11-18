using System.IO;
using System.Linq;
using System.Text;
using RealSteel.Build;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    // 집 문 ↔ 집 안 출입문 씬 이동 지점 만들기 (현재 씬 쪽만 만듦 → 반대쪽 씬을 열고 한 번 더 누르면 짝이 맞음)
    //  - 바깥(고물상 맵): 선택한 문, 없으면 SYS 도구가 만든 '집_바깥 문 · 창 (자동)/문' → "[E] 들어가기" → 집 씬
    //  - 집 안: 선택한 문, 없으면 'SLOT_Door…' / '…출입문' 소품 자리 → "[E] 나가기" → 고물상 맵
    //  - 이동할 씬은 같은 폴더의 짝 씬(Home_Workshop ↔ Map_Test)을 먼저 찾음
    //  - 서로 Door Id를 가리켜서, 넘어가면 상대 문 앞에 나타남
    // SYS 도구를 다시 누르면 '집_바깥 문 · 창 (자동)' 안이 새로 만들어지므로, 바깥 입구는 그 밖(컨테이너 바로 아래)에 따로 둠
    internal static class DoorSetup
    {
        private const string Title = "집 문 ↔ 집 씬 이동";
        private const string FacadeHolderName = "집_바깥 문 · 창 (자동)";
        private const string DoorName = "문";
        private const string InsideDoorPrefix = "SLOT_Door";
        private const string InsideDoorKeyword = "출입문";
        internal const string DefaultScenePath = "Assets/SYS/Scenes/Home_Workshop.unity";
        private const string FontPath = "Assets/LHM/04.Fonts/NanumBarunGothicBold SDF.asset";

        private const string OutsideId = "home_outside";
        private const string InsideId = "home_inside";
        private const string GuildExitId = "guild_inside";
        private const string HomeSceneName = "Home_Workshop";
        private const string FieldSceneName = "Map_Test";
        private const string GuildSceneName = "League_Lobby";
        private const string GuildDoorPrefix = "SLOT_Entrance";   // SLOT_Gate는 경기장으로 가는 셔터문이라 출구 아님
        private const float GuildExitRadius = 1.2f;              // 플레이어 시작 위치(원점)가 입구에서 1.25m라 들어오자마자 안내가 뜨지 않게

        // 길드(League_Lobby) 출구: 고물상 맵으로 나감. 맵에는 짝 문이 없어 원래 시작 위치에 도착 (길드 입장은 LJS 지도로 함)
        private sealed class Side
        {
            public bool Inside;
            public bool Guild;
            public string EntranceName => Guild ? "길드 출구 ([E] 나가기)" : Inside ? "집 출구 ([E] 나가기)" : "집 입구 ([E] 들어가기)";
            public string Label => Inside || Guild ? "나가기" : "들어가기";
            public string DoorId => Guild ? GuildExitId : Inside ? InsideId : OutsideId;
            public string ArriveAt => Guild ? "" : Inside ? OutsideId : InsideId;
            public string TargetName => Inside || Guild ? FieldSceneName : HomeSceneName;
            public string Kind => Guild ? "길드 출구 → 고물상 맵" : Inside ? "집 안 출입문 → 고물상 맵" : "고물상 맵의 집 문 → 집 안";
        }

        private static bool IsGuildScene(Scene scene) => scene.name.StartsWith(GuildSceneName);

        internal static bool Exists()
        {
            Scene scene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(d => d.gameObject.scene == scene);
        }

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform door = FindDoor(scene);
            if (door == null)
            {
                SetupKit.Dialog(Title,
                    "문을 찾지 못했습니다.\n하이어라키에서 문 오브젝트를 선택한 뒤 다시 누르세요.\n" +
                    $"(선택이 없으면 '{FacadeHolderName}' 아래의 '{DoorName}', 또는 이름이 '{InsideDoorPrefix}…'이거나 '{InsideDoorKeyword}'가 든 오브젝트를 찾습니다)", "확인");
                return;
            }

            bool guild = IsGuildScene(scene);
            var side = new Side { Guild = guild, Inside = !guild && IsInsideDoor(door) };
            // 바깥 문은 SYS 컨테이너 밖에, 집 안 문 · 길드 출구는 SYS 소품 자리 밖(씬 맨 위)에 둠 → SYS 도구를 다시 눌러도 안 지워짐
            Transform parent = side.Inside || side.Guild ? null
                : door.parent != null && door.parent.name == FacadeHolderName ? door.parent.parent : door.parent;
            DoorInteractable existing = FindExisting(scene, parent, side);
            string targetScene = FindTargetScene(scene, side);
            string current = string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path;

            if (!SetupKit.Dialog(Title,
                    $"'{PathOf(door)}' 앞에 씬 이동 지점을 {(existing != null ? "다시 맞춥니다" : "만듭니다")}.\n" +
                    (SetupKit.IsLhmScene(scene) ? "" : $"※ LHM 밖의 씬입니다: {current}\n  다른 팀 씬이면 저장 전에 담당자와 확인하세요.\n") +
                    $"- 종류: {side.Kind}\n" +
                    $"- 오브젝트: {(parent != null ? parent.name + "/" : "")}{side.EntranceName}\n" +
                    $"- 가까이 가면 \"[E] {side.Label}\", 멀어지면 사라짐. E → 씬 이동\n" +
                    $"- 이동할 씬: {targetScene ?? "(못 찾음 — 인스펙터에서 지정)"}\n" +
                    (targetScene != null && SceneUtility.GetBuildIndexByScenePath(targetScene) < 0
                        ? "  ※ Build Settings에 없어서 에디터 플레이에서만 넘어갑니다 (빌드 전에 추가 필요)\n" : "") +
                    (side.Guild
                        ? "- 도착: 고물상 맵의 원래 시작 위치 (맵에는 길드 문이 없음)\n"
                        : $"- 도착: 넘어가면 그 씬의 '{side.ArriveAt}' 문 앞에 나타남 (반대쪽 씬에서도 이 버튼을 눌러 두세요)\n") +
                    "\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)",
                    existing != null ? "다시 맞추기" : "만들기", "취소"))
            {
                return;
            }

            var log = new StringBuilder($"[{Title}] {current}\n");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(Title);
            int group = Undo.GetCurrentGroup();

            DoorInteractable entrance = existing;
            if (entrance == null)
            {
                var go = new GameObject(side.EntranceName);
                Undo.RegisterCreatedObjectUndo(go, Title);
                if (parent != null) Undo.SetTransformParent(go.transform, parent, Title);
                else SceneManager.MoveGameObjectToScene(go, scene);
                entrance = Undo.AddComponent<DoorInteractable>(go);
                log.AppendLine($"- {PathOf(go.transform)} 생성");
            }
            else
            {
                log.AppendLine($"- 기존 {PathOf(entrance.transform)} 위치 · 설정 다시 맞춤");
            }

            if (side.Guild) PlaceAtGuildEntrance(entrance, door, log);
            else if (side.Inside) PlaceInside(entrance, door, log);
            else PlaceOutside(entrance, door, parent, log);

            var so = new SerializedObject(entrance);
            SerializedProperty scenePath = so.FindProperty("scenePath");
            if (targetScene != null && (string.IsNullOrEmpty(scenePath.stringValue) || !File.Exists(scenePath.stringValue) || IsSibling(scene, targetScene)))
            {
                scenePath.stringValue = targetScene;
                so.FindProperty("sceneName").stringValue = Path.GetFileNameWithoutExtension(targetScene);
            }
            so.FindProperty("label").stringValue = side.Label;
            so.FindProperty("doorId").stringValue = side.DoorId;
            so.FindProperty("arriveAtDoorId").stringValue = side.ArriveAt;
            if (so.FindProperty("font").objectReferenceValue == null)
            {
                so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            }
            so.ApplyModifiedProperties();
            log.AppendLine($"- 이동할 씬: {(string.IsNullOrEmpty(entrance.ScenePath) ? "(비어 있음 — 인스펙터에서 지정)" : entrance.ScenePath)}");
            log.AppendLine(side.Guild
                ? $"- 문 이름표 '{side.DoorId}' / 넘어가면 고물상 맵의 원래 시작 위치에 도착"
                : $"- 문 이름표 '{side.DoorId}' / 넘어가면 '{side.ArriveAt}' 문 앞에 도착");

            Selection.activeGameObject = entrance.gameObject;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. 위치가 어긋나면 오브젝트를 옮기세요 (초록 원 = 안내가 뜨는 범위, 하늘색 점 = 도착 자리). Ctrl+S로 저장");
            Debug.Log(log.ToString());
        }

        private static bool IsInsideDoor(Transform door)
        {
            if (door.parent != null && door.parent.name == FacadeHolderName) return false;
            return door.name.StartsWith(InsideDoorPrefix) || door.name.Contains(InsideDoorKeyword) || door.GetComponent<RSPropSlot>() != null;
        }

        // 같은 폴더의 짝 씬 → 없으면 프로젝트에서 같은 이름 씬 → (바깥 쪽만) SYS 기본 집 씬
        private static string FindTargetScene(Scene scene, Side side)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                string sibling = $"{Path.GetDirectoryName(scene.path).Replace('\\', '/')}/{side.TargetName}.unity";
                if (File.Exists(sibling)) return sibling;
            }

            string found = AssetDatabase.FindAssets($"{side.TargetName} t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == side.TargetName);
            if (found != null) return found;
            return !side.Inside && File.Exists(DefaultScenePath) ? DefaultScenePath : null;
        }

        private static bool IsSibling(Scene scene, string path)
        {
            return !string.IsNullOrEmpty(scene.path) && Path.GetDirectoryName(scene.path) == Path.GetDirectoryName(path);
        }

        private static Transform FindDoor(Scene scene)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.scene == scene && selected.GetComponent<DoorInteractable>() == null)
            {
                // 임시 모형을 골랐으면 그 위의 소품 자리
                Transform slot = selected.transform.parent;
                if (selected.name.StartsWith(RSPropSlot.TempPrefix) && slot != null && slot.GetComponent<RSPropSlot>() != null) return slot;
                return selected.transform;
            }

            Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            if (IsGuildScene(scene))
            {
                Transform gate = all.FirstOrDefault(t => t.name.StartsWith(GuildDoorPrefix));
                if (gate != null) return gate;
            }
            return all.FirstOrDefault(t => t.name == DoorName && t.parent != null && t.parent.name == FacadeHolderName)
                   ?? all.FirstOrDefault(t => t.name.StartsWith(InsideDoorPrefix))
                   ?? all.FirstOrDefault(t => t.name.Contains(InsideDoorKeyword) && !t.name.StartsWith(RSPropSlot.TempPrefix));
        }

        private static DoorInteractable FindExisting(Scene scene, Transform parent, Side side)
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out DoorInteractable selected)) return selected;
            return Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(d => d.gameObject.scene == scene && d.transform.parent == parent && d.name == side.EntranceName);
        }

        // 문 아래(바닥) 바로 바깥쪽에 둠. 안내는 문 위
        private static void PlaceOutside(DoorInteractable entrance, Transform door, Transform container, StringBuilder log)
        {
            Renderer doorRenderer = door.GetComponent<Renderer>();
            Bounds doorBounds = doorRenderer != null ? doorRenderer.bounds : new Bounds(door.position, Vector3.one);
            Vector3 bottom = new Vector3(doorBounds.center.x, doorBounds.min.y, doorBounds.center.z);

            // 바깥쪽 = 문 앞뒤 방향 중 컨테이너 가운데에서 먼 쪽
            Vector3 outward = door.forward;
            Renderer body = container != null ? container.GetComponent<Renderer>() : null;
            Vector3 center = body != null ? body.bounds.center : container != null ? container.position : door.position - door.forward;
            if (Vector3.Dot(outward, door.position - center) < 0f) outward = -outward;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.zero;

            SetPose(entrance, bottom + outward * 0.6f, outward, doorBounds.size.y + 0.3f);
            log.AppendLine($"- 위치: 문 아래 바깥쪽 0.6m, 안내 높이 {doorBounds.size.y + 0.3f:0.0}m");
        }

        // 집 안 출입문: 소품 자리 기준점 = 바닥 가운데, 앞(-Z) = 방 안쪽 (RSPropSlot 규칙)
        private static void PlaceInside(DoorInteractable entrance, Transform door, StringBuilder log)
        {
            float height = 2f;
            Vector3 bottom = door.position;
            if (door.TryGetComponent(out RSPropSlot slot))
            {
                height = Mathf.Max(0.5f, slot.size.y * Mathf.Abs(door.lossyScale.y));
            }
            else if (door.GetComponentsInChildren<Renderer>().Length > 0)
            {
                Bounds bounds = door.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                height = bounds.size.y;
            }

            Vector3 inward = -door.forward;
            inward.y = 0f;
            inward = inward.sqrMagnitude > 0.0001f ? inward.normalized : Vector3.back;

            SetPose(entrance, bottom + inward * 0.6f, inward, height + 0.3f);
            log.AppendLine($"- 위치: 출입문 아래 방 안쪽 0.6m, 안내 높이 {height + 0.3f:0.0}m (방 반대쪽에 생기면 오브젝트를 180° 돌려 옮기세요)");
        }

        // 길드 입구 소품 자리 위에 그대로 둠 (앞 = 길드 안쪽). 반경은 시작 위치와 겹치지 않게 줄임
        private static void PlaceAtGuildEntrance(DoorInteractable entrance, Transform door, StringBuilder log)
        {
            float height = door.TryGetComponent(out RSPropSlot slot) ? Mathf.Max(0.5f, slot.size.y * Mathf.Abs(door.lossyScale.y)) : 2.5f;
            Vector3 forward = door.forward;
            forward.y = 0f;
            SetPose(entrance, door.position, forward, height + 0.3f);

            var so = new SerializedObject(entrance);
            so.FindProperty("interactRadius").floatValue = GuildExitRadius;
            so.ApplyModifiedProperties();
            log.AppendLine($"- 위치: 길드 입구 자리, 반경 {GuildExitRadius}m, 안내 높이 {height + 0.3f:0.0}m");
        }

        private static void SetPose(DoorInteractable entrance, Vector3 position, Vector3 forward, float promptHeight)
        {
            Undo.RecordObject(entrance.transform, Title);
            entrance.transform.position = position;
            entrance.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0f ? forward : Vector3.forward, Vector3.up);

            var so = new SerializedObject(entrance);
            so.FindProperty("promptOffset").vector3Value = new Vector3(0f, promptHeight, 0f);
            so.ApplyModifiedProperties();
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }

    // 이동할 씬을 씬 파일로 고르게 해 줌 + Build Settings 상태 표시
    [CustomEditor(typeof(DoorInteractable))]
    internal class DoorInteractableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty path = serializedObject.FindProperty("scenePath");
            SerializedProperty sceneName = serializedObject.FindProperty("sceneName");

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path.stringValue);
            EditorGUI.BeginChangeCheck();
            var picked = (SceneAsset)EditorGUILayout.ObjectField("이동할 씬", asset, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                path.stringValue = picked != null ? AssetDatabase.GetAssetPath(picked) : "";
                sceneName.stringValue = picked != null ? picked.name : "";
            }

            if (!string.IsNullOrEmpty(path.stringValue) && SceneUtility.GetBuildIndexByScenePath(path.stringValue) < 0)
            {
                EditorGUILayout.HelpBox("이 씬은 Build Settings에 없어서 에디터 플레이에서만 넘어갑니다. 빌드하려면 File > Build Profiles(Build Settings)에 추가하세요.", MessageType.Warning);
            }
            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "scenePath", "sceneName");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
