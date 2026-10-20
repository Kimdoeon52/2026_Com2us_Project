
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
/// <summary>
/// 총구(ActionData.projectileMuzzle)를 원점으로 광선 N개를 얇은 회전 Hit박스로 만든다 (2026-10-15).
/// ActionDataEditor의 "부채꼴 생성" 버튼과 에셋 생성 도구가 둘 다 이 함수를 거치므로 만들어지는 박스가 항상 같다.
///
/// 거리 감쇠는 박스를 겹쳐 쌓아서 계단식으로 표현한다: 사거리 전체를 덮는 박스(farScale)와
/// 더 짧은 박스들(나머지 배율을 균등 분배)을 겹쳐 두면, 가까이 있는 Hurt박스는 겹친 박스의 배율이 합산되어 1.0,
/// 멀리 있으면 farScale만 합산된다. 한 광선(발)을 거리 구간으로 쪼개 따로 세면 두 구간에 걸친 Hurt박스가
/// 한 발을 두 번 맞은 것이 되어버려서, 쪼개지 않고 겹치는 방식을 쓴다.
/// </summary>
public static class FanShotBoxGenerator
{
    /// <summary>
    /// so의 frameBoxes에서 기존 Hit박스를 모두 지우고 새로 만든다 (Hurt/Push는 유지).
    /// perBoxDamage도 켠다. 호출하는 쪽이 so.Update()/ApplyModifiedProperties()를 맡는다. 반환: 만든 박스 수
    /// </summary>
    public static int Generate(SerializedObject so, BoostShotSettings s, int totalFrames)
    {
        SerializedProperty boxesProp = so.FindProperty("frameBoxes");
        Vector2 muzzle = so.FindProperty("projectileMuzzle").vector2Value;

        // 뒤에서부터 지워야 인덱스가 안 밀린다
        for (int i = boxesProp.arraySize - 1; i >= 0; i--)
        {
            if (boxesProp.GetArrayElementAtIndex(i).FindPropertyRelative("type").enumValueIndex == (int)BoxType.Hit)
                boxesProp.DeleteArrayElementAtIndex(i);
        }

        int pellets = Mathf.Max(1, s.pellets);
        int steps = Mathf.Max(1, s.steps);
        int created = 0;

        for (int p = 0; p < pellets; p++)
        {
            // 산탄이 1발이면 정면, 아니면 -spread/2 ~ +spread/2 로 균등 분할
            float t = pellets == 1 ? 0f : (float)p / (pellets - 1) - 0.5f;
            float angle = t * s.spreadDegrees;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            for (int step = 0; step < steps; step++)
            {
                // step=0: 사거리 전체(배율 farScale), step>=1: 점점 짧아지는 박스(나머지 배율을 균등 분배). 합이 1.0
                float length = s.range * (steps - step) / steps;
                float scale = steps == 1 ? 1f : (step == 0 ? s.farScale : (1f - s.farScale) / (steps - 1));

                // 모든 박스가 총구에서 시작하도록 중심을 광선 방향으로 length/2 만큼 민다
                Vector2 center = muzzle + dir * (length * 0.5f);
                var rect = new Rect(center.x - length * 0.5f, center.y - s.thickness * 0.5f, length, s.thickness);
                AppendHitBox(boxesProp, rect, angle, scale, s.hitLength, totalFrames);
                created++;
            }
        }

        so.FindProperty("perBoxDamage").boolValue = true;
        return created;
    }

    private static void AppendHitBox(SerializedProperty boxesProp, Rect rect, float angle, float scale, int hitLength, int totalFrames)
    {
        int index = boxesProp.arraySize;
        boxesProp.arraySize++;
        SerializedProperty box = boxesProp.GetArrayElementAtIndex(index);

        box.FindPropertyRelative("type").enumValueIndex = (int)BoxType.Hit;
        box.FindPropertyRelative("bodyPart").enumValueIndex = (int)BodyPart.Core;
        box.FindPropertyRelative("rect").rectValue = rect;
        box.FindPropertyRelative("rotation").floatValue = angle;
        box.FindPropertyRelative("damageScale").floatValue = scale;
        box.FindPropertyRelative("anchor").enumValueIndex = (int)FrameAnchor.Active;
        box.FindPropertyRelative("startOffset").intValue = 0;
        box.FindPropertyRelative("length").intValue = Mathf.Max(0, hitLength);

        // 아래 둘은 Legacy 박스에서만 읽히는 값이라 지금은 안 쓰이지만, 값이 비어 보이지 않게 채워둔다
        box.FindPropertyRelative("startFrame").intValue = 1;
        box.FindPropertyRelative("endFrame").intValue = Mathf.Max(1, totalFrames);
    }
}
#endif