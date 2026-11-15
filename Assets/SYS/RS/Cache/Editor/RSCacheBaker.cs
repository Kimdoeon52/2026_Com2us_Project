// RE:AL STEEL - 조립품 · 지형 캐시 굽기 (에디터)
//
// · 씬을 저장할 때(Ctrl+S) 자동: 그 씬의 조립품 메시 · 지형 절차 계산 결과 중 바뀐 것만 굽는다
//     조립품 → Assets/SYS/Data/Cache/조립품/<해시>.asset   (메시)
//     지형   → Assets/SYS/Data/Cache/지형/<해시>.bytes    (절차 계산 결과)
//   처음 한 번은 조립품 수만큼 파일을 만드느라 저장이 몇 초 걸릴 수 있다. 그 다음부터는 고친 것만
// · 메뉴 Tools → RE_AL STEEL → 캐시
//     열린 씬 캐시 굽기 · 씬 전부 캐시 굽기 (SYS/Scenes 의 씬을 차례로 열어 굽고 저장)
//     안 쓰는 캐시 지우기 (프로젝트의 어떤 씬 · 프리팹도 안 쓰는 캐시 파일)
//     열린 씬 캐시 끊기 (문제가 생겼을 때 — 원래처럼 켤 때마다 계산)
// · 캐시는 부품 · 요소 내용의 해시로 맞는지 가린다. 안 맞으면 원래대로 계산하므로 캐시 때문에 모양이 틀어지지는 않는다
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RealSteel.Build;
using RealSteel.Common.EditorTools;
using RealSteel.Terrain;

[InitializeOnLoad]
public static class RSCacheBaker
{
    static string AsmDir { get { return RSPaths.Ensure(RSPaths.Data + "/Cache/조립품"); } }
    static string TerrainDir { get { return RSPaths.Ensure(RSPaths.Data + "/Cache/지형"); } }
    static bool busy;

    static RSCacheBaker()
    {
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    static void OnSceneSaving(Scene scene, string path)
    {
        if (busy || Application.isPlaying || BuildPipeline.isBuildingPlayer) return;
        try { Bake(scene, false); }
        catch (System.Exception e) { Debug.LogWarning("[캐시] 굽다가 실패 — 씬은 그대로 저장됩니다 (다음에 켤 때 원래대로 계산)\n" + e); }
    }

    /// <summary>씬 하나의 캐시를 굽는다. 바꾼 개수</summary>
    static int Bake(Scene scene, bool log)
    {
        busy = true;
        int na = 0, nt = 0;
        try
        {
            string ad = null, td = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<RSTerrain>(true))
                {
                    if (td == null) td = TerrainDir;
                    if (t.BakeProcedural(td)) nt++;
                }
                foreach (var a in root.GetComponentsInChildren<RSAssembly>(true))
                {
                    if (ad == null) ad = AsmDir;
                    if (a.BakeCache(ad)) na++;
                }
            }
        }
        finally { busy = false; }
        if (log || na + nt > 0) Debug.Log("[캐시] " + scene.name + " — 조립품 " + na + "개 · 지형 " + nt + "개 새로 구움");
        return na + nt;
    }

    [MenuItem("Tools/RE_AL STEEL/캐시/열린 씬 캐시 굽기", false, 1)]
    static void MenuOpen()
    {
        var s = SceneManager.GetActiveScene();
        if (Bake(s, true) > 0) EditorSceneManager.SaveScene(s);
    }

    [MenuItem("Tools/RE_AL STEEL/캐시/씬 전부 캐시 굽기 (SYS/Scenes)", false, 2)]
    static void MenuAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string back = SceneManager.GetActiveScene().path;
        string dir = RSPaths.Root + "/Scenes";
        foreach (var g in AssetDatabase.FindAssets("t:Scene", new[] { dir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var sc = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
            if (Bake(sc, true) > 0) EditorSceneManager.SaveScene(sc);   // 저장하면서 한 번 더 확인 (바뀐 것 없음)
        }
        if (!string.IsNullOrEmpty(back) && File.Exists(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
    }

    [MenuItem("Tools/RE_AL STEEL/캐시/안 쓰는 캐시 지우기", false, 20)]
    static void MenuClean()
    {
        string cache = RSPaths.Data + "/Cache";
        if (!AssetDatabase.IsValidFolder(cache)) { Debug.Log("[캐시] 캐시 폴더가 없습니다"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var users = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" })) users.Add(AssetDatabase.GUIDToAssetPath(g));
        var used = new HashSet<string>(AssetDatabase.GetDependencies(users.ToArray(), false));
        var dead = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("", new[] { cache }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (AssetDatabase.IsValidFolder(p) || used.Contains(p)) continue;
            dead.Add(p);
        }
        if (dead.Count == 0) { Debug.Log("[캐시] 지울 캐시가 없습니다"); return; }
        if (!EditorUtility.DisplayDialog("안 쓰는 캐시 지우기", "어떤 씬 · 프리팹도 쓰지 않는 캐시 파일 " + dead.Count + "개를 지웁니다.", "지우기", "취소")) return;
        var failed = new List<string>();
        AssetDatabase.DeleteAssets(dead.ToArray(), failed);
        Debug.Log("[캐시] " + (dead.Count - failed.Count) + "개 지움" + (failed.Count > 0 ? " · 실패 " + failed.Count : ""));
    }

    [MenuItem("Tools/RE_AL STEEL/캐시/열린 씬 캐시 끊기 (켤 때마다 계산으로)", false, 40)]
    static void MenuClear()
    {
        var s = SceneManager.GetActiveScene();
        int n = 0;
        foreach (var root in s.GetRootGameObjects())
        {
            foreach (var a in root.GetComponentsInChildren<RSAssembly>(true)) { Undo.RecordObject(a, "캐시 끊기"); a.ClearCache(); n++; }
            foreach (var t in root.GetComponentsInChildren<RSTerrain>(true)) { Undo.RecordObject(t, "캐시 끊기"); t.proceduralCache = null; EditorUtility.SetDirty(t); n++; }
        }
        EditorSceneManager.MarkSceneDirty(s);
        Debug.Log("[캐시] " + s.name + " — " + n + "개 끊음. 저장하면 다시 구워지니, 계속 끄려면 이 메뉴 뒤에 저장하지 말고 원인을 알려 주세요");
    }
}
