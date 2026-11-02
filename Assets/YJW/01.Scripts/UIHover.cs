using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIHover : MonoBehaviour
{
    PointerEventData pointerData;
    readonly List<RaycastResult> results = new List<RaycastResult>();

    void Update()
    {
        if (EventSystem.current == null) return;

        pointerData ??= new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;

        results.Clear();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count > 0)
            Debug.LogError($"UI: {results[0].gameObject.name}"); // [0]이 가장 위에 있는 UI
    }
}