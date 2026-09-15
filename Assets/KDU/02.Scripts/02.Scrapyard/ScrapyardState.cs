using System;
using System.Collections.Generic;
using UnityEngine;

// 지역 하나의 런타임 잔량. 세이브 대상이라 SO가 아니다
[Serializable]
public class ScrapyardState
{
    public int RegionIndex;
    public int Remaining;
    public int LastRefillMonth;
    public bool Initialized;

    // 월이 바뀌었으면 총량으로 되돌린다
    public void RefillIfMonthChanged(int month, int monthlyTotal)
    {
        if (Initialized && LastRefillMonth == month)
            return;

        Remaining = monthlyTotal > 0 ? monthlyTotal : 0;
        LastRefillMonth = month;
        Initialized = true;
    }

    // 총량 0은 무제한
    public bool HasRemaining(int monthlyTotal)
    {
        if (monthlyTotal <= 0)
            return true;

        return Remaining > 0;
    }

    // 실제로 차감된 양을 반환한다
    public int Consume(int amount, int monthlyTotal)
    {
        if (amount <= 0)
            return 0;

        if (monthlyTotal <= 0)
            return amount;

        int consumed = Mathf.Min(amount, Remaining);
        Remaining -= consumed;
        return consumed;
    }

    // 잔량 시각 표현용. 숫자 UI로 쓰지 않는다
    public float RemainingRatio(int monthlyTotal)
    {
        if (monthlyTotal <= 0)
            return 1f;

        return Mathf.Clamp01((float)Remaining / monthlyTotal);
    }
}

// 지역별 상태 묶음. 세이브 루트가 이걸 들고 있는다
[Serializable]
public class ScrapyardStateSet
{
    [SerializeField] private List<ScrapyardState> _states = new List<ScrapyardState>();

    public IReadOnlyList<ScrapyardState> States => _states;

    public ScrapyardState GetOrCreate(int regionIndex)
    {
        for (int i = 0; i < _states.Count; i++)
        {
            if (_states[i].RegionIndex == regionIndex)
                return _states[i];
        }

        var state = new ScrapyardState { RegionIndex = regionIndex };
        _states.Add(state);
        return state;
    }

    // 모든 지역의 월간 리필 판정
    public void RefillAll(ScrapyardDatabase database, int month)
    {
        if (database == null || database.Regions == null)
            return;

        for (int i = 0; i < database.Regions.Length; i++)
        {
            var definition = database.Regions[i];
            if (definition == null)
                continue;

            GetOrCreate(definition.RegionIndex).RefillIfMonthChanged(month, definition.MonthlyTotal);
        }
    }
}
