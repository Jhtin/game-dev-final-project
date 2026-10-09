using UnityEngine;

namespace HorrorEscape.Inventory
{
    public enum ItemType
    {
        Flashlight,
        Battery,
        Pistol,
        PistolAmmo,
        SMG,
        SMGAmmo,
        FirstAid,
        AlmondWater,
        SanityPills,
        Keycard,
        Note
    }

    /// <summary>
    /// Represents an inventory item entry with icon, count, equip eligibility, and status.
    /// </summary>
    [System.Serializable]
    public class InventoryItem
    {
        public ItemType itemType;
        public string itemName;
        public string description;
        public Sprite icon;
        public int quantity;
        public bool isEquippable;
        public bool isConsumable;
        public string statusText; // e.g. "100%", "6/6", "EQUIPPED"

        public InventoryItem(ItemType type, string name, string desc, Sprite itemIcon, int qty = 1, bool equippable = false, bool consumable = false)
        {
            itemType = type;
            itemName = name;
            description = desc;
            icon = itemIcon;
            quantity = qty;
            isEquippable = equippable;
            isConsumable = consumable;
            statusText = "";
        }
    }
}
