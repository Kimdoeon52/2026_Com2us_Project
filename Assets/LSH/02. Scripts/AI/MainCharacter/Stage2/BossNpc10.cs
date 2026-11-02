using UnityEngine;

// Stage2 보스: BossNpc5 스킬 재사용 (전용 스킬이 나오면 교체)
public class BossNpc10 : BossNpc5
{
    public override void Start()
    {
        base.Start();
        SetSkillChance(40, 1); //40% 확률로 스킬 발동, 1회 발동
        RaiseGold = 15;
    }
}
