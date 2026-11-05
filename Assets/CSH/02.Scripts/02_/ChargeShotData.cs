using UnityEngine;

[CreateAssetMenu(menuName = "CSH/Skill/Charge Shot", fileName = "ChargeShot")]
public class ChargeShotData : HoldSkillData
{
    [Header("차지 단계 — n프레임 이상 입력시 강화")]
    [SerializeField] private int stage2Frames = 60;   // 1.0초
    [SerializeField] private int stage3Frames = 90;   // 1.5초
    [SerializeField] private ActionData stage2;       // 2단계 판정 에셋
    [SerializeField] private ActionData stage3;       // 3단계 판정 에셋

    [Header("홀드 중 강화(D) — 뗄 때의 차지 단계 판정 × 배율")]
    [Tooltip("강화 확정 시 단계 판정의 피해 배율. 기획서: ×1.5")]
    [Min(0f)][SerializeField] private float enhancedDamageScale = 1.5f;

    public override SkillRunner CreateRunner() => new Runner(this);

    // 누른 시간으로 차지 단계 판정을 고른다. 1단계 = 이 에셋 자신
    private ActionData SelectStage(int heldFrames)
    {
        if (heldFrames >= stage3Frames && stage3 != null) return stage3;
        if (heldFrames >= stage2Frames && stage2 != null) return stage2;
        return this;
    }

    private sealed class Runner : HoldSkillRunner
    {
        private readonly ChargeShotData data;
        public Runner(ChargeShotData data) { this.data = data; }

        // 강화 없이 뗄 때 한 번 불린다. 돌려준 에셋으로 판정이 바뀌고 쿨타임·탄이 확정된다
        protected override ActionData SelectReleaseVariant(int heldFrames) => data.SelectStage(heldFrames);

        // 강화(홀드 중 D)는 누르는 순간 탄·쿨이 확정되고 강화 에셋으로 바뀐다. 단계는 아직 모르므로
        // 뗄 때 단계 판정으로 다시 바꾸고 그 행동의 피해 보정(State.Resolved, KKH 계산식이 읽는 값)에 배율을 곱한다.
        // SwapTo가 Resolved를 다시 계산하므로 곱하기는 반드시 SwapTo 뒤에 한다
        protected override void OnHoldReleased(bool timedOut)
        {
            if (!IsEnhanced) return;

            SwapTo(data.SelectStage(HeldFrames));
            Ctx.State.Resolved.Multiply(SkillParamId.Damage, data.enhancedDamageScale);
        }
    }
}
