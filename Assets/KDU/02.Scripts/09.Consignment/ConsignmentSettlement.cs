using System.Collections.Generic;

// 주간 경매에서 위탁품을 고정 낙찰가로 판매하고 골드를 지급한다
public static class ConsignmentSettlement
{
    // 판매된 위탁품 목록. 골드를 줄 수 없으면 위탁을 그대로 둔다
    public static List<ConsignedPart> Settle()
    {
        InventoryHost host = InventoryHost.Instance;
        if (host == null || host.Save.Consignment.Slots.Count == 0 || !GlobalGoldWallet.IsAvailable)
            return new List<ConsignedPart>();

        List<ConsignedPart> sold = host.Save.Consignment.TakeAllForAuction();

        int total = 0;
        for (int i = 0; i < sold.Count; i++)
        {
            total += sold[i].Price;
        }

        GlobalGoldWallet.AddPlayerGold(total);
        host.NotifyChanged();
        return sold;
    }

    public static string NameOf(ConsignedPart part)
    {
        InventoryHost host = InventoryHost.Instance;
        PartsDefinition def = host != null && host.Catalog != null ? host.Catalog.Find(part.PartId) : null;
        return def != null ? def.DisplayName : part.PartId;
    }
}
