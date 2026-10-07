using System.Collections.Generic;

// 전투 쪽 장착 파츠 조회 경계. 부위는 PartMasterData.slotType으로 판정한다
public interface IEquipmentSource
{
    // 장착된 파츠 ID. 빈 부위는 빠진다
    IReadOnlyList<string> GetEquippedPartIds();

    // 영구 파괴된 파츠를 장착에서 제거. 장착 안 된 ID는 무시
    void RemoveDestroyed(IEnumerable<string> partIds);
}
