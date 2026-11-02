using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 잔해 목록을 카드로 만들어 스크롤 뷰에 넣는다
public class PushWreckData : MonoBehaviour
{
    [SerializeField] private WreckCard _restoreCardOrigin;   // 복구 가능 등급용 원본 (꺼둔 상태)
    [SerializeField] private WreckCard _commonCardOrigin;    // 일반 등급용 원본 (꺼둔 상태)
    [SerializeField] private Transform _content;
    [SerializeField] private TextMeshProUGUI _description;

    private List<WreckCard> _cards = new List<WreckCard>();

    public void Refresh()
    {
        foreach (var card in _cards)
        {
            Destroy(card.gameObject);
        }
        _cards.Clear();

        int restorable = 0;
        int lacking = 0;
        int impossible = 0;

        foreach (string id in InventoryHost.Instance.Save.Wrecks)
        {
            PartsDefinition part = InventoryHost.Instance.Catalog.Find(id);
            if (part == null) continue;

            if (!RepairRules.CanRestore(part.Grade)) impossible++;
            else if (FixWindowManager.Instance.CanRestore(part)) restorable++;
            else lacking++;

            CreateCard(part);
        }

        if (_cards.Count > 0)
            _description.text = $"복구 가능 {restorable} · 재료 부족 {lacking} · 복구 불가 {impossible} — 복구 시 {RepairRules.RestoreHours}시간, 내구도 100%";
        else
            _description.text = "파괴된 파츠가 없습니다";
    }

    private void CreateCard(PartsDefinition part)
    {
        WreckCard origin = RepairRules.CanRestore(part.Grade) ? _restoreCardOrigin : _commonCardOrigin;
        WreckCard card = Instantiate(origin, _content);

        card.gameObject.SetActive(true);
        card.SetData(part);
        _cards.Add(card);
    }
}
