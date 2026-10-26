using RealSteel.Terrain;
using UnityEngine;

// 잔량에 따라 더미(RSHeap)의 반경·높이를 줄인다. 숫자 UI는 쓰지 않는다.
// RS 지형 의존은 이 파일 하나에 가둬둔다
[RequireComponent(typeof(ScrapPile))]
public class ScrapHeapVisual : MonoBehaviour
{
    [Tooltip("줄일 더미. 비우면 같은 오브젝트에서 찾는다")]
    [SerializeField] private RSHeap _heap;

    [Tooltip("잔량 0일 때 반경 배율")]
    [Range(0f, 1f)]
    [SerializeField] private float _depletedRadiusScale = 0.5f;

    [Tooltip("잔량 0일 때 높이 배율")]
    [Range(0f, 1f)]
    [SerializeField] private float _depletedHeightScale = 0.15f;

    [Tooltip("가로축 잔량, 세로축 크기 비율")]
    [SerializeField] private AnimationCurve _shrinkCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("현재 크기")]
    [ReadOnly]
    [SerializeField] private float _radius;

    [ReadOnly]
    [SerializeField] private float _height;

    private ScrapPile _pile;
    private float _fullRadius;
    private float _fullHeight;

    private void Awake()
    {
        _pile = GetComponent<ScrapPile>();

        if (_heap == null)
            _heap = GetComponent<RSHeap>();

        if (_heap == null)
        {
            Debug.LogWarning($"{name}: 줄일 RSHeap이 없다.", this);
            return;
        }

        // 인스펙터에 적어둔 값이 만량 기준이다
        _fullRadius = _heap.radius;
        _fullHeight = _heap.height;
    }

    private void OnEnable() => _pile.RemainingChanged += Apply;

    private void OnDisable() => _pile.RemainingChanged -= Apply;

    private void Start() => Apply(_pile.Remaining01);

    private void Apply(float remaining01)
    {
        if (_heap == null)
            return;

        float t = Mathf.Clamp01(_shrinkCurve.Evaluate(Mathf.Clamp01(remaining01)));
        _radius = Mathf.Lerp(_fullRadius * _depletedRadiusScale, _fullRadius, t);
        _height = Mathf.Lerp(_fullHeight * _depletedHeightScale, _fullHeight, t);

        // 만량 그대로면 시작할 때 지형을 두 번 만들지 않는다
        if (Mathf.Approximately(_heap.radius, _radius) && Mathf.Approximately(_heap.height, _height))
            return;

        _heap.radius = _radius;
        _heap.height = _height;

        // 재생성 예약. 같은 프레임의 더미 여러 개는 한 번으로 합쳐진다
        _heap.Notify();
    }
}
