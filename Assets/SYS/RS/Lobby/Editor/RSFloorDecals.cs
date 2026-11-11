// RE:AL STEEL - 장식 부품 충돌 끄기 (바닥 장식 · 머리 위 보)
//
// 11-12 추가: 머리 위 장식(보 · 전구줄 · 윗테 · 윗띠 · 가로 배관)도 같이 옮긴다 — 컨테이너 칸 사이 보(바닥 위 2.6 m)에
// 캐릭터가 걸렸다. 이름이 아래 OverheadNames 로 시작하는 부품이 대상
//
// 바닥에 얇게 깐 부품(두께 1 픽셀 = 3 cm 이하: 러그 · 바닥 이음 · 경고줄 · 동선 · 매트 · 배수구)이 조립품 메시 콜라이더에 들어가면
// 캐릭터(CharacterController)가 그 3 cm 턱에 걸려 못 넘어간다. 이 부품들을 같은 자리의 '바닥 장식 (충돌 없음)' 조립품으로 옮긴다.
//  · 모양 · 칠 · 위치는 그대로 (월드 위치 유지). 원래 조립품의 메시 · 콜라이더에서만 빠진다
//  · 메뉴: Tools → RE_AL STEEL → Stage → 바닥 장식 충돌 끄기 (열린 씬) — 몇 번 눌러도 된다 (이미 옮긴 건 건너뜀)
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RealSteel.Build;

public static class RSFloorDecals
{
    const string Suffix = " · 바닥 장식 (충돌 없음)";
    const float MaxThickness = RSAssembly.Pixel * 1.5f;
    static readonly string[] OverheadNames = { "보", "전구줄", "윗테", "윗띠", "배관_굵은", "배관_가는" };

    static bool IsDecor(RSPart p)
    {
        if (p.shape == RSPartShape.상자 && p.size.y <= MaxThickness) return true;   // 바닥 장식
        foreach (var n in OverheadNames) if (p.name.StartsWith(n)) return true;    // 머리 위 장식
        return false;
    }

    [MenuItem("Tools/RE_AL STEEL/Stage/장식 충돌 끄기 (바닥 장식 · 머리 위 보) (열린 씬)", false, 8)]
    static void Menu()
    {
        int n = Split(SceneManager.GetActiveScene(), true);
        if (n > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[장식 충돌] 바닥 장식 · 머리 위 보 " + n + "개의 충돌을 껐습니다." + (n > 0 ? " 씬 저장은 Ctrl+S." : ""));
    }

    /// <summary>씬의 충돌 켜진 조립품에서 얇은 상자 부품을 충돌 없는 장식 조립품으로 옮긴다. 옮긴 수</summary>
    public static int Split(Scene scene, bool undo = false)
    {
        var moved = 0;
        var asms = new List<RSAssembly>();
        foreach (var root in scene.GetRootGameObjects()) asms.AddRange(root.GetComponentsInChildren<RSAssembly>(true));
        var parts = new List<RSPart>();
        foreach (var a in asms)
        {
            if (!a.colliders || a.name.EndsWith(Suffix)) continue;
            parts.Clear();
            foreach (var p in a.GetComponentsInChildren<RSPart>(true))
                if (p.Owner == a && IsDecor(p)) parts.Add(p);
            if (parts.Count == 0) continue;

            var deco = FindOrMakeDeco(a, undo);
            foreach (var p in parts)
            {
                if (undo) Undo.SetTransformParent(p.transform, deco.transform, "바닥 장식");
                else p.transform.SetParent(deco.transform, true);
                moved++;
            }
            a.Rebuild();
            deco.Rebuild();
        }
        return moved;
    }

    static RSAssembly FindOrMakeDeco(RSAssembly src, bool undo)
    {
        var parent = src.transform.parent;
        string want = src.name + Suffix;
        if (parent != null)
        {
            var t = parent.Find(want);
            var found = t != null ? t.GetComponent<RSAssembly>() : null;
            if (found != null) { found.castShadows = true; return found; }
        }
        var go = new GameObject(want);
        if (undo) Undo.RegisterCreatedObjectUndo(go, "바닥 장식");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = src.transform.localPosition;
        go.transform.localRotation = src.transform.localRotation;
        go.transform.localScale = src.transform.localScale;
        go.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
        var d = go.AddComponent<RSAssembly>();
        d.palette = src.palette;
        d.defaultSurface = src.defaultSurface;
        d.snap = src.snap;
        d.colliders = false;
        d.castShadows = true;    // 보 · 배관 그림자는 남긴다
        return d;
    }
}
