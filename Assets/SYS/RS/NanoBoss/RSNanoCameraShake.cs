// RE:AL STEEL - 화면 흔들림 (나노봇 패턴 연출 트랙)
//
// 카메라 트랜스폼을 렌더링 직전에만 흔들고 직후 되돌린다 → 카메라를 움직이는 다른 스크립트와 싸우지 않는다.
using UnityEngine;
using UnityEngine.Rendering;

namespace RealSteel.NanoBoss
{
    public static class RSNanoCameraShake
    {
        static float amount, until, duration;
        static bool hooked;
        static Vector3 saved;
        static Camera shaking;

        public static void Shake(float amp, float dur)
        {
            if (!hooked)
            {
                RenderPipelineManager.beginCameraRendering += Begin;
                RenderPipelineManager.endCameraRendering += End;
                hooked = true;
            }
            float now = Time.realtimeSinceStartup;
            if (now > until || amp >= Current(now)) { amount = amp; duration = Mathf.Max(0.01f, dur); until = now + duration; }
        }

        static float Current(float now) => now >= until ? 0f : amount * (until - now) / duration;

        static void Begin(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam.cameraType != CameraType.Game) return;
            float a = Current(Time.realtimeSinceStartup);
            if (a <= 0f) return;
            shaking = cam;
            saved = cam.transform.position;
            float t = Time.realtimeSinceStartup * 37f;
            cam.transform.position = saved + new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * a);
        }

        static void End(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != shaking) return;
            cam.transform.position = saved;
            shaking = null;
        }
    }
}
