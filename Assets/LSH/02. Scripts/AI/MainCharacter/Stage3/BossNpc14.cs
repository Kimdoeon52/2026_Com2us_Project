using UnityEngine;

// Stage3 보스: BossNpc4 스킬 재사용 (전용 스킬이 나오면 교체)
public class BossNpc14 : BossNpc4
{
    public override void Start()
    {
        base.Start();
        SetSkillChance(40, 2); //40% 확률로 스킬 발동, 2회 발동
        RaiseGold = 25;
    }
}
