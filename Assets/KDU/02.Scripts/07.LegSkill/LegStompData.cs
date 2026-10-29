using UnityEngine;

// 스톰프. 지상이면 작은 점프 후, 공중이면 바로 급강하 → 착지 시 착지 행동으로 이어짐
// 애니메이션: 이 에셋 animationClipName = 준비/점프, activeClipName = 급강하, 착지 행동 animationClipName = 착지
[CreateAssetMenu(menuName = "KDU/LegSkill/Stomp", fileName = "LegStomp")]
public class LegStompData : SkillData
{
    [Header("지상 사용")]
    [Tooltip("지상 사용 시 점프 높이")]
    [Min(0f)][SerializeField] private float groundHopHeight;

    [Tooltip("지상 사용 시 추가 선딜 프레임")]
    [Min(0)][SerializeField] private int groundExtraStartupFrames;

    [Header("급강하")]
    [Tooltip("급강하 속도")]
    [Min(0f)][SerializeField] private float diveSpeed;

    [Tooltip("착지 못 해도 낙하 종료 프레임")]
    [Min(1)][SerializeField] private int maxDiveFrames = 120;

    [Header("착지")]
    [Tooltip("착지 충격파 행동")]
    [SerializeField] private ActionData landingAction;

    public float GroundHopHeight => groundHopHeight;
    public int GroundExtraStartupFrames => groundExtraStartupFrames;
    public float DiveSpeed => diveSpeed;
    public int MaxDiveFrames => maxDiveFrames;
    public ActionData LandingAction => landingAction;

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : SkillRunner
    {
        private readonly LegStompData data;
        private bool hopped;

        public Runner(LegStompData data)
        {
            this.data = data;
        }

        protected override void OnBegin()
        {
            hopped = Ctx.Mover != null && Ctx.Mover.IsGrounded;
            if (hopped)
                Ctx.Mover.Leap(0f, MoverPhysics.JumpVelocityFor(Ctx.Mover, data.groundHopHeight));
        }

        protected override int GetPhaseLength(SkillPhase phase)
        {
            if (phase == SkillPhase.Startup)
                return base.GetPhaseLength(phase) + (hopped ? data.groundExtraStartupFrames : 0);

            if (phase == SkillPhase.Active)
                return data.maxDiveFrames;

            return base.GetPhaseLength(phase);
        }

        // 급강하 시작: 수평 관성 제거 + 하강 속도 지정
        protected override void OnPhaseEnter(SkillPhase phase)
        {
            if (phase != SkillPhase.Active || Ctx.Mover == null)
                return;

            Ctx.Mover.Leap(0f, 0f);
            Ctx.Mover.SetVerticalVelocity(-data.diveSpeed);
        }

        protected override void OnInput(in SkillInputFrame input)
        {
            if (Phase != SkillPhase.Active || PhaseFrame < 1)
                return;

            if (Ctx.Mover != null && !Ctx.Mover.IsGrounded)
                return;

            if (data.landingAction != null)
                FinishAndChain(data.landingAction, false);
            else
                RequestPhaseExit();
        }
    }
}
