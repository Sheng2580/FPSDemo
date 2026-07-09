using System;

[Serializable]
public class InventoryItemSlot
{
    public int itemId;
    public int count;

    public bool IsValid => itemId > 0 && count > 0;

    public InventoryItemSlot()
    {
    }

    public InventoryItemSlot(int itemId, int count)
    {
        this.itemId = itemId;
        this.count = count;
    }
}
