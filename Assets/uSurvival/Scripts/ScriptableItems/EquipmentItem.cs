using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName="uSurvival Item/Equipment", order=999)]
public class EquipmentItem : UsableItem
{
    [Header("Equipment")]
    public int healthBonus;
    public int hydrationBonus;
    public int nutritionBonus;
    public int damageBonus;
    public int defenseBonus;

    // usage
    // -> can we equip this into any slot?
    public override Usability CanUse(PlayerInventory inventory, int inventoryIndex)
    {
        // check base usability first (cooldown etc.)
        Usability baseUsable = base.CanUse(inventory, inventoryIndex);
        if (baseUsable != Usability.Usable)
            return baseUsable;

        return FindEquipableSlotFor(inventory.GetComponent<PlayerEquipment>(), inventoryIndex) != -1
               ? Usability.Usable
               : Usability.Never;
    }
    public override Usability CanUse(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt) { return Usability.Never; }

    // can we equip this item into this specific equipment slot?
    public bool CanEquip(PlayerEquipment equipment, int inventoryIndex, int equipmentIndex)
    {
        string requiredCategory = equipment.slotInfo[equipmentIndex].requiredCategory;
        return requiredCategory != "" &&
               category.StartsWith(requiredCategory);
    }

    int FindEquipableSlotFor(PlayerEquipment equipment, int inventoryIndex)
    {
        for (int i = 0; i < equipment.slots.Count; ++i)
            if (CanEquip(equipment, inventoryIndex, i))
                return i;
        return -1;
    }

    public override void Use(PlayerInventory inventory, int inventoryIndex)
    {
        // find a slot that accepts this category, then equip it
        PlayerEquipment equipment = inventory.GetComponent<PlayerEquipment>();
        int slot = FindEquipableSlotFor(equipment, inventoryIndex);
        if (slot != -1)
        {
            // merge? e.g. ammo stack on ammo stack
            if (inventory.slots[inventoryIndex].amount > 0 && equipment.slots[slot].amount > 0 &&
                inventory.slots[inventoryIndex].item.Equals(equipment.slots[slot].item))
            {
                ItemSlot slotFrom = inventory.slots[inventoryIndex];
                ItemSlot slotTo = equipment.slots[slot];

                // merge from -> to
                // put as many as possible into 'To' slot
                int put = slotTo.IncreaseAmount(slotFrom.amount);
                slotFrom.DecreaseAmount(put);

                // put back into the lists
                equipment.slots[slot] = slotTo;
                inventory.slots[inventoryIndex] = slotFrom;
            }
            // swap?
            else
            {
                equipment.SwapInventoryEquip(inventoryIndex, slot);
            }
        }
    }
    public override void Use(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt) {}

    // tooltip
    public override string ToolTip()
    {
        StringBuilder tip = new StringBuilder(base.ToolTip());
        tip.Replace("{CATEGORY}", category);
        tip.Replace("{HEALTHBONUS}", healthBonus.ToString());
        tip.Replace("{HYDRATIONBONUS}", hydrationBonus.ToString());
        tip.Replace("{NUTRITIONBONUS}", nutritionBonus.ToString());
        tip.Replace("{DAMAGEBONUS}", damageBonus.ToString());
        tip.Replace("{DEFENSEBONUS}", defenseBonus.ToString());
        return tip.ToString();
    }
}
