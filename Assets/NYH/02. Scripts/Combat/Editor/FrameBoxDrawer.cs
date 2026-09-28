using UnityEditor;
using UnityEngine;

/// <summary>
/// 인스펙터에서 FrameBox를 그리는 드로어. 하는 일은 하나 — **지금 안 읽히는 칸을 못 만지게 잠그고,
/// 실제로 판정이 켜지는 창을 계산해서 보여준다.**
///
/// 앵커를 도입하면서 startFrame/endFrame은 Legacy 박스에서만 읽힌다. 그런데 인스펙터에는 그대로
/// 보이기 때문에, 앵커를 쓰는 박스에서 그 숫자를 고치면 "고쳤는데 아무 일도 안 일어나는" 상황이 된다.
/// 실제로 한 번 겪어서 만든 드로어다.
///
/// 창 계산은 ResolvedAction.GetWindow를 그대로 호출한다 — 여기서 따로 계산하면 인스펙터에 적힌 창과
/// 실제 판정이 갈라져서 §4가 말하는 "거짓말하는 표시기"가 된다.
/// </summary>
[CustomPropertyDrawer(typeof(FrameBox))]
public class FrameBoxDrawer : PropertyDrawer
{
    // 접혀 있으면 한 줄, 펼치면 필드 8개 + 실제 창 한 줄
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float height = LineHeight();
        foreach (string name in FieldNames)
        {
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name), true)
                    + EditorGUIUtility.standardVerticalSpacing;
        }
        return height + LineHeight(); // 맨 아래 "실제 창" 한 줄
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            line.y += LineHeight();

            var anchorProp = property.FindPropertyRelative("anchor");
            bool isLegacy = anchorProp.enumValueIndex == (int)FrameAnchor.Legacy;

            foreach (string name in FieldNames)
            {
                SerializedProperty field = property.FindPropertyRelative(name);
                // 앵커를 쓰면 startFrame/endFrame이 안 읽히고, Legacy면 반대로 startOffset/length가 안 읽힌다.
                // 안 읽히는 쪽을 회색으로 잠가서 "고쳤는데 왜 안 바뀌지"를 없앤다
                bool unused = isLegacy
                    ? (name == "startOffset" || name == "length")
                    : (name == "startFrame" || name == "endFrame");

                using (new EditorGUI.DisabledScope(unused))
                {
                    line.height = EditorGUI.GetPropertyHeight(field, true);
                    EditorGUI.PropertyField(line, field, true);
                }
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
            }

            DrawWindowLabel(line, property);
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    // "실제 창: 5~6 (활성 구간 전체)" 같은 한 줄. 판정이 실제로 켜지는 프레임을 그대로 보여준다
    private static void DrawWindowLabel(Rect line, SerializedProperty property)
    {
        line.height = EditorGUIUtility.singleLineHeight;

        // 부모 에셋(ActionData)에서 프레임을 꺼내와야 창을 계산할 수 있다.
        // 프리팹 등 다른 곳에 박힌 FrameBox면 계산 근거가 없으므로 그냥 안 그린다
        var action = property.serializedObject.targetObject as ActionData;
        if (action == null)
            return;

        FrameBox box = ReadBox(property);
        ResolvedAction.GetWindow(box, action.StartupFrames, action.ActiveFrames, action.RecoveryFrames,
            out int start, out int end);

        if (end < start)
        {
            EditorGUI.LabelField(line, "실제 창", "없음 — 이 박스는 켜지지 않습니다");
            return;
        }

        string follows = box.anchor != FrameAnchor.Legacy && box.length == 0 ? "  (구간 끝까지 자동으로 따라감)" : "";
        EditorGUI.LabelField(line, "실제 창", $"{start} ~ {end} 프레임{follows}");
    }

    // 창 계산에 필요한 값만 SerializedProperty에서 꺼내 임시 FrameBox로 조립한다
    private static FrameBox ReadBox(SerializedProperty property)
    {
        return new FrameBox
        {
            anchor = (FrameAnchor)property.FindPropertyRelative("anchor").enumValueIndex,
            startOffset = property.FindPropertyRelative("startOffset").intValue,
            length = property.FindPropertyRelative("length").intValue,
            startFrame = property.FindPropertyRelative("startFrame").intValue,
            endFrame = property.FindPropertyRelative("endFrame").intValue
        };
    }

    private static float LineHeight() => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

    // 그리는 순서. 앵커 3칸을 먼저, 예전 방식인 startFrame/endFrame을 뒤에 둔다
    private static readonly string[] FieldNames =
    {
        "type", "bodyPart", "rect", "anchor", "startOffset", "length", "startFrame", "endFrame"
    };
}
