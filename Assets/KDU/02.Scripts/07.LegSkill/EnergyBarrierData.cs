using System.Collections.Generic;
using UnityEngine;

// 에너지 방어막. 활성 동안 전방 투사체 소멸, 종료 충격파는 Hit 박스로 처리
[CreateAssetMenu(menuName = "KDU/LegSkill/Energy Barrier", fileName = "EnergyBarrier")]
public class EnergyBarrierData : TapSkillData
{
    [Header("방어막")]
    [Tooltip("투사체 소멸 범위 (로컬)")]
    [SerializeField] private Rect barrierRect;

    [Tooltip("근접 피해 배율")]
    [Range(0f, 1f)][SerializeField] private float meleeDamageScale = 1f;

    public Rect BarrierRect => barrierRect;
    public float MeleeDamageScale => meleeDamageScale;

    // 피해 계산 쪽 연동용. 방어막 활성 중이 아니면 1
    public static float GetMeleeDamageScale(ActionExecutor defender)
    {
        if (defender == null || defender.State.Phase != ActionPhase.Active)
            return 1f;

        return defender.State.CurrentAction is EnergyBarrierData barrier ? barrier.meleeDamageScale : 1f;
    }

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : StandardSkillRunner
    {
        private readonly EnergyBarrierData data;

        public Runner(EnergyBarrierData data)
        {
            this.data = data;
        }

        protected override void OnPhaseTick(SkillPhase phase, in SkillInputFrame input)
        {
            if (phase != SkillPhase.Active)
                return;

            Rect area = BoxResolver.ToWorldRect(Ctx.Transform.position, data.barrierRect, Ctx.FacingRight);
            IReadOnlyList<Projectile> live = global::Projectile.Live;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Projectile p = live[i];
                if (p.Owner == Ctx.Executor)
                    continue;

                if (area.Overlaps(p.GetWorldHitBox()))
                    p.Explode();
            }
        }
    }
}
