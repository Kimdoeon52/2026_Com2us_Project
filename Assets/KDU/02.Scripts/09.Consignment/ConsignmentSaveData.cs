using System;
using System.Collections.Generic;
using UnityEngine;

// 위탁한 파츠 1개와 고정된 낙찰가
[Serializable]
public struct ConsignedPart
{
    public string PartId;
    public int Price;

    public ConsignedPart(string partId, int price)
    {
        PartId = partId;
        Price = price;
    }
}

// 위탁 슬롯과 이번 주 고정 가격. 세이브 대상
[Serializable]
public class ConsignmentSaveData
{
    [SerializeField] private List<ConsignedPart> _slots = new List<ConsignedPart>();
    [SerializeField] private List<ConsignedPart> _lockedPrices = new List<ConsignedPart>();

    public IReadOnlyList<ConsignedPart> Slots => _slots;

    public void Add(ConsignedPart part)
    {
        _slots.Add(part);
    }

    public void Insert(int index, ConsignedPart part)
    {
        _slots.Insert(Mathf.Clamp(index, 0, _slots.Count), part);
    }

    public void RemoveAt(int index)
    {
        if (index >= 0 && index < _slots.Count)
            _slots.RemoveAt(index);
    }

    public int TotalPrice()
    {
        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            total += _slots[i].Price;
        }

        return total;
    }

    public bool TryGetLockedPrice(string partId, out int price)
    {
        for (int i = 0; i < _lockedPrices.Count; i++)
        {
            if (_lockedPrices[i].PartId == partId)
            {
                price = _lockedPrices[i].Price;
                return true;
            }
        }

        price = 0;
        return false;
    }

    public void LockPrice(string partId, int price)
    {
        if (!TryGetLockedPrice(partId, out _))
            _lockedPrices.Add(new ConsignedPart(partId, price));
    }

    // 주간 경매 정산 후 호출. 슬롯을 비우고 고정 가격을 푼다
    public List<ConsignedPart> TakeAllForAuction()
    {
        var taken = new List<ConsignedPart>(_slots);
        _slots.Clear();
        _lockedPrices.Clear();
        return taken;
    }
}
