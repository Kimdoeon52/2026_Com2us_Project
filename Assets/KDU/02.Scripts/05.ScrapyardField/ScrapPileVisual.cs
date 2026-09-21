using UnityEngine;

// 잔량 구간에 따라 더미 비주얼을 교체한다. 숫자 UI는 쓰지 않는다
[RequireComponent(typeof(ScrapPile))]
public class ScrapPileVisual : MonoBehaviour
{
    [Tooltip("잔량 구간. 배열 길이가 구간 개수이고 순서는 상관없다")]
    [SerializeField] private ScrapPileStage[] _stages;

    private ScrapPile _pile;
    private int _activeIndex = -1;

    private void Awake() => _pile = GetComponent<ScrapPile>();

    private void OnEnable() => _pile.RemainingChanged += Apply;

    private void OnDisable() => _pile.RemainingChanged -= Apply;

    private void Start() => Apply(_pile.Remaining01);

    private void Apply(float remaining01)
    {
        int index = ResolveStage(remaining01);
        if (index == _activeIndex)
            return;

        _activeIndex = index;

        for (int i = 0; i < _stages.Length; i++)
        {
            GameObject visual = _stages[i].Visual;
            if (visual != null)
                visual.SetActive(i == index);
        }
    }

    // 잔량을 만족하는 구간 중 문턱이 가장 높은 것
    private int ResolveStage(float remaining01)
    {
        int best = -1;
        float bestThreshold = -1f;

        for (int i = 0; i < _stages.Length; i++)
        {
            float threshold = _stages[i].MinRemaining01;
            if (remaining01 < threshold || threshold <= bestThreshold)
                continue;

            best = i;
            bestThreshold = threshold;
        }

        return best;
    }
}
