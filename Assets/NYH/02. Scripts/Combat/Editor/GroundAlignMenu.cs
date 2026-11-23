using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 메뉴 NYH/씬/바닥을 (0, 0)에 맞추기 (2026-11-23 사용자: 보스2 바닥찍기·손바닥·몸통·플레이어를 바닥에 맞추고, 땅을 0 0에).
/// 바닥 오브젝트(선택한 것, 없으면 씬의 "Ground")의 윗면 가운데가 (0, 0)이 되게 씬 루트 오브젝트를 전부 같이 옮긴다 —
/// 카메라도 같이 움직이므로 화면에 보이는 모습은 그대로다. z(경기장 평면)는 건드리지 않는다.
/// 같이 맞추는 것:
///   · 쿄다이 groundY(월드 값 — 바닥찍기·손바닥 밀기·예고·그림자의 기준) → 바닥 윗면, 고정 경기장 x(카메라 없을 때 값)도 같이 이동
///   · RobotMover가 붙은 로봇(플레이어 등)의 발밑 → 바닥 윗면 (시작 y가 곧 바닥이 되므로)
/// 몸통·손 대기 자리는 보스 루트 기준이라 루트와 같이 옮겨져 보이는 자리 그대로.
/// UI(RectTransform 루트)는 옮기지 않는다. 되돌리기(Ctrl+Z) 한 번으로 전부 원래대로.
/// </summary>
public static class GroundAlignMenu
{
    private const string GroundName = "Ground";

    // 바닥 윗면과 이만큼 이내면 "바닥에 서 있던 것"으로 보고 딱 붙인다 — 더 멀면 일부러 띄운 것으로 보고 그대로 둔다
    private const float SnapTolerance = 0.3f;

    [MenuItem("NYH/씬/바닥을 (0, 0)에 맞추기 (현재 씬)")]
    private static void AlignToOrigin()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("바닥 맞추기", "플레이 중에는 쓸 수 없습니다.", "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        Renderer ground = FindGround(scene);
        if (ground == null)
        {
            EditorUtility.DisplayDialog("바닥 맞추기",
                $"바닥을 찾지 못했습니다. 바닥 오브젝트를 선택하고 다시 실행하거나, 이름을 '{GroundName}'으로 해 주세요(Renderer 필요).", "확인");
            return;
        }

        Bounds b = ground.bounds;
        Vector3 delta = new Vector3(-b.center.x, -b.max.y, 0f);

        var kyodais = Object.FindObjectsByType<KyodaiBossController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var movers = Object.FindObjectsByType<RobotMover>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (!EditorUtility.DisplayDialog("바닥 맞추기",
                $"바닥 '{ground.name}' 윗면 가운데 ({b.center.x:0.###}, {b.max.y:0.###}) → (0, 0)\n" +
                $"씬 루트 오브젝트를 전부 x {delta.x:+0.###;-0.###;0}, y {delta.y:+0.###;-0.###;0} 옮깁니다(카메라 포함, UI 제외).\n\n" +
                $"쿄다이 {kyodais.Length}개의 groundY와 로봇 {movers.Length}개의 발밑을 바닥 윗면(0)에 붙입니다" +
                $"(바닥에서 {SnapTolerance} 넘게 떨어진 것은 그대로).",
                "맞추기", "취소"))
            return;

        Undo.SetCurrentGroupName("바닥을 (0, 0)에 맞추기");
        int group = Undo.GetCurrentGroup();
        var log = new List<string>();

        // 1. 씬 루트 전부 같이 이동 — 보이는 모습 유지
        int moved = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.transform is RectTransform) continue; // UI
            Undo.RecordObject(root.transform, "바닥 맞추기");
            root.transform.position += delta;
            moved++;
        }
        log.Add($"루트 {moved}개 이동");

        // 2. 쿄다이: 월드 값으로 저장된 칸을 같이 옮기고 groundY는 바닥 윗면에 붙인다
        foreach (var boss in kyodais)
        {
            var so = new SerializedObject(boss);
            var groundY = so.FindProperty("groundY");
            float before = groundY.floatValue;
            float shifted = before + delta.y;
            groundY.floatValue = Mathf.Abs(shifted) <= SnapTolerance ? 0f : shifted;
            so.FindProperty("arenaMinX").floatValue += delta.x;
            so.FindProperty("arenaMaxX").floatValue += delta.x;
            so.ApplyModifiedProperties();
            log.Add($"쿄다이 '{boss.name}' groundY {before:0.###} → {groundY.floatValue:0.###}" +
                    (Mathf.Approximately(groundY.floatValue, 0f) ? $" (바닥에서 {shifted:+0.###;-0.###;0} 어긋나 있던 것 붙임)" : " (바닥에서 멀어 그대로 옮김)"));
        }

        // 3. 로봇 발밑을 바닥에 — RobotMover는 시작 y를 바닥으로 쓴다
        foreach (var mover in movers)
        {
            Transform t = mover.transform;
            float y = t.position.y;
            if (Mathf.Abs(y) > SnapTolerance || Mathf.Approximately(y, 0f)) continue;
            Undo.RecordObject(t, "바닥 맞추기");
            t.position = new Vector3(t.position.x, 0f, t.position.z);
            log.Add($"로봇 '{mover.name}' y {y:0.###} → 0");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[바닥 맞추기] " + string.Join(" / ", log) +
                  ". 쿄다이가 프리팹이면 위치·groundY는 이 씬의 덮어쓴 값이다 — 프리팹 기본값으로 쓰려면 'NYH/쿄다이/프리팹으로 저장·갱신'");
    }

    // 씬 루트의 "Ground", 없으면 선택한 오브젝트(Renderer가 있을 때) — 확인 창에 이름이 나오니 엉뚱한 게 잡히면 취소
    private static Renderer FindGround(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == GroundName && root.TryGetComponent(out Renderer r))
                return r;

        var selected = Selection.activeGameObject;
        if (selected != null && selected.scene == scene && selected.TryGetComponent(out Renderer picked))
            return picked;
        return null;
    }
}
