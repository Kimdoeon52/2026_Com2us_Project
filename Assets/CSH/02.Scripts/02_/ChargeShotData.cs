using UnityEngine;

[CreateAssetMenu(menuName = "CSH/Skill/Charge Shot", fileName = "ChargeShot")]
public class ChargeShotData : HoldSkillData
{
    [Header("차지 단계 — n프레임 이상 입력시 강화")]
    [SerializeField] private int stage2Frames = 60;   // 1.0초
    [SerializeField] private int stage3Frames = 90;   // 1.5초
    [SerializeField] private ActionData stage2;       // 2단계 판정 에셋
    [SerializeField] private ActionData stage3;       // 3단계 판정 에셋

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : HoldSkillRunner
    {
        private readonly ChargeShotData data;
        public Runner(ChargeShotData data) { this.data = data; }

        // 뗄 때 한 번 불린다. 돌려준 에셋으로 판정이 바뀌고 쿨타임·탄이 확정된다
        protected override ActionData SelectReleaseVariant(int heldFrames)
        {
            if (heldFrames >= data.stage3Frames && data.stage3 != null) return data.stage3;
            if (heldFrames >= data.stage2Frames && data.stage2 != null) return data.stage2;
            return Current; // 1단계 = 이 에셋 자신
        }
    }
}
