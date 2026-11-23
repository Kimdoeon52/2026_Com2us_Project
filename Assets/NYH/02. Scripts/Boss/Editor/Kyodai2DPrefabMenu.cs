using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 메뉴 NYH/쿄다이/프리팹으로 저장·갱신 (2026-11-23).
/// 씬의 2D 쿄다이를 Boss2_Kyodai 프리팹으로 저장하고 씬 오브젝트를 그 프리팹에 연결한다.
///   · 아직 프리팹이 아니면 → 새로 저장하고 연결
///   · 이미 이 프리팹에 연결돼 있으면 → 씬에서 바꾼 것을 프리팹에 반영(Apply All)
///   · 프리팹 파일은 있는데 씬 쿄다이가 연결 안 돼 있으면 → 물어본 뒤 덮어쓰기
/// 빌더(만들기·갱신)는 씬에서만 돌므로, 프리팹을 고칠 땐 씬의 쿄다이에 빌더를 돌린 뒤 이 메뉴로 반영한다.
/// 주의: 루트 위치·groundY·본체 자리는 만들 때 씬의 카메라·바닥 기준 값이다 — 다른 씬에 놓으면 그 씬에서 덮어쓸 것.
/// </summary>
public static class Kyodai2DPrefabMenu
{
    public const string PrefabFolder = "Assets/NYH/05. Prefab/Boss";
    public const string PrefabPath = PrefabFolder + "/Boss2_Kyodai.prefab";

    [MenuItem("NYH/쿄다이/프리팹으로 저장·갱신", false, Kyodai2DBuilder.MenuPriority + 2)]
    private static void SaveOrUpdate()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("쿄다이 프리팹", "플레이 중에는 저장할 수 없습니다. 플레이를 끄고 다시 실행하세요.", "확인");
            return;
        }
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog("쿄다이 프리팹", "프리팹 편집 화면에서는 쓸 수 없습니다. 씬으로 돌아가서 씬의 쿄다이로 실행하세요.", "확인");
            return;
        }

        var boss = Object.FindAnyObjectByType<KyodaiBossController>(FindObjectsInactive.Include);
        if (boss == null)
        {
            EditorUtility.DisplayDialog("쿄다이 프리팹", "지금 씬에 2D 쿄다이가 없습니다. 먼저 'NYH/쿄다이/2D 쿄다이 만들기·갱신'으로 만드세요.", "확인");
            return;
        }

        StopPreviews();
        GameObject root = boss.gameObject;
        RemoveUnsavedChildren(root);

        if (PrefabUtility.IsPartOfPrefabInstance(root))
            ApplyToExisting(root);
        else
            SaveNew(root);
    }

    // 이미 프리팹 인스턴스 → 씬 변경분 반영
    private static void ApplyToExisting(GameObject root)
    {
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
        if (PrefabUtility.GetOutermostPrefabInstanceRoot(root) != root)
        {
            EditorUtility.DisplayDialog("쿄다이 프리팹",
                $"쿄다이가 다른 프리팹 안에 들어 있습니다({path}). 그 프리팹에서 직접 관리하세요.", "확인");
            return;
        }

        int changed = PrefabUtility.GetObjectOverrides(root, false).Count;
        int addedObjects = PrefabUtility.GetAddedGameObjects(root).Count;
        int addedComponents = PrefabUtility.GetAddedComponents(root).Count;
        int removedObjects = PrefabUtility.GetRemovedGameObjects(root).Count;
        int removedComponents = PrefabUtility.GetRemovedComponents(root).Count;
        if (changed + addedObjects + addedComponents + removedObjects + removedComponents == 0)
        {
            Debug.Log($"[Kyodai] 프리팹({path})과 씬 쿄다이가 같습니다 — 반영할 것 없음");
            return;
        }

        if (!EditorUtility.DisplayDialog("쿄다이 프리팹",
                $"{path}에 씬의 변경분을 반영합니다.\n\n" +
                $"바뀐 오브젝트·컴포넌트 {changed} / 추가된 오브젝트 {addedObjects} · 컴포넌트 {addedComponents} / " +
                $"지운 오브젝트 {removedObjects} · 컴포넌트 {removedComponents}\n\n" +
                "루트 위치·회전은 반영되지 않습니다. groundY 같은 씬 전용 값을 바꿨다면 그 값도 프리팹에 들어갑니다 " +
                "(다른 씬에서 덮어쓴 값은 그대로).",
                "반영", "취소"))
            return;

        PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Kyodai] 프리팹 갱신 — {path}", AssetDatabase.LoadAssetAtPath<GameObject>(path));
    }

    // 아직 프리팹이 아님 → 새로 저장(이미 파일이 있으면 물어보고 덮어쓰기)
    private static void SaveNew(GameObject root)
    {
        EnsureFolder(PrefabFolder);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null
            && !EditorUtility.DisplayDialog("쿄다이 프리팹",
                $"{PrefabPath}가 이미 있는데, 씬의 쿄다이는 그 프리팹에 연결돼 있지 않습니다.\n\n" +
                "덮어쓰면 프리팹 내용이 이 씬의 쿄다이로 통째로 바뀝니다. 다른 씬에 놓인 쿄다이 프리팹은 " +
                "그 씬에서 덮어쓴 값이 끊길 수 있습니다.",
                "덮어쓰기", "취소"))
            return;

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction, out bool success);
        if (!success || prefab == null)
        {
            Debug.LogError($"[Kyodai] 프리팹 저장 실패 — {PrefabPath}");
            return;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[Kyodai] 프리팹 저장 — {PrefabPath}. 씬을 저장해야 씬 쿄다이와 프리팹 연결이 남습니다. " +
                  "다른 씬에 놓을 땐 루트 위치·groundY를 그 씬의 바닥·카메라에 맞출 것", prefab);
    }

    // 미리보기 중이면 손·그림자·예고를 원래대로 돌려놓고 저장한다(미리보기 자세가 프리팹에 들어가지 않게)
    private static void StopPreviews()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<Kyodai2DPreviewWindow>())
            window.StopPreview();
    }

    // 레이저 선·충격파처럼 실행 중에 만드는 DontSave 자식은 프리팹에 넣지 않는다 — 필요하면 그 컴포넌트가 다시 만든다
    private static void RemoveUnsavedChildren(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == root.transform) continue;
            if ((t.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
                Object.DestroyImmediate(t.gameObject);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
