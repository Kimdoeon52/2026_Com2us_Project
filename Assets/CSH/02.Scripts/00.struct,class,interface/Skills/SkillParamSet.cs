using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 생성후 값 변경이 없는 스킬 파라미터 집합
/// 스킬 구현시 SO를 직접 참조하지 않고 이 클래스를 통해서만 접근하도록
/// Get/Has 메서드만 제공하고, 내부 배열은 private로 숨김(변경 가능)
/// </summary>

public class SkillParamSet
{
    public static readonly SkillParamSet Empty = new SkillParamSet(null, null);

    private static int Count = ComputeCount();
    private float[] values = new float[Count];
    private bool[] has = new bool[Count];
    public float Get(SkillParamId id) => values[(int)id];
    public bool Has(SkillParamId id) => has[(int)id];
    public SkillParamSet(SkillParamEntry[] baseParams, IEnumerable<SkillParamOverride> overrides)
    {
        if (baseParams != null)
        {
            foreach (var p in baseParams)
            {
                int i = (int)p.id;
                if (has[i])
                    Debug.LogWarning($"[SkillParamSet] 기본값에 {p.id}가 중복 등록됨. 뒷값이 앞을 덮어씀");
                values[i] = p.value;
                has[i] = true;
            }
        }
        // Add -> Multiply -> Set 일단 이렇게 진행중
        Apply(overrides, ParamOption.Add);
        Apply(overrides, ParamOption.Multiply);
        Apply(overrides, ParamOption.Set);
    }

    private void Apply(IEnumerable<SkillParamOverride> list, ParamOption op)
    {
        if (list == null) return;
        foreach (var o in list)
        {
            if (o.op != op) continue;

            int i = (int)o.id;
            if (!has[i])
            {
                Debug.LogWarning($"[SkillParamSet] 스킬이 쓰지 않는 파라미터 {o.id}에 보정({o.op})이 들어와 무시함");
                continue;
            }

            switch (op)
            {
                case ParamOption.Add: values[i] += o.value; break;
                case ParamOption.Multiply: values[i] *= o.value; break;
                default: values[i] = o.value; break;
            }
        }
    }

    private static int ComputeCount()
    {
        int max = 0;
        foreach (SkillParamId id in Enum.GetValues(typeof(SkillParamId)))
            max = Mathf.Max(max, (int)id);
        return max + 1;
    }

}