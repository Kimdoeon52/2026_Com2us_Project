// RE:AL STEEL - 고물상 콜라이더 채우기 (한 번 실행용 · 다시 눌러도 이미 있는 것은 건너뜀)
//
// 메뉴: Tools → RE_AL STEEL → Stage → 고물상 콜라이더 채우기 (Map_Test)
//  · 부딪혀야 하는데 콜라이더가 없는 소품에 '단순 콜라이더'를 붙인다 (현업 방식: 소품은 메시 콜라이더 대신 상자 · 캡슐)
//    - 상자: 소품 자체 축 기준으로 보이는 모양을 감싸고, 바닥 둘레만 8 % 줄인다 (캐릭터가 바짝 붙을 수 있게)
//    - 가늘고 긴 것(기둥류): 캡슐
//    - 높이가 Step Offset 이하인 납작한 것(눕힌 타이어 등)은 그대로 밟고 넘어간다
//  · 대상: JUNKYARD_꾸미기 의 바로 아래 소품 + 집 컨테이너(Cont). 이미 콜라이더가 있거나 조립품 '메시 콜라이더'가 켜진 것은 건너뜀
//  · 제외: 줄전구 · 전선(머리 위), 임시_/SLOT_ 그림판(울타리 · 광고판에 붙은 판), 꺼 둔 '안 보임' 묶음
//  · Ctrl+Z 로 한 번에 되돌린다
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RealSteel.Build;

public static class RSJunkyardColliders
{
    const string RootName = "JUNKYARD_꾸미기";
    static readonly string[] Skip = { "줄전구_", "전선", "임시_", "SLOT_" };
    const float Shrink = 0.92f;

    [MenuItem("Tools/RE_AL STEEL/Stage/고물상 콜라이더 채우기 (Map_Test)", false, 11)]
    static void Run()
    {
        var root = GameObject.Find(RootName);
        if (root == null) { EditorUtility.DisplayDialog("콜라이더 채우기", "'" + RootName + "' 가 없습니다. Map_Test 를 열고 실행하세요.", "확인"); return; }

        Undo.SetCurrentGroupName("고물상 콜라이더 채우기");
        int group = Undo.GetCurrentGroup();
        var added = new List<string>();
        var skipped = 0;

        var targets = new List<Transform>();
        foreach (Transform t in root.transform) if (t.gameObject.activeInHierarchy) targets.Add(t);
        foreach (var go in root.scene.GetRootGameObjects())
            if (go.name.StartsWith("Cont") && go.GetComponentInChildren<MeshFilter>() != null) targets.Add(go.transform);

        foreach (var t in targets)
        {
            if (SkipName(t.name)) continue;
            if (HasCollision(t)) { skipped++; continue; }
            if (!LocalBounds(t, out Bounds b)) continue;
            AddCollider(t, b);
            added.Add(t.name);
        }

        Undo.CollapseUndoOperations(group);
        if (added.Count > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

        var count = new Dictionary<string, int>();
        foreach (var n in added) count[n] = count.TryGetValue(n, out int c) ? c + 1 : 1;
        var lines = new List<string>();
        foreach (var kv in count) lines.Add(kv.Key + (kv.Value > 1 ? " × " + kv.Value : ""));
        string msg = "붙임 " + added.Count + "개 · 이미 있어서 건너뜀 " + skipped + "개\n\n" + string.Join(", ", lines);
        Debug.Log("[고물상 콜라이더 채우기] " + msg);
        EditorUtility.DisplayDialog("콜라이더 채우기", msg + "\n\nCtrl+Z 한 번으로 되돌립니다.", "확인");
    }

    static bool SkipName(string n)
    {
        foreach (var s in Skip) if (n.StartsWith(s) || n == s) return true;
        return false;
    }

    /// <summary>이미 부딪히는가: 켜진 일반 콜라이더가 있거나, 조립품 메시 콜라이더 옵션이 켜져 있음</summary>
    static bool HasCollision(Transform t)
    {
        foreach (var c in t.GetComponentsInChildren<Collider>(false))
            if (c.enabled && !c.isTrigger && (c.gameObject.hideFlags & HideFlags.DontSave) == 0) return true;
        foreach (var a in t.GetComponentsInChildren<RSAssembly>(false))
            if (a.colliders) return true;
        return false;
    }

    /// <summary>보이는 메시들을 이 오브젝트 자기 축 기준으로 감싼 상자 (빛 · 그림자 판 같은 보조 렌더러는 빼고)</summary>
    static bool LocalBounds(Transform t, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(false))
        {
            var m = mf.sharedMesh;
            var r = mf.GetComponent<MeshRenderer>();
            if (m == null || r == null || !r.enabled) continue;
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off && r.sharedMaterial != null
                && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Unlit")) continue;   // 등 불빛 · 빛 판
            var mb = m.bounds;
            for (int i = 0; i < 8; i++)
            {
                var p = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var lp = t.InverseTransformPoint(mf.transform.TransformPoint(p));
                if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
            }
        }
        return any;
    }

    static void AddCollider(Transform t, Bounds b)
    {
        Vector3 s = t.lossyScale;
        Vector3 worldSize = new Vector3(b.size.x * Mathf.Abs(s.x), b.size.y * Mathf.Abs(s.y), b.size.z * Mathf.Abs(s.z));
        bool thin = worldSize.x < 0.35f && worldSize.z < 0.35f && worldSize.y > 1.2f;

        if (thin)
        {
            var cap = Undo.AddComponent<CapsuleCollider>(t.gameObject);
            cap.direction = 1;
            cap.center = b.center;
            cap.height = b.size.y;
            cap.radius = Mathf.Max(b.size.x, b.size.z) * 0.5f;
            return;
        }

        var box = Undo.AddComponent<BoxCollider>(t.gameObject);
        box.center = b.center;
        box.size = new Vector3(Mathf.Max(0.02f, b.size.x * Shrink), Mathf.Max(0.02f, b.size.y), Mathf.Max(0.02f, b.size.z * Shrink));
    }
}
