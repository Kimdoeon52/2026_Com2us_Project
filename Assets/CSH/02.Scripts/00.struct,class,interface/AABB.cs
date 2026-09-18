using UnityEngine;

[System.Serializable]
public struct CustomAABB
{
    public Vector2 offset; // 캐릭터 발밑/피벗 기준 오프셋
    public Vector2 size;   // 가로, 세로 크기

    // 캐릭터 위치와 바라보는 방향(1: 우, -1: 좌)을 반영해 실제 월드 박스 계산
    public Bounds GetWorldBounds(Vector3 characterPos, int facingDirection)
    {
        Vector3 worldCenter = characterPos;
        worldCenter.x += offset.x * facingDirection;
        worldCenter.y += offset.y;

        return new Bounds(worldCenter, new Vector3(size.x, size.y, 1.0f));
    }
}