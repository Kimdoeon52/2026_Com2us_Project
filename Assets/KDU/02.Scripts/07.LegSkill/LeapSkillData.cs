using UnityEngine;

// 수직 도약 스킬. 활성 진입 시 도약, 정점에서 활성 종료
[CreateAssetMenu(menuName = "KDU/LegSkill/Leap Skill", fileName = "LeapSkill")]
public class LeapSkillData : TapSkillData
{
    [Header("도약")]
    [Tooltip("도약 높이")]
    [Min(0f)][SerializeField] private float leapHeight;

    [Tooltip("상승 최대 프레임")]
    [Min(1)][SerializeField] private int maxRiseFrames = 120;

    public float LeapHeight => leapHeight;
    public int MaxRiseFrames => maxRiseFrames;

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : StandardSkillRunner
    {
        private readonly LeapSkillData data;

        public Runner(LeapSkillData data)
        {
            this.data = data;
        }

        protected override int GetPhaseLength(SkillPhase phase)
        {
            if (phase == SkillPhase.Active)
                return data.maxRiseFrames;

            return base.GetPhaseLength(phase);
        }

        protected override void OnPhaseEnter(SkillPhase phase)
        {
            if (phase != SkillPhase.Active || Ctx.Mover == null)
                return;

            Ctx.Mover.Leap(0f, MoverPhysics.JumpVelocityFor(Ctx.Mover, data.leapHeight));
        }

        // 상승이 끝나면 후딜로
        protected override void OnInput(in SkillInputFrame input)
        {
            if (Phase != SkillPhase.Active || PhaseFrame < 1)
                return;

            if (Ctx.Mover == null || Ctx.Mover.VerticalVelocity <= 0f)
                RequestPhaseExit();
        }
    }
}
