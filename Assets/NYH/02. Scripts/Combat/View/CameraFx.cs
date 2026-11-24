using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 카메라 연출 공용 (2026-11-16) — 화면 흔들림 + 줌인. 카메라를 "그리기 직전에만" 옮기고 그린 직후 되돌린다.
///
/// 왜 그리기 순간에만: 게임 판정·경기장 계산(CameraBounds — 화면 = 경기장)·레이저 투영이 Camera.main의 위치·시야각을 읽는다.
/// 카메라를 실제로 움직이면 흔들림·줌 동안 경기장 폭과 판정이 같이 출렁인다. 그리기 순간에만 바꾸면 화면만 바뀌고 게임은 그대로.
///
/// 흔들림과 줌을 한 곳에서 합치는 이유: 둘이 따로 카메라를 저장·복원하면 순서에 따라 서로의 복원이 덮어써진다.
/// 쓰는 곳: KyodaiCameraShake(흔들림 — 이쪽으로 넘김), DodgeSuccessDirector(회피 성공 줌인).
/// </summary>
public static class CameraFx
{
    // 흔들림
    private static float shakeAmplitude, shakeEnd, shakeDuration;

    // 줌: Zoom01(0~1)은 DOTween이 움직인다. 강도·따라갈 대상은 시작할 때 정한다
    public static float Zoom01;
    private static float zoomFovScale;     // 최대 줌일 때 시야각 배율 (예: 0.88)
    private static float zoomFollow;       // 최대 줌일 때 대상 쪽으로 따라가는 비율
    private static Vector3 zoomTarget;

    private static bool hooked;
    private static Camera fxCamera;
    private static Vector3 savedPosition;
    private static float savedFov;

    /// <summary>amp(월드) 크기로 seconds 동안 흔든다. 점점 줄어든다. 더 센 흔들림이 오면 새로 시작, 약하면 무시</summary>
    public static void Shake(float amp, float seconds)
    {
        if (amp <= 0f) return;
        Hook();
        float now = Time.realtimeSinceStartup;
        if (amp >= CurrentShake(now))
        {
            shakeAmplitude = amp;
            shakeDuration = Mathf.Max(0.01f, seconds);
            shakeEnd = now + shakeDuration;
        }
    }

    /// <summary>줌 준비 — 최대일 때 시야각을 (1 - amount)배로, 대상 쪽으로 follow만큼. 실제 진행(Zoom01)은 호출한 쪽이 트윈으로</summary>
    public static void SetZoom(float amount, float follow, Vector3 target)
    {
        Hook();
        zoomFovScale = 1f - Mathf.Clamp(amount, 0f, 0.9f);
        zoomFollow = Mathf.Clamp01(follow);
        zoomTarget = target;
    }

    private static float CurrentShake(float now) => now >= shakeEnd ? 0f : shakeAmplitude * (shakeEnd - now) / shakeDuration;

    private static void Hook()
    {
        if (hooked) return;
        RenderPipelineManager.beginCameraRendering += BeforeRender;
        RenderPipelineManager.endCameraRendering += AfterRender;
        hooked = true;
    }

    private static void BeforeRender(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;
        // Play 종료 후에도 static 값·렌더 훅이 남아 에디터 Game 뷰가 흔들리던 문제 방지
        if (!Application.isPlaying) return;
        float now = Time.realtimeSinceStartup;
        float shake = CurrentShake(now);
        float zoom = Mathf.Clamp01(Zoom01);
        if (shake <= 0f && zoom <= 0f) return;

        fxCamera = cam;
        savedPosition = cam.transform.position;
        savedFov = cam.fieldOfView;
        savedOrtho = cam.orthographicSize;

        Vector3 p = savedPosition;
        if (zoom > 0f)
        {
            // 대상이 화면 가운데로 오도록 x·y만 따라간다 (깊이는 그대로)
            p.x += (zoomTarget.x - p.x) * zoomFollow * zoom;
            p.y += (zoomTarget.y - p.y) * zoomFollow * zoom;
            if (cam.orthographic) cam.orthographicSize = savedOrtho * Mathf.Lerp(1f, zoomFovScale, zoom);
            else cam.fieldOfView = savedFov * Mathf.Lerp(1f, zoomFovScale, zoom);
        }
        if (shake > 0f)
        {
            float t = now * 40f;
            p += new Vector3(Mathf.PerlinNoise(t, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, t) - 0.5f, 0f) * (2f * shake);
        }
        cam.transform.position = p;
    }

    private static float savedOrtho;

    private static void AfterRender(ScriptableRenderContext context, Camera cam)
    {
        if (cam != fxCamera) return;
        cam.transform.position = savedPosition;
        if (cam.orthographic) cam.orthographicSize = savedOrtho;
        else cam.fieldOfView = savedFov;
        fxCamera = null;
    }
}
