using UnityEngine;

public static class VectorExtensions
{
    // ── Vector3 ──
    public static Vector3 With(this Vector3 v, float? x = null, float? y = null, float? z = null)
    {
        return new Vector3(x ?? v.x, y ?? v.y, z ?? v.z);
    }

    public static Vector3 Add(this Vector3 v, float x = 0f, float y = 0f, float z = 0f)
    {
        return new Vector3(v.x + x, v.y + y, v.z + z);
    }

    // ── Vector2 ──
    public static Vector2 With(this Vector2 v, float? x = null, float? y = null)
    {
        return new Vector2(x ?? v.x, y ?? v.y);
    }

    public static Vector2 Add(this Vector2 v, float x = 0f, float y = 0f)
    {
        return new Vector2(v.x + x, v.y + y);
    }
}