using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 선택한 UI 계층의 크기·위치·폰트에 배율을 곱해 굽는다. localScale은 1 유지
public class UIScaleBakeWindow : EditorWindow
{
    private float _factor = 1.5f;
    private bool _includeRoot = true;

    [MenuItem("KDU/UI/크기 일괄 배율")]
    private static void Open()
    {
        GetWindow<UIScaleBakeWindow>("크기 일괄 배율");
    }

    private void OnGUI()
    {
        _factor = Mathf.Max(0.01f, EditorGUILayout.FloatField("배율", _factor));
        _includeRoot = EditorGUILayout.Toggle("선택 루트 포함", _includeRoot);
        EditorGUILayout.HelpBox("Hierarchy에서 대상 루트를 선택한다. Ctrl+Z로 되돌릴 수 있다.", MessageType.None);

        using (new EditorGUI.DisabledScope(Selection.transforms.Length == 0))
        {
            if (GUILayout.Button($"적용 (선택 {Selection.transforms.Length}개)"))
                Apply();
        }
    }

    private void OnSelectionChange() => Repaint();

    private void Apply()
    {
        Undo.SetCurrentGroupName("UI 크기 일괄 배율");
        int group = Undo.GetCurrentGroup();

        foreach (Transform root in Selection.transforms)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root && !_includeRoot)
                    continue;

                BakeTransform(t);
                BakeText(t);
                BakeLayout(t);
            }
        }

        Undo.CollapseUndoOperations(group);
    }

    private void BakeTransform(Transform t)
    {
        // 루트 캔버스 렉트는 캔버스가 구동하므로 제외
        Canvas canvas = t.GetComponent<Canvas>();
        if (canvas != null && canvas.isRootCanvas)
            return;

        if (t is RectTransform rect)
        {
            Undo.RecordObject(rect, "UI 크기 일괄 배율");
            rect.sizeDelta *= _factor;
            rect.anchoredPosition *= _factor;
            return;
        }

        // UI 계층 안의 일반 Transform은 위치만
        Undo.RecordObject(t, "UI 크기 일괄 배율");
        t.localPosition *= _factor;
    }

    private void BakeText(Transform t)
    {
        if (t.TryGetComponent(out TMP_Text tmp))
        {
            Undo.RecordObject(tmp, "UI 크기 일괄 배율");
            tmp.fontSize *= _factor;
            tmp.fontSizeMin *= _factor;
            tmp.fontSizeMax *= _factor;
        }

        if (t.TryGetComponent(out Text text))
        {
            Undo.RecordObject(text, "UI 크기 일괄 배율");
            text.fontSize = Mathf.RoundToInt(text.fontSize * _factor);
            text.resizeTextMinSize = Mathf.RoundToInt(text.resizeTextMinSize * _factor);
            text.resizeTextMaxSize = Mathf.RoundToInt(text.resizeTextMaxSize * _factor);
        }
    }

    private void BakeLayout(Transform t)
    {
        if (t.TryGetComponent(out LayoutGroup layout))
        {
            Undo.RecordObject(layout, "UI 크기 일괄 배율");
            RectOffset p = layout.padding;
            layout.padding = new RectOffset(Scale(p.left), Scale(p.right), Scale(p.top), Scale(p.bottom));

            if (layout is GridLayoutGroup grid)
            {
                grid.cellSize *= _factor;
                grid.spacing *= _factor;
            }
            else if (layout is HorizontalOrVerticalLayoutGroup line)
            {
                line.spacing *= _factor;
            }
        }

        if (t.TryGetComponent(out LayoutElement element))
        {
            Undo.RecordObject(element, "UI 크기 일괄 배율");
            element.minWidth = ScaleOptional(element.minWidth);
            element.minHeight = ScaleOptional(element.minHeight);
            element.preferredWidth = ScaleOptional(element.preferredWidth);
            element.preferredHeight = ScaleOptional(element.preferredHeight);
        }
    }

    private int Scale(int value) => Mathf.RoundToInt(value * _factor);

    // LayoutElement의 -1은 미사용 표시라 그대로 둔다
    private float ScaleOptional(float value) => value < 0f ? value : value * _factor;
}
