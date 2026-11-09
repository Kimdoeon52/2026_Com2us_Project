using UnityEngine;

/// <summary>
/// 카메라에 실제로 보이는 경기장 평면의 좌우 끝 (2026-11-09 사용자: 카메라 밖으로 못 나가게, 보스 패턴도 화면 안에서만 —
/// 카메라를 뒤로 빼면 그만큼 넓어지게).
///
/// 원근 카메라가 살짝 기울어 있으면 높이마다 보이는 폭이 다르다. 그래서 화면 왼쪽·오른쪽 끝 광선을 위·아래 두 개씩 쏴서
/// 경기장 평면(z = planeZ)과 만나는 선을 구하고, 그 선 위에서 원하는 높이(y)의 x를 읽는다. 직교 카메라도 같은 식으로 맞는다.
/// 순수 계산(상태 없음) — 플레이어 이동(RobotMover)·쿄다이 경기장(KyodaiBossController)이 같은 함수를 쓴다.
/// </summary>
public static class CameraBounds
{
    /// <summary>높이 y에서 화면에 보이는 x 범위. 카메라가 없거나 평면이 카메라 뒤에 있으면 false</summary>
    public static bool TryGetVisibleX(Camera cam, float planeZ, float y, out float minX, out float maxX)
    {
        minX = float.MinValue;
        maxX = float.MaxValue;
        if (cam == null) return false;
        if (!EdgeX(cam, 0f, planeZ, y, out float left) || !EdgeX(cam, 1f, planeZ, y, out float right)) return false;
        minX = Mathf.Min(left, right);
        maxX = Mathf.Max(left, right);
        return true;
    }

    // 화면 가로 위치 vx(0 = 왼쪽 끝, 1 = 오른쪽 끝)의 세로선이 평면에 닿는 선 위에서, 높이 y의 x
    private static bool EdgeX(Camera cam, float vx, float planeZ, float y, out float x)
    {
        x = 0f;
        if (!Hit(cam.ViewportPointToRay(new Vector3(vx, 0f, 0f)), planeZ, out Vector3 a)) return false;
        if (!Hit(cam.ViewportPointToRay(new Vector3(vx, 1f, 0f)), planeZ, out Vector3 b)) return false;
        float dy = b.y - a.y;
        x = Mathf.Abs(dy) < 1e-5f ? a.x : Mathf.LerpUnclamped(a.x, b.x, (y - a.y) / dy);
        return true;
    }

    private static bool Hit(Ray ray, float planeZ, out Vector3 point)
    {
        point = default;
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return false;
        float t = (planeZ - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return false;
        point = ray.origin + ray.direction * t;
        return true;
    }
}
