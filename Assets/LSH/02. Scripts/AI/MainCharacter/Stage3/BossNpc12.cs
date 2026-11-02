using UnityEngine;

// Stage3 보스: BossNpc2 스킬 재사용 (전용 스킬이 나오면 교체)
public class BossNpc12 : BossNpc2
{
    public override void Start()
    {
        base.Start();
        SetSkillChance(15, 2); //15% 확률로 스킬 발동, 2회 발동
        RaiseGold = 25;
    }
}
