using UnityEngine;

public static class TransformExtensions
{
    public static void DestroyChildren(this Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Object.Destroy(t.GetChild(i).gameObject);
    }

    public static void DestroyChildrenImmediate(this Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    public static void DeactivateChildren(this Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
            t.GetChild(i).gameObject.SetActive(false);
    }

    public static Transform GetOrCreateChild(this Transform t, GameObject prefab, int index)
    {
        if (index < t.childCount) return t.GetChild(index);
        return Object.Instantiate(prefab, t).transform;
    }
}