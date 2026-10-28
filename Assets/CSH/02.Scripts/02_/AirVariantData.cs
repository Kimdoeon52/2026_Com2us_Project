using UnityEngine;

[CreateAssetMenu(menuName = "CSH/Skill/Air Variant", fileName = "AirVariant")]
public class AirVariantData : TapSkillData
{
    [Header("공중 버전")]
    [Tooltip("공중에서 시작하면 이 에셋으로 바뀐다. 비우면 지상과 같음")]
    [SerializeField] private ActionData airVariant;

    public ActionData AirVariant => airVariant;

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : StandardSkillRunner
    {
        private readonly AirVariantData data;
        public Runner(AirVariantData data) { this.data = data; }

        // OnBegin은 첫 구간에 들어가기 전에 불린다 — 여기서 바꾸면 선딜부터 공중 버전 수치로 돈다
        protected override void OnBegin()
        {
            bool grounded = Ctx.Mover == null || Ctx.Mover.IsGrounded;
            if (!grounded && data.airVariant != null)
                SwapTo(data.airVariant);
        }
    }
}
