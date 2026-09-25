// RE:AL STEEL - 간단 캐릭터 인스펙터 + "테스트 캐릭터 놓기" 메뉴
using UnityEditor;
using UnityEngine;
using RealSteel.Common.EditorTools;
using RealSteel.Terrain;

[CustomEditor(typeof(RSSimpleCharacter))]
public class RSSimpleCharacterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RSHelpGUI.DrawSummary(target);
        DrawDefaultInspector();
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("처음 각도로"))
                    ((RSSimpleCharacter)target).ResetCameraAngle();
                if (GUILayout.Button("지금 카메라 각도를 기준으로"))
                    ((RSSimpleCharacter)target).RecaptureCamera();
            }
            EditorGUILayout.HelpBox("플레이 중에 바꾼 값은 플레이가 끝나면 되돌아갑니다. 마음에 드는 Pitch · Yaw · Distance 는 컴포넌트 메뉴(⋮) → Copy Component 후 플레이를 끝내고 Paste Component Values 로 옮기세요.", MessageType.None);
            Repaint();   // 게임 중 바뀌는 각도가 인스펙터에 바로 보이게
        }
    }

    [MenuItem("Tools/RE_AL STEEL/Stage/테스트 캐릭터 놓기 (씬 뷰 가운데)", false, 23)]
    static void CreateFromTools() { Create(); }

    [MenuItem("GameObject/RE_AL STEEL/테스트 캐릭터", false, 11)]
    static void CreateFromGameObject() { Create(); }

    static void Create()
    {
        // 씬 뷰 가운데 지면 위
        Vector3 pos = Vector3.zero;
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            var ray = new Ray(sv.camera.transform.position, sv.camera.transform.forward);
            bool hit = false;
            foreach (var t in RSTerrain.All)
                if (t != null && t.Raycast(ray, out Vector3 p)) { pos = p; hit = true; break; }
            if (!hit && Physics.Raycast(ray, out RaycastHit rh, 500f)) { pos = rh.point; hit = true; }
            if (!hit) pos = sv.pivot;
        }

        var go = new GameObject("TestCharacter");
        Undo.RegisterCreatedObjectUndo(go, "테스트 캐릭터");
        go.transform.position = pos;
        go.AddComponent<RSSimpleCharacter>();   // CharacterController 는 자동으로 붙는다
        go.AddComponent<RSSeeThrough>();        // 벽 뒤로 가도 보이게
        Selection.activeGameObject = go;
        Debug.Log("[캐릭터] 테스트 캐릭터를 놓았습니다. 플레이하면 WASD 로 움직이고 Main Camera 가 따라옵니다. " +
                  "모델을 자식으로 넣으면 임시 캡슐 대신 그 모델을 씁니다.");
    }
}
