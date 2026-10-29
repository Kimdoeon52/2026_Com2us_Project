using UnityEngine;

[CreateAssetMenu(menuName = "CSH/Skill/Stomp Skill", fileName = "Stomp")]
public class StompData : SkillData
{
    [Header("스탬프")]
    [Tooltip("땅에 안 닿아도 이 프레임이 지나면 활성을 끝낸다")]
    [Min(1)][SerializeField] private int maxActiveFrames = 120;

    public int MaxActiveFrames => maxActiveFrames;

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : SkillRunner
    {
        private readonly StompData data;
        public Runner(StompData data) { this.data = data; }

        // 공중인지 확인한 뒤에 비용을 내야 하므로, 시작할 때 자동으로 내지 않는다
        protected override bool CommitOnBegin => false;
 
        protected override void OnBegin()
        {
            bool grounded = Ctx.Mover == null || Ctx.Mover.IsGrounded;
            if (grounded)
            {
                Commit();
                return;
            }
            Commit();         // 공중 확인됨 — 쿨타임·탄 확정
        }

        // 활성 구간은 시간이 아니라 착지로 끝난다
        protected override int GetPhaseLength(SkillPhase phase)
        {
            if (phase == SkillPhase.Active) return data.maxActiveFrames;
            return base.GetPhaseLength(phase);
        }

        protected override void OnInput(in SkillInputFrame input)
        {
            if (Phase != SkillPhase.Active || PhaseFrame < 1) return;

            bool grounded = Ctx.Mover == null || Ctx.Mover.IsGrounded;
            if (grounded) RequestPhaseExit();   // 이번 프레임에 후딜로 넘어간다
        }
    }
}
