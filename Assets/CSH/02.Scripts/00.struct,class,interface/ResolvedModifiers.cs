
using System;

/// <summary>
/// 한 행동에 대해 합산이 끝난 장비 효과. ActionState.Begin()에서 한 번 계산되어 그 행동이 끝날 때까지 유지된다.
/// </summary>


public sealed class ResolvedModifiers
{

    private static readonly int Count = Enum.GetValues(typeof(SkillParamId)).Length;

    private readonly float[] add = new float[Count];
    private readonly float[] mul = new float[Count];
    private readonly float[] set = new float[Count];
    private readonly bool[] hasSet = new bool[Count];


    public static readonly ResolvedModifiers Identity = new ResolvedModifiers();

    public ResolvedModifiers() => Reset();

    /// 행동 시작 때마다 호출. 새로 할당하지 않고 값만 되돌린다 (GC 방지)
    public void Reset()
    {
        Array.Clear(set, 0, set.Length);
        Array.Clear(add, 0, add.Length);
        Array.Clear(hasSet, 0, hasSet.Length);
        for (int i = 0; i < mul.Length; i++) mul[i] = 1f;
    }

    public void Add(SkillParamId id, float value) => add[(int)id] += value;
    public void Multiply(SkillParamId id, float value) => mul[(int)id] *= value;

    /// <summary>여러 효과가 같은 id를 Set하면 가장 큰 값만 남는다</summary>
    public void Set(SkillParamId id, float value)
    {
        int i = (int)id;
        if (!hasSet[i] || value > set[i])
        {
            set[i] = value;
            hasSet[i] = true;
        }
    }

    public bool Has(SkillParamId id)
    {
        int i = (int)id;
        return hasSet[i] || add[i] != 0f || mul[i] != 1f;
    }

    public float Apply(SkillParamId id, float baseValue)
    {
        int i = (int)id;
        if (hasSet[i]) return set[i];
        return (baseValue + add[i]) * mul[i];
    }

    private static int ComputeCount()
    {
        int max = 0;
        foreach (SkillParamId id in Enum.GetValues(typeof(SkillParamId)))
            max = Math.Max(max, (int)id);
        return max + 1;
    }
}


/*
public struct ResolvedModifiers
{
    public float HitBoxWidthAdd;
    public float HitBoxHeightAdd;
    public float KnockbackMultiplier;

    // default(ResolvedModifiers)는 KnockbackMultiplier가 0이 되어 넉백이 사라지므로,
    // "보정 없음" 상태가 필요할 땐 반드시 이 Identity를 쓴다
    public static ResolvedModifiers Identity => new ResolvedModifiers { KnockbackMultiplier = 1f };
}
*/