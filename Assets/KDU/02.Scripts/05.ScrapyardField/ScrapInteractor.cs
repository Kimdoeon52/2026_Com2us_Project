using UnityEngine;

// 상호작용 입력을 직접 받는다. 플레이어 이동 스크립트와 완전히 분리되어 있다
public class ScrapInteractor : MonoBehaviour
{
    [Tooltip("대상 노드 공급원")]
    [SerializeField] private ScrapNodeDetector _detector;

    [Tooltip("데이터 레이어 접점")]
    [SerializeField] private ScrapyardFieldHost _host;

    [Tooltip("IScrapMinigame 구현체. 비워두면 즉시 성공 처리한다")]
    [SerializeField] private MonoBehaviour _minigameBehaviour;

    [Tooltip("수집 입력 키")]
    [SerializeField] private KeyCode _interactKey = KeyCode.E;

    private IScrapMinigame _minigame;
    private bool _busy;

    private void Awake() => _minigame = _minigameBehaviour as IScrapMinigame;

    private void Update()
    {
        if (_busy || !Input.GetKeyDown(_interactKey))
            return;

        ScrapDetectionState state = _detector != null ? _detector.State : default;
        if (!state.InInteractRange)
            return;

        Collect(state.Node);
    }

    private void Collect(ScrapNode node)
    {
        ScrapPile pile = node != null ? node.Pile : null;
        if (pile == null || _host == null)
            return;

        ScrapyardDefinition definition = pile.Definition;
        if (!_host.TryCollect(definition, out CollectOutcome outcome))
            return;

        if (!outcome.IsMinigame)
        {
            pile.ConsumeNode(node);
            return;
        }

        if (_minigame == null)
        {
            _host.ResolveMinigameSuccess(definition, outcome.Table);
            pile.ConsumeNode(node);
            return;
        }

        _busy = true;
        MinigameRewardTable table = outcome.Table;

        _minigame.Begin(table, success =>
        {
            _busy = false;

            if (success)
                _host.ResolveMinigameSuccess(definition, table);

            // 실패해도 노드는 소모된다
            pile.ConsumeNode(node);
        });
    }
}
