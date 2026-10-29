using UnityEngine;

// 부스터. 시작 시 이동속도 버프 요청
[CreateAssetMenu(menuName = "KDU/LegSkill/Booster Buff", fileName = "BoosterBuff")]
public class BoosterBuffData : TapSkillData
{
    [Header("부스터")]
    [Tooltip("지속 프레임")]
    [Min(0)][SerializeField] private int durationFrames;

    [Tooltip("이동속도 배율")]
    [Min(0f)][SerializeField] private float speedMultiplier = 1f;

    [Tooltip("달리기 판정 유지")]
    [SerializeField] private bool keepRunning;

    public int DurationFrames => durationFrames;
    public float SpeedMultiplier => speedMultiplier;
    public bool KeepRunning => keepRunning;

    public override SkillRunner CreateRunner() => new Runner(this);

    private sealed class Runner : StandardSkillRunner
    {
        private readonly BoosterBuffData data;

        public Runner(BoosterBuffData data)
        {
            this.data = data;
        }

        protected override void OnBegin()
        {
            ISpeedBuffTarget target = Ctx.Transform.GetComponent<ISpeedBuffTarget>();
            if (target == null)
            {
                Debug.LogWarning($"[BoosterBuff] {Ctx.FighterId}: ISpeedBuffTarget 없음");
                return;
            }

            target.ApplySpeedBuff(data.speedMultiplier, data.durationFrames, data.keepRunning);
        }
    }
}
