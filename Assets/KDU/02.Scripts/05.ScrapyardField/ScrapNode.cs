using UnityEngine;

// 고물 더미 안의 탐지 지점 하나. 평소엔 보이지 않는다
[DisallowMultipleComponent]
public class ScrapNode : MonoBehaviour
{
    [Tooltip("이 반경 안에 들어오면 파동이 반응한다")]
    [Min(0f)]
    [SerializeField] private float _detectRadius = 6f;

    [Tooltip("이 반경 안에서 상호작용 입력을 받는다. 철로 근처 노드는 좁게 잡는다")]
    [Min(0f)]
    [SerializeField] private float _interactRadius = 1.2f;

    [Tooltip("가로축 0=탐지 반경 끝, 1=노드 바로 위. 세로축이 강도")]
    [SerializeField] private AnimationCurve _intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private ScrapPile _pile;
    private bool _consumed;

    public ScrapPile Pile => _pile;
    public float DetectRadius => _detectRadius;
    public float InteractRadius => _interactRadius;
    public bool Consumed => _consumed;

    // 더미가 자식을 수집하면서 자신을 알려준다
    public void BindPile(ScrapPile pile) => _pile = pile;

    private void OnEnable() => ScrapNodeRegistry.Register(this);

    private void OnDisable() => ScrapNodeRegistry.Unregister(this);

    // 반경 밖이면 0
    public float EvaluateIntensity(float distance)
    {
        if (_detectRadius <= 0f || distance >= _detectRadius)
            return 0f;

        float t = 1f - (distance / _detectRadius);
        return Mathf.Clamp01(_intensityCurve.Evaluate(t));
    }

    public bool IsWithinDetect(float distance) => distance < _detectRadius;

    public bool IsWithinInteract(float distance) => distance <= _interactRadius;

    public void Consume()
    {
        if (_consumed)
            return;

        _consumed = true;
        gameObject.SetActive(false);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, _detectRadius);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, _interactRadius);
    }
}
