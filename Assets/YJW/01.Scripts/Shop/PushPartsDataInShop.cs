using System.Collections.Generic;
using UnityEngine;

public class PushPartsDataInShop : MonoBehaviour
{
    [SerializeField] private ShopSlot[] slots;

    private List<ShopSlot> _slotList = new List<ShopSlot>();

    private bool SortPartGrade(PartsDefinition part, PartGrade grade)
    {
        if (part.Grade == grade) return true;
        return false;
    }

    public void ClickGradeButton(int n)
    {
        PushPartDatas(n);
    }

    private void PushPartDatas(int n)
    {
        PartGrade _grade;
        if (n == 1) _grade = PartGrade.Common;
        else if (n == 2) _grade = PartGrade.Rare;
        else _grade = PartGrade.Epic;

        PartsCatalog catalog = CraftingManager.Instance.catalog;
        int count = 0;
        foreach(var part in catalog.Parts)
        {
            if (SortPartGrade(part, _grade) == true)
            {
                ShopSlot slot = GetSlot(count);
                slot.gameObject.SetActive(true);
                slot.SetData(part.DisplayName, part.Grade, part.Sprite, part.Cost, part);
                count++;
            }
            else continue;
        }

        // 이전 등급에서 쓰고 남은 칸은 숨긴다
        for (int i = count; i < _slotList.Count; i++)
        {
            _slotList[i].gameObject.SetActive(false);
        }
    }

    // 파츠가 칸보다 많으면 첫 칸을 복제해서 늘린다
    private ShopSlot GetSlot(int index)
    {
        if (_slotList.Count == 0) _slotList.AddRange(slots);

        while (index >= _slotList.Count)
        {
            ShopSlot clone = Instantiate(slots[0], slots[0].transform.parent);

            // 켜짐 효과 도중에 복제되면 투명한 채로 남으므로 되돌린다
            CanvasGroup group = clone.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;

            _slotList.Add(clone);
        }
        return _slotList[index];
    }
}
