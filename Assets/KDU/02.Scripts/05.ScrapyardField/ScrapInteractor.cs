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

    [Tooltip("결과 문구를 띄울 곳. 비워두면 표시하지 않는다")]
    [SerializeField] private ScrapMessageLog _message;

    [Tooltip("수집 입력 키")]
    [SerializeField] private KeyCode _interactKey = KeyCode.E;

    private IScrapMinigame _minigame;
    private bool _busy;

    public KeyCode InteractKey => _interactKey;

    // 안내 문구가 이 값을 보고 뜨고 사라진다
    public bool CanInteract => !_busy && _detector != null && _detector.State.InInteractRange;

    private void Awake() => _minigame = _minigameBehaviour as IScrapMinigame;

    private void Update()
    {
        if (_busy || !Input.GetKeyDown(_interactKey))
            return;

        ScrapDetectionState state = _detector != null ? _detector.State : default;
        if (!state.InInteractRange)
        {
            ShowWarning(state.HasTarget ? "너무 멀다" : "주변에 아무것도 없다");
            return;
        }

        Collect(state.Node);
    }

    private void Collect(ScrapNode node)
    {
        ScrapPile pile = node != null ? node.Pile : null;
        if (pile == null || _host == null)
            return;

        ScrapyardDefinition definition = pile.Definition;
        if (!_host.TryCollect(definition, out CollectOutcome outcome))
        {
            ShowWarning("더 이상 나올 게 없다");
            return;
        }

        if (!outcome.IsMinigame)
        {
            pile.ConsumeNode(node);
            ShowGranted(outcome);
            return;
        }

        MinigameRewardTable table = outcome.Table;

        if (_minigame == null)
        {
            Settle(pile, node, definition, table, true);
            return;
        }

        _busy = true;

        _minigame.Begin(result =>
        {
            _busy = false;
            Settle(pile, node, definition, table, result.Success);
        });
    }

    // 재도전이 없으므로 성패와 무관하게 노드를 소모하고 보상을 정산한다
    private void Settle(ScrapPile pile, ScrapNode node, ScrapyardDefinition definition, MinigameRewardTable table, bool success)
    {
        if (_host.ResolveMinigame(definition, table, success, out CollectOutcome reward))
            ShowReward(reward, success);
        else if (!success)
            ShowWarning("실패 · 아무것도 건지지 못했다");

        pile.ConsumeNode(node);
    }

    private void ShowGranted(CollectOutcome outcome) => ShowReward(outcome, true);

    private void ShowReward(CollectOutcome outcome, bool success)
    {
        if (_message == null || outcome.Component == null)
            return;

        string text = $"{outcome.Component.DisplayName} x{outcome.Amount}";

        if (success)
            _message.Show(text);
        else
            _message.ShowWarning($"실패 · {text}");
    }

    private void ShowWarning(string message)
    {
        if (_message != null)
            _message.ShowWarning(message);
    }
}
