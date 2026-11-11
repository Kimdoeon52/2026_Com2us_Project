// RE:AL STEEL - Map_Test 의 플레이어 · 카메라 · 화면 효과를 다른 씬으로 가져오기
//
// 메뉴: Tools → RE_AL STEEL → Stage → Map_Test 의 플레이어 · 카메라 가져오기 (열린 씬에)
//  · Map_Test 의 TestCharacter(스프라이트 · 값 그대로), 그 카메라(CAM_Stage: 렌즈 · 각도 · 캐릭터와의 거리 · 자동 초점),
//    전역 Volume(VOLUME_Stage · Depth — 같은 프로필 에셋을 가리킨다)을 복제해 열린 씬의 것과 바꾼다
//  · 캐릭터는 지금 씬의 캐릭터가 서 있던 자리에 놓이고, 카메라는 Map_Test 에서와 같은 상대 위치에 놓인다
//  · Map_Test 씬 파일은 바뀌지 않는다 (잠깐 겹쳐 열었다가 저장 없이 닫는다)
//  · 조명 · 시간대 · 안개 · 지형은 가져오지 않는다 (야외용이라 실내 씬과 안 맞음)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using RealSteel.Common.EditorTools;
using RealSteel.Lighting;

public static class RSMapTestImport
{
    const string SourceScene = "Map_Test";

    [MenuItem("Tools/RE_AL STEEL/Stage/Map_Test 의 플레이어 · 카메라 가져오기 (열린 씬에)", false, 6)]
    static void Menu()
    {
        var target = SceneManager.GetActiveScene();
        if (target.name == SourceScene) { EditorUtility.DisplayDialog("가져오기", "지금 열린 씬이 Map_Test 입니다. 가져올 씬(접수장 · 집)을 열고 누르세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("Map_Test 의 플레이어 · 카메라 가져오기",
            "열린 씬 '" + target.name + "' 의 테스트 캐릭터 · 메인 카메라 · 전역 Volume 을 Map_Test 의 것으로 바꿉니다.\n(Ctrl+Z 로 되돌릴 수 있습니다)", "바꾸기", "취소")) return;
        if (Apply(target)) EditorSceneManager.MarkSceneDirty(target);
    }

    /// <summary>target 씬의 캐릭터 · 카메라 · 전역 Volume 을 Map_Test 것으로 바꾼다. Map_Test 를 못 찾으면 false (아무것도 안 바꿈)</summary>
    public static bool Apply(Scene target)
    {
        string path = RSPaths.Root + "/Scenes/" + SourceScene + ".unity";
        if (!File.Exists(path)) { Debug.LogWarning("[가져오기] " + path + " 가 없습니다."); return false; }

        var src = SceneManager.GetSceneByPath(path);
        bool opened = false;
        if (!src.IsValid() || !src.isLoaded) { src = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
        SceneManager.SetActiveScene(target);

        GameObject clone = null;
        var items = new List<Transform>();
        try
        {
            // ── Map_Test 에서 가져올 것 ──
            var srcChar = FindIn<RSSimpleCharacter>(src);
            if (srcChar == null) { Debug.LogWarning("[가져오기] Map_Test 에 테스트 캐릭터(RSSimpleCharacter)가 없습니다."); return false; }
            Camera srcCam = srcChar.cam;
            if (srcCam == null)
                foreach (var c in AllIn<Camera>(src))
                    if (c.CompareTag("MainCamera") && (srcCam == null || c.depth > srcCam.depth)) srcCam = c;
            if (srcCam == null) { Debug.LogWarning("[가져오기] Map_Test 에서 카메라를 못 찾았습니다."); return false; }

            items.Add(srcChar.transform);
            if (!srcCam.transform.IsChildOf(srcChar.transform)) items.Add(srcCam.transform);
            foreach (var v in AllIn<Volume>(src))
            {
                if (!v.isGlobal || v.gameObject.hideFlags != HideFlags.None || v.name.StartsWith("__")) continue;
                bool inside = false;
                foreach (var t in items) if (v.transform == t || v.transform.IsChildOf(t)) { inside = true; break; }
                if (!inside) items.Add(v.transform);
            }
            Vector3 charPos = srcChar.transform.position;

            // 원본들을 임시 부모 아래에 잠깐 모아 한 번에 복제 → 서로를 가리키는 참조(캐릭터 → 카메라, 자동 초점 → Volume)가 복제본끼리 이어진다
            var parents = new List<Transform>();
            var orders = new List<int>();
            var temp = new GameObject("__RS_가져오기 임시");
            SceneManager.MoveGameObjectToScene(temp, src);
            foreach (var t in items) { parents.Add(t.parent); orders.Add(t.GetSiblingIndex()); t.SetParent(temp.transform, true); }
            clone = Object.Instantiate(temp);
            for (int i = 0; i < items.Count; i++)
            {
                items[i].SetParent(parents[i], true);
                items[i].SetSiblingIndex(orders[i]);
            }
            Object.DestroyImmediate(temp);
            SceneManager.MoveGameObjectToScene(clone, target);

            // ── 지금 씬에서 바꿀 것 ──
            Vector3 spawn = charPos;
            Transform stageParent = null;
            var remove = new List<GameObject>();
            foreach (var ch in AllIn<RSSimpleCharacter>(target))
            {
                if (ch.transform.IsChildOf(clone.transform)) continue;
                if (remove.Count == 0) spawn = ch.transform.position;
                remove.Add(ch.gameObject);
            }
            foreach (var c in AllIn<Camera>(target))
            {
                if (c.transform.IsChildOf(clone.transform) || !c.CompareTag("MainCamera")) continue;
                if (stageParent == null) stageParent = c.transform.parent;
                if (!remove.Contains(c.gameObject)) remove.Add(c.gameObject);
            }
            foreach (var v in AllIn<Volume>(target))
            {
                if (v.transform.IsChildOf(clone.transform) || !v.isGlobal || v.gameObject.hideFlags != HideFlags.None) continue;
                if (stageParent == null) stageParent = v.transform.parent;
                if (!remove.Contains(v.gameObject)) remove.Add(v.gameObject);
            }
            foreach (var go in remove) if (go != null) Undo.DestroyObjectImmediate(go);

            // ── 복제본 놓기 ──
            Vector3 delta = spawn - charPos;
            var kids = new List<Transform>();
            foreach (Transform k in clone.transform) kids.Add(k);
            Camera newCam = null;
            foreach (var k in kids)
            {
                bool isChar = k.GetComponent<RSSimpleCharacter>() != null;
                var cam = k.GetComponentInChildren<Camera>(true);
                if (isChar || cam != null) k.position += delta;      // 캐릭터와 카메라는 같은 상대 위치 그대로
                if (cam != null && newCam == null) newCam = cam;
                k.SetParent(isChar ? null : stageParent, true);
                Undo.RegisterCreatedObjectUndo(k.gameObject, "Map_Test 에서 가져오기");
            }
            Object.DestroyImmediate(clone);
            clone = null;

            if (newCam != null)
            {
                newCam.clearFlags = CameraClearFlags.SolidColor;     // 실내 씬은 스카이박스가 없다
                if (Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
                    newCam.gameObject.AddComponent<AudioListener>();
            }
            Debug.Log("[가져오기] Map_Test 의 캐릭터 · 카메라 · 전역 Volume " + (kids.Count - (newCam != null && kids.Count > 1 ? 2 : 1)) + "개를 '" + target.name + "' 에 넣었습니다.");
            return true;
        }
        finally
        {
            if (clone != null) Object.DestroyImmediate(clone);
            if (opened) EditorSceneManager.CloseScene(src, true);    // 저장 없이 닫는다 — Map_Test 파일은 그대로
        }
    }

    static T FindIn<T>(Scene s) where T : Component
    {
        foreach (var root in s.GetRootGameObjects())
        {
            var c = root.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    static List<T> AllIn<T>(Scene s) where T : Component
    {
        var list = new List<T>();
        foreach (var root in s.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
        return list;
    }
}
