using UnityEngine;

public class Npc2: NpcAiBase
{ //초반형
    public override void Start()
    {
        base.Start();
        RaiseGold = 15; // 초기 레이즈 금액 설정
    }
    
}
