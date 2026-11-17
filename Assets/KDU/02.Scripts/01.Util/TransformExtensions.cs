using UnityEngine;

public static class TransformExtensions
{
    public static Transform GetOrCreateChild(this Transform t, GameObject prefab, int index)
    {
        if (index < t.childCount) return t.GetChild(index);
        return Object.Instantiate(prefab, t).transform;
    }
}