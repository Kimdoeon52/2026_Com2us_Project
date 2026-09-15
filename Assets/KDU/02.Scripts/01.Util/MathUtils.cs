using UnityEngine;

public static class MathUtils
{
    // ── Remap ──

    /// <summary>from 범위의 값을 to 범위로 옮긴다.</summary>
    public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        if (Mathf.Approximately(fromMax, fromMin)) return toMin;
        return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
    }

    /// <summary>Remap 후 to 범위 안으로 자른다.</summary>
    public static float RemapClamped(this float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        float r = value.Remap(fromMin, fromMax, toMin, toMax);
        return toMin < toMax ? Mathf.Clamp(r, toMin, toMax) : Mathf.Clamp(r, toMax, toMin);
    }

    /// <summary>from 범위를 0~1로 정규화한다.</summary>
    public static float Remap01(this float value, float fromMin, float fromMax)
    {
        return value.RemapClamped(fromMin, fromMax, 0f, 1f);
    }

    // ── Easing (t: 0~1) ──

    public static float EaseInQuad(this float t) => t * t;
    public static float EaseOutQuad(this float t) => t * (2f - t);
    public static float EaseInOutQuad(this float t)
        => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

    public static float EaseInCubic(this float t) => t * t * t;
    public static float EaseOutCubic(this float t) { t -= 1f; return t * t * t + 1f; }
    public static float EaseInOutCubic(this float t)
        => t < 0.5f ? 4f * t * t * t : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;

    /// <summary>끝에서 살짝 넘쳤다 돌아온다. UI 등장에 자주 쓴다.</summary>
    public static float EaseOutBack(this float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    /// <summary>탄성 있게 튕기며 멈춘다.</summary>
    public static float EaseOutElastic(this float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        const float c4 = 2f * Mathf.PI / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    /// <summary>바닥에 튕기듯 떨어진다.</summary>
    public static float EaseOutBounce(this float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }
}