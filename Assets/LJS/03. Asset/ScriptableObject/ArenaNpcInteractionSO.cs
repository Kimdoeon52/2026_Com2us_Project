using System;
using UnityEngine;
/// <summary>
/// 플레이어와 아레나 Npc의 접촉시 사용할 코드
/// </summary>
// 이벤트 생성용
[CreateAssetMenu(fileName = "new ArenaNpcInteraction", menuName = "LJS/아레나Npc상호작용")]
public class ArenaNpcInteractionSO : ScriptableObject
{
    public Action<ArenaNpcs> OnInteractableEntered; // NPC 범위 접촉시 발생
    public Action OnInteractableExited; // NPC 범위에서 나갔을 때 발생

    // NPC 접촉시 송출
    public void RaiseInteractableEntered(ArenaNpcs npc)
    {
        OnInteractableEntered?.Invoke(npc);
    }

    // NPC 접촉 종료시 송출
    public void RaiseInteractableExited()
    {
        OnInteractableExited?.Invoke();
    }
}
