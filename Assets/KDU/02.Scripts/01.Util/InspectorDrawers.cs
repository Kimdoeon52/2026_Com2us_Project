#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool prev = GUI.enabled;
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = prev;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

[CustomPropertyDrawer(typeof(MinMaxAttribute))]
public class MinMaxDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Vector2)
        {
            EditorGUI.LabelField(position, label.text, "MinMax는 Vector2에만 사용할 수 있습니다.");
            return;
        }

        var attr = (MinMaxAttribute)attribute;
        Vector2 range = property.vector2Value;
        float min = range.x;
        float max = range.y;

        position = EditorGUI.PrefixLabel(position, label);

        const float fieldWidth = 50f;
        const float gap = 4f;

        var minRect = new Rect(position.x, position.y, fieldWidth, position.height);
        var sliderRect = new Rect(position.x + fieldWidth + gap, position.y,
                                  position.width - (fieldWidth + gap) * 2, position.height);
        var maxRect = new Rect(position.xMax - fieldWidth, position.y, fieldWidth, position.height);

        EditorGUI.BeginChangeCheck();

        min = EditorGUI.FloatField(minRect, min);
        EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, attr.Min, attr.Max);
        max = EditorGUI.FloatField(maxRect, max);

        if (EditorGUI.EndChangeCheck())
        {
            min = Mathf.Clamp(min, attr.Min, attr.Max);
            max = Mathf.Clamp(max, min, attr.Max);
            property.vector2Value = new Vector2(min, max);
        }
    }
}
#endif
