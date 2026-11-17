using UnityEngine;

public static class MathUtils
{
    // ── Easing (t: 0~1) ──

    public static float EaseInQuad(this float t) => t * t;
    public static float EaseOutQuad(this float t) => t * (2f - t);
}