using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 화면 흔들림 (2026-11-04, KyodaiContacts 충격 먼지용).
/// 게임 카메라를 그리기 직전에만 옮기고 그린 직후 되돌린다 — 카메라를 움직이는 다른 스크립트와 부딪히지 않는다.
/// 세게 흔드는 중에 약한 흔들림이 오면 무시하고, 더 센 흔들림이 오면 새로 시작한다.
/// </summary>
public static class KyodaiCameraShake
{
    private static float amplitude, endTime, duration;
    private static bool hooked;
    private static Camera shakingCamera;
    private static Vector3 savedPosition;

    /// <summary>amp(월드) 크기로 seconds 동안 흔든다. 점점 줄어든다</summary>
    public static void Shake(float amp, float seconds)
    {
        if (amp <= 0f) return;
        if (!hooked)
        {
            RenderPipelineManager.beginCameraRendering += BeforeRender;
            RenderPipelineManager.endCameraRendering += AfterRender;
            hooked = true;
        }
        float now = Time.realtimeSinceStartup;
        if (amp >= Current(now))
        {
            amplitude = amp;
            duration = Mathf.Max(0.01f, seconds);
            endTime = now + duration;
        }
    }

    private static float Current(float now) => now >= endTime ? 0f : amplitude * (endTime - now) / duration;

    private static void BeforeRender(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;
        float now = Time.realtimeSinceStartup;
        float a = Current(now);
        if (a <= 0f) return;
        shakingCamera = cam;
        savedPosition = cam.transform.position;
        float t = now * 40f;
        cam.transform.position = savedPosition + new Vector3(Mathf.PerlinNoise(t, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, t) - 0.5f, 0f) * (2f * a);
    }

    private static void AfterRender(ScriptableRenderContext context, Camera cam)
    {
        if (cam != shakingCamera) return;
        cam.transform.position = savedPosition;
        shakingCamera = null;
    }
}
