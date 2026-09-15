using System;
using System.Collections.Generic;
using UnityEngine;

// 등급 하나의 정수 가중치. 합계 100을 강제하지 않는다
[Serializable]
public struct GradeWeight
{
    [Tooltip("등급")]
    public PartGrade Grade;

    [Tooltip("가중치")]
    [Min(0)]
    public int Weight;

    [Tooltip("실제 확률")]
    [ReadOnly]
    public float Percent;
}

public static class GradeWeightUtil
{
    // 음수와 빈 배열을 무시한 가중치 합
    public static int TotalWeight(IReadOnlyList<GradeWeight> weights)
    {
        if (weights == null)
            return 0;

        int total = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i].Weight > 0)
                total += weights[i].Weight;
        }

        return total;
    }

    // 인스펙터 표시용 확률 갱신. OnValidate에서 호출한다
    public static void RecalculatePercents(GradeWeight[] weights)
    {
        if (weights == null)
            return;

        int total = TotalWeight(weights);
        for (int i = 0; i < weights.Length; i++)
        {
            int weight = weights[i].Weight > 0 ? weights[i].Weight : 0;
            weights[i].Percent = total > 0 ? weight * 100f / total : 0f;
        }
    }
}
