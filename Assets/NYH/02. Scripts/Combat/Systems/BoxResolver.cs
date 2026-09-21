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
}
