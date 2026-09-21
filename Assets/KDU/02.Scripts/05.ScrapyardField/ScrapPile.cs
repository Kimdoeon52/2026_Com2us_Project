using System;
using UnityEngine;

// 노드 여러 개를 묶는 고물 더미. 자식 노드를 자동 수집한다
[DisallowMultipleComponent]
public class ScrapPile : MonoBehaviour
{
    [Tooltip("이 더미가 속한 지역 정의. 확률과 보상은 여기서 온다")]
    [SerializeField] private ScrapyardDefinition _definition;

    [Header("잔량")]
    [ReadOnly]
    [SerializeField] private int _totalNodes;

    [ReadOnly]
    [SerializeField] private int _consumedNodes;

    public ScrapyardDefinition Definition => _definition;
    public int TotalNodes => _totalNodes;

    // 0~1. 비주얼 교체 기준값
    public float Remaining01 => _totalNodes <= 0 ? 0f : 1f - ((float)_consumedNodes / _totalNodes);

    public event Action<float> RemainingChanged;

    private void Awake()
    {
        ScrapNode[] nodes = GetComponentsInChildren<ScrapNode>(true);
        _totalNodes = nodes.Length;
        _consumedNodes = 0;

        for (int i = 0; i < nodes.Length; i++)
            nodes[i].BindPile(this);
    }

    // 수집 1회당 잔량이 줄고 노드는 비활성화된다
    public void ConsumeNode(ScrapNode node)
    {
        if (node == null || node.Pile != this || node.Consumed)
            return;

        node.Consume();
        _consumedNodes++;
        RemainingChanged?.Invoke(Remaining01);
    }
}
