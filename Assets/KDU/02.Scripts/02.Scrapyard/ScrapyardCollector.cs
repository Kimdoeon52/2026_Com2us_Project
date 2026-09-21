using UnityEngine;

// 수집 1회 판정. 분기 → 즉시 획득 또는 미니게임 진입
public class ScrapyardCollector
{
    private readonly Rng _rng;
    private readonly IGameClock _clock;
    private readonly ComponentCatalog _catalog;

    public ScrapyardCollector(Rng rng, IGameClock clock, ComponentCatalog catalog)
    {
        _rng = rng;
        _clock = clock;
        _catalog = catalog;
    }

    // 잔량·시간이 모자라거나 가중치가 비어 있으면 false
    public bool TryCollect(ScrapyardDefinition definition, ScrapyardState state, out CollectOutcome outcome)
    {
        outcome = default;

        if (definition == null || state == null || _rng == null || _catalog == null)
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

        if (!TryRollComponent(definition.InstantWeights, out ComponentDefinition component))
            return false;

        int amount = RollAmount(definition.InstantAmount);
        amount = state.Consume(amount, definition.MonthlyTotal);
        outcome = CollectOutcome.Instant(component, amount);
        return true;
    }

    // 미니게임 성공 보상
    public bool TryResolveMinigameSuccess(ScrapyardDefinition definition, MinigameRewardTable table, ScrapyardState state, out CollectOutcome outcome)
    {
        return TryResolveMinigame(definition, table, state, table != null ? table.SuccessWeights : null, table != null ? table.SuccessAmount : default, out outcome);
    }

    // 미니게임 실패 보상. 빈손으로 돌려보내지 않고 낮은 등급을 준다
    public bool TryResolveMinigameFailure(ScrapyardDefinition definition, MinigameRewardTable table, ScrapyardState state, out CollectOutcome outcome)
    {
        return TryResolveMinigame(definition, table, state, table != null ? table.FailureWeights : null, table != null ? table.FailureAmount : default, out outcome);
    }

    private bool TryResolveMinigame(ScrapyardDefinition definition, MinigameRewardTable table, ScrapyardState state, GradeWeight[] weights, Vector2Int amountRange, out CollectOutcome outcome)
    {
        outcome = default;

        if (definition == null || table == null || state == null || _rng == null || _catalog == null)
            return false;

        if (!TryRollComponent(weights, out ComponentDefinition component))
            return false;

        int amount = RollAmount(amountRange);
        amount = state.Consume(amount, definition.MonthlyTotal);
        outcome = CollectOutcome.Instant(component, amount);
        return true;
    }

    // 등급을 뽑고 그 등급 부품 중 하나를 고른다
    private bool TryRollComponent(GradeWeight[] weights, out ComponentDefinition component)
    {
        component = null;

        if (!GradeRoller.TryRoll(weights, _rng, out PartGrade grade))
            return false;

        return _catalog.TryPick(grade, _rng, out component);
    }

    private int RollAmount(Vector2Int range)
    {
        int min = Mathf.Max(0, range.x);
        int max = Mathf.Max(min, range.y);
        return _rng.Range(min, max + 1);
    }
}
