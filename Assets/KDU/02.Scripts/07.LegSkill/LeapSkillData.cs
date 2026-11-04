using UnityEngine;

// 수직 도약 스킬. 활성 프레임 동안 도약 높이까지 상승
[CreateAssetMenu(menuName = "KDU/LegSkill/Leap Skill", fileName = "LeapSkill")]
public class LeapSkillData : TapSkillData
{
    [Header("도약")]
    [Tooltip("도약 높이")]
    [Min(0f)][SerializeField] private float leapHeight;

    public float LeapHeight => leapHeight;

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : StandardSkillRunner
    {
        private readonly LeapSkillData data;

        public Runner(LeapSkillData data)
        {
            this.data = data;
        }

        // 이륙. 수평 이동 잠금
        protected override void OnPhaseEnter(SkillPhase phase)
        {
            if (phase != SkillPhase.Active || Ctx.Mover == null)
                return;

            Ctx.Mover.Leap(0f, RiseVelocity(1));
        }

        // 매 틱 상승 속도 지정 (RobotMover가 이어서 중력을 더하므로 그만큼 보정)
        protected override void OnPhaseTick(SkillPhase phase, in SkillInputFrame input)
        {
            if (phase != SkillPhase.Active || Ctx.Mover == null)
                return;

            Ctx.Mover.SetVerticalVelocity(RiseVelocity(PhaseFrame) - Ctx.Mover.Gravity * CombatClock.TICK);
        }

        // 정점에서 정지. 이후 중력으로 낙하
        protected override void OnPhaseExit(SkillPhase phase)
        {
            if (phase == SkillPhase.Active && Ctx.Mover != null)
                Ctx.Mover.SetVerticalVelocity(0f);
        }

        // tick번째 틱의 속도. 등감속으로 활성 마지막 틱에 정확히 도약 높이 도달
        private float RiseVelocity(int tick)
        {
            int frames = Timeline != null ? Timeline.ActiveFrames : 0;
            if (frames <= 0)
                return 0f;

            return 2f * data.leapHeight * (frames - tick + 1) / (CombatClock.TICK * frames * (frames + 1));
        }
    }
}
