// RE:AL STEEL - 네모난 가구 → 상자 콜라이더
//
// 메뉴: Tools → RE_AL STEEL → Stage → 네모난 가구 → 상자 콜라이더 (열린 씬)
//       Tools → RE_AL STEEL → Stage → 네모난 가구 → 상자 콜라이더 (SYS 씬 전부)
//
// 조립품(RSAssembly) 중 '메시 콜라이더'가 켜진 것을 보고, 네모난 가구면
//   메시 콜라이더 끄기 → 같은 오브젝트에 Box Collider (부품 전체를 감싸는 크기, 바닥 둘레 4 % 줄임)
//   상자 높이는 바닥에서 1.8 m 까지 올린다 — 낮은 탁자 · 침대에 계단 오르기로 올라서지 않게 (높이 0.15 m 미만 깔개는 그대로)
// 현업에서 가구 · 소품 충돌을 단순 상자로 잡는 것과 같다. 씬을 열 때 메시 콜라이더 계산이 줄어 로딩도 빨라진다.
//
// '네모나다' 판정 (하나라도 걸리면 그대로 둔다 — 콘솔에 이유가 나온다)
//  · 계단 · 경사 부품이 있음 (밟고 올라가는 것) / 지붕 부품이 있음 (건물)
//  · 이름에 벽 · 바닥 · 방 · 건물 · 지붕 · 천장 · 문 · 창 · 계단 · 경사 · 다리 · 무대 · 링 이 들어감
//  · 높이 2.4 m 초과 또는 가로 · 깊이 중 하나가 4 m 초과 (가구보다 큰 구조물)
//  · 위에서 본 바닥 면적을 부품이 75 % 미만으로 덮음 (ㄱ자 카운터 · 사이로 지나가는 모양)
// 다 만든 상자는 인스펙터 Box Collider 의 Edit Collider 로 손보면 된다.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RealSteel.Build;
using RealSteel.Common.EditorTools;

public static class RSBoxifyColliders
{
    const float MaxHeight = 2.4f, MaxSide = 4f, MinFill = 0.75f, Shrink = 0.96f, Cell = 0.1f;
    // 막는 높이: 상자 윗면이 낮으면 캐릭터가 계단 오르기(Step Offset)로 올라선다 → 가구 상자는 바닥에서 이 높이까지 올린다 (점프가 없으니 높아도 티 안 남)
    const float BlockHeight = 1.8f, WalkOverHeight = 0.15f;
    static readonly string[] Structure = { "벽", "바닥", "방", "건물", "지붕", "천장", "문", "창", "계단", "경사", "다리", "무대", "링" };

    [MenuItem("Tools/RE_AL STEEL/Stage/네모난 가구 → 상자 콜라이더 (열린 씬)", false, 12)]
    static void MenuOpen()
    {
        var s = SceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("네모난 가구 → 상자 콜라이더");
        int g = Undo.GetCurrentGroup();
        var rep = Apply(s, true, out int n);
        Undo.CollapseUndoOperations(g);
        if (n > 0) EditorSceneManager.MarkSceneDirty(s);
        Debug.Log(rep);
        EditorUtility.DisplayDialog("상자 콜라이더", s.name + ": " + n + "개 바꿈.\n그대로 둔 것과 이유는 콘솔에.\nCtrl+Z 한 번으로 되돌립니다.", "확인");
    }

    [MenuItem("Tools/RE_AL STEEL/Stage/네모난 가구 → 상자 콜라이더 (SYS 씬 전부)", false, 13)]
    static void MenuAll()
    {
        string dir = RSPaths.Root + "/Scenes";
        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { dir })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        if (paths.Count == 0) { EditorUtility.DisplayDialog("상자 콜라이더", dir + " 에 씬이 없습니다.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("상자 콜라이더 (씬 전부)",
                "SYS/Scenes 의 씬 " + paths.Count + "개를 차례로 열어 바꾸고 저장합니다. (Ctrl+Z 불가 — 되돌리려면 깃에서)\n\n" + string.Join("\n", paths),
                "실행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string back = SceneManager.GetActiveScene().path;
        int total = 0;
        foreach (var p in paths)
        {
            var sc = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
            var rep = Apply(sc, false, out int n);
            Debug.Log(rep);
            if (n > 0) EditorSceneManager.SaveScene(sc);
            total += n;
        }
        if (!string.IsNullOrEmpty(back) && File.Exists(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("상자 콜라이더 (씬 전부)", "씬 " + paths.Count + "개 · " + total + "개 바꿈.\n씬마다 바꾼 것 · 그대로 둔 것과 이유는 콘솔에.", "확인");
    }

    static string Apply(Scene s, bool undo, out int changed)
    {
        changed = 0;
        var done = new List<string>();
        var kept = new List<string>();
        foreach (var root in s.GetRootGameObjects())
        {
            foreach (var a in root.GetComponentsInChildren<RSAssembly>(true))
            {
                if (!a.colliders) continue;
                string why = Reject(a, out Bounds b);
                if (why != null) { kept.Add(a.name + " (" + why + ")"); continue; }

                if (undo) Undo.RecordObject(a, "상자 콜라이더");
                a.colliders = false;
                a.Rebuild();
                EditorUtility.SetDirty(a);
                if (a.GetComponent<BoxCollider>() != null) { done.Add(a.name + " (손으로 단 상자 그대로)"); changed++; continue; }   // 이미 맞춰 둔 상자는 크기를 안 건드린다
                var box = undo ? Undo.AddComponent<BoxCollider>(a.gameObject) : a.gameObject.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = new Vector3(Mathf.Max(0.02f, b.size.x * Shrink), Mathf.Max(0.02f, b.size.y), Mathf.Max(0.02f, b.size.z * Shrink));
                done.Add(a.name);
                changed++;
            }
        }
        // 가구 상자(메시 콜라이더 끈 조립품에 붙은 Box Collider)를 막는 높이까지 — 이미 바꾼 씬에도 다시 누르면 적용된다
        int raised = 0;
        foreach (var root in s.GetRootGameObjects())
            foreach (var a in root.GetComponentsInChildren<RSAssembly>(true))
            {
                if (a.colliders) continue;
                var box = a.GetComponent<BoxCollider>();
                if (box == null || box.isTrigger) continue;
                float sy = Mathf.Max(0.0001f, Mathf.Abs(a.transform.lossyScale.y));
                float worldH = box.size.y * sy;
                if (worldH < WalkOverHeight || worldH >= BlockHeight - 0.001f) continue;   // 깔개처럼 납작한 것은 밟고 지나가게 둔다
                if (undo) Undo.RecordObject(box, "상자 콜라이더");
                float bottom = box.center.y - box.size.y * 0.5f;
                float h = BlockHeight / sy;
                box.size = new Vector3(box.size.x, h, box.size.z);
                box.center = new Vector3(box.center.x, bottom + h * 0.5f, box.center.z);
                raised++;
            }
        changed += raised;

        return "[상자 콜라이더] " + s.name + " — 막는 높이(" + BlockHeight + " m)로 올림 " + raised + "개 · 바꿈 " + done.Count + "개: " + string.Join(", ", done) +
               "\n그대로 둠 " + kept.Count + "개: " + string.Join(", ", kept);
    }

    /// <summary>네모난 가구가 아니면 이유, 맞으면 null (b = 조립품 자기 축 기준 부품 전체 상자)</summary>
    static string Reject(RSAssembly a, out Bounds b)
    {
        b = default;
        foreach (var w in Structure) if (a.name.Contains(w)) return "이름: " + w;

        var parts = a.GetComponentsInChildren<RSPart>(true);
        if (parts.Length == 0) return "부품 없음";
        var rects = new List<Rect>();
        bool any = false;
        foreach (var p in parts)
        {
            if (p.Owner != a) continue;   // 안에 든 다른 조립품의 부품
            if (p.shape == RSPartShape.계단 || p.shape == RSPartShape.경사) return "계단 · 경사 부품";
            if (p.shape == RSPartShape.지붕) return "지붕 부품";
            // 부품 기준점 = 바닥 가운데
            Vector3 h = p.size * 0.5f;
            float xmin = float.MaxValue, xmax = float.MinValue, zmin = float.MaxValue, zmax = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? 0f : p.size.y, (i & 4) == 0 ? -h.z : h.z);
                var lp = a.transform.InverseTransformPoint(p.transform.TransformPoint(c));
                if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
                xmin = Mathf.Min(xmin, lp.x); xmax = Mathf.Max(xmax, lp.x);
                zmin = Mathf.Min(zmin, lp.z); zmax = Mathf.Max(zmax, lp.z);
            }
            rects.Add(Rect.MinMaxRect(xmin, zmin, xmax, zmax));
        }
        if (!any) return "부품 없음";

        Vector3 s = a.transform.lossyScale;
        float wx = b.size.x * Mathf.Abs(s.x), wy = b.size.y * Mathf.Abs(s.y), wz = b.size.z * Mathf.Abs(s.z);
        if (wy > MaxHeight) return "높이 " + wy.ToString("0.0") + " m";
        if (wx > MaxSide || wz > MaxSide) return "크기 " + wx.ToString("0.0") + " × " + wz.ToString("0.0") + " m";

        // 위에서 본 덮인 비율 (0.1 m 칸, 조립품 축 기준)
        float cx = Cell / Mathf.Max(0.01f, Mathf.Abs(s.x)), cz = Cell / Mathf.Max(0.01f, Mathf.Abs(s.z));
        int nx = Mathf.Clamp(Mathf.CeilToInt(b.size.x / cx), 1, 200), nz = Mathf.Clamp(Mathf.CeilToInt(b.size.z / cz), 1, 200);
        int hit = 0;
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var pt = new Vector2(b.min.x + (i + 0.5f) * b.size.x / nx, b.min.z + (j + 0.5f) * b.size.z / nz);
                foreach (var r in rects) if (r.Contains(pt)) { hit++; break; }
            }
        float fill = hit / (float)(nx * nz);
        if (fill < MinFill) return "위에서 본 면적 " + Mathf.RoundToInt(fill * 100f) + " %";
        return null;
    }
}
