using UnityEngine;

/// <summary>인스펙터에 표시하되 수정은 막는다.</summary>
public class ReadOnlyAttribute : PropertyAttribute { }

/// <summary>Vector2를 min/max 슬라이더 한 줄로 표시한다. x=min, y=max.</summary>
public class MinMaxAttribute : PropertyAttribute
{
    public readonly float Min;
    public readonly float Max;

    public MinMaxAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
}