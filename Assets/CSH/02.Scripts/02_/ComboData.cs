using UnityEngine;

[CreateAssetMenu(menuName = "CSH/Skill/Combo Skill", fileName = "Combo")]
public class ComboData : TapSkillData
{
    [Header("콤보")]
    [Tooltip("이어서 나갈 2타 에셋")]
    [SerializeField] private ActionData nextHit;

    public ActionData NextHit => nextHit;

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : StandardSkillRunner
    {
        private readonly ComboData data;
        public Runner(ComboData data) { this.data = data; }

        private bool InWindow => Phase == SkillPhase.Active || Phase == SkillPhase.Recovery;

        // 입력 창이 열린 동안은 이 키를 내가 가져간다 — 안 그러면 버퍼에 들어가서 1타가 한 번 더 나간다
        public override bool ListensTo(ActionSlot slot) => IsRunning && InWindow && slot == Slot && data.nextHit != null;

        protected override void OnInput(in SkillInputFrame input)
        {
            if (data.nextHit == null || !InWindow) return;
            if (input[Slot].Pressed)
                FinishAndChain(data.nextHit, payForNext: false);   // 1타를 끝내고 같은 프레임에 2타 시작
        }
    }
}
