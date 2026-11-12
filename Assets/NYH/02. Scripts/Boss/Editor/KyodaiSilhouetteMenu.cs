using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 쿄다이 실루엣 켜기·끄기 (2026-11-12 사용자: 로봇을 어둡게 실루엣만, 멀수록 더 어둡게).
/// 쿄다이는 2D 스프라이트라 URP Render Objects(3D 불투명 물체용)가 안 먹는다 → 스프라이트 셰이더(NYH/Sprite Depth Shade)로
/// 그림 모양은 그대로 두고 색만 어둡게 칠한다. 어둡기·거리는 머티리얼(06.Material/Kyodai_실루엣)에서 조절 — 모든 그림이 같이 바뀐다.
///
/// 바닥 예고(GroundTelegraph)와 손 그림자는 빼고 바꾼다 — 피할 신호가 어둠에 묻히면 안 된다.
/// 촉수는 원본(복제용)을 바꾸면 새로 나오는 촉수가 따라간다. 레이저·이펙트는 실행 중에 따로 만들어져서 영향 없음.
/// </summary>
public static class KyodaiSilhouetteMenu
{
    private const string MaterialPath = "Assets/NYH/06.Material/Kyodai_실루엣.mat";
    private const string ShaderName = "NYH/Sprite Depth Shade";
    private const string DefaultSpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    private const string RobotLayerName = "Robot2";

    [MenuItem("NYH/쿄다이/실루엣 켜기 (현재 씬)", false, 30)]
    private static void On()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError($"[Kyodai] 셰이더 '{ShaderName}'를 못 찾음 — 06.Material/Shader 확인"); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        Apply(mat, "켬");
    }

    [MenuItem("NYH/쿄다이/실루엣 끄기 (현재 씬)", false, 31)]
    private static void Off()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(DefaultSpriteMaterial);
        if (mat == null) { Debug.LogError("[Kyodai] URP 기본 스프라이트 머티리얼을 못 찾음"); return; }
        Apply(mat, "끔");
    }

    private static void Apply(Material mat, string what)
    {
        var boss = Object.FindAnyObjectByType<KyodaiBossController>(FindObjectsInactive.Include);
        if (boss == null) { Debug.LogWarning("[Kyodai] 씬에 2D 쿄다이가 없음"); return; }

        int changed = 0;
        foreach (var r in boss.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r.GetComponentInParent<GroundTelegraph>(true) != null || r.gameObject.name == "그림자") continue;
            if (r.sharedMaterial == mat) continue;
            Undo.RecordObject(r, $"쿄다이 실루엣 {what}");
            r.sharedMaterial = mat;
            changed++;
        }

        // Render Objects(Robot2 레이어)는 스프라이트에 안 먹고 판정 오브젝트까지 레이어가 바뀌어 있어서 Default로 되돌린다
        int robotLayer = LayerMask.NameToLayer(RobotLayerName);
        int relayered = 0;
        if (robotLayer >= 0)
            foreach (var t in boss.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer == robotLayer)
                {
                    Undo.RecordObject(t.gameObject, $"쿄다이 실루엣 {what}");
                    t.gameObject.layer = 0;
                    relayered++;
                }

        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Selection.activeObject = mat;
        Debug.Log($"[Kyodai] 실루엣 {what} — 그림 {changed}개" + (relayered > 0 ? $", {RobotLayerName} 레이어 {relayered}개 → Default" : "") +
                  ". 어둡기·거리는 선택된 머티리얼에서 조절");
    }
}
