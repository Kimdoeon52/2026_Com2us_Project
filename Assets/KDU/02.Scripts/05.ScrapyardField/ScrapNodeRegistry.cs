using System.Collections.Generic;
using UnityEngine;

// 활성 노드 목록. 씬 스캔이나 좌표 가정 없이 노드가 스스로 등록한다
public static class ScrapNodeRegistry
{
    private static readonly List<ScrapNode> _nodes = new List<ScrapNode>();

    public static IReadOnlyList<ScrapNode> Nodes => _nodes;

    public static void Register(ScrapNode node)
    {
        if (node == null || _nodes.Contains(node))
            return;

        _nodes.Add(node);
    }

    public static void Unregister(ScrapNode node)
    {
        if (node == null)
            return;

        _nodes.Remove(node);
    }

    // 도메인 리로드를 끈 상태에서도 플레이 진입 시 목록이 비어 있어야 한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _nodes.Clear();
}
