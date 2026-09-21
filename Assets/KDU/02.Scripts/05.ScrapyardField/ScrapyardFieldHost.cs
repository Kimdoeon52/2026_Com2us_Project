using UnityEngine;

// 필드 오브젝트와 기존 고물상 데이터 레이어를 잇는 유일한 접점.
// 데이터·인벤토리 의존을 이 파일 하나에 가둬둔다
public class ScrapyardFieldHost : MonoBehaviour
{
    [Tooltip("지역 목록. 월간 잔량 리필에 쓴다")]
    [SerializeField] private ScrapyardDatabase _database;

    [Tooltip("수집 결과를 넣을 인벤토리. 부품 목록도 여기서 가져온다")]
    [SerializeField] private InventoryTestHost _inventory;

    [Tooltip("난수 시드")]
    [SerializeField] private int _seed = 1;

    private Rng _rng;
    private DummyGameClock _clock;
    private ScrapyardCollector _collector;
    private ScrapyardStateSet _states;

    public DummyGameClock Clock => _clock;

    private void Awake()
    {
        _rng = new Rng(_seed);
        _clock = new DummyGameClock();
        _states = new ScrapyardStateSet();
        _states.RefillAll(_database, _clock.CurrentMonth);

        ComponentCatalog catalog = _inventory != null ? _inventory.ComponentCatalog : null;
        _collector = new ScrapyardCollector(_rng, _clock, catalog);
    }

    // 즉시 획득이면 그 자리에서 지급한다.
    // 미니게임이면 지급하지 않고 호출자가 ResolveMinigameSuccess를 이어서 부른다
    public bool TryCollect(ScrapyardDefinition definition, out CollectOutcome outcome)
    {
        outcome = default;

        if (definition == null || _collector == null)
            return false;

        ScrapyardState state = _states.GetOrCreate(definition.RegionIndex);
        if (!_collector.TryCollect(definition, state, out outcome))
            return false;

        if (!outcome.IsMinigame)
            Grant(outcome);

        return true;
    }

    // 미니게임 결과 보상. 실패해도 낮은 등급이 나오므로 성패를 받아 분기한다
    public bool ResolveMinigame(ScrapyardDefinition definition, MinigameRewardTable table, bool success, out CollectOutcome outcome)
    {
        outcome = default;

        if (definition == null || table == null || _collector == null)
            return false;

        ScrapyardState state = _states.GetOrCreate(definition.RegionIndex);

        bool resolved = success
            ? _collector.TryResolveMinigameSuccess(definition, table, state, out outcome)
            : _collector.TryResolveMinigameFailure(definition, table, state, out outcome);

        if (!resolved)
            return false;

        Grant(outcome);
        return true;
    }

    private void Grant(CollectOutcome outcome)
    {
        if (_inventory == null || outcome.Component == null || outcome.Amount <= 0)
            return;

        _inventory.GiveComponent(outcome.Component, outcome.Amount);
    }
}
