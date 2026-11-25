using RealSteel.NanoBoss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 마에스트로 디버그 패널(MaestroDebugPanel) 짝 — 에디터 메뉴 (2026-11-25).
///   · 디버그 패널 붙이기: 씬에 패널이 없으면 마에스트로 보스에 붙인다 (빌더도 붙인다)
///   · 복사한 외형·군집 값 붙여넣기: 플레이 중 패널에서 '복사'한 MaestroLook·RSNanoSwarm 값을 플레이를 끈 뒤 씬 보스에 적용(되돌리기 가능).
///     참조(군집·데이터·셰이더·재질 등)는 그대로 두고 숫자 값만
/// </summary>
public static class MaestroDebugMenu
{
    [MenuItem("NYH/보스3/디버그: 패널 붙이기 (F8)", false, 230)]
    private static void AddPanel()
    {
        var boss = Object.FindAnyObjectByType<MaestroBossController>();
        if (boss == null) { Debug.LogWarning("[Maestro] 씬에 마에스트로가 없음"); return; }
        if (boss.GetComponent<MaestroDebugPanel>() != null) { Debug.Log("[Maestro] 디버그 패널이 이미 있음 — 플레이 중 F8"); return; }
        Undo.AddComponent<MaestroDebugPanel>(boss.gameObject);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Debug.Log("[Maestro] 디버그 패널 붙임 — 플레이 중 F8로 열고 닫기");
    }

    [MenuItem("NYH/보스3/디버그: 복사한 외형·군집 값 붙여넣기", false, 231)]
    private static void Paste()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Maestro] 플레이를 끈 뒤에 붙여넣으세요 (플레이 중 값은 끄면 돌아감)"); return; }
        string clip = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(clip) || !clip.StartsWith(MaestroDebugValues.Tag)) { Debug.LogWarning("[Maestro] 클립보드에 디버그 패널에서 복사한 값이 없음"); return; }
        var pack = JsonUtility.FromJson<MaestroDebugValues>(clip.Substring(MaestroDebugValues.Tag.Length));
        var boss = Object.FindAnyObjectByType<MaestroBossController>();
        if (boss == null) { Debug.LogWarning("[Maestro] 씬에 마에스트로가 없음"); return; }

        var look = boss.GetComponentInChildren<MaestroLook>(true);
        if (look != null && !string.IsNullOrEmpty(pack.look))
        {
            Undo.RecordObject(look, "마에스트로 외형 값 붙여넣기");
            var swarmRef = look.swarm; var data = look.data; var reserve = look.reserveShape;
            JsonUtility.FromJsonOverwrite(pack.look, look);
            look.swarm = swarmRef; look.data = data; look.reserveShape = reserve; // 참조는 그대로
            look.standalone = false;
            EditorUtility.SetDirty(look);
        }
        var swarm = boss.GetComponent<RSNanoSwarm>();
        if (swarm != null && !string.IsNullOrEmpty(pack.swarm))
        {
            Undo.RecordObject(swarm, "마에스트로 군집 값 붙여넣기");
            var shader = swarm.shader; var mat = swarm.material; int bots = swarm.botCount;
            JsonUtility.FromJsonOverwrite(pack.swarm, swarm);
            swarm.shader = shader; swarm.material = mat; swarm.botCount = bots; // 참조·봇 수(빌더가 정함)는 그대로
            EditorUtility.SetDirty(swarm);
        }
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Debug.Log("[Maestro] 디버그 패널 값 붙여넣음 — 씬 저장하면 남는다");
    }
}
