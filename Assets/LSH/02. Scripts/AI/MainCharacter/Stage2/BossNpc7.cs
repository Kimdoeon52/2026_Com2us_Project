using UnityEngine;

// Stage2 보스: BossNpc2 스킬 재사용 (전용 스킬이 나오면 교체)
public class BossNpc7 : BossNpc2
{
    public override void Start()
    {
        base.Start();
        SetSkillChance(10, 1); //10% 확률로 스킬 발동, 1회 발동
        RaiseGold = 15;
    }
}
