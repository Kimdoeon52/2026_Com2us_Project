using UnityEngine;

/// <summary>
/// 월드 좌표로 바뀐 판정 박스 하나 (중심 + 반크기 + 회전축).
/// 회전이 없으면 Rect 그대로 AABB 겹침 검사를 하고, 회전이 있을 때만 SAT(분리축 정리)로 검사한다.
/// 화면 표시(BoxDrawer)와 판정(HitDetection)이 둘 다 이 구조체를 거치므로 "그려진 모양 = 판정되는 모양"이 보장된다
/// </summary>
public readonly struct WorldBox
{
    /// <summary>회전 전 축 정렬 사각형. 회전이 없을 때는 이게 곧 판정 범위다</summary>
    public readonly Rect Rect;
    public readonly Vector2 Center;
    public readonly Vector2 Half;
    /// <summary>박스의 가로 방향 단위벡터(회전 후)</summary>
    public readonly Vector2 AxisX;
    /// <summary>박스의 세로 방향 단위벡터(회전 후)</summary>
    public readonly Vector2 AxisY;
    public readonly bool IsRotated;

    public WorldBox(Rect rect, float angleDeg)
    {
        Rect = rect;
        Center = rect.center;
        Half = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
        IsRotated = !Mathf.Approximately(angleDeg, 0f);

        if (IsRotated)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            AxisX = new Vector2(c, s);
            AxisY = new Vector2(-s, c);
        }
        else
        {
            AxisX = Vector2.right;
            AxisY = Vector2.up;
        }
    }

    /// <summary>네 꼭짓점을 좌하→우하→우상→좌상 순서로 채운다 (회전 반영). 그리기 전용. corners 길이는 4 이상이어야 한다</summary>
    public void GetCorners(Vector3[] corners)
    {
        Vector2 x = AxisX * Half.x;
        Vector2 y = AxisY * Half.y;
        corners[0] = Center - x - y;
        corners[1] = Center + x - y;
        corners[2] = Center + x + y;
        corners[3] = Center - x + y;
    }
}