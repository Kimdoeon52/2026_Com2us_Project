using UnityEngine;

/// <summary>
/// 로컬 판정 박스 좌표를 월드 좌표로 바꾸는 단일 지점 (CLAUDE.md §4).
/// "이 변환은 한 군데에서만 수행한다 — 여러 곳에 흩어지면 반드시 한쪽을 빠뜨린다."
/// BoxDrawer(표시)와 HitDetection(판정)이 반드시 이 함수 하나만 통해서 좌우 반전을 처리한다.
/// </summary>
public static class BoxResolver
{
    public static Rect ToWorldRect(Vector3 pivotWorldPos, Rect localRect, bool facingRight)
    {
        float worldX = facingRight
            ? pivotWorldPos.x + localRect.x
            : pivotWorldPos.x - localRect.x - localRect.width;

        float worldY = pivotWorldPos.y + localRect.y;

        return new Rect(worldX, worldY, localRect.width, localRect.height);
    }
}
