using System.Collections.Generic;

// 가중치 기반 등급 뽑기
public static class GradeRoller
{
    // 합계가 0이면 뽑지 않고 false. 난수도 소비하지 않는다
    public static bool TryRoll(IReadOnlyList<GradeWeight> weights, Rng rng, out PartGrade grade)
    {
        grade = PartGrade.Common;

        if (weights == null || rng == null)
            return false;

        int total = GradeWeightUtil.TotalWeight(weights);
        if (total <= 0)
            return false;

        int pick = rng.Range(0, total);
        for (int i = 0; i < weights.Count; i++)
        {
            int weight = weights[i].Weight;
            if (weight <= 0)
                continue;

            pick -= weight;
            if (pick < 0)
            {
                grade = weights[i].Grade;
                return true;
            }
        }

        // 부동소수 없이 정수만 쓰므로 여기까지 오지 않는다
        return false;
    }
}
