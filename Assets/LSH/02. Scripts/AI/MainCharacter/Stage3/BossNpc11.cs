using UnityEngine;

// Stage3 보스: BossNpc1 스킬 재사용 (전용 스킬이 나오면 교체)
public class BossNpc11 : BossNpc1
{
    public override void Start()
    {
        base.Start();
        SetSkillChance(30, 3); //30% 확률로 스킬 발동, 3회 발동
        RaiseGold = 30;
    }
}
