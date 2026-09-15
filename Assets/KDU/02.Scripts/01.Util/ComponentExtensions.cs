using UnityEngine;

public static class ComponentExtensions
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
    {
        return go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
    }

    public static T GetOrAddComponent<T>(this Component self) where T : Component
    {
        return self.gameObject.GetOrAddComponent<T>();
    }
}