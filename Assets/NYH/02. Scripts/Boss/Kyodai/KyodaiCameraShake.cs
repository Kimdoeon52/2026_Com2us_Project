/// <summary>
/// 화면 흔들림 (2026-11-04, KyodaiContacts 충격 먼지용).
/// 게임 카메라를 그리기 직전에만 옮기고 그린 직후 되돌린다 — 카메라를 움직이는 다른 스크립트와 부딪히지 않는다.
/// 세게 흔드는 중에 약한 흔들림이 오면 무시하고, 더 센 흔들림이 오면 새로 시작한다.
/// 2026-11-16: 실제 처리는 공용 CameraFx로 옮김 — 회피 성공 줌인과 같은 순간에 카메라를 저장·복원하면 서로 덮어써서 합쳤다.
/// 쿄다이 코드는 그대로 이걸 부르면 된다.
/// </summary>
public static class KyodaiCameraShake
{
    /// <summary>amp(월드) 크기로 seconds 동안 흔든다. 점점 줄어든다</summary>
    public static void Shake(float amp, float seconds) => CameraFx.Shake(amp, seconds);
}
