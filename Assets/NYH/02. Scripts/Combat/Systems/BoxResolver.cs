using UnityEngine;

/// <summary>
/// 캐릭터 로컬 좌표로 저장된 판정 박스(FrameBox.rect)를, 실제로 그리거나 판정하는 데 쓸 수 있는
/// 월드 좌표로 바꿔주는 함수 딱 하나만 담는 곳. 이 변환(특히 좌우 반전 계산)을 BoxDrawer, HitDetection이
/// 각자 따로 구현하면, 나중에 계산 방식을 하나 고칠 때 다른 한쪽에 반영하는 걸 깜빡하기 쉽다 —
/// 그래서 둘 다 반드시 이 함수 하나만 거치도록 강제해서, "그려지는 위치 = 실제로 판정되는 위치"를 보장한다.
/// </summary>
public static class BoxResolver
{
    // pivotWorldPos: 캐릭터의 지금 월드 위치(발밑 중앙, transform.position).
    // localRect: FrameBox에 저장된 로컬 좌표값.
    // facingRight: 지금 오른쪽을 보고 있는지(RobotMover.FacingRight를 그대로 넘겨받음).
    // 반환값: 화면에 그리거나 겹침 판정에 바로 쓸 수 있는 월드 좌표 Rect
    public static Rect ToWorldRect(Vector3 pivotWorldPos, Rect localRect, bool facingRight)
    {
        // 오른쪽을 볼 때는 로컬 x를 그냥 더하면 되지만, 왼쪽을 볼 때는 캐릭터 전체가 거울처럼
        // 뒤집힌 것이므로 좌표도 똑같이 뒤집어야 한다. 단순히 x의 부호만 뒤집으면 박스의 "시작 모서리"가
        // 반대쪽으로 넘어가버려서 위치가 틀어지기 때문에, width만큼 추가로 빼줘서 박스 전체가
        // 피벗을 기준으로 좌우 대칭 이동하도록 계산한다.
        // (이 보정이 없으면 왼쪽을 보고 있을 때 주먹이 등 뒤에서 나가는 것처럼 판정이 어긋나는 버그가 생긴다)
        float worldX = facingRight
            ? pivotWorldPos.x + localRect.x
            : pivotWorldPos.x - localRect.x - localRect.width;

        // 이 게임은 좌우로만 반전되고 캐릭터가 위아래로 뒤집히는 경우는 없으므로, y는 반전 계산 없이 그대로 더한다
        float worldY = pivotWorldPos.y + localRect.y;

        // width/height(박스 크기)는 반전 여부와 무관하게 항상 그대로 유지된다 — 뒤집혀도 박스 자체가 커지거나 작아지진 않으니까
        return new Rect(worldX, worldY, localRect.width, localRect.height);
    }

    // 2026-10-09 추가: 박스가 아니라 "점"(총구 위치 등)을 같은 규칙으로 뒤집는다.
    // 총구 반전을 ActionExecutor 쪽에서 따로 계산하면 박스와 총구가 서로 다른 반전 규칙을 갖게 될 수 있어서 여기 모았다
    public static Vector3 ToWorldPoint(Vector3 pivotWorldPos, Vector2 localPoint, bool facingRight)
    {
        float worldX = facingRight ? pivotWorldPos.x + localPoint.x : pivotWorldPos.x - localPoint.x;
        return new Vector3(worldX, pivotWorldPos.y + localPoint.y, pivotWorldPos.z);
    }

    // 2026-10-15 추가: 회전을 포함한 월드 박스. BoxDrawer(표시)와 HitDetection(판정)은 이 함수만 쓴다.
    // 좌우 반전 규칙: 박스 중심 x는 ToWorldRect가 이미 뒤집고, 회전은 각도 부호만 뒤집으면 된다
    // (좌우 거울상은 θ → -θ. 박스는 180도 대칭이라 이 규칙으로 충분하다).
    // 회전은 Hit박스에서만 읽는다 — Hurt/Push는 AABB를 전제로 한 다른 코드(RobotMover Push 밀어내기)와 어긋나지 않게 항상 0으로 취급
    public static WorldBox ToWorldBox(Vector3 pivotWorldPos, FrameBox box, bool facingRight)
    {
        Rect world = ToWorldRect(pivotWorldPos, box.rect, facingRight);
        float angle = box.type == BoxType.Hit ? (facingRight ? box.rotation : -box.rotation) : 0f;
        return new WorldBox(world, angle);
    }

    /// <summary>
    /// 두 박스가 겹치는지. 둘 다 회전이 없으면 기존 AABB 검사와 똑같은 식(경계가 닿기만 한 건 겹침 아님)을 쓰고,
    /// 하나라도 회전이 있으면 4개 축(각 박스의 두 축)으로 분리 여부를 확인한다
    /// </summary>
    public static bool Overlap(in WorldBox a, in WorldBox b)
    {
        if (!a.IsRotated && !b.IsRotated)
        {
            Rect ra = a.Rect;
            Rect rb = b.Rect;
            return ra.xMin < rb.xMax && ra.xMax > rb.xMin && ra.yMin < rb.yMax && ra.yMax > rb.yMin;
        }

        Vector2 d = b.Center - a.Center;
        return !IsSeparated(a, b, d, a.AxisX)
            && !IsSeparated(a, b, d, a.AxisY)
            && !IsSeparated(a, b, d, b.AxisX)
            && !IsSeparated(a, b, d, b.AxisY);
    }

    // axis 방향으로 두 박스를 투영했을 때 구간이 안 겹치면(중심 거리 >= 반지름 합) 그 축이 둘을 갈라놓는다.
    // >= 로 비교하는 이유: AABB 경로의 엄격한 부등호(닿기만 하면 겹침 아님)와 규칙을 맞추기 위함
    private static bool IsSeparated(in WorldBox a, in WorldBox b, Vector2 d, Vector2 axis)
    {
        float ra = a.Half.x * Mathf.Abs(Vector2.Dot(axis, a.AxisX)) + a.Half.y * Mathf.Abs(Vector2.Dot(axis, a.AxisY));
        float rb = b.Half.x * Mathf.Abs(Vector2.Dot(axis, b.AxisX)) + b.Half.y * Mathf.Abs(Vector2.Dot(axis, b.AxisY));
        return Mathf.Abs(Vector2.Dot(d, axis)) >= ra + rb;
    }
}
