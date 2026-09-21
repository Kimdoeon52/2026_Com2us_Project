using System;
using System.Collections.Generic;
using UnityEngine;

// 추적 대상 기준 최근접 활성 노드 하나를 고르고 강도를 방송한다.
// 플레이어에 붙이지 않는다. 플레이어에게서 받는 건 Transform 하나뿐이다
public class ScrapNodeDetector : MonoBehaviour
{
    [Tooltip("추적할 대상. 플레이어를 교체하면 여기만 다시 꽂는다")]
    [SerializeField] private Transform _trackedTarget;

    [Tooltip("탐지 갱신 간격(초). 0이면 매 프레임")]
    [Min(0f)]
    [SerializeField] private float _tickInterval = 0.05f;

    [Header("현재 상태")]
    [ReadOnly]
    [SerializeField] private float _currentIntensity;

    private ScrapDetectionState _state;
    private float _tickTimer;

    public ScrapDetectionState State => _state;

    public Transform TrackedTarget
    {
        get => _trackedTarget;
        set => _trackedTarget = value;
    }

    // 매 틱 방송. 연출·사운드·상호작용이 각자 구독한다
    public event Action<ScrapDetectionState> StateUpdated;

    private void Update()
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0f)
            return;

        _tickTimer = _tickInterval;
        Evaluate();
    }

    private void Evaluate()
    {
        ScrapDetectionState next = default;

        if (_trackedTarget != null)
        {
            Vector3 origin = _trackedTarget.position;
            IReadOnlyList<ScrapNode> nodes = ScrapNodeRegistry.Nodes;

            ScrapNode best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                ScrapNode node = nodes[i];
                if (node == null || node.Consumed)
                    continue;

                // 직선 거리
                float distance = Vector3.Distance(origin, node.transform.position);
                if (!node.IsWithinDetect(distance) || distance >= bestDistance)
                    continue;

                best = node;
                bestDistance = distance;
            }

            if (best != null)
            {
                next.Node = best;
                next.Distance = bestDistance;
                next.Intensity01 = best.EvaluateIntensity(bestDistance);
                next.InInteractRange = best.IsWithinInteract(bestDistance);
            }
        }

        _state = next;
        _currentIntensity = next.Intensity01;
        StateUpdated?.Invoke(next);
    }
}
