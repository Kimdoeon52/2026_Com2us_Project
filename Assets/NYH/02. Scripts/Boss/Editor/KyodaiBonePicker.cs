using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene 뷰에 리깅 모델의 뼈를 선과 점으로 그리고, 점을 누르면 그 뼈를 선택한다 (2026-10-29).
/// 애니메이션 창에서 키를 찍을 때 뼈 50개를 Hierarchy에서 찾지 않으려고 — 점 클릭 → E(회전 도구)로 돌리면 녹화된다.
///
/// 리깅 모델(SkinnedMeshRenderer가 있는 모델)이나 그 뼈를 선택하고 있을 때만 그린다.
/// 켜기/끄기: 메뉴 NYH/쿄다이/뼈 선택 표시
/// </summary>
[InitializeOnLoad]
public static class KyodaiBonePicker
{
    private const string PrefKey = "NYH.Kyodai.BonePicker";
    private const string MenuPath = "NYH/쿄다이/뼈 선택 표시";

    private static SkinnedMeshRenderer cachedSmr;
    private static Transform cachedFor;
    private static readonly HashSet<Transform> boneSet = new HashSet<Transform>();

    static KyodaiBonePicker()
    {
        SceneView.duringSceneGui += OnScene;
    }

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static SkinnedMeshRenderer FindSmr(Transform selected)
    {
        if (selected == null) return null;
        if (selected == cachedFor && cachedSmr != null) return cachedSmr;

        // 선택한 것이 모델 루트(또는 그 안)든 뼈든 — 위로 올라가며 그 아래의 SkinnedMeshRenderer를 찾는다
        SkinnedMeshRenderer found = null;
        for (Transform t = selected; t != null && found == null; t = t.parent)
        {
            var smr = t.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.bones != null && smr.bones.Length > 0) found = smr;
        }
        cachedFor = selected;
        cachedSmr = found;
        boneSet.Clear();
        if (found != null) foreach (var b in found.bones) if (b != null) boneSet.Add(b);
        return found;
    }

    private static void OnScene(SceneView sv)
    {
        if (!Enabled) return;
        var smr = FindSmr(Selection.activeTransform);
        if (smr == null) return;

        Transform selected = Selection.activeTransform;
        foreach (var bone in smr.bones)
        {
            if (bone == null) continue;
            Vector3 p = bone.position;

            // 부모 뼈와 잇는 선
            if (bone.parent != null && boneSet.Contains(bone.parent))
            {
                Handles.color = new Color(0.4f, 0.8f, 1f, 0.7f);
                Handles.DrawLine(bone.parent.position, p, 2f);
            }

            bool sel = bone == selected;
            float size = HandleUtility.GetHandleSize(p) * (sel ? 0.06f : 0.04f);
            Handles.color = sel ? Color.yellow : new Color(0.3f, 0.9f, 1f, 0.9f);
            if (Handles.Button(p, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
            {
                Selection.activeTransform = bone;
                if (Tools.current != Tool.Rotate) Tools.current = Tool.Rotate; // 뼈는 돌리는 게 대부분
            }
            if (sel) Handles.Label(p + Vector3.up * size * 2f, bone.name, EditorStyles.whiteBoldLabel);
        }
    }
}
