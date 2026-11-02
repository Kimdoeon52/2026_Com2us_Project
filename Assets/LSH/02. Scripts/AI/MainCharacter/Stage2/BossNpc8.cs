using UnityEngine;

// Stage2 보스: BossNpc3 스킬 재사용 (전용 스킬이 나오면 교체)
public class BossNpc8 : BossNpc3
{
    public override void Start()
    {
        base.Start();
        SetSkillChance(30, 1); //30% 확률로 스킬 발동, 1회 발동
        RaiseGold = 10;
    }
}
