using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 고물상 씬에서 루트 프리팹 3개를 만들고 메인 씬 두 곳에 배치한 뒤 서로 다시 연결한다
public static class MainScenePrefabBuilder
{
    private const string Title = "메인 씬 프리팹 구성";
    private const string SourceScenePath = "Assets/KDU/01.Scene/JunkShop_Map.unity";
    private const string PrefabFolder = "Assets/KDU/04.Prefab/";
    private const string Inventory = "KDU_Inventory";
    private const string JunkShop = "KDU_JunkShop";
    private const string Crafting = "Crafting_UI";
    private const string WindowRootName = "Canvas_Window";
    private const string ManagerPrefabPath = "Assets/KDU/04.Prefab/Canvas/CraftingManager.prefab";
    private const string GameObjectType = "GameObject";

    private static readonly string[] Bundles = { Inventory, JunkShop, Crafting };

    // 메인 씬별로 넣을 묶음
    private static readonly SceneTarget[] Targets =
    {
        new SceneTarget("Assets/Scenes/MainScene/Main_Junk_Shop.unity", Inventory, JunkShop, Crafting),
        new SceneTarget("Assets/Scenes/MainScene/Main_Home_Workshop.unity", Inventory, Crafting),
    };

    private readonly struct SceneTarget
    {
        public readonly string Path;
        public readonly string[] Bundles;

        public SceneTarget(string path, params string[] bundles)
        {
            Path = path;
            Bundles = bundles;
        }
    }

    // 묶음 밖으로 나가는 참조 하나. 씬이 바뀌어도 다시 찾을 수 있게 경로로 들고 있는다
    private struct Link
    {
        public string OwnerBundle;
        public string OwnerPath;
        public string OwnerType;
        public int OwnerIndex;
        public string Property;
        public string TargetBundle;   // null이면 씬 오브젝트
        public string TargetPath;
        public string TargetType;
        public int TargetIndex;
        public string Label;
    }

    [MenuItem("KDU/고물상/메인 씬 프리팹 구성")]
    private static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!ConfirmOverwrite())
            return;

        var log = new StringBuilder();
        Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        UnpackExisting(source, log);

        if (!TryCollect(source, log, out Dictionary<string, List<GameObject>> members, out GameObject scrapyard, out Transform pulse))
        {
            EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog(Title, "필요한 오브젝트를 찾지 못해 아무것도 바꾸지 않았다.\n\n" + log, "확인");
            return;
        }

        var roots = new Dictionary<string, Transform>();
        foreach (string bundle in Bundles)
            roots[bundle] = Group(bundle, members[bundle]);

        // 파동 이미터는 추적 대상 위치를 따라가므로 탐지기 밑에 둔다
        if (pulse != null && pulse.parent != scrapyard.transform)
        {
            Undo.SetTransformParent(pulse, scrapyard.transform, false, Title);
            pulse.localPosition = Vector3.zero;
            pulse.localRotation = Quaternion.identity;
        }

        foreach (Transform root in roots.Values)
            RemoveMissingScripts(root.gameObject, log);

        List<Link> links = CollectLinks(source, roots, log);

        foreach (string bundle in Bundles)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(roots[bundle].gameObject, PathOf(bundle), InteractionMode.UserAction, out bool success);
            if (!success)
            {
                EditorUtility.DisplayDialog(Title, $"{bundle} 저장 실패. 콘솔을 확인할 것.", "확인");
                Debug.LogError($"[{Title}] {bundle} 저장 실패\n{log}");
                return;
            }
        }

        EditorSceneManager.MarkSceneDirty(source);
        EditorSceneManager.SaveScene(source);

        foreach (SceneTarget target in Targets)
            PlaceInScene(target, links, log);

        Debug.Log($"[{Title}] 완료\n{log}");
        EditorUtility.DisplayDialog(Title, "프리팹 3개 저장, 메인 씬 2곳 배치 완료.\n연결 결과는 콘솔 로그에 있다.", "확인");
    }

    private static string PathOf(string bundle)
    {
        return PrefabFolder + bundle + ".prefab";
    }

    private static bool ConfirmOverwrite()
    {
        var existing = new List<string>();
        foreach (string bundle in Bundles)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(bundle)) != null)
                existing.Add(PathOf(bundle));
        }

        foreach (SceneTarget target in Targets)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(target.Path) == null)
            {
                EditorUtility.DisplayDialog(Title, $"메인 씬이 없다: {target.Path}", "확인");
                return false;
            }
        }

        string message = "원본 씬과 메인 씬 2곳을 저장한다. 메인 씬의 기존 배치는 다시 만든다.";
        if (existing.Count > 0)
            message += "\n\n덮어쓸 프리팹:\n" + string.Join("\n", existing);

        return EditorUtility.DisplayDialog(Title, message, "진행", "취소");
    }

    // ---------- 원본 씬 ----------

    // 이전에 만든 묶음이 있으면 풀어서 씬 루트로 되돌린다
    private static void UnpackExisting(Scene scene, StringBuilder log)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (!IsBundleInstance(go))
                continue;

            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);

            var children = new List<Transform>();
            foreach (Transform child in go.transform)
                children.Add(child);

            foreach (Transform child in children)
                Undo.SetTransformParent(child, null, true, Title);

            log.AppendLine($"- 기존 묶음 풀기: {go.name}");
            Undo.DestroyObjectImmediate(go);
        }
    }

    private static bool IsBundleInstance(GameObject go)
    {
        if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            return false;

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        foreach (string bundle in Bundles)
        {
            if (path == PathOf(bundle))
                return true;
        }

        return false;
    }

    // 하나라도 없으면 false
    private static bool TryCollect(Scene scene, StringBuilder log, out Dictionary<string, List<GameObject>> members, out GameObject scrapyard, out Transform pulse)
    {
        bool ok = true;
        members = new Dictionary<string, List<GameObject>>
        {
            [Inventory] = new List<GameObject>
            {
                Require<ExclusiveWindowManager>(scene, log, ref ok),
                Require<InventoryView>(scene, log, ref ok),
                RequirePrefab(scene, "Assets/KDU/04.Prefab/Canvas/Canvas_Inventory.prefab", log, ref ok),
                RequirePrefab(scene, "Assets/KDU/04.Prefab/Canvas/Canvas_Equipment.prefab", log, ref ok),
                Require<SkillWindowView>(scene, log, ref ok),
            },
            [JunkShop] = new List<GameObject>
            {
                Require<ScrapyardFieldHost>(scene, log, ref ok),
                RequirePrefab(scene, "Assets/KDU/04.Prefab/Canvas/Canvas_Log.prefab", log, ref ok),
                RequirePrefab(scene, "Assets/KDU/04.Prefab/Canvas/Canvas_FieldHUD.prefab", log, ref ok),
            },
            [Crafting] = new List<GameObject>
            {
                RequireRoot(scene, WindowRootName, log, ref ok),
                RequirePrefab(scene, ManagerPrefabPath, log, ref ok),
            },
        };

        scrapyard = members[JunkShop][0];
        ScrapPulseEmitter emitter = FindInScene<ScrapPulseEmitter>(scene);
        pulse = emitter != null ? emitter.transform : null;

        foreach (List<GameObject> list in members.Values)
        {
            foreach (GameObject go in list)
            {
                if (go != null && PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsOutermostPrefabInstanceRoot(go))
                {
                    log.AppendLine($"- 다른 프리팹 내부라 옮길 수 없음: {PathOf(go.transform)}");
                    ok = false;
                }
            }
        }

        return ok;
    }

    private static GameObject Require<T>(Scene scene, StringBuilder log, ref bool ok) where T : Component
    {
        T found = FindInScene<T>(scene);
        if (found != null)
            return found.gameObject;

        log.AppendLine($"- 없음: {typeof(T).Name}");
        ok = false;
        return null;
    }

    private static GameObject RequirePrefab(Scene scene, string assetPath, StringBuilder log, ref bool ok)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == assetPath)
                return go;
        }

        log.AppendLine($"- 없음: {assetPath}");
        ok = false;
        return null;
    }

    private static GameObject RequireRoot(Scene scene, string name, StringBuilder log, ref bool ok)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == name)
                return go;
        }

        log.AppendLine($"- 없음: {name}");
        ok = false;
        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            T found = go.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform Group(string bundle, List<GameObject> list)
    {
        var root = new GameObject(bundle);
        Undo.RegisterCreatedObjectUndo(root, Title);

        foreach (GameObject go in list)
            Undo.SetTransformParent(go.transform, root.transform, true, Title);

        return root.transform;
    }

    // 프리팹 인스턴스 내부는 원본 쪽 문제라 지우지 않고 알리기만 한다
    private static void RemoveMissingScripts(GameObject root, StringBuilder log)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (count == 0)
                continue;

            if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
            {
                log.AppendLine($"- Missing Script {count}개 남음 (프리팹 내부): {PathOf(t)}");
                continue;
            }

            Undo.RegisterCompleteObjectUndo(t.gameObject, Title);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            log.AppendLine($"- Missing Script {count}개 제거: {PathOf(t)}");
        }
    }

    // ---------- 참조 기록 ----------

    private static List<Link> CollectLinks(Scene scene, Dictionary<string, Transform> roots, StringBuilder log)
    {
        var links = new List<Link>();

        foreach (KeyValuePair<string, Transform> pair in roots)
        {
            foreach (Component owner in pair.Value.GetComponentsInChildren<Component>(true))
            {
                ForEachReference(owner, (property, target, targetObject) =>
                {
                    if (target.IsChildOf(pair.Value))
                        return;

                    string targetBundle = BundleOf(target, roots);
                    links.Add(new Link
                    {
                        OwnerBundle = pair.Key,
                        OwnerPath = IndexPath(pair.Value, owner.transform),
                        OwnerType = owner.GetType().FullName,
                        OwnerIndex = TypeIndex(owner),
                        Property = property,
                        TargetBundle = targetBundle,
                        TargetPath = targetBundle == null ? PathOf(target) : IndexPath(roots[targetBundle], target),
                        TargetType = targetObject is Component tc ? tc.GetType().FullName : GameObjectType,
                        TargetIndex = targetObject is Component tc2 ? TypeIndex(tc2) : 0,
                        Label = $"{PathOf(owner.transform)} [{owner.GetType().Name}] {property} → {PathOf(target)}",
                    });
                });
            }
        }

        log.AppendLine();
        log.AppendLine("[묶음을 가리키는 다른 오브젝트: 담당자에게 공유]");
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (BundleOf(go.transform, roots) != null)
                continue;

            foreach (Component owner in go.GetComponentsInChildren<Component>(true))
            {
                ForEachReference(owner, (property, target, _) =>
                {
                    string bundle = BundleOf(target, roots);
                    if (bundle != null)
                        log.AppendLine($"- {PathOf(owner.transform)} [{owner.GetType().Name}] {property} → {bundle}: {PathOf(target)}");
                });
            }
        }

        return links;
    }

    private static string BundleOf(Transform t, Dictionary<string, Transform> roots)
    {
        foreach (KeyValuePair<string, Transform> pair in roots)
        {
            if (t.IsChildOf(pair.Value))
                return pair.Key;
        }

        return null;
    }

    // 계층 참조는 제외하고 씬 오브젝트를 가리키는 직렬화 필드만 넘긴다
    private static void ForEachReference(Component owner, Action<string, Transform, Object> visit)
    {
        if (owner == null || owner is Transform)
            return;

        var so = new SerializedObject(owner);
        SerializedProperty p = so.GetIterator();
        while (p.Next(true))
        {
            if (p.propertyType != SerializedPropertyType.ObjectReference || p.name == "m_GameObject")
                continue;

            Object value = p.objectReferenceValue;
            if (value == null || EditorUtility.IsPersistent(value))
                continue;

            Transform target = value is Component c ? c.transform : value is GameObject g ? g.transform : null;
            if (target != null)
                visit(p.propertyPath, target, value);
        }
    }

    // ---------- 메인 씬 ----------

    private static void PlaceInScene(SceneTarget target, List<Link> links, StringBuilder log)
    {
        Scene scene = EditorSceneManager.OpenScene(target.Path, OpenSceneMode.Single);
        log.AppendLine();
        log.AppendLine($"[{System.IO.Path.GetFileNameWithoutExtension(target.Path)}]");

        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (!IsBundleInstance(go))
                continue;

            log.AppendLine($"- 기존 배치 제거: {go.name}");
            Undo.DestroyObjectImmediate(go);
        }

        var roots = new Dictionary<string, Transform>();
        foreach (string bundle in target.Bundles)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(bundle));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            Undo.RegisterCreatedObjectUndo(instance, Title);
            roots[bundle] = instance.transform;
        }

        int done = 0;
        int skipped = 0;
        foreach (Link link in links)
        {
            if (!roots.ContainsKey(link.OwnerBundle))
                continue;

            if (link.TargetBundle != null && !roots.ContainsKey(link.TargetBundle))
            {
                skipped++;
                continue;
            }

            Component owner = FindComponent(FromIndexPath(roots[link.OwnerBundle], link.OwnerPath), link.OwnerType, link.OwnerIndex);
            Object value = ResolveTarget(scene, roots, link);
            if (owner == null || value == null)
            {
                log.AppendLine($"- 실패 ({(owner == null ? "주인" : "대상")} 못 찾음): {link.Label}");
                continue;
            }

            var so = new SerializedObject(owner);
            SerializedProperty p = so.FindProperty(link.Property);
            if (p == null)
            {
                log.AppendLine($"- 실패 (필드 없음): {link.Label}");
                continue;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            done++;
            log.AppendLine($"- 연결: {link.Label}");
        }

        log.AppendLine($"{done}개 연결, 이 씬에 없는 묶음이라 건너뜀 {skipped}개");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Object ResolveTarget(Scene scene, Dictionary<string, Transform> roots, Link link)
    {
        Transform t = link.TargetBundle != null ? FromIndexPath(roots[link.TargetBundle], link.TargetPath) : FindByNamePath(scene, link.TargetPath);
        if (t == null)
            return null;

        return link.TargetType == GameObjectType ? t.gameObject : (Object)FindComponent(t, link.TargetType, link.TargetIndex);
    }

    // ---------- 경로·컴포넌트 탐색 ----------

    private static Component FindComponent(Transform t, string type, int index)
    {
        if (t == null)
            return null;

        int seen = 0;
        foreach (Component c in t.GetComponents<Component>())
        {
            if (c == null || c.GetType().FullName != type)
                continue;
            if (seen == index)
                return c;

            seen++;
        }

        return null;
    }

    private static int TypeIndex(Component target)
    {
        int index = 0;
        foreach (Component c in target.GetComponents<Component>())
        {
            if (c == target)
                return index;
            if (c != null && c.GetType() == target.GetType())
                index++;
        }

        return 0;
    }

    // 같은 이름 형제가 많아서 루트 기준 형제 순서로 경로를 만든다
    private static string IndexPath(Transform root, Transform t)
    {
        var parts = new List<string>();
        for (Transform p = t; p != root && p != null; p = p.parent)
            parts.Insert(0, p.GetSiblingIndex().ToString());

        return string.Join("/", parts);
    }

    private static Transform FromIndexPath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path))
            return root;

        Transform t = root;
        foreach (string part in path.Split('/'))
        {
            int i = int.Parse(part);
            if (i >= t.childCount)
                return null;

            t = t.GetChild(i);
        }

        return t;
    }

    // 씬 구성이 달라 전체 경로로 못 찾으면 이름이 유일한 오브젝트로 찾는다
    private static Transform FindByNamePath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name != parts[0])
                continue;

            Transform found = parts.Length == 1 ? go.transform : go.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
            if (found != null)
                return found;
        }

        string leaf = parts[parts.Length - 1];
        Transform unique = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != leaf)
                    continue;
                if (unique != null)
                    return null;

                unique = t;
            }
        }

        return unique;
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
            path = p.name + "/" + path;

        return path;
    }
}
