using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부채꼴 Hit박스 생성 설정 (런타임 공용 형태). BoostShotSettings / LaserShotData.FanSettings가 이걸로 바뀌어 FanBoxBuilder에 넘어간다.
/// </summary>
public struct FanShape
{
    public int rays;            // 광선(산탄) 수
    public float spreadDegrees; // 전체 각도. 정면 기준 좌우로 절반씩
    public float range;         // 사거리 (총구에서 광선 끝까지)
    public float thickness;     // 광선 두께
    public int steps;           // 거리 감쇠 단계 수 (1이면 감쇠 없음)
    public float farScale;      // 사거리 끝에서의 피해 배율 (steps가 2 이상일 때만 의미)
    public int hitLength;       // 박스가 켜져 있는 프레임 수. 0 = 활성 구간 전체
}

/// <summary>
/// 총구를 원점으로 광선 N개를 얇은 회전 Hit박스로 만드는 공용 함수.
/// 부스터 샷(BoostShotActionData), 레이저 샷(LaserShotData)이 모두 이걸 거친다.
///
/// 거리 감쇠는 박스를 겹쳐 쌓아서 계단식으로 표현한다: 사거리 전체를 덮는 박스(farScale)와
/// 더 짧은 박스들(나머지 배율을 균등 분배)을 겹쳐 두면, 가까이 있는 Hurt박스는 겹친 박스의 배율이 합산되어 1.0,
/// 멀리 있으면 farScale만 합산된다. 한 광선을 거리 구간으로 쪼개 따로 세면 두 구간에 걸친 Hurt박스가
/// 한 발을 두 번 맞은 것이 되어버려서, 쪼개지 않고 겹치는 방식을 쓴다.
/// </summary>
public static class FanBoxBuilder
{
    /// <summary>저장된 박스 중 Hurt/Push는 유지하고 Hit는 부채꼴로 대신한 배열을 만든다</summary>
    public static FrameBox[] BuildWithStored(IEnumerable<FrameBox> stored, in FanShape fan, Vector2 muzzle, int totalFrames)
    {
        var list = new List<FrameBox>();
        if (stored != null)
        {
            foreach (var box in stored)
                if (box.type != BoxType.Hit) list.Add(box);
        }
        AppendHitBoxes(list, fan, muzzle, totalFrames);
        return list.ToArray();
    }

    /// <summary>부채꼴 Hit박스를 list 뒤에 붙인다</summary>
    public static void AppendHitBoxes(List<FrameBox> list, in FanShape fan, Vector2 muzzle, int totalFrames)
    {
        int rays = Mathf.Max(1, fan.rays);
        int steps = Mathf.Max(1, fan.steps);

        for (int p = 0; p < rays; p++)
        {
            // 1발이면 정면, 아니면 -spread/2 ~ +spread/2 로 균등 분할
            float t = rays == 1 ? 0f : (float)p / (rays - 1) - 0.5f;
            float angle = t * fan.spreadDegrees;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            for (int step = 0; step < steps; step++)
            {
                // step 0: 사거리 전체(배율 farScale), 이후: 점점 짧은 박스(나머지 배율을 균등 분배). 겹치면 합이 1.0
                float length = fan.range * (steps - step) / steps;
                float scale = steps == 1 ? 1f : (step == 0 ? fan.farScale : (1f - fan.farScale) / (steps - 1));

                // 모든 박스가 총구에서 시작하도록 중심을 광선 방향으로 length/2 만큼 민다
                Vector2 center = muzzle + dir * (length * 0.5f);
                list.Add(new FrameBox
                {
                    type = BoxType.Hit,
                    bodyPart = BodyPart.Core,
                    rect = new Rect(center.x - length * 0.5f, center.y - fan.thickness * 0.5f, length, fan.thickness),
                    rotation = angle,
                    damageScale = scale,
                    anchor = FrameAnchor.Active,
                    startOffset = 0,
                    length = Mathf.Max(0, fan.hitLength),
                    startFrame = 1,                      // Legacy 앵커에서만 읽히는 값
                    endFrame = Mathf.Max(1, totalFrames)
                });
            }
        }
    }
}
