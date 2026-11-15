using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    // 집 문 앞에 "[E] 들어가기" 씬 이동 지점 만들기
    // 대상: 선택한 오브젝트(문). 아무것도 안 골랐으면 SYS 도구가 만든 '집_바깥 문 · 창 (자동)/문'
    // SYS 도구를 다시 누르면 '집_바깥 문 · 창 (자동)' 안이 새로 만들어지므로, 입구는 그 밖(컨테이너 바로 아래)에 따로 둠
    internal static class DoorSetup
    {
        private const string Title = "집 문 → 집 씬 이동";
        private const string EntranceName = "집 입구 ([E] 들어가기)";
        private const string FacadeHolderName = "집_바깥 문 · 창 (자동)";
        private const string DoorName = "문";
        internal const string DefaultScenePath = "Assets/SYS/Scenes/Home_Workshop.unity";
        private const string FontPath = "Assets/LHM/04.Fonts/NanumBarunGothicBold SDF.asset";

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
                    $"(선택이 없으면 '{FacadeHolderName}' 아래의 '{DoorName}'을 찾습니다)", "확인");
                return;
            }

            Transform parent = door.parent != null && door.parent.name == FacadeHolderName ? door.parent.parent : door.parent;
            DoorInteractable existing = FindExisting(scene, parent);
            bool hasScene = File.Exists(DefaultScenePath);
            string current = string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path;

            if (!SetupKit.Dialog(Title,
                    $"'{PathOf(door)}' 앞에 씬 이동 지점을 {(existing != null ? "다시 맞춥니다" : "만듭니다")}.\n" +
                    (SetupKit.IsLhmScene(scene) ? "" : $"※ LHM 밖의 씬입니다: {current}\n  다른 팀 씬이면 저장 전에 담당자와 확인하세요.\n") +
                    $"- 오브젝트: {(parent != null ? parent.name + "/" : "")}{EntranceName}\n" +
                    "- 가까이 가면 \"[E] 들어가기\", 멀어지면 사라짐. E → 씬 이동\n" +
                    $"- 이동할 씬: {(hasScene ? DefaultScenePath : "(없음 — 인스펙터에서 지정)")}\n" +
                    (hasScene && SceneUtility.GetBuildIndexByScenePath(DefaultScenePath) < 0
                        ? "  ※ Build Settings에 없어서 에디터 플레이에서만 넘어갑니다 (빌드 전에 추가 필요)\n" : "") +
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
                var go = new GameObject(EntranceName);
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

            Place(entrance, door, parent, log);

            var so = new SerializedObject(entrance);
            if (hasScene && string.IsNullOrEmpty(so.FindProperty("scenePath").stringValue))
            {
                so.FindProperty("scenePath").stringValue = DefaultScenePath;
                so.FindProperty("sceneName").stringValue = Path.GetFileNameWithoutExtension(DefaultScenePath);
            }
            if (so.FindProperty("font").objectReferenceValue == null)
            {
                so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            }
            so.ApplyModifiedProperties();
            log.AppendLine($"- 이동할 씬: {(string.IsNullOrEmpty(entrance.ScenePath) ? "(비어 있음 — 인스펙터에서 지정)" : entrance.ScenePath)}");

            Selection.activeGameObject = entrance.gameObject;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. 위치가 어긋나면 오브젝트를 옮기세요 (초록 원 = 안내가 뜨는 범위). Ctrl+S로 저장");
            Debug.Log(log.ToString());
        }

        private static Transform FindDoor(Scene scene)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.scene == scene && selected.GetComponent<DoorInteractable>() == null) return selected.transform;

            return scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == DoorName && t.parent != null && t.parent.name == FacadeHolderName);
        }

        private static DoorInteractable FindExisting(Scene scene, Transform parent)
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out DoorInteractable selected)) return selected;
            return Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(d => d.gameObject.scene == scene && d.transform.parent == parent && d.name == EntranceName);
        }

        // 문 아래(바닥) 바로 바깥쪽에 둠. 안내는 문 위
        private static void Place(DoorInteractable entrance, Transform door, Transform container, StringBuilder log)
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

            Undo.RecordObject(entrance.transform, Title);
            entrance.transform.position = bottom + outward * 0.6f;
            entrance.transform.rotation = Quaternion.LookRotation(outward.sqrMagnitude > 0f ? outward : Vector3.forward, Vector3.up);

            var so = new SerializedObject(entrance);
            so.FindProperty("promptOffset").vector3Value = new Vector3(0f, doorBounds.size.y + 0.3f, 0f);
            so.ApplyModifiedProperties();
            log.AppendLine($"- 위치: 문 아래 바깥쪽 0.6m, 안내 높이 {doorBounds.size.y + 0.3f:0.0}m");
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
