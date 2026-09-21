using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ActionData의 FrameBox를 Scene 뷰에서 마우스로 드래그해 맞추는 에디터 도구 (§4, §9).
/// 숫자를 인스펙터에 직접 타이핑하는 대신, 스프라이트를 보면서 박스 중심/모서리를 끌어 맞춘다.
/// "Editor" 폴더 안에 있어 빌드에는 포함되지 않는다.
///
/// 좌표는 항상 "오른쪽을 본다(facingRight=true)"고 가정한 로컬 좌표로 편집한다.
/// 좌우 반전은 런타임에 BoxResolver 한 곳에서만 하므로, 여기서 왼쪽 기준으로 맞추면 좌표가 꼬인다 (§4).
/// </summary>
[CustomEditor(typeof(ActionData))]
public class ActionDataEditor : Editor
{
    private static readonly Color HitColor = new Color(1f, 0f, 0f, 0.9f);
    private static readonly Color HurtColor = new Color(0.4f, 0.8f, 1f, 0.9f);
    private static readonly Color PushColor = new Color(0f, 1f, 0f, 0.9f);

    private Transform previewTarget;
    private int previewFrame = 1;
    private bool showAllFrames = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("박스 미리보기 / 편집 (Scene 뷰)", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        previewTarget = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("기준 위치(선택)", "씬의 로봇 Transform을 넣으면 그 현재 위치를 피벗으로 박스를 그린다. 비우면 월드 원점(0,0,0) 기준"),
            previewTarget, typeof(Transform), true);

        var action = (ActionData)target;
        int totalFrames = Mathf.Max(1, action.TotalFrames);

        showAllFrames = EditorGUILayout.Toggle("모든 프레임 표시", showAllFrames);
        using (new EditorGUI.DisabledScope(showAllFrames))
        {
            previewFrame = EditorGUILayout.IntSlider("미리보기 프레임", previewFrame, 1, totalFrames);
        }

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("빠른 추가", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Hit 추가")) AddBox(BoxType.Hit, action);
        if (GUILayout.Button("Hurt 추가")) AddBox(BoxType.Hurt, action);
        if (GUILayout.Button("Push 추가")) AddBox(BoxType.Push, action);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Scene 뷰에서 네모 핸들(중심)을 끌면 이동, 점 핸들(모서리)을 끌면 크기 조절됩니다.\n" +
            "좌표는 항상 오른쪽을 본다고 가정하고 편집합니다 — 좌우 반전은 런타임에 자동 처리됩니다.",
            MessageType.Info);
    }

    private void AddBox(BoxType type, ActionData action)
    {
        serializedObject.Update();
        var boxesProp = serializedObject.FindProperty("frameBoxes");

        int newIndex = boxesProp.arraySize;
        boxesProp.arraySize++;
        var newBox = boxesProp.GetArrayElementAtIndex(newIndex);
        newBox.FindPropertyRelative("type").enumValueIndex = (int)type;
        newBox.FindPropertyRelative("bodyPart").enumValueIndex = (int)BodyPart.Core;
        newBox.FindPropertyRelative("rect").rectValue = new Rect(0f, 0f, 1f, 1f);
        newBox.FindPropertyRelative("startFrame").intValue = 1;
        newBox.FindPropertyRelative("endFrame").intValue = Mathf.Max(1, action.TotalFrames);

        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        var boxesProp = serializedObject.FindProperty("frameBoxes");
        if (boxesProp == null) return;

        Vector3 pivot = previewTarget != null ? previewTarget.position : Vector3.zero;

        serializedObject.Update();

        // 기본 zTest(LessEqual)면 바닥/스프라이트 쿼드 같은 3D 지오메트리에 박스가 가려진다.
        // 항상 맨 위에 그려지도록 강제하고 끝나면 원래 값으로 되돌린다.
        CompareFunction prevZTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;

        // 기준 위치 확인용 십자 마커 — 박스가 안 보일 때 "피벗이 여기가 맞는지"부터 확인할 수 있게
        Handles.color = Color.yellow;
        float crossSize = HandleUtility.GetHandleSize(pivot) * 0.15f;
        Handles.DrawLine(pivot + Vector3.left * crossSize, pivot + Vector3.right * crossSize);
        Handles.DrawLine(pivot + Vector3.up * crossSize, pivot + Vector3.down * crossSize);

        for (int i = 0; i < boxesProp.arraySize; i++)
        {
            SerializedProperty boxProp = boxesProp.GetArrayElementAtIndex(i);
            SerializedProperty rectProp = boxProp.FindPropertyRelative("rect");
            SerializedProperty typeProp = boxProp.FindPropertyRelative("type");
            SerializedProperty startProp = boxProp.FindPropertyRelative("startFrame");
            SerializedProperty endProp = boxProp.FindPropertyRelative("endFrame");

            if (!showAllFrames)
            {
                bool inRange = previewFrame >= startProp.intValue && previewFrame <= endProp.intValue;
                if (!inRange) continue;
            }

            DrawBoxHandle(i, pivot, rectProp, (BoxType)typeProp.enumValueIndex);
        }

        Handles.zTest = prevZTest;

        serializedObject.ApplyModifiedProperties();
    }

    // 박스 하나를 그리고, 중심/모서리 핸들 드래그를 rect 값으로 되돌려 쓴다.
    // 항상 facingRight=true 기준 로컬 좌표로 편집 — BoxResolver의 변환 공식과 반드시 짝을 맞춰야 한다 (§4).
    private void DrawBoxHandle(int index, Vector3 pivot, SerializedProperty rectProp, BoxType type)
    {
        Rect r = rectProp.rectValue;

        Vector3 worldMin = new Vector3(pivot.x + r.x, pivot.y + r.y, pivot.z);
        Vector3 worldMax = new Vector3(pivot.x + r.x + r.width, pivot.y + r.y + r.height, pivot.z);
        Vector3 worldCenter = (worldMin + worldMax) * 0.5f;

        Color c = ColorFor(type);
        Handles.DrawSolidRectangleWithOutline(
            new[]
            {
                new Vector3(worldMin.x, worldMin.y, pivot.z),
                new Vector3(worldMax.x, worldMin.y, pivot.z),
                new Vector3(worldMax.x, worldMax.y, pivot.z),
                new Vector3(worldMin.x, worldMax.y, pivot.z)
            },
            new Color(c.r, c.g, c.b, 0.15f), c);

        Handles.Label(worldMax, $"[{index}] {type}");

        Handles.color = c;
        float handleSize = HandleUtility.GetHandleSize(worldCenter) * 0.06f;

        // 중심 — 드래그하면 크기 유지한 채 전체 이동
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.FreeMoveHandle(worldCenter, handleSize, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 delta = newCenter - worldCenter;
            r.x += delta.x;
            r.y += delta.y;
            rectProp.rectValue = r;
            return;
        }

        // 최소 모서리(좌하단) — 반대쪽(최대 모서리)은 고정한 채 크기 조절
        EditorGUI.BeginChangeCheck();
        Vector3 newMin = Handles.FreeMoveHandle(worldMin, handleSize, Vector3.zero, Handles.DotHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            float fixedMaxX = r.x + r.width;
            float fixedMaxY = r.y + r.height;
            r.x = newMin.x - pivot.x;
            r.y = newMin.y - pivot.y;
            r.width = fixedMaxX - r.x;
            r.height = fixedMaxY - r.y;
            rectProp.rectValue = r;
            return;
        }

        // 최대 모서리(우상단) — 최소 모서리는 고정한 채 크기 조절
        EditorGUI.BeginChangeCheck();
        Vector3 newMax = Handles.FreeMoveHandle(worldMax, handleSize, Vector3.zero, Handles.DotHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            r.width = newMax.x - pivot.x - r.x;
            r.height = newMax.y - pivot.y - r.y;
            rectProp.rectValue = r;
        }
    }

    private static Color ColorFor(BoxType type)
    {
        switch (type)
        {
            case BoxType.Hit: return HitColor;
            case BoxType.Hurt: return HurtColor;
            case BoxType.Push: return PushColor;
            default: return Color.white;
        }
    }
}
