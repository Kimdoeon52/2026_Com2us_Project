using UnityEditor;
using UnityEngine;

// 파츠 모양을 칸 토글 그리드로 편집한다
[CustomPropertyDrawer(typeof(PartsShape))]
public class PartsShapeDrawer : PropertyDrawer
{
    private const float CellSize = 22f;
    private const float Gap = 2f;
    private const float Indent = 14f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty sizeProperty = property.FindPropertyRelative("_size");
        SerializedProperty cellsProperty = property.FindPropertyRelative("_cells");

        EditorGUI.BeginProperty(position, label, property);

        var sizeRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(sizeRect, sizeProperty, label);

        Vector2Int size = ClampSize(sizeProperty.vector2IntValue);
        if (size != sizeProperty.vector2IntValue)
            sizeProperty.vector2IntValue = size;

        Resize(cellsProperty, size);
        DrawCells(position, size, cellsProperty);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        Vector2Int size = ClampSize(property.FindPropertyRelative("_size").vector2IntValue);
        return EditorGUIUtility.singleLineHeight + Gap + size.y * (CellSize + Gap);
    }

    // 위쪽 행이 y가 큰 칸
    private void DrawCells(Rect position, Vector2Int size, SerializedProperty cellsProperty)
    {
        float startX = position.x + Indent;
        float startY = position.y + EditorGUIUtility.singleLineHeight + Gap;

        for (int row = 0; row < size.y; row++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int y = size.y - 1 - row;
                var rect = new Rect(startX + x * (CellSize + Gap), startY + row * (CellSize + Gap), CellSize, CellSize);
                SerializedProperty cell = cellsProperty.GetArrayElementAtIndex(y * size.x + x);
                cell.boolValue = GUI.Toggle(rect, cell.boolValue, GUIContent.none, EditorStyles.miniButton);
            }
        }
    }

    // 크기가 바뀌면 꽉 찬 사각형으로 다시 깐다
    private void Resize(SerializedProperty cellsProperty, Vector2Int size)
    {
        int count = size.x * size.y;
        if (cellsProperty.arraySize == count)
            return;

        cellsProperty.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            cellsProperty.GetArrayElementAtIndex(i).boolValue = true;
        }
    }

    private Vector2Int ClampSize(Vector2Int size)
    {
        return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    }
}
