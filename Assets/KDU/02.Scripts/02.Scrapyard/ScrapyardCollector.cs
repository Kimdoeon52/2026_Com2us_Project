using UnityEngine;

// 수집 1회 판정. 분기 → 즉시 획득 또는 미니게임 진입
public class ScrapyardCollector
{
    private readonly Rng _rng;
    private readonly IGameClock _clock;

    public ScrapyardCollector(Rng rng, IGameClock clock)
    {
        _rng = rng;
        _clock = clock;
    }

    // 잔량·시간이 모자라거나 가중치가 비어 있으면 false
    public bool TryCollect(ScrapyardDefinition definition, ScrapyardState state, out CollectOutcome outcome)
    {
        outcome = default;

        if (definition == null || state == null || _rng == null)
            return false;

        if (!state.HasRemaining(definition.MonthlyTotal))
            return false;

        if (_clock != null && !_clock.TryConsume(definition.TimeCostPerCollect))
            return false;

        if (definition.HasRewardTable && _rng.Chance(definition.MinigameChance))
        {
            outcome = CollectOutcome.Minigame(definition.PickRewardTable(_rng));
            return true;
        }

        if (!GradeRoller.TryRoll(definition.InstantWeights, _rng, out PartGrade grade))
            return false;

        int amount = RollAmount(definition.InstantAmount);
        amount = state.Consume(amount, definition.MonthlyTotal);
        outcome = CollectOutcome.Instant(grade, amount);
        return true;
    }

    // 미니게임 성공 보상. 실패는 패널티가 없어 호출하지 않는다
    public bool TryResolveMinigameSuccess(ScrapyardDefinition definition, MinigameRewardTable table, ScrapyardState state, out CollectOutcome outcome)
    {
        outcome = default;

        if (definition == null || table == null || state == null || _rng == null)
            return false;

        if (!GradeRoller.TryRoll(table.SuccessWeights, _rng, out PartGrade grade))
            return false;

        int amount = RollAmount(table.SuccessAmount);
        amount = state.Consume(amount, definition.MonthlyTotal);
        outcome = CollectOutcome.Instant(grade, amount);
        return true;
    }

    private int RollAmount(Vector2Int range)
    {
        int min = Mathf.Max(0, range.x);
        int max = Mathf.Max(min, range.y);
        return _rng.Range(min, max + 1);
    }
}
